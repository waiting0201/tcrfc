using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.CharityPlatform.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCh4Ch5Permissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 參考資料遷移（不是結構變更）：CH-4／CH-5 新增三個權限碼。已經建好的庫（本機或日後的正式庫）只能靠 migration 取得它們——
            // db/seed/generate-charity-seed-sql.py 的 EXTRA_PERMISSIONS 與 db/prod/charity-reference-data.sql 是新建庫用的同一份定義（id 為同一組決定性 UUID）。
            // 全部以「業務自然鍵不存在才新增」守住，兩條路徑誰先到都不會重複。
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM permissions WHERE code = N'n4.settlement.mark_paid')
    INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, name_zh, name_en, is_restricted, sysadmin_only)
    VALUES (N'e1817147-678e-5508-a55f-54ee2f8fb349', N'n4.settlement.mark_paid', N'N', N'N4', N'settlement', N'execute', N'登記結算單已付款', N'Register Settlement Payment', 1, 0);");
            migrationBuilder.Sql(@"
INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
SELECT r.id, p.id, NULL FROM admin_roles r CROSS JOIN permissions p
WHERE r.code = N'system_admin' AND p.code = N'n4.settlement.mark_paid'
  AND NOT EXISTS (SELECT 1 FROM role_permissions rp WHERE rp.admin_role_id = r.id AND rp.permission_id = p.id);");
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM permissions WHERE code = N'n7.audit_log.view')
    INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, name_zh, name_en, is_restricted, sysadmin_only)
    VALUES (N'fc447748-8d4f-5748-bf07-a77381e1944b', N'n7.audit_log.view', N'N', N'N7', N'audit_log', N'view', N'檢視稽核紀錄', N'View Audit Logs', 1, 1);");
            migrationBuilder.Sql(@"
INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
SELECT r.id, p.id, NULL FROM admin_roles r CROSS JOIN permissions p
WHERE r.code = N'system_admin' AND p.code = N'n7.audit_log.view'
  AND NOT EXISTS (SELECT 1 FROM role_permissions rp WHERE rp.admin_role_id = r.id AND rp.permission_id = p.id);");
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM permissions WHERE code = N'n3.donation.hide_credit')
    INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, name_zh, name_en, is_restricted, sysadmin_only)
    VALUES (N'cc00616a-e768-54a0-8ce8-1a765f4a3aba', N'n3.donation.hide_credit', N'N', N'N3', N'donation', N'execute', N'隱藏或恢復徵信名單顯示', N'Hide Donor From Credit List', 0, 0);");
            migrationBuilder.Sql(@"
INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
SELECT r.id, p.id, NULL FROM admin_roles r CROSS JOIN permissions p
WHERE r.code = N'system_admin' AND p.code = N'n3.donation.hide_credit'
  AND NOT EXISTS (SELECT 1 FROM role_permissions rp WHERE rp.admin_role_id = r.id AND rp.permission_id = p.id);");
            migrationBuilder.Sql(@"
INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
SELECT r.id, p.id, NULL FROM admin_roles r CROSS JOIN permissions p
WHERE r.code = N'customer_service_admin' AND p.code = N'n3.donation.hide_credit'
  AND NOT EXISTS (SELECT 1 FROM role_permissions rp WHERE rp.admin_role_id = r.id AND rp.permission_id = p.id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 刻意不移除：權限碼可能已被角色指派，移除參考資料會讓既有授權悄悄失效。
        }
    }
}
