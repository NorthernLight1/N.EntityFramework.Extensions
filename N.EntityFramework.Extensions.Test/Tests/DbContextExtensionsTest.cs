using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using N.EntityFramework.Extensions;
using N.EntityFramework.Extensions.Test.Data;

namespace N.EntityFramework.Extensions.Test.Tests
{
    [TestClass]
    public class DbContextExtensionsTest
    {
        //[TestMethod]
        //public void TestBulkInsert_EF_CustomTable()
        //{
        //    TestDbContext dbContext = new TestDbContext();
        //    var orders = new List<Order>();
        //    for (int i = 0; i < 20000; i++)
        //    {
        //        orders.Add(new Order { Id=i, Price = 1.57M });
        //    }
        //    int oldTotal = dbContext.Orders.Where(o => o.Price <= 10).Count();
        //    int rowsInserted = dbContext.BulkInsert(orders, new BulkInsertOptions<Order> { 
        //        TableName = "[dbo].[Orders3]",
        //        KeepIdentity = true,
        //        InputColumns = (o) => new { o.Id }
        //    });
        //    int newTotal = dbContext.Orders.Where(o => o.Price <= 10).Count();

        //    Assert.IsTrue(rowsInserted == orders.Count, "The number of rows inserted must match the count of order list");
        //    Assert.IsTrue(newTotal - oldTotal == rowsInserted, "The new count minus the old count should match the number of rows inserted.");
        //}
        [TestMethod]
        public void BulkDelete()
        {
            var dbContext = SetupDbContext(true);
            var orders = dbContext.Orders.Where(o => o.Price == 1.25M).ToList();
            int rowsDeleted = dbContext.BulkDelete(orders);
            int newTotal = dbContext.Orders.Where(o => o.Price == 1.25M).Count();

            Assert.IsTrue(orders.Count > 0, "There must be orders in database that match this condition (Price < $2)");
            Assert.IsTrue(rowsDeleted == orders.Count, "The number of rows deleted must match the count of existing rows in database");
            Assert.IsTrue(newTotal == 0, "Must be 0 to indicate all records were deleted");
        }
        [TestMethod]
        public void BulkDelete_Tpc()
        {
            var dbContext = SetupDbContext(true);
            var customers = dbContext.TpcPeople.OfType<TpcCustomer>().ToList();
            int rowsDeleted = dbContext.BulkDelete(customers, options => { options.DeleteOnCondition = (s, t) => s.Id == t.Id; });
            var newCustomers = dbContext.TpcPeople.OfType<TpcCustomer>().Count();

            Assert.IsTrue(customers.Count > 0, "There must be tpcCustomer records in database");
            Assert.IsTrue(rowsDeleted == customers.Count, "The number of rows deleted must match the count of existing rows in database");
            Assert.IsTrue(newCustomers == 0, "Must be 0 to indicate all records were deleted");
        }
        [TestMethod]
        public void BulkDelete_Tph()
        {
            var dbContext = SetupDbContext(true);
            var customers = dbContext.TphPeople.OfType<TphCustomer>().ToList();
            int rowsDeleted = dbContext.BulkDelete(customers);
            var newCustomers = dbContext.TphPeople.OfType<TphCustomer>().Count();

            Assert.IsTrue(customers.Count > 0, "There must be tphCustomer records in database");
            Assert.IsTrue(rowsDeleted == customers.Count, "The number of rows deleted must match the count of existing rows in database");
            Assert.IsTrue(newCustomers == 0, "Must be 0 to indicate all records were deleted");
        }
        [TestMethod]
        public void BulkDelete_Options_DeleteOnCondition()
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
        public void BulkInsert()
        {
            var dbContext = SetupDbContext(false);
            var orders = new List<Order>();
            for (int i = 0; i < 20000; i++)
            {
                orders.Add(new Order { Id = i, Price = 1.57M });
            }
            int oldTotal = dbContext.Orders.Where(o => o.Price <= 10).Count();
            int rowsInserted = dbContext.BulkInsert(orders);
            int newTotal = dbContext.Orders.Where(o => o.Price <= 10).Count();

            Assert.IsTrue(rowsInserted == orders.Count, "The number of rows inserted must match the count of order list");
            Assert.IsTrue(newTotal - oldTotal == rowsInserted, "The new count minus the old count should match the number of rows inserted.");
        }
        [TestMethod]
        public void BulkInsert_Tph()
        {
            var dbContext = SetupDbContext(false);
            var customers = new List<TphCustomer>();
            var vendors = new List<TphVendor>();
            for (int i = 0; i < 20000; i++)
            {
                customers.Add(new TphCustomer { 
                    Id = i, 
                    FirstName = string.Format("John_{0}",i), 
                    LastName = string.Format("Smith_{0}",i),
                    Email = string.Format("john.smith{0}@domain.com", i),
                    Phone = "404-555-1111",
                    AddedDate = DateTime.UtcNow
                });
            }
            for(int i = 20000; i < 30000; i++)
            {
                vendors.Add(new TphVendor
                {
                    Id = i,
                    FirstName = string.Format("Mike_{0}", i),
                    LastName = string.Format("Smith_{0}", i),
                    Phone = "404-555-2222",
                    Email = string.Format("mike.smith{0}@domain.com", i),
                    Url = string.Format("http://domain.com/mike.smith{0}", i)
                });
            }
            int oldTotal = dbContext.TphPeople.Count();
            int customerRowsInserted = dbContext.BulkInsert(customers);
            int vendorRowsInserted = dbContext.BulkInsert(vendors);
            int rowsInserted = customerRowsInserted + vendorRowsInserted;
            int newTotal = dbContext.TphPeople.Count();

            Assert.IsTrue(rowsInserted == customers.Count + vendors.Count, "The number of rows inserted must match the count of order list");
            Assert.IsTrue(newTotal - oldTotal == rowsInserted, "The new count minus the old count should match the number of rows inserted.");
        }
        [TestMethod]
        public void BulkInsert_Tpc()
        {
            var dbContext = SetupDbContext(false);
            var customers = new List<TpcCustomer>();
            var vendors = new List<TpcVendor>();
            for (int i = 0; i < 20000; i++)
            {
                customers.Add(new TpcCustomer
                {
                    Id = i,
                    FirstName = string.Format("John_{0}", i),
                    LastName = string.Format("Smith_{0}", i),
                    Email = string.Format("john.smith{0}@domain.com", i),
                    AddedDate = DateTime.UtcNow
                });
            }
            for (int i = 20000; i < 30000; i++)
            {
                vendors.Add(new TpcVendor
                {
                    Id = i,
                    FirstName = string.Format("Mike_{0}", i),
                    LastName = string.Format("Smith_{0}", i),
                    Email = string.Format("mike.smith{0}@domain.com", i),
                    Url = string.Format("http://domain.com/mike.smith{0}", i)
                });
            }
            int oldTotal = dbContext.TpcPeople.Count();
            int customerRowsInserted = dbContext.BulkInsert(customers);
            int vendorRowsInserted = dbContext.BulkInsert(vendors);
            int rowsInserted = customerRowsInserted + vendorRowsInserted;
            int newTotal = dbContext.TpcPeople.Count();

            Assert.IsTrue(rowsInserted == customers.Count + vendors.Count, "The number of rows inserted must match the count of order list");
            Assert.IsTrue(newTotal - oldTotal == rowsInserted, "The new count minus the old count should match the number of rows inserted.");
        }
        [TestMethod]
        public void BulkInsert_Without_Identity_Column()
        {
            var dbContext = SetupDbContext(true);
            var articles = new List<Article>();
            for (int i = 0; i < 20000; i++)
            {
                articles.Add(new Article { ArticleId = i.ToString(), Price = 1.57M });
            }
            int oldTotal = dbContext.Articles.Where(o => o.Price <= 10).Count();
            int rowsInserted = dbContext.BulkInsert(articles);
            int newTotal = dbContext.Articles.Where(o => o.Price <= 10).Count();

            Assert.IsTrue(rowsInserted == articles.Count, "The number of rows inserted must match the count of order list");
            Assert.IsTrue(newTotal - oldTotal == rowsInserted, "The new count minus the old count should match the number of rows inserted.");
        }
        [TestMethod]
        public void BulkInsert_Options_AutoMapIdentity()
        {

            var dbContext = SetupDbContext(false);
            var orders = new List<Order>();
            for (int i = 0; i < 5000; i++)
            {
                orders.Add(new Order { ExternalId = i.ToString(), Price = ((decimal)i + 0.55M) });
            }
            int rowsAdded = dbContext.BulkInsert(orders, new BulkInsertOptions<Order>
            {
                UsePermanentTable = true
            });
            bool autoMapIdentityMatched = true;
            var ordersInDb = dbContext.Orders.ToList();
            Order order1 = null;
            Order order2 = null;
            foreach (var order in orders)
            {
                order1 = order;
                var orderinDb = ordersInDb.First(o => o.Id == order.Id);
                order2 = orderinDb;
                if (!(orderinDb.ExternalId == order.ExternalId && orderinDb.Price == order.Price))
                {
                    autoMapIdentityMatched = false;
                    break;
                }
            }

            Assert.IsTrue(rowsAdded == orders.Count, "The number of rows inserted must match the count of order list");
            Assert.IsTrue(autoMapIdentityMatched, "The auto mapping of ids of entities that were merged failed to match up");
        }
        [TestMethod]
        public void BulkInsert_Options_KeepIdentity()
        {
            var dbContext = SetupDbContext(false);
            var orders = new List<Order>();
            for (int i = 0; i < 20000; i++)
            {
                orders.Add(new Order { Id = i, Price = 1.57M });
            }
            int oldTotal = dbContext.Orders.Count();
            int rowsInserted = dbContext.BulkInsert(orders, options => { options.KeepIdentity = true; options.BatchSize = 1000; });
            var oldOrders = dbContext.Orders.OrderBy(o => o.Id).ToList();
            var newOrders = dbContext.Orders.OrderBy(o => o.Id).ToList();
            bool allIdentityFieldsMatch = true;
            for (int i = 0; i < 20000; i++)
            {
                if (newOrders[i].Id != oldOrders[i].Id)
                {
                    allIdentityFieldsMatch = false;
                    break;
                }
            }
            try
            {
                int rowsInserted2 = dbContext.BulkInsert(orders, new BulkInsertOptions<Order>()
                {
                    KeepIdentity = true,
                    BatchSize = 1000,
                });
            }
            catch (Exception ex)
            {
                Assert.IsInstanceOfType(ex, typeof(SqlException));
                Assert.IsTrue(ex.Message.StartsWith("Violation of PRIMARY KEY constraint 'PK_dbo.Orders'."));
            }

            Assert.IsTrue(oldTotal == 0, "There should not be any records in the table");
            Assert.IsTrue(rowsInserted == orders.Count, "The number of rows inserted must match the count of order list");
            Assert.IsTrue(allIdentityFieldsMatch, "The identities between the source and the database should match.");
        }
        [TestMethod]
        public void BulkInsert_Options_InsertIfNotExists()
        {
            var dbContext = SetupDbContext(true);
            var orders = new List<Order>();
            long maxId = dbContext.Orders.Max(o => o.Id);
            long expectedRowsInserted = 1000;
            int existingRowsToAdd = 100;
            long startId = maxId - existingRowsToAdd + 1, endId = maxId + expectedRowsInserted + 1;
            for (long i = startId; i < endId; i++)
            {
                orders.Add(new Order { Id = i, Price = 1.57M });
            }

            int oldTotal = dbContext.Orders.Where(o => o.Price <= 10).Count();
            int rowsInserted = dbContext.BulkInsert(orders, new BulkInsertOptions<Order>() { InsertIfNotExists = true });
            int newTotal = dbContext.Orders.Where(o => o.Price <= 10).Count();

            Assert.IsTrue(rowsInserted == expectedRowsInserted, "The number of rows inserted must match the count of order list");
            Assert.IsTrue(newTotal - oldTotal == expectedRowsInserted, "The new count minus the old count should match the number of rows inserted.");
        }
        [TestMethod]
        public void BulkMerge()
        {
            var dbContext = SetupDbContext(true);
            var orders = dbContext.Orders.Where(o => o.Id <= 10000).OrderBy(o => o.Id).ToList();
            int ordersToAdd = 5000;
            int ordersToUpdate = orders.Count;
            foreach (var order in orders)
            {
                order.Price = Convert.ToDecimal(order.Id + .25);
            }
            for (int i = 0; i < ordersToAdd; i++)
            {
                orders.Add(new Order { Id = 100000 + i, Price = 3.55M });
            }
            var result = dbContext.BulkMerge(orders);
            var newOrders = dbContext.Orders.OrderBy(o => o.Id).ToList();
            bool areAddedOrdersMerged = true;
            bool areUpdatedOrdersMerged = true;
            foreach (var newOrder in newOrders.Where(o => o.Id <= 10000).OrderBy(o => o.Id))
            {
                if (newOrder.Price != Convert.ToDecimal(newOrder.Id + .25))
                {
                    areUpdatedOrdersMerged = false;
                    break;
                }
            }
            foreach (var newOrder in newOrders.Where(o => o.Id >= 500000).OrderBy(o => o.Id))
            {
                if (newOrder.Price != 3.55M)
                {
                    areAddedOrdersMerged = false;
                    break;
                }
            }

            Assert.IsTrue(result.RowsAffected == orders.Count(), "The number of rows inserted must match the count of order list");
            Assert.IsTrue(result.RowsUpdated == ordersToUpdate, "The number of rows updated must match");
            Assert.IsTrue(result.RowsInserted == ordersToAdd, "The number of rows added must match");
            Assert.IsTrue(areAddedOrdersMerged, "The orders that were added did not merge correctly");
            Assert.IsTrue(areUpdatedOrdersMerged, "The orders that were updated did not merge correctly");
        }
        [TestMethod]
        public void BulkMerge_Tpc()
        {
            var dbContext = SetupDbContext(true);
            var customers = dbContext.TpcPeople.Where(o => o.Id <= 1000).OfType<TpcCustomer>().ToList();
            int customersToAdd = 5000;
            int customersToUpdate = customers.Count;
            foreach (var customer in customers)
            {
                customer.FirstName = "BulkMerge_Tpc_Update";
            }
            for (int i = 0; i < customersToAdd; i++)
            {
                customers.Add(new TpcCustomer
                {
                    Id = 10000 + i,
                    FirstName = "BulkMerge_Tpc_Add",
                    AddedDate = DateTime.UtcNow
                });
            }
            var result = dbContext.BulkMerge(customers, options => { options.MergeOnCondition = (s, t) => s.Id == t.Id; });
            int customersAdded = dbContext.TpcPeople.Where(o => o.FirstName == "BulkMerge_Tpc_Add").OfType<TpcCustomer>().Count();
            int customersUpdated = dbContext.TpcPeople.Where(o => o.FirstName == "BulkMerge_Tpc_Update").OfType<TpcCustomer>().Count();

            Assert.IsTrue(result.RowsAffected == customers.Count(), "The number of rows inserted must match the count of customer list");
            Assert.IsTrue(result.RowsUpdated == customersToUpdate, "The number of rows updated must match");
            Assert.IsTrue(result.RowsInserted == customersToAdd, "The number of rows added must match");
            Assert.IsTrue(customersToAdd == customersAdded, "The custmoers that were added did not merge correctly");
            Assert.IsTrue(customersToUpdate == customersUpdated, "The customers that were updated did not merge correctly");
        }
        [TestMethod]
        public void BulkMerge_Tph()
        {
            var dbContext = SetupDbContext(true);
            var customers = dbContext.TphPeople.Where(o => o.Id <= 1000).OfType<TphCustomer>().ToList();
            int customersToAdd = 5000;
            int customersToUpdate = customers.Count;
            foreach (var customer in customers)
            {
                customer.FirstName = "BulkMerge_Tph_Update";
            }
            for (int i = 0; i < customersToAdd; i++)
            {
                customers.Add(new TphCustomer
                {
                    Id = 10000 + i,
                    FirstName = "BulkMerge_Tph_Add",
                    AddedDate = DateTime.UtcNow
                });
            }
            var result = dbContext.BulkMerge(customers);
            int customersAdded = dbContext.TphPeople.Where(o => o.FirstName == "BulkMerge_Tph_Add").OfType<TphCustomer>().Count();
            int customersUpdated = dbContext.TphPeople.Where(o => o.FirstName == "BulkMerge_Tph_Update").OfType<TphCustomer>().Count();

            Assert.IsTrue(result.RowsAffected == customers.Count(), "The number of rows inserted must match the count of customer list");
            Assert.IsTrue(result.RowsUpdated == customersToUpdate, "The number of rows updated must match");
            Assert.IsTrue(result.RowsInserted == customersToAdd, "The number of rows added must match");
            Assert.IsTrue(customersToAdd == customersAdded, "The custmoers that were added did not merge correctly");
            Assert.IsTrue(customersToUpdate == customersUpdated, "The customers that were updated did not merge correctly");
        }
        [TestMethod]
        public void BulkMerge_Options_MergeOnCondition()
        {
            var dbContext = SetupDbContext(true);
            var orders = dbContext.Orders.Where(o => o.Id <= 100 && o.ExternalId != null).OrderBy(o => o.Id).ToList();
            int ordersToAdd = 50;
            int ordersToUpdate = orders.Count;
            foreach (var order in orders)
            {
                order.Price = Convert.ToDecimal(order.Id + .25);
            }
            for (int i = 0; i < ordersToAdd; i++)
            {
                orders.Add(new Order { Id = 100000 + i, Price = 3.55M });
            }
            var result = dbContext.BulkMerge(orders, options => { options.MergeOnCondition = (s, t) => s.ExternalId == t.ExternalId; options.BatchSize = 1000; });
            var newOrders = dbContext.Orders.OrderBy(o => o.Id).ToList();
            bool areAddedOrdersMerged = true;
            bool areUpdatedOrdersMerged = true;
            foreach (var newOrder in newOrders.Where(o => o.Id <= 100 && o.ExternalId != null).OrderBy(o => o.Id))
            {
                if (newOrder.Price != Convert.ToDecimal(newOrder.Id + .25))
                {
                    areUpdatedOrdersMerged = false;
                    break;
                }
            }
            foreach (var newOrder in newOrders.Where(o => o.Id >= 500000).OrderBy(o => o.Id))
            {
                if (newOrder.Price != 3.55M)
                {
                    areAddedOrdersMerged = false;
                    break;
                }
            }

            Assert.IsTrue(result.RowsAffected == orders.Count(), "The number of rows inserted must match the count of order list");
            Assert.IsTrue(result.RowsUpdated == ordersToUpdate, "The number of rows updated must match");
            Assert.IsTrue(result.RowsInserted == ordersToAdd, "The number of rows added must match");
            Assert.IsTrue(areAddedOrdersMerged, "The orders that were added did not merge correctly");
            Assert.IsTrue(areUpdatedOrdersMerged, "The orders that were updated did not merge correctly");
        }
        [TestMethod]
        public void BulkMerge_Options_AutoMapIdentity()
        {
            var dbContext = SetupDbContext(true);
            int ordersToUpdate = 3;
            int ordersToAdd = 2;
            var orders = new List<Order>
            {
                new Order { ExternalId = "id-1", Price=7.10M },
                new Order { ExternalId = "id-2", Price=9.33M },
                new Order { ExternalId = "id-3", Price=3.25M },
                new Order { ExternalId = "id-1000001", Price=2.15M },
                new Order { ExternalId = "id-1000002", Price=5.75M },
            };
            var result = dbContext.BulkMerge(orders, new BulkMergeOptions<Order>
            {
                MergeOnCondition = (s, t) => s.ExternalId == t.ExternalId,
                UsePermanentTable = true
            });
            bool autoMapIdentityMatched = true;
            foreach (var order in orders)
            {
                if (!dbContext.Orders.Any(o => o.ExternalId == order.ExternalId && o.Id == order.Id && o.Price == order.Price))
                {
                    autoMapIdentityMatched = false;
                    break;
                }
            }

            Assert.IsTrue(result.RowsAffected == ordersToAdd + ordersToUpdate, "The number of rows inserted must match the count of order list");
            Assert.IsTrue(result.RowsUpdated == ordersToUpdate, "The number of rows updated must match");
            Assert.IsTrue(result.RowsInserted == ordersToAdd, "The number of rows added must match");
            Assert.IsTrue(autoMapIdentityMatched, "The auto mapping of ids of entities that were merged failed to match up");
        }
        [TestMethod]
        public void BulkSync()
        {
            var dbContext = SetupDbContext(true);
            int oldTotal = dbContext.Orders.Count();
            var orders = dbContext.Orders.Where(o => o.Id <= 10000).OrderBy(o => o.Id).ToList();
            int ordersToAdd = 5000;
            int ordersToUpdate = orders.Count;
            foreach (var order in orders)
            {
                order.Price = Convert.ToDecimal(order.Id + .25);
            }
            for (int i = 0; i < ordersToAdd; i++)
            {
                orders.Add(new Order { Id = 100000 + i, Price = 3.55M });
            }
            var result = dbContext.BulkSync(orders);
            var newOrders = dbContext.Orders.OrderBy(o => o.Id).ToList();
            bool areAddedOrdersMerged = true;
            bool areUpdatedOrdersMerged = true;
            foreach (var newOrder in newOrders.Where(o => o.Id <= 10000).OrderBy(o => o.Id))
            {
                if (newOrder.Price != Convert.ToDecimal(newOrder.Id + .25))
                {
                    areUpdatedOrdersMerged = false;
                    break;
                }
            }
            foreach (var newOrder in newOrders.Where(o => o.Id >= 500000).OrderBy(o => o.Id))
            {
                if (newOrder.Price != 3.55M)
                {
                    areAddedOrdersMerged = false;
                    break;
                }
            }

            Assert.IsTrue(result.RowsAffected == oldTotal + ordersToAdd, "The number of rows inserted must match the count of order list");
            Assert.IsTrue(result.RowsUpdated == ordersToUpdate, "The number of rows updated must match");
            Assert.IsTrue(result.RowsInserted == ordersToAdd, "The number of rows added must match");
            Assert.IsTrue(result.RowsDeleted == oldTotal - orders.Count() + ordersToAdd, "The number of rows deleted must match the difference from the total existing orders to the new orders to add/update");
            Assert.IsTrue(areAddedOrdersMerged, "The orders that were added did not merge correctly");
            Assert.IsTrue(areUpdatedOrdersMerged, "The orders that were updated did not merge correctly");
        }
        [TestMethod]
        public void BulkSync_Options_AutoMapIdentity()
        {
            var dbContext = SetupDbContext(true);
            int oldTotal = dbContext.Orders.Count();
            int ordersToUpdate = 3;
            int ordersToAdd = 2;
            var orders = new List<Order>
            {
                new Order { ExternalId = "id-1", Price=7.10M },
                new Order { ExternalId = "id-2", Price=9.33M },
                new Order { ExternalId = "id-3", Price=3.25M },
                new Order { ExternalId = "id-1000001", Price=2.15M },
                new Order { ExternalId = "id-1000002", Price=5.75M },
            };
            var result = dbContext.BulkSync(orders, options => { options.MergeOnCondition = (s, t) => s.ExternalId == t.ExternalId; options.UsePermanentTable = true; });
            bool autoMapIdentityMatched = true;
            foreach (var order in orders)
            {
                if (!dbContext.Orders.Any(o => o.ExternalId == order.ExternalId && o.Id == order.Id && o.Price == order.Price))
                {
                    autoMapIdentityMatched = false;
                    break;
                }
            }

            Assert.IsTrue(result.RowsAffected == oldTotal + ordersToAdd, "The number of rows inserted must match the count of order list");
            Assert.IsTrue(result.RowsUpdated == ordersToUpdate, "The number of rows updated must match");
            Assert.IsTrue(result.RowsInserted == ordersToAdd, "The number of rows added must match");
            Assert.IsTrue(result.RowsDeleted == oldTotal - orders.Count() + ordersToAdd, "The number of rows deleted must match the difference from the total existing orders to the new orders to add/update");
            Assert.IsTrue(autoMapIdentityMatched, "The auto mapping of ids of entities that were merged failed to match up");
        }
        [TestMethod]
        public void BulkSync_Options_MergeOnCondition()
        {
            var dbContext = SetupDbContext(true);
            int oldTotal = dbContext.Orders.Count();
            var orders = dbContext.Orders.Where(o => o.Id <= 100 && o.ExternalId != null).OrderBy(o => o.Id).ToList();
            int ordersToAdd = 50;
            int ordersToUpdate = orders.Count;
            foreach (var order in orders)
            {
                order.Price = Convert.ToDecimal(order.Id + .25);
            }
            for (int i = 0; i < ordersToAdd; i++)
            {
                orders.Add(new Order { Id = 100000 + i, Price = 3.55M });
            }
            var result = dbContext.BulkSync(orders, new BulkSyncOptions<Order>
            {
                MergeOnCondition = (s, t) => s.ExternalId == t.ExternalId,
                BatchSize = 1000
            });
            var newOrders = dbContext.Orders.OrderBy(o => o.Id).ToList();
            bool areAddedOrdersMerged = true;
            bool areUpdatedOrdersMerged = true;
            foreach (var newOrder in newOrders.Where(o => o.Id <= 100 && o.ExternalId != null).OrderBy(o => o.Id))
            {
                if (newOrder.Price != Convert.ToDecimal(newOrder.Id + .25))
                {
                    areUpdatedOrdersMerged = false;
                    break;
                }
            }
            foreach (var newOrder in newOrders.Where(o => o.Id >= 500000).OrderBy(o => o.Id))
            {
                if (newOrder.Price != 3.55M)
                {
                    areAddedOrdersMerged = false;
                    break;
                }
            }

            Assert.IsTrue(result.RowsAffected == oldTotal + ordersToAdd, "The number of rows inserted must match the count of order list");
            Assert.IsTrue(result.RowsUpdated == ordersToUpdate, "The number of rows updated must match");
            Assert.IsTrue(result.RowsInserted == ordersToAdd, "The number of rows added must match");
            Assert.IsTrue(result.RowsDeleted == oldTotal - orders.Count() + ordersToAdd, "The number of rows deleted must match the difference from the total existing orders to the new orders to add/update");
            Assert.IsTrue(areAddedOrdersMerged, "The orders that were added did not merge correctly");
            Assert.IsTrue(areUpdatedOrdersMerged, "The orders that were updated did not merge correctly");
        }
        [TestMethod]
        public void BulkUpdate()
        {
            var dbContext = SetupDbContext(true);
            var orders = dbContext.Orders.Where(o => o.Price == 1.25M).OrderBy(o => o.Id).ToList();
            long maxId = 0;
            foreach (var order in orders)
            {
                order.Price = 2.35M;
                maxId = order.Id;
            }
            int rowsUpdated = dbContext.BulkUpdate(orders);
            var newOrders = dbContext.Orders.Where(o => o.Price == 2.35M).OrderBy(o => o.Id).Count();
            int entitiesWithChanges = dbContext.ChangeTracker.Entries().Where(t => t.State == EntityState.Modified).Count();

            Assert.IsTrue(orders.Count > 0, "There must be orders in database that match this condition (Price = $1.25)");
            Assert.IsTrue(rowsUpdated == orders.Count, "The number of rows updated must match the count of entities that were retrieved");
            Assert.IsTrue(newOrders == rowsUpdated, "The count of new orders must be equal the number of rows updated in the database.");
            //Assert.IsTrue(entitiesWithChanges == 0, "There should be no pending Order entities with changes after BulkInsert completes");
        }
        [TestMethod]
        public void BulkUpdate_Tpc()
        {
            var dbContext = SetupDbContext(true);
            var customers = dbContext.TpcPeople.Where(o => o.LastName != "BulkUpdateTest").OfType<TpcCustomer>().ToList();
            var vendors = dbContext.TpcPeople.OfType<TpcVendor>().ToList();
            foreach (var customer in customers)
            {
                customer.FirstName = string.Format("Id={0}", customer.Id);
                customer.LastName = "BulkUpdate_Tpc";
            }
            int rowsUpdated = dbContext.BulkUpdate(customers, options => { options.UpdateOnCondition = (s, t) => s.Id == t.Id; });
            var newCustomers = dbContext.TpcPeople.Where(o => o.LastName == "BulkUpdate_Tpc").OfType<TpcCustomer>().Count();
            int entitiesWithChanges = dbContext.ChangeTracker.Entries().Where(t => t.State == EntityState.Modified).Count();

            Assert.IsTrue(vendors.Count > 0 && vendors.Count != customers.Count, "There should be vendor records in the database");
            Assert.IsTrue(customers.Count > 0, "There must be customers in database that match this condition (Price = $1.25)");
            Assert.IsTrue(rowsUpdated == customers.Count, "The number of rows updated must match the count of entities that were retrieved");
            Assert.IsTrue(newCustomers == rowsUpdated, "The count of new customers must be equal the number of rows updated in the database.");
        }
        [TestMethod]
        public void BulkUpdate_Tph()
        {
            var dbContext = SetupDbContext(true);
            var customers = dbContext.TphPeople.Where(o => o.LastName != "BulkUpdateTest").OfType<TphCustomer>().ToList();
            var vendors = dbContext.TphPeople.OfType<TphVendor>().ToList();
            foreach (var customer in customers)
            {
                customer.FirstName = string.Format("Id={0}", customer.Id);
                customer.LastName = "BulkUpdateTest";
            }
            int rowsUpdated = dbContext.BulkUpdate(customers);
            var newCustomers = dbContext.TphPeople.Where(o => o.LastName == "BulkUpdateTest").OrderBy(o => o.Id).Count();
            int entitiesWithChanges = dbContext.ChangeTracker.Entries().Where(t => t.State == EntityState.Modified).Count();

            Assert.IsTrue(vendors.Count > 0 && vendors.Count != customers.Count, "There should be vendor records in the database");
            Assert.IsTrue(customers.Count > 0, "There must be customers in database that match this condition (Price = $1.25)");
            Assert.IsTrue(rowsUpdated == customers.Count, "The number of rows updated must match the count of entities that were retrieved");
            Assert.IsTrue(newCustomers == rowsUpdated, "The count of new customers must be equal the number of rows updated in the database.");
        }
        [TestMethod]
        public void BulkUpdate_Options_UpdateOnCondition()
        {
            var dbContext = SetupDbContext(true);
            var orders = dbContext.Orders.Where(o => o.Price == 1.25M && o.ExternalId != null).OrderBy(o => o.Id).ToList();
            foreach (var order in orders)
            {
                order.Price = 2.35M;
            }
            var oldTotal = dbContext.Orders.Where(o => o.Price == 2.35M).OrderBy(o => o.Id).Count();
            int rowsUpdated = dbContext.BulkUpdate(orders, options => { options.UpdateOnCondition = (s, t) => s.ExternalId == t.ExternalId; });
            var newTotal = dbContext.Orders.Where(o => o.Price == 2.35M).OrderBy(o => o.Id).Count();

            Assert.IsTrue(orders.Count > 0, "There must be orders in database that match this condition (Price = $1.25)");
            Assert.IsTrue(rowsUpdated == orders.Count, "The number of rows updated must match the count of entities that were retrieved");
            Assert.IsTrue(newTotal == rowsUpdated + oldTotal, "The count of new orders must be equal the number of rows updated in the database.");
            //Assert.IsTrue(entitiesWithChanges == 0, "There should be no pending Order entities with changes after BulkInsert completes");
        }
        [TestMethod]
        public void DeleteFromQuery_IQuerable()
        {
            var dbContext = SetupDbContext(true);
            int oldTotal = dbContext.Orders.Where(o => o.Price <= 10).Count();
            int rowsDeleted = dbContext.Orders.Where(o => o.Price <= 10).DeleteFromQuery();
            int newTotal = dbContext.Orders.Where(o => o.Price <= 10).Count();

            Assert.IsTrue(oldTotal > 0, "There must be orders in database that match this condition");
            Assert.IsTrue(rowsDeleted == oldTotal, "The number of rows deleted must match the count of existing rows in database");
            Assert.IsTrue(newTotal == 0, "Delete() Failed: must be 0 to indicate all records were delted");
        }
        [TestMethod]
        public void DeleteFromQuery_IEnumerable()
        {
            var dbContext = SetupDbContext(true);
            int oldTotal = dbContext.Orders.Count();
            int rowsDeleted = dbContext.Orders.DeleteFromQuery();
            int newTotal = dbContext.Orders.Count();

            Assert.IsTrue(oldTotal > 0, "There must be orders in database that match this condition");
            Assert.IsTrue(rowsDeleted == oldTotal, "The number of rows deleted must match the count of existing rows in database");
            Assert.IsTrue(newTotal == 0, "The new count must be 0 to indicate all records were deleted");
        }
        [TestMethod]
        public void DeleteFromQuery_With_DateTime()
        {
            var dbContext = SetupDbContext(true);
            int oldTotal = dbContext.Orders.Count();
            DateTime dateTime = dbContext.Orders.Max(o => o.AddedDateTime).AddDays(-30);
            int rowsToDelete = dbContext.Orders.Where(o => o.ModifiedDateTime != null && o.ModifiedDateTime >= dateTime).Count();
            int rowsDeleted = dbContext.Orders.Where(o => o.ModifiedDateTime != null && o.ModifiedDateTime >= dateTime)
                .DeleteFromQuery();
            int newTotal = dbContext.Orders.Count();

            Assert.IsTrue(oldTotal > 0, "There must be orders in database that match this condition");
            Assert.IsTrue(rowsDeleted == rowsToDelete, "The number of rows deleted must match the count of the rows that matched in the database");
            Assert.IsTrue(oldTotal - newTotal == rowsDeleted, "The rows deleted must match the new count minues the old count");
        }
        [TestMethod]
        public void DeleteFromQuery_With_DifferentValues()
        {
            var dbContext = SetupDbContext(true);
            int oldTotal = dbContext.Orders.Count();
            DateTime dateTime = dbContext.Orders.Max(o => o.AddedDateTime).AddDays(-30);
            var orders = dbContext.Orders.Where(o => o.Id == 1 && o.Active && o.ModifiedDateTime >= dateTime);
            int rowsToDelete = orders.Count();
            int rowsDeleted = orders.DeleteFromQuery();
            int newTotal = dbContext.Orders.Count();

            Assert.IsTrue(oldTotal > 0, "There must be orders in database that match this condition");
            Assert.IsTrue(rowsDeleted == rowsToDelete, "The number of rows deleted must match the count of the rows that matched in the database");
            Assert.IsTrue(oldTotal - newTotal == rowsDeleted, "The rows deleted must match the new count minues the old count");
        }
        [TestMethod]
        public void Fetch()
        {
            var dbContext = SetupDbContext(true);
            int batchSize = 1000;
            int batchCount = 0;
            int totalCount = 0;
            int expectedTotalCount = dbContext.Orders.Where(o => o.Price < 10M).Count();
            int expectedBatchCount = (int)Math.Ceiling(expectedTotalCount / (decimal)batchSize);

            dbContext.Orders.Where(o => o.Price < 10M).Fetch(result =>
            {
                batchCount++;
                totalCount += result.Results.Count();
            }, options => { options.BatchSize = 1000; });

            Assert.IsTrue(expectedTotalCount > 0, "There must be orders in database that match this condition");
            Assert.IsTrue(expectedTotalCount == totalCount, "The total number of rows fetched must match the count of existing rows in database");
            Assert.IsTrue(expectedBatchCount == batchCount, "The total number of batches fetched must match what is expected");
        }
        [TestMethod]
        public void InsertFromQuery()
        {
            var dbContext = SetupDbContext(true);
            string tableName = "OrdersUnderTen";
            int oldSourceTotal = dbContext.Orders.Where(o => o.Price < 10M).Count();
            //int oldTargetTotal = dbContext.Orders.Where(o => o.Price < 10M).UsingTable(tableName).Count();
            int rowsInserted = dbContext.Orders.Where(o => o.Price < 10M).InsertFromQuery(tableName, o => new { o.Price, o.Id, o.AddedDateTime });
            int newSourceTotal = dbContext.Orders.Where(o => o.Price < 10M).Count();
            int newTargetTotal = dbContext.Orders.Where(o => o.Price < 10M).UsingTable(tableName).Count();

            Assert.IsTrue(oldSourceTotal > 0, "There should be existing data in the source table");
            Assert.IsTrue(oldSourceTotal == newSourceTotal, "There should not be any change in the count of rows in the source table");
            Assert.IsTrue(rowsInserted == oldSourceTotal, "The number of records inserted  must match the count of the source table");
            //Assert.IsTrue(rowsInserted == newTargetTotal, "The different in count in the target table before and after the insert must match the total row inserted");
        }
        [TestMethod]
        public void InsertFromQuery_With_DateTime()
        {
            var dbContext = SetupDbContext(true);
            string tableName = "OrdersLast30Days";
            DateTime dateTime = dbContext.Orders.Max(o => o.AddedDateTime).AddDays(-30);
            int oldTotal = dbContext.Orders.Count();
            int oldSourceTotal = dbContext.Orders.Where(o => o.AddedDateTime >= dateTime).Count();
            //int oldTargetTotal = dbContext.Orders.Where(o => o.Price < 10M).UsingTable(tableName).Count();
            int rowsInserted = dbContext.Orders.Where(o => o.AddedDateTime >= dateTime).InsertFromQuery(tableName, 
                o => new { o.Id, o.ExternalId, o.Price, o.AddedDateTime, o.ModifiedDateTime });
            int newSourceTotal = dbContext.Orders.Where(o => o.AddedDateTime >= dateTime).Count();
            int newTargetTotal = dbContext.Orders.Where(o => o.AddedDateTime >= dateTime).UsingTable(tableName).Count();

            Assert.IsTrue(oldTotal > oldSourceTotal, "The total should be greater then the number of rows selected from the source table");
            Assert.IsTrue(oldSourceTotal > 0, "There should be existing data in the source table");
            Assert.IsTrue(oldSourceTotal == newSourceTotal, "There should not be any change in the count of rows in the source table");
            Assert.IsTrue(rowsInserted == oldSourceTotal, "The number of records inserted  must match the count of the source table");
            //Assert.IsTrue(rowsInserted == newTargetTotal, "The different in count in the target table before and after the insert must match the total row inserted");
        }
        [TestMethod]
        public void QueryToCsvFile()
        {
            var dbContext = SetupDbContext(true);
            var query = dbContext.Orders.Where(o => o.Price < 10M);
            int count = query.Count();
            var queryToCsvFileResult = query.QueryToCsvFile("QueryToCsvFile-Test.csv");

            Assert.IsTrue(count > 0, "There should be existing data in the source table");
            Assert.IsTrue(queryToCsvFileResult.DataRowCount == count, "The number of data rows written to the file should match the count from the database");
            Assert.IsTrue(queryToCsvFileResult.TotalRowCount == count + 1, "The total number of rows written to the file should match the count from the database plus the header row");
        }
        [TestMethod]
        public void QueryToCsvFile_Options_ColumnDelimiter_TextQualifer_HeaderRow()
        {
            var dbContext = SetupDbContext(true);
            var query = dbContext.Orders.Where(o => o.Price < 10M);
            int count = query.Count();
            var queryToCsvFileResult = query.QueryToCsvFile("QueryToCsvFile_Options_ColumnDelimiter_TextQualifer_HeaderRow-Test.csv", options => { options.ColumnDelimiter = "|"; options.TextQualifer = "\""; options.IncludeHeaderRow = false; });

            Assert.IsTrue(count > 0, "There should be existing data in the source table");
            Assert.IsTrue(queryToCsvFileResult.DataRowCount == count, "The number of data rows written to the file should match the count from the database");
            Assert.IsTrue(queryToCsvFileResult.TotalRowCount == count, "The total number of rows written to the file should match the count from the database without any header row");
        }
        [TestMethod]
        public void QueryToCsvFile_FileStream()
        {
            var dbContext = SetupDbContext(true);
            var query = dbContext.Orders.Where(o => o.Price < 10M);
            int count = query.Count();
            var fileStream = File.Create("QueryToCsvFile_Stream-Test.csv");
            var queryToCsvFileResult = query.QueryToCsvFile(fileStream);

            Assert.IsTrue(count > 0, "There should be existing data in the source table");
            Assert.IsTrue(queryToCsvFileResult.DataRowCount == count, "The number of data rows written to the file should match the count from the database");
            Assert.IsTrue(queryToCsvFileResult.TotalRowCount == count + 1, "The total number of rows written to the file should match the count from the database plus the header row");
        }
        [TestMethod]
        public void SqlQueryToCsvFile()
        {
            var dbContext = SetupDbContext(true);
            int count = dbContext.Orders.Where(o => o.Price > 5M).Count();
            var queryToCsvFileResult = dbContext.Database.SqlQueryToCsvFile("SqlQueryToCsvFile-Test.csv", "SELECT * FROM Orders WHERE Price > @Price", new SqlParameter("@Price", 5M));

            Assert.IsTrue(count > 0, "There should be existing data in the source table");
            Assert.IsTrue(queryToCsvFileResult.DataRowCount == count, "The number of data rows written to the file should match the count from the database");
            Assert.IsTrue(queryToCsvFileResult.TotalRowCount == count + 1, "The total number of rows written to the file should match the count from the database plus the header row");
        }
        [TestMethod]
        public void SqlQueryToCsvFile_Options_ColumnDelimiter_TextQualifer()
        {
            var dbContext = SetupDbContext(true);
            string filePath = "SqlQueryToCsvFile_Options_ColumnDelimiter_TextQualifer-Test.csv";
            int count = dbContext.Orders.Where(o => o.Price > 5M).Count();
            dbContext.Database.SqlQuery<object>("SELECT * FROM Orders WHERE Price > @Price", new SqlParameter("@Price", 5M));
            var queryToCsvFileResult = dbContext.Database.SqlQueryToCsvFile(filePath, options => { options.ColumnDelimiter = "|"; options.TextQualifer = "\""; },
                "SELECT * FROM Orders WHERE Price > @Price", new SqlParameter("@Price", 5M));

            Assert.IsTrue(count > 0, "There should be existing data in the source table");
            Assert.IsTrue(queryToCsvFileResult.DataRowCount == count, "The number of data rows written to the file should match the count from the database");
            Assert.IsTrue(queryToCsvFileResult.TotalRowCount == count + 1, "The total number of rows written to the file should match the count from the database plus the header row");
        }
        [TestMethod]
        public void UpdateFromQuery()
        {
            var dbContext = SetupDbContext(true);
            int oldTotal = dbContext.Orders.Where(o => o.Price < 10M).Count();
            int rowUpdated = dbContext.Orders.Where(o => o.Price < 10M).UpdateFromQuery(o => new Order { Price = 25.30M });
            int newTotal = dbContext.Orders.Where(o => o.Price < 10M).Count();
            int matchCount = dbContext.Orders.Where(o => o.Price == 25.30M).Count();

            Assert.IsTrue(oldTotal > 0, "There must be orders in database that match this condition (Price < $10)");
            Assert.IsTrue(rowUpdated == oldTotal, "The number of rows update must match the count of rows that match the condtion (Price < $10)");
            Assert.IsTrue(newTotal == 0, "The new count must be 0 to indicate all records were updated");
            Assert.IsTrue(matchCount == rowUpdated, "The match count must be equal the number of rows updated in the database.");
        }
        [TestMethod]
        public void UpdateFromQuery_With_Different_Culture()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("sv-SE");
            var dbContext = SetupDbContext(true);
            int oldTotal = dbContext.Orders.Where(o => o.Price < 10M).Count();
            int rowUpdated = dbContext.Orders.Where(o => o.Price < 10M).UpdateFromQuery(o => new Order { Price = 25.30M });
            int newTotal = dbContext.Orders.Where(o => o.Price < 10M).Count();
            int matchCount = dbContext.Orders.Where(o => o.Price == 25.30M).Count();

