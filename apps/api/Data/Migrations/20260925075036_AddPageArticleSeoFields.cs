using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPageArticleSeoFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "seo_keywords",
                table: "pages_i18n",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "canonical_path",
                table: "pages",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_excluded_from_sitemap",
                table: "pages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_noindex",
                table: "pages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "seo_keywords",
                table: "articles_i18n",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "canonical_path",
                table: "articles",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_excluded_from_sitemap",
                table: "articles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_noindex",
                table: "articles",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "seo_keywords",
                table: "pages_i18n");

            migrationBuilder.DropColumn(
                name: "canonical_path",
                table: "pages");

            migrationBuilder.DropColumn(
                name: "is_excluded_from_sitemap",
                table: "pages");

            migrationBuilder.DropColumn(
                name: "is_noindex",
                table: "pages");

            migrationBuilder.DropColumn(
                name: "seo_keywords",
                table: "articles_i18n");

            migrationBuilder.DropColumn(
                name: "canonical_path",
                table: "articles");

            migrationBuilder.DropColumn(
                name: "is_excluded_from_sitemap",
                table: "articles");

            migrationBuilder.DropColumn(
                name: "is_noindex",
                table: "articles");
        }
    }
}
