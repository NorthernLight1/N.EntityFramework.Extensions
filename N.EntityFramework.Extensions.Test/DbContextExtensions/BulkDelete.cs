using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using N.EntityFramework.Extensions.Test.Data;

namespace N.EntityFramework.Extensions.Test.DbContextExtensions;

[TestClass]
public class BulkDelete : DbContextExtensionsBase
{
    [TestMethod]
    public void With_Default_Options()
    {
        var dbContext = SetupDbContext(true);
        var orders = dbContext.Orders.Where(o => o.Price == 1.25M).ToList();
        int rowsDeleted = dbContext.BulkDelete(orders);
        int newTotal = dbContext.Orders.Where(o => o.Price == 1.25M).Count();

        Assert.IsTrue(orders.Count > 0, "There must be orders in database that match this condition (Price = $1.25)");
        Assert.IsTrue(rowsDeleted == orders.Count, "The number of rows deleted must match the count of existing rows in database");
        Assert.IsTrue(newTotal == 0, "Must be 0 to indicate all records were deleted");
    }
    [TestMethod]
    public void With_Default_Options_Tpc()
    {
        var dbContext = SetupDbContext(true, PopulateDataMode.Tpc);
        var customers = dbContext.TpcPeople.OfType<TpcCustomer>().ToList();
        int rowsDeleted = dbContext.BulkDelete(customers, options => { options.DeleteOnCondition = (s, t) => s.Id == t.Id; });
        var newCustomers = dbContext.TpcPeople.OfType<TpcCustomer>().Count();

        Assert.IsTrue(customers.Count > 0, "There must be tpcCustomer records in database");
        Assert.IsTrue(rowsDeleted == customers.Count, "The number of rows deleted must match the count of existing rows in database");
        Assert.IsTrue(newCustomers == 0, "Must be 0 to indicate all records were deleted");
    }
    [TestMethod]
    public void With_Default_Options_Tph()
    {
        var dbContext = SetupDbContext(true, PopulateDataMode.Tph);
        var customers = dbContext.TphPeople.OfType<TphCustomer>().ToList();
        int rowsDeleted = dbContext.BulkDelete(customers);
        var newCustomers = dbContext.TphPeople.OfType<TphCustomer>().Count();

        Assert.IsTrue(customers.Count > 0, "There must be tphCustomer records in database");
        Assert.IsTrue(rowsDeleted == customers.Count, "The number of rows deleted must match the count of existing rows in database");
        Assert.IsTrue(newCustomers == 0, "Must be 0 to indicate all records were deleted");
    }
    [TestMethod]
    public void With_Options_DeleteOnCondition()
    {
        var dbContext = SetupDbContext(true);
        int oldTotal = dbContext.Orders.Where(o => o.Price == 1.25M).Count();
        var orders = dbContext.Orders.Where(o => o.Price == 1.25M && o.ExternalId != null).ToList();
        int rowsDeleted = dbContext.BulkDelete(orders, options => { options.DeleteOnCondition = (s, t) => s.ExternalId == t.ExternalId; options.UsePermanentTable = true; });
        int newTotal = dbContext.Orders.Where(o => o.Price == 1.25M).Count();

        Assert.IsTrue(orders.Count > 0, "There must be orders in database that match this condition (Price < $2)");
        Assert.IsTrue(rowsDeleted == orders.Count, "The number of rows deleted must match the count of existing rows in database");
        Assert.IsTrue(newTotal == oldTotal - rowsDeleted, "Must be 0 to indicate all records were deleted");
    }
    [TestMethod]
    public void With_Transaction()
    {
        var dbContext = SetupDbContext(true);
        var orders = dbContext.Orders.Where(o => o.Price == 1.25M).ToList();
        int rowsDeleted, newTotal = 0;
        using (var transaction = dbContext.Database.BeginTransaction())
        {
            rowsDeleted = dbContext.BulkDelete(orders);
            newTotal = dbContext.Orders.Where(o => o.Price == 1.25M).Count();
            transaction.Rollback();
        }
        var rollbackTotal = dbContext.Orders.Count(o => o.Price == 1.25M);

        Assert.IsTrue(orders.Count > 0, "There must be orders in database that match this condition (Price < $2)");
        Assert.IsTrue(rowsDeleted == orders.Count, "The number of rows deleted must match the count of existing rows in database");
        Assert.IsTrue(newTotal == 0, "Must be 0 to indicate all records were deleted");
        Assert.IsTrue(rollbackTotal == orders.Count, "The number of rows after the transacation has been rollbacked should match the original count");
    }
}