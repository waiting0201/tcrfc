# apps/api — api（單一 .NET 行程，唯讀讀取 API）

**S0-7b（2026-09-21，`backend-engineer`）**：讓 Nuxt 前台（[`apps/web`](../web/README.md)）能打真實資料庫，
建立球員、教練與職員、新聞、賽程與賽果、俱樂部主檔五組**唯讀** GET 端點。

**S0-7d（2026-09-21，`backend-engineer`）**：補齊 S0-7b 刻意縮減的三個缺口——① `/readyz` 加上慈善庫與
Redis 檢查 ② `Caching/IQueryCache.cs` 接縫接上真正的 Redis 實作 ③ 建立 `Tcrfc.Api.Tests` 自動化測試專案
（S0-7b 為止零測試）。細節見下方各段落與「S0-7d 驗收紀錄」。

~~🔴 不含：登入與權限~~ ← **已於 S1（下方）補上**。仍不含：商店與金流、除新聞以外的後台模組寫入端點、
慈善捐款平台的業務功能（S0-7d 對慈善庫只做「開一條連線探活」，不做任何查詢，見下方「`/readyz` 範圍」）。

---

## S1：J1–J3 登入與授權地基（2026-09-23，`backend-engineer`）

**背景**：S0-7 到 S0-8 為止，`apps/api` 完全沒有身分驗證（grep `Authentication`／`Authorize`／
`JwtBearer` 零命中），`Features/AdminNews` 的寫入端點只靠 `Security/DevWriteGate.cs`
（環境旗標 `ENABLE_UNSAFE_DEV_WRITES`）擋住，`created_by`／`updated_by` 由
`Security/IDevOperatorResolver.cs` 從呼叫端自報的 `X-Dev-Operator-Id` 標頭解析——
**這代表這組端點在拿掉開發旗標之前無法在任何環境真正上線**。本輪補齊登入、角色與權限、
資料範圍強制三塊地基，並把 `Features/AdminNews` 改走真實授權。

> 🔴 **2026-09-23 事後更正（使用者裁決）**：本輪最初也做了 J3 操作稽核與登入紀錄
> （`admin_audit_logs`／`admin_login_logs` 兩張表），但派工時沒有先查
> [`docs/12-database-schema.md`](../../docs/12-database-schema.md) §13.1——那一節逐字記著
> **委託方明文指示「本次資料庫設計不含 log」**，明定不建 `AuditLog`／`LoginLog`／`ExportLog`／
> `OperationLog`。發現這個衝突後如實回報，使用者裁決**撤回這兩張表，回到文件記載的狀態**
> （§13.1 本身寫著「若日後法遵或客戶要求補上，只需新增表，不需改動既有綱要」，撤回成本最低，
> 等客戶重新確認再補）。下文已整份改寫為撤回後的現狀，**不再描述已撤回的 J3 稽核機制**，
> 只在明確標註「已撤回」的地方留下記錄供之後參考。**帳號鎖定功能已查證不受影響**——
> `AdminUser.FailedAttemptCount`／`LockedUntil` 兩個欄位本來就是唯一的鎖定判斷依據，
> 從未讀寫過日誌表，見下方「密碼與鎖定」段落。

### 讀到的規劃書條文

| 章節 | 行號 | 內容 |
|---|---|---|
| 主站 §4.10 J | 1208–1214 | J1 帳號管理：新增／停用帳號、密碼政策、2FA、`primary_club_id` |
| 主站 §6 權限與角色矩陣 | 1597–1643 | 十種角色、資料範圍欄、「合作球隊管理」邊界 |
| 主站 §6 資料範圍規則 | 1627–1633 | 「有效範圍＝`AdminUserClub` 中啟用且未到期的俱樂部集合」「必須在資料存取層強制」「授權有起訖日，到期自動失效」 |
| 主站 §5.3 | 1537–1549 | `AdminUserClub`／`AdminUserTeam` 兩張關聯表的設計理由；授權掛在人不掛在角色 |
| docs/12b §7 | 全節 | 七張表、`scope_mode`、權限碼命名慣例、`username` 不用 Email、種子超管 `sa@system.local` |
| docs/12 §13.1 | 626–666 | 🔴 **本版無稽核與登入日誌表，是委託方明文指示**——本輪應該先查這條再動工，沒有查是這次的疏漏，見上方「事後更正」 |

### 我判斷的範圍切法

任務指示原本按 `STATUS.md` 的 `S1-1`（多俱樂部地基）→`S1-2`（站台切換器／`AdminUserClub`／`J4`）→
`S1-3`（`J1`／`J2`／`J3`）排序。`S1-1` 的核心機制（`ClubScope` 型別層強制）已在 S0-7b 做完；
`J2` 要生效必須先有 `AdminUserClub` 才能測，所以這次一次做完「認證＋授權地基」，把 `J4`
的**完整** CRUD（俱樂部主檔管理、法人資料維護）留給下一輪——本輪只做了 `AdminUserClub` 的
**授權判斷邏輯**（`AdminClubAuthorizer` 讀取）與**種子資料**，沒有做「後台畫面點兩下就能發俱樂部
授權」的公開管理端點，理由與後果見下方「本輪沒做的部分」。

### 後台登入權杖設計（執行層決定，規劃書 §1.3 明文排除技術選型）

**存取權杖（access token）：短效 JWT，15 分鐘。**
- 簽章金鑰讀 `JWT_SIGNING_KEY_CLUB`——這個鍵名不是本輪新發明，`deploy/dev/club.env`／
  `docs/20-cicd.md` §7.2 早就預留了（「官網前後台登入權杖簽章」），本輪是第一次真的讀它。
- Claims 只放身分（`sub`／`username`／`is_super_admin`／`jti`），**不放權限與俱樂部範圍**。
  這是刻意的：若把權限或範圍簽進 token，撤銷 `AdminUserClub` 授權或調整角色權限要等 token
  過期才生效，直接牴觸規劃書「到期自動失效」「資料範圍必須在資料存取層強制」。15 分鐘是
  「即使查詢層有快取空窗，最多暴露多久」的上限，每個受保護端點仍即時查 `admin_user_clubs`／
  `role_permissions`（見 `AdminClubAuthorizer`），不快取授權判斷結果。
- `AdminClubAuthorizer` 也**不信任 token 裡的 `is_super_admin` claim**——每次都重查
  `admin_users.is_super_admin`／`status`／`must_change_password`／`two_factor_enabled`，
  避免帳號在 token 簽發後被停用或降級，仍在 15 分鐘效期內誤判。

**更新權杖（refresh token）：不透明亂數字串，不是 JWT。**
- 比照 [`docs/19-app-tech-stack.md`](../../docs/19-app-tech-stack.md) 對 App 更新權杖的既有
  硬性要求（「必須是可由伺服器端撤銷的不透明字串，不得用長效 JWT」）——後台採同一套哲學是
  刻意的一致性選擇。伺服器只存 SHA-256 雜湊（`admin_refresh_tokens.token_hash`），原始值只在
  核發當下出現一次。
- **輪替＋重放偵測**：每次 `/auth/refresh` 成功就撤銷舊權杖、發一把新的（`replaced_by_id`
  串成鏈）。若偵測到「已撤銷的權杖又被拿來用」（唯一可能是權杖外流被偷用），**立刻撤銷該帳號
  名下全部有效權杖**，逼真正的使用者也要重新登入——寧可誤傷一次，不留一個持續有效的偷來的權杖。
  見 `Features/AdminAuth/AdminAuthService.cs` 的 `RefreshAsync`，測試見
  `AdminAuthTests.更新權杖_輪替後舊權杖失效_重用會被偵測且整批撤銷`。
- **存放**：`__Host-` 前綴 Cookie（`docs/14-invariants.md` 明文規定），`HttpOnly`＋`Secure`＋
  `Path=/`、⛔ 不設 `Domain`。存取權杖則回在 JSON body，由前端保存在記憶體（不建議 localStorage，
  避免 XSS 竊取），這是常見的 SPA 安全模式（更新權杖 Cookie 拿不到就沒有東西可長期竊取）。
- ⚠️ **`SameSite=None`（不是 `Lax`／`Strict`）**：`apps/admin`（後台前端）與本 API 是不同來源
  （不同子網域，`docs/17-deployment.md` 的部署拓撲），跨站 `fetch` 要帶 Cookie 必須
  `SameSite=None`，`Lax`／`Strict` 都會讓瀏覽器悄悄不送出這顆 Cookie，整條更新機制形同虛設。
  這是 __Host- 前綴＋多網域拓撲下的必然取捨，CORS 已同步加 `AllowCredentials()`（`Program.cs`）。

**密碼雜湊：Argon2id**（`docs/12b` §7.6「演算法待選型，優先 Argon2id，次選 bcrypt」——本輪選型落地）。
用 `Konscious.Security.Cryptography.Argon2` 套件（純受控 C#，無原生相依，Docker 部署不會遇到
「映像檔缺 .so」問題）。參數 `m=64MiB, t=3, p=1`（比 OWASP 2024 建議下限更保守），輸出 PHC 風格
字串（含 salt 與參數，日後調參不影響舊雜湊的可驗證性）。見 `Security/PasswordHasher.cs`。

**2FA：TOTP（RFC 6238）**，手刻不引套件（演算法本身只需要 `HMACSHA1` 與 Base32，.NET 內建已足夠）。
密鑰用 ASP.NET Core **Data Protection API** 加密存 `two_factor_secret_encrypted`
（`Security/TwoFactorSecretProtector.cs`）。

🔴🔴🔴 **正式環境部署前置條件（本次程式碼無法保證，必須在部署時設定）**：Data Protection 預設把
金鑰環存在容器本機檔案系統，**容器重建（不是重啟）沒有把金鑰目錄掛到持久化 volume，所有使用者的
2FA 密鑰會永久無法解密**（不是可恢復的錯誤，是資料實質遺失）。部署時務必設定
`DATA_PROTECTION_KEYS_PATH` 指向持久化 volume 路徑，細節見 `TwoFactorSecretProtector.cs` 檔頭。

**強制密碼更換與強制 2FA 在哪裡擋**：`Security/AdminClubAuthorizer.cs` 對**每一個俱樂部範圍端點**
（含 `Features/AdminNews` 全部路由）在授權檢查的第②③步之間插入這兩個檢查——
`must_change_password=true` 或 `two_factor_enabled=false` 一律 403。`/auth/change-password`、
`/auth/2fa/setup`、`/auth/2fa/confirm` 本身不是俱樂部範圍端點（沒有 `{club}` 路由段），
不經過 `AdminClubAuthorizer`，才不會變成雞生蛋蛋生雞的死結。

### `ClubScope` 與授權怎麼接

沿用 S0-7b 的既有原則（型別系統擋掉「忘記驗證就查資料庫」），疊一層不取代：

```
公開唯讀端點（既有五組 GET）：  路由 → IClubResolver.ResolveAsync → ClubScope → repository
後台俱樂部範圍端點（AdminNews）：路由 → IAdminClubAuthorizer.AuthorizeAsync → AdminClubScope → repository
```

`AdminClubScope`（`Security/AdminClubScope.cs`）是 `readonly struct`，建構子 `internal`，只有
`AdminClubAuthorizer` 能建立，包一層 `ClubScope` ＋ `AdminIdentity`。`AdminClubAuthorizer.AuthorizeAsync`
依序做四件事，任何一步失敗立刻丟例外（見 `Security/AdminClubAuthorizer.cs` 逐行註解）：

1. **有沒有登入**——`AdminIdentity.FromClaimsPrincipal(httpContext.User)`，`null` 就丟
   `AdminUnauthenticatedException`（401）。JWT 驗證本身由 `Program.cs` 註冊的 `AddJwtBearer`
   中介軟體完成（簽章、`issuer`、`audience`、效期），這裡只讀解析後的 claims。
2. **俱樂部存不存在**——沿用既有 `IClubResolver.ResolveAsync`，行為與既有公開端點**完全不變**
   （含 Redis 快取）。
3. **帳號目前狀態**——重查 `status`／`is_super_admin`／`must_change_password`／
   `two_factor_enabled`（不信任 JWT claims，理由見上）；`is_super_admin=true` 跳過下一步
   （規劃書 §6「系統管理員跳過整個資料範圍查詢」）。
4. **資料範圍**——非超管查 `admin_user_clubs`：`WHERE admin_user_id=@id AND club_id=@clubId
   AND is_active=1 AND (expires_on IS NULL OR expires_on >= 今天)`，查無列即 403。
5. **權限碼**——委派 `IPermissionChecker.HasPermissionAsync`（`Security/PermissionChecker.cs`）：
   展開 `AdminUser.AdminRoles`（隱式多對多，`admin_user_roles` 只有兩個 FK 沒有其他欄位，
   EF Core scaffold 把它建模成跳躍導覽沒有獨立 `DbSet`）→ `AdminRole.RolePermissions` →
   `Permission.Code`，查有沒有命中呼叫端要求的權限碼。

⚠️ **公開唯讀端點的行為完全不變**——任何人仍可用網址指定俱樂部瀏覽公開內容，這條路徑一行都沒改，
見 `AdminClubAuthorizerTests.額外情境_公開唯讀端點的行為完全不受影響` 與既有 `ClubScopingTests`。

### `Features/AdminNews` 改走真實授權

原本的 `Security/DevWriteGate.cs`／`IDevOperatorResolver` 機制已於 2026-09-23 整支刪除
（見下方「開發模式開關：已刪除」），`AdminArticlesEndpoints` 現在的樣子：

- 每個端點先呼叫 `IAdminClubAuthorizer.AuthorizeAsync(httpContext, club, 權限碼, ct)`，權限碼對應
  docs/12b §7.3 命名慣例（`content.article.view/create/update/publish/delete`，`module_code=B`、
  `submodule_code=B2`）。
- `created_by`／`updated_by` 改用 `adminScope.Identity.AdminUserId`（真實登入者），不再讀任何
  呼叫端可自報的標頭。
- `Program.cs` 的 `app.MapAdminNewsEndpoints()` 一律呼叫，不再有任何環境旗標判斷式——路由一律
  註冊，每個請求各自由授權層擋 401／403。這是本輪對既有安全邊界的異動，證據見下方
  「開發模式開關：已刪除」。

**權限碼的一個工程判斷**：規劃書 §6 矩陣寫「內容編輯 ✔ 編輯／發布」，沒有明文列出刪除。
本輪判斷「刪除自己編輯的草稿」是內容管理的常態操作，視為隱含在編輯權限內，
把 `content.article.delete` 也一併授予 `content_editor` 角色（見種子腳本 `18.3` 的註解）。
若這個判斷不符合預期，改一行 `role_permissions` 種子資料即可，不影響任何程式碼。

### 🔴🔴🔴 新增後台端點的必要形狀（2026-09-23，型別層強制授權，使用者裁決後的定案寫法）

**背景**：2026-09-23 的複驗發現，光靠「每個端點自己記得在第一行呼叫 `AuthorizeAsync`」是慣例
不是機制——`AdminArticlesRepository` 原本收的是解包後的裸 `ClubScope`，而 `ClubScope` 從公開的
`IClubResolver.ResolveAsync` 就拿得到、完全不需要登入或授權。一支新端點只要忘記呼叫
`AuthorizeAsync`、改呼叫 `IClubResolver`，就能編譯過、跑得動、繞過整套登入與授權，而且**不會有
任何測試變紅**（原本的 7/7 端點都有正確呼叫，只是慣例剛好每次都對）。這是
[`docs/18-work-errors.md`](../../docs/18-work-errors.md) `E-42` 系譜反覆點名的形狀：**當一類
錯誤的特徵是「程式碼本身沒有錯」，再嚴格的靜態檢查都接不住它。** 下文是修正後、之後每個模組
接真實授權時都要照抄的形狀。

**1. `Security/AdminClubScope.cs`：`sealed class`（不是 `struct`），只暴露扁平化屬性。**
```csharp
public sealed class AdminClubScope
{
    private readonly ClubScope _club;           // 不對外
    public Guid ClubId => _club.ClubId;          // 扁平化，跟 ClubScope 同名同型別
    public string ClubCode => _club.ClubCode;
    public AdminIdentity Identity { get; }
    internal AdminClubScope(ClubScope club, AdminIdentity identity) { ... }  // 只有 AdminClubAuthorizer 能呼叫
}
```
沒有 `.Club` 這個解包出口，代表**沒有任何一行程式碼能把「已授權」的 `AdminClubScope` 換成一個
可以到處傳的裸 `ClubScope`**——想要俱樂部代碼或主鍵，只能用 `ClubId`／`ClubCode`。**是 `class`
不是 `struct` 這件事本身是第二輪修正的重點**，見下方「`default` 破口是怎麼堵的」。

**2. 後台 repository 的每一個公開方法與每一個私有輔助方法，一律收 `AdminClubScope`，不收
`ClubScope`。** 這是真正的強制點——如果收的是 `ClubScope`，第 1 點做得再乾淨也沒用，因為呼叫端
永遠可以繞過 `AdminClubScope` 直接生一個 `ClubScope` 塞進去。`AdminArticlesRepository` 的十個
方法（含 `LoadTrackedForWriteAsync`／`EnsureFeaturedCapAsync`／`InvalidatePublicCacheAsync`
這些私有輔助方法）全部是這個形狀，照抄即可，方法本體幾乎不用改（`scope.ClubId`／
`scope.ClubCode` 這兩個屬性名稱在 `AdminClubScope` 上完全一樣）。

**3. 端點層拿到 `AdminClubScope` 之後直接往下傳，不要自己再解包一次存成別的變數。** 舊寫法
`var scope = adminScope.Club;` 已經連同 `.Club` 一起拿掉——這個習慣本身就是「把已授權的東西
降級回未授權型別」的起點，即使當下無害，也不該留著讓下一個人照抄。

### 型別強制的三層防線與誠實的覆蓋邊界（2026-09-23 第二輪修正）

第一輪只做了「不收 `ClubScope`」，被使用者實測兩次戳破：① `new Tcrfc.Api.Security.AdminClubScope(...)`
（完整命名空間，第一版的字串掃描 `IndexOf("new AdminClubScope(")` 找不到）② `=> default;`
（`readonly struct` 的 `default` 語言規範保證完全不經過建構子，字串掃描從設計上就不可能抓到
「沒有 `new` 這個字」的取值方式）。第二輪修正三件事，**每一件解決的問題不同，缺一都不完整**：

| 防線 | 解決什麼 | 解決不了什麼 |
|---|---|---|
| ① `ClubScope`／`AdminClubScope` 改成 `sealed class`，不是 `readonly struct` | `default`／`default(T)` 破口——**這是型別本身的修正，不是測試**。`class` 的 `default` 是 `null`，用它存取任何屬性會立刻 `NullReferenceException`，從「悄悄拿到一個看似合法的零值範圍」降級成「立刻爆炸」。`new AdminClubScope()`（無參數）現在**直接編譯失敗**（`error CS7036`）——class 只要宣告了任何建構子，編譯器就不再合成公開的無參數建構子，這點 struct 做不到（struct 永遠有隱式無參數建構子） | 不解決「同組件內用 `new AdminClubScope(x, y)` 帶真實引數呼叫 internal 建構子」——這仍然編譯得過，見② |
| ② `Tcrfc.Api.Tests/ArchitectureTests.cs` 改用 **Roslyn 語意模型**掃描，不是逐行字串比對 | 對**任何語法表面形式**都有效，因為比對的是編譯器解析後的型別符號，不是原始碼文字——完整命名空間、裸型別名稱、using 別名、逐字識別碼（`@AdminClubScope`）、C# 9 目標型別 `new()`、`default`／`null` 字面值，全部收斂成同一個語意檢查 | 只在 `dotnet test` 執行時抓到，不是編譯期；且僅涵蓋「原始碼裡看得出型別的建構／取值語法」 |
| ③ 額外掃描反射繞過建構子的三個已知 API（`Activator.CreateInstance`／`RuntimeHelpers.GetUninitializedObject`／`FormatterServices.GetUninitializedObject`） | 這三個是 .NET 本身公開給序列化框架用的「故意不呼叫任何建構子」管道，**連 `internal` 存取層級都繞得過**（`Activator.CreateInstance(type, nonPublic: true)`），純語法掃描看不出「這是在建構 ClubScope」，因為呼叫端只是一般方法呼叫，要另外比對呼叫的方法與 `typeof()` 引數 | **這是有限枚舉，不是通用反射防護**——只認這三個文件記載的已知 API |

**驗收：八種語法變形 ＋ 兩種反射變形，逐一實測「注入 → 測試變紅且點名檔案行號 → 復原 → 變綠」**（見下方逐字輸出）：完整命名空間、裸型別名稱、using 別名（`using ACS = ...; new ACS(...)`）、逐字識別碼（`new Tcrfc.Api.Security.@AdminClubScope(...)`）、C# 9 目標型別 `new(...)`、`default`、`null!`、以及對照組 `ClubScope`（不是只驗 `AdminClubScope`）——八種一次性混在同一支違規檔案裡，`dotnet test` 一次跑全部抓到，逐行標出檔案:行號:違規片段。另外 `Activator.CreateInstance(typeof(AdminClubScope), nonPublic: true)` 與 `RuntimeHelpers.GetUninitializedObject(typeof(ClubScope))` 兩種反射形狀單獨驗證，同樣抓到並標出行號。

**這份反例清單是怎麼確認「涵蓋夠廣」的，誠實回答，不包裝成窮舉**：清單本身**不是**窮舉出來的——不可能窮舉 C# 所有能引用一個型別的語法形式。信心來自**解法的性質**：語意模型比對的是編譯器解析後的型別符號，不是原始碼文字表面形式，所以八種形狀能被同一段比對邏輯一次抓到，證明的是「這個機制對語法表面變形無感」，不是「剛好想到了全部可能的寫法」。這份清單刻意涵蓋幾個不同**類別**的變形（限定詞、別名、逐字識別碼、目標型別推斷、預設值、反射）來支持這個論證，但**沒有涵蓋、也明確承認涵蓋不到**：`unsafe` 指標轉型、`Marshal.PtrToStructure`、手刻 IL 產生器直接 emit 物件——這幾種在原始碼層級幾乎沒有可辨識特徵，純靜態分析做不到，本輪判斷投資報酬率不夠，沒有做，見 `ArchitectureTests.cs` 類別上的完整說明。

**唯一真正解不掉、需要使用者決定要不要投資的洞**：同組件內直接呼叫 `internal` 建構子這件事本身
（不管引數是真是假）**沒有辦法用型別系統解決**，只能靠把 `AdminClubScope`／`IAdminClubAuthorizer`
（可能還要連同 `ClubScope`／`IClubResolver`，維持兩者強制力一致）搬進獨立的類別庫組件——這是
比較大的結構異動（新專案檔、調整參照關係），本輪判斷不在核心範圍內，`ArchitectureTests.cs` 的
CI 層防線是目前的替代方案。

**之後 13 個模組接真實授權的檢查清單**：
1. 該模組的 repository 每一個方法簽章用 `AdminClubScope`，不要用 `ClubScope`（連私有輔助方法也算）。
2. 端點層一律 `var adminScope = await authorizer.AuthorizeAsync(httpContext, club, 權限碼, ct);`，
   不要另外呼叫 `IClubResolver`。
3. 權限碼命名照 docs/12b §7.3：`<domain>.<object>.<action>`，`module_code`／`submodule_code`
   對應該模組的代號。
4. 新增權限碼與角色授予寫進 `db/seed/generate-club-seed-sql.py`（沿用既有 `PERMISSIONS`／
   `ROLE_PERMISSIONS` 清單的形狀）。
5. 交付前跑一次 `dotnet test --filter FullyQualifiedName~ArchitectureTests`，確認沒有新增的
   違規建構——這支測試涵蓋全部模組，不是只驗 `AdminNews`。

### `ArchitectureTests` 逐字輸出（2026-09-23，八種語法變形 ＋ 兩種反射變形，全部單獨驗證過）

**八種語法變形**（同一支違規檔案，一次跑全部抓到）：
```
$ dotnet test --filter FullyQualifiedName~ArchitectureTests
[FAIL] 除了唯一產生者以外_沒有任何地方能產生ClubScope或AdminClubScope的實例
發現不在允許清單內的地方能產生 ClubScope／AdminClubScope 的實例，這會繞過授權：
.../Features/AdminNews/_ScratchBypassProof.cs:10: [new] new Tcrfc.Api.Security.AdminClubScope(default!, default!)
.../Features/AdminNews/_ScratchBypassProof.cs:13: [new] new AdminClubScope(default!, default!)
.../Features/AdminNews/_ScratchBypassProof.cs:22: [new] new ACS(default!, default!)
.../Features/AdminNews/_ScratchBypassProof.cs:28: [new] new Tcrfc.Api.Security.@AdminClubScope(default!, default!)
.../Features/AdminNews/_ScratchBypassProof.cs:31: [new] new Tcrfc.Api.Security.ClubScope(default, default!)
.../Features/AdminNews/_ScratchBypassProof.cs:25: [new] new(default!, default!)
.../Features/AdminNews/_ScratchBypassProof.cs:16: [default] default
.../Features/AdminNews/_ScratchBypassProof.cs:19: [null] null
（其餘 default! 引數本身也各自被記錄，略）
失敗! - 失敗: 1，通過: 0

$ rm .../Features/AdminNews/_ScratchBypassProof.cs
$ dotnet test --filter FullyQualifiedName~ArchitectureTests
已通過! - 失敗: 0，通過: 1，略過: 0，總計: 1
```

**兩種反射變形**：
```
$ dotnet test --filter FullyQualifiedName~ArchitectureTests
[FAIL] 除了唯一產生者以外_沒有任何地方能產生ClubScope或AdminClubScope的實例
.../Features/AdminNews/_ScratchReflectionProof.cs:7: [reflection:CreateInstance] System.Activator.CreateInstance(typeof(Tcrfc.Api.Security.AdminClubScope), nonPublic: true)
.../Features/AdminNews/_ScratchReflectionProof.cs:10: [reflection:GetUninitializedObject] System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Tcrfc.Api.Security.ClubScope))
失敗! - 失敗: 1，通過: 0

$ rm .../Features/AdminNews/_ScratchReflectionProof.cs
$ dotnet test --filter FullyQualifiedName~ArchitectureTests
已通過! - 失敗: 0，通過: 1，略過: 0，總計: 1
```

**`default` 破口的型別層修正**（不是測試層，是編譯器層與執行期行為）：
```
// 修正前（struct）：default 產生零值實例，看起來合法，NOT caught by anything unless the value is used carefully
// 修正後（class）：
$ echo 'private static Tcrfc.Api.Security.AdminClubScope Bypass() => new Tcrfc.Api.Security.AdminClubScope();' >> 測試檔
$ dotnet build
error CS7036: 未提供任何可對應到 'AdminClubScope.AdminClubScope(ClubScope, AdminIdentity)' 之必要參數 'club' 的引數
（無參數建構已直接編譯失敗，class 不會像 struct 一樣自動合成公開無參數建構子）

$ echo 'private static AdminClubScope Bypass() => default;' >> 測試檔  # 改用 default
$ dotnet build   # 編譯成功（default 對 class 合法，是 null）
建置成功，1 個警告（CS8603 可能有 Null 參考傳回）

$ dotnet test --filter FullyQualifiedName~_TempNreProof   # 呼叫端試圖真的使用這個偽造值
已通過! - Assert.Throws<NullReferenceException> 成立——forged.ClubId 這一行立刻丟例外
```

兩批違規檔案與臨時測試都已刪除，本節內容是實際執行輸出的逐字節錄（部分路徑截短為 `...` 方便閱讀）。

### 四種擋下情境的實際輸出（2026-09-23，對本機 `tcrfc_club_dev` 實測，`ASPNETCORE_ENVIRONMENT=Production`）

```
$ curl -s -w "\nHTTP %{http_code}\n" http://127.0.0.1:5299/api/v1/admin/tcrfc/news
{"title":"請先登入","status":401,"detail":"請先登入後台。","instance":"/api/v1/admin/tcrfc/news"}
HTTP 401
```

```
$ curl -s -w "\nHTTP %{http_code}\n" -H "Authorization: Bearer <partner.club@tcrfc.test，只授權 bw>" \
  http://127.0.0.1:5299/api/v1/admin/tcrfc/news
{"title":"沒有權限","status":403,"detail":"你沒有被授權存取俱樂部「tcrfc」的後台資料。", ...}
HTTP 403

# 反向對照：同一個帳號打自己有授權的 bw，正常通過——證明上面擋下的原因確實是「範圍」。
$ curl -s -w "\nHTTP %{http_code}\n" -H "Authorization: Bearer <同一個 partner.club token>" \
  http://127.0.0.1:5299/api/v1/admin/bw/news
{"items":[],"page":1,"pageSize":20,"totalCount":0,"totalPages":0}
HTTP 200
```

```
$ curl -s -w "\nHTTP %{http_code}\n" -H "Authorization: Bearer <expired.grant@tcrfc.test，tcrfc 授權 expires_on=昨天>" \
  http://127.0.0.1:5299/api/v1/admin/tcrfc/news
{"title":"沒有權限","status":403,"detail":"你沒有被授權存取俱樂部「tcrfc」的後台資料。", ...}
HTTP 403
```

```
$ curl -s -w "\nHTTP %{http_code}\n" -H "Authorization: Bearer <content.editor@tcrfc.test，真正有授權 tcrfc>" \
  http://127.0.0.1:5299/api/v1/admin/tcrfc/news
{"items":[{"id":"...","slug":"...", ...}], "page":1, ...}
HTTP 200
```

⚠️ **`own_clubs` 角色打別的俱樂部**這一情境與「情境二」用的是同一個帳號
（`partner.club@tcrfc.test`，角色 `partner_club_manager` 正是 `scope_mode=own_clubs` 那個角色）——
機制上是同一段程式碼路徑，但驗的擔憂不同：情境二驗「一般帳號打沒授權的俱樂部」這個通用機制，
這條額外驗「`own_clubs` 角色本身沒有任何特殊旁路能繞過範圍檢查」，見
`AdminClubAuthorizerTests.情境四_own_clubs角色打別的俱樂部_擋下`。

以上四段輸出對應的自動化測試見 `Tcrfc.Api.Tests/AdminClubAuthorizerTests.cs`（連同「未完成 2FA」
「公開唯讀端點不受影響」兩個額外情境，共 7 個測試方法），token 由 `TestAdminTokens` 直接呼叫
`AdminTokenService` 簽發（不必先真的完成登入＋2FA，理由見該檔案上的說明），curl 示範則是拿同一把
`JWT_SIGNING_KEY_CLUB` 用等效邏輯手動簽出的 token 對真正在跑的行程實測，兩者互相印證。

### 開發模式開關：已刪除（2026-09-23，使用者裁決）

`Security/DevWriteGate.cs`／`IDevOperatorResolver.cs`／`DevOperatorResolver.cs` 與
`ENABLE_UNSAFE_DEV_WRITES` 這整套「環境旗標決定路由存不存在」的機制**已整支刪除**（含
`Program.cs` 的相關註冊、所有測試 fixture 對這個環境變數的設定）。原本的顧慮（任務指示「不要
自己決定移除，提出證據讓使用者裁決」）已經走完：先把真實授權接上並讓 `AdminNews` 不再依賴
`DevWriteGate`，用以下證據支持移除，交由使用者確認後才真的刪檔案：

| 檢查 | 證據 |
|---|---|
| 未登入一律擋下 | 情境一 curl／`AdminClubAuthorizerTests.情境一`／`AdminNewsGateClosedTests`（5 個測試方法，涵蓋清單、建立、單篇、格式不正確的權杖、公開端點不受影響） |
| 登入但無俱樂部授權一律擋下 | 情境二 curl／`AdminClubAuthorizerTests.情境二`＋反向對照 |
| 授權已過期一律擋下 | 情境三 curl／`AdminClubAuthorizerTests.情境三` |
| `own_clubs` 角色打別的俱樂部一律擋下 | 情境四 curl／`AdminClubAuthorizerTests.情境四` |
| 未完成強制前提（改密碼／2FA）一律擋下 | `AdminClubAuthorizerTests.額外情境_已完成改密但尚未完成2FA_擋下俱樂部範圍端點` |
| 正式環境（`ASPNETCORE_ENVIRONMENT=Production`）行為與開發環境一致 | 上面四段 curl 全部對 `ASPNETCORE_ENVIRONMENT=Production` 的行程實測，不是只測過 Development |
| 既有公開唯讀端點不受影響 | `AdminClubAuthorizerTests.額外情境_公開唯讀端點的行為完全不受影響`＋既有 `ClubScopingTests`／`SqlInjectionTests` 全數通過 |
| **刪除後重驗**：`dotnet test` 131/131 全過 | 見下方「測試結果」 |
| **刪除後重驗**：`ASPNETCORE_ENVIRONMENT=Production`（不帶任何舊防線、不帶任何開發旗標）未登入打 `POST /api/v1/admin/tcrfc/news` 仍是 401 | `curl -i -X POST .../admin/tcrfc/news`（無 Authorization 標頭）→ `HTTP/1.1 401 Unauthorized`，`{"title":"請先登入",...}`——刪檔後親自重跑過，不是憑推論 |
| **複驗（使用者發現）**：`ASPNETCORE_ENVIRONMENT=Production` **且刻意帶上已被刪除的 `ENABLE_UNSAFE_DEV_WRITES=true`**，寫入端點仍是 401 | 舊旗標不會讓任何東西重新開門——本輪再次親自複驗：`export ENABLE_UNSAFE_DEV_WRITES=true` 後起行程，`POST /api/v1/admin/tcrfc/news`（無 Authorization）仍回 `401`，`GET`／`DELETE` 同樣 401；帶著 `expectedUpdatedAt` 查詢參數的 `DELETE` 也是 401（不帶時是 400，那是 ASP.NET Core 參數繫結在 handler 執行前就失敗，跟授權無關，見下一節說明） |
| **型別層強制授權**：忘記呼叫 `AuthorizeAsync`、改用公開 `IClubResolver` 取得 `ClubScope` 塞給後台 repository | `error CS1503`，編譯失敗——見上方「新增後台端點的必要形狀」的完整驗證 |

### 稽核記錄（J3）：已撤回（2026-09-23，使用者裁決）

`admin_audit_logs`（操作稽核）與 `admin_login_logs`（登入紀錄）**已撤回**，理由見本節最上方
「S1」標題下的「事後更正」——委託方在 `docs/12` §13.1 明文指示本次資料庫設計不含 log，本輪一開始
沒有查這條就先做了，發現衝突後如實回報，使用者裁決撤回。撤回範圍：`db/club-schema.sql` 的兩張
表定義（含索引與 FK）、本機 `tcrfc_club_dev` 的實際表、`Security/AdminAuditLogger.cs`（整支刪除）
與 `Features/AdminNews` 裡的呼叫點、`Features/AdminAuth/AdminAuthService.cs` 裡寫
`admin_login_logs` 的呼叫點與 `RecordLoginLogAsync` 方法本身、EF scaffold 產物
（`AdminAuditLog.cs`／`AdminLoginLog.cs` 實體類別、`ClubDbContext` 對應的 `DbSet`／
`modelBuilder.Entity<>()` 設定）。**`admin_refresh_tokens` 不受影響、維持存在**——那張表跟
「log」性質不同（是更新權杖輪替機制的必要狀態，不是操作紀錄），且屬於 J1 登入機制的核心部分，
使用者裁決明確排除在撤回範圍外。

🔴 **已查證：帳號鎖定功能不受這次撤回影響。** `AdminAuthService.RegisterFailedAttemptAsync` 從
一開始就只讀寫 `AdminUser.FailedAttemptCount`／`LockedUntil` 兩個欄位（docs/12 §13.1 本來就把
這兩欄列為「保留下來的補償」），從未依賴 `admin_login_logs`。`AdminAuthTests.
登入_連續五次失敗後鎖定_第六次即使密碼正確也擋下` 這個測試在撤回後**照樣通過**，是這件事的
直接證據，不是憑程式碼審查推論。

**現在沒有的能力**（撤回的直接後果，如實記錄）：登入紀錄（誰、何時、成功或失敗原因、來源 IP）
不再落地，`Features/AdminNews` 的寫入動作（建立／更新／發布／排程／刪除）也不再有任何操作留痕
——除了 `articles.updated_at`／隱含的 `created_by`／`updated_by` 這種「最後一次是誰改的」之外，
沒有變更歷程、沒有稽核軌跡。這與 docs/12 §13.1 原本記載的落差**完全一致**（畢竟就是回到那個狀態），
不是新的缺口。日後若要重新補上，docs/12 §13.1 那句「只需新增表，不需改動既有綱要」仍然成立。

### `admin_refresh_tokens.created_ip`／`user_agent`：已拿掉（2026-09-23，使用者裁決）

`system-analyst` 補 `docs/12`／`docs/12a`／`docs/12b` 文件時發現、下游 grep 複核屬實：這兩欄
**只在核發更新權杖時寫入，`AdminAuthService.cs`／`AdminAuthEndpoints.cs` 裡沒有任何地方讀取**
（不做裝置綁定、不做來源比對、不影響輪替或重放偵測的判斷結果），而且**沒有清除機制**——撤銷與
過期的舊列會無限累積，功能上等同一份持續增長的登入位置紀錄，跟 docs/12 §13.1「不能回答」清單裡
的「來源 IP」正好重疊。使用者裁決拿掉，真的要做裝置綁定或異常偵測再加回來（屆時要有讀取端才說
得上是功能，不是紀錄）。

撤回範圍：`db/club-schema.sql` 的 `admin_refresh_tokens` 定義（表本身**維持存在**，只拿掉這兩欄
——它跟「log」性質不同，是更新權杖輪替與重放偵測的必要狀態，不在撤回範圍）、本機
`tcrfc_club_dev` 的實際欄位（`ALTER TABLE ... DROP COLUMN`）、EF scaffold 產物、
`AdminAuthService.LoginAsync`／`RefreshAsync`／`IssueRefreshTokenAsync` 的 `ipAddress`／
`userAgent` 參數、`AdminAuthEndpoints.ExtractClientInfo`（拿掉後沒有其他呼叫端，整支移除）。

⚠️ **`docs/12`／`docs/12a`／`docs/12b` 三份文件目前記載的是拿掉前的狀態，需要使用者回頭更新**
（本次交付不得自行修改 `docs/`）：
- `docs/12-database-schema.md`：第 503、507、592、785 行提到 `created_ip`／`user_agent`
  「待使用者裁決」，現在已經裁決＝拿掉，這幾處待決語氣需要改成past tense的定案敘述。
- `docs/12a-database-erd.md`：第 1241–1242 行的 ERD 欄位列表（`string_64 created_ip`／
  `string_255 user_agent`）需要從 `admin_refresh_token` 實體圖裡刪除；第 1326 行的說明文字
  同樣需要更新。
- `docs/12b-database-tables.md`：§7.7（277 行起）整段「`created_ip`／`user_agent` 的定位需要
  使用者裁決」的查證記錄可以收斂成一句「已裁決拿掉」，287 行的欄位列表需要移除這兩欄。

### 測試結果

```
已通過! - 失敗: 0，通過: 131，略過: 0，總計: 131，持續時間: ~35 秒 - Tcrfc.Api.Tests.dll (net10.0)
```

**131 = 既有 99 個（S0-8 為止的基準，全數維持通過，含因為改走真實授權而需要更新的既有測試）
＋ 第一輪新增 31 個 ＋ 第二輪（型別層強制授權）新增 1 個**：
- `PasswordHasherTests`（5）／`TotpServiceTests`（6）：純單元測試，不碰資料庫。
- `AdminAuthTests`（7）：登入成功／密碼錯誤不洩露帳號存在與否／連續失敗鎖定／完整 2FA 設定
  （設定前免驗證碼、設定後需要正確驗證碼、驗證錯誤碼會被拒）／更新權杖輪替與重放偵測／登出。
- `AdminClubAuthorizerTests`（7）：四種必要情境＋反向對照＋未完成 2FA＋公開端點不受影響。
- `AdminNewsGateClosedTests` 改寫（5，取代舊版驗「開關關閉回 404」的 3 個測試，見下方
  「既有測試怎麼改的」）。
- `ArchitectureTests`（1，第二輪新增）：純掃原始碼，不碰資料庫，確認整個 `apps/api` 除了兩個
  唯一產生者以外沒有任何地方直接建構 `AdminClubScope`／`ClubScope`，見上方「新增後台端點的
  必要形狀」。

**既有測試怎麼改的**（改走真實授權後，原本用假操作者標頭的請求全部變成 401，這是預期中的破壞
性變更，逐一修正而不是繞過）：

1. `AdminNewsGateClosedTests` 整支重寫——舊版驗的是「`ENABLE_UNSAFE_DEV_WRITES` 關閉時回
   404」，這個機制已經不適用於 `AdminNews`（路由一律註冊），新版驗「未登入回 401」「格式不正確
   的權杖回 401」「公開端點不受影響」。
2. `AdminNewsCoverBlobCleanupTests`／`AdminNewsCoverUploadTests`／`AdminNewsCacheInvalidationTests`／
   `AdminNewsWriteTests`／`AdminNewsSlugPolicyTests` 這五個檔案的每一個 `fixture.CreateClient()`
   之後，一律加一行 `client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
   "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"))`
   ——這些測試原本就是在驗 `AdminNews` 的業務邏輯（slug 政策、封面圖片上傳、快取失效……），
   加真實授權不改變它們原本要驗的事，只是補上「這件事現在需要先登入」這個新前提。
3. `AdminNewsWriteTests` 裡兩個「用另一俱樂部路由讀寫」的測試（驗 repository 層 `WHERE club_id`
   過濾），改用 `super.admin@tcrfc.test`（`is_super_admin=true`，略過範圍檢查）而不是
   `content.editor@tcrfc.test`——後者現在會先被 `AdminClubAuthorizer` 的範圍檢查擋在 403，
   根本到不了 repository，測不出這兩個測試原本要驗的「repository 層本身有沒有正確過濾」。
   授權層的範圍擋下行為另有專門測試（`AdminClubAuthorizerTests` 情境二／四）。

### 種子測試帳號（`db/seed/generate-club-seed-sql.py` §18.4，本機開發專用）

| `username` | 密碼 | 角色 | 俱樂部授權 | 狀態 | 用途 |
|---|---|---|---|---|---|
| `sa@system.local` | `Admin@123` | `system_admin` | — | `must_change_password=1`／`2FA 未啟用` | **真正的種子超管**（docs/12b §7.6 明文的帳號），走完整強制流程 |
| `clean.login@tcrfc.test` | `SuperAdmin@123` | `system_admin` | — | 可直接登入 | 唯一能走完整 `/login` HTTP 往返的「已就緒」帳號（見下方原因） |
| `super.admin@tcrfc.test` | `SuperAdmin@123` | `system_admin` | — | `2FA` 已標記啟用但無真實密鑰 | 供 `TestAdminTokens` 直接簽權杖用，略過登入 |
| `content.editor@tcrfc.test` | `ContentEditor@123` | `content_editor` | `tcrfc` | 同上 | 大多數 AdminNews 測試預設用這個 |
| `viewer@tcrfc.test` | `Viewer@123` | `viewer` | `tcrfc` | 同上 | 唯讀角色測試 |
| `partner.club@tcrfc.test` | `PartnerClub@123` | `partner_club_manager`（`own_clubs`） | 僅 `bw` | 同上 | 情境二／四 |
| `expired.grant@tcrfc.test` | `ContentEditor@123` | `content_editor` | `tcrfc`（**已過期**） | 同上 | 情境三 |
| `fresh.setup@tcrfc.test` | `Admin@123` | `viewer` | `tcrfc` | `must_change_password=1`，`2FA 未啟用` | 完整 2FA 設定流程測試（`AdminAuthTests` 用完會重設回本狀態） |
| `lockout.test@tcrfc.test` | `Viewer@123` | `viewer` | `tcrfc` | 同上 | 連續失敗鎖定測試專用（避免與其他測試共用帳號互相污染） |

⚠️ **為什麼大多數「已就緒」帳號的 `two_factor_enabled` 是種子直接設 `1` 但沒有真正可解密的密鑰**：
ASP.NET Core Data Protection 的金鑰環綁在執行中的行程，種子腳本在行程外執行，沒有能力產生「這個
行程解得開」的密文。`AdminClubAuthorizer` 只檢查 `two_factor_enabled` 布林值本身，不會去解密這個
欄位，所以直接種布林值就能讓這些帳號通過強制 2FA 檢查——**但這也代表這些帳號無法透過真正的
`/login` 端點完成登入**（送出任何驗證碼都會被拒，因為沒有真實密鑰算得出正確答案）。需要測試
「真正的登入 HTTP 往返」時，只能用 `two_factor_enabled=0` 的帳號（`sa@system.local`／
`clean.login@tcrfc.test`／`fresh.setup@tcrfc.test`），或走完整的「設定 2FA」流程之後再登入
（`AdminAuthTests.完整2FA設定流程...` 示範了後者）。

### 種子測試帳號的重設（`db/seed/reset-admin-accounts.sh`，2026-09-24 新增）

🔴 **端對端驗收會改掉種子帳號的狀態，驗完要跑這支還原。** `apps/admin` 做端對端驗收
（`npm run dev` 起真實瀏覽器操作，見 `apps/admin/README.md`「驗收紀錄」）時，會真的登入
`sa@system.local`、完成強制改密與強制 2FA 設定——這些是正常的帳號操作，會把該帳號的
`password_hash`／`must_change_password`／`two_factor_enabled` 等欄位永久改成驗收過程中設定的值，
不再是種子腳本原始的 `Admin@123`／2FA 未啟用。

**為什麼 `./db/seed/apply-seed.sh` 救不回來**：種子腳本的冪等策略是「業務自然鍵 `IF NOT EXISTS`
才 `INSERT`」——帳號列本來就已經存在，這條規則只保證「缺的資料會補上」，不會回頭 `UPDATE` 已經
存在、但狀態已經偏離初始值的列。

**還原方式**：

```bash
set -a; source .env; set +a   # 取得 MSSQL_DEV_SA_PASSWORD
./db/seed/reset-admin-accounts.sh            # 產生並套用
./db/seed/reset-admin-accounts.sh --dry-run  # 只看會產生什麼 SQL，不套用
```

只對 `tcrfc_club_dev` 執行 `UPDATE`（白名單模型逐字比照 `apply-seed.sh`），只還原
`db/seed/generate-club-seed-sql.py` 的 `ADMIN_USERS` 清單裡每個帳號的
`password_hash`／`must_change_password`／`is_super_admin`／`two_factor_enabled`／
`two_factor_secret_encrypted`（→`NULL`）／`two_factor_confirmed_at`（→`NULL`）／
`failed_attempt_count`（→`0`）／`locked_until`（→`NULL`）／`status`（→`active`）九個欄位，
**不影響**角色指派（`admin_user_roles`）與俱樂部授權（`admin_user_clubs`）——那兩張表本來就是
「新增才會種」，端對端驗收的登入操作不會弄髒它們。實作是
`generate-club-seed-sql.py --reset-admin-accounts` 這個新增的 CLI 旗標，重用同一份
`ADMIN_USERS` 清單產生 `UPDATE` 陳述式（不是 `INSERT`），刻意不重複維護第二份密碼雜湊。

✅ **已於本次任務執行過一次**：`sa@system.local` 驗收後 `must_change_password=0`／
`two_factor_enabled=1`，執行還原腳本後確認回到 `must_change_password=1`／`two_factor_enabled=0`
（種子初始值），其餘帳號一併核對過欄位值正確。

### 本輪沒做的部分（誠實列出，不假裝做完）

1. ✅ **已補上（S1-3 續作，2026-09-24）**：`Features/AdminAccounts`（J1 帳號 CRUD、停用／啟用、
   重設密碼、重設 2FA）與掛在帳號底下的 J4 俱樂部授權（`admin_user_clubs` 新增／撤銷）已實作，
   見下方「S1-3 續作：J1／J2／J4 端點」整節。
2. ✅ **已補上（S1-3 續作，2026-09-24）**：`Features/AdminRoles`（J2 角色 CRUD、權限碼字典、
   角色權限指派）已實作，見下方新增整節。
3. ✅ **已補上（S1-3 續作，2026-09-24；`AdminUserTeam` 為第二輪補派）**：`AdminUserClub`
   授予／撤銷（`/api/v1/admin/accounts/{id}/club-grants`）與 `AdminUserTeam`
   授予／撤銷（`/api/v1/admin/accounts/{id}/team-grants`）皆已實作，理由見 docs/12b §5.3
   「授權掛在人不是角色」。**只做授權資料的維護，`role_permissions.scope_type=own_teams`
   這類列級限制的強制留給 `S1-8`（C4 賽程與賽果的寫入端點）一併實作**，見下方「第二輪補派：
   J4 球隊授權」整節。
4. **`role_permissions.scope_type` 的細粒度限制沒有實作**——`own_teams`／`academy_only`／
   `masked`／`translate_only` 這幾種欄位與列層級規則（docs/12b §7.4）本輪只讀取但不強制執行，
   `PermissionChecker` 目前只做「有沒有這個權限碼」的布林判斷。等對應模組真的接真實授權時
   （例如翻譯人員只能碰 `*_i18n`）需要另外實作。
5. **權限碼目前鋪了四組**：J 系統管理本身（`system.account.*`／`system.role.*`／
   `system.audit.view`／`system.club_grant.*`／**`system.club.*`，S1-3 續作新增**）、B2 新聞
   （`content.article.*`）與**本輪新增的 `team.competition.*`**（`Competition` 型別，見下方新增
   整節）。K／S／E 等其餘模組的權限碼要等對應模組真的做寫入端點時再依同一套命名慣例
   （`<domain>.<object>.<action>`）補上，不是遺漏，是刻意的範圍縮減。
6. ✅ **已裁決（2026-09-23）**：`docs/12b-database-tables.md` §7.2 的角色代碼表原本與本次種子
   資料不一致（本次沿用 `db/seed/generate-charity-seed-sql.py` 的九個代碼，docs/12b §7.2 是另一套
   命名）。使用者裁決**以種子用的九個代碼為準**，`docs/12b` 由使用者自行更新，本檔不再重複記錄
   細節（避免跟 `docs/12b` 本身兩處各寫一份、日後不同步）。
7. ✅ **已裁決（2026-09-23）**：稽核記錄（`admin_audit_logs`／`admin_login_logs`）與 docs/12
   §13.1「本版無稽核與登入日誌表」的政策衝突，使用者裁決**撤回**，已撤回完畢，見上方
   「稽核記錄（J3）：已撤回」整節。
8. **密碼政策是最小實作**：長度 ≥10、不得等於帳號本身。規劃書沒有寫死具體規則（字元類別要求、
   歷史密碼比對、定期輪替），這是執行層判斷（見 `AdminAuthService.ValidatePasswordPolicy`），
   沒有做業界常見的「不得與最近 N 組密碼相同」（需要密碼歷史表，本次判斷不在核心範圍內）。
9. **鎖定門檻（5 次失敗鎖 15 分鐘）是執行層判斷**，規劃書沒有給具體數字，落在 OWASP
   Authentication Cheat Sheet 建議範圍（3–5 次）內，但沒有經過使用者確認這個具體數字。
10. **沒有「登入紀錄與異常提醒」**（規劃書 J3 的完整條文）——`admin_login_logs` 已撤回（見上方
    「稽核記錄（J3）：已撤回」），目前完全沒有登入歷程可查，只剩 `AdminUser.last_login_at` 單點
    紀錄（docs/12 §13.1 本來就記載的補償欄位，不是本輪新增）。異常提醒更是完全沒有，這兩項要
    等稽核記錄的政策方向重新確認後才有地基可以做。

---

### S1-3 續作：J1／J2／J4 端點（2026-09-24，`backend-engineer`）

補完 `STATUS.md` `S1-3`（以及 `S1-2` 的 J4 部分、`S1-1` 的「`Club`／`Competition` 當成後台可維護
型別」）——J1 帳號管理、J2 角色與權限、J4 俱樂部與授權管理，全部只到「登入與授權地基完成，
管理畫面端點未做」。這一輪把管理畫面端點補上。**綱要一個欄位都沒有改**：`AdminUser`／
`AdminRole`／`Permission`／`RolePermission`／`AdminUserClub`／`Club`／`Competition`／`Season`
既有欄位與唯一鍵已經足夠支撐全部端點，唯一動到 DDL 以外的資料是 `permissions` 表新增五筆權限碼
（見下方「新增的權限碼」）——這是資料不是綱要，跟既有 `content.article.*` 走的是同一套慣例。

#### 兩個新的授權型別

J1／J2／J4（`Club` 主檔與 `admin_user_clubs` 授權）是**全域端點**（不含 `{club}` 路由段——
管的是帳號、角色、俱樂部主檔本身，沒有「當下站在哪個俱樂部」這個概念）。既有的
`IAdminClubAuthorizer`／`AdminClubScope` 硬性要求一個俱樂部代碼，套不上去，因此新增一組平行的
型別，**同一套「型別層強制授權」設計哲學**（不透過 `[Authorize]`，用 `internal` 建構子 ＋ 只有
唯一產生者能建立實例）：

| | 俱樂部範圍（既有） | 全域（本輪新增） |
|---|---|---|
| Scope 型別 | `Security/ClubScope.cs`（唯讀端點）／`Security/AdminClubScope.cs`（寫入端點） | `Security/AdminSystemScope.cs` |
| Authorizer | `Security/IAdminClubAuthorizer.cs`／`AdminClubAuthorizer.cs` | `Security/IAdminSystemAuthorizer.cs`／`AdminSystemAuthorizer.cs` |
| 檢查的東西 | 登入 → 俱樂部存在 → 俱樂部授權 → 權限碼 | 登入 → 權限碼（少了中間兩步，因為沒有俱樂部可言） |
| `ArchitectureTests` | 掃 `ClubScope`／`AdminClubScope` | 同一支測試追加掃 `AdminSystemScope`（見該檔案的 `ForbiddenFullyQualifiedNames`／`allowList`） |

**共用的部分抽成 `Security/AdminAccountGate.cs`**：「這個存取權杖對應的帳號，現在還活著嗎」
（存在、`status=active`、`must_change_password=false`、`two_factor_enabled=true`）這組判斷原本
整段寫在 `AdminClubAuthorizer` 內，本輪抽成 internal static 方法，`AdminClubAuthorizer` 與
`AdminSystemAuthorizer` 共用同一份——避免日後改帳號閘門邏輯（例如新增鎖定條件）時忘記改其中一邊。
**這是純抽取，沒有改變 `AdminClubAuthorizer` 的行為**：既有 168 項測試（含 `AdminClubAuthorizerTests`
五種擋下情境）全過。

`PermissionChecker.HasPermissionAsync` 同時補了一個防禦層：非超管路徑額外比對
`!Permission.SysadminOnly`。理由與細節見 `docs/14-invariants.md` 本輪新增那一條——簡單說是
「`sysadmin_only` 目前只有 seed 資料沒把這種碼指派給非超管角色在保證它生效，J2 本輪新增了角色
權限指派端點後，這個保證需要在程式裡真的擋一次，不能只靠『資料庫裡沒人這樣接』」。

#### 端點清單

**J1 帳號管理**（`Features/AdminAccounts/`，`/api/v1/admin/accounts`，全域，用 `IAdminSystemAuthorizer`）

| 方法 | 路徑 | 權限碼 |
|---|---|---|
| GET | `/api/v1/admin/accounts` | `system.account.view` |
| GET | `/api/v1/admin/accounts/{id}` | `system.account.view` |
| POST | `/api/v1/admin/accounts` | `system.account.create` |
| PUT | `/api/v1/admin/accounts/{id}` | `system.account.update` |
| POST | `/api/v1/admin/accounts/{id}/status` | `system.account.update` |
| POST | `/api/v1/admin/accounts/{id}/reset-password` | `system.account.update` |
| POST | `/api/v1/admin/accounts/{id}/reset-totp` | `system.account.update` |

**J4（掛在帳號底下）：`AdminUserClub` 授權**（同一個 `Features/AdminAccounts/`）

| 方法 | 路徑 | 權限碼 |
|---|---|---|
| GET | `/api/v1/admin/accounts/{id}/club-grants` | `system.club_grant.view` |
| POST | `/api/v1/admin/accounts/{id}/club-grants` | `system.club_grant.update`（新增或重新啟用，upsert） |
| DELETE | `/api/v1/admin/accounts/{id}/club-grants/{clubId}` | `system.club_grant.update`（軟撤銷 `is_active=false`） |

**J4（掛在帳號底下）：`AdminUserTeam` 球隊授權**（同一個 `Features/AdminAccounts/`，
coordinator 第二輪補派新增，見下方「第二輪補派：J4 球隊授權」整節）

| 方法 | 路徑 | 權限碼 |
|---|---|---|
| GET | `/api/v1/admin/accounts/{id}/team-grants` | `system.team_grant.view` |
| POST | `/api/v1/admin/accounts/{id}/team-grants` | `system.team_grant.update`（新增或重新啟用，upsert；只能授權該帳號目前有效俱樂部授權範圍內的球隊） |
| DELETE | `/api/v1/admin/accounts/{id}/team-grants/{teamId}` | `system.team_grant.update`（軟撤銷 `is_active=false`） |

**J2 角色與權限**（`Features/AdminRoles/`，`/api/v1/admin/roles`，全域）

| 方法 | 路徑 | 權限碼 |
|---|---|---|
| GET | `/api/v1/admin/roles/permissions`（權限碼字典） | `system.role.view` |
| GET | `/api/v1/admin/roles` | `system.role.view` |
| GET | `/api/v1/admin/roles/{id}` | `system.role.view` |
| POST | `/api/v1/admin/roles` | `system.role.update` |
| PUT | `/api/v1/admin/roles/{id}` | `system.role.update` |
| DELETE | `/api/v1/admin/roles/{id}` | `system.role.update`（`is_system=true` 或仍被帳號指派會擋下） |
| PUT | `/api/v1/admin/roles/{id}/permissions` | `system.role.update`（整份取代非 `sysadmin_only` 的權限指派） |

**J4：`Club` 主檔**（`Features/AdminClubs/`，`/api/v1/admin/clubs`，全域）

| 方法 | 路徑 | 權限碼 |
|---|---|---|
| GET | `/api/v1/admin/clubs` | `system.club.view` |
| GET | `/api/v1/admin/clubs/{id}` | `system.club.view` |
| POST | `/api/v1/admin/clubs` | `system.club.update` |
| PUT | `/api/v1/admin/clubs/{id}` | `system.club.update` |

**`Competition` 維護**（`Features/AdminCompetitions/`，`/api/v1/admin/{club}/competitions`，
俱樂部範圍，用既有的 `IAdminClubAuthorizer`——`competitions.club_id` 必填，跟 `AdminNews` 同一個形狀）

| 方法 | 路徑 | 權限碼 |
|---|---|---|
| GET | `/api/v1/admin/{club}/competitions` | `team.competition.view` |
| GET | `/api/v1/admin/{club}/competitions/{id}` | `team.competition.view` |
| POST | `/api/v1/admin/{club}/competitions` | `team.competition.create` |
| PUT | `/api/v1/admin/{club}/competitions/{id}` | `team.competition.update` |

#### 新增的權限碼（`db/seed/generate-club-seed-sql.py` §18.2，已灌入本機 `tcrfc_club_dev`）

| 代碼 | module／submodule | `is_club_scoped` | `sysadmin_only` |
|---|---|---|---|
| `system.club.view`／`system.club.update` | `J`／`J4` | 0 | **1** |
| `team.competition.view`／`.create`／`.update` | `C`／`C4` | **1** | 0 |
| `system.team_grant.view`／`system.team_grant.update`（第二輪補派） | `J`／`J4` | 0 | **1** |

`role_permissions` 同時補了對應指派（§18.3）：`system_admin` 全給（跟既有 `content.article.*`
一樣，雖然 `is_super_admin` 已經略過檢查，仍種資料比照慣例）；`team_competition` 角色全給
`team.competition.*`（矩陣「球隊／賽事 ✔全」）；`content_editor`／`viewer` 只給
`team.competition.view`（矩陣唯讀）；`partner_club_manager` 給 `team.competition.*`（`own_clubs`，
矩陣「✔ 自家球隊」）。灌入方式跟既有種子腳本一樣（`IF NOT EXISTS` 條件式 `INSERT`，冪等，
`./db/seed/apply-seed.sh` 可重複執行），**不是 DDL**，`db/club-schema.sql` 一行未改。

#### 執行層判斷（規劃書沒寫死，這一輪做了選擇）

1. ✅ **已裁決（2026-09-24，coordinator）**：J1 建立帳號**不做邀請信**——規劃書 §4.10 J1 只寫
   「新增／停用帳號、密碼政策、兩階段驗證」，這不是暫時的最小可行方案，是定案寫法。建立者直接
   在 `POST /accounts` 指定初始密碼，`must_change_password` 一律強制 `true`（比照種子超管
   `sa@system.local` 的既有慣例），初始密碼由建立者透過站外管道轉交。系統信目前只有 9 封
   （會員 5＋商店 4，`docs/14-invariants.md`），本來就沒有「後台帳號邀請信」樣板，不需要新增。
2. **防呆：不能讓系統歸零到沒有啟用中的最高管理權限帳號**（task 5，規劃書未明文，執行層安全
   措施）：`AdminAccountsRepository.EnsureNotLastActiveSuperAdminAsync` 在「停用帳號」與「把
   `is_super_admin` 從 true 改成 false」這兩個操作前檢查，若目標帳號是唯一啟用中的超管就擋下
   （409）。**沒有做**「不能把自己的角色指派清空」這類更廣義的鎖死防呆——規劃書與 task 都只
   提到「最高管理權限」（`is_super_admin`），沒有提到一般角色指派的鎖死情境。
3. ✅ **已裁決（2026-09-24，coordinator）**：`Competition` 的權限碼**維持歸在 `module_code=C`
   （球隊管理）**，不改到 `J`——規劃書 C4（賽程與賽果）本來就屬於球隊管理範疇，`STATUS.md` 把
   「`Club`／`Competition` 當成可維護型別」列在 `S1-1`／`J4` 底下指的是「這件工作歸在哪一輪做」，
   不是「權限碼要歸在哪個 module_code」，兩者不必一致（`Club` 本身仍是 `system.club.*`／`J`，
   因為它是矩陣「系統」欄，只有系統管理員；`Competition` 是矩陣「球隊／賽事」欄，逐角色都有
   明確格子，性質不同）。`role_permissions` 依矩陣逐角色展開（見上表）。
4. **`Competition.Status` 只接受 `draft`／`published`，拒絕 `scheduled`**——`docs/14-invariants.md`
   「S0-7g」已裁決 `competitions` 沒有 `published_at` 欄位、不得提供排程選項，本輪的驗證直接
   把這個已拍板的不變量落地在寫入層（`AdminCompetitionsRepository.ValidateStatus`），不是新判斷。
5. **`Club` 的標誌／favicon／OG 圖三組欄位本輪唯讀**——另一位 `backend-engineer` 同時在改
   `BlobImageStorageService` 與圖片上傳，任務指示明確要求不要動它。`AdminClubDetailDto` 回傳
   目前的 `*_key` 值，但 `CreateAdminClubRequest`／`UpdateAdminClubRequest` 都沒有讓呼叫端設定
   這些欄位的管道。**待辦**：之後應比照 `Features/AdminNews` 的 multipart 契約（選檔即時預覽、
   儲存才上傳）補上，見規劃書 §4.0 圖片上傳通則。
6. **`Club`／`Competition` 都沒有刪除端點**——前者刪除會牽動約 50 張表的外鍵，後者已有 `Match`
   可能引用；規劃書沒有明文要不要支援刪除這兩個型別，本輪判斷「先不做，回報」比「猜一個刪除
   行為」安全。角色（`AdminRole`）與帳號授權（`AdminUserClub`）都有明確的刪除／撤銷語意
   （角色若未被指派可刪、俱樂部授權可撤銷），跟 `Club`／`Competition` 不是同一種情況。
7. **`AdminRolesRepository.ReplacePermissionsAsync` 拒絕整批寫入含 `sysadmin_only` 權限碼的
   請求**（400），不是靜默忽略——docs/12b §7.3 的 `sysadmin_only` 是帳號層級閘門，不是「指派
   給角色」的東西，即使指派了 `PermissionChecker` 也不會讓非超管帳號拿到效果，為避免介面出現
   「勾了但不會生效」的誤導狀態，直接擋在寫入層。既有（種子灌入的）`system_admin` 角色底下的
   `sysadmin_only` 權限列不受這個端點影響（只替換非 `sysadmin_only` 的子集，見程式碼註解）。

#### 第二輪補派：J4 球隊授權（`AdminUserTeam`，2026-09-24，coordinator 補派）

漏掉的 J4 規格：主站規劃書第 1223–1231 行 J4 表格明列「指派帳號可維護哪些球隊
（`AdminUserTeam`），供『學院管理者不得改動一線隊賽程』這類**列級**限制使用，權限僅系統
管理員」。比照 `AdminUserClub` 補上，一樣掛在 `Features/AdminAccounts/` 底下：

- **權限碼獨立成一組 `system.team_grant.view`／`system.team_grant.update`**，不沿用
  `system.club_grant.*`——兩者是規劃書同一張 J4 表格裡並列的兩件事（「俱樂部**與球隊**授權」），
  資源本身也不同（`admin_user_clubs` vs `admin_user_teams`），拆開才能在日後某個角色只需要
  其中一種時單獨授予，也讓 `PermissionChecker` 的判斷維持「一個資源一組碼」的既有慣例（跟
  `system.account.*` 與 `system.club_grant.*` 本來就是分開的兩組是同一個道理）。跟
  `system.club.*`／`system.club_grant.*` 一樣 `sysadmin_only=1`、`is_club_scoped=0`
  （矩陣「系統」欄只有系統管理員）。
- **只能授權該帳號目前有效俱樂部授權範圍內的球隊**：`AdminAccountsRepository.UpsertTeamGrantAsync`
  在寫入前查 `admin_user_teams` 目標球隊的 `club_id`，要求該帳號在 `admin_user_clubs` 對這個
  俱樂部有一筆 `is_active=true` 且未到期的授權，否則丟 `AdminAccountValidationException`（400）。
  這條規則的理由：球隊授權是俱樂部授權底下更細的列級限制，一個連俱樂部本身都沒被授權的帳號，
  取得球隊授權沒有任何實際意義（`AdminClubAuthorizer` 在俱樂部範圍那一關就會先擋下它）。
  `admin_user_teams` 本身沒有 `granted_on`／`granted_by` 欄位（比 `admin_user_clubs` 精簡），
  這是綱要本身的形狀，不是本輪省略——**未動 `db/club-schema.sql`，`admin_user_teams` 既有欄位
  已足夠支撐這個端點**。
- 撤銷（`DELETE .../team-grants/{teamId}`）一樣是軟撤銷（`is_active=false`），立即生效。
- 🔴 **只做授權資料的維護，不做強制**（coordinator 明確指示）：這一輪**沒有**在任何寫入端點
  加上「檢查呼叫者是否只被授權特定球隊」的判斷——列級限制真正生效的地方是 C4（賽程與賽果）
  的寫入端點檢查 `role_permissions.scope_type = 'own_teams'` 時，同時查 `admin_user_teams`
  過濾「這個人能碰哪些球隊」，但 **C4 的寫入端點本輪根本不存在**（`STATUS.md` `S1-7` 才是
  `C1–C3` 球隊／球員／教練，賽程賽果的寫入是之後的 `S1-8`）。`admin_user_teams` 現在可以被
  維護，但還沒有任何程式碼真的去讀它做過濾判斷——這跟 `role_permissions.scope_type` 的既有
  缺口（上方「本輪沒做的部分」第 4 點）是同一件事在球隊授權這個資料表上的具體落點，**強制
  留到 `S1-8` 一併實作**，不在本輪範圍內。

#### 待裁決事項（規劃書與 docs/12 都答不到，且影響客戶看到的行為）

**沒有**——本輪範圍內遇到的疑問都能在既有文件（規劃書 §4.10／§5.3／§5.4／§6、docs/12b §7）
或既有不變量（`docs/14` S0-7g）裡找到答案，或屬於上面列出的、有明確理由的執行層判斷。J1 邀請信
與 Competition 權限碼歸屬兩項已由 coordinator 裁決（見上方判斷 1／3），不再是待裁決事項。

#### 測試

新增四個測試檔（30 項）：`Tcrfc.Api.Tests/AdminAccountsTests.cs`（14 項，含「停用立即撤銷更新
權杖」「重設密碼撤銷既有工作階段」「俱樂部授權新增即生效、撤銷即失效」「防呆：不能讓系統歸零
到沒有啟用中的超管」「球隊授權新增即生效、撤銷即失效」「球隊授權不在有效俱樂部授權範圍內擋下」）、
`AdminRolesTests.cs`（8 項，含「刪除系統角色擋下」「刪除仍被指派的角色擋下」「拒絕指派
`sysadmin_only` 權限碼」）、`AdminClubsAndCompetitionsTests.cs`（8 項，含 `Club`／`Competition`
的 401／403／跨俱樂部／代碼衝突／狀態驗證）。全部走真正的 HTTP 管線與真正的 `tcrfc_club_dev`，
反例用真實的攻擊或誤用形狀（跨俱樂部、非超管角色、`sysadmin_only` 權限碼、系統角色刪除、球隊
授權超出俱樂部授權範圍），不是隨便塞錯值（`docs/18-work-errors.md` `E-39` 的教訓）。

```
$ dotnet test    # CLUB_SQL_CONNECTION_STRING 指向本機 tcrfc_club_dev，見「怎麼跑」一節
已通過! - 失敗: 0，通過: 172，略過: 0，總計: 172（既有 142 ＋ 第一輪 26 ＋ 第二輪 4）
```

`ArchitectureTests`（型別層強制授權的 Roslyn 掃描）已擴充納入 `AdminSystemScope`，仍然通過。

---

**本輪（新聞 B2 寫入垂直切片，2026-09-22，`backend-engineer`）**：讓 `apps/admin` 能從前端假資料
改接真實資料庫，第一步先做**新聞（B2）**——刻意只做這一個模組，因為它是目前唯一有真實後台畫面
（`apps/admin/src/views/news/`）的模組，做完才有畫面可以驗。新增：

1. **EF Core 一次性 handoff**（`docs/20-cicd.md` §5）：對已用 `db/club-schema.sql` 建好的
   `tcrfc_club_dev` 跑 `dotnet ef dbcontext scaffold`，產出 `Data/ClubDbContext.cs` ＋
   `Data/EfEntities/`（138 個實體，對應全部 145 張表），並建立標記為已套用的空白基準 migration
   `InitialBaseline`（`Data/Migrations/`）——**沒有對資料庫真的跑任何 DDL**，只在
   `__EFMigrationsHistory` 插入一筆紀錄。細節與怎麼重做見下方「EF Core 一次性 handoff」。
2. **後台新聞寫入端點**（`Features/AdminNews/`）＋**後台新聞讀取端點**（同一組檔案，補
   `STATUS.md` S0-12 提到的缺口：既有五組 GET 只回已發布內容，後台驗證不到草稿／排程／已停用）。
3. **寫入端點開發模式開關**（`Security/DevWriteGate.cs`）：🔴 這組端點在接上登入與權限之前
   **不得在任何對外環境啟用**，見下方「寫入端點開發模式開關」整節。
4. ⚠️ **讀取仍是 Dapper（既有五組 GET／repository 一行未動，除 `ClubScopingTests`／
   `CacheBehaviorTests` 兩個因為藍鯨真實資料到位而過期的斷言外，見下方「發現的既有落差」），
   寫入改走 EF Core**——docs/17-deployment.md §0 的技術選型「EF Core ＋ Dapper」本輪首次真正落地。

🔴 **2026-09-21（`deployment-engineer`）：本機開發資料庫合併進既有的 `sqlserver` 容器**，
`docker-compose.dev.yml` 的 `mssql-dev` 服務已退場（見 [`deploy/README.md`](../../deploy/README.md)）。
**下方所有連線範例已改為新位址**（容器內連 `host.docker.internal,1433`、宿主機直連
`127.0.0.1,1433`）；文中提到「本機 `mssql-dev`」的地方是**當時（S0-7b／S0-7d）的驗證紀錄**，
如實保留不回頭改寫歷史，但那個服務現在已不存在，照著跑之前請先看這一段與 `deploy/README.md`。

---

**本輪（圖片上傳共用元件，2026-09-22，`backend-engineer`）**：`STATUS.md` S0-8——後台每一個表單
的圖片欄位都會用到的共用後端元件，逐字對應規劃書 §4.0「後台圖片上傳通則」（v3.9）。新增：

1. **`Images/`**：純轉檔邏輯（`ImageProcessor`，不碰任何 I/O）＋物件儲存接縫（`IImageStorageService`，
   Azure Blob Storage 實作 `BlobImageStorageService`，本機開發接 Azurite）。伺服器端一律重新編碼、
   去除全部中繼資料（含 EXIF GPS）、長邊超過 2560px 等比縮小、固定產 1280／640／320／160 四個衍生檔，
   全部 WebP。
2. **`Features/Uploads/`**：一個通用上傳端點（`POST /api/v1/admin/{club}/uploads/images/{entityType}/{entityId}/{field}`），
   跟 `Features/AdminNews` 一樣掛在 `DevWriteGate` 後面。**這是共用元件，不是新聞專用**——見下方
   「圖片上傳共用元件」整節的完整契約。
3. **接線示範**：`Features/AdminNews/AdminArticlesRepository` 換圖（`UpdateAsync`）與刪除文章
   （`DeleteAsync`）時呼叫 `IImageStorageService.DeleteAsync` 清掉舊物件——這是任務指示「用一個真實
   模組示範接線」的落點，其他模組（球員照片、贊助商標誌……）之後照抄同一個模式即可，見下方
   `UploadSlotPolicy` 的說明。

---

🔴🔴🔴 **本輪修正（上傳時機違反規劃書，2026-09-22，`backend-engineer`）**：上一輪把上傳做成
「先呼叫獨立端點拿物件鍵、表單儲存時再把鍵塞進純 JSON 建立／更新請求」的兩段式設計，README
當時也如實記錄了這是「本輪的判斷，不是規劃書明文」。**這個判斷是錯的**——規劃書第 53 行與
第 972 行明文「選檔不上傳、儲存才上傳……離開或取消表單不留下任何檔案」，兩段式設計下「選檔」
那一刻檔案就已經真的寫進物件儲存，違反這條規則。使用者拍板：改成符合規劃書，不是回頭改規格。
本輪異動：

1. **`Features/AdminNews` 的建立／更新端點改成單一 `multipart/form-data` 請求**（封面圖片跟
   其餘欄位一起送出），新增 `AdminArticleRequestForm.cs`（解析 `payload`＋`file` 兩個表單欄位）、
   `CoverKeyUpdate.cs`（封面圖片三態：維持不變／清空／換新值）。`CreateArticleRequest` 拿掉
   `CoverKey` 欄位、`UpdateArticleRequest` 拿掉 `CoverKey` 改成 `RemoveCover`（布林）。
2. **整支移除 `Features/Uploads` 的獨立上傳端點**（`UploadsEndpoints.cs`／`UploadDtos.cs`，
   `Program.cs` 不再呼叫 `MapUploadsEndpoints`）——那個端點的存在本身就是「選檔即上傳」兩段式
   設計的載體，留著就是留一個讓下一個模組複製同一個錯誤的樣板。`UploadSlotPolicy.cs`（欄位插槽
   允許清單）留下來，改成由每個模組自己的建立／更新端點在同一次 multipart 請求裡直接呼叫。
3. **失敗時的補償交易**：圖片已經上傳成功、但資料列寫入失敗（分類不存在、slug 重複、樂觀並行
   衝突……）時，端點會刪掉剛剛上傳的物件，不留下孤兒物件——見下方「圖片上傳共用元件」整節與
   `AdminNewsCoverUploadTests` 的自動化測試。
4. **這是一筆規格違反，已記入 [`docs/18-work-errors.md`](../../docs/18-work-errors.md) `E-34`
   升級段**（根因不是實作本身，是判斷被當成既定契約往下傳，見該檔案說明）。

✅ **`apps/admin` 已跟進（2026-09-22）**：`ImageUploader.vue` 已改成受控元件（選檔只預覽、檔案留記憶體），由 `NewsEditView.vue` 在按「儲存」時組 multipart 一起送。下一棒
`frontend-architect` 要照這份新契約重做，見下方「給前端接的契約」整節。

---

🔴 **本輪（S0-8c 第②點，2026-09-24，`backend-engineer`）**：`STATUS.md` S0-8c 第②點——
`BlobImageStorageService.UploadAsync` 寫五個物件本身不是原子操作，中途失敗會留下部分衍生檔孤兒，
既有補償交易只覆蓋「資料列寫失敗」這一層，沒覆蓋「五個物件寫到一半」。本輪：

1. `UploadAsync` 內部把物件寫入段包進 `try/catch`，失敗時呼叫既有 `DeleteAsync`（跟呼叫端的
   「資料列寫失敗」補償交易共用同一個方法、同一套 fail-open 語意）盡力刪掉這次已寫入的物件，
   再把原例外原樣拋出——細節與理由見下方「失敗回滾」整節末段。
2. 評估「行程中途崩潰、補償邏輯來不及跑」的孤兒清理機制，**判斷現在不值得做**：S0-8 目前只接了
   `articles.cover_key` 一個模組，涵蓋單一模組的清理機制在之後模組陸續接上時會默默失真（與
   `docs/18-work-errors.md` `E-31`／`E-39` 升級段同一種「局部套用給全面信心」的風險），且誤刪
   合法圖片的後果遠比留著孤兒物件嚴重，理由詳見「已知缺口」第 4 點。
3. 新增 `Tcrfc.Api.Tests/BlobImageStorageServiceUploadFailureTests.cs`（6 項）：不經 HTTP、
   直接建構 `BlobImageStorageService`，用子類化 `BlobContainerClient`／`BlobClient`（兩者的公開
   方法皆為 `virtual` 且保留無參數建構子，Azure SDK 官方支援的作法，不需要額外 mocking 套件）
   包住這個 fixture 真正在跑的 Azurite 容器，在第 N 次 `GetBlobClient` 呼叫注入失敗，
   `N=1..5` 五個物件各驗一次，外加一支「補償刪除本身也失敗」的雙重失敗情境。
4. `dotnet test` 全數 141 項通過（既有 135 ＋本輪 6），未改動 `Data/Migrations/`、未對
   `tcrfc_club_dev` 執行任何 DDL。

---

## S1-4：B1 頁面管理（2026-09-24，`backend-engineer`）

### 讀到的規劃書條文

主站規劃書 §4.2 B1（約行 1010–1016）：

> **區塊化編輯器（Block Editor）**：文字、圖文左右、圖片藝廊、影音嵌入、引言、CTA、手風琴 FAQ、
> 時間軸、步驟條、數據卡、表格、檔案下載
> 每頁具備：狀態（草稿／已發布／排程）、SEO 設定、多語系版本、版本歷程與還原、預覽連結（未發布可分享）

外加 §4.0 後台設計通則（圖片上傳「選檔不上傳、儲存才上傳」）與 docs/14-invariants.md S0-7g／E-44／
E-46／E-47 三筆既有教訓（排程發布要接 `ScheduledPublishRunner`、migration 操作邊界、補償刪除的
token 選用）。

🔴 **落差回報**：規劃書那一句逐字數出來是 **12 個**區塊名稱，但 `docs/12b-database-tables.md` §3.1
與 `db/club-schema.sql` 的 `page_blocks` 表註解都寫「13 種型別」。規劃書本體沒有給出第 13 個名稱，
依任務指示「規劃書已定的照做；沒寫的不自創使用者可見功能」，本輪**只實作這 12 種**
（見 `Features/AdminPages/PageBlockTypes.cs`），不杜撰一個沒有名稱的第 13 種。這是既有文件本身的
落差，不是本輪造成的。✅ **已修正（2026-09-24）**：規劃書是唯一真實來源，`docs/12`、`docs/12c`、DDL 註解與 STATUS.md 已改為 12 種。

### 端點清單

後台（`/api/v1/admin/{club}/pages`，`Features/AdminPages/AdminPagesEndpoints.cs`，一律經
`IAdminClubAuthorizer`）：

| 方法與路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET /api/v1/admin/{club}/pages` | `content.page.view` | 清單（含全部狀態），`status`／`keyword`／`page`／`pageSize` |
| `GET /api/v1/admin/{club}/pages/{id}` | `content.page.view` | 單頁詳情（含區塊、SEO、最新版本編號與預覽權杖） |
| `POST /api/v1/admin/{club}/pages` | `content.page.create` | 建立（一律草稿），`multipart/form-data` |
| `PUT /api/v1/admin/{club}/pages/{id}` | `content.page.update` | 整份取代（SEO ＋ 全部區塊），不改狀態，`multipart/form-data` |
| `POST /api/v1/admin/{club}/pages/{id}/publish` | `content.page.publish` | draft／scheduled → published |
| `POST /api/v1/admin/{club}/pages/{id}/schedule` | `content.page.publish` | draft／scheduled → scheduled（未來時間） |
| `DELETE /api/v1/admin/{club}/pages/{id}` | `content.page.delete` | 刪除（`?expectedUpdatedAt=`），連帶刪除全部區塊圖片物件 |
| `GET /api/v1/admin/{club}/pages/{id}/versions` | `content.page.view` | 版本歷程清單 |
| `GET /api/v1/admin/{club}/pages/{id}/versions/{versionNo}` | `content.page.view` | 單一版本快照詳情 |
| `POST /api/v1/admin/{club}/pages/{id}/versions/{versionNo}/restore` | `content.page.update` | 還原（見下方「我的判斷」） |

公開（`Features/Pages/PagesEndpoints.cs`，無需登入）：

| 方法與路徑 | 說明 |
|---|---|
| `GET /api/v1/{club}/pages/{*slug}` | 只回 `status='published'` 且已到發布時間的內容，`?lang=` 依語系回退 |
| `GET /api/v1/pages/preview/{token}` | 未發布可分享的預覽（無 `{club}` 路由段，權杖本身即授權），回應帶 `X-Robots-Tag: noindex, nofollow` |

權限碼種子（`db/seed/generate-club-seed-sql.py` §18.2／§18.3，DML，未動 DDL）：
`content.page.view`／`create`／`update`／`publish`／`delete`（`module_code=B`、`submodule_code=B1`），
角色指派逐字比照既有 `content.article.*` 的鋪法（規劃書 §6 矩陣「內容」欄同時管 B1 與 B2，矩陣沒有
分欄）：`system_admin` 全給；`content_editor` 給 view/create/update/publish/delete；
`team_competition`／`viewer`／`partner_club_manager`（`own_clubs`）比照 `content.article.*` 現有的
子集合；`academy_program`／`business_sponsorship`／`pr_media`／`customer_service_admin`／
`translator` **本輪未指派**——這不是遺漏，是延續 `content.article.*` 既有的範圍縮減（那五個角色對
B2 新聞本來就是零筆 `role_permissions`），保持兩個內容模組的角色矩陣一致，不在本輪单方面擴大範圍。

### 12 種區塊的欄位與驗證摘要

`Features/AdminPages/PageBlockContentProcessor.cs` 是唯一的驗證與圖片解析落點。**雙語怎麼落在
`page_blocks.content` 這個單一 JSON 欄位裡**（`docs/12b` §3.1／`db/club-schema.sql` 已明確拒絕
`page_blocks_i18n` 側表，「主表已放的欄位優先」）：每一個使用者看得到的自由文字欄位本身是一個
`{"zh": "非空白字串", "en": null|"字串"}` 物件（CLAUDE.md 全域規定 4，英文可空但鍵一定存在）；
網址、識別碼、日期、數值這類非語言內容維持單一純值。圖片替代文字沿用既有後台圖片上傳通則的扁平
`altZh`／`altEn` 兩個鍵（不是巢狀物件）——刻意不統一形狀，理由見該檔案檔頭註解。

| 區塊（代碼） | 必填欄位 | 型別 |
|---|---|---|
| 文字 `text` | `body`（雙語） | — |
| 圖文左右 `text_image` | `body`（雙語）、`imagePosition`（`left`/`right`）、`image`（圖片欄位組） | 有圖片 |
| 圖片藝廊 `gallery` | `images`（≥1 張，圖片欄位組陣列） | 有圖片 |
| 影音嵌入 `video_embed` | `provider`（`youtube`/`vimeo`）、`videoId`；可選 `caption`（雙語） | — |
| 引言 `quote` | `text`（雙語）；可選 `attribution`（雙語） | — |
| CTA `cta` | `text`（雙語）、`buttonLabel`（雙語）、`buttonUrl` | — |
| 手風琴 FAQ `accordion_faq` | `items`（≥1 筆，`question`／`answer` 皆雙語） | — |
| 時間軸 `timeline` | `items`（≥1 筆，`date` 純值、`title` 雙語；可選 `description` 雙語） | — |
| 步驟條 `steps` | `items`（≥1 筆，`title` 雙語；可選 `description` 雙語） | — |
| 數據卡 `stat_cards` | `items`（≥1 筆，`value` 純值、`label` 雙語） | — |
| 表格 `table` | `headers`（≥1 欄，雙語物件陣列）、`rows`（每列欄數須等於 `headers` 長度，儲存格純字串） | — |
| 檔案下載 `file_download` | `label`（雙語）、`fileUrl` | 見下方已知缺口 |

圖片欄位組：`{ pendingUpload?, key?, width?, height?, altZh, altEn }`——`pendingUpload: true` 代表
待上傳（不含 `key`），否則必須已有非空白 `key`（沿用既有圖片，不換圖）。

### 圖片上傳：多檔案 multipart 契約（給前端接的形狀）

跟 B2 新聞固定單一 `file` 欄位不同——頁面區塊可能同時有多張待上傳圖片（圖文左右 1 張、圖片藝廊
N 張，且同一次請求可能有多個這類區塊）。契約（`Features/AdminPages/AdminPageRequestForm.cs`）：

- 固定欄位 `payload`：JSON 文字，`CreatePageRequest`／`UpdatePageRequest`（camelCase）。
- 檔案欄位命名 `file:{區塊索引}:{圖片路徑}`——圖文左右固定是 `file:{i}:image`；圖片藝廊依陣列位置
  是 `file:{i}:images:0`、`file:{i}:images:1`……。`{區塊索引}` 是 `Blocks` 陣列裡的位置（從 0 起算）。
- 對應圖片欄位物件要標示 `"pendingUpload": true` 才會去找對應檔案；沒標示就必須已經帶著既有 `key`。
- 物件鍵路徑：`{club}/pages/{pageId}/blocks/{blockIndex}/{path}`（`path` 的 `:` 換成 `-`）。

失敗時的補償：`AdminPagesRepository.CreateAsync`／`UpdateAsync` 內部把「驗證＋圖片上傳＋寫入資料庫」
包在同一個 `try/catch`，任何一步失敗（含後面某個區塊驗證失敗）都會把這次呼叫已經真的上傳成功的物件
逐一刪除（`CancellationToken.None`，E-47 教訓——不沿用可能已取消的請求 token）。換圖（`UpdateAsync`）
與刪除頁面時，「新圖／新版本寫入成功後才刪舊物件」用的是請求本身的 `cancellationToken`（fail-open，
跟 B2 新聞現有行為一致），這是兩種不同性質的刪除，故意用不同 token，見 repository 上的註解。

### 版本歷程與還原：我的判斷（規劃書沒定義還原後的行為）

- **每次寫入（建立／更新／發布／排程／還原）都會產生一個新的 `page_versions` 列**，`version_no`
  遞增，`snapshot` 存 `{seo:{zh,en}, blocks:[{blockType,content}]}` 的完整 JSON。發布／排程也算一次
  「寫入」是因為狀態轉換本身也是頁面生命週期的一個節點，值得留下歷史快照可回頭比對。
- **還原＝以舊版內容產生一個新版本**，不是把時間倒轉覆蓋掉中間的版本——舊版本列本身不變動、不刪除。
- **還原不改變頁面目前的發布狀態**：若目前是 `published`，還原後的內容立即對外可見（跟一般編輯
  `PUT` 的行為一致，「編輯不需要重新送審」）。若這不是預期行為（例如「還原已發布頁面應該先退回
  草稿」），規劃書沒有這個開關，需要另外裁決再補。

### 預覽連結：權杖何時產生、已知缺口

- **權杖隨每一次新版本快照自動產生**（`GeneratePreviewToken()`，256-bit 亂數 Base64Url），不開獨立的
  「產生預覽連結」端點——`AdminPageDetailDto.PreviewToken` 直接回傳最新版本的權杖，前端組
  `/{locale}/preview/{token}` 即可分享。規劃書只要求「未發布可分享」，沒有規定 token 的產生時機，
  這是本輪判斷「每次存檔自動換一個」比「另開端點手動產生」更貼近「隨時能分享目前狀態」的語意。
- 🔴 **綱要缺口（已回報，未動手加欄位／migration）**：`page_versions.preview_token` **沒有 DB 唯一
  索引**（256-bit 熵值下碰撞機率可忽略，但沒有資料庫層保證），**也沒有到期或撤銷欄位**——權杖一旦
  核發即永久有效，直到那個版本被別的原因取代（實際上因為每次寫入都換一個新版本＋新權杖，舊版本的
  舊權杖仍然永久可用）。規劃書與 `docs/12b` 都沒有定義這兩個欄位，依任務指示「沒有欄位就停下回報，
  不自己加表或欄位」，本輪只用「高熵亂數」與「盡量遵循既有頁面沒有更長效資料外洩管道」降低風險，
  沒有解決「權杖外流即永久有效」這個產品層面的問題——**這是需要決定的事**：要不要補
  `preview_expires_at`／`preview_revoked_at` 欄位。
- 預覽端點刻意**不接快取**（IQueryCache）——draft 內容剛存檔就分享是最常見的使用情境，快取住舊值
  的後果比多查一次 SQL 嚴重；權杖本身流量極低，直接回源沒有效能疑慮。
- `X-Robots-Tag: noindex, nofollow` 只加在這支 API 回應——**真正擋搜尋引擎的是前台渲染頁面本身的
  noindex**，那是 `apps/web`（Nuxt）的職責，本輪任務邊界只有 `apps/api`，這裡只做防禦性補強，
  完整落實需要前端配合（見下方「給下一位的交接事項」）。

### `pages.club_id` 必填：跟 B2 新聞的關鍵差異

`pages` 在 docs/14「50 張必填 `club_id`」之列（不像 `articles` 可為空的 9 張之一）——**沒有「共同
內容」這件事**，每個頁面都明確屬於一個俱樂部。因此：

- 沒有 `SharedArticleReadOnlyException` 對應的分支——`LoadTrackedForWriteAsync` 只有「屬於這個
  俱樂部」與「不屬於（含真的不存在）」兩種結果，後者一律回 404。
- 公開讀取直接 `WHERE club_id = @ClubId`，不套用 `ClubOrSharedSql`（俱樂部專屬優先、回退共同）——
  那條規則是給可為空 `club_id` 的表用的，頁面不適用。

### 排程發布：已接進 `ScheduledPublishRunner`

`Features/News/ScheduledPublishRunner.cs` 新增 `PublishDuePagesAsync`（邏輯與既有
`PublishDueArticlesAsync` 逐字對應：同一個冪等 `UPDATE ... OUTPUT` 寫法、同一個「不覆寫
`published_at`」理由），`ScheduledPublishBackgroundService` 每輪依序呼叫兩個方法。快取失效用
`page-detail` entity（`AdminPagesRepository.PublicDetailEntity`／`PagesRepository` 的
`DetailEntity`／`ScheduledPublishRunner` 的 `PageDetailEntity` 三處字面值必須一致，已 grep 核對）。
排程發布的 TTL 延遲說明與 B2 新聞完全同一份取捨，不重複貼一次。

### 網址名稱（slug）：允許多層路徑，不做跨模組保留字偵測

B1 頁面本身**就是**網站的靜態頁面路由（docs/01「URL 直接對應網站層級」，例 `/zh/academy/join/`），
`PageSlugPolicy` 允許 slug 含 `/`（多層路徑），唯一鍵 `(club_id, slug)`（`UQ_pages_club_slug`，
已存在於 DDL，未新增）。⚠️ **已知缺口**：不偵測 slug 是否撞到 07 新聞／08.3 商店／13 行事曆等
「資料型內容」自己的路由前綴（例如把頁面存成 `"news"` 或 `"shop/x"`）——這類跨模組路由表比對需要
`apps/web` 的完整路由清單，`apps/web` 本輪由另一個 agent 同時在改、依派工指示不得觸碰，性質與
`Features/AdminNews/SlugPolicy.cs` 檔頭記錄的「無法做到跨專案自動比對」同一種缺口。

### 測試（`Tcrfc.Api.Tests`，新增 6 個檔案）

| 檔案 | 涵蓋 |
|---|---|
| `AdminPagesWriteTests.cs` | 401／403（無登入、檢視者、跨俱樂部無授權）、完整生命週期、slug 重複／格式錯誤、多層路徑 slug、跨俱樂部 404、樂觀並行 409、狀態轉換 409、排程時間非未來 400 |
| `AdminPagesBlockValidationTests.cs` | 12 種區塊各一組合法／不合法內容（`[Theory]`），未知區塊型別 400，空區塊陣列允許建立空白草稿 |
| `AdminPagesVersionsAndPreviewTests.cs` | 每次寫入產生新版本、版本列表與單版詳情、還原產生新版本且不覆蓋舊版、還原不存在版本 404、未發布頁面預覽可見＋`noindex` 標頭、猜測權杖 404 |
| `PagesPublicEndpointTests.cs` | 草稿／排程中不可見、已發布可見且雙語物件已化簡、跨俱樂部路由查不到 |
| `AdminPagesImageTests.cs`（Azurite） | 圖文左右／圖片藝廊真實上傳、補償交易（HTTP 層）、換圖刪舊物件、刪除頁面連帶刪圖、**E-47 樣式的補償刪除 token 測試**（見下） |
| `AdminPageMultipart.cs`／`PageBlockSamples.cs`／`Fixtures/TestAdminHttpContext.cs`／`Fixtures/FakeFormFileCollection.cs` | 測試工具，不是測試本身 |

**E-47 樣式測試的做法**：`AdminPagesImageTests.補償刪除沿用CancellationTokenNone_即使外層token已取消仍能清乾淨`
略過 HTTP 層，直接呼叫 `AdminPagesRepository.CreateAsync`——用一個裝飾 `IImageStorageService` 的
類別，在**第一個區塊圖片真的上傳成功的瞬間**取消呼叫端傳入的 `CancellationTokenSource`（模擬
「圖片剛上傳完成、使用者就在這一刻斷線」），接著第二個區塊驗證失敗觸發補償——斷言即使外層 token
此時已經是取消狀態，補償刪除仍然把第一個區塊的物件清乾淨。要拿到合法的 `AdminClubScope`（建構子
`internal`）必須真的呼叫 `IAdminClubAuthorizer.AuthorizeAsync`，`Fixtures/TestAdminHttpContext.cs`
組一個只帶 `sub`／`username` claim 的 `HttpContext`（`AdminAccountGate` 只讀這兩個 claim 加資料庫
查詢，不需要真的跑完整 JWT 中介軟體管線）。

`dotnet test`（`Tcrfc.Api.Tests.csproj`）**224/224 全過**（含本輪新增約 39 項與既有全部項目）；
`ArchitectureTests` 綠燈（本輪沒有新增 `ClubScope`／`AdminClubScope` 型別，不影響那支掃描）；
`dotnet ef migrations has-pending-model-changes` 回報無待處理變更（`Page.UpdatedAt` 加
`IsConcurrencyToken()` 經實測**不產生任何 DDL 操作**——用 `dotnet ef migrations add` 產生過一次探測
用的空白 migration 確認 `Up()`/`Down()` 皆為空後已用 `dotnet ef migrations remove` 移除，該 migration
從未套用到任何資料庫，移除只刪檔案與還原 snapshot，未執行任何 DDL）。

### 給下一位的交接事項

1. **前台 noindex**：`apps/web` 需要在渲染 `/{locale}/preview/{token}` 這類頁面時輸出
   `<meta name="robots" content="noindex, nofollow">`（或等效的 route rule），本輪只在 API 回應
   層補了 `X-Robots-Tag`，這件事只完成一半。
2. ~~`docs/12b`／DDL 的「13 種型別」~~ ✅ 已依規劃書第 1012 行改為 12 種（2026-09-24）。
3. **預覽權杖沒有到期／撤銷欄位**：是否要補 `page_versions.preview_expires_at`／`preview_revoked_at`
   需要使用者裁決，屬於綱要異動（先改 `docs/12` 再走 migration）。
4. **`academy_program`／`business_sponsorship`／`pr_media`／`customer_service_admin`／`translator`
   五個角色目前對 B1／B2 兩個內容模組都是零權限**——等這些角色真的有使用者要指派時，再依規劃書
   §6 矩陣補上對應的 `role_permissions`（矩陣其實有給這些角色「撰稿」「編輯」等格子，只是兩個
   內容模組都還沒真的接線）。

---

## S1-4 續作：`PagesPublicEndpointTests` 間歇性失敗根因排查＋前端回報三個 API 缺口（2026-09-24）

### 根因：`published_at` 與 `SYSUTCDATETIME()` 分屬兩個時鐘來源

`PagesPublicEndpointTests.已發布頁面_公開端點看得到_只回已發布內容` 用
`dotnet test --filter FullyQualifiedName~Pages --no-build` 連跑 15 次失敗 2 次（第一次任務指示
懷疑是樂觀並行權杖精度問題）。**實測定位**（不是憑猜測）：在失敗行加診斷輸出，捕捉發布呼叫本身的
狀態碼與回應內容、以及失敗當下直接查資料庫的列值，重跑重現後拿到的診斷輸出是：

```
publishStatus=OK  publishBody={"status":"published", ...}  dbRow=status=published published_at=... updated_at=...
```

**發布呼叫本身完全成功**（不是 409 樂觀並行衝突），資料庫裡的列也確實是 `published`——但緊接著
的公開查詢仍然 404。這排除了「並行權杖精度」這個方向（`updated_at` 從未參與任何即時時鐘比較，
樂觀並行檢查是 EF Core 產生的 `WHERE updated_at = @原始值` 純值比對）。

真正根因：`PagesRepository.GetBySlugAsync`／`ArticlesRepository` 的公開查詢用
`published_at <= SYSUTCDATETIME()` 判斷「已到發布時間」，但 `AdminPagesRepository.PublishAsync`／
`AdminArticlesRepository.PublishAsync`（立即發布）原本把 `PublishedAt` 設成應用程式行程的
`DateTime.UtcNow`——這是**兩個不同的時鐘來源**（應用程式行程所在機器的作業系統時鐘 vs.
`sqlserver` 容器自己的作業系統時鐘）。兩個時鐘只要有任何飄移（本機環境用 Docker Desktop for Mac，
主機睡眠喚醒後尤其容易有數毫秒到數十毫秒的飄移），剛發布的內容就可能被資料庫自己的「現在」判定
為「還沒到」，直到資料庫時鐘追上為止——飄移窗越小，重現機率越低，正好符合「時好時壞」的表現。

**修法**：新增 `Common/DatabaseClock.cs`，`PublishAsync`（僅此方法，`ScheduleAsync` 與
`ScheduledPublishRunner` 不受影響，理由見該檔案檔頭與兩個 repository 上的行內註解）改用
`SELECT SYSUTCDATETIME()` 向資料庫要一次「資料庫自己的現在」，取代 `DateTime.UtcNow`，確保寫入的
`PublishedAt` 保證不晚於資料庫自己接下來任何一次 `SYSUTCDATETIME()` 讀取。改了
`AdminPagesRepository.cs` 與 `AdminArticlesRepository.cs` 兩處（後者雖然任務指示只點名要查，
但排查後確認是同一個根因、同一個修法，一併修正，且發現既有的
`AdminNewsCacheInvalidationTests.更新已發布文章後...` 也隱性依賴這個時序，一併受益）。

**驗收**：`dotnet test --filter FullyQualifiedName~Pages --no-build` 連跑 **22 次、0 失敗**；
全套 `dotnet test`（`Tcrfc.Api.Tests.csproj`）**224/224 通過**。另外把「每一步寫入都要斷言回應
狀態」這條規則落實到 `PagesPublicEndpointTests.cs`（改用共用的 `CreateAsync`／`PublishAsync`／
`ScheduleAsync` 私有輔助方法，任何一步非預期狀態碼會直接讓測試在那一步失敗並印出回應內容，
不會讓錯誤在後面幾行之外的斷言才冒出來、訊息對不上真正出錯的那一步）。

### 前端回報缺口①：`GET /api/v1/admin/auth/me`

站台切換器（規劃書 §4.0）需要知道「登入的這個人可以切到哪些俱樂部」，登入回應
（`LoginResponse`）只有 `Username`／`IsSuperAdmin` 兩個身分欄位，前端拿不到俱樂部授權清單、
姓名、角色。新增 `Features/AdminAuth/AdminAuthService.GetMeAsync`＋
`GET /api/v1/admin/auth/me`（掛在既有 `AdminAuthEndpoints` 群組，最小「有登入即可」檢查，
比照 change-password／2fa 端點的模式，但額外呼叫 `AdminAccountGate.RequireActiveAccountAsync`
重新確認帳號仍是活躍狀態，不只信任 JWT claims）。

回應（`MeResponse`）：`AdminUserId`／`Username`／`DisplayName`／`IsSuperAdmin`／
`PrimaryClubCode`（`AdminUser.primary_club_id` 解析出的俱樂部代碼）／`ClubGrants`（已過濾到期與
停用的俱樂部授權，每筆帶 `ClubCode`／中英文名稱／`IsPrimary`／`ExpiresOn`）／`Roles`（`Code`／
`NameZh`／`NameEn`）。⛔ 不含密碼雜湊、2FA 密文等任何機密欄位。

🔴 **系統管理員的 `ClubGrants` 資料來源不是 `AdminUserClub`**：規劃書 §6「系統管理員跳過整個
資料範圍查詢」，種子的 `sa@system.local`／`super.admin@tcrfc.test` 本來就沒有任何一筆
`admin_user_clubs`——回傳「全部啟用中的俱樂部」（`Clubs.Where(status='active')`），對系統管理員
而言結果等價於「被授權的俱樂部」（反正它能碰全部），但資料來源刻意不同，`ExpiresOn` 固定回
`null`（系統管理員的存取不受到期日限制）。

### 前端回報缺口②之一：`GET /api/v1/admin/{club}/seasons`

賽事系列（C4）表單需要球季下拉選單。俱樂部範圍端點（加在既有 `Features/AdminCompetitions`），
權限碼**比照同模組既有權限碼**：沿用 `team.competition.view`，不新增權限碼——球季目前只有這一個
唯讀查詢用途，還沒有獨立的維護畫面。回應 `AdminSeasonListItemDto`：`Id`／`Code`／`StartOn`／
`EndOn`（`Season` 沒有側表，規劃書沒有給球季名稱欄位）。

### 前端回報缺口②之二：`GET /api/v1/admin/teams`

J4「球隊授權」（`admin_user_teams`）畫面需要球隊下拉選單，且**必須跨俱樂部**——指派球隊授權的
操作者是系統管理員，球隊本身可能來自任何俱樂部（例如系統管理員要把藍鯨的某個梯隊指派給某個
帳號）。新增 `Features/AdminTeams/`：全域端點（無 `{club}` 路由段），比照
`Features/AdminClubs/AdminClubsEndpoints.cs`（J4 俱樂部主檔同樣需要跨俱樂部列出全部俱樂部）
的既有先例，用 `IAdminSystemAuthorizer`。權限碼**比照同模組既有權限碼**：沿用
`system.team_grant.view`（J4／S1-3 續作已種好，就是「球隊授權」畫面本身的檢視權限），不新增。
支援 `?clubCode=` 選填篩選（畫面已經選定俱樂部時可以少拉一點資料）。回應
`AdminTeamListItemDto`：`Id`／`ClubId`／`ClubCode`／`ClubNameZh`／`Code`／`Type`／`Gender`／
`AgeBand`／`NameZh`／`NameEn`，每列自帶俱樂部代碼與名稱，前端不必再逐一查詢俱樂部主檔湊跨俱樂部
畫面。

### 三個新端點的授權測試

`AdminMeEndpointTests.cs`（401 未登入、一般角色只看得到自己被授權且未過期的俱樂部、過期授權不
出現在清單、系統管理員看得到全部啟用中的俱樂部）、`AdminSeasonsEndpointTests.cs`（401、403 無該
俱樂部授權、有權限的角色 200、檢視者唯讀角色 200）、`AdminTeamsEndpointTests.cs`（401、非系統
管理員 403、系統管理員一次拿到跨俱樂部的球隊清單、`clubCode` 篩選）——共 12 項，全部通過，
全套 `dotnet test`（`Tcrfc.Api.Tests.csproj`）由 224 項增為 **236 項，全數通過**。

### 本次沒動的部分

- 沒有新增／修改任何 DDL、`db/club-schema.sql`、`Data/Migrations/`。
- 沒有修改 `apps/admin`（前端接線由另一位 agent 處理），只在 `apps/admin/README.md` 補一段
  「種子帳號重設腳本已存在」的說明（回應該檔案原本記錄的已知缺口）。
- 沒有 commit。

---

## S1-5：`B2` 新聞與故事後端補完（2026-09-24，`backend-engineer`）

補齊 S0-8 當時明講「刻意不做」的部分（見上方「後台新聞（B2）寫入垂直切片」§「這一輪不做的部分」）：
標籤、核心價值標籤、多型關聯、置頂精選（維持既有）、瀏覽數統計、批次操作、公開端點篩選。

### 規劃書條文逐條對照（主站規劃書 §4.2 B2，行 1017–1020）

| 規劃書條文 | 狀態 |
|---|---|
| 文章 CRUD、分類（對應 7.1~7.8） | ✅ S0-8 已有（`article_categories` 8 個分類已由 `db/seed` 種子） |
| 標籤 | ✅ **本輪新增**（`tags`／`tags_i18n`／`article_tags`，find-or-create） |
| 核心價值標籤 | ✅ **本輪新增**（`value_tag_links`，`entity_type='article'`，值域五個） |
| 封面圖、摘要、內文區塊編輯 | ✅ S0-8／S0-8 修正已有 |
| 關聯（球員／球隊／賽事／課程／夥伴） | ✅ **本輪新增**（`article_relations`，五種 `target_type`，跨俱樂部隔離） |
| 排程發布 | ✅ S0-8／S0-7g 已有，本輪未動 |
| 置頂精選（限 3） | ✅ S0-8 已有（逐俱樂部計算，S0-7h 兩項假設維持不變，本輪未動） |
| 瀏覽數統計 | ✅ **本輪新增**（欄位 `articles.view_count` S0-8 時已存在但沒有寫入路徑；公開端點 `POST .../views` 遞增，後台 DTO 補回填） |
| 批次操作：改分類、批次發布／下架 | ✅ **本輪新增**（三支 `/batch/*` 端點；「下架」的實作方式是本輪的判斷，見下方） |

**規劃書沒寫，本輪沒自創的部分**：分類與標籤是否要有各自獨立的後台 CRUD 畫面（目前分類固定
8 個由種子建立，不開放新增／刪除；標籤走 find-or-create，沒有獨立的「標籤管理」端點）——規劃書
只寫「分類」「標籤」兩個詞，沒有講分類要不要能新增、標籤要不要有獨立管理畫面，`docs/03` 也只重複
規劃書的用詞。這是刻意的保守範圍：**新增分類＝規格變更**（7.1–7.8 是規劃書明文列出的固定分類），
**標籤沒有這個問題**（規劃書用詞本來就是開放式的「標籤」，find-or-create 是最貼近「隨手打幾個字
分類」這個使用情境的實作，不算自創功能）。

### 端點與權限碼

沿用既有五個 `content.article.*` 權限碼（`view`／`create`／`update`／`publish`／`delete`），
**沒有新增權限碼**——標籤／核心價值標籤／關聯是既有建立／更新端點 payload 多出來的欄位，批次
操作的三支端點分別掛 `update`（改分類）與 `publish`（發布／下架）：

- `POST /api/v1/admin/{club}/news/batch/category`（`content.article.update`）
- `POST /api/v1/admin/{club}/news/batch/publish`（`content.article.publish`）
- `POST /api/v1/admin/{club}/news/batch/unpublish`（`content.article.publish`）
- `POST /api/v1/{club}/news/{slug}/views`（公開，不需要登入，回 204／404）

`GET /api/v1/{club}/news`／`GET /api/v1/admin/{club}/news` 新增查詢參數：公開端點加 `tag=<slug>`，
後台清單端點的 `Tags` 是既有查詢附帶回傳（沒有新增查詢參數）。

### 三個欄位「省略＝維持不變、空陣列＝清空」——跟雙語內容欄位相反的語意

`UpdateArticleRequest.Tags`／`CoreValueTags`／`Relations` 省略（JSON 不帶這個欄位）＝維持資料庫
現況；明確傳空陣列 `[]` 才是清空。這跟同一個請求裡 `Content.En` 的「省略英文＝清空既有英文列」
剛好相反——**理由是這三個欄位目前沒有任何後台畫面**（跟 S0-8 時「不做」的理由一樣），若比照內容
欄位「省略＝清空」，任何只改標題不碰這三個欄位的既有存檔動作都會把既有標籤／關聯整批清光，
是比「這個功能還沒做」更糟的資料損毀。**建立時沒有這個問題**（沒有「原本的值」可以維持），
所以 `CreateArticleRequest` 的同名三個欄位省略＝空，跟 Update 不同，已在程式碼註解逐一說明。
已寫入 [`docs/14-invariants.md`](../../docs/14-invariants.md)。

### 瀏覽數：怎麼做到不影響公開讀取效能與快取

`ArticlesRepository.IncrementViewCountAsync`（`Features/News/`）是一支**獨立的公開端點**
（`POST /api/v1/{club}/news/{slug}/views`），不是掛在既有兩支 `GET` 的讀取路徑上「順便」累加：

1. **不碰 `IQueryCache`**——這支方法直接用 Dapper 送一句 `UPDATE articles SET view_count =
   view_count + 1 WHERE ...`，完全不經過 `GetOrCreateAsync`，讀取路徑一行都沒改，效能不受影響。
2. **寫入後不呼叫 `InvalidateAsync`**——瀏覽數不在 docs/17 §4「五類不得讀快取」之列，容忍最多一個
   TTL（預設 300 秒）的顯示落後是可接受的取捨；如果每次瀏覽都讓整篇文章詳情的快取失效，等於
   瀏覽數這個低重要性欄位拖垮標題／內文這些高重要性欄位的快取命中率。
3. **資料庫端遞增**（`view_count = view_count + 1`），不是「讀出來 +1 再寫回去」，高併發下不會
   更新遺失。
4. 回應固定 `204 No Content`，不回傳遞增後的數字——避免呼叫端誤以為這支端點的回應比畫面上
   （可能來自快取）顯示的數字更準確。
5. **沒有新增任何 log／統計表**（CLAUDE.md 全域規定、`docs/18` `E-44`）——`view_count` 本來就是
   `articles` 主表既有欄位（S0-8 當時已存在但沒有寫入路徑），這裡只是補上寫入路徑。
6. 只有 `status = 'published'` 且已到發布時間的文章才會被加到；找不到符合條件的文章（草稿、
   排程中、不存在）回 404，不洩漏「這個 slug 存在但還沒發布」，也不會建立任何列。

### 我的判斷（規劃書沒定義，本輪做了選擇，需要使用者／下一位確認）

1. **批次「下架」＝轉回 `draft`**：`articles.status` 的 CHECK 約束只有 `draft`／`published`／
   `scheduled` 三態，沒有獨立的「已下架」值（S0-8 時已回報的既有落差，見上方「已發現、未動手修改
   的既有落差」第 1 點）。新增值域是規格變更，本輪任務邊界不能改 `db/club-schema.sql`，因此把
   「下架」實作成轉回 `draft`——公開 API 只顯示 `status='published'`，轉回草稿在對外行為上就是
   「從公開站消失」，跟「下架」字面上要達成的效果一致；代價是「從沒發布過的草稿」跟「下架前
   發布過」現在共用同一個狀態值，只能靠 `published_at`（下架後不清空）分辨兩者。**這是需要業務
   判斷確認的假設，跟 `S0-7h` 那兩項同一種性質，只是發生的時間點更晚**——不是同一類錯誤所以不
   升級 `S0-7h`，是新的一筆待確認事項。
2. **批次操作不做逐筆並行權杖檢查**：批次操作的使用情境是「列表頁勾選多筆按一個按鈕」，呼叫端
   沒有（也不該要求畫面先為每一筆蒐集 `updated_at`）；找不到、跨俱樂部、共用內容唯讀、狀態不允許
   四種情況分別列進回應的 `Skipped` 清單並附中文原因，不是丟 409。**能處理的處理，不能處理的
   列出來，不是全有全無**——這跟單篇編輯（有畫面可以顯示個別 409 錯誤）在使用情境上不同。
3. **標籤格式沿用文章網址名稱同一套 kebab-case 正規表示式，但不共用保留字清單**
   （`Features/AdminNews/TagSlugFormat.cs`）：標籤沒有對應的路由頁面，不需要擋「跟 07 單元分類
   landing 頁撞名」，格式規則本身沿用同一套只是因為之後很可能被用在同一種查詢參數的位置
   （`?tag=`），維持 URL 安全與大小寫一致的形狀。
4. **`value_tag_links.entity_type` 與 `article_relations.target_type` 的字面值命名慣例**：
   小寫、單數、對應 `docs/12` 型別詞彙表的大寫型別名（`article`／`player`／`team`／`match`／
   `program`／`partner`）。規劃書沒有給任何字面值，這是本輪定的慣例，已寫入 `docs/14`，供之後
   其他模組接上同一套多型關聯表時比照。
5. **新標籤找不到既有列時，`NameZh` 必填**：標籤名稱要給人看，新建卻不給中文名稱沒有意義；
   找到既有標籤時**忽略**這次輸入的名稱（名稱由標籤自己的資料列管理，不因某一篇文章的輸入被
   悄悄覆寫，否則 A 文章存檔時會改掉 B 文章也在用的同一個標籤顯示名稱）。
6. **已知、接受的競態窗口**：兩個請求同時建立同一個新 slug 的標籤時，`UQ_tags_slug` 會讓其中一個
   `SaveChangesAsync` 失敗並以 500 呈現（未特別攔截轉換）——跟本檔其他「先查後寫」的重複檢查
   （文章 slug、分類代碼）採同一種風險容忍度（本專案目前沒有任何地方對這類競態做重試或攔截），
   不是本輪遺漏，是跟既有慣例一致的取捨。

### 契約變更（給 `apps/admin` 的人看）

**沒有破壞既有欄位或路徑**，全部是新增：

- `AdminArticleListItemDto`／`AdminArticleDetailDto` 新增 `tags`（陣列）、`viewCount`（int）；
  `AdminArticleDetailDto` 另外新增 `coreValueTags`（字串陣列）、`relations`（陣列）。
- `CreateArticleRequest`／`UpdateArticleRequest` 新增 `tags`／`coreValueTags`／`relations`
  三個**選填**欄位（不帶＝維持既有行為，見上方語意說明）。
- 公開 `ArticleListItemDto`／`ArticleDetailDto` 新增 `tags`；`ArticleDetailDto` 另外新增
  `coreValueTags`、`relations`。
- 新增三個端點（見上方「端點與權限碼」），不影響既有端點的路徑或回應形狀。

既有 `apps/admin/src/api/adminNews.ts`／`apps/admin/src/types/news.ts` 沒有引用這些新欄位，
`System.Text.Json` 反序列化多出來的欄位會被忽略，**不需要同步改前端就能繼續運作**；前端要開始
用這些欄位時，直接照上面型別加，不需要改既有欄位。

### 綱要缺口或待裁決（回報，不是自己判斷做或不做）

**沒有發現需要新欄位或新表的缺口**——`tags`／`tags_i18n`／`article_tags`／`value_tag_links`／
`article_relations` 五張表 `db/club-schema.sql` 早就有（S0-8 時已建好，只是沒接讀寫邏輯），
本輪全程沒有碰 DDL、沒有新 migration、沒有對 `tcrfc_club_dev` 做結構變更。上方「我的判斷」
第 1 點（批次下架＝轉回 `draft`）是唯一需要業務確認的事項，不是綱要缺口。

### 測試（`Tcrfc.Api.Tests`，新增 2 個檔案、18 項）

- `AdminNewsTagsRelationsTests.cs`（14 項）：標籤新建與沿用既有（不重複建立、不覆寫既有名稱）、
  新標籤缺中文名稱回 400、標籤格式不正確回 400、更新省略標籤維持不變／空陣列清空、核心價值標籤
  合法值存讀、不合法值回 400、關聯同俱樂部球隊成功、**關聯跨俱樂部球隊回 400（多型關聯的跨俱樂部
  隔離）且不留孤兒資料列**、關聯類型不支援回 400、關聯目標不存在回 400、批次改分類（含跨俱樂部
  略過，用 `super.admin@tcrfc.test` 建立真正的 `bw` 文章驗證）、批次改分類 ids 為空回 400、
  批次發布（只處理草稿／排程中）、批次下架（已發布轉草稿、草稿本身略過、公開 API 立刻查不到）。
- `NewsPublicFilterAndViewCountTests.cs`（4 項）：公開列表 `?tag=` 篩選只回傳掛了該標籤的文章、
  公開詳情頁回傳標籤／核心價值標籤／關聯、瀏覽數端點呼叫後真的遞增（用無快取的 `AdminWriteApiFixture`
  直接斷言）、瀏覽數端點對草稿或不存在的文章回 404 且不建立任何列。

**測試結果**：

```
dotnet test --filter "FullyQualifiedName~News|FullyQualifiedName~Articles" --no-build
# 已通過! - 失敗: 0，通過: 81，總計: 81（連跑 10 次，每次都是 0 失敗）

dotnet test（全套）
# 已通過! - 失敗: 0，通過: 254，總計: 254（既有 236 項 + 本輪新增 18 項）

dotnet ef migrations has-pending-model-changes --context ClubDbContext
# No changes have been made to the model since the last migration.
```

`ArchitectureTests` 包含在全套 254 項裡，一併通過。

### 本次沒動的部分

- 沒有新增／修改任何 DDL、`db/club-schema.sql`、`Data/Migrations/`、EF 模型（`Data/EfEntities/`、
  `ClubDbContext.cs` 一行都沒改）。
- 沒有新增任何權限碼、沒有動 `db/seed/generate-club-seed-sql.py`。
- 沒有修改 `apps/admin`（前端接線由另一位 agent 同時處理，本輪只加後端欄位，不預期任何衝突）。
- 沒有 commit。

---

## S1-6：`B3` 首頁編排／`B4` 常見問題（2026-09-24，`backend-engineer`）

### 規劃書條文逐條對照（主站規劃書 §4.2 B3／B4，行 999–1033；§3.1 首頁九大區塊，行 287–306；
§3.12 FAQ，行 569–585；§7 `GEO-06`，行 1678）

| 規劃書條文 | 狀態 |
|---|---|
| B3：Hero 輪播管理（排序、圖、標題、CTA、上架期間） | ✅ 圖片；**影片本輪未做**（見下方「本輪判斷」） |
| B3：首頁各區塊開關與排序、精選內容指定 | ✅ 九個固定區塊；**精選內容指定只有 Hero 有欄位可用**（見綱要缺口） |
| B4：主題分類管理（新增／排序／停用分類） | ✅ 新增／排序／刪除；**「停用」實作成刪除**（見下方「我的判斷」，`faq_categories` 沒有 is_enabled 欄位） |
| B4：題目 CRUD（問題、答案、所屬分類可複選、排序、狀態、雙語） | ✅ 全部欄位 |
| B4：嵌入設定（指定頁面或由分類自動對應） | ⚠️ **只做了「由分類自動對應」**（`?category=` 篩選）；「指定該題可出現於哪些頁面」沒有對應欄位，見綱要缺口 |
| B4：成效數據（瀏覽數、👍／👎、低評價題目清單） | ✅ 瀏覽數與回饋端點；低評價清單＝`sort=low_rating` |
| `GEO-06`（FAQ 問題完整句子、答案首句即結論） | 這是**內容規範**，不是 API 行為——API 只提供 `question`／`answer` 兩個自由文字欄位，撰寫規則由後台使用者與客戶版文案自律遵守，程式不驗證句子形狀 |

**規劃書沒寫，本輪沒自創的部分**：Hero 輪播的「影片」（規劃書寫「圖／影片」，`banners` 表只有
`image_key` 一欄，沒有影片欄位）；「精選內容指定」對 Hero 以外八個區塊的具體欄位（規劃書只給
一句話，沒有逐區塊定義要指定什麼、`home_sections` 也只有 `featured_banner_id` 一欄可用）；
FAQ 的「指定頁面」嵌入（`faq_category_links` 只能表達「題目屬於哪個分類」，沒有「題目可出現在
哪個頁面路徑」這件事的資料結構）。三者皆已列在下方「綱要缺口」，不是遺漏。

### 端點與權限碼

後台（一律經 `IAdminClubAuthorizer`，除 `faq-categories` 全域端點經 `IAdminSystemAuthorizer`）：

| 方法與路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET/POST /api/v1/admin/{club}/banners`、`GET/PUT/DELETE .../banners/{id}` | `content.banner.view`／`create`／`update`／`delete` | Hero 輪播 CRUD，建立／更新為 `multipart/form-data`（`payload` ＋ `file`，建立必填、更新省略＝維持原圖） |
| `GET /api/v1/admin/{club}/home-sections` | `content.home_section.view` | 九個固定區塊列表 |
| `PUT /api/v1/admin/{club}/home-sections/{sectionCode}` | `content.home_section.update` | 更新單一區塊的開關／排序／（僅 `hero`）精選輪播 |
| `GET/POST /api/v1/admin/{club}/faqs`、`GET/PUT/DELETE .../faqs/{id}` | `content.faq.view`／`create`／`update`／`delete` | 常見問題 CRUD，純 JSON（無圖片欄位） |
| `GET/POST /api/v1/admin/faq-categories`、`GET/PUT/DELETE .../faq-categories/{id}` | `content.faq_category.view`／`create`／`update`／`delete` | 主題分類 CRUD，全域端點（`faq_categories` 無 `club_id`） |

公開（無需登入）：

| 方法與路徑 | 說明 |
|---|---|
| `GET /api/v1/{club}/banners` | 只回目前在上架期間內的輪播 |
| `GET /api/v1/{club}/home-sections` | 九個區塊（含未啟用的，前台自行判斷 `isEnabled`） |
| `GET /api/v1/faq-categories` | 全域分類清單（不分俱樂部） |
| `GET /api/v1/{club}/faqs?category=&keyword=&lang=&page=&pageSize=` | 只回 `published`，`club_id` 可為空套用「俱樂部專屬優先、回退共同」 |
| `GET /api/v1/{club}/faqs/{slug}` | 單題詳情 |
| `POST /api/v1/{club}/faqs/{slug}/views` | 瀏覽數＋1，逐字比照 `Features/News` 既有寫法 |
| `POST /api/v1/{club}/faqs/{slug}/feedback`（body `{helpful}`） | 👍／👎 遞增 |
| `POST /api/v1/{club}/faqs/search-misses`（body `{keyword}`） | 零結果搜尋回報，見下方「我的判斷」 |
| `GET /api/v1/admin/{club}/faqs/search-misses?days=&top=`（2026-09-24 補，`E-51`） | 搜尋無結果關鍵字排行，`content.faq.view`；見下方「我的判斷」第 5 點 |

權限碼種子（`db/seed/generate-club-seed-sql.py` §18.2／§18.3，DML，未動 DDL）：
`content.banner.*`／`content.home_section.*`（`module_code=B`、`submodule_code=B3`）、
`content.faq.*`／`content.faq_category.*`（`submodule_code=B4`）。角色指派**分兩種**：
- **B3 沿用規劃書 §6「內容」欄既有的四個角色足跡**（跟 `content.article.*`／`content.page.*`
  一致）：`system_admin` 全給；`content_editor` 給 view/create/update/delete（banner）與
  view/update（home_section）；`viewer` 唯讀；`partner_club_manager`（`own_clubs`）給
  view/create/update（無 delete）。
- **B4 走規劃書 §6 獨立的「FAQ」欄**（分佈跟「內容」欄不同）：`content_editor` 全給（含分類）；
  `pr_media` 唯讀（矩陣「唯讀」）；`customer_service_admin` 給題目 CRUD ＋分類唯讀（矩陣
  「✔編輯」，本輪判斷不含分類管理，見下方「我的判斷」第 4 點）；`viewer` 唯讀；
  `partner_club_manager`（`own_clubs`）給題目 view/create/update（無 delete）＋分類唯讀；
  `team_competition`／`academy_program`／`business_sponsorship`（矩陣「相關題目」）**本輪未指派**
  ——這需要依 FAQ 分類做列級限定，本輪沒有建立這種限定機制；`translator`（矩陣「僅翻譯欄位」）
  同樣需要欄位層限制，本輪未指派。這是延續 `content.article.*` 既有的範圍縮減模式，不是新遺漏。

### 資料庫綱要：沒有新增任何表或欄位，全部沿用既有 DDL

`banners`／`banners_i18n`／`home_sections`／`faqs`／`faqs_i18n`／`faq_categories`／
`faq_categories_i18n`／`faq_category_links`／`faq_search_misses` 八張表**在本輪開始前就已經存在**
於 `db/club-schema.sql`（含 EF 實體與 `ClubDbContext` 映射，見 `Data/EfEntities/*.cs`）——本輪全程
沒有碰 DDL、沒有新 migration、沒有對 `tcrfc_club_dev` 做結構變更（`dotnet ef migrations
has-pending-model-changes` 全程回報無待處理變更）。**種子資料新增了業務資料（DML，不是結構）**：
`faq_categories` 十個固定主題（規劃書 3.12）、`home_sections` 九個固定區塊 × 兩俱樂部
（`db/seed/generate-club-seed-sql.py` §19／§20）。

### 沒有樂觀並行控制——本輪的判斷，跟 `Article`／`Page` 不同

`Banner`／`HomeSection`／`Faq`／`FaqCategory` 四個型別的 Update 都**不要求呼叫端帶
`expectedUpdatedAt`**，比照 `Features/AdminCompetitions/AdminCompetitionsRepository.cs`（同樣沒有
樂觀並行控制的既有先例），而不是比照 `Features/AdminNews`／`AdminPages`（用 `updated_at` 當並行
權杖）。理由：這四個型別是單一表單／少欄位的設定型內容，多人同時編輯衝突的風險與 `Competition`
同一等級，不是新聞或頁面那種多段落長文的協作場景。需要時可依 `Data/ClubDbContextCustomizations.cs`
既有寫法（`modelBuilder.Entity<T>().Property(x => x.UpdatedAt).IsConcurrencyToken()`）補上，
不是型別上做不到，是本輪的取捨。

### 我的判斷（規劃書沒定義，本輪做了選擇，需要使用者／下一位確認）

1. **FAQ 分類「停用」＝刪除（硬刪）**：`faq_categories` 沒有任何啟用／停用欄位（`docs/12b`／
   `db/club-schema.sql` 皆無），刪除分類會透過 `faq_category_links` 的 `ON DELETE CASCADE`
   自動解除關聯，但**不會刪除題目本身**——一題失去所有分類後仍存在、仍可被關鍵字搜尋到，
   只是無法再透過分類導覽找到。這是本輪判斷「刪除」在對外行為上最接近規劃書「停用」字面
   效果的做法，代價是**不可逆**（沒有「重新啟用」，要恢復只能重新建立一個同樣內容的分類）。
2. **FAQ 建立／更新要求至少 1 個所屬分類**：規劃書寫「所屬分類（可複選）」沒有規定下限，
   本輪判斷 0 個分類等於「這題在前台主題導覽完全找不到」，比「這個功能還沒做」更容易被誤判成
   bug，故要求至少 1 個。
3. **FAQ 狀態是 Update 請求裡的平面欄位，不是獨立的發布／排程端點**：`faqs.status` 只接受
   `draft`／`published`（沒有 `published_at` 欄位，不支援排程，docs/14「S0-7g」），比照
   `Features/AdminCompetitions` 的既有寫法，不是比照 `Article`／`Page` 那種有版本歷程與狀態轉換
   權限的獨立生命週期。
4. **`customer_service_admin` 給題目 CRUD，但分類唯讀**：規劃書矩陣 FAQ 欄對這個角色只寫
   「✔編輯」一格，沒有區分題目與分類。本輪判斷客服人員的日常工作是新增／修改常見問題內容，
   不包含重新設計十個主題分類這種較結構性的異動，因此分類管理只留給 `content_editor`／
   `system_admin`。
5. **零結果搜尋回報（`POST .../faqs/search-misses`）是本輪新增的獨立端點，不是規劃書明文要求**：
   `faq_search_misses` 表在 DDL 裡本來就存在，註解寫明「零結果搜尋關鍵字與次數（成效統計）」，
   跟 S1-5 補齊 `articles.view_count` 寫入路徑同一種性質（既有欄位／表只是還沒接讀寫邏輯）。
   本輪判斷**不要**把這個寫入嵌進 `GET .../faqs?keyword=` 的讀取路徑裡（那是快取讀取路徑，
   嵌寫入會讓同一個 entity 同時身兼讀與寫，且會把「使用者還在打字時的暫時 0 筆」也計入），
   改成一支獨立端點，由前端在真正呈現「找不到結果」畫面給使用者看到的那一刻才呼叫。
   🔴 **2026-09-24 補（`docs/18-work-errors.md` `E-51`）：本輪原本只做了寫入端，後台完全讀不到
   這份排行，直到 `S1-8` 才被發現。現已補上 `GET /api/v1/admin/{club}/faqs/search-misses`
   （`content.faq.view`），沿用同一份 `faq_search_misses` 表，不需要新表或新欄位：**
   - **綱要確認**：`faq_search_misses` 是 `(club_id, keyword)` 彙總列（表定義註解「成效統計，
     不是搜尋日誌」），`hit_count` 逐次 `+1`、`last_searched_at` 每次覆寫成最新時間，
     不是逐次搜尋各存一列——`count`／`lastSearchedAt` 兩個輸出欄位因此都直接對得到欄位，
     但 `days` 篩選只能決定「這個關鍵字最近有沒有人搜尋過」，**不能**把 `count` 收斂成
     「範圍內的次數」（沒有逐次列可以重新加總）。見
     `Features/AdminFaqs/AdminFaqsRepository.ListSearchMissesAsync` 上的完整說明。
   - **正規化補強**：寫入端 `POST .../faqs/search-misses` 原本只做 `Trim()`，同一個關鍵字打大寫、
     全形輸入法會被拆成好幾筆不同的彙總列，排行因此失真。已新增
     `Common.SearchKeywordNormalizer`（全形轉半形、去前後空白、大小寫統一），寫入前先正規化再當
     upsert 鍵；正規化只能在寫入前做，讀取端事後補救不了（資料庫裡已拆散的列無法重新合併）。
   - **契約**：`days` 預設 30（1–365）、`top` 預設 50（1–200），超出範圍回 400
     （`AdminFaqValidationException`）；排序依 `count` 由多到少、同數依 `lastSearchedAt` 新到舊。
6. **Banner 只做圖片，不做影片**：規劃書 B3 寫「圖／影片」，但 `banners` 表只有 `image_key`
   一個媒體欄位，沒有影片欄位或影片供應商／ID 這類欄位（跟 B1 頁面的 `video_embed` 區塊
   不同，那個區塊本來就有 `provider`／`videoId` 欄位）。本輪只實作圖片，影片是綱要缺口。

### 綱要缺口或待裁決（回報，不是自己判斷做或不做）

1. 🔴 **`banners` 只有 `image_key` 一個欄位，缺少 `_width`／`_height`／`_alt_zh`／`_alt_en`**——
   docs/14-invariants.md 的圖片欄位組通則要求每個圖片欄位有這四欄（`<名稱>_width`／`_height`
   供前台輸出 `<img width height>` 避免版面跳動、`_alt_zh`／`_alt_en` 供無障礙與 SEO）。
   `articles.cover_key`／`teams.hero_key` 等其餘既有圖片欄位也是同樣的缺口（S0-8／S1-7 已各自
   遇過，不是本模組獨有），但 `banners` 是**首頁最顯眼的視覺元素**，缺 alt 文字對 WCAG G-08
   （無障礙）與 GEO 的影響相對更直接。本輪的因應：寬高完全不回傳（前台需要用 CSS
   `aspect-ratio` 或其他技巧預留版面，無法用伺服器提供的寬高）；alt 文字沒有任何欄位可用，
   完全沒有實作（既不能沿用標題頂替，因為很多輪播圖是純視覺無文字）。**需要決定**：要不要
   替 `banners` 補這四欄（規格異動，先改 `docs/12` 再走 migration）。
2. 🔴 **`home_sections.featured_banner_id` 是唯一一欄承載「精選內容指定」，只有 `hero` 用得到**：
   規劃書 B3 說「精選內容指定」，但 `home_sections` 沒有給其餘八個區塊（核心價值、體系導覽卡、
   最新賽事、近期賽事、最新消息、夥伴 Logo 牆、商店入口、底部 CTA）任何「指定要精選哪些內容」
   的欄位。以「最新消息」為例，目前的實際行為完全依賴 `articles.is_featured`（S0-8 已有，
   B2 新聞自己的置頂精選機制），不是由 B3 這裡指定——這在功能上「湊合可用」，但規劃書字面上
   「首頁編排可以指定精選內容」跟「每個內容型別各自有自己的置頂欄位」是兩件不同的事，
   後者是各模組自己的功能被首頁借用顯示，不是首頁編排本身的一個可設定項目。**需要決定**：
   要不要讓 `home_sections` 對每個區塊都有辦法指定要精選哪些內容 id（例如一個 JSON 欄位存
   一組 id 清單），或維持現狀（各區塊各自的精選機制，首頁編排只管開關與排序）。
3. 🔴 **FAQ「嵌入設定：指定該題可出現於哪些頁面」完全沒有實作**：規劃書給了「或由分類自動
   對應」這條替代路徑，本輪選擇只做這一種（`?category=` 篩選），因為「指定頁面」需要一張新的
   關聯表（例如 `faq_page_links` 記錄 `faq_id` ＋ 某種頁面識別鍵），而「頁面識別鍵」要指向
   什麼（B1 `pages.id`？還是像 `SlugPolicy` 那種寫死的路由片段清單？）本身就是需要先決定的
   設計問題，任務指示「需要新表就停下回報，不自己加」，故完全不做，不是做一半。
4. ⚠️ **`faqs.status` 沒有考慮排程，但 CHECK 約束仍允許寫入 `'scheduled'`**（docs/14「S0-7g」
   既有落差的其中一張表，非本輪新增）：`AdminFaqsRepository.ValidateStatus` 在應用層擋下
   `'scheduled'`，資料庫層沒有任何約束會擋（`faqs.status` 的 CHECK 是 `draft`／`published`／
   `scheduled` 三選一，跟 `competitions.status` 同一種既有落差）。

### 測試（`Tcrfc.Api.Tests`，新增 3 個檔案、40 項）

| 檔案 | 涵蓋 |
|---|---|
| `AdminBannersAndHomeSectionsTests.cs`（Azurite） | 401／403（未登入、檢視者唯讀、跨俱樂部無授權）、輪播建立必須帶圖片、上架早於下架時間 400、完整生命週期（含換圖刪舊物件、刪除連帶刪圖）、首頁區塊列表固定 9 筆、更新開關與排序、非 Hero 區塊指定精選輪播 400、Hero 指定不存在的輪播 400、不存在的區塊代碼 404、公開端點只回上架期間內的輪播、公開端點回傳全部 9 筆區塊（含停用） |
| `AdminFaqsAndCategoriesTests.cs` | 401／403（未登入、檢視者唯讀、跨俱樂部無授權）、分類建立更新刪除與 slug 重複 409、題目建立未選分類 400、分類不存在 400、完整生命週期（含分類調整、狀態切換）、slug 重複 409、排程狀態被拒 400、**共用內容唯讀 403**（直接寫 SQL 造一筆 `club_id IS NULL` 的題目）、低評價排序、**搜尋無結果關鍵字排行**（2026-09-24 補，`E-51`：401、無 `content.faq.view` 權限 403、跨俱樂部 403／授權範圍內 200、依 count 由多到少同數依 lastSearchedAt 新到舊排序、`days` 篩選不影響 `count` 全站累計值、`top` 限制筆數、`days`／`top` 邊界值與超出範圍 400、省略時採預設值） |
| `PublicFaqsTests.cs` | 分類公開列表雙語、題目公開列表只回已發布且**回退共用內容**、依分類篩選、單題查詢跨俱樂部查不到、瀏覽數與回饋端點遞增、對不存在的題目回 404、**零結果搜尋回報建立與累加**、空白關鍵字不寫入、**大小寫／前後空白／全半形正規化後合併計數**（2026-09-24 補，`E-51`） |

**測試結果**：

```
dotnet test --filter "FullyQualifiedName~AdminBannersAndHomeSectionsTests|FullyQualifiedName~AdminFaqsAndCategoriesTests|FullyQualifiedName~PublicFaqsTests" --no-build
# 已通過! - 失敗: 0，通過: 31，總計: 31（連跑 10 次，每次都是 0 失敗）

dotnet test（全套，先跑 db/seed/reset-admin-accounts.sh 還原被另一個 agent 的端對端驗收弄髒的
sa@system.local／clean.login@tcrfc.test 狀態，見下方「發現但非本輪造成」）
# 已通過! - 失敗: 0，通過: 300，總計: 300

dotnet ef migrations has-pending-model-changes --context ClubDbContext
# No changes have been made to the model since the last migration.
```

`ArchitectureTests` 包含在全套 300 項裡，一併通過（本輪沒有新增 `ClubScope`／`AdminClubScope`
型別，不影響那支掃描）。

**發現但非本輪造成**：全套測試第一次執行時 `AdminAuthTests` 有 4 項失敗（登入／2FA／更新權杖
相關），原因是 `sa@system.local`／`clean.login@tcrfc.test` 的密碼與 2FA 狀態已偏離種子初始值
——README「種子測試帳號的重設」一節記錄的既有現象（另一個 agent 對 `apps/admin` 做端對端驗收時
真的登入過這兩個帳號）。執行 `./db/seed/reset-admin-accounts.sh` 後這 4 項全部轉綠，不是本輪
程式碼造成的問題，記錄在此供下一位核對。

### S1-6 續作：批次操作與 CSV 匯入／匯出（2026-09-24，coordinator 補派）

補齊派工時漏掉的一條規劃書明文——主站規劃書 **行 1033**：「批次操作：批次改分類、批次顯示／隱藏、
匯入／匯出 CSV」。

**批次改分類、批次顯示／隱藏**：逐字比照 `Features/AdminNews` 既有的三支批次端點（不做逐筆並行
權杖檢查、能處理的處理不能處理的列進 `Skipped`，理由與寫法一致，見
`AdminFaqsRepository.BatchChangeCategoryAsync`／`BatchSetVisibilityAsync`）。

| 方法與路徑 | 權限碼 | 說明 |
|---|---|---|
| `POST /api/v1/admin/{club}/faqs/batch/category` | `content.faq.update` | 批次改分類（整批取代成同一組分類，不是新增或移除） |
| `POST /api/v1/admin/{club}/faqs/batch/show` | `content.faq.update` | 批次顯示（`status = 'published'`） |
| `POST /api/v1/admin/{club}/faqs/batch/hide` | `content.faq.update` | 批次隱藏（`status = 'draft'`） |

🔴 **權限碼跟 News 不同的地方**：News 的批次發布／下架掛 `content.article.publish`（獨立的發布
權限碼），FAQ 沒有對應的 `content.faq.publish`——因為本模組從第一輪開始就把「顯示／隱藏」設計成
Update 請求裡的一個平面欄位，不是像新聞那樣有獨立生命週期與發布權限（見 S1-6 一開始「我的判斷」
第 3 點）。「批次操作的權限碼要對應單筆的同一個動作」這條規則本身沒有變，只是 FAQ 這裡單筆的
「顯示／隱藏」原本就對應到 `update`，批次版本自然也對應到 `update`，不是新增一個權限碼。

**CSV 匯入／匯出**：`docs/04-data-model.md` §5、`docs/12b-database-tables.md` §10.1 只確認
「FAQ 題目要支援 CSV 匯入＋匯出」這件事本身（落到 `Faq`＋`FaqCategoryLink`＋`faqs_i18n`），
兩份文件都**沒有逐欄定義格式**——依任務指示「沒定義就採最小可行」，本輪新增以下格式：

| 方法與路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET /api/v1/admin/{club}/faqs/export` | `content.faq.view` | 下載這個俱樂部**自己的**常見問題 CSV（不含共用內容，理由見下方） |
| `POST /api/v1/admin/{club}/faqs/import` | `content.faq.update` | 上傳 CSV 原始位元組（非 multipart，Content-Type 不拘，一律以 UTF-8 解碼） |

**CSV 格式**（`Features/AdminFaqs/AdminFaqsRepository.CsvHeader`）：

- **編碼**：UTF-8 **含 BOM**（`CsvUtils.ToUtf8BytesWithBom`）——Excel 在部分作業系統開啟不含 BOM
  的 UTF-8 CSV 會誤判編碼，中文欄位變亂碼，這是任務指示「沒定義就採最小可行」明講的選項。
- **表頭**（逐字輸出，日常中文）：`網址名稱,所屬分類,狀態,排序,中文問題,中文答案,英文問題,英文答案`。
  🔴 **不是** `slug,category,status,...`——docs/14-invariants.md「CSV 匯出的欄位標題同此規則」
  明文把 CSV 表頭納入「介面上不得出現英文技術詞」的範圍。
- **所屬分類**：多個分類用全形頓號「、」相接（例如 `加入球隊、試訓`），值是分類的**中文名稱**
  不是 slug 或 GUID——同樣是那條規則的延伸，讓後台人員在 Excel 裡看得懂內容，不需要另外查
  「slug 對照表」。用頓號而不是逗號，也是為了不必把這一欄整個包進雙引號。
- **狀態**：`顯示`／`隱藏`（規劃書 B4 原文字面用詞），不是 `published`／`draft`。
- **雙語**：`中文問題`／`中文答案`必填；`英文問題`／`英文答案`可留空＝這題沒有英文版。

**Upsert 語意**：匯入以 `(club_id, slug)` 為對應鍵（對照 `docs/04-data-model.md` 第 130 行
「`id` / `slug` … 客戶素材匯入時作為對應鍵」）——網址名稱在這個俱樂部已存在就整份取代該題內容
（含分類清單），不存在就新增一題。**永遠不會比對到共用內容**（查詢固定帶
`club_id = scope.ClubId`），匯出範圍也因此限縮成「這個俱樂部自己建立的題目」，不含共用內容——
避免匯出共用題目、原地改過再匯入，卻在這個俱樂部底下複製出一筆新的俱樂部專屬列（而不是真的
改到共用那一列）這種混淆。

🔴 **整批驗證，任一列有錯就整批不寫入**（任務指示明文，跟上面「批次操作」刻意是不同的容錯策略
——批次操作是「使用者在畫面上勾選已經看得到的既有資料」，可以部分成功；CSV 匯入是「使用者上傳
一份可能整份都打錯格式的外部檔案」，全有全無比較不會讓使用者誤以為半成品資料已經生效）：
`AdminFaqsRepository.ImportCsvAsync` 分兩輪——第一輪只做純驗證，完全不觸碰 `dbContext` 的任何
寫入方法；只要有一列不合格就直接回傳完整的 `FaqCsvImportRowErrorDto` 清單（HTTP 400），沒有任何
一列被寫入。全部合格才進第二輪真正讀寫並呼叫一次 `SaveChangesAsync`（HTTP 200）。
`RowNumber` 是 CSV 檔案的**實體行號**（表頭算第 1 行，第一筆資料是第 2 行），方便使用者在
文字編輯器或試算表軟體對照原始檔案。**不寫入任何 log 表**（CLAUDE.md 全域規定、docs/18 `E-44`）
——匯入本身沒有稽核需求。

**新增檔案**：`Common/CsvUtils.cs`（最小可行的 RFC 4180 風格 CSV 編碼／解析共用工具，放在
`Common/` 是因為 `docs/12b` §10.1 另外列了整季賽程／積分榜／301 轉址／物流單號四項也要支援
CSV，屆時應該重用這裡而不是各自另刻一份剖析器，見該檔案檔頭說明）。

**測試**：新增 `CsvUtilsTests.cs`（7 項，純函式單元測試）＋ `AdminFaqsAndCategoriesTests.cs`
追加 9 項（批次操作 401／403、批次改分類與顯示隱藏含 `Skipped`、共用內容與跨俱樂部列進
`Skipped`、CSV 授權、CSV 匯出格式與 BOM、CSV 匯入成功含 upsert、**CSV 任一列錯誤整批不寫入**、
**CSV 檔案內網址名稱重複**、CSV 表頭不正確）。

```
dotnet test --filter "FullyQualifiedName~AdminBannersAndHomeSectionsTests|FullyQualifiedName~AdminFaqsAndCategoriesTests|FullyQualifiedName~PublicFaqsTests|FullyQualifiedName~CsvUtilsTests" --no-build
# 已通過! - 失敗: 0，通過: 47，總計: 47（連跑 10 次，每次都是 0 失敗）

dotnet test --filter "FullyQualifiedName!~AdminAuthTests"（排除已知與本模組無關、由 apps/admin
端對端驗收造成的種子帳號狀態污染，見上方「發現但非本輪造成」）
# 已通過! - 失敗: 0，通過: 310，總計: 310

dotnet ef migrations has-pending-model-changes --context ClubDbContext
# No changes have been made to the model since the last migration.
```

**權限碼種子**：本次續作**沒有新增任何權限碼**——批次操作與 CSV 匯入／匯出全部沿用既有的
`content.faq.view`／`content.faq.update`，`db/seed/generate-club-seed-sql.py` 未再修改。

**綱要缺口**：無新增。CSV 格式本身是本輪依「最小可行」原則新增的執行層決定，不是資料庫綱要，
若要正式寫進 `docs/12`（例如統一給其餘四項 CSV 匯入項目的欄位格式規範），留給 system-analyst
另外走同步鏈評估。

### 本次沒動的部分

- 沒有新增／修改任何 DDL、`db/club-schema.sql`、`Data/Migrations/`、EF 模型本體（`Data/EfEntities/`
  裡的檔案是既有的，一個屬性都沒改；`ClubDbContext.cs` 一行都沒改）。
- 沒有修改 `Data/ClubDbContextCustomizations.cs`（本輪判斷不需要樂觀並行控制，見上方說明）。
- 沒有修改 `apps/admin`／`apps/web`（前端接線由其他 agent 同時處理）。
- 沒有修改 `docs/14-invariants.md`／`docs/18-work-errors.md`／`STATUS.md`（依任務指示由派工者收尾）。
- 續作（批次操作／CSV）同樣沒有新增權限碼、沒有動 DDL、沒有修改 `apps/admin`。
- 沒有 commit。

---

## S1-7：`C1–C3` 球隊／球員／教練的後台 API 與前台公開唯讀（2026-09-24，`backend-engineer`）

### 讀到的規劃書條文

| 章節 | 行號 | 內容 |
|---|---|---|
| 主站 §4.3 C1 球隊 | 1053–1062 | 球隊欄位（含 `BW1`、`gender`）、`type=first_team` 每俱樂部至多一筆、`code` 全站唯一、資料範圍 |
| 主站 §4.3 C2 球員 | 1063–1069 | 基本資料、生涯資料、賽季數據、狀態（現役／離隊／外借／海外發展） |
| 主站 §4.3 C3 教練與團隊成員 | 1070–1072 | 教練（證照、負責梯隊）、團隊成員分組（管理層／行政／醫療／後勤） |
| 主站 §5.1 型別總表 | 1477、1479 | `Team`／`Staff` 的欄位與關聯 |
| 主站 §5.4 `club_id` 判定準則 | 1562、1564 | `Team`／`Player` 必填、`Staff` 可為空（兩隊共同） |
| docs/12b §6.1 | 13–22 | `Team.code` 全站唯一不得改複合鍵、`type`／`gender` 值域 |
| docs/12b §4.2 | 390–406 | `staff` 可為空＝兩隊共同、`StaffTeam` 關聯 |
| docs/12b §8 | 315–333 | 受限與加密欄位盤點——**`players`／`staff`／`teams` 不在清單內** |
| docs/12b §7.4 | 216 | 學院／課程管理對球隊的權限是 `scope_type = academy_only` |
| 藍鯨規劃書 §6 | 311 | `Team` 的 `BW1`（`gender=women`、`type=first_team`）示例 |
| STATUS.md `S0-3c` | — | 「顧問」職稱歸入「管理層」分組，已拍板的執行層決定 |

### 端點與權限碼

三組俱樂部範圍 CRUD，形狀逐字比照 `Features/AdminCompetitions`（權限碼命名）與 `Features/AdminNews`
（multipart 圖片上傳契約），module_code=C、domain=`team`（跟既有 `team.competition.*` 同一個
domain，方便權限查詢整組 `domain='team'` 一次撈）：

| 模組 | 路由 | 權限碼 | 圖片欄位插槽 |
|---|---|---|---|
| C1 球隊 | `GET/POST /api/v1/admin/{club}/teams`、`GET/PUT /api/v1/admin/{club}/teams/{id}` | `team.team.view`／`.create`／`.update` | `teams.hero`（`hero_key`） |
| C2 球員 | `GET/POST /api/v1/admin/{club}/players`、`GET/PUT /api/v1/admin/{club}/players/{id}` | `team.player.view`／`.create`／`.update` | `players.photo`（`photo_key`） |
| C3 教練與團隊成員 | `GET/POST /api/v1/admin/{club}/staff`、`GET/PUT /api/v1/admin/{club}/staff/{id}` | `team.staff.view`／`.create`／`.update` | `staff.photo`（`photo_key`） |

**沒有 DELETE**——逐字比照既有 `Features/AdminCompetitions`（同樣沒有刪除端點）的判斷：規劃書
C2／C3 明文用「狀態」表達球員離隊（現役／離隊／外借／海外發展），不是刪除列；球隊與教練都被
賽事明細表（`match_lineups`／`player_season_stats`／`staff_teams`……）外鍵參照，貿然開放刪除會
製造孤兒列或需要另外設計級聯規則，規劃書沒有要求，本輪不做。**這是我的判斷，不是規劃書明文**，
需要刪除功能時再另外評估。

**角色授予**（依主站規劃書 §6 矩陣「球隊／賽事」欄，`db/seed/generate-club-seed-sql.py` 已更新）：

| 角色 | 權限 |
|---|---|
| 系統管理員 | ✔ 全（`[p[0] for p in PERMISSIONS]` 自動涵蓋） |
| 競技／球隊管理（`team_competition`） | ✔ 全（`view`／`create`／`update` 三碼皆給） |
| 內容編輯／商務／贊助／公關／媒體／檢視者 | 唯讀（只給 `.view`） |
| 合作球隊管理（`partner_club_manager`） | ✔ 全，`scope_type=own_clubs`（僅自家俱樂部，靠 `AdminClubAuthorizer` 既有機制強制） |
| 客服／行政 | 不給（矩陣該欄是「—」） |
| 學院／課程管理、翻譯人員 | **本輪刻意不給，見下方「綱要缺口或待裁決」** |

新增測試帳號 `team.manager@tcrfc.test`（`team_competition` 角色，僅授權 `tcrfc`），沿用
`content.editor@tcrfc.test` 的密碼雜湊。

### 受限欄位處理

- **`players`／`staff`／`teams` 三張表都不在 docs/12b §8 受限欄位清單內**——球員名冊（含生日、
  慣用腳）、教練證照是球隊官網例行公開的競技資訊，不是一般會員個資。這是既有的、S0-7b 就已經
  做出的決定（見既有 `Features/Players/PlayerDto.cs` 檔頭），本輪沒有改動，只是延續：後台
  `AdminPlayerDetailDto`／`AdminStaffDetailDto` 沒有對任何欄位遮罩，公開端點的欄位集合也維持
  S0-7b 定的樣子不變。
- **`staff.club_id IS NULL`（兩隊共同）→ 俱樂部範圍端點一律唯讀**，逐字比照
  `Features/AdminNews/AdminArticlesRepository` 對 `articles`（同屬 9 張可為空表）的既有處理：
  建立永遠把 `club_id` 填成路由當下的俱樂部（不接受建立共同資料），更新命中 `club_id IS NULL`
  的既有列一律丟 `SharedStaffReadOnlyException`（403）——**沒有超管特例**，這個俱樂部範圍端點
  目前完全不提供編輯共同內容的路徑（跟 `articles` 目前的狀態一致，不是我在這輪臨時決定收緊）。
  種子資料目前沒有任何 `staff.club_id IS NULL` 的列，測試用直接寫資料庫的方式自己造一筆
  （`AdminTeamsPlayersStaffTests.InsertSharedStaffAsync`）來驗證這條路徑。
- **跨俱樂部球隊指派一律擋下**：C2 的 `TeamId`、C3 的 `Teams[].TeamId` 都必須屬於路由當下的
  俱樂部，否則 400（不是 404——這是輸入錯誤不是資源不存在）。

### 🔴 未實作：球員肖像同意狀態（任務指示要求的欄位，綱要沒有這個欄位）

派工單要求「肖像同意狀態若綱要有欄位，未同意者公開端點不得輸出照片」。**查證結果：
`db/club-schema.sql` 的 `players` 表沒有任何肖像同意相關欄位**（`shirt_no`／`position`／
`birth_on`／`height_cm`／`weight_kg`／`nationality`／`preferred_foot`／`joined_on`／`status`／
`photo_key`，沒有 `portrait_consent`、`consent_status` 或類似欄位；`docs/12b`／`docs/12d`／
ERD 全文檢索也查無此欄位曾被規劃過）。主站規劃書本文同樣沒有在 C2 球員一節列出肖像同意欄位——
未成年素材的肖像同意是**流程與蒐集規範**（見規劃書 §7 GEO-02、§8 非功能性需求「未成年學員資料
須取得監護人同意」），目前的資料模型設計是「同意與否是照片上傳前的線下把關，資料庫不記錄同意
狀態本身」。**這是綱要缺口，不是我可以自行判斷做或不做的事**：加欄位需要先改規劃書與
`docs/12`／`db/club-schema.sql`，任務指示明文「需要新欄位或新表就停下回報」，故本輪**沒有**
新增欄位、沒有實作「未同意不輸出照片」這條規則，`photo_key` 對所有球員一律公開輸出（跟
S0-7b 既有行為一致）。回報給下一輪決定：①要不要真的加欄位 ②在欄位補上之前，未成年球員照片
的公開與否要不要先靠人工流程（不上傳未同意者的照片，而不是靠系統擋）。

### 🔴 學院／課程管理（`academy_program`）本輪刻意不給 C1–C3 權限

矩陣寫「學院梯隊」（`scope_type=academy_only`——只能碰 `team.type='academy'` 的球隊與其球員／
教練），但**這個角色的列級範圍過濾本輪沒有做**（依 `team.type` 或 `AdminUserTeam` 篩資料列）。
任務指示明確要求「本輪球員／教練的寫入若規劃書要求依球隊授權限制，先回報再決定，不要自己擴大
範圍」——在列級強制做出來之前先發這三組權限碼給 `academy_program`，效果等同給它跟
`team_competition` 一樣的全俱樂部球隊存取權（含一線隊），超出矩陣「僅學院梯隊」的授權意圖，
是擴大範圍不是保守預設，因此本輪不發。`role_permissions.scope_type='own_teams'`（行事曆）與
本項（`academy_only`）性質相同，`STATUS.md` 已把前者排在 `S1-8`；**本項的列級強制建議與 `S1-8`
一併處理或另開一項**，屆時把 `academy_program` 的三組權限碼一起補上。

### 改了哪些檔案

**後端**（`apps/api/`）：
- `Features/AdminTeams/`：`AdminTeamDtos.cs`（新增 C1 的 List／Detail／Create／Update DTO）、
  `AdminTeamExceptions.cs`（新檔）、`AdminTeamRequestForm.cs`（新檔）、
  `AdminTeamsRepository.cs`（新增 `ListForClubAsync`／`GetForClubAsync`／`CreateAsync`／
  `UpdateAsync`，與既有 J4 下拉選單查詢共用同一個類別）、`AdminTeamsEndpoints.cs`（新增俱樂部
  範圍 CRUD 路由群組）。
- `Features/AdminPlayers/`（新資料夾）：`AdminPlayerDtos.cs`／`AdminPlayerExceptions.cs`／
  `AdminPlayerRequestForm.cs`／`AdminPlayersRepository.cs`／`AdminPlayersEndpoints.cs`。
- `Features/AdminStaff/`（新資料夾）：`AdminStaffDtos.cs`／`AdminStaffExceptions.cs`／
  `AdminStaffRequestForm.cs`／`AdminStaffRepository.cs`／`AdminStaffEndpoints.cs`。
- `Features/Teams/`（新資料夾，**前台公開唯讀端點**）：`TeamDto.cs`／`TeamsRepository.cs`／
  `TeamsEndpoints.cs`——`GET /api/v1/{club}/teams?lang=`，Dapper＋`IQueryCache`，形狀比照既有
  `Features/Players`／`Features/Staff`。**S0-7b 沒有做球隊本身的公開清單端點**（只做了球員／
  教練與職員／新聞／賽程／俱樂部主檔五組），這是新增端點不是既有契約變更。
- `Features/Uploads/UploadSlotPolicy.cs`：新增 `teams.hero`／`players.photo`／`staff.photo`
  三個欄位插槽。
- `Common/ApiExceptionHandler.cs`：新增 C1–C3 例外家族的狀態碼對應。
- `Program.cs`：註冊 `AdminPlayersRepository`／`AdminStaffRepository`／`TeamsRepository` 三個
  DI 服務，掛上 `MapAdminPlayersEndpoints`／`MapAdminStaffEndpoints`／`MapTeamsEndpoints` 三組路由。

**種子資料**（`db/seed/generate-club-seed-sql.py`）：新增 9 個權限碼
（`team.team.*`／`team.player.*`／`team.staff.*`）、對應的 `ROLE_PERMISSIONS` 指派、新測試帳號
`team.manager@tcrfc.test`。

**測試**（`apps/api/Tcrfc.Api.Tests/AdminTeamsPlayersStaffTests.cs`，新檔，15 項）：分兩個類別——
`AdminTeamsPlayersStaffTests`（`AdminWriteCollection`，授權／驗證／共同資料唯讀，12 項）與
`AdminTeamsPlayersStaffUploadTests`（`AdminWriteAzuriteEnabledCollection`，需要真實 Azurite 的
圖片上傳成功案例，3 項）——分兩個 collection 的理由跟既有 `AdminNewsCoverUploadTests` 一致：
`AdminWriteApiFixture` 注入的是 `UnavailableImageStorageService`，帶檔案的請求會是 500。

**綱要**：沒有新增／修改任何 DDL、`db/club-schema.sql`、`Data/Migrations/`、EF 模型
（`Data/EfEntities/`、`ClubDbContext.cs` 一行都沒改）——`dotnet ef migrations
has-pending-model-changes` 綠燈。

### 我的判斷（規劃書沒定義，本輪做了選擇）

- **沒有樂觀並行控制（`updated_at` 並行權杖）**：逐字比照 `Features/AdminCompetitions`（同樣沒有），
  跟 `articles`／`pages` 不同——後兩者規劃書要求「送審→發布」流程與多人協作編輯，C1–C3 是相對
  低頻的名冊維護，本輪判斷投資報酬率不夠，需要時再補。
- **`Player.Status` 省略時預設 `active`**：規劃書沒有明定新增球員的預設狀態，新增球員預設現役
  是常識性判斷。
- **`Staff.Teams` 省略＝維持不變、空陣列＝清空**：逐字比照既有 `AdminArticlesRepository` 對
  `Tags`／`Relations` 的既有語意（S1-5），不是另外發明一套規則。
- **背號值域 1–99、身高 100–250cm、體重 30–150kg**：規劃書沒有給數字，這是常識性合理範圍檢查，
  純粹防呆打字錯誤（例如背號打成 4 位數），不是業務規則。

### 測試結果

`dotnet test`（`Tcrfc.Api.Tests`，從 `Tcrfc.Api.Tests/` 目錄執行，見「怎麼跑」一節）：
**300／300 全過**（既有 285 項 ＋ 本輪新增 15 項）。本模組 filter
（`AdminTeamsPlayersStaffTests|AdminTeamsPlayersStaffUploadTests`）**連跑 10 次，每次 15／15
全過，0 失敗**。`ArchitectureTests`、`dotnet ef migrations has-pending-model-changes` 皆綠燈。

⚠️ 執行期間發現 `AdminAuthTests`／`AdminClubAuthorizerTests` 在多輪重跑後偶爾失敗
（`sa@system.local`／`clean.login@tcrfc.test` 等帳號的密碼／2FA／鎖定狀態被前幾輪測試改動，
不是冪等的種子重灌能解決的），這是**既有已知行為**（`db/seed/reset-admin-accounts.sh` 就是為
這個情況存在），用該腳本重設後兩者皆綠燈，跟本輪新增的程式碼無關，交付前已重設乾淨。

### 本次沒動的部分

- 沒有 DELETE 端點（見上方「端點與權限碼」的說明）。
- 沒有球員肖像同意欄位與相關輸出邏輯（見上方「未實作」段）。
- 沒有給 `academy_program`／`translator` 兩個角色任何 C1–C3 權限（見上方對應段落）。
- 沒有實作 `role_permissions.scope_type` 的列級強制（`own_teams`／`academy_only`）——跟現有
  `team.competition.*` 的既有狀態一致，`STATUS.md` 已把這件事排在 `S1-8`。
- 沒有修改 `apps/admin`（前端接線留給前端 agent）。
- 沒有 commit。

---

## S1-7a：把 `S1-7a` 綱要補齊（commit `7762fe1`）落到程式（2026-09-24，`backend-engineer`）

`S1-7a`（`system-analyst`）已把六項規劃書要求、綱要沒有的欄位／表補進 `docs/12`／`db/club-schema.sql`
（見該 commit）：`players`／`staff.portrait_consent_status`、`banners` 的 `media_type`／
`image_width`／`image_height`／`video_key`、`banners_i18n.image_alt`、`faq_categories.is_enabled`、
`faq_embed_slots`／`faq_embed_slot_links`、7 張表的 `status` CHECK 收斂。本輪把這些變更落到
EF 實體、`ClubDbContext`、一支 migration、四組端點與種子資料。

### Migration：`AlignSchemaS17a`

**內容**：`AddColumn`（`players.portrait_consent_status`、`staff.portrait_consent_status`、
`banners.media_type`／`image_width`／`image_height`／`video_key`、`banners_i18n.image_alt`、
`faq_categories.is_enabled`）＋ `CreateTable`（`faq_embed_slots`、`faq_embed_slot_links`）＋
一段手寫 `migrationBuilder.Sql(...)`（EF 這個專案的既有慣例是完全不對 CHECK 約束建模，
見 `Data/ClubDbContextCustomizations.cs`／`ClubDbContext.cs` 全文找不到任何
`HasCheckConstraint`——CHECK 一律由 DDL 與這裡的手寫 SQL 維護）：

1. 三個新 CHECK：`CK_players_portrait_consent_status`／`CK_staff_portrait_consent_status`（三態）、
   `CK_banners_media_type`（二態）、`CK_banners_video_key`（互相依賴）。這幾欄是全新欄位，
   不需要處理既有資料衝突值。
2. 收斂 7 張表既有的 `status` CHECK（`draft`／`published`／`scheduled` → `draft`／`published`）：
   這些 CHECK 在原始 DDL 是**欄位層、未命名**的（`CHECK (status IN (...))` 沒有
   `CONSTRAINT CK_xxx` 前綴），SQL Server 會自動配一個系統產生的名稱，所以用
   `sys.check_constraints` JOIN `sys.columns` 動態查出實際名稱再 `DROP`，換成明確命名的版本
   （`CK_<table>_status`），方便以後直接用名稱操作。

**驗收（依 docs/20-cicd.md §5）**：

```
dotnet ef migrations add Probe --context ClubDbContext -o Data/Migrations
# Up()／Down() 皆為空方法主體 → 基準沒有偏移
dotnet ef migrations remove --context ClubDbContext
# 這支從沒套用過，remove 只刪檔案，不會對任何資料庫執行 Down()（E-46 教訓）

dotnet ef migrations script AddMatchOriginalSchedule AlignSchemaS17a --idempotent
# 逐欄核對與 db/club-schema.sql 一致；型別、長度、NULL、預設值、CHECK 皆對齊
```

🔴 **升級路徑驗證（用完即丟的資料庫）**：`git show 7762fe1~1:db/club-schema.sql` 取出
「上一版」DDL，把其中的 `json` 型別字面替換成 `nvarchar(max)`（本機 SQL Server 2022 容器的
既知限制，見 `docs/12` §1.4 第 2 點「本機用 SQL Server 2022 容器驗證 DDL 時要先把 json 換成
nvarchar(max)」，不是 DDL 寫錯），建出 `tcrfc_club_probe`（145 張表），手動插入
`__EFMigrationsHistory` 四筆既有 migration（標記為已套用，不重跑），再對這個資料庫跑
`dotnet ef database update`——**只會執行 `AlignSchemaS17a`**，成功套用（147→148 張表，含
`__EFMigrationsHistory` 本身），實測 CHECK 約束真的擋得下 `status='scheduled'` 與非法的
`portrait_consent_status`。驗完 `DROP DATABASE tcrfc_club_probe`。

**本機 `tcrfc_club_dev`**：依任務指示套用（不是 `remove`）。套用前後核對表數（146→148）、
`players`（56）／`staff`（13）／`banners`（0）／`faq_categories`（10）／`faqs`（0）筆數皆未變動，
新欄位全部正確回填預設值（`portrait_consent_status='not_consented'`、`banners.media_type` 無資料
不受影響、`faq_categories.is_enabled=1` 全數 10 筆）。

### 🔴 套用時發現並修正：`portrait_consent_status` 欄寬算錯（`nvarchar(20)` 裝不下 21 字元的值）

**發現方式**：不是人工核對挑出來的，是測試在對 `tcrfc_club_dev` 實際寫入
`'consented_by_guardian'` 時，SQL Server 直接回 `String or binary data would be truncated`（
`nvarchar(20)` 只能放 20 個字元，`'consented_by_guardian'` 是 21 個字元），API 對外變成 500。
`db/club-schema.sql`（commit `7762fe1`）與初版 migration 都寫 `nvarchar(20)`——這是單純的欄寬
計算錯誤（三態裡最長的字面值算漏了），不是規格分歧，`docs/12` 本身也沒有指定確切欄寬，
故直接修正，不算違反「先改文件再改 DDL」（沒有規格要改，只有算錯的數字要改）：

- `db/club-schema.sql`：`players`／`staff.portrait_consent_status` 改為 `nvarchar(32)`（兩處），
  並在該表註解說明原因。
- EF：`ClubDbContext.cs` 兩處 `.HasMaxLength(20)` 改 `32`；migration `AlignSchemaS17a` 的
  `AddColumn` 呼叫、`ClubDbContextModelSnapshot.cs`、`*.Designer.cs` 同步改為 `nvarchar(32)`／
  `HasMaxLength(32)`（這支 migration 尚未提交，直接修正檔案內容比另開一支「修正欄寬」的
  migration乾淨——但**已經套用到 `tcrfc_club_dev` 的實際欄位**用 `ALTER TABLE ... ALTER COLUMN`
  手動改寬，`CHECK`／`DEFAULT` 約束在 `ALTER COLUMN` 之後仍完整存在，已用 `sys.check_constraints`／
  `sys.default_constraints` 查證，套用前後 `players`／`staff` 筆數不變）。
- `docs/12-database-schema.md` §12 第 32 點已補註這個修正。

### 🔴 Scheduled 列的處理：查證結果是 0 筆，不需要 DML 轉態

依任務指示，改 CHECK 前查了 `tcrfc_club_dev` 與 `db/seed/generate-club-seed-sql.py`：
七張表（`press_resources`／`faqs`／`competitions`／`sponsor_packages`／`collections`／`products`／
`charity_programs`）目前**全部是 0 筆 `status='scheduled'`**——種子腳本裡唯一會寫入 `'scheduled'`
字面值的是 `matches.status`（賽程表的既有語意，跟這 7 張表的 `status` 是不同欄位、不同型別的
狀態機，不受本次收斂影響）；`AdminCompetitionsRepository`／`AdminFaqsRepository` 等既有的
`ValidateStatus` 應用層驗證本來就只接受 `draft`／`published` 兩態，代表寫入路徑本來就沒有機會
產生 `'scheduled'` 的資料列。**因此本輪 migration 的 `Up()` 沒有搭配任何 DML 轉態**，收斂 CHECK
是安全的。

### 肖像同意：在哪些端點生效

| 端點 | 生效方式 |
|---|---|
| `GET /api/v1/{club}/players` | `PlayersRepository.Map`：`portrait_consent_status='not_consented'` 時 `photoKey` 回 `null`，其餘兩態正常輸出 |
| `GET /api/v1/{club}/staff` | `StaffRepository.Map`，同上邏輯 |
| `GET/POST/PUT /api/v1/admin/{club}/players`、`.../staff` | **不遮罩**——後台一律看得到真實 `photoKey` 與 `portraitConsentStatus`，只有公開端點才過濾（後台檢視者本來就該看到完整值，遮罩沒有意義） |
| 新建球員／教練 | **fail-closed**：`CreateAdminPlayerRequest.PortraitConsentStatus` 省略時預設 `not_consented`，公開端點立刻不輸出照片，直到後台明確填寫已取得同意 |

🔴 **未涵蓋、需要下一輪注意**：目前系統唯一會輸出球員／教練照片的公開端點就是這兩支
（`Features/Players`／`Features/Staff`）——已用 `grep -rn "PhotoKey"` 全文檢索確認，沒有其他
公開端點（含既有的 `Features/Teams` 球隊清單）內嵌球員／教練照片。之後若新增任何會輸出這兩張
表 `photo_key` 的公開端點（例如球隊詳情頁若之後改成內嵌名單），**都要重新套用同一條 fail-closed
規則**，不能假設只有這兩支端點需要顧慮。

### Banners：`media_type`／寬高／`alt`

- `AdminBannerDtos`／`AdminBannersRepository`：`CreateBannerRequest`／`UpdateBannerRequest` 新增
  `MediaType`（省略回退 `image`），`AdminBannerLocaleContent` 新增 `ImageAlt`（雙語，隨 `payload`
  JSON 送出，不是檔案上傳的一部分）。`ImageWidth`／`ImageHeight` **不接受呼叫端輸入**——由
  `AdminBannersEndpoints` 從 `UploadedImageInfo.Width`／`Height`（上傳結果）取得後傳進
  `CreateAsync`／`UpdateAsync`，比照 `docs/14` 圖片欄位組通則「由上傳結果自動填入」；更新時
  若這次請求沒有換圖，寬高維持原值不被清空。
- 🔴 **本輪只允許 `MediaType="image"`**：送 `"video"` 一律 400（`AdminBannersRepository.
  ValidateMediaType`，訊息明講「影片的格式、檔案大小上限與是否轉碼尚待裁決」）。`VideoKey`
  沒有任何寫入路徑，永遠是 `null`——`banners.media_type` 因此永遠是 `'image'`，
  `CK_banners_video_key`（`media_type='image' OR video_key IS NOT NULL`）恆滿足，不會擋到任何
  這一輪的寫入。
- 公開端點 `GET /api/v1/{club}/banners`（`Features/Home/HomeRepository.ListBannersAsync`）
  一併補上 `mediaType`／`imageWidth`／`imageHeight`／`videoKey`／`imageAlt`（依語系回退）五個欄位。

### FAQ 分類 `is_enabled`：軟停用取代 DELETE

- `AdminFaqCategoryDtos`／`AdminFaqCategoriesRepository`：`Create`／`UpdateAdminFaqCategoryRequest`
  新增 `IsEnabled`（建立省略預設 `true`；更新為必填欄位，要求呼叫端每次明確帶值，理由是這是
  一個會直接影響公開可見度的開關，不該有「省略時算什麼」的模糊地帶）。`DELETE` 端點**保持不變、
  現在是真正的刪除**（不可逆，經 `ON DELETE CASCADE` 解除 `faq_category_links`）。
- 公開端點 `GET /api/v1/faq-categories`（`Features/Faqs/FaqsRepository.ListCategoriesAsync`）
  加上 `WHERE fc.is_enabled = 1`。**題目本身與既有分類關聯完全不受停用影響**——停用只是讓分類從
  導覽消失，掛在這個分類底下的題目仍然存在、仍可被關鍵字搜尋到，後台仍看得到完整關聯
  （已用測試驗證：停用分類後 `GET /api/v1/admin/tcrfc/faqs/{id}` 仍回傳這個分類）。
- 舊版 `AdminFaqCategoriesRepository`／`AdminFaqCategoriesEndpoints` 檔頭「停用＝刪除」的說明
  已經改寫，避免下一個讀到的人以為現在還是那樣做。

### FAQ 嵌入設定：G-12 掛載點

- 新增 `Features/AdminFaqs/AdminFaqEmbedSlotDtos.cs`／`AdminFaqEmbedSlotsRepository.cs`／
  `AdminFaqEmbedSlotsEndpoints.cs`：`GET /api/v1/admin/faq-embed-slots`（全域、唯讀，沿用
  `content.faq.view` 權限碼——4 筆固定字典不值得為它另開一組權限碼，比照
  `Features/AdminHomeSections/HomeSectionCatalog.cs` 那種「固定字典不開權限碼」的既有慣例）。
- `CreateFaqRequest`／`UpdateFaqRequest` 新增 `EmbedSlotIds`（`IReadOnlyList<Guid>?`）：
  跟 `CategoryIds`（必填、至少 1 個、一律整份取代）刻意不同——**省略（`null`）＝維持不變、
  非 `null`（含空陣列）＝整份取代**，語意比照 `AdminStaffRepository.Teams`。`sort_order` 依
  呼叫端給的清單順序寫入（`faq_embed_slot_links` 本身沒有 `row_seq`）。
- 公開端點 `GET /api/v1/{club}/faqs/embeds/{slotCode}`（`Features/Faqs/FaqsRepository.
  ListByEmbedSlotAsync`）：🔴 **只回傳「逐題額外指定」那一半**——「由分類自動對應」是應用層
  （前台頁面元件）的固定路由決定，`docs/12` §12 第 34 點明講「刻意不建掛載點對應哪個分類的
  對照表」，後端沒有資料可以查出「哪個掛載點該自動帶哪個分類」。前台要湊出規劃書要的完整聯集
  效果，做法是**同時**呼叫這支端點與既有的 `?category=<該頁固定對應的分類 slug>` 篩選，
  自行合併去重——這是 apps/admin／apps/web 前端接線需要知道的**契約**（見下方「契約變更」）。
  找不到的掛載點代碼視為空清單（`200` + `[]`），不是 `404`。
- 快取：新增 `faq-embed` entity，寫入（Create／Update／批次操作／CSV 匯入）皆透過既有的
  `InvalidatePublicCacheAsync` 一併失效。

### 端點清單（本輪新增／變更）

| 方法與路徑 | 說明 |
|---|---|
| `GET /api/v1/admin/faq-embed-slots` | 新增：G-12 掛載點字典（唯讀，4 筆固定值） |
| `GET /api/v1/{club}/faqs/embeds/{slotCode}` | 新增：依掛載點查詢「額外指定」的題目（公開） |
| `POST/PUT /api/v1/admin/{club}/players`、`.../staff` | 變更：payload 新增 `portraitConsentStatus`（省略回退 `not_consented`） |
| `POST/PUT /api/v1/admin/{club}/banners` | 變更：payload 新增 `mediaType`（送 `video` 400）、`content.imageAlt`（雙語） |
| `POST/PUT /api/v1/admin/faq-categories` | 變更：payload 新增 `isEnabled`（建立選填預設 `true`，更新必填） |
| `POST/PUT /api/v1/admin/{club}/faqs` | 變更：payload 新增 `embedSlotIds`（選填，省略＝維持不變） |

### 契約變更（`apps/admin` 需要跟著改）

1. `AdminPlayerListItemDto`／`AdminPlayerDetailDto`、`AdminStaffListItemDto`／
   `AdminStaffDetailDto` 新增 **必填** 欄位 `portraitConsentStatus`——既有前端若用嚴格型別解析
   會需要補上這個欄位（三態字串，`not_consented`／`consented`／`consented_by_guardian`）。
   球員／教練編輯表單需要新增一個「肖像同意」選擇欄位，並在未同意時給出視覺提示
   （例如照片欄位旁加註「未同意肖像使用，公開頁面不會顯示照片」）。
2. `AdminBannerListItemDto`／`AdminBannerDetailDto` 新增必填欄位 `mediaType`，選填欄位
   `imageWidth`／`imageHeight`／`videoKey`；`AdminBannerLocaleContent` 新增選填欄位 `imageAlt`。
   前端表單需要新增「圖片替代文字」欄位（雙語）；`mediaType` 目前固定顯示為圖片即可，
   不需要提供切換到影片的選項（後端會拒絕）。
3. `AdminFaqCategoryListItemDto`／`AdminFaqCategoryDetailDto` 新增必填欄位 `isEnabled`；
   `UpdateAdminFaqCategoryRequest` 的 `isEnabled` 是**必填**欄位（不是選填），前端更新分類時
   一定要帶這個值（通常是「維持目前畫面上看到的狀態」）。分類管理畫面需要新增啟用／停用切換，
   **不能再用「刪除＝停用」的按鈕語意**——刪除按鈕現在是真的刪除，需要有獨立的「停用」開關。
4. `AdminFaqListItemDto`／`AdminFaqDetailDto` 新增必填欄位 `embedSlots`（陣列，每筆
   `{id, code, name}`）；`CreateFaqRequest`／`UpdateFaqRequest` 新增選填欄位 `embedSlotIds`
   （guid 陣列）。FAQ 編輯表單可以選擇性地加一個「額外指定出現於」多選欄位（來源是
   `GET /api/v1/admin/faq-embed-slots`），第一版前端如果暫時不做這個 UI，維持不傳這個欄位即可
   （省略＝維持不變，不會影響既有資料）。

### 測試

新增 7 項（`Tcrfc.Api.Tests`，全套 `dotnet test` 從 316 增至 323）：

| 檔案 | 新增測試 |
|---|---|
| `AdminTeamsPlayersStaffTests.cs` | `Player_肖像同意狀態_省略時fail_closed預設不輸出照片_三態各驗一次`（建立預設 `not_consented`、三態各驗一次公開端點輸出行為、非法值 400）、`Staff_肖像同意狀態_省略時fail_closed預設不輸出照片` |
| `AdminBannersAndHomeSectionsTests.cs` | `Banner_圖片寬高由上傳結果自動填入_alt雙語_media_type送video回400`（建立／換圖／不換圖三種情境的寬高行為、雙語 alt、`video` 一律 400、公開端點吐出新欄位） |
| `AdminFaqsAndCategoriesTests.cs` | `FaqCategory_軟停用_公開端點不列_既有題目與關聯不受影響_可重新啟用`、`FaqEmbedSlot_未登入_擋下_已登入可列出四筆固定值`、`Faq_指定嵌入掛載點_新增_省略維持不變_空陣列清空_公開端點依掛載點查得到`、`公開嵌入端點_不回傳草稿或跨俱樂部題目_找不到的掛載點視為空清單` |

**測試結果**：

```
dotnet test --filter "FullyQualifiedName~AdminTeamsPlayersStaffTests|FullyQualifiedName~AdminBannersAndHomeSectionsTests|FullyQualifiedName~AdminFaqsAndCategoriesTests|FullyQualifiedName~PublicFaqsTests" --no-build
# 已通過! - 失敗: 0，通過: 59，總計: 59（連跑 10 次，每次都是 0 失敗）

dotnet test --no-build（全套，前後各執行一次 db/seed/reset-admin-accounts.sh）
# 已通過! - 失敗: 0，通過: 323，總計: 323

dotnet ef migrations has-pending-model-changes --context ClubDbContext
# No changes have been made to the model since the last migration.
```

### 改了哪些檔案

**後端**（`apps/api/`）：
- `Data/EfEntities/`：`Player.cs`／`Staff.cs`（`PortraitConsentStatus`）、`Banner.cs`
  （`MediaType`／`ImageWidth`／`ImageHeight`／`VideoKey`）、`BannersI18n.cs`（`ImageAlt`）、
  `FaqCategory.cs`（`IsEnabled`）、`Faq.cs`（新增 `FaqEmbedSlotLinks` 導覽）、`AdminUser.cs`
  （新增 `FaqEmbedSlotCreatedByNavigations`／`UpdatedByNavigations`）、新檔
  `FaqEmbedSlot.cs`／`FaqEmbedSlotLink.cs`。
- `Data/ClubDbContext.cs`：對應的屬性映射、兩個新實體的 `OnModelCreating` 設定區塊、
  兩個新 `DbSet`。
- `Data/Migrations/20260924065835_AlignSchemaS17a.cs`（新檔，含 `.Designer.cs`）、
  `ClubDbContextModelSnapshot.cs`（更新）。
- `Features/AdminPlayers/`：`AdminPlayerDtos.cs`／`AdminPlayersRepository.cs`。
- `Features/AdminStaff/`：`AdminStaffDtos.cs`／`AdminStaffRepository.cs`。
- `Features/Players/PlayerDto.cs`／`PlayersRepository.cs`：fail-closed 過濾。
- `Features/Staff/StaffDto.cs`／`StaffRepository.cs`：fail-closed 過濾。
- `Features/AdminBanners/`：`AdminBannerDtos.cs`／`AdminBannersRepository.cs`／
  `AdminBannersEndpoints.cs`。
- `Features/Home/HomeDtos.cs`／`HomeRepository.cs`：公開輪播新增五個欄位。
- `Features/AdminFaqs/`：`AdminFaqCategoryDtos.cs`／`AdminFaqCategoriesRepository.cs`／
  `AdminFaqCategoriesEndpoints.cs`（`IsEnabled`）、`AdminFaqDtos.cs`／`AdminFaqsRepository.cs`
  （`EmbedSlotIds`／`EmbedSlots`），新檔 `AdminFaqEmbedSlotDtos.cs`／
  `AdminFaqEmbedSlotsRepository.cs`／`AdminFaqEmbedSlotsEndpoints.cs`。
- `Features/Faqs/FaqsRepository.cs`／`FaqsEndpoints.cs`：分類 `is_enabled` 過濾、新增
  `ListByEmbedSlotAsync` 與 `GET .../faqs/embeds/{slotCode}`。
- `Features/Uploads/UploadSlotPolicy.cs`：更新 `banners` 插槽註解（寬高／alt 已補齊）。
- `Program.cs`：註冊 `AdminFaqEmbedSlotsRepository`、掛上 `MapAdminFaqEmbedSlotsEndpoints`。

**綱要**：`db/club-schema.sql`（欄寬修正，見上方說明）。

**種子資料**（`db/seed/generate-club-seed-sql.py`）：新增 §21 `faq_embed_slots` 四筆固定值
（DML）。沒有新增權限碼。

**測試**：`Tcrfc.Api.Tests/AdminTeamsPlayersStaffTests.cs`／`AdminBannersAndHomeSectionsTests.cs`／
`AdminFaqsAndCategoriesTests.cs`，見上方「測試」一節，共 7 項新增。

### 本次沒動的部分

- 沒有動 `apps/admin`（契約變更清單已列在上方，交給前端 agent 接線）。
- CSV 匯入／匯出（`AdminFaqsRepository.ImportCsvAsync`／`ExportCsvAsync`）**沒有**納入
  `embedSlotIds`——規劃書與 `docs/12b` §10.1 都沒有要求 CSV 涵蓋嵌入設定，本輪不擴大範圍。
- 沒有替 `faq_embed_slots` 開任何新增／刪除／改名的後台 CRUD——四筆是固定字典，見
  `db/club-schema.sql` 該表註解。
- 影片輪播（`banners.media_type='video'`）完全沒有實作，格式／大小／轉碼規則仍待使用者裁決。
- 沒有修改 `docs/14-invariants.md`（本輪沒有發現需要新增的全站不變量——肖像同意的 fail-closed
  規則已經完整寫在 `docs/12` §12 第 32 點，不重複記一份）、`STATUS.md`、`docs/18-work-errors.md`
  （依任務指示由派工者收尾）。
- 沒有 commit。

---

## S1-8：`C4` 賽程與賽果／積分榜 ＋ 列級授權強制（`own_teams`／`academy_only`）（2026-09-24，`backend-engineer`）

### 讀到的規劃書條文

| 章節 | 行號 | 內容 |
|---|---|---|
| 主站 §4.3 C4 | 1074–1078 | 賽事欄位（含 v3.12 場次編號、v3.13 原定日期時間）、結果（比分、進球者與時間、卡牌、出賽名單）、積分榜、**全部人工維護、CSV 整季匯入** |
| 主站 §3.13 | 586–708 | 隊別分類（跨梯隊友誼賽可複選）、賽事卡片狀態標記（**含「取消」，與 C4 不一致，見下方「缺口」**） |
| 主站 §5.1 型別總表 | 1480–1481 | `Match`（含 v3.13 原定日期時間欄位）、`Standing` |
| 主站 §6 權限矩陣 | 1597–1645 | 「球隊／賽事」欄逐角色分佈；`academy_program` 備註「賽事權限限 `scope_type=academy_only`」 |
| docs/12b §7.1／§7.1b／§7.4 | 161–249 | `AdminUserTeam` 機制、資料範圍執行期規則、`scope_type` 值域與矩陣對照 |
| docs/12b §10.1 | 393–405 | CSV 匯入項目：整季賽程→`Match`（＋`MatchTeam`、`match_i18n`）、積分榜→`Standing` |
| docs/12b §11.1／§11.3 | 437–486、498–517 | 無 `match_no` 唯一鍵（應用層自行檢查）、`match_*` 子表皆 `ON DELETE CASCADE` |
| db/club-schema.sql `matches` 表註解 | 782–814 | `match_no` 業務唯一鍵範圍未定案，交後台驗證；`original_match_on`／`original_kickoff` 只在延賽時有值，交表單驗證 |

### 端點與權限碼

兩組俱樂部範圍 CRUD，module_code=C、submodule_code=C4、domain=`team`（跟既有 `team.team.*` 等
同一個 domain）：

| 模組 | 路由 | 權限碼 | 列級授權 |
|---|---|---|---|
| C4 賽程與賽果 | `GET/POST /api/v1/admin/{club}/matches`、`GET/PUT/DELETE /api/v1/admin/{club}/matches/{id}`、`POST /api/v1/admin/{club}/matches/import`（CSV） | `team.match.view`／`.create`／`.update`／`.delete` | ✅ 套用 `TeamRowScope`（含 CSV 匯入逐列） |
| C4 積分榜 | `GET/POST /api/v1/admin/{club}/standings`、`GET/PUT/DELETE /api/v1/admin/{club}/standings/{id}`、`POST /api/v1/admin/{club}/standings/import`（CSV，整季替換） | `team.standing.view`／`.create`／`.update`／`.delete` | ❌ **刻意不套**，見下方「為什麼積分榜不做列級授權」 |

**有 DELETE**（與 C1–C3 刻意不同）：賽事是純資料紀錄，不是「人」，沒有「離隊」這種需要保留歷史
狀態的語意，資料輸入錯誤直接刪掉重建即可；`match_goals`／`match_cards`／`match_lineups`／
`match_teams`／`matches_i18n` 皆為 `ON DELETE CASCADE`（docs/12b §11.3），刪主表列即完整清除。

**角色授予**（依主站規劃書 §6 矩陣「球隊／賽事」欄，`db/seed/generate-club-seed-sql.py` 已更新）：

| 角色 | `team.match.*` | `team.standing.*` |
|---|---|---|
| 系統管理員 | ✔ 全（`scope_type=all`） | ✔ 全 |
| 競技／球隊管理 | ✔ 全（`all`） | ✔ 全 |
| 商務／贊助、公關／媒體、檢視者 | 唯讀（`all`） | 唯讀 |
| 合作球隊管理 | ✔ 自家全權限（`own_clubs`） | ✔ 自家全權限 |
| **學院／課程管理**（本輪補上，見下方） | ✔ 全，但 `scope_type=academy_only` | **不指派**（見下方原因） |
| 客服／行政、翻譯人員 | — | — |

🔴 **`academy_program` 本輪一併補上 `team.team.*`／`team.player.*`／`team.staff.*`**（S1-3、S1-7
兩輪刻意保留，因為列級強制的執行機制當時還不存在）：四組權限碼全部改為 `scope_type=academy_only`，
現在 `TeamRowScope`／`AdminTeamRowScopeResolver` 已經把這個值真的落實成資料過濾，開放的前提
成立。

### 🔴 列級授權的設計與生效範圍

**問題**：`role_permissions.scope_type` 這個欄位在 `S1-3`／`S1-7` 兩輪都只是「種進去但沒人讀」的
資料（`PermissionChecker.HasPermissionAsync` 只判斷「有沒有這個權限碼」，不管 `scope_type`
是什麼值）。本輪把它接上真正的執行邏輯。

**與既有 `AdminClubScope`／`ArchitectureTests` 型別層強制一致（含取捨說明）**：

新增 `Security/TeamRowScope.cs`（`sealed class`，`internal` 建構子）＋
`Security/IAdminTeamRowScopeResolver.cs`／`AdminTeamRowScopeResolver.cs`（唯一產生者），
套用與 `AdminClubScope`／`AdminSystemScope` 完全相同的三層防護（`ArchitectureTests` 已擴充
納入 `TeamRowScope`，允許清單只加 `AdminTeamRowScopeResolver.cs`）：
1. `sealed class` 不是 `readonly struct`——擋 `default`／`default(T)` 繞過建構子。
2. 建構子 `internal`，只有 `AdminTeamRowScopeResolver` 能造實例。
3. `ArchitectureTests` 的 Roslyn 語意掃描納入這個型別。

**取捨（誠實說明為什麼值得付這筆成本）**：`AdminClubScope` 擋的是「能不能碰這個俱樂部」，繞過
等於跨俱樂部資料外洩；`TeamRowScope` 擋的是「同一個俱樂部內能不能碰特定球隊」，繞過的後果是
「學院管理者改到一線隊賽程」這種**權限逾越**，嚴重度較低。但這個機制會被 C1／C2／C3／C4
四個模組共用（見下方逐一列出），共用機制一旦有漏洞影響面是四個模組一起漏，值得付同一筆型別層
防護成本，而不是退回「repository 自己記得呼叫檢查方法」（後者在四個模組裡有任何一處忘記呼叫，
防線就整個消失且不會有任何測試或工具能自動抓到）。

**執行序**：`IAdminClubAuthorizer.AuthorizeAsync` 先確認「這個人對這個俱樂部有沒有授權、有沒有
這個權限碼」（既有機制不變）→ 再呼叫 `IAdminTeamRowScopeResolver.ResolveAsync(scope,
permissionCode)` 針對**這個具體權限碼**算出 `TeamRowScope`——同一個人對不同權限碼可能有不同
`scope_type`，故每次呼叫都要帶著具體權限碼查，不能快取共用。

**`TeamRowScope` 的三個判斷方法**（供四個模組依資源形狀選用）：

| 方法 | 用途 | 用在哪 |
|---|---|---|
| `Allows(teamId, teamType)` | 單一既有球隊資源 | C1 更新既有球隊本身、C2 球員的 `team_id`、C4 賽事逐一關聯球隊 |
| `AllowsAll(IReadOnlyCollection<(TeamId, TeamType)>)` | 多筆關聯，任何一筆不通過整體就不通過；**空集合視為不通過**（fail-closed） | C3 教練的 `staff_teams`（可能同時帶多個梯隊）、C4 賽事的 `match_teams`（跨梯隊友誼賽可複選） |
| `AllowsCreatingTeamOfType(newTeamType)` | 建立**全新**球隊（沒有既有 id 可比對） | C1 建立球隊：`own_teams` 範圍一律不能新建（現實對應：被個別指派特定梯隊的帳號不該有新建球隊這種俱樂部層級操作）；`academy_only` 範圍只能建 `academy` 類型 |

**`own_teams`／`academy_only` 的組裝邏輯**（`AdminTeamRowScopeResolver.ResolveAsync`）：
- 任一角色的這個權限碼是 `all`（或 `own_clubs`，見下方發現）→ `IsUnrestricted=true`，其餘分支
  略過。
- 否則把 `academy_only`（設 `AllowsAcademyBlanket=true`，比對時看球隊的 `teams.type` 是否為
  `academy`，不用先查一份「全部 academy 球隊 id」的清單）與 `own_teams`（查 `admin_user_teams`
  中 `is_active` 且 `expires_on` 未到期的 `team_id` 集合）**聯集**——同一個人可能同時因為不同
  角色分別拿到這兩種授權方式。
- 查無任何 `scope_type`（理論上不會發生，因為呼叫端已經先過權限碼檢查）→ **fail-closed**（完全
  限制），不是預設放行。

**🔴 意外發現並修正的既有落差**：`db/seed/generate-club-seed-sql.py` 對 `partner_club_manager`
既有的全部 `ROLE_PERMISSIONS` 指派用的是 `scope_type="own_clubs"`（docs/12b §7.1 明文
「`RolePermission: scope_type` 加值 `own_clubs`」，但 §7.4 那張「`scope_type` 是矩陣裡不是布林
的格子」對照表只列了 `own_teams`／`academy_only`／`masked`／`translate_only` 四個，沒有把
`own_clubs` 收進去——**兩段自相矛盾**）。若 `AdminTeamRowScopeResolver` 沒有特別處理
`own_clubs`，會被下面的 fail-closed 分支誤判為「查無有效 `scope_type`」而整批拒絕，**合作球隊
管理角色會變成完全無法操作任何球隊、球員、教練、賽事資料**——這個 bug 在本輪列級強制真正接上
執行邏輯之前完全不會顯現（`scope_type` 以前沒人讀）。**已修正**：`own_clubs` 在
`AdminTeamRowScopeResolver` 裡視同 `all`（不限）——它標記的是「club 層級」的範圍（已經由
`IAdminClubAuthorizer` 的 `AdminUserClub` 檢查在更上一層擋住），不是「同一個俱樂部內部」要不要
再對球隊窄化，兩者是不同層次的問題。**已用 `列級授權_own_teams_只能碰admin_user_teams授權的球隊`
之外的既有 `AdminClubsAndCompetitionsTests`／`AdminTeamsPlayersStaffTests` 對 `partner_club_manager`
的既有測試回歸驗證行為未變**。建議 `system-analyst` 之後把 `own_clubs` 也正式收進 docs/12b §7.4
的表格，消除這個文件內部矛盾。

**逐一列出生效範圍（哪些端點套了列級授權）**：

| 端點 | 檢查點 |
|---|---|
| C1 `POST /teams` | `AllowsCreatingTeamOfType(request.Type)` |
| C1 `PUT /teams/{id}` | 既有球隊 `Allows(id, 目前type)`；若改類型，新類型另外過 `AllowsCreatingTeamOfType` |
| C2 `POST /players` | 目標球隊 `Allows(teamId, teamType)` |
| C2 `PUT /players/{id}` | 既有球隊與目標球隊**都要** `Allows` |
| C3 `POST /staff` | 全部指派球隊 `AllowsAll`（空清單視為不通過） |
| C3 `PUT /staff/{id}` | 既有指派球隊 `AllowsAll`；若提供新的 `Teams`，新清單也要 `AllowsAll` |
| C4 `POST /matches`、`PUT /matches/{id}`、`DELETE /matches/{id}` | 全部關聯球隊 `AllowsAll`（PUT 額外檢查既有關聯與新關聯兩份清單） |
| C4 `POST /matches/import`（CSV） | 逐列在驗證階段就套用 `AllowsAll`，不合格視同一種列驗證錯誤（回傳在 `Errors` 裡，不是 403） |
| C4 積分榜（全部端點） | ❌ 不套用，見下一節 |

**C1–C3 這輪一併套上（任務指示「若 C1–C3 寫入端點也該套同一強制，一併套上並測試」）**：
`AdminTeamsRepository.CreateAsync`／`UpdateAsync`、`AdminPlayersRepository.CreateAsync`／
`UpdateAsync`、`AdminStaffRepository.CreateAsync`／`UpdateAsync` 六個方法簽章都加了
`TeamRowScope rowScope` 參數，對應六個端點（`AdminTeamsEndpoints`／`AdminPlayersEndpoints`／
`AdminStaffEndpoints` 各自的 POST／PUT）都改成先呼叫 `IAdminTeamRowScopeResolver.ResolveAsync`
再把結果傳進 repository。**只套寫入端點，沒有套 GET／list**——任務原文明確寫「寫入端點」，
列表／檢視的列級過濾（例如讓 `academy_program` 的球隊清單只顯示學院梯隊）留給下一輪視需求決定，
不在本輪自行擴大範圍。

### 為什麼積分榜（`Standing`）不套列級授權

`standings` 表的欄位是 `(club_id, season_id, team_name, rank, played, points)`——`team_name`
是**自由文字**（docs/12-database-schema.md §12 第 24 點：「`Team` 只放本會四隊，積分榜其餘球隊
是 `team_name` 字串」），**沒有任何欄位指向本方 `teams.id`**。列級授權需要知道「這一列屬於哪支
本方球隊」才能判斷 `academy_only`／`own_teams` 准不准碰，這張表的結構完全無法回答這個問題——
一份積分榜代表整個聯賽的排名表（本方與對手同列並陳），不是「本方某支球隊的積分」。實務上目前
只有一線隊（企業甲組聯賽）有真正的積分榜需求（規劃書 3.1「Results & Standings」只出現在
FOOTBALL CLUB／一線隊頁），因此本輪判斷：**寧可不開放給 `academy_program`，也不要開放了卻擋不住**
——`db/seed/generate-club-seed-sql.py` 沒有把 `team.standing.*` 指派給 `academy_program`。
若日後真的要讓學院管理者也維護學院賽事的積分榜，`standings` 需要先加一個可為空的 `team_id`
外鍵——**這是本次回報的綱要缺口，本輪未動手加欄位**（見下方「綱要缺口或待裁決」）。

### CSV 格式

**賽程**（`POST /matches/import`，`text/csv` 原始位元組，UTF-8 BOM，中文表頭，**整批新建，不是
upsert**——見 `AdminMatchesRepository.ImportCsvAsync` 檔頭「為什麼不做 upsert」的完整說明：
`matches` 沒有穩定的自然鍵可以拿來判斷「這是不是同一場賽事」）：

```
所屬球隊,賽季代碼,賽事系列代碼,日期,時間,主客場,對手,對手英文,場地,場地英文,賽事類型,場次編號,輪次,狀態
D1,2026-27,enterprise-a,2026-11-01,19:00,主場,高雄先鋒,,楠梓足球場,,聯賽,3,1,未開始
```

- **所屬球隊**：`Team.code`（全站唯一），多支用全形頓號「、」相接（跨梯隊友誼賽）。
- **賽事系列代碼**可留空。**主客場**：`主場`／`客場`／留空（對應 `HOME`／`AWAY`）。**賽事類型**：
  `聯賽`／`盃賽`／`友誼賽`／`其他`／留空（對應 `league`／`cup`／`friendly`／`other`）。**狀態**：
  `未開始`／`進行中`／`已結束`／`延賽`（對應 `scheduled`／`live`／`played`／`postponed`，見下方
  值域說明）。
- 🔴 **整批驗證，任一列有錯就整批不寫入**；列級授權不通過視同一種驗證錯誤（回在 `Errors` 裡）；
  場次編號同時檢查「檔案內部不重複」與「資料庫既有列不重複」；**不寫入任何 log 表**（`E-44`）。

**積分榜**（`POST /standings/import`，**整季替換，不是逐列 upsert**——見
`AdminStandingsRepository.ImportCsvAsync` 檔頭說明：積分榜每週滾動更新，最常見維護方式是把
官方聯賽網站最新排名表整份複製貼上，沒有穩定自然鍵可以逐列比對）：

```
賽季代碼,名次,球隊名稱,出賽場次,積分
2026-27,1,台中磐石,10,28
```

一份檔案只能包含同一個賽季（表頭下每列的賽季代碼必須一致，混雜視為驗證錯誤）；匯入成功會**先
刪除這個賽季的全部既有列，再整批寫入新內容**（同一個交易，驗證失敗完全不影響既有資料）。

### 🔴 `matches.status` 值域定案（規劃書沒有給列舉代碼，本輪第一次定案）

`scheduled`（未開始）／`live`（進行中）／`played`（已結束）／`postponed`（延賽）——逐字沿用種子
資料與既有測試已經在用的三個真實字串（`generate-club-seed-sql.py` 的 `scheduled`／`played`，
`ScheduleOriginalDateTests.cs` 的 `postponed`），只新增 `live`。

### 延賽驗證與場次編號唯一

- **延賽須填原定日期**（主站規劃書 §3.13／§4.3 C4）：`status='postponed'` 時 `OriginalMatchOn`
  必填；非 `postponed` 時 `OriginalMatchOn`／`OriginalKickoff` 必須是空的，不留矛盾資料。
- **場次編號同季同聯賽唯一**：`matches.match_no` 沒有 DB 唯一索引（docs/12b §11.1 只列五個維持
  全站唯一的既有唯一鍵，這條是本輪新增的業務規則），在 `AdminMatchesRepository` 應用層查詢比對
  `(club_id, season_id, competition_id, match_no)`；`competition_id` 為 `null` 時只跟同樣沒掛
  賽事系列的賽事比較。

### 進球／卡牌／出賽名單（`match_goals`／`match_cards`／`match_lineups`）

規劃書 C4「結果：比分、進球者與時間、卡牌、出賽名單」明文要求，本輪內嵌在 `Match` 的
`Create`／`UpdateAdminMatchRequest`（`Goals`／`Cards`／`Lineups` 三個可省略陣列，省略＝維持
不變、提供（含空陣列）＝整份取代，逐字比照 `AdminStaffRepository.UpdateAsync` 對 `Teams` 的既有
語意），不另開三組獨立 CRUD 端點——這三張表天生依附在一場賽事底下，沒有脫離賽事單獨檢視或編輯
的使用情境。**進球者／掛牌者／出賽者的球員必須是這場賽事其中一支所屬球隊底下的球員**
（`AdminMatchesRepository.ResolveMatchPlayerAsync`）——這同時是資料正確性檢查，也順帶把球員限制
在已經通過列級授權的球隊範圍內，不需要對球員另開一次 `TeamRowScope` 檢查。**`player_season_stats`
（球員賽季數據）本輪不做**：規劃書 C2 原文「可手動輸入或由賽事自動彙總」是兩種都合法的設計，
不是 C4 的欄位（是 C2 的），且没有自動彙總機制時另開一組手動輸入端點會製造「跟 `match_goals`／
`match_cards` 兩份資料源各自維護、容易失準」的風險——建議下一輪從 `match_goals`／`match_cards`
自動彙總，而不是另開手動輸入端點，已列入回報。

### 公開唯讀

`GET /api/v1/{club}/schedule`（既有 `Features/Schedule/MatchesEndpoints.cs`）**完全未改動**，向後
相容；寫入後呼叫既有 `IQueryCache.InvalidateAsync("schedule", clubCode)`（同一個快取實體，跟
`AdminTeamsRepository` 對 `teams` 實體的既有做法一致）。**積分榜沒有新增公開端點**——規劃書
§3.13 與首頁區塊都沒有明確要求獨立的積分榜公開 API（球隊詳情頁面若要顯示積分榜，屬於前台頁面
S1-15／S1-19 那一輪的範圍，這裡只確保後台資料已經備妥），暫不新增，需要時再依實際頁面需求評估
要不要加、要不要接快取。

### 改了哪些檔案

新增：
- `Security/TeamRowScope.cs`／`IAdminTeamRowScopeResolver.cs`／`AdminTeamRowScopeResolver.cs`
- `Features/AdminMatches/`（`AdminMatchDtos.cs`／`AdminMatchExceptions.cs`／
  `AdminMatchesRepository.cs`／`AdminMatchesEndpoints.cs`）
- `Features/AdminStandings/`（`AdminStandingDtos.cs`／`AdminStandingExceptions.cs`／
  `AdminStandingsRepository.cs`／`AdminStandingsEndpoints.cs`）
- `Tcrfc.Api.Tests/AdminMatchesAndStandingsTests.cs`（18 項）

修改：
- `Features/AdminTeams/AdminTeamsRepository.cs`／`AdminTeamsEndpoints.cs`（列級授權）
- `Features/AdminPlayers/AdminPlayersRepository.cs`／`AdminPlayersEndpoints.cs`（列級授權）
- `Features/AdminStaff/AdminStaffRepository.cs`／`AdminStaffEndpoints.cs`（列級授權）
- `Common/ApiExceptionHandler.cs`（新增例外對照）
- `Program.cs`（DI 註冊、路由掛載）
- `Tcrfc.Api.Tests/ArchitectureTests.cs`（`TeamRowScope` 納入型別層強制掃描）
- `db/seed/generate-club-seed-sql.py`（`team.match.*`／`team.standing.*` 權限碼、角色指派、
  `academy_program` 補回 C1–C3、新增 `academy.manager@tcrfc.test` 測試帳號）

### 契約變更

- 新增 8 個權限碼（`team.match.*` 四個、`team.standing.*` 四個），DML 已套用到本機 `tcrfc_club_dev`
  （`./db/seed/apply-seed.sh`）。
- `academy_program` 角色新增 7 個權限碼指派（`team.team.*`／`team.player.*`／`team.staff.*`／
  `team.match.*`，皆為 `academy_only`）。
- 新增測試帳號 `academy.manager@tcrfc.test`（密碼 `ContentEditor@123`，沿用既有雜湊）——**只授權
  `bw`**，因為 `tcrfc` 目前只有 `D1`（`first_team`），沒有任何 `academy` 球隊可測。
- **未動 `db/club-schema.sql`、未加 migration**——全部是既有欄位（`matches.match_no`／
  `original_match_on`／`original_kickoff` 皆已由 `S1-7a`／既有 DDL 提供）與 DML。

### 測試結果

`Tcrfc.Api.Tests/AdminMatchesAndStandingsTests.cs` 新增 18 項：401／403／唯讀角色擋建立、CRUD
成功案例（建立／取得／更新／刪除、硬刪除後 404）、狀態值域與對手必填驗證、延賽三種情境（缺原定
日期擋下、非延賽夾原定日期擋下、合法延賽成功）、場次編號同季同聯賽唯一（含更新排除自己不誤判）、
**`academy_only` 列級授權**（學院梯隊成功、一線隊 403、跨梯隊混合整筆擋下、既有一線隊賽事無法
修改／刪除、防止把既有學院賽事改指派到一線隊逃脫範圍）、**`own_teams` 列級授權**（用
`WithTemporaryScopeTypeAsync` 直接改一筆既有 `role_permissions.scope_type` 示範機制本身：
未授權前擋下、授權後成功、授權到期後視同未授權再度擋下，測完還原）、CSV 匯入成功案例、CSV 整批
驗證（一列有錯整批不寫入，含行號核對）、CSV 場次編號檔案內重複、CSV 列級授權（`academy_only`
帳號匯入含一線隊代號的 CSV，回報列級授權錯誤而非其他錯誤）、積分榜 CRUD、積分榜 CSV 整季替換、
積分榜 CSV 混雜賽季代碼擋下、公開賽程端點向後相容。

```
dotnet test Tcrfc.Api.Tests --filter "FullyQualifiedName~AdminMatchesAndStandingsTests" --no-build
已通過! - 失敗: 0，通過: 18，總計: 18（連跑 10 次，每次都是 0 失敗）

dotnet test Tcrfc.Api.Tests --no-build
已通過! - 失敗: 0，通過: 341，總計: 341（既有 323 ＋本輪 18）

dotnet test Tcrfc.Api.Tests --filter "FullyQualifiedName~ArchitectureTests" --no-build
已通過! - 失敗: 0，通過: 1

dotnet ef migrations has-pending-model-changes --context ClubDbContext
No changes have been made to the model since the last migration.
```

跑前跑後各執行一次 `./db/seed/reset-admin-accounts.sh`（第一次全套測試撞到 3 項
`AdminAuthTests` 失敗——`clean.login@tcrfc.test` 的密碼／2FA 狀態被同時進行的前端端對端驗收
弄髒，重設後全數轉綠，跟本輪程式碼改動無關）。

### 🔴 綱要缺口或待裁決（規劃書／docs/12 答不出來，回報請人工裁決）

1. **`standings` 沒有 `team_id` 可以做列級授權**——見上方「為什麼積分榜不套列級授權」。若之後
   要讓學院管理者維護學院賽事積分榜，需要加一個可為空的 `team_id` 外鍵（未動手加）。
2. **`matches.status` 值域：`§3.13` 有「取消」、`§4.3 C4` 沒有**——本輪照 C4（欄位定義的權威
   章節）為準，只定案 `scheduled`／`live`／`played`／`postponed` 四個值，不含「取消」。若確認
   需要「取消」語意（跟延賽不同——延賽是改期，取消是不會再打），需要先補規格再加值域，本輪
   沒有自創。
3. **`player_season_stats`（球員賽季數據）本輪未實作**——規劃書 C2 允許「手動輸入或由賽事自動
   彙總」兩種設計，本輪判斷手動輸入端點會跟 `match_goals`／`match_cards` 兩份資料源不同步，
   建議下一輪改做「從 `match_goals`／`match_cards` 自動彙總」，不是另開手動 CRUD。
4. **docs/12b §7.1／§7.4 對 `own_clubs` 這個 `scope_type` 值的收錄不一致**——§7.1 明文加值、
   §7.4 對照表沒收錄，本輪已在程式碼層修正（視同 `all`），建議 `system-analyst` 之後把 §7.4
   的表格也補上這個值，避免下一個人重新調查一次同樣的事。
5. **`own_teams` 這個 `scope_type` 目前沒有任何角色的任何權限碼真的採用**——規劃書 §7.4 把它
   綁在尚未建置的 L 行事曆模組（「賽事事件」「梯隊賽事」兩格）。本輪的測試因此用直接改
   `role_permissions` 示範機制本身（測完還原），機制已經可用，等 L 模組（`S1-11`）真的需要時
   直接指派即可，不需要再改程式碼。

---

## S1-8 續作：E-52 自動化防呆／`academy_program` 補 `team.competition.view`／「我能寫哪些球隊」端點（2026-09-24，`backend-engineer`）

三件事，全部回應 `apps/admin` 前端實走 S1-8 時回報的落差（見 `apps/admin/README.md`「意外發現的
權限授予缺口」「發現的權限授予缺口」「已知的 API 缺口彙整」第 14 點）。**未動 `db/club-schema.sql`、
未加 migration**；`dotnet ef migrations has-pending-model-changes` 綠燈。

### 1. `docs/18-work-errors.md` `E-52` 的自動化防呆

`AdminClubAuthorizer`／`AdminSystemAuthorizer` 的訊息本身在前一輪已經改掉（見 `E-52` 記錄），但
**改掉兩個已知案例不等於同一類錯不會再犯**——`ApiExceptionHandler` 把 400／403／409 例外的
`.Message` 原樣送進 `ProblemDetails.Detail`，任何一處新的 `throw new XxxException($"...")` 只要
不小心內插了權限碼或 docs/06 §1 禁用的技術詞，畫面上就會重演 `E-52`。新增兩支測試，互補：

- **`Tcrfc.Api.Tests/UserFacingMessageContentTests.cs`**（靜態掃描）：把 `apps/api` 組成 Roslyn
  `CSharpCompilation`，先用 regex 讀 `ApiExceptionHandler.cs` 的 switch 排版慣例抽出「哪些例外型別
  對應 400/403/409」（不是另外維護一份清單，型別清單跟例外處理器脫鉤是 `E-28`／`E-34` 的既有教訓），
  再對這些型別分「直通型」（`AdminForbiddenException(string reason) : Exception(reason)` 這種
  訊息完全由呼叫端決定）與「烤製型」（`ArticleSlugConflictException(string slug) : ...($"...")`
  這種訊息模板刻在類別裡）分頭掃描：直通型掃**呼叫端**引數，烤製型掃**類別宣告自己的基底建構式**
  引數。掃描內容比對「權限碼形狀」正則（三段小寫句點分隔）、docs/06 §1 禁用技術詞、
  `x.Code`／`x.PermissionCode` 成員存取（看語意型別是不是 `Permission` 相關，不看識別字名稱）、
  以及「字串集合參數的內容而非筆數被印進訊息」。完整的涵蓋範圍、邊界、以及寫這支測試時走過的
  彎路（第一版對所有目標型別都掃呼叫端，對 `AdminRoleSysadminOnlyPermissionException` 修好之後
  仍然誤判——因為呼叫端傳的引數內容源頭確實是權限碼，但那個型別內部只印筆數，這是安全的；
  拆成「直通型看呼叫端、烤製型看類別自己的模板」才同時消除誤判又保留抓得到真違規的能力），全部寫在
  該檔案的類別 XML doc 裡，**不重複貼在這裡**。
- **`Tcrfc.Api.Tests/UserFacingMessageHttpContentTests.cs`**（代表性端點實打）：真正的 HTTP 管線＋
  真正的 `tcrfc_club_dev`，對四個已知會回傳 400／403 的路徑直接檢查回應本文——
  ① `AdminClubAuthorizer` 的權限碼分支（`viewer@tcrfc.test` 打 `POST /admin/tcrfc/news` 缺
  `content.article.create`）② `AdminSystemAuthorizer` 的權限碼分支（`content.editor@tcrfc.test`
  打 `POST /admin/accounts` 缺 `system.account.create`）③ `AdminRoleValidationException`「找不到
  權限碼」④ `AdminRoleSysadminOnlyPermissionException`。兩支測試互補的理由與各自的邊界，見前者
  類別 XML doc「涵蓋範圍怎麼確認夠廣」一節。

**紅綠驗證過程（依 `docs/18` `E-39` 的教訓，用真實 bug 本身當反例，不是隨便塞錯值）**：把
`AdminClubAuthorizer.cs` 的訊息暫時改回 `E-52` 原始寫法
（`$"你的角色沒有「{permissionCode}」這項操作的權限。"`，工作目錄乾淨、只改這一個既有追蹤檔案，
測完 `git checkout --` 精準還原這一個檔案，沒有動到當時同時在跑的其他並行工作的未提交異動），
確認兩支測試皆變紅（靜態掃描報出確切的檔名行號；HTTP 測試回應本文真的印出
`team.competition.view`／`content.article.create` 字樣），還原後兩支再度全綠。

**過程中意外發現並修正的兩個既有洞**（`S1-3`／`S1-8` 就存在，不是本輪新增的程式碼，寫測試時被
靜態掃描抓到，不是先看到畫面才發現）：
- `AdminRoleExceptions.cs`：`AdminRoleSysadminOnlyPermissionException` 把整份被拒絕的權限碼清單
  逐字接進訊息（`{string.Join("、", codes)}`）——改成只回報筆數（`{codes.Count}`）。
- `AdminRolesRepository.cs`：`ReplacePermissionsAsync` 找不到權限碼時，把查無資料的代碼清單逐字
  接進訊息——同樣改成只回報筆數。

**涵蓋範圍與邊界（誠實版，完整說明見兩份測試檔各自的 XML doc）**：靜態掃描涵蓋**全部**
400/403/409 例外的原始碼路徑（不受目前有沒有測試命中影響），但只能追蹤同一個方法內的區域變數
宣告，看不到跨方法呼叫的資料流、看不到執行期才決定的內容字串，「識別字名稱像權限碼」是啟發式不是
形式證明。HTTP 整合測試證明**四個具體已知路徑**的實際輸出經過完整 JSON 序列化管線後乾淨，但不像
靜態掃描那樣涵蓋「還沒被任何測試打到」的分支。兩者互補，不是其中一種就足夠——這正是刻意同時要求
兩種測試手法的理由。**沒有做**：沒有對 `apps/web`（前台，不是後台，本來就不會回傳這種內部例外
訊息）做同等掃描；沒有掃 `Tcrfc.Api.Tests` 自己（測試程式碼本身合法出現任何字樣）。

### 2. `academy_program` 補 `team.competition.view`

前端回報：`academy.manager@tcrfc.test`（`academy_program` 角色）完全無法開啟 C4 賽事新增／編輯頁
——賽季是建立賽事的必填欄位，賽季下拉需要 `GET /admin/{club}/seasons`（權限碼
`team.competition.view`），但 `academy_program` 只有 `team.match.*`，沒有任何 `team.competition.*`。
核對主站規劃書 §6 權限矩陣「球隊／賽事」欄（行 1609）：學院／課程管理是「學院梯隊」——這是**唯讀
前提**（讀取賽事分類主檔是這個角色操作學院賽事的必要條件，不是額外授予的寫入權限），矩陣沒有給
這個角色任何 `Competition` 的寫入格。`db/seed/generate-club-seed-sql.py` `ROLE_PERMISSIONS` 新增
一筆 `("academy_program", ["team.competition.view"], "all")`——**`scope_type` 用 `all` 不是
`academy_only`**：`competitions` 沒有 `team_id`，`TeamRowScope` 對這張表根本不生效（跟「為什麼
積分榜不套列級授權」同一個理由），跟既有 `content_editor`／`viewer`／`business_sponsorship`／
`pr_media` 的 `team.competition.view` 一律給 `all` 是同一個模式，不是特例。已用
`./db/seed/apply-seed.sh` 套用到本機 `tcrfc_club_dev` 並用 SQL 查詢核對
（`role_permissions` 多一筆 `academy_program`／`team.competition.view`／`all`）。

### 3. 「我能寫哪些球隊」端點（`GET /api/v1/admin/{club}/teams/writable?module=team|player|staff|match`）

前端回報（`apps/admin/README.md`「已知的 API 缺口彙整」第 14 點）：C1–C4 的「所屬球隊／參賽球隊」
下拉選單目前列出整個俱樂部全部球隊，不分一線隊／學院／個別授權——`S1-8` 的列級授權
（`academy_only`／`own_teams`）只接上了**寫入端點**，前端選了範圍外的球隊要等按下儲存才會被
403 擋下。新增 `Features/AdminTeams/AdminTeamsEndpoints.cs` 的
`GET /api/v1/admin/{club}/teams/writable?module=team|player|staff|match`，回傳
`IReadOnlyList<AdminWritableTeamDto>`（`Id`／`Code`／`Type`／`NameZh`／`NameEn`）。

**契約（給前端用）**：

| 項目 | 內容 |
|---|---|
| 路徑 | `GET /api/v1/admin/{club}/teams/writable` |
| 查詢參數 | `module`（必填，字串）：`team`／`player`／`staff`／`match` 其中之一，對應 C1–C4 四個模組 |
| 權限碼（存取這支端點本身） | 依 `module` 對應的**檢視碼**：`team.team.view`／`team.player.view`／`team.staff.view`／`team.match.view`——只要看得到該模組就能問「我能寫哪些」，不需要先有寫入權限 |
| 回應 | `200 OK`，`AdminWritableTeamDto[]`：**已經是收斂過的清單**，只列出呼叫端依 `TeamRowScope` 真的能寫的球隊，不是「全部球隊 + canWrite 旗標」 |
| 400 | `module` 缺漏或不是上述四個值之一（訊息用中文「球隊／球員／教練／賽事」，不回顯呼叫端傳入的原始字串——見程式碼註解，這是介面文字，跟 `E-52` 同一條規則） |
| 401／403／404 | 同既有俱樂部範圍端點慣例（未登入／該俱樂部無授權或無對應檢視權限／俱樂部不存在） |

**取捨（獨立端點 vs 既有清單加 `canWrite` 旗標，擇一，已選前者）**：前端要的是「選單只列我能寫的」
（收斂選項），不是「列出全部、每筆自己附註能不能寫」——選單元件直接綁這支端點的回應就是完整選項
清單，不需要在畫面上再過濾一次；獨立端點也完全不動既有 `GET /api/v1/admin/{club}/teams` 的回應
形狀，球隊管理列表頁等既有畫面與既有測試零風險。代價是多一個 GET 端點要維護，但邏輯是純讀取＋既有
`TeamRowScope.Allows` 過濾，維護成本低。**用 `.update` 而不是 `.create` 當寫入碼去解析
`TeamRowScope`**：這支端點回答的是「這支**既有**球隊我能不能碰」（對應 `TeamRowScope.Allows`），
跟 C1「建立全新球隊」用的 `AllowsCreatingTeamOfType`（連 `teamId` 都還不存在）是不同問題。目前
種子資料裡同一個角色的 `.create`／`.update` 一律共用同一個 `scope_type`（`ROLE_PERMISSIONS` 同一個
tuple 裡的權限碼共用同一個 `scope_type`），但這是現況慣例不是保證，日後如果角色權限拆到「能新增
但不能改」這種更細的組合，需要重新檢視這裡該用哪個碼。

**測試**（`Tcrfc.Api.Tests/AdminTeamsWritableEndpointTests.cs`，5 項，用 `bw` 俱樂部——`tcrfc` 只有
`D1` 一支球隊，示範不出「收斂成部分球隊」的過濾效果，`bw` 有 `BW1`／`BW-U15`／`BW-U12` 三支）：
未登入 401；`module` 缺漏或不支援 400；系統管理員看得到 `bw` 全部三支球隊；`academy_program`
（`academy.manager@tcrfc.test`）只看得到 `BW-U15`／`BW-U12`，看不到 `BW1`；`own_teams`（直接改
`partner_club_manager`／`team.match.update` 的 `scope_type` 示範機制本身，測完還原）授權前清單為
空、授權 `BW-U15` 後清單只有 `BW-U15`、授權到期後清單再度變空。

### 測試結果

```
$ dotnet test Tcrfc.Api.Tests    # CLUB_SQL_CONNECTION_STRING 指向本機 tcrfc_club_dev
已通過! - 失敗: 0，通過: 367，總計: 367

$ dotnet test Tcrfc.Api.Tests --filter "FullyQualifiedName~ArchitectureTests" --no-build
已通過! - 失敗: 0，通過: 1

$ dotnet ef migrations has-pending-model-changes --context ClubDbContext
No changes have been made to the model since the last migration.
```

跑前跑後各執行一次 `./db/seed/reset-admin-accounts.sh`。

---

## S1-7b：主站規劃書 v3.14 落到程式——`matches.status` 補「取消」／`banners` 草稿發布／開放 Hero 影片上傳（2026-09-24，`backend-engineer`）

### 背景

`system-analyst` 已於 commit `2b439bd` 把 v3.14 的兩項規格異動（`matches.status` 補五值、
`banners` 新增 `status`）同步進 `db/club-schema.sql`／`docs/12`，但**只改了 DDL 與文件，沒有動
`apps/api`**（見 `docs/12` §12 第 35 點檔頭明講「應用層尚未跟上，這是後端待辦」）。本輪把這兩項
異動落到程式，並依派工指示一併開放 Hero 輪播影片上傳（規劃書 §4.2 B3「圖／影片」原本就要求，
S1-6／S1-7a 兩輪都因為「影片格式、大小上限、是否轉碼尚待裁決」而只做圖片）。

### 1. Migration：`AlignSchemaV314`

新增一支 EF Core migration（`Data/Migrations/20260924113455_AlignSchemaV314.cs`），內容：

1. `AddColumn`：`banners.status`（`nvarchar(16) NOT NULL DEFAULT 'draft'`）。
2. 手寫 SQL（本專案既有慣例：CHECK 約束完全不用 `HasCheckConstraint` 建模，見
   `AlignSchemaS17a` 檔頭）：
   - `ALTER TABLE banners ADD CONSTRAINT CK_banners_status CHECK (status IN ('draft','published'));`
   - `ALTER TABLE matches ADD CONSTRAINT CK_matches_status CHECK (status IN ('scheduled','live','played','postponed','cancelled'));`

**既有資料檢查（Up() 是否需要搭配 DML）**：套用前查了 `tcrfc_club_dev`——

```sql
SELECT status, COUNT(*) FROM matches GROUP BY status;
-- played 21、scheduled 21，全部落在五值範圍內
SELECT COUNT(*) FROM banners;  -- 0
```

`matches.status` 兩種既有值皆合法、`banners` 目前 0 筆，**兩張表都不需要任何 DML 轉態**，這支
migration 純粹是 DDL 變更。`matches.status` 欄位本身早就存在（`db/club-schema.sql` 原文：
「`status` 本身沒有 CHECK 約束」），這支 migration 只是第一次替它加上 CHECK，不是收斂既有
CHECK（跟 `AlignSchemaS17a` 收斂 7 張表既有 CHECK、要先動態查詢系統產生名稱再 DROP 的情況不同，
`matches.status` 這裡直接 `ADD CONSTRAINT` 即可）。

**驗收（依 docs/20-cicd.md §5）**：

```
dotnet ef migrations add Probe --context ClubDbContext -o Data/Migrations
# Up()／Down() 皆為空方法主體 → 基準沒有偏移
dotnet ef migrations remove --context ClubDbContext
# 這支從沒套用過，remove 只刪檔案，不會對任何資料庫執行 Down()（E-46 教訓）
```

🔴 **升級路徑驗證（用完即丟的資料庫）**：`git show 2b439bd~1:db/club-schema.sql`（v3.14 DDL
異動前一版）取出「上一版」DDL，把其中的 `json` 型別字面替換成 `nvarchar(max)`（本機 SQL Server
2022 容器的既知限制，`docs/12` §1.4 第 2 點），建出 `tcrfc_club_probe`（147 張表，含
`__EFMigrationsHistory`），手動插入既有 5 支 migration 的歷史紀錄（標記為已套用，不重跑），
再對這個資料庫跑 `dotnet ef database update`——**只會套用 `AlignSchemaV314`**，成功套用
（147→148 張表），實測兩個 CHECK 約束真的擋得下非法值（`banners.status='bogus'` 直接被
`CK_banners_status` 拒絕，`sys.check_constraints` 查證兩條約束定義字面正確）。驗完
`DROP DATABASE tcrfc_club_probe`。

**本機 `tcrfc_club_dev`**：依任務指示套用（不是 `remove`）。套用前後核對 `matches`（42 筆）／
`banners`（0 筆）筆數皆未變動；`dotnet ef migrations has-pending-model-changes` 套用後回報
「No changes have been made to the model since the last migration.」。

### 2. `matches.status` 補「取消」

`Features/AdminMatches/AdminMatchesRepository.cs`：

- `AllowedStatuses`：四值 → 五值（`scheduled`／`live`／`played`／`postponed`／`cancelled`）。
- `StatusZhLabels`：新增 `["取消"] = "cancelled"`（CSV 匯入與後台中文對照表共用同一份字典）。
- `ValidateStatus`／CSV 逐列驗證的錯誤訊息同步補上「取消」。
- **「取消」不受 `ValidatePostponedFields` 的原定時間規則約束**：這個方法只特判
  `status == "postponed"`，`cancelled` 自動落入 else 分支（跟 `scheduled`／`live`／`played`
  同一種行為）——取消是「不會再打」，延賽是「改期」，兩者語意不同，不需要額外程式碼即可正確處理，
  只是在 `AdminMatchDtos.cs` 的值域說明補上這條語意區分的文字。

**公開端點 `GET /api/v1/{club}/schedule`（`Features/Schedule/MatchesRepository.cs`）完全未改**：
這支端點本來就是 `status` 的純值傳遞（`m.status AS Status`），沒有任何 enum 對照或值域限制，
`cancelled` 會自動隨現有欄位流過去，不需要改一行程式碼。

**前台 `apps/web/app/utils/schedule.ts` 的 `MATCH_STATUS_MAP` 檢查結果：已有 `cancelled`，
本輪未改動這個檔案**——`S0-9l`（2026-09-24 較早的一輪）在補「延賽」顯示邏輯時，已經把
`cancelled`／`postponed`／`live` 一併預留進這份對照表（`code: 'cancelled', label: '取消',
schemaOrg: 'https://schema.org/EventCancelled'`），當時的檔頭註解就寫明「這幾個字面值尚未有
真實資料可核對，沿用既有 CSS class 與命名風格推斷」。CSS（`status-pill--cancelled`，
`apps/web/app/pages/zh/schedule.vue`）也已存在。本輪在種子資料與後端寫入路徑補上真實的
`cancelled` 賽事後，這份**沿用既有對照表的既定行為**才第一次有真實資料可以核對——已用
`Tcrfc.Api.Tests` 建立一筆 `cancelled` 賽事驗證 API 回傳與 CSV 往返正確，但**沒有**另外對
`apps/web` 的頁面渲染做端對端驗證（那需要 Nuxt 頁面接上真實 API，屬於前台頁面開發階段
`S1-19` 的範圍，不在本輪任務邊界內）。

`apps/web` 的 `npm run lint:match-status`（`scripts/check-match-status.mjs`）執行結果：
**通過本輪相關的檢查**（`MATCH_STATUS_MAP` 涵蓋種子資料的所有字面值）。🔴 **但整體
`npm run lint` 會在 `lint:match-status` 這一步失敗**——與本輪改動無關的既有缺口：
`apps/api/Tcrfc.Api.Tests/ScheduleOriginalDateTests.cs`（`S0-9l` 新增，`831c211`）裡有一段
`INSERT INTO matches (...)` 的原始 SQL 測試資料，從未被登記進
`KNOWN_MATCHES_STATUS_WRITE_SOURCES`，觸發了檢查腳本的「發現未登記的 matches 寫入路徑」防呆。
已用 `git stash` 確認**這個失敗在本次任何改動之前就存在**（清空工作目錄跑同一支腳本，
同樣的失敗訊息）。本輪未修這個缺口（不在 matches 狀態值域或影片上傳的任務範圍內），已列入
下方「回報」請人工裁決由誰接手登記。

### 3. `banners` 草稿／發布

新增 `banners.status`（`draft`／`published`，預設 `draft`）落到程式，**沿用既有
`content.banner.update` 權限碼，未新增權限碼**（任務指示明文）：

| 方法與路徑 | 說明 |
|---|---|
| `POST /api/v1/admin/{club}/banners/{id}/publish` | 發布：`status` → `published`。冪等，已發布再打一次不報錯 |
| `POST /api/v1/admin/{club}/banners/{id}/unpublish` | 改回草稿：`status` → `draft`。同上，冪等 |

**設計取捨**：banners 沒有樂觀並行控制、沒有像 `Article`／`Page` 那種「只有草稿或排程中才能
發布」的狀態機限制——輪播是單一表單的設定型內容，發布後仍可以隨時改回草稿再重新發布，沒有
複雜的轉換規則需要保護，比照 `AdminFaqCategoriesRepository` 對 `IsEnabled` 這類簡單開關的既有
寬鬆處理，不是比照 `Article` 的狀態機。新建立一律是 `draft`（不接受呼叫端指定初始狀態）。

**公開端點 `GET /api/v1/{club}/banners`（`Features/Home/HomeRepository.ListBannersAsync`）**：
SQL 新增 `AND b.status = 'published'`，**與既有的 `start_at`／`end_at` 時間窗條件皆為
`AND`（兩者都要成立）**。用等於比對單一允許值（白名單），不是排除某個值的黑名單寫法
（`docs/18-work-errors.md` `E-50` 教訓：個資／可見度判斷一律白名單）。時間比較沿用既有
`SYSUTCDATETIME()`（資料庫時鐘），沒有 `E-48` 那種「寫入用應用程式時鐘、讀取用資料庫時鐘」的
坑——`start_at`／`end_at` 是後台人員自己選定的日期，不是「現在」這個時間點本身。

### 4. 開放 Hero 影片上傳

**規則定案與理由見 [`docs/17-deployment.md`](../../docs/17-deployment.md) §6「Hero 輪播影片
上傳」**（只收 MP4／H.264／AAC、上限 50 MB、伺服器端不轉碼、以 `ftyp` box 驗證容器格式）。
新增 `Videos/` 目錄，架構逐字比照既有 `Images/` 目錄的分工（但**不做任何轉檔／衍生檔**，
一支影片只有一個物件）：

- `IVideoStorageService`／`BlobVideoStorageService`／`UnavailableVideoStorageService`：
  跟圖片共用同一個 Azure Storage 帳號、**獨立容器**（`AZURE_BLOB_CONTAINER_VIDEOS`，預設
  `videos`），用 **keyed DI**（`[FromKeyedServices("videos")]`）注入獨立於圖片的
  `BlobContainerClient`，避免跟圖片用的「未具名」單例互相覆蓋。
- `VideoValidator`：純驗證邏輯（大小、`ftyp` box 檔頭），不含任何 I/O。
- `VideoUploadOptions`：`MaxUploadBytes = 50 MB`、`Extension = ".mp4"`、
  `ContentType = "video/mp4"`——唯一來源，不得在別處重複寫死。
- `VideoProcessingExceptions`：`EmptyVideoException`／`VideoTooLargeException`／
  `UnsupportedVideoFormatException`，比照 `ImageProcessingException` 家族由
  `ApiExceptionHandler` 統一轉 400。

**契約**（`banners`，`AdminBannerDtos.cs`／`AdminBannersRepository.cs`／
`AdminBannersEndpoints.cs`）：

- `CreateBannerRequest.MediaType` 開放 `video`（原本 S1-7a 只允許 `image`，送 `video` 一律 400，
  本輪解除這個限制）。`AdminBannersRepository.ValidateMediaType` 改為 `internal static`，讓
  `AdminBannersEndpoints` 能在上傳任何檔案**之前**先驗證這個欄位（fail fast，避免對一個註定
  會被拒絕的請求白白做圖片／影片上傳）。
- multipart 契約新增 `video` 欄位（`AdminBannerRequestForm.ReadAsync` 回傳型別從
  `(T Payload, IFormFile? File)` 改為 `(T Payload, IFormFile? File, IFormFile? VideoFile)`）：
  - `mediaType="image"`：只需要 `file`（輪播圖），`video` 欄位不可帶（帶了 400）。
  - `mediaType="video"`：`file` 仍是必填（**必須搭配海報圖，即既有 `image_key`**，
    `<video poster>` 用），`video` 欄位建立時必填；更新時省略＝維持既有影片（前提是原本
    就是 `video` 模式且已有影片，否則 400）。
- `UploadSlotPolicy` 新增 `"video"` 插槽（`banners.video_key`）。
- 補償刪除（E-47）：建立／更新失敗時，圖片與影片（若有上傳）都用 `CancellationToken.None`
  補償刪除，不沿用可能已取消的請求 token。
- **`UpdateAsync` 的影片鍵推導邏輯**（比圖片複雜，因為多了「模式切換」）：
  - `video` 模式且有新影片 → 換成新影片，刪除舊影片物件。
  - `video` 模式且沒有新影片 → 維持既有影片；既有值也是 `null`（例如剛從 `image` 切過來卻
    沒上傳影片）→ 400。
  - `image` 模式（含從 `video` 切回來）→ 影片鍵清空並刪除舊影片物件（跟圖片「換圖刪舊圖」
    同一種善後邏輯）。
- **Kestrel 請求主體上限**（`Program.cs`）：因為影片模式在同一次請求同時送海報圖與影片，
  上限已改為 `ImageUploadOptions.MaxUploadBytes + VideoUploadOptions.MaxUploadBytes + 1 MB`，
  不再只算圖片那組數字。

**公開端點**：`GET /api/v1/{club}/banners` 已有 `mediaType`／`videoKey` 欄位（S1-7a 就已預先
補上，本輪沒有再改 DTO，只是 `videoKey` 從此真的可能有值）。

### 5. 測試

`Tcrfc.Api.Tests/AdminBannersAndHomeSectionsTests.cs`：

- 修正既有 `Banner_圖片寬高由上傳結果自動填入_alt雙語_media_type送video回400`（video 現在
  允許，這個測試名稱與內容已過期）：移除「送 video 一律 400」斷言，改名為
  `Banner_圖片寬高由上傳結果自動填入_alt雙語`；補上先呼叫 `/publish` 才能在公開端點看到的步驟
  （新建立的輪播預設 `draft`，這是本輪引入的行為變化，既有測試原本假設「建立後立刻公開可見」）。
- `Public_Banners_只回上架期間內的輪播` 依賴的 `CreateBannerAsync` 工具方法補上 `publish`
  參數（預設 `true`，維持既有測試對「已發布輪播」情境的假設；需要測草稿行為本身的新測試改傳
  `false`）。
- 新增 7 項：草稿不出現在公開端點／發布後出現／改回草稿後又不出現（含發布冪等性）、
  發布與改回草稿的授權（檢視者 403、不存在的輪播 404）、影片模式建立成功且公開端點吐出
  `videoKey`、影片模式缺檔案 400／圖片模式送影片檔案 400、影片格式不支援 400／超過大小上限
  400、影片切回圖片清空影片鍵、再切回影片模式須重新上傳。

`Tcrfc.Api.Tests/AdminMatchesAndStandingsTests.cs`：新增 2 項——狀態為「取消」建立成功且不受
原定日期規則約束（含「取消狀態填原定日期」400）、CSV 匯入納入一列「取消」（原本測試只涵蓋
「未開始」「已結束」兩種，補上第三種涵蓋新值域）。

新增測試工具：`Tcrfc.Api.Tests/TestVideos.cs`（`SmallValidMp4`／`FakeVideoBytes`／
`OversizedBytes`，逐字比照既有 `TestImages.cs` 的既有原則——全部在記憶體現產，不讀外部檔案）；
`AdminArticleMultipart.Build` 新增選填的 `videoBytes`／`videoFileName`／`videoContentType`
參數（省略即維持既有行為，不影響 `AdminNews`／`AdminPages`／`AdminTeams` 等其餘呼叫端）。

**測試結果**：

```
dotnet test Tcrfc.Api.Tests/Tcrfc.Api.Tests.csproj --filter "FullyQualifiedName~AdminBannersAndHomeSectionsTests|FullyQualifiedName~AdminMatchesAndStandingsTests" --no-build
已通過! - 失敗: 0，通過: 39，總計: 39（連跑 10 次，每次都是 0 失敗）

dotnet test Tcrfc.Api.Tests/Tcrfc.Api.Tests.csproj --no-build（全套，前後各執行一次
db/seed/reset-admin-accounts.sh）
已通過! - 失敗: 0，通過: 374，總計: 374

dotnet test Tcrfc.Api.Tests/Tcrfc.Api.Tests.csproj --filter "FullyQualifiedName~ArchitectureTests" --no-build
已通過! - 失敗: 0，通過: 1

dotnet ef migrations has-pending-model-changes --context ClubDbContext
No changes have been made to the model since the last migration.
```

`apps/web`：`npm run lint:node-version`／`lint:club-copy`／`lint:homepage-fidelity`／
`lint:eslint` 全部通過（0 errors）；`lint:match-status` 的失敗**已用 `git stash` 確認與本輪
改動無關**，見上方第 2 節說明。

### 契約變更（給 `apps/admin` 的人看）

1. `AdminBannerListItemDto`／`AdminBannerDetailDto` 新增**必填**欄位 `status`
   （`draft`／`published`）。分類管理畫面需要新增「發布」／「改回草稿」按鈕（分別打
   `POST .../banners/{id}/publish`／`.../unpublish`，沿用既有 `content.banner.update`
   權限碼，不需要新的權限檢查）。
2. `CreateBannerRequest`／`UpdateBannerRequest` 的 `MediaType` 現在真的接受 `video`。前端表單
   若要開放影片上傳，需要：素材種類切換為「影片」時顯示第二個檔案選擇欄位（multipart 欄位名
   `video`），送出時 `file` 欄位仍是必填（作為海報圖）。若第一版前端暫不做影片上傳 UI，
   維持只送 `mediaType: "image"` 即可，不受影響。
3. `matches` 的狀態下拉選單需要新增「取消」選項（值 `cancelled`），CSV 範本的狀態欄說明文字
   需要更新為「未開始／進行中／已結束／延賽／取消」。

### 回報（規劃書／既有機制答不出來，請人工裁決）

1. 🔴 **`apps/web/scripts/check-match-status.mjs` 的「未登記寫入路徑」防呆目前紅燈**：
   `apps/api/Tcrfc.Api.Tests/ScheduleOriginalDateTests.cs`（S0-9l 新增）有一段
   `INSERT INTO matches` 測試資料 SQL，從未被登記進該腳本的 `KNOWN_MATCHES_STATUS_WRITE_SOURCES`。
   已用 `git stash` 確認**這個失敗早於本輪任何改動就存在**，不是本輪引入的迴歸。修法很單純
   （把這個檔案路徑加進登記清單，確認寫入的 `status` 字面值——`postponed`——已在
   `MATCH_STATUS_MAP` 涵蓋），但不屬於「matches 狀態值域」或「影片上傳」這兩項任務範圍，
   交由人工決定由誰接手（可能是下一輪 `S0-9` 系列收尾，或另開一個小任務）。
2. **影片上傳只驗證容器格式，不驗證內部編碼是否真的是 H.264／AAC**——已寫進
   `docs/17-deployment.md` §6 與 `Videos/VideoProcessingExceptions.cs` 的程式註解，這是刻意的
   驗證邊界（見該節說明），此處列出是確保這個邊界被看到，不是待辦。

### 本次沒動的部分

- 沒有動 `apps/admin`（契約變更清單已列在上方，另有 agent 在改球隊選單，任務指示明文不得碰）。
- 沒有替 Hero 輪播影片做任何伺服器端轉碼、壓縮或格式轉換——依裁決結果，這是刻意不做。
- 沒有修改 `STATUS.md`、`docs/18-work-errors.md`（依任務指示由派工者收尾）。
- 沒有 commit。

---

## S1-9：`P1–P3` 課程／營隊項目／梯次／報名 ＋ 05 課程與活動公開讀取與報名送出（2026-09-25，`backend-engineer`）

主站規劃書 §4.4 P1–P3（P4 試訓管理不在本次範圍）。沿用既有架構：`AdminClubScope`／
`IAdminClubAuthorizer`、權限碼、`ApiExceptionHandler`、後台圖片欄位直傳（`programs.cover_key`）、
CSV 匯出（`Common/CsvUtils.cs`）。**沒有套用 `TeamRowScope`**——`programs`／`sessions`／
`registrations` 三張表都沒有 `team_id` 欄位，理由詳見
`Features/AdminPrograms/AdminProgramsRepository.cs` 檔頭。

### 綱要異動

`programs`／`sessions`／`registrations`／`trials` 四張表在此之前就已經是完整 DDL（`db/club-schema.sql`
「4.3 P 課程與活動」，S0 系列建的），EF 實體（`Data/EfEntities/TrainingProgram.cs`／`Session.cs`／
`Registration.cs`／`Trial.cs`）與 `ClubDbContext` 對應也早就 scaffold 好，本輪**不需要新增資料表**。
唯一的綱要變更：`programs.status`／`programs.program_type`／`sessions.status` 三欄早就存在，但從來
沒有被任何 CHECK 約束過（跟 `AlignSchemaV314` 補 `matches.status` 約束是同一種落差，見
`docs/12-database-schema.md` §12 第 36 點）。套用前查證 `tcrfc_club_dev` 這兩張表皆為 0 筆，純
DDL 變更，不搭配任何 DML 轉態：

```
migration: 20260925031444_AlignSchemaS19Programs
  ALTER TABLE programs ADD CONSTRAINT CK_programs_status
    CHECK (status IN ('draft','published'));
  ALTER TABLE programs ADD CONSTRAINT CK_programs_program_type
    CHECK (program_type IN ('children_training','summer_camp','winter_camp','specialist_training','school_community'));
  ALTER TABLE sessions ADD CONSTRAINT CK_sessions_status
    CHECK (status IN (N'開放',N'額滿',N'候補',N'已結束'));
```

`sessions.status` 直接沿用規劃書 P2（行 1098）的中文字面，比照 `CK_registrations_status`（早就是
中文值）的既有先例，不翻成英文代碼。

### 後台端點（新增檔案 `Features/AdminPrograms`／`AdminSessions`／`AdminRegistrations`）

| 方法與路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET /api/v1/admin/{club}/programs` | `program.item.view` | 課程／營隊項目清單。`programType` 篩選 |
| `GET /api/v1/admin/{club}/programs/{id}` | `program.item.view` | 單筆詳情 |
| `POST /api/v1/admin/{club}/programs` | `program.item.create` | 建立，`multipart/form-data`（`payload`＋選填 `file` 封面圖） |
| `PUT /api/v1/admin/{club}/programs/{id}` | `program.item.update` | 更新，同上 multipart 契約 |
| `GET /api/v1/admin/{club}/program-sessions` | `program.session.view` | 梯次清單。`programId` 篩選 |
| `GET /api/v1/admin/{club}/program-sessions/{id}` | `program.session.view` | 單筆詳情 |
| `POST /api/v1/admin/{club}/program-sessions` | `program.session.create` | 建立，一般 JSON（沒有圖片欄位） |
| `PUT /api/v1/admin/{club}/program-sessions/{id}` | `program.session.update` | 更新 |
| `GET /api/v1/admin/{club}/registrations` | `program.registration.view` | 報名清單。`sessionId`／`status` 篩選 |
| `GET /api/v1/admin/{club}/registrations/{id}` | `program.registration.view` | 單筆詳情 |
| `GET /api/v1/admin/{club}/registrations/export` | `program.registration.export`（`is_restricted`） | CSV 匯出，`sessionId`／`status` 篩選同上 |
| `POST /api/v1/admin/{club}/registrations` | `program.registration.create` | 後台代填（電話／現場報名） |
| `PUT /api/v1/admin/{club}/registrations/{id}` | `program.registration.update` | 處理報名：確認／取消／轉梯次／候補／備註／學員資料整份覆寫 |

### 公開端點（新增檔案 `Features/Programs`，不需要登入）

| 方法與路徑 | 說明 |
|---|---|
| `GET /api/v1/{club}/programs` | 05 課程與活動列表卡片，只回 `status='published'`。`type`（`programType`）、`lang`、`page`、`pageSize` |
| `GET /api/v1/{club}/programs/{slug}` | 課程詳情：內容、教練團（**只有姓名，不含照片**，見下方說明）、合作夥伴、全部梯次 |
| `POST /api/v1/{club}/programs/sessions/{sessionId}/registrations` | 報名送出，回傳 `registrationNo`／`status`（`待確認`或`候補`） |

### 權限碼與角色指派（`db/seed/generate-club-seed-sql.py`）

新增 10 個權限碼：`program.item.view/create/update`、`program.session.view/create/update`、
`program.registration.view/create/update/export`（`export` 是 `is_restricted=1`）。`module_code=P`、
`domain=program`（獨立於 `team` 之外，矩陣把「球隊／賽事」與「課程／報名」列為兩個獨立欄位）。
依主站規劃書 §6 矩陣「課程／報名」欄逐列展開：系統管理員 ✔全；內容編輯／競技球隊管理／商務贊助／
檢視者 唯讀；**學院／課程管理 ✔全（含匯出）**；**客服／行政「報名處理」→ 只給
`program.registration.view/update`**（不給課程項目／梯次的建立編輯權，也不給匯出）；公關／媒體
矩陣是「—」，不指派；合作球隊管理 ✔自家課程（`own_clubs`，不含匯出，比照既有保守預設）；
翻譯人員本輪維持跟其餘模組一致不指派（「僅翻譯欄位」全系統目前沒有任何模組真的做出欄位級強制）。

新增兩個測試帳號：`customer.service@tcrfc.test`（`customer_service_admin`，僅 `tcrfc`，密碼
`ContentEditor@123`）、`pr.media@tcrfc.test`（`pr_media`，僅 `tcrfc`，同密碼）——理由與既有
`academy.manager@tcrfc.test`／`team.manager@tcrfc.test` 相同，見 `db/seed/generate-club-seed-sql.py`
對應段落。

### 名額控管（規劃書行 1097「額滿自動關閉、候補遞補提醒」）

`sessions.enrolled_count` 只由報名寫入路徑維護，後台建立／更新梯次的端點完全不接受呼叫端指定這個
欄位。「額滿自動關閉」用單一原子 SQL 陳述式完成，不是「先查再寫」：

```sql
UPDATE sessions
SET enrolled_count = enrolled_count + 1,
    status = CASE WHEN capacity IS NOT NULL AND enrolled_count + 1 >= capacity THEN N'額滿' ELSE status END,
    updated_at = SYSUTCDATETIME()
OUTPUT INSERTED.id
WHERE id = @SessionId AND status = N'開放' AND (capacity IS NULL OR enrolled_count < capacity)
```

只有梯次「目前正是開放中且還有名額」時才會真的更新到那一列（影響列數＝1）；梯次目前是「額滿」
「候補」（管理者手動設定，代表只收候補）或「已結束」（更早一步被擋下），或兩個訪客同時搶最後一個
名額，落敗的那一次呼叫影響列數是 0——這一次直接判定為「候補」，不佔用名額。單一 `UPDATE` 陳述式
本身就有隱含的列鎖定保護，不需要額外的重試或 `UPDLOCK`／`HOLDLOCK` 語法。這個判定**只單向收斂到
「額滿」**，不會反向把「額滿」自動打回「開放」，也不會自動把「候補」改回「開放」——規劃書只講
「額滿自動關閉」的單向語意，反向與「候補」都是人工判斷（後台可隨時手動改回）。後台代填報名
（`Features/AdminRegistrations`）與轉梯次也走同一組邏輯（原子 `UPDATE ... SET enrolled_count =
enrolled_count + @delta`），但**不做「額滿即拒絕」的關卡**——那是保護公開訪客的行為，後台操作者
是人，允許人工超額或把候補直接轉正。

### 肖像同意的既有防線沒有被繞過

05 課程詳情把教練團（`program_staff`）嵌進回應，但**刻意只回傳姓名，不含照片**——
`docs/12` §12 第 32 點與 S1-7a 已確認「全系統只有 `Features/Players`／`Features/Staff` 兩支公開
端點會依肖像同意白名單輸出球員／教練照片」，本端點若另外夾帶 `photo_key` 會繞過那道白名單、變成
第三個出口，故刻意不做。前台如需教練完整資料（含已同意的照片）應另外呼叫既有的
`GET /api/v1/{club}/staff`。

### 規劃書沒寫清楚、本輪自行判斷的地方

1. **前台報名流程的「Email／簡訊通知」未實作**（`docs/02-frontend-spec.md` 行 102／主站規劃書行
   389：「送出 → 產生報名編號 → Email／簡訊通知」）。`EmailLog.type` 值域是封閉的 9 個值（會員
   5＋商店 4，`docs/12` §12 第 23 點），課程報名的通知信不在其中；簡訊更是全系統從未建置過的
   通路（`docs/17-deployment.md` 沒有任何簡訊服務的整合紀錄）。這屬於「綱要與規劃書在這件事上
   沒有明確答案」，依任務指示停在這裡、不自行新增 `EmailLog.type` 值域或串接簡訊服務，回報供
   下一輪走 `docs/00-harness.md` §2.5 同步鏈裁決（要嘛新增通知型別與樣板，要嘛裁決課程報名不寄
   系統信）。「候補遞補提醒」同一個缺口，未實作。
2. **`Registration.health_declaration`（健康聲明）維持現行 DDL 的明文欄位，未加密、未做欄位級
   遮罩、未建立蒐集覈實或同意書留存流程**——`docs/12b-database-tables.md` §8 明文標注這欄「🔐
   建議，⚠️ 待法務確認」，但真正待確認的是《個資法》§6 特種個資的蒐集要件與保存期限，不是儲存
   方式本身（該檔案原文：「真正要確認的是能不能蒐集、要不要蒐集、保存多久，那在儲存方式之前」）。
   這層法務判斷超出本次任務邊界，依派工指示「不要自行擴大蒐集範圍」處理：欄位照現行 DDL 收（不
   新增欄位、不新增同意書上傳），可見範圍依 `program.registration.view` 權限碼控管（矩陣角色
   分佈見上方），CSV 匯出**刻意排除**這欄（資料最小化，見下一點）。
3. **CSV 匯出「Excel 匯出」的落地方式**：規劃書行 1090 寫「匯出 Excel：名單匯出（含分組欄位）」，
   本專案沒有任何 `.xlsx` 產生套件，比照既有 FAQ／賽程／積分榜三個模組的先例（`Common/CsvUtils.cs`
   檔頭），以 CSV 實作（Excel 可直接開啟）。匯出欄位**刻意不含健康聲明**——名單匯出的用途是人數
   控管與簽到，不需要醫療類個資；「簽到表列印」由前台／後台畫面直接把這份清單資料印出即可，
   後端不另外產生 PDF。
4. **`program.registration.export` 套用 `is_restricted=1`，但沒有另外實作「執行當下二次驗證」**
   ——`docs/12b` §7.5 承諾的二次驗證（重輸密碼或 2FA）全系統目前沒有任何模組真的做出來（`J2`
   角色管理只有資料層的旗標，`Security/PermissionChecker.cs` 也只做一般權限碼比對），本輪比照
   現狀，只掛旗標與基本權限檢查，不另外發明。
5. **`sessions.status`／`programs.status`／`programs.program_type` 三個值域用 CHECK 約束收斂**
   （見上方「綱要異動」）——規劃書只在文字敘述給了合法值，DDL 從未真正約束過，判斷比照
   `AlignSchemaV314` 補 `matches.status` 的既有先例補齊，不是新增規格。
6. **P1「課程／營隊項目」的「常見問題」欄位不另外新增資料結構**——規劃書 P1 逐字列出的欄位包含
   「常見問題」，但 FAQ 嵌入機制（G-12，`FaqEmbedSlot`／`FaqEmbedSlotLink`，S1-7a 已完成）已有
   `program_detail` 這個掛載點，判斷為同一件事的既有落點，不重複建置。
7. **公開報名送出端點雖然任務描述寫「公開讀取端點」，本輪判斷仍需要一個公開寫入端點**——前台
   報名流程（規劃書行 389）明確要求「送出 → 產生報名編號」，沒有寫入端點 P3 報名管理就沒有真實
   資料來源（後台代填只服務電話／現場報名，多數報名預期來自公開網站表單）。判斷這是 P3 報名
   管理不可或缺的一部分，不是規劃外新增，予以實作。
8. **未成年報名是否強制要求家長／緊急聯絡人**——`registrations.guardian_name`／`guardian_phone`
   在 DDL 都是 `NULL`able，規劃書沒有給年齡門檻。本輪不依 `birth_on` 自動判定「未成年」並強制
   要求家長欄位（沒有法定年齡門檻的依據，屬於會影響蒐集範圍的判斷，依指示不自行擴大），公開送出
   端點只驗證「姓名必填」「電話與 Email 至少一項」，家長欄位是否必填留給前台表單依實際政策決定。
9. **報名編號格式**（`{俱樂部代碼}-{yyyyMMdd}-{6 碼隨機}`，例如 `TCRFC-20260925-K7QXM2`）為本輪
   自訂——規劃書只要求「產生報名編號」，沒有定義格式，比照 `Common/CsvUtils.cs` 檔頭「沒定義就
   採最小可行」的既有慣例，見 `Common/RegistrationNumberGenerator.cs` 檔頭。
10. **`registrations.member_id` 可接受呼叫端指定既有會員**（後台代填與公開送出皆有此欄位，驗證
    FK 存在但不做任何會員登入或自動帶入邏輯）——K1 會員系統尚未開發，前台也沒有會員登入能串接，
    這欄位目前實務上恆為空，只是為了不擋住日後 K1 開發時的相容性預先接上驗證，沒有新增任何行為。

### 未做的部分（P4 試訓管理，`S2-4`）

`trials`／`registrations.trial_id` 那一半完全沒有動，`Features/AdminRegistrations` 的清單查詢明確
用 `WHERE session_id IS NOT NULL` 排除試訓報名，避免這批端點意外把 P4 的資料一起吐出來。

### 測試

`Tcrfc.Api.Tests/AdminProgramsSessionsRegistrationsTests.cs` 新增 12 項（權限矩陣 5 項、P1／P2／P3
CRUD 與驗證 4 項、公開讀取與報名送出 3 項，含跨俱樂部越權、無權限角色 403、額滿轉候補、已結束梯次
拒絕報名、跨俱樂部梯次 404 等反例）。全套 `dotnet test` **386／386 通過**（連跑兩次皆全線）。
`apps/admin`／`apps/web` 的 `npm run lint` 皆通過（0 errors；`apps/web` 既有 539 個 warning 與本輪
無關，未觸碰任何前端檔案）。

---

## S1-10：`G1` 表單設計器／`G2` 詢問收件匣 ＋ 10 表單中心公開讀取與送出（2026-09-25，`backend-engineer`）

主站規劃書 §4.7 G1／G2（後台）、§3.10（10 表單中心，公開讀取與送出）。沿用既有架構：
`IAdminClubAuthorizer`、權限碼、`ApiExceptionHandler`、CSV 匯出（`Common/CsvUtils.cs`）。
**沒有套用 `TeamRowScope`**——本模組另外設計了一套不需要 `role_permissions.scope_type` 的列級授權，
見下方「依表單類別的列級授權」。

### 綱要異動

`forms`／`form_fields`／`enquiries`／`enquiry_answers` 四張表在此之前就已經是完整 DDL
（`db/club-schema.sql`「4.6 G 表單與詢問」，S0 系列建的），EF 實體與 `ClubDbContext` 對應也早就
scaffold 好。本輪異動：

1. **新增欄位** `form_fields.options_json`（`nvarchar(1000)`，下拉／多選的選項清單，JSON 字串陣列）
   ——G1 規劃書明文要求下拉／多選兩種欄位型別，但原本沒有任何欄位能存選項清單
   （`validation_rule` 語意是格式驗證正規表示式，不同用途）。
2. **補齊兩個從未套用過的值域 CHECK**（跟 `AlignSchemaV314`／`AlignSchemaS19Programs` 同一種落差）：
   `form_fields.field_type`（對應規劃書 G1 逐字列出的六種欄位型別）、`enquiries.status`
   （對應規劃書 G2「新進 → 處理中 → 已回覆 → 已結案 / 無效」五個狀態值）。
3. **新增欄位** `form_fields.is_summary`（`bit NOT NULL DEFAULT 0`，審查回饋補做，見下方「G2『內容
   摘要』欄」）＋一個過濾唯一索引，限制同一張表單最多一個欄位可標記為摘要來源。

```
migration: 20260925035351_AlignSchemaS110Forms
  ALTER TABLE form_fields ADD [options_json] nvarchar(1000) NULL;   -- EF AddColumn，來自實體模型異動
  ALTER TABLE form_fields ADD CONSTRAINT CK_form_fields_field_type
    CHECK (field_type IN ('text','textarea','select','multiselect','date','file','consent'));
  ALTER TABLE enquiries ADD CONSTRAINT CK_enquiries_status
    CHECK (status IN (N'新進',N'處理中',N'已回覆',N'已結案',N'無效'));

migration: 20260925051206_AddFormFieldIsSummary
  ALTER TABLE form_fields ADD [is_summary] bit NOT NULL DEFAULT CAST(0 AS bit);
  CREATE UNIQUE INDEX UQ_form_fields_one_summary_per_form ON form_fields (form_id) WHERE is_summary = 1;
```

第一支套用前查證 `tcrfc_club_dev` 的 `form_fields`／`enquiries` 兩張表皆為 0 筆（G 模組本輪才第一次
接上真實 API），純 DDL 變更。第二支套用時 `form_fields` 已有 114 筆種子資料，但這是單純新增有
`DEFAULT` 的欄位（不是對既有資料新增 CHECK），對既有列永遠安全，不需要「0 筆」前提；套用後另外
對已種下的種子資料跑一次 `UPDATE`（依 `docs/12-database-schema.md` §12 第 38 點的分配表）把
`is_summary=1` 補回對應欄位，因為 `db/seed/generate-club-seed-sql.py` 的「`IF NOT EXISTS` 才
`INSERT`」冪等策略對「更新既有列」沒有幫助（同 `ADMIN_USERS` 密碼／2FA 狀態需要另外
`reset-admin-accounts.sh` 才能同步的既有道理）。`Data/EfEntities/FormField.cs` 各加一個
`OptionsJson`／`IsSummary` 屬性、`Data/ClubDbContext.cs` 加對應的 `entity.Property(...)` 設定——
比照既有 `Match.OriginalMatchOn`／`OriginalKickoff` 的先例（手改兩個既有「產生檔」，不整個重新
scaffold），`ClubDbContextModelSnapshot.cs` 已同步（該檔不進版控，見 S0-7j 段）。完整說明另見
docs/12-database-schema.md §12 第 37／38 點。

### `Form.form_code` 九碼目錄（本輪判斷，非資料庫欄位）

規劃書 §3.10 只用中文標題列出 7 類表單＋提案下載＋捐助洽詢，未定義程式用代碼字串。本輪拍板九碼
（`Features/Forms/FormCatalog.cs`）：`join_player`／`academy_children_training`／
`camp_registration`／`international_player_enquiry`／`partnership_sponsorship`／`media_enquiry`／
`general_contact`／`proposal_download`／`donation_enquiry`。**表單顯示名稱不進資料庫**
（2026-09-22 已拍板不建 `forms_i18n.name`），`FormCatalog` 的中英顯示名稱字典是純程式碼常數，
只給 CSV 匯出與清單 API 的便利欄位使用。`db/seed/generate-club-seed-sql.py` 各自宣告一份同樣的
九個代碼字面值（既有慣例，見該檔 `HOME_SECTIONS` 段的檔頭說明），兩處要一起改。

種子資料：兩俱樂部（`tcrfc`／`bw`）各種一份 9 個表單 ＋ 依規劃書 §3.10 逐表單欄位清單設定的預設
`form_fields`（`proposal_download`／`donation_enquiry` 兩者規劃書未列欄位，最小可行自訂）。所有
表單統一補一個 `privacy_consent`（同意條款）欄位，對應規劃書「共通機制：個資同意條款勾選」；每個
表單一律含 `name`／`contact` 兩個慣例欄位鍵，供 G2 收件匣清單顯示「姓名」「聯絡方式」兩欄使用
（見下方「姓名／聯絡方式怎麼從動態欄位取出」）。

### 後台端點（新增檔案 `Features/AdminForms`／`AdminEnquiries`）

| 方法與路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET /api/v1/admin/{club}/forms` | `form.view` | 9 個固定表單清單 |
| `GET /api/v1/admin/{club}/forms/{id}` | `form.view` | 單筆詳情（設定＋動態欄位＋雙語自動回覆信） |
| `PUT /api/v1/admin/{club}/forms/{id}` | `form.update` | 更新設定（通知信、CAPTCHA 開關、送出後導向、自動回覆信雙語）。**沒有建立／刪除表單本身的端點**——9 個表單是固定目錄 |
| `POST /api/v1/admin/{club}/forms/{id}/fields` | `form.update` | 建立動態欄位 |
| `PUT /api/v1/admin/{club}/forms/{id}/fields/{fieldId}` | `form.update` | 編輯動態欄位 |
| `DELETE /api/v1/admin/{club}/forms/{id}/fields/{fieldId}` | `form.update` | 刪除動態欄位。**已有詢問資料引用會被擋下（409）**——`FK_enquiry_answers_field` 沒有 `ON DELETE CASCADE` |
| `GET /api/v1/admin/{club}/enquiries` | `enquiry.inbox.view` 或 `enquiry.course/partnership/media.view` 其一 | 詢問清單。`formCode`／`status`／`keyword`／`dateFrom`／`dateTo`／`page`／`pageSize` 篩選，依角色類別自動過濾 |
| `GET /api/v1/admin/{club}/enquiries/{id}` | 同上 | 單筆詳情（含全部逐欄回答） |
| `GET /api/v1/admin/{club}/enquiries/export` | `enquiry.inbox.export`（`is_restricted`，僅系統管理員） | CSV 匯出，表單類別欄輸出中文顯示名稱，不輸出 `form_code` 字面值 |
| `PUT /api/v1/admin/{club}/enquiries/{id}` | `enquiry.inbox.update` 或 `enquiry.course/partnership/media.update` 其一 | 處理詢問：狀態／指派負責人／內部備註／標籤。**不能改來源表單與逐筆回答內容**（訪客原始送出資料） |

### 公開端點（新增檔案 `Features/Forms`，不需要登入）

| 方法與路徑 | 說明 |
|---|---|
| `GET /api/v1/{club}/forms/{formCode}` | 表單定義（動態欄位＋型別＋必填＋驗證規則＋選項），供前台動態產生表單（S1-17，不在本次範圍） |
| `POST /api/v1/{club}/forms/{formCode}/submissions` | 送出，回傳 `{success:true}`。掛 Rate Limiting（見下方「濫用防護」） |

### 權限碼與角色指派（`db/seed/generate-club-seed-sql.py`）

新增 11 個權限碼，`module_code=G`、`domain=enquiry`（G1／G2 共用同一個 domain，矩陣把「表單詢問」
列為單一欄）：`form.view`／`form.update`（G1）、`enquiry.inbox.view/update/export`
（G2，`export` 是 `is_restricted=1`，僅系統管理員）、`enquiry.course.view/update`、
`enquiry.partnership.view/update`、`enquiry.media.view/update`（G2 的三個類別限定分組）。

依主站規劃書 §6 矩陣「表單詢問」欄逐列指派：系統管理員 ✔全；內容編輯／競技球隊管理／翻譯人員
「—」，不指派；**學院／課程管理 → `enquiry.course.*`**（矩陣「課程類詢問」）；
**商務／贊助 → `enquiry.partnership.*`**（矩陣「合作／贊助類詢問」）；
**公關／媒體 → `enquiry.media.*`**（矩陣「媒體類詢問」）；**客服／行政 → `form.*` ＋
`enquiry.inbox.view/update`**（矩陣「✔全」，不含匯出，比照 P3 匯出不給客服／行政的既有保守預設）；
**檢視者 → `form.view`／`enquiry.inbox.view`**（唯讀）；**合作球隊管理 → `form.*` ＋
`enquiry.inbox.view/update`**（`scope_type=own_clubs`，矩陣「自家」，俱樂部範圍已由既有機制限制，
G 模組內部不需要再疊一層類別過濾）。

🔴 **發現規劃書 §6 原始表格的欄位錯位**：主站規劃書 §6（行 1604）「廣告」／「行動 App」／
「表單詢問」三欄，實測欄位內容與表頭標籤對不上（例如「商務／贊助」列在字面「廣告」欄位置出現的是
「合作／贊助類詢問」，語意明顯屬於表單詢問而非廣告）。本輪改採 `docs/03-admin-spec.md` §3
已手動修正對齊的「詢問」欄核對（該檔案在更早的 S1 階段已把「廣告」「行動 App」兩欄整欄拿掉、
只保留語意正確的「詢問」欄），未回頭修正規劃書原始表格本身。完整記錄見
docs/12-database-schema.md §12 第 37 點、docs/12b-database-tables.md §7.4「S1-10 新增」附註，
建議 `system-analyst` 之後把規劃書 §6 原始表格也修正對齊。

新增一個測試帳號：`business.sponsorship@tcrfc.test`（`business_sponsorship`，僅 `tcrfc`，密碼
`ContentEditor@123`）——`business_sponsorship` 角色早已存在（S1-3 種子），但先前沒有任何測試帳號
被指派過，本輪測 `enquiry.partnership.*` 需要它。

### 依表單類別的列級授權（不是 `role_permissions.scope_type`）

矩陣「課程類詢問」「合作／贊助類詢問」「媒體類詢問」三格看起來很像既有 `TeamRowScope` 那種
「同一權限碼、依角色細分範圍」的情境，但**本輪刻意不沿用 `scope_type`**：`own_teams` 之所以需要
`scope_type` 加一張 `AdminUserTeam` 關聯表，是因為「這個人能碰哪些球隊」是**逐人指派**、會隨時間
變動的；而「課程類詢問＝`academy_children_training`／`camp_registration`」這個分組**是規劃書固定
的 9 個 `form_code` 分類**，不會因為換了哪個使用者而不同，也不需要另外建一張「誰對應哪個類別」的
關聯表。直接拆成 `enquiry.course.*`／`enquiry.partnership.*`／`enquiry.media.*` 三組獨立權限碼，
比「多一個 `scope_type` 列舉值＋在程式碼裡硬編碼一份『scope_type → form_code 集合』對照表」更直接，
也不需要修改 `role_permissions.scope_type` 的 CHECK 值域（零綱要異動）。

執行機制：一個操作可能有多個「等價權限碼」（例如查看詢問清單，`enquiry.inbox.view` 或
`enquiry.course.view` 或...任一個都能通過），既有 `IAdminClubAuthorizer.AuthorizeAsync` 只接受
單一權限碼，因此新增 `AuthorizeAnyAsync`（清單中持有任一個即可通過①②③帳號與俱樂部授權檢查＋
④'寬鬆版權限碼檢查）；`IPermissionChecker` 新增 `GetHeldPermissionCodesAsync`（一次查出候選碼中
持有哪幾個），`AdminEnquiriesRepository.ResolveViewFormCodeFilterAsync`／
`ResolveUpdateFormCodeFilterAsync` 依持有的碼組出 `WHERE form_code IN (...)` 的過濾條件（持有
`*.inbox.*` 回傳 `null` 代表不限；只持有類別碼則聯集對應的 `form_code` 集合；都沒有則回傳空集合，
fail-closed）。清單／匯出用集合過濾整批資料；詳情／更新單筆時額外核對「這一筆的 `form_code`
在不在允許集合內」，不在則視同 404（比照既有跨俱樂部越權「不洩漏存在與否」的慣例）。

### 姓名／聯絡方式怎麼從動態欄位取出

`Enquiry` 本身沒有姓名／聯絡方式欄位——這兩項跟其餘表單內容一樣，全部存在 `EnquiryAnswer`
（`(enquiry_id, form_field_id) → value`），因為 G1 是「表單設計器」，欄位是動態的。本輪採**慣例
欄位鍵**：種子資料把每個表單的姓名欄位 `field_key` 定為 `"name"`、聯絡方式定為 `"contact"`，G2
清單靠這兩個鍵撈出來顯示。**若後台把這兩個鍵改名或刪除，清單只會顯示 `null`，不是程式錯誤**——
這是動態表單的必然取捨，沒有資料庫層的機制能保證「某個 `field_key` 一定存在」。

### 🔴 修正：G2「內容摘要」欄補做（審查回饋，2026-09-25）

**發現的問題**：主站規劃書 G2（行 1164）逐字列出收件匣欄位「來源表單、姓名、聯絡方式、**內容
摘要**、來源頁面、UTM 來源、送出時間」，本輪最初以「表單欄位是動態的，沒有一個穩定的摘要標記」
為由略過這一欄，經審查回饋指出**規劃書明文要求的欄位不能因為實作不便而略過**。

**怎麼補的**：跟姓名／聯絡方式同一種「慣例欄位鍵」精神，但改用**旗標**而不是字面鍵比對——
`form_fields` 新增 `is_summary bit`，G1 表單設計器可以把**任一欄位**標記為「這是內容摘要」，
不受限於固定的 `field_key` 名稱。**同一張表單最多一個欄位可標記**，兩道防線：①應用層
（`AdminFormsRepository.CreateFieldAsync`／`UpdateFieldAsync`，標記新的會自動取代舊的，不是回
錯誤要求先手動取消）；②DB 層過濾唯一索引 `UQ_form_fields_one_summary_per_form`
（`WHERE is_summary = 1`）。G2 清單／CSV 匯出依此旗標取值（`AdminEnquiriesRepository`）。

**種子資料的分配**：只標給有敘述性文字、值得當摘要的欄位——`join_player`／
`academy_children_training`／`international_player_enquiry` 標 `experience`；
`partnership_sponsorship` 標 `cooperation_direction`；`general_contact`／`donation_enquiry`
標 `message`。`camp_registration`（營隊梯次、學員資料、健康聲明、緊急聯絡人）、`media_enquiry`
（媒體名稱、記者姓名、採訪主題、截稿日）、`proposal_download`（公司、姓名、Email）**三個表單沒有
任何合適的敘述性文字欄位，刻意不標記**——這三個表單的內容摘要在清單與匯出上一律是 `null`，
是設計上的必然結果，不是遺漏。

### 濫用防護（規劃書「防機器人（reCAPTCHA / Turnstile）」，本輪沒有做的部分）

全系統沒有串接任何 CAPTCHA 服務的憑證或後端驗證邏輯，串接需要申請站台金鑰、決定環境變數、
寫一支呼叫外部 siteverify API 的服務——這是獨立的執行層基礎建設決定，不在本次任務範圍，比照 S1-9
對簡訊通路的既有處理方式（回報缺口，不自行發明）。本輪改用兩層不需要外部服務的防線：
① `Program.cs` 對 `POST .../submissions` 掛 ASP.NET Core 內建 Rate Limiting（依呼叫端 IP 分區，
固定視窗 5 分鐘 20 次，超過回 429，`QueueLimit=0`）；② `SubmitFormRequest.Website` 誘捕欄位
（honeypot，填了值就安靜回成功但不寫入任何資料）。`PublicFormDto.CaptchaEnabled` 旗標本身**沒有
對應的伺服器端驗證**——前端讀到 `true` 時應該渲染 CAPTCHA 元件，但送出端點目前不會真的驗證 token，
真正串接 Turnstile／reCAPTCHA 留給日後有服務憑證時再補。

### 🔴🔴🔴 修正：限流原本依賴的不是訪客真實 IP（審查回饋，2026-09-25）

**發現的問題**：初版把 `httpContext.Connection.RemoteIpAddress` 直接當限流分區鍵，但正式環境的
路徑是 Cloudflare → Caddy → `api` 容器——`deploy/Caddyfile` 的 `trusted_proxies`／
`client_ip_headers` 只解決 **Caddy 自己**怎麼看穿 Cloudflare，不會讓下游的 `api` 也認得訪客
真實 IP。`api` 容器實際看到的 TCP 連線來源永遠是 Caddy 容器的 Docker 內部 IP，等於**全站訪客
共用同一把「Caddy 的 IP」鑰匙**，依 IP 分區限流形同虛設。

**怎麼修的**：新增 `Security/TrustedProxyConfiguration.cs`（設定
`ForwardedHeadersOptions`）＋`Common/ClientIpResolver.cs`（讀取已被中介軟體處理過的
`RemoteIpAddress`），`Program.cs` 在管線最前面掛 `app.UseForwardedHeaders()`。
`docker-compose.yml` 把 `internal` 網路釘死子網段 `172.28.238.0/24`，給 `proxy`（Caddy）服務
一個固定 IP `172.28.238.2`，透過新環境變數 `TRUSTED_PROXY_IP` 傳給 `api`。**只信任這一個 IP，
不信任整個 Docker 網段**——網段裡還有 `nuxt-tcrfc`／`admin-web` 等其他容器，信任整個網段等於
讓這些容器也能偽造標頭騙過限流，違反「不可信任所有來源」的要求。

**🔴🔴🔴 過程中親自踩到的框架陷阱，務必記住**：`ForwardedHeadersMiddleware` 把
`KnownProxies`／`KnownIPNetworks` **兩者都是空集合**視為「沒有設限制」，行為是**信任所有來源**，
跟直覺剛好相反（多數人會以為空清單＝沒人受信任＝標頭一律被忽略）。第一版的單元測試因此曾經
「未設定 `TRUSTED_PROXY_IP` 時，偽造的 `X-Forwarded-For` 仍被採信」而失敗——這代表如果只靠
「沒設定時清單留空」當防線，本機開發、測試環境、甚至漏設這個環境變數的正式部署都會變成信任
任何人送來的標頭，比完全不做這個功能更危險。真正的防線因此改成：**`TRUSTED_PROXY_IP` 沒設定時，
`Program.cs` 根本不呼叫 `app.UseForwardedHeaders()`**（`TrustedProxyConfiguration.IsEnabled`
判斷），中介軟體完全不在管線裡執行，`RemoteIpAddress` 保證是連線本身看到的值，沒有任何機會被
偽造的標頭覆寫。完整說明見 `Security/TrustedProxyConfiguration.cs` 檔頭。

**測試**：`Tcrfc.Api.Tests/TrustedProxyConfigurationTests.cs` 新增 3 項——受信任代理轉來的
`X-Forwarded-For`、不同來源 IP 各自解析出不同真實 IP（各自獨立額度）；不受信任來源送來的
`X-Forwarded-For` 整個被忽略；未設定 `TRUSTED_PROXY_IP` 時任何 `X-Forwarded-For` 一律不採信。
**不透過 `WebApplicationFactory` 打真正 HTTP**——實測確認 `TestServer` 底下
`HttpContext.Connection.RemoteIpAddress` 永遠是 `null`，`ForwardedHeadersMiddleware` 的信任
判斷永遠不可能命中，無法在那個環境下驗證「受信任代理」這條路徑。改用
`TrustedProxyConfiguration.ResolveEffectiveClientIp`（跟 `Program.cs` 真正管線用的是同一支
`Configure` 設定，內部真的建構並執行一次 `ForwardedHeadersMiddleware`，不是重寫一份邏輯）。
⚠️ `Tcrfc.Api.Tests` 是 `Microsoft.NET.Sdk`（不是 `Sdk.Web`），實測發現無法直接參照
`Microsoft.AspNetCore.HttpOverrides`（`ResolveTargetingPackAssets` 中繼輸出看得到該組件，卻不會
出現在最終傳給 `csc` 的 `-reference` 清單，原因不明，懷疑是 RAR 衝突解決或套件裁剪管線的交互
作用），因此把「建構中介軟體並執行」這段留在主專案，測試專案只呼叫回傳 `string` 的純函式版本，
完全不需要碰任何 ASP.NET Core 型別。

**手動驗收**（本機 `dotnet run`，`curl`）：公開表單定義、成功送出、缺必填欄位（400）、未知表單代碼
（404）、誘捕欄位命中（200 但資料庫 0 筆）、依 IP 分區限流（連續 25 次請求，第 21 次起收到 429）、
**設定 `TRUSTED_PROXY_IP` 後，帶不同 `X-Forwarded-For` 的請求各自獨立計算限流額度、且非受信任
連線來源送的 `X-Forwarded-For` 不被採信**逐項打過，詳見下方「測試」段。後台端點用
`clean.login@tcrfc.test` 走完整登入＋即時完成 2FA 設定（`TotpService` 的 RFC 6238 演算法用
Python 手算驗證碼，不繞過驗證本身）後實際呼叫 G1／G2 端點，確認清單、詳情、CSV 匯出（中文表單
類別名稱、非 `form_code` 字面值）皆正確，驗收後已呼叫 `reset-admin-accounts.sh` 把
`clean.login@tcrfc.test` 的 2FA 狀態還原成種子初始值，不污染 `AdminAuthTests` 對這個帳號
「兩階段驗證未啟用」的既有假設。

### 規劃書沒寫清楚、本輪自行判斷的地方

1. **`donation_enquiry`（捐助洽詢）規劃書全文未定義這個表單的實際欄位**——只在 G2 收件匣分頁清單
   （行 1163）與 `Enquiry` 型別說明兩處被提及，§3.10 逐表單欄位清單只列到 10.1–10.7 七類。本輪
   最小可行自訂三個欄位（姓名、聯絡方式、內容，皆比照 10.7 一般聯絡的欄位精神），不擴大蒐集範圍。
2. **`proposal_download` 併入商務／贊助的「合作／贊助類詢問」類別**——規劃書沒有明文歸類提案下載
   的 Lead 名單該由哪個角色的 G2 收件匣看到，本輪判斷「提案下載＝贊助洽詢的前導動作」（9.4
   CTA「Sponsorship Deck 下載提案簡介」本身就在贊助頁面），歸入商務／贊助能看到的範圍。
3. **檔案上傳（`file`）欄位型別本輪只接受文字／URL 輸入，不是真正的檔案上傳**——全系統既有的
   `IImageStorageService` 是「驗證格式→去 EXIF→縮圖→轉 WebP」的圖片專用管線，履歷等一般文件
   （PDF／Word）不是圖片、也不需要縮圖，直接沿用會誤用圖片轉檔邏輯。建立一套獨立的通用檔案上傳
   服務（儲存體容器、型別與大小驗證）是獨立的基礎建設決定，不在本次任務範圍，見
   `Features/Forms/FormFieldTypes.cs` 上 `File` 常數的說明。
4. **「內容摘要」欄（規劃書 G2 條列的收件匣欄位之一）已於審查回饋後補做**——最初判斷「表單欄位
   是動態的，沒有穩定的摘要標記」而略過，經指出「規劃書明文要求的欄位不能因為實作不便而略過」
   後改正：`form_fields` 新增 `is_summary bit`（migration `AddFormFieldIsSummary`），G1 可以把
   任一欄位標記為內容摘要來源，同一張表單最多一個（應用層＋DB 過濾唯一索引 `UQ_form_fields_
   one_summary_per_form` 兩道防線，設定第二個會自動取代第一個，不是回錯誤）。種子資料把
   `join_player`／`academy_children_training`／`international_player_enquiry` 的 `experience`、
   `partnership_sponsorship` 的 `cooperation_direction`、`general_contact`／`donation_enquiry`
   的 `message` 標記為摘要；`camp_registration`／`media_enquiry`／`proposal_download` 沒有合適
   的敘述性文字欄位，內容摘要維持 `null`，是設計上的必然結果。完整說明見
   `docs/12-database-schema.md` §12 第 38 點與 `Features/AdminEnquiries/AdminEnquiriesRepository.cs`
   檔頭。
5. **`enquiry.inbox.export` 只給系統管理員，客服／行政「✔全」不含匯出**——比照 P3
   `program.registration.export` 不給客服／行政的既有保守預設，矩陣的「✔全」在既有慣例裡本來就
   不必然包含匯出（匯出普遍被視為需要額外授權的敏感動作）。
6. **`enquiry.inbox.export` 套用 `is_restricted=1`，但沒有另外實作「執行當下二次驗證」**——全系統
   目前沒有任何模組真的做出這件事（`Security/PermissionChecker.cs` 只做一般權限碼比對），本輪比照
   現狀，只掛旗標與基本權限檢查，不另外發明，同 S1-9 既有先例。
7. **`form_fields` 沒有 `UNIQUE (form_id, field_key)` 的資料庫層防線**——只在應用層（
   `AdminFormsRepository.CreateFieldAsync`／`UpdateFieldAsync`）擋重複欄位代碼，判斷這個唯一性
   邊界只有這一支程式碼會寫入，資料庫層約束的邊際效益不足以再多開一次 DDL 異動，回報供之後若有
   第二個寫入路徑時重新評估。
8. **G1 沒有欄位批次重新排序的端點**——`PUT .../fields/{fieldId}` 的 `sortOrder` 允許逐一覆寫，
   後台若要做拖曳排序，前端可依序對每個異動的欄位各呼叫一次；規劃書沒有明確要求批次排序端點，
   採最小可行原則不多開。
9. **Rate Limiting 的門檻值（20 次／5 分鐘／依 IP）沒有規格依據**——比照 `Common/CsvUtils.cs`
   檔頭「沒定義就採最小可行」的既有慣例自訂；這個數字同時要照顧到
   `Tcrfc.Api.Tests.AdminFormsEnquiriesTests` 的整合測試呼叫量（`WebApplicationFactory` 測試連線
   共用同一個 IP 分區），見 `Program.cs` 對應段落的完整說明。
10. **公開送出端點回應不含新建的 `Enquiry` id 或確認編號**——規劃書只要求「送出後：自動回覆信＋
    通知信＋寫入後台」，沒有像 P3 報名那樣要求「產生報名編號」，本輪判斷不需要額外的確認碼，只回
    `{success:true}`；測試需要回查 id 時改用 `contact` 欄位值查資料庫（見測試檔案內部工具）。
11. **表單通知信與自動回覆信本輪未接上真正的寄信通路**——同 S1-9 記錄的既有缺口（全系統還沒有
    寄信基礎設施），`forms.notify_emails`／`forms_i18n.auto_reply_body` 兩個設定欄位已可由 G1
    寫入與讀出，但公開送出端點目前不會真的寄出任何信件，回報供下一輪走同步鏈裁決寄信基礎建設。

### 測試

`Tcrfc.Api.Tests/AdminFormsEnquiriesTests.cs` 新增 17 項（權限矩陣 5 項、G1 表單設定與欄位 CRUD
含驗證與衝突反例 4 項、內容摘要「同一表單最多一個、自動取代」1 項、G2 依類別列級授權含跨類別越權
與無摘要表單回 `null` 各 1 項、公開表單定義與送出含誘捕欄位／必填／未知欄位／下拉選項驗證等反例
5 項、CSV 匯出中文化含內容摘要欄 1 項）；`Tcrfc.Api.Tests/TrustedProxyConfigurationTests.cs`
新增 3 項（限流依真實訪客 IP：受信任代理各自獨立額度、不受信任來源標頭不被採信、未設定
`TRUSTED_PROXY_IP` 時中介軟體完全不掛）。全套 `dotnet test` **406／406 通過**（連跑多次皆全線）。
`apps/admin`／`apps/web` 的 `npm run lint` 皆通過（0 errors；`apps/web` 既有 539 個 warning 與
本輪無關，未觸碰任何前端檔案）。

---

## 目錄結構

```
apps/api/
├── Program.cs                     # DI 註冊、middleware 管線、路由掛載、寫入端點開發模式開關
├── Tcrfc.Api.csproj                # net10.0，Dapper + Microsoft.Data.SqlClient + AspNetCore.OpenApi
│                                    #   + EF Core SqlServer/Design（本輪新增，只用在寫入與 migration）
├── Data/
│   ├── IClubSqlConnectionFactory.cs / ClubSqlConnectionFactory.cs   # 唯一的主站庫連線來源（Dapper 用）
│   ├── ClubOrSharedSql.cs         # 「俱樂部專屬優先、回退共同」SQL 片段的唯一真實來源
│   ├── ClubDbContext.cs           # 🔴 產生檔（dotnet ef dbcontext scaffold），不要手改
│   ├── ClubDbContextCustomizations.cs   # 客製化掛進 OnModelCreatingPartial（本輪：並行權杖設定）
│   ├── EfEntities/                # 🔴 產生檔，138 個實體類別，對應 145 張表（含本輪新增的
│   │                                #   __EFMigrationsHistory）。programs 表改名 TrainingProgram，
│   │                                #   見下方「EF Core 一次性 handoff」的命名衝突說明
│   └── Migrations/                # InitialBaseline：Up()/Down() 刻意留空，見下方說明
├── Security/
│   ├── ClubScope.cs               # 已驗證的俱樂部範圍，建構子 internal，繞不過去
│   ├── IClubResolver.cs / ClubResolver.cs   # 唯一能建立 ClubScope 的地方
│   ├── ClubNotFoundException.cs
│   ├── AdminIdentity.cs           # 🔴🔴🔴 S1 新增：JWT claims 解析出的身分（不含權限與範圍）
│   ├── AdminAuthExceptions.cs     # 🔴🔴🔴 S1 新增：AdminUnauthenticatedException（401）／AdminForbiddenException（403）
│   ├── AdminClubScope.cs          # 🔴🔴🔴 S1 新增：ClubScope ＋ AdminIdentity，建構子 internal
│   ├── IAdminClubAuthorizer.cs / AdminClubAuthorizer.cs   # 🔴🔴🔴 S1 新增：AdminClubScope 的唯一產生者，四步檢查
│   ├── IPermissionChecker.cs / PermissionChecker.cs       # 🔴🔴🔴 S1 新增：角色→權限碼查詢
│   ├── AdminTokenService.cs       # 🔴🔴🔴 S1 新增：JWT 存取權杖簽發／驗證參數、更新權杖亂數與雜湊
│   ├── PasswordHasher.cs          # 🔴🔴🔴 S1 新增：Argon2id
│   ├── TotpService.cs             # 🔴🔴🔴 S1 新增：TOTP（RFC 6238），手刻不引套件
│   └── TwoFactorSecretProtector.cs # 🔴🔴🔴 S1 新增：Data Protection 包裝，見檔頭的正式環境前置條件
│
│   ⚠️ 2026-09-23（使用者裁決）已刪除：DevWriteGate.cs／IDevOperatorResolver.cs／
│   DevOperatorResolver.cs（開發模式開關機制，見「開發模式開關：已刪除」整節）、
│   AdminAuditLogger.cs（J3 稽核記錄，見「稽核記錄（J3）：已撤回」整節）。
├── Localization/
│   └── RequestLocale.cs           # zh/en ↔ zh-Hant/en 轉換與回退規則
├── Caching/
│   ├── IQueryCache.cs             # 快取接縫（entity/club/locale/qualifier 四維度 key＋InvalidateAsync）
│   ├── NoOpQueryCache.cs          # REDIS_HOST 未設定時的實作
│   └── RedisQueryCache.cs         # REDIS_HOST 有設定時的實作（S0-7d，fail-open／版本號失效／TTL／single-flight）
├── Common/
│   ├── PagedResult.cs / PagingQuery.cs
│   ├── ApiExceptionHandler.cs     # 統一例外處理，不外流資料庫例外訊息；本輪擴充 AdminArticleException 家族
│   └── HealthEndpoints.cs         # /healthz、/readyz（S0-7d 起含慈善庫與 Redis 檢查）
├── Images/                         # 🔴🔴🔴 S0-8 本輪新增：圖片上傳共用元件
│   ├── ImageUploadOptions.cs       #   數字常數（10MB／2560／1280,640,320／160／WebP 品質）唯一來源
│   ├── ImageProcessor.cs           #   純轉檔邏輯：格式驗證→轉正→去中繼資料→縮放→WebP 編碼，不碰 I/O
│   ├── ImageObjectKey.cs           #   衍生檔物件鍵推導規則（由主鍵算出，不另存欄位）
│   ├── ProcessedImageSet.cs        #   ImageProcessor 的輸出型別
│   ├── ImageProcessingExceptions.cs#   驗證失敗例外家族（400，日常中文訊息）
│   ├── IImageStorageService.cs     #   儲存層接縫：UploadAsync／DeleteAsync
│   ├── BlobImageStorageService.cs  #   Azure Blob Storage 實作（本機開發接 Azurite）
│   └── UnavailableImageStorageService.cs  # AZURE_BLOB_CONNECTION_STRING 未設定時的替身
└── Features/
    ├── Clubs/      (ClubDto, ClubsRepository, ClubsEndpoints)
    ├── Players/    (PlayerDto, PlayersRepository, PlayersEndpoints)
    ├── Staff/      (StaffDto, StaffRepository, StaffEndpoints)
    ├── News/       (ArticleListItemDto/ArticleDetailDto, ArticlesRepository, ArticlesEndpoints)   # 唯讀，Dapper，未改
    ├── Schedule/   (MatchDto, MatchesRepository, MatchesEndpoints)
    ├── AdminNews/  # ⚠️ S1 起改走真實授權（IAdminClubAuthorizer），不再是「開發模式限定」：
    │                #   AdminArticleDtos／AdminArticlesRepository（EF Core）／AdminArticlesEndpoints／
    │                #   AdminArticleExceptions／AdminArticleRequestForm／CoverKeyUpdate。
    ├── Uploads/    # S0-8：UploadSlotPolicy（欄位插槽允許清單，仿 AdminNews/SlugPolicy.cs 的形狀）。
    │                #   ⚠️ 獨立上傳端點已移除，見下方 S0-8 段落，現在只剩這份允許清單。
    └── AdminAuth/  # 🔴🔴🔴 S1 本輪新增：AdminAuthDtos／AdminAuthService（登入、更新權杖輪替、
                     #   登出、變更密碼、2FA 設定／確認／停用的業務邏輯）／AdminAuthEndpoints
                     #   （HTTP 形狀，含 __Host- 前綴 Cookie 讀寫）。不是俱樂部範圍端點，見該
                     #   目錄檔頭說明為什麼不經過 IAdminClubAuthorizer。

apps/api/Tcrfc.Api.Tests/    # S0-7d：自動化測試專案（獨立 .csproj，不進 Docker 映像檔，見下方「測試」）
```

每個 Features 子目錄都是「DTO＋repository＋endpoints」三件套，彼此不互相依賴（除了都經過 `IClubResolver`）。
`Tcrfc.Api.Tests` 是同目錄下的獨立子專案（沒有 `.sln`），`Tcrfc.Api.csproj` 已明確 `<Compile Remove>` 排除它，
避免遞迴萬用字元把測試原始碼一起編譯進主專案（測試專案參照的 xunit／`Microsoft.AspNetCore.Mvc.Testing`
主專案完全不需要）。

---

## 怎麼跑（本機開發）

### 前置

1. 本機既有的 `sqlserver` 容器已啟動（不是本專案 compose 管理的，見
   [`deploy/README.md`](../../deploy/README.md)），DDL 與種子資料已灌入（見
   [`db/seed/README.md`](../../db/seed/README.md)）：
   ```bash
   docker ps --filter name=sqlserver   # 確認既有容器在跑（不是本專案啟動它）
   ./deploy/local-ddl.sh --apply
   ./db/seed/apply-seed.sh
   ```
2. 確認 `.env` 有 `MSSQL_DEV_SA_PASSWORD`，且**值與該既有 `sqlserver` 容器的 SA 密碼一致**
   （這是既有容器，密碼不是本專案決定的）。

### 直接用 dotnet 跑（不經 Docker，最快的開發迴圈）

```bash
cd apps/api
dotnet build

# 連線字串等機密一律用環境變數，⛔ 絕對不要寫進任何檔案並 commit。
# 🔴 用 shell 變數存這種帶分號的連線字串時務必加引號——不加引號被當成 shell 腳本
#    source 執行時，分號會被解讀成命令分隔符，字串會在第一個分號處被悄悄截斷
#    （見 docs/18-work-errors.md，本次任務期間在本機驗證時才發現，非常隱蔽因為
#    程式仍會啟動、仍會嘗試連線，只是連線字串缺了 Database／帳密／TrustServerCertificate）。
export ASPNETCORE_ENVIRONMENT=Development
export CLUB_SQL_CONNECTION_STRING="Server=127.0.0.1,1433;Database=tcrfc_club_dev;User Id=sa;Password=<你的 MSSQL_DEV_SA_PASSWORD>;TrustServerCertificate=True;Encrypt=False;"
export CORS_ALLOWED_ORIGINS="http://localhost:3000,http://localhost:3001"
export ASPNETCORE_URLS="http://127.0.0.1:5299"

dotnet run --no-launch-profile
```

> 🔴 **不要直接 source `deploy/dev/club.env` 的 `CLUB_SQL_CONNECTION_STRING`。**
> 那份檔案是給 **Docker 容器**用的，`Server=host.docker.internal,1433`——在宿主機上直接
> `dotnet run` 會解析不到那個主機名。要用它就得改兩個地方：
> **`host.docker.internal` → `127.0.0.1`**，並**補上 `Encrypt=False;`**
> （少了它會連到伺服器但卡在 `error: 35 - 攔截到內部例外狀況`／「登入前的信號交換時發生錯誤」）。
>
> ⚠️ **這個坑特別難發現，因為 `/healthz` 會騙你**：它只回報行程活著，**不碰資料庫**，
> 所以連線壞掉時它照樣回 `{"status":"ok"}`。2026-09-23 就因此白跑了兩輪比對——
> 前台頁面渲染成「共 0 場」「篩選器空白」，被誤判成前端的真差異。
> **驗證連線一律用會真的查資料的端點**，不要只看 `/healthz`：
> ```bash
> curl -s http://127.0.0.1:5299/api/v1/tcrfc/schedule | head -c 200   # 要看到真的賽程 JSON
> ```
> 一行版（從 `club.env` 取密碼、自己組連線字串，不必把密碼寫進指令）：
> ```bash
> PW=$(grep "^CLUB_SQL_CONNECTION_STRING" ../../deploy/dev/club.env | sed -n 's/.*Password=\([^;]*\);.*/\1/p')
> export CLUB_SQL_CONNECTION_STRING="Server=127.0.0.1,1433;Database=tcrfc_club_dev;User Id=sa;Password=${PW};TrustServerCertificate=True;Encrypt=False;"
> ```

> 🔴 **本機沒設 `DATA_PROTECTION_KEYS_PATH` 時，每次重啟 `dotnet run` 都會讓已經完成兩階段
> 驗證設定的帳號永久解不開密鑰**（2026-09-24，重啟開發行程修另一個問題時實際踩到）——
> `Program.cs` 沒讀到這個環境變數就不會呼叫 `PersistKeysToFileSystem`，Data Protection 金鑰環
> 只存在那個行程的記憶體裡，行程一停金鑰就沒了，舊行程加密過的
> `admin_users.two_factor_secret_encrypted` 全部變成新行程解不開的密文（`two_factor_enabled`
> 欄位本身不會被清掉，畫面上看起來像帳號還在，但輸入任何驗證碼都不可能驗證成功）。
> **重現方式**：完成某個帳號的兩階段驗證設定 → 重啟 `dotnet run` → 用同一組帳密再登入 → 卡在
> 「請輸入兩階段驗證碼」但沒有任何碼算得出來。**解法只有一個**：
> `set -a; source .env; set +a; ./db/seed/reset-admin-accounts.sh`（把種子測試帳號的
> `two_factor_enabled`／`two_factor_secret_encrypted` 都重設回種子初始值，之後可以重新走一次
> 設定）——這支腳本本來就是設計來處理「端對端驗收弄髒種子帳號狀態」的既定還原工具，不是這次
> 才新增的因應措施。
>
> **建議本機也固定一個金鑰目錄**，讓一般的重啟（沒有清資料庫）不會把 2FA 弄丟：
> ```bash
> mkdir -p "$HOME/.local/share/tcrfc-dev/dataprotection-keys"
> export DATA_PROTECTION_KEYS_PATH="$HOME/.local/share/tcrfc-dev/dataprotection-keys"
> ```
> 選在**repo 目錄之外**（使用者家目錄下）是刻意的：金鑰只要沒被 commit 就不算違反規則，但
> 放在 repo 外面連「要不要靠 `.gitignore` 擋」這個問題都不會出現，比在 repo 裡挑一個目錄再去
> 確認 `.gitignore` 涵蓋到它更不容易日後被改壞。**如果偏好放在 repo 裡**（例如想跟專案其他
> 本機產生物放一起），至少要先用 `git check-ignore -v <路徑>` 確認真的被排除、且是用
> `apps/**/` 這種任意深度萬用字元（`docs/18-work-errors.md` `E-27` 記過 `apps/*/` 單層
> 萬用字元擋不到子目錄的坑），**金鑰檔案本身不得出現在 `git status` 裡**。這個環境變數是
> **選用**，不設也能跑（本機開發沒有它一樣能起服務，只是每次重啟都要重設 2FA，見上方
> 「怎麼跑」開頭「連線字串等機密一律用環境變數」的既有慣例——這個變數的值不是機密，是
> 一個檔案系統路徑，不需要額外保護）。

### 要連真的 Redis（選用）

不帶 `REDIS_HOST` 時注入的是 `NoOpQueryCache`，**快取路徑完全不會被執行**——
要驗快取行為（版本號失效、TTL、single-flight、fail-open）就得接真的 Redis。

```bash
# 旗標與正式環境一致（docs/17-deployment.md §1）：512mb、allkeys-lru、不開持久化
redis-server --port 16379 --requirepass dev-redis-password \
  --maxmemory 512mb --maxmemory-policy allkeys-lru --save "" --appendonly no &

export REDIS_HOST=127.0.0.1 REDIS_PORT=16379 REDIS_PASSWORD=dev-redis-password
dotnet run --no-launch-profile
```

驗證（2026-09-23 實測）：

```bash
curl -s http://127.0.0.1:5299/readyz
# {"status":"ready","club_db":"ok","charity_db":"not_configured","redis":"ok"}

curl -s -o /dev/null http://127.0.0.1:5299/api/v1/tcrfc/schedule
redis-cli -p 16379 -a dev-redis-password --no-auth-warning KEYS '*'
# v0:tcrfc:zh-Hant:schedule::::1:20      ← 版本號:俱樂部:語系:entity:qualifier
# v0:tcrfc:_any:club-scope:              ← _any 對應 9 張 club_id 可為空的共同資料表
```

🔴 **為什麼不用 `docker-compose.yml` 裡的 `redis` 服務？** 那個服務是
`networks: [internal]` 且 ⛔ **絕對不能加 `ports:`**（`docs/17` §1 規則 1：Docker 發布
連接埠會**繞過 UFW 直接改 iptables**，主機防火牆關了也擋不住）。而本機開發是
**`dotnet run` 跑在宿主機上**，碰不到 internal 網路裡的容器。
所以只有兩條路：**整套用 compose 跑（`api` 也進容器）**，或**在宿主機跑一個獨立的 Redis**。
上面用的是後者，跟 `sqlserver` 已經是「宿主機上一個獨立於 compose 專案的容器」同一個模式。
⛔ **不要為了本機方便就去 `docker-compose.yml` 的 `redis` 加 `ports:`**——那份檔案是正式環境用的。

⚠️ **`16379` 不是預設的 `6379`**，刻意避開以免跟機器上其他東西撞；`REDIS_PORT` 可帶，不必改程式。
⚠️ 密碼 `dev-redis-password` 與 `docker-compose.dev.yml` 的預設值一致，**僅限本機**，
正式環境的 `REDIS_PASSWORD` 放 `.env`（不納版控）。

驗證：
```bash
curl http://127.0.0.1:5299/healthz     # {"status":"ok"}
curl http://127.0.0.1:5299/readyz
# {"status":"ready","club_db":"ok","charity_db":"not_configured","redis":"not_configured"}
# （club_db／charity_db 都是實際開一條連線查 SELECT 1；charity_db／redis 沒設定對應環境變數時
#  回 not_configured，不影響 ready，見下方「/readyz 範圍」）
curl http://127.0.0.1:5299/api/v1/clubs
```

Swagger／OpenAPI 只在 `ASPNETCORE_ENVIRONMENT=Development` 開放：`http://127.0.0.1:5299/openapi/v1.json`
（沒有掛 Swagger UI 頁面，本次只用建置期就有的 `Microsoft.AspNetCore.OpenApi`，之後要加互動式 UI 可疊 Scalar／Swashbuckle）。
**正式環境（`ASPNETCORE_ENVIRONMENT=Production`）此端點回 404，已用容器實測驗證**（見下方「驗收紀錄」）。

### 用容器跑（貼近正式環境的驗證）

```bash
cd apps/api
docker build -t tcrfc-api-local .

docker run -d --name tcrfc-api-local \
  --add-host=host.docker.internal:host-gateway \
  -e CLUB_SQL_CONNECTION_STRING="Server=host.docker.internal,1433;Database=tcrfc_club_dev;User Id=sa;Password=<你的 MSSQL_DEV_SA_PASSWORD>;TrustServerCertificate=True;" \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e CORS_ALLOWED_ORIGINS="http://localhost:3000" \
  -p 18080:8080 \
  tcrfc-api-local

curl http://127.0.0.1:18080/healthz
docker inspect --format='{{.State.Health.Status}}' tcrfc-api-local   # 等 healthcheck 跑完應為 healthy
```

`host.docker.internal` 是容器連回宿主機（`sqlserver` 容器對外發布的 `1433` port）的方式；
`--add-host=host.docker.internal:host-gateway` 在 macOS 的 Docker Desktop 上其實不必要
（已內建），但補上這行是為了 Linux 相容（Docker 20.10+ 才有 `host-gateway` 這個特殊值），
兩邊都能用。真正的 `docker-compose.yml` 疊 `docker-compose.dev.yml` 跑起來時，`api` 服務已經
在 `docker-compose.dev.yml` 加了同樣的 `extra_hosts`（見 `deploy/dev/club.env`
的 `CLUB_SQL_CONNECTION_STRING` 也是 `host.docker.internal,1433`），不需要在指令列額外帶這個參數。

⚠️ **2026-09-21 之前**這裡連的是本專案自己起的 `mssql-dev` 服務、對外埠 `14330`——那個服務
已退場，現在的既有 `sqlserver` 容器用的是標準 `1433` port，且不是本專案 compose 管理的服務，
容器間無法用服務名稱互連，一律要透過 `host.docker.internal` 這條路徑（即使是
`docker-compose.yml` 疊 `docker-compose.dev.yml` 跑也一樣，因為 `sqlserver` 不在同一個
compose 網路裡）。

正式部署（VM 上、走 `docker-compose.yml`）的環境變數來源見 [`docs/17-deployment.md`](../../docs/17-deployment.md) §1
與 [`docs/20-cicd.md`](../../docs/20-cicd.md) §7.2——本檔不重複那份清單，只列本次新增／確認用得到的鍵名（見下）。

---

## 環境變數

| 變數 | 必填 | 說明 |
|---|---|---|
| `CLUB_SQL_CONNECTION_STRING` | ✅ | 主站庫連線字串。本機見 [`deploy/dev/club.env`](../../deploy/dev/club.env)（不進版控），正式環境見 VM 上 `/opt/tcrfc/secrets/club.env`（`docs/20-cicd.md` §7.2）。**鍵名故意跟兩邊祕密檔一致**，不繞去 `ConnectionStrings:Club` 這種 ASP.NET Core 慣用間接層——本檔的 `ClubSqlConnectionFactory` 直接讀這個鍵。⛔ **不得寫死在任何檔案裡** |
| `CHARITY_SQL_CONNECTION_STRING` | 選填（S0-7d） | 慈善庫連線字串，**只用在 `/readyz` 開一條連線查 `SELECT 1`**，本檔沒有任何 repository 或查詢邏輯碰這個庫。沒設定時 `/readyz` 的 `charity_db` 回 `not_configured`（不影響 ready）；設定了卻連不上回 `fail`（連帶 not ready）。見 [`deploy/dev/charity.env.example`](../../deploy/dev/charity.env.example) |
| `REDIS_HOST` | 選填（S0-7d） | 有設定才會注入 `RedisQueryCache`，沒設定注入 `NoOpQueryCache`。`docker-compose.yml`／`docker-compose.dev.yml` 的 `api` 服務**一律有帶**（值為 `redis`），只有本機直接 `dotnet run`（不經 Docker）不帶時才會落到 no-op |
| `REDIS_PORT` | 選填（S0-7d） | 預設 `6379`。本次新增這個變數主要是為了讓測試能指到一個確定沒人聽的本機連接埠（見 `Tcrfc.Api.Tests`），正式環境不需要設定 |
| `REDIS_PASSWORD` | `REDIS_HOST` 有設定時對應生效 | 與 `docker-compose.yml`／`deploy/dev/club.env` 的既有鍵名一致 |
| `QUERY_CACHE_TTL_SECONDS` | 選填（S0-7d） | 每個快取 key 的 TTL 秒數，預設 `300`（5 分鐘）。預設值的理由與可否調整見下方「快取接縫」段落 |
| `SCHEDULED_PUBLISH_INTERVAL_SECONDS` | 選填（S0-7g） | 排程發布掃描的輪詢間隔秒數，預設 `60`。設定成 `< 1` 會記警告並退回預設值，不會讓服務啟動失敗。理由與可否調低見下方「排程發布：時間到了自動轉為 published」段落 |
| `CORS_ALLOWED_ORIGINS` | 正式環境必填，本機可省略 | 逗號分隔的允許來源清單，來自 `docker-compose.yml` 的 `api` 服務定義（`docs/17-deployment.md` §10.2 的既有缺口，本次由前一任務補上）。本機開發若沒帶，`Development` 環境會退回 `localhost:3000/3001/3002` 三個 `apps/web` 常用埠；**正式環境沒有這個退回值**——沒設定就是沒有任何來源被允許，比「忘記設定就開放全部」安全 |
| `TRUSTED_PROXY_IP` | 正式環境必填（否則限流失去意義），本機可省略 | S1-10 新增：唯一被信任、可以用 `X-Forwarded-For` 覆寫訪客真實 IP 的來源（`docker-compose.yml` 的 `proxy` 服務固定 IP `172.28.238.2`）。**未設定時 `Program.cs` 不會呼叫 `app.UseForwardedHeaders()`**——千萬不要假設「沒設定就是安全的預設值」，`ForwardedHeadersMiddleware` 把空的信任清單當成「信任所有來源」，見 `Security/TrustedProxyConfiguration.cs` 檔頭「未設定時中介軟體本身完全不掛」的完整說明 |
| `ASPNETCORE_ENVIRONMENT` | 建議設 | `Development` 才會開 OpenAPI 端點，其餘值一律關閉 |
| `ASPNETCORE_URLS` | 本機開發用 | 監聽位址，容器內固定用 `Dockerfile` 的 `ASPNETCORE_HTTP_PORTS=8080` |
| ~~`ENABLE_UNSAFE_DEV_WRITES`~~ | 2026-09-23 起不存在 | 舊機制的環境旗標，隨 `Security/DevWriteGate.cs` 一併刪除，本檔任何程式碼都不再讀取這個鍵名，見「開發模式開關：已刪除」整節 |
| `AZURE_BLOB_CONNECTION_STRING` | 選填（S0-8） | 圖片上傳共用元件的物件儲存連線字串。**未設定不會讓服務無法啟動**（跟 `CLUB_SQL_CONNECTION_STRING` 不同）——只有真的呼叫圖片上傳／刪除時才會需要它，沒設定時注入 `UnavailableImageStorageService`（上傳丟出訊息清楚的例外，刪除安靜略過）。本機開發見下方「本機開發：Azurite」，正式環境見 VM 上 `/opt/tcrfc/secrets/club.env` |
| `AZURE_BLOB_CONTAINER_IMAGES` | 選填（S0-8） | 圖片物件儲存的容器名稱，預設 `images` |
| `JWT_SIGNING_KEY_CLUB` | 🔴🔴🔴 S1 起必填 | 後台存取權杖的簽章金鑰，**至少 32 字元，太短直接啟動失敗**（`AdminTokenService` 的建構期檢查，寧可啟動失敗也不要用弱金鑰悄悄跑起來）。鍵名不是本輪新發明，`deploy/dev/club.env`／`docs/20-cicd.md` §7.2 早就預留。⚠️ **上線前暫用網址與正式期建議用不同值**（`docs/14-invariants.md` 既有規則） |
| `DATA_PROTECTION_KEYS_PATH` | 🔴🔴🔴 S1 起正式環境必填 | 2FA 密鑰加密金鑰環的持久化路徑。**沒設定不會讓服務無法啟動**（本機開發沒有也能跑，只是每次容器重建都要重設 2FA），但正式環境沒設定＝容器重建後全部使用者的 2FA 永久無法解密，見 `Security/TwoFactorSecretProtector.cs` 檔頭的完整說明，這是本次程式碼無法防呆的部署前置條件 |

⛔ **S0-8 之後仍完全不碰 LINE Pay**——這個鍵名雖然已經在 `docker-compose.yml` 的
`api` 服務與 `deploy/dev/{club,charity}.env` 裡預留，但本檔的程式碼**沒有讀取它**，留給接下來實作商店金流的
session 使用。**Blob 已在 S0-8 接上，JWT 已在 S1 接上**，見下方對應段落。

---

## 端點清單

所有端點前綴 `/api/v1`；除 `/api/v1/clubs` 系列外，其餘一律要求路由帶 `{club}`（俱樂部代碼，如 `tcrfc`、`bw`）。

| 方法與路徑 | 說明 | 查詢參數 |
|---|---|---|
| `GET /api/v1/clubs` | 俱樂部清單 | `lang` |
| `GET /api/v1/clubs/{club}` | 單一俱樂部主檔 | `lang` |
| `GET /api/v1/{club}/players` | 球員名單 | `team`（球隊代碼）、`lang`、`page`、`pageSize`（預設 50，上限 200） |
| `GET /api/v1/{club}/staff` | 教練與職員 | `team`、`lang`、`page`、`pageSize`（預設 50，上限 200） |
| `GET /api/v1/{club}/news` | 新聞列表 | `category`（分類代碼）、`lang`、`page`、`pageSize`（預設 20，上限 100） |
| `GET /api/v1/{club}/news/{slug}` | 新聞單篇 | `lang` |
| `GET /api/v1/{club}/schedule` | 賽程與賽果 | `team`、`season`（賽季代碼）、`status`、`lang`、`page`、`pageSize`（預設 20，上限 100） |
| `GET /healthz` | 存活探針 | — |
| 🔒 `GET /api/v1/admin/{club}/news` | 需登入＋`content.article.view`。後台新聞清單，**含全部狀態**（草稿／排程／已發布） | `status`、`category`、`keyword`（搜中文標題）、`page`、`pageSize`（預設 20，上限 100） |
| 🔒 `GET /api/v1/admin/{club}/news/{id}` | 需登入＋`content.article.view`。後台單篇詳情（依 GUID，不是 slug），雙語內容不回退，原封回傳 | — |
| 🔒 `POST /api/v1/admin/{club}/news` | 需登入＋`content.article.create`。建立文章，一律從 `draft` 開始 | — |
| 🔒 `PUT /api/v1/admin/{club}/news/{id}` | 需登入＋`content.article.update`。整份取代可編輯內容，不改狀態（樂觀並行） | — |
| 🔒 `POST /api/v1/admin/{club}/news/{id}/publish` | 需登入＋`content.article.publish`。`draft`／`scheduled` → `published`，立即生效 | — |
| 🔒 `POST /api/v1/admin/{club}/news/{id}/schedule` | 需登入＋`content.article.publish`。`draft`／`scheduled` → `scheduled`（未來時間） | — |
| 🔒 `DELETE /api/v1/admin/{club}/news/{id}` | 需登入＋`content.article.delete`。刪除（樂觀並行） | `expectedUpdatedAt`（必填，ISO 8601） |
| `GET /readyz` | 就緒探針（真的開連線查主站庫，見下方「/readyz 範圍」） | — |
| 🔴🔴🔴 `POST /api/v1/admin/auth/login` | S1 新增。帳號密碼＋選填 2FA 碼登入 | — |
| 🔴🔴🔴 `POST /api/v1/admin/auth/refresh` | S1 新增。用 `__Host-tcrfc-admin-rt` Cookie 換一把新的存取權杖（輪替更新權杖） | — |
| 🔴🔴🔴 `POST /api/v1/admin/auth/logout` | S1 新增。撤銷更新權杖、清 Cookie | — |
| 🔒 `POST /api/v1/admin/auth/change-password` | S1 新增。需登入（不需俱樂部授權）。首次登入強制更換走這裡 | — |
| 🔒 `POST /api/v1/admin/auth/2fa/setup` | S1 新增。需登入。開始 2FA 設定，回傳 Base32 密鑰與 `otpauth://` URL | — |
| 🔒 `POST /api/v1/admin/auth/2fa/confirm` | S1 新增。需登入。驗證第一組 TOTP 碼，通過才真的打開 2FA | — |
| 🔒 `POST /api/v1/admin/auth/2fa/disable` | S1 新增。需登入＋重輸密碼 | — |
| 🔒🔴 `GET /api/v1/admin/accounts` | S1-3 續作新增。需登入＋`system.account.view`（`sysadmin_only`）。帳號清單，全域端點 | `status`、`keyword`、`page`、`pageSize` |
| 🔒🔴 `GET /api/v1/admin/accounts/{id}` | 同上＋`system.account.view` | — |
| 🔒🔴 `POST /api/v1/admin/accounts` | 同上＋`system.account.create`。建立帳號，一律強制 `must_change_password=true` | — |
| 🔒🔴 `PUT /api/v1/admin/accounts/{id}` | 同上＋`system.account.update`。更新基本資料與角色指派 | — |
| 🔒🔴 `POST /api/v1/admin/accounts/{id}/status` | 同上＋`system.account.update`。啟用／停用，停用立即撤銷既有更新權杖 | — |
| 🔒🔴 `POST /api/v1/admin/accounts/{id}/reset-password` | 同上＋`system.account.update`。代為重設密碼，撤銷既有更新權杖 | — |
| 🔒🔴 `POST /api/v1/admin/accounts/{id}/reset-totp` | 同上＋`system.account.update`。代為重設 2FA，撤銷既有更新權杖 | — |
| 🔒🔴 `GET /api/v1/admin/accounts/{id}/club-grants` | 同上＋`system.club_grant.view`。這個帳號的俱樂部授權（J4） | — |
| 🔒🔴 `POST /api/v1/admin/accounts/{id}/club-grants` | 同上＋`system.club_grant.update`。新增或重新啟用授權，立即生效 | — |
| 🔒🔴 `DELETE /api/v1/admin/accounts/{id}/club-grants/{clubId}` | 同上＋`system.club_grant.update`。撤銷授權，立即生效 | — |
| 🔒🔴 `GET /api/v1/admin/accounts/{id}/team-grants` | 第二輪補派新增。需登入＋`system.team_grant.view`（`sysadmin_only`）。這個帳號的球隊授權（J4） | — |
| 🔒🔴 `POST /api/v1/admin/accounts/{id}/team-grants` | 同上＋`system.team_grant.update`。新增或重新啟用授權，只能授權該帳號目前有效俱樂部授權範圍內的球隊，立即生效 | — |
| 🔒🔴 `DELETE /api/v1/admin/accounts/{id}/team-grants/{teamId}` | 同上＋`system.team_grant.update`。撤銷授權，立即生效 | — |
| 🔒🔴 `GET /api/v1/admin/roles/permissions` | S1-3 續作新增。需登入＋`system.role.view`。權限碼字典 | — |
| 🔒🔴 `GET /api/v1/admin/roles` | 同上＋`system.role.view`。角色清單（十個種子角色＋自訂角色） | — |
| 🔒🔴 `GET /api/v1/admin/roles/{id}` | 同上＋`system.role.view` | — |
| 🔒🔴 `POST /api/v1/admin/roles` | 同上＋`system.role.update`。建立自訂角色 | — |
| 🔒🔴 `PUT /api/v1/admin/roles/{id}` | 同上＋`system.role.update` | — |
| 🔒🔴 `DELETE /api/v1/admin/roles/{id}` | 同上＋`system.role.update`。系統角色或仍被指派的角色會擋下（403／409） | — |
| 🔒🔴 `PUT /api/v1/admin/roles/{id}/permissions` | 同上＋`system.role.update`。整份取代非 `sysadmin_only` 的權限指派 | — |
| 🔒🔴 `GET /api/v1/admin/clubs` | S1-3 續作新增。需登入＋`system.club.view`（`sysadmin_only`）。俱樂部主檔清單 | — |
| 🔒🔴 `GET /api/v1/admin/clubs/{id}` | 同上＋`system.club.view` | — |
| 🔒🔴 `POST /api/v1/admin/clubs` | 同上＋`system.club.update` | — |
| 🔒🔴 `PUT /api/v1/admin/clubs/{id}` | 同上＋`system.club.update` | — |
| 🔒 `GET /api/v1/admin/{club}/competitions` | S1-3 續作新增。需登入＋`team.competition.view`。賽事系列清單，俱樂部範圍 | `seasonId`、`status` |
| 🔒 `GET /api/v1/admin/{club}/competitions/{id}` | 同上＋`team.competition.view` | — |
| 🔒 `POST /api/v1/admin/{club}/competitions` | 同上＋`team.competition.create`。狀態只接受 `draft`／`published` | — |
| 🔒 `PUT /api/v1/admin/{club}/competitions/{id}` | 同上＋`team.competition.update` | — |

🔒 標記的端點需要 `Authorization: Bearer <存取權杖>`，未登入回 401、已登入但無權回 403，
見「S1：J1–J3 登入與授權地基」整節。🔴 標記的是**全域端點**（不含 `{club}` 路由段，用
`IAdminSystemAuthorizer`），其餘 🔒 端點是俱樂部範圍（用 `IAdminClubAuthorizer`），見「S1-3
續作：J1／J2／J4 端點」整節。

`lang` 值域 `zh`／`en`（不帶預設 `zh`），與 [`docs/06-conventions.md`](../../docs/06-conventions.md)「語系代碼」一致，
不是資料庫實際存的 `zh-Hant`／`en`（轉換邏輯見 `Localization/RequestLocale.cs`）。

分頁回應統一外殼：
```json
{ "items": [...], "page": 1, "pageSize": 20, "totalCount": 83, "totalPages": 5 }
```
`page`／`pageSize` 一律正規化（`page<=0` → `1`；`pageSize<=0` → 該端點預設值；超過上限 → 截到上限），
不會讓呼叫端用超大 `pageSize` 一次撈整張表。

### `/readyz` 範圍（S0-7d 已補齊）

`apps/api/Dockerfile` 原註解寫「真的檢查兩個 DbContext 能連線」。S0-7b 當時刻意縮減範圍只驗證主站庫，
S0-7d 補上慈善庫與 Redis，三者的失敗語意各不相同：

| 檢查項 | 沒設定對應變數 | 設定了但連不上 | 對 `status` 的影響 |
|---|---|---|---|
| `club_db`（`CLUB_SQL_CONNECTION_STRING`） | 不適用（必填） | `fail` | 連不上 → `not_ready`（503）——既有行為，未改 |
| `charity_db`（`CHARITY_SQL_CONNECTION_STRING`） | `not_configured` | `fail` | 沒設定不影響；連不上 → `not_ready`（503） |
| `redis`（`REDIS_HOST`） | `not_configured` | `degraded` | **兩種情況都不影響 `status`**，永遠不會因為 Redis 而 not ready |

`charity_db` 的「沒設定＝not_configured、不影響 ready」是刻意的：慈善平台是還沒開工的獨立交付物
（獨立後台、獨立資料庫，`docs/17-deployment.md` §5），大多數環境（本機、CI、正式站上線初期）本來就
不會設定這個變數，不該為一個沒人用的連線讓整個 `api` 容器被判定 unhealthy。`redis` 的「連不上也不影響
ready」直接對應 [`docs/17-deployment.md`](../../docs/17-deployment.md) §4「連線失敗算警告不算失敗」——
Redis 掛掉時服務仍應該收流量（每個讀取會 fail-open 回源 SQL，只是變慢）。

🔴 **慈善庫檢查只開連線查 `SELECT 1`，不做任何查詢，也不建立任何 repository 或連線工廠**——
慈善庫是台灣足球策略發展協會（另一個法人）的資料，`docs/14-invariants.md`、`docs/17-deployment.md` §5
明訂不得跨庫存取；本檔完全沒有為慈善庫建立任何可被日後誤用來查資料的常駐管道，用完即丟。

---

## 每個端點吐出哪些欄位、為什麼可以公開

先查過 [`docs/12b-database-tables.md`](../../docs/12b-database-tables.md) §8「受限與加密欄位盤點」：
`players`／`staff`／`articles`／`matches`／`clubs`／`teams`／`article_categories`／`competitions`／`seasons`
**都不在那份清單裡**——受限清單裡的表全是 `Member`／`Registration`／`AdminUser`／`Order`／`DrawRoster`／
`PaymentChannel`／`StoreInvoice` 這類承載個資或金流的表，本次沒有碰到任何一張。

以下逐端點列出**明確 SELECT 的欄位**（⛔ 全程沒有任何 `SELECT *`）：

### `GET /api/v1/clubs`、`GET /api/v1/clubs/{club}`

| 欄位 | 公開理由 |
|---|---|
| `code`、`domain`、`defaultLocale` | 俱樂部的公開識別資訊，前台切換站台就是靠這個 |
| `name`、`description`（`clubs_i18n`） | GEO-03「事實單一來源」要用的官方名稱與簡介 |
| `logoLightKey`／`logoDarkKey`／`faviconKey`／`ogImageKey` | 品牌資產物件鍵（**目前種子資料全為 `NULL`**，尚未走過上傳即縮圖 pipeline，見「已知落差」） |
| `brandColor`／`brandSecondaryColor` | 前台品牌色參考值（實際網頁配色仍以 CSS custom properties 為唯一來源，`docs/14-invariants.md`，這裡只是資料庫存的參考色值） |

⛔ **不吐出**：`invoice_title`、`tax_id`（發票與稅務欄位，不是前台事實內容，本次不公開）、`status`（後台操作狀態）。

### `GET /api/v1/{club}/players`

| 欄位 | 公開理由 |
|---|---|
| `id`、`teamCode`、`shirtNo`、`position`、`birthOn`、`heightCm`、`weightKg`、`nationality`、`preferredFoot`、`photoKey`、`name`、`bio` | 球隊官網例行公開的球員名冊資訊（背號、位置、身材、國籍、簡介），`players` 不在 §8 受限清單，且前台本來就有「球員名單」公開頁面（`docs/02-frontend-spec.md`） |

### `GET /api/v1/{club}/staff`

| 欄位 | 公開理由 |
|---|---|
| `id`、`staffGroup`、`licence`、`photoKey`、`name`、`title`、`bio`、`teamCodes`、`isShared` | 教練與團隊成員簡介，官網例行公開內容。`isShared` 是本 API 自己加的旗標（見下方「`club_id` 強制機制」），不是資料庫欄位，用來讓前台知道這筆是不是兩隊共同資料 |

### `GET /api/v1/{club}/news`、`GET /api/v1/{club}/news/{slug}`

| 欄位 | 公開理由 |
|---|---|
| `id`、`slug`、`categoryCode`／`categoryName`、`coverKey`、`isFeatured`、`publishedAt`、`title`、`summary`、`isShared` | 新聞列表頁需要的欄位，`articles` 不在受限清單。⛔ **只回傳 `status='published'` 且已到發布時間的文章**——草稿與排程中的文章即使 slug 被猜到也一律 404，這是刻意的業務規則 |
| 單篇另外多帶：`viewCount`、`bodyJson`、`seoTitle`、`seoDescription` | 單篇詳情頁才需要，列表故意不帶（避免每次列表都拉可能很大的 `body` JSON） |

### `GET /api/v1/{club}/schedule`

| 欄位 | 公開理由 |
|---|---|
| `id`、`seasonCode`、`teamCode`、`matchOn`、`kickoff`、`homeAway`、`opponent`、`venue`、`competitionTag`／`competitionName`、`status`、`scoreHome`、`scoreAway`、`roundNo`、`matchNo`、`originalMatchOn`／`originalKickoff` | 賽程賽果公開頁面的標準欄位，`matches` 不在受限清單。`matchNo`（聯賽官方場次編號，`matches.match_no`）2026-09-21 補上，前台用它重建 mockup 原本的賽事卡片錨點 id（`fx-{日期}-{h\|a}-{場次編號}`），與 `roundNo`（第幾輪）是兩個不同欄位。`originalMatchOn`／`originalKickoff`（`matches.original_match_on`／`original_kickoff`，v3.13「延賽須標示原定時間」）2026-09-24 補上（S0-9l 後端半段）：**只有 `status = 'postponed'` 時才有值，其餘一律 `null`**——種子資料目前沒有任何延賽紀錄，這兩欄只用 `ScheduleOriginalDateTests` 的人造測資驗過，沒有真實延賽資料可以核對 |

---

## `club_id` 強制機制：為什麼繞不過去

`docs/14-invariants.md` 與主站規劃書 §5.4 明訂「`club_id` 過濾不得依賴呼叫端」。本次的落點是
**型別系統**，不是「每個查詢記得加 WHERE」：

1. **`Security/ClubScope.cs`** 是一個 `readonly struct`，建構子是 `internal`。整個 `Tcrfc.Api` 組件裡，
   只有 `Security/ClubResolver.cs`（`IClubResolver` 的唯一實作）能建立它的實例。
2. **所有 repository 的公開方法簽章都要求 `ClubScope`**，不是 `Guid` 或 `string`
   （例如 `PlayersRepository.ListAsync(ClubScope scope, ...)`）。呼叫端**編譯不過**，除非先呼叫
   `IClubResolver.ResolveAsync(clubCode, ct)` 拿到一個真正的 `ClubScope`。
3. **`ClubResolver.ResolveAsync`** 對 `clubs` 表做參數化查詢（`WHERE code = @Code AND status = 'active'`），
   查不到就丟 `ClubNotFoundException`（對應 404）。端點永遠拿不到指向不存在、或非啟用俱樂部的範圍。
4. Repository 內部組 SQL 時，`club_id` 一律用 `scope.ClubId`（來自已驗證的 `ClubScope`），
   **不是從路由字串或任何呼叫端輸入直接組**。

**「俱樂部專屬優先、回退共同」**（9 張 `club_id` 可為空的表，本次碰到 `staff`／`articles`）的弱讀法
集中在 `Data/ClubOrSharedSql.cs` 的兩個常數（`WhereClubOrShared`／`OrderClubBeforeShared`），
`docs/17-deployment.md` §6 的 SQL 寫法原封搬過來，**不是每個 repository 各自重寫一份等價邏輯**。

### 實測：試圖繞過的案例（見下方「驗收紀錄」有完整 curl 輸出）

1. **跨俱樂部讀清單**：`bw`（藍鯨）目前只有 `clubs` 主檔一筆，沒有球員／教練／新聞資料
   （見 [`db/seed/README.md`](../../db/seed/README.md)「藍鯨為什麼只有這一筆」）。
   打 `GET /api/v1/bw/players`、`/staff`、`/news`、`/schedule` 全部回傳 `totalCount: 0`，
   即使 `tcrfc` 底下有 28 筆球員、83 篇新聞、21 場賽事——證明 `club_id` 過濾確實生效，不是「忘記加條件」。
2. **跨俱樂部讀單篇（更直接的繞過嘗試）**：`articles.slug` 是**全站唯一**（不是 `(club_id, slug)` 複合唯一），
   理論上「知道別俱樂部一篇文章的 slug」就有機會繞過範圍檢查直接讀到內容。實測：先用
   `GET /api/v1/tcrfc/news/{slug}` 確認某篇文章存在且屬於 `tcrfc`，換成
   `GET /api/v1/bw/news/{slug}`（同一個 slug，改用 `bw` 的路由）**回傳 404**，不是內容。
   這是因為 `GetBySlugAsync` 的 WHERE 子句永遠帶 `(club_id = @ClubId OR club_id IS NULL)`，
   `@ClubId` 來自已解析的 `bw` 範圍，跟這篇文章實際的 `club_id`（`tcrfc`）對不上。
3. **不存在的俱樂部代碼**：`GET /api/v1/does-not-exist/players` 回傳 `404`（`ClubNotFoundException`），
   連「範圍是什麼」都建立不起來，更談不上查到資料。
4. **SQL Injection**：`GET /api/v1/tcrfc/players?team=D1';DROP TABLE players;--` 回傳
   `{"items":[],...,"totalCount":0}`（合法但查無資料，因為沒有球隊代碼長那樣），資料庫表安然無恙——
   所有查詢一律走 Dapper 的 `CommandDefinition` 參數化，⛔ 全程沒有字串拼接 SQL。

---

## 英文缺漏時的回退行為

見 `Localization/RequestLocale.cs`。規則只有一條，全部端點一致套用：

> **請求語系的欄位值非空白 → 用它；否則 → 用預設語系（`zh-Hant`）的同一欄位值；
> 兩者都沒有 → 回傳 `null`（不是空字串）。**

實測驗證（真實種子資料，見下方「驗收紀錄」）：

- `players_i18n`／`staff_i18n` 的英文姓名是**有的存、有的沒存**（依來源 JSON `name_en` 是否為空字串決定）：
  有存的球員／教練，`?lang=en` 回傳英文名；沒存的（例如教練「許志傑」），`?lang=en` 回傳中文名——
  不會是空字串或 `undefined`。
- `staff_i18n.title`（職稱）**完全沒有任何英文列**：`?lang=en` 時 `title` 欄位一律回退成中文職稱
  （例如「守門員教練」），即使同一筆資料的 `name` 已經是英文——**回退是逐欄位判斷，不是整筆記錄二選一**。
- `matches_i18n` 的 `opponent`／`venue` 種子資料**完全沒有任何英文列**（只有 `venue` 的 `zh-Hant` 列）：
  `?lang=en` 時兩者都回退——`opponent` 回退到 `matches` 基礎表的中文欄位（因為這張表的中文內容存在
  基礎表不是側表，回退鏈與其他實體不同，見 `MatchesRepository` 類別註解），`venue` 回退到
  `matches_i18n` 的 `zh-Hant` 列。
- `competitions_i18n` 只有 `zh-Hant` 列：`?lang=en` 時 `competitionName` 回退成中文賽事名稱
  「企業甲級足球聯賽」，⛔ **不是**因為 SQL JOIN 條件剛好篩不到而悄悄變成 `null`
  （這是本次開發中途發現並修正的一個坑，原本用 `LEFT JOIN ... AND locale = @lang` 會在請求 `en`
  時直接得到 `null`，看起來像正常回應、其實是遺漏，已改成跟其他實體一致的批次回退查詢）。
- `clubs_i18n`（`bw` 台中藍鯨）：`name` 只有 `zh-Hant` 列（無英文），`?lang=en` 回退成「台中藍鯨」；
  `tcrfc` 兩個語系都有，`?lang=en` 正確回傳「Taichung Rock FC」。

---

## 快取接縫（S0-7d 起接上真正的 Redis）

`Caching/IQueryCache.cs` 定義接縫。`Program.cs` 依 `REDIS_HOST` 是否有設定切換 DI 註冊：

- 沒設定 → `NoOpQueryCache`（原樣呼叫 `factory`，不快取）。本機直接 `dotnet run`（不經 Docker）不帶
  這個變數時就是這條路，本機開發不需要 Redis 也能跑。
- 有設定 → `RedisQueryCache`。`docker-compose.yml`／`docker-compose.dev.yml` 的 `api` 服務**一律有帶**
  `REDIS_HOST`，所以容器化跑法（本機 compose 或正式 VM）一定會走這條路。

### 逐條對應 `docs/17-deployment.md` §4「實作的五條硬規則」

| # | 規則 | 落點 |
|---|---|---|
| 1 | fail-open，Redis 掛掉不得讓請求失敗 | `RedisQueryCache` 的每個私有方法（`TryBuildKeyAsync`／`TryGetAsync`／`TrySetAsync`／`InvalidateAsync`）都把 Redis 例外吞掉，讀取失敗一律回源 `factory`，寫入／失效失敗一律忽略（記警告日誌）。`Program.cs` 起始建立 `ConnectionMultiplexer` 也設 `AbortOnConnectFail = false` 並包 try/catch——連 Redis 從一開始就連不上也不得讓行程無法啟動 |
| 2 | 失效用版本號，⛔ 不用 `KEYS` | key 格式 `v{ver}:{club}:{locale}:{entity}:{qualifier}`；失效＝`INCR ver:{entity}:{club}`（`IQueryCache.InvalidateAsync`）。全程沒有任何 `KEYS`／`SCAN` |
| 3 | 每個 key 一定要有 TTL 兜底 | 見下方「TTL 決定」 |
| 4 | single-flight | `RedisQueryCache` 內部 `ConcurrentDictionary<string, SemaphoreSlim>`，per-key 鎖；拿到鎖後再讀一次快取，避免鎖排隊期間前一個請求已經填好 |
| 5 | 五類禁用資料的 repository 根本不注入 | 目前完全沒有任何程式碼把 `IQueryCache` 注入五類禁用資料的 repository（那些 repository 現在也還不存在）——見 `IQueryCache.cs` 上完整抄錄的五類清單 |

### TTL 決定（執行層決定，§4 沒給數字）

預設 **300 秒（5 分鐘）**，可用 `QUERY_CACHE_TTL_SECONDS` 覆寫。理由：**目前後台還不存在，沒有任何寫入層
會呼叫 `InvalidateAsync`**——這是已知且被接受的取捨，TTL 現在是**唯一**會觸發的失效機制，不是「反正有
版本號機制的兜底而已」。5 分鐘讓「後台之後才補上失效呼叫」這段期間的最大陳舊視窗有界，同時仍能吸收
SSR 同一頁重複讀取的量（`docs/17` §4「甜蜜點」講的就是這種反覆被打的小資料）。**之後後台寫入模組做
write-invalidate 時，直接呼叫 `IQueryCache.InvalidateAsync(entity, club, ct)` 即可，介面不用再改**——
這正是本次任務要求「一併提供遞增版本號的公開方法」的目的。

### key 命名維度

`GetOrCreateAsync<T>(entity, club, locale, qualifier, factory, ct)` 四個字串參數對應 `docs/17` §4
「key 命名必須含 club_id 與 locale 維度」；`Caching.CacheDimensions` 提供 `SharedClub`／`AnyLocale`／
`NoQualifier` 三個常數，避免各處各自寫一份「跨俱樂部共用」的魔術字串（字串對不起來＝快取永遠失效或
誤命中）。

### 🔴 `null` 不快取

`RedisQueryCache.GetOrCreateAsync<T>` 只在 `factory` 回傳非 `null` 時才寫入快取（`ClubDto?`、
`ArticleDetailDto?`、`ClubResolver` 內部用的 `Guid?` 皆適用）。查無資料（404）的負向結果不會被快取住，
下一次同樣的請求會直接回源——草稿文章排程發布後、或 slug 打錯字修正後，不會被一個 TTL 週期內的
「查無資料」快取檔住。**傳回空清單**（例如某個篩選條件下 `PagedResult<T>.Items` 為空、`TotalCount`
為 0）**不受影響、正常快取**——`PagedResult<T>` 物件本身從來不是 `null`，「查到 0 筆」是合法且穩定的
答案（`bw` 目前所有內容端點都是這種情況：物件存在、`TotalCount` 是 0，這筆 0 結果本身會被快取）。

### 五組唯讀 repository 全部接上 `IQueryCache`（S0-7d 續作，2026-09-21，使用者拍板）

上一版本次任務原本只在 `Security/ClubResolver.cs` 示範用法、刻意沒有動 `Features/*` 的五個
repository——當時把任務指示「端點與 repository 不得修改」解讀為「連建構子參數都不能加」。
**使用者拍板澄清**：那句話的本意是「接縫從 no-op 換成 Redis 不需要動它們」，不是「一行都不能碰」；
加一個 `IQueryCache` 建構子參數、對外契約（路由、查詢參數、回應 JSON 形狀）完全不變，是接縫本來就
預期的用法。**因此本次把 `ClubsRepository`／`PlayersRepository`／`StaffRepository`／
`ArticlesRepository`／`MatchesRepository` 全部接上**，只改建構子與方法內部（把既有查詢邏輯包進
`cache.GetOrCreateAsync(...)` 的 `factory`），**端點簽章、DTO、JSON 回應欄位逐一未變**——這條線由
`Tcrfc.Api.Tests`（詳見下方「測試」）與 `site/tools/compare-dom.mjs` 既有的逐頁驗收把關。

**逐一 repository 的 entity／club／locale／qualifier：**

| Repository ／方法 | entity | club 維度 | qualifier 涵蓋的參數 |
|---|---|---|---|
| `ClubsRepository.ListAsync` | `clubs-list` | `CacheDimensions.SharedClub`（不屬於任一俱樂部，這支端點本來就回傳全部俱樂部） | 無（只有 locale） |
| `ClubsRepository.GetAsync` | `club-detail` | `scope.ClubCode` | 無（只有 locale）。🔴 查無資料不快取 |
| `PlayersRepository.ListAsync` | `players` | `scope.ClubCode` | `team:page:pageSize` |
| `StaffRepository.ListAsync` | `staff` | `scope.ClubCode` | `team:page:pageSize` |
| `ArticlesRepository.ListAsync` | `articles` | `scope.ClubCode` | `category:page:pageSize` |
| `ArticlesRepository.GetBySlugAsync` | `article-detail` | `scope.ClubCode` | `slug`。🔴 查無資料（404）不快取，見下方「排程發布與快取」 |
| `MatchesRepository.ListAsync` | `schedule` | `scope.ClubCode` | `team:season:status:page:pageSize` |
| `Security/ClubResolver.ResolveAsync`（既有，S0-7d 第一階段） | `club-scope` | 已解析出的俱樂部代碼 | 無 |

`staff`／`articles`／`schedule` 三個 qualifier 裡沒帶值的段落用空字串（不是 `null` 字串），
例如賽程完全不帶篩選時 qualifier 是 `"::1:20"`（team／season／status 三段皆空，page=1，pageSize=20）
——這是刻意的，`CacheDimensions.NoQualifier` 本身就是空字串，讓「沒有這個篩選條件」與「這個篩選條件
剛好是空字串」用同一種表示法，不會有兩種不同的「空」互相衝突。

### ✅ 排程發布：時間到了自動轉為 published（S0-7g，2026-09-24 解決）

`ArticlesRepository` 的兩個方法只回傳 `status = 'published'` **且已到發布時間**
（`published_at <= SYSUTCDATETIME()`）的文章——`status` 必須字面等於 `'published'`，
只有 `published_at` 到期還不夠。**這一段以下是舊版說明，已經不成立，保留刪除線只是讓後續讀者知道
「曾經這樣以為，後來發現不對」**：

> ~~「時間到了」這件事本身沒有任何寫入事件——後台排定 10:00 發布一篇文章，不會有任何程式碼在
> 10:00 那一刻呼叫 `IQueryCache.InvalidateAsync`，「文章排程發布後多久會真的在 API 回應出現」
> 完全由 TTL 決定。~~
>
> 這段推論的前提（沒有寫入事件）是錯的：`docs/17-deployment.md`「排程」一項早就定案
> 「Azure SQL 無 SQL Agent，排程發布、逾時取消訂單、每日對帳一律由 .NET 的 hosted service
> 承擔」，只是當時新聞垂直切片（S0-8）還沒有接上這個 hosted service，導致「排定時間到了，
> `status` 卻永遠停在 `scheduled`」這個落差被實測發現（見下方「已解決」段落）。

**已解決**：`Features/News/ScheduledPublishRunner.cs` ＋ `ScheduledPublishBackgroundService.cs`
是 hosted service 的實作——啟動後立刻執行一次，之後每 `SCHEDULED_PUBLISH_INTERVAL_SECONDS`
秒（預設 60 秒）執行一次，把 `status='scheduled' AND published_at <= SYSUTCDATETIME()` 的文章
轉成 `published`，**同一個交易內**（單一條件式 `UPDATE ... OUTPUT`）就是這個功能唯一的寫入事件，
轉換成功後立刻呼叫 `IQueryCache.InvalidateAsync("articles", club)` 與
`InvalidateAsync("article-detail", club)`——不再是「完全靠 TTL 兜底」，可見延遲改成
**最多一個輪詢間隔（預設 60 秒）**，不是最多一個 TTL 週期（預設 300 秒）。

設計細節（完整理由見 `ScheduledPublishRunner.cs` 檔頭）：

- **冪等且對多實例安全**：條件式 `UPDATE`（不是「先 SELECT 一批 id 再逐筆 UPDATE」），兩個 API
  容器同時跑這支語句時，第二個語句會在同一批列上阻塞到第一個 commit，重新求值 WHERE 後 0 筆
  命中，不會重複發布、不會拋例外——不需要分散式鎖或 leader election。
- **不覆寫 `published_at`**：只改 `status`／`updated_at`，維持原本排定的時間，理由是①同一天
  排程多篇文章的相對順序（`ORDER BY published_at DESC`）不該被輪詢間隔的抖動打亂，②「10:00
  設定發布」的使用者期待是「顯示 10:00 發布」，不是「顯示輪詢器真正跑到的那一刻」。
- **共用內容失效**：`club_id IS NULL`（共用文章）可能同時影響兩個俱樂部的公開頁面，不逐一解析
  受影響的俱樂部代碼，直接對「目前所有啟用俱樂部」失效——俱樂部只有 2 個，成本可忽略。
- **fail-open**：單輪掃描失敗（例如 SQL Server 短暫連不上）只記錄錯誤，不讓整支 hosted service
  停止運作，下一輪還有機會補上。

⚠️ **若 60 秒的延遲對排程發布不可接受**（例如客戶期待「10:00 設定發布，10:00 準時看得到」秒級
精準），可調低 `SCHEDULED_PUBLISH_INTERVAL_SECONDS`，代價是輪詢頻率提高，對 Basic 層 5 DTU 的
壓力也提高（但單次查詢是「條件式 UPDATE，多數時候 0 筆命中」，成本遠低於一般查詢，可承受的下限
比一般快取 TTL 高很多）——這是實測與驗收見下方「S0-7g 驗收紀錄」，本檔維持 60 秒預設值。

🔴 **目前只有 `articles` 接上這個機制**。`db/club-schema.sql` 另外還有 8 張表帶
`CHECK (status IN ('draft','published','scheduled'))`：`pages` 有 `published_at` 欄位但還沒有
任何公開讀取或後台寫入端點（沒有讀取路徑就沒有這個 bug 的實際後果，等 Pages 端點開發時把它加進
`ScheduledPublishRunner` 的掃描清單，欄位已備妥不需要新 migration）；`press_resources`／
`faqs`／`competitions`／`sponsor_packages`／`collections`／`products`／`charity_programs`
**連 `published_at` 欄位都沒有**（已逐張 grep 核對過）——CHECK 約束允許 `'scheduled'`，但沒有
欄位記錄排定時間，這是既有的欄位缺漏，不是這裡能修的（改資料表結構要走 `docs/12` 同步鏈再走
migration，這裡沒有自己加）。

---

## 錯誤處理

`Common/ApiExceptionHandler.cs` 是全站最後一道例外處理防線：

- `ClubNotFoundException` → `404`，訊息只說「找不到俱樂部」＋俱樂部代碼，不含任何 SQL／連線細節。
- 其他任何例外 → `500`，回應固定是「伺服器發生未預期的錯誤，請稍後再試」，**完整例外（含堆疊）只寫進
  `ILogger`**，⛔ 不會出現在 HTTP 回應裡。本次開發期間實際踩過的兩個 500（見下方「開發過程踩的坑」）
  都是先看伺服器端日誌才找到根因，而不是看回應內容——這正是這個設計要達成的效果。

---

## 🔴🔴🔴 寫入端點開發模式開關（❌ 2026-09-23 起已整支刪除，見上方「S1」整節「開發模式開關：已刪除」）

> ⚠️ **本節是 S0-8 為止的歷史記錄，如實保留**（機制本身、`IDevOperatorResolver` 的行為描述都是
> 當時的事實），**但下面描述的機制現在完全不存在**：`Security/DevWriteGate.cs`／
> `Security/IDevOperatorResolver.cs`／`Security/DevOperatorResolver.cs` 三個檔案、
> `ENABLE_UNSAFE_DEV_WRITES` 環境變數的所有讀取點、`Program.cs` 裡的相關註冊，皆已於 2026-09-23
> 使用者裁決後刪除。`Features/AdminNews` 改由 `Security/AdminClubAuthorizer` 逐請求驗證登入與
> 授權，`created_by`／`updated_by` 改用真實登入者的 `AdminClubScope.Identity.AdminUserId`。
> 本節以下內容**純供歷史查閱**，不代表任何現存的程式碼路徑。

專案還沒有登入與權限。**沒有權限把關的寫入端點如果被部署出去，就是任何人都能改資料庫**。
`Features/AdminNews` 的所有端點（含後台專用的讀取端點）因此掛在一個明確、預設關閉的開關後面：

```csharp
// Security/DevWriteGate.cs
public static bool IsEnabled(IConfiguration configuration, IHostEnvironment environment)
    => environment.IsDevelopment()
       && string.Equals(configuration[EnableFlagKey], "true", StringComparison.OrdinalIgnoreCase);
```

`Program.cs` 只在這個判斷式回傳 `true` 時才呼叫 `app.MapAdminNewsEndpoints()`。兩個條件缺一都算關閉：

1. `ASPNETCORE_ENVIRONMENT=Development`——正式環境的 `docker-compose.yml`／`docs/20-cicd.md` §7.2
   一律設 `Production`，就算忘記設下面那個旗標，光是環境判斷這關就先擋住。
2. `ENABLE_UNSAFE_DEV_WRITES=true`——刻意用「unsafe」命名，且要求字面等於 `"true"`（不是「有設定
   就算」），降低「複製一份 `.env` 忘記砍掉」被誤帶到正式環境的機率。

**關閉時的行為是路由完全不註冊，回應是 404，不是 403**——不透露「這裡本來有一組寫入端點」這件事。
已用三種方式實測（見下方「驗收紀錄」的完整輸出）：
- `dotnet run`（`ASPNETCORE_ENVIRONMENT=Development`）不設 `ENABLE_UNSAFE_DEV_WRITES` → 404。
- 同一個 `dotnet run` 設了 `ENABLE_UNSAFE_DEV_WRITES=true` → 端點出現，且啟動時印一則
  `LogWarning` 級別的警告（訊息裡再講一次「不得在任何對外環境開啟」）。
- **容器層級**（`ASPNETCORE_ENVIRONMENT=Production`，模擬正式部署，即使沒設
  `ENABLE_UNSAFE_DEV_WRITES` 也一樣）→ 404，這是兩層防護裡更重要的那一層，因為正式環境的
  `ASPNETCORE_ENVIRONMENT` 本來就固定是 `Production`。

`created_by`／`updated_by` 這兩個稽核欄位由 `Security/IDevOperatorResolver.cs`
（`DevOperatorResolver` 唯一實作）從請求標頭 `X-Dev-Operator-Id` 解析：

> 🔴 **這不是身分驗證。** 沒有任何簽章、沒有 session，呼叫端說是誰就是誰。標頭值會先驗證在
> `admin_users` 表真的存在（那兩個欄位的 FK 約束要求指向真實列），驗不到就回傳 `null`
> （欄位本來就允許 NULL），不會讓外鍵違反炸成 500。**`admin_users` 目前是空表**（登入系統還沒做），
> 所以現況下不管標頭給什麼值，`created_by`／`updated_by` 實際上都會是 `null`——這是正確、預期的
> 行為，不是本輪沒做完。等登入系統做出來、`admin_users` 真的有資料時，這個機制不用改就能接上。

---

## EF Core 一次性 handoff（本輪完成，`docs/20-cicd.md` §5）

`docs/20-cicd.md` §5 明訂：`api` 專案第一次要寫入時，對**已經用 `db/*.sql` 建好的資料庫**跑
`dotnet ef dbcontext scaffold` 產出 Entity，並建立一個**標記為已套用的空白基準 migration**
（`InitialBaseline`），不實際重跑 DDL；之後才進入「每次改動都是一個新 migration」的常態。

### 怎麼做的

```bash
# 1. Scaffold：對本機 tcrfc_club_dev 反向工程出 DbContext 與 138 個實體類別
cd apps/api
dotnet ef dbcontext scaffold "$CLUB_SQL_CONNECTION_STRING" Microsoft.EntityFrameworkCore.SqlServer \
  --context ClubDbContext --context-dir Data --output-dir Data/EfEntities \
  --namespace Tcrfc.Api.Data.EfEntities --context-namespace Tcrfc.Api.Data \
  --no-onconfiguring --force

# 2. 建立基準 migration（此時 Up()/Down() 還是「建立全部 145 張表」的完整 DDL）
dotnet ef migrations add InitialBaseline --context ClubDbContext \
  -o Data/Migrations --namespace Tcrfc.Api.Data.Migrations

# 3. 手動清空 Up()/Down() 的內容（只留註解），Designer.cs 的模型快照原封不動保留
#    （這一步刻意不用工具自動化，直接編輯產生的 .cs 檔——見下方檔案內容）

# 4. 套用：因為 Up() 是空的，這裡只會在 __EFMigrationsHistory 插入一筆紀錄，不會真的動任何 DDL
dotnet ef database update --context ClubDbContext
```

**套用前後的資料庫表數比對**（實跑紀錄，見下方「驗收紀錄」有完整輸出）：套用前 `sys.tables`
144 張、`articles` 83 筆；套用後 145 張（只多了 `__EFMigrationsHistory`）、`articles` 仍是 83 筆。

### 🔴 命名衝突：`programs` 表撞到 ASP.NET Core 的頂層 `Program` 類別

Scaffold 完 `dotnet build` 直接炸掉一百多個 `CS1061`，根因是 P1「課程與活動」模組的 `programs` 表
被 scaffold 成類別 `Program`，跟 `Program.cs` 頂層陳述式產生的 `global::Program`（也是這個組件裡
`WebApplicationFactory<Program>` 測試要用到的那個類別）同名。C# 名稱解析規則下，**全域命名空間的
型別比 `using` 匯入的型別優先**，所以 `ClubDbContext.cs` 裡所有 `modelBuilder.Entity<Program>(...)`
都被誤解析成頂層那個空的 `Program`。**處理方式**：把這一個實體類別（及所有參照它的巡覽屬性）
改名成 `TrainingProgram`（`programs` 表在後台叫「課程項目」，這個名字語意上也更貼切），
只改型別名稱，不改資料表名、不改任何欄位對應。**之後重新 scaffold 會再產生一次 `Program.cs`
這個檔名**，要記得重做這個改名——這是產生流程的已知步驟，不是一次性修好就沒事。

### 之後怎麼加真正會執行 DDL 的 migration（下一位接手者看這裡）

```bash
# 1. 先改 db/club-schema.sql（走 docs/12 的同步鏈，CLAUDE.md 第 2、3 條）
# 2. 手動對本機資料庫套用那個 DDL 異動（或整個重建本機庫）
# 3. 重新 scaffold（見上面第 1 步），這次會抓到新綱要
# 4. dotnet tool restore   # 本機工具清單釘住 dotnet-ef 版本，見下方「本機工具清單」
#    dotnet ef migrations add <描述性名稱> --context ClubDbContext -o Data/Migrations
#    這次不用清空 Up()/Down()——這是真正要執行的變更，讓它照常產生 DDL
# 5. 🔴 驗收：dotnet ef migrations add Probe --context ClubDbContext -o Data/Migrations
#    確認 Probe 的 Up()/Down() 是空的（代表模型與 snapshot 完全同步），再 dotnet ef migrations
#    remove 刪掉——這一步不能省，見下方「EF Core migrations 基準健檢」與 docs/20-cicd.md §5
# 6. 正式環境套用走 docs/20-cicd.md 的 db-migrate.yml（需要 production-db 環境核准），
#    不是在本機對正式庫下 dotnet ef database update
```

⚠️ **本輪（S0-7f）只做了 handoff 本身，沒有新增任何真正的結構異動**——`InitialBaseline` 是唯一一個
migration，Up()/Down() 都是空的，且已用「套用前後表數不變」實測驗證過。

---

### 🔴🔴🔴 修復：`ClubDbContextModelSnapshot.cs` 從未進版控，基準壞了兩次（S0-7j，2026-09-24，`docs/18-work-errors.md` E-45）

**發現的問題**：`InitialBaseline`（S0-7f）建立時只 commit 了 `.cs`／`.Designer.cs`，
**`Data/Migrations/ClubDbContextModelSnapshot.cs` 從沒進版控**。少了它，`dotnet ef migrations add`
拿空模型當比較基準，會產出一個把整份綱要重建一遍的 migration（S0-9l 實測 145 個 `CreateTable`，
已刪除，未 commit）。這個問題**不會讓任何測試變紅**——既有測試接的是用 `db/club-schema.sql`
直接建好的資料庫，不經過 migration，缺 snapshot 對測試結果零影響。之後兩次改綱要
（`e67ef26` 的 `AdminRefreshToken`／S0-9l 的 `matches.original_match_on`／`original_kickoff`）
都因此繞過 migration，改成手改 scaffold 檔＋手動 `ALTER TABLE`，`docs/20-cicd.md` §5 定的
「每次改動都是一個新 migration」路徑因此走不通。

**怎麼修的**：

1. **重建 snapshot**：`ClubDbContextModelSnapshot.cs` 的內容規則上等於
   `InitialBaseline.Designer.cs` 的 `BuildTargetModel`——兩者本來就該描述同一個模型，
   只是分別給「單一 migration 的目標模型」與「目前累積的模型」兩種用途用。用一支腳本把
   `InitialBaseline.Designer.cs` 整份複製，做三個純文字替換（拿掉 `[Migration(...)]` 特性、
   `partial class InitialBaseline` → `partial class ClubDbContextModelSnapshot : ModelSnapshot`、
   `BuildTargetModel` → `BuildModel`），沒有手動改動任何一行模型描述本身。`dotnet build` 通過
   後即視為重建完成——**沒有獨立驗證這個重建本身完全正確**，真正的驗證來自下一步：拿它當基準
   加 migration，若基準有錯，加出來的 migration 內容一定會顯示出破綻（多出或少掉不該有的變更），
   而實測結果（見下）確實只長出預期中的兩張／兩欄異動，沒有任何其餘 143 張表的雜訊，
   這是基準正確的間接但有力的證據。
2. **補回兩次漏掉的 migration**（依時間順序，用 `git stash` 暫時「藏起」還沒 commit 的
   S0-9l 改動，讓當下的 Entity 模型精準對齊每一次要補的異動範圍，逐一 `dotnet ef migrations add`）：
   - `AddAdminRefreshTokens`（20260924011258）：只有 `CREATE TABLE admin_refresh_tokens` 與四個索引，
     沒有動到其餘 143 張表。
   - `AddMatchOriginalSchedule`（20260924011323）：只有 `matches` 表新增 `original_kickoff`
     （`nvarchar(8)`）／`original_match_on`（`date`）兩欄。
3. **驗收**：`dotnet ef migrations add Probe` 產出空的 `Up()`/`Down()`，確認模型與 snapshot
   完全同步後刪除。`dotnet ef migrations script InitialBaseline`（單一起點參數＝從
   `InitialBaseline` 之後到最新）產出的 SQL 逐欄核對過 `db/club-schema.sql`
   （型別、長度、`NULL`、預設值、索引、外鍵），發現一處落差，如實記錄、不自行決定：見下方
   「EF 自動索引與 `db/club-schema.sql` 的一處落差」。
4. **本機工具清單**：新增 `apps/api/.config/dotnet-tools.json`，釘住 `dotnet-ef 10.0.3`——
   之前的 `dotnet ef` 指令全部假設全域安裝了工具，沒有版本釘選，也沒有讓 CI 或新接手的人
   知道要裝什麼版本。`dotnet tool restore` 後即可用，不需要 `dotnet tool install -g`。

**EF 自動索引與 `db/club-schema.sql` 的一處落差（S0-7j 發現時待裁決，已在下方「S0-7k」裁決並修復）**：
`AddAdminRefreshTokens` migration 產生了 `CREATE INDEX IX_admin_refresh_tokens_replaced_by_id
ON admin_refresh_tokens (replaced_by_id)`，但 `db/club-schema.sql`（1547–1558 行）**沒有這個索引**
——本機 `tcrfc_club_dev` 目前也沒有。根因是 EF Core 的 `ForeignKeyIndexConvention`：只要一個屬性
被設定成外鍵（`entity.HasOne(d => d.ReplacedBy).WithMany(...)`），EF 在建置模型時就會自動替它
加一個非叢集索引，除非已經有別的索引涵蓋它——這個慣例**跟這個屬性是不是可為空、有沒有真的在
`OnModelCreating` 裡手寫 `HasIndex` 完全無關，是建置模型當下自動套用的**。`AdminRefreshToken`
這個實體是 `e67ef26` 手改 scaffold 檔加進去的（不是真的重新對資料庫跑 `dotnet ef dbcontext
scaffold`），所以撰寫者沒有機會像對其餘 143 張表那樣，從真實資料庫「有沒有這個索引」反推
要不要寫 `entity.HasIndex(...)`；而這一次是**第一次真的把這個實體的模型拿去跟資料庫比對**
（透過生成 migration），落差因此第一次浮現。**兩個修法都合理，需要裁決**：
① 把這個索引正式收進 `db/club-schema.sql`（`replaced_by_id` 常態查詢輪替鏈時用得到，加索引
本身無害）；② 在 `ClubDbContextCustomizations.cs` 用 Fluent API 明確抑制這個自動索引
（`modelBuilder.Entity<AdminRefreshToken>().Metadata.RemoveIndex(...)`，需要之後有新
migration 才能真的在資料庫層面 `DROP INDEX`）。本輪判斷這不在「補回遺漏 migration」的任務
範圍內，如實記錄、留給下一位或使用者決定，`db/club-schema.sql`、`tcrfc_club_dev` 目前都
維持沒有這個索引的狀態，但 **EF 的 migration／snapshot 已經確實產生了它**——這代表若真的按
`docs/20-cicd.md` §5 的正式流程（`db-migrate.yml`）把 `AddAdminRefreshTokens` 套到一個全新的
正式環境資料庫，會多出這個索引；套到已經手動建好、沒有這個索引的環境（例如 `tcrfc_club_dev`）
則不會有這個索引，因為套用時只在 `__EFMigrationsHistory` 補紀錄，不重跑已經手動完成的 DDL
（見下一段）。

**本機開發庫 `tcrfc_club_dev` 的處理方式**：這個資料庫已經用 `db/club-schema.sql`
（已含 `admin_refresh_tokens`／`matches.original_*`）手動建好，欄位與表本身跟兩個新 migration
最終想要的結果一致（**除了上述那個索引**）。做法是直接對 `tcrfc_club_dev` 的
`__EFMigrationsHistory` 補兩筆紀錄（`INSERT`，不執行任何 DDL），讓 EF 認為這兩個 migration
「已套用」，不重複建立已經存在的表／欄位，也不會意外對正式資料造成影響。**這個資料庫因此
在功能上是完整的，只是實際 DDL 落地路徑跟「跑 migration」不同**——已知的落差就只有上面那個
索引。

**驗證這條路徑走得通（用一個全新的、用完即丟的資料庫）**：`InitialBaseline` 之前的狀態要從
`db/club-schema.sql` 的歷史版本取得——目前這份檔案已經包含 `admin_refresh_tokens`／
`matches.original_*`（`e67ef26`／S0-9l 都已經改了這份檔案），沒辦法從現在的檔案內容重現
「純 `InitialBaseline` 時間點」的資料庫。改用 `git show d5ec4ec:db/club-schema.sql`
（`InitialBaseline` 那次 commit）取出當時的版本，套用 `deploy/local-ddl.sh` 用的同一條
`json → nvarchar(max)` 轉換規則（本機 SQL Server 2022 沒有 Azure SQL 原生 `json` 型別，
S0-6b 既有的已知限制），在一個全新的 `tcrfc_club_scratch` 資料庫上建出 144 張表，標記
`InitialBaseline` 為已套用，再跑 `dotnet ef database update` 真的套用兩個新 migration——
兩個都成功執行了真正的 DDL（`CREATE TABLE`／`ALTER TABLE`，不是只補紀錄），結束後
`tcrfc_club_scratch` 已刪除，全程沒有動到 `tcrfc_club_dev`／`tcrfc_charity_dev`。

**測試**：`dotnet test` 132/132（既有 131 ＋ S0-9l 已經寫好但先前卡在缺 migration 沒被納入
驗收的 `ScheduleOriginalDateTests`，本輪修復基準後這個測試才有意義——它驗的正是
`original_match_on`／`original_kickoff` 這兩欄，跟本輪要補的第二個 migration 是同一組欄位）。

### 🔴 S0-7k（2026-09-24）：裁決並修復 `admin_refresh_tokens.replaced_by_id` 的索引落差

使用者裁決：**依綱要為準**——`db/club-schema.sql` 與 `docs/12` 是真實來源，兩者都沒有這個索引，
**不改 `db/club-schema.sql`，讓 EF 對齊綱要**。

**單純 `RemoveIndex` 無效，實測發現的坑**：一開始照 S0-7j 記錄的方案②，在
`OnModelCreatingPartial` 呼叫 `modelBuilder.Entity<AdminRefreshToken>().Metadata.RemoveIndex(...)`，
`dotnet build` 過，但拿它當基準跑 `dotnet ef migrations add Probe` 驗收時，**Probe 的 `Up()`
又長回一模一樣的 `CreateIndex IX_admin_refresh_tokens_replaced_by_id`**——索引被自動補回來了。
根因：`ForeignKeyIndexConvention` 同時實作 `IIndexRemovedConvention` 與
`IModelFinalizingConvention`，只要偵測到某個外鍵的屬性沒有涵蓋索引，**移除後會立刻自我修復、
補回同一個索引**，這個慣例沒有提供逐一外鍵層級的「這個不要」旗標。

**真正生效的做法**：在 `ClubDbContextCustomizations.cs` 用 `ConfigureConventions` 換掉
`ForeignKeyIndexConvention` 本身，換成一個繼承它的子類別，只在要處理
`AdminRefreshToken.ReplacedById` 這一個屬性時跳過（`CreateIndex` 回傳 `null`），其餘所有屬性
（含下面「已知但不在本輪範圍」提到的其餘落差）呼叫 `base.CreateIndex(...)` 原封不動繼承官方行為，
不影響其餘 137 張表：

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    configurationBuilder.Conventions.Replace(serviceProvider =>
        new AdminRefreshTokenReplacedByIdIndexSuppressingConvention(
            serviceProvider.GetRequiredService<ProviderConventionSetBuilderDependencies>()));
}
```

**重新產生兩支 migration 時踩到的另一個坑（🔴🔴🔴 對正在跑的其他工作有實際風險，務必記住）**：
`AddAdminRefreshTokens`／`AddMatchOriginalSchedule` 兩支 migration 當時都還沒 commit，且
`tcrfc_club_dev` 的 `__EFMigrationsHistory` 已經有這兩筆紀錄。原計畫是
`dotnet ef migrations remove` 依序刪除兩支再重新 `add`。**`dotnet ef migrations remove` 對「已
標記為套用」的 migration 預設會拒絕並提示「Revert it and try again」；加上 `--force` 之後，
它不是只刪本機檔案——它會直接對連線中的資料庫執行該 migration 的 `Down()`（真的跑
`ALTER TABLE ... DROP COLUMN`／`DROP TABLE`），刪完 DB 端的實際結構後才刪 `__EFMigrationsHistory`
的那筆紀錄跟本機檔案。** 對 `AddMatchOriginalSchedule` 用 `--force` 移除時，**這一步真的把
`tcrfc_club_dev.matches` 的 `original_kickoff`／`original_match_on` 兩欄砍掉了**（S0-9l 剛加的
兩欄，另一個 agent 當時正在用同一顆資料庫做排程發布／文章功能）。**發現後立刻用
`dotnet ef migrations add AddMatchOriginalSchedule` 重新產生同一支 migration，再
`dotnet ef database update` 把它合法地重新套用回去**，兩欄復原、`__EFMigrationsHistory` 也正確
補回一筆（時間戳因此從 `20260924011323` 變成新產生當下的 `20260924014130`，不是原本規劃的
「維持相同時間戳」，但這是唯一乾淨、不需要再手動改資料庫的收尾方式）。

**因此對 `AddAdminRefreshTokens` 改用完全不同的做法，全程沒有再對 `tcrfc_club_dev` 執行任何
DDL**：`admin_refresh_tokens` 這張表已經有實際資料表存在（`e67ef26` 之後的登入／授權功能會寫入
真實的 refresh token 資料列），若照原計畫對這支 migration 也用 `--force` 移除，等於
`DROP TABLE admin_refresh_tokens`，會真的遺失資料，風險遠高於兩個欄位。改成**直接手改三個
既有檔案**（`20260924011258_AddAdminRefreshTokens.cs`／`.Designer.cs`／
`ClubDbContextModelSnapshot.cs`），把那一段 `CreateIndex IX_admin_refresh_tokens_replaced_by_id`
與對應的 `b.HasIndex("ReplacedById")` 拿掉——因為 `tcrfc_club_dev` 本來就沒有這個索引，
手改讓檔案內容跟資料庫現況一致，**不需要執行任何 DDL 就能達到一致**，`migrationId` 也維持原本
的 `20260924011258` 不變，`__EFMigrationsHistory` 完全不用動。

**⚠️ 給下一位要重新產生／刪除 migration 的人的教訓**：`docs/20-cicd.md` §5 的「Probe 驗收」流程
本身沒問題（`add`／`remove` 一支從沒套用過的 migration 不會碰資料庫），**真正危險的是對一支
「已經標記為套用」的既有 migration 下 `remove --force`**——這一步不是純粹的檔案操作，會真的對
連線中的資料庫執行 `Down()`。共用的本機開發庫（`tcrfc_club_dev`）常常同時有別的 agent／工作在用，
**下手前先確認：這支 migration 的 `Down()` 會不會刪掉別人正在依賴的表或欄位；會的話，改用
手改既有 migration 檔案這條路，不要依賴 `--force`**。

**已知但當時不在本輪範圍的同類落差**：`ClubDbContextModelSnapshot.cs` 掃過一遍，
`ForeignKeyIndexConvention` 這個慣例造成的「未命名 `HasIndex`」總共約 **281 筆**，涵蓋既有 143 張
表——`created_by`／`updated_by` 這兩個審計欄位各 85 筆、`club_id` 33 筆，其餘約 78 筆分散在各種
FK（`team_id`／`season_id`／`venue_id`／`player_id`……）。S0-7k 任務範圍明訂只裁決 `replaced_by_id`
這一筆，其餘只回報不處理。**這一大批已在下面的「S0-7l」全部處理完畢**，改用整條移除
`ForeignKeyIndexConvention`（而不是子類別跳過清單）一次收斂兩筆任務。

**驗收（S0-7k 本輪實跑）**：`dotnet ef migrations add Probe` 產出空 `Up()`/`Down()` 後刪除；
`dotnet ef migrations has-pending-model-changes` 印出「No changes have been made to the model
since the last migration.」（結束碼 0）；`dotnet ef migrations script InitialBaseline` 產出的
`admin_refresh_tokens` DDL 與 `db/club-schema.sql`（1547–1559 行）逐欄逐索引核對一致（含
`IX_admin_refresh_tokens_user`／兩個 `UQ_*`／兩個 `FK_*`，**沒有** `IX_admin_refresh_tokens_
replaced_by_id`）；`dotnet test`（`Tcrfc.Api.Tests.csproj`）135/135 通過；`dotnet build` 0 警告
0 錯誤。

### 🔴 S0-7l（2026-09-24）：整條移除 `ForeignKeyIndexConvention`，收斂 S0-7k 遺留的 281 筆落差

使用者裁決同 S0-7k：**依綱要為準，不改 `db/club-schema.sql`，讓 EF 模型對齊 DDL**；並明確要求
「S0-7k 的 `AdminRefreshTokenReplacedByIdIndexSuppressingConvention` 要一併收斂，不要留兩套機制」。

**做法**：把 S0-7k 那個繼承 `ForeignKeyIndexConvention`、只對 `AdminRefreshToken.ReplacedById`
回傳 `null` 的子類別整個刪掉，`ClubDbContextCustomizations.ConfigureConventions` 改成一行：

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    configurationBuilder.Conventions.Remove(typeof(ForeignKeyIndexConvention));
}
```

`Remove(Type)` 是 EF Core 官方提供的「整條停用某個慣例」API（不是 hack），效果是 EF 完全不再替
「沒有顯式索引的外鍵屬性」自動加索引——**不影響 `db/club-schema.sql` 已經明確宣告的索引與唯一鍵**：
那 210 筆（74 個 `CREATE INDEX` ＋ 49 個 `ALTER TABLE ... UNIQUE` ＋ 87 個 `row_seq` 叢集唯一鍵）
在 scaffold 階段就已經是顯式的 `HasIndex()`／`HasKey()` Fluent API 設定，走的不是這個慣例。

**為什麼從「跳過清單」改成「整條移除」**：S0-7k 當時只有 1 個例外，繼承子類別、對特定屬性回傳
`null` 還算精準；S0-7l 發現的例外多達 282 個（281 筆新的 ＋ S0-7k 那 1 筆），此時維護一份「跳過
清單」本身就是另一種形式的資料落差來源（清單本身也要跟著 `db/club-schema.sql` 保持同步）。既然
「不要自動加索引」才是這個專案唯一想要的行為，直接不掛這個慣例最乾淨。

**驗證方法（比對用的獨立腳本，未納入版控，需要時可在 scratch 目錄重建）**：寫一支小型
console 專案（`ProjectReference` 指到 `Tcrfc.Api.csproj`），用 `UseSqlServer(<假連線字串>)`
建構 `ClubDbContext`（EF 建模不需要真的連上資料庫），走訪 `Model.GetEntityTypes()` 逐一
`GetIndexes()`，輸出 `(schema.table, 欄位清單, IsUnique, 索引名稱)` 四元組；另寫一支正規表達式
腳本解析 `db/club-schema.sql`（涵蓋三種宣告位置：`ALTER TABLE ... ADD CONSTRAINT ... UNIQUE`、
`CREATE [UNIQUE] INDEX`、`CREATE TABLE` 內文的 `CONSTRAINT ... UNIQUE [CLUSTERED] (...)`，
最後一種容易漏掉——`row_seq` 叢集唯一鍵與 `admin_refresh_tokens.token_hash` 都是這樣宣告的），
輸出同樣的四元組。以 `(table, 欄位清單, IsUnique)` 為鍵比對兩份清單：

| | 移除慣例前 | 移除慣例後 |
|---|---|---|
| 模型索引數 | 491 | **210** |
| DDL 索引數 | 210 | 210 |
| 只在模型有（EF 多的） | **281**（與 STATUS.md 估計一致） | **0** |
| 只在 DDL 有（EF 漏的） | 0 | 0 |
| 索引名稱不一致（不分大小寫） | — | 0 |

移除後兩邊筆數與內容完全一致。`git diff` 也可交叉驗證：`ClubDbContextModelSnapshot.cs`
淨減少 281 個 `HasIndex(...)` 區塊、新增 0 個。

**新增的 migration**：`AlignIndexesWithDdl`（`20260924021003`），`Up()`／`Down()` 比照
`InitialBaseline` 刻意清空並加註解——scaffold 原始產出是 281 個 `DropIndex`／對應的
`CreateIndex`，但這些索引從未真的建到任何資料庫（`InitialBaseline` 的 `Up()` 本來就是空的），對
任何實際資料庫執行這些 `DropIndex` 都會因為索引不存在而失敗，所以清空，只保留這支 migration
「讓 snapshot 變成正確模型」的效果。`tcrfc_club_dev` 的 `__EFMigrationsHistory` 用一條純
`INSERT` 補上這筆紀錄（**這是本輪對 `tcrfc_club_dev` 唯一的寫入**，沒有執行任何 DDL）。

**驗收（本輪實跑）**：
- `dotnet build` 0 警告 0 錯誤。
- `dotnet ef migrations has-pending-model-changes` 綠燈（「No changes have been made ...」）。
- `dotnet ef migrations add Probe` 產出空 `Up()`/`Down()`，確認 Probe 從未套用（查
  `tcrfc_club_dev.__EFMigrationsHistory` 沒有這筆）後 `dotnet ef migrations remove` 刪除。
- **兩個用完即丟的資料庫**（皆已 `DROP DATABASE`，未動 `tcrfc_club_dev`／`tcrfc_charity_dev`
  以外的任何既有資料庫）：
  ① 用**目前**的 `db/club-schema.sql`（比照 `deploy/local-ddl.sh` 的 `json`→`nvarchar(max)`
  轉換規則，手動 `sed`＋`docker exec sqlcmd` 建庫，未修改該腳本的資料庫白名單）建出 145 張表，
  手動 `INSERT` 全部 4 筆 migration 紀錄標記為已套用，`dotnet ef migrations script --idempotent`
  的輸出裡 `AlignIndexesWithDdl` 對應的 `IF NOT EXISTS (...) BEGIN ... END` 區塊只有一句
  `INSERT INTO [__EFMigrationsHistory]`，没有任何 DDL；`dotnet ef database update` 印出
  「No migrations were applied. The database is already up to date.」，表數不變（146，含歷史表）。
  ② 用 **`d5ec4ec`**（`admin_refresh_tokens`／`matches.original_*` 都還不存在的舊版）的
  `db/club-schema.sql` 建出 144 張表，只標記 `InitialBaseline` 已套用，真的跑
  `dotnet ef database update`——依序套用 `AddAdminRefreshTokens`（真的 `CREATE TABLE`／
  `CREATE INDEX`）、`AddMatchOriginalSchedule`（真的 `ALTER TABLE ADD COLUMN`）、
  `AlignIndexesWithDdl`（只有 history `INSERT`，無 DDL）成功，最終 146 張表、
  `matches` 有 `original_kickoff`／`original_match_on` 兩欄，跟①的終態一致——確認
  「從舊 DDL 用純 migration 升級到現況」這條路徑在移除慣例之後依然成立。
- `tcrfc_club_dev` 本身：表數（146）、索引數（356）、`articles` 資料列數（110）在整個過程前後
  不變，唯一變化是 `__EFMigrationsHistory` 多了 `AlignIndexesWithDdl` 這一筆紀錄。
- `dotnet test`（`Tcrfc.Api.Tests.csproj`，接 `tcrfc_club_dev`）135/135 通過。

**EF 查詢行為是否受影響**：不受影響。索引是儲存層 metadata，只影響 SQL Server 的執行計畫（走不走
Index Seek／Scan），不參與 EF Core 的 LINQ-to-SQL 查詢轉換——移除這個慣例不會讓 `WHERE club_id = @x`
這類查詢的 **產生的 SQL 文字** 有任何變化，只會讓「EF 以為資料庫有的索引」跟「資料庫實際有的索引」
一致。真正影響查詢效能的是 `db/club-schema.sql` 是否有幫這個查詢模式建對索引，這是
`docs/12b` §11.2 的範圍，不是這裡的範圍。

**給下一位要新增外鍵欄位的人**：**不用再處理這個慣例**——它已整條移除，EF 不會再幫任何新外鍵
自動加索引。新增外鍵時，索引要不要建，直接看 `db/club-schema.sql`／`docs/12b` §11.2 想不想要：
想要就在 DDL 明確 `CREATE INDEX`（scaffold 之後會自動變成顯式 `HasIndex`），不寫就是不要，
`dotnet ef migrations add` 產出的 migration 不會再多出非預期的 `CreateIndex`。

### EF Core migrations 基準健檢（本機工具清單與 CI 防呆，S0-7j 新增）

`apps/api/.config/dotnet-tools.json` 釘住 `dotnet-ef 10.0.3`，用前先 `dotnet tool restore`
（不需要 `dotnet tool install -g`，CI 與任何新接手的人都一樣）。`.github/workflows/ci.yml`
的 `api` job 在 `dotnet build` 之後新增一步「EF Core migrations 基準健檢」，兩道檢查：
① 有 `Migrations/*.Designer.cs` 卻沒有 `ClubDbContextModelSnapshot.cs` 直接失敗；
② `dotnet ef migrations has-pending-model-changes --context ClubDbContext` 非零結束就失敗
（不需要真的連得上資料庫，只比對記憶體中的模型，但仍需要 `CLUB_SQL_CONNECTION_STRING`
這個設定鍵存在才能建置 DbContext）。完整設計理由、兩種真實錯誤形狀的紅／綠驗收記錄見
[`docs/20-cicd.md`](../../docs/20-cicd.md) §5「新增 migration 的驗收」與「CI 防呆」兩節。

---

## 後台新聞（B2）寫入垂直切片

`Features/AdminNews/` 是這一輪的主體，也是之後其他模組（球員、教練、賽程、商店……）抄的樣板。
四件套：`AdminArticleDtos.cs`（請求／回應）、`AdminArticlesRepository.cs`（EF Core 寫入邏輯）、
`AdminArticlesEndpoints.cs`（路由）、`AdminArticleExceptions.cs`（業務例外，集中在
`Common/ApiExceptionHandler.cs` 轉狀態碼）。

### 通則（其他模組照抄的部分）

| 關注點 | 落點 |
|---|---|
| **俱樂部範圍** | 沿用既有 `IClubResolver`／`ClubScope`（跟五組唯讀端點同一套，型別系統擋，不是 code review 記住）。寫入路徑另外用 `AdminArticlesRepository.LoadTrackedForWriteAsync` 統一擋「共用內容」與「跨俱樂部」兩種情況 |
| **共用內容唯讀** | `club_id IS NULL` 的文章：讀（`GetByIdAsync`）允許，**寫（Update／Delete）一律 403**（`SharedArticleReadOnlyException`）。目前**沒有任何管道能透過這組端點建立共用內容**——`CreateAsync` 寫死 `ClubId = scope.ClubId`，因為「共同內容只有超管能建立」（docs/14-invariants.md），而超管角色還不存在，這裡選擇完全不開這條路，不留半套的後門 |
| **樂觀並行控制** | `articles.updated_at` 當並行權杖，用 EF Core `IsConcurrencyToken()`（`Data/ClubDbContextCustomizations.cs`）——寫入前把追蹤實體的「原始值」設成呼叫端宣稱看到的 `updatedAt`，`SaveChanges` 產生的 SQL 帶 `WHERE updated_at = @原始值`，0 筆命中就丟 `DbUpdateConcurrencyException`，`AdminArticlesRepository.SaveWithConcurrencyHandlingAsync` 接住轉成 `ArticleConcurrencyConflictException`（409）。⛔ 沒有「後寫的贏」 |
| **slug 重複** | 建立與更新前先查 `SELECT ... WHERE slug = @Slug`（更新時排除自己），撞到就丟 `ArticleSlugConflictException`（409），回可讀訊息，不是讓 SQL Server 的 `UQ_articles_slug` 唯一鍵違反直接冒出 `SqlException` |
| **slug 格式與保留字（本輪新增）** | 建立與更新前先呼叫 `SlugPolicy.Validate`（400，`AdminArticleValidationException`）。見下方「網址名稱（slug）保留字與格式驗證」整節 |
| **雙語側表** | `AdminArticleContentInput.Zh` 必填（`Title` 不得空白，400），`En` 可省略。**PUT 是整份取代語意**：省略 `en`＝清掉既有英文列（不是「沒帶就維持原樣」），已用自動化測試涵蓋兩個方向 |
| **狀態轉換** | 獨立端點（`/publish`、`/schedule`），不是 PUT 的一個欄位。轉換規則見下方「三態轉換規則（本輪判斷）」 |
| **快取失效** | 寫入成功（`SaveChangesAsync` 交易完成）後呼叫 `IQueryCache.InvalidateAsync("articles", scope.ClubCode)` 與 `InvalidateAsync("article-detail", scope.ClubCode)`，跟公開讀取 API 用同一組 entity 名稱，讓下一次公開讀取立刻回新值。用真正的 `redis-server` 測過（見下方測試段落） |

### 網址名稱（slug）保留字與格式驗證（本輪新增，`Features/AdminNews/SlugPolicy.cs`）

使用者拍板新聞詳情頁的網址是扁平的 `/zh/news/<網址名稱>/`（跟商品詳情同形狀）。07 新聞單元底下
另外已有 **9 個分類 landing 頁**（`apps/web/app/pages/zh/news/{club,match,academy,player-stories,
international,camps-events,community,media,article}.vue`）。一篇文章的網址名稱如果（不分大小寫）
剛好等於其中一個，前台路由就會跟分類頁撞在一起——使用者點進去的會是分類清單，不是文章。
`SlugPolicy.Validate` 在建立（`CreateAsync`）與更新（`UpdateAsync`）兩條路徑最前面呼叫，
擋到就丟 `AdminArticleValidationException`（400，中文可讀訊息，不含 `slug` 這種英文技術詞，
docs/06 §1）。

**擋兩類東西**：

1. **保留字**（上述 9 個，不分大小寫）。
2. **格式**：只准小寫英文字母、數字、連字號組成，不能開頭／結尾是連字號、不能連續兩個連字號、
   不能整段只有數字。這一條規則同時擋掉了任務指示要求考慮的幾種危險形狀——**含斜線、含句點、
   含空白、大小寫變形、純數字**——不需要為每一種各寫一條規則；大小寫變形因此**多數情況下是被
   格式規則擋下（不是保留字比對），但結果一律是 400，不影響效果**。已用 83 篇既有文章的真實
   slug 逐筆核對過，**全部符合這條格式規則**（見下方「既有資料核對」），所以套用不會影響現有內容。

**🔴🔴🔴 保留字清單放哪裡、怎麼維護（這是本輪要自己判斷的重點）**：

清單的**真實來源其實不是這個檔案**，而是 `apps/web` 的路由檔名——今天 `apps/web` 只要新增一個
07 單元的分類頁，或替既有分類頁改檔名，`SlugPolicy.cs` 裡手動抄的這份清單就會悄悄過期，而且
過期的方向永遠是「漏擋」（新分類頁沒被列進來），不會有任何編譯錯誤、測試失敗或執行期例外提醒
維護者去補。這正是 [`docs/18-work-errors.md`](../../docs/18-work-errors.md) `E-36`
「共用真實來源分裂成兩份」的同一種形狀。

本輪**只做到**：把清單集中在單一檔案（`SlugPolicy.cs`）、把來源路徑與盤點日期寫死在該檔案的
XML 文件註解裡，讓下一個改 `apps/web` 路由的人至少有機會搜到這裡。**⛔ 沒有做**跨專案的自動比對
（例如讓 CI 讀 `apps/web` 的實際路由檔名去驗證這份清單是否過期）——一來 `apps/web` 本輪由另一個
agent 在改，任務邊界不允許本輪觸碰；二來這需要「A 專案的檔案異動觸發 B 專案檢查」這種
`docs/20-cicd.md` 目前還沒有的建置機制，屬於「需要跨專案改動才能真正解決」的情況，依任務邊界
只回報建議、不動手發明。

**回報給下一位／使用者的建議**（根治漂移的做法，其中一種，需要同時改 `apps/web`，本輪不做）：

1. 把 9 個分類代碼與其路由片段抽成一份兩邊都讀的共用資料。這 9 個路由片段本來就跟
   `article_categories.code`（7.1–7.8 分類代碼）同名，是本來就存在的同一份事實——`SlugPolicy.cs`
   目前是重複硬編碼一份而不是查表，長期應該讓保留字清單直接查 `article_categories.code`
   （這樣分類本身的異動至少會自動反映到保留字清單，不會漏掉「改分類代碼」這一種漂移；但「新增一個
   不是分類、純粹是路由層級的頁面」這種漂移仍然擋不住，因為那不是資料庫裡的事實），或建一份
   repo 根目錄的共用設定檔（例如 `content/news-category-slugs.json`），`apps/web` 的路由設定與
   這裡都改成讀它。
2. 或在 CI 加一道檢查：`apps/web` 的 07 單元路由檔名異動時，比對這個檔案的清單是否同步，
   不一致就讓 CI 失敗（跟 `docs/18` `E-35`／`E-36` 已經記錄的「共用真實來源要有跨專案檢查」
   同一個精神）。

**既有資料核對**（實跑，2026-09-22）：對本機 `tcrfc_club_dev` 的 83 篇文章逐筆核對，
**0 筆違反保留字清單、0 筆違反新格式規則**（含大小寫、斜線、句點、空白、純數字全部核對過）。
83 篇全部是「日期開頭＋分類詞＋流水號」的形狀（例如 `2024-12-18-club-079`），分類詞出現在中段
不是整段等於保留字，不受影響。

### 🔴 這一輪不做的部分（沒有畫面可驗，刻意不做）

> **本節是 S0-8 當時的原始回報，如實保留**——標籤與關聯已於 **S1-5**（見本檔下方整節）補上後端，
> 這裡描述的「刻意不做」對這兩項**已不成立**，改動理由是使用者本輪明確要求補上（跟 S0-7g 的更新
> 方式一致：舊文字不改寫，只加這段更新說明）。

- ~~**標籤（`article_tags`）、關聯（`article_relations`）**：規劃書 B2 有提到，但 `NewsEditView.vue`／
  `NewsListView.vue` 目前**完全沒有對應欄位**，做了也是沒有畫面可驗的端點——跟任務指示「不要順手做
  球員／教練／賽程／商店」是同一個精神，只是套用在同一個模組內的子功能上。`db/club-schema.sql` 已有
  這兩張表，下一輪 `apps/admin` 補上畫面時再一併做。~~ **已於 S1-5 補上，另見核心價值標籤與批次操作。**
- **圖片上傳管線**：`coverKey` 只是個欄位（存什麼字串都收），沒有「選檔→縮圖→WebP→去 EXIF」那一整套
  （那是 `S0-8`）。

### 🔴 我的判斷（規劃書沒定義，本輪做了選擇，需要使用者／下一位確認）

1. **置頂精選「限 3」是逐俱樂部算，不是全站算。** 規劃書只寫「置頂精選（限 3）」，沒說是全站
   共用一個上限還是每個俱樂部各自 3 篇。多俱樂部架構下我選了「逐俱樂部」（`EnsureFeaturedCapAsync`
   只算 `club_id = scope.ClubId` 的筆數），理由：這組端點的其他每一條規則（讀、寫、快取失效）
   全部是逐俱樂部隔離的，全站共用一個計數器會是唯一的例外，也會讓藍鯨官網的置頂精選被磐石的
   文章佔滿額度，直覺上不合理。**這是本輪的假設，不是規劃書明文，需要業務判斷確認。**
2. **狀態轉換規則**：`publish`／`schedule` 只接受從 `draft` 或 `scheduled` 出發；已經
   `published` 的文章不能再呼叫 `/schedule`（會 409，理由：把一篇已經上線的文章排到未來，
   等於讓它從公開站消失，這個操作應該要更明確，不該跟「第一次發布前的排程」共用同一個按鈕語意，
   規劃書沒有講這種邊界情況）。`published` 文章仍可以呼叫 `/publish`（视为「立即重新發布」，
   幂等地把 `published_at` 推進到現在——沒有測試涵蓋這個分支的必要性，因為它不影響資料正確性，
   只是把時間戳推近）。**這組轉換規則沒有規劃書依據，是本輪的合理猜測，需要確認。**

### ✅ 排程發布：時間到了，誰把狀態從 `scheduled` 改成 `published`？（S0-7g，2026-09-24 已解決）

> **本節是問題被發現時的原始回報，如實保留**——下面描述的落差確實存在過。**已解決**：
> 使用者裁決「規劃書沒寫的執行層決定，開發端依 `docs/17-deployment.md` 既有結論拍板，不用
> 再往上問」，`docs/17`「排程」一項早就定案「用 .NET 的 hosted service 承擔」，S0-7g 把它接上
> `articles`——實作是 `Features/News/ScheduledPublishRunner.cs`／
> `ScheduledPublishBackgroundService.cs`，詳見上方「排程發布：時間到了自動轉為 published」與
> 下方「S0-7g 驗收紀錄」。以下原始回報內容保持不動，只是問題現況已經不是「未解決」。

實測發現一個貫穿既有 README「排程發布與快取」段落與本輪新程式碼的邏輯落差：

- 既有的公開讀取 `ArticlesRepository`（`Features/News/`，唯讀，Dapper，本輪未改）WHERE 子句是
  `a.status = 'published' AND (a.published_at IS NULL OR a.published_at <= SYSUTCDATETIME())`——
  **兩個條件都要成立**，`status` 必須字面等於 `'published'`。
- 本輪新增的 `/schedule` 端點把文章狀態設成 `'scheduled'`（不是 `'published'`），`published_at`
  設成未來時間，這是 `articles.status` CHECK 約束（`draft`／`published`／`scheduled`）唯一合法
  的做法——不可能既符合「排程中」的業務語意又把 `status` 直接寫成 `published`。
- **後果**：`published_at` 那個未來時間**真的到了之後，這篇文章的 `status` 仍然是 `'scheduled'`**，
  不會自動符合公開 API 的 WHERE 條件，**不會自動出現在公開站上**，除非有某個東西主動把
  `status` 改成 `published`。

**已用 curl 實測驗證這個落差確實存在**（見下方驗收紀錄）：排程一篇文章到未來時間後，
`GET /api/v1/tcrfc/news/{slug}`（公開 API）回 404；即使等到那個時間點過了，只要沒有人呼叫
`/publish`，狀態仍是 `scheduled`，公開 API 仍然 404。

**沒有找到任何規劃書段落定義「誰、什麼時候、用什麼機制」把 `scheduled` 轉成 `published`**
（背景排程器？Azure Function 計時器觸發？後台頁面打開時順便檢查？）。**依任務指示，這裡不自己
發明一個排程器**——這是一個需要業務判斷與架構決策的缺口，回報給使用者／下一位接手者：

- 若要「時間到了自動生效」，需要一個會定期執行的背景工作（例如 Azure Function Timer Trigger，
  或 VM 上的 cron 呼叫一支內部端點），把 `status='scheduled' AND published_at <= now()` 的文章
  批次轉成 `published`，並呼叫 `IQueryCache.InvalidateAsync`。
- 或者，改變既有公開讀取 API 的 WHERE 條件語意，讓 `status IN ('published', 'scheduled')` 都算
  可見（只要 `published_at` 已過）——但這樣「排程中」這個狀態值本身的意義會變得模糊（它就只是
  「有一個未來發布時間的已發布文章」，那 `scheduled` 存在的意義是什麼），且這條路要改既有
  `Features/News/ArticlesRepository.cs`，本輪任務範圍明講「一行都不要動，除非發現真的 bug（發現就
  回報）」——**這就是那個要回報的 bug／缺口**，不是本輪自己去改。

### 已發現、未動手修改的既有落差（回報）

1. **`apps/admin` 的 `ContentStatus` 型別有四態**（`draft`／`scheduled`／`published`／`disabled`，
   `apps/admin/src/types/common.ts`），**但 `articles.status` 的 CHECK 約束只有三態**（沒有
   `disabled`／下架）。`NewsListView.vue` 的下架按鈕、狀態篩選下拉選單在真的接上這組 API 之後
   會有一個選項對不到任何資料庫狀態。⛔ 沒有自己加值域——CHECK 約束要改是規格變更，得先改
   `docs/12` 再走同步鏈。
2. **`NewsArticle`（`apps/admin/src/types/news.ts`）的三個欄位在資料庫找不到對應欄位**：
   `coverImageAlt`（圖片替代文字，雙語）、`noIndex`（不讓搜尋引擎收錄）、`canonicalUrl`（正規網址）。
   `articles`／`articles_i18n` 都沒有這三個欄位。本輪的 DTO 因此**不包含**這三個欄位（寫了也沒地方
   存）。`coverImageAlt` 尤其值得注意：後台圖片上傳通則（規劃書 §4.0 v3.9）明講圖片要有雙語 Alt，
   但 `articles`／`articles_i18n` 的欄位清單裡沒有這個位置——這可能是 `docs/12` 尚未逐張同步的
   13 項落差之一（README 檔頭「v3.0 落差」有提到 `docs/12` §4／§6 明細尚未逐一改寫），也可能是
   真的漏了，需要回頭核對 `docs/12b-database-tables.md` §6 的欄位明細。
3. **`articles.slug` 是全站唯一（`UQ_articles_slug UNIQUE (slug)`），不是 `(club_id, slug)`
   複合唯一**——這點既有 README 段落「`club_id` 強制機制」已經寫過、已經用 curl 實測過，本輪
   只是再次確認：任務指示文字裡寫的「`UNIQUE (club_id, slug)`」跟 `db/club-schema.sql` 第 2309 行
   實際的 DDL 不一致，這裡以資料庫實際的 DDL 為準（已重新用 `grep` 核對過原始檔）。
4. **兩個既有測試假設過期，已修正**（不算本輪的錯，但在本輪執行 `dotnet test` 時才發現）：
   `ClubScopingTests`／`CacheBehaviorTests` 各有一個測試斷言「藍鯨（`bw`）球員數應該是 0」，
   這個假設在 BW-0g（藍鯨舊站資料匯入本機開發資料庫，見 `git log`，與本輪無關的獨立任務）之後
   不再成立——`bw` 現在也有 28 名真實球員。已改成驗證「兩俱樂部球員 id 集合互不重疊」（不論資料
   量怎麼變都能驗證 `club_id` 範圍真的有隔離，見測試檔內註解）。**這件事照 CLAUDE.md 第 13 條
   應該記進 `docs/18-work-errors.md`，但本輪的邊界明講不能碰 `docs/`，這裡先在 README 交代清楚，
   麻煩使用者或下一位轉記一筆。**

---

## 圖片上傳共用元件（S0-8）

規劃書 §4.0「後台圖片上傳通則」（v3.9）的後端落點。**這是全後台每一個表單共用的元件**——不是新聞
模組專屬，`Features/AdminNews` 只是目前唯一有真實後台畫面可以驗收接線的示範。下一棒
`frontend-architect` 接 `apps/admin` 的 `ImageUploader.vue` 時，照這份契約打就好。

### 給前端接的契約（S0-8 修正，2026-09-22：單一請求）

🔴🔴🔴 **這個契約整節改寫過**——舊版是「先呼叫獨立上傳端點拿 key、表單儲存時再把 key 塞進純
JSON 的建立／更新請求」的兩段式設計，因為違反規劃書「選檔不上傳、儲存才上傳」已經整支移除。
新契約：**建立／更新文章時，封面圖片跟其餘欄位在同一次 `multipart/form-data` 請求裡一起送出**。

**端點**（形狀不變，路徑與方法本來就是這兩個）：
- `POST /api/v1/admin/{club}/news`（建立，一律成草稿）
- `PUT /api/v1/admin/{club}/news/{id}`（更新，不改狀態）

（🔴 跟其餘 `Features/AdminNews` 端點一樣掛在 `DevWriteGate` 後面，關閉時 404）

**請求**：`multipart/form-data`，固定兩個欄位：

| 欄位名 | 必填 | 說明 |
|---|---|---|
| `payload` | ✅ | JSON 文字（camelCase），其餘欄位。建立用 `CreateArticleRequest` 的形狀（`slug`／`categoryCode`／`isFeatured`／`content`，**不含封面圖片**）；更新用 `UpdateArticleRequest` 的形狀（同上再加 `expectedUpdatedAt`／`removeCover`） |
| `file` | 選填 | 封面圖片。**建立時**：有夾檔案就上傳成封面，沒夾就是「這篇文章沒有封面圖片」。**更新時**：見下方「封面圖片三態」 |

**封面圖片三態（只影響 PUT）**：呼叫端不會、也不能直接指定物件鍵字串，只能表達「意圖」——

| 這次請求 | 效果 |
|---|---|
| 有夾 `file`，`removeCover` 隨意（會被忽略，見下一列） | 換成新圖：新圖上傳成功才會更新資料列，資料列更新成功後才刪舊物件（換圖與刪除順序不變，見 `AdminArticlesRepository.UpdateAsync`） |
| 沒夾 `file`，`payload.removeCover = true` | 清空封面圖片，並刪掉舊物件 |
| 沒夾 `file`，`payload.removeCover = false`（預設） | 維持目前的封面圖片不變，完全不碰物件儲存 |
| **同時**有夾 `file` **且** `payload.removeCover = true` | ⛔ 視為請求矛盾，回 400（「不能同時上傳新的封面圖片與移除封面圖片，請擇一。」），**不會**嘗試任何上傳 |

**回應**：跟建立／更新端點本來的回應完全一樣（`AdminArticleDetailDto`，含 `coverKey`），**沒有另外的
上傳回應格式**——這是這次修正的重點之一：不再有獨立的「上傳成功」這個中間狀態需要前端自己保管
物件鍵字串，伺服器端把上傳結果直接寫進同一次回應的 `coverKey`。四個衍生檔（1280／640／320／160
方形縮圖）的物件鍵由 `coverKey` 推導（`Images/ImageObjectKey.cs`：把副檔名前插入
`-1280`／`-640`／`-320`／`-thumb`），前端需要顯示縮圖時自己用同一套規則從 `coverKey` 算出來。

**錯誤（一律日常中文，`docs/06-conventions.md` §1）**：

| HTTP | 情境 | `detail` 文案 |
|---|---|---|
| 400 | 請求不是 `multipart/form-data`，或缺少 `payload` 欄位 | 「請求格式錯誤，需要 multipart/form-data（欄位 payload ＋ 選填的 file）。」／「缺少 payload 欄位。」 |
| 400 | `payload` 不是合法 JSON，或缺少必填欄位 | 「payload 欄位不是合法的 JSON，或缺少必填欄位。」 |
| 400 | 更新時同時夾檔案又勾選移除封面 | 「不能同時上傳新的封面圖片與移除封面圖片，請擇一。」 |
| 400 | 夾了檔案但是空檔案 | 「沒有收到圖片檔案，請重新選擇圖片。」 |
| 400 | 夾的檔案超過 10 MB | 「圖片檔案太大（上限 10 MB），請換一張或先壓縮。」 |
| 400 | 格式不支援（含假副檔名、HEIC） | 「圖片格式不支援，請上傳 JPG、PNG 或 WebP 格式的圖片（不支援 HEIC）。」 |
| 404 | `club` 代碼不存在或非啟用 | 沿用既有 `ClubNotFoundException` 文案（**在任何上傳嘗試之前就會擋下**，不會浪費一次上傳） |
| 409 | slug 重複／樂觀並行衝突／狀態轉換不合法／置頂精選已達上限 | 沿用既有文案（見上方各例外類別）。**若這次請求有夾檔案，圖片已經上傳成功但資料列寫入失敗時，伺服器端會自動刪掉剛剛上傳的物件**（補償交易，見下方「失敗回滾」），呼叫端不需要、也不應該自己再呼叫任何清理 |
| 413 | **超過約 11 MB**（見下方「Kestrel 請求主體上限」） | 平台預設的空白 413，不是本服務的 JSON 錯誤格式 |
| 401 | 未登入（S1 起，取代舊版「開發模式開關關閉」的 404） | 「請先登入後台。」，見上方「S1」整節 |

### 失敗回滾（補償交易，不是真正的跨系統 atomic transaction）

物件儲存（Azure Blob／Azurite）跟 SQL Server 是兩個獨立系統，做不到兩者要嘛都成功、要嘛都失敗
的真正 atomic transaction。伺服器端用**補償交易**模擬同樣的使用者體感——`AdminArticlesEndpoints`
的建立／更新處理常式裡：

1. 若這次請求有夾 `file`：**先**呼叫 `IImageStorageService.UploadAsync`（寫入物件儲存），
   成功才繼續下一步（規劃書「寫入 blob 成功才更新資料列」）。
2. 呼叫 `AdminArticlesRepository.CreateAsync`／`UpdateAsync` 寫資料列——這一步本身是 EF Core
   單一 `SaveChangesAsync`（單一 SQL transaction，多筆 INSERT／UPDATE 要嘛都成功要嘛都失敗）。
3. **第 2 步丟例外，或（僅更新）回傳 `null`（找不到這篇文章）**：`catch` 區塊／`null` 分支呼叫
   `IImageStorageService.DeleteAsync` 刪掉第 1 步剛剛上傳的物件（主檔＋四個衍生檔），再把原例外
   原樣往上丟／回 404——不留下沒有任何資料列指著它的孤兒物件。

🔴 **S0-8c 修正（2026-09-24）**：`BlobImageStorageService.UploadAsync` 本身在寫五個物件（主檔＋
四個衍生檔）時是循序寫入，不是單一原子操作——如果寫到一半（例如寫完主檔＋兩個衍生檔）網路中斷，
原本會留下**部分**衍生檔的孤兒物件，這個更深一層的缺口在「兩段式改單一請求」那次修正時被記錄下來
但沒有動手處理。**這次補上**：`UploadAsync` 內部把「寫主檔＋逐一寫衍生檔」整段包進 `try/catch`，
任何一個物件寫入失敗，都會呼叫**同一個** `DeleteAsync`（跟上面「請求端補償」共用同一個方法、
同一套 fail-open 語意，不是另外發明一套規則）盡力刪掉這次呼叫可能已經寫入的物件，再把造成失敗的
**原例外**原樣往外拋——`DeleteAsync` 依主鍵推導全部五把鍵、逐一呼叫 `DeleteIfExistsAsync`，
對「這次根本沒機會寫入」的鍵一樣安全（刪不存在的物件是等冪操作，不是錯誤），所以不需要另外追蹤
「究竟寫到第幾個」。`DeleteAsync` 本身不會往外拋（它自己的 `catch` 只記警告日誌），所以補償刪除
失敗絕不會蓋掉原例外，兩者組合起來就是「盡力清、原例外優先」。**殘留風險**（見下方「已知缺口」
第 4 點）：如果補償刪除本身也失敗（例如儲存體剛好在那個當下也不可用），該次呼叫寫成功的物件仍會
真的留下孤兒——這是雙重失敗才會發生的情況，接受此風險，不視為本次修正的缺口。**另評估過「行程中途
崩潰、補償邏輯根本來不及跑」的情況，判斷不值得現在做定期清理，理由見下方「已知缺口」第 4 點**。
自動化測試：`Tcrfc.Api.Tests/BlobImageStorageServiceUploadFailureTests.cs`（`N=1..5` 五個物件各自
驗證一次「失敗在這裡、原例外原樣拋出、事後不留殘留物件」，外加一支「補償刪除本身也失敗」的雙重
失敗情境）。

### `UploadSlotPolicy`：欄位插槽允許清單（`Features/Uploads/UploadSlotPolicy.cs`）

跟 `Features/AdminNews/SlugPolicy.cs` 同一種形狀：一份集中、有清楚維護說明的允許清單，不是散在
各處各自檢查。🔴🔴🔴 **目前只有一格**：`articles.cover`（對應 `articles.cover_key`，`AdminArticlesRepository`
換圖時的清理也是唯一真的接上的示範）。**這是刻意的最小化**——S0-8 的任務邊界是「共用元件做好＋
一個真實模組示範接線」，不是把後台全部圖片欄位一次接完。**⚠️ S0-8 修正（2026-09-22）：這份清單
不再對應一個獨立的 HTTP 端點**——原本的 `Features/Uploads/UploadsEndpoints.cs` 已整支移除（那是
「選檔即上傳」兩段式設計的載體）。之後其他模組（球員照片 `players.photo_key`、教練職員照片、
贊助商雙色標誌、商品圖集……）真的動工時，各自：

1. 先查 [`docs/12b-database-tables.md`](../../docs/12b-database-tables.md) §6 或
   `db/club-schema.sql` 確認資料表真的有對應的 `_key` 欄位，**不要假設**。
2. 在 `UploadSlotPolicy.AllowedSlots` 加一格 `entityType → { field, ... }`。
3. **把封面／照片欄位併進自己模組的建立／更新端點，比照 `AdminArticlesEndpoints` 的做法**——
   multipart 請求、`payload`＋`file` 兩欄位、上傳成功才寫資料列、失敗回滾——**不要另外開一個
   獨立的上傳端點**，那正是這次要修正的錯誤設計。
4. 在該模組自己的 repository 比照 `AdminArticlesRepository.UpdateAsync`／`DeleteAsync` 的寫法，
   換圖或刪資料列時呼叫 `IImageStorageService.DeleteAsync` 清掉舊物件。

### Kestrel 請求主體上限（本輪新增，`Program.cs`）

全域設定 `MaxRequestBodySize = 10MB + 1MB`（`ImageUploadOptions.MaxUploadBytes` 加一點緩衝給
multipart 邊界字串與其他表單欄位）。⚠️ **這只把「檔案太大」的邊界從 Kestrel 預設的 30MB 下移到
約 11MB，沒有讓所有超過上限的檔案都得到本服務的友善訊息**——實測驗證過兩種情況：

- **10–11 MB 之間**：程式碼裡的 `ImageUploadOptions.MaxUploadBytes` 檢查先跑到，回本服務的 400
  JSON（「圖片檔案太大」）。
- **超過約 11 MB**：Kestrel 在請求主體讀取階段就直接中止連線，回應是**平台內建的空白 413**，
  不會進到本服務的任何程式碼、不會是 JSON 格式、不會有中文訊息。**這是已知且接受的落差**——
  要讓所有超過 10MB 的檔案都得到一致的友善訊息，需要用 `IHttpMaxRequestBodySizeFeature` 搭配
  中介軟體攔截 `BadHttpRequestException` 自己組回應，本輪判斷這個投資報酬率不高（10–11MB 那個窄
  範圍已經涵蓋「使用者選錯檔、稍微超標」的常見情境，真正離譜過大的檔案回一個通用 413 也不算
  太差的使用者體驗），沒有動手做，列為已知缺口。

### 已知缺口（回報，不是自己判斷做或不做）

1. 🔴🔴🔴 **多數帶圖片欄位的資料表沒有 `_width`／`_height`／雙語 `_alt` 欄位**——規劃書 §4.0
   明訂每個圖片欄位是「一組欄位（物件鍵、寬、高、雙語 Alt 文字）」，但 `docs/12d-field-audit.md`
   §11 已經記錄：`db/club-schema.sql` 裡多數 `*_key` 欄位旁邊都沒有 `_width`／`_height`（只有
   `press_resources.cover_width`／`cover_height`、`impact_records.image_width`／`image_height`
   兩張表有），**全庫沒有任何 `_alt` 欄位**。這代表：
   - 本服務的上傳端點回應裡確實有算出真正的 `MainWidth`／`MainHeight`（`ImageProcessor` 的輸出），
     但**呼叫端目前沒有資料庫欄位可以存這兩個值**（`articles.cover_key` 就是唯一的例子——只有
     一個 `_key` 欄位，寬高算出來也沒地方寫回去）。
   - 雙語 Alt 文字完全沒有落點——前台 `<img alt>` 目前只能留空或沿用標題，這是**無障礙缺口**。
   - **這是資料庫綱要的落差，不是本次任務範圍能修的**——要補欄位得先走 `docs/00-harness.md`
     §2.5 同步鏈（改規劃書確認 → 改 `docs/12b` → 改 `db/club-schema.sql` → 重新 scaffold EF Core）。
     **本輪沒有自己加欄位**，只在這裡回報：`docs/12d-field-audit.md` §11 已經有一模一樣的結論，
     這不是新發現，是重新確認同一個已知缺口在圖片上傳管線真的做出來之後確實會卡到。
2. **沒有做「依版位設定尺寸下限」**：規劃書 §4.0「尺寸下限依版位另定……低於下限擋下不收，
   不放大補齊」——這是一個依「這張圖要放在前台哪個版位」而變動的業務規則（例如主視覺類不得小於
   1600px 寬），本服務目前是**通用元件**，沒有這一層「版位」概念，也就沒有實作這個下限檢查。
   呼叫端（各模組自己的前端表單）如果需要，要嘛在前端擋（跟規劃書「兩道驗證」的前端那一道一致），
   要嘛之後在 `UploadSlotPolicy` 的允許清單裡幫每個插槽加一個「最小尺寸」欄位，本輪沒有做，
   因為只有一個插槽（新聞封面圖）在跑，規劃書也沒有明講新聞封面圖的下限是多少。
3. **HEIC 拒絕只用手動測試驗證過，沒有進自動化測試**：`ImageProcessorTests`／`AdminNewsCoverUploadTests`
   涵蓋「假副檔名文字檔」與「截斷的損毀 JPEG」兩種「格式不支援」情境（在任何機器上都能重現，
   不依賴外部工具），但**沒有涵蓋真的 HEIC 檔案**——本機驗證時用 macOS 內建的 `sips` 工具產生了
   一個真正的 HEIC 檔案（`sips -s format heic ...`）並實測確認被擋下（見上方驗收紀錄），但這個
   產生方式是 macOS 專屬，寫進自動化測試會在 CI 的 Linux runner 上跑不動。**兩者的擋法完全相同**
   （格式偵測得出容器格式，但 `ImageSharp` 沒有對應解碼器 → 同一個 `UnsupportedImageFormatException`
   例外路徑），所以自動化測試涵蓋的兩種情境已經證明了同一段程式碼會攔下 HEIC，只是沒有對「HEIC
   本身」這個具體格式跑一次可重複的自動化斷言。
4. **孤兒物件**：🔴 S0-8 修正（2026-09-22）後，「先上傳拿 key、使用者取消表單」這個情境
   **已經不存在**——改成單一 multipart 請求後，使用者不按「儲存」就不會有任何 HTTP 請求送出，
   天然不會有任何物件被寫進儲存體；請求送出但資料列寫入失敗的情境也已經用補償交易回滾（見上方
   「失敗回滾」，`AdminNewsCoverUploadTests` 有自動化測試釘住）。**殘留的孤兒物件來源縮小為兩種**：
   - 換圖或刪除**舊**物件失敗時（`IImageStorageService.DeleteAsync` fail-open 吞例外）——這是
     刻意的取捨（見該方法上的說明：資料庫的新值已經寫入成功，不該讓一個非關鍵的清理步驟讓
     整個請求變成 500），舊物件因此可能永遠留在儲存體裡。
   - ~~`BlobImageStorageService.UploadAsync` 寫五個物件本身不是單一原子操作，寫到一半失敗會留下
     部分衍生檔的孤兒物件~~ ✅ **S0-8c 修正（2026-09-24）已補上**：任一個物件寫入失敗，現在會
     盡力刪掉這次呼叫已經寫入的物件再拋出原例外，見上方「失敗回滾」末段。**殘留的兩種情況**：
     ① 補償刪除本身也失敗（雙重失敗，`DeleteAsync` fail-open 決定不重試）；
     ② **行程在寫到一半時直接崩潰**（容器重建、`OOM`、宿主機斷電……），補償的 `catch` 區塊
     根本沒有機會執行——這種情況目前**沒有**定期清理機制去事後補救，評估後判斷現在不值得做，
     理由：
     - 要安全判斷「這把鍵是孤兒」，必須先有「全系統目前所有合法引用鍵的完整清單」，但 S0-8
       目前**只接了 `articles.cover_key` 一個模組**（見上方「`UploadSlotPolicy`」整節），
       之後每接一個新的圖片欄位（球員照片、贊助商標誌、商品圖集……）都必須記得同步更新這份清單，
       而且**沒有任何機制會在忘記更新時失敗**——這正是這個專案自己在
       [`docs/18-work-errors.md`](../../docs/18-work-errors.md) `E-31`／`E-39` 升級段落裡點名的
       「局部套用的機制會給出全面的信心」：一個只涵蓋單一模組就上線的孤兒清理，比沒有清理更危險，
       因為它會讓人誤以為「有在管」，而它真正涵蓋的範圍會隨著模組增加而**默默失真**。
     - 這一類清理若誤判「合法但恰好還沒被任何清單看到的鍵」為孤兒並刪除，後果是**刪掉正在使用中
       的圖片**——比放著孤兒物件不管嚴重得多（規劃書明訂本通則「不做」使用位置追蹤，見下段），
       不值得為了省一點儲存空間去換這個下修風險。
     - 影響面本身也很小：觸發窗口只有「正在執行五次循序 blob 寫入」的那幾百毫秒到幾秒，
       且只有整個行程被中止（不是一般的例外）才會命中；後果純粹是**儲存體多幾個沒人參照的物件**，
       不影響資料正確性（跟 S0-8c 這次要修正的「中途失敗」是同一個不變量）。
     - 若之後真的要做，時機應該是「S0-8 的圖片欄位全部模組都接完之後」——那時才有辦法一次性
       比對「資料庫裡所有 `*_key` 欄位的值」與「儲存體實際物件」，而不是接一個模組就得重新評估
       一次涵蓋範圍。**這件事本身也應該走 `docs/00-harness.md` §2.5 同步鏈**（規劃書要不要新增
       這個機制），不是後端自己判斷要不要做。

   本服務沒有做孤兒物件的定期清理（例如比對資料庫實際引用的 key 集合，反查儲存體多出來的物件），
   這是規劃書 §4.0 明文「不做」的範圍之一（「本通則不做：使用位置追蹤與刪除前警示」），不是漏做；
   上面「行程中途崩潰」的評估與這條規劃書條文的方向一致，不是互相矛盾。

---

## 開發過程踩的坑（已修正，記錄見 `docs/18-work-errors.md` E-19／E-20／E-3?）

1. **`InvariantGlobalization=true` 讓 `Microsoft.Data.SqlClient` 連線直接炸**（E-19）：
   這個旗標對純 HTTP／JSON 服務通常安全，但只要相依鏈裡有需要定序或編碼轉換的資料庫驅動就是地雷。
   已移除。
2. **Dapper 的 record 具現化對 `DateOnly` 與 SELECT 欄位數要求比預期嚴格**（E-20）：
   SQL `date` 欄位在 ADO.NET 層永遠回報 `DateTime`，record 屬性宣告成 `DateOnly` 會讓 Dapper
   找不到相符的建構子而整支查詢丟 `InvalidOperationException`；另外兩個查詢共用一個 record 型別
   但 SELECT 的欄位數不同，也是同一種例外。已修正（`PlayersRepository`／`MatchesRepository` 的
   內部 row 型別改用 `DateTime`，`Map()` 再轉 `DateOnly`；`ArticlesRepository` 拆出精確對應欄位的型別）。
3. **本輪（S0-8）新踩到：容器「已確保存在」旗標在失敗時也被設成已完成，導致真正的錯誤被
   後續呼叫的誤導性錯誤蓋掉**：`BlobImageStorageService.EnsureContainerAsync` 原本用
   `Interlocked.CompareExchange` 在呼叫 `CreateIfNotExistsAsync` **之前**就把旗標設成「已確保」，
   本機對 Azurite 實測時，第一次呼叫因為 `Azure.Storage.Blobs` SDK 版本比 Azurite 認得的 API
   版本新而失敗（見 README「本機開發：Azurite」），但旗標已經被設成 true；**第二次呼叫因此跳過
   容器建立**，改直接對一個從未真正建立成功的容器寫入，得到的錯誤變成「容器不存在」而不是
   一開始那個真正的 API 版本不相容——排查時得先想到旗標邏輯本身可能有問題，不能只看最新一次的
   錯誤訊息。已修正：改用 `SemaphoreSlim` 包住整段（`CreateIfNotExistsAsync` 之後才設旗標），
   失敗時旗標維持未確保，下一次呼叫會重新嘗試。**這一類「先設完成旗標、再做真正會失敗的動作」
   的寫法本身就是根因**，下次寫類似的「只做一次」快取旗標時要記得旗標必須在動作成功之後才設定。

---

## 驗收紀錄（2026-09-21，本機環境）

環境：`docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d mssql-dev`（已跑、healthy），
DDL 與種子資料已灌（`clubs` 2、`players` 28、`staff` 8、`articles` 83、`matches` 21、`article_categories` 8）。

```
$ dotnet build
建置成功。0 個警告 0 個錯誤

$ docker build -t tcrfc-api-test apps/api    # 用 apps/api/Dockerfile 實際建置一次
...
naming to docker.io/library/tcrfc-api-test:latest done   # 成功

$ docker run ... tcrfc-api-test   # 容器內連本機 mssql-dev（host.docker.internal）
$ docker inspect --format='{{.State.Health.Status}}' tcrfc-api-test-run
healthy    # apps/api/Dockerfile 的 HEALTHCHECK 打 /readyz，通過

$ curl /healthz
{"status":"ok"}
$ curl /readyz
{"status":"ready","club_db":"ok"}

$ curl /api/v1/clubs
[{"code":"tcrfc","name":"台中磐石",...},{"code":"bw","name":"台中藍鯨",...}]

$ curl "/api/v1/tcrfc/players?pageSize=100" | jq '.totalCount'
28
$ curl "/api/v1/bw/players?pageSize=100" | jq '.totalCount'
0

$ curl "/api/v1/tcrfc/staff?pageSize=100" | jq '.totalCount'
8

$ curl "/api/v1/tcrfc/news?pageSize=200" | jq '.totalCount'
83
$ curl "/api/v1/tcrfc/news?category=match&pageSize=200" | jq '.totalCount'
56

$ curl "/api/v1/tcrfc/schedule?pageSize=100" | jq '.totalCount'
21
$ curl "/api/v1/bw/schedule?pageSize=100" | jq '.totalCount'
0

# 跨俱樂部繞過嘗試：拿 tcrfc 一篇文章的 slug 改用 bw 路由查
$ curl -o /dev/null -w "%{http_code}\n" "/api/v1/bw/news/2026-08-10-international-000"
404

# 不存在的俱樂部
$ curl -o /dev/null -w "%{http_code}\n" "/api/v1/does-not-exist/players"
404

# SQL injection 嘗試
$ curl "/api/v1/tcrfc/players?team=D1';DROP TABLE players;--"
{"items":[],"page":1,"pageSize":50,"totalCount":0,"totalPages":0}
$ curl "/api/v1/tcrfc/players?pageSize=1" | jq '.totalCount'   # 確認表還在
28

# 語言回退
$ curl "/api/v1/tcrfc/clubs/tcrfc?lang=en" | jq '.name'   # 有英文
"Taichung Rock FC"
$ curl "/api/v1/clubs/bw?lang=en" | jq '.name'             # 沒英文，回退中文
"台中藍鯨"
$ curl "/api/v1/tcrfc/staff?lang=en&pageSize=50" | jq -r '.items[] | "\(.name) | \(.title)"'
Juliano Rodrigues | 守門員教練    # name 有英文、title 沒有英文各自獨立回退
...
許志傑 | 青訓教練                # name 也沒英文，整欄回退中文

# 正式環境關閉 OpenAPI（容器內 ASPNETCORE_ENVIRONMENT=Production）
$ curl -o /dev/null -w "%{http_code}\n" "/openapi/v1.json"
404
```

（原始輸出格式已用 `jq`／`python3 -m json.tool` 整理以便閱讀，數字與行為與實際 curl 回應一致。）

### S0-7d 驗收紀錄（2026-09-21）

環境：本機 `mssql-dev`（同上，種子資料未變）。**這個環境的 Docker Desktop 對 Docker Hub 的拉取
異常緩慢**（`redis:8-alpine` 光拉取層就卡了超過 30 分鐘，`docker compose up` 因此不可行）——
改用 `brew install redis` 取得**真正的 `redis-server` 二進位檔**（非 mock、非模擬），跑在本機
`16379` port，讓 `apps/api` 的 Docker 容器透過 `host.docker.internal` 連過去，等於用另一條路徑
换到同樣是「真實 Redis」的驗證環境。這件事本身也記進 [`docs/18-work-errors.md`](../../docs/18-work-errors.md)。

```
$ dotnet build   # apps/api 與 apps/api/Tcrfc.Api.Tests 兩個專案
Build succeeded. 0 Warning(s) 0 Error(s)

$ docker build -t x apps/api      # 任務要求的確認命令，build context 仍是 ./apps/api
...
naming to ghcr.io/waiting0201/tcrfc-api:latest done   # 成功，Tcrfc.Api.Tests 未被送進 build context

$ cd apps/api/Tcrfc.Api.Tests && dotnet test
# CLUB_SQL_CONNECTION_STRING 未設定時：
Failed! - Failed: 14, Passed: 0, Skipped: 0, Total: 14   # 全部清楚回報失敗＋可執行的修復訊息，不是悄悄略過
# 設定 CLUB_SQL_CONNECTION_STRING 指向本機 mssql-dev 後：
Passed! - Failed: 0, Passed: 14, Skipped: 0, Total: 14, Duration: 2 s
```

`/readyz` 三種情境（直接 `dotnet run`，未經 Docker）：

```
# 完全沒設定 CHARITY_SQL_CONNECTION_STRING／REDIS_HOST
$ curl /readyz
{"status":"ready","club_db":"ok","charity_db":"not_configured","redis":"not_configured"}

# CHARITY_SQL_CONNECTION_STRING 指向一個確定連不上的位址（127.0.0.1:19999）
$ curl -w "\nHTTP:%{http_code}\n" /readyz
{"status":"not_ready","club_db":"ok","charity_db":"fail","redis":"not_configured"}
HTTP:503
```

容器層級的 Redis fail-open（用 `docker run` 跑已建好的 `ghcr.io/waiting0201/tcrfc-api:latest`，
`ASPNETCORE_ENVIRONMENT=Production`，連本機 `mssql-dev` 與 `brew` 裝的真實 `redis-server`）：

```
# 情境一：REDIS_HOST 指到一個從未有任何服務存在的位址與埠——啟動時就連不上
$ docker run -d -e REDIS_HOST=host.docker.internal -e REDIS_PORT=59999 ... tcrfc-api
$ docker logs <container>   # 正常啟動，沒有因為 Redis 連不上而崩潰或延遲
Now listening on: http://[::]:8080
$ curl /readyz
{"status":"ready","club_db":"ok","charity_db":"not_configured","redis":"degraded"}
$ curl "/api/v1/tcrfc/players?pageSize=1"   # 經過 IClubResolver → RedisQueryCache
{"items":[{...,"name":"蔡俊昇",...}],"page":1,"pageSize":1,"totalCount":28,"totalPages":28}
$ docker inspect --format='{{.State.Health.Status}}' <container>
healthy

# 情境二：REDIS_HOST 指到一個真的在跑的 redis-server（brew，16379 埠，requirepass 開著）
$ docker run -d -e REDIS_HOST=host.docker.internal -e REDIS_PORT=16379 -e REDIS_PASSWORD=<本機一次性測試密碼，已隨 redis-server 程序關閉失效> ...
$ curl /readyz
{"status":"ready","club_db":"ok","charity_db":"not_configured","redis":"ok"}

# 命中 IClubResolver 的快取路徑後，redis-cli 直接檢查——key 命名、TTL、內容全部符合設計：
$ curl "/api/v1/tcrfc/players?pageSize=1" >/dev/null
$ redis-cli -p 16379 -a *** keys '*'
v0:tcrfc:_any:club-scope:
$ redis-cli -p 16379 -a *** ttl "v0:tcrfc:_any:club-scope:"
300
$ redis-cli -p 16379 -a *** get "v0:tcrfc:_any:club-scope:"
"f7cb2444-e607-57fc-aea5-6aa11239d4ea"   # 真正快取住的俱樂部 GUID

# 手動遞增版本號（模擬日後後台寫入層呼叫 InvalidateAsync），驗證失效機制：
$ redis-cli -p 16379 -a *** incr "ver:club-scope:tcrfc"
(integer) 1
$ curl "/api/v1/tcrfc/players?pageSize=1" >/dev/null
$ redis-cli -p 16379 -a *** keys '*club-scope*'
v0:tcrfc:_any:club-scope:
ver:club-scope:tcrfc
v1:tcrfc:_any:club-scope:   # 版本號一變，立刻改寫新 key；舊 v0 key 原封不動留給 TTL／LRU 淘汰

# 情境三：Redis 在連線建立「之後」才掛掉（比情境一更貼近正式環境的真實故障模式）
$ kill <redis-server pid>   # 直接停掉 redis-server，模擬正式環境 Redis 中途掛掉
$ redis-cli -p 16379 -a *** ping
Could not connect to Redis at 127.0.0.1:16379: Connection refused
$ curl "/api/v1/tcrfc/players?pageSize=1"   # 同一個、已經連線過的容器，不重啟
{"items":[{...,"name":"蔡俊昇",...}],"page":1,"pageSize":1,"totalCount":28,"totalPages":28}   # 仍然 200，回源 SQL
$ curl /readyz
{"status":"ready","club_db":"ok","charity_db":"not_configured","redis":"degraded"}   # 從 ok 變 degraded，status 仍是 ready
```

三個情境合起來證明：Redis 從一開始就連不上、Redis 正常運作時真的會寫入並照設計的 key／TTL／版本號
規則運作、以及 Redis 在執行中途才斷線——三種情況下 API 都沒有變成不可用，只有 `/readyz` 的 `redis`
欄位如實反映狀態。

### S0-7d 續作驗收紀錄（2026-09-21，五組 repository 接上 `IQueryCache`）

自動化測試：`dotnet test` 從 14 項增為 **30 項**，全部沿用真實 `mssql-dev`；新增的 16 項裡
10 項是 `CacheBehaviorTests`（用真的啟動的 `redis-server`），6 項是 `CacheFailOpenTests` 擴增
（改用 `[Theory]` 涵蓋五組端點＋新增文章單篇的 fail-open 測試）。

```
$ dotnet build   # apps/api 與 apps/api/Tcrfc.Api.Tests
Build succeeded. 0 Warning(s) 0 Error(s)

$ cd apps/api/Tcrfc.Api.Tests && dotnet test
Passed! - Failed: 0, Passed: 30, Skipped: 0, Total: 30, Duration: 16 s

$ docker build -t x apps/api
...
naming to docker.io/library/x:latest done   # 成功
```

容器層級驗證（`docker run` 跑上面建好的映像檔，接本機 `mssql-dev`，以及 `brew install redis` 裝的
真實 `redis-server`，`ASPNETCORE_ENVIRONMENT=Production`）：

```
$ curl /readyz
{"status":"ready","club_db":"ok","charity_db":"not_configured","redis":"ok"}

# 依序打熱五組端點（clubs／tcrfc 的 players／staff／news／schedule），外加 bw 的 players
$ redis-cli -p 16380 -a *** keys '*'
v0:tcrfc:zh-Hant:players::1:5
v0:tcrfc:_any:club-scope:
v0:tcrfc:zh-Hant:staff::1:5
v0:bw:zh-Hant:players::1:5
v0:tcrfc:zh-Hant:schedule::::1:5
v0:_shared:zh-Hant:clubs-list:
v0:tcrfc:zh-Hant:articles::1:5
v0:bw:_any:club-scope:
```

逐一核對：

- **entity 名稱與 club／locale 維度如設計**：`players`／`staff`／`articles`／`schedule` 都帶著
  `tcrfc` 或 `bw` 的 club 維度；`clubs-list` 用 `_shared`（不屬於任一俱樂部）；`club-scope`
  （既有的 `ClubResolver` 接線）維度是 `_any`（與語系無關）。
- **qualifier 真的把每個參數編進 key**：`schedule` 的 `v0:tcrfc:zh-Hant:schedule::::1:5`——
  `team:season:status:page:pageSize` 五段，前三段（team／season／status）都沒帶篩選時是空字串，
  page=1、pageSize=5 兩段有值，跟 `MatchesRepository.ListAsync` 的 qualifier 組字串邏輯逐字對得上。
- **TTL**：`redis-cli -p 16380 -a *** ttl "v0:tcrfc:zh-Hant:players::1:5"` → `282`（在預設 300 秒
  之內，是打完 API 之後幾秒才查詢的合理耗損）。`clubs-list` 的 TTL 也是 `282`，同一批請求、同一個
  TTL 設定，數字一致。
- **club 隔離不是憑空推論**：`v0:bw:zh-Hant:players::1:5` 的值是
  `{"Items":[],"Page":1,"PageSize":5,"TotalCount":0,"TotalPages":0}`——`bw` 自己的 key、自己的
  0 筆結果，跟 `tcrfc` 那把 28 筆的 key 完全分開。額外用 HTTP 逐一核對 `bw` 的
  `staff`／`news`／`schedule` 三個端點也都回 `totalCount: 0`，而 `tcrfc` 的 `players` 仍是 28。
- **404 不快取**：打一個確定不存在的 slug（`this-does-not-exist-xyz`）回 404 後，
  `redis-cli -p 16380 -a *** keys '*article-detail*'` 回傳空——沒有任何 key 被寫入。
- **版本號失效在新接的 entity 上也成立**：`redis-cli -p 16380 -a *** incr "ver:players:tcrfc"` 之後
  再打一次 `tcrfc` 的 `players`，`keys '*players*'` 同時看得到 `v0:tcrfc:...`（舊，留給 TTL／LRU）
  與 `v1:tcrfc:...`（新），而 `v0:bw:...` 沒被這次遞增影響——版本號的 club 維度也正確隔離。
- **Redis 執行中途掛掉，五組端點與 `/readyz` 都正確 fail-open**（同一個、不重啟的容器）：

  ```
  $ kill <redis-server pid>
  $ for p in /api/v1/clubs "/api/v1/tcrfc/players?pageSize=1" "/api/v1/tcrfc/staff?pageSize=1" \
             "/api/v1/tcrfc/news?pageSize=1" "/api/v1/tcrfc/schedule?pageSize=1"; do
      curl -s -o /dev/null -w "%{http_code}\n" "http://127.0.0.1:18097$p"
    done
  200
  200
  200
  200
  200
  $ curl /readyz
  {"status":"ready","club_db":"ok","charity_db":"not_configured","redis":"degraded"}
  ```

⚠️ **本次驗證再度用 `brew install redis` 的真實 `redis-server` 而不是 Docker 容器**——這個沙盒環境
拉 `redis:8-alpine` 依然極慢（同一個環境限制，見上一階段「S0-7d 驗收紀錄」與 `docs/17` §11 第 12 條），
沿用同一個解法。收尾已確認清掉手動啟動的 `redis-server` 行程與可能產生的 `dump.rdb`
（`--save ""` 已避免它產生，但仍執行過一次 `ls dump.rdb` 確認）。

### 追加：`matches.match_no`（2026-09-21）

`db/club-schema.sql` 補上 `matches.match_no`（聯賽官方場次編號）後，`MatchDto`／`MatchesRepository`／
`MatchesEndpoints` 同步補上這個欄位（明確 SELECT，非 `SELECT *`；沿用既有 `ClubScope` 機制，
沒有另外開任何繞過路徑）。驗證：

```
$ curl "/api/v1/tcrfc/schedule?pageSize=200" | jq '[.items[] | select(.matchNo == null)] | length'
0   # 21 筆全部帶值，與 site/src/data/schedule.json 的 match_no 逐筆核對一致
```

---

## 驗收紀錄（本輪，2026-09-22，後台新聞寫入垂直切片）

環境：本機既有 `sqlserver` 容器（`tcrfc_club_dev`，`articles` 83 筆，`clubs` 2 筆：`tcrfc`／`bw`）。

### 1. `dotnet build`／`dotnet test` 全過，既有測試不回歸

```
$ cd apps/api && dotnet build
建置成功。0 個警告 0 個錯誤

$ cd Tcrfc.Api.Tests && dotnet build
建置成功。0 個警告 0 個錯誤

$ export CLUB_SQL_CONNECTION_STRING="Server=127.0.0.1,1433;Database=tcrfc_club_dev;User Id=sa;Password=***;TrustServerCertificate=True;"
$ dotnet test
已通過! - 失敗: 0，通過: 48，略過: 0，總計: 48，持續時間: 21 s
# 30 項既有（含本輪修正的 2 項過期斷言，見上方「已發現、未動手修改的既有落差」第 4 點）
# + 18 項本輪新增（AdminNewsGateClosedTests 3、AdminNewsWriteTests 14、AdminNewsCacheInvalidationTests 1）
```

測完直接查資料庫確認測試自己建立的資料都清乾淨、沒有污染種子資料：

```
$ (查 slug LIKE 'admin-write-test-%' OR 'shared-test-%' OR 'cache-invalidate-test-%')
leftover_test_articles: 0
total_articles: 83   # 跟測試前一致
```

### 2. EF Core handoff 實測（見上方整節說明）

```
# 套用基準 migration 前
table_count: 144　ef_history_exists: 0　articles_before: 83

$ dotnet ef database update --context ClubDbContext
Applying migration '20260922070223_InitialBaseline'.
# 觀察到的 SQL 只有：CREATE TABLE __EFMigrationsHistory + INSERT 一筆紀錄，沒有其他 DDL

# 套用後
table_count: 145（只多 __EFMigrationsHistory）　articles_after: 83（不變）
```

### 3. 開發模式開關實測：關 → 404 → 開 → 可用 → 容器層級 Production 下仍是 404

```
# 情境一：dotnet run，Development，不設 ENABLE_UNSAFE_DEV_WRITES
$ curl -o /dev/null -w "%{http_code}\n" /api/v1/admin/tcrfc/news
404
$ curl -o /dev/null -w "%{http_code}\n" -X POST /api/v1/admin/tcrfc/news -d '{...}'
404
$ curl /api/v1/tcrfc/news?pageSize=1   # 既有公開端點不受影響
{"items":[...],"totalCount":83,...}

# 情境二：dotnet run，Development，ENABLE_UNSAFE_DEV_WRITES=true
$ curl -o /dev/null -w "%{http_code}\n" /api/v1/admin/tcrfc/news
200
# 啟動 log 印出：🔴 寫入端點開發模式開關已開啟...切勿在任何對外環境開啟此設定。

# 情境三（更重要）：docker build 出來的映像檔，ASPNETCORE_ENVIRONMENT=Production
#   （模擬正式部署，即使沒設 ENABLE_UNSAFE_DEV_WRITES，正式環境本來就不會設）
$ docker build -t x apps/api   # 成功，Tcrfc.Api.Tests 未被送進 build context（.dockerignore 既有排除）
$ docker run ... -e ASPNETCORE_ENVIRONMENT=Production ...
$ curl -o /dev/null -w "%{http_code}\n" /api/v1/admin/tcrfc/news
404
$ curl /healthz
{"status":"ok"}
```

### 4. 完整生命週期（建立草稿 → 改內容 → 排程 → 發布 → 刪除），每一步查資料庫

```
# 1. 建立草稿
$ curl -X POST /api/v1/admin/tcrfc/news -d '{"slug":"curl-verify-article","categoryCode":"club",...}'
{"id":"69fa9fae-...","status":"draft","updatedAt":"2026-09-22T07:15:13.883",...}
$ SELECT id,slug,status,club_id,created_by,updated_by FROM articles WHERE slug='curl-verify-article'
69FA9FAE-... curl-verify-article draft F7CB2444-...(tcrfc) NULL NULL
$ SELECT locale,title FROM articles_i18n WHERE article_id='69fa9fae-...'
zh-Hant  curl 驗證文章

# 2. 改內容（PUT，帶 X-Dev-Operator-Id 標頭示範，admin_users 是空表故仍為 NULL）
$ curl -X PUT .../news/69fa9fae-... -H "X-Dev-Operator-Id: <random-guid>" -d '{...expectedUpdatedAt:上一步的值...}'
{"status":"draft","coverKey":"articles/2026/09/curl-cover.webp","zh":{"title":"curl 驗證文章（已編輯）"},"en":{"title":"Curl Verify Article (Edited)"},"updatedAt":"2026-09-22T07:15:53.661"}
$ SELECT slug,cover_key,updated_by,updated_at FROM articles WHERE id='69fa9fae-...'
curl-verify-article  articles/2026/09/curl-cover.webp  NULL  2026-09-22 07:15:53.661
$ SELECT locale,title,summary FROM articles_i18n WHERE article_id='69fa9fae-...'
en       Curl Verify Article (Edited)   NULL
zh-Hant  curl 驗證文章（已編輯）          改過的摘要

# 3. 排程發布（未來 30 分鐘）
$ curl -X POST .../news/69fa9fae-.../schedule -d '{"expectedUpdatedAt":"...","publishAt":"<UTC+30min>"}'
{"status":"scheduled","publishedAt":"2026-09-22T07:46:04.227",...}
$ SELECT status,published_at,SYSUTCDATETIME() FROM articles WHERE id='69fa9fae-...'
scheduled  2026-09-22 07:46:04.227  2026-09-22 07:16:04.44（確認 published_at 真的在未來）
$ curl -o /dev/null -w "%{http_code}\n" /api/v1/tcrfc/news/curl-verify-article   # 排程中，公開 API 應該看不到
404   # 🔴 見上方「排程發布：誰改狀態」整節——這個 404 會一直維持到有人呼叫 /publish 為止

# 4. 發布（立即生效）
$ curl -X POST .../news/69fa9fae-.../publish -d '{"expectedUpdatedAt":"..."}'
{"status":"published","publishedAt":"2026-09-22T07:16:14.426",...}
$ SELECT status,published_at FROM articles WHERE id='69fa9fae-...'
published  2026-09-22 07:16:14.426
$ curl /api/v1/tcrfc/news/curl-verify-article   # 公開 API 現在看得到了
{"id":"69fa9fae-...","title":"curl 驗證文章（已編輯）","summary":"改過的摘要",...}

# 5. 刪除
$ curl -X DELETE ".../news/69fa9fae-...?expectedUpdatedAt=<url-encoded>"
HTTP 204
$ SELECT COUNT(*) FROM articles WHERE id='69fa9fae-...'          → 0
$ SELECT COUNT(*) FROM articles_i18n WHERE article_id='69fa9fae-...' → 0（FK ON DELETE CASCADE）
$ curl -o /dev/null -w "%{http_code}\n" /api/v1/tcrfc/news/curl-verify-article        → 404
$ curl -o /dev/null -w "%{http_code}\n" /api/v1/admin/tcrfc/news/69fa9fae-...         → 404
```

### 5. 補充驗證（業務規則）

```
$ curl -X POST .../news -d '{"slug":"2026-08-10-international-000",...}'   # 撞既有文章的 slug
{"title":"網址名稱重複","status":409,"detail":"網址名稱「2026-08-10-international-000」已經被使用，請換一個。"}

$ curl -X POST .../news -d '{"categoryCode":"not-real",...}'
{"title":"輸入內容有誤","status":400,"detail":"找不到分類代碼「not-real」。"}
```

俱樂部範圍（另一俱樂部路由改不到／刪不到）、共用內容唯讀（403）、樂觀並行衝突（409，且確認
「不是後寫的贏」）、三態轉換（已發布不能再排程）、雙語側表整份取代語意、快取失效（真實 Redis，
更新後公開 API 立刻反映新標題）——這幾項改用自動化測試涵蓋（見下方「測試」），不在這裡重複貼
curl，測試本身也是對 `tcrfc_club_dev` 打真正的 HTTP 管線，不是 mock。

### 6. 既有五組 GET 端點回歸比對（同一次跑，前後對照 S0-7d 驗收紀錄的數字）

```
clubs 清單筆數:      2     （不變）
tcrfc players 筆數:  28    （不變）
tcrfc staff 筆數:    8     （不變）
tcrfc news 筆數:     83    （不變，且等於本輪操作完、測試清乾淨之後的筆數）
tcrfc schedule 筆數: 21    （不變）
/readyz:             {"status":"ready","club_db":"ok","charity_db":"not_configured","redis":"not_configured"}
```

---

## 測試（`apps/api/Tcrfc.Api.Tests`，`dotnet test` 全部 141 項全過，S1 新增 31 項見上方「S1」整節「測試結果」，S0-7g 新增 3 項見下方 `ScheduledPublishTests`，S0-8c 新增 6 項見下方 `BlobImageStorageServiceUploadFailureTests`）

S0-7b 為止零測試——所有行為保證只存在於本檔的 curl 紀錄裡。S0-7d 新增獨立測試專案
`Tcrfc.Api.Tests`（xUnit 2.9 + `Microsoft.AspNetCore.Mvc.Testing`），對 `Program`
用 `WebApplicationFactory<Program>` 啟動真正的行程內主機，打真正的 HTTP 管線，接真正的
本機 `mssql-dev`——不 mock 資料庫，也不 mock Redis：`RedisUnavailableApiFixture` 用「指向一個
確定沒人聽的本機連接埠」讓失敗是真的失敗；`RedisEnabledApiFixture`（S0-7d 續作新增）直接啟動一個
真正的 `redis-server` 子行程（`brew install redis` 裝的那個二進位檔），驗證 key／TTL／隔離這些
「Redis 正常運作時該長什麼樣」的行為，不是靠讀程式碼推論。**S0-8 沿用同一個原則接上真正的
物件儲存**：`AdminWriteAzuriteEnabledApiFixture` 啟動一個真正的 `azurite-blob` 子行程
（`npm install -g azurite` 裝的那個執行檔），不 mock `Azure.Storage.Blobs`。
🔴 **`99 = 既有 78（零回歸）＋ 本輪新增 21`**（`ImageProcessorTests` 9 項純邏輯單元測試＋
`AdminNewsCoverBlobCleanupTests` 5 項＋`AdminNewsCoverUploadTests` 7 項）。**S0-8 修正
（2026-09-22，上傳時機改單一請求）把原本的 `UploadImagesEndpointTests`（6 項）／
`UploadImagesGateClosedTests`（1 項）整批刪除**——這兩支測試打的是已經整支移除的獨立上傳端點，
不是「測試變少了」，格式／大小／空檔案的驗證覆蓋改搬進 `AdminNewsCoverUploadTests`（透過建立
文章端點觸發同一段驗證邏輯），另外新增了兩支舊契約下不可能寫的測試：「上傳成功但資料列寫入
失敗時回滾、不留孤兒物件」（建立與更新各一支，這正是「取消表單不留下任何檔案」在後端的可驗證
等價形式，見 `apps/api/README.md`「圖片上傳共用元件」§「失敗回滾」）。⚠️ **這個沙盒環境跑 99 項
整套要 3–4 分鐘**（單獨跑
一個測試類別通常在數百毫秒內完成，但 `dotnet test` 的行程啟動與 JIT 暖機開銷在這台開發機上
明顯偏高——本輪驗證時第一次以為是卡死，用 macOS `sample` 對行程取樣後看到主執行緒卡在
`WaitHandle_WaitOneCore`，才確認是「這台機器上等待真的很慢」而不是死鎖，耐心等到最後真的印出
`Passed!` 為止，不要在還沒看到終端摘要前就判斷為卡住而砍掉）。

### 涵蓋範圍

| 測試檔 | 驗證什麼 |
|---|---|
| `ClubScopingTests` | 跨俱樂部球員清單 id 集合互不重疊（2026-09-22 改法，見「已發現、未動手修改的既有落差」第 4 點）、跨俱樂部讀已知 slug 的文章詳情回 404（不是內容）、不存在的俱樂部代碼回 404 |
| `LocalizationFallbackTests` | 語系逐欄位回退（同一筆記錄可以一半英文一半中文）；用 Unicode 範圍判斷中文／英文，不硬編碼種子資料的確切字串 |
| `PagingNormalizationTests` | `page<=0`→1、`pageSize<=0`→端點預設值、超過上限截斷 |
| `SqlInjectionTests` | 惡意 `team` 參數不炸、不回傳資料、資料表安然無恙 |
| `CacheFailOpenTests` | Redis 不可用時，`ClubResolver`＋**五組接了快取的端點**（`clubs`／`players`／`staff`／`news` 列表與單篇／`schedule`）全部仍然成功；`/readyz` 仍回 `ready` 但 `redis` 標示 `degraded`（S0-7d 續作擴大涵蓋範圍，原本只驗證 `players` 一個端點） |
| **`CacheBehaviorTests`（S0-7d 續作新增）** | qualifier 是否真的涵蓋每個會改變結果的參數（`pageSize`／`team`／`category`／`season`／`status`）、同參數兩次結果一致、文章單篇不同 slug 互不污染、**404 不寫入快取**、**用 `IConnectionMultiplexer` 直接核對 key 真的依 club／locale 隔離**（2026-09-22 同上改成 id 集合不重疊）、key 有 TTL 兜底 |
| **`AdminNewsGateClosedTests`（S0-8 原始版本已於 S1 整支重寫）** | ⚠️ **這一列描述的是 S0-8 時的行為，S1 起已不成立**（該機制不再擋 `AdminNews`）。現版驗「未登入回 401（不是 404，路由確實存在）」「格式不正確的權杖回 401」「公開唯讀端點不受影響」，見上方「S1」整節 |
| **`AdminNewsWriteTests`（本輪新增）** | 完整生命週期（建立→改內容→排程→發布→刪除）、分類代碼不存在／中文標題空白回 400、slug 重複回 409、俱樂部範圍（另一俱樂部路由更新／刪除回 404 且本尊不變）、共用內容唯讀（更新／刪除回 403，且後台讀取看得到並標記 `isShared`）、樂觀並行控制（過期 `updatedAt` 回 409 且不是後寫的贏）、三態轉換（已發布不能再排程）、排程時間不在未來回 400、雙語側表（加英文／省略英文清空）、置頂精選超過 3 篇回 409 |
| **`AdminNewsCacheInvalidationTests`（本輪新增）** | 用真正的 `redis-server`（`AdminWriteRedisEnabledApiFixture`）驗證：後台更新已發布文章後，公開 API 立刻反映新標題，不是被 TTL 內的舊快取值擋住 |
| **`ScheduledPublishTests`（S0-7g 新增，3 項）** | 直接呼叫 `ScheduledPublishRunner.PublishDueArticlesAsync`（不等計時器）：排定時間已過的文章掃描後轉為 `published`、`published_at` 不被改寫、公開 API 立刻可見；排定時間未到的文章掃描後仍是 `scheduled`、公開 API 仍 404；連續呼叫兩次不拋例外、第二次不再更動已發布文章的 `updated_at`／`published_at`（冪等）。三支測試都只斷言最終狀態，不斷言「這一次呼叫轉了幾筆」——因為 `AdminWriteApiFixture` 啟動的 `Program` 裡，真正的 `ScheduledPublishBackgroundService` 也在背景跑，兩者互相競速不影響最終正確性，但會讓「這次呼叫剛好轉了幾筆」變得不穩定 |
| **`ImageProcessorTests`（S0-8 新增，純單元測試，不需要任何 fixture）** | 長邊超過 2560px 等比縮小、固定產出 4 個衍生檔（1280／640／320／160 方形縮圖）、全部輸出真的是 WebP、主檔與衍生檔的 EXIF／ICC／IPTC／XMP 全部清除、主檔小於目標尺寸時不放大補齊、接受 PNG／WebP 格式、假副檔名文字檔與損毀 JPEG 檔頭被擋（見 `TestImages.cs`，全部圖片用 ImageSharp 在記憶體現產，不依賴外部檔案，任何機器都能重現） |
| **`AdminNewsCoverBlobCleanupTests`（S0-8 新增，S0-8 修正改寫）** | 圖片上傳共用元件跟 `Features/AdminNews` 實際接線後的行為，改用單一 multipart 請求：建立文章時附封面圖片、五個物件真的寫進儲存體且物件鍵含俱樂部與文章 id；換圖成功後舊的主檔與全部衍生檔被刪除、新的完整保留；`removeCover=true` 清空封面且刪舊物件；沒夾檔案也沒勾選移除時封面維持不變（Keep 語意）；刪除文章後圖片物件一併被刪除 |
| **`AdminNewsCoverUploadTests`（S0-8 修正新增，2026-09-22）** | 🔴 這次修正的核心驗收：格式不支援／空檔案／超過 10MB／俱樂部不存在四種情境透過建立端點觸發，回 400／404 且不留下任何物件；**slug 重複時夾正常圖片**——圖片已上傳成功但建立失敗（409），驗證儲存體物件數量沒有增加（補償交易生效）；**並行衝突時夾正常圖片**——圖片已上傳成功但更新失敗（409），驗證沒有新增物件且舊封面不受影響；同時夾檔案又勾選移除封面回 400 且完全不嘗試上傳 |
| **`BlobImageStorageServiceUploadFailureTests`（S0-8c 新增，2026-09-24，6 項）** | 🔴 不經 HTTP，直接建構 `BlobImageStorageService`，用包住真實 Azurite 容器的假裝飾（`FailAtCallBlobContainerClient`／`FailingBlobClient`，子類化 `BlobContainerClient`／`BlobClient` 的 `virtual` 成員，不需要任何 mocking 套件）在第 N 次 `GetBlobClient` 呼叫注入失敗：`N=1..5`（主檔、三個等比衍生檔、方形縮圖各自失敗一次）驗證原例外原樣拋出、事後五把物件鍵全部不存在；額外一項驗證補償刪除本身也失敗時仍然拋出造成上傳失敗的原例外（不會被清理錯誤蓋掉），且這種雙重失敗下前面真的寫入成功的物件會如預期殘留 |

### 怎麼跑

需要本機既有的 `sqlserver` 容器已啟動且已灌種子資料（同上方「怎麼跑（本機開發）」的前置；
🔴 2026-09-21 起 `mssql-dev` 已併入這個既有容器，不再是獨立服務，見 `deploy/README.md`）：

```bash
docker ps --filter name=sqlserver   # 確認既有容器在跑
./deploy/local-ddl.sh --apply
./db/seed/apply-seed.sh

export CLUB_SQL_CONNECTION_STRING="Server=127.0.0.1,1433;Database=tcrfc_club_dev;User Id=sa;Password=<你的 MSSQL_DEV_SA_PASSWORD>;TrustServerCertificate=True;"

cd apps/api/Tcrfc.Api.Tests
dotnet test
```

🔴 **S0-8 起額外需要 `azurite-blob` 執行檔**（`AdminWriteAzuriteEnabledApiFixture` 用）：
`npm install -g azurite`（macOS／Linux 預設裝在 `/usr/local/bin/azurite-blob`，找不到時可用
`AZURITE_EXECUTABLE_PATH` 環境變數指到實際位置），沿用既有「找不到 `redis-server` 就丟清楚例外」
同一種紀律，不悄悄跳過。⚠️ **這個沙盒環境整套 96 項測試實測約需 3–4 分鐘**（本輪驗收時第一次
懷疑卡死，用 `sample <pid>` 對行程取樣看到主執行緒在 `WaitHandle_WaitOneCore` 才確認只是這台機器
的 `dotnet test` 行程啟動開銷偏高，不是死鎖——耐心等到 `Passed!` 摘要列印出來為止）。

**2026-09-21 驗收**：合併進 `sqlserver` 容器後重跑，30 項全綠（`Passed! - Failed: 0, Passed: 30,
Skipped: 0, Total: 30`），與 S0-7d 的既有結果一致——證明合併容器沒有改變任何行為。

🔴 **資料庫不可用時，測試回報失敗（不是略過、更不會看起來像通過）**：`ApiFixture`／
`RedisUnavailableApiFixture` 的 `IAsyncLifetime.InitializeAsync()` 會先檢查
`CLUB_SQL_CONNECTION_STRING` 是否有設定、能不能連上，連不上就直接丟一個訊息清楚的
`InvalidOperationException`——xUnit 會把使用該 fixture 的每一個測試都回報為 `Failed`
並附上這個訊息（怎麼啟動本機依賴的具體指令）。**這是刻意的選擇，不是引入
`Xunit.SkippableFact` 之類的第三方套件做「略過」**——失敗比略過更難被 CI 儀表板悄悄忽略，
且相依套件數量維持最少；若之後接上 CI 且 CI 固定會提供資料庫，這個決定可以重新評估，
本檔沒有代為決定 CI 一定要用哪一種語意。

### 六個 fixture、六種環境設定

- `ApiFixture`：`REDIS_HOST` 清空（強制走 `NoOpQueryCache`），只驗證主站庫相關行為。
  **本輪起也是「開發模式開關關閉」狀態的代表**（不設 `ENABLE_UNSAFE_DEV_WRITES`），
  `AdminNewsGateClosedTests` 就是刻意用這個 fixture，驗證「大多數測試在關閉狀態下跑」。
- `RedisUnavailableApiFixture`：`REDIS_HOST=127.0.0.1`＋一個用 `TcpListener` 現抓現放的
  本機閒置連接埠（不是猜一個「應該沒人用」的埠號），讓 `RedisQueryCache` 真的拿到連線失敗，
  專門驗證 fail-open。
- **`RedisEnabledApiFixture`（S0-7d 續作新增）**：真的啟動一個 `redis-server` 子行程（同樣用
  `TcpListener` 現抓現放的埠號，然後在那個埠上啟動 `redis-server --requirepass ... --save ""`），
  專門驗證「Redis 正常運作時」的行為——key 命名、qualifier、跨俱樂部與跨語系隔離、TTL。
  找不到 `redis-server` 執行檔時（本機沒裝）會直接丟清楚的例外（含 `brew install redis` 的指示），
  跟資料庫不可用時的行為一致，不會悄悄跳過。可用 `REDIS_SERVER_PATH` 環境變數覆寫執行檔位置。
  ⚠️ `--save ""` 是必要的——沒有它，`redis-server` 收到終止訊號時會在**目前工作目錄**寫一個
  `dump.rdb`（S0-7d 第一階段手動驗證時真的踩過這個坑，見 `docs/18-work-errors.md` E-27 附近的說明），
  用測試自動化跑的話這個風險更大（工作目錄通常就是專案根目錄）。
- **`AdminWriteApiFixture`（本輪新增）**：`ENABLE_UNSAFE_DEV_WRITES=true` ＋ `REDIS_HOST` 清空，
  是唯一開啟寫入端點但不驗證快取行為的 fixture，`AdminNewsWriteTests` 用這個。
- **`AdminWriteRedisEnabledApiFixture`（本輪新增）**：`ENABLE_UNSAFE_DEV_WRITES=true` ＋真正的
  `redis-server` 子行程（跟 `RedisEnabledApiFixture` 同樣的啟動方式），專門驗證「寫入後公開快取
  真的失效」這件事，`AdminNewsCacheInvalidationTests` 用這個。
- **`AdminWriteAzuriteEnabledApiFixture`（S0-8 新增）**：`ENABLE_UNSAFE_DEV_WRITES=true` ＋真正的
  `azurite-blob` 子行程（同樣用 `TcpListener` 現抓現放的埠號；`--location` 指到
  `Directory.CreateTempSubdirectory()` 產生的暫存目錄，避免像 `redis-server` 那樣有寫入目前
  工作目錄的風險）＋`REDIS_HOST` 清空。`AdminNewsCoverBlobCleanupTests`／`AdminNewsCoverUploadTests`
  用這個，並透過 `InspectorContainer`（獨立的 `BlobContainerClient`）直接核對物件是否存在，
  不透過應用程式自己的連線斷言。找不到 `azurite-blob` 執行檔時同樣丟清楚例外
  （可用 `AZURITE_EXECUTABLE_PATH` 覆寫），加了 `--skipApiVersionCheck`（見「本機開發：Azurite」）。

六者都用**行程環境變數**（`Environment.SetEnvironmentVariable`）而不是
`WebApplicationFactory.ConfigureWebHost` 的 `ConfigureAppConfiguration` 來傳遞設定——
因為 `Program.cs` 在 `builder.Build()` 之前就會讀 `REDIS_HOST` 決定要不要注入
`RedisQueryCache`，那段程式碼跑在測試主機的攔截點之前。因此測試組件用
`[assembly: CollectionBehavior(DisableTestParallelization = true)]` 停用平行化，
避免不同 fixture 的環境變數互相污染（測試數量少，停用平行化的時間成本可以接受）。

> 🔴 **本輪實際踩到的坑，記錄下來給下一個加 fixture 的人**：`DisableTestParallelization`
> 只保證**測試方法**不平行跑，**不保證不同 `[Collection]` 的 fixture 之間 `InitializeAsync`
> 的執行順序**。加入 `AdminWriteApiFixture`（會把 `ENABLE_UNSAFE_DEV_WRITES` 設成 `"true"`）之後，
> `AdminNewsGateClosedTests`（用 `ApiFixture`，預期這個變數是關閉的）開始**偶發**失敗——
> 不是每次跑都會中，因為環境變數是行程全域的，`ApiFixture` 若剛好在 `AdminWriteApiFixture`
> **之後**才呼叫 `_ = Server`，就會在變數已經被設成 `"true"` 的狀態下建立測試主機。
> **修法**：每一個「這個開關應該是關的」fixture（`ApiFixture`／`RedisUnavailableApiFixture`／
> `RedisEnabledApiFixture`）都在自己的 `InitializeAsync` 明確把 `ENABLE_UNSAFE_DEV_WRITES`
> 設成 `null`——不依賴「反正我沒設定過就是關閉」，跟既有 `REDIS_HOST` 的處理方式一致（同一段
> 已經在做的事，本輪只是多了一個新變數要比照辦理）。**下一次新增任何會動到行程環境變數的
> fixture，都要檢查現有的「應該關閉」fixture 有沒有明確清掉那個變數**，不要假設「別的 fixture
> 不會設定就不用管」。已用連續 4 次 `dotnet test` 重跑驗證修好（4 次全綠，`48/48`）。

### Docker 映像檔不受影響

`Tcrfc.Api.Tests` 是 `apps/api/` 目錄下的獨立子專案（沒有 `.sln`）。`Tcrfc.Api.csproj`
已明確 `<Compile Remove="Tcrfc.Api.Tests/**/*.cs" />`（否則預設的遞迴萬用字元會把測試原始碼
一起編進主專案，因為兩個 .csproj 在同一個目錄樹下、沒有解決方案檔幫忙切開建置範圍）；
`apps/api/.dockerignore` 也排除了這個目錄，`docker build -t x apps/api` 不受影響
（已實跑驗證，見「S0-7d 驗收紀錄」與本輪「驗收紀錄」第 3 點——本輪特別因為新增了 EF Core
套件與 `Data/Migrations/`／`Data/EfEntities/` 兩個新目錄，額外確認過 `docker build` 仍然成功，
不是只跑 `dotnet build` 就假設容器也沒問題，見 `docs/18-work-errors.md` E-35 的教訓）。

### 本輪驗收紀錄（slug 保留字驗證，2026-09-22）

環境：本機既有 `sqlserver` 容器（同上），`tcrfc_club_dev`，套用前 `articles` **83 筆**。

```
$ dotnet build   # apps/api 與 apps/api/Tcrfc.Api.Tests
建置成功。0 個警告 0 個錯誤

$ dotnet test    # CLUB_SQL_CONNECTION_STRING 指向本機 tcrfc_club_dev
已通過! - 失敗: 0，通過: 78，略過: 0，總計: 78
# 78 = 既有 48（不變，見「開發過程踩的坑」段落末的「4 次全綠 48/48」）
#    ＋ 本輪新增 30 項（9 個保留字逐一 × Theory ＋ 4 種大小寫變形 ＋ 10 種危險格式 ＋
#      建立／更新兩條路徑各自的保留字與格式測試 ＋ 3 個合法 slug 不受誤擋的驗證）
```

⚠️ **過程中發現並修正一個既有測試的隱性缺陷**：`AdminNewsWriteTests.UniqueSlug()` 原本用
`[CallerMemberName]` 把測試方法名稱（中文，含底線）直接嵌進網址名稱（例如
`admin-write-test-完整生命週期_建立草稿到刪除-xxxx`）。這在本輪新增格式驗證之前沒有任何影響，
新增之後**這些測試會被自己送出的 slug 卡在 400**——不是新規則寫錯，是舊的測試資料產生器本來就
沒有產出合法的網址名稱，只是在格式驗證出現之前，沒有任何東西會檢查它合不合法。已改成純 ASCII
亂數字串，不再嵌入呼叫端方法名稱（`apps/api/Tcrfc.Api.Tests/AdminNewsWriteTests.cs`）。

實際打執行中的 API（`ASPNETCORE_ENVIRONMENT=Development` ＋ `ENABLE_UNSAFE_DEV_WRITES=true`）：

```
# 保留字 club（建立）
$ curl -X POST /api/v1/admin/tcrfc/news -d '{"slug":"club",...}'
400 {"detail":"網址名稱「club」是系統保留給分類頁面使用的名稱，...請換一個能代表這篇文章內容的網址名稱..."}

# 保留字大小寫變形 Club（建立，被格式規則擋下，結果一致是 400）
$ curl -X POST /api/v1/admin/tcrfc/news -d '{"slug":"Club",...}'
400 {"detail":"網址名稱「Club」格式不正確：只能使用小寫英文字母、數字與連字號（-）組成..."}

# 保留字 media（建立）
400 {"detail":"網址名稱「media」是系統保留給分類頁面使用的名稱..."}

# 含斜線 has/slash（建立）
400 {"detail":"網址名稱「has/slash」格式不正確...（例如大寫字母、空白、斜線、句點都不能出現）"}

# 純數字 20260922（建立）
400 {"detail":"網址名稱「20260922」不能整段只有數字，請加入能代表文章內容的文字..."}

# 合法網址名稱（建立）
$ curl -X POST ... -d '{"slug":"curl-manual-verify-1790066402",...}'
201 {"id":"822f5299-...","slug":"curl-manual-verify-1790066402",...}
$ curl -X DELETE .../822f5299-...?expectedUpdatedAt=...
204   # 清乾淨

# 更新路徑改成保留字 international
400 {"detail":"網址名稱「international」是系統保留給分類頁面使用的名稱..."}
# 確認原文章沒有被改到：slug／title 皆原封不動
```

套用後 `articles` 仍是 **83 筆**（手動驗收建立的兩筆測試資料都已用 `DELETE` 清掉，實測核對過）；
對 83 筆逐一核對，**0 筆違反保留字清單、0 筆違反新格式規則**。

---

### S0-8 驗收紀錄（圖片上傳共用元件，2026-09-22）

🔴🔴🔴 **這份紀錄是原始（兩段式）設計的驗收，如實保留、不回頭改寫歷史**——那個設計已經因為
違反規劃書「選檔不上傳、儲存才上傳」被判定為規格違反並修正，見本檔最上方「本輪修正（上傳時機
違反規劃書）」與下方「**S0-8 修正驗收紀錄（單一請求，2026-09-22）**」。這份舊紀錄裡提到的
`POST .../uploads/images/...` 端點**已經整支移除**，照著這裡的 `curl` 指令打會得到 404——
要驗證現在的行為請看下面的新紀錄。

環境：本機既有 `sqlserver` 容器（`tcrfc_club_dev`），本機 `npx azurite --skipApiVersionCheck`
（連線字串 `BlobEndpoint=http://127.0.0.1:11000/devstoreaccount1`）。

#### 1. 建置

```
$ dotnet build   # apps/api
建置成功。0 個警告 0 個錯誤

$ docker build -t x apps/api
...
naming to docker.io/library/tcrfc-api-s08:latest done   # 成功
```

#### 2. 手動 curl 驗收（真實檔案，非模擬）

用 Python Pillow＋piexif 現產一張 **3000×2000、帶 EXIF（相機廠牌、方向標記 6＝需要旋轉）與
GPS 座標（24°9'0"N 120°41'0"E）**的 JPEG，另用 macOS `sips -s format heic` 從同一張圖轉出
**真正的 HEIC 檔案**（不是模擬，是系統內建工具真的轉檔）：

```
# 正常 JPEG（3000x2000，有 GPS/EXIF，orientation=6）
$ curl -X POST .../uploads/images/articles/<id>/cover -F "file=@big_with_gps.jpg"
{"key":"tcrfc/articles/<id>/cover/035c0dbd....webp","width":1707,"height":2560,"sizeBytes":7464}
# 1707x2560：3000x2000 因 orientation=6 轉正後變 2000x3000（縱向），長邊 3000 超過 2560 上限，
# 等比縮小為 2560/3000*2000=1706.67→1707，與 ImageProcessorTests 的斷言一致

# 正常 PNG（500x400）
$ curl -X POST .../uploads/images/articles/<id>/cover -F "file=@small.png"
{"key":"...webp","width":500,"height":400,"sizeBytes":432}

# 假副檔名（純文字改名 .jpg）
$ curl -X POST .../uploads/images/articles/<id>/cover -F "file=@fake.jpg"
400 {"detail":"圖片格式不支援，請上傳 JPG、PNG 或 WebP 格式的圖片（不支援 HEIC）。"}

# 真正的 HEIC 檔案（macOS sips 轉出）
$ curl -X POST .../uploads/images/articles/<id>/cover -F "file=@real.heic"
400 {"detail":"圖片格式不支援，請上傳 JPG、PNG 或 WebP 格式的圖片（不支援 HEIC）。"}

# 不支援的欄位插槽
$ curl -X POST .../uploads/images/players/<id>/photo -F "file=@small.png"
400 {"detail":"不支援的圖片欄位「players.photo」。"}
```

**用 Python `azure-storage-blob` SDK 直接查 Azurite 容器內容**（不透過應用程式自己的連線），
對 JPEG 那次上傳的五個物件逐一下載並用 Pillow 檢查：

```
main  size_bytes=7464  1707x2560  WEBP  exif_tags=0
1280  size_bytes=1944   854x1280  WEBP  exif_tags=0
640   size_bytes=546    427x640   WEBP  exif_tags=0
320   size_bytes=200    213x320   WEBP  exif_tags=0
thumb size_bytes=122    160x160   WEBP  exif_tags=0
```

**五個物件全部是真正的 WebP、尺寸比例正確、`exif_tags=0`（含 GPS 在內的全部中繼資料已清除）**。

其餘實測（見上方「圖片上傳共用元件」整節引用的行為）：

- **超過 10MB 上限**（11MB 測試檔）：`400`，友善訊息「圖片檔案太大（上限 10 MB）...」。
- **超過約 11MB**（31MB 測試檔）：`413`（Kestrel 平台層級擋下，非本服務 JSON 格式，見「Kestrel
  請求主體上限」）。
- **空檔案**：`400`，「沒有收到圖片檔案，請重新選擇圖片。」
- **不存在的俱樂部**：`404`。
- **跨俱樂部鍵隔離**：`tcrfc` 與 `bw` 對同一個 `entityId` 各自上傳，物件鍵前綴分別是
  `tcrfc/articles/.../cover/...` 與 `bw/articles/.../cover/...`，互不覆蓋。
- **換圖成功才刪舊物件、主檔與衍生檔一起刪**：對一篇真實建立的文章（`AdminNews` API）上傳
  封面圖 A、`PUT` 存入 `coverKey`，確認 A 的五個物件都在 Azurite 裡；再上傳封面圖 B、`PUT`
  更新 `coverKey`，確認 **A 的五個物件全部消失，B 的五個物件完整保留**。
- **刪除文章一併刪除圖片物件**：`DELETE` 該文章後，B 的五個物件也全部消失。
- **開發模式開關關閉時上傳端點回 404**：確認跟 `Features/AdminNews` 同一套機制。

#### 3. 自動化測試

```
$ cd apps/api/Tcrfc.Api.Tests && dotnet test
已通過! - 失敗: 0，通過: 96，略過: 0，總計: 96，持續時間: 3 m 50 s
# 96 = 既有 78（零回歸）＋ 本輪新增 18（ImageProcessorTests 9、UploadImagesEndpointTests 6、
#      UploadImagesGateClosedTests 1、AdminNewsCoverBlobCleanupTests 2）
```

---

### S0-8 修正驗收紀錄（上傳時機改單一請求，2026-09-22）

環境跟上面一樣：本機既有 `sqlserver` 容器（`tcrfc_club_dev`，`127.0.0.1,1433`）、本機
`azurite-blob`（`npm install -g azurite` 裝的執行檔，測試 fixture 自動啟動子行程，見
`Fixtures/AdminWriteAzuriteEnabledApiFixture.cs`）。

#### 1. 建置

```
$ cd apps/api && dotnet build
建置成功。0 個警告 0 個錯誤

$ cd apps/api && docker build -t tcrfc-api-test-build:s0-8fix .
...
naming to docker.io/library/tcrfc-api-test-build:s0-8fix done   # 成功
```

#### 2. 自動化測試（真實 HTTP 管線＋真實 SQL Server＋真實 Azurite，不 mock）

```
$ cd apps/api/Tcrfc.Api.Tests && dotnet test
已通過! - 失敗: 0，通過: 99，略過: 0，總計: 99，持續時間: 29 s
```

這一次跑就是「實跑驗證」本身，不是另外用 `curl` 模擬一遍：`AdminNewsCoverBlobCleanupTests`／
`AdminNewsCoverUploadTests` 兩支測試檔用真正的 `WebApplicationFactory<Program>`＋真正的
`azurite-blob`＋真正的 SQL Server 走了 CLAUDE.md 任務指示要求的全部四個實跑情境：

- **正常建立帶圖**：`建立文章時附封面圖片_五個物件都真的寫進儲存體_物件鍵含俱樂部與文章id`——
  真的建立一篇文章、真的夾一張帶 EXIF／GPS 的 JPEG，確認回應的 `coverKey` 前綴是
  `tcrfc/articles/<id>/cover/`、五個物件（主檔＋1280／640／320／160）真的存在於 Azurite。
- **更新換圖（舊物件被清）**：`換圖成功後_舊的主檔與全部衍生檔被刪除_新的完整保留`——PUT 夾一張
  新圖，確認新圖先上傳成功、資料列更新成功後舊圖的五個物件才被刪除，新圖五個物件完整保留。
  另外新增 `更新時勾選移除封面圖片且不夾檔案_封面清空_舊物件被刪除`（`removeCover=true`）與
  `更新時沒有夾檔案也沒有勾選移除_封面維持不變_物件不受影響`（Keep 語意），完整涵蓋封面圖片
  三態，不只是「換圖」這一種情境。
- **🔴 中途放棄不留物件**：規劃書「離開或取消表單不留下任何檔案」在單一請求契約下的天然結果是
  「使用者不按儲存＝從來沒有這次請求」，不需要（也不可能）用後端測試模擬「使用者按了取消」這個
  瀏覽器端動作。真正需要自動化釘住的是「請求送出了、圖片已經上傳成功，但後面的驗證失敗」這個
  唯一可能留下孤兒物件的情境，這支測試檔專門針對這個情境：
  - `建立文章_網址名稱重複但夾了正常圖片_圖片已上傳成功但建立失敗_回滾不留孤兒物件`：故意用
    已存在的 slug 建立第二篇文章、夾一張完全正常的圖片，伺服器端先上傳成功、repository 才發現
    slug 衝突丟 409，斷言**儲存體物件總數在請求前後完全相同**（沒有任何孤兒物件殘留）。
  - `更新文章_並行衝突但夾了正常圖片_圖片已上傳成功但更新失敗_回滾不留孤兒物件_舊封面圖片不受影響`：
    故意用過期的 `expectedUpdatedAt` 更新、夾一張新圖，伺服器端先上傳新圖成功、
    樂觀並行檢查才發現版本不對丟 409，斷言物件總數不變**且**舊封面的物件維持存在（沒有被誤刪）。
  - `建立文章_夾假副檔名文字檔_回400_不建立文章_不留下任何物件`／`夾空檔案`／`夾超過10MB的檔案`／
    `不存在的俱樂部代碼`：四種在碰到資料庫之前就會失敗的情境，逐一驗證物件總數不變。
  - `更新文章_同時夾檔案又勾選移除封面_回400_不嘗試上傳`：驗證這個矛盾請求連一次上傳嘗試都不會有。
- **寫入失敗不留半套**：跟上面「中途放棄不留物件」是同一組測試——「半套」在這個情境下指的正是
  「圖寫進去了但資料列沒更新」，兩支回滾測試已經涵蓋建立與更新兩條路徑。

**99 = 既有 78（零回歸，`AdminNewsWriteTests`／`AdminNewsSlugPolicyTests`／
`AdminNewsCacheInvalidationTests` 三支既有測試檔改用 multipart 呼叫，斷言強度不變或提高，
見 `AdminArticleMultipart.cs`）＋ `ImageProcessorTests` 9（不受影響）＋
`AdminNewsCoverBlobCleanupTests` 5（原 2 支＋新增 3 支涵蓋 `removeCover`／Keep 語意）＋
`AdminNewsCoverUploadTests` 7（全新，取代刪掉的 `UploadImagesEndpointTests` 6＋
`UploadImagesGateClosedTests` 1，並新增兩支回滾測試）**。

---

## S0-7g 驗收紀錄（排程發布自動轉為 published，2026-09-24）

### 1. `dotnet test` 全過，既有測試不回歸

```
$ dotnet test Tcrfc.Api.Tests/Tcrfc.Api.Tests.csproj
已通過! - 失敗: 0，通過: 135，略過: 0，總計: 135，持續時間: 41 s
```

新增的 3 項（`ScheduledPublishTests`）單獨跑：

```
$ dotnet test Tcrfc.Api.Tests/Tcrfc.Api.Tests.csproj --filter "FullyQualifiedName~ScheduledPublishTests"
已通過 Tcrfc.Api.Tests.ScheduledPublishTests.重複執行不重複發布也不拋例外 [2 s]
已通過 Tcrfc.Api.Tests.ScheduledPublishTests.排程時間已過_背景掃描後狀態轉為published且公開API可見 [63 ms]
已通過 Tcrfc.Api.Tests.ScheduledPublishTests.排程時間未到_掃描後狀態仍是scheduled且公開API仍404 [44 ms]
通過: 3
```

### 2. curl 手動驗收（本機真實 `dotnet run`，`SCHEDULED_PUBLISH_INTERVAL_SECONDS=5` 縮短輪詢間隔方便觀察）

直接 SQL 插入一篇 `status='scheduled'`、`published_at` 為 10 秒前的文章（後台 `/schedule` 端點
本身會擋「排程時間必須晚於現在」，沒辦法用真正的 API 產生「排定時間已過去」這個狀態，跟
`ScheduledPublishTests` 用同一招）：

```
=== 立即查詢 ===
HTTP 200   # 這一輪 curl 前，背景服務已經先跑過至少一輪（間隔設 5 秒），已經轉過了

=== 資料庫實際內容 ===
status: published
published_at: 2026-09-24 01:47:04.292   ← 維持原本排定的時間，沒有被改寫
updated_at:   2026-09-24 01:47:15.154   ← 掃描把它轉掉的時間

=== 公開 API 回應 ===
{
  "slug": "curl-manual-verify-s0-7g",
  "publishedAt": "2026-09-24T01:47:04.292",
  "title": "curl 手動驗收：排程發布",
  ...
}
```

負向案例（排定時間在 30 分鐘後，確認**不會**被提前轉掉）：

```
=== 插入後立即查詢 ===
HTTP 404

=== 等待兩輪以上（8 秒，輪詢間隔 5 秒）後再查詢 ===
HTTP 404   # 仍然未到排定時間，狀態正確維持 scheduled
```

兩筆測資驗收後已用 SQL 清除，未留在 `tcrfc_club_dev`。

### 3. 時區核對

`published_at`／`updated_at` 全部是 `datetime2(3)` 存 UTC（`docs/12` §1.1 既有定案），
`ScheduledPublishRunner` 的 SQL 用 `SYSUTCDATETIME()` 比較，跟既有 `ArticlesRepository`
公開讀取查詢的 `a.published_at <= SYSUTCDATETIME()` 是同一個時間基準，沒有另外引入
`GETDATE()`（伺服器在地時間）或應用層 `DateTime.Now`（行程在地時間）混用的風險。
邊界情況由 `ScheduledPublishTests` 的兩個正／負案例覆蓋（`-5` 秒必轉、`+10`／`+30` 分鐘必不轉），
沒有另外測試「剛好等於現在」這種奈秒級的臨界點——SQL 執行本身就有毫秒級的時間流逝，
測試機器上量測「剛好相等」既不穩定也沒有額外的業務意義。

---

## 相關文件

- [`docs/12-database-schema.md`](../../docs/12-database-schema.md)／[`12a`](../../docs/12a-database-erd.md)／[`12b`](../../docs/12b-database-tables.md)／[`12c`](../../docs/12c-i18n-tables.md) — 資料表設計、權限模型、受限欄位、i18n 側表
- [`docs/14-invariants.md`](../../docs/14-invariants.md) — `club_id` 維度、跨庫 JOIN 陷阱、不得讀快取清單
- [`docs/17-deployment.md`](../../docs/17-deployment.md) §0／§1／§4／§6 — 技術選型、容器佈局、快取策略、本機開發資料庫
- [`docs/18-work-errors.md`](../../docs/18-work-errors.md) E-19／E-20／E-35／E-38 — 本次與前次開發踩的坑
  （本輪的「兩個測試假設過期」與「programs 撞 Program」兩件事尚未寫進這份文件，見上方「已發現、
  未動手修改的既有落差」第 4 點——本輪邊界不含 `docs/`，麻煩使用者或下一位轉記）
- [`docs/20-cicd.md`](../../docs/20-cicd.md) §5 — EF Core 與手寫 DDL 怎麼接軌（本輪執行的依據）
- [`docs/03-admin-spec.md`](../../docs/03-admin-spec.md) B2 — 新聞模組規格（CRUD、分類、標籤、排程發布、置頂精選）
- [`docs/12d-field-audit.md`](../../docs/12d-field-audit.md) §11 — 圖片欄位組缺 `_width`／`_height`／`_alt` 的資料庫綱要落差（S0-8 本輪再次確認，未動手加欄位）
- [`db/seed/README.md`](../../db/seed/README.md) — 本機資料庫怎麼連、種了哪些資料、已知落差
- [`apps/web/README.md`](../web/README.md) — 這支 API 唯讀端點的呼叫端（Nuxt 前台骨架）
- [`apps/admin/src/views/news/`](../admin/src/views/news/) — 這支 API 後台端點要餵的畫面（✅ 已接上，2026-09-22）
- [`apps/admin/src/`](../admin/src/) 的 `ImageUploader.vue` — S0-8 圖片上傳共用元件的契約消費端（✅ **已接上，2026-09-22**，走單一 multipart 契約「儲存才上傳」，契約見本檔「圖片上傳共用元件」整節）
