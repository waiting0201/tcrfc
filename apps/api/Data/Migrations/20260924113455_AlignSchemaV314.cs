using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// 主站規劃書 v3.14（2026-09-24 拍板，commit 2b439bd 已改 db/club-schema.sql）落到程式：
    /// ① 新增 <c>banners.status</c>（草稿／發布，預設 <c>draft</c>）；② 補齊
    /// <c>matches.status</c> 的 <c>CK_matches_status</c>（五值，含 v3.14 新增的 <c>cancelled</c>）
    /// ——這欄位本身在此之前就存在，只是從來沒有任何 CHECK 約束。
    ///
    /// 🔴 兩個 CHECK 約束都是全新加上去的，不是「收斂既有 CHECK」（跟 AlignSchemaS17a 收斂 7 張表
    /// 既有 status CHECK 那種要先動態查詢系統產生名稱再 DROP 的情況不同）：<c>banners.status</c>
    /// 是這支 migration 自己新增的欄位，套用當下當然還沒有任何 CHECK；<c>matches.status</c> 欄位
    /// 本身早就存在（db/club-schema.sql 原文：「status 本身沒有 CHECK 約束」），只是從沒被約束過，
    /// 直接 ADD CONSTRAINT 即可，不需要先 DROP 任何東西。
    ///
    /// 套用前已用 SQL 查證 tcrfc_club_dev（apps/api/README.md 本節「既有資料檢查」）：
    /// <c>matches.status</c> 目前只有 <c>scheduled</c>（21 筆）／<c>played</c>（21 筆）兩種值，
    /// 全部落在五值範圍內；<c>banners</c> 目前 0 筆。**兩張表都不需要搭配任何 DML 轉態**——
    /// 這支 migration 純粹是 DDL 變更。
    /// </summary>
    public partial class AlignSchemaV314 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "banners",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "draft");

            // 🔴 CHECK 約束本專案既有慣例完全不用 EF HasCheckConstraint 建模，一律手寫 SQL
            // （見 AlignSchemaS17a 檔頭說明），這裡延續同一種分工。
            migrationBuilder.Sql(
                "ALTER TABLE banners ADD CONSTRAINT CK_banners_status CHECK (status IN ('draft','published'));");

            migrationBuilder.Sql(
                "ALTER TABLE matches ADD CONSTRAINT CK_matches_status " +
                "CHECK (status IN ('scheduled','live','played','postponed','cancelled'));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE matches DROP CONSTRAINT CK_matches_status;");
            migrationBuilder.Sql("ALTER TABLE banners DROP CONSTRAINT CK_banners_status;");

            migrationBuilder.DropColumn(
                name: "status",
                table: "banners");
        }
    }
}
