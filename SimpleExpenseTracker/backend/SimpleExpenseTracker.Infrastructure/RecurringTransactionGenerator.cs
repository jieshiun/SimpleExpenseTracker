using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SimpleExpenseTracker.Application;
using SimpleExpenseTracker.Domain;

namespace SimpleExpenseTracker.Infrastructure;

public sealed class RecurringTransactionGenerator(ExpenseDbContext db, ILocalDateProvider clock, ILogger<RecurringTransactionGenerator> logger) : IRecurringTransactionGenerator
{
    public const int BatchLimit = 1000;
    internal IQueryable<RecurringTransaction> Rows => db.RecurringTransactions.Include(r => r.Category).Include(r => r.Account).Include(r => r.Member);

    // Caller holds a database write transaction; occurrence and schedule cursor commit together.
    internal async Task<GenerationResult> GenerateRule(RecurringTransaction r, DateOnly today, CancellationToken ct)
    {
        if (!r.IsActive || !r.NextRunDate.HasValue || r.NextRunDate > today) return new(0, 0, 0, false);
        if (RecurringRules.Warning(r) is not null) return new(0, 0, 1, false);
        var generated = 0; var duplicates = 0;
        for (var count = 0; count < BatchLimit && r.NextRunDate.HasValue && r.NextRunDate <= today; count++)
        {
            var date = r.NextRunDate.Value;
            if (r.EndDate.HasValue && date > r.EndDate) { r.NextRunDate = null; break; }
            // Include soft-deleted rows: deleting one occurrence must never recreate it.
            var prior = await db.Transactions.AnyAsync(t => t.RecurringTransactionId == r.Id && t.RecurringOccurrenceDate == date, ct);
            if (prior) { duplicates++; logger.LogInformation("Skip duplicated recurring transaction {RecurringId} {OccurrenceDate}", r.Id, date); }
            else
            {
                var transaction = new Transaction
                {
                    Type = r.Type, Amount = r.Amount, CategoryId = r.CategoryId, AccountId = r.AccountId,
                    Ownership = r.MemberId.HasValue ? OwnershipKind.Personal : OwnershipKind.Shared, OwnerMemberId = r.MemberId,
                    TransactionDate = date.ToDateTime(TimeOnly.MinValue), Note = string.IsNullOrWhiteSpace(r.Note) ? r.Name : $"{r.Name} · {r.Note}",
                    RecurringTransactionId = r.Id, RecurringOccurrenceDate = date, CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow
                };
                // Name + note must fit the existing transaction note limit.
                if (transaction.Note.Length > 500) transaction.Note = transaction.Note[..500];
                db.Transactions.Add(transaction);
                await db.SaveChangesAsync(ct);
                generated++;
                logger.LogInformation("Generated recurring transaction {RecurringId} {OccurrenceDate} {TransactionId} {Amount}", r.Id, date, transaction.Id, transaction.Amount);
            }
            r.LastGeneratedDate = date;
            r.NextRunDate = RecurringRules.Bound(r, RecurringRules.Schedule(r).Next(date));
        }
        r.Version++; r.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return new(generated, duplicates, 0, r.NextRunDate.HasValue && r.NextRunDate <= today);
    }

    public async Task<GenerationResult> Generate(CancellationToken ct)
    {
        var today = clock.Today;
        var ids = await db.RecurringTransactions.AsNoTracking().Where(r => r.IsActive && r.NextRunDate != null && r.NextRunDate <= today).OrderBy(r => r.NextRunDate).Select(r => r.Id).ToArrayAsync(ct);
        var generated = 0; var duplicates = 0; var blocked = 0; var more = false;
        foreach (var id in ids)
        {
            // SQLite serializes write transactions across contexts/processes; reread AFTER taking the lock.
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            db.ChangeTracker.Clear();
            try
            {
                var rule = await Rows.SingleOrDefaultAsync(r => r.Id == id, ct);
                if (rule is null) continue;
                var result = await GenerateRule(rule, today, ct);
                await transaction.CommitAsync(ct);
                generated += result.Generated; duplicates += result.Duplicates; blocked += result.Blocked; more |= result.HasMore;
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 19 })
            {
                await transaction.RollbackAsync(ct); db.ChangeTracker.Clear();
                logger.LogWarning(ex, "Recurring generation constraint conflict for {RecurringId}; will retry next check", id);
                more = true;
            }
        }
        return new(generated, duplicates, blocked, more);
    }
}
