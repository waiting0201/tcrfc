/* ============================================================================
   稽核 D 類（雙語缺口）遷移 2／2：收縮（contract）——刪除已搬到側表的舊主表欄位
   2026-10-06｜前提：1-expand 已套用，且新版 api（讀寫側表）已部署並驗證。

   🔴 先確認新版 api 已上線再跑：舊版 api 仍會 SELECT 這些欄位，跑了會立刻 500。
   🔴 刪除前逐表核對「舊欄位有值的列，zh-Hant 側表列也有同樣的值」，對不上就 THROW、整批回滾——欄位一刪就無法找回
      （Azure SQL Basic 的 PITR 只有 7 天，docs/20 §5）。
   🔴 冪等：舊欄位不存在就跳過。
   執行：sqlcmd -S <server> -d tcrfc_club -b -i db/migrations/20261006_d-bilingual-gaps_2-contract.sql
   ============================================================================ */
SET XACT_ABORT ON;
BEGIN TRANSACTION;

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
END;

COMMIT TRANSACTION;
