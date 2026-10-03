using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SimpleExpenseTracker.Infrastructure;
using Xunit;

namespace SimpleExpenseTracker.Tests;
public class FoundationTests
{
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
