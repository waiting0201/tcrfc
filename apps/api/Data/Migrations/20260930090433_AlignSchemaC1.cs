using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlignSchemaC1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 既有列（本機／正式庫在 C1 之前建立的資料）：把即將收斂為 NOT NULL 的狀態欄補上預設值。
            migrationBuilder.Sql("UPDATE comic_episodes SET status = 'draft' WHERE status IS NULL OR status NOT IN ('draft','published');");
            migrationBuilder.Sql("UPDATE fan_event_registrations SET status = 'registered' WHERE status IS NULL OR status NOT IN ('registered','waitlist','cancelled','attended');");
            migrationBuilder.Sql("UPDATE product_variants SET status = 'active' WHERE status IS NULL OR status NOT IN ('active','inactive');");
            migrationBuilder.Sql("UPDATE refund_requests SET status = 'requested' WHERE status IS NULL OR status NOT IN ('requested','approved','received','processing','refunded','rejected');");
            migrationBuilder.Sql("UPDATE inventory_movements SET movement_type = 'adjust' WHERE movement_type IS NULL;");

            migrationBuilder.DropIndex(
                name: "UQ_draw_rosters_draw_member_no",
                table: "draw_rosters");

            migrationBuilder.DropIndex(
                name: "UQ_draw_rosters_draw_serial",
                table: "draw_rosters");

            migrationBuilder.AddColumn<DateTime>(
                name: "arrival_notified_at",
                table: "shipments",
                type: "datetime2(3)",
                precision: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "pickup_deadline_on",
                table: "shipments",
                type: "date",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "refund_requests",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "requested",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldNullable: true)
                .Annotation("Relational:DefaultConstraintName", "DF_refund_requests_status");

            migrationBuilder.AddColumn<bool>(
                name: "needs_return",
                table: "refund_requests",
                type: "bit",
                nullable: false,
                defaultValue: true)
                .Annotation("Relational:DefaultConstraintName", "DF_refund_requests_needs_return");

            migrationBuilder.AddColumn<DateTime>(
                name: "received_at",
                table: "refund_requests",
                type: "datetime2(3)",
                precision: 3,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "received_by",
                table: "refund_requests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "refund_reference",
                table: "refund_requests",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "refunded_by",
                table: "refund_requests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "review_note",
                table: "refund_requests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "out_of_stock_behavior",
                table: "products",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "show_unavailable")
                .Annotation("Relational:DefaultConstraintName", "DF_products_oos");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "product_variants",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "active",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldNullable: true)
                .Annotation("Relational:DefaultConstraintName", "DF_product_variants_status");

            migrationBuilder.AddColumn<int>(
                name: "low_stock_threshold",
                table: "product_variants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "product_variants",
                type: "int",
                nullable: false,
                defaultValue: 0);

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
                oldDefaultValue: "draft")
                .Annotation("Relational:DefaultConstraintName", "DF_partner_stores_status");

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
                oldDefaultValue: "all")
                .Annotation("Relational:DefaultConstraintName", "DF_partner_stores_applicable_tier");

            migrationBuilder.AddColumn<string>(
                name: "cancel_reason",
                table: "orders",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "cancelled_at",
                table: "orders",
                type: "datetime2(3)",
                precision: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "completed_at",
                table: "orders",
                type: "datetime2(3)",
                precision: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "customer_note",
                table: "orders",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "internal_note",
                table: "orders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_method",
                table: "orders",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "linepay")
                .Annotation("Relational:DefaultConstraintName", "DF_orders_payment_method");

            migrationBuilder.AddColumn<DateOnly>(
                name: "settled_on",
                table: "orders",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "settlement_note",
                table: "orders",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "settlement_status",
                table: "orders",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "pending")
                .Annotation("Relational:DefaultConstraintName", "DF_orders_settlement_status");

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
                oldDefaultValue: "active")
                .Annotation("Relational:DefaultConstraintName", "DF_memberships_status");

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
                oldDefaultValue: "draft")
                .Annotation("Relational:DefaultConstraintName", "DF_membership_plans_status");

            migrationBuilder.AddColumn<string>(
                name: "internal_note",
                table: "member_draws",
                type: "nvarchar(max)",
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
                oldDefaultValue: "active")
                .Annotation("Relational:DefaultConstraintName", "DF_member_cards_status");

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
                oldDefaultValue: "pending")
                .Annotation("Relational:DefaultConstraintName", "DF_jersey_issues_status");

            migrationBuilder.AlterColumn<string>(
                name: "movement_type",
                table: "inventory_movements",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "reserved_after",
                table: "inventory_movements",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "stock_after",
                table: "inventory_movements",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "location",
                table: "fan_events_i18n",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cover_key",
                table: "fan_events",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ends_at",
                table: "fan_events",
                type: "datetime2(3)",
                precision: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "registration_deadline_at",
                table: "fan_events",
                type: "datetime2(3)",
                precision: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "fan_events",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "draft")
                .Annotation("Relational:DefaultConstraintName", "DF_fan_events_status");

            migrationBuilder.AddColumn<Guid>(
                name: "venue_id",
                table: "fan_events",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "fan_event_registrations",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "registered",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldNullable: true)
                .Annotation("Relational:DefaultConstraintName", "DF_fan_event_registrations_status");

            migrationBuilder.AddColumn<string>(
                name: "applicant_name",
                table: "fan_event_registrations",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "fan_event_registrations",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "note",
                table: "fan_event_registrations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone",
                table: "fan_event_registrations",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "claimed_at",
                table: "draw_rosters",
                type: "datetime2(3)",
                precision: 3,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_backup",
                table: "draw_rosters",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "recipient_address",
                table: "draw_rosters",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "recipient_name",
                table: "draw_rosters",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "recipient_phone",
                table: "draw_rosters",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "roster_version",
                table: "draw_rosters",
                type: "int",
                nullable: false,
                defaultValue: 1)
                .Annotation("Relational:DefaultConstraintName", "DF_draw_rosters_version");

            migrationBuilder.AddColumn<DateTime>(
                name: "shipped_at",
                table: "draw_rosters",
                type: "datetime2(3)",
                precision: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "image_height",
                table: "comic_pages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "image_width",
                table: "comic_pages",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "comic_episodes",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "draft",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldNullable: true)
                .Annotation("Relational:DefaultConstraintName", "DF_comic_episodes_status");

            migrationBuilder.CreateTable(
                name: "draw_roster_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    row_seq = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    member_draw_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    roster_version = table.Column<int>(type: "int", nullable: false),
                    snapshot_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    total_count = table.Column<int>(type: "int", nullable: false),
                    roster_hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    generated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    generated_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    voided_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    voided_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    void_reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_draw_roster_versions", x => x.id)
                        .Annotation("SqlServer:Clustered", false);
                    table.ForeignKey(
                        name: "FK_draw_roster_versions_created_by",
                        column: x => x.created_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_draw_roster_versions_draw",
                        column: x => x.member_draw_id,
                        principalTable: "member_draws",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_draw_roster_versions_generated_by",
                        column: x => x.generated_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_draw_roster_versions_updated_by",
                        column: x => x.updated_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_draw_roster_versions_voided_by",
                        column: x => x.voided_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "fan_event_articles",
                columns: table => new
                {
                    fan_event_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    article_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    sort_order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fan_event_articles", x => new { x.fan_event_id, x.article_id });
                    table.ForeignKey(
                        name: "FK_fan_event_articles_article",
                        column: x => x.article_id,
                        principalTable: "articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fan_event_articles_event",
                        column: x => x.fan_event_id,
                        principalTable: "fan_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fan_event_images",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    row_seq = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    fan_event_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_fan_event_images", x => x.id)
                        .Annotation("SqlServer:Clustered", false);
                    table.ForeignKey(
                        name: "FK_fan_event_images_created_by",
                        column: x => x.created_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_fan_event_images_event",
                        column: x => x.fan_event_id,
                        principalTable: "fan_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fan_event_images_updated_by",
                        column: x => x.updated_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "UQ_shipments_order",
                table: "shipments",
                column: "order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refund_requests_club_status",
                table: "refund_requests",
                columns: new[] { "club_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_refund_requests_order",
                table: "refund_requests",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_variants_product",
                table: "product_variants",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_selling_created",
                table: "orders",
                columns: new[] { "selling_club_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_fan_event_registrations_event_status",
                table: "fan_event_registrations",
                columns: new[] { "fan_event_id", "status" });

            migrationBuilder.CreateIndex(
                name: "UQ_fan_event_registrations_event_member",
                table: "fan_event_registrations",
                columns: new[] { "fan_event_id", "member_id" },
                unique: true,
                filter: "([member_id] IS NOT NULL AND [status]<>'cancelled')");

            migrationBuilder.CreateIndex(
                name: "IX_draw_rosters_draw_winner",
                table: "draw_rosters",
                columns: new[] { "member_draw_id", "roster_version", "is_winner" });

            migrationBuilder.CreateIndex(
                name: "UQ_draw_rosters_draw_ver_member_no",
                table: "draw_rosters",
                columns: new[] { "member_draw_id", "roster_version", "member_no_snapshot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_draw_rosters_draw_ver_serial",
                table: "draw_rosters",
                columns: new[] { "member_draw_id", "roster_version", "serial_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_comic_episodes_club_no",
                table: "comic_episodes",
                columns: new[] { "club_id", "episode_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_draw_roster_versions_draw_ver",
                table: "draw_roster_versions",
                columns: new[] { "member_draw_id", "roster_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_draw_roster_versions_row_seq",
                table: "draw_roster_versions",
                column: "row_seq",
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "UQ_fan_event_images_row_seq",
                table: "fan_event_images",
                column: "row_seq",
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.AddForeignKey(
                name: "FK_fan_events_venue",
                table: "fan_events",
                column: "venue_id",
                principalTable: "venues",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_refund_requests_received_by",
                table: "refund_requests",
                column: "received_by",
                principalTable: "admin_users",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_refund_requests_refunded_by",
                table: "refund_requests",
                column: "refunded_by",
                principalTable: "admin_users",
                principalColumn: "id");

            // CHECK 約束 EF 不建模，比照 db/club-schema.sql 逐條加上（具名，Down() 可依名稱移除）。
            migrationBuilder.Sql("ALTER TABLE comic_episodes ADD CONSTRAINT CK_comic_episodes_status CHECK (status IN ('draft','published'));");
            migrationBuilder.Sql("ALTER TABLE fan_events ADD CONSTRAINT CK_fan_events_status CHECK (status IN ('draft','published'));");
            migrationBuilder.Sql("ALTER TABLE fan_event_registrations ADD CONSTRAINT CK_fan_event_registrations_status CHECK (status IN ('registered','waitlist','cancelled','attended'));");
            migrationBuilder.Sql("ALTER TABLE products ADD CONSTRAINT CK_products_oos CHECK (out_of_stock_behavior IN ('show_unavailable','hide'));");
            migrationBuilder.Sql("ALTER TABLE product_variants ADD CONSTRAINT CK_product_variants_status CHECK (status IN ('active','inactive'));");
            migrationBuilder.Sql("ALTER TABLE product_variants ADD CONSTRAINT CK_product_variants_stock CHECK (stock_qty >= 0 AND reserved_qty >= 0 AND reserved_qty <= stock_qty);");
            migrationBuilder.Sql("ALTER TABLE inventory_movements ADD CONSTRAINT CK_inventory_movements_type CHECK (movement_type IN ('stock_in','stocktake','damage','adjust','reserve','release','sale','cancel_restock','return_restock'));");
            migrationBuilder.Sql("ALTER TABLE orders ADD CONSTRAINT CK_orders_payment_method CHECK (payment_method IN ('linepay','onsite'));");
            migrationBuilder.Sql("ALTER TABLE orders ADD CONSTRAINT CK_orders_settlement_status CHECK (settlement_status IN ('pending','settled'));");
            migrationBuilder.Sql("ALTER TABLE shipments ADD CONSTRAINT CK_shipments_pickup_status CHECK (pickup_status IS NULL OR pickup_status IN ('waiting','picked_up'));");
            migrationBuilder.Sql("ALTER TABLE refund_requests ADD CONSTRAINT CK_refund_requests_status CHECK (status IN ('requested','approved','received','processing','refunded','rejected'));");
            migrationBuilder.Sql("ALTER TABLE draw_rosters ADD CONSTRAINT CK_draw_rosters_claim_method CHECK (claim_method IS NULL OR claim_method IN ('ship','pickup'));");
            migrationBuilder.Sql("ALTER TABLE draw_rosters ADD CONSTRAINT CK_draw_rosters_fulfilment CHECK (fulfilment_status IS NULL OR fulfilment_status IN ('pending','shipped','claimed'));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 先移除 CHECK 約束（之後才能 DropColumn／改回可為空）。
            migrationBuilder.Sql("ALTER TABLE comic_episodes DROP CONSTRAINT CK_comic_episodes_status;");
            migrationBuilder.Sql("ALTER TABLE fan_events DROP CONSTRAINT CK_fan_events_status;");
            migrationBuilder.Sql("ALTER TABLE fan_event_registrations DROP CONSTRAINT CK_fan_event_registrations_status;");
            migrationBuilder.Sql("ALTER TABLE products DROP CONSTRAINT CK_products_oos;");
            migrationBuilder.Sql("ALTER TABLE product_variants DROP CONSTRAINT CK_product_variants_status;");
            migrationBuilder.Sql("ALTER TABLE product_variants DROP CONSTRAINT CK_product_variants_stock;");
            migrationBuilder.Sql("ALTER TABLE inventory_movements DROP CONSTRAINT CK_inventory_movements_type;");
            migrationBuilder.Sql("ALTER TABLE orders DROP CONSTRAINT CK_orders_payment_method;");
            migrationBuilder.Sql("ALTER TABLE orders DROP CONSTRAINT CK_orders_settlement_status;");
            migrationBuilder.Sql("ALTER TABLE shipments DROP CONSTRAINT CK_shipments_pickup_status;");
            migrationBuilder.Sql("ALTER TABLE refund_requests DROP CONSTRAINT CK_refund_requests_status;");
            migrationBuilder.Sql("ALTER TABLE draw_rosters DROP CONSTRAINT CK_draw_rosters_claim_method;");
            migrationBuilder.Sql("ALTER TABLE draw_rosters DROP CONSTRAINT CK_draw_rosters_fulfilment;");

            migrationBuilder.DropForeignKey(
                name: "FK_fan_events_venue",
                table: "fan_events");

            migrationBuilder.DropForeignKey(
                name: "FK_refund_requests_received_by",
                table: "refund_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_refund_requests_refunded_by",
                table: "refund_requests");

            migrationBuilder.DropTable(
                name: "draw_roster_versions");

            migrationBuilder.DropTable(
                name: "fan_event_articles");

            migrationBuilder.DropTable(
                name: "fan_event_images");

            migrationBuilder.DropIndex(
                name: "UQ_shipments_order",
                table: "shipments");

            migrationBuilder.DropIndex(
                name: "IX_refund_requests_club_status",
                table: "refund_requests");

            migrationBuilder.DropIndex(
                name: "IX_refund_requests_order",
                table: "refund_requests");

            migrationBuilder.DropIndex(
                name: "IX_product_variants_product",
                table: "product_variants");

            migrationBuilder.DropIndex(
                name: "IX_orders_selling_created",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_fan_event_registrations_event_status",
                table: "fan_event_registrations");

            migrationBuilder.DropIndex(
                name: "UQ_fan_event_registrations_event_member",
                table: "fan_event_registrations");

            migrationBuilder.DropIndex(
                name: "IX_draw_rosters_draw_winner",
                table: "draw_rosters");

            migrationBuilder.DropIndex(
                name: "UQ_draw_rosters_draw_ver_member_no",
                table: "draw_rosters");

            migrationBuilder.DropIndex(
                name: "UQ_draw_rosters_draw_ver_serial",
                table: "draw_rosters");

            migrationBuilder.DropIndex(
                name: "UQ_comic_episodes_club_no",
                table: "comic_episodes");

            migrationBuilder.DropColumn(
                name: "arrival_notified_at",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "pickup_deadline_on",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "needs_return",
                table: "refund_requests")
                .Annotation("Relational:DefaultConstraintName", "DF_refund_requests_needs_return");

            migrationBuilder.DropColumn(
                name: "received_at",
                table: "refund_requests");

            migrationBuilder.DropColumn(
                name: "received_by",
                table: "refund_requests");

            migrationBuilder.DropColumn(
                name: "refund_reference",
                table: "refund_requests");

            migrationBuilder.DropColumn(
                name: "refunded_by",
                table: "refund_requests");

            migrationBuilder.DropColumn(
                name: "review_note",
                table: "refund_requests");

            migrationBuilder.DropColumn(
                name: "out_of_stock_behavior",
                table: "products")
                .Annotation("Relational:DefaultConstraintName", "DF_products_oos");

            migrationBuilder.DropColumn(
                name: "low_stock_threshold",
                table: "product_variants");

            migrationBuilder.DropColumn(
                name: "sort_order",
                table: "product_variants");

            migrationBuilder.DropColumn(
                name: "cancel_reason",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "cancelled_at",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "completed_at",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "customer_note",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "internal_note",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "payment_method",
                table: "orders")
                .Annotation("Relational:DefaultConstraintName", "DF_orders_payment_method");

            migrationBuilder.DropColumn(
                name: "settled_on",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "settlement_note",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "settlement_status",
                table: "orders")
                .Annotation("Relational:DefaultConstraintName", "DF_orders_settlement_status");

            migrationBuilder.DropColumn(
                name: "internal_note",
                table: "member_draws");

            migrationBuilder.DropColumn(
                name: "reserved_after",
                table: "inventory_movements");

            migrationBuilder.DropColumn(
                name: "stock_after",
                table: "inventory_movements");

            migrationBuilder.DropColumn(
                name: "location",
                table: "fan_events_i18n");

            migrationBuilder.DropColumn(
                name: "cover_key",
                table: "fan_events");

            migrationBuilder.DropColumn(
                name: "ends_at",
                table: "fan_events");

            migrationBuilder.DropColumn(
                name: "registration_deadline_at",
                table: "fan_events");

            migrationBuilder.DropColumn(
                name: "status",
                table: "fan_events")
                .Annotation("Relational:DefaultConstraintName", "DF_fan_events_status");

            migrationBuilder.DropColumn(
                name: "venue_id",
                table: "fan_events");

            migrationBuilder.DropColumn(
                name: "applicant_name",
                table: "fan_event_registrations");

            migrationBuilder.DropColumn(
                name: "email",
                table: "fan_event_registrations");

            migrationBuilder.DropColumn(
                name: "note",
                table: "fan_event_registrations");

            migrationBuilder.DropColumn(
                name: "phone",
                table: "fan_event_registrations");

            migrationBuilder.DropColumn(
                name: "claimed_at",
                table: "draw_rosters");

            migrationBuilder.DropColumn(
                name: "is_backup",
                table: "draw_rosters");

            migrationBuilder.DropColumn(
                name: "recipient_address",
                table: "draw_rosters");

            migrationBuilder.DropColumn(
                name: "recipient_name",
                table: "draw_rosters");

            migrationBuilder.DropColumn(
                name: "recipient_phone",
                table: "draw_rosters");

            migrationBuilder.DropColumn(
                name: "roster_version",
                table: "draw_rosters")
                .Annotation("Relational:DefaultConstraintName", "DF_draw_rosters_version");

            migrationBuilder.DropColumn(
                name: "shipped_at",
                table: "draw_rosters");

            migrationBuilder.DropColumn(
                name: "image_height",
                table: "comic_pages");

            migrationBuilder.DropColumn(
                name: "image_width",
                table: "comic_pages");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "refund_requests",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldDefaultValue: "requested")
                .OldAnnotation("Relational:DefaultConstraintName", "DF_refund_requests_status");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "product_variants",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldDefaultValue: "active")
                .OldAnnotation("Relational:DefaultConstraintName", "DF_product_variants_status");

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
                oldDefaultValue: "draft")
                .OldAnnotation("Relational:DefaultConstraintName", "DF_partner_stores_status");

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
                oldDefaultValue: "all")
                .OldAnnotation("Relational:DefaultConstraintName", "DF_partner_stores_applicable_tier");

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
                oldDefaultValue: "active")
                .OldAnnotation("Relational:DefaultConstraintName", "DF_memberships_status");

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
                oldDefaultValue: "draft")
                .OldAnnotation("Relational:DefaultConstraintName", "DF_membership_plans_status");

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
                oldDefaultValue: "active")
                .OldAnnotation("Relational:DefaultConstraintName", "DF_member_cards_status");

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
                oldDefaultValue: "pending")
                .OldAnnotation("Relational:DefaultConstraintName", "DF_jersey_issues_status");

            migrationBuilder.AlterColumn<string>(
                name: "movement_type",
                table: "inventory_movements",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "fan_event_registrations",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldDefaultValue: "registered")
                .OldAnnotation("Relational:DefaultConstraintName", "DF_fan_event_registrations_status");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "comic_episodes",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldDefaultValue: "draft")
                .OldAnnotation("Relational:DefaultConstraintName", "DF_comic_episodes_status");

            migrationBuilder.CreateIndex(
                name: "UQ_draw_rosters_draw_member_no",
                table: "draw_rosters",
                columns: new[] { "member_draw_id", "member_no_snapshot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_draw_rosters_draw_serial",
                table: "draw_rosters",
                columns: new[] { "member_draw_id", "serial_no" },
                unique: true);
        }
    }
}
