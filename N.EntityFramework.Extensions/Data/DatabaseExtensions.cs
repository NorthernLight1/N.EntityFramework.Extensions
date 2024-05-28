using N.EntityFramework.Extensions.Enums;
using N.EntityFramework.Extensions.Util;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;

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
        internal static int CloneTable(this Database database, string sourceTable, string destinationTable, IEnumerable<string> columnNames = null, string internalIdColumnName = null)
        {
            string columns = columnNames != null && columnNames.Count() > 0 ? string.Join(",", CommonUtil.FormatColumns(columnNames)) : "*";
            columns = !string.IsNullOrEmpty(internalIdColumnName) ? string.Format("{0},CAST( NULL AS INT) AS {1}", columns, internalIdColumnName) : columns;
            return database.ExecuteSqlCommand(TransactionalBehavior.DoNotEnsureTransaction,string.Format("SELECT TOP 0 {0} INTO {1} FROM {2}", columns, destinationTable, sourceTable));
        }
        public static int DropTable(this Database database, string tableName, bool ifExists = false)
        {
            tableName = CommonUtil.FormatTableName(tableName);
            bool deleteTable = !ifExists || (ifExists && database.TableExists(tableName)) ? true : false;
            return deleteTable ? database.ExecuteSqlCommand(TransactionalBehavior.DoNotEnsureTransaction, string.Format("DROP TABLE {0}", tableName)) : -1;
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
            return Convert.ToBoolean(database.ExecuteScalar(string.Format("SELECT CASE WHEN OBJECT_ID(N'{0}', N'U') IS NOT NULL THEN 1 ELSE 0 END", tableName)));
        }
        internal static DbCommand CreateCommand(this Database database, ConnectionBehavior connectionBehavior = ConnectionBehavior.Default)
        {
            var dbConnection = database.GetConnection(connectionBehavior);
            if (dbConnection.State != ConnectionState.Open)
                dbConnection.Open();
            return dbConnection.CreateCommand();
        }
        internal static object ExecuteScalar(this Database database, string query, object[] parameters = null, int? commandTimeout = null)
        {
            object value;
            var dbConnection = database.Connection as SqlConnection;
            using (var sqlCommand = dbConnection.CreateCommand())
            {
                sqlCommand.CommandText = query;
                if (database.CurrentTransaction != null)
                    sqlCommand.Transaction = database.CurrentTransaction.UnderlyingTransaction as SqlTransaction;
                if (dbConnection.State == ConnectionState.Closed)
                    dbConnection.Open();
                if (commandTimeout.HasValue)
                    sqlCommand.CommandTimeout = commandTimeout.Value;
                if (parameters != null)
                    sqlCommand.Parameters.AddRange(parameters);
                value = sqlCommand.ExecuteScalar();
            }
            return value;
        }
        internal static DbConnection GetConnection(this Database database,ConnectionBehavior connectionBehavior)
        {
            return connectionBehavior == ConnectionBehavior.New ? ((ICloneable)database.Connection).Clone() as DbConnection : database.Connection;
        }
    }
}
