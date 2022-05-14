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
