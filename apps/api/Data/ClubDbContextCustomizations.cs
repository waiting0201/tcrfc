using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Tcrfc.Api.Data.EfEntities;

namespace Tcrfc.Api.Data;

/// <summary>
/// 對 <c>dotnet ef dbcontext scaffold</c> 產出的 <see cref="ClubDbContext"/> 做客製化，
/// 不直接改 <c>ClubDbContext.cs</c>（那是產生檔，之後重新 scaffold 會被整份覆寫）。
/// 掛進 scaffold 已預留的 <c>OnModelCreatingPartial</c> 局部方法，這是 EF Core 官方建議的擴充點。
/// </summary>
public partial class ClubDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // 🔴 樂觀並行控制：articles.updated_at 當並行權杖。這張表沒有 rowversion／timestamp 欄位，
        // 任務指示明訂「用 updated_at 做樂觀並行控制」——寫入時把 EF 追蹤的「原始值」設成呼叫端
        // 宣稱看到的 updated_at，SaveChanges 會產生 UPDATE ... WHERE updated_at = @原始值，
        // 若 0 筆命中（代表資料庫的值已經被別人改過）就丟 DbUpdateConcurrencyException，
        // 由 Features/AdminNews 的 repository 接住轉成 409。
        modelBuilder.Entity<Article>()
            .Property(a => a.UpdatedAt)
            .IsConcurrencyToken();

        // 🔴 S1-4（B1 頁面管理）：pages.updated_at 當並行權杖，同一個理由——這張表也沒有
        // rowversion／timestamp 欄位。落點與 Features/AdminPages/AdminPagesRepository.cs 的
        // ApplyConcurrencyToken／SaveWithConcurrencyHandlingAsync 對應。
        modelBuilder.Entity<Page>()
            .Property(p => p.UpdatedAt)
            .IsConcurrencyToken();
    }

    // 🔴 S0-7k／S0-7l（2026-09-24，docs/18-work-errors.md E-45／docs/20-cicd.md §5）：
    // 整條移除 EF Core 的 ForeignKeyIndexConvention，不再讓 EF 替「沒有顯式索引」的外鍵欄位
    // 自動加上非叢集索引。
    //
    // 背景：S0-7k 一開始只想抑制 admin_refresh_tokens.replaced_by_id 這一個屬性的自動索引
    // （db/club-schema.sql 1547–1559 行刻意沒有這個索引），做法是繼承
    // ForeignKeyIndexConvention、覆寫 CreateIndex() 對這一個屬性回傳 null。當時順手把
    // ClubDbContextModelSnapshot.cs 全表掃過一輪，發現同一個慣例還替另外約 281 個外鍵欄位
    // （created_by 85、updated_by 85、club_id 33、其餘業務外鍵約 78）自動加了 db/club-schema.sql
    // 沒有的索引——這些索引全部只存在於 EF 的記憶體模型／migration snapshot，
    // 從未真的建到任何資料庫（InitialBaseline 的 Up() 是空的）。S0-7l 依規劃裁決「依綱要為準，
    // 不改 db/club-schema.sql，讓 EF 模型對齊 DDL」，範圍涵蓋這 281 筆＋原本那 1 筆，
    // 因此改用「整條移除慣例」取代「子類別跳過清單」——後者在只有 1 個例外時還算得上精準，
    // 但例外多達 282 個時，維護一份跳過清單本身就是另一種形式的資料落差來源，不如直接不要
    // 這個慣例的自動行為。
    //
    // ⚠️ 一開始（S0-7k）試過在 OnModelCreatingPartial 用
    // modelBuilder.Entity&lt;T&gt;().Metadata.RemoveIndex(...) 直接刪索引，實測無效：
    // ForeignKeyIndexConvention 同時實作了 IIndexRemovedConvention 與 IModelFinalizingConvention，
    // 只要偵測到一個外鍵的屬性沒有涵蓋索引，移除後會立刻「自我修復」補回同一個索引（用
    // dotnet ef migrations add Probe 驗證過：Up() 又長回一模一樣的 CreateIndex）。這個慣例沒有
    // 提供「這個外鍵不要」的旗標，唯一乾淨的做法是移除慣例本身。
    //
    // 這不會影響 db/club-schema.sql 已經明確宣告的索引與唯一鍵（143 張表的 HasIndex／
    // HasKey／HasAlternateKey 都是 scaffold 從實際資料庫逆向工程產生的顯式設定，走的是
    // Fluent API 而不是這個慣例，Remove 這個慣例不會動到它們）——本輪已用一支獨立腳本比對
    // 「移除慣例前」的模型索引清單與 db/club-schema.sql 的索引／唯一鍵清單：移除前模型
    // 491 筆、DDL 210 筆、DDL 有而模型沒有＝0、模型有而 DDL 沒有＝281（與上述估計一致）；
    // 移除慣例後應變成模型 210 筆、兩邊完全一致（詳見 S0-7l 交付報告）。
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Conventions.Remove(typeof(ForeignKeyIndexConvention));
    }
}
