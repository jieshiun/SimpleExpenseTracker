using System.Net;
using System.Net.Http.Json;
using SimpleExpenseTracker.Application;
using SimpleExpenseTracker.Domain;
using Xunit;

namespace SimpleExpenseTracker.Tests;

public class RecurringApiTests : ApiTestBase
{
    private async Task<RecurringInput> Rule()
    {
        var tx = await Input();
        return new RecurringInput { Name = "租金", Type = tx.Type, CategoryId = tx.CategoryId, AccountId = tx.AccountId,
            Amount = 100, AmountType = AmountType.Variable, Frequency = RecurringFrequency.Monthly,
            DayOfMonth = 31, StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 3, 31), ClientRequestId = Guid.NewGuid() };
    }
    private async Task<RecurringDto> CreateRule(RecurringInput input)
    {
        var response = await Client.PostAsJsonAsync("/api/recurring-transactions", input, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RecurringDto>(Json))!;
    }
    [Fact]
    public async Task DisabledReferenceBlocksGenerationWithoutRemovingTemplate()
    {
        var input = await Rule(); var rule = await CreateRule(input);
        (await Client.DeleteAsync($"/api/accounts/{input.AccountId}")).EnsureSuccessStatusCode();
        var result = (await (await Client.PostAsync("/api/recurring-transactions/generate", null)).Content.ReadFromJsonAsync<GenerationResult>(Json))!;
        Assert.Equal(1, result.Blocked); Assert.Equal(0, result.Generated);
        Assert.NotNull((await Client.GetFromJsonAsync<RecurringDto>($"/api/recurring-transactions/{rule.Id}", Json))!.Warning);
    }
    [Fact]
    public async Task CatchUpIsConcurrentSafeAndDeletedOccurrencesStayDeleted()
    {
        var input = await Rule(); var rule = await CreateRule(input);
        var replay = await CreateRule(input); Assert.Equal(rule.Id, replay.Id);
        var calls = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Client.PostAsync("/api/recurring-transactions/generate", null)));
        foreach (var response in calls) Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = (await Client.GetFromJsonAsync<PageDto<TransactionDto>>("/api/transactions?year=2026&pageSize=50", Json))!.Items;
        Assert.Equal(3, rows.Count); Assert.Equal(3, rows.Select(t => t.RecurringOccurrenceDate).Distinct().Count());
        Assert.All(rows, t => { Assert.Equal(rule.Id, t.RecurringTransactionId); Assert.Equal(100, t.Amount); });
        await DeleteTransaction(rows.First().Id);
        var again = await Client.PostAsync("/api/recurring-transactions/generate", null);
        Assert.Equal(0, (await again.Content.ReadFromJsonAsync<GenerationResult>(Json))!.Generated);
        Assert.Equal(2, (await Client.GetFromJsonAsync<PageDto<TransactionDto>>("/api/transactions?year=2026", Json))!.Total);
        var updated = await Client.GetFromJsonAsync<RecurringDto>($"/api/recurring-transactions/{rule.Id}", Json);
        Assert.Null(updated!.NextRunDate); Assert.Equal(new DateOnly(2026, 3, 31), updated.LastGeneratedDate);
    }
    [Fact]
    public async Task ValidationForecastAndVersionChecks()
    {
        var input = await Rule();
        foreach (var invalid in new[] { input with { Amount = 0 }, input with { Amount = .001m }, input with { Interval = 0 }, input with { CategoryId = 99999 }, input with { EndDate = new DateOnly(2025, 1, 1) } })
            Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsJsonAsync("/api/recurring-transactions", invalid, Json)).StatusCode);
        var rule = await CreateRule(input);
        var summary = await Client.GetFromJsonAsync<RecurringSummaryDto>("/api/recurring-transactions/summary?year=2026&month=2", Json);
        Assert.Equal(100, summary!.MonthlyExpense); Assert.Equal(300, summary.AnnualExpense);
        Assert.Empty((await Client.GetFromJsonAsync<RecurringDto[]>("/api/recurring-transactions?ownership=Personal", Json))!);
        var disabled = await Client.PatchAsJsonAsync($"/api/recurring-transactions/{rule.Id}/status", new RecurringStatus { IsActive = false, Version = rule.Version }, Json);
        Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PutAsJsonAsync($"/api/recurring-transactions/{rule.Id}", input with { Version = rule.Version }, Json)).StatusCode);
        Assert.Equal(0, (await (await Client.PostAsync("/api/recurring-transactions/generate", null)).Content.ReadFromJsonAsync<GenerationResult>(Json))!.Generated);
    }
    [Fact]
    public async Task EditingActiveRuleSettlesOldAmountAndPreservesHistory()
    {
        var input = await Rule(); var rule = await CreateRule(input);
        var response = await Client.PutAsJsonAsync($"/api/recurring-transactions/{rule.Id}", input with { Amount = 200, Version = rule.Version }, Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = (await Client.GetFromJsonAsync<PageDto<TransactionDto>>("/api/transactions?year=2026", Json))!.Items;
        Assert.Equal(3, rows.Count); Assert.All(rows, t => Assert.Equal(100, t.Amount));
        Assert.Equal(200, (await response.Content.ReadFromJsonAsync<RecurringDto>(Json))!.Amount);
    }
    [Fact]
    public async Task VariableActualAmountAndDisableLeaveTemplateAndHistoryIndependent()
    {
        var input = await Rule(); var rule = await CreateRule(input);
        Assert.Single((await Client.GetFromJsonAsync<UpcomingDto[]>("/api/recurring-transactions/upcoming", Json))!);
        (await Client.PostAsync("/api/recurring-transactions/generate", null)).EnsureSuccessStatusCode();
        var row = (await Client.GetFromJsonAsync<PageDto<TransactionDto>>("/api/transactions?year=2026", Json))!.Items.First();
        var actual = await Input(amount: 632);
        (await Client.PutAsJsonAsync($"/api/transactions/{row.Id}", actual with { Version = row.Version }, Json)).EnsureSuccessStatusCode();
        var latest = (await Client.GetFromJsonAsync<RecurringDto>($"/api/recurring-transactions/{rule.Id}", Json))!;
        Assert.Equal(100, latest.Amount);
        var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/recurring-transactions/{rule.Id}") { Content = JsonContent.Create(new RecurringAction(latest.Version), options: Json) };
        Assert.Equal(HttpStatusCode.NoContent, (await Client.SendAsync(delete)).StatusCode);
        Assert.Equal(3, (await Client.GetFromJsonAsync<PageDto<TransactionDto>>("/api/transactions?year=2026", Json))!.Total);
        var edited = (await Client.GetFromJsonAsync<TransactionDto>($"/api/transactions/{row.Id}", Json))!;
        Assert.Equal(632, edited.Amount); Assert.Equal(rule.Id, edited.RecurringTransactionId);
    }
    [Fact]
    public async Task BackgroundStartupCatchesUpAndRestartDoesNotDuplicate()
    {
        var path = Path.Combine(Path.GetTempPath(), $"recurring-restart-{Guid.NewGuid():N}.db");
        try
        {
            using (var factory = new ApiFactory(path, true))
            using (var client = factory.CreateClient())
            {
                using var handshake = await client.GetAsync("/api/backups");
                client.DefaultRequestHeaders.Add("X-Ledger-Generation", handshake.Headers.GetValues("X-Ledger-Generation").Single());
                var categories = (await client.GetFromJsonAsync<CategoryDto[]>("/api/categories", Json))!;
                var accounts = (await client.GetFromJsonAsync<AccountDto[]>("/api/accounts", Json))!;
                (await client.PostAsJsonAsync("/api/recurring-transactions", new RecurringInput { Name = "停機補帳", CategoryId = categories.First(c => c.Type == TransactionType.Expense).Id, AccountId = accounts.First().Id, Amount = 599, Frequency = RecurringFrequency.Monthly, DayOfMonth = 1, StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 3, 1) }, Json)).EnsureSuccessStatusCode();
            }
            for (var restart = 0; restart < 2; restart++)
            {
                using var factory = new ApiFactory(path, true, true); using var client = factory.CreateClient();
                PageDto<TransactionDto>? rows = null;
                for (var attempt = 0; attempt < 100; attempt++)
                {
                    rows = await client.GetFromJsonAsync<PageDto<TransactionDto>>("/api/transactions?year=2026", Json);
                    if (rows!.Total == 3) break;
                    await Task.Delay(30);
                }
                Assert.Equal(3, rows!.Total);
                Assert.Equal(3, rows.Items.Select(t => t.RecurringOccurrenceDate).Distinct().Count());
            }
        }
        finally { File.Delete(path); if (Directory.Exists(path + ".backups")) Directory.Delete(path + ".backups", true); }
    }
}
