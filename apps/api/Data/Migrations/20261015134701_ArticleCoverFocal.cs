using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <summary>
    /// 新聞封面焦點（展開，expand；主站規劃書 v3.28）：<c>articles.cover_focal_x</c>／<c>cover_focal_y</c>（decimal(5,2) NULL，百分比）
    /// 與具名 CHECK <c>CK_articles_cover_focal</c>（兩欄同為 NULL＝置中，或皆在 0–100）。既有資料列維持 NULL。
    /// SQL 與 <c>db/migrations/20261015_article-cover-focal_1-expand.sql</c> 同源（冪等，docs/20 §5 冪等契約）。必須先 migrate 再 deploy（E-289）。
    /// </summary>
    public partial class ArticleCoverFocal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'articles', N'cover_focal_x') IS NULL EXEC(N'ALTER TABLE articles ADD cover_focal_x decimal(5,2) NULL;');
IF COL_LENGTH(N'articles', N'cover_focal_y') IS NULL EXEC(N'ALTER TABLE articles ADD cover_focal_y decimal(5,2) NULL;');
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_articles_cover_focal' AND parent_object_id = OBJECT_ID(N'articles'))
    EXEC(N'ALTER TABLE articles ADD CONSTRAINT CK_articles_cover_focal CHECK ((cover_focal_x IS NULL AND cover_focal_y IS NULL) OR (cover_focal_x BETWEEN 0 AND 100 AND cover_focal_y BETWEEN 0 AND 100));');
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_articles_cover_focal",
                table: "articles");

            migrationBuilder.DropColumn(
                name: "cover_focal_x",
                table: "articles");

            migrationBuilder.DropColumn(
                name: "cover_focal_y",
                table: "articles");
        }
    }
}
