using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace N.EntityFramework.BulkExtensions
{
    public class BulkMergeOptions<T>
    {
        public Expression<Func<T, T, bool>> MergeOnCondition { get; set; }
        public Func<T, T, bool> NotMatchedBySourceCondition { get; set; }
        public Func<T, T, bool> NotMatchedByTargetCondition { get; set; }
        public Func<T, T, bool> MatchedCondition { get; set; }
        public Expression<Func<T, object>> IgnoreColumnsOnInsert { get; set; }
        public Expression<Func<T, object>> IgnoreColumnsOnUpdate { get; set; }
        public BulkMergeOptions()
        {

        }
        public List<string> GetIgnoreColumnsOnInsert()
        {
            return this.IgnoreColumnsOnInsert == null ? new List<string>() : this.IgnoreColumnsOnInsert.Body.Type.GetProperties().Select(o => o.Name).ToList();
        }
        public List<string> GetIgnoreColumnsOnUpdate()
        {
            return this.IgnoreColumnsOnUpdate == null ? new List<string>() : this.IgnoreColumnsOnUpdate.Body.Type.GetProperties().Select(o => o.Name).ToList();
        }
    }
}
