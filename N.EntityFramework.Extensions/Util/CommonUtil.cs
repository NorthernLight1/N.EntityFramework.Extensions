﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace N.EntityFramework.Extensions.Util
{
    internal static class CommonUtil<T>
    {
        internal static string[] GetColumns(Expression<Func<T, T, bool>> expression, string[] tableNames)
        {
            List<string> foundColumns = new List<string>();
            string sqlText = (string)expression.Body.GetPrivateFieldValue("DebugView");

            int startIndex = sqlText.IndexOf("$");
            while (startIndex != -1)
            {
                int endIndex = sqlText.IndexOf(" ", startIndex);
                string column = endIndex == -1 ? sqlText.Substring(startIndex) : sqlText.Substring(startIndex, endIndex - startIndex);
                string[] columnParts = column.Split('.');
                if(tableNames == null || tableNames.Contains(columnParts[0].Remove(0,1)))
                {
                    foundColumns.Add(columnParts[1]);
                }
                startIndex = sqlText.IndexOf("$", startIndex+1);
            }

            return foundColumns.ToArray();
        }
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
