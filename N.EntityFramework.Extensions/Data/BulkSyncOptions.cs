using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace N.EntityFramework.Extensions
{
    public class BulkSyncOptions<T> : BulkMergeOptions<T>
    {
        public BulkSyncOptions() 
        {
            this.DeleteIfNotMatched = true;
        }
    }
}
