using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <summary>
    /// 移除後台帳號的「球隊授權」（收縮，contract）：使用者裁決（2026-10-08）——不需要球隊授權，整個移除。
    /// ① <c>role_permissions.scope_type</c> 值 <c>own_teams</c> 的殘留列刪除（fail-closed：寧可少給權限，也不轉成 <c>all</c> 擴大範圍；種子與正式庫本來就沒有）；
    /// ② 重建 <c>scope_type</c> 的 CHECK，值域不含 <c>own_teams</c>（DDL 建的庫上該 CHECK 是匿名自動命名，依「掛在這個欄位上的 CHECK」動態查名稱再拆，docs/20 §5 注意事項 3）；
    /// ③ 刪表 <c>admin_user_teams</c>（兩條外鍵隨表移除）；④ 刪權限碼 <c>system.team_grant.view／update</c>（<c>role_permissions</c> 由外鍵連動刪除）。
    /// 🔴 <b>收縮型</b>：新版 api（EF 已無 <c>AdminUserTeam</c>、無 <c>/team-grants</c> 與全域 <c>GET /admin/teams</c> 端點）部署並驗證後才可套用（走 <c>production-db</c> 核准關卡）；
    /// 舊版 api 仍會查 <c>admin_user_teams</c>，先套用會 500。刪表無法找回（PITR 只有 7 天）。<c>Down</c> 只還原表結構與舊 CHECK，不還原內容與權限碼。
    /// SQL 與 <c>db/migrations/20261008_admin-user-teams-drop_2-contract.sql</c> 同源（冪等）。
    /// </summary>
    public partial class AdminUserTeamsDropContract : Migration
    {
        private const string ReplaceScopeCheckWithoutOwnTeams = @"
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
END";

        private const string ReplaceScopeCheckWithOwnTeams = @"
DECLARE @ck sysname;
SELECT @ck = cc.name FROM sys.check_constraints cc
JOIN sys.columns c ON c.object_id = cc.parent_object_id AND c.column_id = cc.parent_column_id
WHERE cc.parent_object_id = OBJECT_ID(N'role_permissions') AND c.name = N'scope_type';
IF @ck IS NULL OR EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = @ck AND definition NOT LIKE N'%own_teams%')
BEGIN
    IF @ck IS NOT NULL
    BEGIN
        DECLARE @drop nvarchar(400) = N'ALTER TABLE [role_permissions] DROP CONSTRAINT ' + QUOTENAME(@ck);
        EXEC(@drop);
    END
    EXEC(N'ALTER TABLE [role_permissions] ADD CONSTRAINT [CK_role_permissions_scope_type] CHECK ([scope_type] IN (''all'',''own_teams'',''academy_only'',''masked'',''translate_only'',''own_clubs''))');
END";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM role_permissions WHERE scope_type = N'own_teams';");
            migrationBuilder.Sql(ReplaceScopeCheckWithoutOwnTeams);
            migrationBuilder.Sql("IF OBJECT_ID(N'admin_user_teams', N'U') IS NOT NULL DROP TABLE admin_user_teams;");
            migrationBuilder.Sql("DELETE FROM permissions WHERE code IN (N'system.team_grant.view', N'system.team_grant.update');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'admin_user_teams', N'U') IS NULL
BEGIN
    CREATE TABLE [admin_user_teams] (
        [admin_user_id] uniqueidentifier NOT NULL,
        [team_id]       uniqueidentifier NOT NULL,
        [expires_on]    date NULL,
        [is_active]     bit NOT NULL CONSTRAINT [DF_admin_user_teams_is_active] DEFAULT 1,
        CONSTRAINT [PK_admin_user_teams] PRIMARY KEY CLUSTERED ([admin_user_id], [team_id]),
        CONSTRAINT [FK_admin_user_teams_user] FOREIGN KEY ([admin_user_id]) REFERENCES [admin_users]([id]) ON DELETE CASCADE,
        CONSTRAINT [FK_admin_user_teams_team] FOREIGN KEY ([team_id]) REFERENCES [teams]([id])
    );
END");
            migrationBuilder.Sql(ReplaceScopeCheckWithOwnTeams);
        }
    }
}
