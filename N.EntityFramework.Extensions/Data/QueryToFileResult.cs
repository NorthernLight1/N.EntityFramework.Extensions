using System.Collections.Generic;

namespace N.EntityFramework.Extensions
{
    public class QueryToFileResult
    {
        public long BytesWritten { get; set; }
        public int DataRowCount { get; internal set; }
        public int TotalRowCount { get; internal set; }
    }
}