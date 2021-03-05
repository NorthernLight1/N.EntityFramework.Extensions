using N.EntityFramework.Extensions.Common;
using N.EntityFramework.Extensions.Sql;
using N.EntityFramework.Extensions.Util;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Core.Mapping;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Infrastructure.Interception;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace N.EntityFramework.Extensions
{
    public static partial class DbContextExtensions
    {
        private static EfExtensionsCommandInterceptor efExtensionsCommandInterceptor;
        static DbContextExtensions()
        {
            efExtensionsCommandInterceptor = new EfExtensionsCommandInterceptor();
            DbInterception.Add(efExtensionsCommandInterceptor);
        }
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
                    string[] keyColumnNames = options.DeleteOnCondition != null ? CommonUtil<T>.GetColumns(options.DeleteOnCondition, new[] { "s" }) 
                        : tableMapping.Columns.Where(o => o.Column.IsStoreGeneratedIdentity).Select(o => o.Column.Name).ToArray();

                    SqlUtil.CloneTable(destinationTableName, stagingTableName, keyColumnNames, dbConnection, transaction);
                    BulkInsert(entities, options, tableMapping, dbConnection, transaction, stagingTableName, keyColumnNames, SqlBulkCopyOptions.KeepIdentity);
                    string deleteSql = string.Format("DELETE t FROM {0} s JOIN {1} t ON {2}", stagingTableName, destinationTableName, 
                        CommonUtil<T>.GetJoinConditionSql(options.DeleteOnCondition, keyColumnNames));
                    rowsAffected = SqlUtil.ExecuteSql(deleteSql, dbConnection, transaction, options);
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
            if (tableMapping.Columns.Where(o => o.Column.IsStoreGeneratedIdentity).Count() == 0)
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
                    string stagingTableName = GetStagingTableName(tableMapping, options.UsePermanentTable, dbConnection);
                    string destinationTableName = string.Format("[{0}].[{1}]", tableMapping.Schema, tableMapping.TableName);
                    string[] columnNames = tableMapping.Columns.Where(o => options.KeepIdentity || !o.Column.IsStoreGeneratedIdentity).Select(o => o.Column.Name).ToArray();
                    string[] storeGeneratedColumnNames = tableMapping.Columns.Where(o => o.Column.IsStoreGeneratedIdentity).Select(o => o.Column.Name).ToArray();

                    SqlUtil.CloneTable(destinationTableName, stagingTableName, null, dbConnection, transaction, Common.Constants.Guid_ColumnName);
                    var bulkInsertResult = BulkInsert(entities, options, tableMapping, dbConnection, transaction, stagingTableName, null, SqlBulkCopyOptions.KeepIdentity, true);

                    IEnumerable<string> columnsToInsert = columnNames;

                    List<string> columnsToOutput = new List<string>();
                    List<PropertyInfo> propertySetters = new List<PropertyInfo>();
                    Type entityType = typeof(T);

                    foreach (var storeGeneratedColumnName in storeGeneratedColumnNames)
                    {
                        columnsToOutput.Add(string.Format("inserted.[{0}]", storeGeneratedColumnName));
                        propertySetters.Add(entityType.GetProperty(storeGeneratedColumnName));
                    }

                    string whereSqlText = options.InsertIfNotExists ? string.Format(" WHERE NOT EXISTS(SELECT * FROM {0} WHERE {1})", destinationTableName,
                        CommonUtil<T>.GetJoinConditionSql(options.InsertOnCondition, storeGeneratedColumnNames, destinationTableName, stagingTableName)) : "";

                    string insertSqlText = columnsToOutput.Any()
                        ? string.Format("INSERT INTO {0} ({1}) OUTPUT {2} SELECT {3} FROM {4}{5};",
                            destinationTableName, SqlUtil.ConvertToColumnString(columnsToInsert),
                            SqlUtil.ConvertToColumnString(columnsToOutput),
                            SqlUtil.ConvertToColumnString(columnsToInsert), stagingTableName, whereSqlText)
                        : string.Format("INSERT INTO {0} ({1}) SELECT {2} FROM {3}{4};",
                            destinationTableName, SqlUtil.ConvertToColumnString(columnsToInsert),
                            SqlUtil.ConvertToColumnString(columnsToInsert), stagingTableName, whereSqlText);

                    if(options.KeepIdentity)
                        SqlUtil.ToggleIdentityInsert(true, destinationTableName, dbConnection, transaction);
                    var bulkQueryResult = context.BulkQuery(insertSqlText, dbConnection, transaction, options);
                    if (options.KeepIdentity)
                        SqlUtil.ToggleIdentityInsert(false, destinationTableName, dbConnection, transaction);
                    rowsAffected = bulkQueryResult.RowsAffected;

                    if (options.AutoMapOutputIdentity)
                    {
                        if (rowsAffected == entities.Count())
                        {
                            var entityIndex = 1;
                            foreach(var result in bulkQueryResult.Results)
                            {
                                var entity = bulkInsertResult.EntityMap[entityIndex];
                                propertySetters[0].SetValue(entity,result[0]);
                                entityIndex++;
                            }
                        }
                    }

                    SqlUtil.DeleteTable(stagingTableName, dbConnection, transaction);

                    //ClearEntityStateToUnchanged(context, entities);
                    transaction.Commit();
                    return rowsAffected;
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

            }
        }

        private static BulkInsertResult<T> BulkInsert<T>(IEnumerable<T> entities, BulkOptions options, TableMapping tableMapping, SqlConnection dbConnection, SqlTransaction transaction, string tableName,
            string[] inputColumns = null, SqlBulkCopyOptions bulkCopyOptions = SqlBulkCopyOptions.Default, bool useInteralId=false)
        {
            var dataReader = new EntityDataReader<T>(tableMapping, entities, useInteralId);

            var sqlBulkCopy = new SqlBulkCopy(dbConnection, bulkCopyOptions, transaction)
            {
                DestinationTableName = tableName,
                BatchSize = options.BatchSize
            };
            if (options.CommandTimeout.HasValue)
            {
                sqlBulkCopy.BulkCopyTimeout = options.CommandTimeout.Value;
            }
            foreach (var column in dataReader.TableMapping.Columns)
            {
                if (inputColumns == null || (inputColumns != null && inputColumns.Contains(column.Column.Name)))
                    sqlBulkCopy.ColumnMappings.Add(column.Property.Name, column.Column.Name);
            }
            if (useInteralId)
            {
                sqlBulkCopy.ColumnMappings.Add(Constants.Guid_ColumnName, Constants.Guid_ColumnName);
            }
            sqlBulkCopy.WriteToServer(dataReader);

            return new BulkInsertResult<T> {
                RowsAffected = Convert.ToInt32(sqlBulkCopy.GetPrivateFieldValue("_rowsCopied")),
                EntityMap = dataReader.EntityMap
            };
        }

        public static BulkMergeResult<T> BulkMerge<T>(this DbContext context, IEnumerable<T> entities)
        {
            return BulkMerge(context, entities, new BulkMergeOptions<T>());
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

                    SqlUtil.CloneTable(destinationTableName, stagingTableName, null, dbConnection, transaction, Common.Constants.Guid_ColumnName);
                    var bulkInsertResult = BulkInsert(entities, options, tableMapping, dbConnection, transaction, stagingTableName, null, SqlBulkCopyOptions.KeepIdentity, true);

                    IEnumerable<string> columnsToInsert = columnNames.Where(o => !options.GetIgnoreColumnsOnInsert().Contains(o));
                    IEnumerable<string> columnstoUpdate = columnNames.Where(o => !options.GetIgnoreColumnsOnUpdate().Contains(o)).Select(o => string.Format("t.{0}=s.{0}", o));
                    List<string> columnsToOutput = new List<string> { "$Action", string.Format("{0}.{1}","s", Constants.Guid_ColumnName) };
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
                        destinationTableName, stagingTableName, CommonUtil<T>.GetJoinConditionSql(options.MergeOnCondition,storeGeneratedColumnNames, "s", "t"),
                        SqlUtil.ConvertToColumnString(columnsToInsert),
                        SqlUtil.ConvertToColumnString(columnstoUpdate),
                        SqlUtil.ConvertToColumnString(columnsToOutput)
                        );

                    var bulkQueryResult = context.BulkQuery(mergeSqlText, dbConnection, transaction, options);
                    rowsAffected = bulkQueryResult.RowsAffected;

                    //var entitiesEnumerator = entities.GetEnumerator();
                    //entitiesEnumerator.MoveNext();
                    foreach (var result in bulkQueryResult.Results)
                    {
                        string action = (string)result[0];
                        int id = (int)result[1];
                        var entity = bulkInsertResult.EntityMap[id];
                        outputRows.Add(new BulkMergeOutputRow<T>(action, entity));
                        if (options.AutoMapOutputIdentity && entity != null)
                        {
                            
                            for (int i = 2; i < result.Length; i++)
                            {
                                propertySetters[0].SetValue(entity, result[i]);
                            }
                        }
                        if (action == SqlMergeAction.Insert) rowsInserted++;
                        else if (action == SqlMergeAction.Update) rowsUpdated++;
                        else if (action == SqlMergeAction.Detete) rowsDeleted++;
                    }
                    SqlUtil.DeleteTable(stagingTableName, dbConnection, transaction);

                    //ClearEntityStateToUnchanged(context, entities);
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
        public static int BulkUpdate<T>(this DbContext context, IEnumerable<T> entities)
        {
            return BulkUpdate<T>(context, entities, new BulkUpdateOptions<T>());
        }
        public static int BulkUpdate<T>(this DbContext context, IEnumerable<T> entities, BulkUpdateOptions<T> options)
        {
            int rowsUpdated = 0;
            var outputRows = new List<BulkMergeOutputRow<T>>();
            var tableMapping = context.GetTableMapping(typeof(T));
            var dbConnection = context.GetSqlConnection();

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

                    IEnumerable<string> columnstoUpdate = columnNames.Where(o => !options.IgnoreColumnsOnUpdate.GetObjectProperties().Contains(o));

                    string updateSetExpression = string.Join(",", columnstoUpdate.Select(o => string.Format("t.{0}=s.{0}", o)));
                    string updateSql = string.Format("UPDATE t SET {0} FROM {1} AS s JOIN {2} AS t ON {3}; SELECT @@RowCount;",
                        updateSetExpression, stagingTableName, destinationTableName, CommonUtil<T>.GetJoinConditionSql(options.UpdateOnCondition, storeGeneratedColumnNames, "s", "t"));

                    rowsUpdated = SqlUtil.ExecuteSql(updateSql, dbConnection, transaction, options);
                    SqlUtil.DeleteTable(stagingTableName, dbConnection, transaction);

                    //ClearEntityStateToUnchanged(context, entities);
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

                return rowsUpdated;
            }
        }
        public static void Fetch<T>(this IQueryable<T> querable, Action<FetchResult<T>> action, FetchOptions options) where T : class, new()
        {
            var dbQuery = querable as DbQuery<T>;
            var dbContext = GetDbContextFromIQuerable(querable);
            var dbConnection = dbContext.GetSqlConnection();
            //Open datbase connection
            if (dbConnection.State == ConnectionState.Closed)
                dbConnection.Open();

            var command = new SqlCommand(dbQuery.Sql, dbConnection);
            var reader = command.ExecuteReader();

            List<PropertyInfo> propertySetters = new List<PropertyInfo>();
            var entityType = typeof(T);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                propertySetters.Add(entityType.GetProperty(reader.GetName(i)));
            }
            //Read data
            int batch = 1;
            int count = 0;
            int totalCount = 0;
            var entities = new List<T>();
            while (reader.Read())
            {
                var entity = new T();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.GetValue(i);
                    if (value == DBNull.Value)
                        value = null;
                    propertySetters[i].SetValue(entity, value);
                }
                entities.Add(entity);
                count++;
                totalCount++;
                if (count == options.BatchSize)
                {
                    action(new FetchResult<T> { Results = entities, Batch = batch });
                    entities.Clear();
                    count = 0;
                    batch++;
                }
            }

            if (entities.Count > 0)
                action(new FetchResult<T> { Results = entities, Batch = batch });
            //close the DataReader
            reader.Close();
        }

        private static void ClearEntityStateToUnchanged<T>(DbContext dbContext, IEnumerable<T> entities)
        {
            foreach (var entity in entities)
            {
                var entry = dbContext.Entry(entity);
                if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                    dbContext.Entry(entity).State = EntityState.Unchanged;
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

        private static BulkQueryResult BulkQuery(this DbContext context, string sqlText, SqlConnection dbConnection, SqlTransaction transaction, BulkOptions options)
        {
            var results = new List<object[]>();
            var columns = new List<string>();
            var command = new SqlCommand(sqlText, dbConnection, transaction);
            if (options.CommandTimeout.HasValue)
            {
                command.CommandTimeout = options.CommandTimeout.Value;
            }
            var reader = command.ExecuteReader();
            //Get column names
            for (int i = 0; i < reader.FieldCount; i++)
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
        public static int DeleteFromQuery<T>(this IQueryable<T> querable)
        {
            return querable.DeleteFromQuery(new BulkDeleteOptions<T>());
        }
        public static int DeleteFromQuery<T>(this IQueryable<T> querable, BulkDeleteOptions<T> options)
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
                    rowAffected = SqlUtil.ExecuteSql(sqlQuery.Sql, dbConnection, dbTransaction, options);
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
        public static int InsertFromQuery<T>(this IQueryable<T> querable, string tableName, Expression<Func<T, object>> insertObjectExpression)
        {
            return querable.InsertFromQuery(tableName, insertObjectExpression, new BulkInsertOptions<T> { KeepIdentity = true });
        }
        public static int InsertFromQuery<T>(this IQueryable<T> querable, string tableName, Expression<Func<T, object>> insertObjectExpression, BulkInsertOptions<T> options)
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
                    if (SqlUtil.TableExists(tableName, dbConnection, dbTransaction))
                    {
                        sqlQuery.ChangeToInsert(tableName, insertObjectExpression);
                        if(options.KeepIdentity)
                            SqlUtil.ToggleIdentityInsert(true, tableName, dbConnection, dbTransaction);
                        rowAffected = SqlUtil.ExecuteSql(sqlQuery.Sql, dbConnection, dbTransaction, options);
                        if(options.KeepIdentity)
                            SqlUtil.ToggleIdentityInsert(false, tableName, dbConnection, dbTransaction);
                    }
                    else
                    {
                        sqlQuery.Clauses.First().InputText += string.Format(" INTO {0}", tableName);
                        rowAffected = SqlUtil.ExecuteSql(sqlQuery.Sql, dbConnection, dbTransaction, options);
                    }

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
        public static int UpdateFromQuery<T>(this IQueryable<T> querable, Expression<Func<T, T>> updateExpression)
        {
            return querable.UpdateFromQuery(updateExpression, new BulkUpdateOptions<T>());
        }
        public static int UpdateFromQuery<T>(this IQueryable<T> querable, Expression<Func<T, T>> updateExpression, BulkUpdateOptions<T> options)
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
                    string setSqlExpression = updateExpression.ToSqlUpdateSetExpression("Extent1");
                    sqlQuery.ChangeToUpdate("[Extent1]", setSqlExpression);
                    rowAffected = SqlUtil.ExecuteSql(sqlQuery.Sql, dbConnection, dbTransaction, options);
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
        public static IQueryable<T> UsingTable<T>(this IQueryable<T> querable, string tableName)
        {
            var dbContext = GetDbContextFromIQuerable(querable);
            var tableMapping = dbContext.GetTableMapping(typeof(T));
            efExtensionsCommandInterceptor.AddCommand(Guid.NewGuid(),
                new EfExtensionsCommand
                {
                    CommandType = EfExtensionsCommandType.ChangeTableName,
                    OldValue = tableMapping.FullQualifedTableName,
                    NewValue = string.Format("[{0}].[{1}]", tableMapping.Schema, tableName),
                    Connection = dbContext.GetSqlConnection()
                });
            return querable;
        }
        private static DbContext GetDbContextFromIQuerable<T>(IQueryable<T> querable)
        {
            DbContext dbContext;
            try
            {
                var dbQuery = querable as DbQuery<T>;
                var internalQuery = querable.GetPrivateFieldValue("InternalQuery");
                var internalContext = internalQuery.GetPrivateFieldValue("InternalContext");
                dbContext = internalContext.GetPrivateFieldValue("Owner") as DbContext;
            }
            catch
            {
                throw new Exception("This extension method requires a DbQuery<T> instance");
            }
            return dbContext;
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
            catch(Exception ex)
            {
                throw new Exception("This extension method requires a DbQuery<T> instance", ex);
            }
            return dbConnection;
        }

        private static SqlConnection GetSqlConnection(this DbContext context)
        {
            return context.Database.Connection as SqlConnection;
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

