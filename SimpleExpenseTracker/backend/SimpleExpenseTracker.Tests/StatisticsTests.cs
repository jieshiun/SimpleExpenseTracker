using System.Net;
using System.Net.Http.Json;
using SimpleExpenseTracker.Application;
using SimpleExpenseTracker.Domain;
using Xunit;
namespace SimpleExpenseTracker.Tests;
public class StatisticsTests : ApiTestBase
{
    [Fact]
    public async Task MultipleCategoriesRoundToOneHundredPercentAndEmptyMonthsAreZero()
    {
        var input = await Input(amount: 100);
        var categories = await Client.GetFromJsonAsync<CategoryDto[]>("/api/categories", Json);
        foreach (var category in categories!.Where(c => c.Type == TransactionType.Expense).Take(3))
            await Create(input with { CategoryId = category.Id });
        var stats = await Client.GetFromJsonAsync<CategoryStatisticDto[]>("/api/statistics/categories?year=2026&month=10", Json);
        Assert.NotNull(stats); Assert.Equal(3, stats.Length);
        Assert.InRange(stats.Sum(s => s.Percentage), 99.99m, 100.01m);
        Assert.Equal(new SummaryDto(0, 0, 0), await Client.GetFromJsonAsync<SummaryDto>("/api/dashboard/summary?year=2025&month=1", Json));
        Assert.Empty((await Client.GetFromJsonAsync<CategoryStatisticDto[]>("/api/statistics/categories?year=2025&month=1", Json))!);
    }
    [Fact]
    public async Task MonthlySummaryAndCategoryPercentagesReflectEditsAndDeletes()
    {
        await Create(await Input(TransactionType.Income, 65000));
        var input = await Input(amount: 28520);
        var expense = await Create(input);
        await Create(await Input(amount: 900, date: "2026-11-01"));
        var summary = await Client.GetFromJsonAsync<SummaryDto>("/api/dashboard/summary?year=2026&month=10", Json);
        Assert.Equal(new SummaryDto(65000, 28520, 36480), summary);
        var categories = await Client.GetFromJsonAsync<CategoryStatisticDto[]>("/api/statistics/categories?year=2026&month=10&type=Expense", Json);
        Assert.NotNull(categories); Assert.Equal(28520, categories.Single().Amount); Assert.Equal(100m, categories.Sum(c => c.Percentage));
        await Client.PutAsJsonAsync($"/api/transactions/{expense.Id}", input with { Amount = 0.30m }, Json);
        await Create(input with { Amount = 0.10m });
        Assert.Equal(0.40m, (await Client.GetFromJsonAsync<SummaryDto>("/api/dashboard/summary?year=2026&month=10", Json))!.Expense);
        await Client.DeleteAsync($"/api/transactions/{expense.Id}");
        Assert.Equal(0.10m, (await Client.GetFromJsonAsync<SummaryDto>("/api/dashboard/summary?year=2026&month=10", Json))!.Expense);
    }
    [Fact]
    public async Task TrendIncludesEmptyMonthsAndCurrentMonth()
    {
        var date = DateTime.Today.ToString("yyyy-MM-dd");
        await Create(await Input(amount: 350, date: date));
        var rows = await Client.GetFromJsonAsync<MonthlyStatisticDto[]>("/api/statistics/monthly?months=6", Json);
        Assert.Equal(6, rows!.Length); Assert.Equal(350, rows[^1].Expense); Assert.Equal(DateTime.Today.Month, rows[^1].Month);
        Assert.All(rows[..^1], r => Assert.Equal(0, r.Expense));
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync("/api/statistics/monthly?months=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync("/api/dashboard/summary?year=2026&month=13")).StatusCode);
    }
}
