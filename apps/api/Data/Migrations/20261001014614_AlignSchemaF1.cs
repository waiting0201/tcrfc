using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlignSchemaF1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "carrier_id_encrypted",
                table: "store_invoices",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "buyer_email",
                table: "orders",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "idempotency_key",
                table: "orders",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_url",
                table: "orders",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "request_fingerprint",
                table: "orders",
                type: "char(64)",
                unicode: false,
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UQ_orders_club_idempotency",
                table: "orders",
                columns: new[] { "club_id", "idempotency_key" },
                unique: true,
                filter: "[idempotency_key] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_carts_anonymous_token",
                table: "carts",
                column: "anonymous_token",
                unique: true,
                filter: "[anonymous_token] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_carts_club_member",
                table: "carts",
                columns: new[] { "club_id", "member_id" },
                unique: true,
                filter: "[member_id] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_orders_club_idempotency",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "UQ_carts_anonymous_token",
                table: "carts");

            migrationBuilder.DropIndex(
                name: "UQ_carts_club_member",
                table: "carts");

            migrationBuilder.DropColumn(
                name: "buyer_email",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "idempotency_key",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "payment_url",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "request_fingerprint",
                table: "orders");

            migrationBuilder.AlterColumn<string>(
                name: "carrier_id_encrypted",
                table: "store_invoices",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }
    }
}
