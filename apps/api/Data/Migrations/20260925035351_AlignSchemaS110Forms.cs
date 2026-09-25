using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// S1-10（G1 表單設計器／G2 詢問收件匣）落地前的欄位與值域收斂，見
    /// docs/12-database-schema.md §12 第 37 點：
    /// ① <c>form_fields.options_json</c>（下拉／多選選項清單，EF 模型異動自動產生的
    /// <see cref="MigrationBuilder.AddColumn{T}"/>）；
    /// ② <c>form_fields.field_type</c> 補上 CHECK（對應規劃書 G1 六種欄位型別）；
    /// ③ <c>enquiries.status</c> 補上 CHECK（對應規劃書 G2 五個狀態值）。
    /// ②③ 兩欄本身早已存在（v3.0 建表時就有），但跟 <c>AlignSchemaV314</c>／
    /// <c>AlignSchemaS19Programs</c> 同一種落差，從來沒有被任何 CHECK 約束過。
    ///
    /// 🔴 套用前已查證 <c>tcrfc_club_dev</c> 的 <c>form_fields</c>／<c>enquiries</c> 兩張表皆為
    /// 0 筆（G 模組本輪才第一次接上真實 API），純 DDL 變更，不搭配任何 DML 轉態。
    /// </summary>
    public partial class AlignSchemaS110Forms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "options_json",
                table: "form_fields",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.Sql(
                "ALTER TABLE form_fields ADD CONSTRAINT CK_form_fields_field_type CHECK (field_type IN " +
                "('text','textarea','select','multiselect','date','file','consent'));");

            migrationBuilder.Sql(
                "ALTER TABLE enquiries ADD CONSTRAINT CK_enquiries_status CHECK (status IN " +
                "(N'新進',N'處理中',N'已回覆',N'已結案',N'無效'));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE enquiries DROP CONSTRAINT CK_enquiries_status;");
            migrationBuilder.Sql("ALTER TABLE form_fields DROP CONSTRAINT CK_form_fields_field_type;");

            migrationBuilder.DropColumn(
                name: "options_json",
                table: "form_fields");
        }
    }
}
