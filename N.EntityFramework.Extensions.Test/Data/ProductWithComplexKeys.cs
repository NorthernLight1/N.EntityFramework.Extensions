using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace N.EntityFramework.Extensions.Test.Data
{
    public class ProductWithComplexKey
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key, Column(Order = 1)]
        public Guid Key1 { get; set; }
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key, Column(Order = 2)]
        public Guid Key2 { get; set; }
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key, Column(Order = 3)]
        public Guid Key3 { get; set; }
        public decimal Price { get; set; }
        public bool OutOfStock { get; set; }
        [Column("Status")]
        [StringLength(25)]
        public string StatusString { get; set; }
        public DateTime? UpdatedDateTime { get; set; }
    }
}
