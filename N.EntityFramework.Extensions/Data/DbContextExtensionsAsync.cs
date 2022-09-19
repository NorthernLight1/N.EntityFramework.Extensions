using N.EntityFramework.Extensions.Common;
using N.EntityFramework.Extensions.Extensions;
using N.EntityFramework.Extensions.Sql;
using N.EntityFramework.Extensions.Util;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Core.Mapping;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Infrastructure.Interception;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace N.EntityFramework.Extensions
{
    public static class DbContextExtensionsAsync
    {
        public async static Task<int> BulkDeleteAsync<T>(this DbContext context, IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class
        {
            return await context.BulkDeleteAsync(entities, new BulkDeleteOptions<T>(), cancellationToken);
        }
        public async static Task<int> BulkDeleteAsync<T>(this DbContext context, IEnumerable<T> entities, Action<BulkDeleteOptions<T>> optionsAction, CancellationToken cancellationToken = default) where T : class
        {
            return await context.BulkDeleteAsync(entities, optionsAction.Build(), cancellationToken);
        }
        public async static Task<int> BulkDeleteAsync<T>(this DbContext context, IEnumerable<T> entities, BulkDeleteOptions<T> options, CancellationToken cancellationToken = default) where T : class
        {
            var tableMapping = context.GetTableMapping<T>();

            using (var dbTransactionContext = new DbTransactionContext(context))
            {
                var dbConnection = dbTransactionContext.Connection;
                var transaction = dbTransactionContext.CurrentTransaction;
                int rowsAffected;
                try
                {
                    string stagingTableName = CommonUtil.GetStagingTableName(tableMapping, options.UsePermanentTable, dbConnection);
                    string destinationTableName = string.Format("[{0}].[{1}]", tableMapping.Schema, tableMapping.TableName);
                    string[] keyColumnNames = options.DeleteOnCondition != null ? CommonUtil<T>.GetColumns(options.DeleteOnCondition, new[] { "s" })
                        : tableMapping.GetPrimaryKeyColumns().ToArray();

                    if (keyColumnNames.Length == 0 && options.DeleteOnCondition == null)
                        throw new InvalidDataException("BulkDelete requires that the entity have a primary key or the Options.DeleteOnCondition must be set.");

                    context.Database.CloneTable(destinationTableName, stagingTableName, keyColumnNames);
                    await BulkInsertAsync(entities, options, tableMapping, dbConnection, transaction, stagingTableName, keyColumnNames, SqlBulkCopyOptions.KeepIdentity,
                        false, false, cancellationToken);
                    string deleteSql = string.Format("DELETE t FROM {0} s JOIN {1} t ON {2}", stagingTableName, destinationTableName,
                        CommonUtil<T>.GetJoinConditionSql(options.DeleteOnCondition, keyColumnNames));
                    rowsAffected = await context.Database.ExecuteSqlCommandAsync(deleteSql, cancellationToken);
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
        public async static Task FetchAsync<T>(this IQueryable<T> querable, Action<FetchResult<T>> action, Action<FetchOptions<T>> optionsAction, CancellationToken cancellationToken = default) where T : class, new()
        {
            await FetchAsync(querable, action, optionsAction.Build(), cancellationToken);
        }
        public async static Task FetchAsync<T>(this IQueryable<T> querable, Action<FetchResult<T>> action, FetchOptions<T> options, CancellationToken cancellationToken = default) where T : class, new()
        {
            var dbContext = querable.GetDbContext();
            var dbConnection = dbContext.GetSqlConnection();
            //Open datbase connection
            if (dbConnection.State == ConnectionState.Closed)
                dbConnection.Open();

            var sqlQuery = SqlBuilder.Parse(querable.GetSql(), querable.GetObjectQuery());
            if (options.InputColumns != null || options.IgnoreColumns != null)
            {
                var tableMapping = dbContext.GetTableMapping<T>();
                IEnumerable<string> columnNames = options.InputColumns != null ? options.InputColumns.GetObjectProperties() : tableMapping.GetColumns(true);
                IEnumerable<string> columnsToFetch = CommonUtil.FormatColumns(columnNames.Where(o => !options.IgnoreColumns.GetObjectProperties().Contains(o)));
                sqlQuery.SelectColumns(columnsToFetch);
            }
            var command = new SqlCommand(sqlQuery.Sql, dbConnection);
            command.Parameters.AddRange(sqlQuery.Parameters);
            var reader = await command.ExecuteReaderAsync(cancellationToken);

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
            while (await reader.ReadAsync(cancellationToken))
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
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (entities.Count > 0)
                action(new FetchResult<T> { Results = entities, Batch = batch });
            //close the DataReader
            reader.Close();
        }
        public async static Task<int> BulkInsertAsync<T>(this DbContext context, IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class
        {
            return await context.BulkInsertAsync<T>(entities, new BulkInsertOptions<T> { }, cancellationToken);
        }
        public async static Task<int> BulkInsertAsync<T>(this DbContext context, IEnumerable<T> entities, Action<BulkInsertOptions<T>> optionsAction, CancellationToken cancellationToken = default) where T : class
        {
            return await context.BulkInsertAsync<T>(entities, optionsAction.Build(), cancellationToken);
        }
        public async static Task<int> BulkInsertAsync<T>(this DbContext context, IEnumerable<T> entities, BulkInsertOptions<T> options, CancellationToken cancellationToken = default) where T : class
        {
            int rowsAffected = 0;
            var tableMapping = context.GetTableMapping<T>();

            using (var dbTransactionContext = new DbTransactionContext(context))
            {
                try
                {
                    var dbConnection = dbTransactionContext.Connection;
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
                    var bulkInsertResult = await BulkInsertAsync(entities, options, tableMapping, dbConnection, transaction, stagingTableName, columnNames, SqlBulkCopyOptions.KeepIdentity, true, true, cancellationToken);

                    List<string> columnsToOutput = new List<string> { "$Action", string.Format("{0}.{1}", "s", Constants.InternalId_ColumnName) };
                    List<PropertyInfo> propertySetters = new List<PropertyInfo>();
                    Type entityType = typeof(T);

                    foreach (var autoGeneratedColumnName in autoGeneratedColumnNames)
                    {
                        columnsToOutput.Add(string.Format("inserted.[{0}]", autoGeneratedColumnName));
                        propertySetters.Add(entityType.GetProperty(autoGeneratedColumnName));
                    }

                    string insertSqlText = string.Format("MERGE {0} t USING {1} s ON {2} WHEN NOT MATCHED THEN INSERT ({3}) VALUES ({3}){4};",
                        destinationTableName,
                        stagingTableName,
                        options.InsertIfNotExists ? CommonUtil<T>.GetJoinConditionSql(options.InsertOnCondition, primaryKeyColumnNames, "t", "s") : "1=2",
                        SqlUtil.ConvertToColumnString(columnsToInsert),
                        columnsToOutput.Count > 0 ? " OUTPUT " + SqlUtil.ConvertToColumnString(columnsToOutput) : "");

                    if (options.KeepIdentity && tableMapping.HasIdentity)
                        SqlUtil.ToggleIdentityInsert(true, destinationTableName, dbConnection, transaction);
                    var bulkQueryResult = await context.BulkQueryAsync(insertSqlText, dbConnection, transaction, options, cancellationToken);
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
                                    propertySetters[i-2].SetValue(entity, result[i]);
                                }
                            }
                        }
                    }

                    context.Database.DropTable(stagingTableName);
                    //ClearEntityStateToUnchanged(context, entities);
                    dbTransactionContext.Commit();
                    return rowsAffected;
                }
                catch (Exception ex)
                {
                    dbTransactionContext.Rollback();
                    throw ex;
                }
            }
        }
        private async static Task<BulkInsertResult<T>> BulkInsertAsync<T>(IEnumerable<T> entities, BulkOptions options, TableMapping tableMapping, SqlConnection dbConnection, SqlTransaction transaction, string tableName,
            IEnumerable<string> inputColumns = null, SqlBulkCopyOptions bulkCopyOptions = SqlBulkCopyOptions.Default, bool useInteralId = false, bool includeConditionColumns = true, CancellationToken cancellationToken = default)
        {
            var dataReader = new EntityDataReader<T>(tableMapping, entities, inputColumns, useInteralId);

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
            await sqlBulkCopy.WriteToServerAsync(dataReader, cancellationToken);

            return new BulkInsertResult<T>
            {
                RowsAffected = Convert.ToInt32(sqlBulkCopy.GetPrivateFieldValue("_rowsCopied")),
                EntityMap = dataReader.EntityMap
            };
        }
        public async static Task<BulkMergeResult<T>> BulkMergeAsync<T>(this DbContext context, IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class
        {
            return await BulkMergeAsync(context, entities, new BulkMergeOptions<T>(), cancellationToken);
        }
        public async static Task<BulkMergeResult<T>> BulkMergeAsync<T>(this DbContext context, IEnumerable<T> entities, BulkMergeOptions<T> options, CancellationToken cancellationToken = default) where T : class
        {
            return await InternalBulkMergeAsync(context, entities, options, cancellationToken);
        }
        public async static Task<BulkMergeResult<T>> BulkMergeAsync<T>(this DbContext context, IEnumerable<T> entities, Action<BulkMergeOptions<T>> optionsAction, CancellationToken cancellationToken = default) where T : class
        {
            return await BulkMergeAsync(context, entities, optionsAction.Build(), cancellationToken);
        }
        public async static Task<BulkSyncResult<T>> BulkSyncAsync<T>(this DbContext context, IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class
        {
            return await BulkSyncAsync(context, entities, new BulkSyncOptions<T>(), cancellationToken);
        }
        public async static Task<BulkSyncResult<T>> BulkSyncAsync<T>(this DbContext context, IEnumerable<T> entities, Action<BulkSyncOptions<T>> optionsAction, CancellationToken cancellationToken = default) where T : class
        {
            return BulkSyncResult<T>.Map(await InternalBulkMergeAsync(context, entities, optionsAction.Build(), cancellationToken));
        }
        public async static Task<BulkSyncResult<T>> BulkSyncAsync<T>(this DbContext context, IEnumerable<T> entities, BulkSyncOptions<T> options, CancellationToken cancellationToken = default) where T : class
        {
            return BulkSyncResult<T>.Map(await InternalBulkMergeAsync(context, entities, options, cancellationToken));
        }
        private async static Task<BulkMergeResult<T>> InternalBulkMergeAsync<T>(this DbContext context, IEnumerable<T> entities, BulkMergeOptions<T> options, CancellationToken cancellationToken = default) where T : class
        {
            int rowsAffected = 0;
            var outputRows = new List<BulkMergeOutputRow<T>>();
            var tableMapping = context.GetTableMapping<T>();
            int rowsInserted = 0;
            int rowsUpdated = 0;
            int rowsDeleted = 0;

            using (var dbTransactionContext = new DbTransactionContext(context))
            {
                var dbConnection = dbTransactionContext.Connection;
                var transaction = dbTransactionContext.CurrentTransaction;
                try
                {
                    string stagingTableName = CommonUtil.GetStagingTableName(tableMapping, options.UsePermanentTable, dbConnection);
                    string destinationTableName = string.Format("[{0}].[{1}]", tableMapping.Schema, tableMapping.TableName);
                    string[] columnNames = tableMapping.GetColumns().ToArray();
                    string[] primaryKeyColumnNames = tableMapping.GetPrimaryKeyColumns().ToArray();
                    IEnumerable<string> autoGeneratedColumnNames = options.AutoMapOutput ? tableMapping.GetAutoGeneratedColumns() : new string[] { };

                    if (primaryKeyColumnNames.Length == 0 && options.MergeOnCondition == null)
                        throw new InvalidDataException("BulkMerge requires that the entity have a primary key or the Options.MergeOnCondition must be set.");

                    context.Database.CloneTable(destinationTableName, stagingTableName, null, Common.Constants.InternalId_ColumnName);
                    var bulkInsertResult = await BulkInsertAsync(entities, options, tableMapping, dbConnection, transaction, stagingTableName, null, SqlBulkCopyOptions.KeepIdentity, true, true, cancellationToken);

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

                    var bulkQueryResult = await context.BulkQueryAsync(mergeSqlText, dbConnection, transaction, options, cancellationToken);
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
                                id = (primaryKeyColumnNames.Length > 0 ? Convert.ToString(result[2]) : "PrimaryKeyMissing");
                                entity = bulkInsertResult.EntityMap[entityId];
                                if (entity != null)
                                {

                                    for (int i = 2; i < 2 + primaryKeyColumnNames.Length; i++)
                                    {
                                        propertySetters[i - 2].SetValue(entity, SqlUtil.GetDBValue(result[i]));
                                    }
                                }
                            }
                            else
                            {
                                id = Convert.ToString(result[2 + primaryKeyColumnNames.Length]);
                            }
                            outputRows.Add(new BulkMergeOutputRow<T>(action, id));

                            if (action == SqlMergeAction.Insert) rowsInserted++;
                            else if (action == SqlMergeAction.Update) rowsUpdated++;
                            else if (action == SqlMergeAction.Delete) rowsDeleted++;
                            cancellationToken.ThrowIfCancellationRequested();
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
        public async static Task<int> BulkUpdateAsync<T>(this DbContext context, IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class
        {
            return await BulkUpdateAsync<T>(context, entities, new BulkUpdateOptions<T>(), cancellationToken);
        }
        public async static Task<int> BulkUpdateAsync<T>(this DbContext context, IEnumerable<T> entities, Action<BulkUpdateOptions<T>> optionsAction, CancellationToken cancellationToken = default) where T : class
        {
            return await BulkUpdateAsync<T>(context, entities, optionsAction.Build(), cancellationToken);
        }
        public async static Task<int> BulkUpdateAsync<T>(this DbContext context, IEnumerable<T> entities, BulkUpdateOptions<T> options, CancellationToken cancellationToken = default) where T : class
        {
            int rowsUpdated = 0;
            var outputRows = new List<BulkMergeOutputRow<T>>();
            var tableMapping = context.GetTableMapping<T>();

            using (var dbTransactionContext = new DbTransactionContext(context))
            {
                var dbConnection = dbTransactionContext.Connection;
                var transaction = dbTransactionContext.CurrentTransaction;
                try
                {
                    string stagingTableName = CommonUtil.GetStagingTableName(tableMapping, options.UsePermanentTable, dbConnection);
                    string destinationTableName = string.Format("[{0}].[{1}]", tableMapping.Schema, tableMapping.TableName);
                    string[] primaryKeyColumnNames = tableMapping.GetPrimaryKeyColumns().ToArray();
                    IEnumerable<string> columnNames = CommonUtil.FilterColumns(tableMapping.GetColumns(), primaryKeyColumnNames, options.InputColumns, options.IgnoreColumns);

                    if (primaryKeyColumnNames.Length == 0 && options.UpdateOnCondition == null)
                        throw new InvalidDataException("BulkUpdate requires that the entity have a primary key or the Options.UpdateOnCondition must be set.");

                    context.Database.CloneTable(destinationTableName, stagingTableName);
                    await BulkInsertAsync(entities, options, tableMapping, dbConnection, transaction, stagingTableName, null, SqlBulkCopyOptions.KeepIdentity);

                    IEnumerable<string> columnstoUpdate = CommonUtil.FormatColumns(columnNames.Where(o => !options.IgnoreColumns.GetObjectProperties().Contains(o)));

                    string updateSetExpression = string.Join(",", columnstoUpdate.Select(o => string.Format("t.{0}=s.{0}", o)));
                    string updateSql = string.Format("UPDATE t SET {0} FROM {1} AS s JOIN {2} AS t ON {3}; SELECT @@RowCount;",
                        updateSetExpression, stagingTableName, destinationTableName, CommonUtil<T>.GetJoinConditionSql(options.UpdateOnCondition, primaryKeyColumnNames, "s", "t"));

                    rowsUpdated = await context.Database.ExecuteSqlCommandAsync(updateSql, cancellationToken);
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
        private async static Task<BulkQueryResult> BulkQueryAsync(this DbContext context, string sqlText, SqlConnection dbConnection, SqlTransaction transaction, BulkOptions options, CancellationToken cancellationToken = default)
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
            var reader = await command.ExecuteReaderAsync(cancellationToken);
            //Get column names
            for (int i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(reader.GetName(i));
            }
            try
            {
                //Read data
                while (await reader.ReadAsync(cancellationToken))
                {
                    Object[] values = new Object[reader.FieldCount];
                    reader.GetValues(values);
                    results.Add(values);
                }
            }
            finally
            {
                //close the DataReader
                reader.Close();
            }

            return new BulkQueryResult
            {
                Columns = columns,
                Results = results,
                RowsAffected = reader.RecordsAffected
            };
        }
        public async static Task<int> DeleteFromQueryAsync<T>(this IQueryable<T> querable, int? commandTimeout = null, CancellationToken cancellationToken = default) where T : class
        {
            int rowAffected = 0;
            var dbContext = querable.GetDbContext();
            using (var dbTransactionContext = new DbTransactionContext(dbContext))
            {
                var dbConnection = dbTransactionContext.Connection;
                var dbTransaction = dbTransactionContext.CurrentTransaction;
                try
                {
                    var sqlQuery = SqlBuilder.Parse(querable.GetSql(), querable.GetObjectQuery());
                    sqlQuery.ChangeToDelete(sqlQuery.GetTableAlias());
                    rowAffected = await dbContext.Database.ExecuteSqlCommandAsync(sqlQuery.Sql, cancellationToken, sqlQuery.Parameters);

                    dbTransactionContext.Commit();
                }
                catch (Exception ex)
                {
                    dbTransactionContext.Rollback();
                    throw ex;
                }
            }
            return rowAffected;
        }
        public async static Task<int> InsertFromQueryAsync<T>(this IQueryable<T> querable, string tableName, Expression<Func<T, object>> insertObjectExpression, int? commandTimeout = null,
            CancellationToken cancellationToken = default) where T : class
        {
            int rowAffected = 0;
            var dbContext = querable.GetDbContext();
            using (var dbTransactionContext = new DbTransactionContext(dbContext))
            {
                var dbConnection = dbTransactionContext.Connection;
                var dbTransaction = dbTransactionContext.CurrentTransaction;
                try
                {
                    var sqlQuery = SqlBuilder.Parse(querable.GetSql(), querable.GetObjectQuery());
                    if (SqlUtil.TableExists(tableName, dbConnection, dbTransaction))
                    {
                        sqlQuery.ChangeToInsert(tableName, insertObjectExpression);
                        SqlUtil.ToggleIdentityInsert(true, tableName, dbConnection, dbTransaction);
                        rowAffected = await dbContext.Database.ExecuteSqlCommandAsync(sqlQuery.Sql, cancellationToken, sqlQuery.Parameters.ToArray());
                        SqlUtil.ToggleIdentityInsert(false, tableName, dbConnection, dbTransaction);
                    }
                    else
                    {
                        sqlQuery.Clauses.First().InputText += string.Format(" INTO {0}", tableName);
                        rowAffected = await dbContext.Database.ExecuteSqlCommandAsync(sqlQuery.Sql, cancellationToken, sqlQuery.Parameters.ToArray());
                    }

                    dbTransactionContext.Commit();
                }
                catch (Exception ex)
                {
                    dbTransactionContext.Rollback();
                    throw ex;
                }
            }
            return rowAffected;
        }
        public async static Task<int> UpdateFromQueryAsync<T>(this IQueryable<T> querable, Expression<Func<T, T>> updateExpression, int? commandTimeout = null,
            CancellationToken cancellationToken = default) where T : class
        {
            int rowAffected = 0;
            var dbContext = querable.GetDbContext();
            using (var dbTransactionContext = new DbTransactionContext(dbContext))
            {
                var dbConnection = dbTransactionContext.Connection;
                var dbTransaction = dbTransactionContext.CurrentTransaction;
                try
                {
                    var sqlQuery = SqlBuilder.Parse(querable.GetSql(), querable.GetObjectQuery());
                    sqlQuery.ChangeToUpdate(sqlQuery.GetTableAlias(), updateExpression);
                    rowAffected = await dbContext.Database.ExecuteSqlCommandAsync(sqlQuery.Sql, cancellationToken, sqlQuery.Parameters.ToArray());
                    dbTransactionContext.Commit();
                }
                catch (Exception ex)
                {
                    dbTransactionContext.Rollback();
                    throw ex;
                }
            }
            return rowAffected;
        }
        public async static Task<QueryToFileResult> QueryToCsvFileAsync<T>(this IQueryable<T> querable, String filePath, CancellationToken cancellationToken = default) where T : class
        {
            return await QueryToCsvFileAsync<T>(querable, filePath, new QueryToFileOptions(), cancellationToken);
        }
        public async static Task<QueryToFileResult> QueryToCsvFileAsync<T>(this IQueryable<T> querable, Stream stream, CancellationToken cancellationToken = default) where T : class
        {
            return await QueryToCsvFileAsync<T>(querable, stream, new QueryToFileOptions(), cancellationToken);
        }
        public async static Task<QueryToFileResult> QueryToCsvFileAsync<T>(this IQueryable<T> querable, String filePath, Action<QueryToFileOptions> optionsAction,
            CancellationToken cancellationToken = default) where T : class
        {
            return await QueryToCsvFileAsync<T>(querable, filePath, optionsAction.Build(), cancellationToken);
        }
        public async static Task<QueryToFileResult> QueryToCsvFileAsync<T>(this IQueryable<T> querable, Stream stream, Action<QueryToFileOptions> optionsAction,
            CancellationToken cancellationToken = default) where T : class
        {
            return await QueryToCsvFileAsync<T>(querable, stream, optionsAction.Build(), cancellationToken);
        }
        public async static Task<QueryToFileResult> QueryToCsvFileAsync<T>(this IQueryable<T> querable, String filePath, QueryToFileOptions options,
            CancellationToken cancellationToken = default) where T : class
        {
            var fileStream = File.Create(filePath);
            return await QueryToCsvFileAsync<T>(querable, fileStream, options, cancellationToken);
        }
        public async static Task<QueryToFileResult> QueryToCsvFileAsync<T>(this IQueryable<T> querable, Stream stream, QueryToFileOptions options,
            CancellationToken cancellationToken = default) where T : class
        {
            return await InternalQueryToFileAsync<T>(querable, stream, options, cancellationToken);
        }
        public async static Task<QueryToFileResult> SqlQueryToCsvFileAsync(this Database database, string filePath, string sqlText, object[] parameters,
            CancellationToken cancellationToken = default)
        {
            return await SqlQueryToCsvFileAsync(database, filePath, new QueryToFileOptions(), sqlText, parameters, cancellationToken);
        }
        public async static Task<QueryToFileResult> SqlQueryToCsvFileAsync(this Database database, Stream stream, string sqlText, object[] parameters,
            CancellationToken cancellationToken = default)
        {
            return await SqlQueryToCsvFileAsync(database, stream, new QueryToFileOptions(), sqlText, parameters, cancellationToken);
        }
        public async static Task<QueryToFileResult> SqlQueryToCsvFileAsync(this Database database, string filePath, Action<QueryToFileOptions> optionsAction, string sqlText, object[] parameters,
            CancellationToken cancellationToken = default)
        {
            return await SqlQueryToCsvFileAsync(database, filePath, optionsAction.Build(), sqlText, parameters, cancellationToken);
        }
        public async static Task<QueryToFileResult> SqlQueryToCsvFileAsync(this Database database, Stream stream, Action<QueryToFileOptions> optionsAction, string sqlText, object[] parameters,
            CancellationToken cancellationToken = default)
        {
            return await SqlQueryToCsvFileAsync(database, stream, optionsAction.Build(), sqlText, parameters, cancellationToken);
        }
        public async static Task<QueryToFileResult> SqlQueryToCsvFileAsync(this Database database, string filePath, QueryToFileOptions options, string sqlText, object[] parameters,
            CancellationToken cancellationToken = default)
        {
            var fileStream = File.Create(filePath);
            return await SqlQueryToCsvFileAsync(database, fileStream, options, sqlText, parameters, cancellationToken);
        }
        public async static Task<QueryToFileResult> SqlQueryToCsvFileAsync(this Database database, Stream stream, QueryToFileOptions options, string sqlText, object[] parameters,
            CancellationToken cancellationToken = default)
        {
            var dbConnection = database.Connection as SqlConnection;
            return await InternalQueryToFileAsync(dbConnection, stream, options, sqlText, parameters, cancellationToken);
        }
        public async static Task ClearAsync<T>(this DbSet<T> dbSet, CancellationToken cancellationToken = default) where T : class
        {
            var dbContext = dbSet.GetDbContext();
            var tableMapping = dbContext.GetTableMapping<T>();
            await dbContext.Database.ClearTableAsync(tableMapping.FullQualifedTableName, cancellationToken);
        }
        public async static Task TruncateAsync<T>(this DbSet<T> dbSet, CancellationToken cancellationToken = default) where T : class
        {
            var dbContext = dbSet.GetDbContext();
            var tableMapping = dbContext.GetTableMapping<T>();
            await dbContext.Database.TruncateTableAsync(tableMapping.FullQualifedTableName, false, cancellationToken);
        }
        private async static Task<QueryToFileResult> InternalQueryToFileAsync<T>(this IQueryable<T> querable, Stream stream, QueryToFileOptions options,
            CancellationToken cancellationToken = default) where T : class
        {
            var dbContext = querable.GetDbContext();
            var dbConnection = dbContext.GetSqlConnection();
            return await InternalQueryToFileAsync(dbConnection, stream, options, querable.GetSql(), null, cancellationToken);
        }
        private async static Task<QueryToFileResult> InternalQueryToFileAsync(SqlConnection dbConnection, Stream stream, QueryToFileOptions options, string sqlText, object[] parameters = null,
            CancellationToken cancellationToken = default)
        {
            int dataRowCount = 0;
            int totalRowCount = 0;
            long bytesWritten = 0;

            //Open datbase connection
            if (dbConnection.State == ConnectionState.Closed)
                dbConnection.Open();

            var command = new SqlCommand(sqlText, dbConnection);
            if (parameters != null)
            {
                command.Parameters.AddRange(parameters);
            }
            if (options.CommandTimeout.HasValue)
            {
                command.CommandTimeout = options.CommandTimeout.Value;
            }

            StreamWriter streamWriter = new StreamWriter(stream);
            using (var reader = await command.ExecuteReaderAsync(cancellationToken))
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
                            await streamWriter.WriteAsync(options.ColumnDelimiter);
                        }
                    }
                    totalRowCount++;
                    await streamWriter.WriteAsync(options.RowDelimiter);
                }
                //Write data rows to file
                while (await reader.ReadAsync(cancellationToken))
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
                            await streamWriter.WriteAsync(options.ColumnDelimiter);
                        }
                    }
                    await streamWriter.WriteAsync(options.RowDelimiter);
                    dataRowCount++;
                    totalRowCount++;
                }
                await streamWriter.FlushAsync();
                bytesWritten = streamWriter.BaseStream.Length;
                streamWriter.Close();
            }
            return new QueryToFileResult()
            {
                BytesWritten = bytesWritten,
                DataRowCount = dataRowCount,
                TotalRowCount = totalRowCount
            };
        }
    }
}
