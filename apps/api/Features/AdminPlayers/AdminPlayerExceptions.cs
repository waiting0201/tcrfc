namespace Tcrfc.Api.Features.AdminPlayers;

public abstract class AdminPlayerException(string message) : Exception(message);

public sealed class AdminPlayerValidationException(string message, string? field = null) : AdminPlayerException(message), Tcrfc.Api.Common.IFieldApiException
{
    public IReadOnlyDictionary<string, string> FieldErrors { get; } = Tcrfc.Api.Common.FieldKey.Single(field, message);
}

/// <summary>同一個俱樂部內網址代稱（<c>(club_id, slug)</c>）重複。對應 409。</summary>
public sealed class AdminPlayerSlugConflictException(string message) : AdminPlayerException(message), Tcrfc.Api.Common.IFieldApiException
{
    public IReadOnlyDictionary<string, string> FieldErrors => Tcrfc.Api.Common.FieldKey.Single("slug", Message);
}

/// <summary>圖片欄位插槽把「照片」對到 <c>players.photo_key</c> 三態，形狀比照
/// <c>Features/AdminNews/CoverKeyUpdate.cs</c>。</summary>
public readonly record struct PhotoKeyUpdate(bool Change, string? NewKey)
{
    public static readonly PhotoKeyUpdate Keep = new(false, null);
    public static PhotoKeyUpdate Set(string? newKey) => new(true, newKey);
}
