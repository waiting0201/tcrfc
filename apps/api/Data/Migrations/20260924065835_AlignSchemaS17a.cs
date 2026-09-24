using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlignSchemaS17a : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 🔴 nvarchar(32)，不是最初的 20：最長的值 'consented_by_guardian' 是 21 個字元，
            // nvarchar(20) 裝不下，寫入即噴「String or binary data would be truncated」——
            // 單純的欄寬計算錯誤，見 db/club-schema.sql 該表註解與 apps/api/README.md 本節說明。
            migrationBuilder.AddColumn<string>(
                name: "portrait_consent_status",
                table: "staff",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "not_consented");

            migrationBuilder.AddColumn<string>(
                name: "portrait_consent_status",
                table: "players",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "not_consented");

            migrationBuilder.AddColumn<bool>(
                name: "is_enabled",
                table: "faq_categories",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "image_alt",
                table: "banners_i18n",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "image_height",
                table: "banners",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "image_width",
                table: "banners",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "media_type",
                table: "banners",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "image");

            migrationBuilder.AddColumn<string>(
                name: "video_key",
                table: "banners",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "faq_embed_slots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    row_seq = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_faq_embed_slots", x => x.id)
                        .Annotation("SqlServer:Clustered", false);
                    table.ForeignKey(
                        name: "FK_faq_embed_slots_created_by",
                        column: x => x.created_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_faq_embed_slots_updated_by",
                        column: x => x.updated_by,
                        principalTable: "admin_users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "faq_embed_slot_links",
                columns: table => new
                {
                    faq_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    faq_embed_slot_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    sort_order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_faq_embed_slot_links", x => new { x.faq_id, x.faq_embed_slot_id });
                    table.ForeignKey(
                        name: "FK_faq_embed_slot_links_faq",
                        column: x => x.faq_id,
                        principalTable: "faqs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_faq_embed_slot_links_slot",
                        column: x => x.faq_embed_slot_id,
                        principalTable: "faq_embed_slots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_faq_embed_slot_links_slot",
                table: "faq_embed_slot_links",
                column: "faq_embed_slot_id");

            migrationBuilder.CreateIndex(
                name: "UQ_faq_embed_slots_code",
                table: "faq_embed_slots",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_faq_embed_slots_row_seq",
                table: "faq_embed_slots",
                column: "row_seq",
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            // 🔴 以下 CHECK 約束 EF 不會自動產生（本專案既有慣例：整個模型完全不宣告
            // HasCheckConstraint，CHECK 約束一律由 db/club-schema.sql 的 DDL 與這裡的手寫 SQL
            // 維護，見 docs/20-cicd.md §5「新增外鍵欄位」段同一種「索引由 DDL 決定」的分工延伸）。

            // 新欄位的 CHECK：players／staff.portrait_consent_status 三態、banners.media_type
            // 二態、banners 的 media_type／video_key 互相依賴（CK_banners_video_key，
            // db/club-schema.sql 該表註解）。這三張表都是全新欄位，不需要處理既有資料衝突值。
            migrationBuilder.Sql(
                "ALTER TABLE players ADD CONSTRAINT CK_players_portrait_consent_status " +
                "CHECK (portrait_consent_status IN ('not_consented','consented','consented_by_guardian'));");
            migrationBuilder.Sql(
                "ALTER TABLE staff ADD CONSTRAINT CK_staff_portrait_consent_status " +
                "CHECK (portrait_consent_status IN ('not_consented','consented','consented_by_guardian'));");
            migrationBuilder.Sql(
                "ALTER TABLE banners ADD CONSTRAINT CK_banners_media_type CHECK (media_type IN ('image','video'));");
            migrationBuilder.Sql(
                "ALTER TABLE banners ADD CONSTRAINT CK_banners_video_key " +
                "CHECK (media_type = 'image' OR video_key IS NOT NULL);");

            // 🔴 收斂 7 張表既有的 status CHECK（draft/published/scheduled → draft/published，
            // S1-8／S1-7a，docs/14 S0-7g 已裁決）。這些 CHECK 是 db/club-schema.sql 建表時的
            // 「欄位層」CHECK，沒有顯式命名，SQL Server 會自動配一個系統產生的名稱——先用
            // sys.check_constraints 動態查出實際名稱再 DROP，換成下面明確命名的版本，方便未來
            // 需要再改時可以直接用名稱操作。呼叫端（Repository 層）已在應用層擋掉 'scheduled'
            // （AdminCompetitionsRepository／AdminFaqsRepository 等既有 AllowedStatuses 清單），
            // 這裡是把資料庫層也收斂到一致，不是本輪新增的應用層驗證。
            // 套用前已用 SQL 查證 tcrfc_club_dev 這 7 張表目前皆為 0 筆 status='scheduled'
            // （見 apps/api/README.md 本節「scheduled 列的處理」），不需要在這裡搭配 DML 轉態。
            //
            // 🔴 每張表用各自獨立命名的區域變數（@cc_<table>），不是共用 @constraintName：
            // `dotnet ef migrations script --idempotent` 產出的檔案是給人工核准者閱讀＋可能
            // 直接貼到 SSMS／sqlcmd 執行的審查稿（docs/20-cicd.md §5），這些 IF EXISTS 區塊之間
            // 沒有 GO 分隔、屬於同一個查詢批次——同一個變數名稱 DECLARE 兩次會直接噴
            // 「Msg 134: The variable name has already been declared」，實測驗證過（見 apps/api/
            // README.md 本節）。`dotnet ef database update` 實際套用時每個 Sql() 呼叫各自是一次
            // 獨立的 ExecuteNonQuery（不受影響），但審查稿本身也必須是一份跑得動的合法 SQL。
            foreach (var table in new[]
                     {
                         "press_resources", "faqs", "competitions", "sponsor_packages",
                         "collections", "products", "charity_programs",
                     })
            {
                migrationBuilder.Sql($"""
                    DECLARE @cc_{table} sysname;
                    SELECT @cc_{table} = cc.name
                    FROM sys.check_constraints cc
                    JOIN sys.columns col ON col.object_id = cc.parent_object_id AND col.column_id = cc.parent_column_id
                    WHERE cc.parent_object_id = OBJECT_ID(N'{table}') AND col.name = 'status';
                    IF @cc_{table} IS NOT NULL
                        EXEC('ALTER TABLE {table} DROP CONSTRAINT [' + @cc_{table} + ']');
                    ALTER TABLE {table} ADD CONSTRAINT CK_{table}_status CHECK (status IN ('draft','published'));
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 還原 7 張表的 status CHECK（加回 'scheduled'），對稱於 Up() 的收斂。
            foreach (var table in new[]
                     {
                         "press_resources", "faqs", "competitions", "sponsor_packages",
                         "collections", "products", "charity_programs",
                     })
            {
                migrationBuilder.Sql($"ALTER TABLE {table} DROP CONSTRAINT CK_{table}_status;");
                migrationBuilder.Sql(
                    $"ALTER TABLE {table} ADD CONSTRAINT CK_{table}_status " +
                    "CHECK (status IN ('draft','published','scheduled'));");
            }

            migrationBuilder.Sql("ALTER TABLE banners DROP CONSTRAINT CK_banners_video_key;");
            migrationBuilder.Sql("ALTER TABLE banners DROP CONSTRAINT CK_banners_media_type;");
            migrationBuilder.Sql("ALTER TABLE staff DROP CONSTRAINT CK_staff_portrait_consent_status;");
            migrationBuilder.Sql("ALTER TABLE players DROP CONSTRAINT CK_players_portrait_consent_status;");

            migrationBuilder.DropTable(
                name: "faq_embed_slot_links");

            migrationBuilder.DropTable(
                name: "faq_embed_slots");

            migrationBuilder.DropColumn(
                name: "portrait_consent_status",
                table: "staff");

            migrationBuilder.DropColumn(
                name: "portrait_consent_status",
                table: "players");

            migrationBuilder.DropColumn(
                name: "is_enabled",
                table: "faq_categories");

            migrationBuilder.DropColumn(
                name: "image_alt",
                table: "banners_i18n");

            migrationBuilder.DropColumn(
                name: "image_height",
                table: "banners");

            migrationBuilder.DropColumn(
                name: "image_width",
                table: "banners");

            migrationBuilder.DropColumn(
                name: "media_type",
                table: "banners");

            migrationBuilder.DropColumn(
                name: "video_key",
                table: "banners");
        }
    }
}
