namespace Tcrfc.Api.Security;

/// <summary>
/// 「這個已登入的人，有沒有權限做這件不分俱樂部的系統管理操作」——已驗證的結果。
/// 供 J1 帳號管理／J2 角色與權限／J4 俱樂部主檔與俱樂部授權（皆為全域操作，不含 <c>{club}</c>
/// 路由段）使用，與既有的 <see cref="AdminClubScope"/>（俱樂部範圍操作）是同一設計哲學的
/// 另一半——沿用同一套「型別層強制授權」（見 <see cref="AdminClubScope"/> 上的完整說明），
/// 建構子 internal，只有 <see cref="IAdminSystemAuthorizer"/> 的實作能建立實例。
///
/// 🔴 這個型別也是 <c>sealed class</c> 不是 <c>readonly struct</c>——理由與
/// <see cref="ClubScope"/>／<see cref="AdminClubScope"/> 完全相同：<c>struct</c> 的
/// <c>default</c> 永遠繞過建構子產生零值實例，<c>internal</c> 建構子擋不住；改用 <c>class</c>
/// 後 <c>default</c>／<c>default(AdminSystemScope)</c> 是 <c>null</c>，一用就
/// <see cref="NullReferenceException"/>，不會是一個看起來合法的偽造值。
/// <see cref="Tcrfc.Api.Tests.ArchitectureTests"/> 已把本型別納入同一組 Roslyn 語意掃描。
/// </summary>
public sealed class AdminSystemScope
{
    public AdminIdentity Identity { get; }

    internal AdminSystemScope(AdminIdentity identity)
    {
        Identity = identity;
    }
}
