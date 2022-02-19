using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace N.EntityFramework.Extensions
{
    public static class DabaseExtensionsAsync
    {
        public async static Task<int> ClearTableAsync(this Database database, string tableName, CancellationToken cancellationToken = default)
        {
            return await database.ExecuteSqlCommandAsync(string.Format("DELETE FROM {0}", tableName), cancellationToken);
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
