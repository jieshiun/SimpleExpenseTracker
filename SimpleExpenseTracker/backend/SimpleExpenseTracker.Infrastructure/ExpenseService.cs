using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SimpleExpenseTracker.Application;
using SimpleExpenseTracker.Domain;

namespace SimpleExpenseTracker.Infrastructure;

public sealed class ExpenseService(ExpenseDbContext db) : IExpenseService
{
    private static AppException Missing() => new(404, "找不到這筆資料，可能已被刪除。");
    private static CategoryDto Map(Category c) => new(c.Id, c.Name, c.Type, c.Icon, c.SortOrder, c.IsActive);
    private static AccountDto Map(Account a) => new(a.Id, a.Name, a.Type, a.InitialBalance, a.IsActive);
    private static TransactionDto Map(Transaction t) => new(t.Id, t.Type, t.Amount, t.CategoryId, t.Category.Name, t.Category.Icon, t.AccountId, t.Account.Name, t.TransactionDate, t.Note, t.CreatedAt, t.UpdatedAt, t.Ownership, t.OwnerMemberId, t.OwnerMember?.Name, t.CreatedById, t.CreatedBy?.Name, t.UpdatedById, t.UpdatedBy?.Name, t.Version, t.IsDeleted, t.DeletedAt, t.DeletedById, t.DeletedBy?.Name, t.RecurringTransactionId, t.RecurringOccurrenceDate);
    private IQueryable<Transaction> Transactions => db.Transactions.Include(t => t.Category).Include(t => t.Account).Include(t => t.OwnerMember).Include(t => t.CreatedBy).Include(t => t.UpdatedBy).Include(t => t.DeletedBy);
    private static AppException Conflict() => new(409, "這筆交易已被更新或刪除，請重新載入最新內容後再操作。");
    private async Task<HouseholdMember> Actor(int id, CancellationToken ct) => await db.Members.SingleOrDefaultAsync(m => m.Id == id && m.IsActive, ct) ?? throw new AppException(400, "請選擇仍啟用的本次操作人。");
    private async Task SaveChanges(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw Conflict(); }
    }

    public async Task<PageDto<TransactionDto>> TransactionsAsync(TransactionQuery q, CancellationToken ct)
    {
        if (q.Month.HasValue && !q.Year.HasValue) throw new AppException(400, "指定月份時也需要指定年份。");
        var rows = Transactions.AsNoTracking().Where(t => t.IsDeleted == q.Deleted);
        rows = FamilyFilter.Apply(rows, q.Ownership, q.OwnerMemberId);
        if (q.Year.HasValue)
        {
            var start = new DateTime(q.Year.Value, q.Month ?? 1, 1);
            var end = q.Month.HasValue ? start.AddMonths(1) : start.AddYears(1);
            rows = rows.Where(t => t.TransactionDate >= start && t.TransactionDate < end);
        }
        if (q.Type.HasValue) rows = rows.Where(t => t.Type == q.Type);
        if (q.CategoryId.HasValue) rows = rows.Where(t => t.CategoryId == q.CategoryId);
        if (q.AccountId.HasValue) rows = rows.Where(t => t.AccountId == q.AccountId);
        var total = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(t => t.TransactionDate).ThenByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToListAsync(ct);
        return new(items.Select(Map).ToArray(), total, q.Page, q.PageSize);
    }
    public async Task<TransactionDto> TransactionAsync(int id, CancellationToken ct) => Map(await Transactions.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct) ?? throw Missing());
    public async Task<TransactionDto> SaveTransactionAsync(int? id, TransactionInput input, CancellationToken ct)
    {
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input with { Version = null }))));
        if (!id.HasValue)
        {
            if (input.ClientRequestId is null || input.ClientRequestId == Guid.Empty) throw new AppException(400, "新增交易需要有效的送出識別碼。");
            var prior = await Transactions.AsNoTracking().SingleOrDefaultAsync(t => t.ClientRequestId == input.ClientRequestId, ct);
            if (prior is not null) return Replay(prior, fingerprint);
        }
        var t = id.HasValue ? await Transactions.SingleOrDefaultAsync(t => t.Id == id.Value, ct) ?? throw Missing() : new Transaction();
        if (id.HasValue && (t.IsDeleted || input.Version != t.Version)) throw Conflict();
        var actor = await Actor(input.OperatorId, ct);
        HouseholdMember? owner = null;
        if (input.Ownership == OwnershipKind.Personal)
        {
            owner = await db.Members.SingleOrDefaultAsync(m => m.Id == input.OwnerMemberId, ct) ?? throw new AppException(400, "請選擇收支歸屬成員。");
            if (!owner.IsActive && (!id.HasValue || t.OwnerMemberId != owner.Id)) throw new AppException(400, "歸屬成員已停用。");
        }
        else if (input.OwnerMemberId is not null || !Enum.IsDefined(input.Ownership) || (input.Ownership == OwnershipKind.Unknown && (!id.HasValue || t.Ownership != OwnershipKind.Unknown)))
            throw new AppException(400, "請選擇個人成員或家庭共同歸屬。");
        if (input.Amount <= 0 || decimal.Round(input.Amount, 2) != input.Amount) throw new AppException(400, "金額必須大於零，最多兩位小數。");
        if (input.TransactionDate is null || input.TransactionDate.Value.Year < 1 || input.TransactionDate.Value.Year > 9998) throw new AppException(400, "請輸入有效交易日期。");
        var category = await db.Categories.FindAsync([input.CategoryId], ct) ?? throw new AppException(400, "分類不存在。");
        var account = await db.Accounts.FindAsync([input.AccountId], ct) ?? throw new AppException(400, "帳戶不存在。");
        if (category.Type != input.Type) throw new AppException(400, "分類與交易的收入／支出類型必須一致。");
        if (!category.IsActive && (!id.HasValue || t.CategoryId != category.Id)) throw new AppException(400, "這個分類已停用，請選擇其他分類。");
        if (!account.IsActive && (!id.HasValue || t.AccountId != account.Id)) throw new AppException(400, "這個帳戶已停用，請選擇其他帳戶。");
        t.Type = input.Type; t.Amount = input.Amount; t.Category = category; t.Account = account;
        t.Ownership = input.Ownership; t.OwnerMember = owner; t.OwnerMemberId = owner?.Id;
        t.UpdatedBy = actor;
        if (id.HasValue) t.Version++;
        else { t.CreatedBy = actor; t.ClientRequestId = input.ClientRequestId; t.CreationHash = fingerprint; }
        t.TransactionDate = DateTime.SpecifyKind(input.TransactionDate.Value.Date, DateTimeKind.Unspecified);
        t.Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim(); t.UpdatedAt = DateTime.UtcNow;
        if (!id.HasValue) db.Transactions.Add(t);
        try { await SaveChanges(ct); }
        catch (DbUpdateException ex) when (!id.HasValue && ex.InnerException is SqliteException { SqliteErrorCode: 19 })
        {
            // The unique request key also protects simultaneous HTTP retries.
            db.ChangeTracker.Clear();
            var prior = await Transactions.AsNoTracking().SingleOrDefaultAsync(t => t.ClientRequestId == input.ClientRequestId, ct);
            if (prior is null) throw;
            return Replay(prior, fingerprint);
        }
        return Map(t);
    }
    private static TransactionDto Replay(Transaction prior, string fingerprint)
    {
        if (prior.CreationHash != fingerprint) throw new AppException(409, "此送出識別碼已用於不同內容；請確認上一筆儲存結果後重新新增。");
        if (prior.IsDeleted) throw Conflict();
        return Map(prior);
    }
    public async Task DeleteTransactionAsync(int id, TransactionAction input, CancellationToken ct)
    {
        var t = await Transactions.SingleOrDefaultAsync(t => t.Id == id, ct) ?? throw Missing();
        if (t.IsDeleted || input.Version != t.Version) throw Conflict();
        var actor = await Actor(input.OperatorId, ct);
        t.IsDeleted = true; t.DeletedAt = DateTime.UtcNow; t.DeletedBy = actor;
        t.UpdatedAt = t.DeletedAt.Value; t.UpdatedBy = actor; t.Version++;
        await SaveChanges(ct);
    }
    public async Task<TransactionDto> RestoreTransactionAsync(int id, TransactionAction input, CancellationToken ct)
    {
        var t = await Transactions.SingleOrDefaultAsync(t => t.Id == id, ct) ?? throw Missing();
        if (!t.IsDeleted || input.Version != t.Version) throw Conflict();
        t.UpdatedBy = await Actor(input.OperatorId, ct); t.UpdatedAt = DateTime.UtcNow;
        t.IsDeleted = false; t.Version++;
        await SaveChanges(ct); return Map(t);
    }
    public async Task<IReadOnlyList<MemberDto>> MembersAsync(CancellationToken ct) => await db.Members.AsNoTracking().OrderBy(m => m.Id).Select(m => new MemberDto(m.Id, m.Name, m.IsActive)).ToArrayAsync(ct);
    public async Task<MemberDto> SaveMemberAsync(int? id, MemberInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) throw new AppException(400, "成員名稱不可空白。");
        var member = id.HasValue ? await db.Members.FindAsync([id.Value], ct) ?? throw Missing() : new HouseholdMember();
        member.Name = input.Name.Trim(); member.IsActive = input.IsActive; member.UpdatedAt = DateTime.UtcNow;
        if (!id.HasValue) db.Members.Add(member);
        await db.SaveChangesAsync(ct); return new(member.Id, member.Name, member.IsActive);
    }
    public async Task<IReadOnlyList<CategoryDto>> CategoriesAsync(CancellationToken ct) => (await db.Categories.AsNoTracking().OrderBy(c => c.Type).ThenBy(c => c.SortOrder).ThenBy(c => c.Id).ToListAsync(ct)).Select(Map).ToArray();
    public async Task<CategoryDto> CategoryAsync(int id, CancellationToken ct) => Map(await db.Categories.FindAsync([id], ct) ?? throw Missing());
    public async Task<CategoryDto> SaveCategoryAsync(int? id, CategoryInput input, CancellationToken ct)
    {
        var c = id.HasValue ? await db.Categories.FindAsync([id.Value], ct) ?? throw Missing() : new Category();
        if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.Icon)) throw new AppException(400, "名稱與圖示不可空白。");
        if (id.HasValue && c.Type != input.Type && (await db.Transactions.AnyAsync(t => t.CategoryId == id, ct) || await db.RecurringTransactions.AnyAsync(r => r.CategoryId == id, ct))) throw new AppException(400, "已使用的分類無法改變收入／支出類型。");
        c.Name = input.Name.Trim(); c.Icon = input.Icon.Trim(); c.Type = input.Type; c.SortOrder = input.SortOrder; c.IsActive = input.IsActive; c.UpdatedAt = DateTime.UtcNow;
        if (!id.HasValue) db.Categories.Add(c);
        await db.SaveChangesAsync(ct); return Map(c);
    }
    public async Task DeleteCategoryAsync(int id, CancellationToken ct)
    {
        var c = await db.Categories.FindAsync([id], ct) ?? throw Missing();
        if (await db.Transactions.AnyAsync(t => t.CategoryId == id, ct) || await db.RecurringTransactions.AnyAsync(r => r.CategoryId == id, ct)) { c.IsActive = false; c.UpdatedAt = DateTime.UtcNow; }
        else db.Categories.Remove(c);
        await db.SaveChangesAsync(ct);
    }
    public async Task<IReadOnlyList<AccountDto>> AccountsAsync(CancellationToken ct) => (await db.Accounts.AsNoTracking().OrderBy(a => a.Id).ToListAsync(ct)).Select(Map).ToArray();
    public async Task<AccountDto> AccountAsync(int id, CancellationToken ct) => Map(await db.Accounts.FindAsync([id], ct) ?? throw Missing());
    public async Task<AccountDto> SaveAccountAsync(int? id, AccountInput input, CancellationToken ct)
    {
        var a = id.HasValue ? await db.Accounts.FindAsync([id.Value], ct) ?? throw Missing() : new Account();
        if (string.IsNullOrWhiteSpace(input.Name)) throw new AppException(400, "名稱不可空白。");
        if (decimal.Round(input.InitialBalance, 2) != input.InitialBalance) throw new AppException(400, "初始金額最多兩位小數。");
        a.Name = input.Name.Trim(); a.Type = input.Type; a.InitialBalance = input.InitialBalance; a.IsActive = input.IsActive; a.UpdatedAt = DateTime.UtcNow;
        if (!id.HasValue) db.Accounts.Add(a);
        await db.SaveChangesAsync(ct); return Map(a);
    }
    public async Task DeleteAccountAsync(int id, CancellationToken ct)
    {
        var a = await db.Accounts.FindAsync([id], ct) ?? throw Missing();
        if (await db.Transactions.AnyAsync(t => t.AccountId == id, ct) || await db.RecurringTransactions.AnyAsync(r => r.AccountId == id, ct)) { a.IsActive = false; a.UpdatedAt = DateTime.UtcNow; }
        else db.Accounts.Remove(a);
        await db.SaveChangesAsync(ct);
    }
}
