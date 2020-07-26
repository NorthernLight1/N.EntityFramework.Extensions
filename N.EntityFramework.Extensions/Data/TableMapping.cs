using System.Collections.Generic;
using System.Data.Entity.Core.Mapping;
using System.Data.Entity.Core.Metadata.Edm;

namespace N.EntityFramework.Extensions
{
    public class TableMapping
    {
        public EntitySetMapping Mapping { get; set; }
        public EntitySet EntitySet { get; set; }
        public EntityType EntityType { get; set; }
        public List<ScalarPropertyMapping> Columns { get; set; }
        public string Schema { get; }
        public string TableName { get; }
        public TableMapping(List<ScalarPropertyMapping> columns, EntitySet entitySet, EntityType entityType, EntitySetMapping mapping)
        {
            Columns = columns;
            EntitySet = entitySet;
            EntityType = entityType;
            Mapping = mapping;
            Schema = string.IsNullOrEmpty(EntitySet.Schema) ? "dbo" : EntitySet.Schema;
            TableName = entitySet.Name;
        }

    }
}

