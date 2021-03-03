using N.EntityFramework.Extensions.Test.Data;
using System.Data.Entity;


namespace N.EntityFramework.Extensions.Test.Data
{
    public class TestDbContext : DbContext
    {
        public virtual DbSet<Order> Orders { get; set;  }
        public virtual DbSet<Article> Articles { get; set;  }

        public TestDbContext()
        {
            Database.SetInitializer(new MigrateDatabaseToLatestVersion<TestDbContext, TestDbConfiguration>());
        }
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        { 
            base.OnModelCreating(modelBuilder);
        }
    }
}
