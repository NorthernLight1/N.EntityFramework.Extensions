using N.EntityFramework.Extensions.Sql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Core.Mapping;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace N.EntityFramework.Extensions
{
    public static partial class DbContextExtensions
    {
        public static int BulkDelete<T>(this DbContext context, IEnumerable<T> entities)
        {
            return context.BulkDelete(entities, new BulkDeleteOptions<T>());
        }
        public static int BulkDelete<T>(this DbContext context, IEnumerable<T> entities, BulkDeleteOptions<T> options)
        {
            int rowsAffected = 0;
            var tableMapping = context.GetTableMapping(typeof(T));
            Validate(tableMapping);
            var dbConnection = context.GetSqlConnection();

            if (dbConnection.State == ConnectionState.Closed)
                dbConnection.Open();

            using (var transaction = dbConnection.BeginTransaction())
            {
                try
                {
                    string stagingTableName = GetStagingTableName(tableMapping, options.UsePermanentTable, dbConnection);
                    string destinationTableName = string.Format("[{0}].[{1}]", tableMapping.Schema, tableMapping.TableName);
                    string[] storeGeneratedColumnNames = tableMapping.Columns.Where(o => o.Column.IsStoreGeneratedIdentity).Select(o => o.Column.Name).ToArray();
                    string deleteCondition = string.Join(" AND ", storeGeneratedColumnNames.Select(o => string.Format("s.{0}=t.{0}", o)));

                    SqlUtil.CloneTable(destinationTableName, stagingTableName, storeGeneratedColumnNames, dbConnection, transaction);
                    BulkInsert(entities, options, tableMapping, dbConnection, transaction, stagingTableName, storeGeneratedColumnNames, SqlBulkCopyOptions.KeepIdentity);
                    string deleteSql = string.Format("DELETE t FROM {0} s JOIN {1} t ON {2}", stagingTableName, destinationTableName, deleteCondition);
                    rowsAffected = SqlUtil.ExecuteSql(deleteSql, dbConnection, transaction);
                    SqlUtil.DeleteTable(stagingTableName, dbConnection, transaction);
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw ex;
                }
                finally
                {
                    dbConnection.Close();
                }
                return rowsAffected;
            }
        }

        private static void Validate(TableMapping tableMapping)
        {
            if(tableMapping.Columns.Where(o => o.Column.IsStoreGeneratedIdentity).Count() == 0)
            {
                throw new Exception("You must have a primary key on this table to use this function.");
            }
        }

        public static int BulkInsert<T>(this DbContext context, IEnumerable<T> entities)
        {
            return context.BulkInsert<T>(entities, new BulkInsertOptions<T> { });
        }
        public static int BulkInsert<T>(this DbContext context, IEnumerable<T> entities, BulkInsertOptions<T> options)
        {
            int rowsAffected = 0;
            var tableMapping = context.GetTableMapping(typeof(T));
            var dbConnection = context.GetSqlConnection();

            if (dbConnection.State == ConnectionState.Closed)
                dbConnection.Open();

            using (var transaction = dbConnection.BeginTransaction())
            {
                try
                {
                    string tableName = string.IsNullOrEmpty(options.TableName) ? string.Format("[{0}].[{1}]", tableMapping.Schema, tableMapping.TableName) : options.TableName;
                    SqlBulkCopyOptions bulkCopyOptions = SqlBulkCopyOptions.Default;
                    if (options.KeepIdentity)
                        bulkCopyOptions = bulkCopyOptions | SqlBulkCopyOptions.KeepIdentity;
                    rowsAffected = BulkInsert(entities, options, tableMapping, dbConnection, transaction, tableName, options.GetInputColumns(), bulkCopyOptions);
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw ex;
                }
                finally
                {
                    dbConnection.Close();
                }
                return rowsAffected;
            }
        }

        private static int BulkInsert<T>(IEnumerable<T> entities, BulkOptions options, TableMapping tableMapping, SqlConnection dbConnection, SqlTransaction transaction, string tableName, 
            string[] inputColumns = null, SqlBulkCopyOptions bulkCopyOptions = SqlBulkCopyOptions.Default)
        {
            //string destinationTableName = string.IsNullOrEmpty(tableName) ? string.Format("[{0}].[{1}]", tableMapping.Schema, tableMapping.TableName) : tableName;
            var dataReader = new EntityDataReader<T>(tableMapping, entities);

            var sqlBulkCopy = new SqlBulkCopy(dbConnection, bulkCopyOptions, transaction)
            {
                DestinationTableName = tableName,
                BatchSize = options.BatchSize
            };
            foreach (var column in dataReader.TableMapping.Columns)
            {
                if(inputColumns == null || (inputColumns != null && inputColumns.Contains(column.Column.Name)))
                    sqlBulkCopy.ColumnMappings.Add(column.Property.Name, column.Column.Name);
            }
            sqlBulkCopy.WriteToServer(dataReader);
            return Convert.ToInt32(sqlBulkCopy.GetPrivateFieldValue("_rowsCopied"));
        }

        public static BulkMergeResult<T> BulkMerge<T>(this DbContext context, IEnumerable<T> entities, BulkMergeOptions<T> options)
        {
            int rowsAffected = 0;
            var outputRows = new List<BulkMergeOutputRow<T>>();
            var tableMapping = context.GetTableMapping(typeof(T));
            var dbConnection = context.GetSqlConnection();
            int rowsInserted = 0;
            int rowsUpdated = 0;
            int rowsDeleted = 0;

            if (dbConnection.State == ConnectionState.Closed)
                dbConnection.Open();

            using (var transaction = dbConnection.BeginTransaction())
            {
                try
                {
                    string stagingTableName = GetStagingTableName(tableMapping, options.UsePermanentTable, dbConnection);
                    string destinationTableName = string.Format("[{0}].[{1}]", tableMapping.Schema, tableMapping.TableName);
                    string[] columnNames = tableMapping.Columns.Where(o => !o.Column.IsStoreGeneratedIdentity).Select(o => o.Column.Name).ToArray();
                    string[] storeGeneratedColumnNames = tableMapping.Columns.Where(o => o.Column.IsStoreGeneratedIdentity).Select(o => o.Column.Name).ToArray();

                    SqlUtil.CloneTable(destinationTableName, stagingTableName, null, dbConnection, transaction);
                    BulkInsert(entities, options, tableMapping, dbConnection, transaction, stagingTableName, null, SqlBulkCopyOptions.KeepIdentity);

                    IEnumerable<string> columnsToInsert = columnNames.Where(o => !options.GetIgnoreColumnsOnInsert().Contains(o));
                    IEnumerable<string> columnstoUpdate = columnNames.Where(o => !options.GetIgnoreColumnsOnUpdate().Contains(o)).Select(o => string.Format("t.{0}=s.{0}", o));
                    List<string> columnsToOutput = new List<string> { "$Action" };
                    List<PropertyInfo> propertySetters = new List<PropertyInfo>();
                    Type entityType = typeof(T);

                    foreach (var storeGeneratedColumnName in storeGeneratedColumnNames)
                    {
                        //columnsToOutput.Add(string.Format("deleted.[{0}]", storeGeneratedColumnName)); Not Yet Supported
                        columnsToOutput.Add(string.Format("inserted.[{0}]", storeGeneratedColumnName));
                        var storedGeneratedColumn = tableMapping.Columns.First(o => o.Column.Name == storeGeneratedColumnName);
                        propertySetters.Add(entityType.GetProperty(storeGeneratedColumnName));
                    }

                    string mergeSqlText = string.Format("MERGE {0} t USING {1} s ON ({2}) WHEN NOT MATCHED BY TARGET THEN INSERT ({3}) VALUES ({3}) WHEN MATCHED THEN UPDATE SET {4} OUTPUT {5};",
                        destinationTableName, stagingTableName, options.MergeOnCondition.ToSqlPredicate("s", "t"),
                        SqlUtil.ConvertToColumnString(columnsToInsert),
                        SqlUtil.ConvertToColumnString(columnstoUpdate),
                        SqlUtil.ConvertToColumnString(columnsToOutput)
                        );

                    var bulkQueryResult = context.BulkQuery(mergeSqlText, dbConnection, transaction);
                    rowsAffected = bulkQueryResult.RowsAffected;

                    var entitiesEnumerator = entities.GetEnumerator();
                    entitiesEnumerator.MoveNext();
                    foreach (var result in bulkQueryResult.Results)
                    {
                        var entity = entitiesEnumerator.Current;
                        string action = (string)result[0];
                        outputRows.Add(new BulkMergeOutputRow<T>(action, entity));
                        if (options.AutoMapOutputIdentity)
                        {
                            for (int i = 1; i < result.Length; i++)
                            {
                                propertySetters[0].SetValue(entity, result[i]);
                            }
                        }
                        if (action == SqlMergeAction.Insert) rowsInserted++;
                        if (action == SqlMergeAction.Update) rowsUpdated++;
                        if (action == SqlMergeAction.Detete) rowsDeleted++;
                        entitiesEnumerator.MoveNext();
                    }
                    SqlUtil.DeleteTable(stagingTableName, dbConnection, transaction);
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw ex;
                }
                finally
                {
                    dbConnection.Close();
                }

                return new BulkMergeResult<T>
                {
                    Output = outputRows,
                    RowsAffected = rowsAffected,
                    RowsDeleted = rowsDeleted,
                    RowsInserted = rowsInserted,
                    RowsUpdated = rowsUpdated,
                };
            }
        }

        private static string GetStagingTableName(TableMapping tableMapping, bool usePermanentTable, SqlConnection sqlConnection)
        {
            string tableName = string.Empty;
            if (usePermanentTable)
                tableName = string.Format("[{0}].[tmp_be_xx_{1}_{2}]", tableMapping.Schema, tableMapping.TableName, sqlConnection.ClientConnectionId.ToString());
            else
                tableName = string.Format("[{0}].[#tmp_be_xx_{1}]", tableMapping.Schema, tableMapping.TableName);
            return tableName;
        }

        private static BulkQueryResult BulkQuery(this DbContext context, string sqlText, SqlConnection dbConnection, SqlTransaction transaction)
        {
            var results = new List<object[]>();
            var columns = new List<string>();
            var command = new SqlCommand(sqlText, dbConnection, transaction);
            var reader = command.ExecuteReader();
            //Get column names
            for(int i=0; i<reader.FieldCount; i++)
            {
                columns.Add(reader.GetName(i));
            }
            //Read data
            while (reader.Read())
            {
                Object[] values = new Object[reader.FieldCount];
                reader.GetValues(values);
                results.Add(values);
            }
            return new BulkQueryResult
            {
                Columns = columns,
                Results = results,
                RowsAffected = reader.RecordsAffected
            };
        }
        public static int Delete<T>(this IQueryable<T> querable)
        {
            int rowAffected = 0;
            var dbQuery = querable as DbQuery<T>;
            var dbConnection = GetSqlConnectionFromIQuerable(querable);
            //Open datbase connection
            if (dbConnection.State == ConnectionState.Closed)
                dbConnection.Open();

            using (var dbTransaction = dbConnection.BeginTransaction())
            {
                try
                {
                    var sqlQuery = SqlQuery.Parse(dbQuery.Sql);
                    sqlQuery.ChangeToDelete("[Extent1]");
                    rowAffected = SqlUtil.ExecuteSql(sqlQuery.Sql, dbConnection, dbTransaction);
                    dbTransaction.Commit();
                }
                catch (Exception ex)
                {
                    dbTransaction.Rollback();
                    throw ex;
                }
                finally
                {
                    dbConnection.Close();
                }
            }
            return rowAffected;
        }

        private static SqlConnection GetSqlConnectionFromIQuerable<T>(IQueryable<T> querable)
        {
            SqlConnection dbConnection;
            try
            {
                var dbQuery = querable as DbQuery<T>;
                var internalQuery = querable.GetPrivateFieldValue("InternalQuery");
                var context = internalQuery.GetPrivateFieldValue("InternalContext");
                dbConnection = context.GetPrivateFieldValue("Connection") as SqlConnection;
            }
            catch
            {
                throw new Exception("This extension method requires a DbQuery<T> instance");
            }
            return dbConnection;
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

