using System;
using System.Data.Entity;
using System.Data.SqlClient;

namespace N.EntityFramework.Extensions
{
    internal class DbTransactionContext : IDisposable
    {
        private bool closeConnection;
        private bool ownsTransaction;
        private DbContext context;
        private DbContextTransaction transaction;

        public SqlConnection Connection { get; internal set; }
        public SqlTransaction CurrentTransaction => transaction != null ? transaction.UnderlyingTransaction as SqlTransaction : null;



        public DbTransactionContext(DbContext context, bool openConnection = true, bool useTransaction = true)
        {
            this.context = context;
            this.ownsTransaction = context.Database.CurrentTransaction == null;
            this.transaction = context.Database.CurrentTransaction;
            if(useTransaction && transaction == null)
            {
                this.transaction = context.Database.BeginTransaction();
            }
            this.Connection = context.GetSqlConnection();

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
            if (closeConnection)
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