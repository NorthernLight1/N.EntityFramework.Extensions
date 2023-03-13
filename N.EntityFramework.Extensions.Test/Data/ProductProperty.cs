using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace N.EntityFramework.Extensions.Test.Data
{
    public class ProductProperty
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public string Id { get; set; }

        public string Description { get; set; }
        public string OtherProperty { get; set; }

        public Product Product { get; set; }
    }
}
