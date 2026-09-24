namespace Tcrfc.Api.Features.AdminPlayers;

public abstract class AdminPlayerException(string message) : Exception(message);

public sealed class AdminPlayerValidationException(string message) : AdminPlayerException(message);

/// <summary>圖片欄位插槽把「照片」對到 <c>players.photo_key</c> 三態，形狀比照
/// <c>Features/AdminNews/CoverKeyUpdate.cs</c>。</summary>
public readonly record struct PhotoKeyUpdate(bool Change, string? NewKey)
{
    public static readonly PhotoKeyUpdate Keep = new(false, null);
    public static PhotoKeyUpdate Set(string? newKey) => new(true, newKey);
}
