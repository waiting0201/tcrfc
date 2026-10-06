using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <summary>
    /// 稽核 D 類（雙語缺口）擴張（expand）：建 <c>standings_i18n</c>／<c>achievements_i18n</c>／<c>proposals_i18n</c>，
    /// <c>programs_i18n.audience</c>、<c>membership_plans_i18n.mid_season_rule</c> 補欄，並把主表既有中文值搬進 zh-Hant 列。
    /// 🔴 只加不刪：舊欄位保留，舊版 api 不受影響（docs/20 §5 展開—收縮）。收縮見 <c>DBilingualGapsContract</c>。
    /// 🔴 SQL 與 <c>db/migrations/20261006_d-bilingual-gaps_1-expand.sql</c> 同源（冪等；剛加的欄位在同批次引用以 EXEC 延後解析）。
    /// </summary>
    public partial class DBilingualGapsExpand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- ① standings_i18n
IF OBJECT_ID(N'standings_i18n', N'U') IS NULL
BEGIN
  CREATE TABLE standings_i18n (
    standing_id     uniqueidentifier NOT NULL,
    locale          nvarchar(10)     NOT NULL,
    team_name       nvarchar(128)    NULL,
    CONSTRAINT PK_standings_i18n PRIMARY KEY CLUSTERED (standing_id, locale)
  );
  CREATE INDEX IX_standings_i18n_locale ON standings_i18n (locale);
  ALTER TABLE standings_i18n ADD CONSTRAINT FK_standings_i18n_standing FOREIGN KEY (standing_id) REFERENCES standings(id) ON DELETE CASCADE;
