using SimpleExpenseTracker.Application;

namespace SimpleExpenseTracker.Api;

public sealed class RecurringTransactionBackgroundService(IServiceScopeFactory scopes, DatabaseAccess access, IConfiguration config, ILogger<RecurringTransactionBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("Recurring:Enabled", true)) return;
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                using var lease = access.Enter();
                await using var scope = scopes.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<IRecurringTransactionGenerator>().Generate(stoppingToken);
                logger.LogInformation("Recurring check: generated {Generated}, duplicated {Duplicates}, blocked {Blocked}, hasMore {HasMore}", result.Generated, result.Duplicates, result.Blocked, result.HasMore);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (AppException ex) when (ex.Status == 503) { logger.LogInformation("Recurring check deferred during database restore"); }
            catch (Exception ex) { logger.LogError(ex, "Failed to generate recurring transactions; next check in one hour"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
