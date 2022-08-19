using N.EntityFramework.Extensions.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity.Core.Mapping;
using System.Linq;
using System.Linq.Expressions;

namespace N.EntityFramework.Extensions
{
    internal class EntityDataReader<T> : IDataReader
    {
        public TableMapping TableMapping { get; set; }
        public Dictionary<long,T> EntityMap { get; set; }
        private Dictionary<string, int> columnIndexes;
        private long currentId;
        private bool useInternalId;
        private int tableFieldCount;
        private IEnumerable<T> entities;
        private IEnumerator<T> enumerator;
        private Dictionary<int, Func<T, object>> selectors;
        private Dictionary<int, ConditionPropertyMapping> conditions;

        public EntityDataReader(TableMapping tableMapping, IEnumerable<T> entities, IEnumerable<string> inputColumns, bool useInternalId)
        {
            this.columnIndexes = new Dictionary<string, int>();
            this.currentId = 0;
            this.useInternalId = useInternalId;
            this.tableFieldCount = tableMapping.Columns.Count;
            this.entities = entities;
            this.enumerator = entities.GetEnumerator();
            this.selectors = new Dictionary<int, Func<T, object>>();
            this.conditions = new Dictionary<int, ConditionPropertyMapping>();
            this.EntityMap = new Dictionary<long, T>();
            this.FieldCount = 0;
            this.TableMapping = tableMapping;
            

            int i = 0;
            foreach (var column in tableMapping.Columns)
            {
                if (inputColumns == null || (inputColumns != null && inputColumns.Contains(column.Column.Name)))
                {
                    var type = Expression.Parameter(typeof(T), "type");
                    var propertyExpression = Expression.PropertyOrField(type, column.Property.Name);
                    var expression = Expression.Lambda<Func<T, object>>(Expression.Convert(propertyExpression, typeof(object)), type);
                    selectors[i] = expression.Compile();
                    columnIndexes[column.Property.Name] = i;
                    i++;
                }
            }
            foreach(var condition in TableMapping.Conditions)
            {
                conditions[i] = condition;
                columnIndexes[condition.Column.Name] = i;
                i++;
            }
            if(useInternalId)
            {
                columnIndexes[Constants.InternalId_ColumnName] = i;
                i++;
            }

            this.FieldCount = i;
        }

        public object this[int i] => throw new NotImplementedException();

        public object this[string name] => throw new NotImplementedException();

        public int Depth { get; set; }

        public bool IsClosed => throw new NotImplementedException();

        public int RecordsAffected => throw new NotImplementedException();

        public int FieldCount { get; set; }

        public void Close()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            selectors = null;
            enumerator.Dispose();
        }

        public bool GetBoolean(int i)
        {
            throw new NotImplementedException();
        }

        public byte GetByte(int i)
        {
            throw new NotImplementedException();
        }

        public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length)
        {
            throw new NotImplementedException();
        }

        public char GetChar(int i)
        {
            throw new NotImplementedException();
        }

        public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length)
        {
            throw new NotImplementedException();
        }

        public IDataReader GetData(int i)
        {
            throw new NotImplementedException();
        }

        public string GetDataTypeName(int i)
        {
            throw new NotImplementedException();
        }

        public DateTime GetDateTime(int i)
        {
            throw new NotImplementedException();
        }

        public decimal GetDecimal(int i)
        {
            throw new NotImplementedException();
        }

        public double GetDouble(int i)
        {
            throw new NotImplementedException();
        }

        public Type GetFieldType(int i)
        {
            throw new NotImplementedException();
        }

        public float GetFloat(int i)
        {
            throw new NotImplementedException();
        }

        public Guid GetGuid(int i)
        {
            throw new NotImplementedException();
        }

        public short GetInt16(int i)
        {
            throw new NotImplementedException();
        }

        public int GetInt32(int i)
        {
            throw new NotImplementedException();
        }

        public long GetInt64(int i)
        {
            throw new NotImplementedException();
        }

        public string GetName(int i)
        {
            throw new NotImplementedException();
        }

        public int GetOrdinal(string name)
        {
            return columnIndexes[name];
        }

        public DataTable GetSchemaTable()
        {
            throw new NotImplementedException();
        }

        public string GetString(int i)
        {
            throw new NotImplementedException();
        }

        public object GetValue(int i)
        {
            if(useInternalId && i == this.FieldCount - 1)
            {
                return this.currentId;
            }
            else
            {
                return i < tableFieldCount ? selectors[i](enumerator.Current) : conditions[i].GetPrivateFieldValue("Value");
            }
            
        }

        public int GetValues(object[] values)
        {
            throw new NotImplementedException();
        }

        public bool IsDBNull(int i)
        {
            throw new NotImplementedException();
        }

        public bool NextResult()
        {
            throw new NotImplementedException();
        }

        public bool Read()
        {
            bool moveNext = enumerator.MoveNext();
            
            if (moveNext && this.useInternalId)
            {
                this.currentId++;
                this.EntityMap.Add(this.currentId, enumerator.Current);
            }
            return moveNext;
        }
    }
}

