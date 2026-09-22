using Microsoft.EntityFrameworkCore;
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
}
