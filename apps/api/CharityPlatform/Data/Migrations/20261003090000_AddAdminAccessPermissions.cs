using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.CharityPlatform.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminAccessPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 參考資料遷移（不是結構變更）：後台帳號與角色管理的四個權限碼（全部 sysadmin_only，只掛給 system_admin 角色）。
            // 與 db/seed/generate-charity-seed-sql.py 的 EXTRA_PERMISSIONS、db/prod/charity-reference-data.sql 是同一份定義（同一組決定性 UUID），
            // 以「自然鍵不存在才新增」守住，兩條路徑誰先到都不會重複。
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM permissions WHERE code = N'n7.admin_account.view')
    INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, name_zh, name_en, is_restricted, sysadmin_only)
    VALUES (N'f041a6f1-064d-534e-a1ec-83bf9d78e4f8', N'n7.admin_account.view', N'N', N'N7', N'admin_account', N'view', N'檢視後台帳號', N'View Admin Accounts', 1, 1);");
            migrationBuilder.Sql(@"
INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
SELECT r.id, p.id, NULL FROM admin_roles r CROSS JOIN permissions p
WHERE r.code = N'system_admin' AND p.code = N'n7.admin_account.view'
  AND NOT EXISTS (SELECT 1 FROM role_permissions rp WHERE rp.admin_role_id = r.id AND rp.permission_id = p.id);");
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM permissions WHERE code = N'n7.admin_account.manage')
    INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, name_zh, name_en, is_restricted, sysadmin_only)
    VALUES (N'09f486c6-2641-519f-90e2-59a0d1e8c46b', N'n7.admin_account.manage', N'N', N'N7', N'admin_account', N'update', N'管理後台帳號', N'Manage Admin Accounts', 1, 1);");
            migrationBuilder.Sql(@"
INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
SELECT r.id, p.id, NULL FROM admin_roles r CROSS JOIN permissions p
WHERE r.code = N'system_admin' AND p.code = N'n7.admin_account.manage'
  AND NOT EXISTS (SELECT 1 FROM role_permissions rp WHERE rp.admin_role_id = r.id AND rp.permission_id = p.id);");
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM permissions WHERE code = N'n7.admin_role.view')
    INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, name_zh, name_en, is_restricted, sysadmin_only)
    VALUES (N'29c61d0d-48a9-567e-a957-aff843c4a462', N'n7.admin_role.view', N'N', N'N7', N'admin_role', N'view', N'檢視角色與權限', N'View Roles & Permissions', 1, 1);");
            migrationBuilder.Sql(@"
INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
SELECT r.id, p.id, NULL FROM admin_roles r CROSS JOIN permissions p
WHERE r.code = N'system_admin' AND p.code = N'n7.admin_role.view'
  AND NOT EXISTS (SELECT 1 FROM role_permissions rp WHERE rp.admin_role_id = r.id AND rp.permission_id = p.id);");
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM permissions WHERE code = N'n7.admin_role.manage')
    INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, name_zh, name_en, is_restricted, sysadmin_only)
    VALUES (N'0382bb73-0515-54ae-8873-e11a8728ca56', N'n7.admin_role.manage', N'N', N'N7', N'admin_role', N'update', N'管理角色與權限', N'Manage Roles & Permissions', 1, 1);");
            migrationBuilder.Sql(@"
INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
SELECT r.id, p.id, NULL FROM admin_roles r CROSS JOIN permissions p
WHERE r.code = N'system_admin' AND p.code = N'n7.admin_role.manage'
  AND NOT EXISTS (SELECT 1 FROM role_permissions rp WHERE rp.admin_role_id = r.id AND rp.permission_id = p.id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 刻意不移除：權限碼可能已被角色指派，移除參考資料會讓既有授權悄悄失效。
        }
    }
}
