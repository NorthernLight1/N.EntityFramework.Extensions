# N.EntityFramework.Extensions

## Bulk data support for the EntityFramework 6.4.4+

The framework currently supports the following operations:

  BulkInsert(), BulkMerge(), BulkDelete(), Delete()
  
 ## Examples
 
  **BulkDelete()**  
  
  var dbcontext = new MyDbContext();  
  var orders = dbcontext.Orders.Where(o => o.Price < 5.35M);  
  dbcontext.BulkDelete(orders);
  
  **Delete()**  
    
  var dbcontext = new MyDbContext(); 
  
  //This will delete all orders  
  dbcontext.Orders.Delete() 
  
  //This will delete all orders that are under $5.35  
  dbcontext.Orders.Where(o => o.Price < 5.35M).Delete()  


## Future support will include:

  BulkQuery(), BulkUpdate()
  
