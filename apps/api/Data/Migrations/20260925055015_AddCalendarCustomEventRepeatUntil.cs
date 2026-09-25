using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarCustomEventRepeatUntil : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "repeat_until",
                table: "calendar_custom_events",
                type: "date",
                nullable: true);

            // S1-11：套用前查證 calendar_custom_events 為 0 筆（本輪才第一次接上真實 API），
            // 純 DDL 變更，不搭配任何 DML 轉態——見 docs/12-database-schema.md §12 第 39 點、
            // db/club-schema.sql 對應建表陳述式（CK_calendar_custom_events_repeat_rule）。
            migrationBuilder.Sql(
                "ALTER TABLE calendar_custom_events ADD CONSTRAINT CK_calendar_custom_events_repeat_rule " +
                "CHECK (repeat_rule IN ('weekly','biweekly','monthly') OR repeat_rule IS NULL);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE calendar_custom_events DROP CONSTRAINT CK_calendar_custom_events_repeat_rule;");

            migrationBuilder.DropColumn(
                name: "repeat_until",
                table: "calendar_custom_events");
        }
    }
}
