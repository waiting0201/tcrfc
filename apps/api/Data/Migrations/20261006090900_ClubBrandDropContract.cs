using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <summary>
    /// <c>clubs</c> 品牌欄位刪除（收縮，contract）：<c>logo_light_key</c>／<c>logo_dark_key</c>／<c>favicon_key</c>／<c>brand_color</c>／<c>brand_secondary_color</c>
    /// （主站規劃書 v3.20：標誌、Favicon、品牌色由前台靜態資產與 CSS 定義，後台不設定；<c>og_image_*</c> 保留）。
    /// 🔴 <b>收縮型</b>：新版 api（EF 的 <c>Club</c> 已無這五個屬性）部署並驗證後才可套用（走 <c>production-db</c> 核准關卡）；
    /// 舊版 api 仍會 SELECT 這些欄位，先套用會 500。<c>Down</c> 只還原欄位形狀，不還原內容。
    /// SQL 與 <c>db/migrations/20261006_club-brand-drop_2-contract.sql</c> 同源（冪等）。
    /// </summary>
    public partial class ClubBrandDropContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'clubs', N'logo_light_key') IS NOT NULL EXEC(N'ALTER TABLE clubs DROP COLUMN logo_light_key;');
IF COL_LENGTH(N'clubs', N'logo_dark_key') IS NOT NULL EXEC(N'ALTER TABLE clubs DROP COLUMN logo_dark_key;');
IF COL_LENGTH(N'clubs', N'favicon_key') IS NOT NULL EXEC(N'ALTER TABLE clubs DROP COLUMN favicon_key;');
IF COL_LENGTH(N'clubs', N'brand_color') IS NOT NULL EXEC(N'ALTER TABLE clubs DROP COLUMN brand_color;');
IF COL_LENGTH(N'clubs', N'brand_secondary_color') IS NOT NULL EXEC(N'ALTER TABLE clubs DROP COLUMN brand_secondary_color;');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'clubs', N'logo_light_key') IS NULL ALTER TABLE [clubs] ADD [logo_light_key] nvarchar(255) NULL;
IF COL_LENGTH(N'clubs', N'logo_dark_key') IS NULL ALTER TABLE [clubs] ADD [logo_dark_key] nvarchar(255) NULL;
IF COL_LENGTH(N'clubs', N'favicon_key') IS NULL ALTER TABLE [clubs] ADD [favicon_key] nvarchar(255) NULL;
IF COL_LENGTH(N'clubs', N'brand_color') IS NULL ALTER TABLE [clubs] ADD [brand_color] nvarchar(16) NULL;
IF COL_LENGTH(N'clubs', N'brand_secondary_color') IS NULL ALTER TABLE [clubs] ADD [brand_secondary_color] nvarchar(16) NULL;");
        }
    }
}
