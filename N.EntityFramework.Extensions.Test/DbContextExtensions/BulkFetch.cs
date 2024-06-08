using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using N.EntityFramework.Extensions.Test.Data;

namespace N.EntityFramework.Extensions.Test.DbContextExtensions
{
    [TestClass]
    public class BulkFetch : DbContextExtensionsBase
    {
        [TestMethod]
        public void With_Default_Options()
        {
            var dbContext = SetupDbContext(true);
            var orders = dbContext.Orders.Where(o => o.Price == 1.25M).ToList();
            var fetchedOrders = dbContext.Orders.BulkFetch(orders);
            bool ordersAreMatched = true;

            foreach (var fetchedOrder in fetchedOrders)
            {
                var order = orders.First(o => o.Id == fetchedOrder.Id);
                if (order.ExternalId != fetchedOrder.ExternalId || order.AddedDateTime != fetchedOrder.AddedDateTime || order.ModifiedDateTime != fetchedOrder.ModifiedDateTime)
                {
                    ordersAreMatched = false;
                    break;
                }
            }

            Assert.IsTrue(orders.Count > 0, "There must be orders in database that match this condition (Price = $1.25)");
            Assert.IsTrue(orders.Count == fetchedOrders.Count(), "The number of rows deleted must match the count of existing rows in database");
            Assert.IsTrue(ordersAreMatched, "The orders from BulkFetch() should match what is retrieved from DbContext");
        }
        [TestMethod]
        public void With_IQueryable()
        {
            var dbContext = SetupDbContext(true);
            var orders = dbContext.Orders.Where(o => o.Price <= 10 && o.ExternalId != null);
            var fetchedOrders = dbContext.Orders.BulkFetch(orders, options => { options.IgnoreColumns = o => new { o.ExternalId }; }).ToList();
            int newTotal = dbContext.Orders.Where(o => o.Price <= 10 && o.ExternalId == null).Count();
            bool foundNullExternalId = fetchedOrders.Where(o => o.ExternalId != null).Any();

            Assert.IsTrue(orders.Count() > 0, "There must be orders in the database that match condition (Price <= 10 And ExternalId != null)");
            Assert.IsTrue(orders.Count() == fetchedOrders.Count(), "The number of orders must match the number of fetched orders");
            Assert.IsTrue(!foundNullExternalId, "Fetched orders should not contain any items where ExternalId is null.");
        }
        [TestMethod]
        public void With_Options_IgnoreColumns()
        {
            var dbContext = SetupDbContext(true);
            var orders = dbContext.Orders.Where(o => o.Price <= 10 && o.ExternalId != null).ToList();
            var fetchedOrders = dbContext.Orders.BulkFetch(orders, options => { options.IgnoreColumns = o => new { o.ExternalId }; }).ToList();
            int newTotal = dbContext.Orders.Where(o => o.Price <= 10 && o.ExternalId == null).Count();
            bool foundNullExternalId = fetchedOrders.Where(o => o.ExternalId != null).Any();

            Assert.IsTrue(orders.Count() > 0, "There must be orders in the database that match condition (Price <= 10 And ExternalId != null)");
            Assert.IsTrue(orders.Count() == fetchedOrders.Count(), "The number of orders must match the number of fetched orders");
            Assert.IsTrue(!foundNullExternalId, "Fetched orders should not contain any items where ExternalId is null.");
        }
        //[TestMethod]
        //public void With_Options_InputColumns()
        //{
        //    var dbContext = SetupDbContext(false);
        //    var orders = new List<Order>();
        //    for (int i = 0; i < 20000; i++)
        //    {
        //        orders.Add(new Order { Id = i, ExternalId = i.ToString(), Price = 1.57M, Active = true });
        //    }
        //    int oldTotal = dbContext.Orders.Where(o => o.Price == 1.57M && o.ExternalId == null && o.Active == true).Count();
        //    int rowsInserted = dbContext.BulkInsert(orders, options => { options.UsePermanentTable = true; options.InputColumns = o => new { o.Price, o.Active, o.AddedDateTime }; });
        //    int newTotal = dbContext.Orders.Where(o => o.Price == 1.57M && o.ExternalId == null && o.Active == true).Count();

        //    Assert.IsTrue(rowsInserted == orders.Count, "The number of rows inserted must match the count of order list");
        //    Assert.IsTrue(newTotal - oldTotal == rowsInserted, "The new count minus the old count should match the number of rows inserted.");
        //}
    }
}