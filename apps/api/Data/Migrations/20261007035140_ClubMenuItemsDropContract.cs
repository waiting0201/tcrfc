using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <summary>
    /// <c>menu_items</c>／<c>menu_items_i18n</c> 刪表（收縮，contract）：主站規劃書 v3.22——前台選單（主選單／Mega Menu／Footer）固定在前台版型，後台不提供選單管理（S2-24）。
    /// 🔴 <b>收縮型</b>：新版 api（EF 已無 <c>MenuItem</c> 實體、無 <c>/menus</c> 端點）部署並驗證後才可套用（走 <c>production-db</c> 核准關卡）；
    /// 舊版 api 仍會查這兩張表，先套用會 500。同一支遷移並刪除四個權限碼（<c>site.menu.view／update</c>、<c>content.page.create／delete</c>，<c>role_permissions</c> 由外鍵連動刪除）。<c>Down</c> 只還原表結構，不還原內容與權限碼。
    /// SQL 與 <c>db/migrations/20261007_menu-items-drop_2-contract.sql</c> 同源（冪等）。
    /// </summary>
    public partial class ClubMenuItemsDropContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'menu_items_i18n', N'U') IS NOT NULL DROP TABLE menu_items_i18n;
IF OBJECT_ID(N'menu_items', N'U') IS NOT NULL DROP TABLE menu_items;
DELETE FROM permissions WHERE code IN (N'site.menu.view', N'site.menu.update', N'content.page.create', N'content.page.delete');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "menu_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    club_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    parent_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    is_external = table.Column<bool>(type: "bit", nullable: false),
                    menu_location = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    row_seq = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    sort_order = table.Column<int>(type: "int", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_menu_items", x => x.id)
                        .Annotation("SqlServer:Clustered", false);
                    table.ForeignKey(
                        name: "FK_menu_items_club",
                        column: x => x.club_id,
                        principalTable: "clubs",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_menu_items_created_by",
                        column: x => x.created_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_menu_items_parent",
                        column: x => x.parent_id,
                        principalTable: "menu_items",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_menu_items_updated_by",
                        column: x => x.updated_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "menu_items_i18n",
                columns: table => new
                {
                    menu_item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    locale = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    label = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_menu_items_i18n", x => new { x.menu_item_id, x.locale });
                    table.ForeignKey(
                        name: "FK_menu_items_i18n_item",
                        column: x => x.menu_item_id,
                        principalTable: "menu_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_menu_items_row_seq",
                table: "menu_items",
                column: "row_seq",
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "IX_menu_items_i18n_locale",
                table: "menu_items_i18n",
                column: "locale");
        }
    }
}
