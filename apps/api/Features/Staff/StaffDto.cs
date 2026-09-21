namespace Tcrfc.Api.Features.Staff;

/// <summary>教練與團隊成員公開欄位。<c>staff</c> 不在受限欄位清單內（docs/12b-database-tables.md §8）。</summary>
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
}
