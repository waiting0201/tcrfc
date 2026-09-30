using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tcrfc.Api.Common;

/// <summary>
/// 全站 <see cref="DateTime"/> 的 JSON 慣例（D 批，2026-09-30，回應 C 批畫面的回報「前端得猜這個時間有沒有帶時區」）：
/// <b>輸出一律是 UTC 並帶 <c>Z</c></b>（<c>2026-09-30T05:00:00.123Z</c>）；資料庫的時間戳一律存 UTC，EF／Dapper 讀回的 <see cref="DateTime"/>
/// 其 <see cref="DateTimeKind"/> 是 <c>Unspecified</c>，System.Text.Json 預設會把它輸出成<b>不帶 <c>Z</c></b>，前端只能猜——這裡統一補上。
/// <b>輸入</b>：帶時區位移或 <c>Z</c> 的值換算成 UTC；<b>沒有時區記號的值視為 UTC</b>（E-90：不得當成伺服器本機時間換算）。
/// 只影響 <see cref="DateTime"/>／<see cref="Nullable{DateTime}"/>；<see cref="DateOnly"/>（台灣當地日期）與 <see cref="DateTimeOffset"/> 不受影響。
/// </summary>
public sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString();
        if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var value))
        {
            throw new JsonException("日期時間的格式不正確，請使用 ISO 8601（例如 2026-09-30T05:00:00Z）。");
        }

        return value.UtcDateTime;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
        writer.WriteStringValue(utc);
    }
}
