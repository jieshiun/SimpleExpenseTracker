using Microsoft.EntityFrameworkCore;
using SimpleExpenseTracker.Domain;

namespace SimpleExpenseTracker.Infrastructure;

public static class SeedData
{
    public static async Task InitializeAsync(ExpenseDbContext db, CancellationToken ct = default)
    {
        // Seed only an empty database; deleted/renamed defaults must not reappear.
        if (await db.Categories.AnyAsync(ct) || await db.Accounts.AnyAsync(ct)) return;
        string[] expenses = ["餐飲", "交通", "購物", "娛樂", "居家", "水電", "醫療", "教育", "保險", "訂閱", "其他"];
        string[] icons = ["🍜", "🚗", "🛒", "🎮", "🏠", "💡", "🏥", "📚", "🛡️", "📱", "📌"];
        string[] incomes = ["薪資", "獎金", "投資", "兼職", "其他收入"];
        for (var i = 0; i < expenses.Length; i++)
            db.Categories.Add(new Category { Name = expenses[i], Icon = icons[i], Type = TransactionType.Expense, SortOrder = i });
        for (var i = 0; i < incomes.Length; i++)
            db.Categories.Add(new Category { Name = incomes[i], Icon = "💰", Type = TransactionType.Income, SortOrder = i });
        db.Accounts.AddRange(new Account { Name = "現金", Type = AccountType.Cash }, new Account { Name = "銀行帳戶", Type = AccountType.Bank }, new Account { Name = "信用卡", Type = AccountType.CreditCard });
        await db.SaveChangesAsync(ct);
    }
}
