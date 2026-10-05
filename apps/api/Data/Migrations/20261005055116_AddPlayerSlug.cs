using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <summary>
    /// 球員網址代稱（2026-10-05）：<c>players.slug</c>（必填、<c>[a-z0-9-]</c>）＋唯一鍵 <c>(club_id, slug)</c>＋索引 <c>(slug, club_id)</c>。
    /// 依據：App 規劃書 §2.3 深連結 <c>tcrfc://player/{slug}</c> → <c>/zh/club/first-team/player/{slug}</c>；資料表定義見 docs/12、docs/12b §11.1。
    /// 🔴 每一步先查現況再動（冪等）：正式庫是用 <c>db/club-schema.sql</c> 建的（新建庫已含這個欄位與索引），本機庫與舊庫則沒有。
    /// 既有球員的回填值是 <c>player-{row_seq}</c>（唯一、符合格式、不含個資）；漂亮的代稱由種子（<c>db/seed</c>）與後台「網址代稱」欄位補上。
    /// </summary>
    public partial class AddPlayerSlug : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF COL_LENGTH(N'players', N'slug') IS NULL ALTER TABLE [players] ADD [slug] nvarchar(160) NULL;");
            // 欄位剛加進來時同一批次內引用它會在編譯期失敗，後續陳述式一律以 EXEC 延後解析。
            migrationBuilder.Sql("EXEC(N'UPDATE [players] SET [slug] = N''player-'' + CONVERT(nvarchar(20), [row_seq]) WHERE [slug] IS NULL OR LEN([slug]) = 0;');");
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'players') AND name = N'slug' AND is_nullable = 1)
    EXEC(N'ALTER TABLE [players] ALTER COLUMN [slug] nvarchar(160) NOT NULL');");
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'players') AND name = N'UQ_players_club_slug')
    EXEC(N'CREATE UNIQUE INDEX [UQ_players_club_slug] ON [players] ([club_id], [slug])');");
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'players') AND name = N'IX_players_slug_club')
    EXEC(N'CREATE INDEX [IX_players_slug_club] ON [players] ([slug], [club_id])');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'players') AND name = N'IX_players_slug_club') DROP INDEX [IX_players_slug_club] ON [players];");
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'players') AND name = N'UQ_players_club_slug')
    ALTER TABLE [players] DROP CONSTRAINT [UQ_players_club_slug];
ELSE IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'players') AND name = N'UQ_players_club_slug')
    DROP INDEX [UQ_players_club_slug] ON [players];");
            migrationBuilder.Sql("IF COL_LENGTH(N'players', N'slug') IS NOT NULL ALTER TABLE [players] DROP COLUMN [slug];");
        }
    }
}
