using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace N.EntityFramework.Extensions
{
    static class LinqExtensions
    {
        internal static object GetExpressionValue(MemberBinding binding)
        {
            if (binding.GetPrivateFieldValue("Expression") is ConstantExpression constantExpression)
            {
                return constantExpression.Value;
            }

            return Expression.Lambda(binding.GetPrivateFieldValue("Expression") as Expression).Compile().DynamicInvoke();
        }
        internal static string GetExpressionValueAsString<T>(MemberBinding binding)
        {
            var value = GetExpressionValue(binding);

            if (value == null)
                return "NULL";
            if (value is string str)
                return "'" + str.Replace("'", "''") + "'";
            if (value is bool b)
                return b ? "1" : "0";
            if (value is DateTime dt)
                return "'" + dt.ToString("yyyy-MM-ddTHH:mm:ss.fff") + "'"; // Convert to ISO-8601
            if (!value.GetType().IsClass)
                return Convert.ToString(value, CultureInfo.InvariantCulture);

            throw new NotImplementedException("Unhandled data type.");
        }
        public static List<string> GetObjectProperties<T>(this Expression<Func<T, object>> expression)
        {
            return expression == null ? new List<string>() : expression.Body.Type.GetProperties().Select(o => o.Name).ToList();
        }
        internal static string ToSqlPredicate<T>(this Expression<T> expression, params string[] parameters)
        {
            var stringBuilder = new StringBuilder((string)expression.Body.GetPrivateFieldValue("DebugView"));
            int i = 0;
            foreach (var expressionParam in expression.Parameters)
            {
                if (parameters.Length <= i) break;
                stringBuilder.Replace((string)expressionParam.GetPrivateFieldValue("DebugView"), parameters[i]);
                i++;
            }
            stringBuilder.Replace("&&", "AND");
            stringBuilder.Replace("==", "=");
            return stringBuilder.ToString();
        }
        internal static string ToSqlUpdateSetExpression<T>(this Expression<T> expression, string tableName)
        {
            List<string> setValues = new List<string>();
            var memberInitExpression = expression.Body as MemberInitExpression;
            foreach (var binding in memberInitExpression.Bindings)
            {
                string constantValue = GetExpressionValueAsString<T>(binding);

                setValues.Add(string.Format("[{0}].[{1}]={2}", tableName, binding.Member.Name, constantValue));
            }
            return string.Join(",", setValues);
        }
    }
}
