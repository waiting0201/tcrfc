using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
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
    }

    // 🔴 S0-7k（2026-09-24，docs/18-work-errors.md E-45／docs/20-cicd.md §5）：
    // 抑制 EF Core ForeignKeyIndexConvention 替 admin_refresh_tokens.replaced_by_id 這個可為空外鍵
    // 自動加上的非叢集索引（預設會叫 IX_admin_refresh_tokens_replaced_by_id）。
    // db/club-schema.sql（真實來源，1547–1559 行）刻意沒有這個索引，本機 tcrfc_club_dev 也沒有；
    // 使用者裁決「依綱要為準，不改 db/club-schema.sql」，所以讓 EF 對齊綱要。
    //
    // ⚠️ 一開始試過在 OnModelCreatingPartial 用 modelBuilder.Entity<T>().Metadata.RemoveIndex(...)
    // 直接刪索引，實測無效：ForeignKeyIndexConvention 同時實作了 IIndexRemovedConvention 與
    // IModelFinalizingConvention，只要偵測到一個外鍵的屬性沒有涵蓋索引，移除後會立刻「自我修復」
    // 補回同一個索引（用 dotnet ef migrations add Probe 驗證過：Up() 又長回一模一樣的 CreateIndex）。
    // 這個慣例沒有提供「這個外鍵不要」的旗標，唯一乾淨、範圍夠小的做法是換掉這個慣例本身，
    // 只在它要處理 AdminRefreshToken.ReplacedById 這一個屬性時跳過，其餘 137 張表的自動索引行為
    // （含下面提到的其餘落差）完全不受影響、原封不動繼承自官方實作。
    //
    // ⚠️ 這不是唯一一筆這種落差——ClubDbContextModelSnapshot.cs 另有約 280 筆同類、未命名的
    // HasIndex（多半是既有 143 張表 created_by／updated_by／club_id 等審計與維度欄位的自動 FK
    // 索引），全部是 InitialBaseline 當初 scaffold＋慣例產生、從未逐欄跟 db/club-schema.sql
    // 核對過的既有落差，範圍遠大於這一筆。S0-7k 任務範圍只裁決這一筆，其餘依指示只回報不處理
    // ——因此這裡刻意只換掉「AdminRefreshToken.ReplacedById 這一個屬性」的行為，不是整條慣例。
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Conventions.Replace(serviceProvider =>
            new AdminRefreshTokenReplacedByIdIndexSuppressingConvention(
                serviceProvider.GetRequiredService<ProviderConventionSetBuilderDependencies>()));
    }
}

/// <summary>
/// 見 <see cref="ClubDbContext.ConfigureConventions"/> 的說明註解。
/// 除了對 <see cref="AdminRefreshToken.ReplacedById"/> 這一個屬性跳過建立索引之外，
/// 其餘行為（含其餘 137 張表既有的自動索引落差）完全繼承 <see cref="ForeignKeyIndexConvention"/> 原樣。
/// </summary>
internal sealed class AdminRefreshTokenReplacedByIdIndexSuppressingConvention : ForeignKeyIndexConvention
{
    public AdminRefreshTokenReplacedByIdIndexSuppressingConvention(ProviderConventionSetBuilderDependencies dependencies)
        : base(dependencies)
    {
    }

    protected override IConventionIndex? CreateIndex(
        IReadOnlyList<IConventionProperty> properties,
        bool unique,
        IConventionEntityTypeBuilder entityTypeBuilder)
    {
        if (properties.Count == 1
            && entityTypeBuilder.Metadata.ClrType == typeof(AdminRefreshToken)
            && properties[0].Name == nameof(AdminRefreshToken.ReplacedById))
        {
            return null;
        }

        return base.CreateIndex(properties, unique, entityTypeBuilder);
    }
}
