using System.Net;
using System.Net.Http.Json;
using SimpleExpenseTracker.Application;
using SimpleExpenseTracker.Domain;
using Xunit;

namespace SimpleExpenseTracker.Tests;
public sealed class CatalogTests : ApiTestBase
{
    [Fact]
    public async Task CategoryLifecyclePreservesUsedReferencesAndSortOrder()
    {
        var input = new CategoryInput { Name = "咖啡", Icon = "☕", Type = TransactionType.Expense, SortOrder = 2 };
        var created = await Client.PostAsJsonAsync("/api/categories", input, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var category = (await created.Content.ReadFromJsonAsync<CategoryDto>(Json))!;
        var used = await Input();
        await Create(used with { CategoryId = category.Id });
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PutAsJsonAsync($"/api/categories/{category.Id}", input with { Type = TransactionType.Income }, Json)).StatusCode);
        var update = await Client.PutAsJsonAsync($"/api/categories/{category.Id}", input with { Name = "咖啡點心", SortOrder = 0, IsActive = false }, Json);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var value = (await update.Content.ReadFromJsonAsync<CategoryDto>(Json))!;
        Assert.Equal("咖啡點心", value.Name); Assert.Equal(0, value.SortOrder); Assert.False(value.IsActive);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsJsonAsync("/api/transactions", used with { CategoryId = category.Id }, Json)).StatusCode);
        await Client.PutAsJsonAsync($"/api/categories/{category.Id}", input with { IsActive = true }, Json);
        await Create(used with { CategoryId = category.Id });
    }
    [Fact]
    public async Task UnusedCategoryAndAccountCanBeDeleted()
    {
        var c = await Client.PostAsJsonAsync("/api/categories", new CategoryInput { Name = "測試", Icon = "📌" }, Json);
        var category = (await c.Content.ReadFromJsonAsync<CategoryDto>(Json))!;
        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync($"/api/categories/{category.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/categories/{category.Id}")).StatusCode);
        var a = await Client.PostAsJsonAsync("/api/accounts", new AccountInput { Name = "電子支付", Type = AccountType.EWallet }, Json);
        var account = (await a.Content.ReadFromJsonAsync<AccountDto>(Json))!;
        var update = await Client.PutAsJsonAsync($"/api/accounts/{account.Id}", new AccountInput { Name = "我的電子支付", Type = AccountType.EWallet, InitialBalance = 100.20m }, Json);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(100.20m, (await update.Content.ReadFromJsonAsync<AccountDto>(Json))!.InitialBalance);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync($"/api/accounts/{account.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/accounts/{account.Id}")).StatusCode);
    }
    [Theory]
    [InlineData("/api/categories", "{\"name\":\" \",\"icon\":\"📌\"}")]
    [InlineData("/api/accounts", "{\"name\":\"現金\",\"type\":\"Invalid\"}")]
    [InlineData("/api/transactions", "{\"amount\":0}")]
    public async Task ValidationUsesConsistentSafeProblemDetails(string path, string body)
    {
        var response = await Client.PostAsync(path, new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(400, json.RootElement.GetProperty("status").GetInt32());
        Assert.True(json.RootElement.TryGetProperty("title", out _));
        Assert.True(json.RootElement.TryGetProperty("traceId", out _));
        Assert.False(json.RootElement.TryGetProperty("stackTrace", out _));
    }
}
