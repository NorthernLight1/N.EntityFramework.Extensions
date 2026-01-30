using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

namespace N.EntityFramework.Extensions
{
    internal static class SqlUtil
    {
        internal static int ExecuteSql(string query, DbConnection connection, DbTransaction transaction, int? commandTimeout = null)
        {
            return SqlUtil.ExecuteSql(query, connection, transaction, null, commandTimeout);
        }
        internal static int ExecuteSql(string query, DbConnection connection, DbTransaction transaction, object[] parameters = null, int? commandTimeout = null)
        {
            using (var dbCommand = connection.CreateCommand())
            {
                dbCommand.CommandText = query;
                if (transaction != null)
                    dbCommand.Transaction = transaction;
                if (connection.State == ConnectionState.Closed)
                    connection.Open();
                if (commandTimeout.HasValue)
                    dbCommand.CommandTimeout = commandTimeout.Value;
                if (parameters != null)
                    dbCommand.Parameters.AddRange(parameters);
                return dbCommand.ExecuteNonQuery();
            }
        }
        internal static async System.Threading.Tasks.Task<int> ExecuteSqlAsync(string query, DbConnection connection, DbTransaction transaction, object[] parameters = null, int? commandTimeout = null)
        {
            using (var dbCommand = connection.CreateCommand())
            {
                dbCommand.CommandText = query;
                if (transaction != null)
                    dbCommand.Transaction = transaction;
                if (connection.State == ConnectionState.Closed)
                    await connection.OpenAsync();
                if (commandTimeout.HasValue)
                    dbCommand.CommandTimeout = commandTimeout.Value;
                if (parameters != null)
                    dbCommand.Parameters.AddRange(parameters);
                return await dbCommand.ExecuteNonQueryAsync();
            }
        }
        internal static object ExecuteScalar(string query, DbConnection connection, DbTransaction transaction, object[] parameters = null, int? commandTimeout = null)
        {
            
            using (var dbCommand = connection.CreateCommand())
            {
                dbCommand.CommandText = query;
                if (transaction != null)
                    dbCommand.Transaction = transaction;
                if (connection.State == ConnectionState.Closed)
                    connection.Open();
                if (commandTimeout.HasValue)
                    dbCommand.CommandTimeout = commandTimeout.Value;
                if (parameters != null)
                    dbCommand.Parameters.AddRange(parameters);

                return dbCommand.ExecuteScalar();
            }
            
        }
        internal static async Task<object> ExecuteScalarAsync(string query, DbConnection connection, DbTransaction transaction, object[] parameters = null, int? commandTimeout = null)
        {
            using (var dbCommand = connection.CreateCommand())
            {
                dbCommand.CommandText = query;
                if (transaction != null)
                    dbCommand.Transaction = transaction;
                if (connection.State == ConnectionState.Closed)
                    await connection.OpenAsync();
                if (commandTimeout.HasValue)
                    dbCommand.CommandTimeout = commandTimeout.Value;
                if (parameters != null)
                    dbCommand.Parameters.AddRange(parameters);

                return await dbCommand.ExecuteScalarAsync();
            }
        }
        internal static int ClearTable(string tableName, DbConnection connection, DbTransaction transaction)
        {
            return ExecuteSql(string.Format("DELETE FROM {0}", tableName), connection, transaction, null);
        }
        internal static string ConvertToColumnString(IEnumerable<string> columnNames)
        {
            return string.Join(",", columnNames);
        }
        internal static int ToggleIdentityInsert(bool enable, string tableName, DbConnection dbConnection, DbTransaction dbTransaction)
        {
            string boolString = enable ? "ON" : "OFF";
            return ExecuteSql(string.Format("SET IDENTITY_INSERT {0} {1}", tableName, boolString), dbConnection, dbTransaction, null);
        }

        internal static bool TableExists(string tableName, DbConnection dbConnection, DbTransaction dbTransaction)
        {
            return Convert.ToBoolean(ExecuteScalar(string.Format("SELECT CASE WHEN OBJECT_ID(N'{0}', N'U') IS NOT NULL THEN 1 ELSE 0 END", tableName),
                dbConnection, dbTransaction, null));
        }

        internal static object GetDBValue(object value)
        {
            return value == DBNull.Value ? null : value;
        }
    }
}