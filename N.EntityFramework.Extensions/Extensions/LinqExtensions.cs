using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace N.EntityFramework.Extensions
{
    static class LinqExtensions
    {
        public static List<string> GetObjectProperties<T>(this Expression<Func<T, object>> expression)
        {
            return expression == null ? new List<string>() : expression.Body.Type.GetProperties().Select(o => o.Name).ToList();
        }
    }
}
