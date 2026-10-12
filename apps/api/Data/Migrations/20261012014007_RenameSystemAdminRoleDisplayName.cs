using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameSystemAdminRoleDisplayName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 參考資料遷移（不是結構變更，屬 expand 安全類）：角色 system_admin 的顯示名稱改為「全模組管理」。
            // 原因：帳號上的 is_super_admin 開關才是「系統管理員」（含 sysadmin_only 權限），同名角色拿不到那些權限，名稱重疊易誤導。
            // 角色代碼不變；只動 code = 'system_admin' 那一列，且已是目標值時不寫入（冪等）。
            // 與 generate-*-seed-sql.py、db/prod/*-reference-data.sql 是同一份定義。
            migrationBuilder.Sql(@"
UPDATE admin_roles SET name_zh = N'全模組管理', name_en = N'All-Module Manager'
WHERE code = N'system_admin' AND (name_zh <> N'全模組管理' OR ISNULL(name_en, N'') <> N'All-Module Manager');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE admin_roles SET name_zh = N'系統管理員', name_en = N'System Administrator'
WHERE code = N'system_admin' AND (name_zh <> N'系統管理員' OR ISNULL(name_en, N'') <> N'System Administrator');");
        }
    }
}
