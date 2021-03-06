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
        //public BulkSyncOptions(BulkMergeOptions<T> options)
        //{
        //    AutoMapOutputIdentity = options.AutoMapOutputIdentity;
        //    BatchSize = options.BatchSize;
        //    CommandTimeout = options.CommandTimeout;
        //    IgnoreColumnsOnInsert = options.IgnoreColumnsOnInsert;
        //    IgnoreColumnsOnUpdate = options.IgnoreColumnsOnUpdate;
        //    MergeOnCondition = options.MergeOnCondition;
        //    TableName = options.TableName;
        //    UsePermanentTable = options.UsePermanentTable;
        //    DeleteIfNotMatched = false;
        //}
    }
}
