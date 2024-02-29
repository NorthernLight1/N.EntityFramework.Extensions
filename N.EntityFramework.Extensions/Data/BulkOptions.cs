using N.EntityFramework.Extensions.Enums;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace N.EntityFramework.Extensions
{
    public class BulkOptions
    {
        public int BatchSize { get; set; }
        public bool UsePermanentTable { get; set; }
        public int? CommandTimeout { get; set; }
        internal TransactionalBehavior TransactionalBehavior { get; set; }
        internal ConnectionBehavior ConnectionBehavior { get; set; }
        internal Type ClrType { get; set; }

        public BulkOptions()
        {
            BatchSize = 1000;
            TransactionalBehavior = TransactionalBehavior.EnsureTransaction;
            ConnectionBehavior = ConnectionBehavior.Default;
        }
    }
}
