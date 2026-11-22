using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LogiTrack.Migrations
{
    public partial class AddPersistentCartCheckout : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Orders",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    OrderItemId = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrderId = table.Column<int>(nullable: false),
                    InventoryItemId = table.Column<int>(nullable: false),
                    Name = table.Column<string>(nullable: false),
                    Quantity = table.Column<int>(nullable: false),
                    Location = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.OrderItemId);
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                "INSERT INTO \"OrderItems\" (\"OrderId\", \"InventoryItemId\", \"Name\", \"Quantity\", \"Location\") " +
                "SELECT \"OrderId\", \"ItemId\", \"Name\", \"Quantity\", \"Location\" " +
                "FROM \"InventoryItems\" WHERE \"OrderId\" IS NOT NULL;");

            migrationBuilder.Sql(
                "CREATE TABLE \"InventoryItems_new\" (" +
                "\"ItemId\" INTEGER NOT NULL CONSTRAINT \"PK_InventoryItems\" PRIMARY KEY AUTOINCREMENT, " +
                "\"Name\" TEXT NOT NULL, " +
                "\"Quantity\" INTEGER NOT NULL, " +
                "\"Location\" TEXT NOT NULL); " +
                "INSERT INTO \"InventoryItems_new\" (\"ItemId\", \"Name\", \"Quantity\", \"Location\") " +
                "SELECT \"ItemId\", \"Name\", \"Quantity\", \"Location\" " +
                "FROM \"InventoryItems\" WHERE \"OrderId\" IS NULL; " +
                "DROP TABLE \"InventoryItems\"; " +
                "ALTER TABLE \"InventoryItems_new\" RENAME TO \"InventoryItems\"; " +
                "INSERT INTO \"sqlite_sequence\" (\"name\", \"seq\") " +
                "SELECT 'InventoryItems', COALESCE(MAX(\"InventoryItemId\"), 0) " +
                "FROM \"OrderItems\" " +
                "WHERE NOT EXISTS (SELECT 1 FROM \"sqlite_sequence\" WHERE \"name\" = 'InventoryItems'); " +
                "UPDATE \"sqlite_sequence\" SET \"seq\" = MAX(\"seq\", " +
                "(SELECT COALESCE(MAX(\"InventoryItemId\"), 0) FROM \"OrderItems\")) " +
                "WHERE \"name\" = 'InventoryItems';");

            migrationBuilder.CreateTable(
                name: "ShoppingCarts",
                columns: table => new
                {
                    CartId = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(nullable: false),
                    ExpiresAt = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShoppingCarts", x => x.CartId);
                    table.ForeignKey(
                        name: "FK_ShoppingCarts_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShoppingCartItems",
                columns: table => new
                {
                    CartItemId = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CartId = table.Column<int>(nullable: false),
                    InventoryItemId = table.Column<int>(nullable: false),
                    Quantity = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShoppingCartItems", x => x.CartItemId);
                    table.ForeignKey(
                        name: "FK_ShoppingCartItems_ShoppingCarts_CartId",
                        column: x => x.CartId,
                        principalTable: "ShoppingCarts",
                        principalColumn: "CartId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShoppingCartItems_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UserId",
                table: "Orders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId",
                table: "OrderItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingCartItems_InventoryItemId",
                table: "ShoppingCartItems",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingCartItems_CartId_InventoryItemId",
                table: "ShoppingCartItems",
                columns: new[] { "CartId", "InventoryItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingCarts_UserId",
                table: "ShoppingCarts",
                column: "UserId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShoppingCartItems");

            migrationBuilder.DropTable(
                name: "ShoppingCarts");

            migrationBuilder.Sql(
                "CREATE TABLE \"InventoryItems_legacy\" (" +
                "\"ItemId\" INTEGER NOT NULL CONSTRAINT \"PK_InventoryItems\" PRIMARY KEY AUTOINCREMENT, " +
                "\"Name\" TEXT NOT NULL, " +
                "\"Quantity\" INTEGER NOT NULL, " +
                "\"Location\" TEXT NOT NULL, " +
                "\"OrderId\" INTEGER NULL, " +
                "CONSTRAINT \"FK_InventoryItems_Orders_OrderId\" FOREIGN KEY (\"OrderId\") " +
                "REFERENCES \"Orders\" (\"OrderId\") ON DELETE RESTRICT); " +
                "INSERT INTO \"InventoryItems_legacy\" (\"ItemId\", \"Name\", \"Quantity\", \"Location\") " +
                "SELECT \"ItemId\", \"Name\", \"Quantity\", \"Location\" FROM \"InventoryItems\"; " +
                "INSERT INTO \"InventoryItems_legacy\" (\"Name\", \"Quantity\", \"Location\", \"OrderId\") " +
                "SELECT \"Name\", \"Quantity\", \"Location\", \"OrderId\" FROM \"OrderItems\"; " +
                "DROP TABLE \"InventoryItems\"; " +
                "ALTER TABLE \"InventoryItems_legacy\" RENAME TO \"InventoryItems\";");

            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_OrderId",
                table: "InventoryItems",
                column: "OrderId");

            migrationBuilder.DropIndex(
                name: "IX_Orders_UserId",
                table: "Orders");
        }
    }
}
