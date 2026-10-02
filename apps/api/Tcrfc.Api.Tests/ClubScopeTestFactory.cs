using System.Reflection;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 只給「不連資料庫的離線測試」用：<see cref="ClubScope"/>／<see cref="AdminClubScope"/> 的建構子是 <c>internal</c>（唯一合法產生者是
/// <c>ClubResolver</c>／<c>AdminClubAuthorizer</c>，這是刻意的型別層防線，見 <c>ArchitectureTests</c>），離線測試沒有資料庫可以解析俱樂部與授權，所以用反射建一個。
/// 正式程式碼不得這樣做；ArchitectureTests 掃的是 <c>apps/api</c> 的正式程式碼，不含測試專案。
/// </summary>
internal static class ClubScopeTestFactory
{
    public static ClubScope Create(Guid clubId, string clubCode)
        => (ClubScope)Activator.CreateInstance(
            typeof(ClubScope), BindingFlags.Instance | BindingFlags.NonPublic, binder: null, args: [clubId, clubCode], culture: null)!;

    public static AdminClubScope CreateAdmin(Guid clubId, string clubCode, bool isSuperAdmin = true)
        => (AdminClubScope)Activator.CreateInstance(
            typeof(AdminClubScope), BindingFlags.Instance | BindingFlags.NonPublic, binder: null,
            args: [Create(clubId, clubCode), new AdminIdentity(Guid.NewGuid(), "offline-test", isSuperAdmin)], culture: null)!;
}
