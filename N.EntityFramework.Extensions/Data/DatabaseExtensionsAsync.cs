using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using N.EntityFramework.Extensions.Util;

namespace N.EntityFramework.Extensions
{
    public static class DatabaseExtensionsAsync
    {
        public async static Task<int> ClearTableAsync(this Database database, string tableName, CancellationToken cancellationToken = default)
        {
            return await database.ExecuteSqlCommandAsync(string.Format("DELETE FROM {0}", tableName), cancellationToken);
        }
        internal async static Task<int> CloneTableAsync(this Database database, string sourceTable, string destinationTable, IEnumerable<string> columnNames, string internalIdColumnName = null, CancellationToken cancellationToken = default)
        {
            string columns = columnNames != null && columnNames.Count() > 0 ? string.Join(",", CommonUtil.FormatColumns(columnNames)) : "*";
            columns = !string.IsNullOrEmpty(internalIdColumnName) ? string.Format("{0},CAST( NULL AS INT) AS {1}", columns, internalIdColumnName) : columns;
            return await database.ExecuteSqlCommandAsync(string.Format("SELECT TOP 0 {0} INTO {1} FROM {2}", columns, destinationTable, sourceTable), cancellationToken);
        }
        public async static Task TruncateTableAsync(this Database database, string tableName, bool ifExists = false, CancellationToken cancellationToken = default)
        {
            bool truncateTable = !ifExists || (ifExists && database.TableExists(tableName)) ? true : false;
            if (truncateTable)
            {
                await database.ExecuteSqlCommandAsync(string.Format("TRUNCATE TABLE {0}", tableName), cancellationToken);
            }
        }
    }
}