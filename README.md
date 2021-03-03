# N.EntityFramework.Extensions

**If you are using Entity Framework Core v5.0.1+, you can use https://github.com/NorthernLight1/N.EntityFrameworkCore.Extensions

## Bulk data support for the EntityFramework 6.4.4+

The framework currently supports the following operations:

  BulkDelete, BulkInsert, BulkMerge, BulkUpdate, DeleteFromQuery, InsertFromQuery, UpdateFromQuery
  
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
  **Fetch() - Retrieve data in batches**  
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
  
