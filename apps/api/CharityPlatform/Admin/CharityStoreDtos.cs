namespace Tcrfc.Api.CharityPlatform.Admin;

// N1 店家管理與 QR Code（規劃書 §6.1）。⚠️ 慈善「捐款合作店家」（DonationStore，掃碼引流、有金流有分潤）與主站的
// K4 特約店家（PartnerStore，會員折扣、無金流無分潤）是兩張不同資料庫裡的不同表，完全獨立——
// 介面上也必須用不同名稱（本模組叫「捐款合作店家」）。

public sealed record AdminStoreListItemDto
{
    public required Guid Id { get; init; }

    /// <summary>QR Code 目標網址的識別字串，系統產生、不可由編號推導、全站唯一（規劃書 §2.3）。僅是歸屬標記。</summary>
    public required string Slug { get; init; }

    public required string? NameZh { get; init; }
    public required string? NameEn { get; init; }
    public required string? Category { get; init; }

    /// <summary><c>active</c>（合作中）／<c>inactive</c>（已停止）。</summary>
    public required string Status { get; init; }

    /// <summary>店家分潤百分比（0–100，兩位小數）。</summary>
    public required decimal StoreSharePct { get; init; }

    public required DateOnly? StartOn { get; init; }
    public required DateOnly? EndOn { get; init; }
    public required string? LogoUrl { get; init; }

    /// <summary>累計已付款筆數、金額與應付回饋金（只計 <c>paid</c>；規劃書 §6.1「檢視」，連向 N4）。</summary>
    public required int PaidCount { get; init; }

    public required long PaidTotal { get; init; }
    public required long StoreShareAccrued { get; init; }
}

public sealed record AdminStoreDetailDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public required string? NameZh { get; init; }
    public required string? NameEn { get; init; }
    public required string? LogoAltZh { get; init; }
    public required string? LogoAltEn { get; init; }
    public required string? Category { get; init; }
    public required string? Address { get; init; }
    public required string? ContactName { get; init; }
    public required string? ContactPhone { get; init; }
    public required string Status { get; init; }
    public required decimal StoreSharePct { get; init; }
    public required DateOnly? StartOn { get; init; }
    public required DateOnly? EndOn { get; init; }
    public required string? LogoUrl { get; init; }
    public required int PaidCount { get; init; }
    public required long PaidTotal { get; init; }
    public required long StoreShareAccrued { get; init; }

    /// <summary>QR Code 實際編碼的網址（一律指向中文版：<c>{前台網址}/zh/s/{slug}</c>）；前台網址尚未設定時為 <c>null</c>。</summary>
    public required string? QrTargetUrl { get; init; }
}

/// <summary>建立／更新店家。<c>slug</c> 一律由系統產生，不接受外部指定；分潤百分比需要獨立的「設定分潤」權限，
/// 省略（<c>null</c>）代表不變（更新時）或 0（建立時）。</summary>
public sealed record UpsertStoreRequest
{
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }
    public string? LogoAltZh { get; init; }
    public string? LogoAltEn { get; init; }
    public string? Category { get; init; }
    public string? Address { get; init; }
    public string? ContactName { get; init; }
    public string? ContactPhone { get; init; }
    public DateOnly? StartOn { get; init; }
    public DateOnly? EndOn { get; init; }

    /// <summary><c>active</c>／<c>inactive</c>；省略時建立為 <c>active</c>、更新時不變。</summary>
    public string? Status { get; init; }

    public decimal? StoreSharePct { get; init; }
}

public sealed record RegenerateStoreSlugRequest
{
    /// <summary>二次確認（規劃書 §2.3：重產後舊 QR 立即失效，須明確警示）。必須為 <c>true</c>。</summary>
    public bool Confirm { get; init; }
}

public sealed record AdminStoreSlugResponse(Guid Id, string Slug, string? QrTargetUrl);
