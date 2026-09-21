namespace Tcrfc.Api.Data;

/// <summary>
/// 「俱樂部專屬優先、回退共同」弱讀法的唯一實作來源（docs/17-deployment.md §6、`STATUS.md` B-12）。
/// 只用在 9 張 <c>club_id</c> 可為空的表（本次任務碰到的是 <c>staff</c>、<c>articles</c>）。
/// ⛔ 這段邏輯只能寫在這裡一次——docs/17 §6 明訂「共用小資料」查詢分散在各端點是會失控的坑，
/// 每個 repository 一律 <c>string.Format</c> 這裡的常數，不得各自重寫等價的 SQL 片段。
/// </summary>
public static class ClubOrSharedSql
{
    /// <summary>WHERE 片段：命中本俱樂部專屬列，或命中 <c>club_id IS NULL</c> 的共同列。</summary>
    public const string WhereClubOrShared = "(club_id = @ClubId OR club_id IS NULL)";

    /// <summary>ORDER BY 片段：俱樂部專屬列排在共同列之前（用於「同一業務鍵兩者皆存在」的情境）。</summary>
    public const string OrderClubBeforeShared = "CASE WHEN club_id IS NULL THEN 1 ELSE 0 END";
}
