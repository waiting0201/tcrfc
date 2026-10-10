using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <summary>
    /// 報名與詢問留存隱私同意紀錄（展開，expand；主站規劃書 v3.25）：registrations、enquiries 各加 privacy_consented_at（datetime2(3)，UTC）與
    /// privacy_policy_version（nvarchar(50)），純新增可為空欄位。SQL 與 <c>db/migrations/20261010_privacy-consent_1-expand.sql</c> 同源（冪等）。
    /// 必須先 migrate 再 deploy（E-289）。
    /// </summary>
    public partial class ClubPrivacyConsentExpand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'registrations', N'privacy_consented_at') IS NULL EXEC(N'ALTER TABLE registrations ADD privacy_consented_at datetime2(3) NULL;');
IF COL_LENGTH(N'registrations', N'privacy_policy_version') IS NULL EXEC(N'ALTER TABLE registrations ADD privacy_policy_version nvarchar(50) NULL;');
IF COL_LENGTH(N'enquiries', N'privacy_consented_at') IS NULL EXEC(N'ALTER TABLE enquiries ADD privacy_consented_at datetime2(3) NULL;');
IF COL_LENGTH(N'enquiries', N'privacy_policy_version') IS NULL EXEC(N'ALTER TABLE enquiries ADD privacy_policy_version nvarchar(50) NULL;');
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "privacy_consented_at",
                table: "registrations");

            migrationBuilder.DropColumn(
                name: "privacy_policy_version",
                table: "registrations");

            migrationBuilder.DropColumn(
                name: "privacy_consented_at",
                table: "enquiries");

            migrationBuilder.DropColumn(
                name: "privacy_policy_version",
                table: "enquiries");
        }
    }
}
