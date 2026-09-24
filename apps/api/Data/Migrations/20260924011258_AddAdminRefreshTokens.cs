using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminRefreshTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admin_refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    row_seq = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    admin_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    token_hash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    issued_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    expires_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    revoked_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    replaced_by_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_refresh_tokens", x => x.id)
                        .Annotation("SqlServer:Clustered", false);
                    table.ForeignKey(
                        name: "FK_admin_refresh_tokens_replaced",
                        column: x => x.replaced_by_id,
                        principalTable: "admin_refresh_tokens",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_admin_refresh_tokens_user",
                        column: x => x.admin_user_id,
                        principalTable: "admin_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // 🔴 S0-7k（docs/18-work-errors.md E-45）：EF 的 ForeignKeyIndexConvention 原本會在這裡
            // 自動多產生 CREATE INDEX IX_admin_refresh_tokens_replaced_by_id ON admin_refresh_tokens
            // (replaced_by_id)，但 db/club-schema.sql（真實來源）沒有這個索引，使用者裁決依綱要為準，
            // 已在 Data/ClubDbContextCustomizations.cs 用 Fluent API 抑制。這裡手動拿掉對應那一段
            // CreateIndex，讓 migration 檔跟綱要與 Fluent API 設定三邊一致。

            migrationBuilder.CreateIndex(
                name: "IX_admin_refresh_tokens_user",
                table: "admin_refresh_tokens",
                column: "admin_user_id");

            migrationBuilder.CreateIndex(
                name: "UQ_admin_refresh_tokens_row_seq",
                table: "admin_refresh_tokens",
                column: "row_seq",
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "UQ_admin_refresh_tokens_token_hash",
                table: "admin_refresh_tokens",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_refresh_tokens");
        }
    }
}
