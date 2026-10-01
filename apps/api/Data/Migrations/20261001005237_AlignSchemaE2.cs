using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlignSchemaE2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "membership_order_id",
                table: "membership_payments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "failed_attempt_count",
                table: "members",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "line_user_id_hash",
                table: "members",
                type: "char(64)",
                unicode: false,
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "locked_until",
                table: "members",
                type: "datetime2(3)",
                precision: 3,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "member_refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    row_seq = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    member_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    token_hash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    is_persistent = table.Column<bool>(type: "bit", nullable: false),
                    issued_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    expires_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    revoked_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    replaced_by_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_member_refresh_tokens", x => x.id)
                        .Annotation("SqlServer:Clustered", false);
                    table.ForeignKey(
                        name: "FK_member_refresh_tokens_member",
                        column: x => x.member_id,
                        principalTable: "members",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_member_refresh_tokens_replaced",
                        column: x => x.replaced_by_id,
                        principalTable: "member_refresh_tokens",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "membership_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    row_seq = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    order_no = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    member_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    club_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    collecting_club_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    membership_plan_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    idempotency_key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    amount = table.Column<int>(type: "int", nullable: false),
                    status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false, defaultValue: "created"),
                    payment_method = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    payment_transaction_id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    payment_url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    expires_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    paid_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    activated_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    activation_source = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    membership_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    failure_reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_membership_orders", x => x.id)
                        .Annotation("SqlServer:Clustered", false);
                    table.CheckConstraint("CK_membership_orders_activation_source", "[activation_source] IS NULL OR [activation_source] IN ('payment','internal','admin')");
                    table.CheckConstraint("CK_membership_orders_payment_method", "[payment_method] IS NULL OR [payment_method] IN ('linepay')");
                    table.CheckConstraint("CK_membership_orders_status", "[status] IN ('created','pending_payment','paid','activated','expired','activation_failed','cancelled','refunded')");
                    table.ForeignKey(
                        name: "FK_membership_orders_club",
                        column: x => x.club_id,
                        principalTable: "clubs",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_membership_orders_collecting_club",
                        column: x => x.collecting_club_id,
                        principalTable: "clubs",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_membership_orders_member",
                        column: x => x.member_id,
                        principalTable: "members",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_membership_orders_membership",
                        column: x => x.membership_id,
                        principalTable: "memberships",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_membership_orders_plan",
                        column: x => x.membership_plan_id,
                        principalTable: "membership_plans",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "UQ_membership_payments_order",
                table: "membership_payments",
                column: "membership_order_id",
                unique: true,
                filter: "[membership_order_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_members_line_user_id_hash",
                table: "members",
                column: "line_user_id_hash",
                unique: true,
                filter: "[line_user_id_hash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_member_refresh_tokens_member",
                table: "member_refresh_tokens",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "UQ_member_refresh_tokens_row_seq",
                table: "member_refresh_tokens",
                column: "row_seq",
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "UQ_member_refresh_tokens_token_hash",
                table: "member_refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_membership_orders_club_status",
                table: "membership_orders",
                columns: new[] { "club_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_membership_orders_member_created",
                table: "membership_orders",
                columns: new[] { "member_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "UQ_membership_orders_member_idempotency",
                table: "membership_orders",
                columns: new[] { "member_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_membership_orders_order_no",
                table: "membership_orders",
                column: "order_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_membership_orders_row_seq",
                table: "membership_orders",
                column: "row_seq",
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.AddForeignKey(
                name: "FK_membership_payments_order",
                table: "membership_payments",
                column: "membership_order_id",
                principalTable: "membership_orders",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_membership_payments_order",
                table: "membership_payments");

            migrationBuilder.DropTable(
                name: "member_refresh_tokens");

            migrationBuilder.DropTable(
                name: "membership_orders");

            migrationBuilder.DropIndex(
                name: "UQ_membership_payments_order",
                table: "membership_payments");

            migrationBuilder.DropIndex(
                name: "UQ_members_line_user_id_hash",
                table: "members");

            migrationBuilder.DropColumn(
                name: "membership_order_id",
                table: "membership_payments");

            migrationBuilder.DropColumn(
                name: "failed_attempt_count",
                table: "members");

            migrationBuilder.DropColumn(
                name: "line_user_id_hash",
                table: "members");

            migrationBuilder.DropColumn(
                name: "locked_until",
                table: "members");
        }
    }
}
