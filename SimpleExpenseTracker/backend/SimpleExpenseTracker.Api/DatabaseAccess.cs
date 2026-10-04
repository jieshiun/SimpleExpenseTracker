using SimpleExpenseTracker.Application;

namespace SimpleExpenseTracker.Api;

// A single container owns the database. Readers/writers run normally until a restore drains them.
public sealed class DatabaseAccess
{
    private readonly object sync = new();
    private int active;
    private bool maintenance;
    private TaskCompletionSource? drained;

    public IDisposable Enter()
    {
        lock (sync)
        {
            if (maintenance) throw new AppException(503, "資料還原中，請稍候再重新整理。");
            active++;
            return new Lease(this);
        }
    }

    public async Task BeginRestore(CancellationToken ct)
    {
        Task wait;
        lock (sync)
        {
            if (maintenance) throw new AppException(409, "已有資料還原作業進行中。");
            maintenance = true;
            wait = active == 0 ? Task.CompletedTask : (drained = new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
        }
        try { await wait.WaitAsync(ct); }
        catch { EndRestore(); throw; }
    }

    public void EndRestore() { lock (sync) { maintenance = false; drained = null; } }
    private void Exit() { lock (sync) { if (--active == 0) drained?.TrySetResult(); } }
    private sealed class Lease(DatabaseAccess owner) : IDisposable
    {
        public void Dispose() => owner.Exit();
    }
}
