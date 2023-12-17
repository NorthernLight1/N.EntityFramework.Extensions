namespace N.EntityFramework.Extensions.Test.Data
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Initial : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Orders",
                c => new
                    {
                        Id = c.Long(nullable: false, identity: true),
                        ExternalId = c.String(),
                        GlobalId = c.Guid(),
                        Price = c.Decimal(nullable: false, precision: 18, scale: 2),
                        AddedDateTime = c.DateTime(nullable: false),
                        ModifiedDateTime = c.DateTime(),
                        DbAddedDateTime = c.DateTime(nullable: false, defaultValueSql: "GETDATE()"),
                        Trigger = c.Boolean(),
                        Active = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.Products",
                c => new
                    {
                        Id = c.String(nullable: false, maxLength: 128),
                        Price = c.Decimal(nullable: false, precision: 18, scale: 2),
                        OutOfStock = c.Boolean(nullable: false),
                        Status = c.String(maxLength: 25),
                        StatusEnum = c.Int(),
                        UpdatedDateTime = c.DateTime(),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.ProductWithComplexKeys",
                c => new
                    {
                        Key1 = c.Guid(nullable: false, identity: true),
                        Key2 = c.Guid(nullable: false, identity: true),
                        Key3 = c.Guid(nullable: false, identity: true),
                        Price = c.Decimal(nullable: false, precision: 18, scale: 2),
                        OutOfStock = c.Boolean(nullable: false),
                        Status = c.String(maxLength: 25),
                        UpdatedDateTime = c.DateTime(),
                    })
                .PrimaryKey(t => new { t.Key1, t.Key2, t.Key3 });
            
            CreateTable(
                "dbo.TphPeople",
                c => new
                    {
                        Id = c.Long(nullable: false, identity: true),
                        FirstName = c.String(),
                        LastName = c.String(),
                        Email = c.String(),
                        Phone = c.String(),
                        AddedDate = c.DateTime(),
                        Email1 = c.String(),
                        Phone1 = c.String(),
                        Url = c.String(),
                        Discriminator = c.String(nullable: false, maxLength: 128),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.TptPeople",
                c => new
                    {
                        Id = c.Long(nullable: false, identity: true),
                        FirstName = c.String(),
                        LastName = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.TptCustomer",
                c => new
                    {
                        Id = c.Long(nullable: false),
                        Email = c.String(),
                        Phone = c.String(),
                        AddedDate = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.TptPeople", t => t.Id)
                .Index(t => t.Id);
            
            CreateTable(
                "dbo.TptVendor",
                c => new
                    {
                        Id = c.Long(nullable: false),
                        Email = c.String(),
                        Phone = c.String(),
                        Url = c.String(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.TptPeople", t => t.Id)
                .Index(t => t.Id);
            
            CreateTable(
                "dbo.TpcCustomer",
                c => new
                    {
                        Id = c.Long(nullable: false),
                        FirstName = c.String(),
                        LastName = c.String(),
                        Email = c.String(),
                        Phone = c.String(),
                        AddedDate = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.TpcVendor",
                c => new
                    {
                        Id = c.Long(nullable: false),
                        FirstName = c.String(),
                        LastName = c.String(),
                        Email = c.String(),
                        Phone = c.String(),
                        Url = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.TptVendor", "Id", "dbo.TptPeople");
            DropForeignKey("dbo.TptCustomer", "Id", "dbo.TptPeople");
            DropIndex("dbo.TptVendor", new[] { "Id" });
            DropIndex("dbo.TptCustomer", new[] { "Id" });
            DropTable("dbo.TpcVendor");
            DropTable("dbo.TpcCustomer");
            DropTable("dbo.TptVendor");
            DropTable("dbo.TptCustomer");
            DropTable("dbo.TptPeople");
            DropTable("dbo.TphPeople");
            DropTable("dbo.ProductWithComplexKeys");
            DropTable("dbo.Products");
            DropTable("dbo.Orders");
        }
    }
}
