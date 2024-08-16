namespace N.EntityFramework.Extensions.Test.Data;

using System;
using System.Data.Entity.Migrations;

public partial class Initial : DbMigration
{
    public override void Up()
    {
        Sql("CREATE TRIGGER trgProductWithTriggers\r\nON ProductWithTriggers\r\nFOR INSERT, UPDATE, DELETE\r\nAS\r\nBEGIN\r\n" +
        "   PRINT 1 END");
    }

    public override void Down()
    {
    }
}