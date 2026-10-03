using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using SimpleExpenseTracker.Application;
using SimpleExpenseTracker.Domain;
using Xunit;

namespace SimpleExpenseTracker.Tests;
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string file = Path.Combine(Path.GetTempPath(), $"expense-{Guid.NewGuid()}.db");
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Default"] = $"Data Source={file};Pooling=False" }));
    protected override void Dispose(bool disposing) { base.Dispose(disposing); if (File.Exists(file)) File.Delete(file); }
}
public class ApiTests : IDisposable
{
    private readonly ApiFactory factory = new();
    protected readonly HttpClient Client;
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    public ApiTests() { Client = factory.CreateClient(); }
    public void Dispose() { Client.Dispose(); factory.Dispose(); GC.SuppressFinalize(this); }
    protected async Task<TransactionInput> Input(TransactionType type = TransactionType.Expense, decimal amount = 350, string date = "2026-10-03")
    {
        var categories = await Client.GetFromJsonAsync<CategoryDto[]>("/api/categories", Json);
        var accounts = await Client.GetFromJsonAsync<AccountDto[]>("/api/accounts", Json);
        return new() { Type = type, Amount = amount, CategoryId = categories!.First(c => c.Type == type).Id, AccountId = accounts!.First().Id, TransactionDate = DateTime.Parse(date), Note = "午餐" };
    }
    protected async Task<TransactionDto> Create(TransactionInput input)
    {
        var response = await Client.PostAsJsonAsync("/api/transactions", input, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TransactionDto>(Json))!;
    }
    [Theory]
    [InlineData(TransactionType.Expense)] [InlineData(TransactionType.Income)]
    public async Task CreateUpdateDelete(TransactionType type)
    {
        var input = await Input(type);
        var item = await Create(input);
        var updated = await Client.PutAsJsonAsync($"/api/transactions/{item.Id}", input with { Amount = 420.50m }, Json);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(420.50m, (await updated.Content.ReadFromJsonAsync<TransactionDto>(Json))!.Amount);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync($"/api/transactions/{item.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/transactions/{item.Id}")).StatusCode);
    }
    [Fact] public async Task RejectInvalidValues()
    {
        var input = await Input();
        foreach (var invalid in new[] { input with { Amount = 0 }, input with { Amount = -1 }, input with { Amount = .001m }, input with { Type = TransactionType.Income }, input with { CategoryId = 999999 }, input with { AccountId = 999999 }, input with { TransactionDate = null } })
            Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsJsonAsync("/api/transactions", invalid, Json)).StatusCode);
    }
    [Fact] public async Task UsedReferencesAreDisabledAndHistoryRemainsEditable()
    {
        var input = await Input(); var item = await Create(input);
        await Client.DeleteAsync($"/api/categories/{input.CategoryId}");
        await Client.DeleteAsync($"/api/accounts/{input.AccountId}");
        Assert.False((await Client.GetFromJsonAsync<CategoryDto>($"/api/categories/{input.CategoryId}", Json))!.IsActive);
        Assert.False((await Client.GetFromJsonAsync<AccountDto>($"/api/accounts/{input.AccountId}", Json))!.IsActive);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsJsonAsync("/api/transactions", input, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.PutAsJsonAsync($"/api/transactions/{item.Id}", input with { Note = "修改備註" }, Json)).StatusCode);
    }
    [Fact] public async Task FiltersAndPaginationAreValidated()
    {
        await Create(await Input());
        var result = await Client.GetFromJsonAsync<PageDto<TransactionDto>>("/api/transactions?year=2026&month=10&pageSize=1", Json);
        Assert.Equal(1, result!.Total);
        Assert.Single(result.Items);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync("/api/transactions?month=13&page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync("/api/transactions?month=10")).StatusCode);
    }
}
