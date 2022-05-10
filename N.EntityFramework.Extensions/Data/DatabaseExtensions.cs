using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Core.EntityClient;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace N.EntityFramework.Extensions
{
    public static class DatabaseExtensions
    {
        public static SqlQuery FromSqlQuery(this Database database, string sqlText, params object[] parameters)
        {
            var dbConnection = database.Connection as SqlConnection;
            return new SqlQuery(dbConnection, sqlText, parameters);
        }
        public static int ClearTable(this Database database, string tableName)
        {
            var dbConnection = database.Connection as SqlConnection;
            return SqlUtil.ClearTable(tableName, dbConnection, null);
        }
        internal static int CloneTable(this Database database, string sourceTable, string destinationTable, string[] columnNames = null, string internalIdColumnName = null)
        {
            string columns = columnNames != null && columnNames.Length > 0 ? string.Join(",", columnNames) : "*";
            columns = !string.IsNullOrEmpty(internalIdColumnName) ? string.Format("{0},CAST( NULL AS INT) AS {1}", columns, internalIdColumnName) : columns;
            return database.ExecuteSqlCommand(string.Format("SELECT TOP 0 {0} INTO {1} FROM {2}", columns, destinationTable, sourceTable));
        }
        public static int DropTable(this Database database, string tableName, bool ifExists = false)
        {
            bool deleteTable = !ifExists || (ifExists && database.TableExists(tableName)) ? true : false;
            return deleteTable ? database.ExecuteSqlCommand(string.Format("DROP TABLE {0}", tableName)) : -1;
        }
        public static void TruncateTable(this Database database, string tableName, bool ifExists = false)
        {
            var dbConnection = database.Connection as SqlConnection;
            bool truncateTable = !ifExists || (ifExists && SqlUtil.TableExists(tableName, dbConnection, null)) ? true : false;
            if (truncateTable)
            {
                database.ExecuteSqlCommand(string.Format("TRUNCATE TABLE {0}", tableName));
            }
        }
        public static bool TableExists(this Database database, string tableName)
        {
            var dbTransaction = database.CurrentTransaction != null ? database.CurrentTransaction.UnderlyingTransaction as SqlTransaction : null;
            var dbConnection = database.Connection as SqlConnection;
            return SqlUtil.TableExists(tableName, dbConnection, dbTransaction);
        }
    }
}
