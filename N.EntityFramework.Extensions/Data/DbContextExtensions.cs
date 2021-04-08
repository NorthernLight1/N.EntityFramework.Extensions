using N.EntityFramework.Extensions.Common;
using N.EntityFramework.Extensions.Extensions;
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
using System.IO;
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
        public static int BulkDelete<T>(this DbContext context, IEnumerable<T> entities, Action<BulkDeleteOptions<T>> optionsAction)
        {
            return context.BulkDelete(entities, optionsAction.Build());
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
                    rowsAffected = SqlUtil.ExecuteSql(deleteSql, dbConnection, transaction, options.CommandTimeout);
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
            return context.BulkInsert<T>(entities, new BulkInsertOptions<T>());
        }
        public static int BulkInsert<T>(this DbContext context, IEnumerable<T> entities, Action<BulkInsertOptions<T>> optionsAction)
        {
            return context.BulkInsert<T>(entities, optionsAction.Build());
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

                    SqlUtil.CloneTable(destinationTableName, stagingTableName, null, dbConnection, transaction, Common.Constants.InternalId_ColumnName);
                    var bulkInsertResult = BulkInsert(entities, options, tableMapping, dbConnection, transaction, stagingTableName, null, SqlBulkCopyOptions.KeepIdentity, true);

                    IEnumerable<string> columnsToInsert = columnNames;

                    List<string> columnsToOutput = new List<string> { "$Action", string.Format("{0}.{1}", "s", Constants.InternalId_ColumnName) };
                    List<PropertyInfo> propertySetters = new List<PropertyInfo>();
                    Type entityType = typeof(T);

                    foreach (var storeGeneratedColumnName in storeGeneratedColumnNames)
                    {
                        columnsToOutput.Add(string.Format("inserted.[{0}]", storeGeneratedColumnName));
                        propertySetters.Add(entityType.GetProperty(storeGeneratedColumnName));
                    }

                    string insertSqlText = string.Format("MERGE {0} t USING {1} s ON {2} WHEN NOT MATCHED THEN INSERT ({3}) VALUES ({3}){4};",
                        destinationTableName, 
                        stagingTableName,
                        options.InsertIfNotExists ? CommonUtil<T>.GetJoinConditionSql(options.InsertOnCondition, storeGeneratedColumnNames, "t", "s") : "1=2",
                        SqlUtil.ConvertToColumnString(columnsToInsert),
                        columnsToOutput.Count > 0 ? " OUTPUT " + SqlUtil.ConvertToColumnString(columnsToOutput) : "");

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
                            //var entityIndex = 1;
                            foreach(var result in bulkQueryResult.Results)
                            {
                                int entityId = (int)result[1];
                                var entity = bulkInsertResult.EntityMap[entityId];
                                for (int i = 2; i < columnsToOutput.Count; i++)
                                {
                                    propertySetters[2-i].SetValue(entity, result[i]);
                                }
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
                sqlBulkCopy.ColumnMappings.Add(Constants.InternalId_ColumnName, Constants.InternalId_ColumnName);
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
            return InternalBulkMerge(context, entities, options);
        }
        public static BulkMergeResult<T> BulkMerge<T>(this DbContext context, IEnumerable<T> entities, Action<BulkMergeOptions<T>> optionsAction)
        {
            return BulkMerge(context, entities, optionsAction.Build());
        }
        public static BulkSyncResult<T> BulkSync<T>(this DbContext context, IEnumerable<T> entities)
        {
            return BulkSync(context, entities, new BulkSyncOptions<T>());
        }
        public static BulkSyncResult<T> BulkSync<T>(this DbContext context, IEnumerable<T> entities, Action<BulkSyncOptions<T>> optionsAction)
        {
            return BulkSyncResult<T>.Map(InternalBulkMerge(context, entities, optionsAction.Build()));
        }
        public static BulkSyncResult<T> BulkSync<T>(this DbContext context, IEnumerable<T> entities, BulkSyncOptions<T> options)
        {
            return BulkSyncResult<T>.Map(InternalBulkMerge(context, entities, options));
        }
        private static BulkMergeResult<T> InternalBulkMerge<T>(this DbContext context, IEnumerable<T> entities, BulkMergeOptions<T> options)
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

                    SqlUtil.CloneTable(destinationTableName, stagingTableName, null, dbConnection, transaction, Common.Constants.InternalId_ColumnName);
                    var bulkInsertResult = BulkInsert(entities, options, tableMapping, dbConnection, transaction, stagingTableName, null, SqlBulkCopyOptions.KeepIdentity, true);

                    IEnumerable<string> columnsToInsert = columnNames.Where(o => !options.GetIgnoreColumnsOnInsert().Contains(o));
                    IEnumerable<string> columnstoUpdate = columnNames.Where(o => !options.GetIgnoreColumnsOnUpdate().Contains(o)).Select(o => string.Format("t.{0}=s.{0}", o));
                    List<string> columnsToOutput = new List<string> { "$Action", string.Format("{0}.{1}", "s", Constants.InternalId_ColumnName) };
                    List<PropertyInfo> propertySetters = new List<PropertyInfo>();
                    Type entityType = typeof(T);

                    foreach (var storeGeneratedColumnName in storeGeneratedColumnNames)
                    {
                        columnsToOutput.Add(string.Format("inserted.[{0}]", storeGeneratedColumnName));
                        columnsToOutput.Add(string.Format("deleted.[{0}]", storeGeneratedColumnName));
                        var storedGeneratedColumn = tableMapping.Columns.First(o => o.Column.Name == storeGeneratedColumnName);
                        propertySetters.Add(entityType.GetProperty(storeGeneratedColumnName));
                    }

                    string mergeSqlText = string.Format("MERGE {0} t USING {1} s ON ({2}) WHEN NOT MATCHED BY TARGET THEN INSERT ({3}) VALUES ({3}) WHEN MATCHED THEN UPDATE SET {4}{5}OUTPUT {6};",
                        destinationTableName, stagingTableName, CommonUtil<T>.GetJoinConditionSql(options.MergeOnCondition, storeGeneratedColumnNames, "s", "t"),
                        SqlUtil.ConvertToColumnString(columnsToInsert),
                        SqlUtil.ConvertToColumnString(columnstoUpdate),
                        options.DeleteIfNotMatched ? " WHEN NOT MATCHED BY SOURCE THEN DELETE " : " ",
                        SqlUtil.ConvertToColumnString(columnsToOutput)
                        );

                    var bulkQueryResult = context.BulkQuery(mergeSqlText, dbConnection, transaction, options);
                    rowsAffected = bulkQueryResult.RowsAffected;

                    foreach (var result in bulkQueryResult.Results)
                    {
                        string id = string.Empty;
                        object entity = null;
                        string action = (string)result[0];
                        if (action != SqlMergeAction.Delete)
                        {
                            int entityId = (int)result[1];
                            id = Convert.ToString(result[2]);
                            entity = bulkInsertResult.EntityMap[entityId];
                            if (options.AutoMapOutputIdentity && entity != null)
                            {

                                for (int i = 2; i < 2 + storeGeneratedColumnNames.Length; i++)
                                {
                                    propertySetters[0].SetValue(entity, result[i]);
                                }
                            }
                        }
                        else
                        {
                            id = Convert.ToString(result[2+storeGeneratedColumnNames.Length]);
                        }
                        outputRows.Add(new BulkMergeOutputRow<T>(action, id));

                        if (action == SqlMergeAction.Insert) rowsInserted++;
                        else if (action == SqlMergeAction.Update) rowsUpdated++;
                        else if (action == SqlMergeAction.Delete) rowsDeleted++;
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
        public static int BulkUpdate<T>(this DbContext context, IEnumerable<T> entities, Action<BulkUpdateOptions<T>> optionsAction)
        {
            return BulkUpdate<T>(context, entities, optionsAction.Build());
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

                    rowsUpdated = SqlUtil.ExecuteSql(updateSql, dbConnection, transaction, options.CommandTimeout);
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
        public static void Fetch<T>(this IQueryable<T> querable, Action<FetchResult<T>> action, Action<FetchOptions> optionsAction) where T : class, new()
        {
            Fetch(querable, action, optionsAction.Build());
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
            return querable.DeleteFromQuery(null);
        }
        public static int DeleteFromQuery<T>(this IQueryable<T> querable, int? commandTimeout)
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
                    var sqlQuery = SqlBuilder.Parse(dbQuery.Sql);
                    sqlQuery.ChangeToDelete("[Extent1]");
                    rowAffected = SqlUtil.ExecuteSql(sqlQuery.Sql, dbConnection, dbTransaction, commandTimeout);
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
            return querable.InsertFromQuery(tableName, insertObjectExpression, null);
        }
        public static int InsertFromQuery<T>(this IQueryable<T> querable, string tableName, Expression<Func<T, object>> insertObjectExpression, int? commandTimeout)
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
                    var sqlQuery = SqlBuilder.Parse(dbQuery.Sql);
                    if (SqlUtil.TableExists(tableName, dbConnection, dbTransaction))
                    {
                        sqlQuery.ChangeToInsert(tableName, insertObjectExpression);
                        SqlUtil.ToggleIdentityInsert(true, tableName, dbConnection, dbTransaction);
                        rowAffected = SqlUtil.ExecuteSql(sqlQuery.Sql, dbConnection, dbTransaction, commandTimeout);
                        SqlUtil.ToggleIdentityInsert(false, tableName, dbConnection, dbTransaction);
                    }
                    else
                    {
                        sqlQuery.Clauses.First().InputText += string.Format(" INTO {0}", tableName);
                        rowAffected = SqlUtil.ExecuteSql(sqlQuery.Sql, dbConnection, dbTransaction, commandTimeout);
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
        public static SqlQuery FromSqlQuery(this Database database, string sqlText, params object[] parameters)
        { 
            var dbConnection = database.Connection as SqlConnection;
            return new SqlQuery(dbConnection, sqlText, parameters);
        }
        public static bool TableExists(this Database database, string tableName)
        {
            var dbConnection = database.Connection as SqlConnection;
            return SqlUtil.TableExists(tableName, dbConnection, null);
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
            var dbConnection = database.Connection as SqlConnection;
            return InternalQueryToFile(dbConnection, stream, options, sqlText, parameters);
        }
        private static QueryToFileResult InternalQueryToFile<T>(this IQueryable<T> querable, Stream stream, QueryToFileOptions options)
        {
            var dbQuery = querable as DbQuery<T>;
            var dbConnection = GetSqlConnectionFromIQuerable(querable);
            return InternalQueryToFile(dbConnection, stream, options, dbQuery.Sql);
        }
        private static QueryToFileResult InternalQueryToFile(SqlConnection dbConnection, Stream stream, QueryToFileOptions options, string sqlText, object[] parameters=null)
        {
            int dataRowCount = 0;
            int totalRowCount=0;
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
                    for(int i=0; i<values.Length; i++)
                    {
                        streamWriter.Write(options.TextQualifer);
                        streamWriter.Write(values[i]);
                        streamWriter.Write(options.TextQualifer);
                        if(i != values.Length - 1)
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
            var dbQuery = querable as DbQuery<T>;
            var dbConnection = GetSqlConnectionFromIQuerable(querable);
            //Open datbase connection
            if (dbConnection.State == ConnectionState.Closed)
                dbConnection.Open();

            using (var dbTransaction = dbConnection.BeginTransaction())
            {
                try
                {
                    var sqlQuery = SqlBuilder.Parse(dbQuery.Sql);
                    string setSqlExpression = updateExpression.ToSqlUpdateSetExpression("Extent1");
                    sqlQuery.ChangeToUpdate("[Extent1]", setSqlExpression);
                    rowAffected = SqlUtil.ExecuteSql(sqlQuery.Sql, dbConnection, dbTransaction, commandTimeout);
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
                var context = internalQuery.GetPrivateFieldValue("_internalContext");
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

