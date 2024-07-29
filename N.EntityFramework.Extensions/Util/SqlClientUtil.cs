using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace N.EntityFramework.Extensions.Util
{
    internal static class SqlClientUtil
    {

        
        internal static Enum GetSqlBulkCopyOptionsDefault(DbConnection dbConnection)
        {
            if (dbConnection is System.Data.SqlClient.SqlConnection)
            {
                return (Enum)Enum.Parse(typeof(System.Data.SqlClient.SqlBulkCopyOptions), "Default");
            }
            else if (dbConnection is Microsoft.Data.SqlClient.SqlConnection)
            {
                return (Enum)Enum.Parse(typeof(Microsoft.Data.SqlClient.SqlBulkCopyOptions), "Default");
            }
            else
            {
                throw new NotSupportedException("Unsupported provider: " + dbConnection.GetType().Namespace);
            }
        }

        internal static Enum GetSqlBulkCopyOptionsKeepIdentity(DbConnection dbConnection)
        {
            if (dbConnection is System.Data.SqlClient.SqlConnection)
            {
                return (Enum)Enum.Parse(typeof(System.Data.SqlClient.SqlBulkCopyOptions), "KeepIdentity");
            }
            else if (dbConnection is Microsoft.Data.SqlClient.SqlConnection)
            {
                return (Enum)Enum.Parse(typeof(Microsoft.Data.SqlClient.SqlBulkCopyOptions), "KeepIdentity");
            }
            else
            {
                throw new NotSupportedException("Unsupported provider: " + dbConnection.GetType().Namespace);
            }
        }
        internal static dynamic CreateSqlBulkCopy(
            DbConnection dbConnection,
            DbTransaction transaction,
            string tableName,
            Enum bulkCopyOptions,
            int batchSize,
            int? commandTimeout)
        {
            dynamic sqlBulkCopy;

            if (dbConnection is System.Data.SqlClient.SqlConnection)
            {
                sqlBulkCopy = new System.Data.SqlClient.SqlBulkCopy((System.Data.SqlClient.SqlConnection)dbConnection, (System.Data.SqlClient.SqlBulkCopyOptions)bulkCopyOptions, (System.Data.SqlClient.SqlTransaction)transaction)
                {
                    DestinationTableName = tableName,
                    BatchSize = batchSize,
                    BulkCopyTimeout = commandTimeout ?? 0
                };
            }
            else if (dbConnection is Microsoft.Data.SqlClient.SqlConnection)
            {
                sqlBulkCopy = new Microsoft.Data.SqlClient.SqlBulkCopy((Microsoft.Data.SqlClient.SqlConnection)dbConnection, (Microsoft.Data.SqlClient.SqlBulkCopyOptions)bulkCopyOptions, (Microsoft.Data.SqlClient.SqlTransaction)transaction)
                {
                    DestinationTableName = tableName,
                    BatchSize = batchSize,
                    BulkCopyTimeout = commandTimeout ?? 0
                };
            }
            else
            {
                throw new NotSupportedException("Unsupported provider: " + dbConnection.GetType().Namespace);
            }

            return sqlBulkCopy;
        }

        internal static string GetClientConnectionId(DbConnection dbConnection)
        {
            if (dbConnection is System.Data.SqlClient.SqlConnection sqlConnection)
            {
                return sqlConnection.ClientConnectionId.ToString();
            }
            else if (dbConnection is Microsoft.Data.SqlClient.SqlConnection msSqlConnection)
            {
                return msSqlConnection.ClientConnectionId.ToString();
            }
            else
            {
                throw new NotSupportedException("Unsupported provider: " + dbConnection.GetType().Namespace);
            }
        }
    }
}
