namespace Tcrfc.Api.CharityPlatform.Security;

/// <summary>
/// 「這個已登入的慈善後台使用者，確實有權限做這件事」——已驗證的結果。慈善後台所有寫入與讀取個資的
/// repository／service 方法都要求傳入這個型別，編譯器因此強制「呼叫端必須先通過
/// <see cref="ICharityAdminAuthorizer"/>」，與主站 <c>AdminClubScope</c>／<c>AdminSystemScope</c>
/// 是同一套「型別層強制授權」哲學。
///
/// 🔴 慈善是單一法人（協會），沒有 <c>club_id</c> 範圍維度，所以這裡只有「身分＋剛剛通過的權限碼」，
/// 不是俱樂部範圍。🔴 <c>sealed class</c> 不是 <c>readonly struct</c>：struct 的 <c>default</c> 會繞過
/// <c>internal</c> 建構子產生一個「看起來合法」的零值，class 的 <c>default</c> 是 <c>null</c>，一用就 NRE。
/// <c>Tcrfc.Api.Tests.ArchitectureTests</c> 已把本型別納入同一組 Roslyn 語意掃描：除了
/// <see cref="CharityAdminAuthorizer"/> 以外，任何地方建構它都會讓 <c>dotnet test</c> 變紅。
/// </summary>
public sealed class CharityAdminScope
{
    public CharityAdminIdentity Identity { get; }

    /// <summary>剛剛通過檢查的權限碼（供稽核紀錄與除錯用；介面不得顯示，規劃書 §4.0）。</summary>
    public string PermissionCode { get; }

    internal CharityAdminScope(CharityAdminIdentity identity, string permissionCode)
    {
        Identity = identity;
        PermissionCode = permissionCode;
    }
}
