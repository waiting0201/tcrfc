using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlignSchemaD1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 既有列（本機／正式庫在 D 批之前建立的名單）：把即將收斂為 NOT NULL 的狀態欄補上預設值。
            migrationBuilder.Sql("UPDATE newsletter_subscribers SET status = 'subscribed' WHERE status IS NULL OR status NOT IN ('subscribed','unsubscribed');");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "newsletter_subscribers",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "subscribed",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "unsubscribed_at",
                table: "newsletter_subscribers",
                type: "datetime2(3)",
                precision: 3,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ad_slots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    row_seq = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    slot_code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    surface = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false, defaultValue: "app"),
                    screen_code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    block_order = table.Column<int>(type: "int", nullable: true),
                    aspect_ratio = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    min_width = table.Column<int>(type: "int", nullable: true),
                    min_height = table.Column<int>(type: "int", nullable: true),
                    max_file_kb = table.Column<int>(type: "int", nullable: true),
                    allowed_formats = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    allow_video = table.Column<bool>(type: "bit", nullable: false),
                    session_impression_cap = table.Column<int>(type: "int", nullable: true),
                    rotation_cap = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    fallback_image_key = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    fallback_image_width = table.Column<int>(type: "int", nullable: true),
                    fallback_image_height = table.Column<int>(type: "int", nullable: true),
                    fallback_link = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ad_slots", x => x.id)
                        .Annotation("SqlServer:Clustered", false);
                    table.ForeignKey(
                        name: "FK_ad_slots_created_by",
                        column: x => x.created_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_ad_slots_updated_by",
                        column: x => x.updated_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "advertisers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    row_seq = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    tax_id = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    contact_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    contact_phone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    contact_email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    contract_note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    cooperation_start_on = table.Column<DateOnly>(type: "date", nullable: true),
                    cooperation_end_on = table.Column<DateOnly>(type: "date", nullable: true),
                    sponsor_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "negotiating"),
                    created_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_advertisers", x => x.id)
                        .Annotation("SqlServer:Clustered", false);
                    table.ForeignKey(
                        name: "FK_advertisers_created_by",
                        column: x => x.created_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_advertisers_sponsor",
                        column: x => x.sponsor_id,
                        principalTable: "sponsors",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_advertisers_updated_by",
                        column: x => x.updated_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "app_announcements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    row_seq = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    link_url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    starts_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    ends_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    audience_tier = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "all"),
                    audience_club_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    is_enabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_announcements", x => x.id)
                        .Annotation("SqlServer:Clustered", false);
                    table.ForeignKey(
                        name: "FK_app_announcements_club",
                        column: x => x.audience_club_id,
                        principalTable: "clubs",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_app_announcements_created_by",
                        column: x => x.created_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_app_announcements_updated_by",
                        column: x => x.updated_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "app_credentials",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    row_seq = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    label = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    external_ref = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    created_on = table.Column<DateOnly>(type: "date", nullable: false),
                    last_rotated_on = table.Column<DateOnly>(type: "date", nullable: true),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: true),
                    rotation_period_days = table.Column<int>(type: "int", nullable: true),
                    note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_credentials", x => x.id)
                        .Annotation("SqlServer:Clustered", false);
                    table.ForeignKey(
                        name: "FK_app_credentials_created_by",
                        column: x => x.created_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_app_credentials_updated_by",
                        column: x => x.updated_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "app_deep_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    row_seq = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    code = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                    app_link = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    web_url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    requires_login = table.Column<bool>(type: "bit", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    sort_order = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_deep_links", x => x.id)
                        .Annotation("SqlServer:Clustered", false);
                    table.ForeignKey(
                        name: "FK_app_deep_links_created_by",
                        column: x => x.created_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_app_deep_links_updated_by",
                        column: x => x.updated_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "app_devices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    row_seq = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    device_install_id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    platform = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    os_version = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    app_version = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    locale = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    push_token_encrypted = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    push_token_hash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: true),
                    push_token_status = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false, defaultValue: "none"),
                    push_permission = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "not_determined"),
                    member_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    first_seen_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    last_active_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    refresh_token_hash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: true),
                    refresh_token_expires_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    refresh_token_rotated_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    revoked_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_devices", x => x.id)
                        .Annotation("SqlServer:Clustered", false);
                    table.ForeignKey(
                        name: "FK_app_devices_member",
                        column: x => x.member_id,
                        principalTable: "members",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "app_diagnostic_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    row_seq = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    device_install_id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    platform = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    app_version = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    build_number = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    os_version = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    occurred_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    report_type = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    metric_value = table.Column<int>(type: "int", nullable: true),
                    summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    detail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "new"),
                    received_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_diagnostic_reports", x => x.id)
                        .Annotation("SqlServer:Clustered", false);
                    table.ForeignKey(
                        name: "FK_app_diagnostic_reports_updated_by",
                        column: x => x.updated_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "app_feature_flags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    row_seq = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    flag_key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    is_enabled = table.Column<bool>(type: "bit", nullable: false),
                    string_value = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    platform = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false, defaultValue: "all"),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_feature_flags", x => x.id)
                        .Annotation("SqlServer:Clustered", false);
                    table.ForeignKey(
                        name: "FK_app_feature_flags_created_by",
                        column: x => x.created_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_app_feature_flags_updated_by",
                        column: x => x.updated_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "app_releases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    row_seq = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    platform = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    version = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    build_number = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    released_on = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "testing"),
                    is_min_supported = table.Column<bool>(type: "bit", nullable: false),
                    is_recommended = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_releases", x => x.id)
                        .Annotation("SqlServer:Clustered", false);
                    table.ForeignKey(
                        name: "FK_app_releases_created_by",
                        column: x => x.created_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_app_releases_updated_by",
                        column: x => x.updated_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "app_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    row_seq = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    setting_key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    setting_value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_settings", x => x.id)
                        .Annotation("SqlServer:Clustered", false);
                    table.ForeignKey(
                        name: "FK_app_settings_updated_by",
                        column: x => x.updated_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "push_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    row_seq = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "announcement"),
                    image_key = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    image_width = table.Column<int>(type: "int", nullable: true),
                    image_height = table.Column<int>(type: "int", nullable: true),
                    deep_link = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    audience_tier = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "all"),
                    audience_club_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    audience_team_codes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    scheduled_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "draft"),
                    reject_note = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    sent_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    audience_estimate = table.Column<int>(type: "int", nullable: true),
                    sent_count = table.Column<int>(type: "int", nullable: false),
                    delivered_count = table.Column<int>(type: "int", nullable: false),
                    failed_count = table.Column<int>(type: "int", nullable: false),
                    opened_count = table.Column<int>(type: "int", nullable: false),
                    send_cursor = table.Column<long>(type: "bigint", nullable: false),
                    failure_message = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_push_messages", x => x.id)
                        .Annotation("SqlServer:Clustered", false);
                    table.ForeignKey(
                        name: "FK_push_messages_club",
                        column: x => x.audience_club_id,
                        principalTable: "clubs",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_push_messages_created_by",
                        column: x => x.created_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_push_messages_reviewed_by",
                        column: x => x.reviewed_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_push_messages_updated_by",
                        column: x => x.updated_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "ad_slots_i18n",
                columns: table => new
                {
                    ad_slot_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    locale = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    fallback_alt = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ad_slots_i18n", x => new { x.ad_slot_id, x.locale });
                    table.ForeignKey(
                        name: "FK_ad_slots_i18n_slot",
                        column: x => x.ad_slot_id,
                        principalTable: "ad_slots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ad_campaigns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    row_seq = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    advertiser_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    slot_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    starts_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    ends_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    weight = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    daily_impression_cap = table.Column<int>(type: "int", nullable: true),
                    per_device_daily_cap = table.Column<int>(type: "int", nullable: true),
                    goal_type = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "traffic"),
                    goal_impressions = table.Column<int>(type: "int", nullable: true),
                    delivered_today = table.Column<int>(type: "int", nullable: false),
                    delivered_on = table.Column<DateOnly>(type: "date", nullable: true),
                    delivered_total = table.Column<int>(type: "int", nullable: false),
                    contract_amount = table.Column<int>(type: "int", nullable: true),
                    is_amount_hidden = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "draft"),
                    paused_from = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    pause_reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ad_campaigns", x => x.id)
                        .Annotation("SqlServer:Clustered", false);
                    table.ForeignKey(
                        name: "FK_ad_campaigns_advertiser",
                        column: x => x.advertiser_id,
                        principalTable: "advertisers",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_ad_campaigns_created_by",
                        column: x => x.created_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_ad_campaigns_reviewed_by",
                        column: x => x.reviewed_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_ad_campaigns_slot",
                        column: x => x.slot_id,
                        principalTable: "ad_slots",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_ad_campaigns_updated_by",
                        column: x => x.updated_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "advertisers_i18n",
                columns: table => new
                {
                    advertiser_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    locale = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_advertisers_i18n", x => new { x.advertiser_id, x.locale });
                    table.ForeignKey(
                        name: "FK_advertisers_i18n_advertiser",
                        column: x => x.advertiser_id,
                        principalTable: "advertisers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "app_announcements_i18n",
                columns: table => new
                {
                    app_announcement_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    locale = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    message = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_announcements_i18n", x => new { x.app_announcement_id, x.locale });
                    table.ForeignKey(
                        name: "FK_app_announcements_i18n_item",
                        column: x => x.app_announcement_id,
                        principalTable: "app_announcements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "app_deep_links_i18n",
                columns: table => new
                {
                    app_deep_link_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    locale = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    label = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_deep_links_i18n", x => new { x.app_deep_link_id, x.locale });
                    table.ForeignKey(
                        name: "FK_app_deep_links_i18n_link",
                        column: x => x.app_deep_link_id,
                        principalTable: "app_deep_links",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "app_layout_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    row_seq = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    item_key = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                    deep_link_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    icon_key = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: true),
                    sort_order = table.Column<int>(type: "int", nullable: false),
                    is_enabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_layout_items", x => x.id)
                        .Annotation("SqlServer:Clustered", false);
                    table.ForeignKey(
                        name: "FK_app_layout_items_created_by",
                        column: x => x.created_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_app_layout_items_deep_link",
                        column: x => x.deep_link_id,
                        principalTable: "app_deep_links",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_app_layout_items_updated_by",
                        column: x => x.updated_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "push_topic_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    row_seq = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    device_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    member_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    topic_type = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    topic_value = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    is_following = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    is_push_enabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_push_topic_subscriptions", x => x.id)
                        .Annotation("SqlServer:Clustered", false);
                    table.ForeignKey(
                        name: "FK_push_topic_subscriptions_device",
                        column: x => x.device_id,
                        principalTable: "app_devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_push_topic_subscriptions_member",
                        column: x => x.member_id,
                        principalTable: "members",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "app_releases_i18n",
                columns: table => new
                {
                    app_release_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    locale = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    whats_new = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    force_message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    recommend_message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_releases_i18n", x => new { x.app_release_id, x.locale });
                    table.ForeignKey(
                        name: "FK_app_releases_i18n_release",
                        column: x => x.app_release_id,
                        principalTable: "app_releases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "push_message_stats",
                columns: table => new
                {
                    push_message_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    platform = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    locale = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    sent = table.Column<int>(type: "int", nullable: false),
                    delivered = table.Column<int>(type: "int", nullable: false),
                    opened = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_push_message_stats", x => new { x.push_message_id, x.platform, x.locale });
                    table.ForeignKey(
                        name: "FK_push_message_stats_message",
                        column: x => x.push_message_id,
                        principalTable: "push_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "push_messages_i18n",
                columns: table => new
                {
                    push_message_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    locale = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    body = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    image_alt = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_push_messages_i18n", x => new { x.push_message_id, x.locale });
                    table.ForeignKey(
                        name: "FK_push_messages_i18n_message",
                        column: x => x.push_message_id,
                        principalTable: "push_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ad_creatives",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    row_seq = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    campaign_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    locale = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    image_key = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    image_width = table.Column<int>(type: "int", nullable: true),
                    image_height = table.Column<int>(type: "int", nullable: true),
                    video_key = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    alt_text = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    cta_text = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    click_url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    theme = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false, defaultValue: "both"),
                    variant_tag = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    review_status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "pending"),
                    reject_reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    is_paused = table.Column<bool>(type: "bit", nullable: false),
                    sort_order = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ad_creatives", x => x.id)
                        .Annotation("SqlServer:Clustered", false);
                    table.ForeignKey(
                        name: "FK_ad_creatives_campaign",
                        column: x => x.campaign_id,
                        principalTable: "ad_campaigns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ad_creatives_created_by",
                        column: x => x.created_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_ad_creatives_reviewed_by",
                        column: x => x.reviewed_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_ad_creatives_updated_by",
                        column: x => x.updated_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "app_layout_items_i18n",
                columns: table => new
                {
                    app_layout_item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    locale = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    label = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_layout_items_i18n", x => new { x.app_layout_item_id, x.locale });
                    table.ForeignKey(
                        name: "FK_app_layout_items_i18n_item",
                        column: x => x.app_layout_item_id,
                        principalTable: "app_layout_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ad_daily_stats",
                columns: table => new
                {
                    stat_date = table.Column<DateOnly>(type: "date", nullable: false),
                    campaign_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    creative_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    slot_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    platform = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    locale = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    impressions = table.Column<int>(type: "int", nullable: false),
                    clicks = table.Column<int>(type: "int", nullable: false),
                    unique_devices = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ad_daily_stats", x => new { x.stat_date, x.campaign_id, x.creative_id, x.slot_id, x.platform, x.locale });
                    table.ForeignKey(
                        name: "FK_ad_daily_stats_creative",
                        column: x => x.creative_id,
                        principalTable: "ad_creatives",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ad_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    event_type = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    creative_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    campaign_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    slot_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    received_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    device_install_id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    platform = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    app_version = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    locale = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    presentation_id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    batch_id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    dedupe_key = table.Column<string>(type: "char(32)", unicode: false, fixedLength: true, maxLength: 32, nullable: false),
                    aggregated_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ad_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_ad_events_creative",
                        column: x => x.creative_id,
                        principalTable: "ad_creatives",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ad_campaigns_advertiser",
                table: "ad_campaigns",
                column: "advertiser_id");

            migrationBuilder.CreateIndex(
                name: "IX_ad_campaigns_slot_status",
                table: "ad_campaigns",
                columns: new[] { "slot_id", "status", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "UQ_ad_campaigns_row_seq",
                table: "ad_campaigns",
                column: "row_seq",
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "IX_ad_creatives_campaign",
                table: "ad_creatives",
                column: "campaign_id");

            migrationBuilder.CreateIndex(
                name: "UQ_ad_creatives_row_seq",
                table: "ad_creatives",
                column: "row_seq",
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "IX_ad_daily_stats_campaign_date",
                table: "ad_daily_stats",
                columns: new[] { "campaign_id", "stat_date" });

            migrationBuilder.CreateIndex(
                name: "IX_ad_events_campaign_occurred",
                table: "ad_events",
                columns: new[] { "campaign_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_ad_events_device_creative",
                table: "ad_events",
                columns: new[] { "device_install_id", "creative_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_ad_events_unaggregated",
                table: "ad_events",
                columns: new[] { "aggregated_at", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "UQ_ad_events_dedupe_key",
                table: "ad_events",
                column: "dedupe_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_ad_slots_row_seq",
                table: "ad_slots",
                column: "row_seq",
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "UQ_ad_slots_slot_code",
                table: "ad_slots",
                column: "slot_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ad_slots_i18n_locale",
                table: "ad_slots_i18n",
                column: "locale");

            migrationBuilder.CreateIndex(
                name: "IX_advertisers_status",
                table: "advertisers",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "UQ_advertisers_row_seq",
                table: "advertisers",
                column: "row_seq",
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "IX_advertisers_i18n_locale",
                table: "advertisers_i18n",
                column: "locale");

            migrationBuilder.CreateIndex(
                name: "UQ_app_announcements_row_seq",
                table: "app_announcements",
                column: "row_seq",
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "IX_app_announcements_i18n_locale",
                table: "app_announcements_i18n",
                column: "locale");

            migrationBuilder.CreateIndex(
                name: "UQ_app_credentials_row_seq",
                table: "app_credentials",
                column: "row_seq",
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "UQ_app_deep_links_code",
                table: "app_deep_links",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_app_deep_links_row_seq",
                table: "app_deep_links",
                column: "row_seq",
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "IX_app_deep_links_i18n_locale",
                table: "app_deep_links_i18n",
                column: "locale");

            migrationBuilder.CreateIndex(
                name: "IX_app_devices_last_active",
                table: "app_devices",
                column: "last_active_at");

            migrationBuilder.CreateIndex(
                name: "IX_app_devices_member",
                table: "app_devices",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "IX_app_devices_token_hash",
                table: "app_devices",
                column: "push_token_hash");

            migrationBuilder.CreateIndex(
                name: "UQ_app_devices_install_id",
                table: "app_devices",
                column: "device_install_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_app_devices_row_seq",
                table: "app_devices",
                column: "row_seq",
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "IX_app_diagnostic_reports_received",
                table: "app_diagnostic_reports",
                column: "received_at");

            migrationBuilder.CreateIndex(
                name: "IX_app_diagnostic_reports_type",
                table: "app_diagnostic_reports",
                columns: new[] { "report_type", "status" });

            migrationBuilder.CreateIndex(
                name: "UQ_app_diagnostic_reports_row_seq",
                table: "app_diagnostic_reports",
                column: "row_seq",
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "UQ_app_feature_flags_key_platform",
                table: "app_feature_flags",
                columns: new[] { "flag_key", "platform" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_app_feature_flags_row_seq",
                table: "app_feature_flags",
                column: "row_seq",
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "UQ_app_layout_items_kind_key",
                table: "app_layout_items",
                columns: new[] { "kind", "item_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_app_layout_items_row_seq",
                table: "app_layout_items",
                column: "row_seq",
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "IX_app_layout_items_i18n_locale",
                table: "app_layout_items_i18n",
                column: "locale");

            migrationBuilder.CreateIndex(
                name: "UQ_app_releases_platform_version",
                table: "app_releases",
                columns: new[] { "platform", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_app_releases_row_seq",
                table: "app_releases",
                column: "row_seq",
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "IX_app_releases_i18n_locale",
                table: "app_releases_i18n",
                column: "locale");

            migrationBuilder.CreateIndex(
                name: "UQ_app_settings_key",
                table: "app_settings",
                column: "setting_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_app_settings_row_seq",
                table: "app_settings",
                column: "row_seq",
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "IX_push_messages_status_scheduled",
                table: "push_messages",
                columns: new[] { "status", "scheduled_at" });

            migrationBuilder.CreateIndex(
                name: "UQ_push_messages_row_seq",
                table: "push_messages",
                column: "row_seq",
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "IX_push_messages_i18n_locale",
                table: "push_messages_i18n",
                column: "locale");

            migrationBuilder.CreateIndex(
                name: "IX_push_topic_subscriptions_topic",
                table: "push_topic_subscriptions",
                columns: new[] { "topic_type", "topic_value", "is_push_enabled" });

            migrationBuilder.CreateIndex(
                name: "UQ_push_topic_subscriptions_device_topic",
                table: "push_topic_subscriptions",
                columns: new[] { "device_id", "topic_type", "topic_value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_push_topic_subscriptions_row_seq",
                table: "push_topic_subscriptions",
                column: "row_seq",
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            // CHECK 約束（EF 模型不描述 CHECK，比照 AlignSchemaC1 用原生 SQL 補上，與 db/club-schema.sql 一致）。
            migrationBuilder.Sql("ALTER TABLE ad_slots ADD CONSTRAINT CK_ad_slots_surface CHECK (surface IN ('app','web'));");
            migrationBuilder.Sql("ALTER TABLE ad_slots ADD CONSTRAINT CK_ad_slots_rotation_cap CHECK (rotation_cap BETWEEN 1 AND 10);");
            migrationBuilder.Sql("ALTER TABLE advertisers ADD CONSTRAINT CK_advertisers_status CHECK (status IN ('negotiating','active','ended'));");
            migrationBuilder.Sql("ALTER TABLE ad_campaigns ADD CONSTRAINT CK_ad_campaigns_weight CHECK (weight BETWEEN 1 AND 100);");
            migrationBuilder.Sql("ALTER TABLE ad_campaigns ADD CONSTRAINT CK_ad_campaigns_goal_type CHECK (goal_type IN ('guaranteed','traffic'));");
            migrationBuilder.Sql("ALTER TABLE ad_campaigns ADD CONSTRAINT CK_ad_campaigns_status CHECK (status IN ('draft','pending_review','scheduled','running','paused','ended','closed','voided'));");
            migrationBuilder.Sql("ALTER TABLE ad_campaigns ADD CONSTRAINT CK_ad_campaigns_period CHECK (ends_at > starts_at);");
            migrationBuilder.Sql("ALTER TABLE ad_creatives ADD CONSTRAINT CK_ad_creatives_theme CHECK (theme IN ('light','dark','both'));");
            migrationBuilder.Sql("ALTER TABLE ad_creatives ADD CONSTRAINT CK_ad_creatives_review_status CHECK (review_status IN ('pending','approved','rejected'));");
            migrationBuilder.Sql("ALTER TABLE ad_events ADD CONSTRAINT CK_ad_events_event_type CHECK (event_type IN ('impression','click'));");
            migrationBuilder.Sql("ALTER TABLE app_devices ADD CONSTRAINT CK_app_devices_platform CHECK (platform IN ('ios','android'));");
            migrationBuilder.Sql("ALTER TABLE app_devices ADD CONSTRAINT CK_app_devices_push_token_status CHECK (push_token_status IN ('none','valid','invalid'));");
            migrationBuilder.Sql("ALTER TABLE app_devices ADD CONSTRAINT CK_app_devices_push_permission CHECK (push_permission IN ('not_determined','granted','denied','provisional'));");
            migrationBuilder.Sql("ALTER TABLE push_topic_subscriptions ADD CONSTRAINT CK_push_topic_subscriptions_topic_type CHECK (topic_type IN ('team','news_category','club'));");
            migrationBuilder.Sql("ALTER TABLE push_messages ADD CONSTRAINT CK_push_messages_kind CHECK (kind IN ('announcement','news','match'));");
            migrationBuilder.Sql("ALTER TABLE push_messages ADD CONSTRAINT CK_push_messages_audience_tier CHECK (audience_tier IN ('all','fan_club','registered','anonymous'));");
            migrationBuilder.Sql("ALTER TABLE push_messages ADD CONSTRAINT CK_push_messages_status CHECK (status IN ('draft','pending_review','scheduled','sending','sent','partial','failed','cancelled'));");
            migrationBuilder.Sql("ALTER TABLE app_releases ADD CONSTRAINT CK_app_releases_platform CHECK (platform IN ('ios','android'));");
            migrationBuilder.Sql("ALTER TABLE app_releases ADD CONSTRAINT CK_app_releases_status CHECK (status IN ('testing','live','withdrawn'));");
            migrationBuilder.Sql("ALTER TABLE app_diagnostic_reports ADD CONSTRAINT CK_app_diagnostic_reports_platform CHECK (platform IN ('ios','android'));");
            migrationBuilder.Sql("ALTER TABLE app_diagnostic_reports ADD CONSTRAINT CK_app_diagnostic_reports_report_type CHECK (report_type IN ('crash','abnormal_exit','api_error','startup_time','user_report'));");
            migrationBuilder.Sql("ALTER TABLE app_diagnostic_reports ADD CONSTRAINT CK_app_diagnostic_reports_status CHECK (status IN ('new','reviewing','resolved','ignored'));");
            migrationBuilder.Sql("ALTER TABLE app_layout_items ADD CONSTRAINT CK_app_layout_items_kind CHECK (kind IN ('home_section','quick_entry','more_item'));");
            migrationBuilder.Sql("ALTER TABLE app_announcements ADD CONSTRAINT CK_app_announcements_audience_tier CHECK (audience_tier IN ('all','fan_club','registered','anonymous'));");
            migrationBuilder.Sql("ALTER TABLE app_feature_flags ADD CONSTRAINT CK_app_feature_flags_platform CHECK (platform IN ('all','ios','android'));");
            migrationBuilder.Sql("ALTER TABLE app_credentials ADD CONSTRAINT CK_app_credentials_kind CHECK (kind IN ('apns_key','fcm_credential','apple_developer_program','google_play_account','maps_api_key','other'));");
            migrationBuilder.Sql("ALTER TABLE newsletter_subscribers ADD CONSTRAINT CK_newsletter_subscribers_status CHECK (status IN ('subscribed','unsubscribed'));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE newsletter_subscribers DROP CONSTRAINT CK_newsletter_subscribers_status;");

            migrationBuilder.DropTable(
                name: "ad_daily_stats");

            migrationBuilder.DropTable(
                name: "ad_events");

            migrationBuilder.DropTable(
                name: "ad_slots_i18n");

            migrationBuilder.DropTable(
                name: "advertisers_i18n");

            migrationBuilder.DropTable(
                name: "app_announcements_i18n");

            migrationBuilder.DropTable(
                name: "app_credentials");

            migrationBuilder.DropTable(
                name: "app_deep_links_i18n");

            migrationBuilder.DropTable(
                name: "app_diagnostic_reports");

            migrationBuilder.DropTable(
                name: "app_feature_flags");

            migrationBuilder.DropTable(
                name: "app_layout_items_i18n");

            migrationBuilder.DropTable(
                name: "app_releases_i18n");

            migrationBuilder.DropTable(
                name: "app_settings");

            migrationBuilder.DropTable(
                name: "push_message_stats");

            migrationBuilder.DropTable(
                name: "push_messages_i18n");

            migrationBuilder.DropTable(
                name: "push_topic_subscriptions");

            migrationBuilder.DropTable(
                name: "ad_creatives");

            migrationBuilder.DropTable(
                name: "app_announcements");

            migrationBuilder.DropTable(
                name: "app_layout_items");

            migrationBuilder.DropTable(
                name: "app_releases");

            migrationBuilder.DropTable(
                name: "push_messages");

            migrationBuilder.DropTable(
                name: "app_devices");

            migrationBuilder.DropTable(
                name: "ad_campaigns");

            migrationBuilder.DropTable(
                name: "app_deep_links");

            migrationBuilder.DropTable(
                name: "advertisers");

            migrationBuilder.DropTable(
                name: "ad_slots");

            migrationBuilder.DropColumn(
                name: "unsubscribed_at",
                table: "newsletter_subscribers");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "newsletter_subscribers",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldDefaultValue: "subscribed");
        }
    }
}
