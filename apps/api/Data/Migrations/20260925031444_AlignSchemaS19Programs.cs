using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// S1-9（P1–P3 課程項目／梯次／報名）落地前的欄位收斂：<c>programs.status</c>／
    /// <c>programs.program_type</c>／<c>sessions.status</c> 三欄早在 v3.0 建表時就存在，但跟
    /// <c>AlignSchemaV314</c> 補上 <c>matches.status</c> CHECK 約束那次一樣，從來沒有被任何
    /// CHECK 約束過，見 docs/12-database-schema.md §12 第 36 點。
    ///
    /// 🔴 三個 CHECK 約束都是全新加上去的，不是收斂既有 CHECK——套用前已查證
    /// <c>tcrfc_club_dev</c> 的 <c>programs</c>／<c>sessions</c> 兩張表皆為 0 筆
    /// （apps/api/README.md 本節「既有資料檢查」），純 DDL 變更，不搭配任何 DML 轉態。
    /// </summary>
    public partial class AlignSchemaS19Programs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE programs ADD CONSTRAINT CK_programs_status CHECK (status IN ('draft','published'));");

            migrationBuilder.Sql(
                "ALTER TABLE programs ADD CONSTRAINT CK_programs_program_type CHECK (program_type IN " +
                "('children_training','summer_camp','winter_camp','specialist_training','school_community'));");

            migrationBuilder.Sql(
                "ALTER TABLE sessions ADD CONSTRAINT CK_sessions_status CHECK (status IN (N'開放',N'額滿',N'候補',N'已結束'));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE sessions DROP CONSTRAINT CK_sessions_status;");
            migrationBuilder.Sql("ALTER TABLE programs DROP CONSTRAINT CK_programs_program_type;");
            migrationBuilder.Sql("ALTER TABLE programs DROP CONSTRAINT CK_programs_status;");
        }
    }
}
