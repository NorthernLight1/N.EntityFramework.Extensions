using System;
using System.ComponentModel.DataAnnotations;

namespace N.EntityFramework.Extensions.Test.Data
{
    public class Article
    {
        [Key]
        public string ArticleId { get; set; }
        public decimal Price { get; set; }
        public Article()
        {

        }
    }
}
