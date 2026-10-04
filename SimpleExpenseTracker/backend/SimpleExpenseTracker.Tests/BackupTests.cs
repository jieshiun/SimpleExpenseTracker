using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SimpleExpenseTracker.Api;
using SimpleExpenseTracker.Application;
using SimpleExpenseTracker.Infrastructure;
using Xunit;

namespace SimpleExpenseTracker.Tests;

public sealed class BackupTests : ApiTestBase
{
    private static async Task<byte[]> LegacyBackup(string migration)
    {
        var path = Path.Combine(Path.GetTempPath(), $"legacy-backup-{Guid.NewGuid():N}.db");
        try
        {
            await using (var db = new ExpenseDbContext(new DbContextOptionsBuilder<ExpenseDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options))
            {
                await db.GetService<IMigrator>().MigrateAsync("20261003024600_InitialCreate");
                await SeedData.InitializeAsync(db);
                var category = await db.Categories.FirstAsync(c => c.Type == SimpleExpenseTracker.Domain.TransactionType.Expense);
                var account = await db.Accounts.FirstAsync();
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Transactions (Type,Amount,CategoryId,AccountId,TransactionDate,Note,CreatedAt,UpdatedAt) VALUES (0, {"123.45"}, {category.Id}, {account.Id}, {new DateTime(2026, 10, 3)}, {"舊版帳目"}, {DateTime.UtcNow}, {DateTime.UtcNow})");
                await db.GetService<IMigrator>().MigrateAsync(migration);
                if (migration.EndsWith("FamilyLedger"))
                    await db.Database.ExecuteSqlRawAsync("UPDATE Transactions SET IsDeleted=1, Version=3; UPDATE Members SET Name='原家庭成員' WHERE Id=1");
            }
            return await File.ReadAllBytesAsync(path);
        }
        finally { File.Delete(path); File.Delete(path + "-wal"); File.Delete(path + "-shm"); }
    }
    [Theory]
    [InlineData("20261003024600_InitialCreate")]
    [InlineData("20261004050755_FamilyLedger")]
    public async Task LegacyBackupsUpgradeOnlyStagingThenRestorePreservingHistory(string migration)
    {
        await Create(await Input(amount: 999)); var before = await Client.GetStringAsync("/api/transactions");
        var original = await LegacyBackup(migration); var copy = original.ToArray();
        var preview = await Preview(original);
        Assert.True(preview.WasUpgraded); Assert.Equal(migration, preview.SourceMigration);
        Assert.Equal(1, preview.Transactions); Assert.Equal(0, preview.RecurringTransactions);
        Assert.Equal(before, await Client.GetStringAsync("/api/transactions")); Assert.Equal(copy, original);
        using var restored = await Restore(preview); restored.EnsureSuccessStatusCode(); AcceptGeneration(restored);
        var deleted = migration.EndsWith("FamilyLedger");
        var rows = (await Client.GetFromJsonAsync<PageDto<TransactionDto>>($"/api/transactions?deleted={deleted.ToString().ToLowerInvariant()}", Json))!.Items;
        var row = Assert.Single(rows); Assert.Equal(123.45m, row.Amount); Assert.Equal("舊版帳目", row.Note);
        Assert.Equal(deleted ? 3 : 1, row.Version); Assert.Equal(deleted, row.IsDeleted); Assert.Null(row.RecurringTransactionId);
        if (deleted) Assert.Contains("原家庭成員", preview.Members);
        Assert.False((await Preview(await Export())).WasUpgraded);
    }
    [Theory]
    [InlineData("CREATE TABLE Unrelated(Id INTEGER)")]
    [InlineData("DELETE FROM __EFMigrationsHistory")]
    [InlineData("UPDATE __EFMigrationsHistory SET MigrationId='20990101000000_Future' WHERE MigrationId='20261004050755_FamilyLedger'")]
    [InlineData("PRAGMA foreign_keys=OFF; UPDATE Transactions SET CategoryId=999999")]
    [InlineData("UPDATE Transactions SET Amount='invalid'")]
    public async Task AlteredLegacyBackupIsRejectedWithoutChangingLiveLedger(string sql)
    {
        await Create(await Input()); var before = await Client.GetStringAsync("/api/transactions");
        var path = Path.Combine(Path.GetTempPath(), $"invalid-legacy-{Guid.NewGuid():N}.db");
        try
        {
            await File.WriteAllBytesAsync(path, await LegacyBackup("20261004050755_FamilyLedger"));
            await using (var db = new SqliteConnection($"Data Source={path};Pooling=False"))
            { await db.OpenAsync(); using var command = db.CreateCommand(); command.CommandText = sql; await command.ExecuteNonQueryAsync(); }
            Assert.Equal(HttpStatusCode.BadRequest, (await PreviewResponse(await File.ReadAllBytesAsync(path))).StatusCode);
            Assert.Equal(before, await Client.GetStringAsync("/api/transactions"));
        }
        finally { File.Delete(path); }
    }
    [Fact]
    public async Task RecurringTemplatesAndGeneratedSourceRoundTrip()
    {
        var input = await Input();
        var create = await Client.PostAsJsonAsync("/api/recurring-transactions", new RecurringInput { Name = "固定帳目", CategoryId = input.CategoryId, AccountId = input.AccountId, Amount = 123, Frequency = SimpleExpenseTracker.Domain.RecurringFrequency.Monthly, DayOfMonth = 1, StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 1, 1) }, Json);
        create.EnsureSuccessStatusCode();
        (await Client.PostAsync("/api/recurring-transactions/generate", null)).EnsureSuccessStatusCode();
        var rules = await Client.GetStringAsync("/api/recurring-transactions");
        var transactions = await Client.GetStringAsync("/api/transactions");
        var preview = await Preview(await Export()); Assert.Equal(1, preview.RecurringTransactions);
        var rule = (await Client.GetFromJsonAsync<RecurringDto[]>("/api/recurring-transactions", Json))!.Single();
        (await Client.PatchAsJsonAsync($"/api/recurring-transactions/{rule.Id}/status", new RecurringStatus { Version = rule.Version, IsActive = false }, Json)).EnsureSuccessStatusCode();
        using var restored = await Restore(preview); restored.EnsureSuccessStatusCode(); AcceptGeneration(restored);
        Assert.Equal(rules, await Client.GetStringAsync("/api/recurring-transactions"));
        Assert.Equal(transactions, await Client.GetStringAsync("/api/transactions"));
        Assert.Equal(0, (await (await Client.PostAsync("/api/recurring-transactions/generate", null)).Content.ReadFromJsonAsync<GenerationResult>(Json))!.Generated);
    }
    private async Task<byte[]> Export()
    {
        using var response = await Client.GetAsync("/api/backups/export");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        Assert.EndsWith(".db", response.Content.Headers.ContentDisposition!.FileName!.Trim('"'));
        return await response.Content.ReadAsByteArrayAsync();
    }
    private async Task<HttpResponseMessage> PreviewResponse(byte[] bytes)
    {
        using var body = new MultipartFormDataContent(); body.Add(new ByteArrayContent(bytes), "file", "test.db");
        return await Client.PostAsync("/api/backups/preview", body);
    }
    private async Task<BackupPreview> Preview(byte[] bytes)
    {
        using var response = await PreviewResponse(bytes); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BackupPreview>(Json))!;
    }
    private async Task<HttpResponseMessage> Restore(BackupPreview preview, string confirmation = "還原") =>
        await Client.PostAsJsonAsync("/api/backups/restore", new RestoreInput(preview.Token, confirmation), Json);
    private void AcceptGeneration(HttpResponseMessage response)
    {
        Client.DefaultRequestHeaders.Remove("X-Ledger-Generation");
        Client.DefaultRequestHeaders.Add("X-Ledger-Generation", response.Headers.GetValues("X-Ledger-Generation").Single());
    }
    [Fact]
    public async Task RoundTripRestoresAllHistoryAndSavesPreviousLedger()
    {
        var input = await Input(amount: 123.45m); var kept = await Create(input);
        var removed = await Create(input with { Amount = 67.89m });
        await DeleteTransaction(removed.Id);
        await Client.PutAsJsonAsync($"/api/members/{input.OperatorId}", new MemberInput { Name = "家人", IsActive = true }, Json);
        await Client.DeleteAsync($"/api/categories/{input.CategoryId}");
        await Client.DeleteAsync($"/api/accounts/{input.AccountId}");
        var originalTransactions = await Client.GetStringAsync("/api/transactions");
        var originalDeleted = await Client.GetStringAsync("/api/transactions?deleted=true");
        var originalCategories = await Client.GetStringAsync("/api/categories");
        var originalAccounts = await Client.GetStringAsync("/api/accounts");
        var originalMembers = await Client.GetStringAsync("/api/members");
        var backup = await Export(); var preview = await Preview(backup);
        Assert.Equal(2, preview.Transactions); Assert.Equal(1, preview.DeletedTransactions); Assert.Contains("家人", preview.Members);
        var updated = await Client.PutAsJsonAsync($"/api/transactions/{kept.Id}", input with { Version = kept.Version, Amount = 999 }, Json);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Restore(preview, "確認")).StatusCode);
        Assert.Equal(999m, (await Client.GetFromJsonAsync<TransactionDto>($"/api/transactions/{kept.Id}", Json))!.Amount);
        using var restored = await Restore(preview); Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        var stale = await Client.PutAsJsonAsync($"/api/transactions/{kept.Id}", input with { Version = kept.Version }, Json);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        AcceptGeneration(restored);
        Assert.Equal(originalTransactions, await Client.GetStringAsync("/api/transactions"));
        Assert.Equal(originalDeleted, await Client.GetStringAsync("/api/transactions?deleted=true"));
        Assert.Equal(originalCategories, await Client.GetStringAsync("/api/categories"));
        Assert.Equal(originalAccounts, await Client.GetStringAsync("/api/accounts"));
        Assert.Equal(originalMembers, await Client.GetStringAsync("/api/members"));
        Assert.Equal(123.45m, (await Client.GetFromJsonAsync<SummaryDto>("/api/dashboard/summary?year=2026&month=10", Json))!.Expense);
        var saved = (await Client.GetFromJsonAsync<SafetyBackup[]>("/api/backups", Json))!.Single();
        var before = await Client.GetByteArrayAsync($"/api/backups/saved/{saved.Name}");
        var previous = await Preview(before); using var undo = await Restore(previous);
        Assert.Equal(HttpStatusCode.OK, undo.StatusCode); AcceptGeneration(undo);
        Assert.Equal(999m, (await Client.GetFromJsonAsync<TransactionDto>($"/api/transactions/{kept.Id}", Json))!.Amount);
    }
    [Fact]
    public async Task InvalidSchemaCorruptionAndForeignKeysDoNotChangeCurrentLedger()
    {
        await Create(await Input()); var before = await Client.GetStringAsync("/api/transactions");
        Assert.Equal(HttpStatusCode.BadRequest, (await PreviewResponse(new byte[200])).StatusCode);
        var backup = await Export();
        foreach (var sql in new[] { "CREATE TABLE Unrelated(Id INTEGER)", "DELETE FROM __EFMigrationsHistory", "PRAGMA foreign_keys=OFF; UPDATE Transactions SET CategoryId=999999", "UPDATE Transactions SET Amount='invalid'", "UPDATE Transactions SET Version=0" })
        {
            var path = Path.Combine(Path.GetTempPath(), $"bad-backup-{Guid.NewGuid():N}.db");
            try
            {
                await File.WriteAllBytesAsync(path, backup);
                using (var db = new SqliteConnection($"Data Source={path};Pooling=False"))
                { db.Open(); using var command = db.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery(); }
                Assert.Equal(HttpStatusCode.BadRequest, (await PreviewResponse(await File.ReadAllBytesAsync(path))).StatusCode);
            }
            finally { File.Delete(path); }
        }
        Assert.Equal(before, await Client.GetStringAsync("/api/transactions"));
        Assert.Empty((await Client.GetFromJsonAsync<SafetyBackup[]>("/api/backups", Json))!);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsJsonAsync("/api/backups/restore", new RestoreInput("../../outside", "還原"), Json)).StatusCode);
    }
    [Fact]
    public async Task ConcurrentRestoresHaveOneWinnerAndKeepOnlyFiveSafetyBackups()
    {
        var backup = await Export();
        var first = await Preview(backup); var second = await Preview(backup);
        var attempts = await Task.WhenAll(Restore(first), Restore(second));
        Assert.Single(attempts, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(attempts, r => r.StatusCode == HttpStatusCode.Conflict);
        AcceptGeneration(attempts.Single(r => r.StatusCode == HttpStatusCode.OK));
        for (var i = 0; i < 5; i++) { using var restored = await Restore(await Preview(backup)); restored.EnsureSuccessStatusCode(); AcceptGeneration(restored); }
        Assert.Equal(5, (await Client.GetFromJsonAsync<SafetyBackup[]>("/api/backups", Json))!.Length);
    }
    [Fact]
    public async Task MaintenanceDrainsRequestsAndRejectsNewDatabaseOperations()
    {
        var access = new DatabaseAccess(); var request = access.Enter();
        var restore = access.BeginRestore(default);
        Assert.False(restore.IsCompleted);
        Assert.Equal(503, Assert.Throws<AppException>(() => access.Enter()).Status);
        request.Dispose(); await restore;
        Assert.Equal(409, (await Assert.ThrowsAsync<AppException>(() => access.BeginRestore(default))).Status);
        access.EndRestore(); using var resumed = access.Enter();
    }
    [Fact]
    public async Task StartupRecoversInterruptedRestoreBeforeOpeningLedgerAndPreservesGenerationOnNormalRestart()
    {
        var path = Path.Combine(Path.GetTempPath(), $"recovery-{Guid.NewGuid():N}.db");
        try
        {
            string oldGeneration;
            byte[] backup;
            using (var factory = new ApiFactory(path, true))
            using (var client = factory.CreateClient())
            {
                using var response = await client.GetAsync("/api/backups/export");
                oldGeneration = response.Headers.GetValues("X-Ledger-Generation").Single();
                backup = await response.Content.ReadAsByteArrayAsync();
                client.DefaultRequestHeaders.Add("X-Ledger-Generation", oldGeneration);
                var members = (await client.GetFromJsonAsync<MemberDto[]>("/api/members", Json))!;
                (await client.PutAsJsonAsync($"/api/members/{members[0].Id}", new MemberInput { Name = "中斷時的新帳本" }, Json)).EnsureSuccessStatusCode();
            }
            var root = path + ".backups";
            await File.WriteAllBytesAsync(Path.Combine(root, "before-restore-recovery.db"), backup);
            await File.WriteAllTextAsync(Path.Combine(root, "restore-pending.json"), JsonSerializer.Serialize("before-restore-recovery.db"));
            string recoveredGeneration;
            using (var factory = new ApiFactory(path, true))
            using (var client = factory.CreateClient())
            {
                using var response = await client.GetAsync("/api/members"); response.EnsureSuccessStatusCode();
                Assert.Equal("成員一", (await response.Content.ReadFromJsonAsync<MemberDto[]>(Json))![0].Name);
                recoveredGeneration = response.Headers.GetValues("X-Ledger-Generation").Single();
                Assert.NotEqual(oldGeneration, recoveredGeneration);
                Assert.False(File.Exists(Path.Combine(root, "restore-pending.json")));
                client.DefaultRequestHeaders.Add("X-Ledger-Generation", oldGeneration);
                Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/members", new MemberInput { Name = "過期表單" }, Json)).StatusCode);
            }
            using (var factory = new ApiFactory(path, true))
            using (var client = factory.CreateClient())
            {
                using var response = await client.GetAsync("/api/backups");
                Assert.Equal(recoveredGeneration, response.Headers.GetValues("X-Ledger-Generation").Single());
            }
        }
        finally { File.Delete(path); File.Delete(path + "-wal"); File.Delete(path + "-shm"); if (Directory.Exists(path + ".backups")) Directory.Delete(path + ".backups", true); }
    }
    [Fact]
    public async Task ExportIncludesUncheckpointedWalAndRestartDoesNotReseedEmptyRestoredCatalogs()
    {
        var path = Path.Combine(Path.GetTempPath(), $"wal-backup-{Guid.NewGuid():N}.db");
        try
        {
            using (var factory = new ApiFactory(path, true))
            using (var client = factory.CreateClient())
            {
                using (var writer = new SqliteConnection($"Data Source={path};Pooling=False"))
                {
                    writer.Open(); using var command = writer.CreateCommand();
                    command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA wal_autocheckpoint=0; UPDATE Members SET Name='WAL 中的成員' WHERE Id=1"; command.ExecuteNonQuery();
                    Assert.True(new FileInfo(path + "-wal").Length > 0);
                    using var export = await client.GetAsync("/api/backups/export"); export.EnsureSuccessStatusCode();
                    client.DefaultRequestHeaders.Add("X-Ledger-Generation", export.Headers.GetValues("X-Ledger-Generation").Single());
                    using var walUpload = new MultipartFormDataContent(); walUpload.Add(new ByteArrayContent(await export.Content.ReadAsByteArrayAsync()), "file", "wal.db");
                    using var walResult = await client.PostAsync("/api/backups/preview", walUpload); walResult.EnsureSuccessStatusCode();
                    Assert.Contains("WAL 中的成員", (await walResult.Content.ReadFromJsonAsync<BackupPreview>(Json))!.Members);
                }
                foreach (var category in (await client.GetFromJsonAsync<CategoryDto[]>("/api/categories", Json))!) (await client.DeleteAsync($"/api/categories/{category.Id}")).EnsureSuccessStatusCode();
                foreach (var account in (await client.GetFromJsonAsync<AccountDto[]>("/api/accounts", Json))!) (await client.DeleteAsync($"/api/accounts/{account.Id}")).EnsureSuccessStatusCode();
                var bytes = await client.GetByteArrayAsync("/api/backups/export");
                using var upload = new MultipartFormDataContent(); upload.Add(new ByteArrayContent(bytes), "file", "empty.db");
                using var result = await client.PostAsync("/api/backups/preview", upload); result.EnsureSuccessStatusCode();
                var preview = (await result.Content.ReadFromJsonAsync<BackupPreview>(Json))!;
                (await client.PostAsJsonAsync("/api/backups/restore", new RestoreInput(preview.Token, "還原"), Json)).EnsureSuccessStatusCode();
            }
            using (var factory = new ApiFactory(path, true))
            using (var client = factory.CreateClient())
            {
                Assert.Empty((await client.GetFromJsonAsync<CategoryDto[]>("/api/categories", Json))!);
                Assert.Empty((await client.GetFromJsonAsync<AccountDto[]>("/api/accounts", Json))!);
            }
        }
        finally { File.Delete(path); File.Delete(path + "-wal"); File.Delete(path + "-shm"); if (Directory.Exists(path + ".backups")) Directory.Delete(path + ".backups", true); }
    }
}
