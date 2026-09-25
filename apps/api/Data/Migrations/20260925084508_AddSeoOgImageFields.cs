using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSeoOgImageFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "og_image_alt",
                table: "pages_i18n",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "og_image_height",
                table: "pages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "og_image_key",
                table: "pages",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "og_image_width",
                table: "pages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "og_image_height",
                table: "clubs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "og_image_width",
                table: "clubs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "og_image_alt",
                table: "articles_i18n",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "og_image_height",
                table: "articles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "og_image_key",
                table: "articles",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "og_image_width",
                table: "articles",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "og_image_alt",
                table: "pages_i18n");

            migrationBuilder.DropColumn(
                name: "og_image_height",
                table: "pages");

            migrationBuilder.DropColumn(
                name: "og_image_key",
                table: "pages");

            migrationBuilder.DropColumn(
                name: "og_image_width",
                table: "pages");

            migrationBuilder.DropColumn(
                name: "og_image_height",
                table: "clubs");

            migrationBuilder.DropColumn(
                name: "og_image_width",
                table: "clubs");

            migrationBuilder.DropColumn(
                name: "og_image_alt",
                table: "articles_i18n");

            migrationBuilder.DropColumn(
                name: "og_image_height",
                table: "articles");

            migrationBuilder.DropColumn(
                name: "og_image_key",
                table: "articles");

            migrationBuilder.DropColumn(
                name: "og_image_width",
                table: "articles");
        }
    }
}
