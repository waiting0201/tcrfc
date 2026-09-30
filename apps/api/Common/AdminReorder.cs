namespace Tcrfc.Api.Common;

/// <summary>
/// 「依清單順序重排」的共用計算（E1a 起各模組的 <c>PUT …/order</c> 通則）：請求裡的 id 依序排最前面，
/// 未列出的維持原本相對順序接在後面；空清單、重複或不存在的 id 一律 400。
/// </summary>
public static class AdminReorder
{
    /// <param name="currentOrder">目前的排序（已依 <c>sort_order</c> 排好）。</param>
    /// <param name="requested">請求的排序。</param>
    /// <param name="subject">給畫面的名詞（例如「角色」「內頁」）。</param>
    public static IReadOnlyList<Guid> Compute(IReadOnlyList<Guid> currentOrder, IReadOnlyList<Guid> requested, string subject)
    {
        if (requested.Count == 0 || requested.Distinct().Count() != requested.Count)
        {
            throw new AdminValidationException("排序清單不可為空，也不可重複。");
        }

        var known = currentOrder.ToHashSet();
        if (requested.Any(id => !known.Contains(id)))
        {
            throw new AdminValidationException($"排序清單含有不存在的{subject}，請重新整理後再試。");
        }

        var requestedSet = requested.ToHashSet();
        return requested.Concat(currentOrder.Where(id => !requestedSet.Contains(id))).ToList();
    }
}
