using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <summary>
    /// <c>fan_events_i18n.cover_alt</c>／<c>press_resources_i18n.cover_alt</c>（展開，expand）：封面圖片替代文字（主站規劃書 §4.0 圖片欄位組）。
    /// 純新增可為空欄位，舊版 api 不受影響，可隨新版 api 一起上。SQL 與 <c>db/migrations/20261007_cover-alt_1-expand.sql</c> 同源（冪等）。
    /// </summary>
    public partial class ClubCoverAltExpand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'fan_events_i18n', N'cover_alt') IS NULL EXEC(N'ALTER TABLE fan_events_i18n ADD cover_alt nvarchar(200) NULL;');
IF COL_LENGTH(N'press_resources_i18n', N'cover_alt') IS NULL EXEC(N'ALTER TABLE press_resources_i18n ADD cover_alt nvarchar(200) NULL;');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cover_alt",
                table: "press_resources_i18n");

            migrationBuilder.DropColumn(
                name: "cover_alt",
                table: "fan_events_i18n");
        }
    }
}
