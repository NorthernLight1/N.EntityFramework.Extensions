using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace N.EntityFramework.Extensions.Sql
{
    class SqlQuery
    {
        private static IEnumerable<string> keywords = new string[] { "SELECT", "FROM", "WHERE" };
        public string Sql
        {
            get { return this.ToString(); }
        }
        public List<SqlClause> Clauses { get; private set; }
        private SqlQuery(string sql)
        {
            Clauses = new List<SqlClause>();
            Initialize(sql);
        }

        private void Initialize(string sqlText)
        {
            string curClause = string.Empty;
            int curClauseIndex = 0;
            for (int i = 0; i < sqlText.Length;)
            {
                //Find new Sql clause
                int maxLenToSearch = sqlText.Length - i >= 6 ? 6 : sqlText.Length - i;
                string keyword = StartsWithString(sqlText.Substring(i, maxLenToSearch), keywords, StringComparison.OrdinalIgnoreCase);
                //Process Sql clause
                if (keyword != null && curClause != keyword)
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
        public override string ToString()
        {
            return string.Join("\r\n", Clauses.Select(o => o.ToString()));
        }
        private static string StartsWithString(string textToSearch, IEnumerable<string> valuesToFind, StringComparison stringComparison)
        {
            string value=null;
            foreach (var valueToFind in valuesToFind)
            {
                if (textToSearch.StartsWith(valueToFind, stringComparison))
                {
                    value = valueToFind;
                    break;
                }
            }

            return value;
        }

        public static SqlQuery Parse(string sql)
        {
            return new SqlQuery(sql);
        }

        public void ChangeToDelete(string expression)
        {
            var sqlClause = Clauses.FirstOrDefault();
            if(sqlClause != null)
            {
                sqlClause.Name = "DELETE";
                sqlClause.InputText = expression;
            }
            else
            {
                throw new Exception("You must parse a valid sql statement before you can use this funciton.");
            }
        }
    }
}
