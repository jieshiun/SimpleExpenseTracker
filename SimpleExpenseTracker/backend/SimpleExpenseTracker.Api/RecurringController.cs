using Microsoft.AspNetCore.Mvc;
using SimpleExpenseTracker.Application;

namespace SimpleExpenseTracker.Api;

[ApiController, Route("api/recurring-transactions")]
public sealed class RecurringController(IRecurringTransactionService service, IRecurringTransactionGenerator generator) : ControllerBase
{
    [HttpGet] public Task<IReadOnlyList<RecurringDto>> List([FromQuery] RecurringQuery query, CancellationToken ct) => service.List(query, ct);
    [HttpGet("{id:int}")] public Task<RecurringDto> Get(int id, CancellationToken ct) => service.Get(id, ct);
    [HttpPost] public async Task<IActionResult> Create(RecurringInput input, CancellationToken ct)
    { var item = await service.Save(null, input, ct); return CreatedAtAction(nameof(Get), new { id = item.Id }, item); }
    [HttpPut("{id:int}")] public Task<RecurringDto> Update(int id, RecurringInput input, CancellationToken ct) => service.Save(id, input, ct);
    [HttpPatch("{id:int}/status")] public Task<RecurringDto> Status(int id, RecurringStatus input, CancellationToken ct) => service.Status(id, input, ct);
    [HttpDelete("{id:int}")] public async Task<IActionResult> Disable(int id, [FromBody] RecurringAction input, CancellationToken ct)
    { await service.Status(id, new() { IsActive = false, Version = input.Version }, ct); return NoContent(); }
    [HttpGet("summary")] public Task<RecurringSummaryDto> Summary([FromQuery] RecurringQuery query, CancellationToken ct, [FromQuery] int? year = null, [FromQuery] int? month = null) => service.Summary(query, year, month, ct);
    [HttpGet("upcoming")] public Task<IReadOnlyList<UpcomingDto>> Upcoming([FromQuery] RecurringQuery query, CancellationToken ct, [FromQuery] int days = 30) => service.Upcoming(query, days, ct);
    [HttpPost("generate")] public Task<GenerationResult> Generate(CancellationToken ct) => generator.Generate(ct);
}
