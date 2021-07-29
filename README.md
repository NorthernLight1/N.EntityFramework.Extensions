# N.EntityFramework.Extensions
--------------------

[![latest version](https://img.shields.io/nuget/v/N.EntityFramework.Extensions)](https://www.nuget.org/packages/N.EntityFramework.Extensions)

**If you are using Entity Framework Core v5.0.1+, you can use https://github.com/NorthernLight1/N.EntityFrameworkCore.Extensions

## Bulk data support for the EntityFramework 6.2.0+

The framework currently supports the following operations:

Entity Framework Extensions extends your DbContext with high-performance bulk operations: BulkDelete, BulkInsert, BulkMerge, BulkSync, BulkUpdate, Fetch, FromSqlQuery, DeleteFromQuery, InsertFromQuery, UpdateFromQuery, QueryToCsvFile, SqlQueryToCsvFile

Inheritance models supported: Table-Per-Hierarchy, Table-Per-Concrete

  ### Installation

  The latest stable version is available on [NuGet](https://www.nuget.org/packages/N.EntityFramework.Extensions).

  ```sh
  Install-Package N.EntityFramework.Extensions
  ```

 ## Usage
   
 **BulkInsert()**  
   ```
  var dbcontext = new MyDbContext();  
  var orders = new List<Order>();  
  for(int i=0; i<10000; i++)  
  {  
      orders.Add(new Order { OrderDate = DateTime.UtcNow, TotalPrice = 2.99 });  
  }  
  dbcontext.BulkInsert(orders);  
 ```
  **BulkDelete()**  
  ```
  var dbcontext = new MyDbContext();  
  var orders = dbcontext.Orders.Where(o => o.TotalPrice < 5.35M);  
  dbcontext.BulkDelete(orders);
  ```
  **BulkUpdate()**  
  ```
  var dbcontext = new MyDbContext();  
  var products = dbcontext.Products.Where(o => o.Price < 5.35M);
  foreach(var product in products)
  {
      order.Price = 6M;
  }
  dbcontext.BulkUpdate(products);
  ```
  **BulkMerge()**  
  ```
  var dbcontext = new MyDbContext();
  var products = new List<Product>();
  var existingProducts = dbcontext.Products.Where(o => o.Price < 5.35M);
  foreach(var product in existingProducts)
  {
      product.Price = 6M;
  }
  products.AddRange(existingProducts);
  products.Add(new Product { Name="Hat", Price=10.25M });
  products.Add(new Product { Name="Shirt", Price=20.95M });
  dbcontext.BulkMerge(products);
  ```
   **BulkSync() - Performs a full sync on the target databse. Any entities that do not exists in the source list will be deleted**
  ```
  var dbcontext = new MyDbContext();
  var products = new List<Product>();
  var existingProducts = dbcontext.Products.Where(o => o.Id <= 1000);
  foreach(var product in existingProducts)
  {
      product.Price = 6M;
  }
  products.AddRange(existingProducts);
  products.Add(new Product { Name="Hat", Price=10.25M });
  products.Add(new Product { Name="Shirt", Price=20.95M });
  //All existing products with Id > 1000 will be deleted
  dbcontext.BulkSync(products);
  ```
  **Fetch() - Retrieves data in batches.**  
  ```
  var dbcontext = new MyDbContext();  
  var query = dbcontext.Products.Where(o => o.Price < 5.35M);
  query.Fetch(result =>
    {
      batchCount++;
      totalCount += result.Results.Count();
    }, 
    new FetchOptions { BatchSize = 1000 }
  );
  dbcontext.BulkUpdate(products);
  ```
  **DeleteFromQuery()**  
   ``` 
  var dbcontext = new MyDbContext(); 
  
  //This will delete all products  
  dbcontext.Products.DeleteFromQuery() 
  
  //This will delete all products that are under $5.35  
  dbcontext.Products.Where(x => x.Price < 5.35M).DeleteFromQuery()  
```
  **InsertFromQuery()**  
   ``` 
  var dbcontext = new MyDbContext(); 
  
  //This will take all products priced under $10 from the Products table and 
  //insert it into the ProductsUnderTen table
  dbcontext.Products.Where(x => x.Price < 10M).InsertFromQuery("ProductsUnderTen", o => new { o.Id, o.Price });
```
  **UpdateFromQuery()**  
   ``` 
  var dbcontext = new MyDbContext(); 
  
  //This will change all products priced at $5.35 to $5.75 
  dbcontext.Products.Where(x => x.Price == 5.35M).UpdateFromQuery(o => new Product { Price = 5.75M }) 
```
  
