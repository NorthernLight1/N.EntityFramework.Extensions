# N.EntityFramework.Extensions

[![latest version](https://img.shields.io/nuget/v/N.EntityFramework.Extensions)](https://www.nuget.org/packages/N.EntityFramework.Extensions) [![downloads](https://img.shields.io/nuget/dt/N.EntityFramework.Extensions)](https://www.nuget.org/packages/N.EntityFramework.Extensions) [![.NET](https://github.com/NorthernLight1/N.EntityFramework.Extensions/actions/workflows/dotnet.yml/badge.svg)](https://github.com/NorthernLight1/N.EntityFramework.Extensions/actions/workflows/dotnet.yml)

**If you are using Entity Framework Core v8.0.0+ you can use https://github.com/NorthernLight1/N.EntityFrameworkCore.Extensions**

## Bulk data support for the EntityFramework 6.5.0+

Entity Framework Extensions extends your DbContext with high-performance bulk operations: BulkDelete, BulkFetch, BulkInsert, BulkMerge, BulkSaveChanges, BulkSync, BulkUpdate, Fetch, FromSqlQuery, DeleteFromQuery, InsertFromQuery, UpdateFromQuery, QueryToCsvFile, SqlQueryToCsvFile

Supports: Multiple Schemas, Transaction, Asynchronous Execution, Inheritance Models (Table-Per-Hierarchy, Table-Per-Concrete)

Supports Databases: SQL Server

  ### Installation

  The latest stable version is available on [NuGet](https://www.nuget.org/packages/N.EntityFramework.Extensions).

  ```sh
  Install-Package N.EntityFramework.Extensions
  ```

 ## Usage
   
 **BulkInsert() - Performs a insert operation with a large number of entities**  
   ```csharp
  var dbContext = new MyDbContext();  
  var orders = new List<Order>();  
  for(int i=0; i<10000; i++)  
  {  
      orders.Add(new Order { OrderDate = DateTime.UtcNow, TotalPrice = 2.99 });  
  }  
  dbContext.BulkInsert(orders);

  //Using Options
  dbContext.BulkInsert(orders, options =>
  {
    options.CommandTimeout = 30;
    options.BatchSize = 100000;
    options.InsertIfNotExists = true;
    options.InsertOnCondition = (s, t) => s.ExternalId == t.ExternalId;
	options.KeepIdentity = true;
  });

  //Async
  await dbContext.BulkInsertAsync(orders);
 ```
  **BulkDelete() - Performs a delete operation with a large number of entities**  
  ```csharp
  var dbContext = new MyDbContext();  
  var orders = dbContext.Orders.Where(o => o.TotalPrice < 5.35M).ToList();  
  dbContext.BulkDelete(orders);

  //Async
  await dbContext.BulkDeleteAsync(orders);
  ```
  **BulkFetch() - Retrieves entities that are contained in a list**  
  ```csharp
  var ids = new List<int> { 10001, 10002, 10003, 10004, 10005 };
  var products = dbContext.Products.BulkFetch(ids, options => { options.JoinOnCondition = (s, t) => s.Id == t.Id; }).ToList();

  //Async
  var products = await dbContext.Products.BulkFetchAsync(ids, options => { options.JoinOnCondition = (s, t) => s.Id == t.Id; });
  ```
  **BulkUpdate() - Performs a update operation with a large number of entities**  
  ```csharp
  var dbContext = new MyDbContext();  
  var products = dbContext.Products.Where(o => o.Price < 5.35M).ToList();
  foreach(var product in products)
  {
      product.Price = 6M;
  }
  dbContext.BulkUpdate(products);

  //Async
  await dbContext.BulkUpdateAsync(products);
  ```
  **BulkMerge() - Performs a merge operation with a large number of entities**
  ```csharp
  var dbContext = new MyDbContext();
  var products = new List<Product>();
  var existingProducts = dbContext.Products.Where(o => o.Price < 5.35M).ToList();
  foreach(var product in existingProducts)
  {
      product.Price = 6M;
  }
  products.AddRange(existingProducts);
  products.Add(new Product { Name="Hat", Price=10.25M });
  products.Add(new Product { Name="Shirt", Price=20.95M });
  dbContext.BulkMerge(products);

  //Async
  await dbContext.BulkMergeAsync(products);
  ```
  **BulkSaveChanges() - Saves all changes using bulk operations**  
   ```csharp
  var dbContext = new MyDbContext();  
  var orders = new List<Order>();  
  for(int i=0; i<10000; i++)  
  {  
      orders.Add(new Order { Id=-i, OrderDate = DateTime.UtcNow, TotalPrice = 2.99 });  
  }
  dbContext.Orders.AddRange(orders);
  dbContext.BulkSaveChanges();

  //Async
  await dbContext.BulkSaveChangesAsync();
  ```
   **BulkSync() - Performs a sync operation with a large number of entities.** 
   
   By default any entities that do not exists in the source list will be deleted, but this can be disabled in the options.
  ```csharp
  var dbContext = new MyDbContext();
  var products = new List<Product>();
  var existingProducts = dbContext.Products.Where(o => o.Id <= 1000).ToList();
  foreach(var product in existingProducts)
  {
      product.Price = 6M;
  }
  products.AddRange(existingProducts);
  products.Add(new Product { Name="Hat", Price=10.25M });
  products.Add(new Product { Name="Shirt", Price=20.95M });
  //All existing products with Id > 1000 will be deleted
  dbContext.BulkSync(products);

  //Async
  await dbContext.BulkSyncAsync(products);
  ```
  **Fetch() - Retrieves data in batches.**  
  ```csharp
  var dbContext = new MyDbContext();  
  var query = dbContext.Products.Where(o => o.Price < 5.35M);
  query.Fetch(result =>
    {
      batchCount++;
      totalCount += result.Results.Count();
    }, 
    new FetchOptions { BatchSize = 1000 }
  );
  ```
  **DeleteFromQuery() - Deletes records from the database using a LINQ query without loading data in the context**  
   ``` csharp
  var dbContext = new MyDbContext(); 
  
  //This will delete all products  
  dbContext.Products.DeleteFromQuery();
  
  //This will delete all products that are under $5.35  
  dbContext.Products.Where(x => x.Price < 5.35M).DeleteFromQuery();

  //Async
  await dbContext.Products.Where(x => x.Price < 5.35M).DeleteFromQueryAsync();
```
  **InsertFromQuery() - Inserts records from the database using a LINQ query without loading data in the context**  
   ``` csharp
  var dbContext = new MyDbContext(); 
  
  //This will take all products priced under $10 from the Products table and 
  //insert it into the ProductsUnderTen table
  dbContext.Products.Where(x => x.Price < 10M).InsertFromQuery("ProductsUnderTen", o => new { o.Id, o.Price });

  //Async
  await dbContext.Products.Where(x => x.Price < 10M).InsertFromQueryAsync("ProductsUnderTen", o => new { o.Id, o.Price });
```
  **UpdateFromQuery() - Updates records from the database using a LINQ query without loading data in the context**  
   ``` csharp
  var dbContext = new MyDbContext(); 
  
  //This will change all products priced at $5.35 to $5.75 
  dbContext.Products.Where(x => x.Price == 5.35M).UpdateFromQuery(o => new Product { Price = 5.75M });

  //Async
  await dbContext.Products.Where(x => x.Price == 5.35M).UpdateFromQueryAsync(o => new Product { Price = 5.75M });
```
  **QueryToCsvFile() - Exports a LINQ query to a CSV file**  
  ```csharp
  var dbContext = new MyDbContext();

  //Export to file path
  dbContext.Products.Where(x => x.Price < 10M).QueryToCsvFile("products.csv");

  //Export to stream with options
  dbContext.Products.Where(x => x.Price < 10M).QueryToCsvFile(myStream, options =>
  {
      options.ColumnDelimiter = ",";
      options.IncludeHeaderRow = true;
  });
  ```
  **SqlQueryToCsvFile() - Exports a raw SQL query to a CSV file**  
  ```csharp
  var dbContext = new MyDbContext();

  //Export to file path
  dbContext.Database.SqlQueryToCsvFile("products.csv", "SELECT * FROM Products WHERE Price < @p0", 10M);

  //Export to stream with options
  dbContext.Database.SqlQueryToCsvFile(myStream, options =>
  {
      options.ColumnDelimiter = ",";
      options.IncludeHeaderRow = true;
  }, "SELECT * FROM Products WHERE Price < @p0", 10M);
  ```

## Options
  **Transaction** 
  
  When using any of the following bulk data operations (BulkDelete, BulkInsert, BulkMerge, BulkSaveChanges, BulkSync, BulkUpdate, DeleteFromQuery, InsertFromQuery, UpdateFromQuery), if an external transaction exists, then it will be utilized.
   
   ``` csharp
  var dbContext = new MyDbContext(); 
  var transaction = dbContext.Database.BeginTransaction();
  try
  {
      dbContext.BulkInsert(orders);
      transaction.Commit();
  }
  catch
  {
      transaction.Rollback();
  }
```

## Documentation
| Name  | Description |
| ------------- | ------------- |
| **BulkDelete** |
| BulkDelete<T>(items)  | Bulk delete entities in your database.  |
| BulkDelete<T>(items, options)  | Bulk delete entities in your database.   |
| BulkDeleteAsync(items)  | Bulk delete entities asynchronously in your database.  |
| BulkDeleteAsync(items, cancellationToken)  | Bulk delete entities asynchronously in your database.  |
| BulkDeleteAsync(items, options)  | Bulk delete entities asynchronously in your database.  |
| BulkDeleteAsync(items, options, cancellationToken)  | Bulk delete entities asynchronously in your database.  |
| **BulkDeleteOptions** |
| DeleteOnCondition | Gets or sets the join condition used to match entities for deletion. If null, the primary key is used. (Default=null) |
| **BulkFetch** |
| BulkFetch<T>(items)  | Retrieve entities that are contained in the items list.  |
| BulkFetch<T>(items, options)  | Retrieve entities that are contained in the items list.  |
| BulkFetchAsync<T>(items)  | Retrieve entities that are contained in the items list.  |
| BulkFetchAsync<T>(items, options)  | Retrieve entities that are contained in the items list.  | 
| **BulkFetchOptions** |
| IgnoreColumns | Columns that will be excluded from the fetch. |
| InputColumns | Columns that will be included in the fetch. |
| JoinOnCondition | Gets or sets the join condition used to match entities. If null, the primary key is used. (Default=null) |
| **BulkInsert** |
| BulkInsert<T>(items)  | Bulk insert entities in your database.  |
| BulkInsert<T>(items, options)  | Bulk insert entities in your database.   |
| BulkInsertAsync(items)  | Bulk insert entities asynchronously in your database.  |
| BulkInsertAsync(items, cancellationToken)  | Bulk insert entities asynchronously in your database.  |
| BulkInsertAsync(items, options)  | Bulk insert entities asynchronously in your database.  |
| BulkInsertAsync(items, options, cancellationToken)  | Bulk insert entities asynchronously in your database.  |
| **BulkInsertOptions** |
| AutoMapOutput | Assigns the output of all database generated columns to the entities. Performance can be improved by disabling this option. (Default=true) |
| CommandTimeout | Gets or sets the wait time (in seconds) before terminating the attempt. |
| IgnoreColumns | Columns that will be excluded. |
| InputColumns | Columns that will be included. |
| InsertIfNotExists | Inserts data into the target table only if it doesn't already exist. (Default=false) |
| InsertOnCondition | Gets or sets the join condition for inserting data. If this condition is null, then the primary key is used. (Default=null) |
| KeepIdentity | Keeps the identity when inserting data into a table. (Default=false)|
| UsePermanentTable | Uses a permanent table when inserting data. If a table uses Always Encrypt, a permanent table is required. (Default=false)|
| **BulkMerge** |
| BulkMerge<T>(items)  | Bulk merge entities in your database.  |
| BulkMerge<T>(items, options)  | Bulk merge entities in your database.   |
| BulkMergeAsync(items)  | Bulk merge entities asynchronously in your database.  |
| BulkMergeAsync(items, cancellationToken)  | Bulk merge entities asynchronously in your database.  |
| BulkMergeAsync(items, options)  | Bulk merge entities asynchronously in your database.  |
| BulkMergeAsync(items, options, cancellationToken)  | Bulk merge entities asynchronously in your database.  |
| **BulkMergeOptions** |
| AutoMapOutput | Assigns the output of all database generated columns to the entities. (Default=true) |
| IgnoreColumnsOnInsert | Columns that will be excluded during the insert phase of the merge. |
| IgnoreColumnsOnUpdate | Columns that will be excluded during the update phase of the merge. |
| MergeOnCondition | Gets or sets the join condition used to match entities. If null, the primary key is used. (Default=null) |
| **BulkSaveChanges** |
| BulkSaveChanges()  | Save changes using high-performance bulk operations. Should be used instead of SaveChanges(). |
| BulkSaveChanges(acceptAllChangesOnSave)  | Save changes using high-performance bulk operations. Should be used instead of SaveChanges(). |
| BulkSaveChangesAsync()  | Save changes using high-performance bulk operations. Should be used instead of SaveChanges(). |
| BulkSaveChangesAsync(acceptAllChangesOnSave, cancellationToken)  | Save changes using high-performance bulk operations. Should be used instead of SaveChanges(). |
| **BulkSync** |
| BulkSync<T>(items)  | Bulk sync entities in your database.  |
| BulkSync<T>(items, options)  | Bulk sync entities in your database.   |
| BulkSyncAsync(items)  | Bulk sync entities asynchronously in your database.  |
| BulkSyncAsync(items, cancellationToken)  | Bulk sync entities asynchronously in your database.  |
| BulkSyncAsync(items, options)  | Bulk sync entities asynchronously in your database.  |
| BulkSyncAsync(items, options, cancellationToken)  | Bulk sync entities asynchronously in your database.  |
| **BulkSyncOptions** |
| AutoMapOutput | Assigns the output of all database generated columns to the entities. (Default=true) |
| IgnoreColumnsOnInsert | Columns that will be excluded during the insert phase of the sync. |
| IgnoreColumnsOnUpdate | Columns that will be excluded during the update phase of the sync. |
| MergeOnCondition | Gets or sets the join condition used to match entities. If null, the primary key is used. (Default=null) |
| **BulkUpdate** |  
| BulkUpdate<T>(items)  | Bulk update entities in your database.  |
| BulkUpdate<T>(items, options)  | Bulk update entities in your database.   |
| BulkUpdateAsync(items)  | Bulk update entities asynchronously in your database.  |
| BulkUpdateAsync(items, cancellationToken)  | Bulk update entities asynchronously in your database.  |
| BulkUpdateAsync(items, options)  | Bulk update entities asynchronously in your database.  |
| BulkUpdateAsync(items, options, cancellationToken)  | Bulk update entities asynchronously in your database.  |
| **BulkUpdateOptions** |
| IgnoreColumns | Columns that will be excluded from the update. |
| InputColumns | Columns that will be included in the update. |
| UpdateOnCondition | Gets or sets the join condition used to match entities. If null, the primary key is used. (Default=null) |
| **DeleteFromQuery** |
| DeleteFromQuery() | Deletes all rows from the database using a LINQ query without loading in context |
| DeleteFromQueryAsync() | Deletes all rows from the database using a LINQ query without loading in context using asynchronous task |
| DeleteFromQueryAsync(cancellationToken) | Deletes all rows from the database using a LINQ query without loading in context using asynchronous task  |
| **InsertFromQuery** |
| InsertFromQuery(tableName, selectExpression) | Insert all rows from the database using a LINQ query without loading in context |
| InsertFromQueryAsync(tableName, selectExpression) | Insert all rows from the database using a LINQ query without loading in context using asynchronous task |
| InsertFromQueryAsync(tableName, selectExpression, cancellationToken) | Insert all rows from the database using a LINQ query without loading in context using asynchronous task  |
| **UpdateFromQuery** |
| UpdateFromQuery(updateExpression, commandTimeout=null) | Updates all rows from the database using a LINQ query without loading in context |
| UpdateFromQueryAsync(updateExpression, commandTimeout=null) | Updates all rows from the database using a LINQ query without loading in context using asynchronous task |
| UpdateFromQueryAsync(updateExpression, commandTimeout=null, cancellationToken) | Updates all rows from the database using a LINQ query without loading in context using asynchronous task  |
| **Fetch** |
| Fetch(fetchAction) | Fetch rows in batches from the database using a LINQ query |
| Fetch(fetchAction, options) | Fetch rows in batches from the database using a LINQ query |
| FetchAsync(fetchAction)  | Fetch rows asynchronously in batches from the database using a LINQ query |
| FetchAsync(fetchAction, options)  | Fetch rows asynchronously in batches from the database using a LINQ query |
| FetchAsync(fetchAction, cancellationToken) | Fetch rows asynchronously in batches from the database using a LINQ query  | 
| FetchAsync(fetchAction, options, cancellationToken) | Fetch rows asynchronously in batches from the database using a LINQ query  |
| **QueryToCsvFile** |
| QueryToCsvFile(filePath) | Export a LINQ query to a CSV file at the given path |
| QueryToCsvFile(stream) | Export a LINQ query to a CSV stream |
| QueryToCsvFile(filePath, optionsAction) | Export a LINQ query to a CSV file with options |
| QueryToCsvFile(stream, optionsAction) | Export a LINQ query to a CSV stream with options |
| QueryToCsvFile(filePath, options) | Export a LINQ query to a CSV file with options |
| QueryToCsvFile(stream, options) | Export a LINQ query to a CSV stream with options |
| **SqlQueryToCsvFile** |
| SqlQueryToCsvFile(filePath, sqlText, params) | Export a SQL query to a CSV file at the given path |
| SqlQueryToCsvFile(stream, sqlText, params) | Export a SQL query to a CSV stream |
| SqlQueryToCsvFile(filePath, optionsAction, sqlText, params) | Export a SQL query to a CSV file with options |
| SqlQueryToCsvFile(stream, optionsAction, sqlText, params) | Export a SQL query to a CSV stream with options |
| SqlQueryToCsvFile(filePath, options, sqlText, params) | Export a SQL query to a CSV file with options |
| SqlQueryToCsvFile(stream, options, sqlText, params) | Export a SQL query to a CSV stream with options |
| **QueryToFileOptions** |
| ColumnDelimiter | The delimiter used to separate columns. (Default=",") |
| CommandTimeout | Gets or sets the wait time (in seconds) before terminating the attempt. |
| IncludeHeaderRow | Whether to include a header row with column names. (Default=true) |
| RowDelimiter | The delimiter used to separate rows. (Default="\r\n") |
| TextQualifer | The text qualifier wrapped around field values. (Default="") |