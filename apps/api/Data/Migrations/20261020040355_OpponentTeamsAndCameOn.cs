using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// 對手球隊庫＋替補上場旗標（展開，expand；主站規劃書 v3.31）：<c>opponent_teams</c>／<c>opponent_teams_i18n</c> 新表、
    /// <c>matches.opponent_team_id</c>／<c>standings.opponent_team_id</c> 可為空外鍵、<c>match_lineups.came_on</c>（bit NOT NULL DEFAULT 0）。
    /// 純新增、不回填。SQL 與 <c>db/migrations/20261020_opponent-teams_1-expand.sql</c> 同源（冪等，docs/20 §5 冪等契約）。必須先 migrate 再 deploy（E-289）。
    /// </summary>
    public partial class OpponentTeamsAndCameOn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- ① 對手球隊庫
IF OBJECT_ID(N'opponent_teams', N'U') IS NULL
BEGIN
  CREATE TABLE opponent_teams (
    id              uniqueidentifier NOT NULL CONSTRAINT DF_opponent_teams_id DEFAULT NEWID(),
    row_seq         bigint IDENTITY(1,1) NOT NULL,
    club_id         uniqueidentifier NOT NULL,
    name            nvarchar(128)    NOT NULL,
    crest_key       nvarchar(500)    NULL,
    crest_width     int              NULL,
    crest_height    int              NULL,
    sort_order      int              NOT NULL CONSTRAINT DF_opponent_teams_sort_order DEFAULT 0,
    created_at      datetime2(3)     NOT NULL CONSTRAINT DF_opponent_teams_created_at DEFAULT SYSUTCDATETIME(),
    updated_at      datetime2(3)     NOT NULL CONSTRAINT DF_opponent_teams_updated_at DEFAULT SYSUTCDATETIME(),
    created_by      uniqueidentifier NULL,
    updated_by      uniqueidentifier NULL,
    CONSTRAINT PK_opponent_teams PRIMARY KEY NONCLUSTERED (id),
    CONSTRAINT UQ_opponent_teams_row_seq UNIQUE CLUSTERED (row_seq),
    CONSTRAINT UQ_opponent_teams_club_name UNIQUE (club_id, name)
  );
  CREATE INDEX IX_opponent_teams_club_sort ON opponent_teams (club_id, sort_order);
  ALTER TABLE opponent_teams ADD CONSTRAINT FK_opponent_teams_club FOREIGN KEY (club_id) REFERENCES clubs(id);
  ALTER TABLE opponent_teams ADD CONSTRAINT FK_opponent_teams_created_by FOREIGN KEY (created_by) REFERENCES admin_users(id);
  ALTER TABLE opponent_teams ADD CONSTRAINT FK_opponent_teams_updated_by FOREIGN KEY (updated_by) REFERENCES admin_users(id);
END;

IF OBJECT_ID(N'opponent_teams_i18n', N'U') IS NULL
BEGIN
  CREATE TABLE opponent_teams_i18n (
    opponent_team_id uniqueidentifier NOT NULL,
    locale          nvarchar(10)     NOT NULL,
    name            nvarchar(128)    NULL,
    CONSTRAINT PK_opponent_teams_i18n PRIMARY KEY CLUSTERED (opponent_team_id, locale)
  );
  CREATE INDEX IX_opponent_teams_i18n_locale ON opponent_teams_i18n (locale);
  ALTER TABLE opponent_teams_i18n ADD CONSTRAINT FK_opponent_teams_i18n_opponent_team FOREIGN KEY (opponent_team_id) REFERENCES opponent_teams(id) ON DELETE CASCADE;
END;

-- ② matches／standings 的對手球隊外鍵（可為空，不回填）
IF COL_LENGTH(N'matches', N'opponent_team_id') IS NULL EXEC(N'ALTER TABLE matches ADD opponent_team_id uniqueidentifier NULL;');
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_matches_opponent_team')
    EXEC(N'ALTER TABLE matches ADD CONSTRAINT FK_matches_opponent_team FOREIGN KEY (opponent_team_id) REFERENCES opponent_teams(id);');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_matches_opponent_team' AND object_id = OBJECT_ID(N'matches'))
    EXEC(N'CREATE INDEX IX_matches_opponent_team ON matches (opponent_team_id);');

IF COL_LENGTH(N'standings', N'opponent_team_id') IS NULL EXEC(N'ALTER TABLE standings ADD opponent_team_id uniqueidentifier NULL;');
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_standings_opponent_team')
    EXEC(N'ALTER TABLE standings ADD CONSTRAINT FK_standings_opponent_team FOREIGN KEY (opponent_team_id) REFERENCES opponent_teams(id);');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_standings_opponent_team' AND object_id = OBJECT_ID(N'standings'))
    EXEC(N'CREATE INDEX IX_standings_opponent_team ON standings (opponent_team_id);');

-- ③ 出賽名單：替補是否上場（新增欄位預設 0，下方再回填）
IF COL_LENGTH(N'match_lineups', N'came_on') IS NULL
    EXEC(N'ALTER TABLE match_lineups ADD came_on bit NOT NULL CONSTRAINT DF_match_lineups_came_on DEFAULT 0;');

-- 既有資料回填：替補但在該場有進球或牌的球員視為有上場（保留舊的出賽判定結果；舊規則「替補有進球／牌算出賽」改由 came_on 承載）。
EXEC(N'UPDATE l SET l.came_on = 1
       FROM match_lineups l
       WHERE l.is_starter = 0 AND l.came_on = 0
         AND (EXISTS (SELECT 1 FROM match_goals g WHERE g.match_id = l.match_id AND g.player_id = l.player_id)
              OR EXISTS (SELECT 1 FROM match_cards c WHERE c.match_id = l.match_id AND c.player_id = l.player_id));');
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_matches_opponent_team",
                table: "matches");

            migrationBuilder.DropForeignKey(
                name: "FK_standings_opponent_team",
                table: "standings");

            migrationBuilder.DropTable(
                name: "opponent_teams_i18n");

            migrationBuilder.DropTable(
                name: "opponent_teams");

            migrationBuilder.DropIndex(
                name: "IX_standings_opponent_team",
                table: "standings");

            migrationBuilder.DropIndex(
                name: "IX_matches_opponent_team",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "opponent_team_id",
                table: "standings");

            migrationBuilder.DropColumn(
                name: "opponent_team_id",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "came_on",
                table: "match_lineups");
        }
    }
}
