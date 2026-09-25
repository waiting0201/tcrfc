using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// S1-10（審查回饋補做，2026-09-25）：主站規劃書 G2（行 1164）逐字列出收件匣欄位含
    /// 「內容摘要」，`form_fields` 新增 `is_summary bit NOT NULL DEFAULT 0`，供 G1 表單設計器
    /// 標記「這是內容摘要來源欄位」，見 docs/12-database-schema.md §12 第 38 點。
    ///
    /// 同時補上 `UQ_form_fields_one_summary_per_form`（<c>is_summary = 1</c> 的過濾唯一索引）：
    /// 同一張表單最多一個欄位可標記為摘要，DB 層是第二道防線（第一道是
    /// <c>AdminFormsRepository</c> 的應用層檢查）。
    ///
    /// 🔴 套用時 <c>form_fields</c> 已有 114 筆種子資料（S1-10 前一輪已種），但這是單純新增欄位
    /// （帶 DEFAULT，不是 CHECK 約束既有資料），與「套用前查證 0 筆」的既有先例（AlignSchemaV314／
    /// AlignSchemaS19Programs／AlignSchemaS110Forms）處理的是不同情境——那些是在既有資料上新增
    /// CHECK 約束，這裡只是加一個有預設值的新欄位，對既有資料永遠安全。
    /// </summary>
    public partial class AddFormFieldIsSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_summary",
                table: "form_fields",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX UQ_form_fields_one_summary_per_form ON form_fields (form_id) WHERE is_summary = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX UQ_form_fields_one_summary_per_form ON form_fields;");

            migrationBuilder.DropColumn(
                name: "is_summary",
                table: "form_fields");
        }
    }
}
