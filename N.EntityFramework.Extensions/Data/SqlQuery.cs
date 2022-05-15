using N.EntityFramework.Extensions.Sql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace N.EntityFramework.Extensions
{
    public class SqlQuery
    {
        private SqlConnection Connection { get; set; }
        public string SqlText { get; private set; }
        public object[] Parameters { get; private set; }

        public SqlQuery(SqlConnection sqlConnection, String sqlText, params object[] parameters)
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
        public int ExecuteNonQuery()
        {
            return SqlUtil.ExecuteSql(this.SqlText, this.Connection, null, this.Parameters);
        }
    }
}
