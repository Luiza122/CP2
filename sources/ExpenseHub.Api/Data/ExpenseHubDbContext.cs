using ExpenseHub.Api.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ExpenseHub.Api.Data;

internal sealed class ExpenseHubDbContext : IdentityDbContext<ApplicationUser>
{
    public ExpenseHubDbContext(DbContextOptions<ExpenseHubDbContext> options)
        : base(options)
    {
    }

    public DbSet<Expense> Expenses => Set<Expense>();

    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();

    public DbSet<ExpenseHistory> ExpenseHistories => Set<ExpenseHistory>();

    public DbSet<PaymentRecord> PaymentRecords => Set<PaymentRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Expense>(entity =>
        {
            entity.HasKey(expense => expense.Id);
            entity.Property(expense => expense.Description).HasMaxLength(ExpenseRules.MaximumTextLength).IsRequired();
            entity.Property(expense => expense.OwnerId).IsRequired();
            entity.Property(expense => expense.Amount).HasPrecision(18, 2);
            entity.HasOne(expense => expense.ExpenseCategory)
                .WithMany(category => category.Expenses)
                .HasForeignKey(expense => expense.ExpenseCategoryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ExpenseCategory>(entity =>
        {
            entity.HasKey(category => category.Id);
            entity.Property(category => category.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(category => category.Name).IsUnique();
        });

        builder.Entity<ExpenseHistory>(entity =>
        {
            entity.HasKey(history => history.Id);
            entity.Property(history => history.Action).HasMaxLength(50).IsRequired();
            entity.Property(history => history.ActorUserId).IsRequired();
            entity.Property(history => history.Justification).HasMaxLength(ExpenseRules.MaximumTextLength);
            entity.Property(history => history.Changes).HasMaxLength(500);
            entity.HasOne(history => history.Expense)
                .WithMany(expense => expense.History)
                .HasForeignKey(history => history.ExpenseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PaymentRecord>(entity =>
        {
            entity.HasKey(payment => payment.Id);
            entity.Property(payment => payment.ActorUserId).IsRequired();
            entity.HasIndex(payment => payment.ExpenseId).IsUnique();
            entity.HasOne(payment => payment.Expense)
                .WithOne(expense => expense.PaymentRecord)
                .HasForeignKey<PaymentRecord>(payment => payment.ExpenseId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
