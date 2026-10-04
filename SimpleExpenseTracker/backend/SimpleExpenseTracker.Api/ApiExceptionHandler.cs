using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SimpleExpenseTracker.Application;

namespace SimpleExpenseTracker.Api;
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is OperationCanceledException && ct.IsCancellationRequested) return false;
        var status = exception is AppException app ? app.Status : 500;
        if (status == 500) logger.LogError("Unhandled API error of type {Type}; trace {Trace}", exception.GetType().Name, context.TraceIdentifier);
        else logger.LogWarning("API returned {Status}; trace {Trace}", status, context.TraceIdentifier);
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = exception is AppException ? exception.Message : "系統暫時無法完成操作，請稍後重試。", Extensions = { ["traceId"] = context.TraceIdentifier } }, ct);
        return true;
    }
}
