using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data.EfEntities;

namespace Tcrfc.Api.Data;

/// <summary>
/// 讓 EF 模型的索引與 <c>db/club-schema.sql</c>／docs/12b §11 一致（依「綱要的真實來源是 DDL」，
/// STATUS B-12、docs/20 §5「DDL 是否等於所有 migration 套用後」的差異清單）。
/// 與 <c>AlignIndexesWithDdl</c>（外鍵自動索引）同一類對齊，但這一批 DDL 與 EF 都有該索引、只是細節不同，
/// 所以對應的 migration（<c>AlignIndexesWithDdl2</c>）用「先查後做」的冪等 SQL，正式庫（DDL 建的）上是無操作。
/// </summary>
public partial class ClubDbContext
{
    private static void ConfigureIndexAlignment(ModelBuilder modelBuilder)
    {
        // 1) 五個 UNIQUE (club_id, slug)：EF 對「可空欄位的唯一索引」預設加 WHERE club_id IS NOT NULL，
        //    DDL 是無篩選的 UNIQUE 約束（B-12 定案：SQL Server 唯一索引把 NULL 視為相等，共同內容的 slug 也要唯一，
        //    不加篩選索引）。拿掉篩選＝EF 認知與 DDL 一致。
        modelBuilder.Entity<Charity>().HasIndex(e => new { e.ClubId, e.Slug }, "UQ_charities_club_slug").HasFilter(null);
        modelBuilder.Entity<CharityProgram>().HasIndex(e => new { e.ClubId, e.Slug }, "UQ_charity_programs_club_slug").HasFilter(null);
        modelBuilder.Entity<Faq>().HasIndex(e => new { e.ClubId, e.Slug }, "UQ_faqs_club_slug").HasFilter(null);
        modelBuilder.Entity<PartnerStore>().HasIndex(e => new { e.ClubId, e.Slug }, "UQ_partner_stores_club_slug").HasFilter(null);
        modelBuilder.Entity<PressResource>().HasIndex(e => new { e.ClubId, e.Slug }, "UQ_press_resources_club_slug").HasFilter(null);

        // 2) 只在 DDL 的兩個索引（DDL 為準，docs/12b §11.2 沒寫）：
        //    同一張表單最多一個「內容摘要」欄位的過濾唯一索引（第二道防線，主要防線在 AdminFormsRepository）；
        //    試訓報名的查詢索引（registrations 同時服務 session 與 trial）。
        modelBuilder.Entity<FormField>().HasIndex(e => e.FormId, "UQ_form_fields_one_summary_per_form")
            .IsUnique().HasFilter("[is_summary] = 1");
        // 3) docs/12b §11.2「所有 *_i18n 建 (locale) 索引」：form_fields_i18n（EF 已有，補進 DDL）與
        //    sponsor_activations_i18n（DDL 與 EF 原本都漏了，兩邊一起補）。
        modelBuilder.Entity<SponsorActivationsI18n>().HasIndex(e => e.Locale, "IX_sponsor_activations_i18n_locale");
        modelBuilder.Entity<Registration>().HasIndex(e => new { e.TrialId, e.Status }, "IX_registrations_trial_status");
    }
}
