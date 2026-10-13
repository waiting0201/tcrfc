using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCorporateCtaSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 資料遷移（不是結構變更，屬 expand 安全類）：主站規劃書 v3.27 起，後台 B5 不再提供「企業合作連結／文案」，
            // 11 章企業合作 CTA 由前台版型固定導向 10.5。刪除設定鍵 charity.corporate_cta（逐語系）與 charity.corporate_url（單值）的所有列。
            // 先刪 settings_i18n 再刪 settings（不依賴外鍵串聯）；沒有符合列時是 no-op（冪等）。
            // 同時把 B5 權限顯示名稱改為「檢視／編輯捐款導流設定」（已是目標值時不寫入）。
            migrationBuilder.Sql(@"
DELETE si FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
WHERE s.setting_key IN (N'charity.corporate_cta', N'charity.corporate_url');
DELETE FROM settings WHERE setting_key IN (N'charity.corporate_cta', N'charity.corporate_url');
UPDATE permissions SET name_zh = N'檢視捐款導流設定' WHERE code = N'charity.setting.view' AND name_zh <> N'檢視捐款導流設定';
UPDATE permissions SET name_zh = N'編輯捐款導流設定' WHERE code = N'charity.setting.update' AND name_zh <> N'編輯捐款導流設定';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 刪除的設定值是後台填過的內容，無法還原；只還原權限顯示名稱。
            migrationBuilder.Sql(@"
UPDATE permissions SET name_zh = N'檢視捐款導流與參與方式設定' WHERE code = N'charity.setting.view' AND name_zh <> N'檢視捐款導流與參與方式設定';
UPDATE permissions SET name_zh = N'編輯捐款導流與參與方式設定' WHERE code = N'charity.setting.update' AND name_zh <> N'編輯捐款導流與參與方式設定';");
        }
    }
}
