using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlignSchemaE1a : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE proposals SET status = 'draft' WHERE status IS NULL;");
            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "proposals",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "draft",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldNullable: true)
                .Annotation("Relational:DefaultConstraintName", "DF_proposals_status");

            migrationBuilder.AddColumn<string>(
                name: "content",
                table: "partners_i18n",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "image_alt",
                table: "milestones_i18n",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "image_height",
                table: "milestones",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "image_key",
                table: "milestones",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "image_width",
                table: "milestones",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_visible",
                table: "milestones",
                type: "bit",
                nullable: false,
                defaultValue: true)
                .Annotation("Relational:DefaultConstraintName", "DF_milestones_is_visible");

            migrationBuilder.AddColumn<bool>(
                name: "is_pinned",
                table: "impact_records",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "impact_records",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "unit",
                table: "impact_metrics_i18n",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "charity_program_id",
                table: "impact_metrics",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "impact_metrics",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "proposal_id",
                table: "enquiries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_pinned",
                table: "charity_programs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "charity_programs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "charity_program_articles",
                columns: table => new
                {
                    charity_program_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    article_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    sort_order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_charity_program_articles", x => new { x.charity_program_id, x.article_id });
                    table.ForeignKey(
                        name: "FK_charity_program_articles_article",
                        column: x => x.article_id,
                        principalTable: "articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_charity_program_articles_program",
                        column: x => x.charity_program_id,
                        principalTable: "charity_programs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "charity_program_partners",
                columns: table => new
                {
                    charity_program_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    partner_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_charity_program_partners", x => new { x.charity_program_id, x.partner_id });
                    table.ForeignKey(
                        name: "FK_charity_program_partners_partner",
                        column: x => x.partner_id,
                        principalTable: "partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_charity_program_partners_program",
                        column: x => x.charity_program_id,
                        principalTable: "charity_programs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "charity_program_sponsors",
                columns: table => new
                {
                    charity_program_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    sponsor_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_charity_program_sponsors", x => new { x.charity_program_id, x.sponsor_id });
                    table.ForeignKey(
                        name: "FK_charity_program_sponsors_program",
                        column: x => x.charity_program_id,
                        principalTable: "charity_programs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_charity_program_sponsors_sponsor",
                        column: x => x.sponsor_id,
                        principalTable: "sponsors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sponsor_activations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    row_seq = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    club_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    sponsor_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    happened_on = table.Column<DateOnly>(type: "date", nullable: true),
                    sort_order = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sponsor_activations", x => x.id)
                        .Annotation("SqlServer:Clustered", false);
                    table.ForeignKey(
                        name: "FK_sponsor_activations_club",
                        column: x => x.club_id,
                        principalTable: "clubs",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_sponsor_activations_created_by",
                        column: x => x.created_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_sponsor_activations_sponsor",
                        column: x => x.sponsor_id,
                        principalTable: "sponsors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sponsor_activations_updated_by",
                        column: x => x.updated_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "sponsor_articles",
                columns: table => new
                {
                    sponsor_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    article_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    sort_order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sponsor_articles", x => new { x.sponsor_id, x.article_id });
                    table.ForeignKey(
                        name: "FK_sponsor_articles_article",
                        column: x => x.article_id,
                        principalTable: "articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sponsor_articles_sponsor",
                        column: x => x.sponsor_id,
                        principalTable: "sponsors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sponsor_activation_images",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    row_seq = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    sponsor_activation_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    image_key = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    image_width = table.Column<int>(type: "int", nullable: true),
                    image_height = table.Column<int>(type: "int", nullable: true),
                    sort_order = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sponsor_activation_images", x => x.id)
                        .Annotation("SqlServer:Clustered", false);
                    table.ForeignKey(
                        name: "FK_sponsor_activation_images_act",
                        column: x => x.sponsor_activation_id,
                        principalTable: "sponsor_activations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sponsor_activation_images_created_by",
                        column: x => x.created_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_sponsor_activation_images_updated_by",
                        column: x => x.updated_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "sponsor_activations_i18n",
                columns: table => new
                {
                    sponsor_activation_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    locale = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    result_summary = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sponsor_activations_i18n", x => new { x.sponsor_activation_id, x.locale });
                    table.ForeignKey(
                        name: "FK_sponsor_activations_i18n_act",
                        column: x => x.sponsor_activation_id,
                        principalTable: "sponsor_activations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_impact_record_images_record",
                table: "impact_record_images",
                columns: new[] { "impact_record_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_enquiries_proposal",
                table: "enquiries",
                column: "proposal_id");

            migrationBuilder.CreateIndex(
                name: "IX_charity_program_images_program",
                table: "charity_program_images",
                columns: new[] { "charity_program_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_sponsor_activation_images_activation",
                table: "sponsor_activation_images",
                columns: new[] { "sponsor_activation_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "UQ_sponsor_activation_images_row_seq",
                table: "sponsor_activation_images",
                column: "row_seq",
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "IX_sponsor_activations_sponsor",
                table: "sponsor_activations",
                columns: new[] { "sponsor_id", "happened_on" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "UQ_sponsor_activations_row_seq",
                table: "sponsor_activations",
                column: "row_seq",
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.AddForeignKey(
                name: "FK_enquiries_proposal",
                table: "enquiries",
                column: "proposal_id",
                principalTable: "proposals",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // 值域約束（EF 模型不表達 CHECK，比照 AlignSchemaS19Programs 用 Sql）。proposals／press_resources
            // 套用前皆為 0 筆（後台 E3／B6 本輪才第一次接上 API），純 DDL，不需要 DML 轉態。
            migrationBuilder.Sql(
                "ALTER TABLE proposals ADD CONSTRAINT CK_proposals_status CHECK (status IN ('draft','published'));");
            migrationBuilder.Sql(
                "ALTER TABLE press_resources ADD CONSTRAINT CK_press_resources_resource_type CHECK (resource_type IN ('press_release','brand_kit','hires_image'));");
            // enquiries.proposal_id 的外鍵 ON DELETE SET NULL 與 sponsor／charity 關聯表外鍵由上方 CreateTable／AddForeignKey 產生。
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE press_resources DROP CONSTRAINT CK_press_resources_resource_type;");
            migrationBuilder.Sql("ALTER TABLE proposals DROP CONSTRAINT CK_proposals_status;");

            migrationBuilder.DropForeignKey(
                name: "FK_enquiries_proposal",
                table: "enquiries");

            migrationBuilder.DropTable(
                name: "charity_program_articles");

            migrationBuilder.DropTable(
                name: "charity_program_partners");

            migrationBuilder.DropTable(
                name: "charity_program_sponsors");

            migrationBuilder.DropTable(
                name: "sponsor_activation_images");

            migrationBuilder.DropTable(
                name: "sponsor_activations_i18n");

            migrationBuilder.DropTable(
                name: "sponsor_articles");

            migrationBuilder.DropTable(
                name: "sponsor_activations");

            migrationBuilder.DropIndex(
                name: "IX_impact_record_images_record",
                table: "impact_record_images");

            migrationBuilder.DropIndex(
                name: "IX_enquiries_proposal",
                table: "enquiries");

            migrationBuilder.DropIndex(
                name: "IX_charity_program_images_program",
                table: "charity_program_images");

            migrationBuilder.DropColumn(
                name: "content",
                table: "partners_i18n");

            migrationBuilder.DropColumn(
                name: "image_alt",
                table: "milestones_i18n");

            migrationBuilder.DropColumn(
                name: "image_height",
                table: "milestones");

            migrationBuilder.DropColumn(
                name: "image_key",
                table: "milestones");

            migrationBuilder.DropColumn(
                name: "image_width",
                table: "milestones");

            migrationBuilder.DropColumn(
                name: "is_visible",
                table: "milestones")
                .Annotation("Relational:DefaultConstraintName", "DF_milestones_is_visible");

            migrationBuilder.DropColumn(
                name: "is_pinned",
                table: "impact_records");

            migrationBuilder.DropColumn(
                name: "sort_order",
                table: "impact_records");

            migrationBuilder.DropColumn(
                name: "unit",
                table: "impact_metrics_i18n");

            migrationBuilder.DropColumn(
                name: "sort_order",
                table: "impact_metrics");

            migrationBuilder.DropColumn(
                name: "proposal_id",
                table: "enquiries");

            migrationBuilder.DropColumn(
                name: "is_pinned",
                table: "charity_programs");

            migrationBuilder.DropColumn(
                name: "sort_order",
                table: "charity_programs");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "proposals",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldDefaultValue: "draft")
                .OldAnnotation("Relational:DefaultConstraintName", "DF_proposals_status");

            migrationBuilder.AlterColumn<Guid>(
                name: "charity_program_id",
                table: "impact_metrics",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
