namespace Tcrfc.Api.Common;

/// <summary>「依清單順序重排」的共用請求（E1a 起新增模組共用）。<c>Ids</c> 是希望排在最前面的 id，依序排列。</summary>
public sealed record ReorderRequest
{
    public required IReadOnlyList<Guid> Ids { get; init; }
}

/// <summary>批次操作被略過的單筆（例如共同內容、類型不相容），<c>Reason</c> 是給畫面的日常中文。</summary>
public sealed record BatchSkippedItemDto
{
    public required Guid Id { get; init; }
    public required string Reason { get; init; }
}

/// <summary>批次操作結果：能處理的處理、不能處理的列進 <c>Skipped</c>（不是全有全無），語意同 B4 常見問題的批次操作。</summary>
public sealed record BatchOperationResultDto
{
    public required int UpdatedCount { get; init; }
    public required IReadOnlyList<BatchSkippedItemDto> Skipped { get; init; }
}

/// <summary>批次操作請求：<c>Ids</c> 一次最多 200 筆。</summary>
public sealed record BatchIdsRequest
{
    public required IReadOnlyList<Guid> Ids { get; init; }
}
