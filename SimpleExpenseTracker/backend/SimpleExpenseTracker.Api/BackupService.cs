using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SimpleExpenseTracker.Application;
using SimpleExpenseTracker.Domain;
using SimpleExpenseTracker.Infrastructure;

namespace SimpleExpenseTracker.Api;

public sealed record BackupPreview(string Token, int Transactions, int DeletedTransactions, int Categories, int Accounts, string[] Members, string? FirstDate, string? LastDate, DateTime ExpiresAt);
public sealed record SafetyBackup(string Name, DateTime CreatedAt, long Size);
public sealed record RestoreInput(string Token, string Confirmation);

public sealed class BackupService(IConfiguration config, DatabaseAccess access, ILogger<BackupService> logger)
{
    public const long MaxBytes = 100 * 1024 * 1024;
    private string connectionString = "";
    private string root = "";
    private string[] schema = [];
    private string[] migrations = [];
    public string Generation { get; private set; } = "";
    private string Marker => Path.Combine(root, "restore-pending.json");
    private string GenerationFile => Path.Combine(root, "generation.txt");

    public async Task Initialize(CancellationToken ct = default)
    {
        var settings = new SqliteConnectionStringBuilder(config.GetConnectionString("Default") ?? "Data Source=expense-tracker.db");
        if (settings.Mode == SqliteOpenMode.Memory || settings.DataSource == ":memory:") throw new InvalidOperationException("Backups require a file-backed database.");
        settings.DataSource = Path.GetFullPath(settings.DataSource);
        settings.Pooling = false;
        connectionString = settings.ToString();
        root = settings.DataSource + ".backups";
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "staging"));
        Generation = File.Exists(GenerationFile) ? (await File.ReadAllTextAsync(GenerationFile, ct)).Trim() : Guid.NewGuid().ToString("N");
        if (!Guid.TryParseExact(Generation, "N", out _)) throw new InvalidOperationException("Invalid ledger generation.");
        AtomicWrite(GenerationFile, Generation);
        // A durable marker means the restore never completed. Recover BEFORE migrations/seed.
        if (File.Exists(Marker))
        {
            var name = JsonSerializer.Deserialize<string>(await File.ReadAllTextAsync(Marker, ct))!;
            CopyDatabase(SafetyPath(name), connectionString);
            ChangeGeneration();
            File.Delete(Marker);
            logger.LogWarning("Recovered interrupted database restore");
        }
        foreach (var file in Directory.GetFiles(Path.Combine(root, "staging"))) File.Delete(file);
    }

    public async Task CaptureSchema(CancellationToken ct = default)
    {
        await using var db = await Open(connectionString, ct);
        schema = await ReadSchema(db, ct);
        migrations = await ReadStrings(db, "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId", ct);
    }

    private static async Task<SqliteConnection> Open(string value, CancellationToken ct)
    {
        var db = new SqliteConnection(value);
        try { await db.OpenAsync(ct); return db; }
        catch { await db.DisposeAsync(); throw; }
    }
    private static string FileConnection(string path, bool readOnly = true) => new SqliteConnectionStringBuilder { DataSource = path, Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString();
    private static async Task<string[]> ReadStrings(SqliteConnection db, string sql, CancellationToken ct)
    {
        using var command = db.CreateCommand(); command.CommandText = sql; command.CommandTimeout = 30;
        var items = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) items.Add(reader.GetString(0));
        return items.ToArray();
    }
    private static Task<string[]> ReadSchema(SqliteConnection db, CancellationToken ct) => ReadStrings(db,
        "SELECT type || '|' || name || '|' || tbl_name || '|' || coalesce(sql,'') FROM sqlite_master WHERE name NOT LIKE 'sqlite_%' ORDER BY type,name", ct);

    private static void CopyDatabase(string sourceFile, string destinationConnection)
    {
        using var source = new SqliteConnection(FileConnection(sourceFile)); source.Open();
        using var destination = new SqliteConnection(destinationConnection); destination.Open();
        source.BackupDatabase(destination);
    }
    private void Snapshot(string path)
    {
        var temporary = path + ".tmp";
        try
        {
        using (var source = new SqliteConnection(connectionString))
        using (var destination = new SqliteConnection(FileConnection(temporary, false)))
        {
            source.Open(); destination.Open(); source.BackupDatabase(destination);
            using var command = destination.CreateCommand(); command.CommandText = "PRAGMA journal_mode=DELETE"; command.ExecuteScalar();
        }
        using (var file = new FileStream(temporary, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) file.Flush(true);
        File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static void AtomicWrite(string path, string value)
    {
        var temporary = path + ".tmp";
        using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        { var bytes = System.Text.Encoding.UTF8.GetBytes(value); file.Write(bytes); file.Flush(true); }
        File.Move(temporary, path, true);
    }
    private void ChangeGeneration() { Generation = Guid.NewGuid().ToString("N"); AtomicWrite(GenerationFile, Generation); }
    public void CheckGeneration(string? value)
    {
        if (value != Generation) throw new AppException(409, "帳本已還原或頁面版本過舊，請重新整理整個頁面再操作。");
    }

    public Stream Export()
    {
        var path = Path.Combine(root, "staging", $"export-{Guid.NewGuid():N}.db");
        try { Snapshot(path); return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 65536, FileOptions.DeleteOnClose); }
        catch { if (File.Exists(path)) File.Delete(path); throw; }
    }

    public async Task<BackupPreview> Preview(IFormFile file, CancellationToken ct)
    {
        CleanupExpired();
        if (file.Length < 100 || file.Length > MaxBytes) throw new AppException(400, "請選擇有效的 SQLite 備份，檔案上限為 100 MB。");
        var token = Guid.NewGuid().ToString("N"); var path = StagePath(token);
        try
        {
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { await file.CopyToAsync(output, ct); await output.FlushAsync(ct); }
            return await Validate(path, token, ct);
        }
        catch { if (File.Exists(path)) File.Delete(path); throw; }
    }
    private void CleanupExpired()
    {
        foreach (var path in Directory.GetFiles(Path.Combine(root, "staging"), "*.db"))
            if (File.GetLastWriteTimeUtc(path) < DateTime.UtcNow.AddMinutes(-30))
                try { File.Delete(path); } catch (IOException) { /* An export may still be downloading. */ }
    }
    private string StagePath(string token)
    {
        if (!Guid.TryParseExact(token, "N", out _)) throw new AppException(400, "備份識別碼不正確。");
        return Path.Combine(root, "staging", token + ".db");
    }
    private string SafetyPath(string name)
    {
        if (Path.GetFileName(name) != name || !name.StartsWith("before-restore-", StringComparison.Ordinal) || !name.EndsWith(".db", StringComparison.Ordinal))
            throw new AppException(400, "備份名稱不正確。");
        return Path.Combine(root, name);
    }
    private async Task<BackupPreview> Validate(string path, string token, CancellationToken ct)
    {
        try
        {
            await using var db = await Open(FileConnection(path), ct);
            using (var command = db.CreateCommand()) { command.CommandText = "PRAGMA trusted_schema=OFF"; await command.ExecuteNonQueryAsync(ct); }
            if (!schema.SequenceEqual(await ReadSchema(db, ct)) || !migrations.SequenceEqual(await ReadStrings(db, "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId", ct)))
                throw new AppException(400, "備份資料庫結構或版本不相容，請使用目前版本匯出的完整備份。");
            if (!(await ReadStrings(db, "PRAGMA integrity_check", ct)).SequenceEqual(new[] { "ok" })) throw new AppException(400, "備份資料庫已損壞。");
            using (var command = db.CreateCommand())
            {
                command.CommandText = "PRAGMA foreign_key_check";
                await using var reader = await command.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct)) throw new AppException(400, "備份資料關聯不完整。");
            }
            await using var context = new ExpenseDbContext(new DbContextOptionsBuilder<ExpenseDbContext>().UseSqlite(db).Options);
            // Materialize every application field as well as checking SQLite structure.
            var transactions = await context.Transactions.AsNoTracking().ToListAsync(ct);
            var categories = await context.Categories.AsNoTracking().ToListAsync(ct);
            var accounts = await context.Accounts.AsNoTracking().ToListAsync(ct);
            var members = await context.Members.AsNoTracking().OrderBy(m => m.Id).ToListAsync(ct);
            var categoryTypes = categories.ToDictionary(c => c.Id, c => c.Type);
            if (transactions.Any(t => t.Amount <= 0 || t.Amount > 999999999999.99m || decimal.Round(t.Amount, 2) != t.Amount || !Enum.IsDefined(t.Type) || !Enum.IsDefined(t.Ownership) || t.Version < 1 || t.TransactionDate.Year > 9998 || t.TransactionDate != t.TransactionDate.Date || (t.Note?.Length ?? 0) > 500 || (t.Ownership == OwnershipKind.Personal ? t.OwnerMemberId is null : t.OwnerMemberId is not null) || categoryTypes[t.CategoryId] != t.Type)
                || categories.Any(c => string.IsNullOrWhiteSpace(c.Name) || c.Name.Length > 50 || !Enum.IsDefined(c.Type) || string.IsNullOrWhiteSpace(c.Icon) || c.Icon.Length > 16 || c.SortOrder is < 0 or > 10000)
                || accounts.Any(a => string.IsNullOrWhiteSpace(a.Name) || a.Name.Length > 50 || !Enum.IsDefined(a.Type) || Math.Abs(a.InitialBalance) > 999999999999.99m || decimal.Round(a.InitialBalance, 2) != a.InitialBalance)
                || members.Any(m => string.IsNullOrWhiteSpace(m.Name) || m.Name.Length > 50)) throw new AppException(400, "備份內容不符合帳本資料規則。");
            var dates = transactions.Select(t => t.TransactionDate).Order().ToArray();
            return new(token, transactions.Count, transactions.Count(t => t.IsDeleted), categories.Count, accounts.Count, members.Select(m => m.Name).ToArray(), dates.Length == 0 ? null : dates[0].ToString("yyyy/MM/dd", CultureInfo.InvariantCulture), dates.Length == 0 ? null : dates[^1].ToString("yyyy/MM/dd", CultureInfo.InvariantCulture), File.GetLastWriteTimeUtc(path).AddMinutes(30));
        }
        catch (Exception ex) when (ex is SqliteException or FormatException or InvalidCastException or OverflowException or InvalidOperationException)
        { throw new AppException(400, "無法讀取備份，請選擇目前版本匯出的完整 SQLite 資料庫。"); }
    }

    public async Task<string> Restore(RestoreInput input, string? generation, CancellationToken ct)
    {
        if (input.Confirmation != "還原") throw new AppException(400, "請輸入「還原」確認取代全部帳目。");
        var path = StagePath(input.Token);
        await access.BeginRestore(ct);
        var mayResume = true;
        try
        {
            CheckGeneration(generation);
            if (!File.Exists(path) || File.GetLastWriteTimeUtc(path) < DateTime.UtcNow.AddMinutes(-30)) throw new AppException(400, "備份預覽已過期，請重新選擇檔案。");
            await Validate(path, input.Token, ct);
            var name = $"before-restore-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.db";
            var safety = SafetyPath(name);
            Snapshot(safety);
            AtomicWrite(Marker, JsonSerializer.Serialize(name));
            // Once the marker exists, finish or roll back even if the browser disconnects.
            try
            {
                ChangeGeneration();
                SqliteConnection.ClearAllPools();
                CopyDatabase(path, connectionString);
                await Validate(new SqliteConnectionStringBuilder(connectionString).DataSource, input.Token, CancellationToken.None);
                File.Delete(Marker);
            }
            catch
            {
                try { CopyDatabase(safety, connectionString); ChangeGeneration(); File.Delete(Marker); }
                catch (Exception rollback)
                { mayResume = false; logger.LogError(rollback, "Database rollback failed; maintenance remains active until restart recovery"); }
                throw new AppException(500, mayResume ? "還原未完成，已恢復原帳本。請重新整理頁面。" : "還原未完成，服務已暫停，請重新啟動容器以恢復原帳本。");
            }
            try
            {
                File.Delete(path);
                foreach (var old in ListSafety().Skip(5)) File.Delete(SafetyPath(old.Name));
            }
            catch (IOException ex) { logger.LogWarning(ex, "Restore succeeded; backup cleanup will be retried later"); }
            return name;
        }
        finally { if (mayResume) access.EndRestore(); }
    }
    public SafetyBackup[] ListSafety() => Directory.GetFiles(root, "before-restore-*.db").Select(p => new FileInfo(p))
        .OrderByDescending(f => f.LastWriteTimeUtc).Select(f => new SafetyBackup(f.Name, f.LastWriteTimeUtc, f.Length)).ToArray();
    public Stream DownloadSafety(string name)
    {
        var path = SafetyPath(name);
        if (!File.Exists(path)) throw new AppException(404, "找不到這份自動備份。");
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }
}
