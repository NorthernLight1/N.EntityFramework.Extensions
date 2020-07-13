using System.Data.SqlClient;

namespace N.EntityFramework.BulkExtensions
{
    internal static class SqlUtil
    {
        internal static int ExecuteSql(string query, SqlConnection connection, SqlTransaction transaction)
        {
            var sqlCommand = new SqlCommand(query, connection, transaction);
            return sqlCommand.ExecuteNonQuery();
        }
        internal static int DeleteTable(string tableName, SqlConnection connection, SqlTransaction transaction)
        {
            return ExecuteSql(string.Format("DROP TABLE {0}", tableName), connection, transaction);
        }
        internal static int CloneTable(string sourceTable, string destinationTable, SqlConnection connection, SqlTransaction transaction)
        {
            return ExecuteSql(string.Format("SELECT TOP 0 * INTO {0} FROM {1}", destinationTable, sourceTable), connection, transaction);
        }
    }
}

