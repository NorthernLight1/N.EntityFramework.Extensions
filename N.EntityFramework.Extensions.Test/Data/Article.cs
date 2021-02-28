using System;
using System.ComponentModel.DataAnnotations;

namespace N.EntityFramework.Extensions.Test.Data
{
    public class Article
    {
        [Key]
        public string ArticleId { get; set; }
        public decimal Price { get; set; }
        public bool OutOfStock { get; set; }
        public DateTime? Updated { get; set; }
        public Article()
        {

        }
    }
}
