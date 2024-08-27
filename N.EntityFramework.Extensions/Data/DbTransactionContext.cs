using System;
using System.Data.Common;
using System.Data.Entity;
using N.EntityFramework.Extensions.Enums;

namespace N.EntityFramework.Extensions
{
    internal class DbTransactionContext : IDisposable
    {
        private int? defaultCommandTimeout;
        private bool closeConnection;
        private bool ownsTransaction;
        private DbContext context;
        private DbContextTransaction transaction;
        private ConnectionBehavior connectionBehavior;

        public DbConnection Connection { get; internal set; }
        public DbTransaction CurrentTransaction => transaction != null ? transaction.UnderlyingTransaction: null;


        public DbTransactionContext(DbContext context, BulkOptions options) : this(context, options.ConnectionBehavior, options.TransactionalBehavior, options.CommandTimeout)
        {

        }
        public DbTransactionContext(DbContext context, ConnectionBehavior connectionBehavior = ConnectionBehavior.Default, TransactionalBehavior transactionalBehavior = TransactionalBehavior.DoNotEnsureTransaction, int? commandTimeout = null, bool openConnection = true)
        {
            this.context = context;
            this.connectionBehavior = connectionBehavior;
            if (connectionBehavior == ConnectionBehavior.Default)
            {
                this.ownsTransaction = context.Database.CurrentTransaction == null;
                this.transaction = context.Database.CurrentTransaction;
            }
            this.defaultCommandTimeout = context.Database.CommandTimeout;
            context.Database.CommandTimeout = commandTimeout;

            if (transaction == null && transactionalBehavior == TransactionalBehavior.EnsureTransaction)
            {
                this.transaction = context.Database.BeginTransaction();
            }
            this.Connection = context.Database.GetConnection(connectionBehavior);
            if (openConnection)
            {
                if (this.Connection.State == System.Data.ConnectionState.Closed)
                {
                    this.Connection.Open();
                    this.closeConnection = true;
                }
            }
        }

        public void Dispose()
        {
            context.Database.CommandTimeout = defaultCommandTimeout;
            if (closeConnection | (Connection.State == System.Data.ConnectionState.Open && connectionBehavior == ConnectionBehavior.New))
            {
                this.Connection.Close();
            }
        }

        internal void Commit()
        {
            if (this.ownsTransaction && this.transaction != null)
                transaction.Commit();
        }
        internal void Rollback()
        {
            if (this.ownsTransaction && transaction != null)
                transaction.Rollback();
        }
    }
}