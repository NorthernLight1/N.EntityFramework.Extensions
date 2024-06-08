using System;
using System.Collections.Generic;
using System.Data.Entity.Migrations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace N.EntityFramework.Extensions.Test.Data
{
    internal sealed class TestDbConfiguration : DbMigrationsConfiguration<TestDbContext>
    {
        public TestDbConfiguration()
        {
            AutomaticMigrationsEnabled = true;
            AutomaticMigrationDataLossAllowed = true;
            ContextKey = "N.EntityFramework.Extensions.Test.Data.TestDbContext";
        }
    }
}