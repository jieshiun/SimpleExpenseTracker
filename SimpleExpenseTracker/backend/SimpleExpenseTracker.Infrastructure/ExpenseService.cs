using Microsoft.EntityFrameworkCore;
using SimpleExpenseTracker.Application;
using SimpleExpenseTracker.Domain;

namespace SimpleExpenseTracker.Infrastructure;

public sealed class ExpenseService(ExpenseDbContext db) : IExpenseService
{
    private static AppException Missing() => new(404, "找不到這筆資料，可能已被刪除。");
    private static CategoryDto Map(Category c) => new(c.Id, c.Name, c.Type, c.Icon, c.SortOrder, c.IsActive);
    private static AccountDto Map(Account a) => new(a.Id, a.Name, a.Type, a.InitialBalance, a.IsActive);
    private static TransactionDto Map(Transaction t) => new(t.Id, t.Type, t.Amount, t.CategoryId, t.Category.Name, t.Category.Icon, t.AccountId, t.Account.Name, t.TransactionDate, t.Note, t.CreatedAt, t.UpdatedAt);
    private IQueryable<Transaction> Transactions => db.Transactions.Include(t => t.Category).Include(t => t.Account);

    public async Task<PageDto<TransactionDto>> TransactionsAsync(TransactionQuery q, CancellationToken ct)
    {
        if (q.Month.HasValue && !q.Year.HasValue) throw new AppException(400, "指定月份時也需要指定年份。");
        var rows = Transactions.AsNoTracking();
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
    public async Task<TransactionDto> TransactionAsync(int id, CancellationToken ct) => Map(await Transactions.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, ct) ?? throw Missing());
    public async Task<TransactionDto> SaveTransactionAsync(int? id, TransactionInput input, CancellationToken ct)
    {
        var t = id.HasValue ? await db.Transactions.FindAsync([id.Value], ct) ?? throw Missing() : new Transaction();
        if (input.Amount <= 0 || decimal.Round(input.Amount, 2) != input.Amount) throw new AppException(400, "金額必須大於零，最多兩位小數。");
        if (input.TransactionDate is null || input.TransactionDate.Value.Year < 1 || input.TransactionDate.Value.Year > 9998) throw new AppException(400, "請輸入有效交易日期。");
        var category = await db.Categories.FindAsync([input.CategoryId], ct) ?? throw new AppException(400, "分類不存在。");
        var account = await db.Accounts.FindAsync([input.AccountId], ct) ?? throw new AppException(400, "帳戶不存在。");
        if (category.Type != input.Type) throw new AppException(400, "分類與交易的收入／支出類型必須一致。");
        if (!category.IsActive && (!id.HasValue || t.CategoryId != category.Id)) throw new AppException(400, "這個分類已停用，請選擇其他分類。");
        if (!account.IsActive && (!id.HasValue || t.AccountId != account.Id)) throw new AppException(400, "這個帳戶已停用，請選擇其他帳戶。");
        t.Type = input.Type; t.Amount = input.Amount; t.Category = category; t.Account = account;
        t.TransactionDate = DateTime.SpecifyKind(input.TransactionDate.Value.Date, DateTimeKind.Unspecified);
        t.Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim(); t.UpdatedAt = DateTime.UtcNow;
        if (!id.HasValue) db.Transactions.Add(t);
        await db.SaveChangesAsync(ct);
        return Map(t);
    }
    public async Task DeleteTransactionAsync(int id, CancellationToken ct)
    {
        var t = await db.Transactions.FindAsync([id], ct) ?? throw Missing();
        db.Transactions.Remove(t); await db.SaveChangesAsync(ct);
    }
    public async Task<IReadOnlyList<CategoryDto>> CategoriesAsync(CancellationToken ct) => (await db.Categories.AsNoTracking().OrderBy(c => c.Type).ThenBy(c => c.SortOrder).ThenBy(c => c.Id).ToListAsync(ct)).Select(Map).ToArray();
    public async Task<CategoryDto> CategoryAsync(int id, CancellationToken ct) => Map(await db.Categories.FindAsync([id], ct) ?? throw Missing());
    public async Task<CategoryDto> SaveCategoryAsync(int? id, CategoryInput input, CancellationToken ct)
    {
        var c = id.HasValue ? await db.Categories.FindAsync([id.Value], ct) ?? throw Missing() : new Category();
        if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.Icon)) throw new AppException(400, "名稱與圖示不可空白。");
        if (id.HasValue && c.Type != input.Type && await db.Transactions.AnyAsync(t => t.CategoryId == id, ct)) throw new AppException(400, "已使用的分類無法改變收入／支出類型。");
        c.Name = input.Name.Trim(); c.Icon = input.Icon.Trim(); c.Type = input.Type; c.SortOrder = input.SortOrder; c.IsActive = input.IsActive; c.UpdatedAt = DateTime.UtcNow;
        if (!id.HasValue) db.Categories.Add(c);
        await db.SaveChangesAsync(ct); return Map(c);
    }
    public async Task DeleteCategoryAsync(int id, CancellationToken ct)
    {
        var c = await db.Categories.FindAsync([id], ct) ?? throw Missing();
        if (await db.Transactions.AnyAsync(t => t.CategoryId == id, ct)) { c.IsActive = false; c.UpdatedAt = DateTime.UtcNow; }
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
        if (await db.Transactions.AnyAsync(t => t.AccountId == id, ct)) { a.IsActive = false; a.UpdatedAt = DateTime.UtcNow; }
        else db.Accounts.Remove(a);
        await db.SaveChangesAsync(ct);
    }
}
