using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace N.EntityFramework.Extensions.Test.DatabaseExtensions
{
    [TestClass]
    public class TruncateTable : DatabaseExtensionsBase
    {
        [TestMethod]
        public void With_TableName()
        {
            var dbContext = SetupDbContext(true);
            int oldOrdersCount = dbContext.Orders.Count();
            dbContext.Database.TruncateTable("Orders");
            int newOrdersCount = dbContext.Orders.Count();

            Assert.IsTrue(oldOrdersCount > 0, "Orders table should have data");
            Assert.IsTrue(newOrdersCount == 0, "Order table should be empty after truncating");
        }
    }
}
