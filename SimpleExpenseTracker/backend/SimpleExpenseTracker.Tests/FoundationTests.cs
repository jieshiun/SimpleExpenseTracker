using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SimpleExpenseTracker.Infrastructure;
using SimpleExpenseTracker.Domain;
using Xunit;

namespace SimpleExpenseTracker.Tests;
public class FoundationTests
{
    [Fact]
    public async Task TransactionsSurviveDatabaseReopen()
    {
        var path = Path.Combine(Path.GetTempPath(), $"expense-persistence-{Guid.NewGuid()}.db");
        var options = new DbContextOptionsBuilder<ExpenseDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
        try
        {
            await using (var db = new ExpenseDbContext(options))
            {
                await db.Database.MigrateAsync(); await SeedData.InitializeAsync(db);
                var category = await db.Categories.FirstAsync(c => c.Type == TransactionType.Expense);
                var account = await db.Accounts.FirstAsync();
                db.Transactions.Add(new Transaction { Amount = 350.25m, CategoryId = category.Id, AccountId = account.Id, TransactionDate = new DateTime(2026, 10, 3) });
                await db.SaveChangesAsync();
            }
            await using (var reopened = new ExpenseDbContext(options))
            {
                await reopened.Database.MigrateAsync(); await SeedData.InitializeAsync(reopened);
                Assert.Equal(350.25m, (await reopened.Transactions.SingleAsync()).Amount);
                Assert.Equal(16, await reopened.Categories.CountAsync());
                Assert.Equal(3, await reopened.Accounts.CountAsync());
            }
        }
        finally { foreach (var suffix in new[] { "", "-wal", "-shm" }) if (File.Exists(path + suffix)) File.Delete(path + suffix); }
    }
    [Fact]
    public async Task MigrationAndSeedAreIdempotent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ExpenseDbContext(new DbContextOptionsBuilder<ExpenseDbContext>().UseSqlite(connection).Options);
        await db.Database.MigrateAsync();
        await SeedData.InitializeAsync(db);
        await SeedData.InitializeAsync(db);
        Assert.Equal(16, await db.Categories.CountAsync());
        Assert.Equal(3, await db.Accounts.CountAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }
}
