using System;
using System.Linq.Expressions;

namespace N.EntityFramework.Extensions
{
    public class BulkInsertOptions<T>
    {
        public Expression<Func<T, object>> InputColumns { get; set; }
    }
}