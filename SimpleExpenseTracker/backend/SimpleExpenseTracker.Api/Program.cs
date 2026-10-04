using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SimpleExpenseTracker.Infrastructure;
using SimpleExpenseTracker.Application;
using SimpleExpenseTracker.Api;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFilter("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", LogLevel.None);
builder.Services.AddDbContext<ExpenseDbContext>(o => o.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=expense-tracker.db"));
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<IStatisticsService, StatisticsService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<DatabaseAccess>();
builder.Services.AddSingleton<BackupService>();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.Configure<ApiBehaviorOptions>(o => o.InvalidModelStateResponseFactory = context =>
{
    context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("ApiValidation")
        .LogWarning("API validation returned 400; trace {Trace}", context.HttpContext.TraceIdentifier);
    return new BadRequestObjectResult(new ProblemDetails { Status = 400, Title = "輸入資料格式不正確，請檢查金額、名稱、日期及必填欄位。", Extensions = { ["traceId"] = context.HttpContext.TraceIdentifier } });
});
builder.Services.AddProblemDetails();
var app = builder.Build();
app.UseExceptionHandler();
var backups = app.Services.GetRequiredService<BackupService>();
await backups.Initialize();
app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/api") || context.Request.Path == "/api/health") { await next(context); return; }
    var restoring = context.Request.Path == "/api/backups/restore" && HttpMethods.IsPost(context.Request.Method);
    using var lease = restoring ? null : context.RequestServices.GetRequiredService<DatabaseAccess>().Enter();
    context.Response.Headers["Cache-Control"] = "no-store";
    context.Response.Headers["X-Ledger-Generation"] = backups.Generation;
    if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)) backups.CheckGeneration(context.Request.Headers["X-Ledger-Generation"]);
    await next(context);
});
app.UseDefaultFiles();
app.UseStaticFiles();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ExpenseDbContext>();
    var newDatabase = !(await db.Database.GetAppliedMigrationsAsync()).Any();
    app.Logger.LogInformation("Applying database migrations");
    await db.Database.MigrateAsync();
    if (newDatabase) await SeedData.InitializeAsync(db);
}
await backups.CaptureSchema();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();
app.MapFallback("/api/{**path}", () => Results.Problem(statusCode: 404, title: "找不到此 API。"));
app.MapFallbackToFile("index.html");
app.Logger.LogInformation("Expense tracker started");
app.Run();
public partial class Program { }
