# N.EntityFramework.Extensions

## Bulk data support for the EntityFramework 6.4.4+

The framework currently supports the following operations:

  BulkInsert(), BulkMerge(), BulkDelete(), Delete()
  
 ## Examples
   
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
  **Delete()**  
   ``` 
  var dbcontext = new MyDbContext(); 
  
  //This will delete all orders  
  dbcontext.Orders.Delete() 
  
  //This will delete all orders that are under $5.35  
  dbcontext.Orders.Where(o => o.TotalPrice < 5.35M).Delete()  
```

## Future support will include:

  BulkQuery(), BulkUpdate()
  
