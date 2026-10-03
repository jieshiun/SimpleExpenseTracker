using System.ComponentModel.DataAnnotations;
using SimpleExpenseTracker.Domain;

namespace SimpleExpenseTracker.Application;

public sealed record TransactionInput
{
    [EnumDataType(typeof(TransactionType))] public TransactionType Type { get; init; }
    [Range(typeof(decimal), "0.01", "999999999999.99")] public decimal Amount { get; init; }
    [Range(1, int.MaxValue)] public int CategoryId { get; init; }
    [Range(1, int.MaxValue)] public int AccountId { get; init; }
    [Required] public DateTime? TransactionDate { get; init; } = DateTime.Today;
    [StringLength(500)] public string? Note { get; init; }
}
public sealed record CategoryInput
{
    [Required, StringLength(50)] public string Name { get; init; } = "";
    [EnumDataType(typeof(TransactionType))] public TransactionType Type { get; init; }
    [Required, StringLength(16)] public string Icon { get; init; } = "📌";
    [Range(0, 10000)] public int SortOrder { get; init; }
    public bool IsActive { get; init; } = true;
}
public sealed record AccountInput
{
    [Required, StringLength(50)] public string Name { get; init; } = "";
    [EnumDataType(typeof(AccountType))] public AccountType Type { get; init; }
    [Range(typeof(decimal), "-999999999999.99", "999999999999.99")] public decimal InitialBalance { get; init; }
    public bool IsActive { get; init; } = true;
}
public sealed record TransactionQuery
{
    [Range(1, 9998)] public int? Year { get; init; }
    [Range(1, 12)] public int? Month { get; init; }
    [EnumDataType(typeof(TransactionType))] public TransactionType? Type { get; init; }
    [Range(1, int.MaxValue)] public int? CategoryId { get; init; }
    [Range(1, int.MaxValue)] public int? AccountId { get; init; }
    [Range(1, 1000000)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 50;
}
public sealed record TransactionDto(int Id, TransactionType Type, decimal Amount, int CategoryId, string CategoryName, string CategoryIcon, int AccountId, string AccountName, DateTime TransactionDate, string? Note, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record CategoryDto(int Id, string Name, TransactionType Type, string Icon, int SortOrder, bool IsActive);
public sealed record AccountDto(int Id, string Name, AccountType Type, decimal InitialBalance, bool IsActive);
public sealed record PageDto<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
public sealed class AppException(int status, string message) : Exception(message) { public int Status { get; } = status; }

public interface IExpenseService
{
    Task<PageDto<TransactionDto>> TransactionsAsync(TransactionQuery query, CancellationToken ct);
    Task<TransactionDto> TransactionAsync(int id, CancellationToken ct);
    Task<TransactionDto> SaveTransactionAsync(int? id, TransactionInput input, CancellationToken ct);
    Task DeleteTransactionAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<CategoryDto>> CategoriesAsync(CancellationToken ct);
    Task<CategoryDto> CategoryAsync(int id, CancellationToken ct);
    Task<CategoryDto> SaveCategoryAsync(int? id, CategoryInput input, CancellationToken ct);
    Task DeleteCategoryAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<AccountDto>> AccountsAsync(CancellationToken ct);
    Task<AccountDto> AccountAsync(int id, CancellationToken ct);
    Task<AccountDto> SaveAccountAsync(int? id, AccountInput input, CancellationToken ct);
    Task DeleteAccountAsync(int id, CancellationToken ct);
}
