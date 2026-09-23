namespace Tcrfc.Api.Security;

/// <summary>
/// 「這個已登入的人，有沒有權限對這個俱樂部做這件事」——已驗證的結果。建構子 internal，
/// 只有 <see cref="IAdminClubAuthorizer"/> 能建立實例，理由與既有的 <see cref="ClubScope"/>
/// 完全相同（見該檔案上的說明）：型別系統擋掉「忘記驗證就寫資料庫」這條路。
///
/// 與 <see cref="ClubScope"/> 的關係：**本型別包一層，不取代**。公開唯讀端點（既有五組 GET）
/// 繼續只用 <see cref="ClubScope"/>——任何人都能用網址指定俱樂部瀏覽公開內容，這是既有行為，
/// 不因為本次加入登入系統而改變。只有後台寫入與後台專用讀取端點改用 <see cref="AdminClubScope"/>，
/// 多疊一層「這個人是否被授權存取這個俱樂部」的檢查。
///
/// 🔴🔴🔴 2026-09-23（使用者裁決，型別層強制授權）：**內部包的 <see cref="ClubScope"/> 不對外
/// 公開**（沒有 <c>.Club</c> 這個解包出口），只暴露 <see cref="ClubId"/>／<see cref="ClubCode"/>
/// 兩個扁平化屬性。理由：如果 <c>.Club</c> 是公開的，取出來的是一個「跟 <see cref="IClubResolver"/>
/// 產生的合法 <see cref="ClubScope"/> 一模一樣」的值——沒有任何型別上的標記能區分「這把 scope
/// 是通過授權拿到的」與「這把 scope 只是查了俱樂部代碼存不存在」。真正的強制在於：
/// **後台 repository 的方法簽章一律宣告 <c>AdminClubScope</c>，不是 <c>ClubScope</c>**
/// （見 <c>Features/AdminNews/AdminArticlesRepository.cs</c> 與 apps/api/README.md
/// 「新增後台端點的必要形狀」）——拿掉 <c>.Club</c> 這個出口，是為了讓「這個方法只想要俱樂部
/// 代碼、不在乎授不授權」這種寫法**在型別層面就不方便寫出來**：要嘛乖乖收 <c>AdminClubScope</c>
/// （只能從 <see cref="IAdminClubAuthorizer.AuthorizeAsync"/> 拿到），要嘛只能拿到
/// <c>Guid</c>／<c>string</c> 這種完全不帶「已授權」語意的裸值，沒有一條路能不小心產生出一個
/// 「看起來像已授權、其實只是隨手查來的」<see cref="ClubScope"/>。
///
/// 🔴🔴🔴 2026-09-23（使用者裁決，堵 `default` 破口）：**這個型別是 `class` 不是 `struct`。**
/// 理由與 <see cref="ClubScope"/> 上的說明完全相同——`readonly struct` 的 `default` 永遠繞過
/// 建構子產生一個零值實例，`internal` 建構子擋不住。改成 class 後，`default`／
/// `default(AdminClubScope)` 是 `null`，用它存取 <see cref="ClubId"/> 等屬性會立刻
/// <see cref="NullReferenceException"/>，不會是一個能通過型別檢查、看起來合法的偽造範圍。
/// </summary>
public sealed class AdminClubScope
{
    private readonly ClubScope _club;

    /// <summary>俱樂部主鍵。與 <see cref="ClubScope.ClubId"/> 同一個值，扁平化暴露，
    /// 不透過中介的 <see cref="ClubScope"/> 物件——理由見本型別上的說明。</summary>
    public Guid ClubId => _club.ClubId;

    /// <summary>俱樂部代碼（如 "tcrfc"、"bw"）。僅供記錄與回應標註、快取 key、物件儲存路徑組字串用，
    /// 不用於任何 SQL 組字串。</summary>
    public string ClubCode => _club.ClubCode;

    public AdminIdentity Identity { get; }

    internal AdminClubScope(ClubScope club, AdminIdentity identity)
    {
        _club = club;
        Identity = identity;
    }
}
