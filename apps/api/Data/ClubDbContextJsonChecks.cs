using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data.EfEntities;

namespace Tcrfc.Api.Data;

/// <summary>
/// JSON 內容欄位的具名 CHECK 約束（2026-10-13 使用者決定：資料庫不使用原生 <c>json</c> 型別，
/// 一律 <c>nvarchar(max)</c> ＋ <c>CK_&lt;表&gt;_&lt;欄&gt;_json CHECK (ISJSON(欄, OBJECT|ARRAY) = 1)</c>；docs/17 §6 第 2 項、docs/14）。
/// 模型描述它們，是為了讓 <c>deploy/schema-drift-check.sh</c> 能比對「DDL 的 CHECK」與「EF 認為存在的 CHECK」，
/// 名稱與定義與 <c>db/club-schema.sql</c> 一致；對應的 migration <c>JsonColumnsNvarcharWithCheck</c> 用冪等 SQL 建立（正式庫由 DDL 建、原生 json）。
/// 🔴 新增 JSON 內容欄位時：DDL 的欄位宣告、這裡、migration 三處一起改，且寫入端走 <c>Common/JsonColumn.cs</c>。
/// </summary>
public partial class ClubDbContext
{
    private static void ConfigureJsonChecks(ModelBuilder modelBuilder)
    {
        const string obj = "OBJECT";
        const string arr = "ARRAY";

        modelBuilder.Entity<PageBlock>().ToTable(t => t.HasCheckConstraint("CK_page_blocks_content_json", Check("content", obj)));
        modelBuilder.Entity<PageVersion>().ToTable(t => t.HasCheckConstraint("CK_page_versions_snapshot_json", Check("snapshot", obj)));
        modelBuilder.Entity<ArticlesI18n>().ToTable(t => t.HasCheckConstraint("CK_articles_i18n_body_json", Check("body", obj, arr)));
        modelBuilder.Entity<ProgramsI18n>().ToTable(t => t.HasCheckConstraint("CK_programs_i18n_content_json", Check("content", obj, arr)));
        modelBuilder.Entity<Session>().ToTable(t => t.HasCheckConstraint("CK_sessions_weekly_schedule_json", Check("weekly_schedule", obj, arr)));
        modelBuilder.Entity<PartnerStore>().ToTable(t => t.HasCheckConstraint("CK_partner_stores_business_hours_json", Check("business_hours", obj)));
        modelBuilder.Entity<Product>().ToTable(t => t.HasCheckConstraint("CK_products_size_chart_json", Check("size_chart", obj, arr)));
        modelBuilder.Entity<CharityProgramsI18n>().ToTable(t => t.HasCheckConstraint("CK_charity_programs_i18n_content_json", Check("content", obj, arr)));
        modelBuilder.Entity<PushMessage>().ToTable(t => t.HasCheckConstraint("CK_push_messages_audience_team_codes_json", Check("audience_team_codes", arr)));
        modelBuilder.Entity<AppSetting>().ToTable(t => t.HasCheckConstraint("CK_app_settings_setting_value_json", Check("setting_value", obj, arr)));
    }

    /// <summary>產生 <c>ISJSON(欄, OBJECT) = 1 [OR ISJSON(欄, ARRAY) = 1]</c>；與 DDL 逐字相同（schema-drift 比對時另會正規化）。</summary>
    private static string Check(string column, params string[] kinds)
        => string.Join(" OR ", kinds.Select(k => $"ISJSON({column}, {k}) = 1"));
}
