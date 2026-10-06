using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <summary>
    /// 稽核 D 類（雙語缺口）收縮（contract）：刪除已搬到側表的舊主表欄位
    /// （<c>standings.team_name</c>、<c>achievements.competition_name／placing</c>、<c>programs.audience</c>、
    /// <c>membership_plans.mid_season_rule</c>、<c>proposals.title</c>）。
    /// 🔴 <b>收縮型</b>：前提是 <c>DBilingualGapsExpand</c> 已套用、且新版 api（讀寫側表）已部署並驗證——舊版 api 仍 SELECT 這些欄位，
    /// 先套用會 500。刪除前逐表核對「舊欄位有值的列，zh-Hant 側表列有同樣的值」，對不上就 THROW 整批回滾。走 <c>production-db</c> 核准關卡。
    /// <c>Down</c> 把欄位加回並由 zh-Hant 側表回填（名次／適合對象受舊長度 32 限制而截斷）。
    /// SQL 與 <c>db/migrations/20261006_d-bilingual-gaps_2-contract.sql</c> 同源（冪等）。
    /// </summary>
    public partial class DBilingualGapsContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'standings', N'team_name') IS NOT NULL
BEGIN
  EXEC(N'IF EXISTS (SELECT 1 FROM standings s LEFT JOIN standings_i18n i ON i.standing_id = s.id AND i.locale = N''zh-Hant''
                    WHERE i.standing_id IS NULL OR ISNULL(i.team_name, N'''') <> ISNULL(s.team_name, N''''))
           THROW 50011, N''standings.team_name 與 standings_i18n(zh-Hant) 不一致，中止'', 1;');
  EXEC(N'ALTER TABLE standings DROP COLUMN team_name;');
END;

IF COL_LENGTH(N'achievements', N'competition_name') IS NOT NULL
BEGIN
  EXEC(N'IF EXISTS (SELECT 1 FROM achievements a LEFT JOIN achievements_i18n i ON i.achievement_id = a.id AND i.locale = N''zh-Hant''
                    WHERE i.achievement_id IS NULL
                       OR ISNULL(i.competition_name, N'''') <> ISNULL(a.competition_name, N'''')
                       OR ISNULL(i.placing, N'''') <> ISNULL(a.placing, N''''))
           THROW 50012, N''achievements 與 achievements_i18n(zh-Hant) 不一致，中止'', 1;');
  EXEC(N'ALTER TABLE achievements DROP COLUMN competition_name, placing;');
END;

IF COL_LENGTH(N'programs', N'audience') IS NOT NULL
BEGIN
  EXEC(N'IF EXISTS (SELECT 1 FROM programs p LEFT JOIN programs_i18n i ON i.program_id = p.id AND i.locale = N''zh-Hant''
                    WHERE ISNULL(i.audience, N'''') <> ISNULL(p.audience, N''''))
           THROW 50013, N''programs.audience 與 programs_i18n(zh-Hant) 不一致，中止'', 1;');
  EXEC(N'ALTER TABLE programs DROP COLUMN audience;');
END;

IF COL_LENGTH(N'membership_plans', N'mid_season_rule') IS NOT NULL
BEGIN
  EXEC(N'IF EXISTS (SELECT 1 FROM membership_plans m LEFT JOIN membership_plans_i18n i ON i.membership_plan_id = m.id AND i.locale = N''zh-Hant''
                    WHERE ISNULL(i.mid_season_rule, N'''') <> ISNULL(m.mid_season_rule, N''''))
           THROW 50014, N''membership_plans.mid_season_rule 與 membership_plans_i18n(zh-Hant) 不一致，中止'', 1;');
  EXEC(N'ALTER TABLE membership_plans DROP COLUMN mid_season_rule;');
END;

IF COL_LENGTH(N'proposals', N'title') IS NOT NULL
BEGIN
  EXEC(N'IF EXISTS (SELECT 1 FROM proposals p LEFT JOIN proposals_i18n i ON i.proposal_id = p.id AND i.locale = N''zh-Hant''
                    WHERE i.proposal_id IS NULL OR ISNULL(i.title, N'''') <> ISNULL(p.title, N''''))
           THROW 50015, N''proposals.title 與 proposals_i18n(zh-Hant) 不一致，中止'', 1;');
  EXEC(N'ALTER TABLE proposals DROP COLUMN title;');
END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'standings', N'team_name') IS NULL
BEGIN
    ALTER TABLE [standings] ADD [team_name] nvarchar(128) NULL;
    EXEC(N'UPDATE s SET s.team_name = ISNULL(i.team_name, N'''') FROM standings s LEFT JOIN standings_i18n i ON i.standing_id = s.id AND i.locale = N''zh-Hant'';');
    EXEC(N'ALTER TABLE standings ALTER COLUMN team_name nvarchar(128) NOT NULL;');
END;
IF COL_LENGTH(N'achievements', N'competition_name') IS NULL
BEGIN
    ALTER TABLE [achievements] ADD [competition_name] nvarchar(128) NULL, [placing] nvarchar(32) NULL;
    EXEC(N'UPDATE a SET a.competition_name = i.competition_name, a.placing = LEFT(i.placing, 32) FROM achievements a JOIN achievements_i18n i ON i.achievement_id = a.id AND i.locale = N''zh-Hant'';');
END;
IF COL_LENGTH(N'programs', N'audience') IS NULL
BEGIN
    ALTER TABLE [programs] ADD [audience] nvarchar(32) NULL;
    EXEC(N'UPDATE p SET p.audience = LEFT(i.audience, 32) FROM programs p JOIN programs_i18n i ON i.program_id = p.id AND i.locale = N''zh-Hant'';');
END;
IF COL_LENGTH(N'membership_plans', N'mid_season_rule') IS NULL
BEGIN
    ALTER TABLE [membership_plans] ADD [mid_season_rule] nvarchar(255) NULL;
    EXEC(N'UPDATE m SET m.mid_season_rule = i.mid_season_rule FROM membership_plans m JOIN membership_plans_i18n i ON i.membership_plan_id = m.id AND i.locale = N''zh-Hant'';');
END;
IF COL_LENGTH(N'proposals', N'title') IS NULL
BEGIN
    ALTER TABLE [proposals] ADD [title] nvarchar(128) NULL;
    EXEC(N'UPDATE p SET p.title = ISNULL(i.title, N'''') FROM proposals p LEFT JOIN proposals_i18n i ON i.proposal_id = p.id AND i.locale = N''zh-Hant'';');
    EXEC(N'ALTER TABLE proposals ALTER COLUMN title nvarchar(128) NOT NULL;');
END;");
        }
    }
}
