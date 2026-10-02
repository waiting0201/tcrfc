using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <summary>
    /// S0-7h（2026-10-02）：<c>articles</c> 封面圖片欄位組補齊——<c>cover_width</c>／<c>cover_height</c>（int NULL）與
    /// <c>articles_i18n.cover_alt</c>（nvarchar(200) NULL）。三欄皆可為空、無預設值，既有資料列維持 NULL（前台無寬高時不輸出寬高屬性、無 Alt 時回退文章標題）。
    /// 🔴 每一步先查現況再動（冪等）：正式庫是用 <c>db/club-schema.sql</c> 建的（欄位可能已存在），本機庫與舊庫則沒有；兩種都要能安全套用。
    /// </summary>
    public partial class AlignSchemaG1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF COL_LENGTH(N'articles', N'cover_width') IS NULL ALTER TABLE [articles] ADD [cover_width] int NULL;");
            migrationBuilder.Sql("IF COL_LENGTH(N'articles', N'cover_height') IS NULL ALTER TABLE [articles] ADD [cover_height] int NULL;");
            migrationBuilder.Sql("IF COL_LENGTH(N'articles_i18n', N'cover_alt') IS NULL ALTER TABLE [articles_i18n] ADD [cover_alt] nvarchar(200) NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF COL_LENGTH(N'articles_i18n', N'cover_alt') IS NOT NULL ALTER TABLE [articles_i18n] DROP COLUMN [cover_alt];");
            migrationBuilder.Sql("IF COL_LENGTH(N'articles', N'cover_height') IS NOT NULL ALTER TABLE [articles] DROP COLUMN [cover_height];");
            migrationBuilder.Sql("IF COL_LENGTH(N'articles', N'cover_width') IS NOT NULL ALTER TABLE [articles] DROP COLUMN [cover_width];");
        }
    }
}
