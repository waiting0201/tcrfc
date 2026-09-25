namespace Tcrfc.Api.Features.AdminPrograms;

public abstract class AdminProgramException(string message) : Exception(message);

public sealed class AdminProgramValidationException(string message) : AdminProgramException(message);

/// <summary>網址名稱（<c>slug</c>）在同一個俱樂部底下重複——對應 <c>UQ_programs_club_slug</c>
/// （<c>club_id</c>、<c>slug</c>）複合唯一鍵。形狀比照 <c>Features/AdminNews/ArticleSlugConflictException</c>。</summary>
public sealed class ProgramSlugConflictException(string slug)
    : AdminProgramException($"網址名稱「{slug}」已經有其他課程／營隊項目使用，請換一個。");

/// <summary>圖片欄位插槽把「封面圖」對到 <c>programs.cover_key</c> 三態，形狀比照
/// <c>Features/AdminStaff/StaffPhotoKeyUpdate</c>。</summary>
public readonly record struct ProgramCoverKeyUpdate(bool Change, string? NewKey)
{
    public static readonly ProgramCoverKeyUpdate Keep = new(false, null);
    public static ProgramCoverKeyUpdate Set(string? newKey) => new(true, newKey);
}
