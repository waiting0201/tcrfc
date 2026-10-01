using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <summary>
    /// 索引對齊 DDL（STATUS B-12、docs/20 §5「DDL 是否等於所有 migration 套用後」差異清單 1、2）。
    /// 🔴 <b>正式庫是用 <c>db/club-schema.sql</c> 建的，不是靠 migration 長出來的</b>（<c>prod-db-init.sh</c> 把所有
    /// migration 直接寫進歷史表），而 DDL 裡 5 個 <c>UNIQUE (club_id, slug)</c> 是<b>約束</b>——對它們
    /// <c>DropIndex</c> 會失敗。所以這支 migration 的每一步都「先查現況、不符才動」（冪等）：
    /// 在 DDL 建的庫上是無操作；在 EF 模型建的庫（本機實驗、舊開發庫）上才會真的修正。
    /// 目標狀態＝DDL：
    ///   1. 五個 <c>UQ_*_club_slug</c> 無篩選（EF 預設的 <c>WHERE club_id IS NOT NULL</c> 拿掉）。
    ///   2. <c>UQ_form_fields_one_summary_per_form</c>（<c>WHERE is_summary = 1</c>）、<c>IX_registrations_trial_status</c> 存在。
    ///   3. <c>IX_form_fields_i18n_locale</c>、<c>IX_sponsor_activations_i18n_locale</c> 存在（docs/12b §11.2「所有 *_i18n 建 (locale) 索引」有寫，原 DDL 漏了；本批同步補進 DDL，後者 EF 也原本沒有）。
    /// Down 刻意無操作：這些物件屬於 DDL 基準，還原成舊 EF 狀態會讓正式庫偏離 DDL。
    /// </summary>
    public partial class AlignIndexesWithDdl2 : Migration
    {
        private static readonly (string Table, string Index)[] SlugUniques =
        [
            ("charities", "UQ_charities_club_slug"),
            ("charity_programs", "UQ_charity_programs_club_slug"),
            ("faqs", "UQ_faqs_club_slug"),
            ("partner_stores", "UQ_partner_stores_club_slug"),
            ("press_resources", "UQ_press_resources_club_slug"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, index) in SlugUniques)
            {
                // 不存在 → 建；存在但帶篩選（EF 建的庫）→ 重建成無篩選；存在且無篩選（DDL 的 UNIQUE 約束）→ 不動。
                migrationBuilder.Sql($@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'{index}' AND object_id = OBJECT_ID(N'{table}'))
    CREATE UNIQUE INDEX [{index}] ON [{table}] (club_id, slug);
ELSE IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'{index}' AND object_id = OBJECT_ID(N'{table}') AND has_filter = 1)
BEGIN
    DROP INDEX [{index}] ON [{table}];
    CREATE UNIQUE INDEX [{index}] ON [{table}] (club_id, slug);
END");
            }

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_form_fields_one_summary_per_form' AND object_id = OBJECT_ID(N'form_fields'))
    CREATE UNIQUE INDEX UQ_form_fields_one_summary_per_form ON form_fields (form_id) WHERE is_summary = 1;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_registrations_trial_status' AND object_id = OBJECT_ID(N'registrations'))
    CREATE INDEX IX_registrations_trial_status ON registrations (trial_id, status);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_form_fields_i18n_locale' AND object_id = OBJECT_ID(N'form_fields_i18n'))
    CREATE INDEX IX_form_fields_i18n_locale ON form_fields_i18n (locale);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_sponsor_activations_i18n_locale' AND object_id = OBJECT_ID(N'sponsor_activations_i18n'))
    CREATE INDEX IX_sponsor_activations_i18n_locale ON sponsor_activations_i18n (locale);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 刻意無操作，理由見類別說明：這些索引是 DDL 基準的一部分，正式庫不該回到舊的 EF 狀態。
        }
    }
}
