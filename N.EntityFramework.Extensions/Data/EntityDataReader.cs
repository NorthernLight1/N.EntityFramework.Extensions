using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;

namespace N.EntityFramework.Extensions
{
    internal class EntityDataReader<T> : IDataReader
    {
        public TableMapping TableMapping { get; set; }
        private Dictionary<string, int> columnIndexes;
        private IEnumerable<T> entities;
        private IEnumerator<T> enumerator;
        private Dictionary<int, Func<T, object>> selectors;

        public EntityDataReader(TableMapping tableMapping, IEnumerable<T> entities)
        {
            this.columnIndexes = new Dictionary<string, int>();
            this.entities = entities;
            this.enumerator = entities.GetEnumerator();
            this.selectors = new Dictionary<int, Func<T, object>>();
            this.FieldCount = tableMapping.Columns.Count;
            this.TableMapping = tableMapping;

            int i = 0;
            foreach (var column in tableMapping.Columns)
            {
                var type = Expression.Parameter(typeof(T), "type");
                var propertyExpression = Expression.PropertyOrField(type, column.Property.Name);
                var expression = Expression.Lambda<Func<T, object>>(Expression.Convert(propertyExpression, typeof(object)), type);
                selectors[i] = expression.Compile();
                columnIndexes[column.Property.Name] = i;
                i++;
            }
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
            return selectors[i](enumerator.Current);
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
            return enumerator.MoveNext();
        }
    }
}

