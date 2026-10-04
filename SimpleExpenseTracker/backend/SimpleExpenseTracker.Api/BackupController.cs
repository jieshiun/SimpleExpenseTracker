using Microsoft.AspNetCore.Mvc;

namespace SimpleExpenseTracker.Api;

[ApiController, Route("api/backups")]
public sealed class BackupController(BackupService backups) : ControllerBase
{
    [HttpGet("export")]
    public IActionResult Export() => File(backups.Export(), "application/octet-stream", $"daily-expense-{DateTime.UtcNow:yyyyMMdd-HHmmss}.db");
    [HttpPost("preview"), RequestSizeLimit(BackupService.MaxBytes + 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = BackupService.MaxBytes + 1024 * 1024)]
    public Task<BackupPreview> Preview(IFormFile file, CancellationToken ct) => backups.Preview(file, ct);
    [HttpPost("restore")]
    public async Task<IActionResult> Restore(RestoreInput input, CancellationToken ct)
    {
        var safetyBackup = await backups.Restore(input, Request.Headers["X-Ledger-Generation"], ct);
        Response.Headers["X-Ledger-Generation"] = backups.Generation;
        return Ok(new { safetyBackup, generation = backups.Generation });
    }
    [HttpGet]
    public SafetyBackup[] List() => backups.ListSafety();
    [HttpGet("saved/{name}")]
    public IActionResult Download(string name) => File(backups.DownloadSafety(name), "application/octet-stream", name);
}
