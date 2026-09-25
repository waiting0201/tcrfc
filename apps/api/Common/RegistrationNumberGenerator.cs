using System.Security.Cryptography;

namespace Tcrfc.Api.Common;

/// <summary>
/// P3 報名管理的人類可讀報名編號（<c>registrations.registration_no</c>，<c>nvarchar(32)</c>，
/// UNIQUE：<c>UQ_registrations_registration_no</c>）產生器。
///
/// 規劃書（前台報名流程，主站規劃書 3.5／行 389）只要求「送出 → 產生報名編號」，沒有定義格式，
/// 本檔採「最小可行」原則自訂（比照 <c>Common/CsvUtils.cs</c> 檔頭同樣的既有慣例：規劃書沒有
/// 逐欄定義格式時，由本輪依最小可行原則決定，並在這裡寫清楚）。
///
/// 格式：<c>{俱樂部代碼大寫}-{yyyyMMdd}-{6 碼隨機}</c>，例如 <c>TCRFC-20260925-K7QXM2</c>／
/// <c>BW-20260925-K7QXM2</c>。俱樂部代碼目前最長是 "tcrfc"（5 碼），整體長度遠低於欄位上限
/// 32 字元，保留充裕的未來俱樂部代碼加長空間。
///
/// 🔴 **不依賴 <c>row_seq</c>／IDENTITY 值排編號**：<c>registration_no</c> 是 NOT NULL 且要在
/// INSERT 當下就有值，若編號要嵌入 IDENTITY 值就得先插入再回頭 UPDATE，徒增一次往返且讓
/// UNIQUE 約束在兩個時間點各驗證一次。改採「日期＋隨機碼」，隨機碼故意排除易混淆字元
/// （<c>0/O</c>、<c>1/I/L</c>），供人工在電話或現場核對報名編號時使用。**呼叫端仍須處理
/// 「隨機碰撞」的極端情況**（機率上可忽略但非零）：寫入前用
/// <c>UQ_registrations_registration_no</c> 违反時重新產生一次再試，見
/// <c>Features/Programs/ProgramsRepository.CreatePublicRegistrationAsync</c>／
/// <c>Features/AdminRegistrations/AdminRegistrationsRepository.CreateAsync</c> 的重試迴圈。
/// </summary>
public static class RegistrationNumberGenerator
{
    // 排除 0/O、1/I/L 等易混淆字元，供電話／現場核對報名編號時使用。
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
    private const int RandomLength = 6;

    public static string Generate(string clubCode, DateTime utcNow)
    {
        var datePart = utcNow.ToString("yyyyMMdd");
        var randomPart = RandomChars(RandomLength);
        return $"{clubCode.ToUpperInvariant()}-{datePart}-{randomPart}";
    }

    private static string RandomChars(int length)
    {
        Span<char> buffer = stackalloc char[length];
        for (var i = 0; i < length; i++)
        {
            buffer[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }
        return new string(buffer);
    }
}
