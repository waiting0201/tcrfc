using System.Text.Json;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 🔴 API 回應是 camelCase（Program.cs 設定 <c>JsonNamingPolicy.CamelCase</c>），但這裡直接引用
/// 的是 <c>Tcrfc.Api</c> 專案裡的 PascalCase record（例如 <c>PagedResult&lt;PlayerDto&gt;</c>）。
/// <see cref="System.Net.Http.Json.HttpClientJsonExtensions.GetFromJsonAsync{TValue}(HttpClient, string)"/>
/// 不帶參數時用的是預設 <see cref="JsonSerializerOptions"/>，預設**大小寫敏感**——不特別指定
/// <see cref="JsonSerializerOptions.PropertyNameCaseInsensitive"/>，camelCase 的 JSON 會對不上
/// PascalCase 的屬性，欄位會悄悄變成 null／預設值而不是丟例外，是很難被發現的假陰性。
/// 全部測試一律用這份選項讀 JSON。
/// </summary>
public static class TestJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// 本輪（後台新聞寫入）新增：<c>PostAsJsonAsync</c>／<c>PutAsJsonAsync</c> 送出的請求主體要用
    /// camelCase（Program.cs 的 <c>JsonOptions.SerializerOptions.PropertyNamingPolicy</c> 是
    /// CamelCase，模型繫結解析請求主體用的是同一份設定），否則 PascalCase 的 C# record 屬性名稱
    /// 送出去對不上，欄位會悄悄繫結成預設值而不是驗證失敗（跟上面 <see cref="Options"/> 的
    /// 讀取端問題是同一種坑，只是方向相反）。
    /// </summary>
    public static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}
