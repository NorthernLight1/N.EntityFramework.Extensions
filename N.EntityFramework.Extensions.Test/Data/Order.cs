using System.ComponentModel.DataAnnotations;

namespace N.EntityFramework.Extensions.Test.Data
{
    public class Order
    {
        [Key]
        public long Id { get; set; }
        public string ExternalId { get; set; }
        public decimal Price { get; set; }
        public Order()
        {

        }
    }
}