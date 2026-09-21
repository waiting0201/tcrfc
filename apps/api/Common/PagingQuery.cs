namespace Tcrfc.Api.Common;

/// <summary>分頁查詢參數正規化：擋下負數、零、過大的 pageSize（避免一次撈整張表）。</summary>
public static class PagingQuery
{
    public static (int Page, int PageSize) Normalize(int? page, int? pageSize, int defaultPageSize, int maxPageSize)
    {
        var normalizedPage = page is > 0 ? page.Value : 1;
        var normalizedPageSize = pageSize switch
        {
            null or <= 0 => defaultPageSize,
            _ when pageSize > maxPageSize => maxPageSize,
            _ => pageSize.Value,
        };
        return (normalizedPage, normalizedPageSize);
    }
}
