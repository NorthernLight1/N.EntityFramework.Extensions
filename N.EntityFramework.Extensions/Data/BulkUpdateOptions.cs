using System;
using System.Linq.Expressions;

namespace N.EntityFramework.Extensions
{
    public class BulkUpdateOptions<T> : BulkOptions
    {
        public Expression<Func<T, object>> IgnoreColumnsOnUpdate { get; set; }
    }
}