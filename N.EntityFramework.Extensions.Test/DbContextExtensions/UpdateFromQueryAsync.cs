﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using N.EntityFramework.Extensions.Test.Data;
using N.EntityFramework.Extensions.Test.Data.Enums;

namespace N.EntityFramework.Extensions.Test.DbContextExtensions;

[TestClass]
public class UpdateFromQueryAsync : DbContextExtensionsBase
{
    [TestMethod]
    public async Task With_Boolean_Value()
    {
        var dbContext = SetupDbContext(true);
        int oldTotal = dbContext.Products.Count(a => a.OutOfStock);
        int rowUpdated = await dbContext.Products.Where(a => a.OutOfStock).UpdateFromQueryAsync(a => new Product { OutOfStock = false });
        int newTotal = dbContext.Products.Count(o => o.OutOfStock);

        Assert.IsTrue(oldTotal > 0, "There must be articles in database that match this condition (OutOfStock == true)");
        Assert.IsTrue(rowUpdated == oldTotal, "The number of rows update must match the count of rows that match the condition (OutOfStock == false)");
        Assert.IsTrue(newTotal == 0, "The new count must be 0 to indicate all records were updated");
    }
    [TestMethod]
    public async Task With_Concatenating_String()
    {
        var dbContext = SetupDbContext(true);
        var orders = dbContext.Orders.Where(o => o.ExternalId == null);
        int oldTotal = orders.Count();
        int rowUpdated = await orders.UpdateFromQueryAsync(o => new Order { ExternalId = Convert.ToString(o.Id) + "Test" });
        int newTotal = orders.Count();

        Assert.IsTrue(oldTotal > 0, "There must be orders in database that match this condition (ExternalId == null)");
        Assert.IsTrue(rowUpdated == oldTotal, "The number of rows update must match the count of rows that match the condition (ExternalId == null)");
        Assert.IsTrue(newTotal == 0, "The new count must be 0 to indicate all records were updated");
    }
    [TestMethod]
    public async Task With_Concatenating_String_And_Number()
    {
        var dbContext = SetupDbContext(true);
        var orders = dbContext.Orders.Where(o => o.ExternalId == null);
        int oldTotal = orders.Count();
        int rowUpdated = await orders.UpdateFromQueryAsync(o => new Order { ExternalId = Convert.ToString(o.Id) + "Test" });
        int newTotal = orders.Count();

        Assert.IsTrue(oldTotal > 0, "There must be orders in database that match this condition (ExternalId == null)");
        Assert.IsTrue(rowUpdated == oldTotal, "The number of rows update must match the count of rows that match the condition (ExternalId == null)");
        Assert.IsTrue(newTotal == 0, "The new count must be 0 to indicate all records were updated");
    }
    [TestMethod]
    public async Task With_DateTime_Value()
    {
        var dbContext = SetupDbContext(true);
        DateTime dateTime = dbContext.Orders.Max(o => o.AddedDateTime).AddDays(-30);
        DateTime now = DateTime.Parse(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss"));

        int oldTotal = dbContext.Orders.Where(o => o.AddedDateTime >= dateTime).Count();
        int rowUpdated = await dbContext.Orders.Where(o => o.AddedDateTime >= dateTime).UpdateFromQueryAsync(o => new Order { ModifiedDateTime = now });
        int newTotal = dbContext.Orders.Where(o => o.ModifiedDateTime == now).Count();

        Assert.IsTrue(oldTotal > 0, "There must be orders in database that match this condition (Orders added in last 30 days)");
        Assert.IsTrue(rowUpdated == newTotal, "The number of rows updated should equal the new count of rows that match the condition (Orders added in last 30 days)");
        Assert.IsTrue(oldTotal == newTotal, "The count of rows matching the condition before and after the update should be equal. (Orders added in last 30 days)");
    }
    [TestMethod]
    public async Task With_DateTimeOffset_Value()
    {
        var dbContext = SetupDbContext(true);
        DateTime dateTime = dbContext.Orders.Max(o => o.AddedDateTime).AddDays(-30);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        int oldTotal = dbContext.Orders.Where(o => o.AddedDateTime >= dateTime).Count();
        int rowUpdated = await dbContext.Orders.Where(o => o.AddedDateTime >= dateTime).UpdateFromQueryAsync(o => new Order { ModifiedDateTimeOffset = now });
        int newTotal = dbContext.Orders.Where(o => o.ModifiedDateTimeOffset == now).Count();

        Assert.IsTrue(oldTotal > 0, "There must be orders in database that match this condition (Orders added in last 30 days)");
        Assert.IsTrue(rowUpdated == newTotal, "The number of rows updated should equal the new count of rows that match the condition (Orders added in last 30 days)");
        Assert.IsTrue(oldTotal == newTotal, "The count of rows matching the condition before and after the update should be equal. (Orders added in last 30 days)");
    }
    [TestMethod]
    public async Task With_DateTimeOffset_No_UTC_Value()
    {
        var dbContext = SetupDbContext(true);
        DateTime dateTime = dbContext.Orders.Max(o => o.AddedDateTime).AddDays(-30);
        DateTimeOffset now = DateTimeOffset.Parse("2020-06-17T16:00:00+05:00");

        int oldTotal = dbContext.Orders.Where(o => o.AddedDateTime >= dateTime).Count();
        int rowUpdated = await dbContext.Orders.Where(o => o.AddedDateTime >= dateTime).UpdateFromQueryAsync(o => new Order { ModifiedDateTimeOffset = now });
        int newTotal = dbContext.Orders.Where(o => o.ModifiedDateTimeOffset == now).Count();

        Assert.IsTrue(oldTotal > 0, "There must be orders in database that match this condition (Orders added in last 30 days)");
        Assert.IsTrue(rowUpdated == newTotal, "The number of rows updated should equal the new count of rows that match the condition (Orders added in last 30 days)");
        Assert.IsTrue(oldTotal == newTotal, "The count of rows matching the condition before and after the update should be equal. (Orders added in last 30 days)");
    }
    [TestMethod]
    public async Task With_Decimal_Value()
    {
        var dbContext = SetupDbContext(true);
        var orders = dbContext.Orders.Where(o => o.Price < 10M);
        int oldTotal = orders.Count();
        int rowUpdated = await orders.UpdateFromQueryAsync(o => new Order { Price = 25.30M });
        int newTotal = orders.Count();
        int matchCount = dbContext.Orders.Where(o => o.Price == 25.30M).Count();

        Assert.IsTrue(oldTotal > 0, "There must be orders in database that match this condition (Price < $10)");
        Assert.IsTrue(rowUpdated == oldTotal, "The number of rows update must match the count of rows that match the condtion (Price < $10)");
        Assert.IsTrue(newTotal == 0, "The new count must be 0 to indicate all records were updated");
        Assert.IsTrue(matchCount == rowUpdated, "The match count must be equal the number of rows updated in the database.");
    }
    [TestMethod]
    public async Task With_Different_Culture()
    {
        Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("sv-SE");
        var dbContext = SetupDbContext(true);
        int oldTotal = dbContext.Orders.Where(o => o.Price < 10M).Count();
        int rowUpdated = await dbContext.Orders.Where(o => o.Price < 10M).UpdateFromQueryAsync(o => new Order { Price = 25.30M });
        int newTotal = dbContext.Orders.Where(o => o.Price < 10M).Count();
        int matchCount = dbContext.Orders.Where(o => o.Price == 25.30M).Count();

        Assert.AreEqual("25,30", Convert.ToString(25.30M));
        Assert.IsTrue(oldTotal > 0, "There must be orders in database that match this condition (Price < $10)");
        Assert.IsTrue(rowUpdated == oldTotal, "The number of rows update must match the count of rows that match the condtion (Price < $10)");
        Assert.IsTrue(newTotal == 0, "The new count must be 0 to indicate all records were updated");
        Assert.IsTrue(matchCount == rowUpdated, "The match count must be equal the number of rows updated in the database.");
    }
    [TestMethod]
    public async Task With_Enum_Value()
    {
        var dbContext = SetupDbContext(true);
        int oldTotal = dbContext.Products.Count(a => a.StatusEnum == null && a.OutOfStock);
        int rowUpdated = await dbContext.Products.Where(a => a.StatusEnum == null && a.OutOfStock)
            .UpdateFromQueryAsync(a => new Product { StatusEnum = ProductStatus.OutOfStock });
        int newTotal = dbContext.Products.Count(o => o.StatusEnum == null && o.OutOfStock);
        int newTotal2 = dbContext.Products.Count(o => o.StatusEnum == ProductStatus.OutOfStock && o.OutOfStock);

        Assert.IsTrue(oldTotal > 0, "There must be articles in database that match this condition (OutOfStock == true)");
        Assert.IsTrue(rowUpdated == oldTotal, "The number of rows update must match the count of rows that match the condition (OutOfStock == false)");
        Assert.IsTrue(newTotal == 0, "The new count must be 0 to indicate all records were updated");
        Assert.IsTrue(newTotal2 == oldTotal, "All rows must have been updated");
    }
    [TestMethod]
    public async Task With_Guid_Value()
    {
        var dbContext = SetupDbContext(true);
        var guid = Guid.NewGuid();
        var orders = dbContext.Orders.Where(o => o.Price < 10M);
        int oldTotal = await orders.CountAsync();
        int rowUpdated = await orders.UpdateFromQueryAsync(o => new Order { GlobalId = guid });
        int matchCount = await dbContext.Orders.Where(o => o.GlobalId == guid).CountAsync();

        Assert.IsTrue(oldTotal > 0, "There must be orders in database that match this condition (Price < $10)");
        Assert.IsTrue(rowUpdated == oldTotal, $"The number of rows update must match the count of rows that match the condition (GlobalId = '{guid}')");
        Assert.IsTrue(matchCount == rowUpdated, "The match count must be equal the number of rows updated in the database.");
    }
    [TestMethod]
    public async Task With_Long_List()
    {
        var dbContext = SetupDbContext(true);
        var ids = new List<long>() { 1, 2, 3, 4, 5, 6, 7, 8 };
        var orders = dbContext.Orders.Where(o => ids.Contains(o.Id));
        int oldTotal = orders.Count();
        int rowUpdated = await orders.UpdateFromQueryAsync(o => new Order { Price = 25.25M });
        int newTotal = orders.Where(o => o.Price != 25.25M).Count();
        int matchCount = dbContext.Orders.Where(o => ids.Contains(o.Id) && o.Price == 25.25M).Count();

        Assert.IsTrue(oldTotal > 0, "There must be orders in database that match this condition (Price < $10)");
        Assert.IsTrue(rowUpdated == oldTotal, "The number of rows update must match the count of rows that match the condtion (Price < $10)");
        Assert.IsTrue(newTotal == 0, "The new count must be 0 to indicate all records were updated");
        Assert.IsTrue(matchCount == rowUpdated, "The match count must be equal the number of rows updated in the database.");
    }
    [TestMethod]
    public async Task With_Contains_Empty_List()
    {
        var dbContext = SetupDbContext(false);
        var guid = Guid.NewGuid();
        var emptyList = new List<long>();
        var orders = dbContext.Orders.Where(o => emptyList.Contains(o.Id));
        int rowsToUpdate = orders.Count();
        int rowsUpdated = await orders.UpdateFromQueryAsync(o => new Order { GlobalId = guid });

        Assert.IsTrue(rowsToUpdate == 0, "There must be no orders in database that match this condition");
        Assert.IsTrue(rowsToUpdate == rowsUpdated, "The number of rows updated must match the count of existing rows in database");
    }
    [TestMethod]
    public async Task With_MethodCall()
    {
        var dbContext = SetupDbContext(true);

        int oldTotal = dbContext.Orders.Count(a => a.Price < 10);
        int rowUpdated = await dbContext.Orders.Where(a => a.Price < 10).UpdateFromQueryAsync(o => new Order { Price = Math.Ceiling((o.Price + 10.5M) * 3 / 1) });
        int newTotal = dbContext.Orders.Count(o => o.Price < 10);

        Assert.IsTrue(oldTotal > 0, "There must be order in database that match this condition (Price < 10)");
        Assert.IsTrue(rowUpdated == oldTotal, "The number of rows update must match the count of rows that match the condition (Price < 10)");
        Assert.IsTrue(newTotal == 0, "The new count must be 0 to indicate all records were updated");
    }
    [TestMethod]
    public async Task With_Null_Value()
    {
        var dbContext = SetupDbContext(true);
        var orders = dbContext.Orders.Where(o => o.ExternalId != null);
        int oldTotal = orders.Count();
        int rowUpdated = await orders.UpdateFromQueryAsync(o => new Order { ExternalId = null });
        int newTotal = orders.Count();

        Assert.IsTrue(oldTotal > 0, "There must be orders in database that match this condition (ExternalId != null)");
        Assert.IsTrue(rowUpdated == oldTotal, "The number of rows update must match the count of rows that match the condition (ExternalId != null)");
        Assert.IsTrue(newTotal == 0, "The new count must be 0 to indicate all records were updated");
    }
    [TestMethod]
    public async Task With_String_Containing_Apostrophe()
    {
        var dbContext = SetupDbContext(true);
        int oldTotal = dbContext.Orders.Where(o => o.ExternalId == null).Count();
        int rowUpdated = await dbContext.Orders.Where(o => o.ExternalId == null).UpdateFromQueryAsync(o => new Order { ExternalId = "inv'alid" });
        int newTotal = dbContext.Orders.Where(o => o.ExternalId == null).Count();

        Assert.IsTrue(oldTotal > 0, "There must be orders in database that match this condition (ExternalId == null)");
        Assert.IsTrue(rowUpdated == oldTotal, "The number of rows update must match the count of rows that match the condition (ExternalId == null)");
        Assert.IsTrue(newTotal == 0, "The new count must be 0 to indicate all records were updated");
    }
    [TestMethod]
    public async Task With_Transaction()
    {
        var dbContext = SetupDbContext(true);
        int oldTotal = dbContext.Orders.Where(o => o.Price < 10M).Count();
        int rowUpdated;
        using (var transaction = dbContext.Database.BeginTransaction())
        {
            rowUpdated = await dbContext.Orders.Where(o => o.Price < 10M).UpdateFromQueryAsync(o => new Order { Price = 25.30M });
            transaction.Rollback();
        }
        int newTotal = dbContext.Orders.Where(o => o.Price < 10M).Count();
        int matchCount = dbContext.Orders.Where(o => o.Price == 25.30M).Count();

        Assert.IsTrue(oldTotal > 0, "There must be orders in database that match this condition (Price < $10)");
        Assert.IsTrue(rowUpdated == oldTotal, "The number of rows update must match the count of rows that match the condtion (Price < $10)");
        Assert.IsTrue(newTotal == oldTotal, "The new count must match the old count since the transaction was rollbacked");
        Assert.IsTrue(matchCount == 0, "The match count must be equal to 0 since the transaction was rollbacked.");
    }
    [TestMethod]
    public async Task With_Transaction_And_SqlError()
    {
        var dbContext = SetupDbContext(true);
        int oldTotal = dbContext.Orders.Where(o => o.Price < 10M).Count();
        int rowUpdated = 0;
        bool rollbackHasNotCrashed = true;
        using (var transaction = dbContext.Database.BeginTransaction())
        {
            try
            {
                rowUpdated = await dbContext.Orders.Where(o => o.Price < 10M).UpdateFromQueryAsync(o => new Order { Price = int.MaxValue });
            }
            catch
            {
                try
                {
                    transaction.Rollback();
                }
                catch
                {
                    rollbackHasNotCrashed = false;
                }
            }
        }

        Assert.IsTrue(rollbackHasNotCrashed, "Rollback has crashed, could means it was already rollbacked");
    }
    [TestMethod]
    public async Task With_Variables()
    {
        var dbContext = SetupDbContext(true);
        decimal priceStart = 10M;
        decimal priceUpdate = 0.34M;

        int oldTotal = dbContext.Orders.Count(a => a.Price < 10);
        int rowUpdated = await dbContext.Orders.Where(a => a.Price < 10).UpdateFromQueryAsync(a => new Order { Price = priceStart + priceUpdate });
        int newTotal = dbContext.Orders.Count(o => o.Price < 10);

        Assert.IsTrue(oldTotal > 0, "There must be orders in database that match this condition (Price < 10)");
        Assert.IsTrue(rowUpdated == oldTotal, "The number of rows update must match the count of rows that match the condition (Price < 10)");
        Assert.IsTrue(newTotal == 0, "The new count must be 0 to indicate all records were updated");
    }
    [TestMethod]
    public async Task With_Variable_And_Decimal()
    {
        var dbContext = SetupDbContext(true);
        decimal priceStart = 10M;

        int oldTotal = dbContext.Orders.Count(a => a.Price < 10);
        int rowUpdated = await dbContext.Orders.Where(a => a.Price < 10).UpdateFromQueryAsync(a => new Order { Price = priceStart + 7M });
        int newTotal = dbContext.Orders.Count(o => o.Price < 10);

        Assert.IsTrue(oldTotal > 0, "There must be orders in database that match this condition (Price < 10)");
        Assert.IsTrue(rowUpdated == oldTotal, "The number of rows update must match the count of rows that match the condition (Price < 10)");
        Assert.IsTrue(newTotal == 0, "The new count must be 0 to indicate all records were updated");
    }
}