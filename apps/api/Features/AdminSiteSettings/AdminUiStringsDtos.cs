namespace Tcrfc.Api.Features.AdminSiteSettings;

/// <summary>介面字串（按鈕、表單標籤、提示訊息、錯誤訊息等介面文案）的雙語對照。全站共用，不分俱樂部。</summary>
public sealed record AdminUiStringDto
{
    public required Guid Id { get; init; }

    /// <summary>字串鍵，例 <c>form.submit</c>、<c>error.required</c>；建立後不可改（前台以它取字串）。</summary>
    public required string Key { get; init; }

    /// <summary>分組（例 <c>button</c>、<c>form</c>、<c>error</c>），可為 <c>null</c>。</summary>
    public string? Group { get; init; }

    /// <summary>語系代碼 → 文字；沒有翻譯的語系不在字典裡。</summary>
    public required IReadOnlyDictionary<string, string> Values { get; init; }

    public required DateTime UpdatedAt { get; init; }
}

public sealed record CreateAdminUiStringRequest
{
    public string? Key { get; init; }

    public string? Group { get; init; }

    /// <summary>至少要有預設語系（<c>zh-Hant</c>）的文字。</summary>
    public IReadOnlyDictionary<string, string?>? Values { get; init; }
}

/// <summary>更新：<see cref="Values"/> 只處理有出現的語系（空白文字＝刪除該語系的翻譯；預設語系不能刪）。
/// 只有 <c>site.string.update</c> 能改分組與預設語系文字；只有 <c>site.string.translate</c>（翻譯人員）只能改非預設語系，碰到其他欄位回 403。</summary>
public sealed record UpdateAdminUiStringRequest
{
    public string? Group { get; init; }

    public IReadOnlyDictionary<string, string?>? Values { get; init; }
}

public sealed record AdminUiStringListQuery
{
    public string? Group { get; init; }

    public string? Keyword { get; init; }

    /// <summary>只列「這個語系還沒有翻譯」的字串，例 <c>en</c>。</summary>
    public string? Missing { get; init; }

    public int? Page { get; init; }

    public int? PageSize { get; init; }
}