            Assert.AreEqual("25,30", Convert.ToString(25.30M));
            Assert.IsTrue(oldTotal > 0, "There must be orders in database that match this condition (Price < $10)");
            Assert.IsTrue(rowUpdated == oldTotal, "The number of rows update must match the count of rows that match the condtion (Price < $10)");
            Assert.IsTrue(newTotal == 0, "The new count must be 0 to indicate all records were updated");
            Assert.IsTrue(matchCount == rowUpdated, "The match count must be equal the number of rows updated in the database.");
        }
        [TestMethod]
        public void UpdateFromQuery_With_Null_Value()
        {
            var dbContext = SetupDbContext(true);
            int oldTotal = dbContext.Orders.Where(o => o.ExternalId != null).Count();
            int rowUpdated = dbContext.Orders.Where(o => o.ExternalId != null).UpdateFromQuery(o => new Order { ExternalId = null });
            int newTotal = dbContext.Orders.Where(o => o.ExternalId != null).Count();

            Assert.IsTrue(oldTotal > 0, "There must be orders in database that match this condition (ExternalId != null)");
            Assert.IsTrue(rowUpdated == oldTotal, "The number of rows update must match the count of rows that match the condition (ExternalId != null)");
            Assert.IsTrue(newTotal == 0, "The new count must be 0 to indicate all records were updated");
        }
        [TestMethod]
        public void UpdateFromQuery_With_Boolean_Value()
        {
            var dbContext = SetupDbContext(true);
            int oldTotal = dbContext.Articles.Count(a => a.OutOfStock);
            int rowUpdated = dbContext.Articles.Where(a => a.OutOfStock).UpdateFromQuery(a => new Article { OutOfStock = false });
            int newTotal = dbContext.Articles.Count(o => o.OutOfStock);

            Assert.IsTrue(oldTotal > 0, "There must be articles in database that match this condition (OutOfStock == true)");
            Assert.IsTrue(rowUpdated == oldTotal, "The number of rows update must match the count of rows that match the condition (OutOfStock == false)");
            Assert.IsTrue(newTotal == 0, "The new count must be 0 to indicate all records were updated");
        }
        [TestMethod]
        public void UpdateFromQuery_With_Expression()
        {
            var dbContext = SetupDbContext(true);
            decimal priceStart = 10M;
            decimal priceUpdate = 0.34M;

            int oldTotal = dbContext.Orders.Count(a => a.Price < 10);
            int rowUpdated = dbContext.Orders.Where(a => a.Price < 10).UpdateFromQuery(a => new Order { Price = priceStart + priceUpdate });
            int newTotal = dbContext.Orders.Count(o => o.Price < 10);

            Assert.IsTrue(oldTotal > 0, "There must be articles in database that match this condition (Price < 10)");
            Assert.IsTrue(rowUpdated == oldTotal, "The number of rows update must match the count of rows that match the condition (Price < 10)");
            Assert.IsTrue(newTotal == 0, "The new count must be 0 to indicate all records were updated");
        }

