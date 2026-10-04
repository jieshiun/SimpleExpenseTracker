using System.Net;
using System.Net.Http.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SimpleExpenseTracker.Application;
using SimpleExpenseTracker.Domain;
using SimpleExpenseTracker.Infrastructure;
using Xunit;

namespace SimpleExpenseTracker.Tests;

public class FamilyTests : ApiTestBase
{
    [Fact]
    public async Task SharedAndPersonalFiltersAgreeAcrossListsAndStatistics()
    {
        var date = DateTime.Today;
        var input = await Input(amount: 100, date: date.ToString("yyyy-MM-dd"));
        var members = (await Client.GetFromJsonAsync<MemberDto[]>("/api/members", Json))!;
        await Create(input);
        await Create(input with { OwnerMemberId = members[1].Id, Amount = 200 });
        await Create(input with { Ownership = OwnershipKind.Shared, OwnerMemberId = null, Amount = 300 });
        await Create(await Input(TransactionType.Income, 1000, date.ToString("yyyy-MM-dd")));
        foreach (var (filter, expense, income, count) in new[] {
            ("", 600m, 1000m, 4), ("&ownership=Shared", 300m, 0m, 1),
            ($"&ownership=Personal&ownerMemberId={members[0].Id}", 100m, 1000m, 2),
            ($"&ownerMemberId={members[1].Id}", 200m, 0m, 1), ("&ownership=Unknown", 0m, 0m, 0) })
        {
            var query = $"year={date.Year}&month={date.Month}{filter}";
            Assert.Equal(count, (await Client.GetFromJsonAsync<PageDto<TransactionDto>>($"/api/transactions?{query}", Json))!.Total);
            Assert.Equal(new SummaryDto(income, expense, income - expense), await Client.GetFromJsonAsync<SummaryDto>($"/api/dashboard/summary?{query}", Json));
            Assert.Equal(expense, (await Client.GetFromJsonAsync<CategoryStatisticDto[]>($"/api/statistics/categories?{query}", Json))!.Sum(x => x.Amount));
            var trend = (await Client.GetFromJsonAsync<MonthlyStatisticDto[]>($"/api/statistics/monthly?months=1{filter}", Json))!.Single();
            Assert.Equal(expense, trend.Expense); Assert.Equal(income, trend.Income);
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync($"/api/transactions?ownership=Shared&ownerMemberId={members[0].Id}")).StatusCode);
    }

    [Fact]
    public async Task RetriedAndSimultaneousCreatesProduceOnlyOneTransaction()
    {
        var input = await Input();
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Client.PostAsJsonAsync("/api/transactions", input, Json)));
        var ids = new List<int>();
        foreach (var response in responses) { Assert.Equal(HttpStatusCode.Created, response.StatusCode); ids.Add((await response.Content.ReadFromJsonAsync<TransactionDto>(Json))!.Id); }
        Assert.Single(ids.Distinct());
        Assert.Equal(1, (await Client.GetFromJsonAsync<PageDto<TransactionDto>>("/api/transactions", Json))!.Total);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PostAsJsonAsync("/api/transactions", input with { Amount = 1 }, Json)).StatusCode);
    }

    [Fact]
    public async Task ConcurrentEditsHaveOneWinnerAndPreserveCreator()
    {
        var input = await Input(); var item = await Create(input);
        var members = (await Client.GetFromJsonAsync<MemberDto[]>("/api/members", Json))!;
        var edits = await Task.WhenAll(Enumerable.Range(1, 2).Select(i => Client.PutAsJsonAsync($"/api/transactions/{item.Id}", input with { Version = item.Version, Amount = i, OperatorId = members[1].Id }, Json)));
        Assert.Single(edits, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(edits, r => r.StatusCode == HttpStatusCode.Conflict);
        var latest = (await Client.GetFromJsonAsync<TransactionDto>($"/api/transactions/{item.Id}", Json))!;
        Assert.Equal(item.Version + 1, latest.Version);
        Assert.Equal(item.CreatedById, latest.CreatedById);
        Assert.Equal(members[1].Id, latest.UpdatedById);
        Assert.Equal(item.OwnerMemberId, latest.OwnerMemberId);
        Assert.Equal(HttpStatusCode.Conflict, (await DeleteTransaction(item.Id, item.Version)).StatusCode);
    }

    [Fact]
    public async Task SoftDeleteRestorePreservesReferencesAndExcludesDeletedMoney()
    {
        var input = await Input(); var item = await Create(input);
        Assert.Equal(HttpStatusCode.NoContent, (await DeleteTransaction(item.Id)).StatusCode);
        var deleted = (await Client.GetFromJsonAsync<PageDto<TransactionDto>>("/api/transactions?deleted=true", Json))!.Items.Single();
        Assert.True(deleted.IsDeleted); Assert.NotNull(deleted.DeletedAt); Assert.Equal(input.OperatorId, deleted.DeletedById);
        Assert.Equal(0, (await Client.GetFromJsonAsync<SummaryDto>("/api/dashboard/summary?year=2026&month=10", Json))!.Expense);
        Assert.Empty((await Client.GetFromJsonAsync<CategoryStatisticDto[]>("/api/statistics/categories?year=2026&month=10", Json))!);
        await Client.DeleteAsync($"/api/categories/{item.CategoryId}");
        await Client.DeleteAsync($"/api/accounts/{item.AccountId}");
        Assert.False((await Client.GetFromJsonAsync<CategoryDto>($"/api/categories/{item.CategoryId}", Json))!.IsActive);
        var action = new TransactionAction { OperatorId = input.OperatorId, Version = deleted.Version };
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PostAsJsonAsync($"/api/transactions/{item.Id}/restore", action with { Version = item.Version }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.PostAsJsonAsync($"/api/transactions/{item.Id}/restore", action, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PostAsJsonAsync($"/api/transactions/{item.Id}/restore", action, Json)).StatusCode);
        Assert.Equal(input.Amount, (await Client.GetFromJsonAsync<SummaryDto>("/api/dashboard/summary?year=2026&month=10", Json))!.Expense);
        Assert.Empty((await Client.GetFromJsonAsync<PageDto<TransactionDto>>("/api/transactions?deleted=true", Json))!.Items);
    }

    [Fact]
    public async Task RenamingAndDisablingMembersPreservesHistoryButRejectsNewActions()
    {
        var input = await Input(); var item = await Create(input);
        var members = (await Client.GetFromJsonAsync<MemberDto[]>("/api/members", Json))!;
        Assert.Equal(HttpStatusCode.OK, (await Client.PutAsJsonAsync($"/api/members/{input.OperatorId}", new MemberInput { Name = "先生", IsActive = false }, Json)).StatusCode);
        var history = (await Client.GetFromJsonAsync<TransactionDto>($"/api/transactions/{item.Id}", Json))!;
        Assert.Equal("先生", history.CreatedByName); Assert.Equal("先生", history.OwnerMemberName);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsJsonAsync("/api/transactions", input, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.PutAsJsonAsync($"/api/transactions/{item.Id}", input with { Version = item.Version, OperatorId = members[1].Id }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsJsonAsync("/api/transactions", input with { OperatorId = members[1].Id }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsJsonAsync("/api/transactions", input with { OperatorId = 0 }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsJsonAsync("/api/transactions", input with { Ownership = OwnershipKind.Unknown, OwnerMemberId = null, OperatorId = members[1].Id }, Json)).StatusCode);
    }

    [Fact]
    public async Task UpgradeKeepsLegacyMoneyAndLeavesAttributionUnknown()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new ExpenseDbContext(new DbContextOptionsBuilder<ExpenseDbContext>().UseSqlite(connection).Options);
        await db.GetService<IMigrator>().MigrateAsync("20261003024600_InitialCreate");
        await SeedData.InitializeAsync(db);
        var category = await db.Categories.FirstAsync(c => c.Type == TransactionType.Expense);
        var account = await db.Accounts.FirstAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Transactions (Type,Amount,CategoryId,AccountId,TransactionDate,Note,CreatedAt,UpdatedAt) VALUES (0, {"123.45"}, {category.Id}, {account.Id}, {new DateTime(2026, 10, 3)}, {"原始帳目"}, {DateTime.UtcNow}, {DateTime.UtcNow})");
        await db.Database.MigrateAsync();
        var item = await db.Transactions.SingleAsync();
        Assert.Equal(123.45m, item.Amount); Assert.Equal("原始帳目", item.Note);
        Assert.Equal(OwnershipKind.Unknown, item.Ownership); Assert.Null(item.OwnerMemberId); Assert.Null(item.CreatedById);
        Assert.Equal(1, item.Version); Assert.False(item.IsDeleted); Assert.Equal(2, await db.Members.CountAsync());
        var stats = new StatisticsService(db, TimeProvider.System);
        Assert.Equal(123.45m, (await stats.SummaryAsync(2026, 10, default)).Expense);
        Assert.Equal(123.45m, (await stats.SummaryAsync(2026, 10, default, OwnershipKind.Unknown)).Expense);
    }
}
