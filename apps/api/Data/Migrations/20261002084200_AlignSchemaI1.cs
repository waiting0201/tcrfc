using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <summary>
    /// I 網站設定其餘子模組（2026-10-02，規劃書 §4.9 I2 選單／I3 全域設定／I4 多語系與字串翻譯表／I5 場地／I6 EDM 設定）：
    /// ① 結構：<c>venues</c> 補照片圖片欄位組（<c>photo_width</c>／<c>photo_height</c>）與 <c>venues_i18n.photo_alt</c>——規劃書 v3.5 §4.0「圖片欄位是一組：物件鍵、寬、高、雙語 Alt」。
    /// ② 參照資料：15 個 <c>site.*</c> 權限碼與其角色指派（系統管理員全給、翻譯人員 <c>site.string.view</c>／<c>site.string.translate</c>）。
    /// 已經建好的庫（本機、正式庫）只能靠這支 migration 取得它們；<c>db/seed/generate-club-seed-sql.py</c> 與 <c>db/prod/club-reference-data.sql</c> 是新建庫用的同一份定義
    /// （id 為同一組決定性 UUID）。全部以「業務自然鍵不存在才新增」守住，兩條路徑誰先到都不會重複（<c>docs/20</c> §5 migration 注意事項第 1 點）。
    /// 🔴 每一步先查現況再動（冪等）：正式庫是用 <c>db/club-schema.sql</c> 建的，欄位可能已存在；本機庫與舊庫則沒有。
    /// </summary>
    public partial class AlignSchemaI1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF COL_LENGTH(N'venues', N'photo_width') IS NULL ALTER TABLE [venues] ADD [photo_width] int NULL;");
            migrationBuilder.Sql("IF COL_LENGTH(N'venues', N'photo_height') IS NULL ALTER TABLE [venues] ADD [photo_height] int NULL;");
            migrationBuilder.Sql("IF COL_LENGTH(N'venues_i18n', N'photo_alt') IS NULL ALTER TABLE [venues_i18n] ADD [photo_alt] nvarchar(200) NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM permissions WHERE code = N'site.menu.view')
    INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
    VALUES (N'a03e29a5-4ebe-5121-a2cc-d57c587587a7', N'site.menu.view', N'I', N'I2', N'site', N'view', 1, 0, 1, N'檢視選單管理', N'View Menus');");
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM permissions WHERE code = N'site.menu.update')
    INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
    VALUES (N'a24f9f0e-5f38-56f1-b93b-cb83ea23d847', N'site.menu.update', N'I', N'I2', N'site', N'update', 1, 0, 1, N'編輯主選單、Mega Menu 與頁尾選單', N'Update Menus');");
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM permissions WHERE code = N'site.global.view')
    INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
    VALUES (N'dd3c5b2c-f26e-5816-aad0-382e31ad7891', N'site.global.view', N'I', N'I3', N'site', N'view', 1, 0, 1, N'檢視全域設定', N'View Global Settings');");
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM permissions WHERE code = N'site.global.update')
    INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
    VALUES (N'5363d27f-da7a-5792-ab9c-edb9e73b970b', N'site.global.update', N'I', N'I3', N'site', N'update', 1, 0, 1, N'編輯 Logo、品牌色、Favicon、政策頁與維護模式', N'Update Global Settings');");
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM permissions WHERE code = N'site.locale.view')
    INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
    VALUES (N'029afe94-560e-5378-bf8c-e84eeea0adb5', N'site.locale.view', N'I', N'I4', N'site', N'view', 1, 0, 1, N'檢視多語系設定與翻譯狀態總覽', N'View Language Settings');");
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM permissions WHERE code = N'site.locale.update')
    INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
    VALUES (N'2e7391bc-0d91-5599-b2f2-b176cef39f16', N'site.locale.update', N'I', N'I4', N'site', N'update', 1, 0, 1, N'編輯啟用語系、備援規則與日期數字格式', N'Update Language Settings');");
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM permissions WHERE code = N'site.string.view')
    INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
    VALUES (N'f770c8ec-3936-50af-87ce-dbc33bbb42ef', N'site.string.view', N'I', N'I4', N'site', N'view', 0, 0, 0, N'檢視介面字串翻譯表', N'View UI Strings');");
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM permissions WHERE code = N'site.string.update')
    INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
    VALUES (N'9cfa154e-0421-5796-a51e-dcde6d3b344f', N'site.string.update', N'I', N'I4', N'site', N'update', 0, 0, 0, N'新增、刪除與編輯介面字串（含繁中原文）', N'Update UI Strings');");
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM permissions WHERE code = N'site.string.translate')
    INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
    VALUES (N'd4e9eca5-6bc9-5885-a95f-dc4b863324c7', N'site.string.translate', N'I', N'I4', N'site', N'update', 0, 0, 0, N'翻譯介面字串（僅非預設語系）', N'Translate UI Strings');");
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM permissions WHERE code = N'site.venue.view')
    INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
    VALUES (N'd3e346b5-60fe-51ec-b1d2-0f6892a300ec', N'site.venue.view', N'I', N'I5', N'site', N'view', 0, 0, 1, N'檢視場地管理', N'View Venues');");
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM permissions WHERE code = N'site.venue.create')
    INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
    VALUES (N'96b5d373-801b-5b30-b104-5efa3f0072e3', N'site.venue.create', N'I', N'I5', N'site', N'create', 0, 0, 1, N'新增場地', N'Create Venues');");
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM permissions WHERE code = N'site.venue.update')
    INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
    VALUES (N'42e5a581-e103-5acf-9716-addca6d4c0f8', N'site.venue.update', N'I', N'I5', N'site', N'update', 0, 0, 1, N'編輯場地（地址、經緯度、交通說明、照片）', N'Update Venues');");
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM permissions WHERE code = N'site.venue.delete')
    INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
    VALUES (N'274873b2-2104-577c-b624-d0342c8980bf', N'site.venue.delete', N'I', N'I5', N'site', N'delete', 0, 0, 1, N'刪除場地', N'Delete Venues');");
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM permissions WHERE code = N'site.edm.view')
    INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
    VALUES (N'd03e7fbe-4de1-570b-ab3f-bb462329ada8', N'site.edm.view', N'I', N'I6', N'site', N'view', 1, 1, 1, N'檢視 EDM 平台設定', N'View EDM Settings');");
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM permissions WHERE code = N'site.edm.update')
    INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, is_club_scoped, is_restricted, sysadmin_only, name_zh, name_en)
    VALUES (N'4ca0536e-b18f-554b-b95a-ae2f237ebaa7', N'site.edm.update', N'I', N'I6', N'site', N'update', 1, 1, 1, N'編輯 EDM 平台設定與憑證', N'Update EDM Settings');");
            // 系統管理員全給（is_super_admin 本來就略過權限檢查，與種子腳本一致仍寫入）；翻譯人員只給字串翻譯表的檢視與翻譯。
            migrationBuilder.Sql(@"
INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
SELECT r.id, p.id, N'all' FROM admin_roles r CROSS JOIN permissions p
WHERE r.code = N'system_admin'
  AND p.code IN (N'site.menu.view', N'site.menu.update', N'site.global.view', N'site.global.update', N'site.locale.view', N'site.locale.update',
                 N'site.string.view', N'site.string.update', N'site.string.translate',
                 N'site.venue.view', N'site.venue.create', N'site.venue.update', N'site.venue.delete', N'site.edm.view', N'site.edm.update')
  AND NOT EXISTS (SELECT 1 FROM role_permissions rp WHERE rp.admin_role_id = r.id AND rp.permission_id = p.id);");
            migrationBuilder.Sql(@"
INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
SELECT r.id, p.id, N'translate_only' FROM admin_roles r CROSS JOIN permissions p
WHERE r.code = N'translator' AND p.code IN (N'site.string.view', N'site.string.translate')
  AND NOT EXISTS (SELECT 1 FROM role_permissions rp WHERE rp.admin_role_id = r.id AND rp.permission_id = p.id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 權限碼刻意不移除：可能已被角色指派，移除參考資料會讓既有授權悄悄失效（同慈善庫 AddCh4Ch5Permissions）。
            migrationBuilder.Sql("IF COL_LENGTH(N'venues_i18n', N'photo_alt') IS NOT NULL ALTER TABLE [venues_i18n] DROP COLUMN [photo_alt];");
            migrationBuilder.Sql("IF COL_LENGTH(N'venues', N'photo_height') IS NOT NULL ALTER TABLE [venues] DROP COLUMN [photo_height];");
            migrationBuilder.Sql("IF COL_LENGTH(N'venues', N'photo_width') IS NOT NULL ALTER TABLE [venues] DROP COLUMN [photo_width];");
        }
    }
}
