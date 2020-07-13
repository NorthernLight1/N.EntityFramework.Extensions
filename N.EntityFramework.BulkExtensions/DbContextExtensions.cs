using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Core.Mapping;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace N.EntityFramework.BulkExtensions
{
    internal static partial class DbContextExtensions
    {
        public static int BulkInsert<T>(this DbContext context, IEnumerable<T> entities, BulkInsertOptions<T> options)
        {
            int rowsAffected = 0;
            var tableMapping = context.GetTableMapping(typeof(T));
            var dbConnection = context.GetSqlConnection();

            dbConnection.Open();

            using (var transaction = dbConnection.BeginTransaction())
            {
                try
                {
                    BulkInsert(entities, tableMapping, dbConnection, transaction);
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw ex;
                }
                return rowsAffected;
            }
        }

        private static void BulkInsert<T>(IEnumerable<T> entities, TableMapping tableMapping, SqlConnection dbConnection, SqlTransaction transaction, string tableName=null)
        {
            string destinationTableName = string.IsNullOrEmpty(tableName) ? string.Format("[{0}].[{1}]", tableMapping.Schema, tableMapping.TableName) : tableName;
            string[] columnNames = tableMapping.Columns.Where(o => !o.Column.IsStoreGeneratedIdentity).Select(o => o.Column.Name).ToArray();

            var dataReader = new EntityDataReader<T>(tableMapping, entities);
            var sqlBulkCopy = new SqlBulkCopy(dbConnection, new SqlBulkCopyOptions(), transaction)
            {
                DestinationTableName = destinationTableName
            };
            foreach (var column in dataReader.TableMapping.Columns)
            {
                sqlBulkCopy.ColumnMappings.Add(column.Property.Name, column.Column.Name);
            }
            sqlBulkCopy.WriteToServer(dataReader);
        }

        public static int BulkMerge<T>(this DbContext context, IEnumerable<T> entities, BulkMergeOptions<T> options)
        {
            int rowsAffected = 0;
            var tableMapping = context.GetTableMapping(typeof(T));
            var dbConnection = context.GetSqlConnection();

            dbConnection.Open();

            using (var transaction = dbConnection.BeginTransaction())
            {
                try
                {
                    string stagingTableName = string.Format("[{0}].[tmp_be_xx_{1}]", tableMapping.Schema, tableMapping.TableName);
                    string destinationTableName = string.Format("[{0}].[{1}]", tableMapping.Schema, tableMapping.TableName);
                    string[] columnNames = tableMapping.Columns.Where(o => !o.Column.IsStoreGeneratedIdentity).Select(o => o.Column.Name).ToArray();

                    SqlUtil.CloneTable(destinationTableName, stagingTableName, dbConnection, transaction);
                    BulkInsert(entities, tableMapping, dbConnection, transaction, stagingTableName);

                    string columnsToInsert = string.Join(",", columnNames.Where(o => !options.GetIgnoreColumnsOnInsert().Contains(o)));
                    string columnstoUpdate = string.Join(",", columnNames.Where(o => !options.GetIgnoreColumnsOnUpdate().Contains(o)).Select(o => string.Format("t.{0}=s.{0}", o)));

                    string mergeSql = string.Format("MERGE {0} t USING {1} s ON ({2}) WHEN NOT MATCHED BY TARGET THEN INSERT ({3}) VALUES ({3}) WHEN MATCHED THEN UPDATE SET {4};",
                        destinationTableName, stagingTableName, options.MergeOnCondition.ToSqlPredicate("s", "t"),
                        columnsToInsert,
                        columnstoUpdate
                        );

                    rowsAffected = SqlUtil.ExecuteSql(mergeSql, dbConnection, transaction);
                    SqlUtil.DeleteTable(stagingTableName, dbConnection, transaction);
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw ex;
                }
                return rowsAffected;
            }
        }

        private static SqlConnection GetSqlConnection(this DbContext context)
        {
            return context.Database.Connection as SqlConnection;
        }

        private static string ToSqlPredicate<T>(this Expression<T> expression, params string[] parameters)
        {
            var stringBuilder = new StringBuilder((string)expression.Body.GetPrivateFieldValue("DebugView"));
            int i = 0;
            foreach (var expressionParam in expression.Parameters)
            {
                if (parameters.Length <= i) break;
                stringBuilder.Replace((string)expressionParam.GetPrivateFieldValue("DebugView"), parameters[i]);
                i++;
            }
            stringBuilder.Replace("&&", "AND");
            stringBuilder.Replace("==", "=");
            return stringBuilder.ToString();
        }

        public static TableMapping GetTableMapping(this IObjectContextAdapter context, Type type)
        {
            var metadata = context.ObjectContext.MetadataWorkspace;

            // Get the part of the model that contains info about the actual CLR types
            var objectItemCollection = ((ObjectItemCollection)metadata.GetItemCollection(DataSpace.OSpace));

            // Get the entity type from the model that maps to the CLR type
            var entityType = metadata
                                .GetItems<EntityType>(DataSpace.OSpace)
                                      .Single(e => objectItemCollection.GetClrType(e) == type);

            // Get the entity set that uses this entity type
            var entitySet = metadata
                            .GetItems<EntityContainer>(DataSpace.CSpace)
                                  .Single()
                                  .EntitySets
                                  .Single(s => s.ElementType.Name == entityType.Name);

            // Find the mapping between conceptual and storage model for this entity set
            var mapping = metadata.GetItems<EntityContainerMapping>(DataSpace.CSSpace)
                                     .Single()
                                     .EntitySetMappings
                                     .Single(s => s.EntitySet == entitySet);

            // Find all properties (column) that are mapped
            var columns = mapping
                           .EntityTypeMappings.Single()
                           .Fragments.Single()
                           .PropertyMappings
                           .OfType<ScalarPropertyMapping>()
                           .ToList();

            return new TableMapping(columns, entitySet, entityType, mapping);
        }
    }
}

