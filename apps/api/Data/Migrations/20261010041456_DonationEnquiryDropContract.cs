using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <summary>
    /// 移除「捐助洽詢」表單種類（收縮，contract；使用者 2026-10-09 裁決，主站規劃書 v3.25）：刪除 form_code = 'donation_enquiry' 的收件資料與表單定義
    /// （欄位與欄位文字由 ON DELETE CASCADE 帶走）。<b>只刪資料、不改結構</b>；Down 無法還原（資料已刪）。
    /// 與 <c>db/migrations/20261010_donation-enquiry-drop_2-contract.sql</c> 同源（冪等）；新版 api 已上線後才可套用。
    /// </summary>
    public partial class DonationEnquiryDropContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE a FROM enquiry_answers a JOIN enquiries e ON e.id = a.enquiry_id JOIN forms f ON f.id = e.form_id WHERE f.form_code = N'donation_enquiry';
DELETE e FROM enquiries e JOIN forms f ON f.id = e.form_id WHERE f.form_code = N'donation_enquiry';
DELETE FROM forms WHERE form_code = N'donation_enquiry';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 資料刪除無法還原（捐助洽詢整個拿掉）；刻意留空。
        }
    }
}
