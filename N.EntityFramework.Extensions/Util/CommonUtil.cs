using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace N.EntityFramework.Extensions.Util
{
    internal static class CommonUtil<T>
    {
        internal static string GetJoinConditionSql(Expression<Func<T, T, bool>> joinKeyExpression, string[] storeGeneratedColumnNames, string sourceTableName="s", string targetTableName="t")
        {
            string joinConditionSql = string.Empty;
            if (joinKeyExpression != null)
            {
                joinConditionSql = joinKeyExpression.ToSqlPredicate(sourceTableName, targetTableName);
            }
            else
            {
                int i = 1;
                foreach (var storeGeneratedColumnName in storeGeneratedColumnNames)
                {
                    joinConditionSql += (i > 1 ? "AND" : "") + string.Format("{0}.{2}={1}.{2}", sourceTableName, targetTableName, storeGeneratedColumnName);
                    i++;
                }
            }
            return joinConditionSql;
        }
    }
}
