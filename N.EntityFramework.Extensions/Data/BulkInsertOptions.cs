using System;
using System.Linq;
using System.Linq.Expressions;

namespace N.EntityFramework.Extensions
{
    public class BulkInsertOptions<T> : BulkOptions
    {
        public Expression<Func<T, object>> IgnoreColumns { get; set; }
        public Expression<Func<T, object>> InputColumns { get; set; }
        public bool AutoMapOutput { get; set; }
        [Obsolete("BulkMergeOptions.AutoMapOutputIdentity has been replaced by AutoMapOutput.")]
        public bool AutoMapOutputIdentity { get { return AutoMapOutput; } set { AutoMapOutput = value; } }
        public bool KeepIdentity { get; set; }
        public bool InsertIfNotExists { get; set; }
        public Expression<Func<T, T, bool>> InsertOnCondition { get; set; }

        public string[] GetInputColumns()
        {
            return this.InputColumns == null ? null : this.InputColumns.Body.Type.GetProperties().Select(o => o.Name).ToArray();
        }

        public BulkInsertOptions()
        {
            this.AutoMapOutput = true;
            this.InsertIfNotExists = false;
        }
    }
}