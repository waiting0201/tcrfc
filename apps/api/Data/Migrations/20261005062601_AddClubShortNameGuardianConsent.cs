using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <summary>
    /// 2026-10-05 第四批 App 契約補強的三個結構變更（docs/12、12a、12b、12c 已先改；對照 <c>db/club-schema.sql</c>）：
    /// ① <c>clubs_i18n.short_name</c>：俱樂部簡稱（磐石「台中磐石」／「Taichung Rock FC」、藍鯨「台中藍鯨」；藍鯨英文一律空，B-5）。
    /// ② <c>members.guardian_*</c> 四欄：未滿 18 歲註冊的監護人同意紀錄（主站規劃書「會員資料安全要求」、App 規劃書 §4.5）。
    /// ③ <c>partner_stores</c> 座標 CHECK：NULL＝未確認，有值必須成對、在範圍內、不得是 (0,0) 替代值；既有不合規的列先清成 NULL（未確認）。
    /// 🔴 每一步先查現況再動（冪等）：正式庫是用 <c>db/club-schema.sql</c> 建的（新建庫已含這些欄位與約束），本機庫與舊庫則沒有；
    /// 剛加的欄位在同一批次內引用會編譯失敗，所以後續陳述式以 EXEC 延後解析。
    /// </summary>
    public partial class AddClubShortNameGuardianConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ① 簡稱＋回填（依俱樂部代碼與語系；已有值不覆蓋）
            migrationBuilder.Sql("IF COL_LENGTH(N'clubs_i18n', N'short_name') IS NULL ALTER TABLE [clubs_i18n] ADD [short_name] nvarchar(32) NULL;");
            migrationBuilder.Sql(@"
EXEC(N'
UPDATE ci SET short_name = v.short_name
FROM clubs_i18n ci
JOIN clubs c ON c.id = ci.club_id
JOIN (VALUES (N''tcrfc'', N''zh-Hant'', N''台中磐石''), (N''tcrfc'', N''en'', N''Taichung Rock FC''), (N''bw'', N''zh-Hant'', N''台中藍鯨'')) AS v(club_code, locale, short_name)
  ON v.club_code = c.code AND v.locale = ci.locale
WHERE ci.short_name IS NULL;');");

            // ② 監護人同意
            migrationBuilder.Sql("IF COL_LENGTH(N'members', N'guardian_consented_at') IS NULL ALTER TABLE [members] ADD [guardian_consented_at] datetime2(3) NULL;");
            migrationBuilder.Sql("IF COL_LENGTH(N'members', N'guardian_name') IS NULL ALTER TABLE [members] ADD [guardian_name] nvarchar(64) NULL;");
            migrationBuilder.Sql("IF COL_LENGTH(N'members', N'guardian_relationship') IS NULL ALTER TABLE [members] ADD [guardian_relationship] nvarchar(16) NULL CONSTRAINT [CK_members_guardian_relationship] CHECK ([guardian_relationship] IN ('parent','legal_guardian'));");
            migrationBuilder.Sql("IF COL_LENGTH(N'members', N'guardian_consent_version') IS NULL ALTER TABLE [members] ADD [guardian_consent_version] nvarchar(32) NULL;");
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'members') AND name = N'CK_members_guardian_consent')
    EXEC(N'ALTER TABLE [members] ADD CONSTRAINT [CK_members_guardian_consent] CHECK ([guardian_consented_at] IS NOT NULL OR ([guardian_name] IS NULL AND [guardian_relationship] IS NULL AND [guardian_consent_version] IS NULL))');");

            // ③ 店家座標：不合規的既有值視為「未確認」（NULL），再加約束
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'partner_stores') AND name = N'CK_partner_stores_coords')
BEGIN
    EXEC(N'UPDATE [partner_stores] SET [lat] = NULL, [lng] = NULL WHERE ([lat] IS NULL AND [lng] IS NOT NULL) OR ([lat] IS NOT NULL AND [lng] IS NULL) OR ([lat] = 0 AND [lng] = 0) OR [lat] NOT BETWEEN -90 AND 90 OR [lng] NOT BETWEEN -180 AND 180');
    EXEC(N'ALTER TABLE [partner_stores] ADD CONSTRAINT [CK_partner_stores_coords] CHECK (([lat] IS NULL AND [lng] IS NULL) OR ([lat] IS NOT NULL AND [lng] IS NOT NULL AND [lat] BETWEEN -90 AND 90 AND [lng] BETWEEN -180 AND 180 AND NOT ([lat] = 0 AND [lng] = 0)))');
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'partner_stores') AND name = N'CK_partner_stores_coords') ALTER TABLE [partner_stores] DROP CONSTRAINT [CK_partner_stores_coords];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'members') AND name = N'CK_members_guardian_consent') ALTER TABLE [members] DROP CONSTRAINT [CK_members_guardian_consent];");
            // 值域 CHECK 在 DDL 建的庫上是匿名（系統自動命名），在本 migration 建的庫上叫 CK_members_guardian_relationship——
            // 依「掛在這個欄位上的 CHECK」查名稱再刪，兩種來源都適用（MigrationsOnBlankDatabaseTests 的回滾步驟逮到過這個差異）。
            migrationBuilder.Sql(@"
DECLARE @ck sysname;
SELECT @ck = cc.name FROM sys.check_constraints cc
JOIN sys.columns c ON c.object_id = cc.parent_object_id AND c.column_id = cc.parent_column_id
WHERE cc.parent_object_id = OBJECT_ID(N'members') AND c.name = N'guardian_relationship';
IF @ck IS NOT NULL
    BEGIN
        DECLARE @sql nvarchar(400) = N'ALTER TABLE [members] DROP CONSTRAINT ' + QUOTENAME(@ck);
        EXEC(@sql);
    END");
            migrationBuilder.Sql("IF COL_LENGTH(N'members', N'guardian_consent_version') IS NOT NULL ALTER TABLE [members] DROP COLUMN [guardian_consent_version];");
            migrationBuilder.Sql("IF COL_LENGTH(N'members', N'guardian_relationship') IS NOT NULL ALTER TABLE [members] DROP COLUMN [guardian_relationship];");
            migrationBuilder.Sql("IF COL_LENGTH(N'members', N'guardian_name') IS NOT NULL ALTER TABLE [members] DROP COLUMN [guardian_name];");
            migrationBuilder.Sql("IF COL_LENGTH(N'members', N'guardian_consented_at') IS NOT NULL ALTER TABLE [members] DROP COLUMN [guardian_consented_at];");
            migrationBuilder.Sql("IF COL_LENGTH(N'clubs_i18n', N'short_name') IS NOT NULL ALTER TABLE [clubs_i18n] DROP COLUMN [short_name];");
        }
    }
}
