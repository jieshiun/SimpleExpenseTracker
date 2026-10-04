namespace SimpleExpenseTracker.Domain;

public sealed class RecurringTransaction : Entity
{
    public string Name { get; set; } = "";
    public TransactionType Type { get; set; }
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;
    public int? MemberId { get; set; }
    public HouseholdMember? Member { get; set; }
    public decimal Amount { get; set; }
    public AmountType AmountType { get; set; }
    public RecurringFrequency Frequency { get; set; }
    public int Interval { get; set; } = 1;
    public int? DayOfMonth { get; set; }
    public DayOfWeek? DayOfWeek { get; set; }
    public int? MonthOfYear { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateOnly? NextRunDate { get; set; }
    public DateOnly? LastGeneratedDate { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Note { get; set; }
    public int Version { get; set; } = 1;
    public Guid? ClientRequestId { get; set; }
    public string? CreationHash { get; set; }
}
