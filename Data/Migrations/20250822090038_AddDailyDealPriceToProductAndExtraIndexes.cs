using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceMudblazorWebApp.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyDealPriceToProductAndExtraIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "ProductViews",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DailyDealPrice",
                table: "Products",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tags_Name",
                table: "Tags",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductViews_UserId",
                table: "ProductViews",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductTags_ProductId_TagId",
                table: "ProductTags",
                columns: new[] { "ProductId", "TagId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ProductId_OrderId",
                table: "OrderItems",
                columns: new[] { "ProductId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_FlashSales_StartAt_EndAt",
                table: "FlashSales",
                columns: new[] { "StartAt", "EndAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FlashSaleItems_FlashSaleId_ProductId",
                table: "FlashSaleItems",
                columns: new[] { "FlashSaleId", "ProductId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductViews_AspNetUsers_UserId",
                table: "ProductViews",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductViews_AspNetUsers_UserId",
                table: "ProductViews");

            migrationBuilder.DropIndex(
                name: "IX_Tags_Name",
                table: "Tags");

            migrationBuilder.DropIndex(
                name: "IX_ProductViews_UserId",
                table: "ProductViews");

            migrationBuilder.DropIndex(
                name: "IX_ProductTags_ProductId_TagId",
                table: "ProductTags");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_ProductId_OrderId",
                table: "OrderItems");

            migrationBuilder.DropIndex(
                name: "IX_FlashSales_StartAt_EndAt",
                table: "FlashSales");

            migrationBuilder.DropIndex(
                name: "IX_FlashSaleItems_FlashSaleId_ProductId",
                table: "FlashSaleItems");

            migrationBuilder.DropColumn(
                name: "DailyDealPrice",
                table: "Products");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "ProductViews",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);
        }
    }
}
