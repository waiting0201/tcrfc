using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFormFieldsI18n : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "form_fields_i18n",
                columns: table => new
                {
                    form_field_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    locale = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    label = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    options_json = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_form_fields_i18n", x => new { x.form_field_id, x.locale });
                    table.ForeignKey(
                        name: "FK_form_fields_i18n_field",
                        column: x => x.form_field_id,
                        principalTable: "form_fields",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_form_fields_i18n_locale",
                table: "form_fields_i18n",
                column: "locale");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "form_fields_i18n");
        }
    }
}
