using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Core.Common;
using System.Data.Entity.Core.Mapping;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Infrastructure.Interception;
using System.Data.Entity.Migrations.Model;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using N.EntityFramework.Extensions.Common;
using N.EntityFramework.Extensions.Extensions;
using N.EntityFramework.Extensions.Sql;
using N.EntityFramework.Extensions.Util;

namespace N.EntityFramework.Extensions
{
    public static partial class DbContextExtensions
    {
        private static readonly EfExtensionsCommandInterceptor efExtensionsCommandInterceptor;
        static DbContextExtensions()
        {
            efExtensionsCommandInterceptor = new EfExtensionsCommandInterceptor();
            DbInterception.Add(efExtensionsCommandInterceptor);
        }
        public static int BulkDelete<T>(this DbContext context, IEnumerable<T> entities) where T : class
        {
            return context.BulkDelete(entities, new BulkDeleteOptions<T>());
        }
        public static int BulkDelete<T>(this DbContext context, IEnumerable<T> entities, Action<BulkDeleteOptions<T>> optionsAction) where T : class
        {
            return context.BulkDelete(entities, optionsAction.Build());
        }
        public static int BulkDelete<T>(this DbContext context, IEnumerable<T> entities, BulkDeleteOptions<T> options) where T : class
        {
            int rowsAffected = 0;
            var tableMapping = context.GetTableMapping<T>(options.ClrType);

            using (var dbTransactionContext = new DbTransactionContext(context, options))
            {
                var dbConnection = dbTransactionContext.Connection;
                var transaction = dbTransactionContext.CurrentTransaction;
                try
                {
                    string stagingTableName = CommonUtil.GetStagingTableName(tableMapping, options.UsePermanentTable, dbConnection);
                    string destinationTableName = string.Format("[{0}].[{1}]", tableMapping.Schema, tableMapping.TableName);
                    string[] keyColumnNames = options.DeleteOnCondition != null ? CommonUtil<T>.GetColumns(options.DeleteOnCondition, new[] { "s" })
                        : tableMapping.GetPrimaryKeyColumns().ToArray();

                    if (keyColumnNames.Length == 0 && options.DeleteOnCondition == null)
                        throw new InvalidDataException("BulkDelete requires that the entity have a primary key or the Options.DeleteOnCondition must be set.");


                    context.Database.CloneTable(destinationTableName, stagingTableName, keyColumnNames);
                    BulkInsert(entities, options, tableMapping, dbConnection, transaction, stagingTableName, keyColumnNames, SqlBulkCopyOptions.KeepIdentity, false, false);
                    string deleteSql = string.Format("DELETE t FROM {0} s JOIN {1} t ON {2}", stagingTableName, destinationTableName,
                        CommonUtil<T>.GetJoinConditionSql(options.DeleteOnCondition, keyColumnNames));
                    rowsAffected = SqlUtil.ExecuteSql(deleteSql, dbConnection, transaction, options.CommandTimeout);
                    context.Database.DropTable(stagingTableName);
                    dbTransactionContext.Commit();
                }
                catch (Exception)
                {
                    dbTransactionContext.Rollback();
                    throw;
                }
                return rowsAffected;
            }
        }
        public static IEnumerable<T> BulkFetch<T, U>(this DbSet<T> dbSet, IEnumerable<U> entities) where T : class, new()
        {
            return dbSet.BulkFetch(entities, new BulkFetchOptions<T>());
        }
        public static IEnumerable<T> BulkFetch<T, U>(this DbSet<T> dbSet, IEnumerable<U> entities, Action<BulkFetchOptions<T>> optionsAction) where T : class, new()
        {
            return dbSet.BulkFetch(entities, optionsAction.Build());
        }
        public static IEnumerable<T> BulkFetch<T, U>(this DbSet<T> dbSet, IEnumerable<U> entities, BulkFetchOptions<T> options) where T : class, new()
        {
            var context = dbSet.GetDbContext();
            var tableMapping = context.GetTableMapping<T>();

            using (var dbTransactionContext = new DbTransactionContext(context, Enums.ConnectionBehavior.New, TransactionalBehavior.DoNotEnsureTransaction))
            {
                string selectSql, stagingTableName = string.Empty;
                var dbConnection = dbTransactionContext.Connection;
                var transaction = dbTransactionContext.CurrentTransaction;
                try
                {
                    stagingTableName = CommonUtil.GetStagingTableName(tableMapping, true, dbConnection);
                    string destinationTableName = string.Format("[{0}].[{1}]", tableMapping.Schema, tableMapping.TableName);
                    string[] keyColumnNames = options.JoinOnCondition != null ? CommonUtil<T>.GetColumns(options.JoinOnCondition, new[] { "s" })
                        : tableMapping.GetPrimaryKeyColumns().ToArray();
                    IEnumerable<string> columnNames = CommonUtil.FilterColumns<T>(tableMapping.GetColumns(true, true), keyColumnNames, options.InputColumns, options.IgnoreColumns);
                    IEnumerable<string> columnsToFetch = CommonUtil.FormatColumns("t", columnNames);

                    if (keyColumnNames.Length == 0 && options.JoinOnCondition == null)
                        throw new InvalidDataException("BulkFetch requires that the entity have a primary key or the Options.JoinOnCondition must be set.");

                    context.Database.CloneTable(destinationTableName, stagingTableName, keyColumnNames);
                    BulkInsert(entities, options, tableMapping, dbConnection, transaction, stagingTableName, keyColumnNames, SqlBulkCopyOptions.KeepIdentity, false, false);
                    selectSql = string.Format("SELECT {0} FROM {1} s JOIN {2} t ON {3}", SqlUtil.ConvertToColumnString(columnsToFetch), stagingTableName, destinationTableName,
                        CommonUtil<T>.GetJoinConditionSql(options.JoinOnCondition, keyColumnNames));


                    dbTransactionContext.Commit();
                }
                catch (Exception)
                {
                    dbTransactionContext.Rollback();
                    throw;
                }

                foreach (var item in context.FetchInternal<T>(selectSql))
                {
                    yield return item;
                }
                context.Database.DropTable(stagingTableName);
            }
        }
        public static int BulkInsert<T>(this DbContext context, IEnumerable<T> entities) where T : class
        {
            return context.BulkInsert<T>(entities, new BulkInsertOptions<T>());
        }
        public static int BulkInsert<T>(this DbContext context, IEnumerable<T> entities, Action<BulkInsertOptions<T>> optionsAction) where T : class
        {
            return context.BulkInsert<T>(entities, optionsAction.Build());
        }
        public static int BulkInsert<T>(this DbContext context, IEnumerable<T> entities, BulkInsertOptions<T> options) where T : class
        {
            int rowsAffected = 0;
            var tableMapping = context.GetTableMapping<T>(options.ClrType);
            var dbConnection = context.GetSqlConnection();

            using (var dbTransactionContext = new DbTransactionContext(context, options))
            {
                try
                {
                    var transaction = dbTransactionContext.CurrentTransaction;
                    string stagingTableName = CommonUtil.GetStagingTableName(tableMapping, options.UsePermanentTable, dbConnection);
                    string destinationTableName = string.Format("[{0}].[{1}]", tableMapping.Schema, tableMapping.TableName);

                    string[] primaryKeyColumnNames = tableMapping.GetPrimaryKeyColumns().ToArray();
                    IEnumerable<string> columnNames = CommonUtil.FilterColumns(tableMapping.GetColumns(options.KeepIdentity), primaryKeyColumnNames, options.InputColumns, options.IgnoreColumns);
                    IEnumerable<string> autoGeneratedColumnNames = options.AutoMapOutput ? tableMapping.GetAutoGeneratedColumns() : new string[] { };
                    IEnumerable<string> columnsToInsert = CommonUtil.FormatColumns(columnNames);
                    if (options.InsertIfNotExists)
                    {
                        columnNames = columnNames.Union(primaryKeyColumnNames);
                    }

                    context.Database.CloneTable(destinationTableName, stagingTableName, columnNames, Common.Constants.InternalId_ColumnName);
                    var bulkInsertResult = BulkInsert(entities, options, tableMapping, dbConnection, transaction, stagingTableName, columnNames, SqlBulkCopyOptions.KeepIdentity, true);

                    List<string> columnsToOutput = new List<string> { "$Action", string.Format("{0}.{1}", "s", Constants.InternalId_ColumnName) };
                    List<PropertyInfo> propertySetters = new List<PropertyInfo>();
                    Type clrType = tableMapping.ClrType;

                    foreach (var autoGeneratedColumnName in autoGeneratedColumnNames)
                    {
                        columnsToOutput.Add(string.Format("inserted.[{0}]", autoGeneratedColumnName));
                        propertySetters.Add(clrType.GetProperty(autoGeneratedColumnName));
                    }

                    bool outputColumns = options.AutoMapOutput && columnsToOutput.Count > 0;
                    string insertSqlText = string.Format("MERGE {0} t USING {1} s ON {2} WHEN NOT MATCHED THEN INSERT ({3}) VALUES ({3}){4};",
                        destinationTableName,
                        stagingTableName,
                        options.InsertIfNotExists ? CommonUtil<T>.GetJoinConditionSql(options.InsertOnCondition, primaryKeyColumnNames, "t", "s") : "1=2",
                        SqlUtil.ConvertToColumnString(columnsToInsert),
                        outputColumns ? " OUTPUT " + SqlUtil.ConvertToColumnString(columnsToOutput) : "");

                    if (options.KeepIdentity && tableMapping.HasIdentity)
                        SqlUtil.ToggleIdentityInsert(true, destinationTableName, dbConnection, transaction);
                    var bulkQueryResult = context.BulkQuery(insertSqlText, dbConnection, transaction, options);
                    if (options.KeepIdentity && tableMapping.HasIdentity)
                        SqlUtil.ToggleIdentityInsert(false, destinationTableName, dbConnection, transaction);
                    rowsAffected = bulkQueryResult.RowsAffected;

                    if (options.AutoMapOutput)
                    {
                        if (rowsAffected == entities.Count())
                        {
                            foreach (var result in bulkQueryResult.Results)
                            {
                                int entityId = (int)result[1];
                                var entity = bulkInsertResult.EntityMap[entityId];
                                for (int i = 2; i < columnsToOutput.Count; i++)
                                {
                                    propertySetters[i - 2].SetValue(entity, SqlUtil.GetDBValue(result[i]));
                                }
                            }
                        }
                    }

                    context.Database.DropTable(stagingTableName);

                    //ClearEntityStateToUnchanged(context, entities);
                    dbTransactionContext.Commit();
                }
                catch (Exception ex)
                {
                    dbTransactionContext.Rollback();
                    throw ex;
                }
                return rowsAffected;
            }
        }

