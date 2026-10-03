using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SimpleExpenseTracker.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<ExpenseDbContext>(o => o.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=expense-tracker.db"));
builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
var app = builder.Build();
app.UseExceptionHandler();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ExpenseDbContext>();
    app.Logger.LogInformation("Applying database migrations");
    await db.Database.MigrateAsync();
    await SeedData.InitializeAsync(db);
}
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();
app.Logger.LogInformation("Expense tracker started");
app.Run();
public partial class Program { }