END;
IF COL_LENGTH(N'standings', N'team_name') IS NOT NULL
  EXEC(N'INSERT INTO standings_i18n (standing_id, locale, team_name)
         SELECT s.id, N''zh-Hant'', s.team_name FROM standings s
         WHERE NOT EXISTS (SELECT 1 FROM standings_i18n i WHERE i.standing_id = s.id AND i.locale = N''zh-Hant'');');

-- ② achievements_i18n
IF OBJECT_ID(N'achievements_i18n', N'U') IS NULL
BEGIN
  CREATE TABLE achievements_i18n (
    achievement_id    uniqueidentifier NOT NULL,
    locale            nvarchar(10)     NOT NULL,
    competition_name  nvarchar(128)    NULL,
    placing           nvarchar(64)     NULL,
    CONSTRAINT PK_achievements_i18n PRIMARY KEY CLUSTERED (achievement_id, locale)
  );
  CREATE INDEX IX_achievements_i18n_locale ON achievements_i18n (locale);
  ALTER TABLE achievements_i18n ADD CONSTRAINT FK_achievements_i18n_achievement FOREIGN KEY (achievement_id) REFERENCES achievements(id) ON DELETE CASCADE;
END;
IF COL_LENGTH(N'achievements', N'competition_name') IS NOT NULL
  EXEC(N'INSERT INTO achievements_i18n (achievement_id, locale, competition_name, placing)
         SELECT a.id, N''zh-Hant'', a.competition_name, a.placing FROM achievements a
         WHERE NOT EXISTS (SELECT 1 FROM achievements_i18n i WHERE i.achievement_id = a.id AND i.locale = N''zh-Hant'');');

-- ③ programs_i18n.audience（側表早已存在；沒有 zh-Hant 列的課程不會發生——name 必存——但仍用 NOT EXISTS 保險補列）
IF COL_LENGTH(N'programs_i18n', N'audience') IS NULL
  ALTER TABLE programs_i18n ADD audience nvarchar(64) NULL;
IF COL_LENGTH(N'programs', N'audience') IS NOT NULL
BEGIN
  EXEC(N'UPDATE i SET i.audience = p.audience
         FROM programs_i18n i JOIN programs p ON p.id = i.program_id
         WHERE i.locale = N''zh-Hant'' AND i.audience IS NULL AND p.audience IS NOT NULL;');
  EXEC(N'INSERT INTO programs_i18n (program_id, locale, audience)
         SELECT p.id, N''zh-Hant'', p.audience FROM programs p
         WHERE p.audience IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM programs_i18n i WHERE i.program_id = p.id AND i.locale = N''zh-Hant'');');
END;

-- ④ membership_plans_i18n.mid_season_rule
IF COL_LENGTH(N'membership_plans_i18n', N'mid_season_rule') IS NULL
  ALTER TABLE membership_plans_i18n ADD mid_season_rule nvarchar(255) NULL;
IF COL_LENGTH(N'membership_plans', N'mid_season_rule') IS NOT NULL
BEGIN
  EXEC(N'UPDATE i SET i.mid_season_rule = m.mid_season_rule
         FROM membership_plans_i18n i JOIN membership_plans m ON m.id = i.membership_plan_id
         WHERE i.locale = N''zh-Hant'' AND i.mid_season_rule IS NULL AND m.mid_season_rule IS NOT NULL;');
  EXEC(N'INSERT INTO membership_plans_i18n (membership_plan_id, locale, mid_season_rule)
         SELECT m.id, N''zh-Hant'', m.mid_season_rule FROM membership_plans m
         WHERE m.mid_season_rule IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM membership_plans_i18n i WHERE i.membership_plan_id = m.id AND i.locale = N''zh-Hant'');');
END;

-- ⑤ proposals_i18n
IF OBJECT_ID(N'proposals_i18n', N'U') IS NULL
BEGIN
  CREATE TABLE proposals_i18n (
    proposal_id     uniqueidentifier NOT NULL,
    locale          nvarchar(10)     NOT NULL,
    title           nvarchar(128)    NULL,
    CONSTRAINT PK_proposals_i18n PRIMARY KEY CLUSTERED (proposal_id, locale)
  );
  CREATE INDEX IX_proposals_i18n_locale ON proposals_i18n (locale);
  ALTER TABLE proposals_i18n ADD CONSTRAINT FK_proposals_i18n_proposal FOREIGN KEY (proposal_id) REFERENCES proposals(id) ON DELETE CASCADE;
END;
IF COL_LENGTH(N'proposals', N'title') IS NOT NULL
  EXEC(N'INSERT INTO proposals_i18n (proposal_id, locale, title)
         SELECT p.id, N''zh-Hant'', p.title FROM proposals p
         WHERE NOT EXISTS (SELECT 1 FROM proposals_i18n i WHERE i.proposal_id = p.id AND i.locale = N''zh-Hant'');');

-- 自我核對：舊欄位仍在時，每一列都必須已有 zh-Hant 列，否則整批回滾
IF COL_LENGTH(N'standings', N'team_name') IS NOT NULL
   AND EXISTS (SELECT 1 FROM standings s WHERE NOT EXISTS (SELECT 1 FROM standings_i18n i WHERE i.standing_id = s.id AND i.locale = N'zh-Hant'))
  THROW 50001, N'standings 有列沒搬到 standings_i18n(zh-Hant)', 1;
IF COL_LENGTH(N'achievements', N'competition_name') IS NOT NULL
   AND EXISTS (SELECT 1 FROM achievements a WHERE NOT EXISTS (SELECT 1 FROM achievements_i18n i WHERE i.achievement_id = a.id AND i.locale = N'zh-Hant'))
  THROW 50002, N'achievements 有列沒搬到 achievements_i18n(zh-Hant)', 1;
IF COL_LENGTH(N'proposals', N'title') IS NOT NULL
   AND EXISTS (SELECT 1 FROM proposals p WHERE NOT EXISTS (SELECT 1 FROM proposals_i18n i WHERE i.proposal_id = p.id AND i.locale = N'zh-Hant'))
  THROW 50003, N'proposals 有列沒搬到 proposals_i18n(zh-Hant)', 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'proposals_i18n', N'U') IS NOT NULL DROP TABLE [proposals_i18n];
IF OBJECT_ID(N'achievements_i18n', N'U') IS NOT NULL DROP TABLE [achievements_i18n];
IF OBJECT_ID(N'standings_i18n', N'U') IS NOT NULL DROP TABLE [standings_i18n];
IF COL_LENGTH(N'membership_plans_i18n', N'mid_season_rule') IS NOT NULL ALTER TABLE [membership_plans_i18n] DROP COLUMN [mid_season_rule];
IF COL_LENGTH(N'programs_i18n', N'audience') IS NOT NULL ALTER TABLE [programs_i18n] DROP COLUMN [audience];");
        }
    }
}
