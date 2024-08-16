using System.Data.Common;
using System.Data.Entity.Infrastructure.Interception;

namespace N.EntityFramework.Extensions
{
    class EfExtensionsCommand
    {
        public EfExtensionsCommandType CommandType { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public DbConnection Connection { get; internal set; }

        internal bool Execute(DbCommand command, DbCommandInterceptionContext<DbDataReader> interceptionContext)
        {
            if (CommandType == EfExtensionsCommandType.ChangeTableName)
            {
                command.CommandText = command.CommandText.Replace(OldValue, NewValue);
            }

            return true;
        }
    }
}