namespace Tcrfc.Api.Features.AdminPrograms;

public abstract class AdminProgramException(string message) : Exception(message);

public sealed class AdminProgramValidationException(string message, string? field = null) : AdminProgramException(message), Tcrfc.Api.Common.IFieldApiException
{
    public IReadOnlyDictionary<string, string> FieldErrors { get; } = Tcrfc.Api.Common.FieldKey.Single(field, message);
}

/// <summary>網址名稱（<c>slug</c>）在同一個俱樂部底下重複——對應 <c>UQ_programs_club_slug</c>
/// （<c>club_id</c>、<c>slug</c>）複合唯一鍵。形狀比照 <c>Features/AdminNews/ArticleSlugConflictException</c>。</summary>
public sealed class ProgramSlugConflictException(string slug)
    : AdminProgramException($"網址名稱「{slug}」已經有其他課程／營隊項目使用，請換一個。"), Tcrfc.Api.Common.IFieldApiException
{
    public IReadOnlyDictionary<string, string> FieldErrors => Tcrfc.Api.Common.FieldKey.Single("slug", Message);
}

/// <summary>圖片欄位插槽把「封面圖」對到 <c>programs.cover_key</c> 三態，形狀比照
/// <c>Features/AdminStaff/StaffPhotoKeyUpdate</c>。</summary>
public readonly record struct ProgramCoverKeyUpdate(bool Change, string? NewKey, int? Width = null, int? Height = null)
{
    public static readonly ProgramCoverKeyUpdate Keep = new(false, null);

    /// <summary>換圖時帶主檔縮小後的寬高；清空時 <paramref name="newKey"/> 傳 null，寬高一併清成 null。</summary>
    public static ProgramCoverKeyUpdate Set(string? newKey, int? width = null, int? height = null) => new(true, newKey, width, height);
}
