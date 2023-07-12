using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace N.EntityFramework.Extensions.Test.Data
{
    public class Order
    {
        [Key]
        public long Id { get; set; }
        public string ExternalId { get; set; }
        public Guid GlobalId { get; set; }
        public decimal Price { get; set; }
        public DateTime AddedDateTime { get; set; }
        public DateTime? ModifiedDateTime { get; set; }
        public bool? Trigger { get; set; }
        public bool Active { get; set; }
        public Order()
        {
            AddedDateTime = DateTime.UtcNow;
            Active = true;
        }
    }
}