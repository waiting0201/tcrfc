/* ============================================================================
   admin_user_teams 刪表、role_permissions.scope_type 移除 own_teams、刪 system.team_grant.* 權限碼（contract）
   2026-10-08｜依據：使用者裁決——後台帳號的「球隊授權」不需要，整個移除。

   🔴 先確認新版 api 已上線（EF 已無 AdminUserTeam、已無 /accounts/{id}/team-grants 與全域 GET /api/v1/admin/teams 端點）再跑：
      舊版 api 仍會查 admin_user_teams，跑了會立刻 500。
   🔴 刪表無法找回（PITR 只有 7 天，docs/20 §5）。
   🔴 冪等：表不存在就跳過；CHECK 已不含 own_teams 就不重建。
   🔴 role_permissions 殘留的 own_teams 列直接刪除（fail-closed，不轉 all 以免擴大範圍；預期為 0 列）。
   🔴 role_permissions.scope_type 的 CHECK 在 DDL 建的庫上是匿名自動命名，依「掛在該欄位上的 CHECK」動態查名稱。
   與 apps/api/Data/Migrations 的 AdminUserTeamsDropContract 遷移 同源。
   執行：sqlcmd -S <server> -d tcrfc_club -b -I -i db/migrations/20261008_admin-user-teams-drop_2-contract.sql
   ============================================================================ */
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DELETE FROM role_permissions WHERE scope_type = N'own_teams';

DECLARE @ck sysname;
SELECT @ck = cc.name FROM sys.check_constraints cc
JOIN sys.columns c ON c.object_id = cc.parent_object_id AND c.column_id = cc.parent_column_id
WHERE cc.parent_object_id = OBJECT_ID(N'role_permissions') AND c.name = N'scope_type';
IF @ck IS NULL OR EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = @ck AND definition LIKE N'%own_teams%')
BEGIN
    IF @ck IS NOT NULL
    BEGIN
        DECLARE @drop nvarchar(400) = N'ALTER TABLE [role_permissions] DROP CONSTRAINT ' + QUOTENAME(@ck);
        EXEC(@drop);
    END
    EXEC(N'ALTER TABLE [role_permissions] ADD CONSTRAINT [CK_role_permissions_scope_type] CHECK ([scope_type] IN (''all'',''academy_only'',''masked'',''translate_only'',''own_clubs''))');
END

IF OBJECT_ID(N'admin_user_teams', N'U') IS NOT NULL DROP TABLE admin_user_teams;

-- role_permissions 以 FK ON DELETE CASCADE 隨之移除。
DELETE FROM permissions WHERE code IN (N'system.team_grant.view', N'system.team_grant.update');

COMMIT TRANSACTION;
