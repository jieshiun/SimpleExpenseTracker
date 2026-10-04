using Microsoft.EntityFrameworkCore;
using SimpleExpenseTracker.Domain;

namespace SimpleExpenseTracker.Infrastructure;

public sealed class ExpenseDbContext(DbContextOptions<ExpenseDbContext> options) : DbContext(options)
{
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<HouseholdMember> Members => Set<HouseholdMember>();
    public DbSet<RecurringTransaction> RecurringTransactions => Set<RecurringTransaction>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<RecurringTransaction>().Property(r => r.Name).HasMaxLength(100);
        model.Entity<RecurringTransaction>().Property(r => r.Note).HasMaxLength(500);
        model.Entity<RecurringTransaction>().Property(r => r.Amount).HasConversion<string>();
        model.Entity<RecurringTransaction>().Property(r => r.Version).HasDefaultValue(1).IsConcurrencyToken();
        model.Entity<RecurringTransaction>().HasIndex(r => r.ClientRequestId).IsUnique();
        model.Entity<RecurringTransaction>().HasIndex(r => new { r.IsActive, r.NextRunDate });
        model.Entity<RecurringTransaction>().HasOne(r => r.Category).WithMany().HasForeignKey(r => r.CategoryId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<RecurringTransaction>().HasOne(r => r.Account).WithMany().HasForeignKey(r => r.AccountId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<RecurringTransaction>().HasOne(r => r.Member).WithMany().HasForeignKey(r => r.MemberId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Transaction>().HasOne(t => t.RecurringTransaction).WithMany().HasForeignKey(t => t.RecurringTransactionId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Transaction>().HasIndex(t => new { t.RecurringTransactionId, t.RecurringOccurrenceDate }).IsUnique().HasFilter("RecurringTransactionId IS NOT NULL");
        model.Entity<HouseholdMember>().Property(m => m.Name).HasMaxLength(50);
        model.Entity<Transaction>().Property(t => t.Version).HasDefaultValue(1).IsConcurrencyToken();
        model.Entity<Transaction>().HasIndex(t => t.ClientRequestId).IsUnique();
        model.Entity<Transaction>().HasOne(t => t.OwnerMember).WithMany().HasForeignKey(t => t.OwnerMemberId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Transaction>().HasOne(t => t.CreatedBy).WithMany().HasForeignKey(t => t.CreatedById).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Transaction>().HasOne(t => t.UpdatedBy).WithMany().HasForeignKey(t => t.UpdatedById).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Transaction>().HasOne(t => t.DeletedBy).WithMany().HasForeignKey(t => t.DeletedById).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Transaction>().HasOne(t => t.Category).WithMany().HasForeignKey(t => t.CategoryId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Transaction>().HasOne(t => t.Account).WithMany().HasForeignKey(t => t.AccountId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Transaction>().HasIndex(t => t.TransactionDate);
        model.Entity<Category>().Property(c => c.Name).HasMaxLength(50);
        model.Entity<Account>().Property(a => a.Name).HasMaxLength(50);
        model.Entity<Transaction>().Property(t => t.Note).HasMaxLength(500);
        // SQLite stores decimal as TEXT, preserving exact decimal values.
        model.Entity<Transaction>().Property(t => t.Amount).HasConversion<string>();
        model.Entity<Account>().Property(a => a.InitialBalance).HasConversion<string>();
    }
}
