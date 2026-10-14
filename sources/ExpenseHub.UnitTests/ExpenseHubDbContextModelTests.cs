using System.Linq;
using ExpenseHub.Api.Data;
using ExpenseHub.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

[TestClass]
internal sealed class ExpenseHubDbContextModelTests
{
    [TestMethod]
    internal void Model_ContainsRequiredExpenseEntities()
    {
        using ExpenseHubDbContext context = CreateContext();

        Assert.IsNotNull(context.Model.FindEntityType(typeof(Expense)));
        Assert.IsNotNull(context.Model.FindEntityType(typeof(ExpenseCategory)));
        Assert.IsNotNull(context.Model.FindEntityType(typeof(ExpenseHistory)));
        Assert.IsNotNull(context.Model.FindEntityType(typeof(PaymentRecord)));
    }

    [TestMethod]
    internal void ExpenseMapping_ConfiguresRequiredDescriptionAndMoneyPrecision()
    {
        using ExpenseHubDbContext context = CreateContext();
        Microsoft.EntityFrameworkCore.Metadata.IEntityType? entity =
            context.Model.FindEntityType(typeof(Expense));

        Assert.IsNotNull(entity);
        Assert.AreEqual(500, entity!.FindProperty(nameof(Expense.Description))?.GetMaxLength());
        Assert.AreEqual(18, entity.FindProperty(nameof(Expense.Amount))?.GetPrecision());
        Assert.AreEqual(2, entity.FindProperty(nameof(Expense.Amount))?.GetScale());
        Assert.IsTrue(entity.FindProperty(nameof(Expense.OwnerId))?.IsNullable == false);
    }

    [TestMethod]
    internal void PaymentMapping_EnforcesAtMostOnePaymentPerExpense()
    {
        using ExpenseHubDbContext context = CreateContext();
        Microsoft.EntityFrameworkCore.Metadata.IEntityType? entity =
            context.Model.FindEntityType(typeof(PaymentRecord));

        Assert.IsNotNull(entity);
        Assert.IsTrue(entity!.GetIndexes().Any(index =>
            index.IsUnique && index.Properties.Any(property => property.Name == nameof(PaymentRecord.ExpenseId))));
    }

    private static ExpenseHubDbContext CreateContext()
    {
        DbContextOptions<ExpenseHubDbContext> options =
            new DbContextOptionsBuilder<ExpenseHubDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;

        return new ExpenseHubDbContext(options);
    }
}
