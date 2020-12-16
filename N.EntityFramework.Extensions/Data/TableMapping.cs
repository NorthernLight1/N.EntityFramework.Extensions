using System.Collections.Generic;
using System.Data.Entity.Core.Mapping;
using System.Data.Entity.Core.Metadata.Edm;
using System.Linq;

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
        public string FullQualifedTableName
        {
            get { return string.Format("[{0}].[{1}]", this.Schema, this.TableName);  }
        }

        public TableMapping(List<ScalarPropertyMapping> columns, EntitySet entitySet, EntityType entityType, EntitySetMapping mapping)
        {
            var storeEntitySet = mapping.EntityTypeMappings.Single().Fragments.Single().StoreEntitySet;
            Columns = columns;
            EntitySet = entitySet;
            EntityType = entityType;
            Mapping = mapping;
            Schema = (string)storeEntitySet.MetadataProperties["Schema"].Value ?? storeEntitySet.Schema; 
            TableName = (string)storeEntitySet.MetadataProperties["Table"].Value ?? storeEntitySet.Name;
        }
    }
}

