using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using N.EntityFramework.Extensions.Test.Data.Enums;

namespace N.EntityFramework.Extensions.Test.Data
{
    public class ProductWithTrigger
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public string Id { get; set; }
        public decimal Price { get; set; }
        public bool OutOfStock { get; set; }
        [Column("Status")]
        [StringLength(25)]
        public string StatusString { get; set; }
        public int? ProductCategoryId { get; set; }
        public ProductStatus? StatusEnum { get; set; }
        public DateTime? UpdatedDateTime { get; set; }
        [Timestamp]
        public byte[] RowVersion { get; set; }

        public virtual ProductCategory ProductCategory { get; set; }
        public ProductWithTrigger()
        {

        }
    }
}