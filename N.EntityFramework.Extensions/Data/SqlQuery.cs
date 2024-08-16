using System;
using System.Data.Common;
using System.Threading.Tasks;
using N.EntityFramework.Extensions.Sql;

namespace N.EntityFramework.Extensions
{
    public class SqlQuery
    {
        private DbConnection Connection { get; set; }
        public string SqlText { get; private set; }
        public object[] Parameters { get; private set; }

        public SqlQuery(DbConnection sqlConnection, String sqlText, params object[] parameters)
        {
            this.Connection = sqlConnection;
            this.SqlText = sqlText;
            this.Parameters = parameters;
        }
        public int Count()
        {
            string countSqlText = SqlBuilder.Parse(this.SqlText).Count();
            return (int)SqlUtil.ExecuteScalar(countSqlText, this.Connection, null, this.Parameters);
        }
        public async Task<int> CountAsync()
        {
            string countSqlText = SqlBuilder.Parse(this.SqlText).Count();
            return (int)(await SqlUtil.ExecuteScalarAsync(countSqlText, this.Connection, null, this.Parameters));
        }
    }
}