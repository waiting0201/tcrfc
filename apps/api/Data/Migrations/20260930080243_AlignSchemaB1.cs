using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlignSchemaB1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 既有列（本機／正式庫在 B1 之前建立的資料）：把即將收斂為 NOT NULL 的狀態欄補上預設值。
            migrationBuilder.Sql("UPDATE partner_stores SET status = 'draft' WHERE status IS NULL;");
            migrationBuilder.Sql("UPDATE partner_stores SET applicable_tier = 'all' WHERE applicable_tier IS NULL;");
            migrationBuilder.Sql("UPDATE memberships SET status = 'active' WHERE status IS NULL;");
            migrationBuilder.Sql("UPDATE membership_plans SET status = 'draft' WHERE status IS NULL;");
            migrationBuilder.Sql("UPDATE member_cards SET status = 'active' WHERE status IS NULL;");
            migrationBuilder.Sql("UPDATE jersey_issues SET status = 'pending' WHERE status IS NULL;");
            // memberships.status 在複合索引內，變更可否為空前先拿掉索引，結尾重建（同 db/club-schema.sql）。
            migrationBuilder.Sql("DROP INDEX IX_memberships_club_status_end ON memberships;");

            migrationBuilder.AddColumn<int>(
                name: "enrolled_count",
                table: "trials",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "trials",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "開放");

            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "partner_stores_i18n",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "partner_stores",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "draft",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "applicable_tier",
                table: "partner_stores",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "all",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "map_url",
                table: "partner_stores",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "region",
                table: "partner_stores",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "memberships",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "active",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_adjust_reason",
                table: "memberships",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_adjusted_at",
                table: "memberships",
                type: "datetime2(3)",
                precision: 3,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "membership_plan_id",
                table: "memberships",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "membership_plans",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "draft",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ends_on",
                table: "membership_plans",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "starts_on",
                table: "membership_plans",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "membership_benefits_i18n",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "membership_benefits_i18n",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "membership_benefits",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "draft");

            migrationBuilder.AddColumn<DateTime>(
                name: "email_verified_at",
                table: "members",
                type: "datetime2(3)",
                precision: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "internal_note",
                table: "members",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_login_at",
                table: "members",
                type: "datetime2(3)",
                precision: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "locale",
                table: "members",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "merged_into_member_id",
                table: "members",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "member_cards",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "active",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "revoked_at",
                table: "member_cards",
                type: "datetime2(3)",
                precision: 3,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "jersey_issues",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "pending",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "membership_id",
                table: "jersey_issues",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "received_on",
                table: "jersey_issues",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "calendar_feed_fetches",
                columns: table => new
                {
                    club_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    feed_key = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    fetched_on = table.Column<DateOnly>(type: "date", nullable: false),
                    client_hash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calendar_feed_fetches", x => new { x.club_id, x.feed_key, x.fetched_on, x.client_hash });
                    table.ForeignKey(
                        name: "FK_calendar_feed_fetches_club",
                        column: x => x.club_id,
                        principalTable: "clubs",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "calendar_team_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    row_seq = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    club_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    team_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    colour = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    sort_order = table.Column<int>(type: "int", nullable: true),
                    is_public = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calendar_team_settings", x => x.id)
                        .Annotation("SqlServer:Clustered", false);
                    table.ForeignKey(
                        name: "FK_calendar_team_settings_club",
                        column: x => x.club_id,
                        principalTable: "clubs",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_calendar_team_settings_created_by",
                        column: x => x.created_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_calendar_team_settings_team",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_calendar_team_settings_updated_by",
                        column: x => x.updated_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "trials_i18n",
                columns: table => new
                {
                    trial_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    locale = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    audience = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trials_i18n", x => new { x.trial_id, x.locale });
                    table.ForeignKey(
                        name: "FK_trials_i18n_trial",
                        column: x => x.trial_id,
                        principalTable: "trials",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "calendar_team_settings_i18n",
                columns: table => new
                {
                    calendar_team_setting_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    locale = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    display_name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calendar_team_settings_i18n", x => new { x.calendar_team_setting_id, x.locale });
                    table.ForeignKey(
                        name: "FK_calendar_team_settings_i18n_setting",
                        column: x => x.calendar_team_setting_id,
                        principalTable: "calendar_team_settings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_trials_club_on",
                table: "trials",
                columns: new[] { "club_id", "trial_on" });

            migrationBuilder.CreateIndex(
                name: "IX_members_phone",
                table: "members",
                column: "phone");

            migrationBuilder.CreateIndex(
                name: "IX_jersey_issues_club_status",
                table: "jersey_issues",
                columns: new[] { "club_id", "status" });

            migrationBuilder.CreateIndex(
                name: "UQ_calendar_team_settings_row_seq",
                table: "calendar_team_settings",
                column: "row_seq",
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "UQ_calendar_team_settings_team",
                table: "calendar_team_settings",
                column: "team_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_calendar_team_settings_i18n_locale",
                table: "calendar_team_settings_i18n",
                column: "locale");

            migrationBuilder.CreateIndex(
                name: "IX_trials_i18n_locale",
                table: "trials_i18n",
                column: "locale");

            migrationBuilder.AddForeignKey(
                name: "FK_jersey_issues_membership",
                table: "jersey_issues",
                column: "membership_id",
                principalTable: "memberships",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_members_merged_into",
                table: "members",
                column: "merged_into_member_id",
                principalTable: "members",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_memberships_plan",
                table: "memberships",
                column: "membership_plan_id",
                principalTable: "membership_plans",
                principalColumn: "id");

            migrationBuilder.Sql("CREATE INDEX IX_memberships_club_status_end ON memberships (club_id, status, membership_end_on);");

            // 值域約束（EF 模型不表達 CHECK，與 db/club-schema.sql 逐條一致）。
            migrationBuilder.Sql("ALTER TABLE trials ADD CONSTRAINT CK_trials_status CHECK (status IN (N'開放',N'額滿',N'候補',N'已結束'));");
            migrationBuilder.Sql("ALTER TABLE members ADD CONSTRAINT CK_members_locale CHECK (locale IN (N'zh-Hant',N'en'));");
            migrationBuilder.Sql("ALTER TABLE memberships ADD CONSTRAINT CK_memberships_status CHECK (status IN ('pending','active','expired','cancelled'));");
            migrationBuilder.Sql("ALTER TABLE member_cards ADD CONSTRAINT CK_member_cards_status CHECK (status IN ('active','revoked'));");
            migrationBuilder.Sql("ALTER TABLE membership_plans ADD CONSTRAINT CK_membership_plans_status CHECK (status IN ('draft','published'));");
            migrationBuilder.Sql("ALTER TABLE membership_benefits ADD CONSTRAINT CK_membership_benefits_group CHECK (benefit_group IN ('member_card','store_discount','jersey','event'));");
            migrationBuilder.Sql("ALTER TABLE membership_benefits ADD CONSTRAINT CK_membership_benefits_status CHECK (status IN ('draft','published'));");
            migrationBuilder.Sql("ALTER TABLE jersey_issues ADD CONSTRAINT CK_jersey_issues_status CHECK (status IN ('pending','shipped','received'));");
            migrationBuilder.Sql("ALTER TABLE jersey_issues ADD CONSTRAINT CK_jersey_issues_delivery_method CHECK (delivery_method IN ('ship','pickup'));");
            migrationBuilder.Sql("ALTER TABLE partner_stores ADD CONSTRAINT CK_partner_stores_applicable_tier CHECK (applicable_tier IN ('all','fan_club'));");
            migrationBuilder.Sql("ALTER TABLE partner_stores ADD CONSTRAINT CK_partner_stores_status CHECK (status IN ('draft','published'));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE partner_stores DROP CONSTRAINT CK_partner_stores_status;");
            migrationBuilder.Sql("ALTER TABLE partner_stores DROP CONSTRAINT CK_partner_stores_applicable_tier;");
            migrationBuilder.Sql("ALTER TABLE jersey_issues DROP CONSTRAINT CK_jersey_issues_delivery_method;");
            migrationBuilder.Sql("ALTER TABLE jersey_issues DROP CONSTRAINT CK_jersey_issues_status;");
            migrationBuilder.Sql("ALTER TABLE membership_benefits DROP CONSTRAINT CK_membership_benefits_status;");
            migrationBuilder.Sql("ALTER TABLE membership_benefits DROP CONSTRAINT CK_membership_benefits_group;");
            migrationBuilder.Sql("ALTER TABLE membership_plans DROP CONSTRAINT CK_membership_plans_status;");
            migrationBuilder.Sql("ALTER TABLE member_cards DROP CONSTRAINT CK_member_cards_status;");
            migrationBuilder.Sql("ALTER TABLE memberships DROP CONSTRAINT CK_memberships_status;");
            migrationBuilder.Sql("ALTER TABLE members DROP CONSTRAINT CK_members_locale;");
            migrationBuilder.Sql("ALTER TABLE trials DROP CONSTRAINT CK_trials_status;");

            migrationBuilder.DropForeignKey(
                name: "FK_jersey_issues_membership",
                table: "jersey_issues");

            migrationBuilder.DropForeignKey(
                name: "FK_members_merged_into",
                table: "members");

            migrationBuilder.DropForeignKey(
                name: "FK_memberships_plan",
                table: "memberships");

            migrationBuilder.DropTable(
                name: "calendar_feed_fetches");

            migrationBuilder.DropTable(
                name: "calendar_team_settings_i18n");

            migrationBuilder.DropTable(
                name: "trials_i18n");

            migrationBuilder.DropTable(
                name: "calendar_team_settings");

            migrationBuilder.DropIndex(
                name: "IX_trials_club_on",
                table: "trials");

            migrationBuilder.DropIndex(
                name: "IX_members_phone",
                table: "members");

            migrationBuilder.DropIndex(
                name: "IX_jersey_issues_club_status",
                table: "jersey_issues");

            migrationBuilder.DropColumn(
                name: "enrolled_count",
                table: "trials");

            migrationBuilder.DropColumn(
                name: "status",
                table: "trials");

            migrationBuilder.DropColumn(
                name: "address",
                table: "partner_stores_i18n");

            migrationBuilder.DropColumn(
                name: "map_url",
                table: "partner_stores");

            migrationBuilder.DropColumn(
                name: "region",
                table: "partner_stores");

            migrationBuilder.DropColumn(
                name: "last_adjust_reason",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "last_adjusted_at",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "membership_plan_id",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "ends_on",
                table: "membership_plans");

            migrationBuilder.DropColumn(
                name: "starts_on",
                table: "membership_plans");

            migrationBuilder.DropColumn(
                name: "description",
                table: "membership_benefits_i18n");

            migrationBuilder.DropColumn(
                name: "name",
                table: "membership_benefits_i18n");

            migrationBuilder.DropColumn(
                name: "status",
                table: "membership_benefits");

            migrationBuilder.DropColumn(
                name: "email_verified_at",
                table: "members");

            migrationBuilder.DropColumn(
                name: "internal_note",
                table: "members");

            migrationBuilder.DropColumn(
                name: "last_login_at",
                table: "members");

            migrationBuilder.DropColumn(
                name: "locale",
                table: "members");

            migrationBuilder.DropColumn(
                name: "merged_into_member_id",
                table: "members");

            migrationBuilder.DropColumn(
                name: "revoked_at",
                table: "member_cards");

            migrationBuilder.DropColumn(
                name: "membership_id",
                table: "jersey_issues");

            migrationBuilder.DropColumn(
                name: "received_on",
                table: "jersey_issues");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "partner_stores",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldDefaultValue: "draft");

            migrationBuilder.AlterColumn<string>(
                name: "applicable_tier",
                table: "partner_stores",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldDefaultValue: "all");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "memberships",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldDefaultValue: "active");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "membership_plans",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldDefaultValue: "draft");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "member_cards",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldDefaultValue: "active");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "jersey_issues",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldDefaultValue: "pending");
        }
    }
}
