using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Tcrfc.Api.CharityPlatform.Data;

/// <summary>
/// 對 <c>dotnet ef dbcontext scaffold</c> 產出的 <see cref="CharityDbContext"/> 做客製化，不直接改
/// <c>CharityDbContext.cs</c>（那是產生檔，之後重新 scaffold 會被整份覆寫）。
///
/// 🔴 這是<b>慈善平台的獨立資料庫</b>的 DbContext（<c>CHARITY_SQL_CONNECTION_STRING</c>），與主站的
/// <c>ClubDbContext</c> 完全分開：不共用連線、不共用 migration 歷史、不得互相 join
/// （docs/16 §1：兩個資料庫互相獨立，Azure SQL 本身也不支援跨庫查詢）。
/// 慈善的實體類別放在 <c>Tcrfc.Api.CharityPlatform.Data.Entities</c>，命名空間刻意不叫
/// <c>Tcrfc.Api.Charity</c>——主站已有名為 <c>Charity</c> 的實體類別，同名命名空間會讓主站 Features
/// 裡的 <c>Charity</c> 被解析成命名空間（<c>docs/18-work-errors.md</c> 的「功能命名空間不得與實體同名」）。
/// </summary>
public partial class CharityDbContext
{
    /// <summary>
    /// 與 <c>ClubDbContext</c> 同一個決定（S0-7l）：整條移除 EF 的 <see cref="ForeignKeyIndexConvention"/>，
    /// 外鍵要不要索引完全由 <c>db/charity-schema.sql</c> 決定，EF 模型不得替綱要沒有的外鍵欄位自動加索引，
    /// 否則 <c>migrations add</c> 會產出綱要沒有的 <c>CreateIndex</c>（基準偏移）。
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Conventions.Remove(typeof(ForeignKeyIndexConvention));
    }
}
