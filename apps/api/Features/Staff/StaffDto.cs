using Tcrfc.Api.Features.Seo;

namespace Tcrfc.Api.Features.Staff;

/// <summary>教練與團隊成員公開欄位。<c>staff</c> 不在受限欄位清單內（docs/12b-database-tables.md §8）。
/// 🔴 <see cref="PhotoKey"/> 例外：<c>portrait_consent_status = 'not_consented'</c> 時一律回
/// <c>null</c>（fail-closed），見 <c>StaffRepository.Map</c>，理由同 <c>Features/Players/PlayerDto.cs</c>。</summary>
public sealed record StaffDto
{
    public required Guid Id { get; init; }
    public string? StaffGroup { get; init; }
    public string? Licence { get; init; }
    public string? PhotoKey { get; init; }
    public string? Name { get; init; }
    public string? Title { get; init; }
    public string? Bio { get; init; }
    public required IReadOnlyList<string> TeamCodes { get; init; }

    /// <summary>true＝這筆是兩隊共同資料（<c>club_id IS NULL</c>），不是本俱樂部專屬——
    /// docs/17-deployment.md §6「俱樂部專屬優先、回退共同」弱讀法讓呼叫端知道是不是回退結果。</summary>
    public required bool IsShared { get; init; }

    /// <summary>教練／團隊成員照片完整可公開存取網址（S1-12f 新增），理由與計算方式同
    /// <c>Features/Players/PlayerDto.PhotoUrl</c>：已套用肖像同意 fail-closed 規則後的
    /// <see cref="PhotoKey"/> 經 <see cref="Tcrfc.Api.Images.IImagePublicUrlResolver"/> 算出。</summary>
    public string? PhotoUrl { get; init; }

    /// <summary>GEO-05（S1-12c／S1-12f）：這筆教練／團隊成員資料是否足以輸出 Person 結構化資料
    /// （<see cref="SchemaType.Person"/> 只要求 <c>name</c>）。判斷條件單一來源見
    /// <see cref="SchemaRequiredFields"/>，這裡不重新判斷一次（E-39）。</summary>
    public required bool SchemaEligible { get; init; }
}
