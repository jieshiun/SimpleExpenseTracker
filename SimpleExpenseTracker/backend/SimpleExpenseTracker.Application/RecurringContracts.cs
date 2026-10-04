using System.ComponentModel.DataAnnotations;
using SimpleExpenseTracker.Domain;

namespace SimpleExpenseTracker.Application;

public sealed record RecurringInput
{
    [Required, StringLength(100)] public string Name { get; init; } = "";
    [EnumDataType(typeof(TransactionType))] public TransactionType Type { get; init; }
    [Range(1, int.MaxValue)] public int CategoryId { get; init; }
    [Range(1, int.MaxValue)] public int AccountId { get; init; }
    [Range(1, int.MaxValue)] public int? MemberId { get; init; }
    [Range(typeof(decimal), "0.01", "999999999999.99")] public decimal Amount { get; init; }
    [EnumDataType(typeof(AmountType))] public AmountType AmountType { get; init; }
    [EnumDataType(typeof(RecurringFrequency))] public RecurringFrequency Frequency { get; init; }
    [Range(1, 120)] public int Interval { get; init; } = 1;
    [Range(1, 31)] public int? DayOfMonth { get; init; }
    [EnumDataType(typeof(DayOfWeek))] public DayOfWeek? DayOfWeek { get; init; }
    [Range(1, 12)] public int? MonthOfYear { get; init; }
    [Required] public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public bool IsActive { get; init; } = true;
    [StringLength(500)] public string? Note { get; init; }
    [Range(1, int.MaxValue)] public int? Version { get; init; }
    public Guid? ClientRequestId { get; init; }
}
public sealed record RecurringQuery
{
    [Range(1, int.MaxValue)] public int? MemberId { get; init; }
    [EnumDataType(typeof(OwnershipKind))] public OwnershipKind? Ownership { get; init; }
    [EnumDataType(typeof(TransactionType))] public TransactionType? Type { get; init; }
    public bool? IsActive { get; init; }
}
public sealed record RecurringStatus
{
    public bool IsActive { get; init; }
    [Range(1, int.MaxValue)] public int Version { get; init; }
}
public sealed record RecurringAction([Range(1, int.MaxValue)] int Version);
public sealed record RecurringDto(int Id, string Name, TransactionType Type, int CategoryId, string CategoryName, string CategoryIcon, int AccountId, string AccountName, int? MemberId, string? MemberName, decimal Amount, AmountType AmountType, RecurringFrequency Frequency, int Interval, int? DayOfMonth, DayOfWeek? DayOfWeek, int? MonthOfYear, DateOnly StartDate, DateOnly? EndDate, DateOnly? NextRunDate, DateOnly? LastGeneratedDate, bool IsActive, string? Note, int Version, string? Warning);
public sealed record RecurringSummaryDto(decimal MonthlyExpense, decimal MonthlyIncome, decimal AnnualExpense, decimal AnnualIncome, int ActiveCount, int Year, int Month);
public sealed record UpcomingDto(int Id, string Name, DateOnly NextRunDate, decimal Amount, TransactionType Type, AmountType AmountType, int? MemberId, string? MemberName);
public sealed record GenerationResult(int Generated, int Duplicates, int Blocked, bool HasMore);
public interface ILocalDateProvider { DateOnly Today { get; } DateTime UtcNow { get; } }
public interface IRecurringTransactionService
{
    Task<IReadOnlyList<RecurringDto>> List(RecurringQuery query, CancellationToken ct);
    Task<RecurringDto> Get(int id, CancellationToken ct);
    Task<RecurringDto> Save(int? id, RecurringInput input, CancellationToken ct);
    Task<RecurringDto> Status(int id, RecurringStatus input, CancellationToken ct);
    Task<RecurringSummaryDto> Summary(RecurringQuery query, int? year, int? month, CancellationToken ct);
    Task<IReadOnlyList<UpcomingDto>> Upcoming(RecurringQuery query, int days, CancellationToken ct);
}
public interface IRecurringTransactionGenerator { Task<GenerationResult> Generate(CancellationToken ct); }
