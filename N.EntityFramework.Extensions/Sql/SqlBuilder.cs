using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.Entity.Core.Objects;
using System.Linq;
using System.Linq.Expressions;

namespace N.EntityFramework.Extensions.Sql
{
    internal class SqlBuilder
    {
        private static IEnumerable<string> keywords = new string[] { "SELECT", "FROM", "WHERE", "GROUP BY", "ORDER BY" };
        public string Sql
        {
            get { return this.ToString(); }
        }
        public DbParameter[] Parameters { get; private set; }
        public List<SqlClause> Clauses { get; private set; }
        private SqlBuilder(string sql, DbParameter[] parameters = default)
        {
            Clauses = new List<SqlClause>();
            Parameters = parameters == null ? new DbParameter[0] : parameters;
            Initialize(sql);
        }

        private void Initialize(string sqlText)
        {
            //Clean Sql Text
            sqlText = sqlText.Replace("\r\n", " ");
            //Process Sql Text
            string curClause = string.Empty;
            int curClauseIndex = 0, wrappedCount = 0;
            for (int i = 0; i < sqlText.Length;)
            {
                //Find new Sql clause
                int maxLenToSearch = sqlText.Length - i >= 10 ? 10 : sqlText.Length - i;
                string keyword = StartsWithString(sqlText.Substring(i, maxLenToSearch), keywords, StringComparison.OrdinalIgnoreCase);
                bool isWordStart = i > 0 ? sqlText[i - 1] == ' ' : true;

                if (sqlText[i] == '(')
                    wrappedCount++;
                else if (sqlText[i] == ')')
                    wrappedCount--;

                //Process Sql clause
                if (keyword != null && curClause != keyword && isWordStart && wrappedCount == 0)
                {
                    if (!string.IsNullOrEmpty(curClause))
                    {
                        Clauses.Add(SqlClause.Parse(curClause, sqlText.Substring(curClauseIndex, i - curClauseIndex)));
                    }
                    curClause = keyword;
                    curClauseIndex = i + curClause.Length;
                    i = i + curClause.Length;
                }
                else
                {
                    i++;
                }
            }
            if (!string.IsNullOrEmpty(curClause))
                Clauses.Add(SqlClause.Parse(curClause, sqlText.Substring(curClauseIndex)));
        }
        public string Count()
        {
            return string.Format("SELECT COUNT(*) FROM ({0}) s", string.Join("\r\n", Clauses.Where(o => o.Name != "ORDER BY").Select(o => o.ToString())));
        }
        public String GetTableAlias()
        {
            var sqlFromClause = Clauses.First(o => o.Name == "FROM");
            var startIndex = sqlFromClause.InputText.LastIndexOf(" AS ");
            return startIndex > 0 ? sqlFromClause.InputText.Substring(startIndex + 4) : "";
        }
        public override string ToString()
        {
            return string.Join("\r\n", Clauses.Select(o => o.ToString()));
        }
        private static string StartsWithString(string textToSearch, IEnumerable<string> valuesToFind, StringComparison stringComparison)
        {
            string value = null;
            foreach (var valueToFind in valuesToFind)
            {
                bool isWord = textToSearch.Length > valueToFind.Length && textToSearch[valueToFind.Length] == ' ';
                if (textToSearch.StartsWith(valueToFind, stringComparison) && isWord)
                {
                    value = valueToFind;
                    break;
                }
            }

            return value;
        }
        public static SqlBuilder Parse(string sql)
        {
            return new SqlBuilder(sql);
        }
        public static SqlBuilder Parse<T>(string sql, ObjectQuery<T> objectQuery)
        {
            var sqlParameters = new List<DbParameter>();

            if (objectQuery != null)
            {
                foreach (var parameter in objectQuery.Parameters)
                {
                    DbParameter sqlParameter;
                    DbConnection connection = objectQuery.Context.Connection is System.Data.Entity.Core.EntityClient.EntityConnection entityConnection ? entityConnection.StoreConnection : objectQuery.Context.Connection;

                    if (connection is System.Data.SqlClient.SqlConnection)
                    {
                        sqlParameter = new System.Data.SqlClient.SqlParameter(parameter.Name, parameter.Value);
                    }
                    else if (connection is Microsoft.Data.SqlClient.SqlConnection)
                    {
                        sqlParameter = new Microsoft.Data.SqlClient.SqlParameter(parameter.Name, parameter.Value);
                    }
                    else
                    {
                        throw new NotSupportedException("Unsupported provider: " + objectQuery.Context.Connection.GetType().Namespace);
                    }

                    if (sqlParameter.DbType == System.Data.DbType.DateTime)
                    {
                        sqlParameter.DbType = System.Data.DbType.DateTime2;
                    }

                    sqlParameters.Add(sqlParameter);
                }
            }

            return new SqlBuilder(sql, sqlParameters.ToArray());
        }
        public void ChangeToDelete()
        {
            Validate();
            var sqlClause = Clauses.FirstOrDefault();
            var sqlFromClause = Clauses.First(o => o.Name == "FROM");
            if (sqlClause != null)
            {
                sqlClause.Name = "DELETE";
                int searchStartIndex = sqlFromClause.InputText.LastIndexOf(")");
                searchStartIndex = searchStartIndex == -1 ? 0 : searchStartIndex;
                int aliasStartIndex = sqlFromClause.InputText.IndexOf("AS ", searchStartIndex) + 3;
                int aliasLength = sqlFromClause.InputText.IndexOf("]", aliasStartIndex) - aliasStartIndex + 1;
                sqlClause.InputText = sqlFromClause.InputText.Substring(aliasStartIndex, aliasLength);
            }
        }
        public void ChangeToUpdate<T>(string tableName, Expression<Func<T, T>> updateExpression)
        {
            Validate();
            string setSqlExpression = updateExpression.ToSqlUpdateSetExpression(tableName);
            var sqlClause = Clauses.FirstOrDefault();
            if (sqlClause != null)
            {
                sqlClause.Name = "UPDATE";
                sqlClause.InputText = tableName;
                Clauses.Insert(1, new SqlClause { Name = "SET", InputText = setSqlExpression });
            }
        }
        internal void ChangeToInsert<T>(string tableName, Expression<Func<T, object>> insertObjectExpression)
        {
            Validate();
            var sqlSelectClause = Clauses.FirstOrDefault();
            string columnsToInsert = string.Join(",", insertObjectExpression.GetObjectProperties());
            string insertValueExpression = string.Format("INTO {0} ({1})", tableName, columnsToInsert);
            Clauses.Insert(0, new SqlClause { Name = "INSERT", InputText = insertValueExpression });
            sqlSelectClause.InputText = columnsToInsert;
        }
        internal void SelectColumns(IEnumerable<string> columns)
        {
            var tableAlias = GetTableAlias();
            var sqlClause = Clauses.FirstOrDefault();
            if (sqlClause.Name == "SELECT")
            {
                sqlClause.InputText = string.Join(",", columns.Select(c => string.Format("{0}.{1}", tableAlias, c)));
            }
        }
        private void Validate()
        {
            if (Clauses.Count == 0)
            {
                throw new Exception("You must parse a valid sql statement before you can use this function.");
            }
        }
    }
}