        [TestMethod]
        public void UpdateFromQuery_With_Expression_Accessing_Previous_Value_Fails()
        {
            var dbContext = SetupDbContext(true);
            decimal priceUpdate = 0.34M;

            // Access to previous value creates an exception as the value must be present when we create the SQL code.
            Assert.ThrowsException<InvalidOperationException>(() => dbContext.Orders.Where(a => a.Price < 10).UpdateFromQuery(a => new Order { Price = a.Price + priceUpdate }));
        }

        [TestMethod]
        public void UpdateFromQuery_With_String_Containing_Apostrophe()
        {
            var dbContext = SetupDbContext(true);
            int oldTotal = dbContext.Orders.Where(o => o.ExternalId == null).Count();
            int rowUpdated = dbContext.Orders.Where(o => o.ExternalId == null).UpdateFromQuery(o => new Order { ExternalId = "inv'alid" });
            int newTotal = dbContext.Orders.Where(o => o.ExternalId == null).Count();

            Assert.IsTrue(oldTotal > 0, "There must be orders in database that match this condition (ExternalId == null)");
            Assert.IsTrue(rowUpdated == oldTotal, "The number of rows update must match the count of rows that match the condition (ExternalId == null)");
            Assert.IsTrue(newTotal == 0, "The new count must be 0 to indicate all records were updated");
        }
        [TestMethod]
        public void UpdateFromQuery_With_DateTime()
        {
            var dbContext = SetupDbContext(true);
            DateTime dateTime = dbContext.Orders.Max(o => o.AddedDateTime).AddDays(-30);
            DateTime now = DateTime.UtcNow;

            int oldTotal = dbContext.Orders.Where(o => o.AddedDateTime >= dateTime).Count();
            int rowUpdated = dbContext.Orders.Where(o => o.AddedDateTime >= dateTime).UpdateFromQuery(o => new Order { ModifiedDateTime = now });
            int newTotal = dbContext.Orders.Where(o => o.ModifiedDateTime == now).Count();

            Assert.IsTrue(oldTotal > 0, "There must be orders in database that match this condition (Orders added in last 30 days)");
            Assert.IsTrue(rowUpdated == oldTotal, "The number of rows update must match the count of rows that match the condition (Orders added in last 30 days)");
            Assert.IsTrue(newTotal == 0, "The new count must be 0 to indicate all records were updated");
        }
        [TestMethod]
        public void Sql_SqlQuery_Count()
        {
            var dbContext = SetupDbContext(true);
            int efCount = dbContext.Orders.Where(o => o.Price > 5M).Count();
            var sqlCount = dbContext.Database.FromSqlQuery("SELECT * FROM Orders WHERE Price > @Price", new SqlParameter("@Price", 5M)).Count();

            Assert.IsTrue(efCount > 0, "Count from EF should be greater than zero");
            Assert.IsTrue(efCount > 0, "Count from SQL should be greater than zero");
            Assert.IsTrue(efCount == sqlCount, "Count from EF should match the count from the SqlQuery");
        }
        [TestMethod]
        public void Sql_SqlQuery_Count_With_OrderBy()
        {
            var dbContext = SetupDbContext(true);
            int efCount = dbContext.Orders.Where(o => o.Price > 5M).Count();
            var sqlCount = dbContext.Database.FromSqlQuery("SELECT * FROM Orders WHERE Price > @Price ORDER BY Id", new SqlParameter("@Price", 5M)).Count();

            Assert.IsTrue(efCount > 0, "Count from EF should be greater than zero");
            Assert.IsTrue(efCount > 0, "Count from SQL should be greater than zero");
            Assert.IsTrue(efCount == sqlCount, "Count from EF should match the count from the SqlQuery");
        }
        [TestMethod]
        public void Sql_TableExists()
        {
            var dbContext = SetupDbContext(true);
            int efCount = dbContext.Orders.Where(o => o.Price > 5M).Count();
            bool ordersTableExists = dbContext.Database.TableExists("Orders");
            bool orderNewTableExists = dbContext.Database.TableExists("OrdersNew");

            Assert.IsTrue(ordersTableExists, "Orders table should exist");
            Assert.IsTrue(!orderNewTableExists, "Orders_New table should not exist");
        }
        private TestDbContext SetupDbContext(bool populateData)
        {
            TestDbContext dbContext = new TestDbContext();
            dbContext.Database.CreateIfNotExists();
            dbContext.Orders.DeleteFromQuery();
            dbContext.Articles.DeleteFromQuery();
            dbContext.Database.ClearTable("TphPeople");
            dbContext.Database.ClearTable("TpcCustomer");
            dbContext.Database.ClearTable("TpcVendor");
            if (populateData)
            {
                var orders = new List<Order>();
                int id = 1;
                for (int i = 0; i < 2050; i++)
                {
                    DateTime addedDateTime = DateTime.UtcNow.AddDays(-id);
                    orders.Add(new Order { 
                        Id = id, 
                        ExternalId = string.Format("id-{0}", i), 
                        Price = 1.25M,
                        AddedDateTime = addedDateTime,
                        ModifiedDateTime = addedDateTime.AddHours(3)
                    });
                    id++;
                }
                for (int i = 0; i < 1050; i++)
                {
                    orders.Add(new Order { Id = id, Price = 5.35M });
                    id++;
                }
                for (int i = 0; i < 2050; i++)
                {
                    orders.Add(new Order { Id = id, Price = 1.25M });
                    id++;
                }
                for (int i = 0; i < 6000; i++)
                {
                    orders.Add(new Order { Id = id, Price = 15.35M });
                    id++;
                }
                for (int i = 0; i < 6000; i++)
                {
                    orders.Add(new Order { Id = id, Price = 15.35M });
                    id++;
                }

                Debug.WriteLine("Last Id for Order is {0}", id);
                dbContext.BulkInsert(orders, new BulkInsertOptions<Order>() { KeepIdentity = true });
                var articles = new List<Article>();
                id = 1;
                for (int i = 0; i < 2050; i++)
                {
                    articles.Add(new Article { ArticleId = string.Format("id-{0}", i), Price = 1.25M, OutOfStock = false });
                    id++;
                }
                for (int i = 0; i < 2050; i++)
                {
                    articles.Add(new Article { ArticleId = string.Format("id-{0}", id), Price = 1.25M, OutOfStock = true });
                    id++;
                }

                Debug.WriteLine("Last Id for Article is {0}", id);
                dbContext.BulkInsert(articles, new BulkInsertOptions<Article>() { KeepIdentity = false, AutoMapOutputIdentity = false });

                //TPH Customers & Vendors
                var tphCustomers = new List<TphCustomer>();
                var tphVendors = new List<TphVendor>();
                for (int i = 0; i < 2000; i++)
                {
                    tphCustomers.Add(new TphCustomer
                    {
                        Id = i,
                        FirstName = string.Format("John_{0}", i),
                        LastName = string.Format("Smith_{0}", i),
                        Email = string.Format("john.smith{0}@domain.com", i),
                        Phone = "404-555-1111",
                        AddedDate = DateTime.UtcNow
                    });
                }
                for (int i = 2000; i < 3000; i++)
                {
                    tphVendors.Add(new TphVendor
                    {
                        Id = i,
                        FirstName = string.Format("Mike_{0}", i),
                        LastName = string.Format("Smith_{0}", i),
                        Phone = "404-555-2222",
                        Email = string.Format("mike.smith{0}@domain.com", i),
                        Url = string.Format("http://domain.com/mike.smith{0}", i)
                    });
                }
                dbContext.BulkInsert(tphCustomers, new BulkInsertOptions<TphCustomer>() { KeepIdentity = true });
                dbContext.BulkInsert(tphVendors, new BulkInsertOptions<TphVendor>() { KeepIdentity = true });

                //TPC Customers & Vendors
                var tpcCustomers = new List<TpcCustomer>();
                var tpcVendors = new List<TpcVendor>();
                for (int i = 0; i < 2000; i++)
                {
                    tpcCustomers.Add(new TpcCustomer
                    {
                        Id = i,
                        FirstName = string.Format("John_{0}", i),
                        LastName = string.Format("Smith_{0}", i),
                        Email = string.Format("john.smith{0}@domain.com", i),
                        Phone = "404-555-1111",
                        AddedDate = DateTime.UtcNow
                    });
                }
                for (int i = 2000; i < 3000; i++)
                {
                    tpcVendors.Add(new TpcVendor
                    {
                        Id = i,
                        FirstName = string.Format("Mike_{0}", i),
                        LastName = string.Format("Smith_{0}", i),
                        Phone = "404-555-2222",
                        Email = string.Format("mike.smith{0}@domain.com", i),
                        Url = string.Format("http://domain.com/mike.smith{0}", i)
                    });
                }
                dbContext.BulkInsert(tpcCustomers, new BulkInsertOptions<TpcCustomer>() { KeepIdentity = true });
                dbContext.BulkInsert(tpcVendors, new BulkInsertOptions<TpcVendor>() { KeepIdentity = true });
            }
            return dbContext;
        }
    }
}
