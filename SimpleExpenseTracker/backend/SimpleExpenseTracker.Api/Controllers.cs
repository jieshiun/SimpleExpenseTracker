using Microsoft.AspNetCore.Mvc;
using SimpleExpenseTracker.Application;

namespace SimpleExpenseTracker.Api;

[ApiController, Route("api/transactions")]
public sealed class TransactionsController(IExpenseService service) : ControllerBase
{
    [HttpGet] public Task<PageDto<TransactionDto>> List([FromQuery] TransactionQuery query, CancellationToken ct) => service.TransactionsAsync(query, ct);
    [HttpGet("{id:int}")] public Task<TransactionDto> Get(int id, CancellationToken ct) => service.TransactionAsync(id, ct);
    [HttpPost] public async Task<IActionResult> Create(TransactionInput input, CancellationToken ct) { var item = await service.SaveTransactionAsync(null, input, ct); return CreatedAtAction(nameof(Get), new { id = item.Id }, item); }
    [HttpPut("{id:int}")] public Task<TransactionDto> Update(int id, TransactionInput input, CancellationToken ct) => service.SaveTransactionAsync(id, input, ct);
    [HttpDelete("{id:int}")] public async Task<IActionResult> Delete(int id, CancellationToken ct) { await service.DeleteTransactionAsync(id, ct); return NoContent(); }
}
[ApiController, Route("api/categories")]
public sealed class CategoriesController(IExpenseService service) : ControllerBase
{
    [HttpGet] public Task<IReadOnlyList<CategoryDto>> List(CancellationToken ct) => service.CategoriesAsync(ct);
    [HttpGet("{id:int}")] public Task<CategoryDto> Get(int id, CancellationToken ct) => service.CategoryAsync(id, ct);
    [HttpPost] public async Task<IActionResult> Create(CategoryInput input, CancellationToken ct) { var item = await service.SaveCategoryAsync(null, input, ct); return CreatedAtAction(nameof(Get), new { id = item.Id }, item); }
    [HttpPut("{id:int}")] public Task<CategoryDto> Update(int id, CategoryInput input, CancellationToken ct) => service.SaveCategoryAsync(id, input, ct);
    [HttpDelete("{id:int}")] public async Task<IActionResult> Delete(int id, CancellationToken ct) { await service.DeleteCategoryAsync(id, ct); return NoContent(); }
}
[ApiController, Route("api/accounts")]
public sealed class AccountsController(IExpenseService service) : ControllerBase
{
    [HttpGet] public Task<IReadOnlyList<AccountDto>> List(CancellationToken ct) => service.AccountsAsync(ct);
    [HttpGet("{id:int}")] public Task<AccountDto> Get(int id, CancellationToken ct) => service.AccountAsync(id, ct);
    [HttpPost] public async Task<IActionResult> Create(AccountInput input, CancellationToken ct) { var item = await service.SaveAccountAsync(null, input, ct); return CreatedAtAction(nameof(Get), new { id = item.Id }, item); }
    [HttpPut("{id:int}")] public Task<AccountDto> Update(int id, AccountInput input, CancellationToken ct) => service.SaveAccountAsync(id, input, ct);
    [HttpDelete("{id:int}")] public async Task<IActionResult> Delete(int id, CancellationToken ct) { await service.DeleteAccountAsync(id, ct); return NoContent(); }
}
