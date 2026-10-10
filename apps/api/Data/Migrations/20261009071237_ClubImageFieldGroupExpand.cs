using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <summary>
    /// 圖片欄位組補齊（展開，expand；主站規劃書 §4.0）：寬高放主表、Alt 放側表，多圖子表用 image_alt_zh／image_alt_en。
    /// 純新增可為空欄位；SQL 與 <c>db/migrations/20261009_image-field-group_1-expand.sql</c> 同源（冪等）。必須先 migrate 再 deploy（E-289）。
    /// </summary>
    public partial class ClubImageFieldGroupExpand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'teams', N'hero_width') IS NULL EXEC(N'ALTER TABLE teams ADD hero_width int NULL;');
IF COL_LENGTH(N'teams', N'hero_height') IS NULL EXEC(N'ALTER TABLE teams ADD hero_height int NULL;');
IF COL_LENGTH(N'teams_i18n', N'hero_alt') IS NULL EXEC(N'ALTER TABLE teams_i18n ADD hero_alt nvarchar(200) NULL;');
IF COL_LENGTH(N'players', N'photo_width') IS NULL EXEC(N'ALTER TABLE players ADD photo_width int NULL;');
IF COL_LENGTH(N'players', N'photo_height') IS NULL EXEC(N'ALTER TABLE players ADD photo_height int NULL;');
IF COL_LENGTH(N'players_i18n', N'photo_alt') IS NULL EXEC(N'ALTER TABLE players_i18n ADD photo_alt nvarchar(200) NULL;');
IF COL_LENGTH(N'staff', N'photo_width') IS NULL EXEC(N'ALTER TABLE staff ADD photo_width int NULL;');
IF COL_LENGTH(N'staff', N'photo_height') IS NULL EXEC(N'ALTER TABLE staff ADD photo_height int NULL;');
IF COL_LENGTH(N'staff_i18n', N'photo_alt') IS NULL EXEC(N'ALTER TABLE staff_i18n ADD photo_alt nvarchar(200) NULL;');
IF COL_LENGTH(N'programs', N'cover_width') IS NULL EXEC(N'ALTER TABLE programs ADD cover_width int NULL;');
IF COL_LENGTH(N'programs', N'cover_height') IS NULL EXEC(N'ALTER TABLE programs ADD cover_height int NULL;');
IF COL_LENGTH(N'programs_i18n', N'cover_alt') IS NULL EXEC(N'ALTER TABLE programs_i18n ADD cover_alt nvarchar(200) NULL;');
IF COL_LENGTH(N'comic_characters', N'image_width') IS NULL EXEC(N'ALTER TABLE comic_characters ADD image_width int NULL;');
IF COL_LENGTH(N'comic_characters', N'image_height') IS NULL EXEC(N'ALTER TABLE comic_characters ADD image_height int NULL;');
IF COL_LENGTH(N'comic_characters_i18n', N'image_alt') IS NULL EXEC(N'ALTER TABLE comic_characters_i18n ADD image_alt nvarchar(200) NULL;');
IF COL_LENGTH(N'comic_episodes', N'cover_width') IS NULL EXEC(N'ALTER TABLE comic_episodes ADD cover_width int NULL;');
IF COL_LENGTH(N'comic_episodes', N'cover_height') IS NULL EXEC(N'ALTER TABLE comic_episodes ADD cover_height int NULL;');
IF COL_LENGTH(N'comic_episodes_i18n', N'cover_alt') IS NULL EXEC(N'ALTER TABLE comic_episodes_i18n ADD cover_alt nvarchar(200) NULL;');
IF COL_LENGTH(N'fan_events', N'cover_width') IS NULL EXEC(N'ALTER TABLE fan_events ADD cover_width int NULL;');
IF COL_LENGTH(N'fan_events', N'cover_height') IS NULL EXEC(N'ALTER TABLE fan_events ADD cover_height int NULL;');
IF COL_LENGTH(N'partner_stores', N'image_width') IS NULL EXEC(N'ALTER TABLE partner_stores ADD image_width int NULL;');
IF COL_LENGTH(N'partner_stores', N'image_height') IS NULL EXEC(N'ALTER TABLE partner_stores ADD image_height int NULL;');
IF COL_LENGTH(N'partner_stores_i18n', N'image_alt') IS NULL EXEC(N'ALTER TABLE partner_stores_i18n ADD image_alt nvarchar(200) NULL;');
IF COL_LENGTH(N'member_draws', N'cover_width') IS NULL EXEC(N'ALTER TABLE member_draws ADD cover_width int NULL;');
IF COL_LENGTH(N'member_draws', N'cover_height') IS NULL EXEC(N'ALTER TABLE member_draws ADD cover_height int NULL;');
IF COL_LENGTH(N'member_draws_i18n', N'cover_alt') IS NULL EXEC(N'ALTER TABLE member_draws_i18n ADD cover_alt nvarchar(200) NULL;');
IF COL_LENGTH(N'calendar_custom_events', N'cover_width') IS NULL EXEC(N'ALTER TABLE calendar_custom_events ADD cover_width int NULL;');
IF COL_LENGTH(N'calendar_custom_events', N'cover_height') IS NULL EXEC(N'ALTER TABLE calendar_custom_events ADD cover_height int NULL;');
IF COL_LENGTH(N'calendar_custom_events_i18n', N'cover_alt') IS NULL EXEC(N'ALTER TABLE calendar_custom_events_i18n ADD cover_alt nvarchar(200) NULL;');
IF COL_LENGTH(N'product_images', N'image_alt_zh') IS NULL EXEC(N'ALTER TABLE product_images ADD image_alt_zh nvarchar(200) NULL;');
IF COL_LENGTH(N'product_images', N'image_alt_en') IS NULL EXEC(N'ALTER TABLE product_images ADD image_alt_en nvarchar(200) NULL;');
IF COL_LENGTH(N'charities', N'logo_width') IS NULL EXEC(N'ALTER TABLE charities ADD logo_width int NULL;');
IF COL_LENGTH(N'charities', N'logo_height') IS NULL EXEC(N'ALTER TABLE charities ADD logo_height int NULL;');
IF COL_LENGTH(N'charities_i18n', N'logo_alt') IS NULL EXEC(N'ALTER TABLE charities_i18n ADD logo_alt nvarchar(200) NULL;');
IF COL_LENGTH(N'charity_programs', N'cover_width') IS NULL EXEC(N'ALTER TABLE charity_programs ADD cover_width int NULL;');
IF COL_LENGTH(N'charity_programs', N'cover_height') IS NULL EXEC(N'ALTER TABLE charity_programs ADD cover_height int NULL;');
IF COL_LENGTH(N'charity_programs_i18n', N'cover_alt') IS NULL EXEC(N'ALTER TABLE charity_programs_i18n ADD cover_alt nvarchar(200) NULL;');
IF COL_LENGTH(N'charity_program_images', N'image_width') IS NULL EXEC(N'ALTER TABLE charity_program_images ADD image_width int NULL;');
IF COL_LENGTH(N'charity_program_images', N'image_height') IS NULL EXEC(N'ALTER TABLE charity_program_images ADD image_height int NULL;');
IF COL_LENGTH(N'charity_program_images', N'image_alt_zh') IS NULL EXEC(N'ALTER TABLE charity_program_images ADD image_alt_zh nvarchar(200) NULL;');
IF COL_LENGTH(N'charity_program_images', N'image_alt_en') IS NULL EXEC(N'ALTER TABLE charity_program_images ADD image_alt_en nvarchar(200) NULL;');
IF COL_LENGTH(N'impact_record_images', N'image_width') IS NULL EXEC(N'ALTER TABLE impact_record_images ADD image_width int NULL;');
IF COL_LENGTH(N'impact_record_images', N'image_height') IS NULL EXEC(N'ALTER TABLE impact_record_images ADD image_height int NULL;');
IF COL_LENGTH(N'impact_record_images', N'image_alt_zh') IS NULL EXEC(N'ALTER TABLE impact_record_images ADD image_alt_zh nvarchar(200) NULL;');
IF COL_LENGTH(N'impact_record_images', N'image_alt_en') IS NULL EXEC(N'ALTER TABLE impact_record_images ADD image_alt_en nvarchar(200) NULL;');
IF COL_LENGTH(N'impact_records_i18n', N'image_alt') IS NULL EXEC(N'ALTER TABLE impact_records_i18n ADD image_alt nvarchar(200) NULL;');
IF COL_LENGTH(N'partners', N'logo_dark_width') IS NULL EXEC(N'ALTER TABLE partners ADD logo_dark_width int NULL;');
IF COL_LENGTH(N'partners', N'logo_dark_height') IS NULL EXEC(N'ALTER TABLE partners ADD logo_dark_height int NULL;');
IF COL_LENGTH(N'partners', N'logo_light_width') IS NULL EXEC(N'ALTER TABLE partners ADD logo_light_width int NULL;');
IF COL_LENGTH(N'partners', N'logo_light_height') IS NULL EXEC(N'ALTER TABLE partners ADD logo_light_height int NULL;');
IF COL_LENGTH(N'partners_i18n', N'logo_alt') IS NULL EXEC(N'ALTER TABLE partners_i18n ADD logo_alt nvarchar(200) NULL;');
IF COL_LENGTH(N'sponsors', N'logo_dark_width') IS NULL EXEC(N'ALTER TABLE sponsors ADD logo_dark_width int NULL;');
IF COL_LENGTH(N'sponsors', N'logo_dark_height') IS NULL EXEC(N'ALTER TABLE sponsors ADD logo_dark_height int NULL;');
IF COL_LENGTH(N'sponsors', N'logo_light_width') IS NULL EXEC(N'ALTER TABLE sponsors ADD logo_light_width int NULL;');
IF COL_LENGTH(N'sponsors', N'logo_light_height') IS NULL EXEC(N'ALTER TABLE sponsors ADD logo_light_height int NULL;');
IF COL_LENGTH(N'sponsors_i18n', N'logo_alt') IS NULL EXEC(N'ALTER TABLE sponsors_i18n ADD logo_alt nvarchar(200) NULL;');
IF COL_LENGTH(N'sponsor_activation_images', N'image_alt_zh') IS NULL EXEC(N'ALTER TABLE sponsor_activation_images ADD image_alt_zh nvarchar(200) NULL;');
IF COL_LENGTH(N'sponsor_activation_images', N'image_alt_en') IS NULL EXEC(N'ALTER TABLE sponsor_activation_images ADD image_alt_en nvarchar(200) NULL;');
IF COL_LENGTH(N'comic_pages', N'image_alt_zh') IS NULL EXEC(N'ALTER TABLE comic_pages ADD image_alt_zh nvarchar(200) NULL;');
IF COL_LENGTH(N'comic_pages', N'image_alt_en') IS NULL EXEC(N'ALTER TABLE comic_pages ADD image_alt_en nvarchar(200) NULL;');
IF COL_LENGTH(N'fan_event_images', N'image_alt_zh') IS NULL EXEC(N'ALTER TABLE fan_event_images ADD image_alt_zh nvarchar(200) NULL;');
IF COL_LENGTH(N'fan_event_images', N'image_alt_en') IS NULL EXEC(N'ALTER TABLE fan_event_images ADD image_alt_en nvarchar(200) NULL;');
IF COL_LENGTH(N'clubs_i18n', N'og_image_alt') IS NULL EXEC(N'ALTER TABLE clubs_i18n ADD og_image_alt nvarchar(200) NULL;');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "hero_alt",
                table: "teams_i18n");

            migrationBuilder.DropColumn(
                name: "hero_height",
                table: "teams");

            migrationBuilder.DropColumn(
                name: "hero_width",
                table: "teams");

            migrationBuilder.DropColumn(
                name: "photo_alt",
                table: "staff_i18n");

            migrationBuilder.DropColumn(
                name: "photo_height",
                table: "staff");

            migrationBuilder.DropColumn(
                name: "photo_width",
                table: "staff");

            migrationBuilder.DropColumn(
                name: "logo_alt",
                table: "sponsors_i18n");

            migrationBuilder.DropColumn(
                name: "logo_dark_height",
                table: "sponsors");

            migrationBuilder.DropColumn(
                name: "logo_dark_width",
                table: "sponsors");

            migrationBuilder.DropColumn(
                name: "logo_light_height",
                table: "sponsors");

            migrationBuilder.DropColumn(
                name: "logo_light_width",
                table: "sponsors");

            migrationBuilder.DropColumn(
                name: "image_alt_en",
                table: "sponsor_activation_images");

            migrationBuilder.DropColumn(
                name: "image_alt_zh",
                table: "sponsor_activation_images");

            migrationBuilder.DropColumn(
                name: "cover_alt",
                table: "programs_i18n");

            migrationBuilder.DropColumn(
                name: "cover_height",
                table: "programs");

            migrationBuilder.DropColumn(
                name: "cover_width",
                table: "programs");

            migrationBuilder.DropColumn(
                name: "image_alt_en",
                table: "product_images");

            migrationBuilder.DropColumn(
                name: "image_alt_zh",
                table: "product_images");

            migrationBuilder.DropColumn(
                name: "photo_alt",
                table: "players_i18n");

            migrationBuilder.DropColumn(
                name: "photo_height",
                table: "players");

            migrationBuilder.DropColumn(
                name: "photo_width",
                table: "players");

            migrationBuilder.DropColumn(
                name: "logo_alt",
                table: "partners_i18n");

            migrationBuilder.DropColumn(
                name: "logo_dark_height",
                table: "partners");

            migrationBuilder.DropColumn(
                name: "logo_dark_width",
                table: "partners");

            migrationBuilder.DropColumn(
                name: "logo_light_height",
                table: "partners");

            migrationBuilder.DropColumn(
                name: "logo_light_width",
                table: "partners");

            migrationBuilder.DropColumn(
                name: "image_alt",
                table: "partner_stores_i18n");

            migrationBuilder.DropColumn(
                name: "image_height",
                table: "partner_stores");

            migrationBuilder.DropColumn(
                name: "image_width",
                table: "partner_stores");

            migrationBuilder.DropColumn(
                name: "cover_alt",
                table: "member_draws_i18n");

            migrationBuilder.DropColumn(
                name: "cover_height",
                table: "member_draws");

            migrationBuilder.DropColumn(
                name: "cover_width",
                table: "member_draws");

            migrationBuilder.DropColumn(
                name: "image_alt",
                table: "impact_records_i18n");

            migrationBuilder.DropColumn(
                name: "image_alt_en",
                table: "impact_record_images");

            migrationBuilder.DropColumn(
                name: "image_alt_zh",
                table: "impact_record_images");

            migrationBuilder.DropColumn(
                name: "image_height",
                table: "impact_record_images");

            migrationBuilder.DropColumn(
                name: "image_width",
                table: "impact_record_images");

            migrationBuilder.DropColumn(
                name: "cover_height",
                table: "fan_events");

            migrationBuilder.DropColumn(
                name: "cover_width",
                table: "fan_events");

            migrationBuilder.DropColumn(
                name: "image_alt_en",
                table: "fan_event_images");

            migrationBuilder.DropColumn(
                name: "image_alt_zh",
                table: "fan_event_images");

            migrationBuilder.DropColumn(
                name: "image_alt_en",
                table: "comic_pages");

            migrationBuilder.DropColumn(
                name: "image_alt_zh",
                table: "comic_pages");

            migrationBuilder.DropColumn(
                name: "cover_alt",
                table: "comic_episodes_i18n");

            migrationBuilder.DropColumn(
                name: "cover_height",
                table: "comic_episodes");

            migrationBuilder.DropColumn(
                name: "cover_width",
                table: "comic_episodes");

            migrationBuilder.DropColumn(
                name: "image_alt",
                table: "comic_characters_i18n");

            migrationBuilder.DropColumn(
                name: "image_height",
                table: "comic_characters");

            migrationBuilder.DropColumn(
                name: "image_width",
                table: "comic_characters");

            migrationBuilder.DropColumn(
                name: "og_image_alt",
                table: "clubs_i18n");

            migrationBuilder.DropColumn(
                name: "cover_alt",
                table: "charity_programs_i18n");

            migrationBuilder.DropColumn(
                name: "cover_height",
                table: "charity_programs");

            migrationBuilder.DropColumn(
                name: "cover_width",
                table: "charity_programs");

            migrationBuilder.DropColumn(
                name: "image_alt_en",
                table: "charity_program_images");

            migrationBuilder.DropColumn(
                name: "image_alt_zh",
                table: "charity_program_images");

            migrationBuilder.DropColumn(
                name: "image_height",
                table: "charity_program_images");

            migrationBuilder.DropColumn(
                name: "image_width",
                table: "charity_program_images");

            migrationBuilder.DropColumn(
                name: "logo_alt",
                table: "charities_i18n");

            migrationBuilder.DropColumn(
                name: "logo_height",
                table: "charities");

            migrationBuilder.DropColumn(
                name: "logo_width",
                table: "charities");

            migrationBuilder.DropColumn(
                name: "cover_alt",
                table: "calendar_custom_events_i18n");

            migrationBuilder.DropColumn(
                name: "cover_height",
                table: "calendar_custom_events");

            migrationBuilder.DropColumn(
                name: "cover_width",
                table: "calendar_custom_events");
        }
    }
}
