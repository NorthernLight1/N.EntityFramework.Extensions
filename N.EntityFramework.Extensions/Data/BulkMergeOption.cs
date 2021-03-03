using N.EntityFramework.Extensions.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace N.EntityFramework.Extensions
{
    public class BulkMergeOptions<T> : BulkOptions
    {
        public Expression<Func<T, T, bool>> MergeOnCondition { get; set; }
        public Func<T, T, bool> NotMatchedBySourceCondition { get; set; }
        public Func<T, T, bool> NotMatchedByTargetCondition { get; set; }
        public Func<T, T, bool> MatchedCondition { get; set; }
        public Expression<Func<T, object>> IgnoreColumnsOnInsert { get; set; }
        public Expression<Func<T, object>> IgnoreColumnsOnUpdate { get; set; }
        public bool AutoMapOutputIdentity { get; set; }

        public BulkMergeOptions()
        {
            this.AutoMapOutputIdentity = true;
        }
        public List<string> GetIgnoreColumnsOnInsert()
        {
            return this.IgnoreColumnsOnInsert == null ? new List<string>() : this.IgnoreColumnsOnInsert.Body.Type.GetProperties().Select(o => o.Name).ToList();
        }
        public List<string> GetIgnoreColumnsOnUpdate()
        {
            return this.IgnoreColumnsOnUpdate == null ? new List<string>() : this.IgnoreColumnsOnUpdate.Body.Type.GetProperties().Select(o => o.Name).ToList();
        }
        internal string GetMergeOnConditionSql(string[] storeGeneratedColumnNames)
        {
            string mergeOnConditionSql = string.Empty;
            if (this.MergeOnCondition != null)
            {
                mergeOnConditionSql = this.MergeOnCondition.ToSqlPredicate("s", "t");
            }
            else
            {
                int i = 1;
                foreach(var storeGeneratedColumnName in storeGeneratedColumnNames)
                {
                    mergeOnConditionSql += (i > 1 ? "AND" : "") + string.Format("s.{0}=t.{0}", storeGeneratedColumnName);
                    i++;
                }
            }
            return mergeOnConditionSql;
        }
    }
}