        private static BulkInsertResult<T> BulkInsert<T>(IEnumerable<T> entities, BulkOptions options, TableMapping tableMapping, SqlConnection dbConnection, SqlTransaction transaction, string tableName,
            IEnumerable<string> inputColumns = null, SqlBulkCopyOptions bulkCopyOptions = SqlBulkCopyOptions.Default, bool useInteralId = false, bool includeConditionColumns = true)
        {
            using (var dataReader = new EntityDataReader<T>(tableMapping, entities, inputColumns, useInteralId))
            {
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
                if (includeConditionColumns)
                {
                    foreach (var condition in dataReader.TableMapping.Conditions)
                    {
                        sqlBulkCopy.ColumnMappings.Add(condition.Column.Name, condition.Column.Name);
                    }
                }
                if (useInteralId)
                {
                    sqlBulkCopy.ColumnMappings.Add(Constants.InternalId_ColumnName, Constants.InternalId_ColumnName);
                }
                sqlBulkCopy.WriteToServer(dataReader);

                return new BulkInsertResult<T>
                {
                    RowsAffected = Convert.ToInt32(sqlBulkCopy.GetPrivateFieldValue("_rowsCopied")),
                    EntityMap = dataReader.EntityMap
                };
            }
        }
        public static BulkMergeResult<T> BulkMerge<T>(this DbContext context, IEnumerable<T> entities) where T : class
        {
            return BulkMerge(context, entities, new BulkMergeOptions<T>());
        }
        public static BulkMergeResult<T> BulkMerge<T>(this DbContext context, IEnumerable<T> entities, BulkMergeOptions<T> options) where T : class
        {
            return InternalBulkMerge(context, entities, options);
        }
        public static BulkMergeResult<T> BulkMerge<T>(this DbContext context, IEnumerable<T> entities, Action<BulkMergeOptions<T>> optionsAction) where T : class
        {
            return BulkMerge(context, entities, optionsAction.Build());
        }
        public static int BulkSaveChanges(this DbContext dbContext)
        {
            return dbContext.BulkSaveChanges(true);
        }
        public static int BulkSaveChanges(this DbContext dbContext, bool acceptAllChangesOnSuccess = true)
        {
            int rowsAffected = 0;
            var entries = dbContext.GetEntriesToSave();

            foreach (var saveEntryGroup in entries.GroupBy(o => new { EntityType = o.Entity.GetType(), o.State }))
            {
                var key = saveEntryGroup.Key;
                var entities = saveEntryGroup.AsEnumerable().Select(o => o.Entity);
                if (key.State == EntityState.Added)
                {
                    rowsAffected += dbContext.BulkInsert(entities, o => { o.ClrType = key.EntityType; });
                }
                else if (key.State == EntityState.Modified)
                {
                    rowsAffected += dbContext.BulkUpdate(entities, o => { o.ClrType = key.EntityType; });
                }
                else if (key.State == EntityState.Deleted)
                {
                    rowsAffected += dbContext.BulkDelete(entities, o => { o.ClrType = key.EntityType; });
                }
            }

            if (acceptAllChangesOnSuccess)
            {
                foreach (var entry in entries)
                {
                    entry.State = EntityState.Unchanged;
                }
            }

            return rowsAffected;
        }
        public static BulkSyncResult<T> BulkSync<T>(this DbContext context, IEnumerable<T> entities) where T : class
        {
            return BulkSync(context, entities, new BulkSyncOptions<T>());
        }
        public static BulkSyncResult<T> BulkSync<T>(this DbContext context, IEnumerable<T> entities, Action<BulkSyncOptions<T>> optionsAction) where T : class
        {
            return BulkSyncResult<T>.Map(InternalBulkMerge(context, entities, optionsAction.Build()));
        }
        public static BulkSyncResult<T> BulkSync<T>(this DbContext context, IEnumerable<T> entities, BulkSyncOptions<T> options) where T : class
        {
            return BulkSyncResult<T>.Map(InternalBulkMerge(context, entities, options));
        }
        private static BulkMergeResult<T> InternalBulkMerge<T>(this DbContext context, IEnumerable<T> entities, BulkMergeOptions<T> options) where T : class
        {
            int rowsAffected = 0;
            var outputRows = new List<BulkMergeOutputRow<T>>();
            var tableMapping = context.GetTableMapping<T>();
            var dbConnection = context.GetSqlConnection();
            int rowsInserted = 0;
            int rowsUpdated = 0;
            int rowsDeleted = 0;

            using (var dbTransactionContext = new DbTransactionContext(context, options))
            {
                try
                {
                    var transaction = dbTransactionContext.CurrentTransaction;
                    string stagingTableName = CommonUtil.GetStagingTableName(tableMapping, options.UsePermanentTable, dbConnection);
                    string destinationTableName = string.Format("[{0}].[{1}]", tableMapping.Schema, tableMapping.TableName);
                    IEnumerable<string> columnNames = tableMapping.GetColumns();
                    string[] primaryKeyColumnNames = tableMapping.GetPrimaryKeyColumns().ToArray();
                    IEnumerable<string> autoGeneratedColumnNames = options.AutoMapOutput ? tableMapping.GetAutoGeneratedColumns() : new string[] { };

                    if (primaryKeyColumnNames.Length == 0 && options.MergeOnCondition == null)
                        throw new InvalidDataException("BulkMerge requires that the entity have a primary key or the Options.MergeOnCondition must be set.");

                    context.Database.CloneTable(destinationTableName, stagingTableName, null, Common.Constants.InternalId_ColumnName);
                    var bulkInsertResult = BulkInsert(entities, options, tableMapping, dbConnection, transaction, stagingTableName, null, SqlBulkCopyOptions.KeepIdentity, true);

                    IEnumerable<string> columnsToInsert = CommonUtil.FormatColumns(columnNames.Where(o => !options.GetIgnoreColumnsOnInsert().Contains(o)));
                    IEnumerable<string> columnstoUpdate = CommonUtil.FormatColumns(columnNames.Where(o => !options.GetIgnoreColumnsOnUpdate().Contains(o))).Select(o => string.Format("t.{0}=s.{0}", o));
                    List<string> columnsToOutput = new List<string> { "$Action", string.Format("{0}.{1}", "s", Constants.InternalId_ColumnName) };
                    List<PropertyInfo> propertySetters = new List<PropertyInfo>();
                    Type entityType = typeof(T);

                    foreach (var autoGeneratedColumnName in autoGeneratedColumnNames)
                    {
                        columnsToOutput.Add(string.Format("inserted.[{0}]", autoGeneratedColumnName));
                        columnsToOutput.Add(string.Format("deleted.[{0}]", autoGeneratedColumnName));
                        propertySetters.Add(entityType.GetProperty(autoGeneratedColumnName));
                    }

                    string mergeSqlText = string.Format("MERGE {0} t USING {1} s ON ({2}) WHEN NOT MATCHED BY TARGET THEN INSERT ({3}) VALUES ({3}) WHEN MATCHED THEN UPDATE SET {4}{5}OUTPUT {6};",
                        destinationTableName, stagingTableName, CommonUtil<T>.GetJoinConditionSql(options.MergeOnCondition, primaryKeyColumnNames, "s", "t"),
                        SqlUtil.ConvertToColumnString(columnsToInsert),
                        SqlUtil.ConvertToColumnString(columnstoUpdate),
                        options.DeleteIfNotMatched ? " WHEN NOT MATCHED BY SOURCE THEN DELETE " : " ",
                        SqlUtil.ConvertToColumnString(columnsToOutput)
                        );

                    var bulkQueryResult = context.BulkQuery(mergeSqlText, dbConnection, transaction, options);
                    rowsAffected = bulkQueryResult.RowsAffected;

                    if (options.AutoMapOutput)
                    {
                        foreach (var result in bulkQueryResult.Results)
                        {
                            string id = string.Empty;
                            object entity = null;
                            string action = (string)result[0];
                            if (action != SqlMergeAction.Delete)
                            {
                                int entityId = (int)result[1];
                                entity = bulkInsertResult.EntityMap[entityId];
                                if (entity != null)
                                {
                                    for (int i = 2; i < autoGeneratedColumnNames.Count() + 2; i++)
                                    {
                                        propertySetters[i - 2].SetValue(entity, SqlUtil.GetDBValue(result[i]));
                                    }
                                }
                            }
                            outputRows.Add(new BulkMergeOutputRow<T>(action));

                            if (action == SqlMergeAction.Insert) rowsInserted++;
                            else if (action == SqlMergeAction.Update) rowsUpdated++;
                            else if (action == SqlMergeAction.Delete) rowsDeleted++;
                        }
                    }
                    context.Database.DropTable(stagingTableName);

                    //ClearEntityStateToUnchanged(context, entities);
                    dbTransactionContext.Commit();
                }
                catch (Exception)
                {
                    dbTransactionContext.Rollback();
                    throw;
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
        public static int BulkUpdate<T>(this DbContext context, IEnumerable<T> entities) where T : class
        {
            return BulkUpdate<T>(context, entities, new BulkUpdateOptions<T>());
        }
        public static int BulkUpdate<T>(this DbContext context, IEnumerable<T> entities, Action<BulkUpdateOptions<T>> optionsAction) where T : class
        {
            return BulkUpdate<T>(context, entities, optionsAction.Build());
        }
        public static int BulkUpdate<T>(this DbContext context, IEnumerable<T> entities, BulkUpdateOptions<T> options) where T : class
        {
            int rowsUpdated = 0;
            var outputRows = new List<BulkMergeOutputRow<T>>();
            var tableMapping = context.GetTableMapping<T>(options.ClrType);
            var dbConnection = context.GetSqlConnection();

            using (var dbTransactionContext = new DbTransactionContext(context, options))
            {
                try
                {
                    var transaction = dbTransactionContext.CurrentTransaction;
                    string stagingTableName = CommonUtil.GetStagingTableName(tableMapping, options.UsePermanentTable, dbConnection);
                    string destinationTableName = string.Format("[{0}].[{1}]", tableMapping.Schema, tableMapping.TableName);
                    string[] primaryKeyColumnNames = tableMapping.GetPrimaryKeyColumns().ToArray();
                    IEnumerable<string> columnNames = CommonUtil.FilterColumns(tableMapping.GetColumns(), primaryKeyColumnNames, options.InputColumns, options.IgnoreColumns);

                    if (primaryKeyColumnNames.Length == 0 && options.UpdateOnCondition == null)
                        throw new InvalidDataException("BulkUpdate requires that the entity have a primary key or the Options.UpdateOnCondition must be set.");

                    context.Database.CloneTable(destinationTableName, stagingTableName);
                    BulkInsert(entities, options, tableMapping, dbConnection, transaction, stagingTableName, null, SqlBulkCopyOptions.KeepIdentity);

                    IEnumerable<string> columnstoUpdate = CommonUtil.FormatColumns(columnNames.Where(o => !options.IgnoreColumns.GetObjectProperties().Contains(o)));

                    string updateSetExpression = string.Join(",", columnstoUpdate.Select(o => string.Format("t.{0}=s.{0}", o)));
                    string updateSql = string.Format("UPDATE t SET {0} FROM {1} AS s JOIN {2} AS t ON {3}; SELECT @@RowCount;",
                        updateSetExpression, stagingTableName, destinationTableName, CommonUtil<T>.GetJoinConditionSql(options.UpdateOnCondition, primaryKeyColumnNames, "s", "t"));

                    rowsUpdated = SqlUtil.ExecuteSql(updateSql, dbConnection, transaction, options.CommandTimeout);
                    context.Database.DropTable(stagingTableName);

                    //ClearEntityStateToUnchanged(context, entities);
                    dbTransactionContext.Commit();
                }
                catch (Exception)
                {
                    dbTransactionContext.Rollback();
                    throw;
                }

                return rowsUpdated;
            }
        }
        public static void Fetch<T>(this IQueryable<T> querable, Action<FetchResult<T>> action, Action<FetchOptions<T>> optionsAction) where T : class, new()
        {
            Fetch(querable, action, optionsAction.Build());
        }
        public static void Fetch<T>(this IQueryable<T> querable, Action<FetchResult<T>> action, FetchOptions<T> options) where T : class, new()
        {
            var dbContext = querable.GetDbContext();
            var sqlQuery = SqlBuilder.Parse(querable.GetSql(), querable.GetObjectQuery());
            if (options.InputColumns != null || options.IgnoreColumns != null)
            {
                var tableMapping = dbContext.GetTableMapping<T>();
                IEnumerable<string> columnNames = options.InputColumns != null ? options.InputColumns.GetObjectProperties() : tableMapping.GetColumns(true);
                IEnumerable<string> columnsToFetch = CommonUtil.FormatColumns(columnNames.Where(o => !options.IgnoreColumns.GetObjectProperties().Contains(o)));
                sqlQuery.SelectColumns(columnsToFetch);
            }
            using (var command = dbContext.Database.CreateCommand(Enums.ConnectionBehavior.New))
            {
                command.CommandText = sqlQuery.Sql;
                command.Parameters.AddRange(sqlQuery.Parameters);

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
        }
        private static IEnumerable<T> FetchInternal<T>(this DbContext dbContext, string sqlText, object[] parameters = null) where T : class, new()
        {
            using (var command = dbContext.Database.CreateCommand(Enums.ConnectionBehavior.New))
            {
                command.CommandText = sqlText;
                if (parameters != null)
                    command.Parameters.AddRange(parameters);

                var reader = command.ExecuteReader();

                List<PropertyInfo> propertySetters = new List<PropertyInfo>();
                var entityType = typeof(T);
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    propertySetters.Add(entityType.GetProperty(reader.GetName(i)));
                }
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
                    yield return entity;
                }
                //close the DataReader
                reader.Close();
            }
        }
        private static void ClearEntityStateToUnchanged<T>(DbContext dbContext, IEnumerable<T> entities)
        {
            bool autoDetectCahngesEnabled = dbContext.Configuration.AutoDetectChangesEnabled;
            dbContext.Configuration.AutoDetectChangesEnabled = false;
            foreach (var entity in entities)
            {
                var entry = dbContext.Entry(entity);
                if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                    dbContext.Entry(entity).State = EntityState.Unchanged;
            }
            dbContext.Configuration.AutoDetectChangesEnabled = autoDetectCahngesEnabled;
        }
        private static BulkQueryResult BulkQuery(this DbContext context, string sqlText, SqlConnection dbConnection, SqlTransaction transaction, BulkOptions options)
        {
            var results = new List<object[]>();
            var columns = new List<string>();
            var command = context.Database.Connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sqlText;
            if (options.CommandTimeout.HasValue)
            {
                command.CommandTimeout = options.CommandTimeout.Value;
            }
            using (var reader = command.ExecuteReader())
            {
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
        }
        public static int DeleteFromQuery<T>(this IQueryable<T> querable)
        {
            return querable.DeleteFromQuery(null);
        }
        public static int DeleteFromQuery<T>(this IQueryable<T> querable, int? commandTimeout)
        {
            int rowAffected = 0;

            using (var dbTransactionContext = new DbTransactionContext(querable.GetDbContext(), commandTimeout: commandTimeout))
            {
                var dbConnection = dbTransactionContext.Connection;
                var dbTransaction = dbTransactionContext.CurrentTransaction;
                try
                {
                    var sqlQuery = SqlBuilder.Parse(querable.GetSql(), querable.GetObjectQuery());
                    //Ignore empty list
                    if (!sqlQuery.Clauses.Any(o => o.Name == "FROM" && o.InputText.EndsWith("[SingleRowTable1]")))
                    {
                        sqlQuery.ChangeToDelete();
                        rowAffected = SqlUtil.ExecuteSql(sqlQuery.Sql, dbConnection, dbTransaction, sqlQuery.Parameters, commandTimeout);
                    }
                    dbTransactionContext.Commit();
                }
                catch (Exception)
                {
                    dbTransactionContext.Rollback();
                    throw;
                }
            }
            return rowAffected;
        }
        public static int InsertFromQuery<T>(this IQueryable<T> querable, string tableName, Expression<Func<T, object>> insertObjectExpression)
        {
            return querable.InsertFromQuery(tableName, insertObjectExpression, null);
        }
        public static int InsertFromQuery<T>(this IQueryable<T> querable, string tableName, Expression<Func<T, object>> insertObjectExpression, int? commandTimeout)
        {
            int rowAffected = 0;

            using (var dbTransactionContext = new DbTransactionContext(querable.GetDbContext(), commandTimeout: commandTimeout))
            {
                try
                {
                    var dbConnection = dbTransactionContext.Connection;
                    var dbTransaction = dbTransactionContext.CurrentTransaction;
                    var sqlQuery = SqlBuilder.Parse(querable.GetSql(), querable.GetObjectQuery());
                    tableName = CommonUtil.FormatTableName(tableName);
                    if (SqlUtil.TableExists(tableName, dbConnection, dbTransaction))
                    {
                        sqlQuery.ChangeToInsert(tableName, insertObjectExpression);
                        SqlUtil.ToggleIdentityInsert(true, tableName, dbConnection, dbTransaction);
                        rowAffected = SqlUtil.ExecuteSql(sqlQuery.Sql, dbConnection, dbTransaction, sqlQuery.Parameters, commandTimeout);
                        SqlUtil.ToggleIdentityInsert(false, tableName, dbConnection, dbTransaction);
                    }
                    else
                    {
                        sqlQuery.Clauses.First().InputText += string.Format(" INTO {0}", tableName);
                        rowAffected = SqlUtil.ExecuteSql(sqlQuery.Sql, dbConnection, dbTransaction, sqlQuery.Parameters, commandTimeout);
                    }
                    dbTransactionContext.Commit();
                }
                catch (Exception)
                {
                    dbTransactionContext.Rollback();
                    throw;
                }
            }
            return rowAffected;
        }
        public static QueryToFileResult QueryToCsvFile<T>(this IQueryable<T> querable, String filePath)
        {
            return QueryToCsvFile<T>(querable, filePath, new QueryToFileOptions());
        }
        public static QueryToFileResult QueryToCsvFile<T>(this IQueryable<T> querable, Stream stream)
        {
            return QueryToCsvFile<T>(querable, stream, new QueryToFileOptions());
        }
        public static QueryToFileResult QueryToCsvFile<T>(this IQueryable<T> querable, String filePath, Action<QueryToFileOptions> optionsAction)
        {
            return QueryToCsvFile<T>(querable, filePath, optionsAction.Build());
        }
        public static QueryToFileResult QueryToCsvFile<T>(this IQueryable<T> querable, Stream stream, Action<QueryToFileOptions> optionsAction)
        {
            return QueryToCsvFile<T>(querable, stream, optionsAction.Build());
        }
        public static QueryToFileResult QueryToCsvFile<T>(this IQueryable<T> querable, String filePath, QueryToFileOptions options)
        {
            var fileStream = File.Create(filePath);
            return QueryToCsvFile<T>(querable, fileStream, options);
        }
        public static QueryToFileResult QueryToCsvFile<T>(this IQueryable<T> querable, Stream stream, QueryToFileOptions options)
        {
            return InternalQueryToFile<T>(querable, stream, options);
        }
        public static QueryToFileResult SqlQueryToCsvFile(this Database database, string filePath, string sqlText, params object[] parameters)
        {
            return SqlQueryToCsvFile(database, filePath, new QueryToFileOptions(), sqlText, parameters);
        }
        public static QueryToFileResult SqlQueryToCsvFile(this Database database, Stream stream, string sqlText, params object[] parameters)
        {
            return SqlQueryToCsvFile(database, stream, new QueryToFileOptions(), sqlText, parameters);
        }
        public static QueryToFileResult SqlQueryToCsvFile(this Database database, string filePath, Action<QueryToFileOptions> optionsAction, string sqlText, params object[] parameters)
        {
            return SqlQueryToCsvFile(database, filePath, optionsAction.Build(), sqlText, parameters);
        }
        public static QueryToFileResult SqlQueryToCsvFile(this Database database, Stream stream, Action<QueryToFileOptions> optionsAction, string sqlText, params object[] parameters)
        {
            return SqlQueryToCsvFile(database, stream, optionsAction.Build(), sqlText, parameters);
        }
        public static QueryToFileResult SqlQueryToCsvFile(this Database database, string filePath, QueryToFileOptions options, string sqlText, params object[] parameters)
        {
            var fileStream = File.Create(filePath);
            return SqlQueryToCsvFile(database, fileStream, options, sqlText, parameters);
        }
        public static QueryToFileResult SqlQueryToCsvFile(this Database database, Stream stream, QueryToFileOptions options, string sqlText, params object[] parameters)
        {
            return InternalQueryToFile(database, stream, options, sqlText, parameters);
        }
        private static QueryToFileResult InternalQueryToFile<T>(this IQueryable<T> querable, Stream stream, QueryToFileOptions options)
        {
            var dbQuery = querable as DbQuery<T>;
            return InternalQueryToFile(dbQuery.GetDbContext().Database, stream, options, dbQuery.Sql);
        }
        private static QueryToFileResult InternalQueryToFile(Database database, Stream stream, QueryToFileOptions options, string sqlText, object[] parameters = null)
        {
            int dataRowCount = 0;
            int totalRowCount = 0;
            long bytesWritten = 0;

            using (var command = database.CreateCommand())
            {
                command.CommandText = sqlText;
                if (parameters != null)
                {
                    command.Parameters.AddRange(parameters);
                }
                if (options.CommandTimeout.HasValue)
                {
                    command.CommandTimeout = options.CommandTimeout.Value;
                }

                StreamWriter streamWriter = new StreamWriter(stream);
                using (var reader = command.ExecuteReader())
                {
                    //Header row
                    if (options.IncludeHeaderRow)
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            streamWriter.Write(options.TextQualifer);
                            streamWriter.Write(reader.GetName(i));
                            streamWriter.Write(options.TextQualifer);
                            if (i != reader.FieldCount - 1)
                            {
                                streamWriter.Write(options.ColumnDelimiter);
                            }
                        }
                        totalRowCount++;
                        streamWriter.Write(options.RowDelimiter);
                    }
                    //Write data rows to file
                    while (reader.Read())
                    {
                        Object[] values = new Object[reader.FieldCount];
                        reader.GetValues(values);
                        for (int i = 0; i < values.Length; i++)
                        {
                            streamWriter.Write(options.TextQualifer);
                            streamWriter.Write(values[i]);
                            streamWriter.Write(options.TextQualifer);
                            if (i != values.Length - 1)
                            {
                                streamWriter.Write(options.ColumnDelimiter);
                            }
                        }
                        streamWriter.Write(options.RowDelimiter);
                        dataRowCount++;
                        totalRowCount++;
                    }
                    streamWriter.Flush();
                    bytesWritten = streamWriter.BaseStream.Length;
                    streamWriter.Close();
                }
            }
            return new QueryToFileResult()
            {
                BytesWritten = bytesWritten,
                DataRowCount = dataRowCount,
                TotalRowCount = totalRowCount
            };
        }
        public static int UpdateFromQuery<T>(this IQueryable<T> querable, Expression<Func<T, T>> updateExpression)
        {
            return querable.UpdateFromQuery(updateExpression, null);
        }
        public static int UpdateFromQuery<T>(this IQueryable<T> querable, Expression<Func<T, T>> updateExpression, int? commandTimeout)
        {
            int rowAffected = 0;
            using (var dbTransactionContext = new DbTransactionContext(querable.GetDbContext(), commandTimeout: commandTimeout))
            {
                try
                {
                    var dbConnection = dbTransactionContext.Connection;
                    var dbTransaction = dbTransactionContext.CurrentTransaction as SqlTransaction;
                    var sqlQuery = SqlBuilder.Parse(querable.GetSql(), querable.GetObjectQuery());
                    sqlQuery.ChangeToUpdate("Extent1", updateExpression);
                    rowAffected = SqlUtil.ExecuteSql(sqlQuery.Sql, dbConnection, dbTransaction, sqlQuery.Parameters, commandTimeout);
                    dbTransactionContext.Commit();
                }
                catch (Exception)
                {
                    dbTransactionContext.Rollback();
                    throw;
                }
            }
            return rowAffected;
        }
        public static IQueryable<T> UsingTable<T>(this IQueryable<T> querable, string tableName) where T : class
        {
            var dbContext = querable.GetDbContext();
            var tableMapping = dbContext.GetTableMapping<T>();
            efExtensionsCommandInterceptor.AddCommand(Guid.NewGuid(),
                new EfExtensionsCommand
                {
                    CommandType = EfExtensionsCommandType.ChangeTableName,
                    OldValue = tableMapping.FullQualifedTableName,
                    NewValue = CommonUtil.FormatTableName(tableName),
                    Connection = dbContext.GetSqlConnection()
                });
            return querable;
        }
        public static void Clear<T>(this DbSet<T> dbSet) where T : class
        {
            var dbContext = dbSet.GetDbContext();
            var tableMapping = dbContext.GetTableMapping<T>();
            dbContext.Database.ClearTable(tableMapping.FullQualifedTableName);
        }
        public static void Truncate<T>(this DbContext dbContext) where T : class
        {
            var tableMapping = dbContext.GetTableMapping<T>();
            dbContext.Database.TruncateTable(tableMapping.FullQualifedTableName);
        }
        public static void Truncate<T>(this DbSet<T> dbSet) where T : class
        {
            var dbContext = dbSet.GetDbContext();
            dbContext.Truncate<T>();
        }
        internal static DbContext GetDbContext<T>(this IQueryable<T> querable)
        {
            DbContext dbContext;
            try
            {
                if (querable is DbQuery<T> dbQuery)
                {
                    var internalQuery = querable.GetPrivateFieldValue("InternalQuery");
                    var internalContext = internalQuery.GetPrivateFieldValue("InternalContext");
                    dbContext = internalContext.GetPrivateFieldValue("Owner") as DbContext;
                }
                else if (querable is ObjectQuery<T> objecQuery)
                {
                    var context = querable.GetPrivateFieldValue("Context") as ObjectContext;
                    dbContext = new DbContext(context, true);
                }
                else
                {
                    throw new NotSupportedException("This extension method requires a DbQuery<T> or ObjectQuery<T> instance");
                }
            }
            catch
            {
                throw;
            }
            return dbContext;
        }
        internal static IEnumerable<DbEntityEntry> GetEntriesToSave(this DbContext dbContext)
        {
            dbContext.ChangeTracker.DetectChanges();
            return dbContext.ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added ||
                    e.State == EntityState.Modified ||
                    e.State == EntityState.Deleted);
        }
        internal static ObjectQuery<T> GetObjectQuery<T>(this IQueryable<T> querable)
        {
            try
            {
                if (querable is DbQuery<T> dbQuery)
                {
                    var internalQuery = dbQuery.GetPrivateFieldValue("_internalQuery");
                    return internalQuery.GetPrivateFieldValue("ObjectQuery") as ObjectQuery<T>;
                }
                else if (querable is ObjectQuery<T>)
                {
                    return querable as ObjectQuery<T>;
                }
                else
                {
                    throw new NotSupportedException("This extension method requires a DbQuery<T> or ObjectQuery<T> instance");
                }
            }
            catch
            {
                throw;
            }
        }
        internal static string GetSql<T>(this IQueryable<T> querable)
        {
            String sql;
            try
            {
                if (querable is DbQuery<T> dbQuery)
                {
                    sql = dbQuery.Sql;
                }
                else if (querable is ObjectQuery<T> objectQuery)
                {
                    sql = objectQuery.ToTraceString();
                }
                else
                {
                    throw new NotSupportedException();
                }
            }
            catch
            {
                throw new NotSupportedException("This extension method requires a DbQuery<T> or ObjectQuery<T> instance");
            }
            return sql;
        }
        internal static SqlConnection GetSqlConnection(this DbContext context)
        {
            return context.Database.Connection as SqlConnection;
        }
        internal static TableMapping GetTableMapping<T>(this IObjectContextAdapter context, Type clrType = null) where T : class
        {
            clrType = ObjectContext.GetObjectType(clrType ?? typeof(T));
            var metadata = context.ObjectContext.MetadataWorkspace;

            // Get the part of the model that contains info about the actual CLR types
            var objectItemCollection = ((ObjectItemCollection)metadata.GetItemCollection(DataSpace.OSpace));

            //Force metadata to load when LazyLoading is enabled
            if (!metadata.GetItems<EntityType>(DataSpace.OSpace).Where(e => objectItemCollection.GetClrType(e) == clrType).Any())
            {
                context.ObjectContext.CreateObjectSet<T>();
            }

            // Get the entity type from the model that maps to the CLR type
            var entityType = metadata.GetItems<EntityType>(DataSpace.OSpace).Single(e => objectItemCollection.GetClrType(e) == clrType);

            // Get the entity set that uses this entity type
            var entitySet = metadata
                            .GetItems<EntityContainer>(DataSpace.CSpace)
                                  .Single()
                                  .EntitySets
                                  .Single(s => (s.ElementType.Name == entityType.Name)
                                    || (entityType.BaseType != null && s.ElementType.Name == entityType.BaseType.Name));

            // Find the mapping between conceptual and storage model for this entity set
            var mappings = metadata.GetItems<EntityContainerMapping>(DataSpace.CSSpace)
                                     .Single()
                                     .EntitySetMappings
                                     .Single(s => s.EntitySet == entitySet);

            // Find all properties (column) that are mapped
            var columns = new List<ScalarPropertyMapping>();
            var conditions = new List<ConditionPropertyMapping>();
            foreach (var mapping in mappings.EntityTypeMappings
                .Where(o => (o.EntityType == null || o.EntityType.Name == entityType.Name)))
            {
                foreach (var propertyMapping in mapping.Fragments.Single().PropertyMappings.OfType<ScalarPropertyMapping>().ToList())
                {
                    if (!columns.Any(o => o.Column == propertyMapping.Column))
                        columns.Add(propertyMapping);
                }
                conditions.AddRange(mapping.Fragments.Single().Conditions);
            }

            return new TableMapping(entitySet, entityType, clrType, mappings, columns, conditions);
        }
    }
}