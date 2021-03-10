using N.EntityFramework.Extensions.Test.Data;
using System.Data.Entity;


namespace N.EntityFramework.Extensions.Test.Data
{
    public class TestDbContext : DbContext
    {
        private static readonly string _connectionString = @"Data Source=(LocalDb)\MSSQLLocalDB;Initial Catalog = N.EntityFramework.Extensions.Test.Data.TestDbContext; Integrated Security = True; MultipleActiveResultSets=True";
        public virtual DbSet<Order> Orders { get; set;  }
        public virtual DbSet<Article> Articles { get; set;  }

        public TestDbContext() : base(_connectionString)
        {
            Database.SetInitializer(new MigrateDatabaseToLatestVersion<TestDbContext, TestDbConfiguration>());
        }
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        { 
            base.OnModelCreating(modelBuilder);
        }
    }
}
