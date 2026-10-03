namespace SimpleExpenseTracker.Domain;

public enum TransactionType { Expense, Income }
public enum AccountType { Cash, Bank, CreditCard, EWallet, Other }

public abstract class Entity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class Category : Entity
{
    public string Name { get; set; } = "";
    public TransactionType Type { get; set; }
    public string Icon { get; set; } = "📌";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Account : Entity
{
    public string Name { get; set; } = "";
    public AccountType Type { get; set; }
    public decimal InitialBalance { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Transaction : Entity
{
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;
    // A local calendar date without timezone conversion. Timestamps above are UTC.
    public DateTime TransactionDate { get; set; } = DateTime.Today;
    public string? Note { get; set; }
}
