namespace Tcrfc.Api.Features.AdminFaqs;

/// <summary>FAQ 快捷區塊（G-12）掛載點字典（S1-7a）——供後台下拉選單使用，本身不提供新增／
/// 刪除 CRUD，四筆固定值由種子 DML 灌入（<c>db/seed/generate-club-seed-sql.py</c> §21），
/// 見 db/club-schema.sql <c>faq_embed_slots</c> 表註解。</summary>
public sealed record AdminFaqEmbedSlotDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
}
