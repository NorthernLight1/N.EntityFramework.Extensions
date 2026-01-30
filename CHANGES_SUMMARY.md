# Summary of Changes for Source Control

## Overview
Fixes and improvements from source code review: correctness (connection/transaction usage), resource disposal, exception handling, and consistency.

---

## Files Modified

### N.EntityFramework.Extensions\Data\DbContextExtensions.cs

- **BulkQuery – use correct connection**
  - Create command from `dbConnection` instead of `context.Database.Connection` so MERGE/query runs on the same connection (and transaction) as the rest of the bulk operation when using `ConnectionBehavior.New`.
  - Wrap command and reader in `using` for proper disposal.

- **Fetch / FetchInternal – dispose readers**
  - Wrap `command.ExecuteReader()` in `using (var reader = ...)` so the reader is disposed if `action` throws or when iteration stops.

- **QueryToCsvFile (file path overloads) – dispose file streams**
  - Wrap `File.Create(filePath)` in `using` in `QueryToCsvFile(querable, filePath, options)` and `SqlQueryToCsvFile(database, filePath, options, sqlText, parameters)`.

- **InternalQueryToFile – dispose StreamWriter**
  - Use `using (var streamWriter = new StreamWriter(stream))` and remove explicit `streamWriter.Close()`.

- **Exception rethrow**
  - In BulkInsert catch block: change `throw ex` to `throw` to preserve stack trace.

- **Typo**
  - Rename variable `autoDetectCahngesEnabled` to `autoDetectChangesEnabled` in `ClearEntityStateToUnchanged`.

---

### N.EntityFramework.Extensions\Data\DbContextExtensionsAsync.cs

- **BulkDeleteAsync – run DELETE in same transaction**
  - Replace `context.Database.ExecuteSqlCommandAsync(deleteSql, cancellationToken)` with `SqlUtil.ExecuteSqlAsync(deleteSql, dbConnection, transaction, null, options.CommandTimeout)` so the DELETE runs on the same connection and transaction as CloneTable/BulkInsert/DropTable.

- **BulkQueryAsync – correct connection and disposal**
  - Create command from `dbConnection.CreateCommand()` instead of `context.Database.CreateCommand(options.ConnectionBehavior)` so it matches the passed connection/transaction.
  - Wrap command and reader in `using` and return from inside the reader block so both are disposed.

- **BulkInsertAsync – dispose EntityDataReader**
  - Wrap `new EntityDataReader<T>(...)` in `using (var dataReader = ...)` so the reader (and its enumerator) is disposed.

- **QueryToCsvFileAsync / SqlQueryToCsvFileAsync (file path overloads) – dispose file streams**
  - Wrap `File.Create(filePath)` in `using` in the file-path overloads of `QueryToCsvFileAsync` and `SqlQueryToCsvFileAsync`.

- **InternalQueryToFileAsync – dispose StreamWriter**
  - Use `using (var streamWriter = new StreamWriter(stream))` and remove explicit `streamWriter.Close()`.

- **Exception rethrow**
  - In catch blocks for BulkInsertAsync, DeleteFromQueryAsync, InsertFromQueryAsync, UpdateFromQueryAsync: change `throw ex` to `throw` to preserve stack trace.

- **BulkSaveChangesAsync default**
  - Change default of `autoMapOutput` from `false` to `true` in `BulkSaveChangesAsync(acceptAllChangesOnSuccess, autoMapOutput, cancellationToken)` to match sync `BulkSaveChanges(..., autoMapOutput)`.

---

### N.EntityFramework.Extensions\Util\SqlUtil.cs

- **ExecuteSqlAsync**
  - Add async overload: `ExecuteSqlAsync(string query, DbConnection connection, DbTransaction transaction, object[] parameters = null, int? commandTimeout = null)` that uses `CreateCommand`, sets transaction/timeout/parameters, and calls `ExecuteNonQueryAsync()` within a `using` block. Used by BulkDeleteAsync so the DELETE runs on the transaction context’s connection.

---

## Summary by Category

| Category              | Change |
|----------------------|--------|
| Correctness          | BulkQuery and BulkQueryAsync use passed `dbConnection`; BulkDeleteAsync DELETE runs in same transaction via `SqlUtil.ExecuteSqlAsync`. |
| Resource disposal    | File streams, StreamWriter, DbCommand, DbDataReader, and EntityDataReader wrapped in `using` where applicable. |
| Exception handling  | All `throw ex` replaced with `throw` to preserve stack traces. |
| Consistency          | BulkSaveChangesAsync `autoMapOutput` default set to `true` to match sync API. |
| Typo                 | `autoDetectCahngesEnabled` → `autoDetectChangesEnabled`. |

---

## Suggested Commit Message

```
Fix bulk operations connection/transaction, disposal, and rethrow

- BulkQuery/BulkQueryAsync: use passed dbConnection for command so MERGE
  runs on same connection/transaction; wrap command and reader in using.
- BulkDeleteAsync: run DELETE via SqlUtil.ExecuteSqlAsync on
  dbConnection/transaction so it participates in same transaction.
- Add SqlUtil.ExecuteSqlAsync for async non-query on given connection.
- Dispose: wrap File.Create, StreamWriter, command, reader, and
  EntityDataReader in using in Fetch, FetchInternal, BulkQuery,
  BulkQueryAsync, BulkInsertAsync, QueryToCsvFile, SqlQueryToCsvFile,
  and async file/stream overloads.
- Replace throw ex with throw to preserve stack trace (multiple catch blocks).
- BulkSaveChangesAsync: default autoMapOutput to true to match sync API.
- Fix typo: autoDetectCahngesEnabled -> autoDetectChangesEnabled.
```
