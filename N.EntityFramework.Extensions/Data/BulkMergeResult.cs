﻿using System.Collections.Generic;

namespace N.EntityFramework.Extensions
{
    public class BulkMergeResult<T>
    {
        public IEnumerable<BulkMergeOutputRow<T>> Output { get; set; }
        public int RowsAffected { get; set; }
        internal virtual int RowsDeleted { get; set; }
        public int RowsInserted { get; internal set; }
        public int RowsUpdated { get; internal set; }

    }
}