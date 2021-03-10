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
            string newSqlText = string.Format("SELECT COUNT(*) FROM ({0}) s", this.SqlText);
            return (int)SqlUtil.ExecuteScalar(newSqlText, this.Connection, null, this.Parameters);
        }
        public int ExecuteNonQuery()
        {
            return SqlUtil.ExecuteSql(this.SqlText, this.Connection, null, this.Parameters);
        }
    }
}
