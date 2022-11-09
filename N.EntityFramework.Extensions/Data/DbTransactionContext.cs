using N.EntityFramework.Extensions.Enums;
using System;
using System.Data.Entity;
using System.Data.SqlClient;

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

        public SqlConnection Connection { get; internal set; }
        public SqlTransaction CurrentTransaction => transaction != null ? transaction.UnderlyingTransaction as SqlTransaction : null;


        public DbTransactionContext(DbContext context, BulkOptions options) : this(context, options.ConnectionBehavior, options.TransactionalBehavior, options.CommandTimeout)
        {

        }
        public DbTransactionContext(DbContext context, ConnectionBehavior connectionBehavior = ConnectionBehavior.Default, TransactionalBehavior transactionalBehavior = TransactionalBehavior.DoNotEnsureTransaction, int? commandTimeout = null, bool openConnection = true)
        {
            this.context = context;
            this.ownsTransaction = context.Database.CurrentTransaction == null;
            this.transaction = context.Database.CurrentTransaction;
            this.connectionBehavior = connectionBehavior;
            this.defaultCommandTimeout = context.Database.CommandTimeout;
            context.Database.CommandTimeout = commandTimeout;

            if (transaction == null && transactionalBehavior == TransactionalBehavior.EnsureTransaction)
            {
                this.transaction = context.Database.BeginTransaction();
            }
            this.Connection = context.Database.GetConnection(connectionBehavior) as SqlConnection;
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
            if(transaction != null)
                transaction.Rollback();
        }
    }
}