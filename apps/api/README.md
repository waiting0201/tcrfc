# apps/api — api（單一 .NET 行程，唯讀讀取 API）

> 🔴 **2026-09-30：本機資料庫已合併為 `tcrfc_club`（網站）與 `tcrfc_charity`（慈善）兩個。**
> 本檔下方**歷史紀錄**（各輪驗收、migration 套用紀錄）提到的 `tcrfc_club_dev`、`tcrfc_club_test`
> 是當時的名稱，現在都是 `tcrfc_club`，不逐條改寫以保留當時的證據；**操作步驟以「怎麼跑」與
> 「直接用 dotnet 跑」為準**。

**S0-7b（2026-09-21，`backend-engineer`）**：讓 Nuxt 前台（[`apps/web`](../web/README.md)）能打真實資料庫，
建立球員、教練與職員、新聞、賽程與賽果、俱樂部主檔五組**唯讀** GET 端點。

**S0-7d（2026-09-21，`backend-engineer`）**：補齊 S0-7b 刻意縮減的三個缺口——① `/readyz` 加上慈善庫與
Redis 檢查 ② `Caching/IQueryCache.cs` 接縫接上真正的 Redis 實作 ③ 建立 `Tcrfc.Api.Tests` 自動化測試專案
（S0-7b 為止零測試）。細節見下方各段落與「S0-7d 驗收紀錄」。

~~🔴 不含：登入與權限~~ ← **已於 S1（下方）補上**。仍不含：商店與金流、除新聞以外的後台模組寫入端點、
慈善捐款平台的業務功能（S0-7d 對慈善庫只做「開一條連線探活」，不做任何查詢，見下方「`/readyz` 範圍」）。
**🔴 2026-10-01 更新：慈善捐款平台的後端 API（CH-2 公開端點＋CH-3 獨立後台）已補上，見文末「慈善 CH-2／CH-3」**——實作在 `CharityPlatform/`，與主站 `Features/` 完全分開。
**🔴 2026-10-02 更新：慈善後端 CH-3 補完＋CH-4 帳務與報表（結算／對帳／憑證管理／報表）＋CH-5（徵信名單／成果回顧／站台設定）已補上，見「慈善 CH-3 補完／CH-4 帳務與報表／CH-5 延伸」**——畫面由前端另做。

---

## 原生 json 測試（2026-10-01，`docs/18` `E-111`）

正式環境（Azure SQL）的 `json` 欄位是**原生 json 型別，只收 JSON 物件或陣列**；本機預設的 SQL Server 2022 容器沒有原生 json，`deploy/local-ddl.sh` 把它轉成 `nvarchar(max)`（什麼都收），所以**只在預設的 `tcrfc_club` 上跑 `dotnet test` 看不到純量寫入錯誤**。這支腳本用 `mcr.microsoft.com/mssql/server:2025-latest`（原生 json）接**原樣** `db/*.sql`（不經轉換）跑整套測試：

```bash
apps/api/scripts/native-json-test.sh up       # 建獨立容器（127.0.0.1:14335）、建兩庫（原樣 DDL）、灌既有種子；已存在就重用
apps/api/scripts/native-json-test.sh test     # 在該容器上跑 dotnet test（可附參數，例如 --filter "FullyQualifiedName~JsonColumn"）
apps/api/scripts/native-json-test.sh reset    # 測試弄髒資料庫時：丟掉兩庫重建
apps/api/scripts/native-json-test.sh down     # 移除容器
apps/api/scripts/native-json-test.sh sql "SELECT 1" [資料庫]   # 除錯用
```

- **與開發用的 `sqlserver` 容器完全隔離**：獨立容器名（`tcrfc-mssql2025-nativejson`）、獨立埠、隨機 SA 密碼（存 `~/.cache/tcrfc-native-json/sa-password`，權限 600，不進版控、不印出）；只用既有的 `db/seed/apply-seed.sh`／`apply-charity-seed.sh`（以環境變數指向該容器），不碰任何既有庫。
- 結果（2026-10-01，本輪修正後）：**2025 原生 json 1,046 項全綠；2022 本機 1,046 項全綠**。修正前同一套在 2025 上 1,016／1,019（3 項失敗：2 項營業時間字串純量、1 項 `MembershipOrderTests` 依賴手改種子）。
- **守門規則**：寫入 json 欄位的內容必須是物件或陣列（`Common/JsonColumn.cs`，`docs/14`）；自由文字包成 `{"text":"…"}`（營業時間）；輸入驗證不符回 400（訊息為日常中文）。12 個 json 欄位與逐一處置見 [`docs/20`](../../docs/20-cicd.md) §5。
- **新聞內文 `articles_i18n.body`**：後台送純文字，寫入時包成 `{"text":"…"}`（物件／陣列原樣保存，為區塊編輯器保留），讀取（後台詳情、公開單篇 `bodyJson`）一律還原成原文字，對外契約不變、前端不用改（`JsonColumn.NormalizeTextOrStructured`／`UnwrapText`；`ArticleBodyJsonColumnTests`）。
- CI 目前仍接 2022；建議改接 2025 映像檔並灌原樣 DDL（評估見 `docs/20` §5，未改 CI）。

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
🔵 **2026-10-01 起有啟動檢查（E-109，`Common/DataProtectionKeyRing.cs`）**：`Production` 下該變數未設、空白、目錄不存在或
不可寫（實際寫入並刪除探測檔）一律**啟動失敗**，訊息指向 `infra/README.md` §4.3「金鑰環」；非 Production 維持沒設就不持久化。

**🔴 2026-09-30 使用者裁決：改密與 2FA 不再強制（正式環境亦同）。** `Security/AdminAccountGate.cs`
只檢查帳號存在且 `status=active`，**不再**因 `must_change_password=true` 或 `two_factor_enabled=false`
回 403。變更密碼與 2FA 設定／停用保留為選用功能（帳號安全頁仍可用）。**已啟用 2FA 的帳號登入時仍須
輸入驗證碼**：`/login` 密碼正確但沒帶 `totpCode` 回 `status: "totp_required"`，流程不變。
帳號鎖定、IP 限流、登入時序拉平、權杖設計均未動。

**給後台前端的接法**：`/login` 成功回應仍帶 `mustChangePassword`／`twoFactorEnabled`，欄位與型別
不變，但**只是提示**——前端不可再依它強制跳轉到改密／2FA 頁；要提示時（例如管理員代為重設他人密碼後
`must_change_password=true`）顯示可略過的橫幅即可。**新建帳號預設 `must_change_password=false`**（`DEFAULT 0`，migration
`AlignAdminUserMustChangePasswordDefault`）；只有 `POST /accounts/{id}/reset-password` 會把它設為 `true`（提示、不強制）。所有後台端點在這兩個旗標為任何值時都可用。

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
3. **帳號目前狀態**——重查 `status`／`is_super_admin`（2026-09-30 起不再查 `must_change_password`／
   `two_factor_enabled`；不信任 JWT claims，理由見上）；`is_super_admin=true` 跳過下一步
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

以上四段輸出對應的自動化測試見 `Tcrfc.Api.Tests/AdminClubAuthorizerTests.cs`（連同「未改密且未啟用 2FA 可直接存取」
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
| ~~未完成強制前提（改密碼／2FA）一律擋下~~ 2026-09-30 已取消；未改密且未啟用 2FA 可直接存取 | `AdminClubAuthorizerTests.額外情境_未改密且未啟用2FA的帳號_可直接存取俱樂部範圍端點與me` |
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
| `sa@system.local` | `Admin@123` | `system_admin` | — | `must_change_password=0`（2026-09-30 起）／`2FA 未啟用` | **真正的種子超管**（docs/12b §7.6 明文的帳號），可直接登入使用（不再強制改密／2FA） |
| `clean.login@tcrfc.test` | `SuperAdmin@123` | `system_admin` | — | 可直接登入 | 唯一能走完整 `/login` HTTP 往返的「已就緒」帳號（見下方原因） |
| `super.admin@tcrfc.test` | `SuperAdmin@123` | `system_admin` | — | `2FA 未啟用`（2026-09-30 起） | 可直接登入；`TestAdminTokens` 亦可直接簽權杖 |
| `content.editor@tcrfc.test` | `ContentEditor@123` | `content_editor` | `tcrfc` | 同上 | 大多數 AdminNews 測試預設用這個 |
| `viewer@tcrfc.test` | `Viewer@123` | `viewer` | `tcrfc` | 同上 | 唯讀角色測試 |
| `partner.club@tcrfc.test` | `PartnerClub@123` | `partner_club_manager`（`own_clubs`） | 僅 `bw` | 同上 | 情境二／四 |
| `expired.grant@tcrfc.test` | `ContentEditor@123` | `content_editor` | `tcrfc`（**已過期**） | 同上 | 情境三 |
| `fresh.setup@tcrfc.test` | `Admin@123` | `viewer` | `tcrfc` | `must_change_password=1`（**唯一保留為 1 的種子帳號**，測試依賴它驗證改密碼後旗標清除），`2FA 未啟用` | 完整 2FA 設定流程測試（`AdminAuthTests` 用完會重設回本狀態） |
| `lockout.test@tcrfc.test` | `Viewer@123` | `viewer` | `tcrfc` | 同上 | 連續失敗鎖定測試專用（避免與其他測試共用帳號互相污染） |
| `academy.login@tcrfc.test` | `ContentEditor@123` | `academy_program` | 僅 `bw` | 可直接登入 | 學院／課程管理的端對端實走帳號（S1-8） |
| `customer.service.login@tcrfc.test` | `ContentEditor@123` | `customer_service_admin` | `tcrfc` | 可直接登入 | 客服／行政的端對端實走帳號（S1-11 補） |
| `pr.media.login@tcrfc.test` | `ContentEditor@123` | `pr_media` | `tcrfc` | 可直接登入 | 公關／媒體的端對端實走帳號（S1-11 補） |
| `business.sponsorship.login@tcrfc.test` | `ContentEditor@123` | `business_sponsorship` | `tcrfc` | 可直接登入 | 商務／贊助的端對端實走帳號（S1-11 補） |
| `content.editor.login@tcrfc.test` | `ContentEditor@123` | `content_editor` | `tcrfc` | 可直接登入 | 內容編輯的端對端實走帳號（S1-12 補，見「S1-12」段落④） |

⚠️ **2026-09-30 種子帳號處置**：原本大多數測試帳號的 `two_factor_enabled` 種為 `1` 但沒有真實密鑰
（`two_factor_secret_encrypted` 為 NULL），唯一目的是繞過強制 2FA 閘門。閘門移除後，這種帳號反而無法
`/login`（已啟用 2FA 必須輸入驗證碼，密鑰卻不存在）。因此 `generate-club-seed-sql.py` 的
`ADMIN_USERS` 已全部改為 `two_factor_enabled=0`——**表格中所有帳號現在都可直接登入**，整合測試用的
`TestAdminTokens` 直接簽權杖不受影響。已存在於資料庫的帳號不會被 INSERT 冪等策略更新，需執行
`./db/seed/reset-admin-accounts.sh`（`--reset-admin-accounts` 模式）才會套用。想測 `totp_required`
流程時，走 `AdminAuthTests.完整2FA設定流程...` 那種「先實際設定 2FA」的作法。

🔴 **「-login」變體帳號的既有慣例（S1-8 起）**：每當一輪新增角色的測試帳號會撞到上述限制、且
該角色需要被無頭瀏覽器／端對端驗收真的登入過一次時，另開一個 `two_factor_enabled=0`、角色與
俱樂部授權逐一對應本尊的孿生帳號，命名規則是本尊帳號 local-part 加上 `.login`（例：
`academy.manager@tcrfc.test` → `academy.login@tcrfc.test`）。S1-11 依此規則補上
`customer.service.login@tcrfc.test`／`pr.media.login@tcrfc.test`／
`business.sponsorship.login@tcrfc.test` 三個（見下方「S1-11」段落）；S1-12 驗收退回後補上
`content.editor.login@tcrfc.test`（見「S1-12」段落④，讓「內容編輯看得到單頁 SEO、看不到全站
SEO 與轉址」這條權限限制驗收可以走真實 `/login`）。

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
8. **密碼政策是最小實作**：長度 ≥9（2026-10-03 使用者裁決由 10 改 9；常數 `AdminAuthService.MinPasswordLength`，
   主站與慈善後台共用同一支 `ValidatePasswordPolicy`）、不得等於帳號本身。**帳號是一般字串**（同日裁決，不限 Email 或英數字）：
   去前後空白後非空、≤64、不含任何空白字元，中文等 Unicode 皆可，不設最短長度（`AdminAccountsRepository.ValidateUsername`）；
   建立時先 Trim，登入端（主站與慈善）也先 Trim 再比對。規劃書沒有寫死具體規則（字元類別要求、
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
（存在、`status=active`；2026-09-30 起不再檢查 `must_change_password`／`two_factor_enabled`）這組判斷原本
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
   在 `POST /accounts` 指定初始密碼，`must_change_password` 新建時為 `false`（2026-09-30 使用者裁決改密碼為選用，
   原「一律強制 `true`」已取消），初始密碼由建立者透過站外管道轉交。系統信目前只有 9 封
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
   🔵 **2026-10-06 複核：已由他處補上**——`clubs.og_image_key` 由 H1（`Features/AdminSeo`），`logo_light_key`／`logo_dark_key`／`favicon_key` 由 I3 全域設定（2026-10-02）寫入，`UploadSlotPolicy` 的 `clubs` 槽位四項齊全；J4 本身維持唯讀顯示。
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

### 濫用防護（規劃書「防機器人（reCAPTCHA / Turnstile）」，S1-17 已串接 Turnstile）

三層防線：① `Program.cs` 對 `POST .../submissions` 掛 ASP.NET Core 內建 Rate Limiting（依訪客真實 IP 分區，固定視窗 5 分鐘 20 次，超過回 429，`QueueLimit=0`）；
② `SubmitFormRequest.Website` 誘捕欄位（honeypot，填了值就安靜回成功但不寫入任何資料，**排在人機驗證之前**）；
③ **Cloudflare Turnstile**（`Security/ClubTurnstileVerifier.cs`，與慈善平台的 `ITurnstileVerifier` 刻意各自一份、互不引用）。

- 契約：`SubmitFormRequest.TurnstileToken`（JSON `turnstileToken`，選填）。
- 設定鍵 `TURNSTILE_SECRET_KEY`（club.env，主站與藍鯨共用；慈善是另一把 `TURNSTILE_SECRET_KEY_CHARITY`）。**未設＝`NotConfiguredClubTurnstileVerifier`，一律放行**（只剩①②）。
- **只有該表單 `forms.captcha_enabled = true` 且已設密鑰時才驗證**；旗標關閉不驗。（`captcha_enabled` 種子預設為開，部署端一設密鑰，前台就必須同時有 `TURNSTILE_SITE_KEY`，見 `infra/README.md` §4.3。）
- 缺 token 或驗證失敗 → **422**，ProblemDetails `code = captcha_failed`，訊息「人機驗證未通過，請重新整理頁面後再試一次。」（`CaptchaFailedException : ICodedApiException`）。
- 傳給 siteverify 的 `remoteip` 用 `ClientIpResolver.Resolve`（`TRUSTED_PROXY_IPS` 解析後的訪客真實 IP），不是原始 `RemoteIpAddress`。
- Cloudflare 服務本身異常（逾時 5 秒、5xx、連線失敗、回應無法解析）→ **放行**並記 warning（fail-open，IP 限流與 honeypot 仍在）。
- 驗證只掛在公開表單端點（`FormsRepository.SubmitAsync(..., verifyCaptcha: true)`）。提案下載 `POST proposals/{id}/download-requests` 內部重用 `SubmitAsync` 但**不驗證**（它的請求沒有 token 欄位，規劃書沒寫要求）；該端點只有 IP 限流。
- 測試：`ClubTurnstileTests.cs`（假驗證器端點整合 ＋ 假 `HttpMessageHandler` 驗證器單元，不連網）。

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

> 🔴 **2026-09-29 更新（見「S1-17 修正」節）**：上面這一段是 2026-09-25 當時的狀態，
> **環境變數已改名**（`TRUSTED_PROXY_IP` 單數 → `TRUSTED_PROXY_IPS` 複數），且信任來源從
> 「只信任 Caddy 一個 IP」擴充為「信任 Caddy ＋ `nuxt-tcrfc` ＋ `nuxt-bw` 三個固定 IP」，
> 因為 10 表單中心公開送出後來（S1-17）改由 Nuxt 伺服器端代理轉發，不再是瀏覽器直接呼叫
> 公開 API 網域。本段其餘敘述（框架陷阱、測試手法、驗收方式）原理不變，仍可參考；
> 只有「只信任這一個 IP」這句與環境變數名稱已過期，請以「S1-17 修正」節為準。

---

## S1-10 修正：題目文字語系化、G2 指派負責人姓名選單、`/auth/me` 權限碼清單（2026-09-25，`backend-engineer`）

驗收退回兩項缺口（見 `STATUS.md` S1-10 列），本輪逐一修完，另外一併處理任務指示要求的第三項
（`/auth/me` 回傳權限碼清單，供下一輪前端改接、根治 E-39 同類風險）。

### 缺口一：`form_fields` 沒有題目文字，違反全域規定第 4 條

**問題**：`form_fields` 只有 `field_key`（英文小寫代碼），沒有題目文字欄位，也沒有 `*_i18n` 側表，
公開表單無題目可顯示，後台 G2 詢問詳情只能印英文欄位代碼給人看。2026-09-22 曾經以「規劃書行1159
只列出欄位型別，沒有提到欄位標籤需要雙語」為由判斷不建 `form_fields_i18n`（`docs/12` §4.6 附註、
`docs/12c` §4 舊列），但這個判斷忽略了 CLAUDE.md 全域規定第 4 條與主站規劃書 §4.0「介面一律日常
中文」是跨全站的**通則**，不需要規劃書在每一個型別上逐字重申才算數。

**怎麼修的**：新增 `form_fields_i18n(form_field_id, locale, label, options_json)`，比照
`docs/12c` §2.2 標準側表形狀：

- `label`：題目文字。**zh-Hant 列必存**（`AdminFormsRepository.ValidateLabelZh` 應用層強制必填、
  非空白，`Create`／`UpdateAdminFormFieldRequest.LabelZh` 是 `required` 屬性），**en 列可缺**——
  沒有翻譯時公開端點回退顯示中文，跟「這一列不存在」語意合一，不用空字串表示「沒有翻譯」。
- `options_json`：下拉／多選選項的**顯示文字**，與 `form_fields.options_json`（canonical，送出值
  與驗證用，維持單一語系、不因這次修正而改變）同順序、同筆數的 JSON 字串陣列，**只有 en 列會用到
  這欄**——canonical 值本身就是 zh-Hant 的顯示文字，不重複存一份。`AdminFormsRepository.
  ValidateOptionLabelsEn` 檢查筆數與 `Options` 一致，不一致回 400。

migration：`AddFormFieldsI18n`（純加表，`form_fields` 當下已有 114 筆種子資料但不影響——新增
一張獨立表，不是對既有表加 CHECK 或 NOT NULL 欄位）。

```
migration: 20260925064538_AddFormFieldsI18n
  CREATE TABLE form_fields_i18n (
    form_field_id uniqueidentifier NOT NULL,
    locale        nvarchar(10)     NOT NULL,
    label         nvarchar(255)    NOT NULL,
    options_json  nvarchar(1000)   NULL,
    CONSTRAINT PK_form_fields_i18n PRIMARY KEY (form_field_id, locale),
    CONSTRAINT FK_form_fields_i18n_field FOREIGN KEY (form_field_id)
      REFERENCES form_fields(id) ON DELETE CASCADE
  );
  CREATE INDEX IX_form_fields_i18n_locale ON form_fields_i18n (locale);
```

**API 異動**：

| 端點 | 異動 |
|---|---|
| `GET /api/v1/{club}/forms/{formCode}?lang=zh\|en` | 新增 `lang` 查詢參數（既有慣例，比照 `FaqsEndpoints`）；`PublicFormFieldDto` 新增 `label`（必填，依語系回退）、`optionLabels`（選項顯示文字，同順序同筆數，`null`＝這個欄位沒有選項）。**快取維度改用 `dbLocale` 取代 `CacheDimensions.AnyLocale`**——語系化之後繼續共用同一把 key 會讓後填入的語系覆蓋另一個語系的結果，這是本輪順手修正的快取 bug（修正前的行為在自動化測試裡測不出來，因為單一測試行程一次只打一種語系） |
| `GET/POST /api/v1/admin/{club}/forms/{id}/fields...` | `Create`／`UpdateAdminFormFieldRequest` 新增 `LabelZh`（必填）、`LabelEn`（選填）、`OptionLabelsEn`（選填，筆數需與 `Options` 一致）；`AdminFormFieldDto` 對應回傳 `LabelZh`／`LabelEn`／`OptionLabelsEn` |

**種子資料**：`db/seed/generate-club-seed-sql.py` 新增 `field()` 輔助函式，114 個既有欄位（9 個
表單 × 2 俱樂部）逐一補上中文題目文字，並為找得到合理翻譯的欄位一併補上英文題目；
`enrollment_category`（10.2）／`enquiry_type`（10.5）兩個下拉欄位額外補上英文選項顯示文字。
套用 `apply-seed.sh` 後實測 `form_fields_i18n` 為 228 列（114 zh-Hant ＋ 114 en，本輪所有欄位皆
提供了英文翻譯，不是規格要求，是判斷「反正翻了就一起補」比留一半機會之後又漏掉更省事）。

**判斷**：canonical 值故意**不語系化**（不建「選項代碼」與「選項顯示文字」分離的新抽象）——
`enquiry_answers.value` 已經直接儲存 canonical（中文）字面值超過一輪，改成語系無關的代碼需要同時
遷移既有資料與所有比對邏輯，本輪判斷「維持 canonical＝中文，另外疊一層顯示文字」是風險最低的修正
路徑，不是規劃書要求的規格；也**不新增 `placeholder`**（提示文字）欄位——規劃書全文未提及，維持
最小可行，不多加規劃書沒有要求的東西。完整說明見 `docs/12-database-schema.md` §12 第 40 點、
`docs/12c-i18n-tables.md` §3.6／§5 第 8 點。

### 缺口二：G2「指派負責人」姓名選單僅系統管理員能用

**問題**：`AdminEnquiryListItemDto`／`AdminEnquiryDetailDto` 只回傳 `assigneeAdminUserId`
（GUID），能把它對照回姓名、或列出「可以指派給誰」的 `GET /api/v1/admin/accounts` 是
`system.account.view`（僅系統管理員）。持有 `enquiry.*.update` 但不是系統管理員的角色（客服／
行政、合作球隊管理、學院／課程管理、商務／贊助、公關／媒體）因此沒有任何後端端點能用姓名指派
負責人。

**怎麼修的**：新增 `GET /api/v1/admin/{club}/enquiries/assignable-users?formCode=...`
（`AdminEnquiriesRepository.ListAssignableUsersAsync`）：

- 權限碼：`AdminEnquiriesRepository.UpdateCandidateCodes`（跟 `PUT .../enquiries/{id}` 同一組）
  ——能處理詢問的人才能查「能指派給誰」。
- 二次檢查：`formCode` 必須落在呼叫端（依 `ResolveUpdateFormCodeFilterAsync`）持有更新權限的類別
  範圍內，否則回 404（比照既有跨類別越權「不洩漏存在與否」慣例）；`formCode` 本身不是已知的九碼
  之一也回 404。
- 回應**只有必要欄位**（`id`、`displayName`），不重用 `AdminAccountListItemDto`（那份明細含
  Email、角色、俱樂部與球隊授權，刻意只給系統管理員）——不能把 J1 帳號管理端點的存取範圍跟著
  放寬，否則等於繞道讓非系統管理員也能查到別人的 Email。
- 範圍：`scope.ClubId` 目前有效授權（`AdminUserClub.is_active` 且未過期）的帳號 ＋ 系統管理員一律
  有效，且**只回傳對 `formCode` 所屬類別持有 update 權限的帳號**（例如查 `media_enquiry` 只會列出
  持有 `enquiry.inbox.update` 或 `enquiry.media.update` 的帳號，不是這個俱樂部隨便一個有效帳號）。

**同時修正發現的邊界漏洞**：`AdminEnquiriesRepository.ValidateAssigneeAsync` 原本只驗證「被指派者
有沒有這個俱樂部的授權」，沒有驗證「被指派者對這一類詢問有沒有處理權限」——一個只有
`enquiry.media.update` 的公關／媒體帳號，先前可以被指派一筆 `partnership_sponsorship` 詢問，指派
後卻連自己被指派的這筆都看不到（G2 依類別過濾），形成「指派了也等於沒指派」的死資料。現在
`UpdateAsync` 呼叫 `ValidateAssigneeAsync` 時多帶 `enquiry.Form.FormCode`，額外要求被指派者持有
對應類別的 update 權限碼（或為系統管理員），不符合回 400「指定的負責人帳號對這一類詢問沒有處理
權限，無法指派。」

**判斷**：「候選人清單」與「指派時驗證」共用同一份 `CandidateUpdateCodesForFormCode(formCode)`
邏輯（`enquiry.inbox.update` 一定在內，另加 `formCode` 所屬類別的專屬碼），確保「清單上看得到的人」
跟「真正能被成功指派的人」永遠是同一個集合，不會有「選單顯示了卻指派失敗」或「選單沒顯示卻能用
其他管道指派成功」兩種不一致。

### 任務指示第三項：`GET /admin/auth/me` 回傳有效權限碼清單

**問題**：前端（`useProgramPermissions`／`useFormsPermissions`）依角色手寫一份「角色→操作」對照
表，要跟種子腳本手動同步，已經是 E-39 同類風險第二次發生（`useRolePermissions.ts` 檔頭已自行記錄
這個根本限制，回報「若後端需要回傳權限清單才能根治，寫進報告，不要改後端」）。

**怎麼修的**：`IPermissionChecker` 新增 `GetAllHeldPermissionsAsync(adminUserId, isSuperAdmin, ct)`
——回傳這個帳號目前實際持有的**全部**權限碼，形狀是 `Code → 這個人對這個權限碼持有的 scope_type
原始集合`（一個人可能透過多個角色持有同一個權限碼、各自帶不同 `scope_type`，這裡**不做「多個
scope_type 該合併成單一有效值」的商業判斷**，那件事留給 `TeamRowScope`／`AdminTeamRowScopeResolver`
這種已經為特定資源類型定義過合併規則的型別，避免發明一個只有這個端點在用的合併規則）。
`isSuperAdmin=true` 時回傳系統裡**全部**權限碼（含 `sysadmin_only`），每個標記 `["all"]`——系統
管理員跳過整個 `role_permissions` 查詢直接視為持有一切，跟 `HasPermissionAsync` 同一條規則。

`MeResponse` 新增 `permissions: MePermissionDto[]`（`{code, scopeTypes}`），`AdminAuthService.
GetMeAsync` 呼叫上述方法填入。**權限碼只給程式判斷用，前端不得顯示**（主站規劃書 §4.0「介面一律
日常中文……不顯示……權限碼」）。

**判斷（回報供下一輪前端改接參考，本輪未改 `apps/admin`）**：

1. **這份清單跟「目前俱樂部」無關**——本系統的角色指派（`admin_user_roles`）與角色的權限指派
   （`role_permissions`）都沒有 `club_id` 維度，一個人對某個權限碼持有哪些 `scope_type` 不會因為
   切換到哪個俱樂部而改變；真正決定「這個人能不能碰這個俱樂部」的是既有 `ClubGrants`
   （`AdminUserClub`）。前端要判斷「在目前這個俱樂部能不能做某件事」，需要同時看兩份清單：先確認
   目前俱樂部在 `ClubGrants` 裡，再查 `Permissions` 有沒有對應權限碼——這是本輪判斷，`/auth/me`
   端點本身沒有 `club` 參數，因為權限碼清單不會因俱樂部而異，加這個參數只會誤導呼叫端以為有這種
   相依性。
2. **`scopeTypes` 回傳原始集合，不做合併**——例如某人同時是「學院／課程管理」（`own_teams` 之類）
   與「合作球隊管理」（`own_clubs`）兩個角色，對同一個權限碼會回傳兩個 `scope_type`。前端若要做
   「是否受列級限制」的粗判斷，含 `"all"` 或 `"own_clubs"` 即代表這個人對這個權限碼至少有一個
   角色是不受列級限制的（比照 `AdminTeamRowScopeResolver` 現有的「`own_clubs` 視同 `all`」判斷）；
   若前端要做更細的列級 UI（例如「只顯示我能碰的球隊」），現階段仍得靠既有的專屬端點（例如
   `own_teams` 相關資料），`/auth/me` 的權限碼清單不是要取代那些端點，只是取代前端手寫的
   「角色→操作」推導表。

### 測試

`Tcrfc.Api.Tests/AdminFormsEnquiriesTests.cs` 新增 2 項（公開表單定義依語系回傳題目與選項顯示
文字、未翻譯回退中文；G1 建立／更新欄位題目文字必填與選項英文顯示文字筆數驗證，含清空英文題目
後公開端點正確回退）。全套 `dotnet test` **439／439 通過**（含本輪新增與既有全部項目）。

**手動驗收**（本機 `dotnet run` 另開 `5499` 埠，`curl`＋自簽 JWT，未使用任何互動式登入或 2FA 流程
——理由：既有「-login」後綴測試帳號當時正被另一個並行 session 的無頭瀏覽器驗收使用中，直接登入
會互相干擾；比照 `Tcrfc.Api.Tests.Fixtures.TestAdminTokens` 同一套簽章邏輯與金鑰另外寫一支一次性
小工具直接簽出有效存取權杖，验证的是真正跑在獨立行程的 API、真正的 HTTP 請求與真正的
`tcrfc_club_dev`，不是走 `WebApplicationFactory` 的行程內管線）：

1. `GET /auth/me`（`customer.service@tcrfc.test`）：`permissions` 陣列正確含
   `enquiry.inbox.view/update`／`form.view/update`，`scopeTypes` 皆為 `["all"]`。
2. `GET .../forms/partnership_sponsorship?lang=en`：`enquiry_type` 欄位 `label` 為
   `"Enquiry Type"`、`options` 為中文 canonical 值、`optionLabels` 為對應英文；`company` 欄位（無
   選項）`label` 為 `"Company Name"`、`optionLabels` 為 `null`；`?lang=zh` 對照組 `optionLabels`
   回退等於 canonical 值本身。
3. `GET .../enquiries/assignable-users?formCode=general_contact`（`customer.service`）：200，列出
   系統管理員與全部持有 `enquiry.inbox.update` 的帳號。
4. `GET .../enquiries/assignable-users?formCode=general_contact`（`pr.media`，只有
   `enquiry.media.*`）：404（越權，不洩漏存在與否）。
5. `GET .../enquiries/assignable-users?formCode=media_enquiry`（`pr.media`）：200，清單同時含
   `enquiry.inbox.update`（客服／行政）與 `enquiry.media.update`（公關／媒體）持有者的聯集。
6. `GET .../enquiries/assignable-users`（`content.editor`，完全沒有 `enquiry.*` 權限碼）：403。
7. `GET .../enquiries/assignable-users?formCode=not_a_real_code`：404。
8. 用公開端點送出一筆 `general_contact` 測試詢問 → `PUT .../enquiries/{id}` 指派給 `pr.media`
   （只有 `enquiry.media.*`，`general_contact` 屬於 inbox-only 類別）：400「指定的負責人帳號對這
   一類詢問沒有處理權限，無法指派。」→ 改指派給自己（`customer.service` 持有
   `enquiry.inbox.update`）：200，成功。
9. **收尾**：刪除本輪建立的測試詢問資料（`DELETE FROM enquiry_answers`／`enquiries` 對應列）、
   關閉本機 `dotnet run`（`5499`）行程、刪除一次性簽權杖小工具（未進版控）。**未動用任何共用
   「-login」帳號的 2FA 或密碼狀態**——本輪驗收方式全程繞開互動式登入，不會與其他並行 session
   互相干擾。

🔴 **驗收期間發現的既有帳號狀態污染，非本輪造成**：跑 `dotnet test` 全套時 `AdminAuthTests` 三項
（`登入成功_回傳存取權杖與更新權杖Cookie` 等）一度失敗，原因是共用開發資料庫的
`clean.login@tcrfc.test` 當下 `two_factor_enabled=1`（另一個並行 session 的無頭瀏覽器 E2E 驗收
正在使用這個帳號，`ps aux` 可見其 `dotnet run`／headless Chrome 行程），跟本輪任何改動無關（這三項
測試只碰 `/login`／`/refresh`／`/logout`，本輪對 `AdminAuthService.cs` 的唯一改動在 `GetMeAsync`
方法本體）。該並行 session 的行程結束後執行 `db/seed/reset-admin-accounts.sh` 還原種子帳號初始
狀態，重跑 `dotnet test` 全綠（**439／439**）。

`apps/admin`／`apps/web` 的 `npm run lint` 皆通過（`apps/admin` 0 errors／0 warnings，`apps/web`
0 errors，既有 539 個 warning 與本輪無關）——**本輪未觸碰任何 `apps/admin`／`apps/web` 檔案**，
後台畫面（G1 題目文字欄位、G2 姓名選單）留給下一輪 `frontend-architect`。

---

## S1-11：`L1` 行事曆總覽／`L2` 自建事件 ＋ 13 賽事行事曆公開讀取（含單場 `.ics`）（2026-09-25，`backend-engineer`）

主站規劃書 §4.12 L1／L2（後台）、§3.13（13 賽事行事曆，公開讀取）。沿用既有架構：
`IAdminClubAuthorizer`、`IClubResolver`／`ClubScope`、`IQueryCache`、後台圖片欄位直傳
（`calendar_custom_events.cover_key`）。**沒有套用 `TeamRowScope`**——見下方「為什麼不套列級授權」。

### 讀到的規劃書條文

| 章節 | 行號 | 內容 |
|---|---|---|
| 主站 §4.12 L1 | 1368–1376 | 月曆呈現全部賽事與自建活動、隊別分軌檢視、拖曳改期回寫賽事、衝突偵測、篩選、檢視切換；「課程／營隊／專項訓練不進入行事曆」 |
| 主站 §4.12 L2 | 1378–1381 | 自建事件欄位、重複規則（每週／每兩週／每月，可設定結束日期與例外日期） |
| 主站 §4.12「資料一致性原則」 | 1396–1398 | 行事曆是彙整層而非資料源，僅 L2 為自有資料 |
| 主站 §3.13 | 591–711 | 隊別分類（D1／U15／U14／U12，另有全部與俱樂部活動）、賽程／賽果切換、賽事卡片、加入我的行事曆 `.ics`、SEO 網址規則 |
| 主站 §6 矩陣「行事曆」欄 | 1607 | 十個角色逐列分佈，見下方權限碼段 |
| docs/12b §7.4 | — | `own_teams` 綁在「賽事事件」「梯隊賽事」兩格，S1-8 保留給 L 模組使用 |

### STATUS.md 既定的範圍切法（沿用，非本輪判斷）

`STATUS.md` 把規劃書 L1 原文列出的「隊別分軌檢視」「拖曳調整日期回寫賽事」「衝突偵測」與 L3／L4
一起排進 `S2-6`（「行事曆進階」）。本輪只做 **L1 合併讀取**（把 `matches` 與
`calendar_custom_events` 換算成同一種形狀回傳）與 **L2 自建事件 CRUD**，不做分軌並排、拖曳改期、
衝突警示、訂閱與匯出——這是既有工作分解，不是本輪重新裁量，任務指示本身也是照這個切法派工。

### 資料庫：既有 DDL 已經備妥，只補一個欄位與一個 CHECK 約束

`calendar_custom_events`／`calendar_custom_events_i18n`／`calendar_event_teams`／
`calendar_event_exceptions`／`event_types`／`event_types_i18n` 六張表在 S0 系列就已經是完整 DDL
（`db/club-schema.sql`「4.10 L 行事曆管理」），EF 實體與 `ClubDbContext` 對應也早就 scaffold 好。
本輪異動：

1. **新增欄位** `calendar_custom_events.repeat_until`（`date NULL`）——規劃書 L2「可設定結束日期與
   例外日期」，例外日期已有 `calendar_event_exceptions` 承接，但原始 DDL沒有任何欄位承接「結束
   日期」，`docs/12d-field-audit.md` 也記過這個缺口。
2. **補齊從未約束過的值域** `repeat_rule`：定案為 `weekly`／`biweekly`／`monthly` 三個英文字面值
   （比照 `matches.status`「挑最直白的英文單字」既有風格，規劃書只給中文頻率敘述，沒有給代碼或
   RRULE 格式的技術決定），補上 `CK_calendar_custom_events_repeat_rule`。

```
migration: 20260925055015_AddCalendarCustomEventRepeatUntil
  ALTER TABLE calendar_custom_events ADD [repeat_until] date NULL;   -- EF AddColumn，來自實體模型異動
  ALTER TABLE calendar_custom_events ADD CONSTRAINT CK_calendar_custom_events_repeat_rule
    CHECK (repeat_rule IN ('weekly','biweekly','monthly') OR repeat_rule IS NULL);
```

套用前查證 `calendar_custom_events` 為 0 筆（本輪才第一次接上真實 API），純 DDL 變更，不搭配任何
DML 轉態。完整說明另見 `docs/12-database-schema.md` §12 第 39 點。

**重複規則不 materialize 成事件實例表**——比照「行事曆是彙整層而非資料源」的既有原則，改為讀取
當下依呼叫端要求的日期範圍即時展開（`Common/RecurrenceExpander.cs`），範圍本身已經是呼叫端的
必要輸入（月曆檢視一次看一個月、公開列表也有 `from`／`to`），迭代次數天然有界（防呆上限 400 次）。

**種子資料**：`event_types` 種六個起始分類（記者會／簽名會／球迷見面會／公開訓練／休館公告／
其他，不帶 `club_id`，兩俱樂部共用）——L3「賽事類型維護」正式的 CRUD 管理畫面留給 `S2-6`，這裡
只種最小可行的起始字典，讓 L2 建立事件時有分類可選，比照既有 `HOME_SECTIONS`／`FAQ_EMBED_SLOTS`
「先種固定字典，完整維護畫面留給後續」的既有先例。

### 後台端點（新增檔案 `Features/AdminCalendar`）

| 方法與路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET /api/v1/admin/{club}/calendar/events` | `calendar.view` | L1 總覽：合併 `matches`＋`calendar_custom_events`。`from`／`to`（預設本月，上限 366 天）、`team`（球隊代碼或 `club`）、`sourceType`（`match`／`custom`）篩選 |
| `GET /api/v1/admin/{club}/calendar/event-types` | `calendar.custom_event.view` | L2 建立／編輯事件用的分類下拉選單（唯讀，L3 正式管理畫面留給 `S2-6`） |
| `GET /api/v1/admin/{club}/calendar/custom-events` | `calendar.custom_event.view` | L2 清單。`team` 篩選 |
| `GET /api/v1/admin/{club}/calendar/custom-events/{id}` | `calendar.custom_event.view` | 單筆詳情 |
| `POST /api/v1/admin/{club}/calendar/custom-events` | `calendar.custom_event.create` | 建立，`multipart/form-data`（`payload`＋選填 `file` 封面圖） |
| `PUT /api/v1/admin/{club}/calendar/custom-events/{id}` | `calendar.custom_event.update` | 更新，同上 multipart 契約 |
| `DELETE /api/v1/admin/{club}/calendar/custom-events/{id}` | `calendar.custom_event.delete` | 硬刪除 |

### 公開端點（新增檔案 `Features/Calendar`，不需要登入）

| 方法與路徑 | 說明 |
|---|---|
| `GET /api/v1/{club}/calendar/events` | 13 賽事行事曆。見下方「兩種查詢模式」 |
| `GET /api/v1/{club}/matches/{id}/ics` | 單場賽事「加入我的行事曆」下載 |

#### 兩種查詢模式（本輪判斷，規劃書沒有把這兩種模式的參數形狀寫清楚）

1. **列表模式**（`from`／`to` 皆未提供）：依 `team` 決定內容——
   - `team=club`：回傳「俱樂部活動」分頁（規劃書「隊別分頁……另有『全部』與『俱樂部活動』」），
     只給公開（`is_public=1`）、**未掛任何球隊**、活動類型公開（或未設類型）的自建事件；
     **重複規則即時展開**（預設區間＝今天起 180 天，例外日排除，`repeat_until` 截止），每個發生日一筆，
     依 `startsAt` 由近到遠，`page`／`pageSize` 分頁；
   - 其餘（含未指定）：回傳**只有賽事**的「賽程 Fixtures／賽果 Results」分頁（`mode=fixtures`
     預設／`results`），支援 `season`／`type`（賽事類型標籤）／`homeAway` 篩選、`page`／`pageSize`
     分頁——這兩個分頁在規劃書原文本來就是賽事的概念（未來／過去），俱樂部活動沒有「賽果」的語意。
2. **月曆模式**（提供 `from`／`to`，上限 366 天）：合併賽事與公開自建事件，L2 重複規則即時展開，
   排序一律由近到遠，不分賽程／賽果，仍可疊加 `team`／`season`／`type`／`homeAway` 篩選。

**自建活動回應欄位**（`PublicCalendarEventDto`，A-9／B-6）：`id`＝原活動 id（重複活動同 id 多筆）、
`startsAt`／`endsAt`＝該次發生的起訖、`occurrenceId`＝每次發生的唯一鍵（`{id}:{yyyyMMddHHmm}`，v-for key 用）、
`isRecurring`、`eventTypeCode`／`eventTypeName`（依 `lang`，缺譯回退中文）／`eventTypeColour`／`eventTypeIcon`。
**team 篩選語意**（兩種模式一致）：`team=club`＝只回未掛任何球隊的活動；`team={code}`＝只回掛在該隊的活動
（列表模式的 `team={code}` 仍只回賽事，要看該隊活動請用月曆模式 `from`／`to`）。
**活動類型 `event_types.is_public=0` 時其下活動不輸出**（公開列表、月曆模式與 `calendar/feed.ics` 一致）。

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

### 濫用防護（規劃書「防機器人（reCAPTCHA / Turnstile）」，S1-17 已串接 Turnstile）

三層防線：① `Program.cs` 對 `POST .../submissions` 掛 ASP.NET Core 內建 Rate Limiting（依訪客真實 IP 分區，固定視窗 5 分鐘 20 次，超過回 429，`QueueLimit=0`）；
② `SubmitFormRequest.Website` 誘捕欄位（honeypot，填了值就安靜回成功但不寫入任何資料，**排在人機驗證之前**）；
③ **Cloudflare Turnstile**（`Security/ClubTurnstileVerifier.cs`，與慈善平台的 `ITurnstileVerifier` 刻意各自一份、互不引用）。

- 契約：`SubmitFormRequest.TurnstileToken`（JSON `turnstileToken`，選填）。
- 設定鍵 `TURNSTILE_SECRET_KEY`（club.env，主站與藍鯨共用；慈善是另一把 `TURNSTILE_SECRET_KEY_CHARITY`）。**未設＝`NotConfiguredClubTurnstileVerifier`，一律放行**（只剩①②）。
- **只有該表單 `forms.captcha_enabled = true` 且已設密鑰時才驗證**；旗標關閉不驗。（`captcha_enabled` 種子預設為開，部署端一設密鑰，前台就必須同時有 `TURNSTILE_SITE_KEY`，見 `infra/README.md` §4.3。）
- 缺 token 或驗證失敗 → **422**，ProblemDetails `code = captcha_failed`，訊息「人機驗證未通過，請重新整理頁面後再試一次。」（`CaptchaFailedException : ICodedApiException`）。
- 傳給 siteverify 的 `remoteip` 用 `ClientIpResolver.Resolve`（`TRUSTED_PROXY_IPS` 解析後的訪客真實 IP），不是原始 `RemoteIpAddress`。
- Cloudflare 服務本身異常（逾時 5 秒、5xx、連線失敗、回應無法解析）→ **放行**並記 warning（fail-open，IP 限流與 honeypot 仍在）。
- 驗證只掛在公開表單端點（`FormsRepository.SubmitAsync(..., verifyCaptcha: true)`）。提案下載 `POST proposals/{id}/download-requests` 內部重用 `SubmitAsync` 但**不驗證**（它的請求沒有 token 欄位，規劃書沒寫要求）；該端點只有 IP 限流。
- 測試：`ClubTurnstileTests.cs`（假驗證器端點整合 ＋ 假 `HttpMessageHandler` 驗證器單元，不連網）。

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

> 🔴 **2026-09-29 更新（見「S1-17 修正」節）**：上面這一段是 2026-09-25 當時的狀態，
> **環境變數已改名**（`TRUSTED_PROXY_IP` 單數 → `TRUSTED_PROXY_IPS` 複數），且信任來源從
> 「只信任 Caddy 一個 IP」擴充為「信任 Caddy ＋ `nuxt-tcrfc` ＋ `nuxt-bw` 三個固定 IP」，
> 因為 10 表單中心公開送出後來（S1-17）改由 Nuxt 伺服器端代理轉發，不再是瀏覽器直接呼叫
> 公開 API 網域。本段其餘敘述（框架陷阱、測試手法、驗收方式）原理不變，仍可參考；
> 只有「只信任這一個 IP」這句與環境變數名稱已過期，請以「S1-17 修正」節為準。

---

## S1-10 修正：題目文字語系化、G2 指派負責人姓名選單、`/auth/me` 權限碼清單（2026-09-25，`backend-engineer`）

驗收退回兩項缺口（見 `STATUS.md` S1-10 列），本輪逐一修完，另外一併處理任務指示要求的第三項
（`/auth/me` 回傳權限碼清單，供下一輪前端改接、根治 E-39 同類風險）。

### 缺口一：`form_fields` 沒有題目文字，違反全域規定第 4 條

**問題**：`form_fields` 只有 `field_key`（英文小寫代碼），沒有題目文字欄位，也沒有 `*_i18n` 側表，
公開表單無題目可顯示，後台 G2 詢問詳情只能印英文欄位代碼給人看。2026-09-22 曾經以「規劃書行1159
只列出欄位型別，沒有提到欄位標籤需要雙語」為由判斷不建 `form_fields_i18n`（`docs/12` §4.6 附註、
`docs/12c` §4 舊列），但這個判斷忽略了 CLAUDE.md 全域規定第 4 條與主站規劃書 §4.0「介面一律日常
中文」是跨全站的**通則**，不需要規劃書在每一個型別上逐字重申才算數。

**怎麼修的**：新增 `form_fields_i18n(form_field_id, locale, label, options_json)`，比照
`docs/12c` §2.2 標準側表形狀：

- `label`：題目文字。**zh-Hant 列必存**（`AdminFormsRepository.ValidateLabelZh` 應用層強制必填、
  非空白，`Create`／`UpdateAdminFormFieldRequest.LabelZh` 是 `required` 屬性），**en 列可缺**——
  沒有翻譯時公開端點回退顯示中文，跟「這一列不存在」語意合一，不用空字串表示「沒有翻譯」。
- `options_json`：下拉／多選選項的**顯示文字**，與 `form_fields.options_json`（canonical，送出值
  與驗證用，維持單一語系、不因這次修正而改變）同順序、同筆數的 JSON 字串陣列，**只有 en 列會用到
  這欄**——canonical 值本身就是 zh-Hant 的顯示文字，不重複存一份。`AdminFormsRepository.
  ValidateOptionLabelsEn` 檢查筆數與 `Options` 一致，不一致回 400。

migration：`AddFormFieldsI18n`（純加表，`form_fields` 當下已有 114 筆種子資料但不影響——新增
一張獨立表，不是對既有表加 CHECK 或 NOT NULL 欄位）。

```
migration: 20260925064538_AddFormFieldsI18n
  CREATE TABLE form_fields_i18n (
    form_field_id uniqueidentifier NOT NULL,
    locale        nvarchar(10)     NOT NULL,
    label         nvarchar(255)    NOT NULL,
    options_json  nvarchar(1000)   NULL,
    CONSTRAINT PK_form_fields_i18n PRIMARY KEY (form_field_id, locale),
    CONSTRAINT FK_form_fields_i18n_field FOREIGN KEY (form_field_id)
      REFERENCES form_fields(id) ON DELETE CASCADE
  );
  CREATE INDEX IX_form_fields_i18n_locale ON form_fields_i18n (locale);
```

**API 異動**：

| 端點 | 異動 |
|---|---|
| `GET /api/v1/{club}/forms/{formCode}?lang=zh\|en` | 新增 `lang` 查詢參數（既有慣例，比照 `FaqsEndpoints`）；`PublicFormFieldDto` 新增 `label`（必填，依語系回退）、`optionLabels`（選項顯示文字，同順序同筆數，`null`＝這個欄位沒有選項）。**快取維度改用 `dbLocale` 取代 `CacheDimensions.AnyLocale`**——語系化之後繼續共用同一把 key 會讓後填入的語系覆蓋另一個語系的結果，這是本輪順手修正的快取 bug（修正前的行為在自動化測試裡測不出來，因為單一測試行程一次只打一種語系） |
| `GET/POST /api/v1/admin/{club}/forms/{id}/fields...` | `Create`／`UpdateAdminFormFieldRequest` 新增 `LabelZh`（必填）、`LabelEn`（選填）、`OptionLabelsEn`（選填，筆數需與 `Options` 一致）；`AdminFormFieldDto` 對應回傳 `LabelZh`／`LabelEn`／`OptionLabelsEn` |

**種子資料**：`db/seed/generate-club-seed-sql.py` 新增 `field()` 輔助函式，114 個既有欄位（9 個
表單 × 2 俱樂部）逐一補上中文題目文字，並為找得到合理翻譯的欄位一併補上英文題目；
`enrollment_category`（10.2）／`enquiry_type`（10.5）兩個下拉欄位額外補上英文選項顯示文字。
套用 `apply-seed.sh` 後實測 `form_fields_i18n` 為 228 列（114 zh-Hant ＋ 114 en，本輪所有欄位皆
提供了英文翻譯，不是規格要求，是判斷「反正翻了就一起補」比留一半機會之後又漏掉更省事）。

**判斷**：canonical 值故意**不語系化**（不建「選項代碼」與「選項顯示文字」分離的新抽象）——
`enquiry_answers.value` 已經直接儲存 canonical（中文）字面值超過一輪，改成語系無關的代碼需要同時
遷移既有資料與所有比對邏輯，本輪判斷「維持 canonical＝中文，另外疊一層顯示文字」是風險最低的修正
路徑，不是規劃書要求的規格；也**不新增 `placeholder`**（提示文字）欄位——規劃書全文未提及，維持
最小可行，不多加規劃書沒有要求的東西。完整說明見 `docs/12-database-schema.md` §12 第 40 點、
`docs/12c-i18n-tables.md` §3.6／§5 第 8 點。

### 缺口二：G2「指派負責人」姓名選單僅系統管理員能用

**問題**：`AdminEnquiryListItemDto`／`AdminEnquiryDetailDto` 只回傳 `assigneeAdminUserId`
（GUID），能把它對照回姓名、或列出「可以指派給誰」的 `GET /api/v1/admin/accounts` 是
`system.account.view`（僅系統管理員）。持有 `enquiry.*.update` 但不是系統管理員的角色（客服／
行政、合作球隊管理、學院／課程管理、商務／贊助、公關／媒體）因此沒有任何後端端點能用姓名指派
負責人。

**怎麼修的**：新增 `GET /api/v1/admin/{club}/enquiries/assignable-users?formCode=...`
（`AdminEnquiriesRepository.ListAssignableUsersAsync`）：

- 權限碼：`AdminEnquiriesRepository.UpdateCandidateCodes`（跟 `PUT .../enquiries/{id}` 同一組）
  ——能處理詢問的人才能查「能指派給誰」。
- 二次檢查：`formCode` 必須落在呼叫端（依 `ResolveUpdateFormCodeFilterAsync`）持有更新權限的類別
  範圍內，否則回 404（比照既有跨類別越權「不洩漏存在與否」慣例）；`formCode` 本身不是已知的九碼
  之一也回 404。
- 回應**只有必要欄位**（`id`、`displayName`），不重用 `AdminAccountListItemDto`（那份明細含
  Email、角色、俱樂部與球隊授權，刻意只給系統管理員）——不能把 J1 帳號管理端點的存取範圍跟著
  放寬，否則等於繞道讓非系統管理員也能查到別人的 Email。
- 範圍：`scope.ClubId` 目前有效授權（`AdminUserClub.is_active` 且未過期）的帳號 ＋ 系統管理員一律
  有效，且**只回傳對 `formCode` 所屬類別持有 update 權限的帳號**（例如查 `media_enquiry` 只會列出
  持有 `enquiry.inbox.update` 或 `enquiry.media.update` 的帳號，不是這個俱樂部隨便一個有效帳號）。

**同時修正發現的邊界漏洞**：`AdminEnquiriesRepository.ValidateAssigneeAsync` 原本只驗證「被指派者
有沒有這個俱樂部的授權」，沒有驗證「被指派者對這一類詢問有沒有處理權限」——一個只有
`enquiry.media.update` 的公關／媒體帳號，先前可以被指派一筆 `partnership_sponsorship` 詢問，指派
後卻連自己被指派的這筆都看不到（G2 依類別過濾），形成「指派了也等於沒指派」的死資料。現在
`UpdateAsync` 呼叫 `ValidateAssigneeAsync` 時多帶 `enquiry.Form.FormCode`，額外要求被指派者持有
對應類別的 update 權限碼（或為系統管理員），不符合回 400「指定的負責人帳號對這一類詢問沒有處理
權限，無法指派。」

**判斷**：「候選人清單」與「指派時驗證」共用同一份 `CandidateUpdateCodesForFormCode(formCode)`
邏輯（`enquiry.inbox.update` 一定在內，另加 `formCode` 所屬類別的專屬碼），確保「清單上看得到的人」
跟「真正能被成功指派的人」永遠是同一個集合，不會有「選單顯示了卻指派失敗」或「選單沒顯示卻能用
其他管道指派成功」兩種不一致。

### 任務指示第三項：`GET /admin/auth/me` 回傳有效權限碼清單

**問題**：前端（`useProgramPermissions`／`useFormsPermissions`）依角色手寫一份「角色→操作」對照
表，要跟種子腳本手動同步，已經是 E-39 同類風險第二次發生（`useRolePermissions.ts` 檔頭已自行記錄
這個根本限制，回報「若後端需要回傳權限清單才能根治，寫進報告，不要改後端」）。

**怎麼修的**：`IPermissionChecker` 新增 `GetAllHeldPermissionsAsync(adminUserId, isSuperAdmin, ct)`
——回傳這個帳號目前實際持有的**全部**權限碼，形狀是 `Code → 這個人對這個權限碼持有的 scope_type
原始集合`（一個人可能透過多個角色持有同一個權限碼、各自帶不同 `scope_type`，這裡**不做「多個
scope_type 該合併成單一有效值」的商業判斷**，那件事留給 `TeamRowScope`／`AdminTeamRowScopeResolver`
這種已經為特定資源類型定義過合併規則的型別，避免發明一個只有這個端點在用的合併規則）。
`isSuperAdmin=true` 時回傳系統裡**全部**權限碼（含 `sysadmin_only`），每個標記 `["all"]`——系統
管理員跳過整個 `role_permissions` 查詢直接視為持有一切，跟 `HasPermissionAsync` 同一條規則。

`MeResponse` 新增 `permissions: MePermissionDto[]`（`{code, scopeTypes}`），`AdminAuthService.
GetMeAsync` 呼叫上述方法填入。**權限碼只給程式判斷用，前端不得顯示**（主站規劃書 §4.0「介面一律
日常中文……不顯示……權限碼」）。

**判斷（回報供下一輪前端改接參考，本輪未改 `apps/admin`）**：

1. **這份清單跟「目前俱樂部」無關**——本系統的角色指派（`admin_user_roles`）與角色的權限指派
   （`role_permissions`）都沒有 `club_id` 維度，一個人對某個權限碼持有哪些 `scope_type` 不會因為
   切換到哪個俱樂部而改變；真正決定「這個人能不能碰這個俱樂部」的是既有 `ClubGrants`
   （`AdminUserClub`）。前端要判斷「在目前這個俱樂部能不能做某件事」，需要同時看兩份清單：先確認
   目前俱樂部在 `ClubGrants` 裡，再查 `Permissions` 有沒有對應權限碼——這是本輪判斷，`/auth/me`
   端點本身沒有 `club` 參數，因為權限碼清單不會因俱樂部而異，加這個參數只會誤導呼叫端以為有這種
   相依性。
2. **`scopeTypes` 回傳原始集合，不做合併**——例如某人同時是「學院／課程管理」（`own_teams` 之類）
   與「合作球隊管理」（`own_clubs`）兩個角色，對同一個權限碼會回傳兩個 `scope_type`。前端若要做
   「是否受列級限制」的粗判斷，含 `"all"` 或 `"own_clubs"` 即代表這個人對這個權限碼至少有一個
   角色是不受列級限制的（比照 `AdminTeamRowScopeResolver` 現有的「`own_clubs` 視同 `all`」判斷）；
   若前端要做更細的列級 UI（例如「只顯示我能碰的球隊」），現階段仍得靠既有的專屬端點（例如
   `own_teams` 相關資料），`/auth/me` 的權限碼清單不是要取代那些端點，只是取代前端手寫的
   「角色→操作」推導表。

### 測試

`Tcrfc.Api.Tests/AdminFormsEnquiriesTests.cs` 新增 2 項（公開表單定義依語系回傳題目與選項顯示
文字、未翻譯回退中文；G1 建立／更新欄位題目文字必填與選項英文顯示文字筆數驗證，含清空英文題目
後公開端點正確回退）。全套 `dotnet test` **439／439 通過**（含本輪新增與既有全部項目）。

**手動驗收**（本機 `dotnet run` 另開 `5499` 埠，`curl`＋自簽 JWT，未使用任何互動式登入或 2FA 流程
——理由：既有「-login」後綴測試帳號當時正被另一個並行 session 的無頭瀏覽器驗收使用中，直接登入
會互相干擾；比照 `Tcrfc.Api.Tests.Fixtures.TestAdminTokens` 同一套簽章邏輯與金鑰另外寫一支一次性
小工具直接簽出有效存取權杖，验证的是真正跑在獨立行程的 API、真正的 HTTP 請求與真正的
`tcrfc_club_dev`，不是走 `WebApplicationFactory` 的行程內管線）：

1. `GET /auth/me`（`customer.service@tcrfc.test`）：`permissions` 陣列正確含
   `enquiry.inbox.view/update`／`form.view/update`，`scopeTypes` 皆為 `["all"]`。
2. `GET .../forms/partnership_sponsorship?lang=en`：`enquiry_type` 欄位 `label` 為
   `"Enquiry Type"`、`options` 為中文 canonical 值、`optionLabels` 為對應英文；`company` 欄位（無
   選項）`label` 為 `"Company Name"`、`optionLabels` 為 `null`；`?lang=zh` 對照組 `optionLabels`
   回退等於 canonical 值本身。
3. `GET .../enquiries/assignable-users?formCode=general_contact`（`customer.service`）：200，列出
   系統管理員與全部持有 `enquiry.inbox.update` 的帳號。
4. `GET .../enquiries/assignable-users?formCode=general_contact`（`pr.media`，只有
   `enquiry.media.*`）：404（越權，不洩漏存在與否）。
5. `GET .../enquiries/assignable-users?formCode=media_enquiry`（`pr.media`）：200，清單同時含
   `enquiry.inbox.update`（客服／行政）與 `enquiry.media.update`（公關／媒體）持有者的聯集。
6. `GET .../enquiries/assignable-users`（`content.editor`，完全沒有 `enquiry.*` 權限碼）：403。
7. `GET .../enquiries/assignable-users?formCode=not_a_real_code`：404。
8. 用公開端點送出一筆 `general_contact` 測試詢問 → `PUT .../enquiries/{id}` 指派給 `pr.media`
   （只有 `enquiry.media.*`，`general_contact` 屬於 inbox-only 類別）：400「指定的負責人帳號對這
   一類詢問沒有處理權限，無法指派。」→ 改指派給自己（`customer.service` 持有
   `enquiry.inbox.update`）：200，成功。
9. **收尾**：刪除本輪建立的測試詢問資料（`DELETE FROM enquiry_answers`／`enquiries` 對應列）、
   關閉本機 `dotnet run`（`5499`）行程、刪除一次性簽權杖小工具（未進版控）。**未動用任何共用
   「-login」帳號的 2FA 或密碼狀態**——本輪驗收方式全程繞開互動式登入，不會與其他並行 session
   互相干擾。

🔴 **驗收期間發現的既有帳號狀態污染，非本輪造成**：跑 `dotnet test` 全套時 `AdminAuthTests` 三項
（`登入成功_回傳存取權杖與更新權杖Cookie` 等）一度失敗，原因是共用開發資料庫的
`clean.login@tcrfc.test` 當下 `two_factor_enabled=1`（另一個並行 session 的無頭瀏覽器 E2E 驗收
正在使用這個帳號，`ps aux` 可見其 `dotnet run`／headless Chrome 行程），跟本輪任何改動無關（這三項
測試只碰 `/login`／`/refresh`／`/logout`，本輪對 `AdminAuthService.cs` 的唯一改動在 `GetMeAsync`
方法本體）。該並行 session 的行程結束後執行 `db/seed/reset-admin-accounts.sh` 還原種子帳號初始
狀態，重跑 `dotnet test` 全綠（**439／439**）。

`apps/admin`／`apps/web` 的 `npm run lint` 皆通過（`apps/admin` 0 errors／0 warnings，`apps/web`
0 errors，既有 539 個 warning 與本輪無關）——**本輪未觸碰任何 `apps/admin`／`apps/web` 檔案**，
後台畫面（G1 題目文字欄位、G2 姓名選單）留給下一輪 `frontend-architect`。

---

## S1-11：`L1` 行事曆總覽／`L2` 自建事件 ＋ 13 賽事行事曆公開讀取（含單場 `.ics`）（2026-09-25，`backend-engineer`）

主站規劃書 §4.12 L1／L2（後台）、§3.13（13 賽事行事曆，公開讀取）。沿用既有架構：
`IAdminClubAuthorizer`、`IClubResolver`／`ClubScope`、`IQueryCache`、後台圖片欄位直傳
（`calendar_custom_events.cover_key`）。**沒有套用 `TeamRowScope`**——見下方「為什麼不套列級授權」。

### 讀到的規劃書條文

| 章節 | 行號 | 內容 |
|---|---|---|
| 主站 §4.12 L1 | 1368–1376 | 月曆呈現全部賽事與自建活動、隊別分軌檢視、拖曳改期回寫賽事、衝突偵測、篩選、檢視切換；「課程／營隊／專項訓練不進入行事曆」 |
| 主站 §4.12 L2 | 1378–1381 | 自建事件欄位、重複規則（每週／每兩週／每月，可設定結束日期與例外日期） |
| 主站 §4.12「資料一致性原則」 | 1396–1398 | 行事曆是彙整層而非資料源，僅 L2 為自有資料 |
| 主站 §3.13 | 591–711 | 隊別分類（D1／U15／U14／U12，另有全部與俱樂部活動）、賽程／賽果切換、賽事卡片、加入我的行事曆 `.ics`、SEO 網址規則 |
| 主站 §6 矩陣「行事曆」欄 | 1607 | 十個角色逐列分佈，見下方權限碼段 |
| docs/12b §7.4 | — | `own_teams` 綁在「賽事事件」「梯隊賽事」兩格，S1-8 保留給 L 模組使用 |

### STATUS.md 既定的範圍切法（沿用，非本輪判斷）

`STATUS.md` 把規劃書 L1 原文列出的「隊別分軌檢視」「拖曳調整日期回寫賽事」「衝突偵測」與 L3／L4
一起排進 `S2-6`（「行事曆進階」）。本輪只做 **L1 合併讀取**（把 `matches` 與
`calendar_custom_events` 換算成同一種形狀回傳）與 **L2 自建事件 CRUD**，不做分軌並排、拖曳改期、
衝突警示、訂閱與匯出——這是既有工作分解，不是本輪重新裁量，任務指示本身也是照這個切法派工。

### 資料庫：既有 DDL 已經備妥，只補一個欄位與一個 CHECK 約束

`calendar_custom_events`／`calendar_custom_events_i18n`／`calendar_event_teams`／
`calendar_event_exceptions`／`event_types`／`event_types_i18n` 六張表在 S0 系列就已經是完整 DDL
（`db/club-schema.sql`「4.10 L 行事曆管理」），EF 實體與 `ClubDbContext` 對應也早就 scaffold 好。
本輪異動：

1. **新增欄位** `calendar_custom_events.repeat_until`（`date NULL`）——規劃書 L2「可設定結束日期與
   例外日期」，例外日期已有 `calendar_event_exceptions` 承接，但原始 DDL沒有任何欄位承接「結束
   日期」，`docs/12d-field-audit.md` 也記過這個缺口。
2. **補齊從未約束過的值域** `repeat_rule`：定案為 `weekly`／`biweekly`／`monthly` 三個英文字面值
   （比照 `matches.status`「挑最直白的英文單字」既有風格，規劃書只給中文頻率敘述，沒有給代碼或
   RRULE 格式的技術決定），補上 `CK_calendar_custom_events_repeat_rule`。

```
migration: 20260925055015_AddCalendarCustomEventRepeatUntil
  ALTER TABLE calendar_custom_events ADD [repeat_until] date NULL;   -- EF AddColumn，來自實體模型異動
  ALTER TABLE calendar_custom_events ADD CONSTRAINT CK_calendar_custom_events_repeat_rule
    CHECK (repeat_rule IN ('weekly','biweekly','monthly') OR repeat_rule IS NULL);
```

套用前查證 `calendar_custom_events` 為 0 筆（本輪才第一次接上真實 API），純 DDL 變更，不搭配任何
DML 轉態。完整說明另見 `docs/12-database-schema.md` §12 第 39 點。

**重複規則不 materialize 成事件實例表**——比照「行事曆是彙整層而非資料源」的既有原則，改為讀取
當下依呼叫端要求的日期範圍即時展開（`Common/RecurrenceExpander.cs`），範圍本身已經是呼叫端的
必要輸入（月曆檢視一次看一個月、公開列表也有 `from`／`to`），迭代次數天然有界（防呆上限 400 次）。

**種子資料**：`event_types` 種六個起始分類（記者會／簽名會／球迷見面會／公開訓練／休館公告／
其他，不帶 `club_id`，兩俱樂部共用）——L3「賽事類型維護」正式的 CRUD 管理畫面留給 `S2-6`，這裡
只種最小可行的起始字典，讓 L2 建立事件時有分類可選，比照既有 `HOME_SECTIONS`／`FAQ_EMBED_SLOTS`
「先種固定字典，完整維護畫面留給後續」的既有先例。

### 後台端點（新增檔案 `Features/AdminCalendar`）

| 方法與路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET /api/v1/admin/{club}/calendar/events` | `calendar.view` | L1 總覽：合併 `matches`＋`calendar_custom_events`。`from`／`to`（預設本月，上限 366 天）、`team`（球隊代碼或 `club`）、`sourceType`（`match`／`custom`）篩選 |
| `GET /api/v1/admin/{club}/calendar/event-types` | `calendar.custom_event.view` | L2 建立／編輯事件用的分類下拉選單（唯讀，L3 正式管理畫面留給 `S2-6`） |
| `GET /api/v1/admin/{club}/calendar/custom-events` | `calendar.custom_event.view` | L2 清單。`team` 篩選 |
| `GET /api/v1/admin/{club}/calendar/custom-events/{id}` | `calendar.custom_event.view` | 單筆詳情 |
| `POST /api/v1/admin/{club}/calendar/custom-events` | `calendar.custom_event.create` | 建立，`multipart/form-data`（`payload`＋選填 `file` 封面圖） |
| `PUT /api/v1/admin/{club}/calendar/custom-events/{id}` | `calendar.custom_event.update` | 更新，同上 multipart 契約 |
| `DELETE /api/v1/admin/{club}/calendar/custom-events/{id}` | `calendar.custom_event.delete` | 硬刪除 |

### 公開端點（新增檔案 `Features/Calendar`，不需要登入）

| 方法與路徑 | 說明 |
|---|---|
| `GET /api/v1/{club}/calendar/events` | 13 賽事行事曆。見下方「兩種查詢模式」 |
| `GET /api/v1/{club}/matches/{id}/ics` | 單場賽事「加入我的行事曆」下載 |

#### 兩種查詢模式（本輪判斷，規劃書沒有把這兩種模式的參數形狀寫清楚）

1. **列表模式**（`from`／`to` 皆未提供）：依 `team` 決定內容——
   - `team=club`：回傳「俱樂部活動」分頁（規劃書「隊別分頁……另有『全部』與『俱樂部活動』」），
     只給公開（`is_public=1`）、**未掛任何球隊**、活動類型公開（或未設類型）的自建事件；
     **重複規則即時展開**（預設區間＝今天起 180 天，例外日排除，`repeat_until` 截止），每個發生日一筆，
     依 `startsAt` 由近到遠，`page`／`pageSize` 分頁；
   - 其餘（含未指定）：回傳**只有賽事**的「賽程 Fixtures／賽果 Results」分頁（`mode=fixtures`
     預設／`results`），支援 `season`／`type`（賽事類型標籤）／`homeAway` 篩選、`page`／`pageSize`
     分頁——這兩個分頁在規劃書原文本來就是賽事的概念（未來／過去），俱樂部活動沒有「賽果」的語意。
2. **月曆模式**（提供 `from`／`to`，上限 366 天）：合併賽事與公開自建事件，L2 重複規則即時展開，
   排序一律由近到遠，不分賽程／賽果，仍可疊加 `team`／`season`／`type`／`homeAway` 篩選。

⚠️ **已知簡化**：「俱樂部活動」列表分頁對重複規則事件只用原始 `starts_at` 排序，不逐一展開每一次
重複發生的時間（那需要月曆模式的 `from`／`to` 才有界限可以展開）。前台若要看到某個重複活動「下
一次」的確切時間，應改用月曆檢視。

### 權限碼與角色指派（`db/seed/generate-club-seed-sql.py`）

新增 5 個權限碼：`calendar.view`（L1，`module_code=L`、`submodule_code=L1`、`domain=calendar`）、
`calendar.custom_event.view/create/update/delete`（L2，`submodule_code=L2`）。依主站規劃書 §6
矩陣「行事曆」欄逐列展開——**這一欄沒有任何角色是「—」**，全部角色都至少能看到 `calendar.view`：

| 角色 | 矩陣格 | 權限碼 |
|---|---|---|
| 系統管理員 | ✔全 | 全部 5 碼（`PERMISSIONS` 清單自動展開） |
| 內容編輯／公關媒體 | 自建事件 | `calendar.view`＋`calendar.custom_event.*` 全給 |
| 競技／球隊管理 | 賽事事件 | 只給 `calendar.view`——這一格對應的是既有 `team.match.*`（C4），不是本模組權限碼 |
| 學院／課程管理 | 梯隊賽事 | 只給 `calendar.view`——理由同上，`team.match.*` 已是 `academy_only` |
| 商務／贊助、客服／行政、檢視者 | 唯讀 | `calendar.view`＋`calendar.custom_event.view` |
| 合作球隊管理 | 自家事件 | `calendar.view`＋`calendar.custom_event.*`（`scope_type=own_clubs`） |
| 翻譯人員 | 僅翻譯欄位 | 不指派（見下方「規劃書沒寫清楚」第 3 點） |

### 🔴 為什麼讀取端不套用 `TeamRowScope`（`own_teams` 盤點結果）

S1-8 把 `own_teams` 這個 `scope_type` 保留給「等 L 模組真的需要時再指派」。本輪盤點後判斷**不需要**：
`matches` 本身已經是 `GET /api/v1/{club}/schedule`（任何人不需要登入就能看到的公開資訊）——行事曆
總覽只是換一種畫面（月曆／列表）呈現同一份資料，不會因為多了「總覽」這個入口就變成需要列級限制
的敏感資料。規劃書「行事曆權限採跟隨來源模組」講的是**編輯**哪些事件（學院管理者可調整所屬梯隊
賽程，但不能改一線隊賽程），這條限制已經由既有 `team.match.*`＋`academy_only`（S1-8）承接；
`calendar.custom_event.*` 拿到的角色在種子資料裡也都是 `all`（或效果等同 `all` 的 `own_clubs`），
沒有一個角色需要「只能碰特定球隊的自建事件」。`own_teams` 依然是**已實作但未被任何內建角色使用**
的能力，等 `S2-6` 真的做拖曳改期回寫 `Match` 時才有「改到不該碰的球隊」的風險，那時再指派即可，
見 `docs/14-invariants.md` 對應段落。

### 時區換算：全站僅 `.ics` 這一處

`matches.match_on`（`date`）＋`kickoff`（`nvarchar(8)`）依 `docs/12` §12 第 31 點是「當地牆上時間」
的展示值，本俱樂部主場都在台灣，`.ics` 輸出把牆上時間視為 `Asia/Taipei`（UTC+8，無夏令時間）換算
成 UTC。`calendar_custom_events.starts_at`／`ends_at` 不在這條規則內——那兩欄是一般 `datetime2(3)`，
依 §1 型別詞彙表本來就已經是 UTC 時間戳，本輪只實作賽事的 `.ics`（規劃書明確要求單一賽事），L2
自建事件的 `.ics` 留給日後有實際需求時比照 `Features/Calendar/CalendarIcsRepository.cs` 的模式擴充。
`.ics` 沒有儲存賽事時長，比照一般足球比賽（含中場）估算 2 小時當 `DTEND`，純粹用於 `.ics` 輸出，
不影響任何資料庫欄位或其他端點的回應。

### 🔴 修正：`generate-club-seed-sql.py` 缺少 `SET QUOTED_IDENTIFIER ON`（E-59）

本輪套用種子資料時撞到 `Msg 1934`——`form_fields` 的篩選唯一索引（S1-10 新增）要求 session 層級
`QUOTED_IDENTIFIER` 為 `ON`，`generate-charity-seed-sql.py` 早就補過這道防線，`club` 版的腳本
沒有比照補上。已在檔頭補齊 `SET ANSI_NULLS ON;`／`SET QUOTED_IDENTIFIER ON;`，完整記錄見
`docs/18-work-errors.md` `E-59`。

### 補種子測試帳號缺口（任務一，`-login` 變體）

`customer.service@tcrfc.test`／`pr.media@tcrfc.test`（S1-9）與 `business.sponsorship@tcrfc.test`
（S1-10）三個帳號跟 `academy.manager@tcrfc.test` 同一種問題——`two_factor_enabled=1` 但沒有真實
密鑰，無頭瀏覽器／端對端驗收無法真的完成 `/login`。比照既有 `academy.login`／`clean.login` 的
「-login」變體做法，各開一個 `two_factor_enabled=0` 的孿生帳號（角色與俱樂部授權逐一對應本尊，
沿用 `content.editor` 的雜湊）：`customer.service.login@tcrfc.test`／`pr.media.login@tcrfc.test`／
`business.sponsorship.login@tcrfc.test`（密碼皆 `ContentEditor@123`）。已補進「種子測試帳號」表
（含先前遺漏的 `academy.login@tcrfc.test` 一併補上）與腳本的密碼明文清單。**只新增帳號、角色
指派與俱樂部授權，未執行任何實際登入驗收**——這是後端補種子資料的任務，端對端驗收屬於
`apps/admin` 那條線，這批帳號能不能真的完成 `/login` 尚未實測，回報供 `frontend-architect` 那輪
使用時自行驗證。

### 改了哪些檔案

新增：
- `Common/RecurrenceExpander.cs`／`Common/IcsBuilder.cs`
- `Features/AdminCalendar/`（`AdminCalendarDtos.cs`／`AdminCalendarExceptions.cs`／
  `AdminCalendarRequestForm.cs`／`AdminCalendarOverviewRepository.cs`（Dapper，L1）／
  `AdminCalendarCustomEventsRepository.cs`（EF Core，L2）／`AdminCalendarEndpoints.cs`）
- `Features/Calendar/`（`CalendarDto.cs`／`CalendarRepository.cs`（Dapper，公開合併讀取）／
  `CalendarIcsRepository.cs`（單場賽事 `.ics`）／`CalendarEndpoints.cs`）
- `Data/Migrations/20260925055015_AddCalendarCustomEventRepeatUntil.cs`
- `Tcrfc.Api.Tests/`（`RecurrenceExpanderTests.cs`／`IcsBuilderTests.cs`／`AdminCalendarTests.cs`／
  `CalendarPublicTests.cs`）

修改：
- `Data/EfEntities/CalendarCustomEvent.cs`（新增 `RepeatUntil`）、`Data/ClubDbContext.cs`（對應
  屬性設定）
- `Features/Uploads/UploadSlotPolicy.cs`（新增 `calendar_custom_events.cover` 插槽）
- `Common/ApiExceptionHandler.cs`（新增例外對照）
- `Features/AdminMatches/AdminMatchesRepository.cs`（既有 4 處 `InvalidateAsync("schedule", ...)`
  各自補上 `InvalidateAsync("calendar", ...)`——賽事資料異動也要讓合併讀取的快取失效）
- `Program.cs`（DI 註冊、路由掛載）
- `db/club-schema.sql`（`calendar_custom_events` 新增 `repeat_until` 欄位與 CHECK 約束）
- `db/seed/generate-club-seed-sql.py`（`calendar.*` 權限碼與角色指派、`event_types` 六個起始
  分類、三個 `-login` 測試帳號、檔頭補 `SET QUOTED_IDENTIFIER ON`）

### 契約變更

- 新增 5 個權限碼（`calendar.*`），DML 已套用到本機 `tcrfc_club_dev`（`./db/seed/apply-seed.sh`）。
- 新增 migration `AddCalendarCustomEventRepeatUntil`，已套用到本機 `tcrfc_club_dev`
  （`dotnet ef database update --context ClubDbContext`）。
- 新增 3 個測試帳號（`customer.service.login`／`pr.media.login`／`business.sponsorship.login`，
  皆 `@tcrfc.test`，密碼 `ContentEditor@123`）。

### 規劃書沒寫清楚、本輪自行判斷的地方

1. **13 賽事行事曆的公開查詢參數形狀規劃書沒有寫死**——本輪定案「列表模式（賽程／賽果／俱樂部
   活動三種分頁）＋月曆模式（`from`／`to`）」兩種模式，見上方「兩種查詢模式」整節的完整理由。
2. **`own_teams` 這個 `scope_type` 盤點後仍未指派給任何角色**——S1-8 保留給 L 模組使用，本輪
   實際做出 L1／L2 後判斷讀取端不需要（賽事本身已公開），寫入端目前拿到權限碼的角色也都不需要
   球隊層級限制，回報供 `S2-6`（拖曳改期真正回寫 `Match`）時視需求指派，見上方完整說明。
3. **翻譯人員（`translator`，`scope_type=translate_only`）本輪同樣不指派**——跟其餘模組保持
   一致的既有判斷（「僅翻譯欄位」全系統目前沒有任何模組真的做出欄位級強制），不是本輪新增的
   判斷，是延續既有慣例。
4. **`calendar_custom_events.repeat_rule` 值域與 `repeat_until` 欄位是本輪新增的技術決定**——
   規劃書只給「每週／每兩週／每月，可設定結束日期」的文字敘述，沒有給代碼或 RRULE 格式的決定，
   比照 `matches.status` 既有先例定案為三個英文字面值＋新增 `date` 欄位，不是規劃外新增功能。
5. **重複規則不 materialize 成事件實例表，改為讀取當下即時展開**——`docs/12` 明文「行事曆是
   彙整層不是資料源」，複製一份重複規則的展開結果進資料庫等於又多一個真實來源；範圍有界
   （最長 366 天）使得即時展開的效能可接受，不需要背景工作預先產生。
6. **「俱樂部活動」列表分頁對重複規則事件只用原始 `starts_at` 排序，不做精確的「下一次發生」
   計算**——這需要無界的展開（列表分頁沒有 `from`／`to` 上限），與「重複規則有界展開」的設計
   原則衝突，判斷這個精確度留給月曆模式，列表分頁只需要「大致排序」。
7. **L2 自建事件的 `.ics` 下載本輪未實作**——規劃書明確要求的是「單一賽事下載 `.ics`」，自建
   事件的加入行事曆能力規劃書沒有明文要求（「動作按鈕」欄位表只列在賽事卡片），本輪依範圍縮減
   原則不多做，架構上（`CalendarIcsRepository`）已經預留擴充空間，日後有需求可直接比照擴充。
8. **`GET /api/v1/admin/{club}/calendar/event-types` 只有唯讀端點，沒有 L3 的完整 CRUD 管理
   畫面**——比照 `STATUS.md` 既定的範圍切法（L3 排進 `S2-6`），這裡只提供 L2 建立事件時選分類
   需要的最小可行讀取端點，種子種了六個起始分類，不是規劃外縮減。
9. **`calendar_event_teams` 的隊別關聯不做 `TeamRowScope` 授權檢查，只做「這些球隊是不是屬於
   本俱樂部」的資料正確性檢查**——見上方「為什麼讀取端不套用 `TeamRowScope`」整節，沒有任何
   角色需要這種列級限制。

### 未做的部分（`L1` 拖曳改期／衝突偵測、`L3`／`L4`，`S2-6`）

隊別分軌並排檢視、拖曳調整日期回寫 `Match`（含改期通知）、同場地或同梯隊時段衝突偵測、賽事類型
與隊別分類的正式管理畫面（含圖示挑選）、iCal 訂閱網址管理與訂閱數統計、指定期間 CSV／`.ics` 匯出、
整季 CSV 批次匯入，全部沒有動，`STATUS.md` 已排進 `S2-6`。

### 測試

`Tcrfc.Api.Tests/RecurrenceExpanderTests.cs` 新增 9 項（不重複事件的範圍內外判斷、每週／每兩週／
每月展開、月底日期夾到目標月最後一天、例外日期排除、`repeat_until` 截止、未知重複規則值不丟例外）；
`Tcrfc.Api.Tests/IcsBuilderTests.cs` 新增 5 項（基本欄位、全天事件 `VALUE=DATE`、特殊字元逸出、
取消狀態、無地點說明時不輸出對應欄位）；`Tcrfc.Api.Tests/AdminCalendarTests.cs` 新增 11 項（權限
矩陣 3 項、L2 CRUD 成功案例與驗證反例各 1 項、跨俱樂部 404、事件分類清單、L1 合併總覽含真實
賽事與自建事件 1 項、每週重複展開出多次 1 項、查詢範圍上限 1 項）；
`Tcrfc.Api.Tests/CalendarPublicTests.cs` 新增 7 項（賽程／賽果分頁各自的時間過濾、俱樂部活動分頁
排除私密活動、月曆模式合併賽事與公開自建事件並排除私密活動、只給 `from` 或 `to` 其中一個回 400、
單場賽事 `.ics` 下載格式正確含時區換算、不存在的賽事 `.ics` 回 404）。

```
dotnet test Tcrfc.Api.Tests --filter "FullyQualifiedName~AdminCalendarTests|FullyQualifiedName~CalendarPublicTests|FullyQualifiedName~RecurrenceExpanderTests|FullyQualifiedName~IcsBuilderTests" --no-build
已通過! - 失敗: 0，通過: 31，總計: 31（連跑 3 次，每次都是 0 失敗）

dotnet test Tcrfc.Api.Tests --no-build
已通過! - 失敗: 0，通過: 437，總計: 437（既有 406 ＋ 本輪新增 31；跑前跑後各執行一次
./db/seed/reset-admin-accounts.sh，過程中曾撞到 3 項 AdminAuthTests 失敗——clean.login@tcrfc.test
的密碼／2FA 狀態被同時進行的前端端對端驗收弄髒，重設後全數轉綠，跟本輪程式碼改動無關，同
S1-8／S1-9／S1-10 皆記錄過的既有現象）

dotnet test Tcrfc.Api.Tests --filter "FullyQualifiedName~ArchitectureTests" --no-build
已通過! - 失敗: 0，通過: 1

dotnet ef migrations has-pending-model-changes --context ClubDbContext
No changes have been made to the model since the last migration.
```

`apps/admin`／`apps/web` 的 `npm run lint` 皆通過（0 errors；`apps/admin` 全數通過含禁用詞／對比
度／EditView 響應式檢查；`apps/web` 既有 539 個 warning 與本輪無關，未觸碰任何前端檔案）。

**手動驗收**（本機 `dotnet run`，`curl`）：公開合併行事曆列表模式（`team=club`）、月曆模式
（`from`／`to`，實際回傳三場真實種子賽事，含隊別、場地、賽事系列名稱）、單場賽事 `.ics` 下載
（實際下載並確認 `DTSTART`／`DTEND`／`SUMMARY`／`LOCATION` 皆正確，時區換算 19:00 台灣時間
正確轉為 11:00 UTC）逐項打過。後台端點僅透過整合測試（`TestAdminTokens` 直接簽權杖）驗證，未
另外走真實 `/login` HTTP 往返——理由與既有大多數後台測試帳號相同（`two_factor_enabled=1` 無
真實密鑰，見「種子測試帳號」表），本輪新增的三個 `-login` 變體帳號**能否真的完成 `/login` 尚未
實測**，如上方「補種子測試帳號缺口」段所述。

---

## E1a：E1 夥伴／E2 贊助／E3 提案與 Lead／B5 慈善／B6 媒體專區／C5 榮譽與里程碑（2026-09-30，`backend-engineer`）

主站規劃書 §4.5 E1–E3、§4.2 B5／B6、§4.3 C5（後台），產出前台 09 合作夥伴與贊助、11 慈善與社會影響、7.8 媒體專區、02 關於（榮譽與里程碑）。
沿用既有架構：`AdminClubScope`／`IAdminClubAuthorizer`、權限碼、`ApiExceptionHandler`、`multipart` 圖片欄位直傳、EF 寫入＋Dapper 公開讀取＋`IQueryCache` 寫入失效。
**新增元件**：`Common/AdminExceptions.cs`（`AdminValidationException` 400／`AdminConflictException` 409／`SharedContentReadOnlyException` 403）、
`Common/AdminInput.cs`（輸入驗證小工具）、`Features/Uploads/{AdminMultipartForm,UploadTransaction}.cs`（多欄位上傳補償交易）、
`Documents/`（PDF／ZIP 檔案儲存，公開＋私有兩個容器）。**給畫面的人：只讀這一節就能串接，不需要看程式碼。**

### 通則（六個模組全部適用）

| 項目 | 規定 |
|---|---|
| 路徑與授權 | 後台一律 `/api/v1/admin/{club}/…`，`Authorization: Bearer <存取權杖>`。未登入 401；帳號對該俱樂部沒有授權或沒有該權限碼 403；跨俱樂部的 id 一律 **404**（不洩漏存在與否）。 |
| 錯誤格式 | `application/problem+json`：`{ "title", "status", "detail", "instance" }`。**`detail` 是日常中文，可直接顯示給使用者**（不含資料表名／欄位名／權限碼）。400＝輸入有誤（`title`＝「輸入內容有誤」）；409＝重複或仍被引用（`title` 依情況，如「網址名稱重複」「團體仍被引用」）；403＝沒有權限（`title`＝「沒有權限」）或共同內容唯讀（`title`＝「共用內容唯讀」）。 |
| 雙語內容 | `content: { zh: {…}, en?: {…} }`。**`zh` 必填**（名稱／標題欄位必填），`en` 可省略。**PUT 是整份取代**：省略 `en` ＝ 移除既有英文版；英文版的名稱／標題空白也視為沒有英文版。回應的 `zh`／`en` **不做語系回退**（原封回傳，`en` 沒有時為 `null`）。 |
| 網址名稱 `slug` | 全部選填：省略＝自動產生（優先用英文名稱轉小寫連字號，否則 `前綴-8位隨機字元`）。有填時格式只能是小寫英文字母、數字與連字號，重複 409（同一俱樂部內唯一）。PUT 省略 `slug` ＝ 維持不變。介面文案請稱「網址名稱」。 |
| 日期 | `DateOnly` 一律 `yyyy-MM-dd`；時間戳為 UTC ISO 8601。 |
| 圖片欄位 | 建立／更新含圖片者是 **`multipart/form-data`**：欄位 `payload`（JSON 文字，camelCase）＋各端點宣告的檔案欄位。**選檔不上傳、儲存才上傳**（§4.0）；JPG／PNG／WebP、單檔 ≤ 10 MB、伺服器一律重新編碼為 WebP（長邊 ≤ 2560px、去 EXIF）。回應內每個圖片欄位有三個屬性：`xxxKey`（內部鍵，不要顯示）、`xxxUrl`（主檔完整網址）、`xxxThumbUrl`（160px 縮圖，僅列表回應）。圖片欄位語意：**新檔案＝換圖**（舊物件寫入成功後才刪）、`removeXxx: true`＝移除、都沒有＝維持不變；**同時給新檔案與 `removeXxx` → 400**。圖片錯誤（格式不支援／太大／空檔）400，`title`＝「圖片無法處理」。 |
| 檔案欄位 | 新聞稿、品牌識別包、贊助提案是 **PDF 或 ZIP**（以檔頭判斷不看副檔名）、單檔 ≤ 50 MB，錯誤 400，`title`＝「檔案無法處理」。 |
| 關聯陣列 | `packageIds`／`articleIds`／`partnerIds`／`sponsorIds`：**省略（null）＝維持不變；空陣列 `[]`＝清空；有值＝整份取代**。含不存在或別的俱樂部的 id → 400。 |
| 排序 | 各清單的 `PUT …/order`，body `{ "ids": ["…"] }`：清單內的 id 依序排最前（`sortOrder` 重排為 0,1,2…），未列出的維持相對順序在其後；重複或不存在的 id → 400；成功 204。 |
| 批次 | `POST …/batch/*` body `{ "ids": [...] }`（1–200 筆）→ `{ "updatedCount": n, "skipped": [{ "id", "reason" }] }`。能處理的處理、不能處理的列進 `skipped`（不是全有全無）。 |
| 共同內容 | `club_id` 可為空的表（B5 四張、B6 一張）：清單與詳情會一併列出共同列（`isShared: true`），但**編輯／刪除一律 403**（`title`＝「共用內容唯讀」）。建立一律歸屬呼叫端當下的俱樂部。 |
| 分頁 | 有分頁的清單回 `{ items, page, pageSize, totalCount, totalPages }`，`page`／`pageSize` 查詢參數，預設 20、上限 100。其餘清單直接回陣列。 |
| 快取 | 每次寫入會讓對應公開端點的快取立即失效（不需要前端處理）。 |

### 權限碼與角色矩陣（`db/seed/generate-club-seed-sql.py`，`role_permissions` 已種入）

| 權限碼 | 用途 | 系統管理員 | 商務／贊助 | 內容編輯 | 公關／媒體 | 競技／球隊 | 學院／課程 | 檢視者 | 合作球隊管理（僅自家） |
|---|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| `business.partner.*`（view／create／update／delete） | E1 夥伴 | 全 | 全 | 唯讀 | 唯讀 | — | — | 唯讀 | 全 |
| `business.sponsor.*` | E2 贊助商＋贊助活動 | 全 | 全 | 唯讀 | 唯讀 | — | — | 唯讀 | 全 |
| `business.sponsor_package.*` | E2 贊助方案 | 全 | 全 | 唯讀 | 唯讀 | — | — | 唯讀 | 全 |
| `business.proposal.*` | E3 提案與檔案 | 全 | 全 | 唯讀 | 唯讀 | — | — | 唯讀 | 全 |
| `business.lead.view`／`update` | E3 Lead 名單（含個資） | 全 | ✔ | — | — | — | — | — | ✔ |
| `business.lead.export`（🔴 受限） | Lead 名單 CSV | ✔ | ✔ | — | — | — | — | — | — |
| `charity.content.*` | B5 團體／計畫／事蹟／數據 | 全 | 唯讀 | 全 | 全 | — | — | 唯讀 | — |
| `charity.setting.view`／`update` | B5 捐款導流與參與方式 | 全 | 唯讀 | ✔ | ✔ | — | — | 唯讀 | — |
| `content.press.*` | B6 媒體專區 | 全 | — | 全 | 全 | — | — | 唯讀 | view／create／update |
| `team.achievement.*` | C5 榮譽（**帶球隊列級授權**） | 全 | 唯讀 | 唯讀 | 唯讀 | 全 | 全（`academy_only`） | 唯讀 | 全 |
| `team.milestone.*` | C5 里程碑 | 全 | 唯讀 | 唯讀 | 唯讀 | 全 | — | 唯讀 | 全 |

角色與權限的中文對照由 J2 畫面依 `permissions.name_zh` 顯示（例：「檢視合作夥伴」「匯出提案下載名單」）。`GET /api/v1/admin/auth/me` 回傳目前帳號持有的權限碼清單，畫面用它決定要不要顯示按鈕。

---

### E1 夥伴 `/api/v1/admin/{club}/partners`（產出前台 09.1、首頁 Logo 牆、頁尾）

| 方法 路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET /partners?partnerType=&keyword=` | `business.partner.view` | 清單（陣列，依 `sortOrder`），`keyword` 搜網址名稱與名稱 |
| `GET /partners/types` | `business.partner.view` | `{ standardTypes: ["策略夥伴","國際夥伴","訓練夥伴","教育夥伴","品牌夥伴"], usedTypes: [...] }`，供類型下拉（可選標準類型，也允許該俱樂部自訂，如藍鯨的「指導單位」） |
| `GET /partners/{id}` | 同上 | 詳情，找不到 404 |
| `POST /partners` | `business.partner.create` | **multipart**：`payload` ＋ 選填檔案欄位 `logoDark`（深色底 Logo）、`logoLight`（淺色底 Logo）。201 |
| `PUT /partners/{id}` | `business.partner.update` | multipart，同上；`payload` 內 `removeLogoDark`／`removeLogoLight` 移除 Logo |
| `DELETE /partners/{id}` | `business.partner.delete` | 204；Logo 物件、課程夥伴關聯、慈善計畫關聯一併清除 |
| `PUT /partners/order` | `business.partner.update` | 排序（見通則） |

**payload（建立與更新同形）**：`slug?`、`partnerType`（必填，≤32 字，自由文字）、`country?`（≤32）、`startOn?`／`endOn?`（合作期間，結束不可早於開始）、`websiteUrl?`（http／https 完整網址）、`showInFooter`、`showOnHome`、`sortOrder`（預設 0）、`content.zh.name`（必填，≤128）／`content.zh.content?`（合作內容）、`content.en?`、`removeLogoDark?`、`removeLogoLight?`。
**回應**：清單項 `{ id, slug, partnerType, country, startOn, endOn, websiteUrl, showInFooter, showOnHome, sortOrder, logoDarkKey/Url/ThumbUrl, logoLightKey/Url/ThumbUrl, nameZh, nameEn, isActive（合作期間涵蓋今天；沒填起訖視為進行中）, updatedAt }`；詳情多 `zh`／`en`（`{ name, content }`）、`createdAt`。
**錯誤**：400（夥伴類型空白、網址格式錯、期間顛倒、名稱空白、圖片問題）、409（網址名稱重複）、404。

### E2 贊助商／贊助方案／贊助活動（產出前台 09.2、09.4）

**贊助商** `/api/v1/admin/{club}/sponsors`

| 方法 路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET /sponsors?tier=&contractStatus=&keyword=` | `business.sponsor.view` | 陣列。`contractStatus`：`alert`（已到提醒日）／`expired`（合約已結束）／`active`（進行中，含 alert），其他值 400 |
| `GET /sponsors/{id}` | 同上 | 詳情含 `packages`（已勾選方案）、`articles`（贊助故事，含標題與狀態） |
| `POST /sponsors` | `business.sponsor.create` | **multipart**：`payload` ＋ `logoDark`／`logoLight`。201 |
| `PUT /sponsors/{id}` | `business.sponsor.update` | multipart；`removeLogoDark`／`removeLogoLight` |
| `DELETE /sponsors/{id}` | `business.sponsor.delete` | 204；活動與圖集、Logo 物件一併清除 |
| `PUT /sponsors/order` | `business.sponsor.update` | 排序 |

**payload**：`slug?`、`tier`（必填：`主贊助`／`官方贊助`／`支持夥伴`，中文字面）、`contractStartOn?`／`contractEndOn?`（不可顛倒）、`expiryAlertOn?`（到期提醒日，不可晚於合約結束日）、`contactName?`（≤64）／`contactPhone?`（≤32）／`contactEmail?`（Email 格式）、`sortOrder`、`content.zh.name`（必填）／`content.zh.content?`（贊助內容）、`packageIds?`（贊助方案，關聯陣列語意）、`articleIds?`（贊助故事＝關聯文章，只能選本俱樂部或共同的文章）。
**回應**新增 `contractStatus`：`none`（沒填結束日）／`active`／`alert`（已到提醒日且合約尚未結束）／`expired`。**列表畫面用它標示到期提醒**。清單項另有 `packageCount`、`activationCount`。
**贊助商聯絡窗口只出現在後台**，公開端點不輸出。

**贊助方案** `/api/v1/admin/{club}/sponsor-packages`（純 JSON）

| 方法 路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET /sponsor-packages?status=` | `business.sponsor_package.view` | 陣列（`draft`／`published`） |
| `GET /sponsor-packages/{id}` | 同上 | |
| `POST /sponsor-packages` | `business.sponsor_package.create` | 201 |
| `PUT /sponsor-packages/{id}` | `business.sponsor_package.update` | |
| `DELETE /sponsor-packages/{id}` | `business.sponsor_package.delete` | 204，贊助商的方案勾選一併解除 |
| `PUT /sponsor-packages/order` | `business.sponsor_package.update` | 排序 |

**body**：`slug?`、`priceMin?`／`priceMax?`（≥0，上限不可低於下限）、`isPricePublic`（預設 `true`；**`false` 時公開端點完全不輸出價格**）、`sortOrder`、`status`（必填 `draft`／`published`）、`content.zh`：`name`（必填）／`content?`（方案內容）／`benefitList?`（權益清單，純文字一行一項）／`audience?`（適合對象，≤128）。九種方案是規劃書內容，不限制筆數。

**贊助活動（含圖集）** `/api/v1/admin/{club}/sponsors/{sponsorId}/activations`（權限碼沿用 `business.sponsor.view`／`update`）

| 方法 路徑 | 說明 |
|---|---|
| `GET …/activations` | 陣列（日期新到舊）；贊助商不存在或屬於別的俱樂部 404 |
| `GET …/activations/{id}` | |
| `POST …/activations` | JSON：`happenedOn?`、`sortOrder`、`content.zh.title`（必填，≤200）／`content.zh.resultSummary?`（成效摘要）、`content.en?`。201 |
| `PUT …/activations/{id}` | 同上 |
| `DELETE …/activations/{id}` | 204，圖集物件一併清除 |
| `POST …/activations/{id}/images` | **multipart，只有檔案欄位 `file`（一次一張）**；回傳完整活動（201），圖片加在最後 |
| `DELETE …/activations/{id}/images/{imageId}` | 204 |
| `PUT …/activations/{id}/images/order` | 排序，回傳完整活動 |

活動回應：`{ id, sponsorId, happenedOn, sortOrder, zh, en, images: [{ id, imageKey, imageUrl, thumbUrl, imageWidth, imageHeight, sortOrder }], updatedAt }`。

### E3 提案與 Lead（產出前台 9.4 CTA「下載提案簡介」）

**提案** `/api/v1/admin/{club}/proposals`

| 方法 路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET /proposals` | `business.proposal.view` | 陣列：`{ id, title, versionNo, status, locales: ["zh","en"], fileCount, leadCount, updatedAt }`（`leadCount`＝這份提案累計被下載次數，用來比較 A/B 版本） |
| `GET /proposals/{id}` | 同上 | 含 `files: [{ id, locale, versionNo, fileBytes, createdAt }]` |
| `POST /proposals` | `business.proposal.create` | JSON `{ title(必填≤128), versionNo(≥1，預設1), status }`；**建立時不能直接 `published`**（還沒有檔案）。201 |
| `PUT /proposals/{id}` | `business.proposal.update` | 同上；**沒有任何檔案的提案不能改成 `published`**（400） |
| `DELETE /proposals/{id}` | `business.proposal.delete` | 204；檔案物件刪除，**已產生的 Lead 保留**（其 `proposalId` 變為空） |
| `POST /proposals/{id}/files` | `business.proposal.update` | **multipart**：`payload` `{ locale: "zh"｜"en", versionNo? }`（省略＝沿用提案版本號）＋ `file`（PDF 或 ZIP，≤50MB）。同提案同語言同版本重複 → 409。201，回傳完整提案 |
| `DELETE /proposals/{id}/files/{fileId}` | `business.proposal.update` | 已發布提案的最後一份檔案不能刪（400，請先改回草稿）；回傳完整提案 |
| `GET /proposals/{id}/files/{fileId}/download` | `business.proposal.view` | 後台預覽／下載（串流，不受前台表單關卡限制） |

提案檔放**私有容器**，沒有公開網址；訪客必須填表單才會拿到限時連結（見下方公開端點）。「設定下載表單欄位」由 G1 表單設計器維護表單代碼 `proposal_download`，本模組不重複提供。

**Lead 名單** `/api/v1/admin/{club}/proposal-leads`（Lead 就是 `form_code = proposal_download` 的詢問，不另建表；含個資）

| 方法 路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET /proposal-leads?proposalId=&status=&keyword=&dateFrom=&dateTo=&page=&pageSize=` | `business.lead.view` | 分頁。項目：`{ id, company, name, email, proposalId, proposalTitle, sourcePath, utmSource, utmCampaign, status, assigneeAdminUserId, tags, createdAt }`（誰下載、公司、時間、來源頁面） |
| `GET /proposal-leads/{id}` | 同上 | 詳情多 `proposalVersionNo`、`internalNote`、`updatedAt` |
| `PUT /proposal-leads/{id}` | `business.lead.update` | 標記跟進：`{ status（必填：新進／處理中／已回覆／已結案／無效）, assigneeAdminUserId?, internalNote?, tags?(≤255) }` |
| `GET /proposal-leads/assignable-users` | `business.lead.update` | 指派負責人選單：`[{ id, displayName }]`（該俱樂部有效授權且持有 `business.lead.update` 的帳號＋系統管理員）。指派給不在這份清單的人 → 400 |
| `GET /proposal-leads/export?…同篩選` | `business.lead.export`（🔴 受限） | CSV（UTF-8 BOM，欄位標題中文：公司、姓名、Email、下載的提案、來源頁面、UTM 來源、跟進狀態、標籤、下載時間） |

### B5 慈善與社會影響 `/api/v1/admin/{club}/charity/…`（產出前台 11.1–11.4、球迷捐款導流）

權限碼：內容四項共用 `charity.content.view／create／update／delete`；導流設定 `charity.setting.view／update`。四張主檔表 `club_id` 可為空（共同列唯讀，見通則）。**慈善是磐石主站單元，藍鯨不設**（藍鯨規劃書 §1.3）：合作球隊管理角色沒有慈善權限；若系統管理員切到藍鯨站台操作，資料會建在藍鯨名下但前台不顯示。

**公益團體** `/charity/organizations`：`GET`（陣列，`?keyword=`）／`GET /{id}`／`POST`（multipart：`payload` ＋ 選填 `logo`）／`PUT /{id}`（multipart，`removeLogo`）／`DELETE /{id}`（**仍被慈善計畫或事蹟引用 → 409**，訊息含筆數）。
payload：`slug?`、`websiteUrl?`（http／https）、`contactName?`（≤64）、`contactPhone?`（≤32）、`content.zh.name`（必填）／`content.zh.intro?`、`content.en?`、`removeLogo?`。詳情多 **`programs`／`records`（合作紀錄，唯讀彙整：這個團體受贈的計畫與事蹟）**；清單項有 `programCount`／`recordCount`。

**慈善計畫** `/charity/programs`：`GET ?status=&keyword=&page=&pageSize=`（分頁，排序＝置頂優先→排序值→開始日新到舊）／`GET /{id}`／`POST`（multipart：`payload` ＋ 選填 `cover` 封面）／`PUT /{id}`（multipart，`removeCover`）／`DELETE /{id}`（**仍被事蹟或影響力數據引用 → 409**）。
payload：`slug?`、`charityId`（必填，受贈公益團體）、`startOn?`／`endOn?`、`status`（必填 `draft`／`published`）、`sortOrder`、`isPinned`（置頂）、`content.zh`：`name`（必填，≤128）／`targetAudience?`（對象，≤200）／`content?`（**緣起與內容：區塊編輯器整段 JSON 字串，只驗證是合法 JSON**）／`donationContent?`（捐助內容）、`content.en?`、`partnerIds?`（贊助夥伴＝E1 夥伴）、`sponsorIds?`（贊助夥伴＝E2 贊助商）、`articleIds?`（關聯報導＝7.7 新聞）、`removeCover?`。
回應：`progress`（**`ongoing`／`completed` 由期間推導**：沒填結束日或結束日尚未到＝進行中；`status` 是發布狀態，兩者不同）、`partners`／`sponsors`／`articles`（`[{ id, slug, title }]`）、`images`（活動圖片藝廊，不含封面）。
**圖集**：`POST /{id}/images`（multipart，`file` 單張，201 回完整計畫）／`DELETE /{id}/images/{imageId}`（204）／`PUT /{id}/images/order`（排序，回完整計畫）。

**事蹟紀錄** `/charity/records`：`GET ?charityId=&programId=&year=&keyword=&page=&pageSize=`（分頁，置頂優先→排序值→日期新到舊）／`GET /{id}`／`POST`／`PUT /{id}`／`DELETE /{id}`。
**三項必填**：`charityId`（公益團體）、`content.zh.donationContent`（捐助內容，如「足球 50 顆、訓練背心 100 件」）、**活動圖片**。建立為 multipart：`payload` ＋ **必填 `image`**（主圖，缺少 → 400「事蹟紀錄必須上傳活動圖片」）；更新時 `image` 選填（換主圖，**主圖不可移除**）。其餘 payload：`charityProgramId?`（所屬計畫）、`happenedOn?`、`sortOrder`、`isPinned`、`content.zh.location?`（≤128）／`content.zh.briefDescription?`（簡述）、`content.en?`。
**其他活動圖片（可多張）**：`POST /{id}/images`／`DELETE /{id}/images/{imageId}`／`PUT /{id}/images/order`，同計畫圖集。詳情回應含 `imageKey/imageUrl/imageWidth/imageHeight`（主圖）與 `images`（其餘）。

**影響力數據** `/charity/metrics`（純 JSON）：`GET ?programId=`（陣列）／`GET /{id}`／`POST`／`PUT /{id}`／`DELETE /{id}`。
body：`charityProgramId?`（**可不掛計畫＝全站層級統計項目**）、`value?`（整數）、`isPublic`（**預設 `false`；金額類項目一律預設不公開，要公開必須明確送 `true`**）、`sortOrder`、`content.zh.name`（必填，≤64）／`content.zh.unit?`（單位，≤16，如「人」「場」「元」）、`content.en?`。

**捐款導流與參與方式設定** `/charity/settings`：`GET`／`PUT`（整份取代，未送的欄位＝清空，每個俱樂部各一份）。body／回應：
`{ donationUrl?, donationCta?: { zh, en }, corporateCta?: { zh, en }, corporateUrl?, fanCta?: { zh, en } }`。驗證：`donationUrl` 必須 **https**；設定了 `donationUrl` 時 **`donationCta.zh` 必填且必須包含「台灣足球策略發展協會」**（規劃書 §3.11：CTA 須點明捐款由協會收受，不得讓人誤以為捐給台中磐石），否則 400；`corporateUrl` 是站內路徑（以 `/` 開頭，不可 `//`）或 https 網址；`fanCta` 導向固定為 `donationUrl`。**前台不得寫死捐款網址，一律讀公開端點 `GET /api/v1/{club}/charity/cta`。**

### B6 媒體專區 `/api/v1/admin/{club}/press-resources`（產出前台 7.8）

| 方法 路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET ?resourceType=&status=&keyword=&page=&pageSize=` | `content.press.view` | 分頁；共同列一併列出（`isShared`）。項目：`{ id, slug, isShared, resourceType, status, publishedOn, sortOrder, downloadCount（唯讀，累計下載次數）, fileBytes, coverThumbUrl, titleZh, titleEn, updatedAt }` |
| `GET /{id}` | 同上 | 詳情：`fileKey`、`fileUrl`（後台預覽，部署層未開放容器公開讀取時可能無法直接開）、`coverKey/coverUrl/coverWidth/coverHeight`、`zh`／`en`（`{ title, description }`） |
| `POST` | `content.press.create` | **multipart**：`payload` ＋ **必填 `file`** ＋ 選填 `cover`（封面圖，**高解析圖類別不可傳**）。201 |
| `PUT /{id}` | `content.press.update` | multipart；`file` 選填（換檔）、`cover` 選填、`removeCover`。**類別在「高解析圖」與其他類別間切換時必須同時重新上傳檔案**（400） |
| `DELETE /{id}` | `content.press.delete` | 204，檔案與封面物件一併清除 |
| `PUT /order` | `content.press.update` | 排序（只能排本俱樂部自己的資源） |
| `POST /batch/show`｜`/batch/hide` | `content.press.update` | 批次顯示／隱藏；顯示且沒有發布日期時自動填今天 |
| `POST /batch/type` | `content.press.update` | body `{ ids, resourceType }` 批次改類別；**文件類↔高解析圖不相容，該筆進 `skipped`**（原因中文） |

payload：`slug?`、`resourceType`（必填：`press_release` 新聞稿／`brand_kit` 品牌識別包／`hires_image` 高解析圖）、`status`（必填 `draft` 隱藏／`published` 顯示）、`publishedOn?`、`sortOrder`、`content.zh.title`（必填，≤200）／`content.zh.description?`、`content.en?`、`removeCover?`。
檔案規則：新聞稿與品牌識別包的 `file` 是 **PDF 或 ZIP**（≤50MB，不轉檔）；**高解析圖的 `file` 是圖片**（JPG／PNG／WebP，走圖片通則重新編碼：主檔長邊 ≤2560px、去 EXIF／GPS，並自動產生縮圖，**不另外上傳封面**）。

### C5 榮譽與里程碑（產出前台 02 關於）

**榮譽** `/api/v1/admin/{club}/achievements`（`team.achievement.view／create／update／delete`；🔴 **帶球隊列級授權**：學院／課程管理只能寫學院梯隊的榮譽，把榮譽改掛到範圍外球隊也擋下，403）
`GET ?teamId=&seasonId=&year=`（陣列，年份新到舊）／`GET /{id}`／`POST`／`PUT /{id}`／`DELETE /{id}`（204）。
body：`seasonId`（必填，本俱樂部球季，選單用既有 `GET /api/v1/admin/{club}/seasons`）、`teamId`（必填，本俱樂部球隊，選單可用 `GET /api/v1/admin/{club}/teams`；範圍受限帳號用 `GET …/teams/writable?module=team`）、`year?`（省略＝取球季開始日的西元年）、`competitionName`（必填，≤128）、`placing`（必填，≤32，如「冠軍」）。
回應：`{ id, seasonId, seasonCode, teamId, teamCode, teamNameZh, year, competitionName, placing, updatedAt }`（隊別／球季代號僅供辨識，畫面請顯示球隊名稱）。

**里程碑** `/api/v1/admin/{club}/milestones`（`team.milestone.view／create／update／delete`，不套球隊列級授權）
`GET`（陣列，日期由舊到新）／`GET /{id}`／`POST`（multipart：`payload` ＋ 選填 `image`）／`PUT /{id}`（multipart，`removeImage`）／`DELETE /{id}`。
payload：`happenedOn`（必填）、`sortOrder`、`isVisible`（**是否顯示於前台時間軸**，預設 `true`）、`content.zh.title`（必填，≤200）／`description?`／`imageAlt?`（圖片替代文字，≤200）、`content.en?`、`removeImage?`。
回應：`{ id, happenedOn, sortOrder, isVisible, imageKey, imageUrl, imageThumbUrl, imageWidth, imageHeight, zh, en, updatedAt }`。

---

### 公開讀取端點（不需要登入；`lang=zh|en`，英文缺漏回退中文）

| 端點 | 前台 | 說明 |
|---|---|---|
| `GET /api/v1/{club}/partners?type=&home=&footer=&lang=` | 09.1、首頁 Logo 牆、頁尾 | 陣列。**只列合作期間涵蓋今天的夥伴**；`type` 依類型、`home=true` 只列「首頁曝光」、`footer=true` 只列「頁尾曝光」。項目：`{ id, slug, partnerType, country, startOn, endOn, websiteUrl, showInFooter, showOnHome, sortOrder, name, content, logoDarkUrl, logoLightUrl, charityPrograms: [{ slug, name }] }`（`charityPrograms`＝共同參與的公益計畫，只含已發布的計畫） |
| `GET /api/v1/{club}/sponsors?lang=` | 09.2 | 依等級（主贊助→官方贊助→支持夥伴）排序；**合約已結束的不列**；含 `stories`（贊助故事，已發布文章的 `slug/title/summary`）、`activations`（贊助活動紀錄，含 `images`）、`charityPrograms`。**不輸出聯絡窗口、合約日期、到期提醒** |
| `GET /api/v1/{club}/sponsor-packages?lang=` | 09.4 | 只列已發布的方案；**價格區間只有後台設為公開時才輸出**，否則 `priceMin`／`priceMax` 為 `null` |
| `GET /api/v1/{club}/proposals` | 9.4 CTA | 已發布且至少有一份檔案的提案：`[{ id, title, versionNo, locales }]`（**不含檔案網址**）。前台依 A/B 版本自行挑一份 |
| `POST /api/v1/{club}/proposals/{id}/download-requests` | 9.4 CTA | 限流 `public-submission`（20 次／5 分鐘／IP）。body：`{ company, name, email, consent: true, lang?, sourcePath?, utmSource?, utmCampaign?, website? }`。**建立 Lead（帶提案編號）並回傳 30 分鐘有效的下載連結** `{ downloadPath, expiresAt }`。400（日常中文：請填寫…／Email 格式不正確／請勾選同意條款…）、404（提案不存在或未發布或沒有檔案）、429。`website` 是誘捕欄位：有值就安靜回 `{ downloadPath: null }`，不建 Lead |
| `GET /api/v1/{club}/proposals/downloads/{token}` | 9.4 CTA | 憑上面拿到的路徑下載（串流 PDF／ZIP，`Cache-Control: private, no-store`）。權杖過期、被竄改、換俱樂部、提案改回草稿一律 404 |
| `GET /api/v1/{club}/charity/programs?lang=&page=&pageSize=` | 11.2 列表 | 只列已發布；置頂優先。項目含 `progress`（`ongoing`／`completed`）、`coverUrl`、`charityName` |
| `GET /api/v1/{club}/charity/programs/{slug}?lang=` | 11.2 詳情 | 計畫緣起（`content`，區塊 JSON 字串）、受贈公益團體 `charity`、`donationContent`、`images` 藝廊、`partners`／`sponsors`（含 Logo）、`articles`（關聯報導，已發布） |
| `GET /api/v1/{club}/charity/records?program=<slug>&year=&lang=&page=&pageSize=` | 11.3 | 三項核心資料：`charityName`、`donationContent`、`imageUrl`＋`images`；另有 `happenedOn`、`location`、`briefDescription`、`programSlug`／`programName`。`GET …/charity/records/years` 回有事蹟的年份（年份篩選用） |
| `GET /api/v1/{club}/charity/impact?lang=` | 11.4 | `{ metrics（只含後台標為公開的項目）, charityCount, donationItemCount（事蹟筆數）, regions（服務地區＝事蹟地點）, charities（Logo 牆） }` |
| `GET /api/v1/{club}/charity/cta?lang=` | 11 各頁 CTA | `{ donationUrl, donationCta, fanCta, corporateCta, corporateUrl }`。**捐款網址與文案來自後台 B5 設定**；沒設定網址時 `donationUrl`／`donationCta`／`fanCta` 皆 `null`（前台不顯示球迷捐款按鈕）。藍鯨一律為空 |
| `GET /api/v1/{club}/press?type=&lang=&page=&pageSize=` | 7.8 | 只列已發布；`type`＝`press_release`／`brand_kit`／`hires_image`（其他值 400）。項目含 `downloadPath`、`fileBytes`、`fileExtension`、`coverUrl` |
| `GET /api/v1/{club}/press/{slug}/download` | 7.8 | 限流 `public-light-interaction`。**累計下載次數後 302 轉址到檔案**；未發布或找不到 404 |
| `GET /api/v1/{club}/achievements?team=<隊別代號>&lang=` | 02 榮譽 | `[{ id, year, seasonCode, teamCode, teamName, competitionName, placing }]`，年份新到舊 |
| `GET /api/v1/{club}/milestones?lang=` | 02 時間軸 | 只列「顯示於時間軸」的里程碑，日期由舊到新；`imageUrl`／`imageAlt`／`imageWidth`／`imageHeight` |

公開端點的圖片一律只給完整網址（`xxxUrl`），不給物件鍵。所有快取 key 帶俱樂部與語系維度；跨日相關的（合作期間、合約到期、計畫進行狀態）帶日期維度，不會拖到 TTL 才更新。

### 資料庫綱要異動（`db/club-schema.sql`、migration `AlignSchemaE1a`）

規劃書寫了、原綱要沒有落點的欄位／表，**全部是把規劃書已有的功能落到資料表，不是新增規格**（`docs/12` §14.3 既有先例）：

| 異動 | 理由（規劃書出處） |
|---|---|
| `partners_i18n.content` | E1「合作內容」 |
| 新表 `sponsor_activations`／`sponsor_activations_i18n`／`sponsor_activation_images` | E2「贊助活動（活動名稱、日期、圖集、成效摘要）」，`docs/12d` 早已記為「型別總表缺席」 |
| 新表 `sponsor_articles` | E2「贊助故事：關聯文章」。不用 `article_relations`：那張表由 B2 新聞編輯器整批取代，混入會被清掉 |
| 新表 `charity_program_partners`／`charity_program_sponsors`／`charity_program_articles` | §3.11「慈善計畫可標記贊助夥伴（關聯 E1/E2）」、B5「關聯報導（7.7）」 |
| `charity_programs`／`impact_records` 加 `sort_order`、`is_pinned` | B5「顯示控制：於慈善單元內的排序與置頂」 |
| `impact_metrics.charity_program_id` 改可為空、加 `sort_order`；`impact_metrics_i18n.unit` | B5「影響力數據：名稱、單位、數值、是否公開」；前台 11.4 是「累計統計」，沒有要求統計項目必須掛在某個計畫 |
| `milestones` 加 `image_key`／`image_width`／`image_height`／`is_visible`；`milestones_i18n.image_alt` | C5「里程碑：日期、標題、描述、圖片、是否顯示於時間軸」 |
| `enquiries.proposal_id`（外鍵 `ON DELETE SET NULL`） | 9.4「可 A/B 版本」——Lead 要知道下載的是哪一份提案 |
| `proposals.status` 改 `NOT NULL DEFAULT 'draft'`＋`CK_proposals_status`；`press_resources.resource_type` 加 `CK_press_resources_resource_type` | 值域收斂，同 `AlignSchemaS19Programs` 的既有先例；套用前兩表皆 0 筆 |

本機庫已對 `tcrfc_club` 套用（等同 migration `20260930065939_AlignSchemaE1a`；`dotnet ef migrations add Probe` 驗證為空 migration，模型與 snapshot 同步）。
EF 實體：新增 5 個實體檔（`CharityProgramArticle`／`SponsorActivation`／`SponsorActivationImage`／`SponsorActivationsI18n`／`SponsorArticle`），其餘既有實體與 `ClubDbContext` 只**插入**新屬性與新表設定（未整檔重新 scaffold，理由與注意事項見 `patterns_ef_scaffold_editing` 與 `docs/18-work-errors.md` E-85）。

### 種子（`db/seed/backoffice_seed.py` §34–39，全部【測試】虛構資料，搜尋「【測試】」可找出）

tcrfc：夥伴 5（五種類型各一，1 筆合作期間已結束）、贊助商 3（三個等級；其中 1 筆已到提醒日、1 筆合約已結束）、贊助方案 9（規劃書九種名稱）、贊助活動 2、提案 2（A/B 草稿，無檔案）、Lead 3（`@example.com`）、公益團體 2、慈善計畫 3（1 置頂進行中、1 已完成、1 草稿）、事蹟 2、影響力數據 3（含 1 筆不公開的金額）、捐款導流設定（網址 `charity.example.com`，文案已依規劃書點明協會）、媒體資源 3（**draft＋佔位檔案鍵**，公開端點不會顯示）、榮譽 3。
沒有圖片與真實檔案（沒有可上傳的公開素材）。權限碼 37 個與角色指派已在 `generate-club-seed-sql.py` 種入。

### 規劃書沒寫清楚、本輪自行判斷（保守做法，需要使用者確認的列在「待裁決」）

1. **檔案格式與大小**：規劃書只定義圖片通則。新聞稿／品牌識別包／提案只收 PDF 與 ZIP、≤50MB、不轉檔不掃毒（`Documents/DocumentUploadOptions.cs` 檔頭）。
2. **提案下載的表單關卡**：檔案放私有容器，只有「填表單→拿限時連結」一條路；連結 30 分鐘有效（Data Protection time-limited 權杖，綁定俱樂部與檔案，不查庫）。
3. **Lead 的可見範圍**：Lead 含個資，`business.lead.view` 只給商務／贊助與合作球隊管理（矩陣「唯讀」講的是夥伴與贊助內容），匯出只給商務／贊助。
4. **夥伴／贊助商「現有」的判斷**：夥伴依合作期間、贊助商依合約結束日自動下架（沒填日期＝視為進行中）。
5. **慈善進行狀態**：`ongoing`／`completed` 由期間推導，不另設欄位；`draft`／`published` 是發布狀態。
6. **影響力數據預設不公開**：沒有「是否金額類」欄位，採最保守做法——所有項目預設不公開。
7. **慈善導流文案必須點明「台灣足球策略發展協會」**（§3.11 明文），後端強制驗證（設定了網址就必填、且必含該名稱）。
8. **事蹟沒有草稿狀態**（規劃書沒有為事蹟定義發布流程），建立即前台可見；活動圖片建立時必填。
9. **高解析圖走圖片通則**：主檔長邊 ≤2560px、去 EXIF／GPS，等於「高解析」受 2560px 上限約束。
10. **C5 學院／課程管理**：榮譽給 `academy_only`（有球隊維度），里程碑不給（沒有球隊維度，理由同 C4 積分榜）。

### 待裁決（規劃書沒寫、又會影響客戶看得到的行為；先採上述保守做法）

- **B6 高解析圖是否例外保留原檔尺寸**：現況受 §4.0 通則約束（≤2560px 重新編碼）。若媒體專區要提供真正的原尺寸高解析原檔，需要例外條款（並決定 EXIF／GPS 是否保留）。
- **夥伴類型是否限定五種**：現況允許自訂（藍鯨既有種子是「指導單位」「官方合作夥伴」）；`GET /partners/types` 同時回標準五種與實際用過的。
- **Lead 唯讀角色可見性**：內容編輯／公關媒體／檢視者目前看不到 Lead 名單（個資最小授權），若客戶希望唯讀可看需補權限碼指派。
- **慈善事蹟／團體「共同列」的建立入口**：目前只有 DB 層與超管能產生共同列（俱樂部範圍端點一律建成自家資料），與 `docs/14`「共同內容只有超管能建立」一致，但尚無超管專用的共同內容建立畫面。

### 測試（`Tcrfc.Api.Tests`，新增 6 個檔案、31 項；全套 585／585 通過，含 `ArchitectureTests` 的「公開 DTO 物件鍵欄位須有對應網址」「公開非 GET 須掛限流」）

| 檔案 | 項數 | 涵蓋 |
|---|---:|---|
| `AdminBusinessModulesTests` | 9 | E1／E2／E3：授權矩陣、跨俱樂部 404、驗證與 400／409、排序、到期提醒狀態、關聯陣列語意、提案發布規則、Lead 篩選／更新／指派驗證／匯出受限碼 |
| `AdminCharityTests` | 4 | B5：授權矩陣、完整流程與引用保護（409）、共同列唯讀（403）、導流設定驗證（收受者、https） |
| `AdminPressAndHonorsTests` | 6 | B6 授權與驗證、共同列與批次略過、公開端點；C5 榮譽（球隊列級授權）、里程碑顯示旗標 |
| `AdminBusinessUploadTests` | 5 | **真實 Azurite**：Logo 換圖／移除／刪除時物件清理、媒體資源 PDF／高解析圖／下載累計、提案私有檔的表單關卡與限時連結／Lead 帶提案／竄改與跨俱樂部失效、事蹟必填圖片與圖集、贊助活動圖集與里程碑圖片 |
| `BusinessPublicEndpointsTests` | 6 | 公開端點：合作期間篩選、贊助商排序與不輸出聯絡窗口、價格不公開、慈善計畫／事蹟／影響力數據／導流、關聯陣列語意 |
| `BusinessCacheInvalidationTests` | 1 | **真實 Redis**：夥伴、贊助方案、導流設定寫入後公開端點立即看到新值 |

### 已知限制

- 圖片欄位組目前只有 `_key`／寬高，**沒有雙語 alt 文字**（夥伴／贊助商 Logo 前台以名稱當 alt；里程碑有 `imageAlt`）——與既有 staff／players 同一個既有落差，未在本輪擴張。
- 伺服器端不掃毒、不檢查 ZIP 內容；PDF 只驗檔頭。
- 提案下載沒有「一次性連結」（30 分鐘內可重複下載同一份）；Lead 只在「要求連結」時建立一次。
- 未做 App 端與慈善獨立庫的同步（慈善庫的 `charity_program_refs` 快照不受本輪異動影響）。
- 公開端點使用 Dapper 手寫 SQL，欄位別名與 record 的對應由測試涵蓋；新增欄位時要同步 SQL 與 record（Dapper 對 `date` 欄位一律用 `DateTime` 接再轉 `DateOnly`）。


---

## B1：P4 試訓／K1–K4 會員系統／L3–L4 行事曆進階（2026-09-30，`backend-engineer`）

主站規劃書 §4.4 P4（＋P3 進階）、§4.11 K1–K4、§4.12 L1 進階／L3／L4（後台），對應 `STATUS.md` 的 `S2-4`／`S2-5`／`S2-6`。
沿用 E1a 的全部通則（路徑、錯誤格式、`content: { zh, en? }` 雙語、`PUT` 整份取代、`PUT …/order`、`POST …/batch/*`、分頁形狀、跨俱樂部 id 一律 404，見「E1a」整節的「通則」表），
**本節只寫 B1 新增的規則與每支端點的契約。給畫面的人：只讀這一節就能串接，不需要看程式碼。**

### B1 通則（新增）

| 項目 | 規定 |
|---|---|
| 給畫面顯示的標籤 | 資料庫存英文代碼，回應**同時給代碼與中文標籤**（`tier`／`tierLabel`、`status`／`statusLabel`、`effectiveStatus`／`effectiveStatusLabel`、`deliveryMethod`／`deliveryMethodLabel`…）。篩選一律傳**代碼**；畫面只顯示標籤，不顯示代碼（§4.0）。試訓與課程報名的狀態本來就是中文字面（`待確認`…），照傳照顯示。 |
| **個資遮罩**（K1／K3） | 會員名單一律遮罩（姓名 `王○明`、Email `a***@gmail.com`、電話 `09******78`、生日 `****-**-**`、地址前 6 字＋`***`）。**完整值只有兩條路**：① 會員詳情 `GET …/members/{id}?reveal=true`（需 `member.pii.reveal`，沒有權限 → 403）；② K3 球衣的收件資訊：持有 `member.pii.reveal` 的角色直接看到完整值（出貨要用），其餘遮罩。回應一律有 `isMasked`（這份回應是不是遮罩值）與（詳情）`canReveal`（你有沒有解除遮罩的權限，畫面用來決定要不要顯示「顯示完整資料」按鈕）。**LINE 綁定識別碼、密碼雜湊、QR 憑證字串（token）永遠不出現在任何回應**（只有 `lineBound: true/false`）。 |
| 搜尋不能繞過遮罩 | 沒有 `member.pii.reveal` 的人，`keyword` **只比對會員編號**（否則搜尋結果會變成探測個資的工具）；有權限者才比對姓名／Email／電話。 |
| 資料範圍（兩層） | `Member` 是帳號層（不分俱樂部）、`Membership` 才分俱樂部。**名單預設只列「在目前操作的俱樂部有會籍」的會員**；`crossClub=true` 改為「你有授權的所有俱樂部」（系統管理員為全部、含尚無會籍的帳號）。**無論哪種，回應裡的會籍列只含你有授權的俱樂部**——受限帳號（合作球隊管理）絕對看不到對方俱樂部的任何會籍列；直接打對方會員的 id → 404。 |
| 匯出與敏感操作 | 匯出端點需要受限權限碼（`is_restricted`）＋**必填 `purpose`（用途備註，≤200 字，缺 → 400）**。匯出、解除遮罩、停用／啟用帳號、重產 QR、合併帳號、調整會籍、批次到期、球衣出貨清單都會寫**結構化敏感操作日誌**（帳號、俱樂部、對象、筆數、用途；不含個資）。🔴 本庫依委託方指示沒有日誌表，這是暫行落點，見「待裁決」。 |
| 到期日為準 | 會籍「有效與否」以**到期日**為準：`status = active` 但到期日已過的，回應的 `effectiveStatus` 是 `expired`。篩選與畫面都用 `effectiveStatus`；`status` 欄只是人工覆寫（待確認／取消／批次到期）。 |
| 通知信 | 「會籍開通確認」「候補遞補」「延賽通知」等**本輪一律不寄**：全系統沒有寄信通路（`email_logs` 只是紀錄表）。相關端點回應不含「已通知」語意，`rescheduled.notificationSent` 恆為 `false`。 |

### 權限碼與角色矩陣（`db/seed/generate-club-seed-sql.py`，`role_permissions` 已種入，共 39 碼）

| 權限碼 | 用途 | 系統管理員 | 客服／行政 | 學院／課程 | 內容／競技／商務／檢視者 | 公關／媒體 | 合作球隊管理（僅自家） |
|---|---|:-:|:-:|:-:|:-:|:-:|:-:|
| `program.trial.view／create／update／delete` | P4 場次 | 全 | 唯讀 | 全 | 唯讀 | — | 全 |
| `program.trial_registration.view／create／update` | P4 報名名單 | 全 | 檢視＋處理（無建立） | 全 | 唯讀 | — | 全 |
| `program.trial_registration.export`（🔴 受限） | 試訓名單 CSV | ✔ | — | ✔ | — | — | — |
| `member.account.view`／`create`／`update` | K1 名單、現場建立、處理帳號 | 全 | 全 | — | — | — | 只有 view |
| `member.pii.reveal` | 解除遮罩（會員詳情、K3 收件資訊、匯出時的搜尋比對） | ✔ | ✔ | — | — | — | — |
| `member.export`（🔴 受限） | K1 名單 CSV、續會名單 CSV | ✔ | ✔ | — | — | — | — |
| `member.account.merge`（sysadmin_only） | 合併重複帳號（不可逆） | ✔ | — | — | — | — | — |
| `member.membership.view`／`create`／`update` | K2 會籍、付款紀錄；開通（create）；調整、會員卡、批次到期（update） | 全 | 全 | — | — | — | 只有 view |
| `member.plan.view`／`create`／`update`／`delete` | K2 方案 | 全 | 只有 view | — | — | — | 只有 view |
| `member.setting.view`／`update` | K2 會員編號規則 | 全 | 只有 view | — | — | — | — |
| `member.jersey.view`／`create`／`update` | K3 | 全 | 全 | — | — | — | 只有 view |
| `member.jersey.export`（🔴 受限） | K3 出貨清單 CSV | ✔ | ✔ | — | — | — | — |
| `member.store.*`／`member.benefit.*`（view／create／update／delete） | K4 特約店家、權益對照表 | 全 | 全 | — | — | — | — |
| `calendar.setting.view` | L3 讀取 | ✔ | ✔ | ✔ | ✔（全部） | ✔ | ✔ |
| `calendar.setting.update` | L3 寫入（隊別設定、預設檢視、試訓同步）；**賽事類型只有系統管理員** | ✔ | — | — | 只有內容編輯 | ✔ | ✔ |
| `calendar.subscription.view`／`calendar.export` | L4 訂閱網址與訂閱數／匯出（行事曆是公開資料，不設受限） | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ |

改期／匯入不另開碼：**賽事改期＝`team.match.update` ＋球隊列級授權**（行事曆權限跟隨來源模組）、**自建活動改期＝`calendar.custom_event.update`**、**整季賽程 CSV 匯入＝`team.match.create`**。
檢視 `GET /api/v1/admin/auth/me` 的權限碼清單決定按鈕顯示。合作球隊管理（藍鯨方）第一階段可不指派使用者，但範圍已在資料存取層強制。

---

### P4 試訓場次 `/api/v1/admin/{club}/trials`（產出前台 3.3／4.7／6.3 試訓資訊）

| 方法 路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET /trials?teamId=&status=&from=&to=` | `program.trial.view` | 陣列（依試訓日新→舊） |
| `GET /trials/{id}` | 同上 | 詳情，404 |
| `POST /trials` → 201 | `program.trial.create` | JSON |
| `PUT /trials/{id}` | `program.trial.update` | JSON，整份取代 |
| `DELETE /trials/{id}` → 204 | `program.trial.delete` | **有任何報名 → 409**（改狀態為「已結束」） |
| `GET /trials/{id}/registrations?status=&keyword=&isMember=` | `program.trial_registration.view` | 陣列（依報名先後）。`keyword` 比對編號／姓名／電話／Email |
| `GET /trials/{id}/registrations/{regId}` | 同上 | 詳情（含健康聲明） |
| `POST /trials/{id}/registrations` → 201 | `…registration.create` | 後台代填 |
| `PUT /trials/{id}/registrations/{regId}` | `…registration.update` | 整份覆寫（確認／取消／加入候補／備註） |
| `POST /trials/{id}/registrations/{regId}/promote` | `…registration.update` | 候補 → 已確認（佔名額）；非候補 → 400 |
| `GET /trials/{id}/registrations/export?status=&purpose=` | `…registration.export` | CSV（UTF-8 BOM）。**不含健康聲明**。缺 `purpose` → 400 |
| `GET /trials/{id}/sign-in-sheet` | `…registration.view` | 簽到表資料（畫面直接列印）：只列會到場的人（待確認／已確認／已繳費／完成），**不含健康聲明與備註** |

**場次 payload**：`teamId?`（省略＝俱樂部整體試訓）、`venueId?`、`trialOn`（必填，`yyyy-MM-dd`）、`capacity?`（≥1，省略＝不限）、`deadlineOn?`（不可晚於試訓日）、`status?`（`開放`／`額滿`／`候補`／`已結束`；新增省略＝開放、更新省略＝不變）、`content.zh.audience`（必填，對象說明，≤255）／`content.en?.audience`。
**回應**：清單項 `{ id, teamId, teamCode, teamName, venueId, venueName, trialOn, capacity, enrolledCount, deadlineOn, status, isSignupOpen, syncToCalendar, audienceZh, audienceEn, waitlistCount, updatedAt }`（`isSignupOpen`＝狀態為開放且試訓日與截止日都還沒過）；詳情多 `zh`／`en`（`{ audience }`）、`createdAt`。
**名額連動**：報名狀態 `待確認／已確認／已繳費／完成` 佔名額，`取消／候補` 不佔；用原子 SQL 調整 `enrolledCount`，**達 `capacity` 時「開放」自動轉「額滿」（單向，取消後不會自動轉回，由人工決定）**。後台不擋超額（人為判斷，可直接遞補到超額）。名額不能調到低於已報名人數（400）。
`syncToCalendar` 是 L3 的全站開關的結果（新場次沿用目前開關值），P4 不逐場設定。
**報名 payload（新增）**：`applicantName`（必填）、`phone?`、`email?`、`birthOn?`、`guardianName?`、`guardianPhone?`、`healthDeclaration?`、`note?`、`memberId?`（可為空，非會員可報名）、`status?`（省略＝待確認）；更新同形且 `status` 必填。**報名回應**：清單項 `{ id, registrationNo, memberId, isMember, applicantName, phone, email, birthOn, guardianName, guardianPhone, note, status, createdAt }`、詳情多 `trialId`／`healthDeclaration`／`updatedAt`。報名編號 `TCRFC-yyyyMMdd-XXXXXX`（同課程報名）。
**錯誤**：400（名額／截止日／球隊或場地不存在／狀態值／姓名空白／用途缺）、404（場次不存在或不屬於這個俱樂部）、409（刪除有報名的場次）。

### P3 報名進階（新增於既有 `/api/v1/admin/{club}/registrations`）

| 方法 路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET /registrations?sessionId=&status=&programId=&keyword=&isMember=&dateFrom=&dateTo=` | `program.registration.view` | 既有清單加進階篩選（回應形狀不變，仍是陣列）。`dateFrom`／`dateTo` 是報名建立日（含當天） |
| `GET /registrations/export?…同上…&purpose=` | `program.registration.export` | 既有 CSV 匯出也吃同一組篩選（欄位含課程名稱、梯次開始日期＝分組欄位）。**`purpose`（匯出用途，≤200 字）必填**，缺 → 400；每次匯出寫敏感操作日誌（誰、幾筆、用途，2026-10-03 起，比照 P4／K1）。建立時間以台灣時間輸出 |
| `POST /registrations/batch/status` | `program.registration.update` | body `{ ids: [...], status }`（1–200 筆）→ `{ updatedCount, skipped: [{ id, reason }] }`。逐筆套用與單筆相同的名額連動；已是該狀態或找不到的進 `skipped` |
| `POST /registrations/{id}/promote` | 同上 | 候補 → 已確認（佔名額）；非候補 → 400 |
| `GET /registrations/waitlist-reminders` | `program.registration.view` | **候補遞補提醒清單**：有空位而且還有人候補的梯次 `[{ sessionId, programNameZh, startOn, endOn, capacity, enrolledCount, vacancy, waiting: [{ order, registrationId, registrationNo, applicantName, phone, guardianName, guardianPhone, queuedAt }] }]`（候補依報名先後排序）。通知信本輪不寄，由承辦依此清單電話聯繫 |
| `GET /registrations/sign-in-sheet?sessionId=` | `program.registration.view` | 簽到表資料 `{ sessionId, programNameZh, startOn, endOn, venueName, generatedAt, rows: [{ no, registrationNo, applicantName, phone, guardianName, guardianPhone, status }] }`；缺 `sessionId` → 400、梯次不存在 → 404。只列會到場的人，不含健康聲明與備註 |

---

### K1 會員名單 `/api/v1/admin/{club}/members`

| 方法 路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET /members?crossClub=&includeNoMembership=&keyword=&clubCode=&tier=&membershipStatus=&status=&signupSource=&lineBound=&registeredFrom=&registeredTo=&seasonId=&expiringWithinDays=&jerseyStatus=&locale=&page=&pageSize=` | `member.account.view` | 分頁（預設 20、上限 100）。**一律遮罩**。篩選：`tier`＝`registered`／`fan_club`；`membershipStatus`＝`pending`／`active`／`expired`／`cancelled`（依有效狀態）；`status`＝`active`／`unverified`／`suspended`／`deleted`（省略＝不含已刪除／已合併）；`signupSource`＝`web`／`line`／`admin`／`app`；`expiringWithinDays`＝1–366（有效會籍且到期日在這幾天內）；`jerseyStatus`＝`pending`／`shipped`／`received`；`locale`＝`zh-Hant`／`en`；會籍相關條件（層級／狀態／球季／即將到期／俱樂部）套在**同一份會籍**上 |
| `GET /members/{id}?reveal=true` | `member.account.view`（`reveal` 另需 `member.pii.reveal`） | 詳情 |
| `POST /members` → 201 | `member.account.create` | 現場入會（`signupSource = admin`）。**沒有設定登入密碼**，會員日後用「忘記密碼」自行設定；Email 視為未驗證；會員編號依 K2 的編號規則產生。Email 重複 → 409 |
| `PUT /members/{id}/status` | `member.account.update` | `{ status: "active"｜"suspended", reason? }`。**沒有任何會籍的帳號只有系統管理員能處理**（受限帳號 → 404） |
| `PUT /members/{id}/note` | 同上 | `{ internalNote }`（≤2000，空白＝清除） |
| `POST /members/{id}/cards/{cardId}/reissue` | 同上 | 重新產生會員卡 QR：**同一張卡換一組新憑證、舊憑證立即失效**，`reissueCount` +1。只能處理目前操作的俱樂部的卡；已停用的卡 → 400。回應不含憑證字串 |
| `GET /members/duplicates?crossClub=` | `member.account.view` | 疑似重複帳號 `[{ matchKind: "phone"｜"email", matchKindLabel, members: [{ id, memberNo, name, email, phone, createdAt, membershipCount }] }]`（遮罩）。比對：同一支電話（去符號、`+886` 視為 `0`）；Email 正規化後相同（大小寫、Gmail 的點號與 `+標籤`） |
| `POST /members/merge` | `member.account.merge`（**sysadmin_only**） | `{ targetMemberId, sourceMemberId }`（保留哪個、併掉哪個）→ `{ targetMemberId, targetMemberNo, sourceMemberNo, movedMemberships, movedRegistrations, movedOrders, movedJerseys }`。會籍、付款、報名、訂單、球衣、寄信紀錄、購物車、球迷活動報名轉給保留帳號；被併帳號變 `deleted`（保留會員編號、姓名遮罩、Email 換成 `merged-…@merged.invalid`、電話／生日／LINE 清除、無法登入），`mergedIntoMemberNo` 指向保留帳號。**兩個帳號在同一俱樂部同一球季都有會籍 → 整批 409**（請先處理掉其中一份）。不可逆 |
| `GET /members/export?purpose=…（同名單篩選）` | `member.export` | CSV（UTF-8 BOM），**每份會籍一列**（沒有會籍者一列空白會籍）；欄位：會員編號、姓名、Email、電話、俱樂部、球季、會員層級、會籍狀態、到期日、註冊來源、帳號狀態、語系偏好、註冊日期。**不含生日與 LINE 識別碼**。範圍與名單完全相同 |

**清單項**：`{ id, memberNo, name, email, phone, signupSource(+Label), lineBound, status, displayStatus(+Label), locale(+Label), createdAt, lastLoginAt, jerseyStatus(+Label), memberships: [ { membershipId, clubId, clubCode, clubName, seasonId, seasonCode, tier(+Label), status, effectiveStatus(+Label), startOn, endOn, daysToExpire, planId, planName } ], isMasked }`。`displayStatus`＝`unverified` 表示帳號啟用但 Email 尚未驗證（不是資料庫狀態值）。
**詳情**：以上欄位加 `birthOn`（`yyyy-MM-dd` 或遮罩 `****-**-**`）、`emailVerifiedAt`、`internalNote`、`mergedIntoMemberNo`、`canReveal`，`memberships[]` 每筆為 `{ membership: <上面的摘要>, lastAdjustReason, lastAdjustedAt, payments: [ { id, planName, method(+Label), amount, paidOn, collectingClubCode, beneficiaryClubCode, note, handledByName, activatedStartOn, activatedEndOn, createdAt } ], cards: [ { id, membershipId, holderName, status(+Label), issuedAt, revokedAt, reissueCount } ] }`，另有 `jerseyIssues: [ { id, clubCode, recipientName, size, deliveryMethod(+Label), status(+Label), shippedOn, receivedOn } ]`。持卡人與領用人姓名同樣遮罩。**沒有「登入紀錄」清單**（本庫不建日誌表，只有 `lastLoginAt`）。
**沒做**：「重寄驗證信」「代發密碼重設信」（全系統沒有寄信通路）——畫面先不放這兩顆按鈕。

### K2 會籍與方案

**方案** `/api/v1/admin/{club}/membership-plans`（`member.plan.*`）：

| 方法 路徑 | 說明 |
|---|---|
| `GET ?seasonId=&status=` | 陣列（球季新→舊、排序）：`{ id, seasonId, seasonCode, code, fee, cardQuota, jerseyQuota, startsOn, endsOn, sortOrder, status, statusLabel, nameZh, nameEn, membershipCount, updatedAt }` |
| `GET /{id}` | 詳情多 `midSeasonRule`、`zh`／`en`（`{ name, benefitNote }`）、`createdAt` |
| `POST`／`PUT /{id}` | payload：`seasonId`（必填，須屬於目前俱樂部）、`code`（必填，小寫英數與連字號 ≤32，**同俱樂部同球季內不可重複 → 409**）、`fee`（≥0，元）、`cardQuota`（1–10，預設 1）、`jerseyQuota`（0–10）、`midSeasonRule?`、`startsOn?`／`endsOn?`、`sortOrder`、`status`（`published` 上架／`draft` 下架，預設 `draft`）、`content.zh.name`（必填）／`content.zh.benefitNote?`／`content.en?`。**有會籍或付款紀錄使用時不能換球季（400）** |
| `DELETE /{id}` | 204；**有會籍或付款紀錄使用 → 409**（改為下架）。方案底下的權益條目一併刪除 |
| `PUT /order` | 排序（`{ ids }`） |

**會籍** `/api/v1/admin/{club}/memberships`（只看得到目前操作的俱樂部）：

| 方法 路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET ?memberId=&keyword=&tier=&status=&seasonId=&planId=&expiringWithinDays=&page=&pageSize=` | `member.membership.view` | 分頁；**會籍到期提醒清單＝`expiringWithinDays=30`**（依到期日近→遠）。清單項 `{ membershipId, memberId, memberNo, memberName（遮罩）, tier, status, effectiveStatus(+Label), seasonId, seasonCode, startOn, endOn, daysToExpire, planId, planName, cardCount, paidTotal, updatedAt }` |
| `GET /{id}` | 同上 | 詳情 `{ membership, lastAdjustReason, lastAdjustedAt, payments[], cards[], cardQuota, jerseyQuota }` |
| `POST /activate` | `member.membership.create` | **手動開通／續會**。body：`memberId`、`planId`（須屬於目前俱樂部且**上架**）、`beneficiaryClubId?`（受益俱樂部；省略或＝目前俱樂部，不同 → 400）、`paymentMethod`（`linepay`／`onsite`）、`amount`（≥0，實收元）、`paidOn`（不可晚於今天）、`note?`、`startOn?`（省略＝方案起日；方案已開始則今天）、`endOn?`（省略＝方案迄日，再省略用球季結束日）。找到或建立「這位會員在這個俱樂部這個球季」的會籍 → 升為球迷會員、設起訖 → 寫一筆付款紀錄（**同時記受益俱樂部與收款法人**：`collectingClubCode` 恆為收款主體俱樂部＝代收代付）→ 確保有一張使用中的會員卡（不足的副卡另用 `POST /{id}/cards`）。**續會＝用下一球季的方案再開通一次**。同一會籍、同方案、同付款日、同金額重複送出 → 409。**補登已結束球季的會籍必須明確給 `startOn` 與 `endOn`**（否則 400「方案期間已結束」）。回傳會籍詳情 |
| `POST /` → 201 | `member.membership.create` | 建立免費（一般會員）會籍與第一張卡：`{ memberId, seasonId }`；已有 → 409 |
| `PUT /{id}/adjust` | `member.membership.update` | 手動調整：`{ tier?, status?, startOn?, endOn?, reason }`，**`reason` 必填、至少改一項**。**只留最近一次原因**（不建歷程表）。改成 `cancelled` 時其會員卡一併停用 |
| `POST /{id}/cards` | 同上 | 新增副卡（家庭方案）：`{ holderName }`；**不得超過方案 `cardQuota`（409）** |
| `POST /{id}/cards/{cardId}/revoke` | 同上 | 停用單張卡 |
| `POST /expire-batch` | 同上 | **球季末批次到期處理（依目前俱樂部各自執行）**：`{ asOf?, seasonId?, dryRun? }` → `{ asOf, count, dryRun }`。把「到期日早於基準日的有效會籍」標為已到期；先 `dryRun: true` 試算件數 |
| `GET /renewal-export?kind=expiring｜expired&days=30&seasonId=&purpose=` | `member.export`（🔴） | **續會名單 CSV**：`expiring`（預設，`days` 1–366，預設 30）或 `expired`（已到期尚未續會）。含姓名／Email／電話，須填 `purpose` |

**付款紀錄** `GET /api/v1/admin/{club}/membership-payments?membershipId=&from=&to=&page=&pageSize=`（`member.membership.view`）：`{ payment: { id, planName, method(+Label), amount, paidOn, collectingClubCode, beneficiaryClubCode, note, handledByName, … }, membershipId, memberId, memberNo, seasonCode }`（供對帳）。
**會員編號規則** `GET／PUT /api/v1/admin/{club}/member-settings`（`member.setting.view／update`）：`{ memberNoPrefix（英數 ≤8，可空）, memberNoDigits（4–10） }`，`GET` 另回 `nextMemberNoPreview`。預設 `M`＋6 位數字；只影響**後台建立**的新會員，已存在的編號不變。
**金流不做**（LINE Pay 商店號未到位，B-10）：只有客服核對款項後的手動開通與人工登錄的付款紀錄；內部端點 `POST /api/membership/activate`（自動化接口）**本期不啟用**。

### K3 球衣發放 `/api/v1/admin/{club}/jerseys`

| 方法 路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET ?status=&size=&deliveryMethod=&memberId=&membershipId=&keyword=&page=&pageSize=` | `member.jersey.view` | 分頁，**待處理→已寄出→已領取**排序。收件資訊依 `member.pii.reveal` 遮罩（有權限者的每次名單查詢會寫日誌） |
| `GET /size-summary?status=pending` | 同上 | **依尺寸統計備貨量** `[{ size, sizeLabel, total, ship, pickup }]`（依 XS…4XL 排序，未填尺寸另列「未填尺寸」）；`status` 省略＝待處理 |
| `GET /{id}` | 同上 | `{ id, memberId, memberNo, membershipId, recipientName, phone, size, deliveryMethod(+Label), address, status(+Label), shippedOn, receivedOn, createdAt, updatedAt, isMasked }` |
| `POST /` → 201 | `member.jersey.create` | 後台代填：`{ membershipId, recipientName, phone?, size, deliveryMethod（"ship"｜"pickup"）, address? }`；**寄送必須有電話與地址**；**件數不得超過方案 `jerseyQuota`（409；免費會籍不含球衣）**；尺寸自動轉大寫 |
| `PUT /{id}` | `member.jersey.update` | 只改有帶的欄位（省略＝不變）。**修改領用人／電話／地址需要 `member.pii.reveal`（否則 403）**。狀態：`pending`／`shipped`／`received`——`shipped` 只有寄送的球衣可用（到場領取請直接標已領取），記寄出日；`received` 記領取日；回 `pending` 清掉兩個日期；可往回改（更正誤按）。狀態變更**即時反映在會員端**（同一張表） |
| `POST /batch/status` | 同上 | `{ ids（1–200）, status }` → `{ updatedCount, skipped }`（不符規則的列進 `skipped`） |
| `GET /export?status=&purpose=` | `member.jersey.export`（🔴） | 出貨清單 CSV（**含完整收件資訊**），須填 `purpose` |

### K4 特約店家與權益

**特約店家** `/api/v1/admin/{club}/partner-stores`（`member.store.*`，**multipart**：`payload` JSON ＋選填檔案欄位 `image`，圖片規則同 E1a 通則）：

| 方法 路徑 | 說明 |
|---|---|
| `GET ?category=&region=&status=&tier=&keyword=` | 陣列，**含兩隊共同的店家（`isShared: true`，所有俱樂部看得到、只有系統管理員能編輯，其餘 403「共用內容唯讀」）**：`{ id, slug, isShared, category, region, address, lat, lng, phone, applicableTier(+Label), startOn, endOn, isActive, sortOrder, status(+Label), imageKey/Url/ThumbUrl, nameZh, nameEn, offerZh, updatedAt }` |
| `GET /filters` | `{ categories, regions }`——8.4 清單頁分類與地區篩選項目（由實際用過的值自然形成，自由文字） |
| `GET /{id}` | 詳情多 `businessHours`（自由文字）、`mapUrl`、`websiteUrl`、`zh`／`en`（`{ name, address, offerContent }`）、`createdAt` |
| `POST`（201）／`PUT /{id}` | payload：`slug?`（省略自動產生）、`category?`（≤32）、`region?`（≤32）、`lat?`＋`lng?`（**成對，人工確認後儲存**；範圍 ±90／±180）、`phone?`、`businessHours?`（≤500）、`mapUrl?`／`websiteUrl?`（http／https）、`applicableTier`（`all` 全會員／`fan_club` 限付費）、`startOn?`／`endOn?`（合作起訖）、`sortOrder`、`status`（`published`／`draft`）、`isShared?`（**只有系統管理員可建立兩隊共同的店家**，更新時忽略）、`removeImage?`、`content.zh.name`（必填）／`address?`／`offerContent?`、`content.en?`。中文地址存店家主檔（供座標定位）、英文地址存英文版 |
| `DELETE /{id}` | 204（共同店家 → 403） |
| `PUT /order` | 排序（只含本俱樂部的店家，共同店家不參與 → 400） |

🔴 **「由地址定位」輔助按鈕本輪沒有做**：需要外部地址轉座標服務（Google／內政部等），尚未選定，見「待裁決」。目前座標由人工輸入。

**權益對照表** `/api/v1/admin/{club}/membership-benefits`（`member.benefit.*`）：條目**掛在方案底下**（`membership_benefits` 不帶 `club_id`，範圍靠「方案屬於目前俱樂部」強制）。

| 方法 路徑 | 說明 |
|---|---|
| `GET ?planId=&group=&status=` | 陣列（依球季、方案排序、條目排序）：`{ id, planId, planCode, planName, seasonCode, group, groupLabel, sortOrder, status(+Label), nameZh, nameEn, freeValueZh, paidValueZh, updatedAt }` |
| `GET /groups` | 分組 `[{ code, label }]`：`member_card` 會員卡／`store_discount` 店家折扣／`jersey` 球衣／`event` 活動 |
| `GET /{id}` | 詳情 `zh`／`en`：`{ name, description, freeValue, paidValue }` |
| `POST`（201）／`PUT /{id}` | payload：`planId`（必填，屬於目前俱樂部；**更新時不可變更**）、`group`、`sortOrder?`（省略＝排在該方案最後）、`status`（`published`／`draft`）、`content.zh.name`（必填）／`description?`／`freeValue?`（如「✓」「✗」「9 折」）／`paidValue?`、`content.en?`。分組的雙語顯示文案由系統依分組代碼自動帶入 |
| `DELETE /{id}` | 204 |
| `PUT /order` | `{ planId, ids }`：同一方案內重排（含不屬於該方案的 id → 400） |

---

### L1 進階 `/api/v1/admin/{club}/calendar`

| 方法 路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET /events?from=&to=&team=&sourceType=&venueId=&status=&type=` | `calendar.view` | 既有總覽補三個篩選：`venueId`（場地）、`status`（賽事狀態 `scheduled／live／played／postponed／cancelled`；**依狀態篩選時自建活動不出現**）、`type`（賽事類型 `league／cup／friendly／other` 或自建活動類型代碼）；`sourceType` 新增 `trial`（**只有 L3 試訓同步開關開啟時，才有試訓來源**）。回應每筆多 `venueId`、`kickoff`（僅賽事，`HH:mm`；賽事的 `startsAt` 只有日期）、`competitionTag`（僅賽事） |
| `GET /tracks?from=&to=` | `calendar.view` | **隊別分軌檢視**（預設本月，上限 366 天）：`{ from, toExclusive, tracks: [{ teamId, teamCode, name, colour, sortOrder, events: [<總覽事件>] }], clubEvents: [...], conflicts: [...] }`。每支球隊一條軌道（名稱／代表色／排序沿用 L3，沒設定就沿用球隊本身）；**一場跨隊賽事同時出現在它涉及的每條軌道**；沒有隊別的自建活動與同步試訓在 `clubEvents` |
| `GET /conflicts?from=&to=` | `calendar.view` | **衝突偵測**：`[{ reasons: ["venue"｜"team"], description（日常中文，可直接顯示）, venueName, sharedTeamCodes, first: {…事件}, second: {…事件} }]`。規則：兩件事**時段重疊**且（**同一場地**或**同一梯隊**）。賽事沒有時長，估 2 小時（同 .ics）；沒有開賽時間視為整天；已取消的賽事與試訓（只有日期）不參與；自建活動的時間是 UTC 時間點，全天活動以 UTC 日期整天計 |
| `POST /matches/{id}/reschedule` | `team.match.update`＋球隊列級授權 | **賽事拖曳改期**：`{ matchOn, kickoff?, markAsPostponed?, acknowledgeConflicts? }`。`kickoff`：省略＝維持、`""`＝清除、`HH:mm`＝新時間；`markAsPostponed: true`＝同時標為延賽並記下原定日期／時間（第一次標記時記錄，之後再改期不覆蓋）；預設 false（只改日期，例如更正輸入）。**新時段有衝突且沒帶 `acknowledgeConflicts: true` → 409，`Content-Type: application/problem+json`，除了 `title`（「排程衝突」）與 `detail` 外多 `conflicts`（同上格式）與 `saved: false`，沒有寫入**——畫面警示後帶確認旗標重送。成功 `{ saved: true, sourceType, sourceId, startsAt, kickoff, status, originalMatchOn, conflicts: [], notificationSent: false }`。已開賽／已結束／已取消的賽事 → 400；範圍外的球隊 → 403 |
| `POST /custom-events/{id}/move` | `calendar.custom_event.update` | **自建活動拖曳改期**：`{ startsAt, endsAt?, isAllDay?, acknowledgeConflicts? }`（UTC）；衝突處理同上。**重複活動改的是整個系列的起始時間**（例外日期不會跟著位移） |

### L3 分類與顯示設定 `/api/v1/admin/{club}/calendar`

| 方法 路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET /settings` | `calendar.setting.view` | `{ defaultView（list｜month）, defaultRange（upcoming｜this_month｜next_30_days｜season）, defaultTeamCode（all 或隊別代碼）, homeTeamCodes[], firstTeamCode, syncTrials, teams[], eventTypes[] }`。`teams[]`：`{ teamId, code, type, teamNameZh, displayNameZh, displayNameEn, colour（覆寫）, effectiveColour, sortOrder（覆寫）, effectiveSortOrder, isPublic }`；`eventTypes[]` 同下方賽事類型 |
| `PUT /settings` | `calendar.setting.update` | `{ defaultView, defaultRange, defaultTeamCode?, homeTeamCodes?, firstTeamCode?, syncTrials }`（隊別代碼必須屬於目前俱樂部，否則 400）。`homeTeamCodes`＝首頁近期賽事元件顯示哪些隊別（空＝全部公開隊別）；`firstTeamCode`＝一線隊頁固定顯示的隊別（各梯隊頁自動顯示自己那隊，不需設定）。**`syncTrials`＝「試訓是否同步至行事曆」（預設關閉）：寫入設定並連動該俱樂部所有試訓的同步旗標**，回傳完整設定 |
| `PUT /settings/teams` | 同上 | 整批更新隊別分類：`{ teams: [{ teamId, displayNameZh?, displayNameEn?, colour?（#RRGGBB）, sortOrder?, isPublic }] }`（沒列出的隊別不變；空白覆寫＝沿用球隊本身）。**`isPublic: false` 的隊別不出現在前台選單，其訂閱 feed 回 404** |
| `GET /event-types`（既有） | `calendar.custom_event.view` | 賽事／活動類型 `[{ id, code, colour, icon, nameZh, nameEn, isPublic, sortOrder, usageCount }]`（`usageCount`＝有幾個自建活動使用） |
| `GET /event-types/icons` | `calendar.setting.view` | **系統預設圖示集**（20 個）`[{ code, label }]`；賽事類型的圖示只能從這裡選，**不是上傳圖片** |
| `POST`（201）／`PUT /event-types/{id}`／`DELETE /event-types/{id}`（204）／`PUT /event-types/order` | `calendar.setting.update` | payload `{ code（小寫英文開頭，英數底線連字號，全站唯一 → 409；建立後不可變更）, nameZh, nameEn?, colour?（#RRGGBB）, icon?（圖示集內）, isPublic, sortOrder }`。🔴 **賽事類型是兩隊共用資料（不帶俱樂部）：只有系統管理員能寫，其餘角色 → 403「共用內容唯讀」**；有自建活動使用 → 刪除 409 |

### L4 訂閱與匯出

| 方法 路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET /api/v1/admin/{club}/calendar/subscriptions` | `calendar.subscription.view` | `{ feeds: [{ feedKey（all｜隊別代碼）, label, httpsUrl, webcalUrl, subscribers30d, subscribers7d, lastFetchedOn, isPublic }], statsNote }`。第一筆是全站，之後每支隊別一筆（依 L3 排序）。**網址以這次請求的網址為基底**（部署時 API 由 Caddy 反向代理，需確保轉送 `Host`／`X-Forwarded-*`）。**訂閱數是估計值**：以「最近有多少個不同的訂閱來源（IP＋User-Agent 雜湊）」計算，Google 行事曆由 Google 伺服器代抓，多位訂閱者會算成一個，`statsNote` 有說明，畫面請照顯示 |
| `GET /api/v1/admin/{club}/calendar/export?format=csv｜ics&from=&to=&team=&sourceType=&venueId=&status=&type=` | `calendar.export` | 指定期間匯出（預設本月，上限 366 天），篩選同總覽。CSV 欄位：類別、日期、開始時間、結束時間、標題／對手、隊別、場地、主客場、狀態、類型、是否公開；`.ics` 為標準行事曆檔。**不公開的自建活動只有持有 `calendar.custom_event.view` 的人匯得到** |
| `POST /api/v1/admin/{club}/calendar/matches/import` | `team.match.create`＋球隊列級授權 | **整季賽程 CSV 匯入，與 C4 賽程匯入共用同一套機制**（同一份 CSV 格式、同一個匯入函式）。body 是 CSV 原始位元組（不是 multipart）；格式錯誤 400 |
| `GET /api/v1/{club}/calendar/feed.ics?team=D1&lang=zh` | **公開，不需登入** | **webcal 訂閱 feed**（`text/calendar`，`Cache-Control: public, max-age=900`，限流同輕量互動）。`team` 省略或 `all`＝全站；不存在的隊別或 L3 設為不公開 → 404。內容：賽事（含取消——`STATUS:CANCELLED`——與延賽，客戶端才會同步）、前台公開的自建活動（重複規則展開）、L3 同步的試訓（全天）；範圍今天前 90 天到後 400 天；隊別 feed 只含該隊別事件（沒有隊別的俱樂部活動只在全站 feed）；含 `X-WR-CALNAME` 與 6 小時重新整理建議。**每次抓取會記一筆去識別化的訂閱來源**（HMAC 雜湊，同來源同天一筆，保存 90 天） |
| `GET /api/v1/{club}/calendar/settings?lang=zh` | **公開** | 前台讀 L3 設定：`{ defaultView, defaultRange, defaultTeamCode, homeTeamCodes, firstTeamCode, teams: [{ code, displayName, colour, sortOrder }], eventTypes: [{ code, name, colour, icon }] }`（只含公開的隊別與賽事類型；快取實體 `calendar`，L3 寫入時失效） |

---

### 資料庫綱要異動（`db/club-schema.sql`、migration `AlignSchemaB1`）

全部是把規劃書已有的功能落到資料表（`docs/12` §12 第 44 點有完整清單與理由）：`trials` 加 `enrolled_count`／`status`＋新表 `trials_i18n(audience)`；`members` 加 `email_verified_at`／`last_login_at`／`internal_note`／`locale`／`merged_into_member_id`；
`memberships` 加 `membership_plan_id`／`last_adjust_reason`／`last_adjusted_at` 並收斂 `status`；`member_cards` 收斂 `status`＋`revoked_at`；`membership_plans` 加 `starts_on`／`ends_on`／`status`；`membership_benefits`（＋`_i18n`）加 `status`／`name`／`description` 與分組值域；
`jersey_issues` 加 `membership_id`／`received_on` 並收斂狀態與領取方式；`partner_stores`（＋`_i18n`）加 `region`／`map_url`／英文地址並收斂 `applicable_tier`／`status`；新表 `calendar_team_settings`（＋`_i18n`）、`calendar_feed_fetches`。
本機庫已對 `tcrfc_club` 套用（套用前這些表皆 0 筆，`dotnet ef migrations add Probe` 驗證為空 migration，模型與 snapshot 同步）；`db/club-schema.sql` 已用 `SET PARSEONLY ON` 逐批解析驗證語法。EF 實體：新增 4 個（`TrialsI18n`／`CalendarTeamSetting`／`CalendarTeamSettingsI18n`／`CalendarFeedFetch`），其餘只**插入**新屬性與新表設定（同 E1a 的做法，未整檔重新 scaffold）。
另修一個既有 bug：`IcsBuilder.FoldLine` 折疊最後一段讀到陣列外（`docs/18` E-88，訂閱 feed 才第一次觸發）。

### 種子（`db/seed/backoffice_seed.py` §42–48，全部【測試】虛構資料）

- **會員 7 位**（`M900001`–`M900007`，Email `@example.com`、電話 `0900-000-0XX`）：涵蓋**雙會籍**（甲：磐石＋藍鯨球迷會員）、現場入會、Email 未驗證、已停用、疑似重複帳號（甲與己同一支電話）、**即將到期**（戊，磐石到期日 2026-10-15）、LINE 來源；會籍 8 份（含待確認與已到期）、會員卡 9 張（家庭方案含副卡）、付款紀錄 4 筆、球衣 4 件（三種狀態、含尺寸）。
- **方案 3 個**：磐石 2026-27 單人（1200 元、1 卡 1 衣）／家庭（3000 元、3 卡 3 衣）、藍鯨 2025 單人；**權益條目 6 條**（四個分組）；**特約店家 6 家**（磐石 4 含 1 家草稿、兩隊共同 1、藍鯨 1；座標為台中市區近似值）。
- **試訓 3 場**（磐石一線隊 2 場含 1 場已結束、藍鯨 U15 1 場）、試訓報名 4 筆。
- **行事曆設定**：兩俱樂部的預設檢視／範圍／隊別／試訓同步（預設關閉）、`D1`／`BW1` 的顯示名稱與代表色、藍鯨 `BW-U12` 示範「不公開」。會員編號規則 `M`＋6 位。
- 權限碼 39 個與角色指派已在 `generate-club-seed-sql.py` 種入。沒有圖片。**跑法**：`set -a; source .env; set +a; ./db/seed/apply-seed.sh`（冪等）。

### 測試（新增 56 項，全套 641 項通過）

| 測試類別 | 項數 | 涵蓋 |
|---|---:|---|
| `AdminMembersTests` | 16 | 未登入／無權限／跨俱樂部；名單一律遮罩且回應本文不含真實個資；合作球隊管理只看得到自家會籍（含 `crossClub` 也繞不過、對方會員 404）；無解除權限時搜尋不比對個資；系統管理員跨俱樂部雙會籍；各種篩選與未知代碼 400；即將到期；詳情遮罩／解除／403；回應不含 token 與 LINE；建立會員（編號規則、重複 409、格式錯）；停用啟用與備註；重產 QR（舊憑證失效）；重複帳號比對（電話、Gmail 點號）；合併（權限、轉移、被併帳號清除、衝突 409、不可重複合併）；匯出（用途、權限、BOM、不含生日） |
| `AdminMembershipsTests` | 10 | 方案 CRUD／驗證／重複／排序／使用中保護；開通（付款紀錄雙欄位、重複、受益俱樂部、下架方案、跨俱樂部方案）；調整（原因、取消停用卡、家庭方案副卡上限）；批次到期（試算再執行、只動符合基準日者）；清單篩選與遮罩；續會名單匯出；會員編號規則 |
| `AdminJerseysAndStoresTests` | 8 | 球衣（件數上限、狀態流轉與寄送規則、尺寸統計排序、批次略過、遮罩與改收件人 403、匯出）；特約店家（CRUD、雙語地址、座標成對、網址驗證、篩選項目、共同店家唯讀）；權益條目（CRUD、分組、跨方案／跨俱樂部、排序） |
| `AdminTrialsAndRegistrationsTests` | 6 | 試訓權限矩陣；種子可讀；CRUD 與驗證；報名名額連動（額滿自動關閉、候補不佔、取消釋回、遞補、有報名不能刪除、簽到表）；客服可處理不可建立不可匯出、匯出不含健康聲明；P3 進階（篩選、批次、遞補提醒、簽到表） |
| `AdminCalendarAdvancedTests` | 11 | 分軌與衝突偵測（同場地同梯隊重疊、取消與不重疊不算）；總覽新篩選；賽事改期（衝突 409 不寫入、確認後寫入、延賽記原定日期、驗證與權限）；自建活動改期；L3 設定與試訓同步連動；隊別設定與不公開隊別 feed 404；賽事類型僅系統管理員；feed 內容與訂閱數；匯出 CSV／ics；CSV 匯入權限；公開設定端點 |
| `IcsBuilderTests`（新增 5 項） | 5 | 長行折疊回歸（E-88）、多事件日曆 |

### 待裁決（規劃書沒寫、本輪先採最保守做法）

1. **稽核日誌**：規劃書要求「匯出／個資檢視須寫入稽核日誌（誰、何時、幾筆、用途）」，但委託方明示本庫不建日誌表（`docs/12` §13.1）。本輪暫行做法是寫應用程式結構化日誌（`SensitiveActionLogger`，正式環境進 Application Insights，不含個資）。**建議客戶重新確認稽核表的政策**——若要求可查詢的稽核紀錄，需要建 `AuditLog`／`ExportLog` 並改寫 `SensitiveActionLogger`（呼叫端不用改）。同理「手動調整層級的異動原因」只留最近一次，若要完整歷程也需要表。
2. **通知**：會籍開通確認信、候補遞補提醒信、課程／試訓確認信、賽事改期通知、重寄驗證信、代發密碼重設信——全系統沒有寄信通路，`EmailLog.type` 值域也封閉（會員 5＋商店 4，沒有課程與試訓通知）。需要先決定寄信服務與是否擴充值域。
3. **由地址定位（K4）**：需外部 geocoding 服務，尚未選定供應商；座標目前人工輸入。
4. **合併帳號的權限**：規劃書寫了合併功能但沒指定誰能做；不可逆且牽涉會籍與付款，本輪限系統管理員（`sysadmin_only`）。客服／行政是否可合併待裁決。
5. **方案管理的權限**：費用是經營決策，本輪客服／行政只給方案檢視；日常誰來新增方案／改價（現在只有系統管理員）待裁決。
6. **合作球隊管理（藍鯨方）的會員權限**：矩陣寫「僅自家會籍、Member 主檔遮罩」，本輪只給檢視（名單、會籍、方案、球衣），**沒有處理與匯出**；是否能替藍鯨會員開通／調整待裁決。
7. **收款主體**：`collecting_club_id` 取 `clubs.is_collecting_subject = 1` 的俱樂部（現況磐石）。若兩隊都標為收款主體會取排序最前者；藍鯨是否日後成為獨立收款主體時要重看。
8. **賽事改期預設不標延賽**：規劃書只說「拖曳調整日期回寫賽事資料（改期時觸發通知）」，沒說改期是否等於延賽。預設只改日期（`markAsPostponed: false`），畫面若要「改期＝延賽」請帶 `true`。
9. **自建活動時區**：`calendar_custom_events.starts_at` 是 UTC 時間點（既有決定），全天活動以 UTC 日期計；衝突偵測與 feed 沿用這個口徑。若畫面以當地時間建立全天活動，需在畫面端換算成 UTC 再送。
10. **會員編號規則是每俱樂部一份設定，但會員編號全站唯一**：兩個俱樂部設定不同前綴是允許的；相同前綴時共用同一個流水號序列。

### 已知限制

- 訂閱 feed 的來源辨識靠 IP＋User-Agent 雜湊，**無法精確計算訂閱人數**（見 `statsNote`）；行程內去重用記憶體，多實例部署時每個實例各記一次（資料庫主鍵擋重複，只是多一次寫入嘗試）。
- 衝突偵測的賽事時長是估計值（2 小時），且只比對「賽事＋自建活動」；課程梯次永不進行事曆（規則不變）。
- K1 名單的「重複帳號」在記憶體比對（單次最多載入該範圍內全部會員），會員數上萬時要改成資料庫端比對；目前資料量無此問題。
- `dotnet test` 對共用的 `tcrfc_club` 執行：訂閱數的測試會在 `calendar_feed_fetches` 留下每天最多幾筆去識別化列（不清理，因為行程內去重會讓清掉的列不再重寫）。
- 特約店家照片沒有種子（無可上傳的公開素材）；`business_hours` 只接自由文字。
- 前台公開端點（會員中心、`/m/<token>` 驗證頁、8.4 特約店家清單、權益對照表公開讀取）**不在本批**（`S2-11`）；本批只提供 L3 設定與訂閱 feed 兩支公開端點。

---

### B1 回應形狀補齊（C1 批補記，2026-09-30）

畫面端原本是讀 DTO 推定的形狀，這裡逐項寫死。所有回應都是 camelCase；`<Label>` 結尾的欄位是日常中文標籤；日期 `yyyy-MM-dd`；時間戳是 UTC ISO 8601（**JSON 不帶 `Z`**，前端一律當 UTC 解析，見 C1 通則）。

| 端點 | 回應形狀 |
|---|---|
| `POST /members` → 201、`PUT /members/{id}/status`、`PUT /members/{id}/note` | **一律回會員詳情**（同 `GET /members/{id}`）：`{ id, memberNo, name, email, phone, birthOn, signupSource(+Label), lineBound, status, displayStatus(+Label), locale(+Label), emailVerifiedAt, createdAt, lastLoginAt, internalNote, mergedIntoMemberNo, memberships: [ { membership: <會籍摘要>, lastAdjustReason, lastAdjustedAt, payments[], cards[] } ], jerseyIssues: [ { id, clubId, clubCode, recipientName, size, deliveryMethod(+Label), status(+Label), shippedOn, receivedOn } ], isMasked, canReveal }`。`birthOn` 是字串（`yyyy-MM-dd` 或遮罩 `****-**-**`）。建立後回應的個資是否遮罩依呼叫者是否有 `member.pii.reveal`。 |
| `GET /trials/{id}/sign-in-sheet` | `{ trialId, trialOn, teamName, venueName, audienceZh, generatedAt, rows: [ { no, registrationNo, applicantName, phone, guardianName, guardianPhone, status } ] }`（`rows` 只含會到場的人：待確認／已確認／已繳費／完成；不含健康聲明與備註） |
| K3 `GET /jerseys` 清單項 | 分頁 `{ items, page, pageSize, totalCount, totalPages }`，每一項與 `GET /jerseys/{id}` 完全相同：`{ id, memberId, memberNo, membershipId, recipientName, phone, size, deliveryMethod(+Label), address, status, statusLabel, shippedOn, receivedOn, createdAt, updatedAt, isMasked }`（收件三欄依 `member.pii.reveal` 遮罩） |
| K2 會籍詳情 `GET /memberships/{id}`、`POST /memberships`、`POST /memberships/activate`、`PUT /memberships/{id}/adjust`、`POST /memberships/{id}/cards`… | `{ membership: <會籍清單項>, lastAdjustReason, lastAdjustedAt, payments: [ <付款> ], cards: [ <卡> ], cardQuota, jerseyQuota }`。**`membership`（＝清單項）欄位**：`{ membershipId, memberId, memberNo, memberName（遮罩）, tier(+Label), status, effectiveStatus(+Label), seasonId, seasonCode, startOn, endOn, daysToExpire, planId, planName, cardCount, paidTotal, updatedAt }`。**付款 `<付款>`**：`{ id, planId, planName, method(+Label), amount, paidOn, collectingClubCode, beneficiaryClubCode, note, handledByName, activatedStartOn, activatedEndOn, createdAt }`；**卡 `<卡>`**：`{ id, membershipId, holderName, status(+Label), issuedAt, revokedAt, reissueCount }`（**不含 QR 憑證字串**） |
| `GET /membership-payments` | **分頁物件**（不是陣列，已定案）：`{ items: [ { payment: <付款>, membershipId, memberId, memberNo, seasonCode } ], page, pageSize, totalCount, totalPages }` |
| L4 `POST /calendar/matches/import` | body 是 CSV 原始位元組。全部通過 → **200** `{ importedCount, errors: [] }`；有任何一列不合格（含與資料庫既有場次編號衝突）→ **400** `{ importedCount: 0, errors: [ { rowNumber, reason } ] }`（`rowNumber` 從 1 起算，表頭是第 1 行；**整批不寫入**，不是部分成功；`reason` 是日常中文）。球隊列級授權不合格的列也算列錯誤（不是 403）。**檔案是空的或表頭不符** → 400 一般錯誤格式（`{ title, status, detail }`，不是 `errors` 陣列），表頭必須依序是 C4 賽程匯入的欄位 |

### 會籍球季下拉（C1 補，2026-09-30）

`GET /api/v1/admin/{club}/membership-seasons`（**`member.membership.view`**）→ `[ { id, code, startOn, endOn } ]`，只含目前俱樂部的球季（新→舊）。給會籍畫面（方案表單、開通、續會名單）用——客服／行政沒有 `team.competition.view`，**不要再借用 `GET …/seasons`**（會 403）。

---

## C1：F1 漫畫／F2 球迷會活動／S1–S6 站內商店／K5 抽獎名單／新聞挑選搜尋（2026-09-30，`backend-engineer`）

主站規劃書 §4.6 F1／F2、§4.13 S1–S6、§4.11 K5（後台），對應 `STATUS.md` 的 `S3-1`／`S3-3`／`S3-4`／`S3-8`。
沿用 E1a／B1 的全部通則（路徑、錯誤格式、`content: { zh, en? }` 雙語、`PUT` 整份取代、`PUT …/order`、`POST …/batch/*`、分頁形狀、跨俱樂部 id 一律 404、標籤欄位、敏感操作日誌）。
**本節只寫 C1 新增的規則與每支端點的契約。給畫面的人：只讀這一節就能串接，不需要看程式碼。**

### C1 通則（新增）

| 項目 | 規定 |
|---|---|
| 時間戳與日期 | **時間戳一律是 UTC，回應的 JSON 一律帶 `Z`**（例如 `2026-09-30T09:12:33.123Z`；**D 批起全站統一**，見下方「全站時間格式通則」）——前端用 `new Date(value)` 直接解析即可，顯示時 +8 小時，不必再猜有沒有時區。**送進來的時間戳：無時區記號視為 UTC**（帶 `Z` 或 `+08:00` 的照標示換算）。日期欄位（`claimDeadlineOn`、`pickupDeadlineOn`、`settledOn`…）是台灣當地日期 `yyyy-MM-dd`。 |
| 圖片 | 同 E1a：含圖片的建立／更新是 `multipart/form-data`（`payload` ＋ 檔案欄位）；圖集（漫畫內頁、活動回顧、商品圖）用獨立端點 `POST …/pages`／`…/images`（檔案欄位 `files`，**一次可多張**，整批全有或全無：任何一張不合格 → 400 且不留物件）。 |
| 訂單狀態 | **訂單狀態 `orderStatus` 資料庫本身就是中文**（`待付款／已付款／備貨中／已出貨／已完成／已取消／退貨處理中／已退款`），直接顯示、直接當篩選參數傳。其餘狀態（付款狀態、退款案件、庫存異動…）是英文代碼＋`…Label`。 |
| 個資遮罩 | **訂單收件人（姓名／電話／地址）視同會員個資**：完整值需 `shop.order.reveal`（系統管理員、客服／行政、合作球隊管理）；其餘一律遮罩，回應有 `isMasked`（這份是不是遮罩值）與（詳情）`canReveal`。**沒有 reveal 權限時關鍵字搜尋只比對訂單編號**（否則搜尋會變成探測個資的工具）。抽獎名單與報名名單同理（`member.pii.reveal`）。 |
| 匯出 | 受限匯出（`shop.order.export`、`shop.report.export`、`member.draw.export`）＋公開版名單匯出：**必填 `purpose`（≤200 字，缺 → 400）**，寫敏感操作日誌。訂單匯出**先套資料範圍（`selling_club_id`）再套受限欄位授權**，兩道關卡。CSV 一律 UTF-8 BOM。 |
| 資料範圍 | 商品／庫存／出貨／退款以 `club_id`；**訂單一律以 `selling_club_id` 為目前俱樂部**（受範圍限制的帳號看不到別的俱樂部的訂單）。藍鯨的訂單由磐石代收（`collectingClubId` 是收款主體俱樂部），只在藍鯨站台看得到。 |
| 庫存 | **庫存量與已保留量只透過庫存異動改動**（`inventory_movements`，可追溯數量、原因、經辦人）；規格 CRUD 不能直接改庫存（新增規格時的 `initialStock` 也是記成一筆進貨）。可售量＝庫存量－已保留量。**庫存、訂單、商品可購買狀態一律不讀快取**（`ArchitectureTests` 鎖定；並行安全靠交易內對規格列加更新鎖，並行扣減不會超賣）。 |
| 缺貨／可售 | 商品在資料庫只有 `draft`（下架）／`published`（上架）兩態；**「缺貨」由庫存自動判定**（`displayStatus = sold_out`：已上架且所有販售中規格可售量 ≤ 0）。 |
| 金流與發票 | **不串接**（B-10：LINE Pay 商店號未到位）。金流與電子發票以介面隔開（`ILinePayGateway`／`IEInvoiceService`，預設「尚未串接」實作）；訂單狀態機、庫存扣減與回補、退款紀錄等後台邏輯都能用。LINE Pay 訂單的退款執行目前回 **409「LINE Pay 尚未串接」**（案件退回原狀態）；現場收款訂單走人工退款。 |
| 不做 | 通知信（中獎通知、訂單通知、退款通知全部不寄，全系統沒有寄信通路）、折扣碼與會員價、多倉別／批號／預購、物流商 API、兩隊分潤計算與結算單、前台抽獎頁。 |
| 待付款釋回 | 目前**沒有背景排程**：`POST …/shop/orders/release-expired`（`shop.order.update`）把逾時未付款的訂單釋回庫存；日後前台結帳上線時由排程呼叫同一支服務。 |

### C1 回應形狀補充（2026-09-30，D 批回應 C 批畫面的回報）

> 下列都是**既有端點**的實際回應形狀，前面各表只寫「回詳情」的地方在這裡展開。所有金額是**整數元**、時間戳帶 `Z`、`…Label` 是日常中文直接顯示。

**庫存異動類型代碼（`movementType`，共 9 個，`GET …/inventory/movements` 的篩選參數 `type` 與 `POST …/inventory/movements` 的 `type` 用同一組代碼）**

| 代碼 | 標籤 | 誰產生 | `quantity` 正負與影響 |
|---|---|---|---|
| `stock_in` | 進貨 | 人工（`POST /movements`） | ＋，增加庫存量 |
| `stocktake` | 盤點 | 人工 | 送出的是「實際盤點總數」，記錄的 `quantity` ＝差額（可正可負），庫存量改成盤點值 |
| `damage` | 報損 | 人工（原因必填） | －，減少庫存量 |
| `adjust` | 調整 | 人工（原因必填） | 正負皆可（≠0） |
| `reserve` | 下單保留 | **系統**（建立待付款訂單） | ＋，只增加「已保留量」，庫存量不變 |
| `release` | 釋回保留 | **系統**（待付款取消／逾時釋回） | －，只減少已保留量 |
| `sale` | 售出扣減 | **系統**（付款成立；現場收款建單即扣） | －，同時減少庫存量與（有保留時）已保留量 |
| `cancel_restock` | 取消回補 | **系統**（已付款／備貨中的訂單被取消） | ＋，增加庫存量 |
| `return_restock` | 退貨回補 | **系統**（退貨驗收 `receive` 且 `restock: true`） | ＋，增加庫存量 |

人工只能送前四個（`POST /movements` 送其他代碼 → 400）；後五個只由系統流程產生，列表與訂單詳情的異動紀錄都看得到（`orderNo` 帶出關聯訂單）。`stockAfter`／`reservedAfter` 是該筆異動當下的快照。

**`POST /orders`（人工建立訂單）→ 201，回 `AdminOrderDetailDto`**：
`{ id, orderNo, createdAt, paidAt, completedAt, cancelledAt, cancelReason, subtotal, shippingFee, total, linepayTransactionId（現場收款為 null）, paymentStatus, paymentStatusLabel, paymentMethod, paymentMethodLabel, orderStatus（中文）, deliveryMethod, deliveryMethodLabel, shipmentStatusLabel, isMember, memberId, memberNo, isManual, sellingClubId, sellingClubCode, sellingClubName, collectingClubId, collectingClubName, recipientName／recipientPhone／recipientAddress（依權限遮罩）, customerNote, internalNote, settlementStatus(+Label), settledOn, settlementNote, items, shipment, invoice, refunds, availableActions, isMasked, canReveal, updatedAt }`。
子物件：`items[] = { id, variantId, productName, variantLabel, sku, unitPrice（下單當下的價格快照）, quantity, lineTotal, refundedQuantity }`；`shipment = { id, carrier, trackingNo, storeBranchCode, shippedAt, deliveredAt, pickupStatus(waiting／picked_up／overdue)(+Label), pickupDeadlineOn, arrivalNotifiedAt }`（尚未出貨為 `null`）；`invoice = { invoiceNo, issuedAt, issueStatus, voidStatus }`（沒有發票為 `null`）；`refunds[] = { id, status(+Label), refundAmount, reason, createdAt }`；`availableActions` 是動作代碼陣列（`mark_paid`／`prepare`／`ship`／`complete`／`cancel`／`request_refund`，畫面依此顯示按鈕，伺服器端仍各自檢查狀態）。**任何狀態動作（`prepare`／`cancel`／`ship`／`complete`／備註／分帳）的回應都是這個形狀。**

**S5 退款案件動作的回應**：`approve`／`reject`／`receive` 都回 `AdminRefundDetailDto`：
`{ id, orderId, orderNo, orderStatus, orderTotal, orderRefundedTotal, paymentMethod(+Label), status(+Label), refundAmount, reason, needsReturn, reviewNote, approvedByName, receivedAt, receivedByName, refundMethod(+Label), refundReference, refundedAt, refundedByName, items: [ { orderItemId, productName, variantLabel, sku, quantity, unitPrice } ], availableActions, createdAt, updatedAt }`；
`availableActions` 依狀態與權限給出：申請中→`approve`／`reject`；已核准（需退回）→`receive`／`reject`；已核准（不需退回）或已驗收→`execute`（**只有 `shop.refund.execute` 的人才看得到 `execute`**）。**`execute` 回 `{ refund: <上面的詳情>, invoiceAction: string｜null }`**；狀態不對一律 409（訊息「案件狀態不允許」＋目前狀態）；`reject` 的 `note` 必填（400）。

**F2 報名 `POST …/fan-events/{id}/registrations`（201）與 `PUT …/registrations/{registrationId}` 都回 `AdminFanEventRegistrationDto`**：
`{ id, memberId, memberNo, isMember, applicantName, phone, email（依 member.pii.reveal 遮罩）, status(registered／waitlist／cancelled／attended), statusLabel, note, createdAt, isMasked }`。`PUT` 之後重新讀清單即可看到候補遞補後的名額；**取消已報名者不會自動遞補候補者**（由人工把候補改成已報名，名額由伺服器把關）。

**K5 動作的回應形狀**

| 端點 | 回應 |
|---|---|
| `POST /{id}/roster/preview` | `{ asOf, eligibleCount }`（不寫入） |
| `POST /{id}/roster`（產生／作廢重產） | 活動詳情 `AdminDrawDetailDto`（含 `versions[]`、`rosterHash`、`availableActions[]`） |
| `PUT /{id}/winners`、`POST /{id}/winners/remove` | `{ updatedCount, draw: <活動詳情> }` |
| `PUT /{id}/fulfilment/{serialNo}` | 單筆發放 `{ serialNo, memberNo, memberName, isBackup, prizeName, claimMethod(+Label), recipientName, recipientPhone, recipientAddress, fulfilmentStatus, effectiveStatus(+Label), shippedAt, claimedAt, note, isMasked }` |
| `POST /{id}/fulfilment/batch/status` | `{ updatedCount, skipped: [ { serialNo, reason } ] }` |
| `POST /{id}/announcement-draft`（201） | `{ articleId, articleSlug, draw: <活動詳情> }` |
| `PUT /{id}/announcement-article`、`POST /{id}/mark-announced`、`POST /{id}/close`、`POST /{id}/void` | 活動詳情 |
| `GET /{id}/announcement-preview` | `{ drawName, prizeDescription, snapshotAt, eligibleCount, winners: [ { serialNo, memberNo, maskedName, prizeName } ] }` |

**規格編號（貨號）重複的 409 措辭（C 批回報第 2 項）**：貨號全站唯一（跨俱樂部）。撞到**本俱樂部**自己的規格 → 「貨號「X」已經被這個俱樂部的另一個規格使用，請換一個」；撞到**別的俱樂部**的規格 → 只回「貨號「X」無法使用，請換一個」，**不說已被使用、不說在哪一隊**（受範圍限制的帳號不得靠貨號探測對方的商品）。同理會員 Email 撞號（全站唯一）回「這個 Email 目前無法用來建立新的會員帳號…」，不確認這個人存在。

**全站時間格式通則（D 批統一，2026-09-30）**：`DateTime`（時間戳）**一律輸出 UTC 並帶 `Z`**；輸入無時區記號視為 UTC。實作在 `Common/UtcDateTimeJsonConverter.cs`（註冊於 `Program.cs` 的 `JsonOptions`），**所有模組同一套**（含 S1 以前的舊模組——它們原本輸出不帶 `Z` 的 UTC，如 `2026-09-30T09:12:33.123`，這個改動讓前端不必再猜）。`DateOnly`（台灣當地日期）與帶位移的 `DateTimeOffset` 不受影響。（E-90 的 `Kind=Unspecified` 規則不變：不得當成伺服器本機時間換算。）

### 權限碼與角色矩陣（`db/seed/generate-club-seed-sql.py`，`role_permissions` 已種入，共 50 碼）

| 權限碼 | 用途 | 系統管理員 | 內容編輯 | 商務／贊助 | 公關／媒體 | 客服／行政 | 檢視者 | 合作球隊管理（僅自家） |
|---|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| `culture.comic.view／create／update／delete` | F1 漫畫（**藍鯨不設，端點回 403**） | 全 | 全 | — | 全 | — | 唯讀 | —（藍鯨不設漫畫） |
| `culture.fan_event.view／create／update／delete` | F2 球迷會活動與報名 | 全 | 全 | — | 全 | — | 唯讀 | 全 |
| `shop.collection.*`（view／create／update／delete） | S1 商品系列 | 全 | view／create／update | 全 | 唯讀 | — | 唯讀 | 全 |
| `shop.product.*` | S1 商品、圖集 | 全 | view／create／update | 全 | 唯讀 | 唯讀 | 唯讀 | 全 |
| `shop.variant.*` | S1 規格與售價（**檢視者沒有＝看不到金額**） | 全 | 唯讀 | 全 | 唯讀 | 唯讀 | — | 全 |
| `shop.cost.view／update`（🔴 受限） | 商品成本（依 `docs/12b` §7：系統管理員與商務／贊助） | ✔ | — | ✔ | — | — | — | — |
| `shop.inventory.view／update` | S2 庫存 | 全 | — | 唯讀 | — | 全 | — | 全 |
| `shop.order.view／create／update` | S3 訂單、人工建立、狀態動作與分帳標記 | 全 | — | 唯讀（遮罩） | — | 全 | — | 全 |
| `shop.order.reveal` | 訂單收件人完整資料 | ✔ | — | — | — | ✔ | — | ✔ |
| `shop.order.export`（🔴 受限） | 訂單 CSV | ✔ | — | — | — | ✔ | — | — |
| `shop.shipment.view／update` | S4 出貨與物流 | 全 | — | — | — | 全 | — | 全 |
| `shop.refund.view／update` | S5 退貨案件（建立、審核、驗收） | 全 | — | — | — | 全 | — | 唯讀 |
| `shop.refund.execute`（**sysadmin_only**） | 執行退款 | ✔ | — | — | — | — | — | — |
| `shop.setting.view／update` | S6 運費與政策 | 全 | — | 全 | — | — | — | 全 |
| `shop.credential.view／update`（**sysadmin_only**＋受限） | S6 LINE Pay 與發票憑證 | ✔ | — | — | — | — | — | — |
| `shop.report.view`／`shop.report.export`（🔴 受限） | S6 報表／匯出 | 全 | — | 全 | — | — | — | 只有 view |
| `shop.donation_code.*`（**全系統共用，不分俱樂部**） | S6 發票捐贈碼 | 全 | — | 全 | — | — | — | — |
| `member.draw.view` | K5 檢視抽獎活動、名單、發放（**姓名與收件資訊一律遮罩，完整值需 `member.pii.reveal`**） | 全 | — | — | **唯讀（遮罩）**（C 批畫面回報後補：公關／媒體要能打開活動才寫得了公布稿） | 全 | — | — |
| `member.draw.create／update` | K5 建立活動、產生名單、回填中獎人、發放 | 全 | — | — | — | 全 | — | — |
| `member.draw.announce` | K5 產生公布稿（**只取得遮罩名單**；需搭配 `member.draw.view` 才打得開活動） | ✔ | — | — | ✔ | ✔ | — | — |
| `member.draw.export`（🔴 受限） | K5 中獎人聯絡名單與出貨清單 | ✔ | — | — | — | ✔ | — | — |

`GET /api/v1/admin/auth/me` 回傳權限碼清單，畫面用它決定要不要顯示按鈕（**權限碼只給程式判斷，不得顯示**）。

---

### F1 漫畫 `/api/v1/admin/{club}/comic`（藍鯨 → 一律 403「台中藍鯨不設漫畫，這個功能只在台中磐石使用。」，系統管理員也一樣）

| 方法 路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET／PUT /about` | view／update | **企劃設定（8.1 世界觀說明頁）** `{ zh: { title, body }, en?: { title, body }, updatedAt }`；PUT 整份取代（省略 `en` ＝ 移除英文版；`title` ≤ 200、`body` ≤ 20000）。 |
| `GET /characters` | view | 陣列（依 `sortOrder`）：`{ id, playerId, playerName, imageKey, imageUrl, imageThumbUrl, sortOrder, zh: { name, description }, en?, updatedAt }` |
| `GET /characters/{id}` | view | 同上單筆 |
| `POST /characters` → 201／`PUT /characters/{id}` | create／update | **multipart**：`payload` `{ playerId?（關聯真實球員，須屬於本俱樂部，否則 400）, sortOrder?, removeImage?, content: { zh: { name（必填 ≤64）, description? }, en? } }` ＋ 選填檔案欄位 `image`。新增省略 `sortOrder` ＝ 排最後。圖片規則同 E1a（新檔＝換圖、`removeImage`、同時給 → 400） |
| `DELETE /characters/{id}` → 204 | delete | 一併刪圖片物件 |
| `PUT /characters/order` → 204 | update | `{ ids }` 重排 |
| `GET /episodes?status=` | view | 陣列（集數新→舊）：`{ id, episodeNo, coverKey, coverUrl, coverThumbUrl, publishedOn, status(draft／published), statusLabel, isLatest, viewCount, pageCount, titleZh, titleEn, updatedAt }` |
| `GET /episodes/{id}` | view | 詳情：以上＋`zh: { title }`、`en?`、`pages: [ { id, imageKey, imageUrl, imageThumbUrl, imageWidth, imageHeight, sortOrder } ]`、`createdAt` |
| `POST /episodes` → 201／`PUT /episodes/{id}` | create／update | **multipart**：`payload` `{ episodeNo（≥1，同俱樂部唯一 → 重複 409）, publishedOn?, status（"draft"｜"published"）, removeCover?, content: { zh: { title（必填 ≤128） }, en? } }` ＋ 選填 `cover`。**新集數不能直接發布（還沒有內頁 → 400）；發布必須至少一張內頁**；發布時沒填 `publishedOn` ＝ 今天。 |
| `DELETE /episodes/{id}` → 204 | delete | 一併刪封面與全部內頁物件 |
| `POST /episodes/{id}/pages` → 201 | update | **multipart，檔案欄位 `files`（多張，≤60）**，依上傳順序接在既有內頁之後；回集數詳情 |
| `DELETE /episodes/{id}/pages/{pageId}` → 204 | update | 已發布的集數不能刪光內頁（409） |
| `PUT /episodes/{id}/pages/order` | update | `{ ids }` → 集數詳情 |

**`isLatest` 是系統自動判定，不是人工勾選**：已發布、發布日不晚於今天（沒有發布日視為已到）的**最大集數**；每次新增／更新／刪除集數後重算（未來發布日的集數不算）。全部集數免費公開閱讀，沒有付費牆欄位。閱讀數 `viewCount` 唯讀（前台讀取時累計）。

### F2 球迷會活動 `/api/v1/admin/{club}/fan-events`

| 方法 路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET ?status=&from=&to=&keyword=` | view | 陣列（開始時間新→舊）：`{ id, slug, startsAt, endsAt, registrationDeadlineAt, capacity, isPaidMembersOnly, status(draft／published), statusLabel, coverKey, coverThumbUrl, venueId, nameZh, nameEn, registeredCount, waitlistCount, isRegistrationOpen, updatedAt }`。`from`／`to` 是台灣日期（含當天）。`registeredCount` ＝ 已報名＋已到場（佔名額）；`isRegistrationOpen` ＝ 已發布、截止與開始時間還沒過、名額有空位 |
| `GET /{id}` | view | 詳情：以上＋`coverUrl`、`venueName`、`zh／en?: { name, description, location }`、`images: [ { id, imageKey, imageUrl, imageThumbUrl, imageWidth, imageHeight, sortOrder } ]`（**活動回顧圖集**）、`articles: [ { id, slug, titleZh, status } ]`（**活動回顧的關聯文章**）、`createdAt` |
| `POST` → 201／`PUT /{id}` | create／update | **multipart**：`payload` `{ slug?, startsAt?, endsAt?, registrationDeadlineAt?（不可晚於開始）, capacity?（≥1，省略＝不限；不可低於已報名人數 → 409）, isPaidMembersOnly, venueId?, status, removeCover?, articleIds?（省略＝不變；[]＝清空；有值＝整份取代，須是本俱樂部或共用文章）, content: { zh: { name（必填）, description?, location? }, en? } }` ＋ 選填 `cover`。**發布必須填開始時間**。 |
| `DELETE /{id}` → 204 | delete | **已有任何報名紀錄 → 409**（改為草稿）；一併刪封面與圖集物件 |
| `POST /{id}/images` → 201 | update | multipart `files`（多張，≤40）→ 詳情 |
| `DELETE /{id}/images/{imageId}` → 204／`PUT /{id}/images/order` | update | 同 F1 內頁 |
| `GET /{id}/registrations?status=&keyword=` | view | 陣列：`{ id, memberId, memberNo, isMember, applicantName, phone, email, status(registered／waitlist／cancelled／attended), statusLabel, note, createdAt, isMasked }`。**姓名／電話／Email 依 `member.pii.reveal` 遮罩**（F2 的角色不因此取得會員模組權限）；沒有權限時 `keyword` 只比對會員編號 |
| `POST /{id}/registrations` → 201 | update | 後台代填：`{ memberId?, applicantName?, phone?, email?, note? }`。會員報名只給 `memberId`；非會員必須有姓名與電話或 Email。**限付費會員的活動：非會員 → 400、沒有有效球迷會員會籍的會員 → 400**；同一會員重複報名 → 409；**名額已滿自動進候補**（回應 `status: "waitlist"`） |
| `PUT /{id}/registrations/{registrationId}` | update | `{ status, note? }`：`registered`／`waitlist`／`cancelled`／`attended`。轉入佔名額的狀態（已報名／已到場）時名額已滿 → 409；只有已報名的人能標到場；`note` 省略＝不變、空字串＝清除 |

### S1 商品與規格 `/api/v1/admin/{club}/shop/…`

**系列 `/collections`**（`shop.collection.*`）：`GET ?status=` 陣列 `{ id, slug, sortOrder, status, statusLabel, nameZh, nameEn, productCount, updatedAt }`；`GET /{id}` 詳情多 `zh／en?: { name, narrative }`（**系列介紹文**）、`createdAt`；`POST`（201）／`PUT /{id}`：`{ slug?, sortOrder?, status（draft／published）, content: { zh: { name, narrative? }, en? } }`；`DELETE /{id}`（**系列底下有商品 → 409**）；`PUT /order`。

**商品 `/products`**（`shop.product.*`）：

| 方法 路徑 | 說明 |
|---|---|
| `GET ?status=&collectionId=&keyword=&page=&pageSize=` | 分頁。項目：`{ id, slug, collectionId, collectionName, isNewArrival, sortOrder, status(draft／published), displayStatus(draft／published／sold_out), displayStatusLabel（下架（草稿）／上架／缺貨）, outOfStockBehavior(show_unavailable／hide), outOfStockBehaviorLabel, coverThumbUrl, nameZh, nameEn, variantCount, priceMin, priceMax, availableTotal, updatedAt }`。**`priceMin`／`priceMax`／`availableTotal` 只有持有 `shop.variant.view` 才有，否則 `null`**（檢視者看不到金額）。`keyword` 比對網址名稱、商品名稱與貨號 |
| `GET /{id}` | 詳情：以上＋`sizeChart`（任意 JSON，沒有＝`null`）、`zh／en?: { name, narrative, seoTitle, seoDescription, tags（逗號分隔） }`、`images: [ { id, imageKey, imageUrl, imageThumbUrl, width, height, sortOrder } ]`、`variants: [ <規格> ]`（沒有 `shop.variant.view` → `[]`）、`canViewVariants`、`canViewCost`、`createdAt` |
| `POST`（201）／`PUT /{id}` | JSON：`{ slug?, collectionId?（須屬於本俱樂部）, isNewArrival, sortOrder?, status, outOfStockBehavior?, sizeChart?（任意 JSON；PUT 省略＝清除）, content: { zh: { name（必填）, narrative?, seoTitle?, seoDescription?, tags? }, en? } }`。**新增時不能直接上架；上架前必須至少有一個販售中規格（400）**；`outOfStockBehavior` 省略：新增＝`show_unavailable`、更新＝不變 |
| `DELETE /{id}` → 204 | **已有訂單 → 409**（改為下架）；一併刪規格與圖片物件 |
| `PUT /order` | 排序 |
| `POST /{id}/images` → 201／`DELETE /{id}/images/{imageId}`／`PUT /{id}/images/order` | 圖集（`files` 多張 ≤20），權限 `shop.product.update`；回商品詳情 |

**規格 `/products/{productId}/variants`**（`shop.variant.*`）：`GET`（陣列）／`GET /{id}`／`POST`（201）／`PUT /{id}`／`DELETE /{id}`（**有訂單或保留紀錄 → 409，請改為停售**；沒有訂單的規格連同其庫存異動一併刪除）／`PUT /order`。
**規格 `<規格>`**：`{ id, productId, sku, size, colour, label（「尺寸／顏色」）, price, salePrice, effectivePrice, cost, stockQty, reservedQty, availableQty, status(active／inactive), statusLabel, lowStockThreshold, isLowStock, sortOrder, updatedAt }`。**`cost` 只有 `shop.cost.view` 看得到，其餘為 `null`**。
**寫入 payload**：`{ sku（必填，≤64，不可含空白，**全站唯一**含跨俱樂部 → 409）, size?, colour?, price（≥0）, salePrice?（0..price）, cost?, clearCost?, status?（active 預設／inactive）, lowStockThreshold?, sortOrder?, initialStock?（**只有新增有效**，記成一筆進貨）}`。**改成本需要 `shop.cost.update`，否則帶 `cost`／`clearCost` → 403；沒帶＝維持不變。** 庫存量不在這裡改。

### S2 庫存 `/api/v1/admin/{club}/shop/inventory`

| 方法 路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET ?keyword=&productId=&lowStockOnly=&status=&page=&pageSize=` | `shop.inventory.view` | 分頁，依可售量少→多：`{ variantId, productId, productName, productStatus, sku, label, status(+Label), stockQty, reservedQty, availableQty, lowStockThreshold, isLowStock }`。**`lowStockOnly=true` ＝ 補貨提醒清單**（販售中且可售量 ≤ 門檻；門檻＝規格自己的，沒有就用俱樂部設定 `lowStockThreshold`，預設 5） |
| `GET /movements?variantId=&type=&from=&to=&orderId=&page=&pageSize=` | 同上 | 分頁（新→舊）：`{ id, variantId, sku, productName, movementType, movementTypeLabel（進貨／盤點／報損／調整／下單保留／釋回保留／售出扣減／取消回補／退貨回補）, quantity（**有正負號**；`reserve`／`release` 是保留量的變動，其餘是庫存量）, stockAfter, reservedAfter, reason, orderId, orderNo, handledByName, occurredAt }` |
| `POST /movements` → 201 | `shop.inventory.update` | `{ variantId, type, quantity, reason? }`：`stock_in`（quantity > 0，增加）／`damage`（quantity > 0，減少，**原因必填**）／`adjust`（quantity 正負皆可、≠0，**原因必填**）／`stocktake`（**quantity＝實際盤點的庫存總數**，異動量＝差額，原因預設「盤點」）。扣到低於已保留量或負數 → **409「規格「XXX」的可售量不足（目前只剩 N）…」**，庫存不變、不留紀錄。回 `{ movement, item }`（`item` 同清單項） |

### S3 訂單 `/api/v1/admin/{club}/shop/orders`

| 方法 路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET ?paymentStatus=&orderStatus=&deliveryMethod=&paymentMethod=&settlementStatus=&isMember=&from=&to=&keyword=&page=&pageSize=` | `shop.order.view` | 分頁（新→舊）。`paymentStatus`：`pending／paid／failed／expired／refunded`；`deliveryMethod`：`home_delivery`（宅配）／`cvs_pickup`（超商取貨）／`onsite_pickup`（現場自取）；`paymentMethod`：`linepay`／`onsite`；`settlementStatus`：`pending`／`settled`；`from`／`to` 台灣日期（含當天）。項目：`{ id, orderNo, createdAt, paidAt, subtotal, shippingFee, total, paymentStatus(+Label), paymentMethod(+Label), orderStatus, deliveryMethod(+Label), shipmentStatusLabel（未出貨／已出貨／已送達／已領取）, isMember, isManual, sellingClubId, sellingClubCode, sellingClubName, settlementStatus(+Label), recipientName（遮罩依權限）, itemCount, isMasked }`。訂單編號依俱樂部加前綴 `TR-`／`BW-` |
| `GET /{id}` | 同上 | 詳情：`{ id, orderNo, createdAt, paidAt, completedAt, cancelledAt, cancelReason, subtotal, shippingFee, total, linepayTransactionId, paymentStatus(+Label), paymentMethod(+Label), orderStatus, deliveryMethod(+Label), shipmentStatusLabel, isMember, memberId, memberNo, isManual, sellingClubId/Code/Name, collectingClubId, collectingClubName, recipientName, recipientPhone, recipientAddress, customerNote, internalNote, settlementStatus(+Label), settledOn, settlementNote, items: [ { id, variantId, productName, variantLabel, sku, unitPrice, quantity, lineTotal, refundedQuantity } ]（**值複製快照，改名改價不影響歷史訂單**）, shipment: { id, carrier, trackingNo, storeBranchCode, shippedAt, deliveredAt, pickupStatus(waiting／picked_up／overdue), pickupStatusLabel, pickupDeadlineOn, arrivalNotifiedAt }｜null, invoice: { invoiceNo, issuedAt, issueStatus, voidStatus }｜null, refunds: [ { id, status(+Label), refundAmount, reason, createdAt } ], availableActions[], isMasked, canReveal, updatedAt }`。**`availableActions`（畫面依此顯示按鈕）**：`prepare`／`ship`／`complete`／`cancel`／`request_refund` |
| `POST` → 201 | `shop.order.create` | **人工建立與補登（現場收款）**：`{ items: [ { variantId, quantity } ]（1–50 個品項；同規格重複會合併）, deliveryMethod, recipientName?, recipientPhone?, recipientAddress?, memberId?, customerNote?, internalNote?, shippingFee?（覆寫）, completeImmediately?（僅現場自取，訂單直接完成）}`。宅配須有姓名＋電話＋地址、超商取貨須有姓名＋電話。**付款方式固定「現場收款」、付款狀態已付款、當下扣庫存（售出）**；價格取當下售價（有促銷價用促銷價）；運費依俱樂部設定（現場自取免運、達免運門檻免運，可用 `shippingFee` 覆寫）。可售量不足 → **409 且整張訂單不成立**。**購物車不得跨俱樂部混買**：規格必須屬於目前俱樂部（否則 400）。`sellingClubId` ＝ 目前俱樂部；`collectingClubId` ＝收款主體俱樂部（代收代付） |
| `PUT /{id}/notes` | `shop.order.update` | `{ internalNote }`（空白＝清除）→ 詳情 |
| `PUT /{id}/settlement` | 同上 | **代收代付分帳標記**（人工旗標，**不是狀態機**，系統不計算應付金額、不產生結算單）：`{ status（pending／settled）, settledOn?（標已結算省略＝今天）, note? }`；尚未付款的訂單 → 409 |
| `POST /batch/settlement` | 同上 | `{ ids（1–200）, status, settledOn?, note? }` → `{ updatedCount, skipped: [ { id, reason } ] }` |
| `POST /release-expired` | 同上 | 釋回逾時未付款的訂單（逾時＝俱樂部設定 `pendingTimeoutMinutes`，預設 30）→ `{ expiredCount, timeoutMinutes }` |
| `POST /{id}/prepare` | 同上 | 已付款 → 備貨中 |
| `POST /{id}/cancel` | 同上 | `{ reason（必填）}`。**待付款**：釋回保留的庫存；**已付款／備貨中**：回補庫存並**自動建立一張「已核准、不需退回商品」的全額退款案件**（等系統管理員執行退款）；其餘狀態 → 409（已出貨的請走退貨退款） |
| `POST /{id}/ship` | `shop.shipment.update` | `{ carrier?, trackingNo?（可先出貨、之後回填）, storeBranchCode?（**超商取貨必填**）, pickupDeadlineOn?（現場自取的領取期限）}`：已付款／備貨中 → 已出貨並建立出貨資料；現場自取＝備妥待領（`pickupStatus: waiting`） |
| `PUT /{id}/shipment` | 同上 | 更正物流資料 `{ carrier?, trackingNo?, storeBranchCode?, pickupDeadlineOn? }`（整份取代這四欄） |
| `POST /{id}/arrival-notified` | 同上 | 超商取貨到店通知（只記錄時間並進入待領取，**本系統不寄任何通知**）：`{ pickupDeadlineOn? }` |
| `POST /{id}/complete` | 同上 | 已出貨 → 已完成（超商取貨與現場自取同時標記已領取） |
| `GET /export?purpose=…（同清單篩選）` | `shop.order.export`（🔴） | CSV，**含完整收件人資料**；上限 20000 列（超過 400 請縮小條件） |

**所有狀態動作都是帶前置狀態的單句更新**：狀態不對 → 409（訊息「訂單狀態不允許」＋目前狀態），並行重送只有一個會成功，**不會重複扣庫存或重複退款**。動作回應一律是最新的訂單詳情。
**狀態機**：`待付款 → 已付款 → 備貨中 → 已出貨 → 已完成`；分支 `已取消`（待付款逾時／取消）、`退貨處理中`（有處理中的退貨案件）、`已退款`（全額退款完成）。**付款成立（金流回呼）由內部服務 `ShopOrderLifecycle.ConfirmPaymentAsync` 提供**（冪等：重送不重複扣庫存；已逾時的訂單不能再成立），前台結帳與 LINE Pay 回呼上線時呼叫它，目前只有現場收款（建單即已付款）會走到扣減。

### S4 出貨 `/api/v1/admin/{club}/shop/shipments`

| 方法 路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET ?orderStatus=&deliveryMethod=&pickupStatus=&keyword=&page=&pageSize=` | `shop.shipment.view` | 分頁。預設列出已付款／備貨中／已出貨的訂單（`orderStatus` 只接受這三個）；`pickupStatus`：`waiting`／`picked_up`／`overdue`（**待領取且已過領取期限＝逾期，讀取時換算**）。項目：`{ orderId, orderNo, orderStatus, deliveryMethod(+Label), createdAt, recipientName（依權限遮罩）, itemCount, shipment, isMasked }`（`shipment` 同訂單詳情） |
| `GET /picking-list?orderIds=…&orderIds=…` | 同上 | **揀貨單**（畫面直接列印）：待出貨（已付款／備貨中）訂單的品項依貨號加總 `{ generatedAt, orderCount, totalQuantity, lines: [ { sku, productName, variantLabel, quantity, orderCount } ] }`；省略 `orderIds`＝全部待出貨訂單（一次指定最多 200 張） |
| `GET /dispatch-slips?orderIds=…` | 同上 | **出貨單資料**（一張訂單一張，1–100 張）：`[ { orderId, orderNo, deliveryMethod(+Label), recipientName, recipientPhone, recipientAddress, storeBranchCode, customerNote, items: [ { sku, productName, variantLabel, quantity } ], isMasked } ]`；收件人依 `shop.order.reveal` 遮罩，有權限者的每次取得寫日誌 |
| `POST /batch/ship` | `shop.shipment.update` | `{ ids（1–200）, carrier? }` 批次標記已出貨 → `{ updatedCount, skipped: [ { id, reason } ] }`（超商取貨缺門市代碼、狀態不對的略過） |
| `POST /import` | 同上 | **物流單號 CSV 批次回填**：multipart 檔案欄位 `file`（≤2 MB、≤2000 列）。第一列表頭，順序不拘，欄位 `訂單編號`（必填）、`物流單號`（必填）、`物流商`、`門市代碼`（英文表頭 `order_no`／`tracking_no`／`carrier`／`store_branch_code` 也可）。已付款／備貨中的訂單一併標記已出貨；已出貨／已完成的只更新物流資料；其餘略過 → `{ updatedCount, skipped: [ { row, orderNo, reason } ] }` |

### S5 退貨與退款 `/api/v1/admin/{club}/shop/refunds`

案件承接前台以**表單或客服信箱**進來的申請（不做專屬的線上退貨精靈），由客服在後台建立。狀態：`requested 申請中 → approved 已核准 → received 已驗收退回品 → refunded 已退款`（`processing` 退款處理中是執行瞬間的鎖定狀態），另有 `rejected 已駁回`。**不需退回商品的案件（`needsReturn: false`，例如取消未出貨訂單自動建立的）核准後直接可執行退款。**

| 方法 路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET ?status=&keyword=&page=&pageSize=` | `shop.refund.view` | 分頁（申請中優先）：`{ id, orderId, orderNo, status(+Label), refundAmount, reason, needsReturn, paymentMethod(+Label), refundMethod(+Label), createdAt, updatedAt }`；`keyword` 比對訂單編號 |
| `GET /{id}` | 同上 | 詳情：以上＋`orderStatus`、`orderTotal`、`orderRefundedTotal`、`reviewNote`、`approvedByName`、`receivedAt`、`receivedByName`、`refundReference`、`refundedAt`、`refundedByName`、`items: [ { orderItemId, productName, variantLabel, sku, quantity, unitPrice } ]`、`availableActions[]`（`approve`／`reject`／`receive`／`execute`） |
| `POST` → 201 | `shop.refund.update` | `{ orderId, reason（必填）, items: [ { orderItemId, quantity } ]（**支援部分退款**，不可超過尚可退的數量）, refundAmount?（省略＝所退品項小計；要連運費一起退請自行填，不可超過這張訂單尚可退的金額）, needsReturn?（預設 true）}`。**只有已出貨／已完成的已付款訂單可申請**（尚未出貨請直接取消訂單，409）；同一張訂單同時只能有一件處理中案件（409）；建立後訂單進入「退貨處理中」 |
| `POST /{id}/approve` | 同上 | `{ note? }` 申請中 → 已核准 |
| `POST /{id}/reject` | 同上 | `{ note（必填）}` 申請中／已核准 → 已駁回；訂單回到「已完成」或「已出貨」（沒有其他處理中案件時） |
| `POST /{id}/receive` | 同上 | **退回驗收**：`{ restock?（預設 true，商品損毀無法再賣請設 false）, note? }` 已核准（需退回）→ 已驗收；`restock` 時把品項**回補庫存**（`return_restock` 異動）。不需退回的案件 → 409 |
| `POST /{id}/execute` | `shop.refund.execute`（**sysadmin_only**） | `{ note? }` **退款執行**：LINE Pay 訂單走原路退回（**目前 409「LINE Pay 尚未串接」，案件退回原狀態、不會留下已退款痕跡**）；現場收款訂單以**人工退款**登錄經辦人。**防重複退款**：先以單句更新搶到「退款處理中」，並行的第二個請求 409。成功後：**全額**（累計退款達訂單總額）→ 付款狀態 `refunded`、訂單「已退款」（已取消的訂單維持「已取消」）；**部分退款** → 訂單回到「已完成」、付款狀態維持已付款。**已開立的發票同步登記作廢（全額）或折讓（部分）**。回 `{ refund: <詳情>, invoiceAction }`，`invoiceAction`（有發票才有）是給畫面的一句話——**電子發票服務未串接時註明「請至發票服務端手動處理發票 XXX」** |

### S6 設定與報表

**商店設定** `GET／PUT /api/v1/admin/{club}/shop/settings`（`shop.setting.view／update`；**運費設定是俱樂部層級**，兩隊各自一份；`PUT` 整份取代）：
`{ collectingSubject: { clubId, name, notice }, shippingFee（單一固定運費，元）, freeShippingThreshold?（免運門檻；省略＝不設）, excludedRegions: string[]（離島與不配送地區，去除重複，≤60）, lowStockThreshold（預設 5）, pendingTimeoutMinutes（5–1440，預設 30）, entryTitle／entryIntro／policyNotice／policyShipping／policyReturns／policyTerms: { zh?, en? }（商店入口與政策，中英，各 ≤20000 字）, updatedAt }`。
**🔴 介面必須顯示 `collectingSubject.notice`**（「本商店的收款主體是俱樂部，不是慈善捐款平台的主辦協會…」）。**不做重量或級距計費。**

**憑證** `/api/v1/admin/{club}/shop/credentials`（**僅系統管理員**：`shop.credential.*` 是 sysadmin_only；憑證屬於**收款主體俱樂部**，不論從哪個站台操作都是同一組）：

| 方法 路徑 | 說明 |
|---|---|
| `GET` | `{ collectingSubject, environment（sandbox／production，目前使用的金流環境）, linePay: { sandbox, production }, eInvoice: { sandbox, production }, invoiceRetry: { maxRetries, intervalMinutes }, integrationConnected }`；每個環境 `{ configured, identifierMasked（例 `******7890`）, invoicePrefix, rotatedAt（金鑰輪替時間）}`。**密鑰永遠不回傳、識別碼只回遮罩、任何日誌都不含密鑰；憑證以 Data Protection 加密存放。`integrationConnected` 恆為 `false`（B-10：憑證可先存放，系統目前不會用它連線）** |
| `PUT /linepay` | `{ environment, channelId, channelSecret? }`：第一次設定必須有密鑰；之後省略密鑰＝沿用；**換密鑰才更新輪替時間** → 回 GET 的內容 |
| `PUT /einvoice` | `{ environment, merchantId?, apiKey?, invoicePrefix（字軌，兩位大寫英文字母，小寫會轉大寫）}` |
| `PUT /mode` | `{ environment }` 金流環境開關（測試／正式） |
| `PUT /invoice-retry` | `{ maxRetries（0–10）, intervalMinutes（1–1440）}` 開立與作廢的重試設定 |

**報表** `/api/v1/admin/{club}/shop/reports`：

| 方法 路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET /summary?from=&to=` | `shop.report.view` | 預設最近 30 天（`from`／`to` 台灣日期含當天，最長 800 天）：`{ from, to, grossRevenue, refundedAmount, netRevenue, orderCount, averageOrderValue, returnOrderCount, returnRatePercent, topSkus: [ { sku, productName, variantLabel, quantity, revenue } ]（前 10，依數量）, lowStockCount, outOfStockCount, totalAvailableQuantity }`。**口徑（現金基礎）**：營收＝付款成立時間落在期間內的訂單總額（含運費，不論之後是否取消）；退款＝退款執行時間落在期間內的金額；淨營收＝營收－退款；客單價＝營收÷訂單數；退貨率＝期間內有退款的訂單數÷訂單數；庫存是即時值 |
| `GET /by-selling-club?from=&to=` | 同上 | **依 `selling_club_id` 的加總，供線下分帳**：只列帳號有授權的俱樂部（系統管理員＝全部）`[ { sellingClubId, sellingClubCode, sellingClubName, orderCount, grossRevenue, refundedAmount, netRevenue, settledAmount（分帳標記為已結算的訂單總額）, pendingSettlementAmount（待結算） } ]`。**不計算應付金額、不產生結算單**（兩隊分潤走線下合約） |
| `GET /export?kind=summary｜by-selling-club&from=&to=&purpose=…` | `shop.report.export`（🔴） | CSV（`kind` 省略＝summary），須填用途並寫日誌 |

**發票捐贈碼**（**全系統共用、不分俱樂部**，走全域路徑，不含 `{club}`）`/api/v1/admin/shop/donation-codes`（`shop.donation_code.*`）：`GET`（陣列 `{ id, code, orgName, isActive, sortOrder, updatedAt }`）／`GET /{id}`／`POST`（201）／`PUT /{id}`／`DELETE /{id}`（204）；payload `{ code（3–7 位數字，重複 409）, orgName（必填）, isActive?（預設 true）, sortOrder? }`。

### K5 抽獎名單 `/api/v1/admin/{club}/draws`

**🔴 系統不抽出**：沒有任何隨機抽出的端點；實體抽獎於現場或直播由人工進行，中獎人以**序號**回填。各俱樂部各自舉辦（不合辦）。**資格條件固定、不可由後台自訂**：基準時間當下持有**本活動主辦俱樂部**的球迷會員（`fan_club`）會籍（**狀態有效或已批次到期但基準時間落在會籍期間內**；待確認與已取消不算）且帳號啟用；同時持有兩隊會籍者在兩份名單各佔一號（**活動辦法須明示「同時具備兩隊會籍者可分別參加兩隊抽獎」**）。

**狀態流**：`draft 草稿 → roster_locked 名單已鎖定 → drawn 已抽出 → announced 已公布 → closed 已結案`，另有 `voided 作廢`（結案前任何時候）。

| 方法 路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET /notice`／`PUT /notice` | view／update | **蒐集告知確認**：`{ confirmed, confirmedAt }`；PUT `{ confirmed }`。**未確認前不得舉辦抽獎——產生名單會 409**（會員條款須增列「會籍有效期間將自動列入球迷會員抽獎合格名單；中獎時，姓名將以遮罩方式於最新消息公布」，每個俱樂部各自確認） |
| `GET ?status=&keyword=&page=&pageSize=` | `member.draw.view` | 分頁（新→舊）：`{ id, drawCode, nameZh, nameEn, snapshotAt, drawnAt, drawOccasion(+Label), claimDeadlineOn, status, statusLabel, rosterVersion, totalCount（合格人數）, winnerCount, backupCount, fulfilledCount（已寄出或已領取）, announcementArticleId, announcementStatus(+Label), createdByName, createdAt, updatedAt }` |
| `GET /{id}` | 同上 | 詳情：以上＋`coverKey／coverUrl／coverThumbUrl`、`internalNote`、`rosterHash`、`lockedAt`、`lockedByName`、`zh／en?: { name, prizeDescription, rules, notes }`、`versions: [ { version, snapshotAt, totalCount, rosterHash, generatedAt, generatedByName, voidedAt, voidReason, isCurrent } ]`（**名單版本歷程，作廢的舊版保留、不可刪除**）、`availableActions[]`（`edit`／`generate_roster`／`regenerate_roster`／`record_winners`／`announce`／`mark_announced`／`close`／`void`／`delete`） |
| `POST` → 201／`PUT /{id}` | `create`／`update` | **multipart**：`payload` `{ drawCode?（英數字與連字號 ≤32，同俱樂部唯一 → 409；新增省略＝自動產生 `Dyyyymmdd-XXXX`；會出現在匯出檔名）, snapshotAt?（資格基準時間，UTC；**省略且有開獎時間＝開獎日（台灣時間）當天 00:00**）, drawnAt?（開獎時間，UTC）, drawOccasion?（home_match 主場賽事日／livestream 直播／other 其他）, claimDeadlineOn?（領獎期限）, internalNote?, removeCover?, content: { zh: { name（必填）, prizeDescription?, rules?（**活動辦法：鎖定名單前必填**）, notes? }, en? } }` ＋ 選填 `cover`。**名單鎖定後不能改活動代碼與基準時間（409）**；已結案／作廢不能編輯 |
| `DELETE /{id}` → 204 | update | **只有還沒產生過名單的草稿**可刪；否則 409（不辦了請作廢） |
| `POST /{id}/roster/preview` | update | **試算**：`{ asOf, eligibleCount }`，不寫入資料、不配發序號（沒設基準時間 → 400） |
| `POST /{id}/roster` | update | **產生並鎖定名單**：依會員編號升冪配發連號序號 1…N（一人一號），複製當下姓名、會員編號、層級與到期日，記錄基準時間、合格人數、**名單雜湊（SHA-256）**與執行人，狀態 → 名單已鎖定。前置：蒐集告知已確認、有基準時間、**活動辦法（中文）已填**、至少一位合格會員（各 409／400）。**名單已鎖定時再送＝作廢重產**：`{ voidReason（必填）}`，版本 +1、舊版標記作廢並保留；已進入抽出階段（已抽出／已公布）不可重產（409）。回活動詳情 |
| `GET /{id}/roster?version=&keyword=&winnersOnly=&page=&pageSize=` | view | 分頁（序號升冪，預設目前版本，可指定舊版 `version`）：`{ serialNo, memberNo, name, tier(+Label), membershipEndOn, isWinner, isBackup, prizeName, isMasked }`。**姓名依 `member.pii.reveal` 遮罩（王○明）**；沒有權限時 `keyword` 只比對序號與會員編號 |
| `PUT /{id}/winners` | update | **回填中獎人（以序號為準）**：`{ winners: [ { serialNo, prizeName（中獎必填 ≤128）, isBackup? } ]（1–200 筆，序號不可重複）, reason? }`。系統比對序號存在於目前版本，不符 → 400；**備取**（`isBackup: true`，原中獎人逾期未領時遞補）；**遞補＝同一個序號再送一次 `isBackup: false` 與獎項**；中獎人不能直接改備取（409，請先取消中獎）。首次回填中獎人時狀態 → 已抽出。**名單已公布後再修改必須填 `reason`（400）**；已結案／作廢 409。回 `{ updatedCount, draw }` |
| `POST /{id}/winners/remove` | update | `{ serialNos, reason? }` 取消中獎／備取標記（清除發放資料）；全部取消後狀態回到名單已鎖定 |
| `GET /{id}/fulfilment?status=&claimMethod=&page=&pageSize=` | view | **獎品發放（比照 K3）**：中獎人清單 `{ serialNo, memberNo, memberName, isBackup, prizeName, claimMethod(ship 寄送／pickup 現場領取)(+Label), recipientName, recipientPhone, recipientAddress, fulfilmentStatus(pending／shipped／claimed), effectiveStatus(+Label), shippedAt, claimedAt, note, isMasked }`。**`effectiveStatus`：待處理且已過領獎期限自動為 `overdue`（逾期）**；姓名與收件資訊依 `member.pii.reveal` 遮罩 |
| `PUT /{id}/fulfilment/{serialNo}` | update | `{ claimMethod?, recipientName?, recipientPhone?, recipientAddress?, status?, note? }`（省略＝不變；`note` 空字串＝清除）。**改收件資訊需要 `member.pii.reveal`（否則 403）**；`shipped` 只有寄送的能標且必須有收件人姓名／電話／地址（400）；`claimed` 記領取時間；`pending` 清掉兩個時間。只有已抽出／已公布的活動可改（409）。非中獎序號 → 404 |
| `POST /{id}/fulfilment/batch/status` | update | `{ serialNos, status }` → `{ updatedCount, skipped: [ { serialNo, reason } ] }` |
| `GET /{id}/export/public?purpose=` | view | **現場抽獎／可公開投影版 CSV** `draw-<活動代碼>-v<版本>-public.csv`：抽獎序號、會員編號、**遮罩姓名**、活動代碼、名單版本、基準時間（台灣時間）。全體合格名單，與 K1 檢視同級權限，**須填用途並寫日誌** |
| `GET /{id}/export/winners?purpose=` | `member.draw.export`（🔴） | **中獎人聯絡用（受限版）** `…-winners.csv`：**僅已回填的中獎人**（不得匯出全體合格名單的完整個資），欄位：抽獎序號、會員編號、姓名、手機、Email、領獎方式、收件人、收件電話、收件地址、獎項 |
| `GET /{id}/export/shipping?purpose=` | 同上 | **獎品出貨清單** `…-shipping.csv`（比照 K3）：中獎人、獎項、領獎方式、收件資訊、發放狀態（逾期會標示） |
| `GET /{id}/announcement-preview` | `member.draw.announce` | 公布稿預覽 `{ drawName, prizeDescription, snapshotAt, eligibleCount, winners: [ { serialNo, memberNo, maskedName, prizeName } ] }`——**公關／媒體只拿到遮罩名單，不因此取得會員模組權限** |
| `POST /{id}/announcement-draft` → 201 | 同上 | **交接 B2**：產生一篇**草稿**文章（分類 7.1 Club News＝`club`、加掛標籤「球迷會員抽獎／Member Draw」〔slug `member-draw`〕，內文只有活動名稱、獎品、基準時間、合格人數與遮罩後中獎名單），並連結到活動 → `{ articleId, articleSlug, draw }`。已連結過 → 409。**內文是 `{"blocks":[{"type":"paragraph","text":"…"}]}` 區塊 JSON**（格式以文章編輯器為準，見待裁決） |
| `PUT /{id}/announcement-article` | 同上 | `{ articleId }` 連結既有文章（須是本俱樂部或共用；已結案／作廢 409） |
| `POST /{id}/mark-announced` | `update` | 已抽出 → 已公布：**連結的文章必須已發布**（否則 409 說明）。列表顯示公布狀態與連結 |
| `POST /{id}/close` | 同上 | 已公布 → 已結案（唯讀） |
| `POST /{id}/void` | 同上 | `{ reason（必填）}` 作廢（保留資料，不可刪除；原因記在內部備註） |

**個資與稽核**：抽獎名單視同會員個資。產生／重產名單、回填中獎人、取消中獎、匯出（三種）、檢視完整姓名與收件資訊、結案、作廢都寫**敏感操作日誌**（活動代碼、版本、雜湊、序號、筆數、用途／原因，**不含個資本身**）。**不寄中獎通知信、不做站內信、不做 LINE 推播**；如需聯繫中獎人由客服電話處理。**扣繳憑單資料（身分證統一編號、戶籍地址）本批不蒐集**（門檻待會計師確認，`withholding_data_encrypted` 欄位保留、沒有端點），首波獎品單價建議壓在門檻以下。

### 新聞挑選搜尋（A 批畫面回報，2026-09-30）

`GET /api/v1/admin/{club}/news/lookup?keyword=&status=&category=&ids=&page=&pageSize=`（`content.article.view`）——**關聯報導的挑選視窗專用**（球迷會活動回顧、贊助故事、抽獎公布稿…）：範圍同新聞清單（本俱樂部＋共用）；`keyword` 比對**中文標題、英文標題與網址名稱**；**分頁**（預設 20、上限 50，舊文章翻頁就選得到，不受「一次最多 100 篇」限制）；`ids`（可重複 `ids=<id>&ids=<id>`，最多 200）把已選取的 id 解回標題；依發布時間新→舊（草稿排後）。回 `{ items: [ { id, slug, categoryCode, status, statusLabel, publishedAt, isShared, titleZh, titleEn } ], page, pageSize, totalCount, totalPages }`（精簡欄位，不含內文）。既有 `GET …/news?keyword=` 的關鍵字也一併擴充為比對英文標題與網址名稱。

### 待裁決（規劃書沒寫又影響客戶可見行為，本批先採最保守做法）

1. **商品狀態「草稿」與「下架」無法區分**：規劃書 S1 寫四態（草稿／上架／缺貨／下架），但 S1-8 已把 `products.status` 收斂為 `draft`／`published`（同 `press_resources`），故只有「下架（草稿）」一種；缺貨由庫存自動判定。要區分需加欄位並重開該決定。
2. **成本欄位的授權角色**：規劃書只寫「成本僅授權角色可見」，`docs/12b` §7 寫明「系統管理員、商務／贊助」——照它指派 `shop.cost.view／update`（其他角色沒有；合作球隊管理有規格權限但沒有成本），日後由 J2 角色管理調整。
3. **合作球隊管理（藍鯨方）的商店權限**：矩陣寫「自家商品與訂單」。本批給商品／規格／庫存／訂單（含 `shop.order.reveal`）／出貨／退款檢視／設定／報表檢視，**不含**成本、退款處理與執行、匯出、憑證。
4. **人工建立訂單的付款方式**：規劃書「人工建立與補登（現場收款、賽事日擺攤）」——本批固定「現場收款」、當下即已付款並扣庫存；沒有「人工建立待付款訂單」。
5. **取消已付款訂單的退款**：規劃書只寫「取消回補」。本批取消已付款／備貨中訂單時**自動建立全額退款案件**（已核准、不需退回），等系統管理員執行退款，避免「取消了卻沒人退款」。
6. **待付款逾時釋回沒有背景排程**：規劃書寫「自動釋回」，但前台結帳尚未開發、系統裡目前沒有排程機制；本批提供服務與 `release-expired` 端點，前台結帳上線時需補排程（例如每分鐘一次）。
7. **公布稿內文格式**：抽獎公布稿以 `{"blocks":[{"type":"paragraph","text":"…"}]}` 寫入文章內文，實際格式以 B2 文章編輯器（`apps/admin`）為準，不合時請告知調整。
8. **蒐集告知確認**：規劃書要求「未完成告知前不得舉辦抽獎」，告知文字在會員條款（I 設定）。本批以 `member.draw_notice_confirmed` 旗標（每俱樂部）承接：管理者確認條款已增列後在 K5 勾選，未勾選不能產生名單。條款本身的維護不在本批。
9. **歷史基準時間的資格判定**：以會籍的起訖日期與「有效／已到期」狀態判定，**會員帳號狀態取「現在」的值**（沒有帳號狀態歷程）。
10. **電子發票與稅務**：代收代付的稅務認定與發票字軌屬規劃書 §10 待確認事項；退款登記作廢／折讓的欄位已備，實際開立與作廢待服務串接。
11. **報表口徑**：見 S6 報表（現金基礎，取消的已付款訂單仍計入營收直到退款執行）；若要改為會計認列口徑需另行定義。

### 已知限制

- 金流（LINE Pay）與電子發票**不串接**（B-10）；前台結帳、金流回呼、發票開立都尚未開發，訂單只能由後台「現場收款」建立（種子另有示範用的待付款／已付款…各狀態訂單）。
- 沒有寄信通路：中獎通知、訂單通知、到店通知、退款通知一律不寄；到店通知只記錄時間。
- 公開讀取端點（前台漫畫閱讀、球迷活動報名、商店櫥窗與購物車、結帳）本批**未做**（任務範圍是後台 API）；因此本批寫入不做公開快取失效。
- 庫存並行安全靠資料庫更新鎖；單機單資料庫前提下成立，換成多副本讀取複本需重新評估。
- 訂單匯出上限 20000 列；報表期間上限 800 天。
- 名單產生一次寫入全部合格會員（數千人可）；上萬人以上需改批次寫入。

### 表與種子變更（C1）

- **綱要**（`db/club-schema.sql`＋EF migration `AlignSchemaC1`，同步 `docs/12`／`12b`／`12c`）：新增 **3 張表**（`fan_event_images`、`fan_event_articles`、`draw_roster_versions`）；`comic_episodes`（`status` 收斂為 `draft`／`published`、`UNIQUE(club_id, episode_no)`）、`comic_pages`（圖片寬高）、`fan_events`（封面、結束時間、報名截止、場地、狀態）與 `fan_events_i18n`（地點）、`fan_event_registrations`（狀態約束、非會員姓名／電話／Email／備註、**同活動同會員唯一（排除已取消）**）、`products`（缺貨顯示方式）、`product_variants`（狀態、低庫存門檻、排序、**庫存非負且保留量 ≤ 庫存量的 CHECK**）、`inventory_movements`（類型約束、異動後水位）、`orders`（付款方式、顧客備註、內部註記、**分帳旗標**、完成／取消時間與原因）、`shipments`（領取期限、到店通知、**每張訂單一筆**）、`refund_requests`（狀態約束、是否需退回、驗收、審核意見、退款經辦、退款序號）、`member_draws`（內部備註）、`draw_rosters`（**名單版本，唯一鍵含版本**、備取、收件資訊、寄出／領取時間、發放與領獎方式約束）。
- **權限**：49 個新權限碼與角色指派（見上表）。
- **種子**（`db/seed/backoffice_seed.py`，全部【測試】虛構）：漫畫企劃設定＋3 角色＋3 集草稿；球迷活動 4 場（磐石 3、藍鯨 1）與報名；商店設定與 2 個發票捐贈碼（`9990001`／`9990002`）；商品系列 4、商品 6（含低庫存、缺貨、草稿示範）、規格 11；訂單 9 張（磐石 8 涵蓋待付款／已付款／備貨中／已出貨／已完成／已取消／退貨處理中／現場收款，藍鯨 1 張代收代付）、出貨 3 筆、退款案件 2 件，庫存與異動同步；抽獎 2 場（`TEST-DRAW-01` 已抽出，名單 2 人、1 位中獎；`TEST-DRAW-02` 草稿）與蒐集告知確認。

### 測試（`Tcrfc.Api.Tests`）

`AdminCultureTests`（F1／F2＋商品圖集與抽獎封面，真實 Azurite）、`AdminShopCatalogTests`（S1／S2，含並行扣減不超賣）、`AdminShopOrdersTests`（S3／S4／S5，含並行搶最後庫存、付款回呼冪等、逾時釋回、假金流下並行退款只退一次）、`AdminShopSettingsTests`（S6）、`AdminDrawsTests`（K5）、`AdminC1MiscTests`（新聞挑選搜尋、會籍球季、商店類別不注入快取的反射檢查＋庫存欄位只有 `InventoryService` 能寫的原始碼掃描）。共用工具 `C1TestSupport.cs`／`ShopTestSupport.cs`。**測試一律在 `finally` 清資料；會改動共用設定的測試用 `C1Test.SnapshotSettingsAsync` 拍照還原（`E-81`／`E-89`）。**

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

#### 一行版（2026-09-30 起，推薦）：設定檔自動讀取，不必 export

```bash
# 第一次（或換機器）：從 .env 的 MSSQL_DEV_SA_PASSWORD 與 deploy/dev/club.env 的 JWT_SIGNING_KEY_CLUB
# 產生 apps/api/appsettings.Development.json（已存在不覆寫，要重產加 --force；不會印出密碼）
apps/api/scripts/init-local-settings.sh

cd apps/api && dotnet run                      # 用 Properties/launchSettings.json 的 http 設定檔：Development、http://127.0.0.1:5299
cd apps/api/Tcrfc.Api.Tests && dotnet test     # 沒有 export 也能跑；連線字串取自同一份檔案
```

- `appsettings.Development.json` **已被 `.gitignore` 與 `.dockerignore` 排除**（含密碼與金鑰），納管的只有
  佔位範本 `appsettings.Development.example.json`（手動建立時複製它）。
- 鍵名沿用環境變數的扁平鍵：`CLUB_SQL_CONNECTION_STRING`、`JWT_SIGNING_KEY_CLUB`、`CORS_ALLOWED_ORIGINS`、
  `DATA_PROTECTION_KEYS_PATH`。要連 Redis／Azurite 才另加 `REDIS_HOST`、`AZURE_BLOB_CONNECTION_STRING` 等
  （⚠️ 測試主機也會讀這個檔，加了會改變測試預期，見 `docs/14-invariants.md`；建議這兩項仍用環境變數臨時帶入）。
- **環境變數優先於檔案**（ASP.NET Core 預設順序），所以下面舊的 export 做法照舊有效，Docker 與正式環境不受影響。
- `dotnet test`：`Fixtures/TestLocalSettings.cs` 只在環境變數未設定時，從該檔補 `CLUB_SQL_CONNECTION_STRING`；
  `TestDatabaseGuard` 的「只准連 `tcrfc_club`」檢查不變。JWT 金鑰仍由各 fixture 以環境變數設為測試專用值（E-79）。
- VS Code：開專案根目錄按 F5 選「啟動並偵錯 API」（`.vscode/launch.json`／`tasks.json`，納管）。

#### 替代方案：export 環境變數（舊做法）


```bash
cd apps/api
dotnet build

# 連線字串等機密一律用環境變數，⛔ 絕對不要寫進任何檔案並 commit。
# 🔴 用 shell 變數存這種帶分號的連線字串時務必加引號——不加引號被當成 shell 腳本
#    source 執行時，分號會被解讀成命令分隔符，字串會在第一個分號處被悄悄截斷
#    （見 docs/18-work-errors.md，本次任務期間在本機驗證時才發現，非常隱蔽因為
#    程式仍會啟動、仍會嘗試連線，只是連線字串缺了 Database／帳密／TrustServerCertificate）。
export ASPNETCORE_ENVIRONMENT=Development
export CLUB_SQL_CONNECTION_STRING="Server=127.0.0.1,1433;Database=tcrfc_club;User Id=sa;Password=<你的 MSSQL_DEV_SA_PASSWORD>;TrustServerCertificate=True;Encrypt=False;"
export CORS_ALLOWED_ORIGINS="http://localhost:3000,http://localhost:3001,http://localhost:3002,http://localhost:3003,http://localhost:5174,http://localhost:5175"   # 5174＝apps/admin 後台、3003／5175＝慈善前台／慈善後台；少了它們登入會被 CORS 擋下
export ASPNETCORE_URLS="http://127.0.0.1:5299"
# 🔴 必填：少了它行程照樣啟動，但 JWT 驗證參數是第一個請求進來時才建構，之後「每一支端點」
#    （連 /healthz）都回 500（E-79）。值取自 deploy/dev/club.env，不必把金鑰打在指令上：
export JWT_SIGNING_KEY_CLUB="$(grep '^JWT_SIGNING_KEY_CLUB=' ../../deploy/dev/club.env | cut -d= -f2-)"
# 建議：固定 Data Protection 金鑰目錄，否則每次重啟都會讓已設定兩階段驗證的帳號解不開（見下方）
export DATA_PROTECTION_KEYS_PATH="$HOME/.tcrfc/dp-keys"

dotnet run --no-launch-profile   # 明確不用 launchSettings，全靠上面的 export（此時仍會讀 appsettings.Development.json 若存在，但環境變數優先）
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
> export CLUB_SQL_CONNECTION_STRING="Server=127.0.0.1,1433;Database=tcrfc_club;User Id=sa;Password=${PW};TrustServerCertificate=True;Encrypt=False;"
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

### 本機開發：Azurite（圖片／文件／影片上傳要用，2026-10-05）

沒有 `AZURE_BLOB_CONNECTION_STRING` 時，所有上傳端點一律回「檔案儲存尚未設定」（503，正確行為）——要在本機實際驗證上傳
（S2-1／S2-2／S2-3／S3-1／S3-3／AP-1 與各處圖片欄位），三步：

```bash
apps/api/scripts/dev-azurite.sh up                 # 1. 起 Azurite 容器（127.0.0.1:10000，volume 保留資料；status／down／reset 同一支）
python3 db/seed/seed-dev-blobs.py                  # 2. 預建 images／videos／documents（公開讀取）與 proposals（私有）＋漫畫占位圖
cd apps/api && dotnet run --launch-profile http-azurite   # 3. 啟動 API（launchSettings 的 http-azurite 設定檔多帶 UseDevelopmentStorage=true）
```

- **為什麼用 launch profile 而不是寫進 `appsettings.Development.json`**：上面「直接用 dotnet 跑」一節已說明，測試主機也會讀那個檔，
  加了 Blob 連線會改變測試預期；launch profile 的環境變數只影響 `dotnet run`，不影響 `dotnet test`。
- **第 2 步不能省**：API 自建容器時一律是私有（正式環境的公開讀取由 Bicep 設定，docs/17 §13）。本機若讓 API 自建，
  上傳成功但瀏覽器直接讀圖片網址會 403，後台縮圖與前台圖片全部破圖。腳本預先把三個公開容器建成「匿名可讀 Blob」；
  已被 API 建成私有的，執行 `python3 db/seed/seed-dev-blobs.py --fix-access`。
- `UseDevelopmentStorage=true` 是 Azure SDK 內建簡寫，展開為 `127.0.0.1:10000`（已用 SDK 實際讀寫驗證）。**容器化跑法**（`docker compose`）
  改連 compose 自己的 `azurite` 服務，與本節是二選一（兩者搶同一個 10000 埠，`dev-azurite.sh up` 偵測到會拒絕並說明）。
- 腳本只動容器 `tcrfc-azurite` 與 volume `tcrfc_azurite_dev`；`seed-dev-blobs.py` 拒絕非本機端點的連線字串。
- 驗證 §4.0 圖片上傳通則：`ImageUploadGeneralRuleAcrossModulesTests`（見「測試」）。

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

**建置期產生 OpenAPI（AP-8，2026-10-02）**：`Tcrfc.Api.csproj` 另外參照 `Microsoft.Extensions.ApiDescription.Server`（`PrivateAssets=all`，不進執行期），
`OpenApiGenerateDocuments` 預設 `false`——一般建置與 Docker 建置完全不變。`shared/scripts/gen-openapi.sh` 傳 `-p:OpenApiGenerateDocuments=true` 建置，
由記憶體內的 host 讀端點中繼資料產出文件，**不開 Kestrel、不連資料庫**（用寫死的假連線字串通過啟動檢查）。產出收斂後放進 `shared/openapi.json`，見 [`shared/README.md`](../../shared/README.md)。
**改了端點、請求／回應型別或會員一族的錯誤代碼後，要跑 `./shared/scripts/gen-all.sh` 並提交 `shared/`**，否則 CI 的 `shared-contract` 會紅燈。

### 用容器跑（貼近正式環境的驗證）

```bash
cd apps/api
docker build -t tcrfc-api-local .

docker run -d --name tcrfc-api-local \
  --add-host=host.docker.internal:host-gateway \
  -e CLUB_SQL_CONNECTION_STRING="Server=host.docker.internal,1433;Database=tcrfc_club;User Id=sa;Password=<你的 MSSQL_DEV_SA_PASSWORD>;TrustServerCertificate=True;" \
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
| `TRUSTED_PROXY_IPS` | 正式環境必填（否則限流失去意義），本機可省略 | S1-10 新增、**S1-17 修正改名為複數**：被信任、可以用 `X-Forwarded-For` 覆寫訪客真實 IP 的來源清單（逗號分隔；`docker-compose.yml` 給 `proxy`／`nuxt-tcrfc`／`nuxt-bw` 三個服務的固定 IP `172.28.238.2`／`.3`／`.4`）。**未設定時 `Program.cs` 不會呼叫 `app.UseForwardedHeaders()`**——千萬不要假設「沒設定就是安全的預設值」，`ForwardedHeadersMiddleware` 把空的信任清單當成「信任所有來源」，見 `Security/TrustedProxyConfiguration.cs` 檔頭「未設定時中介軟體本身完全不掛」的完整說明 |
| `ASPNETCORE_ENVIRONMENT` | 建議設 | `Development` 才會開 OpenAPI 端點，其餘值一律關閉 |
| `ASPNETCORE_URLS` | 本機開發用 | 監聽位址，容器內固定用 `Dockerfile` 的 `ASPNETCORE_HTTP_PORTS=8080` |
| ~~`ENABLE_UNSAFE_DEV_WRITES`~~ | 2026-09-23 起不存在 | 舊機制的環境旗標，隨 `Security/DevWriteGate.cs` 一併刪除，本檔任何程式碼都不再讀取這個鍵名，見「開發模式開關：已刪除」整節 |
| `AZURE_BLOB_CONNECTION_STRING` | 選填（S0-8） | 圖片上傳共用元件的物件儲存連線字串。**未設定不會讓服務無法啟動**（跟 `CLUB_SQL_CONNECTION_STRING` 不同）——只有真的呼叫圖片上傳／刪除時才會需要它，沒設定時注入 `UnavailableImageStorageService`（上傳丟出訊息清楚的例外，刪除安靜略過）。本機開發見下方「本機開發：Azurite」，正式環境見 VM 上 `/opt/tcrfc/secrets/club.env` |
| `AZURE_BLOB_CONTAINER_IMAGES` | 選填（S0-8） | 圖片物件儲存的容器名稱，預設 `images` |
| `AZURE_BLOB_PUBLIC_BASE_URL` | 選填（2026-10-01） | **公開網址基底**，如 `https://img-stg.tcrfc.tw`（Cloudflare CDN 子網域）。圖片／影片／documents 三個公開解析器改組 `{base}/{容器}/{key}`（key 逐段 URL 編碼、base 尾斜線自動處理）；**上傳與刪除仍走 `AZURE_BLOB_CONNECTION_STRING`**，`proposals` 私有容器不受影響。未設定＝回退 `BlobContainerClient.Uri`（本機 Azurite 不變）。須為絕對 https URL（`Development` 放行 http），不得含帳密／query／fragment，格式錯誤**啟動即失敗**。實作 `Common/PublicBlobUrl.cs`，測試 `PublicBlobBaseUrlTests`。正式環境寫在 VM 的 `club.env`，**不要**寫進 compose 的 `environment:`（會以空字串覆蓋 env_file） |
| `JWT_SIGNING_KEY_CLUB` | 🔴🔴🔴 S1 起必填 | 後台存取權杖的簽章金鑰，**至少 32 字元，缺值或太短在啟動期直接失敗**（`Program.cs` 於 `builder.Build()` 前呼叫 `AdminTokenService.ValidateSigningKeyConfigured`，E-79 修正——原本只有第一個請求才檢查，行程起得來但每支端點 500）。鍵名不是本輪新發明，`deploy/dev/club.env`／`docs/20-cicd.md` §7.2 早就預留。⚠️ **上線前暫用網址與正式期建議用不同值**（`docs/14-invariants.md` 既有規則） |
| `DATA_PROTECTION_KEYS_PATH` | 🔴🔴🔴 S1 起正式環境必填 | 2FA 密鑰加密金鑰環的持久化路徑。**非 Production 沒設定不會讓服務無法啟動**（本機開發沒有也能跑，只是每次重啟都要重設 2FA）；🔵 **`Production` 缺值、空白、目錄不存在或不可寫（實際寫入探測檔）一律啟動失敗**（2026-10-01，E-109，`Common/DataProtectionKeyRing.cs`，測試 `DataProtectionKeyRingTests`）。正式環境沒持久化＝容器重建後 2FA 等已加密資料永久無法解密，見 `Security/TwoFactorSecretProtector.cs` 檔頭 |

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
| 🔒🔴 `POST /api/v1/admin/accounts` | 同上＋`system.account.create`。建立帳號，`must_change_password` 預設 `false`（2026-09-30 起） | — |
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

> **E1a（2026-09-30）新增**：E1 夥伴、E2 贊助商／方案／活動、E3 提案與 Lead、B5 慈善、B6 媒體專區、C5 榮譽與里程碑的後台端點 87 支與公開端點 16 支，完整契約見「E1a」整節（含權限碼矩陣與每支端點的欄位／驗證／錯誤碼），本表不逐條重複。
>
> **B1（2026-09-30）新增**：P4 試訓場次與報名（含 P3 報名進階）、K1 會員名單、K2 會籍與方案、K3 球衣發放、K4 特約店家與權益對照表、L1 進階（分軌／衝突／改期）、L3 分類與顯示設定、L4 訂閱與匯出，
> 後台端點 79 支與公開端點 2 支（`calendar/feed.ics`、`calendar/settings`），完整契約見「B1」整節，本表不逐條重複。

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

### 欄位錯誤（`errors`，2026-10-06）

後台編輯頁要把驗證失敗標到欄位，所以 400／409 的回應可以多帶 `errors`（欄位鍵 → 訊息陣列，與 `ValidationProblemDetails` 形狀相容）：

```json
{ "status": 400, "title": "輸入內容有誤", "detail": "中文標題為必填欄位。",
  "errors": { "titleZh": ["中文標題為必填欄位。"] }, "code": "...", "messageZh": "...", "retryable": false }
```

- **`detail` 不變**；`errors` 是相容擴充。沒有欄位歸屬的錯誤（請求格式錯誤、`payload` 解析失敗、並行衝突、狀態轉換不合法）**不帶** `errors`，前端退回頁首提示。
- **怎麼帶**：驗證例外（所有 `*ValidationException`、`AdminValidationException`、`AdminConflictException`、網址名稱／隊別代號重複類）建構子都有選填 `field`（`new AdminArticleValidationException("…", "slug")`），實作 `Common/FieldApiException.cs` 的 `IFieldApiException`。`AdminInput` 的 `RequireText`／`OptionalText`／`OptionalHttpUrl` 等同樣有選填 `field`，往下傳。`ApiExceptionHandler` 只在 400／409 輸出；鍵格式不合法會被丟棄並記警告。**新增 `*ValidationException` 必須實作 `IFieldApiException`**（`ArchitectureTests` 守著）。
- **欄位鍵規則**：邏輯欄位名，不是資料庫欄位名；camelCase；雙語欄位 `xxxZh`／`xxxEn`（`FieldKey.Bi("title", "zh")`）；陣列元素 `blocks[2].bodyEn`（`FieldKey.Item("blocks", 2, "bodyEn")`）；網址名稱重複類固定 `slug`。格式 regex：`^[a-z][A-Za-z0-9]*(\[\d+\])?(\.[a-z][A-Za-z0-9]*(\[\d+\])?)*$`（`FieldKey.Pattern()`）。前端表單狀態用平的鍵（`nameZh`），與這裡對齊。
- **鍵是給前端對應用，絕不出現在訊息文字裡**（訊息仍是日常中文，docs/06 §1）；`AdminFieldErrorsTests` 逐則斷言。
- **補鍵的優先順序**：唯一性、跨欄位、依資料庫狀態的規則先補；請求格式錯誤不補。補鍵時順便把技術味訊息改白話（不回顯內部代碼、不提「資料庫欄位」）。
- **已補鍵清單（2026-10-06 第 3 階段，全部 `Features/Admin*`；雙語＝`xxxZh`／`xxxEn`，與前端各頁 form 屬性名對齊）**：
  - **通則**：圖片欄位的「上傳＋移除互斥」鍵＝該圖片欄位邏輯名（`hero`／`cover`／`photo`／`image`／`logoLight`／`ogImage`／`fallbackImage`）；網址名稱重複／格式錯誤一律 `slug`；`xxxCode` 重複標該代碼欄；兩欄日期範圍錯誤標在**結束**欄（`endOn`／`endsOn`／`endsAt`…），橫跨兩欄的期間用 `period`（首頁輪播）；列表驗證用陣列鍵（`teams[i].teamId`、`goals[i].playerId`、`winners[i].prizeName`、`userAgents[i].userAgent`、`additionalExcludePaths[i]`、`homeVenues[i].nameZh`、`blocks[i].…`、選單 `items[i].children[j].labelZh`）。
  - **球隊（前一輪）**：`code`／`type`／`gender`／`nameZh`／`hero`；**新聞**：`titleZh`／`slug`／`category`／`publishAt`／`tags[i].slug`／`tags[i].nameZh`／`coreValueTags[i]`／`relations[i].targetType`／`relations[i].targetId`／`cover`／`ogImage`。
  - **帳號／角色／俱樂部／賽事系列**：帳號 `username`／`displayName`／`initialPassword`／`newPassword`／`primaryClubId`／`roleCodes`，俱樂部授權子表單 `clubGrantClubId`／`clubGrantExpiresOn`，球隊授權子表單 `teamGrantTeamId`；角色 `code`／`scopeMode`／`permissions`；俱樂部 `code`／`domain`／`nameZh`／`shortNameZh`／`shortNameEn`；賽事系列 `seasonId`／`code`／`nameZh`／`status`。
  - **球隊／球員／教練／賽事／積分**：球隊（前一輪）；球員 `teamId`／`slug`／`nameZh`／`shirtNo`／`heightCm`／`weightKg`／`status`／`portraitConsentStatus`／`photo`；教練 `teams[i].teamId`／`staffGroup`／`nameZh`／`portraitConsentStatus`／`photo`；賽事 `seasonId`／`competitionId`／`venueId`／`teamIds`／`matchNo`／`status`／`homeAway`／`competitionTag`／`opponent`／`originalMatchOn`／`originalKickoff`／`goals[i].playerId`／`cards[i].cardType|playerId`／`lineups[i].playerId`；積分 `seasonId`／`teamName`；場地 `nameZh|En`／`addressZh|En`／`directionsZh|En`／`photoAltZh|En`／`lat`／`lng`／`sortOrder`／`photo`。
  - **課程／梯次／報名／試訓**：課程 `slug`／`programType`／`status`／`ageMin`／`ageMax`／`nameZh`／`contentZh|En`／`staffIds`／`partnerIds`／`cover`；梯次 `programId`／`venueId`／`capacity`／`price`／`earlyBirdPrice`／`startOn`／`signupOpensAt`／`weeklySchedule`／`status`；報名 `sessionId`／`memberId`／`applicantName`／`phone`／`email`／`guardianPhone`／`status`；試訓場次 `capacity`／`deadlineOn`／`status`／`audienceZh|En`／`teamId`／`venueId`；試訓報名另有 `birthOn`／`guardianName`。
  - **行事曆**：自建活動 `teamIds`／`eventTypeId`／`venueId`／`titleZh`／`endsAt`／`repeatRule`／`cover`；改期 `kickoff`／`endsAt`；設定 `defaultView`／`defaultRange`／`defaultTeamCode`／`homeTeamCodes`／`firstTeamCode`／`teams[i].teamId|colour|displayNameZh|displayNameEn|sortOrder`；類型 `code`／`nameZh|En`／`colour`／`icon`／`sortOrder`。
  - **內容**：頁面（區塊 `blocks[i].…`，路徑沿用區塊內容屬性、雙語攤平成 `xxxZh`，圖片 `blocks[i].image|images[j]`＋`.altZh`；`type`、`slug`、`publishAt`、`ogImage`）；常見問題 `slug`／`categoryIds`／`embedSlotIds`／`status`／`questionZh`／`answerZh`，分類 `slug`／`nameZh`；首頁輪播 `image`／`video`／`mediaType`／`period`、首頁區塊 `featuredBannerId`；表單 `notifyEmails`／`redirectPath`，欄位 `fieldKey`／`fieldType`／`validationRule`／`options`／`labelZh|En`／`optionLabelsEn`。
  - **商務／慈善／媒體／榮譽／漫畫／球迷會**：夥伴 `slug`／`partnerType`／`country`／`websiteUrl`／`endOn`／`nameZh|En`／`logoLight`；贊助商 `slug`／`tier`／`contractEndOn`／`expiryAlertOn`／`contactName|Phone|Email`／`nameZh|En`／`packageIds`／`articleIds`／`logoLight`，方案 `status`／`priceMin`／`priceMax`／`nameZh|En`／`audienceZh|En`，活動 `titleZh|En`；提案 `title`／`versionNo`／`status`／`file`，名單 `status`／`tags`／`assigneeAdminUserId`；慈善團體 `slug`／`websiteUrl`／`contactName|Phone`／`nameZh|En`，計畫 `slug`／`status`／`endOn`／`nameZh|En`／`audienceZh|En`／`contentZh|En`／`charityId`／`partnerIds`／`sponsorIds`／`articleIds`，事蹟 `programId`／`charityId`／`donationZh`／`locationZh|En`／`image`，數據 `value`／`nameZh|En`／`unitZh|En`／`programId`，設定 `donationUrl`／`donationCtaZh|En`／`corporateUrl`／`corporateCtaZh|En`／`fanCtaZh|En`；媒體 `resourceType`／`status`／`slug`／`titleZh|En`／`file`／`cover`；榮譽 `competitionName`／`placing`／`year`／`seasonId`／`teamId`、里程碑 `titleZh|En`／`altZh|En`；漫畫 `titleZh|En`／`bodyZh|En`、角色 `nameZh|En`／`descZh|En`／`playerId`／`sortOrder`／`image`、集數 `episodeNo`／`status`／`titleZh|En`／`cover`；球迷會活動 `slug`／`status`／`nameZh|En`／`locZh|En`／`capacity`／`startsAt`／`endsAt`／`registrationDeadlineAt`／`venueId`／`articleIds`／`cover`，報名 `memberId`／`applicantName`／`phone`／`email`／`note`／`status`。
  - **會員／會籍／抽獎／店家／商店**：會員 `name`／`email`／`phone`／`locale`／`birthOn`／`internalNote`；會籍方案 `seasonId`／`code`／`fee`／`cardQuota`／`jerseyQuota`／`midSeasonRule`／`endsOn`／`status`／`nameZh|En`；開通 `planId`／`memberId`／`paymentMethod`／`amount`／`paidOn`／`endOn`；調整 `reason`／`tier`／`status`；編號 `prefix`／`digits`；權益 `group`／`status`／`sortOrder`／`nameZh|En`／`freeValueZh|En`／`paidValueZh|En`／`planId`；球衣 `recipientName`／`size`／`deliveryMethod`／`phone`／`address`／`membershipId`；特約店家 `slug`／`category`／`region`／`phone`／`mapUrl`／`websiteUrl`／`businessHours`／`applicableTier`／`status`／`endOn`／`lat`／`lng`／`nameZh|En`／`addressZh|En`／`image`；抽獎 `drawCode`／`snapshotAt`／`nameZh|En`／`rulesZh|En`／`prizeZh|En`／`notesZh|En`／`drawOccasion`／`internalNote`／`cover`／`winners`／`winners[i].prizeName|isBackup`／`reason`／`articleId`，發放 `claimMethod`／`recipientName|Phone|Address`／`status`；電子報 `email`／`source`／`reason`；詢問 `status`／`assigneeAdminUserId`。
  - **商店**：商品 `slug`／`status`／`nameZh|En`／`seoTitleZh|En`／`seoDescZh|En`／`tagsZh|En`／`outOfStockBehavior`／`collectionId`／`sizeChart`／`sortOrder`，規格 `sku`／`size`／`colour`／`price`／`salePrice`／`cost`／`lowStockThreshold`／`initialStock`；系列 `slug`／`status`／`nameZh|En`；設定 `shippingFee`／`freeShippingThreshold`／`lowStockThreshold`／`pendingTimeoutMinutes`／`excludedRegions`／`entryTitleZh|En`／`entryIntroZh|En`／`noticeZh|En`／`shippingZh|En`／`returnsZh|En`／`termsZh|En`；捐贈碼 `code`／`orgName`／`sortOrder`；憑證 `environment`／`channelId`／`channelSecret`／`invoicePrefix`／`merchantId`／`apiKey`／`maxRetries`／`intervalMinutes`；庫存異動 `type`／`quantity`／`reason`；手動訂單 `deliveryMethod`／`items`／`recipientName|Phone|Address`／`shippingFee`／`memberId`／`completeImmediately`；退款 `reason`／`items`／`refundAmount`／`note`；出貨 `carrier`／`trackingNo`／`storeBranchCode`。
  - **網站設定／搜尋／App／廣告**：全域 `brandColor`／`brandSecondaryColor`／`cookieZh|En`／`privacyZh|En`／`termsZh|En`／`maintenanceMessageZh|En`；選單 `items[i](.children[j]).labelZh|labelEn|url`；電子報服務 `provider`／`listId`／`senderEmail`／`apiKey`；語系 `name`／`sortOrder`／`isEnabled`／`fallbackCode`；語系設定 `fallbackMode`／`dateFormatZh|En`／`numberFormatZh|En`；字串 `key`／`group`／`defaultValue`／`values`；站台事實 `foundedYear`／`foundingDateDisplayZh`／`leagueNameZh`／`squadStructureZh`／`blueWhaleSiteUrl`／`homeVenues[i].nameZh`；SEO `titleTemplateZh`／`defaultDescriptionZh`／`ogImage`、轉址 `fromPath`／`toPath`、爬蟲 `userAgents[i].userAgent`／`additionalExcludePaths[i]`；App 版面 `kind`／`itemKey`／`labelZh|En`／`deepLinkId`／`iconKey`、深連結 `code`／`appLink`／`webUrl`／`labelZh`、公告 `messageZh|En`／`linkUrl`／`audienceTier`／`endsAt`、推播 `titleZh|En`／`bodyZh|En`／`kind`／`deepLink`／`scheduledAt`／`audienceTier`／`audienceClubCode`／`audienceTeamCodes`、開關 `flagKey`／`platform`／`stringValue`、版本 `version`／`buildNumber`／`status`／`whatsNewZh`／`forceMessageZh`／`recommendMessageZh`、維護 `messageZh|En`（路由帶範圍，無列索引）、憑證 `kind`／`label`／`expiresOn`／`rotationPeriodDays`、裝置 `belowVersion`、自動推播規則 `matchReminderHours`／`membershipExpiryDays`；廣告主 `nameZh|En`／`taxId`／`contactName|Phone|Email`／`cooperationEndOn`／`status`／`sponsorId`，版位 `slotCode`／`nameZh|En`／`screenCode`／`aspectRatio`／`minWidth`／`minHeight`／`maxFileKb`／`rotationCap`／`fallbackLink`／`fallbackImage`，檔期 `name`／`startsAt`／`endsAt`／`weight`／`dailyImpressionCap`／`perDeviceDailyCap`／`reason`，素材 `locale`／`altText`／`title`／`ctaText`／`clickUrl`／`theme`／`variantTag`／`image`／`video`。
  - **慈善後台（2026-10-07，`CharityPlatform/Admin/*`；鍵與 `apps/admin-charity` 各頁 `FormField field=` 一致，雙語 `xxxZh`／`xxxEn`）**：
    - **例外通道**：`CharityApiException` 基底改實作 `IFieldApiException`，建構子多選填 `field`；`CharityConflictException(title, message, field)` 可帶欄位（既有錯誤碼、標題、訊息不變）。只有 400／409 輸出 `errors`（與主站同一個處理器）；404／422／503 不帶。其餘慈善衝突（結算、發票、捐款狀態…）沒有欄位歸屬，刻意不帶。
    - **項目**：`nameZh|En`／`oneLinerZh|En`／`coverAltZh|En`／`descriptionZh|En`／`fundUsageZh|En`（內文端點同鍵）／`invoiceMode`／`minAmount`／`maxAmount`／`amountOptions`／`projectSharePct`（含「加店家分潤超過 100%」）／`charityRefCode`／`charityProgramRefCode`／`slug`（重複與格式）。
    - **店家**：`nameZh|En`／`logoAltZh|En`／`category`／`address`／`contactName`／`contactPhone`／`endOn`（起迄日）／`status`／`storeSharePct`（含「加項目分潤超過 100%」）。匯入檔的列層錯誤與表頭錯誤刻意不補。
    - **站台設定**：`homeIntroZh|En`／`thankYouTemplateZh|En`／`noticeZh|En`／`privacyPolicyZh|En`／`clubSiteUrl`／`defaultMinAmount`／`defaultMaxAmount`。**系統信樣板**：`subjectZh|En`／`bodyZh|En`／`isActive`（沒有繁中內容就啟用）。**金流憑證**：`credential`／`invoicePrefix`／`environment`（目前前端沒有對應表單，先補上）。
    - **帳號**：`username`（格式與重複 409）／`displayName`／`initialPassword`／`newPassword`／`roleCodes`／`status`／`isSuperAdmin`（編輯時降級最後一位系統管理員的 409）。**角色**：`code`（格式與重複 409）／`nameZh`／`permissions`（權限範圍不合法、查無權限碼、僅限系統管理員的權限）。
    - 測試：`CharityAdminFieldErrorsTests`（每個服務至少一支，打真正的 HTTP 管線）。
  - **刻意不補**：請求格式／`payload` 解析錯誤、並行衝突、狀態轉換、批次數量限制（`一次最多處理…`）、CSV／檔案匯入的列層錯誤與表頭錯誤、匯出用途、權限（403）、仍被引用不能刪（409）、列表篩選的查詢參數錯誤、找不到路由上的資源（`ProgramNotFound` 之外的 404／路由 id 類）。**空的 multipart（沒有任何 part）已統一回 400**（`ApiExceptionHandler` 接 `InvalidDataException`），不再是 500。
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

🔴🔴🔴 **2026-09-30 起，`dotnet test` 直接跑在本機網站庫 `tcrfc_club` 上**（使用者裁決：本機只保留
`tcrfc_club`、`tcrfc_charity` 兩庫，不再有獨立測試庫；S0-13 的 `tcrfc_club_test` 與開發用
`tcrfc_club_dev` 已合併）。**已知代價**：測試會改動後台看到的資料，中途失敗可能留下殘骸；
需要時 `./db/seed/setup-club-db.sh --recreate` 重灌即可。測試進行中不要同時用同一個庫做無頭瀏覽器實走
（測試會重置種子帳號的 2FA 狀態與 `settings`，S0-13 當初就是為此才分庫）。見
[`docs/14-invariants.md`](../../docs/14-invariants.md)、[`docs/18-work-errors.md`](../../docs/18-work-errors.md)。

需要本機既有的 `sqlserver` 容器已啟動（同上方「怎麼跑（本機開發）」的前置；🔴 2026-09-21 起
`mssql-dev` 已併入這個既有容器，不再是獨立服務，見 `deploy/README.md`）：

```bash
docker ps --filter name=sqlserver   # 確認既有容器在跑

set -a; source .env; set +a   # 取得 MSSQL_DEV_SA_PASSWORD

# 一鍵建立／灌 tcrfc_club（見 db/seed/setup-club-db.sh 檔頭說明）：
# 第一次跑、或想從零重來一次乾淨的資料庫時用 --recreate；平常重灌種子（冪等）不用加。
./db/seed/setup-club-db.sh --recreate

export CLUB_SQL_CONNECTION_STRING="Server=127.0.0.1,1433;Database=tcrfc_club;User Id=sa;Password=${MSSQL_DEV_SA_PASSWORD};TrustServerCertificate=True;"

cd apps/api/Tcrfc.Api.Tests
dotnet test
```

⛔ **`CLUB_SQL_CONNECTION_STRING` 指到 `tcrfc_club` 以外的任何資料庫，`dotnet test` 會在
每一個 fixture 的 `InitializeAsync()` 直接拒絕啟動**（`Fixtures/TestDatabaseGuard.cs`，見下方
「六個 fixture、六種環境設定」前的說明）——這是刻意的硬性防呆，不是「建議」，防止誤連
這個 SQL Server instance 上使用者其他專案的資料庫、`tcrfc_charity` 或已廢除的舊庫名。

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

🔴 **S0-13 新增 `Fixtures/TestDatabaseGuard.cs`**：六個 fixture 原本各自重複「讀
`CLUB_SQL_CONNECTION_STRING`→檢查非空→開連線」，且完全沒有檢查連到的是哪一個資料庫，
是造成 S0-13 那次三次互相干擾的直接原因。現在六個 fixture 的 `InitializeAsync()` 一律先呼叫
`await TestDatabaseGuard.ResolveAndVerifyAsync()`：除了原本「未設定」「連不上」兩種失敗，新增
第三種——**資料庫名稱必須精確等於 `tcrfc_club`，不是就直接丟例外**，不做模糊比對。

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

## S1-12：`H` 搜尋與 AI 能見度（2026-09-25，`backend-engineer`）

主站規劃書 §4.8 H：全站 SEO 預設、單頁 Meta／OG／Canonical／noindex、Sitemap（含 hreflang）、
robots.txt、301 轉址批次匯入、追蹤碼、孤立頁面偵測。**GEO-01 `llms.txt` 維護與 GEO-02 AI 爬蟲
授權屬 `S1-12a`／`S1-12b`，依 `STATUS.md` 的相依關係不在本輪範圍**，見下方「本輪刻意不做的部分」。

🔴 **驗收退回後補做（2026-09-25）**：本節原始內容（下方表格與判斷 1、7）曾記錄「OG 圖文覆寫」與
「robots.txt 動態產生」刻意不做，**驗收退回後兩者皆已補做完成**，完整說明見本節最後的
「驗收退回後補做」小節；下方表格與判斷已同步更新為目前的正確狀態，不留舊敘述。

### 規劃書條文逐條對照

| 規劃書條文 | 狀態 |
|---|---|
| 全站 SEO 預設（標題模板、預設描述、預設 OG 圖、網站名稱） | ✅ 標題模板、預設描述（`Setting`）；預設 OG 圖沿用既有 `clubs.og_image_key`（見「驗收退回後補做」） |
| 單頁 SEO（Meta Title／Description／Keywords、OG 圖文、Canonical、noindex 開關） | ✅ 全部完成，含 OG 圖片覆寫（`articles`／`pages` 新增 `og_image_key`／`_width`／`_height`） |
| 結構化資料 | 不在本輪範圍（`GEO-05`／`S1-12c`） |
| Sitemap.xml（含 zh／en hreflang，可排除指定頁） | ✅ 排除依 `is_noindex`／`is_excluded_from_sitemap`；hreflang 沿用既有 zh-only 現況（見下）。**僅涵蓋 `Article`，`Page` 待 B1 動態路由落地後補（`STATUS.md` 明列待辦）** |
| robots.txt（線上編輯） | ✅ 後端＋前台皆完成，接上 `NUXT_PUBLIC_SITE_ENV` 白名單環境閘門（見「驗收退回後補做」） |
| 301 轉址管理（含批次匯入） | ✅ `redirects` 表既有 DDL 已完整，本輪補 CRUD＋CSV 匯入匯出＋前台 301 中介軟體 |
| 追蹤碼管理（GA4、GTM、Meta Pixel、LINE Tag） | ✅ 後端＋前台注入皆完成 |
| 內部連結建議（孤立頁面偵測） | ✅ 字串比對啟發式（見下方限制說明，`STATUS.md` 明列待辦） |

### 資料庫綱要異動

- `pages`／`articles` 新增 `canonical_path`（`nvarchar(500)` `NULL`）、`is_noindex`（`bit` 預設 `0`）、
  `is_excluded_from_sitemap`（`bit` 預設 `0`）；`pages_i18n`／`articles_i18n` 新增 `seo_keywords`
  （`nvarchar(200)` `NULL`）。Migration `AddPageArticleSeoFields`（`20260925075036`），已用
  `dotnet ef migrations has-pending-model-changes` 確認模型與 snapshot 同步（**任務指示明文禁止
  `dotnet ef migrations remove`，本輪全程沒有使用這個指令，改用這個唯讀檢查取代原本文件裡
  「加一支 `Probe` migration 再 remove」的驗證步驟**，見下方「驗收紀錄」）。
- `redirects` 表**既有 DDL 已完整存在**（`db/club-schema.sql`，含 `UQ_redirects_club_path`
  `(club_id, from_path)`），EF 實體 `Redirect.cs` 也已經 scaffold 好——本輪查證後確認**不需要任何
  schema 異動**，只補 `Features/AdminSeo` 的讀寫程式碼。
- 其餘（標題模板、預設描述、追蹤碼、robots.txt 自訂規則）**不新增資料表**，沿用既有
  `settings`／`settings_i18n`（`(club_id, setting_key)` 唯一鍵已存在）。設定鍵詞彙：
  `setting_group='seo'` 下 `seo.title_template`／`seo.default_description`（皆逐語系）、
  `seo.robots_custom_rules`（單一值）；`setting_group='tracking'` 下
  `tracking.ga4_measurement_id`／`tracking.gtm_container_id`／`tracking.meta_pixel_id`／
  `tracking.line_tag_id`（皆單一值）。

### 權限碼

矩陣「SEO／設定」欄除了內容編輯的「單頁 SEO」（跟隨既有 `content.page.update`／
`content.article.update`，未新增權限碼）之外，十個角色裡只有系統管理員打勾，比照 J 模組同樣
「單一角色排他欄位」的先例，八個新權限碼全部 `sysadmin_only=1`（`module_code=H`）：

- `seo.setting.view`／`seo.setting.update`（`H1`）：全站 SEO 預設、追蹤碼、robots.txt 自訂規則
- `seo.redirect.view`／`create`／`update`／`delete`／`import`（`H2`）：301 轉址
- `seo.report.view`（`H3`）：孤立頁面偵測

### 端點

**後台（需登入＋`sysadmin_only`）**：
- `GET`／`PUT /api/v1/admin/{club}/seo/settings` — 全站 SEO 預設＋追蹤碼（`AdminSeoSettingsDto`）
- `GET /api/v1/admin/{club}/seo/redirects?keyword=&page=&pageSize=`
- `POST /api/v1/admin/{club}/seo/redirects`（409 來源網址重複）
- `PUT`／`DELETE /api/v1/admin/{club}/seo/redirects/{id}`
- `GET /api/v1/admin/{club}/seo/redirects/export`（CSV，UTF-8 BOM）
- `POST /api/v1/admin/{club}/seo/redirects/import`（CSV，upsert 依 `(club_id, from_path)`，整批驗證、
  任一列有誤整批不寫入，比照 `AdminFaqsRepository.ImportCsvAsync` 既有語意）
- `GET /api/v1/admin/{club}/seo/orphan-pages`

**公開（不需要登入，供 `apps/web` 串接）**：
- `GET /api/v1/{club}/seo/settings` — `PublicSeoSettingsDto`（前台組 `<title>`／`<meta description>`／
  注入追蹤碼腳本／未來 robots.txt 自訂規則來源）
- `GET /api/v1/{club}/seo/redirects` — 只回傳 `is_active=1` 的規則
- `GET /api/v1/{club}/seo/sitemap-entries` — 目前只涵蓋 `Article`（見下方判斷說明）

### 規劃書沒寫清楚、本輪自行判斷的部分

1. ✅ **（驗收退回後補做，2026-09-25）OG 圖文覆寫已完成**：原始判斷是「範圍大、風險高，本輪不做」，
   驗收退回後確認「範圍大」不是略過規劃書明文規格的理由，已比照 `Features/AdminPages`／`AdminNews`
   既有的圖片上傳共用元件（`UploadSlotPolicy`＋「選檔不上傳、儲存才上傳」）補齊，完整說明見本節
   最後的「驗收退回後補做」小節。
2. **`seo.*` 權限碼標記 `sysadmin_only=1`**：矩陣沒有明文要求用這個旗標而非單純不指派其他角色，
   本輪比照 `system.*`（J 模組）的既有判斷邏輯——十個角色裡只有一個打勾的欄位視為單一角色排他，
   用 `sysadmin_only` 做雙重防線（見 `docs/12b` §7.4「S1-12 新增」附註）。
3. **Sitemap 目前只涵蓋 `Article`，不含 `Page`**：`Page` 雖然規格上「就是網站的靜態頁面路由」，但
   目前 `apps/web` 的既有 80 個單元頁仍是 mockup 搬遷的靜態 Vue 檔案，不是查 `pages` 表渲染（見
   「B1 頁面管理」一節「網址名稱」既有落差說明）。把 `Page` 併入 Sitemap 會列出前台實際不存在
   對應內容、或內容與實際渲染不符的網址，故本輪刻意排除，等 B1 真正接上前台動態路由後再擴充。
   ✅ **驗收確認這個理由成立，已明確登記為 `STATUS.md` 的 S1-12 待辦**（不是隨口記錄，是下一輪
   接手 B1 動態路由時要一併處理的項目）。
4. **孤立頁面偵測是字串比對的啟發式做法，不是完整的連結圖或 DOM 解析**：掃描已發布 `Page`／
   `Article` 彼此的 `page_blocks.content`／`articles_i18n.body`（JSON 轉字串）是否含有對方的公開
   網址子字串，不解析 HTML／JSON 結構、不驗證那段文字真的是超連結。**也不知道前台目前尚未
   資料庫化的靜態導覽選單**——這代表這份報表只能反映「內容彼此之間的引用」，反映不出「這一頁
   有沒有被主選單或麵包屑連到」。詳細限制寫在 `Features/AdminSeo/AdminSeoReportRepository.cs`
   檔頭，是本輪在現有資料狀態下的最務實做法，不是完整方案。✅ **驗收確認這個理由成立，已明確
   登記為 `STATUS.md` 的 S1-12 待辦**（等未來有「選單管理」或 B1 動態路由落地、頁面間的連結關係
   有真正的資料結構可查之後再改用那份資料）。
5. **301 轉址匯入採 upsert，不是純建立**：轉址表的典型工作流程是「先匯出現況、編輯、再匯入」，
   upsert（依 `(club_id, from_path)`）比照 `AdminFaqsRepository` 的既有語意，不是比照
   `AdminMatchesRepository` 的純建立式匯入。
6. **`AdminSeoSettingsRepository`／`AdminRedirectsRepository` 刻意不呼叫 `IQueryCache.InvalidateAsync`**：
   比照 `IQueryCache` 介面文件本身既有的說明（「目前後台還不存在，沒有任何寫入層會呼叫這個方法」），
   維持跟其餘既有唯讀 repository 一致的取捨——管理員改設定或轉址後，公開端點最多延後一個 TTL
   （預設 300 秒）才會反映，不是遺漏。
7. ✅ **（驗收退回後補做，2026-09-25）robots.txt 動態產生已接上，用環境閘門解決不一致疑慮**：
   原始判斷是「怕跟全站無條件的 noindex 標頭產生不一致，暫緩」，驗收退回時的指示是「用環境閘門
   解決」——`apps/web/server/routes/robots.txt.ts` 只有在 `NUXT_PUBLIC_SITE_ENV` **精確等於**
   `'production'` 時才輸出「允許索引＋後台自訂規則」，其他任何值（含未設定、拼錯）一律回傳
   `Disallow: /`；**全站 `X-Robots-Tag` noindex 標頭本身完全沒有被觸碰**（不在本輪範圍，見任務
   指示原文）。完整說明見本節最後的「驗收退回後補做」小節。

### 前台（`apps/web`）串接

1. **`server/utils/sitemap-urls.ts`**：新聞逐篇網址改呼叫 `GET /api/v1/{club}/seo/sitemap-entries`
   （取代原本直接打 `/news` 列表、不知道排除欄位的寫法），`server/routes/sitemap.xml.ts` 補上
   `<lastmod>` 輸出。
2. **`server/middleware/redirects.ts`**（新增）：Nitro 伺服器層中介軟體，每個請求（排除 `/api/`、
   `/_nuxt/`、帶副檔名的靜態資源）查一次 `GET /api/v1/{club}/seo/redirects`，命中就送 301。用
   Nitro 中介軟體而非 Vue Router 中介軟體是必要的——舊網址（例如 Wix 商店網址）在新站沒有對應的
   頁面元件，Router 連比對這一步都不會發生。
3. **`app/app.vue`**：追蹤碼腳本注入（GA4／GTM／Meta Pixel／LINE Tag），走既有的
   `/api/backend/{club}/...` 同源代理，個別 ID 未設定時整段不輸出。
4. ✅ **（驗收退回後補做）`server/routes/robots.txt.ts`**：見「驗收退回後補做」小節。
5. ✅ **（驗收退回後補做）`app/pages/zh/news/[slug]/index.vue`**：`og:title`／`og:description`／
   `og:image`（含尺寸與 alt）／`keywords`／`<meta name="robots">`／`<link rel="canonical">`，
   見「驗收退回後補做」小節。

**`llms.txt` 內容未串接**（`GEO-01` 屬 `S1-12a`，本輪不動 `server/routes/llms.txt.ts`／
`llms-en.txt.ts` 既有的骨架佔位內容）。

### 測試

`Tcrfc.Api.Tests/AdminSeoTests.cs`（12 項）：

| 測試 | 涵蓋 |
|---|---|
| `Settings_*` | 401、非系統管理員 403、系統管理員讀寫、中文必填英文可空、公開端點即時反映 |
| `Redirect_*` | 401、403、建立／更新／刪除完整生命週期、來源網址重複 409、格式錯誤 400、
  來源與目的相同 400、公開端點只回傳生效中的規則 |
| `RedirectCsv_*` | 匯出 UTF-8 BOM 與表頭、匯入 upsert（更新既有＋新增）、表頭錯誤整批不寫入 |
| `OrphanReport_*` | 401、403、沒有被引用的已發布文章出現在清單 |
| `SitemapEntries_*` | 排除 `is_excluded_from_sitemap` 為真的文章 |

✅ **驗收退回後補做（2026-09-25）新增 `Tcrfc.Api.Tests/AdminSeoImageTests.cs`（4 項，真實
Azurite，見下方小節）**，且 `AdminSeoTests.cs` 的設定讀寫測試已改為測試前拍照、`finally` 精準
還原（見下方小節）。全套 `dotnet test`（`Tcrfc.Api.Tests.csproj`）**455/455 通過**
（`AdminSeoTests` 12 項＋`AdminSeoImageTests` 4 項＋既有 439 項）。

### 驗收紀錄（2026-09-25，本機環境，含驗收退回後補做的重新驗證）

1. **`dotnet build`／`dotnet test` 全過**：455/455；`ArchitectureTests`／
   `UserFacingMessageContentTests`／`UserFacingMessageHttpContentTests` 針對本輪新增程式碼
   全部通過（掃描沒有找到違規的例外訊息內插權限碼或英文技術詞）。
2. **兩支 migration 皆已用 `dotnet ef migrations has-pending-model-changes` 確認無待處理變更**
   （`AddPageArticleSeoFields`、驗收退回後補做新增的 `AddSeoOgImageFields`；全程未使用
   `migrations remove`，任務指示明文禁止；`AddSeoOgImageFields` 產生時一度把 `OgImageAlt` 的
   欄位名／型別寫錯成 `nvarchar(max)` 而非 `og_image_alt nvarchar(200)`，因為忘了在
   `ClubDbContext.cs` 補對應的 Fluent 設定——**沒有刪除重建這個 migration**，改為手動修正
   migration 本體、`.Designer.cs`、`ClubDbContextModelSnapshot.cs` 三處後再確認同步）。
3. **`dotnet run` 本機真實啟動 `apps/api`（含 `AZURE_BLOB_CONNECTION_STRING` 指到本機 Azurite
   位址，僅供 `IImagePublicUrlResolver` 算網址字串，讀取端點不需要真的連得上），`curl` 實測**：
   - `GET /api/v1/tcrfc/seo/settings`／`/seo/redirects`／`/seo/sitemap-entries` 皆回傳正確資料。
   - 直接以 SQL 插入一筆測試轉址與一篇測試文章（含 `canonical_path`／`is_noindex`／
     `seo_keywords`／`og_image_key`／`og_image_alt`），公開端點皆正確回傳；驗收後已全數刪除，
     未留在 `tcrfc_club_dev`。
4. **`apps/web` 串接實測**（`npm run build` 產物 ＋ `node .output/server/index.mjs`，前面接上述
   本機 `apps/api`）：
   - `curl /sitemap.xml`：既有 93 筆單元＋新聞不變，新聞項目多出 `<lastmod>`。
   - **`curl /robots.txt`（未設定 `NUXT_PUBLIC_SITE_ENV`）**：`User-agent: *\nDisallow: /`（封鎖側，
     符合「漏設變數要落在封鎖那一側」）。
   - **`curl /robots.txt`（`NUXT_PUBLIC_SITE_ENV=production`）**：`User-agent: *\nAllow: /` ＋
     `Sitemap:` 一行；再插入一筆 `seo.robots_custom_rules` 測試值後重新整理，自訂規則正確附加
     在輸出裡；驗收後已刪除。
   - **`curl /robots.txt`（`NUXT_PUBLIC_SITE_ENV=Production`，刻意打錯大小寫）**：仍是封鎖側，
     驗證白名單判斷（`=== 'production'`）而不是黑名單判斷（`!== 'prelaunch'`）。
   - `curl -I /zh/`：`X-Robots-Tag: noindex, nofollow` 在任何 `NUXT_PUBLIC_SITE_ENV` 值下皆存在
     （任務指示要求本輪不得觸碰這個標頭本身的邏輯，已確認未受影響）。
   - `curl /product-page/manual-test-sock/`：`301`，`Location` 指到 `/zh/shop/cushioned-socks/`；
     `curl /zh/about/`：`200`（未命中轉址表的路徑不受影響）。
   - **`curl /zh/news/manual-verify-og-test/` 的實際 HTML**（見下方「OG／canonical／noindex／
     keywords 真的有輸出」小節）：`<meta property="og:image">`／`og:image:width`／
     `og:image:height`／`og:image:alt`、`<meta name="keywords">`、
     `<meta name="robots" content="noindex">`、`<link rel="canonical">` 全部正確輸出，
     `<title>` 內容正確。
   - `curl /zh/` 的 HTML 內含 `googletagmanager.com/gtag/js?id=G-TEST123` 與
     `gtag('config','G-TEST123')`（驗證追蹤碼腳本確實注入）。
5. **`npm run lint`**：`apps/web`（0 錯誤，既有 539 筆屬性排序等警告與本輪無關）、`apps/admin`
   （全過，本輪未修改該專案任何檔案）皆綠燈。
6. **`docker build`**（`apps/api`／`apps/web`，比照 `docker-compose.yml` 的
   `context`／`dockerfile` 設定）皆成功（`E-35` 教訓：只跑本機 build 不夠，本輪兩邊都真的跑過
   `docker build`）。

### 驗收退回後補做（2026-09-25）

四項修正，逐項對照協調者的退回意見：

**① OG 圖文與預設 OG 圖真的做了**——見上方「資料庫綱要異動」docs/12 §12 第 42 點的完整說明。
補充三個實作細節：
- `Images/IImagePublicUrlResolver`（`BlobImagePublicUrlResolver`／`UnavailableImagePublicUrlResolver`
  兩個實作，比照 `IImageStorageService` 的條件式 DI 註冊）是**本專案第一次**把 Blob 物件鍵換成
  完整可公開存取的網址——先前所有前台圖片顯示（新聞封面、球員照片……）全部繞過真正的物件鍵，
  改用 mockup 既有的靜態檔名慣例（`apps/web/app/utils/news.ts` 檔頭「已知資料落差」），因為種子
  資料的 `*_key` 欄位從未真正寫入過 Blob。這裡回傳的網址在容器沒有開公開讀取權限或接 CDN
  前不會是真的可存取，但**格式與計算規則正確**，是部署層的後續工作，不影響這裡的程式碼。
- `Features/Uploads/ImageFieldUpdate`（新增的共用三態 `record struct`：維持／清空／換新＋尺寸）
  供 `Features/AdminNews`（Article 的 OG 圖片）、`Features/AdminPages`（Page 的 OG 圖片）、
  `Features/AdminSeo`（全站預設 OG 圖片）三處共用，不重複各自宣告一份幾乎相同的型別——跟既有
  `Features/AdminNews/CoverKeyUpdate` 的差異是多帶 `Width`／`Height`（`cover_key` 當初沒有寬高
  欄位是既有缺口，本輪新增的 `og_image_key` 一開始就照「圖片欄位組」通則設計）。
- **全站預設 OG 圖片沒有新建欄位**：查證後發現 `clubs.og_image_key` 早就存在（J4 品牌欄位，
  `AdminClubDetailDto` 原本因為「另一位 backend agent 同時在改圖片上傳共用元件」而刻意唯讀，
  見 `Features/AdminClubs/AdminClubDtos.cs` 的既有註解——那個限制只針對那一輪的任務邊界，
  不適用本輪）。改為補上 `clubs.og_image_width`／`og_image_height` 兩欄，寫入路徑由
  `Features/AdminSeo/AdminSeoSettingsRepository` 直接更新 `Club` 實體（不透過
  `Features/AdminClubs`），`logo_light_key`／`logo_dark_key`／`favicon_key` 三個品牌欄位維持
  原本刻意唯讀，不受影響。

**優先序**（公開端點算好一個最終答案，前台直接用，不在前台重算）：
- `Article`：這篇文章專屬 OG 圖片 > 全站預設 OG 圖片（`Club.OgImageKey`） > 這篇文章的封面圖片
  （`cover_key`）。回退到全站預設圖或封面圖時**不輸出 alt**——兩者都沒有對應的替代文字來源
  （`Club` 沒有這個欄位，`cover_key` 本身就沒有 alt 欄位，是既有落差）。
- `Page`：這個頁面專屬 OG 圖片 > 全站預設 OG 圖片（Page 沒有「封面圖片」的概念，不像 Article
  多一層回退）。

**② robots.txt 接上環境閘門**——`apps/web/server/routes/robots.txt.ts`（新增，取代
`nuxt.config.ts` 原本 `@nuxtjs/robots` 的 `disallow: ['/']`，改用 `enabled: false` 完全關閉該
模組，理由跟 `docs/18-work-errors.md` E-18（sitemap 那次）完全同一個模式，見該檔案的完整說明）：
- 環境旗標是**白名單**判斷（`siteEnv === 'production'`），不是黑名單（`siteEnv !== 'prelaunch'`）
  ——確保任何未預期的值（未設定、拼錯、大小寫不符、未來新增的過渡值）一律落在封鎖側，這是任務
  指示明文要求的方向。
- production 分支目前只到「基本允許索引＋後台 `seo.robots_custom_rules` 自訂規則＋Sitemap
  參照」，**不含 GEO-02 逐一 AI 爬蟲的允許清單與排除路徑**（那屬於 `S1-12b`，本輪不做，等那張票
  做完再擴充這支路由的 production 分支，不需要改動環境旗標判斷邏輯本身）。
- **全站 `X-Robots-Tag` noindex 標頭完全沒有被觸碰**——這是任務指示明文要求本輪不要處理的部分
  （該標頭目前無條件套用，不看 `siteEnv`；上線時要不要也讓它跟著這個變數切換，是
  `docs/17-deployment.md` §10.4「上線前兩層防護」的完整機制要一併決定的事，不是這支檔案的職責）。

**③ OG／canonical／noindex／keywords 真的有輸出到 HTML**——上一輪只把這些欄位加進
`apps/api` 的 DTO，沒有接到任何前台頁面消費。`app/pages/zh/news/[slug]/index.vue`（目前
**唯一**有動態內容可以渲染 SEO 資料的公開頁面，其餘 79 頁是靜態 mockup 搬遷頁）新增：
`useSeoMeta` 的 `keywords`／`ogTitle`／`ogDescription`／`ogImage`／`ogImageWidth`／
`ogImageHeight`／`ogImageAlt`／`robots`（`isNoindex` 為真時輸出 `noindex`，跟全站 noindex
是兩個獨立機制，見程式碼註解）；`canonicalPath` 有值時用 `useHead` 疊加
`<link rel="canonical">`（沿用既有 `app/pages/zh/schedule.vue` 的 `useSiteConfig()` 既有先例
取得網域）。已用一篇真實插入的測試文章（含中文標題／描述／關鍵字／OG 圖片與 alt／
`canonical_path`／`is_noindex=1`）取得實際 SSR 輸出的 HTML 逐一核對，見上方驗收紀錄第 4 點。

**④ 測試不再永久改動開發資料庫的網站設定**：
- `AdminSeoTests.Settings_系統管理員_可讀可寫...` 改為呼叫前先用
  `CaptureSeoSettingsRowsAsync()` 讀出 `tcrfc` 俱樂部目前 `seo.*`／`tracking.*` 這批鍵的完整
  現況（含 i18n 列），`finally` 用 `RestoreSeoSettingsRowsAsync()` 精準還原（整批刪除後依快照
  重建，不是假設「執行前一定是空的」）。
- 新增 `AdminSeoImageTests.cs`（真實 Azurite，見上方「測試」小節）驗證 OG 圖片上傳／解析出
  網址／優先序，同樣測試前拍照、`finally` 還原 `clubs.og_image_key`／`_width`／`_height`。
- 🔴 **過程中額外發現並修正一個既有小問題**：`AdminSeoSettingsRepository.UpdateAsync` 原本
  每次 PUT（即使呼叫端只是要改標題樣板）都會把全部七個 `seo.*`／`tracking.*` 鍵各建一列
  （即使沒有值也建一列 `setting_value = NULL` 的空殼），是撰寫 `AdminSeoImageTests` 的清理邏輯
  時，實測發現 `tcrfc_club_dev` 留下五筆非預期的空列才抓到的——已修正
  `AdminSeoSettingsRepository.UpsertValue`：只有「這個鍵已經有列」或「這次要寫入非空白值」才會
  建立新列，不再產生用不到的空殼列。

### 已知缺口（回報，不在本輪自行判斷做或不做）

1. **後台畫面待做**：`apps/admin` 完全未改動（任務邊界僅 `apps/api`／`apps/web`）。
2. ✅ **`GEO-01`／`GEO-02`**：已於 `S1-12a`／`S1-12b`（2026-09-25）完成，見下方兩節。
3. **全站預設 OG 圖片沒有替代文字欄位**：`clubs`／`clubs_i18n` 沒有對應的 `og_image_alt`——
   Open Graph 規格本身沒有強制要求 `og:image:alt`，本輪判斷這個缺口可接受，不特別為全站預設圖
   新增一個 i18n 欄位；單頁（Article／Page）專屬的 OG 圖片已有 alt 欄位，不受影響。

## S1-12a：`GEO-01` `llms.txt` 維護（2026-09-25，`backend-engineer`）

主站規劃書 §7 `GEO-01`／§4.8「`llms.txt` 維護」：站點定位、代表頁清單、事實摘要、授權與引用、
聯絡窗口，**繁中英文各一份、兩站各自一份、隨發布重產、不以人工改檔**。**不新增資料型別**，
沿用既有 `settings`／`settings_i18n`（`setting_group='geo'`）。

### 資料庫綱要異動

**沒有 migration**——五個區塊各自一個鍵，沿用既有 `settings`／`settings_i18n` 結構，不需要任何
DDL 異動：`geo.llms_positioning`／`geo.llms_key_pages`／`geo.llms_facts_summary`／
`geo.llms_license`／`geo.llms_contact`，皆逐語系（`zh-Hant`／`en`）。

### 權限碼

矩陣「SEO／設定」欄同 `S1-12` 既有的 `seo.*` 一組判斷——十個角色只有系統管理員打勾，
`sysadmin_only=1`（`module_code=H`、`submodule_code=H4`、`domain=seo`）：

- `seo.llms.view`／`seo.llms.update`：`llms.txt` 內容維護

### 端點

**後台（需登入＋`sysadmin_only`）**：
- `GET`／`PUT /api/v1/admin/{club}/seo/llms-content`——`AdminLlmsContentDto`，純 JSON（沒有圖片
  欄位，不需要 `multipart/form-data`）。

**公開（不需要登入，供 `apps/web` 串接）**：
- `GET /api/v1/{club}/seo/llms-content`——`PublicLlmsContentDto`，接上既有 `IQueryCache`
  （entity `seo-llms-content`），跟其餘 `Features/Seo` 讀取端點一致，最多延後一個 TTL
  （預設 300 秒）反映後台異動。

### 規劃書沒寫清楚、本輪自行判斷的部分

1. **五個區塊皆可為空，沒有必填欄位**：跟同模組既有的 `seo.title_template`／
   `seo.default_description`（缺中文會擋 400）不同——這五個區塊留白時前台有安全的內建預設文字
   可回退（見下方 `apps/web` 串接），管理員可以先不填、之後再逐步補齊，不會讓 `/llms.txt`
   輸出壞掉或消失。CLAUDE.md 全域規定 4「英文可空但欄位必須存在」在這裡的落實方式是五個區塊
   各自都有 `*Zh`／`*En` 兩個屬性，不是省略英文欄位。
2. **「代表頁清單」設計成管理員自行維護的自由文字，不是自動從 `getEnabledSiteUnits()` 算出來的
   清單**：規劃書明文「由後台 `H` 模組維護」，若改成後端自動產生，就不是「後台維護」而是「開發者
   寫死在程式碼」，管理員也就沒有實際可編輯的東西。管理員可以直接用 Markdown 清單語法撰寫
   （例如 `- [關於我們](/zh/about/)`），前台原樣輸出，不額外解析。空白時前台回退到
   `getEnabledSiteUnits()` 自動產生的清單（骨架階段既有邏輯），確保上線初期這一段不會是空白。
3. **「隨發布重產、不以人工改檔」的落實方式：沒有「發布」這個額外動作**——`apps/web` 的
   `/llms.txt`／`/llms-en.txt` 每個請求都重新呼叫後端組字串，不是建置期產生的靜態檔案；管理員
   在後台按下「儲存」，下一次請求（最多延後一個 TTL）就是最新內容，不需要另外觸發一次建置或
   部署。`GEO-01`「不以人工改檔」在這個實作下自動成立——內容只存在資料庫裡，沒有檔案可以讓人工
   去改。
4. **不做 `IQueryCache.InvalidateAsync` 寫入失效**：比照既有 `Features/Seo` 讀取端點一貫的取捨
   （`SeoRepository` 檔頭「寫入端刻意不呼叫 `InvalidateAsync`」），不是本輪特例。

### `apps/web` 串接

`server/routes/llms.txt.ts`／`llms-en.txt.ts` 改讀 `GET /api/v1/{club}/seo/llms-content`：
- 繁中版：每個區塊「後台內容 → 內建預設文字」兩層回退（代表頁清單的預設值沿用既有
  `getEnabledSiteUnits()` 邏輯）。
- 英文版：每個區塊「英文欄位 → 中文欄位 → 內建英文預設文字」三層回退——英文尚未翻譯時退回中文
  總比空白好（比照 `docs/01` G-01「未翻譯 fallback 繁中並標示」的既有精神，這裡沒有畫面可以
  加註標示，直接回退內容本身）。
- `apps/api` 暫時連不上時整份回退到內建預設文字，不讓 `/llms.txt` 500（同既有
  `sitemap-urls.ts`／`robots.txt.ts` 的防禦性寫法）。

### 測試

`Tcrfc.Api.Tests/AdminGeoLlmsTests.cs`（4 項）：401、403（內容編輯角色沒有 `sysadmin_only`
權限）、系統管理員可讀可寫且五個區塊皆可為空（含公開端點即時反映、整份清空回到 `null`）、
跨俱樂部（`tcrfc` 寫入的內容不會出現在 `bw` 的公開端點）。

## S1-12b：`GEO-02` AI 爬蟲授權（2026-09-25，`backend-engineer`）

主站規劃書 §7 `GEO-02`／§4.8「AI 爬蟲授權」：AI 使用者代理清單與允許／拒絕設定、排除路徑清單，
輸出至 `robots.txt`。**全站允許爬取，但一律排除**會員中心、七類表單、訂單查詢、
`/m/<token>` 會員卡驗證頁、未成年與學員照片路徑——docs/14-invariants.md 明文「這條排除是個資
防線，不是 SEO 設定，不得為了『讓 AI 多抓一點』而放寬」，因此**排除路徑分成強制（程式碼寫死，
後台不能移除，只能再加）與後台自行再加**兩層。**不新增資料型別**，沿用 `settings`
（`setting_group='geo'`）。

### 資料庫綱要異動

**沒有 migration**：`geo.crawler_agents`（JSON 陣列 `[{"userAgent":"GPTBot","allowed":true}, ...]`，
非人類語言不進 i18n 側表，跟既有 `seo.robots_custom_rules` 同一個判斷）、
`geo.crawler_extra_exclude_paths`（JSON 陣列，後台自行再加的排除路徑）。

### 強制排除路徑（`Features/Seo/GeoCrawlerDefaults.GetMandatoryExcludePaths`，單一來源）

程式碼寫死、不存在 `settings`、後台完全沒有 API 能讀到「目前的強制清單」再整批覆蓋掉。
內部以**語系無關的相對路徑片段**定義（`LocalizedSegments`），輸出時依站台語系（`zh`／`en`）
展開成兩份；`/m/` 是唯一不展開語系的例外（本身就是語系目錄外的短網址）：

| 分類 | 語系無關片段 | 實際輸出（`tcrfc`／`bw` 共同） |
|---|---|---|
| 會員中心 | `member/` | `/zh/member/`、`/en/member/` |
| 七類表單（10.1–10.7） | `join/{player,academy,camp-registration,international-player,partnership,media,general}/` | `/zh/join/…/`、`/en/join/…/`（各 7 條 ×2 語系） |
| 訂單查詢 | `order/lookup/` | `/zh/order/lookup/`、`/en/order/lookup/` |
| 會員卡驗證頁 | 不展開語系 | `/m/` |

外加**俱樂部專屬的未成年照片頁**（`ClubLocalizedSegments`，每條 ×2 語系；兩隊共用同一套前台路由，
藍鯨青年隊 U15／U12 與磐石學院走同樣網址）：

| 俱樂部 | 片段 | 依據（`apps/web/app/pages/zh/…`） |
|---|---|---|
| `tcrfc`、`bw` | `academy/teams/` | `academy/teams.vue`：梯隊球員名單與照片（`bw` 為 BW-U15／BW-U12） |
| `tcrfc`、`bw` | `academy/life/` | `academy/life.vue`：訓練影像牆（`bw` 目前為「肖像同意後公布」空狀態，先擋不等上架才補） |
| `tcrfc` | `programs/childrens-training/`、`programs/summer-camp/`、`programs/specialist/` | 三頁皆有兒童／學員照片牆；`bw` 版對應區塊未渲染（`isTcrfc` 分支），`bw` 若補學員照片須回頭加入 |

（2026-09-30 修正：先前誤記「`bw` 沒有對應頁面、待路由落地才補」——實際 `teams.vue` 早已渲染藍鯨青年隊名單，
見 `docs/18-work-errors.md`。）
**學員照片圖片目錄**（2026-09-30 主 session 裁決，`StaticExcludePaths`，不分語系、不分俱樂部、不展開）：

| 路徑 | 依據（`apps/web/public/assets/img/`） |
|---|---|
| `/assets/img/academy/` | `life-01…13.jpg` 訓練與比賽影像；另含成人教練照 `coach-hsu-chih-chieh.jpg`，一併被擋（可接受的取捨） |
| `/assets/img/programs/` | `childrens-*`、`summer-camp-*`、`specialist-*` 全為兒童與學員照片 |

只以**裝飾性 hero 背景**使用學員照片的頁面（`academy/index`、`overview`、`pathway`、`curriculum`、
`coaches`、`join`，`programs/index`）**不整頁排除**（依 `GEO-02` 原文「學員照片」，避免學院行銷頁無法被收錄），
改以擋圖片目錄保護。`robots.txt` 的排除清單套在 `User-agent: *`（另加每個「允許」的 AI 代理區塊），
所以搜尋引擎的圖片索引也會略過這兩個目錄——正符合保護學員照片的目的。
🔴 前台若新增學員照片目錄，須同一次交付補進 `StaticExcludePaths`。

⚠️ **不含 `/zh/join/`（單元入口頁）、`/zh/join/location/`（Location & Map）、
`/zh/join/contact/`（Contact Information）**——這三頁是靜態資訊頁，不收集個資，規劃書「七類
表單」明確只指會收件的那七頁。

✅ **2026-09-25（協調者驗收退回後補做）：`/en/` 版本現在就同時輸出，不留成「`/en/` 上線時再補」
的已知缺口**——原始判斷是「站上目前只有 `/zh/` 頁面，`/en/` 上線時才補」，驗收退回後確認：對
目前還不存在的 `/en/…` 路徑輸出 `Disallow:` 沒有任何副作用（不會誤擋任何真實頁面，不影響任何
爬蟲的正常爬取），但「等事後才記得補」正是個資防線最容易出漏洞的模式，因此改為現在就展開兩個
語系（見 `GeoCrawlerDefaults.Locales`），`/en/` 頁面日後上線時這裡完全不需要再改。

### 權限碼

同 `S1-12a` 判斷理由，`sysadmin_only=1`（`module_code=H`、`submodule_code=H5`、`domain=seo`）：

- `seo.crawler.view`／`seo.crawler.update`：AI 爬蟲授權維護

### 端點

**後台（需登入＋`sysadmin_only`）**：
- `GET /api/v1/admin/{club}/seo/crawler-settings`——`AdminCrawlerSettingsDto`（含唯讀的
  `mandatoryExcludePaths` 供畫面陳列，這個 DTO 沒有對應的可寫入欄位）。
- `PUT /api/v1/admin/{club}/seo/crawler-settings`——`UpdateCrawlerSettingsRequest`
  （`userAgents`／`additionalExcludePaths`）。整份取代語意，逐項驗證使用者代理格式
  （`^[A-Za-z0-9._-]{1,100}$`，去重不分大小寫）與排除路徑格式（以 `/` 開頭、以 `/` 結尾、
  不含空白、長度上限），任一筆有誤整批不寫入（比照既有匯入類端點語意）。

**公開（不需要登入，供 `apps/web` 串接）**：
- `GET /api/v1/{club}/seo/crawler-settings`——`PublicCrawlerSettingsDto`。`excludePaths`
  **已經是強制排除路徑 ∪ 後台自行再加的路徑**的合併結果（`SeoRepository.GetCrawlerSettingsAsync`
  是唯一組出「最終排除清單」的地方），`apps/web` 不需要、也不應該自己再合併一次強制清單。
  接上既有 `IQueryCache`（entity `seo-crawler-settings`）。

### 規劃書沒寫清楚、本輪自行判斷的部分

1. **強制排除路徑的落實方式：結構上就不存在「移除」這個操作**，不是靠程式碼判斷「使用者是不是
   想移除」擋下來——`UpdateCrawlerSettingsRequest` 這個型別根本沒有欄位可以承載強制路徑，
   公開端點的合併邏輯一律用程式碼常數聯集。已用專門的反例測試驗證（見下方測試小節）。
2. **後台尚未設定過時，`GET` 回傳規劃書條文原文列的五個範例代理（`GPTBot`／`ClaudeBot`／
   `PerplexityBot`／`Google-Extended`／`CCBot`）當建議值，全部預設允許**——這份清單只是初次
   進入後台畫面時的建議值，不會因此寫入資料庫，管理員儲存後才真的落地，之後改預設值不影響
   已儲存過的俱樂部。
3. **`robots.txt` 排除路徑套用到 `User-agent: *`（全站、對所有爬蟲一視同仁），不是只套用到
   命名的 AI 代理**：這些路徑排除的理由是個資與肖像同意（會員資料、未成年素材），不是「只想省
   AI 的爬取額度」，沒有理由只告訴 AI 爬蟲不要看、放一般爬蟲進去。命名的 AI 使用者代理在此之上
   明列允許（`GEO-02`「明列允許的 AI 使用者代理」的字面要求），套用同一份排除清單；後台若把
   某個代理設為拒絕，該代理拿到專屬的 `Disallow: /`（robots.txt 規格「較具體的 `User-agent`
   區塊覆蓋 `*`」的既有語意）。
4. **使用者代理格式驗證採白名單字元集**（英數字、句點、連字號、底線），不接受空白或其他符號——
   規劃書沒有給格式規則，本輪判斷：這個字串會原樣寫進 `robots.txt` 的 `User-agent:` 欄位，
   放行任意字元有 header/robots.txt 語法注入風險（例如換行字元偽造出額外的 `Disallow:` 行）。
5. **排除路徑格式要求以 `/` 開頭且以 `/` 結尾**（目錄前綴語意）：以 `/` 開頭比照既有
   `RedirectPathPolicy`（301 轉址的既有路徑格式規則，本輪直接重用同一個驗證器）；額外要求
   以 `/` 結尾是本輪新增的判斷——避免 `/zh/join/media` 這種沒有結尾斜線的寫法意外前綴比對到
   `/zh/join/media-kit/` 之類不該被排除的路徑。
6. **不做 `IQueryCache.InvalidateAsync` 寫入失效**：同 `S1-12a` 判斷理由。

### `apps/web` 串接

`server/routes/robots.txt.ts` 的 `production` 分支擴充（環境旗標白名單判斷不變，見既有
`S1-12` 說明）：改讀 `GET /api/v1/{club}/seo/crawler-settings`，`excludePaths` 套用到
`User-agent: *` 區塊，並為每個設定的 AI 代理輸出專屬區塊（允許＝`Allow: /` ＋ 同一份排除清單；
拒絕＝整段 `Disallow: /`）。`apps/api` 暫時連不上時，不輸出任何排除路徑或 AI 代理區塊，只保留
最基本的 `User-agent: *` 允許索引規則（fail-open 到「最基本可用」，跟既有 `seo.robots_custom_rules`
的防禦性寫法一致）。

### 測試

`Tcrfc.Api.Tests/AdminGeoCrawlerTests.cs`（7 項）：401、403、尚未設定過回傳規劃書預設清單與
強制排除路徑（🔴 含 `zh`／`en` 兩語系皆檢查）、🔴 **公開端點同時包含 `zh` 與 `en` 的強制排除
路徑**（2026-09-25 驗收退回後補做新增，見下方說明）、系統管理員可讀可寫並驗證輸入格式（代理
格式錯誤／重複、路徑格式錯誤／缺結尾斜線皆 400）、🔴 **反例：後台清空自行再加的排除路徑後，
公開端點的強制排除路徑仍然存在**、跨俱樂部（強制清單與後台自加路徑不互相污染，`bw` 沒有
`tcrfc` 專屬的課程照片頁；`bw` 的 `academy/teams/`、`academy/life/` 兩語系必定出現）。

### 驗收紀錄（2026-09-25，本機環境，含驗收退回後補做 `/en/` 版本的重新驗證）

1. `dotnet test`（`Tcrfc.Api.Tests`）**466/466 通過**（`AdminGeoLlmsTests` 4 項＋
   `AdminGeoCrawlerTests` 7 項＋既有 455 項）。過程中一度出現 3 項既有 `AdminAuthTests` 失敗
   （`Set-Cookie` 缺失／`MustChangePassword` 非預期為真）——實際查證後確認是**種子測試帳號被
   本機今天多輪 `dotnet test` 弄髒的既有狀態**，不是本次改動造成：① 這三項在只跑
   `--filter "FullyQualifiedName~AdminAuthTests"`（完全隔離其他測試類別）時依然失敗；②
   `git diff` 確認本輪唯一touch 到的既有檔案只有 `Program.cs` 的 7 行 DI 註冊與端點掛載，跟
   認證／Cookie 完全無關。執行 `./db/seed/reset-admin-accounts.sh`（既定的種子帳號還原工具）
   後全套重跑轉為 466/466 全綠，證實根因確實是帳號狀態而非本輪程式碼。
2. 種子資料：`db/seed/generate-club-seed-sql.py` 新增 4 個權限碼（`seo.llms.*`／
   `seo.crawler.*`），已用 `./db/seed/apply-seed.sh` 灌入 `tcrfc_club_dev`（冪等腳本，`sqlcmd`
   確認 `system_admin` 角色已自動取得全部四個權限碼）。**沒有 migration**（兩項功能都沒有
   異動資料庫綱要，只新增 `settings` 底下的鍵）。
3. `dotnet run` 本機真實啟動 `apps/api`，`curl` 實測 `GET /api/v1/tcrfc/seo/crawler-settings`
   （公開端點，未寫入任何測試資料、純讀取程式碼裡的強制清單）：`excludePaths` 正確回傳 21 筆
   （9 個共用片段 ×2 語系＋`tcrfc` 專屬 1 個片段 ×2 語系＋不展開語系的 `/m/`），`/zh/…`／
   `/en/…` 成對出現，逐項核對與 `GeoCrawlerDefaults` 原始碼一致。本輪僅讀取、未寫入資料庫，
   無需清理。
4. `apps/web` 串接實測（`npm run build` 產物 ＋ `node .output/server/index.mjs`，前面接上述
   本機 `apps/api`）：
   - `curl /robots.txt`（`NUXT_PUBLIC_SITE_ENV=production`，`tcrfc`）：`User-agent: *` 區塊
     正確列出 21 條強制排除路徑（`/zh/…` 與 `/en/…` 成對），五個預設 AI 代理各自區塊同樣完整
     複製這 21 條；`Sitemap:` 一行正確帶 `NUXT_PUBLIC_SITE_URL`。
   - `curl /robots.txt`（`NUXT_PUBLIC_CLUB=bw`，`production`）：`User-agent: *` 區塊正確列出
     19 條（9 個共用片段 ×2 語系＋`/m/`），當時**沒有** `academy/teams/`（2026-09-30 已補 `academy/teams/`、`academy/life/`，`bw` 現為 23 條）。
   - 上一輪（`S1-12b` 首次完成）已驗證過的「未設定環境變數」「`Production` 拼錯大小寫」封鎖側
     行為、`X-Robots-Tag` 不受影響、AI 代理允許／拒絕分流，本輪未改動這些邏輯，未重複列出。
5. `npm run lint`：`apps/web`（0 錯誤，既有警告與本輪無關）、`apps/admin`（全過，本輪未修改
   該專案任何檔案）皆綠燈。
6. `apps/web` `npm run build` 成功；`apps/api` `docker build` 成功（本輪只改了 `apps/api` 的
   C# 原始碼，`apps/web` 沒有程式碼異動，故本輪重跑 docker build 只針對 `apps/api`）。

### 已知缺口（回報，不在本輪自行判斷做或不做）

1. **後台畫面待做**：`apps/admin` 完全未改動（任務邊界僅 `apps/api`／`apps/web`）。
2. ~~`bw` 缺少未成年學員照片頁面的強制排除路徑~~：已於 2026-09-30 補上（見上方「強制排除路徑」與 `docs/18` E-82）。
3. **`GEO-05`（結構化資料完整性檢查）／`GEO-03`／`GEO-04`（事實單一來源與雙重呈現）不在本輪
   範圍**：依 `STATUS.md` 排程分屬 `S1-12c`（後台）、`S1-12d`（主站前台）。

## S1-12c：`GEO-05` 結構化資料完整性檢查（2026-09-25，`backend-engineer`）

主站規劃書 §7 `GEO-05`／§4.8「結構化資料完整性檢查」：逐型別輸出 Organization、SportsTeam、
Event、SportsEvent（行事曆）、Person、Article、Course、BreadcrumbList、FAQPage 九種 Schema.org
型別；**必填欄位不得留空，資料不足時不輸出該型別，不輸出殘缺的 Schema**。任務指示明文要求
「每個型別的必填欄位定義成單一來源（程式碼中一份），後台報表與前台輸出判斷都讀它，不要兩處各
寫一份」（docs/18-work-errors.md E-39）。**不新增資料表、沒有 migration**——沿用既有欄位。

### 必填欄位單一來源（`Features/Seo/SchemaCompleteness.cs`）

新增 `SchemaType` 列舉與 `SchemaRequiredFields.ByType` 靜態字典，逐型別列出必填欄位鍵與中英文
標籤；`GetMissingFields`／`IsComplete` 是唯一的判斷函式。**跨語言的「單一來源」怎麼做到**：
`apps/api`（C#）與 `apps/web`（TypeScript／Nuxt）是兩個獨立執行環境，物理上無法共用同一份程式碼
檔案。本輪的解法是「必填欄位清單只在這裡宣告一次，前台不重新宣告一次判斷條件」——已經有動態
內容可用的兩個公開端點（`ArticlesRepository.GetBySlugAsync`／`MatchesRepository.ListAsync`）
呼叫這個共用函式，把結果算成 `schemaEligible` 布林值放進公開 DTO；`apps/web` 只讀這個布林值
決定輸不輸出 JSON-LD，不再自己判斷「哪些欄位算必填」。後台報表（見下方）直接呼叫同一個函式。

**逐型別必填欄位與判斷依據**（規劃書只列型別清單，沒有列逐欄位規格，以下是本輪判斷，完整理由見
`SchemaRequiredFields` 類別檔頭 XML 文件註解）：

| 型別 | 必填欄位 | 依據 |
|---|---|---|
| `Organization` | 名稱、官網網址、隊徽圖片 | schema.org 只要求 `name`；`url`／`logo` 是本輪依 Google 建議加的，且是 `clubs` 既有欄位 |
| `SportsTeam` | 同 `Organization` 三項 | 同上，`url` 用俱樂部網域、`logo` 回退俱樂部隊徽 |
| `Event` | 名稱、開始時間、地點 | Google Event 結構化資料必要屬性（對應 `calendar_custom_events`） |
| `SportsEvent` | 比賽日期、開球時間、主客場、對手、場地、賽事名稱 | 沿用既有 `app/pages/zh/schedule.vue`（S0-9j）已實作的六欄位判斷，本輪只是收斂進單一來源，判斷條件本身不變 |
| `Person` | 姓名 | schema.org 只要求 `name`（對應 `players`）；肖像同意不影響型別輸不輸出，只影響 `image` 屬性 |
| `Article` | 標題、發布時間、圖片 | Google Article 結構化資料必要屬性；`image` 直接用 `ArticleDetailDto.OgImageUrl`（後端已算好的三層優先序），不重新判斷一次 |
| `Course` | 課程名稱、課程說明 | Google Course 結構化資料必要屬性（`provider.name` 固定為俱樂部本身，不列為資料庫必填欄位） |
| `BreadcrumbList` | 頁面標題、頁面網址 | 只檢查「這一頁本身有沒有可用的標題與網址」這個最小前提，`pages` 沒有頁面層級的標題欄位，只能用 `SeoTitle` 頂替 |
| `FAQPage` | 問題、答案 | schema.org `Question`／`acceptedAnswer.text` 必要屬性 |

🔴 **刻意不把 `SportsTeam.foundingDate` 這類「建議」屬性訂為必填**：`docs/12d-field-audit.md`
已記錄 `clubs` 沒有 `founded_on` 欄位，訂為必填只會讓這個型別永遠輸出不了，對 GEO 沒有幫助；
事實單一來源的欄位缺口屬於 `GEO-03`／`GEO-04`（`S1-12d`）範圍，本輪不為了 `GEO-05` 新增資料欄位。

### 已接上輸出的兩個型別（`Article`／`SportsEvent`）

- `Features/Schedule/MatchDto.SchemaEligible`（`MatchesRepository.Map`）：`app/pages/zh/schedule.vue`
  的 JSON-LD 迴圈改讀 `m.schemaEligible`，取代原本行內的六欄位 `if` 判斷。
- `Features/News/ArticleDtos.ArticleDetailDto.SchemaEligible`（`ArticlesRepository.GetBySlugAsync`）：
  `app/pages/zh/news/[slug]/index.vue` 的 `watchEffect` 改讀 `a.schemaEligible`，取代原本只看
  `publishedAt` 一個欄位的判斷；同時把 Article Schema 的 `image` 欄位從本地 mockup 靜態檔案
  判斷（`hasNewsCover()`）改成 `a.ogImageUrl`——兩者原本可能互相矛盾（`schemaEligible` 說有圖，
  畫面卻因為 mockup 沒有對應檔案而輸出 `undefined`），改用同一份值後不會再有這個落差。

其餘六個型別（`Organization`／`SportsTeam`／`Event`／`Person`／`Course`／`BreadcrumbList`／
`FAQPage`）**目前尚未接上任何前台輸出**（依 `STATUS.md` 排程留給 `S1-12f` 等後續任務），但後台
報表已經在掃描這些型別的資料現況（見下方），資料結構與判斷函式已經讓它們接得上。

### 後台報表（`Features/AdminSeo/AdminSeoSchemaCompletenessRepository`）

`GET /api/v1/admin/{club}/seo/schema-completeness`（權限碼 `seo.schema.view`，`sysadmin_only`，
矩陣「SEO／設定」欄只有系統管理員打勾，理由同既有 `seo.setting.*`／`seo.report.*`）。逐型別掃描
本俱樂部（或俱樂部＋共同）資料，**只回傳有缺漏的列**（比照既有孤立頁面偵測同一種「只列有問題的」
報表設計），列出型別、內部型別詞彙（`club`／`team`／`event`／`match`／`player`／`article`／
`program`／`page`／`faq`）、辨識名稱、公開網址（若有）與缺漏欄位的中英文標籤。

⚠️ **已知簡化**（規劃書沒有列出逐型別掃描範圍，以下是本輪判斷）：
- `BreadcrumbList` 只檢查「這一頁本身有沒有可用的標題與網址」，不驗證完整的頁面階層——B1 頁面
  尚未接上動態路由（同既有 `AdminSeoReportRepository` 孤立頁面偵測的已知落差）。
- `Article` 的圖片優先序（文章專屬 OG 圖片 > 全站預設 OG 圖片 > 封面圖片）在報表裡重新寫了一次
  三層 null 合併運算（不呼叫 `ArticlesRepository.ResolveOgImageAsync`，因為報表只需要知道
  「有沒有圖」，不需要把 key 換成公開網址、不需要注入 `IImagePublicUrlResolver`）——這三層優先序
  若改動，兩處要一起改，比照既有 `ArticleEntityType` 常數在讀寫兩個 repository 各自宣告一份的
  既有先例（風險可控的小範圍重複，不是引入新的重複模式）。

### 權限碼

`db/seed/generate-club-seed-sql.py` 新增 1 個（`module_code=H`、`submodule_code=H6`、
`domain=seo`）：

- `seo.schema.view`：檢視結構化資料完整性檢查

### 測試

- `SchemaCompletenessTests.cs`（18 項，純單元測試，不碰資料庫）：`SchemaRequiredFields` 逐型別
  必填欄位判斷本身——完整／缺單一欄位／空白字串視同缺漏／呼叫端漏傳欄位鍵視為缺漏（防呆）／
  `SchemaTypeCodes.ToCode` 對應 schema.org 正確字面值（含 `FAQPage` 的正確大小寫）。
- `AdminSeoSchemaCompletenessTests.cs`（4 項，真實 HTTP 管線＋真實 `tcrfc_club_test`＋真實
  Azurite）：未登入 401、內容編輯角色 403、**SportsEvent 正反例**（建立缺開球時間／主客場／
  場地／賽事名稱的賽事→報表列出且缺漏欄位剛好是那四個、公開端點 `schemaEligible=false`；
  補齊四欄後報表不再列出、公開端點恢復 `true`；跨俱樂部反例：`bw` 報表看不到 `tcrfc` 這筆測試
  資料）、**Article 正反例**（建立無圖片的已發布文章→報表列出缺漏「文章圖片」、公開端點
  `schemaEligible=false`；用真實 Azurite 上傳一張 OG 圖片後報表不再列出、公開端點恢復 `true`）。

### 驗收紀錄（2026-09-25，本機環境）

1. `dotnet test`（`Tcrfc.Api.Tests`，`tcrfc_club_test`）**494/494 通過**（既有 466 ＋
   `SchemaCompletenessTests` 18 項 ＋ `AdminSeoSchemaCompletenessTests` 4 項 ＋ 過程中發現／
   修正的既有計數差異；純單元測試不需要任何 fixture，整合測試需要
   `AdminWriteAzuriteEnabledApiFixture`——Article 正反例要真的上傳 OG 圖片）。
2. 種子資料：新增 1 個權限碼（`seo.schema.view`），已用 `./db/seed/setup-club-db.sh`（現為 tcrfc_club）
   與 `./db/seed/apply-seed.sh`（`tcrfc_club_dev`）灌入，`sqlcmd` 確認 `system_admin` 角色已
   自動取得。**沒有 migration**。
3. `dotnet run` 本機真實啟動 `apps/api` ＋ `apps/web` `npm run build` 產物
   （`node .output/server/index.mjs`），用真實 HTTP 驗證前台輸出：
   - `curl /zh/news/2025-03-22-match-070/`（該篇無任何圖片來源）：`X-Robots-Tag: noindex, nofollow`
     仍在；頁面只有 `@nuxtjs/seo` 自動輸出的 `WebSite`／`WebPage` 節點，**沒有** `Article` 節點
     （`schemaEligible=false`，公開 API 實測確認）。
   - 在開發庫**暫時**設定 `clubs.og_image_key`（事後已還原為 `NULL`，兩俱樂部皆確認）後重新
     整理同一頁：`Article` 節點正確出現，含 `headline`／`datePublished`／`image` 三個必填欄位，
     `X-Robots-Tag: noindex, nofollow` 不受影響。
   - `curl /zh/schedule/`：21 場 tcrfc 種子賽事的 `schemaEligible` 全數為 `true`（六個欄位皆
     齊全），頁面正確輸出 21 個 `SportsEvent` 節點，`X-Robots-Tag: noindex, nofollow` 不受影響。
4. `npm run lint`：`apps/web`（0 錯誤，既有警告與本輪無關）、`apps/admin`（全過，本輪未修改
   該專案任何檔案）皆綠燈。
5. `apps/web` `npm run build` 成功；`apps/web`／`apps/api` `docker build` 皆成功。

### 已知缺口（回報，不在本輪範圍）

1. **後台畫面待做**：`apps/admin` 完全未改動（任務邊界僅 `apps/api`／`apps/web`）。
2. **其餘六個型別（`Organization`／`SportsTeam`／`Event`／`Person`／`Course`／`BreadcrumbList`／
   `FAQPage`）尚未接上任何前台輸出**：依 `STATUS.md` 排程留給 `S1-12f` 等後續任務，本輪只確保
   後台報表看得到這些型別的資料缺漏現況、`SchemaRequiredFields` 已經涵蓋全部九型別。
3. **`GEO-03`／`GEO-04`（事實單一來源與雙重呈現）不在本輪範圍**：依 `STATUS.md` 排程屬
   `S1-12d`（主站前台）。

## S1-12f：Schema 逐型別輸出第一批（2026-09-25，`frontend-architect`）

主站規劃書 §7 SEO 九項｜`GEO-05`：把 `Organization`／`SportsTeam`／`Person`／`Article`／
`BreadcrumbList` 五個型別接上前台輸出。`Article` 已在 S1-12c 完成，本輪確認它符合同一套做法
（`schemaEligible` 閘門）即可，不重複改動。**沒有新增資料表、沒有 migration**——沿用既有欄位，
只新增 DTO 上的計算欄位。**判斷一律呼叫既有 `SchemaRequiredFields.IsComplete`**（`Features/Seo/
SchemaCompleteness.cs`），沒有另外寫一份必填判斷（E-39）。

### DTO 新增欄位

| DTO | 新增欄位 | 對應型別 | 必填欄位判斷用的值 |
|---|---|---|---|
| `Features/Clubs/ClubDto` | `SchemaEligible`、`LogoUrl` | `Organization` | `name`＝`Name`、`url`＝`Domain`、`logo`＝`LogoLightKey` |
| `Features/Teams/TeamDto` | `SchemaEligible`、`LogoUrl` | `SportsTeam` | `name`＝`Name`、`url`＝所屬俱樂部 `Domain`（需要 join `clubs`）、`logo`＝`HeroKey` **擇一回退** `Club.LogoLightKey`（`SchemaRequiredFields` 檔頭「logo（Club.LogoLightKey／Team.HeroKey）」的「／」判讀為「擇一即可」，比照既有 OG 圖片優先序寫法，非新規則） |
| `Features/Players/PlayerDto` | `SchemaEligible`、`PhotoUrl` | `Person` | `name`＝`Name`（只要求姓名） |
| `Features/Staff/StaffDto` | `SchemaEligible`、`PhotoUrl` | `Person` | `name`＝`Name`（只要求姓名） |
| `Features/News/ArticleDtos.ArticleDetailDto` | `BreadcrumbSchemaEligible` | `BreadcrumbList` | `name`＝`Title`、`path`＝`Slug` |

`LogoUrl`／`PhotoUrl` 都是既有 `IImagePublicUrlResolver.Resolve(objectKey)` 的直接呼叫（同一支
S1-12 新增的服務，已用於 `OgImageUrl`），不是新機制。`PlayerDto.PhotoUrl`／`StaffDto.PhotoUrl`
用的是**已經套用肖像同意 fail-closed 規則之後**的 `PhotoKey`（S1-7a：`portrait_consent_status`
非同意時 `PhotoKey` 本身已經是 `null`），這裡不需要再檢查一次同意狀態，沿用同一個 fail-closed
結果即可——未同意肖像使用的球員／教練，`PhotoUrl` 恆為 `null`。

`TeamsRepository.ListAsync` 因此新增 `JOIN clubs c ON c.id = t.club_id`，多查 `c.domain`／
`c.logo_light_key` 兩欄（只供 `SchemaEligible`／`LogoUrl` 計算用，不進 `TeamDto` 既有欄位，不是
契約變更）。

### 🔴 已知現況：兩個俱樂部目前 `Organization`／`SportsTeam` 恆為不合格

`clubs.logo_light_key`／`teams.hero_key` 目前的種子資料（`db/seed/generate-club-seed-sql.py`）
與既有後台（`Features/AdminClubs/AdminClubDtos.cs` 對標誌三組欄位刻意唯讀，見該檔案既有註解）
都沒有任何寫入路徑——兩個俱樂部現況下這兩個布林值恆為 `false`。這是 `GEO-05`「缺漏者不輸出
該型別」的正確行為，**不是這裡的判斷有誤**：`SchemaRequiredFields` 對 Organization／SportsTeam
的必填欄位定義（`name`／`url`／`logo`）是 S1-12c 就已經寫定的單一來源，本輪只是把既有定義接上
真正的資料庫查詢，沒有重新定義必填欄位（否則會違反 E-39 的精神）。等後台補上隊徽上傳路徑、
`logo_light_key`／`hero_key` 有真實值之後，這裡的 `SchemaEligible` 會自動變 `true`，不需要再
改任何程式碼。

### 測試

新增 `Tcrfc.Api.Tests/PublicSchemaEligibilityTests.cs`（8 項，`ApiFixture`／`NoOpQueryCache`，
不需要處理快取失效）：

| 測試 | 涵蓋 |
|---|---|
| `Club_種子資料沒有隊徽物件鍵_Organization不合格` | 現況驗證：`SchemaEligible=false`、`LogoUrl=null` |
| `Club_補上隊徽物件鍵後_Organization合格且LogoUrl有值` | 直接 `UPDATE clubs SET logo_light_key=...`，驗證 `SchemaEligible` 翻正，`finally` 還原 |
| `Team_種子資料沒有識別圖片_SportsTeam不合格` | 現況驗證 |
| `Team_補上球隊識別圖片後_SportsTeam合格` | 直接 `UPDATE teams SET hero_key=...`，`finally` 還原 |
| `Team_球隊自己沒有識別圖片但俱樂部有隊徽時_擇一合格` | 驗證 `logo` 欄位「擇一即可」的回退邏輯 |
| `Players_種子資料皆有姓名_Person全數合格` | 28 名球員皆 `SchemaEligible=true`；`PhotoUrl` 皆 `null`（種子資料未設定同意） |
| `Staff_種子資料皆有姓名_Person全數合格` | 8 位教練同上 |
| `Article_有標題與網址_BreadcrumbList合格` | `BreadcrumbSchemaEligible=true` |

🔴 **`LogoUrl`／`PhotoUrl` 在這批測試裡不斷言非空字串**：`ApiFixture` 沒有接真實 Azurite，
`IImagePublicUrlResolver` 綁的是一律回傳 `null` 的 `UnavailableImagePublicUrlResolver`——
`Resolve()` 本身的解析邏輯已由 `AdminSeoImageTests`（真實 Azurite）驗證過，這裡只驗證
`SchemaEligible` 有沒有正確吃到新寫入的物件鍵。

`dotnet test`（`Tcrfc.Api.Tests.csproj`）**502/502 全過**（既有 494 ＋ 本輪新增 8）。
`dotnet build`／`apps/web` `npm run lint`（0 錯誤）／`apps/web` `npm run build`／`apps/api`
與 `apps/web` 的 `docker build` 全過。

### 前台（`apps/web`）串接

見 [`apps/web/README.md`](../web/README.md)「S1-12f」節（新增 `useSchemaOrgClub.ts`，接上
首頁／關於頁／一線隊頁／`our-people.vue`／`news/[slug]`；`academy/teams.vue` 因梯隊為未成年學員刻意不輸出 Person）。

### 已知缺口（回報，不在本輪範圍）

1. **`clubs.logo_light_key`／`teams.hero_key` 無寫入路徑**：見上方「已知現況」——機制已就緒，
   等後台補上傳路徑即生效。
2. **`apps/admin` 未改動**：`H6`（`SchemaCompletenessView.vue`）畫面上「目前前台實際已輸出的
   型別只有新聞與故事文章、賽程賽事兩種」這句文字，本輪之後已經過時（Organization／SportsTeam／
   Person／BreadcrumbList 部分頁面也已輸出），依任務邊界本輪不得改 `apps/admin`，留給下一輪
   處理 `apps/admin` 的人同步更新這句文字。
3. **`Event`（`calendar_custom_events`）／`Course`（`training_programs`）／`FAQPage`（`faqs`）
   三個型別仍未接上任何前台輸出**：不在本輪任務範圍（任務指示明列的第一批只有五個型別），
   `SchemaRequiredFields` 已涵蓋這三型別的必填欄位定義，留給後續任務。

## E-64：公開端點物件鍵解析成網址（2026-09-29，`backend-engineer`）

`docs/18-work-errors.md` E-64 記過：`Features/Home/HomeRepository.ListBannersAsync` 把
`banners.image_key` 原封不動塞進 `BannerDto.ImageKey`，沒有像 `Features/Staff`／`Features/Players`
那樣注入 `IImagePublicUrlResolver` 解析成可以直接放進 `<img src>` 的完整網址——前台拿到手完全
無法組出可用網址。本輪修正這支端點，並**照任務指示排查其他公開端點是否有同一種缺口，找到的一併修**
（範圍**只含公開端點**，`Features/Admin*` 不在這次任務範圍內，見下方「刻意沒有修的範圍」）。

### 修正範圍

| Repository | DTO | 新增欄位 | 解析器 |
|---|---|---|---|
| `Features/Home/HomeRepository` | `BannerDto` | `ImageUrl`、`VideoUrl` | `IImagePublicUrlResolver`／**新增** `IVideoPublicUrlResolver` |
| `Features/Teams/TeamsRepository` | `TeamDto` | `HeroUrl` | `IImagePublicUrlResolver`（既有，已注入） |
| `Features/Clubs/ClubsRepository` | `ClubDto` | `LogoDarkUrl`、`FaviconUrl`、`OgImageUrl` | `IImagePublicUrlResolver`（既有，已注入） |
| `Features/Calendar/CalendarRepository` | `PublicCalendarEventDto` | `CoverUrl` | `IImagePublicUrlResolver`（**新增注入**） |
| `Features/Programs/ProgramsRepository` | `ProgramListItemDto`／`ProgramDetailDto`／`ProgramPartnerSummaryDto` | `CoverUrl`（前兩者）、`LogoDarkUrl`／`LogoLightUrl`（夥伴摘要） | `IImagePublicUrlResolver`（**新增注入**） |
| `Features/News/ArticlesRepository` | `ArticleListItemDto`／`ArticleDetailDto` | `CoverUrl` | `IImagePublicUrlResolver`（既有，已注入） |

原始的 `XxxKey` 欄位全部保留（不是替換），比照 `StaffDto.PhotoKey`／`PhotoUrl` 並存的既有慣例——
`Key` 給需要原始物件鍵的既有呼叫端（例如後台比對用），`Url` 給前台直接顯示用。

**影片獨立走一顆新解析器**：`banners.video_key` 是獨立的 Blob 容器（`AZURE_BLOB_CONTAINER_VIDEOS`，
跟圖片的 `AZURE_BLOB_CONTAINER_IMAGES` 分開，見 `Videos/BlobVideoStorageService.cs`），不能沿用
`IImagePublicUrlResolver`（容器不同，算出來的網址會指到錯誤的容器）。新增
`Videos/IVideoPublicUrlResolver.cs`／`BlobVideoPublicUrlResolver.cs`（具名 DI 注入
`[FromKeyedServices("videos")] BlobContainerClient`，寫法照抄 `BlobImagePublicUrlResolver`）／
`UnavailableVideoPublicUrlResolver.cs`（`AZURE_BLOB_CONNECTION_STRING` 未設定時的替身），
`Program.cs` 兩個分支（設定／未設定連線字串）都各自註冊對應實作。

**`ClubDto.LogoLightKey` 沒有新增 `LogoLightUrl`**——它在 S1-12f 就已經解析成 `LogoUrl`（既有欄位，
只是命名不對稱，不是漏解析），本輪沒有改名（改名要動 `PublicSchemaEligibilityTests` 既有斷言，
屬於契約變更，不在這次修錯字缺口的範圍內）；`ArchitectureTests` 的新守門測試已把這個既有命名
差異寫進 `knownExceptions` 並附理由，見下方「防呆」。

### 刻意沒有修的範圍

`Features/Admin*` 一樣有同一種缺口（例如 `AdminBannerListItemDto.ImageKey`／`VideoKey`、
`AdminClubDtos` 的標誌三欄位），任務指示明文只點名「公開端點」，後台畫面的同款缺口留給下一輪。

### 前台要照這個串（`apps/web`）

公開 API 回傳的 JSON 是 camelCase（既有慣例，見 `banners` API 既有的 `cta1Label`／`cta1Url`），
新欄位在前台看到的名稱是 `imageUrl`／`videoUrl`／`heroUrl`／`logoDarkUrl`／`faviconUrl`／
`ogImageUrl`／`coverUrl`／`logoLightUrl`。`apps/web/app/pages/zh/index.vue` 目前寫死「輪播圖片
本身維持現有素材直到後端補上 `ImageUrl` 欄位」的等待註解（S1-14），現在可以改讀
`GET /api/v1/{club}/banners` 回應的 `imageUrl` 欄位；`banners` 資料表目前種子資料是 0 筆
（`db/seed` 沒有種子資料），實際能不能看到圖片仍要等後台建立輪播資料。

### 測試

新增／擴充：
- `Tcrfc.Api.Tests/AdminBannersAndHomeSectionsTests.cs`：`Banner_圖片寬高由上傳結果自動填入_alt雙語`
  新增斷言公開端點 `imageUrl` 非空、且網址含有 `imageKey`；
  `Banner_影片模式_建立成功_海報圖與影片鍵皆有值_公開端點吐出videoKey` 新增斷言 `imageUrl`／`videoUrl`
  兩者非空、互不相同、且分別落在 `images-test`／`videos` 兩個不同容器路徑——用真實 Azurite 驗證
  「影片鍵有沒有誤接到圖片解析器」這個最容易犯的錯（兩顆解析器介面長得一模一樣，只差容器）。
- `Tcrfc.Api.Tests/ArchitectureTests.cs`：新增純語法掃描測試（見下方「防呆」），不需要資料庫。

`dotnet test`（`Tcrfc.Api.Tests.csproj`，`tcrfc_club_test`）：**503／503 全過**（既有 502 ＋
本輪新增 1 項架構守門測試；既有測試的斷言擴充不算新增筆數）。`dotnet build` 全過，0 警告 0 錯誤。

### 防呆

`Tcrfc.Api.Tests/ArchitectureTests.cs` 新增
`公開DTO的物件鍵欄位都必須有對應的完整網址欄位`：掃 `Features/*`（排除 `Features/Admin*`）裡
`public` 且型別名稱以 `Dto` 結尾的 record，任何名稱以 `Key` 結尾的 `string`／`string?` 屬性都必須
有同一個 record 裡對應的 `{去掉 Key 的字首}Url` 屬性，否則測試失敗並印出檔案與行號。這支測試會
在下一次有人加圖片欄位卻忘記接解析器時，讓 `dotnet test` 直接紅燈，不必等到前台真的串接時才
發現。詳細設計理由、涵蓋邊界、已知例外清單見 `docs/18-work-errors.md` E-64「防呆」段。

## S1-12d：`I` 網站設定——`GEO-03`／`GEO-04` 站台事實承載與公開端點（2026-09-29，`backend-engineer`）

主站規劃書 §7 `GEO-03`（成立年份、主場與場地、梯隊組成、所屬聯賽、聯絡方式**全站只有一個維護處**，
後台 `I` 網站設定或其所屬模組）／`GEO-04`（結構化資料與明文同時呈現、數值一致）；規劃書 §7 行 1690
明文「GEO-03 的事實沿用既有欄位（`Setting`／`Club`／`Team`／`Venue`／`ImpactMetric`）」——**不新增
資料表**，這是接續 `apps/web/README.md`「S1-12d」節（`frontend-architect` 當時盤點出後端完全沒有
承載這五類事實的欄位與公開端點，做了前台暫定的 `shared/utils/site-facts.ts`）的後端補完。

### 資料放在哪

不改資料庫綱要。純量與逐語系事實沿用既有 `settings`／`settings_i18n`（`setting_group='site'`），
主場場地本身沿用既有 `venues`／`venues_i18n`：

| 事實 | 存放位置 |
|---|---|
| 成立年份 | `settings.site.founded_year`（純值，非人類語言） |
| 成立日期（ISO） | `settings.site.founding_date`（純值） |
| 「＿＿年創立」顯示句 | `settings.site.founding_date_display`（逐語系） |
| 首季頭銜 | `settings.site.founding_title`（逐語系，可整筆不存在） |
| 所屬聯賽全名 | `settings.site.league_name`（逐語系） |
| 聯賽簡稱 | `settings.site.league_short_name`（逐語系，可整筆不存在） |
| 梯隊組成敘述 | `settings.site.squad_structure_summary`（逐語系） |
| 梯隊年齡層代碼清單 | `settings.site.squad_codes`（逗號分隔純值，例：`U15,U14,U12`） |
| 聯絡電話 | `settings.site.contact_phone`（純值，兩俱樂部現況皆 `NULL`） |
| 營業時間 | `settings.site.contact_hours`（逐語系，兩俱樂部現況皆 `NULL`） |
| 主場場地引用清單 | `settings.site.home_venue_ids`（逗號分隔 `venues.id`，依顯示順序，第一筆＝主要主場） |
| 主場場地名稱／地址 | 既有 `venues`／`venues_i18n`（**不在 `Setting` 裡重複存一份**） |

🔴 **`Venue` 本身刻意不帶 `club_id`**（docs/12 §4.7：場地是地理實體，重複建會產生兩組人工標的座標）。
「這個俱樂部的主場是哪幾筆既有 `Venue`」這件事本身才是俱樂部範圍的事實，因此用
`settings.site.home_venue_ids`（`club_id` 必填）表達引用清單，不是在 `Venue` 上加
`club_id`／`is_home_ground` 欄位——這樣完全不用改既有綱要。從清單移除一筆場地**不會刪除**
`Venue` 列本身（可能仍被其他俱樂部或其他資料引用）。**聯絡地址不重複儲存**：直接取主要主場地址
計算得出，不是獨立欄位。

### 端點

| 方法與路徑 | 說明 | 權限 |
|---|---|---|
| `GET /api/v1/admin/{club}/site-facts` | 後台讀取（`I` 模組） | `site.fact.view`，`sysadmin_only` |
| `PUT /api/v1/admin/{club}/site-facts`（JSON） | 後台整份取代寫入 | `site.fact.update`，`sysadmin_only` |
| `GET /api/v1/{club}/site-facts?lang=zh\|en` | 公開讀取，不需登入 | — |

新增檔案：`Features/AdminSiteFacts/`（`AdminSiteFactsDtos.cs`／`AdminSiteFactsRepository.cs`
／`AdminSiteFactsEndpoints.cs`／`AdminSiteFactsExceptions.cs`，EF Core，比照
`Features/AdminSeo/AdminSeoSettingsRepository` 既有寫法）、`Features/SiteFacts/`
（`SiteFactsDtos.cs`／`SiteFactsRepository.cs`／`SiteFactsEndpoints.cs`，Dapper＋`IQueryCache`，
比照 `Features/Seo/SeoRepository` 既有寫法）。`Program.cs`／`Common/ApiExceptionHandler.cs` 已接上
DI 註冊、路由掛載、例外轉狀態碼。

### 回應形狀（camelCase，前台照這個串）

`PUT` 是**整份取代語意**（比照 `AdminSeoSettingsDto` 既有慣例）：呼叫端一律送出完整表單內容，
省略欄位＝清空該欄位，不是「維持不變」；`homeVenues` 陣列裡帶 `id` 的既有場地會被更新（找不到
對應 `id` 回 400，不會默默改成新增一筆），不帶 `id` 的會新增一筆。

```jsonc
// GET /api/v1/tcrfc/site-facts?lang=zh
{
  "foundedYear": "2024",
  "foundingDateIso": null,
  "foundedDisplay": "2024 年創立",
  "foundingTitle": "全國乙級聯賽冠軍",
  "league": { "name": "企業甲級聯賽", "shortName": null },
  "venues": [{ "name": "西屯足球場", "address": "台中市北屯區崇平路二段景谷巷 11 弄 41 號", "isHomeGround": true }],
  "squadStructureSummary": "一線隊與足球學院（U15／U14／U12）三個梯隊並行的發展體系",
  "squadCodes": ["U15", "U14", "U12"],
  "contact": { "address": "台中市北屯區崇平路二段景谷巷 11 弄 41 號", "phone": null, "hours": null }
}
```

🔴 **公開端點的人類語言欄位依 `?lang=` 解析成單一語系字串**（比照 `Features/Players`／
`Features/Schedule` 既有慣例：請求語系有值就用，沒有就退回中文，兩者都沒有回傳 `null`），
跟 `Features/Seo` 的公開設定端點一次回傳 `xxxZh`／`xxxEn` 兩份欄位的既有慣例**不同**——這是本輪
自行判斷（`Features/Seo` 的設定值是後台編輯表單直接消費雙欄位，這裡是給一般頁面顯示用的公開內容，
比照大多數既有公開端點的單語系慣例）。⚠️ **若前台之後做 JSON-LD（`SportsTeam.memberOf.name`
等）需要不受 `lang` 影響的中文全名**，另外用 `?lang=zh` 呼叫一次本端點即可（有快取，成本很低），
不在回應裡重複塞兩種語系的欄位。後台端點（`AdminSiteFactsDto`）維持雙欄位（`xxxZh`／`xxxEn`）
給編輯表單用，跟 `AdminSeoSettingsDto` 一致。

### 種子資料

`db/seed/generate-club-seed-sql.py` 新增「24. site facts」段（緊接在既有「23. event_types」之後，
`--reset-admin-accounts` 分支之前），把 `apps/web/shared/utils/site-facts.ts` 目前兩站的已核實真實值
寫入 `settings`／`settings_i18n`；並新增台中磐石（tcrfc）主場「西屯足球場」的 `Venue` 列（既有種子
只建了藍鯨的兩座場地）。電話與營業時間兩俱樂部皆未核實，S1-12d 當時刻意不種；**2026-09-30 起改種明顯的測試值**（電話 `04-0000-0000`、營業時間「【測試】平日 09:00–18:00」且只有中文），見 [`db/seed/README.md`](../../db/seed/README.md)「測試值清單」與「後台模組種子（2026-09-30）」一節。

🔴 **盤點時發現的既有資料落差（不是本輪造成，回報給下一輪決定）**：台中藍鯨兩座既有 `Venue` 列
（太原足球場／豐原體育場，S1-11 賽事匯入時已建立）的 `venues_i18n.address` 其實**已經有真實地址**
（取自 `content/blue-whale/data/venues.json`／`venues.md` 盤點），但 `apps/web/shared/utils/
site-facts.ts` 目前對這兩個場地的 `address` 欄位寫死 `null`（該檔案的註解引用
`content/blue-whale/gap-analysis.md`「沒有任何實體地址」，講的其實是「聯絡地址」不是「場地地址」，
兩者在 `site-facts.ts` 目前合併成同一個判斷）。本輪**沒有覆寫**這兩筆既有 `Venue` 列，只是把它們
透過 `site.home_venue_ids` 接上——這代表**公開端點現在會如實回傳這兩個地址**，跟前台目前的
`null` 假設不一致。前台下一輪整合這支端點時，需要決定要不要更新 `site-facts.ts` 改用地址（或
保留現有判斷，讓 API 回傳的地址暫不顯示）。

另外，既有 `Venue` 列「豐原體育場」的官方全名是「台中市立豐原體育場」（`venues_i18n.name` 既有值），
跟 `site-facts.ts` 目前的簡稱「台中豐原體育場」不同——本輪**沿用既有列**（不重複建一筆近似場地，
GEO-03「同一事實不得在兩處各寫一份」），公開端點回傳的是既有列的全名，前台整合時名稱會跟著換成
這個較長的官方全名。

### 權限

新增 `site.fact.view`／`site.fact.update`（`module_code=I`，新分配 `submodule_code=I1`，
新增 `domain=site`）。**規劃書 §6 權限矩陣沒有「網站設定」欄**（`I` 模組在本輪之前完全沒有後端
實作），本輪比照 `seo.*`／`system.*` 既有先例——「全站層級設定、非逐篇內容編輯」的既有判斷——
把兩碼都標記 `sysadmin_only=1`，十個角色只有系統管理員可存取。這是本輪自行判斷，規劃書沒有明文
要求，已同步寫入 [`docs/12b-database-tables.md`](../../docs/12b-database-tables.md) §7.3／§7.4
「S1-12d 新增」。

### 後台畫面（`apps/admin`）

**沒有做**——`apps/admin` 目前完全沒有對應 `I` 模組的任何畫面（`src/views/` 底下沒有
`site-facts`／`settings` 相關檔案，只有 H 模組的 `SeoSettingsView.vue`）。依任務指示「沒有的話在
回報中說明，不要從零搭整個模組」，本輪只交付 API，後台編輯畫面留給下一輪（`frontend-architect`
或指派的前端工作）。

### 快取

公開端點接上既有 `IQueryCache`（entity `site-facts`，club／locale 兩個維度，跟其餘五組既有
repository 同一套機制）。比照 `Features/Seo/SeoRepository` 的既有取捨：後台寫入端**刻意不呼叫**
`InvalidateAsync`，管理員改設定後最多延後一個 TTL（預設 300 秒）才會反映到公開端點，不是遺漏。
`GEO-03`／`GEO-04` 的內容不在 `docs/17-deployment.md` §4「五類不得讀快取」清單內，可以安全接。

### 測試

新增 `Tcrfc.Api.Tests/SiteFactsTests.cs`（9 項）：後台未登入 401、非系統管理員角色 403、
系統管理員讀取種子資料正確（含藍鯨兩座主場依序排列、`squadCodes`、`foundingTitleZh` 為 `null`）、
完整寫入輪替（含中文必填欄位驗證、`homeVenues` 帶既有 `id` 更新不新增、公開端點立即反映、
測試後用 `finally` 還原成原始內容——比照 `AdminSeoTests` 既有紀律，避免 E-62 那一類跨測試互相
干擾）、指定不存在的場地 `id` 回 400、公開端點中文預設值、`lang=en` 解析與回退中文、俱樂部隔離
（藍鯨與磐石讀到不同事實，藍鯨沒有磐石的 `foundingTitle`）。

**驗收（2026-09-29）**：
```
dotnet build                                    # 0 個警告，0 個錯誤
dotnet test --filter FullyQualifiedName~SiteFactsTests   # 9/9 通過
dotnet test                                     # 512/512 全過（既有 503 ＋ 本輪新增 9）
docker build -f apps/api/Dockerfile apps/api    # 成功
```
種子資料以 `./db/seed/setup-club-db.sh --recreate` 重建 `tcrfc_club_test` 後，直接用 `sqlcmd`
核對過 `settings`／`settings_i18n`／`venues`／`venues_i18n` 四張表的實際寫入內容（見上方「種子資料」
的落差說明），不是只看 API 回應。

### 已知缺口（回報，不在本輪範圍）

1. **`apps/web` 尚未串接**——依任務指示本輪不改 `apps/web`，`shared/utils/site-facts.ts` 仍是前台
   暫定來源，下一輪需要整批改成 `useFetch('/api/backend/{club}/site-facts?lang=...')`，並決定
   「JSON-LD 用中文全名」的呼叫方式（見上方「回應形狀」的 `?lang=zh` 說明）。
2. **`apps/admin` 沒有編輯畫面**——見上方「後台畫面」。
3. **既有藍鯨場地地址／全名與 `site-facts.ts` 現況不一致**——見上方「種子資料」的兩則落差說明，
   需要前端／內容盤點決定怎麼處理，不是本輪能單方面決定的內容判斷。
4. **`squadCodes` 是編輯值不是即時查詢 `teams` 表**——台中磐石（tcrfc）目前完全沒有建立對應的
   `Team` 列（U15／U14／U12，球員名單與肖像同意未到位，見 `STATUS.md`），若改成即時查詢會得到
   空清單而非正確答案，故 `squadCodes` 目前是獨立維護的純值。日後磐石真的建立這些 `Team` 列時，
   要考慮是否改為即時查詢以避免兩處各寫一份（GEO-03）——本輪判斷「暫不能查詢」不等於「永遠不查詢」。

### 後續補完（2026-09-29）：藍鯨官網網址與場地清單端點

主站規劃書 §3.6「06 女子足球」入口頁明文「藍鯨官網網址於後台 `I` 網站設定可維護」，以及
`apps/admin/README.md`「I：網站設定」規格疑點第 1 點與
`apps/admin/src/views/teams/MatchEditView.vue` 檔頭都記過的同一個缺口——後台沒有任何「列出全部
場地」的端點，「從既有場地中選擇主場／比賽地點」做不出來。本節補這兩件事，**不改資料庫綱要**
（`Setting` 鍵值與既有 `venues` 主檔已足夠）。

#### 藍鯨官網網址

新增 `Setting` 鍵 `site.blue_whale_site_url`（`setting_group='site'`，單一值、非人類語言——網址
本身不需要逐語系，命名與既有 `site.home_venue_ids` 同一組詞彙慣例）。**概念上只屬於台中磐石
（`tcrfc`）**——藍鯨官網本身沒有 06 單元（`docs/13-blue-whale-site.md` §6「不設 06」），因此以
`bw` 呼叫時這個鍵預期恆為 `null`。**沒有另外加俱樂部白名單檢查強制這件事**——沿用既有「有些站台
事實不是每個俱樂部都有」的原則（比照 `foundingTitle`），這是本輪自行判斷，任務指示本身也是描述
而非要求技術層面的強制。

- **DTO 欄位**：`AdminSiteFactsDto.BlueWhaleSiteUrl`／`UpdateSiteFactsRequest.BlueWhaleSiteUrl`／
  `PublicSiteFactsDto.BlueWhaleSiteUrl`（camelCase `blueWhaleSiteUrl`），皆為 `string?`，
  `null`＝尚未設定。
- **驗證**：`AdminSiteFactsRepository.ValidateBlueWhaleSiteUrl`——空白合法（未設定），有值時必須是
  `Uri.TryCreate(value, UriKind.Absolute, ...)` 可解析**且** `Scheme == Uri.UriSchemeHttps`，
  否則 400（「台中藍鯨官網網址格式不正確，須為 https:// 開頭的完整網址。」）。不接受
  `http://`、相對路徑、或 `javascript:` 這類非預期 scheme。
- **整份取代語意不變**：PUT 省略這個欄位＝清空既有值，跟其餘 `site.fact.*` 欄位一致。
- **種子資料**（`db/seed/generate-club-seed-sql.py`「24. site facts」段）：只種 `tcrfc` 一筆，
  值為 staging 網域 `https://bw-stg.tcrfc.tw`——跟 `apps/web/nuxt.config.ts` 的
  `NUXT_PUBLIC_BLUE_WHALE_SITE_URL` 預設值一致（見 `apps/web/README.md`「S1-16」），藍鯨正式
  網域定案前不放正式網址。**不種 `bw`**。
- **`apps/web` 尚未串接**（沿用上方「已知缺口」第 1 點同一個狀態）——`apps/web/app/pages/zh/
  womens/index.vue` 目前讀的是 `useRuntimeConfig().public.blueWhaleSiteUrl`（容器環境變數），
  不是這支 API；下一輪整批改用 `useFetch` 時可以一併改讀這個欄位，改完之後藍鯨正式網域定案就只
  需要後台改一個值，不必再改環境變數或重新部署容器。

#### 場地清單端點（`Features/AdminVenues`，新增檔案）

新增唯讀端點 `GET /api/v1/admin/{club}/venues`（`Features/AdminVenues/AdminVenuesDtos.cs`／
`AdminVenuesRepository.cs`／`AdminVenuesEndpoints.cs`，EF Core，比照
`Features/AdminCompetitions.ListSeasonsAsync` 既有「唯讀清單掛在既有相關模組權限碼底下」的寫法）。

- **回應內容與 `{club}` 路由段無關**：`Venue` 本身不帶 `club_id`（docs/12 §4.7，場地是地理實體，
  兩俱樂部可能共用同一座球場），這支端點回傳的是**全站**場地，路由掛 `{club}` 只是借用既有
  `IAdminClubAuthorizer` 授權管線（帳號狀態、俱樂部存在、俱樂部授權、權限碼四步一次到齊），任何
  俱樂部呼叫都會拿到同一份清單。
- **回應形狀**（camelCase）：
  ```jsonc
  // GET /api/v1/admin/tcrfc/venues
  [
    { "id": "…", "nameZh": "西屯足球場", "nameEn": "Xitun Football Field", "address": "台中市北屯區崇平路二段景谷巷 11 弄 41 號" },
    { "id": "…", "nameZh": "台中北屯太原足球場", "nameEn": null, "address": "…" },
    { "id": "…", "nameZh": "台中市立豐原體育場", "nameEn": null, "address": "…" }
  ]
  ```
  排序：`SortOrder` 優先（既有列目前全部是 0），再以中文名稱（Ordinal）穩定排序，避免下拉選單
  順序看起來隨機。
- **權限**：`IAdminClubAuthorizer.AuthorizeAnyAsync`，候選碼 `site.fact.view`（`I` 網站設定挑
  主場）與 `team.match.view`（`C4` 賽程與賽果挑比賽地點）任一通過即可——這是任務指示「能看網站
  設定或賽事的人都能讀」的具體判斷，這兩個是目前僅有的兩處「需要挑選既有場地」的既有畫面／缺口。
  **不新增權限碼**：這只是一份共用主檔的唯讀清單，不是需要獨立授權把關的新業務功能，比照
  `Features/AdminCompetitions.ListSeasonsAsync` 掛在既有賽事模組權限碼底下、不另開球季模組權限碼
  的既有先例。403 訊息沿用 `AdminClubAuthorizer` 既有文字（「你的角色沒有這項操作的權限，請洽
  系統管理員。」），不內插權限碼（E-52）。
- **只做唯讀清單，不做場地的新增／刪除管理**——任務範圍明文排除，規格也沒有要求。新增場地仍然
  透過既有管道完成（例如 `Features/AdminSiteFacts` 的 `UpdateSiteFactVenueRequest` 省略 `Id`
  即新增一筆）。
- **`apps/admin/src/views/teams/MatchEditView.vue` 與 `apps/admin/README.md`「I：網站設定」
  規格疑點第 1 點的缺口至此已補齊後端**——兩個畫面要改成「從既有場地中選擇」下拉選單，需要下一輪
  前端工作接上這支端點，本輪不改 `apps/admin`（任務範圍排除）。

#### 測試與驗證（2026-09-29）

`SiteFactsTests.cs` 擴充（藍鯨官網網址讀取／寫入輪替／`https` 格式驗證三種失敗情境／俱樂部隔離
斷言 `bw` 恆為 `null`），新增 `AdminVenuesTests.cs`（5 項：未登入 401、兩組候選權限碼都沒有的
角色 403 且訊息不帶權限碼、只持有 `team.match.view` 的角色可讀、系統管理員讀取含雙語名稱與地址、
回應與 `{club}` 路由段無關）。

```
dotnet build                                              # 0 個警告，0 個錯誤
dotnet test --filter FullyQualifiedName~SiteFactsTests\|FullyQualifiedName~AdminVenuesTests
                                                           # 17/17 通過
dotnet test                                               # 520/520 全過（既有 512 ＋ 本輪新增 8）
docker build -f apps/api/Dockerfile apps/api              # 成功
```

種子資料以 `./db/seed/setup-club-db.sh` 重灌 `tcrfc_club_test` 後，直接用 `sqlcmd` 核對過
`settings` 只有 `tcrfc` 一筆 `site.blue_whale_site_url`、`venues` 總筆數為 3，不是只看 API 回應。

#### 已知缺口（回報，不在本輪範圍）

1. **`apps/web`／`apps/admin` 皆未串接**——依任務指示本輪只改 `apps/api`，前台「前往台中藍鯨
   官網」按鈕與後台「網站設定」「賽程」兩個畫面的下拉選單串接留給下一輪。
2. **`Venue` 沒有「新增／刪除管理」端點**——本輪刻意只做唯讀清單（任務範圍排除），新增場地仍然
   只能透過 `Features/AdminSiteFacts` 的既有「更新主場清單時順便新增」管道，不是獨立的場地管理
   功能；日後若真的需要獨立的場地 CRUD 畫面，需要先決定要不要新增場地管理權限碼。

---

## S1-17 修正：多重受信任代理來源——10 表單中心改走 Nuxt 伺服器端代理後，補齊限流的訪客真實 IP 判斷（2026-09-29，`backend-engineer`）

### 背景：S1-17 把公開表單送出改成同源代理，S1-10 的「只信任 Caddy 一個 IP」假設因此失效

`apps/web` 這一輪（S1-17）把 10 表單中心的 `POST /api/v1/{club}/forms/{formCode}/submissions`
改成由 Nuxt 伺服器端路由（`server/api/backend/[...path].ts`）同源代理轉發，不再是瀏覽器直接呼叫
`API_DOMAIN`。理由是避免額外處理 CORS／`runtimeConfig`（見該檔案檔頭），效果是 SSR 容器
（`nuxt-tcrfc`／`nuxt-bw`）本來就有的內部呼叫路徑（`NUXT_API_INTERNAL_BASE=http://api:8080`）
現在也承載了這個 POST。

問題：S1-10 的依 IP 分區限流只信任 `TRUSTED_PROXY_IP`（單數）這一個固定 IP——`docker-compose.yml`
只給 `proxy`（Caddy）配了固定 IP。這條新路徑下，`api` 容器看到的 TCP 連線來源變成
`nuxt-tcrfc`／`nuxt-bw` 容器自己的 Docker 內部 IP，不在信任清單內，`ForwardedHeadersMiddleware`
不會採信它們轉來的 `X-Forwarded-For`，`ClientIpResolver.Resolve` 因此拿到的是「nuxt 容器的 IP」，
**全站訪客共用同一把鑰匙**——退回 S1-10 修正前的狀況，依 IP 分區限流形同虛設。

`docs/14-invariants.md` 當時記的踩雷點甚至明文寫「前台的公開表單送出一律由瀏覽器直接呼叫公開
API 網域，不得經由 Nuxt 伺服器端代轉」——這條規則被 S1-17 的既成事實推翻了，本輪的工作就是把
`api` 端補上讓這個新架構安全成立的信任機制，並回頭修正這條已經過期的不變量（見
`docs/14-invariants.md` 對應段落，已標註「舊規則已撤銷」）。

### 設計：從「信任一個 IP」改成「信任一組固定 IP」，不是「信任整個網段」

`docker-compose.yml` 比照 `proxy` 既有做法，給 `nuxt-tcrfc`（`172.28.238.3`）與 `nuxt-bw`
（`172.28.238.4`）也各配一個固定 IP；`api` 服務的環境變數改名為 **`TRUSTED_PROXY_IPS`**（複數，
逗號分隔），值是 `172.28.238.2,172.28.238.3,172.28.238.4`。

**為什麼是「明確列舉三個 IP」而不是「信任整個 `172.28.238.0/24` 網段」**：網段裡還有
`admin-web`／`admin-charity`／`nuxt-charity`／`redis` 等其他容器，信任整個網段等於讓這些容器
（或任何拿到該網段某個 IP 的東西）也能偽造標頭騙過限流，這是 S1-10 當初就定下、本輪延續的原則。

**刻意不包含 `nuxt-charity`**：慈善捐款平台是主站規劃書之外的獨立產品，10 表單中心是主站
§3.10 的機制，慈善站台目前沒有已知的等價代理路徑會打 `form-submission` 這個限流政策。若日後
慈善前台也新增類似的伺服器端代理轉發到本 API 的公開寫入端點，要重新評估補上固定 IP 與清單。

**`ForwardLimit` 維持 `1`，沒有跟著調高**：三個受信任 IP 是三條**互斥**的直連路徑（Caddy 直連、
經 `nuxt-tcrfc` 代理、經 `nuxt-bw` 代理），同一個請求只會經其中一條抵達 `api`；而且
`nuxt-tcrfc`／`nuxt-bw` 的代理路由只轉發 Caddy 已經解析好的**單一值** `X-Forwarded-For`，不會
在自己這層再往後面疊加一段。因此不論走哪一條路徑，`api` 收到的 `X-Forwarded-For` 都只有一層要
剝，`ForwardLimit = 1` 對所有路徑都成立，不是「三層代理要設 3」的誤解。

**改名為複數，不做向後相容**：本專案還沒有對外部署過依賴 `TRUSTED_PROXY_IP`（單數）這個鍵名的
正式環境（`.env.example`／`docker-compose.yml` 都是這次一起改），改名即改乾淨，不留兩套鍵名
互相打架。

**副作用修的一個潛在缺口**：`IsEnabled` 原本的判準是「字串非空白」，這次改成「至少解析出一個
合法 IP」。原本的判準有個隱藏風險：如果 `TRUSTED_PROXY_IPS` 設定值是打錯字的非空字串，
`IsEnabled` 仍會回 `true`、`Program.cs` 仍會掛上 `app.UseForwardedHeaders()`，但
`Configure` 內部逐一 `IPAddress.TryParse` 全部失敗、`KnownProxies` 最終是空集合——**這正好撞上
「空的 KnownProxies＝信任所有來源」的框架陷阱**，形同開了一個因設定打錯字而產生的後門。現在的
判準保證「有掛中介軟體」與「`KnownProxies` 至少有一個合法項目」同時成立或同時不成立。完整設計
理由見 `apps/api/Security/TrustedProxyConfiguration.cs` 檔頭與各方法上的 XML 文件註解。

### 改了哪些檔案

- `apps/api/Security/TrustedProxyConfiguration.cs`——`ConfigKey` 改名 `TRUSTED_PROXY_IPS`；
  `Configure`／`IsEnabled`／`ResolveEffectiveClientIp` 改吃逗號／分號分隔的 IP 清單（新增私有
  `ParseIps`，解析失敗的項目略過、不丟例外）；`ForwardLimit` 維持 `1`，補上完整理由。
- `apps/api/Program.cs`——變數改名 `trustedProxyIps`，更新周邊註解反映兩條路徑（Caddy 直連／
  經 Nuxt 代理）。
- `docker-compose.yml`——`nuxt-tcrfc`／`nuxt-bw` 各自加上 `networks.internal.ipv4_address`
  （`.3`／`.4`）；`api` 服務的 `TRUSTED_PROXY_IP` 改名 `TRUSTED_PROXY_IPS`，值改成三個 IP。
- `apps/api/Tcrfc.Api.Tests/TrustedProxyConfigurationTests.cs`——既有 3 項測試改用新的常數命名，
  新增 8 項（經 `nuxt-tcrfc`／`nuxt-bw` 代理各自解析正確、同一訪客走不同路徑解析結果一致、
  不受信任容器偽造標頭不被採信、訪客直接偽造標頭不被採信、設定值打錯字視同未設定、清單混一個
  錯誤項目其餘仍生效、逗號分號混用並自動 trim），共 11 項。
- `docs/17-deployment.md` §2——新增「代理信任鏈（`api` 端如何認得訪客真實 IP）」小節。
- `docs/14-invariants.md`——修正「依訪客 IP 計算的濫用防護」這條踩雷點，標註舊規則
  （「不得經由 Nuxt 伺服器端代轉」）已被 S1-17 與本輪取代，寫明新規則與仍然成立的部分。
- `deploy/README.md`——「Cloudflare 在前面，對 Caddy 的 TLS 有什麼影響」段落同步更新為多重
  受信任來源的敘述。
- 本檔（`apps/api/README.md`）：S1-10 歷史段落補一則指向本節的過期提醒；env 變數對照表的
  `TRUSTED_PROXY_IP` 列改名並更新說明。
- 🔴🔴 **`deploy/Caddyfile`／`deploy/Caddyfile.prelaunch`／`deploy/Caddyfile.dev`**——主 session
  在本輪過程中發現一個獨立於「`api` 端信任設定」之外的漏洞並要求一併處理：Caddy 對
  `X-Forwarded-For` 的預設行為是「把連線本身看到的直接對端（正式環境永遠是 Cloudflare 邊緣節點
  IP）原封加到既有標頭最後面」，不是加已解析過的可信值；而 Cloudflare 對訪客自己送的
  `X-Forwarded-For` 是保留並附加，不是取代。三份 Caddyfile 在 `reverse_proxy nuxt-tcrfc`／
  `nuxt-bw`／`nuxt-charity` 都加上 `header_up X-Real-IP {client_ip}`，在 `reverse_proxy api`
  （`API_DOMAIN` 區塊）加上 `header_up X-Forwarded-For {client_ip}`（皆為取代語意）。**本輪已用
  本機 `caddy:2.9.1-alpine`＋Python 回聲伺服器實測驗證**（不是只憑官方文件推論），見下方「測試」
  段與 `docs/17-deployment.md` §2「Caddy 這一側也要正確設定」的完整實測記錄。`api` 端既有的
  `TrustedProxyConfiguration`／`ForwardLimit = 1` 設計不需要因此變動——修正後 Nuxt 收到的
  `X-Real-IP` 與 `api` 直接收到的 `X-Forwarded-For` 都保證是單一乾淨值，完全符合原本的假設。

### 測試

`Tcrfc.Api.Tests/TrustedProxyConfigurationTests.cs` 從 3 項擴充為 **11 項**，全部用
`TrustedProxyConfiguration.ResolveEffectiveClientIp` 這個純函式介面驗證（理由同 S1-10：
`WebApplicationFactory` 的 `TestServer` 底下 `RemoteIpAddress` 永遠是 `null`，無法在那個環境
驗證「受信任代理」這條路徑）。新增的 8 項涵蓋：經 `nuxt-tcrfc`／經 `nuxt-bw` 代理轉來的請求
各自正確解析出訪客真實 IP；同一個訪客不論經 Caddy 直連或經 Nuxt 代理，解析出的真實 IP 一致
（限流分區鍵不會因路徑不同而分裂成兩個人）；刻意不信任的 `nuxt-charity`／任意訪客直接偽造
`X-Forwarded-For` 均不被採信；設定值打錯字時視同未設定（關掉上面提到的潛在缺口）；清單裡混一個
錯誤項目時其餘合法 IP 仍生效；逗號與分號混用、兩側留白會被正確 trim。

```bash
cd apps/api
dotnet build                                                          # 成功，0 警告 0 錯誤
dotnet test Tcrfc.Api.Tests --filter "FullyQualifiedName~TrustedProxyConfigurationTests"
                                                                       # 11/11 通過
dotnet test Tcrfc.Api.Tests                                           # 528/528 全套通過
docker compose -f docker-compose.yml config                           # 語法驗證通過，三個
                                                                       # ipv4_address 皆正確輸出
docker compose -f docker-compose.yml -f docker-compose.dev.yml config # 語法驗證通過，dev override
                                                                       # 正確繼承固定 IP 與
                                                                       # TRUSTED_PROXY_IPS

# 三份 Caddyfile 語法驗證（caddy validate，帶假網域值）
docker run --rm -e TCRFC_DOMAIN=... -e ... \
  -v "$(pwd)/deploy/Caddyfile:/etc/caddy/Caddyfile:ro" \
  caddy:2.9.1-alpine caddy validate --config /etc/caddy/Caddyfile   # Valid configuration
#（Caddyfile.prelaunch 同樣通過；Caddyfile.dev 額外驗證過，見下方）

# Caddy header_up 修正的本機實測（caddy:2.9.1-alpine ＋ 印出全部收到標頭的 Python http.server）：
# 沒有 header_up 時，帶 X-Forwarded-For: 6.6.6.6 直接呼叫，下游收到
#   X-Forwarded-For: 6.6.6.6, <Caddy 直接對端 IP>（取最右邊會拿到對端 IP，不是訪客 IP）
# 加上 header_up X-Forwarded-For {client_ip} 後，即使同時偽造 X-Forwarded-For 與試圖偽造
# X-Real-IP，下游收到的都是乾淨單一值，且等於模擬的 CF-Connecting-IP（不是偽造值）；
# header_up X-Real-IP {client_ip} 對 nuxt-* 路徑驗證同樣結果。完整重現步驟見
# docs/17-deployment.md §2「Caddy 這一側也要正確設定」。
```

全套測試對照：本輪開工前既有 520 項全過，本輪新增 8 項，528/528 全部通過，沒有既有測試因這次
改動回歸失敗。

### 部署時要改的設定

- **VM 上的 `.env`／`docker-compose.yml` 若已手動調整過 `TRUSTED_PROXY_IP`（單數）**，下次部署
  這份 `docker-compose.yml` 時環境變數鍵名會自動變成 `TRUSTED_PROXY_IPS`（本檔已內建三個正確的
  IP 值，不需要另外在 VM 的 `.env`／secrets 檔案手動加這個變數——它是直接寫在 `docker-compose.yml`
  裡的固定值，不是從 `.env` 讀入的）。**唯一要注意的是重建 `internal` 網路後三個容器要拿到跟
  compose 檔一致的固定 IP**——`docker compose up -d` 重建網路時會依 compose 設定自動配置，
  不需要手動介入；只有在懷疑 Docker 網路狀態不一致時，`docker network inspect tcrfc_internal`
  可以核對三個容器實際拿到的 IP 是否與 `172.28.238.2`／`.3`／`.4` 一致。
- **沒有新的機密要加**——這三個 IP 是固定的內部網路位址，不是機密，維持寫在 `docker-compose.yml`
  裡（不需要進 `/opt/tcrfc/secrets/*.env`）。

### 防機器人現況盤點（任務指示第 4 項，順便盤點，本輪未實作）

主站規劃書 §3.10「共通機制」要求 10 表單中心的公開送出端點要有防機器人機制（原文舉例
reCAPTCHA／Turnstile）。盤點結果——**全系統目前沒有串接任何 CAPTCHA／Turnstile 服務**：

- **後端（`apps/api`）**：`FormsRepository` 檔頭與 `PublicFormDto.CaptchaEnabled` 已經誠實記錄
  這個缺口（S1-10 就寫了）——`CaptchaEnabled` 只是一個資料庫旗標，沒有對應的伺服器端 token 驗證
  邏輯；`Program.cs`／`FormsEndpoints` 只有兩層不需要外部服務的防線：① 依真實訪客 IP 分區的固定
  視窗限流（本輪修正的重點，5 分鐘 20 次）；② `SubmitFormRequest.Website` 誘捕欄位（honeypot）。
  沒有找到任何 Turnstile／reCAPTCHA 的 SDK 參照、siteverify 呼叫、或站台金鑰設定讀取。
- **前端（`apps/web`）有一個容易誤判的細節**：7 個表單頁面（`app/pages/zh/join/{general,academy,
  international-player,camp-registration,partnership,player,media}/index.vue`）**都已經放了一個
  Turnstile 佔位標記**：`<div class="cf-turnstile" data-sitekey="" role="group" ...>`，緊跟在
  `useFormSubmit(...)`／`<HoneypotField ...>` 旁邊。**但這個標記是死的**——
  ① `data-sitekey=""` 是空字串，② 全站沒有任何地方載入 Turnstile 官方腳本
  （`<script src="https://challenges.cloudflare.com/turnstile/v0/api.js">`，`grep -rn` 確認
  不存在），③ 沒有 Turnstile token 被讀取或附加到 `SubmitFormRequest`。少了腳本，這個 `div`
  在瀏覽器裡就是一個空的 `<div>`，不會渲染出任何驗證元件，訪客送出表單時完全不會被要求做任何
  驗證。這看起來像是更早的版面／mockup 階段留下的預留位置，跟後來（S1-9／S1-10）才長出來的
  `useFormSubmit`／`HoneypotField` 送出邏輯是各自獨立寫的，兩者目前沒有真正串在一起。
- **結論**：規劃書要求的防機器人機制目前**完全未生效**，現況只有「依真實訪客 IP 限流」＋
  「誘捕欄位」兩層土法煉鋼防線真正在運作（本輪修正後，限流才真的依訪客而非依路徑分區，見上）；
  前端那個 Turnstile 佔位標記**目前只是視覺殘留，沒有任何防護效果**，容易被誤讀成「已經有
  Turnstile」而略過這個缺口。
  **依任務指示，本輪不實作 Turnstile**（需要在 Cloudflare 建立資源，且要決定放哪個環境變數、
  誰持有站台金鑰，是需要使用者決定的執行層基礎建設決定，不是本輪能自行判斷的範圍）。若日後要
  接：後端需新增一支呼叫 Cloudflare `siteverify` API 的服務（比照 `IImageStorageService` 這種
  「有憑證才啟用」的既有模式）驗證前端送來的 token；前端**不需要從零加 widget**——只需要載入
  官方腳本、把 7 個頁面既有 `cf-turnstile` 佔位標記的 `data-sitekey` 填入真實值，並把驗證後拿到
  的 token 一併送進 `SubmitFormRequest`（目前這個 DTO 也還沒有承接 token 的欄位，要一併新增）。

### 規劃書沒寫清楚、本輪自行判斷的地方

1. **`nuxt-charity` 刻意不列入信任清單**——慈善站台目前沒有已知的等價代理路徑，任務指示也明確
   排除 `apps/web` 以外的前台，見上方設計段。
2. **舊鍵名 `TRUSTED_PROXY_IP` 不做向後相容，直接改名**——判斷理由見上方設計段「改名為複數，
   不做向後相容」；這是「規劃書沒寫、執行層自行決定」的具體選擇。
3. **`docs/14-invariants.md` 的舊規則用「標註已撤銷＋說明新規則」處理，不是直接刪除整段**——
   保留「這裡曾經有一條更嚴格的規則、後來被什麼取代」的軌跡，避免下一個人在版本歷史裡看到舊
   commit 卻找不到解釋；這與 `docs/18-work-errors.md` E-10「代號順移後舊代號語意改變」記過的
   教訓同一類——概念變更要能被回溯理解，不是憑空消失。

## S1-18c：補齊公開寫入端點的限流缺口（2026-09-29，`backend-engineer`）

### 背景：`RequireRateLimiting` 原本只掛在表單送出一個端點上

S1-10／S1-17 只把 `Program.cs` 的依 IP 分區限流掛到 `POST .../forms/{formCode}/submissions`
（10 表單中心公開送出）一個端點上。盤點後發現同樣「不需要登入、任何人都能呼叫」的其餘公開
非 GET 端點完全沒有限流，任何人可以無限次呼叫，灌票（FAQ 👍／👎）、洗瀏覽數，或塞爆
`faq_search_misses`／`registrations` 等表。

### 盤點結果：`apps/api` 全部不需要登入的非 GET 端點

判斷「需不需要登入」的依據是**檔案所在資料夾**：`Features/AdminXxx/` 底下的端點一律經過
`IAdminClubAuthorizer`／`IAdminSystemAuthorizer` 型別層強制授權（見 `ArchitectureTests.cs`
第一支測試），`Features/Xxx/`（非 Admin）底下的端點依既有架構慣例全部不需要登入（每個檔案
開頭的 XML 文件註解都明文寫「全部不需要登入」）。

| 路徑 | 用途 | 本輪之前是否限流 | 本輪處置 |
|---|---|---|---|
| `POST /api/v1/{club}/forms/{formCode}/submissions` | 10 表單中心公開送出 | ✅ 已有（`form-submission`，20／5 分鐘） | 不動（任務指示明文不改既有數值） |
| `POST /api/v1/{club}/faqs/{slug}/views` | FAQ 瀏覽數＋1 | ❌ 無 | 新增，掛 `public-light-interaction` |
| `POST /api/v1/{club}/faqs/{slug}/feedback` | FAQ 👍／👎 回饋 | ❌ 無 | 新增，掛 `public-light-interaction` |
| `POST /api/v1/{club}/faqs/search-misses` | FAQ 零結果搜尋關鍵字記錄 | ❌ 無 | 新增，掛 `public-light-interaction` |
| `POST /api/v1/{club}/news/{slug}/views` | 新聞瀏覽數＋1 | ❌ 無 | 新增，掛 `public-light-interaction` |
| `POST /api/v1/{club}/programs/sessions/{sessionId}/registrations` | 05 課程與活動公開報名送出 | ❌ 無 | 新增，掛 `public-submission` |
| `POST /api/v1/admin/auth/login` | 後台登入 | ❌ 無 IP 限流（有帳號鎖定，見下） | **本輪未動**，見下方說明 |
| `POST /api/v1/admin/auth/refresh` | 後台更新權杖 | ❌ 無 IP 限流 | **本輪未動**，見下方說明 |
| `POST /api/v1/admin/auth/logout` | 後台登出 | ❌ 無 IP 限流 | **本輪未動**，風險低（純清空 Cookie，無業務副作用可濫用） |

**`Features/AdminAuth/AdminAuthEndpoints.cs` 的 `/login`／`/refresh`／`/logout` 三個端點雖然
路由在 `Features/AdminXxx/` 之下，但呼叫當下確實不需要先登入**（`/login` 本來就是登入本身；
`/refresh`／`/logout` 只檢查 Cookie，不檢查 JWT）——技術上符合任務指示「所有不需登入的非 GET
端點」，因此列在這裡盤點，但**本輪刻意不動**：① `/login` 已有帳號層級的鎖定機制
（`LoginOutcome.Locked`，見 `AdminAuthService`），跟本輪要解決的「公開內容端點被灌爆資料庫」
是不同風險模型（帳號枚舉／暴力破解 vs. 業務資料表被灌爆），加不加 IP 限流、額度多少屬於另一個
判斷、規劇書與 docs 都沒有明文要求，不屬於本輪任務指示點名的範圍（任務指示的例子全部是
FAQ／瀏覽數這類公開內容端點）；② 硬把它們也套進 `public-light-interaction`／`public-submission`
兩個政策不合理——登入端點的合理額度、要不要跟 `/login` 帳號鎖定分開計算，是需要另外設計的
獨立題目，勉強共用會讓兩邊的數值互相牽制、日後很難個別調整。**這是刻意留下的缺口，不是漏看**，
若日後要補，建議另開一個政策名稱、額度也重新評估（帳號枚舉的合理防禦額度通常比內容端點更嚴格）。
`Features/Calendar`／`Features/Clubs`／`Features/Home`／`Features/Pages`／`Features/Players`／
`Features/Schedule`／`Features/Seo`／`Features/SiteFacts`／`Features/Staff`／`Features/Teams`
目前**沒有任何非 GET 端點**，盤點時確認過（`grep -n "app.Map\(Post\|Put\|Delete\|Patch\)"`
逐檔掃過，結果是空的），不需要處置。

### 兩個新政策：為什麼跟表單送出分開、數值怎麼來的

新增 `Common/PublicRateLimitPolicies.cs` 集中兩個政策名稱與額度常數（`Program.cs` 註冊、
各端點掛 `.RequireRateLimiting(...)`、`Tcrfc.Api.Tests` 驗證三邊共用同一組常數，不重複寫魔術
數字）：

- **`public-light-interaction`**（60 次／1 分鐘）：FAQ 瀏覽數／回饋／零結果搜尋、新聞瀏覽數——
  都是「使用者正常瀏覽時就可能連續觸發好幾次」的輕量互動（連續點開多篇 FAQ、快速翻頁看多篇
  新聞），額度刻意比表單寬鬆。
- **`public-submission`**（20 次／5 分鐘）：目前只有 05 課程與活動的公開報名送出。跟表單送出
  同一風險等級（都是「建立一筆真正業務紀錄」），數值刻意抄表單那組，但**用獨立政策名稱、獨立
  額度計數**，不共用表單的計數——避免兩個功能互搶額度，也讓兩者未來各自調整數值互不牽連。

兩組數字都跟既有 `form-submission` 政策一樣，**是「規劃書或 docs 沒寫、執行層自行決定」的
具體選擇，沒有規格依據，屬最小可行防護**（比照 `Program.cs` 既有政策註冊時的既有慣例，
`Common/CsvUtils.cs` 檔頭「沒定義就採最小可行」的既有慣例）。

### 架構測試：忘記掛限流會讓 `dotnet test` 直接失敗

`Tcrfc.Api.Tests/ArchitectureTests.cs` 新增第三支 Roslyn 語意掃描測試
`公開端點的非GET寫入呼叫都必須掛限流政策`：掃 `Features/*`（排除 `Features/AdminXxx`，跟
「公開DTO的物件鍵欄位」那支既有測試共用同一個 `IsUnderAdminFeature` 判斷），找每一個
`MapPost`／`MapPut`／`MapDelete`／`MapPatch` 呼叫，沿著它的 fluent chain
（`.WithName(...).Produces(...)` 那一長串）往外走，要求路上一定要出現 `.RequireRateLimiting(...)`，
否則記一筆違規。往後任何人在 `Features/*`（非 Admin）新增公開非 GET 端點卻忘記掛限流，
`dotnet test` 會直接失敗，不必等到真的被灌爆才發現——這是 `docs/14-invariants.md`「公開寫入
端點一律限流」這條新不變量的自動化防呆。

只驗證「有沒有掛」，不驗證掛的是哪個政策、額度是否合理——額度合理性是下面
`PublicRateLimitPoliciesTests` 的職責。

### 測試

- **`Tcrfc.Api.Tests/PublicRateLimitPoliciesTests.cs`**（新增，2 項，不需要資料庫、不需要
  `WebApplicationFactory`）：直接對 `PublicRateLimitPolicies` 實際設定的數值建一個
  `PartitionedRateLimiter<string>`，驗證額度用盡後下一次請求被拒絕、且不同分區鍵（模擬不同
  訪客 IP）互不影響。刻意不透過 HTTP 打——`TestServer` 底下 `RemoteIpAddress` 恆為 `null`，
  所有透過真正 HTTP 請求的呼叫一律解析成同一個「unknown」分區鍵，沒辦法在那個環境下驗證
  「不同 IP 分區互相獨立」，改用兩個不同的字串分區鍵直接餵限流器本身繞開這個環境限制。
- **`Tcrfc.Api.Tests/PublicWriteEndpointRateLimitingTests.cs`**（新增，2 項，需要
  `WebApplicationFactory`＋測試資料庫）：走真正 HTTP 管線驗證 `Program.cs` 真的把兩個政策
  接到對應路由上（超過額度收到 429），跟上一份檔案分工不同——那份測政策本身的行為，這份測
  「有沒有真的接上」。用共用的 `ApiCollection`／`ApiFixture`：這個 collection 目前只有純讀取
  端點測試在用，不會互相污染分區計數；本檔每個政策只寫一支會把額度用盡的測試方法，避免同一個
  「unknown」分區被多支測試方法的執行順序互相干擾。
- **既有測試回歸**：`PublicFaqsTests`／`NewsPublicFilterAndViewCountTests`／
  `AdminProgramsSessionsRegistrationsTests`（公開報名送出）皆與
  `AdminFormsEnquiriesTests`／`AdminFaqsAndCategoriesTests` 共用 `AdminWriteCollection`，
  對 `public-light-interaction`／`public-submission` 兩個政策實際呼叫次數分別約 13 次與
  6 次，遠低於 60／20 的額度，跑過 82 項全部通過，沒有因為新增限流而誤傷。
- **實際執行**（2026-09-29，本機 `tcrfc_club_test`）：`dotnet test Tcrfc.Api.Tests` 全數
  533 項通過（0 失敗、0 略過），涵蓋上述新增 4 項＋既有 529 項回歸。

### 同一 IP 對同一題可以重複投票 FAQ 回饋，本輪評估後刻意不做去重

`faqs.helpful_count`／`unhelpful_count` 是彙總計數欄位，資料庫層沒有任何「這個訪客對這一題
投過票」的紀錄（沒有 IP／裝置指紋／Cookie 之類的去重鍵），主站規劃書也沒有提到 FAQ 回饋需要
防止重複投票（§3.12 只寫「回饋數據回寫後台供優化」，`docs/12b`／`docs/12` 都沒有提到去重）。
本輪限流只能把「短時間內灌爆」的速度壓下來（60 次／1 分鐘），**不能防止同一個訪客用同一個 IP、
分散在多個時間視窗內反覆對同一題投票**——這件事技術上可行，但**規劃書沒寫、任務指示也明確
要求「規劃書沒寫的行為不要發明」**，因此本輪不新增任何去重機制（例如按 IP＋FAQ id 記一張
去重表、或改用「已投過票」Cookie）。如果要補，需要先回頭跟規劃書要求方確認「回饋數據」是否
真的需要防重複灌票這個特性，再決定去重鍵要用什麼（IP 太粗，同一個辦公室／校園網路會互相
擋到；Cookie 又擋不住清 Cookie 的人），這是需要另外討論、不是本輪能自行判斷的範圍。

### 沒有新的機密要加

兩個新政策名稱與額度都是寫死在程式碼裡的常數，不是機密，不需要新增任何環境變數或
`/opt/tcrfc/secrets/*.env` 項目。

## S1-18d：補上 `/login`／`/refresh` 的依 IP 限流，修正帳號枚舉時序側錄（2026-09-29，`backend-engineer`）

S1-18c 交付時刻意把 `Features/AdminAuth` 的 `/login`／`/refresh`／`/logout` 排除在外（見上一節
「盤點結果」表格下方的說明），理由是風險模型不同、需要另外設計。這輪把這個刻意留下的缺口補上，
同時修正一個盤點時發現的既有漏洞：帳號枚舉的時序側錄。

> 🔴 **本節記錄的是第三版**。第一版把 `admin-login`／`admin-refresh` 的額度直接寫死成
> 「蓋過既有測試呼叫次數」的數字（40／20），收到回饋「不要讓測試用量決定正式環境的安全額度」
> 後，同日改成「額度可設定值＋正式環境嚴格預設值（5／30）＋測試環境用
> `Environment.SetEnvironmentVariable` 覆寫」的設計（第二版）。第二版又收到回饋：
> `Environment.SetEnvironmentVariable` 寫的是行程全域狀態，跟 xUnit 平行執行不同 collection
> 可能互相踩到彼此的覆寫值，已改成第三版——測試覆寫改用
> `IWebHostBuilder.ConfigureAppConfiguration` 加入只屬於單一測試主機的 in-memory 設定來源，
> `Program.cs` 讀取額度的時機也一併調整（改讀 DI 容器裡的 `IConfiguration`，見下方「處置」
> 一節與「測試環境的覆寫機制」小節）。本檔只保留改完的最終狀態，不重複列出被取代的前兩版數字。

### 問題：三個既有缺口

1. **同一 IP 對多個帳號輪流猜密碼（password spraying）不受帳號鎖定限制**——`AdminAuthService`
   既有的鎖定機制（連續 5 次失敗鎖 15 分鐘）是**帳號層級**的，攻擊者只要每個帳號只試 4 次、
   換下一個帳號，永遠不會觸發任何一個帳號的鎖定，也完全不受任何限流限制（`/login` 之前完全
   沒有依 IP 的濫用防護）。
2. **攻擊者可以故意打錯密碼把管理員帳號鎖住（阻斷服務）**——這個風險**沒有被本輪完全解決**，
   見下方「殘留風險：鎖定機制本身仍可被用來做 DoS」。
3. **帳號枚舉時序側錄**——`AdminAuthService.LoginAsync` 原本「帳號不存在」路徑直接回傳，不會
   跑 Argon2id 密碼雜湊比對；「帳號存在但密碼錯誤」路徑會跑一次 Argon2id（依 `PasswordHasher`
   的參數，耗時約 100–200ms）。即使兩條路徑的**狀態碼與回應內容**完全一致（既有測試
   `登入_密碼錯誤_回401且訊息不洩露帳號是否存在` 已經驗證過這件事），**耗時**仍然天差地遠，
   攻擊者可以單純量測回應時間來判斷任一組帳號是否存在，繞過訊息層級的防護。

### 處置：兩個新的依 IP 限流政策＋一次時序修正

**`Features/AdminAuth/AdminAuthEndpoints.cs`** 新增兩個政策名稱常數（`Program.cs` 用同一套
「依 `ClientIpResolver.Resolve` 分區的 `FixedWindowRateLimiter`」寫法註冊，跟
`PublicRateLimitPolicies` 的既有寫法一致）：

| 政策 | 端點 | 正式環境預設額度 | 為什麼 |
|---|---|---|---|
| `admin-login` | `POST /api/v1/admin/auth/login` | 每 IP 每分鐘 **5** 次 | 密碼噴灑／整體暴力破解速率的第一層防線，跟帳號層級鎖定互補（見下方風險說明），不是取代。業界常見登入端點依 IP 限流多落在每分鐘個位數量級 |
| `admin-refresh` | `POST /api/v1/admin/auth/refresh` | 每 IP 每分鐘 **30** 次 | 更新權杖是 256 bits 亂數＋伺服器端 SHA-256 雜湊，猜測在計算上不可行，這裡防的是「權杖外流後被重放濫用的速率」與一般性灌流量，風險本來就比登入低，額度可以更寬鬆 |
| （無新政策）`POST /api/v1/admin/auth/logout` | 只清 Cookie＋可能撤銷單一 `admin_refresh_tokens` 列 | 評估後判斷風險低：沒有能被濫用來鎖住別人帳號或灌爆資料表的業務副作用，刻意不加 |

**🔴 額度是可設定值，不是寫死的常數**（2026-09-29 當天二次修正，回應「不要讓測試用量決定
正式環境的安全額度」的回饋）。這輪任務原本的第一版把兩個政策的 `PermitLimit` 直接寫死成
「蓋過既有測試在共用 `AdminWriteApiFixture`（32 個測試檔共用同一個 `WebApplicationFactory`
行程、同一個「unknown」IP 分區）裡實際呼叫次數」的數字（40／20 次／5 分鐘）——**這是本末
倒置**：讓測試環境的用量決定了正式環境的安全額度，而不是反過來讓正式環境的安全需求決定額度、
測試環境另外想辦法適應。已改為：

- `AdminAuthRateLimitOptions.cs` 定義兩個環境變數（`ADMIN_LOGIN_RATE_LIMIT_PERMIT_LIMIT`／
  `ADMIN_REFRESH_RATE_LIMIT_PERMIT_LIMIT`，皆選填，沿用本專案既有的「扁平鍵名、直接讀
  `IConfiguration` 索引子」設定慣例，不是巢狀 `IOptions<T>`），**未設定或設定值不是正整數時，
  一律退回上表的嚴格預設值**（5／30）——寧可設定壞掉時退回更嚴格的行為，也不要退回「不限流」。
- 視窗大小（1 分鐘）刻意固定、不開放設定：只讓「額度」可調，避免多一個能在不動額度數字的情況
  下實質放寬限制的旋鈕（拉長視窗＝變相放寬，卻不會被「額度變大了」這種明顯訊號提醒到）。
- 測試環境的覆寫機制見下一節「測試環境的覆寫機制：從環境變數改成 `ConfigureAppConfiguration`」。

**多層防禦，不是單一防線**：即使正式環境的額度是嚴格數字，`AdminAuthService` 既有的帳號鎖定
機制完全獨立運作、不受這裡的數字影響。單一帳號被暴力破解的防線主要仍然靠帳號鎖定；這裡的
IP 限流補的是「同一來源對多個不同帳號輪流嘗試（密碼噴灑）」與「大量自動化嘗試的整體速率」，
兩層防線互補。

### 測試環境的覆寫機制：從環境變數改成 `ConfigureAppConfiguration`（第三次修正）

**問題**：第二版用 `Environment.SetEnvironmentVariable` 覆寫測試環境的額度值——這是**行程
全域**狀態，同一個 `dotnet test` 行程裡的每一個 `WebApplicationFactory` 讀的、寫的都是同一份
系統環境變數。這組覆寫剛好需要**兩種互斥的值同時存在**：一般用途 fixture 要寬鬆值
（`100000`），驗證「額度用盡回 429」的 `AdminAuthRateLimitTestApiFixture` 要一個很小的值
（`3`）。如果 xUnit 平行執行不同 `[Collection]`，兩種 fixture 的 `InitializeAsync` 有可能
交錯執行，後寫入的值覆蓋先寫入的值，造成間歇性失敗——回饋內容正是這個風險。

**盤點結果**：本測試組件（`Tcrfc.Api.Tests/AssemblyInfo.cs`）其實已經有
`[assembly: CollectionBehavior(DisableTestParallelization = true)]`，理由寫在同一個檔案的
註解裡：「兩個 fixture（`ApiFixture`／`RedisUnavailableApiFixture`）之間用行程環境變數傳遞
設定給 `WebApplicationFactory`——並行執行會讓不同 collection 的環境變數互相踩到彼此」——
這代表**在目前的設定下**，`REDIS_HOST`／`CLUB_SQL_CONNECTION_STRING` 等既有環境變數覆寫、
以及本輪新增的額度覆寫，實際上都不會真的並行執行，理論上的競態目前不會發生。但這是**仰賴一個
組件層級的全域開關**，不是這組設定本身該有的隔離範圍——本輪新增的額度覆寫刻意不再依賴這個
開關，改用不受影響的機制。

**修法**：`Tcrfc.Api.Tests/Fixtures/TestRateLimitOverrides.cs` 改用
`IWebHostBuilder.ConfigureAppConfiguration` 加入 in-memory 設定來源，每個
`WebApplicationFactory` 的覆寫值只存在於**該實例自己建出來的 `IConfiguration`**（透過該主機的
DI 容器解析），不是行程全域狀態——即使兩個 fixture 真的並行初始化，也不會互相干擾。6 個一般
用途 fixture（`ApiFixture`／`AdminWriteApiFixture`／`AdminWriteAzuriteEnabledApiFixture`／
`RedisUnavailableApiFixture`／`AdminWriteRedisEnabledApiFixture`／`RedisEnabledApiFixture`）
與新增的 `AdminAuthRateLimitTestApiFixture` 都改為覆寫 `WebApplicationFactory.ConfigureWebHost`
呼叫 `TestRateLimitOverrides.ApplyLooseAdminAuthOverrides(builder)`／
`ApplyAdminAuthOverrides(builder, "3")`。

**`Program.cs` 讀取額度的時機也一併調整，並且已經實測驗證，不是憑印象判斷**：`Program.cs`
對這兩個政策的額度解析是**惰性**的——只有在真的有 HTTP 請求打進 `admin-login`／
`admin-refresh`、且是該分區鍵第一次出現時，`FixedWindowRateLimiterOptions` 的 `PermitLimit`
才會被讀取一次，這個時間點遠晚於整個 `IHost` 建置完成、開始服務請求之後（跟 `REDIS_HOST` 在
`Program.cs` 頂層、`builder.Build()` **之前**就同步讀取、決定 DI 要注入哪個 `IQueryCache`
實作，時機完全不同——那個情境下 `ConfigureAppConfiguration` 確實太晚生效，這裡不是）。額度
解析已改讀 `httpContext.RequestServices.GetRequiredService<IConfiguration>()`（DI 容器裡
`Build()` 完成後的那一份，保證含有 `ConfigureAppConfiguration` 加入的所有設定來源），不再
讀取 Program.cs 頂層 `builder.Configuration` 的閉包。

用一個獨立於本專案之外的最小重現專案實測驗證過（不在 `Tcrfc.Api.Tests` 本身跑，因為那裡每個
fixture 都需要真的資料庫連線，沒辦法快速反覆驗證這個框架行為，且該重現過程不涉及任何本專案的
資料庫或密碼）：① 完全比照 `Program.cs` 頂層、`builder.Build()` 之前的同步讀取，確認讀不到
`ConfigureAppConfiguration` 的覆寫（3 個測試中 1 個驗證，結果與 `REDIS_HOST` 既有情境一致）；
② 惰性讀取（閉包捕捉 `builder.Configuration`，實際呼叫延後到請求時）與③ 從
`httpContext.RequestServices` 解析 `IConfiguration`，兩者都確認讀得到覆寫（3 個測試全數通過）
——`Program.cs` 採用③，是更明確、不依賴「`ConfigurationManager` 物件原地可變」這種容易被
忽略之細節的寫法。

**時序修正**（`AdminAuthService.cs`）：新增 `DummyPasswordHashForTimingSafety`（`static readonly`
欄位，型別第一次被用到時算一次，不是每次請求重算）——一組固定的、跟真實帳號無關的完整
Argon2id 編碼雜湊值。「帳號不存在」路徑現在會對這組假雜湊值跑一次 `PasswordHasher.Verify`
（結果必然是 `false`，刻意不使用，純粹是為了讓 CPU 花掉等量的時間）才回傳，讓兩條路徑的耗時
量級一致。

### 帳號鎖定政策：檢視後維持不變，殘留風險已知且記錄

任務要求檢視「加入 IP 限流後，帳號鎖定政策是否仍合理」。**結論：維持現狀（連續 5 次失敗鎖定
15 分鐘），不調整**，理由：

- 這個門檻本來就不是規劃書明訂的數字（`AdminAuthService.cs` 既有註解已經寫明「業界常見門檻，
  執行層判斷」），IP 限流是**額外加的一層**，不是取代鎖定機制的理由去調整它。
- **殘留風險：鎖定機制本身仍可被用來對單一帳號做阻斷服務**——攻擊者只要對**同一個**已知帳號
  連續送 5 次錯誤密碼（跟 `admin-login` 每分鐘 5 次的 IP 額度打平，剛好不會被 IP 限流擋下），
  就能把該帳號鎖 15 分鐘，這件事 IP 限流**擋不住**（IP 限流擋的是「同一 IP 打很多不同帳號」或
  「同一 IP 打太多次」，擋不住「剛好只打 5 次、打同一個帳號」這種低量攻擊）。這是**任何純計數式
  鎖定機制的既有取捨**
  （OWASP 也承認這個 trade-off），業界常見的緩解手法是 CAPTCHA 或漸進式延遲，但
  **本系統目前沒有串接任何 CAPTCHA 服務**（`Program.cs` 既有註解已提過這件事），加 CAPTCHA
  超出本輪任務範圍，這裡只記錄殘留風險，不在本輪處理。
- 如果日後要處理，兩個方向都可以考慮：① 把鎖定改成「帳號＋來源 IP」複合鍵而不是純帳號
  （壞處：攻擊者只要換 IP 就繞過，防禦力反而下降，除非搭配本輪的 IP 限流一起看）；
  ② 引入 CAPTCHA 或漸進式延遲（例如第 3 次失敗後要求驗證碼）。這是需要跟客戶／規劃書
  討論優先序的題目，不是本輪能自行判斷的範圍。

### 帳號枚舉一致性：狀態碼與訊息本來就一致，這輪補的是時序

檢查結果：`AdminAuthEndpoints.cs` 的 `LoginAsync` 對「帳號不存在」與「密碼錯誤」兩種情況，
**狀態碼（皆 401）與回應本體（皆 `{"status":"invalid_credentials","message":"帳號或密碼
錯誤。"}）本來就完全一致**——既有測試 `AdminAuthTests.登入_密碼錯誤_回401且訊息不洩露帳號是否
存在` 已經涵蓋這件事，本輪沒有發現需要修正的落差。**唯一的落差是時序**（見上一節），本輪已修正。

### 架構測試：新增一支專門鎖定 `/login`／`/refresh` 的掃描

`ArchitectureTests.cs` 既有的 `公開端點的非GET寫入呼叫都必須掛限流政策` 排除整個
`Features/AdminXxx/`（因為那個資料夾底下的端點一律經過型別層強制授權），這條規則對
`AdminAuth` 資料夾底下其餘端點（`/change-password`／`/2fa/*`／`/me`）仍然正確——**不能
把整個資料夾排除規則拿掉**。改成新增一支**專門鎖定這一個檔案、只鎖定 `/login`／`/refresh`
兩個路由字面值**的測試：`AdminAuth的登入與更新權杖端點必須掛限流政策`。這支測試同時斷言
「這兩個路由確實存在於檔案裡」（`SetEquals` 檢查），避免路由字面值或檔案結構改變時測試
悄悄變成恆真、失去防呆效果。`/logout` 刻意不在檢查清單內，理由同上方「處置」表格。

`docs/14-invariants.md`「公開寫入端點一律限流」條的排除說明已同步更新——不再寫「未來要另外
判斷」，改成指向這裡實際落地的兩個政策名稱與理由。

### 測試

新增／擴充七項：

- **`Tcrfc.Api.Tests/AdminAuthRateLimitPoliciesTests.cs`**（新增，7 項，不需要資料庫、不需要
  `WebApplicationFactory`）：
  - `未設定任何環境變數時_登入額度預設值是嚴格的個位數量級`／
    `未設定任何環境變數時_更新權杖額度預設值等於程式碼常數`：用一個空的
    `ConfigurationBuilder().Build()`（不含任何環境變數來源）驗證
    `AdminAuthRateLimitOptions.ResolveLoginPermitLimit`／`ResolveRefreshPermitLimit` 解析出來的
    就是嚴格預設值（5／30）——這是回應「不要讓測試用量決定正式環境的安全額度」這個回饋修正的
    核心驗證：**證明「沒有任何覆寫時」的行為就是正式環境會用到的行為**。
  - `設定值壞掉時_退回嚴格預設值_不會悄悄變成不限流`（`Theory`，5 組壞資料：空字串、空白、
    非數字、`0`、負數）：驗證設定壞掉時退回預設值，不會讓一個打錯的設定值變成限流形同虛設。
  - `設定合法正整數時_採用覆寫值_不是預設值`：驗證覆寫機制本身確實生效。
  - `Login政策_以預設額度_額度用盡後拒絕_不同分區互不影響`／
    `Refresh政策_以預設額度_額度用盡後拒絕_不同分區互不影響`：跟 `PublicRateLimitPoliciesTests`
    同一種寫法，直接對**預設值**建 `PartitionedRateLimiter<string>`，驗證額度用盡後拒絕、不同
    分區鍵互不影響。
- **`Tcrfc.Api.Tests/AdminAuthRateLimitingTests.cs`**（重寫，2 項，需要 `WebApplicationFactory`
  ＋測試資料庫）：走真正 HTTP 驗證 `Program.cs` 真的把兩個政策接到 `/login`／`/refresh` 路由上。
  **改用新增的專屬 `AdminAuthRateLimitTestApiFixture`＋`AdminAuthRateLimitTestCollection`**（不
  跟任何其他測試共用），把兩個政策的額度都覆寫成一個很小的專用數字（`TestPermitLimit = 3`）——
  一般用途 fixture 現在額度是寬鬆覆寫值（十萬），沒辦法在裡面驗證「額度用盡後真的 429」；這支
  測試需要的是相反的設定，兩種需求互斥，所以獨立成自己的 fixture／collection。
- **`Tcrfc.Api.Tests/Fixtures/TestRateLimitOverrides.cs`**（新增，第三版改用
  `IWebHostBuilder.ConfigureAppConfiguration`，不是環境變數，見上方「測試環境的覆寫機制」
  一節）：一般用途 fixture 共用的寬鬆覆寫方法（`ApplyLooseAdminAuthOverrides(IWebHostBuilder)`，
  額度覆寫成 `100000`）＋通用覆寫方法（`ApplyAdminAuthOverrides(IWebHostBuilder, string)`）。
  已接到 6 個一般用途 fixture：`ApiFixture`／`AdminWriteApiFixture`／
  `AdminWriteAzuriteEnabledApiFixture`／`RedisUnavailableApiFixture`／
  `AdminWriteRedisEnabledApiFixture`／`RedisEnabledApiFixture`——每一個都覆寫
  `WebApplicationFactory.ConfigureWebHost` 呼叫這個方法，只影響該 fixture 自己建出來的測試
  主機，不再寫行程全域環境變數。
- **`Tcrfc.Api.Tests/Fixtures/AdminAuthRateLimitTestApiFixture.cs`**（新增，同樣改用
  `ConfigureWebHost`）：見上方 `AdminAuthRateLimitingTests.cs` 說明；`CollectionDefinitions.cs`
  新增對應的 `AdminAuthRateLimitTestCollection`。
- **`Tcrfc.Api.Tests/ArchitectureTests.cs`**（新增 1 項）：`AdminAuth的登入與更新權杖端點必須
  掛限流政策`，見上一節（這支測試只檢查「有沒有掛 `.RequireRateLimiting(...)`」，不涉及額度
  數字，本輪額度改成可設定值不影響這支測試）。
- **`Tcrfc.Api.Tests/AdminAuthTests.cs`**（新增 1 項）：
  `登入時序安全_帳號不存在與密碼錯誤耗時相近_防止枚舉攻擊`——3 組樣本，各量測一次「密碼錯誤」
  與「帳號不存在」兩種請求的耗時，比較平均值比例，容忍區間刻意放寬（0.4～2.5 倍）只抓「量級
  差一個數量級」這種明顯漏洞，不追求精確相等（時序測試天生受機器負載雜訊影響，見該測試方法
  上的完整說明）。⚠️ 這支測試會製造 3 次密碼錯誤嘗試，`finally` 區塊比照既有慣例呼叫
  `ResetAccountLockAsync` 歸零，不留殘餘鎖定狀態。這支測試現在跑在
  `AdminWriteApiFixture`（已套用寬鬆覆寫），不會被 `admin-login` 的嚴格預設值誤傷。

**實際執行**：⚠️ **本輪仍未執行 `dotnet test`**——限制不變：依任務指示不讀取或組合資料庫密碼，
本機唯一已知取得 `CLUB_SQL_CONNECTION_STRING` 的方式（見本檔上方「怎麼跑」一節的「一行版」）
需要把密碼讀進 shell 變數組字串，已依指示停下來，交由使用者執行測試。**新增／修改的檔案已用
`dotnet build`（`apps/api` 主專案＋`Tcrfc.Api.Tests`，皆不需要資料庫連線）確認可以編譯通過，
0 警告、0 錯誤**，但沒有實際 `dotnet test` 通過數字可回報。

### 沒有新的機密要加

兩個新環境變數（`ADMIN_LOGIN_RATE_LIMIT_PERMIT_LIMIT`／`ADMIN_REFRESH_RATE_LIMIT_PERMIT_LIMIT`）
是**選填的調整值，不是機密**，不設定就用程式碼內建的嚴格預設值——不需要新增
`/opt/tcrfc/secrets/*.env` 項目，`deploy/dev/club.env`／`deploy/dev/club.env.example` 也不需要
補上這兩個鍵才能運作。已列入 [`docs/17-deployment.md`](../../docs/17-deployment.md) §11 表格
（第 13 項）供日後需要調整額度時查閱。

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

## 後台模組種子（2026-09-30，`backend-engineer`）

使用者要求「後台打開就有資料可以看、可以測試」。`db/seed/backoffice_seed.py`（由 `generate-club-seed-sql.py`
尾端呼叫）為已完成的後台模組種下兩俱樂部各自一份資料：`B1` 頁面、`B3` 輪播（draft）、`B4` FAQ、`P1／P2` 課程與梯次、
`C4` 積分榜、`L2` 自建事件、`B2` 標籤與核心價值標籤、`H` 301 轉址、全站 SEO 預設、`llms.txt` 五區塊、AI 爬蟲設定，
以及 `I` 網站設定補齊電話／營業時間／英文值。**真實內容與測試值的界線、來源、筆數、測試值清單與灌庫步驟見
[`db/seed/README.md`](../../db/seed/README.md)「後台模組種子（2026-09-30）」一節。**

三個既有整合測試因此一併調整（否則種子被吃掉或測試失敗，見 `docs/18-work-errors.md` `E-81`）：
`AdminSeoImageTests`（`DeleteSeoTextSettingsAsync` 改為快照後還原）、`AdminMatchesAndStandingsTests`
（`Standing_CSV匯入_整季替換` 記下既有列、`DeletedCount` 算進去並於 `finally` 補回）、`SiteFactsTests`
（電話／營業時間改為只驗證有值）。⚠️ 這三處只確認過 `dotnet build` 通過，**未重跑 `dotnet test`**，灌庫後請跑全套。

---

## D 批：G3 電子報／E4–E6 App 廣告／M1–M5 App 後台／J3 帳號活動／App 公開端點（2026-09-30，`backend-engineer`）

主站規劃書 §4.7 G3、§4.5 E4–E6、§4 M、§4.10 J3；App 規劃書 §6、§7、§8、§9.2、§10、§11；`docs/19` §5／§6／§7；對應 `STATUS.md` 的 `S3-10`、`AP-1`。
沿用 E1a／B1／C1 的通則（錯誤格式 `ProblemDetails`、`content: { zh, en? }` 雙語、`PUT` 整份取代、分頁形狀、跨範圍一律 404、敏感操作日誌）與 C1 的**全站時間格式通則**（時間戳一律 UTC 帶 `Z`）。
**本節只寫 D 批新增的規則與每支端點的契約。給畫面的人：只讀這一節就能串接，不需要看程式碼。**

### D 批通則

| 項目 | 規定 |
|---|---|
| 路徑 | G3 走俱樂部範圍 `/api/v1/admin/{club}/newsletter/…`（名單兩站各自獨立）。**廣告與 App 不分俱樂部（App 是兩隊共用平台）**：`/api/v1/admin/ads/…`（E4–E6）、`/api/v1/admin/app/…`（M1–M5）、`/api/v1/admin/security/overview`（J3），全部走全域授權（`IAdminSystemAuthorizer`），**沒有 `{club}` 路由段**，權限碼的 `is_club_scoped=0`。App 公開端點 `/api/v1/app/…`（匿名）。 |
| 權限 | 見下方矩陣。**合作球隊管理沒有任何 `ad.*`／`app.*`**（規劃書 §11）。`sysadmin_only` 的碼即使角色被勾選也只有系統管理員能持有。畫面用 `GET /auth/me` 的權限碼決定按鈕；**權限碼只給程式判斷，不得顯示**。 |
| 個資 | 電子報名單（Email）、推播權杖、裝置識別碼**視同個資**：匯出必填 `purpose`（≤200 字，缺 → 400）並寫敏感操作日誌；推播權杖加密儲存、**任何回應都不含權杖**（只有 `reveal=true` 且有 `app.device.reveal` 才回完整值）；裝置識別碼在清單只給遮罩（前 4 碼＋`****`＋後 2 碼）。CSV 一律 UTF-8 BOM，**含使用者輸入的文字欄位以 `'` 中和公式注入**（`= + - @`）。 |
| 圖片與影片 | 同 E1a：含圖片的建立／更新是 `multipart/form-data`（`payload` ＋ 檔案欄位），寫入失敗補償刪除已上傳物件。廣告素材另可附影片（檔案欄位 `video`，不轉碼）；素材圖上傳時就檢查版位規格（最小尺寸→長寬比→檔案大小）。回應只給完整網址（`imageUrl`／`imageThumbUrl`／`videoUrl`），公開回應**沒有物件鍵**。 |
| 尚未串接的接縫 | **推播傳輸（APNs／FCM）、EDM 平台、Cloudflare 靜態設定**都以介面隔開、預設「尚未串接」實作：後台邏輯照常運作，回應**如實說明**沒串接（不假裝成功），見 `docs/17` §3「D 批的接縫」。 |
| 背景作業 | `AppMaintenanceBackgroundService`（hosted service，預設每 60 秒；`APP_JOBS_INTERVAL_SECONDS` 覆寫，**0＝停用**，整合測試主機停用）：到點的推播發送、廣告檔期依起訖時間推進、廣告事件每日聚合、清除 90 天前的已聚合事件與診斷回報。手動觸發：`POST /ads/maintenance/run`（`ad.maintenance.run`）、`POST /app/push/dispatch-due`（`app.push.approve`）。 |
| 限流 | App 公開**寫入**端點掛 `public-app` 政策（每 IP 每分鐘 120 次，行動網路共用 IP 所以較寬；`APP_PUBLIC_RATE_LIMIT_PERMITS` 覆寫；架構測試強制寫入端點必掛限流）。公開讀取端點不限流（靠邊緣快取）。 |
| 快取標頭 | 內容對所有裝置相同的讀取（`/config`、沒帶裝置識別的 `/layout`／`/notifications`）：`public, max-age=60, stale-while-revalidate=60, stale-if-error=86400`；**帶了裝置識別的回應一律 `private, no-store`**（內容因裝置而異，不得被邊緣快取誤送給別人）；`/ads/{slotCode}` 沒帶裝置識別 `public, max-age=60`、帶了 `no-store`（每人頻次上限）。 |

### 權限碼與角色矩陣（`db/seed/generate-club-seed-sql.py`，共 38 碼）

| 權限碼 | 用途 | 系統管理員 | 內容編輯 | 商務／贊助 | 公關／媒體 | 客服／行政 | 檢視者 | 合作球隊管理 |
|---|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| `form.newsletter.view／update` | G3 名單檢視、新增、退訂、同步 EDM | 全 | — | — | — | 全 | — | 全（僅自家） |
| `form.newsletter.export`（🔴 受限） | G3 匯出 CSV | ✔ | — | — | — | — | — | — |
| `ad.advertiser.*`、`ad.slot.*`（view／create／update／delete） | E4 廣告主、版位 | 全 | — | 全 | — | — | 唯讀 | — |
| `ad.campaign.view／create／update／delete` | E5 檔期與素材（送審、結案、作廢、素材上傳） | 全 | — | 全 | — | — | 唯讀 | — |
| `ad.campaign.review` | E5 審核檔期與素材（核可／退回） | ✔ | — | ✔ | — | — | — | — |
| `ad.campaign.pause` | E5 緊急暫停與恢復（檔期與素材） | ✔ | — | ✔ | — | — | — | — |
| `ad.contract.view／update`（🔴 受限） | 合約金額檢視／編輯（**規劃書寫「財務可見」，專案沒有財務角色**） | ✔ | — | ✔ | — | — | — | — |
| `ad.report.view` | E6 成效報表 | 全 | — | ✔ | ✔ | — | ✔ | — |
| `ad.report.export`（🔴 受限） | E6 報表匯出 | ✔ | — | ✔ | — | — | — | — |
| `ad.maintenance.run`（**sysadmin_only**） | 手動執行廣告維護作業 | ✔ | — | — | — | — | — | — |
| `app.release.view` | M1 檢視版本與維護模式 | ✔ | — | — | — | — | ✔ | — |
| `app.release.update`（**sysadmin_only**） | M1 版本、更新門檻、維護模式 | ✔ | — | — | — | — | — | — |
| `app.layout.view／update` | M2 內容編排與深連結 | 全 | 全 | — | — | — | 唯讀 | — |
| `app.push.view／create` | M3 檢視、建立、預覽、試送、送審、取消、刪除草稿 | 全 | — | — | 全 | — | 唯讀 | — |
| `app.push.approve`（**sysadmin_only**） | M3 覆核（核可／退回）、失敗重送、自動推播規則、手動觸發到點發送 | ✔ | — | — | — | — | — | — |
| `app.device.view` | M4 裝置清單（遮罩）與統計 | ✔ | — | — | — | ✔ | ✔ | — |
| `app.device.reveal`（🔴 受限＋**sysadmin_only**） | M4 檢視完整識別碼與推播權杖 | ✔ | — | — | — | — | — | — |
| `app.device.update`（**sysadmin_only**） | M4 失效權杖清理 | ✔ | — | — | — | — | — | — |
| `app.config.view` | M5 檢視功能開關、連線檢查 | ✔ | — | — | — | — | ✔ | — |
| `app.config.update`（**sysadmin_only**） | M5 管理功能開關 | ✔ | — | — | — | — | — | — |
| `app.credential.view／update`（🔴 受限＋**sysadmin_only**） | M5 金鑰與憑證列管、輪替 | ✔ | — | — | — | — | — | — |
| `app.diagnostic.view`／`update` | M5 診斷回報檢視／處理狀態 | ✔（兩者） | — | — | — | — | view | — |
| `system.audit.view`（既有，**sysadmin_only**） | J3 帳號活動概況 | ✔ | — | — | — | — | — | — |

---

### G3 電子報 `/api/v1/admin/{club}/newsletter`

名單 `(club_id, email)` 唯一：**同一人可以只退訂其中一站**（另一站的名單不受影響）。用對方俱樂部的路由操作這一筆一律 404。

| 方法 路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET /subscribers?status=&source=&keyword=&page=&pageSize=` | view | 分頁（新→舊）：`{ id, email, source, sourceLabel（沒填顯示「未註明」）, status(subscribed／unsubscribed), statusLabel(已訂閱／已退訂), subscribedAt, unsubscribedAt, updatedAt }`；`keyword` 比對 Email；`status` 錯誤 → 400 |
| `GET /summary` | view | `{ subscribedCount, unsubscribedCount, sources: [ { source, sourceLabel, count } ] }`（來源分布只算目前訂閱中的人） |
| `POST /subscribers` → 201 | update | `{ email（必填，轉小寫）, source?（預設「後台新增」）}`。**已在名單中 → 409；曾經退訂 → 409「不能由後台直接加回」**（退訂是法遵事實） |
| `PUT /subscribers/{id}/status` | update | `{ status, reason? }`。改成 `unsubscribed` 記 `unsubscribedAt`；改回 `subscribed` **必須填 `reason`（≤200，說明是訂閱者本人要求）**，否則 400，並寫敏感操作日誌 |
| `DELETE /subscribers/{id}` → 204 | update | 個資刪除請求；**不留退訂紀錄**（只是要求退訂請改用退訂）；寫日誌 |
| `GET /export?purpose=&status=&source=&keyword=` | `form.newsletter.export`（🔴） | CSV：`Email,來源,狀態,訂閱時間,退訂時間`（訂閱／退訂時間為 UTC）；用途必填 |
| `GET /edm` | view | `{ configured, provider, message }`（**目前恆為 `configured:false`**：供應商未定） |
| `POST /edm/sync` | update | 把「訂閱名單＋退訂名單」交給 EDM 平台：`{ configured, subscribedCount, unsubscribedCount, syncedCount, message }`；**未串接時 `configured:false`、`syncedCount:0`、說明「尚未串接」**（不是錯誤）。退訂名單一併送出（抑制清單） |

**沒有公開訂閱／退訂端點**（官網頁尾訂閱表單屬前台工作，本批沒有）；**不做電子報群發**（官網仍不寄信，規劃書 §1.3），EDM 平台端才寄。

---

### E4 版位與廣告主 `/api/v1/admin/ads`

**版位 `/slots`**：`GET`（陣列）／`GET /{id}`／`POST`（201）／`PUT /{id}`／`DELETE /{id}`（**有檔期 → 409，請改停用**）／`GET /{id}/schedule`（見 E5）。
**版位 `<AdminAdSlotDto>`**：`{ id, slotCode, surface("app"), screenCode, blockOrder, aspectRatio("16:9"), minWidth, minHeight, maxFileKb, allowedFormats, allowVideo, sessionImpressionCap, rotationCap(1–10), fallbackImageKey, fallbackImageUrl, fallbackImageThumbUrl, fallbackLink, isActive, nameZh, nameEn, fallbackAltZh, fallbackAltEn, campaignCount, updatedAt }`。
**寫入（multipart）**：`payload` `{ slotCode（必填，格式「畫面_位置」小寫英數底線，例 `home_top`，**建立後不可改**）, screenCode?, blockOrder?, aspectRatio?（「寬:高」）, minWidth?, minHeight?, maxFileKb?, allowedFormats?, allowVideo, sessionImpressionCap?, rotationCap（1–10）, fallbackLink?, isActive, removeFallbackImage?, content: { zh: { name（必填）, fallbackAlt? }, en? } }` ＋ 選填檔案欄位 `fallbackImage`（備援素材，自家內容，**版位永不空白**）。
🔴 規則：**兒童向畫面（`S15`／`S16`／`S17`：課程列表、課程報名表、我的報名）不設版位 → 400**；**不設慈善相關版位**（代號含 `charity`／`donation` → 400）；代號重複 409；代號改動 400。

**廣告主 `/advertisers`**：`GET ?status=&keyword=`（陣列）／`GET /{id}`／`POST`（201）／`PUT /{id}`／`DELETE /{id}`（**有檔期 → 409**）／`GET /sponsor-options?keyword=`（挑選贊助商：`[ { id, name, clubCode } ]`，跨俱樂部）。
**`<AdminAdvertiserDto>`**：`{ id, nameZh, nameEn, taxId, contactName, contactPhone, contactEmail, contractNote, cooperationStartOn, cooperationEndOn, sponsorId, sponsorName, status(negotiating洽談中／active合作中／ended已結束), statusLabel, campaignCount, updatedAt }`。
**寫入（JSON）**：`{ taxId?, contactName?, contactPhone?, contactEmail?, contractNote?, cooperationStartOn?, cooperationEndOn?, sponsorId?（**可為空，指向既有贊助商，只用來避免重複維護聯絡窗口，不是合併**）, status?（預設 negotiating）, content: { zh: { name（必填）}, en? } }`。合作已結束的廣告主不能再建立新檔期（400）。

---

### E5 檔期與素材 `/api/v1/admin/ads/campaigns`、`/creatives`

**狀態機**：`draft 草稿 → pending_review 待審核 → scheduled 已排程 → running 投放中 → ended 已結束 → closed 已結案`；`running ↔ paused 已暫停`（恢復時回到暫停前狀態：投放中／已排程／已過期則直接結束）；`voided 已作廢`（任一狀態，不可逆）。**已排程→投放中→已結束由起訖時間自動推進**（背景作業、公開投放端點與後台讀取都會推進，最多延遲 30 秒）。
🔴 **素材未通過審核的檔期不得進入投放中**：核可（→已排程）需要至少一個「已通過」的素材；自動推進與「恢復」都需要「已通過且未暫停」的素材。

| 方法 路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET /campaigns?status=&slotId=&advertiserId=&keyword=` | `ad.campaign.view` | 陣列（開始時間新→舊，最多 500）：`{ id, name, advertiserId, advertiserName, slotId, slotCode, slotName, startsAt, endsAt, weight, goalType(guaranteed曝光保證／traffic導流), goalTypeLabel, goalImpressions, deliveredTotal, status, statusLabel, creativeCount, approvedCreativeCount, updatedAt }` |
| `GET /campaigns/{id}` | 同上 | 詳情：以上＋`dailyImpressionCap`、`perDeviceDailyCap`、`deliveredToday`、**`contractAmount`（沒有 `ad.contract.view` → `null`）、`contractAmountLabel`（沒權限＝「不公開」；有權限＝「NT$ 30,000」／「尚未填寫」）、`isAmountHidden`（沒權限 `null`）**、`pauseReason`（暫停或作廢原因）、`reviewedBy`／`reviewedAt`、`availableActions[]`、**`pacing`**（`{ goalImpressions, delivered, expectedByNow, dailyTarget, status(ahead超前／on_track正常／behind落後), statusLabel }`，只有曝光保證型）、`creatives[]`、`updatedAt` |
| `POST /campaigns` → 201／`PUT /campaigns/{id}` | create／update | JSON：`{ advertiserId, slotId, name, startsAt, endsAt（必須晚於開始）, weight?（1–100，預設 1）, dailyImpressionCap?, perDeviceDailyCap?, goalType?（預設 traffic）, goalImpressions?（**曝光保證必填**）, contractAmount?, isAmountHidden? }`。**金額欄位需要 `ad.contract.update`，沒有權限卻帶非空值 → 403**。草稿可改全部；**待審核不能改（先退回）**；**已排程／投放中／已暫停只能改名稱、權重與兩個上限，改廣告主／版位／期間／目標 → 409（請作廢重建）**；已結束／結案／作廢 → 409。版位停用或廣告主已結束 → 400 |
| `DELETE /campaigns/{id}` → 204 | delete | **只有草稿**；否則 409（請作廢） |
| `POST /campaigns/{id}/submit` | update | 草稿 → 待審核（**至少要有一個素材**，否則 409） |
| `POST /campaigns/{id}/approve` | `ad.campaign.review` | 待審核 → 已排程（需已通過素材，否則 409）；開始時間已過就直接進入投放中 |
| `POST /campaigns/{id}/return` | 同上 | 待審核 → 草稿 |
| `POST /campaigns/{id}/pause` | `ad.campaign.pause` | `{ reason（必填）}` 已排程／投放中 → 已暫停（**緊急暫停：立刻停止投放**） |
| `POST /campaigns/{id}/resume` | 同上 | 已暫停 → 回到原狀態（需已通過且未暫停的素材，否則 409） |
| `POST /campaigns/{id}/close` | update | 已結束 → 已結案 |
| `POST /campaigns/{id}/void` | update | `{ reason（必填）}` 任一狀態 → 已作廢（不可逆） |
| `GET /slots/{id}/schedule?from=&to=` | `ad.campaign.view` | **衝突檢視**：同版位同時段的檔期（待審核／已排程／投放中／已暫停，預設從現在起 30 天）：`{ slotId, slotCode, rotationCap, maxConcurrent（期間內同時最多幾個檔期）, exceedsRotationCap（只是提示，不擋存檔）, items: [ { campaignId, name, advertiserName, startsAt, endsAt, weight, status, statusLabel, weightSharePercent } ] }` |

所有狀態動作回檔期詳情；狀態不對 → 409（「目前狀態不能這樣操作」）。

**素材**：`GET /campaigns/{id}/creatives`（陣列）；`POST /campaigns/{id}/creatives`（201，multipart）；`PUT /creatives/{id}`（multipart）；`DELETE /creatives/{id}`（204；**只有草稿／待審核的檔期能刪，之後只能暫停**，成效要留著對帳）；`POST /creatives/{id}/approve`、`/reject`（`{ reason（必填）}`）（`ad.campaign.review`；只能從「待審」）；`POST /creatives/{id}/pause`、`/resume`（`ad.campaign.pause`；單一素材緊急暫停）。寫入權限 `ad.campaign.update`。
**寫入 payload**：`{ locale("zh"｜"en"，依語系分別上傳), altText（必填 ≤200）, title?, ctaText?, clickUrl?（`tcrfc://…` 或 http(s)）, theme?（light／dark／both）, variantTag?（"A"｜"B"）, removeVideo? }` ＋ 檔案欄位 `image`（**新增必填**；影片素材的圖片是海報）、選填 `video`（**版位不允許影片 → 400**）。素材規格檢查（版位設定）：最小尺寸 → 長寬比（容差 2%）→ 檔案大小；不合 400。
**`<AdminAdCreativeDto>`**：`{ id, campaignId, locale("zh"｜"en"), imageKey, imageUrl, imageThumbUrl, imageWidth, imageHeight, videoKey, videoUrl, altText, title, ctaText, clickUrl, theme(+ThemeLabel), variantTag, reviewStatus(pending待審／approved通過／rejected退回)(+Label), rejectReason, reviewedAt, isPaused, updatedAt }`。
🔴 **素材內容被修改（文案、點擊目的地、換圖、換影片）後一律回到「待審」**；**沒有任何改動的儲存不會退回待審**。

---

### E6 成效報表與維護 `/api/v1/admin/ads`

| 方法 路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET /reports?from=&to=&campaignId=&slotId=&creativeId=&platform=&locale=&groupBy=` | `ad.report.view` | `from`／`to` 是台灣日期（預設最近 30 天，最長 366 天）；`platform`：`ios`／`android`；`locale`：`zh`／`en`；`groupBy`：`campaign`（預設）／`slot`／`creative`／`platform`／`locale`／`date`。回 `{ from, to, groupBy, rows: [ { label, id, impressions, clicks, ctr（百分比兩位小數）, uniqueDevices } ], total, pendingEvents, pacing: [ { campaignId, campaignName, pacing } ] }`。**沒有任何個人層級資料**（裝置清單、會員關聯）。`pendingEvents`＞0 表示期間內還有尚未聚合的原始事件（數字還沒包含它們，請系統管理員執行維護作業）。⚠️ **`uniqueDevices` 跨多日彙總是「每日不重複裝置數的加總」（裝置日）**，不是期間內的真正不重複人數 |
| `GET /reports/export?…同上…&purpose=` | `ad.report.export`（🔴） | CSV：`項目,曝光數,點擊數,點擊率（%）,不重複裝置數`（最後一列合計）；用途必填，**日誌記錄誰、哪個檔期（或全部）、期間、用途**。PDF 匯出不提供（畫面可列印） |
| `POST /maintenance/run` | `ad.maintenance.run` | 推進檔期、聚合、清除：`{ campaignsStarted, campaignsEnded, eventsAggregated, daysRebuilt, eventsPurged, diagnosticsPurged, overdueUnaggregated }`。`overdueUnaggregated`＞0＝**聚合落後告警**（超過 2 天仍未聚合，或超過 90 天無法重算的事件；清除只刪已聚合的，所以不會刪掉還沒聚合的資料） |

**曝光與點擊的定義（App 規劃書 §7.5）由 App 端量測**（可見面積 ≥50% 連續 ≥1 秒；備援素材與載入失敗不計）；伺服器端只做去重與時間關卡，見下方「廣告事件」。**贊助商 Logo 牆不計曝光、不入報表。**

---

### M1 版本與維護 `/api/v1/admin/app`（**寫入僅系統管理員**）

| 方法 路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET /releases?platform=` | `app.release.view` | 陣列（同平台版本新→舊）：`{ id, platform(ios／android), platformLabel, version, buildNumber, releasedOn, status(testing測試／live已上架／withdrawn已下架)(+Label), isMinSupported, isRecommended, zh?: { whatsNew, forceMessage, recommendMessage }, en?, updatedAt }` |
| `POST /releases` → 201／`PUT /releases/{id}` | `app.release.update` | `{ platform, version（`主.次.修`，建置號不參與比較）, buildNumber?, releasedOn?, status?（預設 testing）, content: { zh: { whatsNew?, forceMessage?, recommendMessage? }, en? } }`。同平台同版本 409；**版本號與平台建立後不能改（400）**；被設為更新門檻的版本不能改成測試／下架（409）。`PUT` 回 `{ value: <版本>, edgePublish }` |
| `DELETE /releases/{id}` → 204 | 同上 | 只有未上架且未被設為門檻的版本（409） |
| `PUT /releases/{id}/flags` | 同上 | `{ isMinSupported, isRecommended, confirmForceUpdate }`：**最低支援版本**（低於它啟動時強制更新、不可略過）與**建議版本**（低於它建議更新、可略過並每 7 天再提醒）。**只有已上架的版本能設為門檻（409）**；每平台各至多一筆（設新的自動取消舊的）；🔴 **設最低支援版本必須 `confirmForceUpdate: true`（二次確認，否則 409 說明「會強制舊版更新」）**。回 `{ value, edgePublish }`，寫日誌 |
| `GET /maintenance` | view | `[ { scope(all／ios／android), scopeLabel, enabled, messageZh, messageEn } ]`（固定三筆） |
| `PUT /maintenance/{scope}` | update | `{ enabled, messageZh?, messageEn? }`；**開啟必須填繁中訊息（400）**。回 `{ value, edgePublish }`，寫日誌 |

**`edgePublish`**：`{ published: bool, message }`——設定存檔後是否已同步到 Cloudflare 靜態設定（`docs/19` §7 第 1 層來源，VM 全滅時 App 仍讀得到「維護中」）。**目前恆為 `published:false`＋「尚未串接」說明，存檔本身一律成功**（畫面應顯示這句提醒，不是錯誤）。

### M2 內容編排 `/api/v1/admin/app/layout`（`app.layout.view／update`）

| 方法 路徑 | 說明 |
|---|---|
| `GET /items?kind=` | 版面項目（依 `kind`、排序）：`{ id, kind(home_section首頁區塊／quick_entry快捷入口／more_item「更多」分頁項目), kindLabel, itemKey, deepLinkId, deepLinkCode, iconKey, sortOrder, isEnabled, isFixed, labelZh, labelEn }`。**首頁區塊固定九個（`isFixed:true`）：`next_match`、`ad_home_top`、`latest_news`、`member_card`、`recent_matches`、`ad_home_mid`、`nearby_stores`、`quick_entries`、`sponsor_wall`，只能開關與排序、改名稱與連結，不能新增或刪除** |
| `POST /items` → 201／`PUT /items/{id}` | `{ kind（僅 quick_entry／more_item）, itemKey（新增必填，小寫英數底線，同類型唯一 → 409）, deepLinkId?, iconKey?, isEnabled, label: { zh（必填）, en? } }`（`PUT` 忽略 `kind`／`itemKey`） |
| `DELETE /items/{id}` → 204 | 首頁區塊 → 409 |
| `POST /items/reorder` | `{ kind, ids }` 排到最前面，其餘維持相對順序；含不屬於該類型的 id 或重複 → 400。回該類型的項目陣列 |
| `GET／POST／PUT／DELETE /deep-links` | **深連結對照表**：`{ id, code, appLink, webUrl, requiresLogin, isActive, sortOrder, labelZh, labelEn, usedByCount }`；寫入 `{ code（小寫英數底線，唯一 409）, appLink（**必須 `tcrfc://` 開頭**，否則 400；scheme 固定）, webUrl?, requiresLogin, isActive, label: { zh, en? } }`；**有版面項目使用時不能刪（409）** |
| `GET／POST／PUT／DELETE /announcements` | **公告條**：`{ id, messageZh, messageEn, linkUrl, startsAt, endsAt, audienceTier(all／fan_club／registered／anonymous)(+Label), audienceClubCode, isEnabled, isActiveNow }`；寫入 `{ message: { zh（必填 ≤200）, en? }, linkUrl?（`tcrfc://` 或 http(s)）, startsAt?, endsAt?（須晚於開始）, audienceTier?, audienceClubCode?, isEnabled }` |

**不做** App 內的內容 CRUD（新聞、賽事、球員、店家仍在既有模組維護，M2 只管呈現順序與開關）。

### M3 推播 `/api/v1/admin/app/push`（雙人覆核）

**流程**：公關／媒體 **建立草稿（`draft`）→ 預覽／試送 → 送審（`pending_review`）** → **另一位**系統管理員 **核可（→ `scheduled`，時間到就發送）**；`sending` 是發送中；結果 `sent 已發送`／`partial 部分送出`／`failed 失敗`；`cancelled 已取消`。
🔴 **核可者不得是建立者本人（409「必須由另一位系統管理員覆核」）——系統管理員自己建立的批次一樣要另一位核可。**🔴 **二次確認**：核可必須帶 `expectedAudience`（操作者在畫面上看到的預估觸及裝置數），與伺服器當下重算的人數不同 → 409（分眾在核可前變動了）；沒有任何符合條件的裝置 → 409。
🔴 **系統層阻擋（建立、修改、送審、核可、試送都檢查）**：**推播不得成為繞過「中獎只以最新消息公布」承諾的後門**——標題或內文（中英）含「中獎」「得獎」「獲獎」「抽中」「winner」等字樣 → 409；**深連結指向帶「球迷會員抽獎」標籤的文章（`tcrfc://news/{slug}` 或官網新聞網址）→ 409**。分眾條件本身沒有「以中獎名單為對象」的維度，所以無法用分眾送到中獎人。**自動推播（新聞發布）必須先呼叫 `PushContentGuard.IsMemberDrawArticleAsync`：帶抽獎標籤的文章一律不自動推播。**

| 方法 路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET /messages?status=` | `app.push.view` | 陣列（新→舊，最多 200）：`{ id, titleZh, kind, status, statusLabel, scheduledAt, sentAt, sentCount, deliveredCount, openedCount, createdBy, createdAt }` |
| `GET /messages/{id}` | 同上 | 詳情：`{ id, kind, status, statusLabel, content: { zh: { title, body, imageAlt }, en? }, imageKey, imageUrl, deepLink, audienceTier(+Label), audienceClubCode, audienceTeamCodes[], scheduledAt, audienceEstimate, createdBy, reviewedBy, reviewedAt, rejectNote, sentAt, sentCount, deliveredCount, failedCount, openedCount, failureMessage, stats: [ { platform, platformLabel, locale, localeLabel, sent, delivered, opened } ], statsNote, availableActions[], updatedAt }`。**`statsNote` 是三個數字的語意說明，畫面必須照這個措辭呈現**：送出＝我方交給推播服務的則數；**送達＝推播服務已接受且沒有回報權杖失效，不等於已到達使用者手機**；開啟＝App 回報的次數，**系統不追蹤是哪一位使用者開啟** |
| `POST /messages` → 201／`PUT /messages/{id}` | `app.push.create` | **multipart**：`payload` `{ kind?（announcement 預設／news／match）, deepLink?（`tcrfc://…` 或 http(s)）, audienceTier?（all 預設／fan_club／registered／anonymous）, audienceClubCode?（**用於一般公告，不得用於商業訊息的差別投放**）, audienceTeamCodes?（追蹤球隊，只算推播開啟的追蹤）, scheduledAt?（不可是過去）, removeImage?, content: { zh: { title（必填 ≤120）, body（必填 ≤500）, imageAlt? }, en?（**文案須雙語**，英文缺漏時英文語系的裝置收到繁中）} }` ＋ 選填 `image`。**只有草稿能修改**（待審核請先退回） |
| `DELETE /messages/{id}` → 204 | 同上 | 只有草稿或已取消的批次（409） |
| `POST /estimate` | `app.push.view` | **發送前分眾人數試算**（只回人數，不寫入、不回傳裝置清單）：`{ audienceTier?, audienceClubCode?, audienceTeamCodes? }` → `{ total, breakdown: [ { platform, platformLabel, locale, localeLabel, sent（＝該群人數）, delivered:0, opened:0 } ] }`。**只算「權杖有效且已允許推播」的裝置** |
| `GET /messages/{id}/preview` | 同上 | **預覽（雙語各一）**：`{ zh, en, enEffective（沒有英文時英文裝置實際看到的＝繁中）, deepLink, imageUrl, audienceSummary }` |
| `POST /messages/{id}/test-send` | `app.push.create` | 指定測試裝置試送：`{ deviceInstallIds（1–10 台）}` → `{ configured, sent, failed, unknownDevices, message }`。**傳輸尚未串接時 `configured:false`＋「尚未串接」說明** |
| `POST /messages/{id}/submit` | 同上 | 草稿 → 待覆核（繁中標題與內文必填） |
| `POST /messages/{id}/approve` | `app.push.approve` | `{ expectedAudience }`（見上）→ 已排程；**排程時間沒填或已到就立刻發送**（傳輸未串接時批次停在 `failed`，`failureMessage`＝「推播服務尚未串接…」，**不動任何裝置、不動游標，串接後可重送**） |
| `POST /messages/{id}/return` | 同上 | `{ note（必填）}` 待覆核 → 草稿 |
| `POST /messages/{id}/cancel` | `app.push.create` | 取消尚未送達的批次（草稿／待覆核／已排程）；已發送或發送中 → 409 |
| `POST /messages/{id}/retry` | `app.push.approve` | 失敗／部分送出（已核可）重送：**從游標續送，已處理的裝置不會重送**（權杖失效或暫時性失敗的個別裝置不會重試——已知限制） |
| `POST /dispatch-due` | 同上 | 手動觸發「已核可且排程時間已到」的批次發送 → `{ dispatched }`（背景作業本來就會做） |
| `GET／PUT /rules` | view／`app.push.approve` | **自動推播規則**：`{ matchReminderHours（1–72，預設 2）, membershipExpiryDays（預設 [30,7]，1–5 個 1–365 的不重複天數，大到小排列）, toggles: [ { key, label, enabled } ] }`；`PUT` 只送要改的欄位，`toggles` 是 `{ key: bool }`。九個開關：`match_reminder`／`venue_confirmed`／`match_change`／`match_result`／**`news_published`（預設關閉）**／`membership_expiry`／`membership_activated`／`jersey_status`／`program_status`。⚠️ **只保存規則，實際觸發（掃描賽事與到期、產生批次）屬 App 開發階段** |

**分眾（規劃書 §6.3，刻意不做行為定向）**：會籍層級（`all`／`fan_club`＝持有有效球迷會員會籍／`registered`＝已登入但不是有效球迷會員／`anonymous`＝未登入裝置）、追蹤球隊（`is_push_enabled` 才算）、俱樂部歸屬（追蹤該俱樂部、追蹤其球隊、或持有其會籍；有指定俱樂部時 `fan_club` 只看該俱樂部的會籍）。**分眾一律在 .NET 端解析成裝置清單，不使用 FCM topic**（付費狀態不送進 Google 的索引）。

### M4 裝置 `/api/v1/admin/app/devices`

| 方法 路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET ?platform=&appVersion=&permission=&tokenStatus=&page=&pageSize=` | `app.device.view` | 分頁（最近活躍優先）：`{ id, deviceInstallIdMasked, platform(+Label), osVersion, appVersion, locale("zh"｜"en"), localeLabel, pushPermission(not_determined尚未詢問／granted已允許／denied已拒絕／provisional暫時允許)(+Label), pushTokenStatus(none沒有權杖／valid有效／invalid已失效)(+Label), isMemberBound, firstSeenAt, lastActiveAt }`。**沒有權杖欄位；`isMemberBound` 只說有沒有綁定會員，不給會員資料** |
| `GET /{id}?reveal=true` | view；`reveal=true` 需 `app.device.reveal` | `{ device, deviceInstallId, pushToken, subscriptionCount, revealed }`；不帶 `reveal` 時 `deviceInstallId`／`pushToken` 為 `null`；`reveal=true` 沒權限 → **403**，有權限寫日誌 |
| `GET /stats?platform=&belowVersion=` | view | `{ totalDevices, activeLast7Days, activeLast30Days, invalidTokenCount, byVersion: [ { platform, platformLabel, appVersion, count } ], byPermission: [ { key, label, count } ], devicesBelowVersion }`（`platform`＋`belowVersion` 才有最後一項：**供決定最低支援版本**） |
| `POST /cleanup-invalid-tokens` | `app.device.update` | 把「已失效」的權杖資料清空（狀態改為沒有權杖，裝置列與追蹤偏好保留）→ `{ tokensCleared }` |

### M5 設定、憑證、診斷與連線檢查 `/api/v1/admin/app`

| 方法 路徑 | 權限碼 | 說明 |
|---|---|---|
| `GET／POST／PUT／DELETE /config/flags` | view／`app.config.update` | **功能開關**：`{ id, flagKey, isEnabled, stringValue, platform(all／ios／android), platformLabel, description, updatedAt }`；`POST`／`PUT` 回 `{ value, edgePublish }`（`DELETE` 回 `{ value:true, edgePublish }`）。`flagKey` 格式 `{模組}_{功能}` 小寫蛇形（`ads_enabled`、`map_enabled`…）；同名同平台 409；**建立後名稱與平台範圍不能改（400）**；同名時**單一平台的值優先於 `all`**。**`payment_mode`（`off`／`external`／`inapp`，填在 `stringValue`）是唯一的三態開關**（其他開關填 `stringValue` → 400）；🔴 **只能降級：不得建立為 `inapp`、不得從 `external`／`off` 改成 `inapp`（409）**；已經是 `inapp` 的可以降級回 `external`／`off`（以 `external` 送審、通過後再遠端開 `inapp` 是違規，會被商店下架） |
| `GET／POST／PUT／DELETE /config/credentials`、`POST /config/credentials/{id}/rotate?newExpiresOn=` | `app.credential.view／update`（🔴） | **金鑰與憑證列管——只存列管資訊，絕不存金鑰本身**：`{ id, kind(apns_keyAPNs金鑰／fcm_credentialFCM認證資料／apple_developer_program／google_play_account／maps_api_key／other)(+KindLabel), label, externalRef（Key ID 之類，不是金鑰）, createdOn, lastRotatedOn, expiresOn, rotationPeriodDays, nextDueOn, daysUntilDue, health(ok正常／due_soon即將屆期／overdue已屆期／untracked未設定期限)(+HealthLabel), note }`。**部分金鑰沒有到期日：無 `expiresOn` 時以「上次輪替日（沒有就建立日）＋輪替週期」為基準；已屆期或屆期前 60 天告警**；清單依下次屆期日排序（最急的在前，沒有期限的在最後）。`rotate`：上次輪替日＝今天（台灣日期），可同時給新的屆期日（必須晚於今天），**寫日誌** |
| `GET /config/connection-check` | `app.config.view` | `{ checkedAt, items: [ { key, label, status(ok正常／warning注意／not_configured尚未串接／error異常), statusLabel, message } ] }`，項目：`database`、`push_transport`（APNs／FCM）、`edge_config`（Cloudflare 靜態設定）、`credentials`（有已屆期 → error、60 天內屆期或沒有列管 → warning）、`min_version`（兩個平台都要有最低支援版本）、`diagnostics`（未處理的崩潰回報）。**「尚未串接」與「異常」分開顯示**；連線檢查不會真的送任何推播 |
| `GET /diagnostics?type=&status=&platform=&appVersion=&page=&pageSize=` | `app.diagnostic.view` | 分頁（新→舊）：`{ id, platform(+Label), appVersion, buildNumber, osVersion, occurredAt, reportType(crash崩潰／abnormal_exit異常退出／api_errorAPI錯誤／startup_time啟動耗時／user_report使用者回報)(+Label), metricValue, summary, status(new新回報／reviewing處理中／resolved已解決／ignored略過)(+Label) }`（**清單不含技術細節**） |
| `GET /diagnostics/{id}` | 同上 | `{ report, detail }` |
| `PUT /diagnostics/{id}/status` | `app.diagnostic.update` | `{ status }` → 清單項目形狀 |
| `GET /diagnostics/summary?days=` | view | 近 N 天（預設 7、最多 90）：`{ days, byVersion: [ { platform, appVersion, crashes, activeDevices, devicesWithCrash, crashFreeDevicePercent } ], byType, startupMedianMs, startupP90Ms, apiErrorNote }`。**無崩潰裝置比例是近似值**（只算有註冊裝置識別的回報）；**API 錯誤率需要分母（總請求數），診斷回報只有錯誤次數，這裡只彙總次數**（正式錯誤率由伺服器端請求監控提供） |

### J3 帳號活動概況 `GET /api/v1/admin/security/overview`（`system.audit.view`，僅系統管理員）

回 `{ generatedAt, auditTrailAvailable(**恆為 false**), auditTrailMessage, activeAccounts, lockedAccounts, dormantAccounts, alerts: [ { kind(locked被鎖定／failed_attempts連續登入失敗／dormant久未登入／never_logged_in從未登入), kindLabel, accountId, username, message } ], accounts: [ { id, username, displayName, status, isSuperAdmin, twoFactorEnabled, lastLoginAt, failedAttemptCount, lockedUntil, isLockedNow, daysSinceLastLogin, passwordChangedAt, createdAt } ] }`。
**規則**：只對「啟用中」的帳號告警；連續失敗 ≥3 次、久未登入 ≥90 天、建立超過 7 天從未登入。**不含密碼雜湊與雙因素密鑰。**

**🔴 J3 的判定（為什麼只有這個）**：規劃書 J3 寫「操作稽核記錄（誰在何時對哪筆資料做了什麼）保存 ≥12 個月」「登入紀錄與異常提醒」「每日自動備份」，§4.11／§4.13 也多處要求「匯出寫入稽核日誌」。**但**：`docs/12` §13.1 記著**委託方明文指示「本次資料庫設計不含 log」**（不建 `AuditLog`／`LoginLog`／`ExportLog`／`OperationLog`），且 **2026-09-23 使用者已裁決撤回**先前做出的 `admin_audit_logs`／`admin_login_logs`（見上方「稽核記錄（J3）：已撤回」與 `docs/18` E-44）。所以 D 批**沒有**依規劃書建稽核表——規劃書是規格的真實來源，但 §13.1 是**客戶對範圍的指示**，宣告這一段本期不實作。做了規劃書允許且不需要日誌表的部分：帳號狀態概況與登入異常提醒；備份不是資料表（見 `docs/17` §6）。**「須寫稽核」的操作**（廣告成效匯出、電子報匯出／重新訂閱／刪除、App 憑證輪替、推播核可／取消／重送／試送、更新門檻與維護模式、裝置完整值檢視、失效權杖清理…）照 B／C 批的做法寫 **`SensitiveActionLogger`**（結構化日誌，不含個資本身），這不是稽核表。**待客戶重新確認稽核政策**（見下方「待裁決」第 1 項）。

---

### App 公開端點 `/api/v1/app`（匿名；App 與官網共用同一套 API，B-14）

裝置識別 `deviceInstallId`：8–64 個英數字元或連字號（解除安裝即失效、不跨 App）。**所有錯誤是 `ProblemDetails`（400／404／409），訊息為日常中文，不洩漏內部細節**。

| 方法 路徑 | 說明 |
|---|---|
| `PUT /devices/{deviceInstallId}` | **裝置註冊／更新**：`{ platform("ios"｜"android"), osVersion?, appVersion?（`主.次.修`）, locale?("zh"｜"en"，推播語系以此為準), pushToken?（沒帶＝不動既有權杖）, pushPermission?(not_determined／granted／denied／provisional) }` → `{ deviceInstallId, isNew, pushTokenStatus }`。**權杖加密儲存**；**同一個權杖出現在別的裝置列（重裝、換機）時舊列的權杖失效**（不會重複收到）。限流 |
| `GET／PUT /devices/{deviceInstallId}/subscriptions` | **追蹤與推播訂閱**：`[ { topicType(team球隊代碼／news_category新聞分類代碼／club俱樂部代碼), topicValue, isFollowing, isPushEnabled } ]`。`PUT` `{ items: [...], replaceAll? }`：**只更新列出的項目**（`replaceAll:true` 才移除沒列出的）；對象必須存在（400）；同一批不可重複（400）；最多 100 筆；**「追蹤但不推播」是合法組合**；裝置沒註冊 → 404。回目前完整訂閱清單；`GET` 為 `private, no-store` |
| `GET /config?platform=&appVersion=` | **設定讀取**（`docs/19` §7 第 2 層來源）：`{ generatedAt, ios: <平台設定>, android: <平台設定>, evaluation }`；`<平台設定> = { minSupportedVersion, recommendedVersion, forceUpdateMessage: { zh, en }, recommendUpdateMessage, whatsNew, maintenance: { enabled, message: { zh, en } }, featureFlags: { "ads_enabled": true, … } }`；帶 `platform` 與 `appVersion` 才有 `evaluation: { maintenance, updateRequired, updateRecommended }`（App 也可自己用文件內容判斷，兩者一致）。**版本用語意化比較，建置號不參與**（`1.10.0` > `1.9.0`）。可邊緣快取 |
| `GET /layout?lang=&deviceInstallId=` | **內容編排**：`{ generatedAt, homeSections: [ { code, label, icon, deepLink, webUrl } ]（只含啟用的、依排序）, quickEntries, moreItems, announcements: [ { id, message, linkUrl, endsAt } ]（期間內且啟用；**目標對象非「全部」的公告只有帶 `deviceInstallId` 才依會籍層級與俱樂部篩選**）, deepLinks: [ { code, label, appLink, webUrl, requiresLogin } ] }` |
| `GET /notifications?lang=&deviceInstallId=` | **通知中心**：已送出（`sent`／`partial`）的推播，**保留 90 天**，最多 50 則，新→舊：`{ id, title, body, imageUrl, deepLink, sentAt }`。**沒帶裝置識別只列對所有人發送的訊息；帶了就列這台裝置是對象的**。標題與內文依 `lang`，缺漏回退繁中 |
| `POST /push/{messageId}/opened` → 204 | **通知被開啟**：`{ deviceInstallId }`。**只累加該批次（平台×語系）的彙總數字，不記錄是哪台裝置**；因此同一台重複回報無法去重（已知限制，靠限流）。批次不存在或尚未送出／裝置未註冊 → 404。限流 |
| `GET /ads/{slotCode}?lang=&deviceInstallId=&theme=` | **廣告投放**：`{ slotCode, isFallback, disclosureLabel("廣告"｜"Ad"，**每個版位必須顯示的揭露標示**), sessionImpressionCap, items: [ { creativeId, campaignId, isFallback, imageUrl, imageWidth, imageHeight, videoUrl, altText, title, ctaText, clickUrl, theme } ] }`。**投放規則**：只投「投放中且在起訖時間內」的檔期、只投「已通過審核且未暫停」的素材（依 `lang` 取素材，沒有就回退繁中）、每日曝光上限、**每人（每裝置）每日頻次上限**（帶 `deviceInstallId` 才套用）、**曝光保證型 pacing**（剩餘量平均分配到剩餘天數，今天的份額用完先停；達到目標即停）、同版位依**權重加權隨機（不重複抽取，最多到版位的輪播張數上限）**；**沒有可投放的就回備援素材**（`isFallback:true`、`creativeId:null`——**備援不計曝光**；版位沒設備援圖則 `items:[]`）。版位不存在或已停用 → 404 |
| `POST /ads/events` | **廣告事件批次上報**：`{ batchId?, deviceInstallId, platform, appVersion?, locale?, events: [ { type("impression"｜"click"), creativeId, occurredAt（含時區的 ISO 8601，**以發生時間記錄**，離線暫存的事件帶原始時間）, presentationId?（每次素材裝載進版位的 UUID，同一個只算一次曝光）} ]（1–200 筆）}` → `{ accepted, duplicates, rejected: [ { index, reason(invalid／too_old超過24小時／future／unknown_creative／not_serving) } ] }`。**伺服器端去重**：曝光以 `presentationId`（沒有就以素材＋裝置＋秒）、**點擊同裝置同素材 5 秒內只計 1 次**；同一批整份重送全部算重複（冪等）；檔期與版位由素材推導（不信任 App 傳來的）；草稿與作廢檔期的素材不收。**不存 `member_id`、完整 IP、定位座標、廣告識別碼。** 限流 |
| `POST /diagnostics` → 202 | **診斷與錯誤回報**：`{ reports: [ { deviceInstallId?, platform, appVersion, buildNumber?, osVersion?, occurredAt, type(crash／abnormal_exit／api_error／startup_time／user_report), metricValue?, summary?, detail? } ]（1–50 筆）}` → `{ accepted }`。**不得存個資**：自由文字中的 Email 與 8 位以上數字入庫前遮成 `[已遮蔽]`；發生時間超過 7 天前 → 400；保存 90 天。限流 |

**不在這裡的 App 端點**（規劃書 §9.2 有列，屬 AP-2／AP-3）：賽事／新聞／球隊／特約店家／課程／夥伴／FAQ 的列表與單筆（多數已是既有公開端點，`/api/v1/{club}/…`）、會員註冊登入與權杖續期、會員卡、會籍與付款、球衣、我的報名、抽獎資訊、`俱樂部`／`賽事系列` 列表。**會員綁定裝置（`app_devices.member_id`）由會員登入（AP-3）寫入，本批不提供。**

---

### 表與種子變更（D 批）

- **綱要**（`db/club-schema.sql` 的 **4.13**＋EF migration `AlignSchemaD1`，同步 `docs/12`／`12a`／`12b` §16／`12c`）：**新增 25 張表**（廣告 8、App 營運 8、M2 內容編排 6、M5 設定 3）；`newsletter_subscribers.status` 收斂為 `NOT NULL DEFAULT 'subscribed'` 並加 `unsubscribed_at`。**表 162 → 187**。本機庫已對 `tcrfc_club` 套用。
- **migration 歷史對齊**：本機庫是由 `club-schema.sql` 建的，`__EFMigrationsHistory` 表原本不存在（C 批的 `AlignSchemaC1` 因此顯示待套用）。已逐欄核對本機庫與 EF 模型一致（新增 `EfModelMatchesDatabaseTests`，資料庫與模型的表／欄位雙向比對，長期守著），建立歷史表並補登全部 18 筆 migration（`InitialBaseline` … `AlignSchemaC1`、`AlignSchemaD1`）；`dotnet ef database update` 回報沒有待套用、`has-pending-model-changes` 綠燈。⚠️ **不要在這個庫執行 `database update` 來「補跑」舊 migration**——表已存在會失敗。
- **權限**：38 個新權限碼與角色指派（見上表）；**另補 `member.draw.view` 給公關／媒體**（C 批畫面回報：原本該角色只有公布稿權限，打不開任何抽獎活動；姓名與收件資訊仍一律遮罩）。
- **種子**（`db/seed/backoffice_seed.py` §55–59，全部【測試】虛構）：電子報名單 5 筆（`example.com`，tcrfc 3 訂閱＋1 退訂、bw 1）；深連結 9 條（規劃書 §2.3 的 8 條＋會籍升級）、**首頁九個區塊**、快捷入口 2、「更多」分頁 8、公告條 1；功能開關 4（`ads_enabled`／`map_enabled`／`biometric_unlock_enabled`／`payment_mode=external`）；版本 3（iOS 0.9.0 已上架且為最低支援版本、iOS 1.0.0 測試中、Android 0.9.0）；憑證列管 3；版位 2（`home_top`、`home_mid`）、廣告主 2、檔期 2（1 個已結束的曝光保證檔期含 **14 天 × 2 平台的示範日聚合**、1 個草稿）；示範裝置 5 台（**都沒有推播權杖**，所以不會被誤送）、推播 2 則（1 已發送含虛構統計、1 草稿）、診斷回報 3。**沒有圖片**（素材與備援 `image_key` 為 NULL）。**跑法**：`set -a; source .env; set +a; ./db/seed/apply-seed.sh`（冪等）。

### 測試（`Tcrfc.Api.Tests`，新增／更動）

`AdminNewsletterTests`（6）、`AdminAdsTests`（8）、`AdminAdCreativeUploadTests`（2，真實 Azurite）、`AppPublicTests`（10）、`AdminAppTests`（13）、`AdminSecurityTests`（2）、`EfModelMatchesDatabaseTests`（1）；共用工具 `AppTestSupport.cs`（**測試資料一律用 `ZZTEST`／`test-dev-`／`zz-test-` 前綴並在 `finally` 清掉；會動到共用設定的測試用 `AppTest.SnapshotAppStateAsync` 拍照還原**——版本旗標、維護模式、功能開關、憑證、自動推播規則；`RecordingPushTransport` 模擬「已串接」的推播傳輸）。整合測試主機以 `AppTestEnvironment` 停用 App 背景作業並把 App 公開端點限流調寬。既有測試更動：`AdminDrawsTests`（公關／媒體現在可檢視遮罩名單）、`AdminShopCatalogTests`（貨號撞號措辭）、`AppPublicTests` 內對 `AppLayoutItemDto` 的欄位名。

### 待裁決（規劃書沒寫或與既有指示衝突，本批先採最保守做法）

1. **稽核日誌（J3）**：規劃書要求可查閱的操作稽核（≥12 個月）、登入紀錄、匯出稽核，但委託方指示不建日誌表（§13.1）且使用者 2026-09-23 裁決撤回。本批只做帳號活動概況與登入異常提醒，「須寫稽核」的操作寫結構化日誌（正式環境進 Application Insights）。**若客戶要求可查詢的稽核紀錄，需要建 `AuditLog`／`LoginLog`／`ExportLog` 並改寫 `SensitiveActionLogger`（呼叫端不用改）**——需要客戶先重新確認政策，並先改 `docs/12` §13.1。
2. **資料備份（J3）**：規劃書「每日自動備份，可手動還原點」是基礎設施設定。後台**沒有備份與還原功能**（見 `docs/17` §6「備份與還原（J3）」：Azure SQL 內建備份的保留期與 Basic 層限制、建議的補強）。
3. **合約金額的可見角色**：規劃書 §11 寫「廣告合約金額僅商務／贊助與**財務**可見」，但專案的十個角色沒有財務。本批給商務／贊助與系統管理員（`ad.contract.view／update`）；若要有財務角色須先建角色（J2）。
4. **電子報公開訂閱／退訂**：G3 只做後台；官網頁尾訂閱表單與退訂連結（含 token）是前台工作，且全系統沒有寄信通路，退訂連結由 EDM 平台提供。
5. **推播雙人覆核只有一位系統管理員時**：核可者不得是建立者，意味著公關建立、系統管理員核可。若只剩一個系統管理員帳號又要自己建立推播，會無法核可（刻意如此，規劃書 §11「雙人覆核」）。
6. **廣告素材的「素材審核」與「檔期審核」是兩層**：規劃書 §8.8 寫「素材審核：待審／通過／退回」，§7.4 寫檔期狀態機含「待審核」與「審核者」。本批：素材各自審核（`ad.campaign.review`），檔期核可（同一權限）需至少一個已通過素材；沒有另設「審核者」角色。
7. **`payment_mode` 的降級方向**：`off < external < inapp`，`off→external` 視為合法（重新啟用外開瀏覽器），只有「升到 `inapp`」被禁止。
8. **廣告曝光的「台灣日期」邊界**：每日聚合、報表日期、每日上限與 pacing 的「今天」都以台灣當地日期（UTC+8）切日，與 K5／商店報表一致。

### 已知限制

- **推播傳輸、EDM、Cloudflare 靜態設定尚未串接**（介面＋預設「尚未串接」實作）；APNs／FCM 金鑰未建立。批次核可後停在 `failed` 並保留，串接後按「重送」。**會員條款完成推播蒐集告知（App 規劃書 §16.2 第 12 項）前不得啟用推播**（§6.7）——本批的 App 端點沒有做這道閘門（由 App 端與傳輸實作把關）。
- **自動推播只有規則設定**，沒有觸發程式（賽事提醒、到期提醒、新聞發布）。
- **推播統計無個人層級**：不逐裝置記錄投遞結果，所以「重送」只能從游標續送、個別失敗的裝置不會重試；開啟回報無法去重。
- **`uniqueDevices` 跨日彙總是裝置日**；原始事件只留 90 天，超過 90 天仍未聚合的事件不會重算。
- **廣告報表沒有 PDF 匯出**（規劃書 §7.7 寫 CSV 與 PDF）；CSV 已有。
- **通知中心只有「已送出的一般推播」**；對單一會員的推播（會籍開通、到期）與其收件匣屬 AP-3 之後。
- **廣告投放沒有做每日跨日的 `delivered_today` 主動歸零**：以 `delivered_on` 日期比對，讀取時視同 0。
- 診斷回報的 `deviceInstallId` 目前不強制要求該裝置已註冊；崩潰裝置比例只算有帶識別的回報。
- 電子報名單沒有匯入功能（CSV 匯入）；沒有批次退訂。

---

## E 批：會員前台（S2-11）與文化公開端點（S3-2）（2026-10-01，`backend-engineer`）

主站規劃書 §3.14 會員中心、§3.8 08 文化（8.1 漫畫、8.2 球迷會、8.4 特約店家）、§4.11 K；App 規劃書 §4 會員與認證、§5.3／§5.4／§9.7 付款訂單與冪等開通、§9.2／§9.3；對應 `STATUS.md` 的 `S2-11`、`S3-2`（並為 `AP-3` 備好會員端點與訂單／開通邏輯）。
沿用既有通則：路徑 `/api/v1/{club}/…`、錯誤一律 `ProblemDetails`、**時間戳 UTC 帶 `Z`**、`?lang=zh|en`（英文缺漏回退繁中）、公開寫入一律依 IP 限流。**給前端的人：只讀這一節就能串接，不需要看程式碼。**

### E 批通則（新增）

| 項目 | 規定 |
|---|---|
| **錯誤格式** | `ProblemDetails`（`title`／`status`／`detail`）＋**`code`**（機器可讀，前端依它決定流程；`detail` 是給人看的日常中文，可直接顯示）。鎖定時另有 `lockedUntil`（UTC）。下表「代碼」欄是全部會出現的 `code`。 |
| **會員權杖** | **存取權杖**：JWT、15 分鐘，`Authorization: Bearer …`。**與後台權杖完全分開**：獨立驗證機制（`MemberBearer`）、獨立 issuer／audience（`tcrfc-member`／`tcrfc-member-api`）、獨立簽章金鑰（`JWT_SIGNING_KEY_MEMBER`；**沒設時由 `JWT_SIGNING_KEY_CLUB` 以 HKDF 衍生**，密碼學上互相獨立，所以本機與既有測試不必多設環境變數）。拿後台權杖打會員端點、反之亦然，一律 **401**（有測試）。claims 只有 `sub`（會員 id）與 `jti`，**每個受保護端點仍即時查庫確認帳號啟用**——停用／刪除的帳號立刻失效，不必等 15 分鐘。 |
| **更新權杖** | 不透明亂數、**只存 SHA-256 雜湊**（新表 `member_refresh_tokens`）、**每次使用即輪替**、**舊權杖被重放＝外流，撤銷該會員全部更新權杖**；變更密碼／重設密碼／登出全部裝置／刪除帳號也撤銷全部。「記住我」＝30 天（持久 Cookie），否則 24 小時（工作階段 Cookie）。⚠️ **瀏覽器同時開兩個分頁各自 refresh 會互相踢掉**——前端要 single-flight（同 `docs/19` §3）。App 的更新權杖鏈仍掛在 `app_devices`（App 規劃書 §10.1，AP-3 才做），**不使用本表**。 |
| **權杖遞送方式** | `tokenDelivery`：`cookie`（**預設，瀏覽器**）→ 更新權杖放 `__Host-tcrfc-member-rt` Cookie（`HttpOnly; Secure; SameSite=None; Path=/`，**不設 Domain**），回應本文**沒有**更新權杖，前端 `fetch` 要 `credentials: 'include'`；`body`（**App、Nuxt 伺服器端代理**）→ 回應本文有 `refreshToken`／`refreshTokenExpiresAt`，**不設 Cookie**，呼叫端自己存安全儲存區，續期時 `POST /auth/refresh` 本文帶 `{ "refreshToken": "…" }`。 |
| **只能讀寫本人資料** | 所有會員端點的查詢條件**一律來自權杖裡的會員 id**，不接受路由或本文傳會員 id（App 規劃書 §9.3 硬規則）；別人的會籍／卡／訂單／球衣一律 **404**。 |
| **限流** | `POST /auth/*`（登入、註冊、驗證信、忘記／重設／變更密碼、LINE）與刪帳號、內部開通：每 IP **5 分鐘 30 次**（`public-member-auth`，`MEMBER_AUTH_RATE_LIMIT_PERMITS` 可覆寫）；其餘會員寫入（個人資料、重產 QR、球衣、建立／請款／確認／取消訂單、活動報名／取消、卡片驗證頁）：每 IP **每分鐘 60 次**（`public-member-write`，`MEMBER_WRITE_RATE_LIMIT_PERMITS`）；漫畫閱讀數沿用 `public-light-interaction`。超過回 **429**。登入另有**帳號鎖定**：連續 5 次失敗鎖 15 分鐘（423，`code: account_locked`），鎖定中連正確密碼也不放行，成功登入歸零。 |
| **不洩漏帳號是否存在** | 登入失敗（查無此人／密碼錯／已刪除）**同一句訊息、同樣耗時**（查無此人也跑一次 Argon2id）；忘記密碼、重寄驗證信**一律 202**；唯一例外是**註冊**（Email 已註冊回 409，使用者需要知道該去登入——靠限流壓住枚舉，見待裁決 2）。 |
| **日期與標籤** | 日期欄位（`startOn`／`endOn`／`validUntil`…）是台灣當地日期 `yyyy-MM-dd`；有 `…Label` 的欄位是日常中文（`?lang=en` 時英文）直接顯示。會籍「有效與否」**以到期日為準**（`status` 欄仍是 `active` 但到期日已過 → 回 `expired`）。 |

### 會員（帳號）`/api/v1/member/…`

| 方法 路徑 | 登入 | 說明 |
|---|:-:|---|
| `POST /auth/register` | — | `{ club, email, password, name, phone?, birthOn?, lang? }` → **201** `{ memberNo, emailVerificationRequired: true, emailSent }`。建立**未驗證**帳號（`signup_source=web`）並寄驗證信；`emailSent=false` 代表寄信服務尚未串接（正式環境目前如此，前端要如實告知）。`club` 是註冊的站台（驗證後在該俱樂部成為一般會員）。Email 一律轉小寫；密碼 8–128 字元、須含英文字母與數字、不得是常見弱密碼（`weak_password`）；Email 已註冊 → 409 `email_taken`。 |
| `POST /auth/verify-email` | — | `{ token }`（信件連結的 `token`）→ 200 `{ verified: true, club }`。**冪等**；順便為該俱樂部建立免費（一般會員）會籍與第一張會員卡。無效／過期／用途不符 → 400 `token_invalid`。權杖 24 小時。 |
| `POST /auth/resend-verification` | — | `{ email, club, lang? }` → **一律 202**。 |
| `POST /auth/login` | — | `{ email, password, rememberMe?, tokenDelivery? }` → 200 **`MemberSession`**：`{ accessToken, accessTokenExpiresAt, refreshToken?, refreshTokenExpiresAt?, member: { memberNo, name, emailVerified, hasPassword, lineBound } }`。401 `invalid_credentials`；403 `email_not_verified`（密碼正確才告知）／`account_suspended`；423 `account_locked`。**Email 驗證前不能用密碼登入。** |
| `POST /auth/refresh` | — | 本文 `{ refreshToken? }` 或 Cookie → 200 `MemberSession`（新的更新權杖）；401 `session_expired`（同時清掉 Cookie）。 |
| `POST /auth/logout` | — | 撤銷目前的更新權杖並清 Cookie → 204。 |
| `POST /auth/logout-all` | ✅ | 撤銷全部裝置 → 204。 |
| `POST /auth/forgot-password` | — | `{ email, club, lang? }` → **一律 202**。帳號存在才寄信；連結 1 小時有效。 |
| `POST /auth/reset-password` | — | `{ token, newPassword }` → 204。**連結只能用一次**（權杖內含目前密碼雜湊的指紋，密碼一改就失效）；成功後**全部裝置登出**、解除鎖定，並**順便完成 Email 驗證**（能收到信＝控制信箱）。 |
| `POST /auth/change-password` | ✅ | `{ currentPassword?, newPassword, tokenDelivery? }` → 200 `MemberSession`（**撤銷全部其他裝置**，替目前這台發新權杖）。已設定密碼者 `currentPassword` 必填（錯 → 401）；**LINE 註冊、尚未設定密碼者留空即設定第一組密碼**。 |
| `GET /me` | ✅ | 個人資料 `{ memberNo, name, email, phone, birthOn, locale("zh"｜"en"｜null), emailVerified, hasPassword, lineBound, signupSource(+Label), createdAt }`。**本人看自己的資料不遮罩**；永遠不含密碼雜湊、LINE 識別碼、權杖。 |
| `PUT /me` | ✅ | `{ name, phone?, birthOn?, locale? }` 整份取代（省略＝清除）。**Email 不可由此修改**（登入鍵；本批不提供換 Email）。 |
| `DELETE /me` | ✅ | `{ password }`（沒有密碼的 LINE 帳號改填 `{ confirm: "DELETE" }`）→ 204。**欄位清除、不是刪列**：保留會員編號與遮罩姓名，Email 改成不可寄送的佔位值、電話／生日／LINE／密碼清除；**會員卡全部作廢**、更新權杖全撤、App 裝置解除綁定、球衣收件個資清除、未完成訂單取消。**保留**會籍列與付款紀錄（稅法與會計法規優先於刪除請求）與已鎖定的抽獎名單快照。 |
| `POST /auth/line/authorize` | bind 才要 | `{ club, mode: "login"｜"bind", redirectUri? }` → `{ authorizeUrl, state }`。`redirectUri` 必須在 `LINE_LOGIN_REDIRECT_URIS` 白名單（省略＝第一個）。**前端要把 `state` 存起來（sessionStorage），LINE 導回時與網址上的 `state` 比對，不符就丟棄**（防登入 CSRF）。憑證未設定 → **503** `line_not_configured`。 |
| `POST /auth/line/callback` | bind 才要 | `{ code, state, tokenDelivery? }`（前端從導回網址取 `code`、`state`）→ `{ status: "logged_in", session }`／`{ status: "bound" }`／`{ status: "signup_required", ticket, displayName, suggestedEmail }`。授權碼經 LINE 驗證 id_token 與 nonce。**找不到會員時不自動併入同 Email 的既有帳號**（LINE 的 Email 不保證驗證過，見待裁決 3）。綁定：LINE 已綁別人 → 409 `line_in_use`；自己已綁 → 409 `line_already_bound`；state 內的會員必須就是目前登入的人。 |
| `POST /auth/line/complete` | — | `{ club, ticket, email, name?, phone?, birthOn?, lang?, tokenDelivery? }` → 200 `MemberSession`。補 Email 完成 LINE 註冊（`signup_source=line`，**沒有密碼**）；直接成為該俱樂部一般會員；仍寄 Email 驗證信。Email 已有帳號 → 409 `email_taken`（引導先用 Email 登入、再到設定綁定 LINE）。票據 15 分鐘。 |
| `DELETE /me/line` | ✅ | 解除 LINE 綁定 → 204。**至少保留一種登入方式**：沒設定過密碼 → 409 `password_required`。 |

**信件連結（前端要實作的兩個頁面）**：`{站台網址}/{zh|en}/member/verify-email?token=…` 與 `{站台網址}/{zh|en}/member/reset-password?token=…`。站台網址預設 `https://{clubs.domain}`，本機用 `MEMBER_EMAIL_LINK_BASE_URL`（如 `http://localhost:3000`）覆寫。本機開發信件寫成文字檔（`EMAIL_OUTBOX_PATH`，預設 `{暫存目錄}/tcrfc-email-outbox`），**點檔案裡的連結即可走完驗證**。

### 會員中心 `/api/v1/member/…`（帶會員權杖；`{club}` 路由寫入端點另需 Email 已驗證）

| 方法 路徑 | 說明 |
|---|---|
| `GET /member/memberships?lang=` | **我的會籍**：`{ memberships: [ { id, club:{code,name,logoLightUrl,logoDarkUrl,brandColor,brandSecondaryColor}, seasonCode, tier(+Label), status(+Label), startOn, endOn, planCode, planName, cardQuota, jerseyQuota, isCurrentSeason, renewalDue, pendingOrder:{orderNo,status,statusLabel}｜null, cards:[…] } ], joinableClubs: [ {code,name} ] }`。**逐俱樂部、一人每俱樂部一份**；`renewalDue`＝到期前 30 天內且仍有效（顯示續會提示）；`pendingOrder` 有值＝升級申請待確認（網頁的「待確認」就是它）；沒有目前球季會籍的俱樂部在 `joinableClubs`（顯示加入入口，不隱藏）。 |
| `POST /{club}/member/memberships/join` | 加入這個俱樂部（免費一般會員，建立會籍與第一張卡），冪等。該俱樂部沒有開放的球季 → 409 `season_not_available`（**藍鯨目前就是這樣**，其 2025 球季已結束）。 |
| `GET /member/cards?lang=` | **電子會員卡**（每份會籍一張、家庭方案多張，已展平）：`{ id, membershipId, club:{…品牌}, memberNo, holderName, tier(+Label), validUntil, status("valid"｜"expired"｜"revoked")(+Label), isValid, token, reissueCount, issuedAt }`。`token` 是這張卡的 QR 憑證，**QR 內容＝`{官網網址}/m/{token}`，在裝置端／前端組出**；只有本人看得到。 |
| `POST /member/cards/{cardId}/regenerate` | **重新產生 QR**：同一張卡換 token、`reissueCount` +1，**舊 token 立即失效**。別人的卡 404；已停用的卡 409 `card_revoked`。 |
| `GET /m/{token}?lang=` | **電子會員卡公開驗證頁（不需登入）**。回應**只有**：`nameInitial`（姓名首字）、`memberNo`、`tier`／`tierLabel`、`status`（`valid`｜`expired`）／`statusLabel`。🔴 **不得新增欄位**（尤其「適用球隊」，主站 §3.14／App §3.6）；查無、已撤銷、帳號停用或刪除一律 **404**（不分原因）；**`Cache-Control: no-store`、直接查庫不讀快取**（docs/14）。 |
| `GET /member/jerseys?lang=` | **球衣登記概況**：每份付費會籍一組 `{ membershipId, clubCode, clubName, seasonCode, quota, used, canRegister, items:[{ id, recipientName, phone, size, deliveryMethod(+Label), address, status(+Label), shippedOn, receivedOn, editable }] }`。沒有球衣額度也沒有登記的會籍不列。 |
| `POST /{club}/member/jerseys` | `{ membershipId, recipientName, size, deliveryMethod: "ship"｜"pickup", phone?, address? }` → 201。**只有有效的球迷會員會籍**（409 `not_fan_club`）；**件數受方案 `jerseyQuota` 限制**（409 `jersey_quota_reached`，並行也不會超收）；寄送必須有電話與地址；尺寸轉大寫。 |
| `PUT /{club}/member/jerseys/{id}` | 同上欄位（不含 `membershipId`）；**只有「待處理」能改**，已寄出／已領取 → 409 `jersey_locked`。 |
| `GET /member/registrations` | **我的課程／試訓報名**（行動 App 的「我的報名」用，網頁前台不做歸戶，主站 §3.14）。報名時**帶著會員權杖**才會記到會員（見下方「既有端點的異動」）。 |

### 會籍付款訂單（App 規劃書 §5.3／§5.4／§9.7）

> 🔴 **範圍提醒**：主站 §3.14 寫明「會籍的網頁站內結帳仍不做，要不要改走商店結帳是尚未拍板的修訂」。本批的訂單端點是 **App 規劃書 Phase B 明文要求先做完的訂單與冪等開通**（`docs/19` §10），**網頁前端在拍板前只應使用「升級申請」（建立訂單後停在 `created`，不呼叫 `pay`／`confirm`）**；`canPayOnline` 在正式環境因金流未串接（B-10）**恆為 false**。

| 方法 路徑 | 說明 |
|---|---|
| `POST /{club}/member/membership-orders` | 標頭 **`Idempotency-Key`（必填，8–64 字元，英數與 `_-:.`）**；本文 `{ planCode }`——**沒有金額欄位，金額一律由伺服器依方案重算**（夾帶的金額被忽略）。→ **201** 新訂單／**200** 同一冪等鍵同內容重送（回原訂單，並行也只成立一張）。409：`idempotency_key_reused`（同鍵不同內容）、`open_order_exists`（同方案已有未完成訂單）、`already_fan_club`（本球季已是球迷會員）、`plan_closed`（方案期間已結束）；404 `plan_not_found`；400 `plan_free`。需 Email 已驗證。 |
| `GET /member/membership-orders?lang=`／`GET …/{orderNo}` | 我的訂單（最近 50 筆）／單筆。待付款超過 15 分鐘**讀到時換算**為 `expired`。 |
| `POST /member/membership-orders/{orderNo}/pay` | 向金流方請款 → `pending_payment`、`paymentUrl`（把使用者導過去）、`expiresAt`（15 分鐘）；重複呼叫回同一個網址。**金流尚未串接 → 503 `payment_not_configured`**（訂單維持 `created`，不會被誤標成待付款）；金流方失敗退回 `created` 可重試。 |
| `POST /member/membership-orders/{orderNo}/confirm` | `{ transactionId }`（使用者從 LINE Pay 導回後，前端帶回交易識別；`confirmUrlType` 採 CLIENT，`docs/17` §3）。**由伺服器向金流方確認**（金額用訂單上的值），成功才標 `paid` 並**立即冪等開通**→ 200 訂單（`activated`）。`transaction_mismatch`（400）、`order_expired`／`payment_failed`／`order_not_pending`（409）；已 `paid`／`activated` 重複確認 → 200（冪等）。**用戶端無法自己宣稱付款成功。** |
| `POST /member/membership-orders/{orderNo}/cancel` | `created`／`pending_payment` → `cancelled`（冪等）。 |
| `POST /api/membership/activate` | **伺服器內部**（憑證保護，**不對外開放**）：標頭 `X-Internal-Credential`＝`MEMBERSHIP_ACTIVATE_CREDENTIAL`（≥32 字元；**沒設定或太短 → 整個端點 503**；錯 → 401，固定時間比對）；本文 `{ orderNo }`。冪等：已開通回 `{ …, alreadyActivated: true }`；訂單未付款 409 `order_not_paid`。 |

**訂單狀態** `status`：`created`（待確認）→ `pending_payment`（待付款）→ `paid`（已付款）→ `activated`（已開通）；另有 `expired`（已逾時）、`activation_failed`（開通失敗，客服處理中）、`cancelled`、`refunded`（僅後台人工）。

**開通的三道冪等防線**：① 以條件式 `UPDATE … paid → activated` 搶佔，**並以 `sp_getapplock` 讓同一張訂單一次只有一個交易動資料**（並行測試實測會死結，見 `docs/18` E-97）；② 已開通再呼叫直接回原結果，不重複延長會籍、不重複寫付款紀錄；③ `membership_payments.membership_order_id` 篩選唯一索引（一張訂單最多一筆付款紀錄）。**不得靜默失敗**：已付款但開通出錯時，訂單標 `activation_failed`（附原因）、寫 **`Critical` 日誌**（正式環境會觸發 Application Insights 告警）、回 409 `activation_failed`。開通成功寄「會籍開通確認」信（寄信失敗不影響開通）。
**與後台 K2 的銜接**：客服在 K2 開通同一位會員同一方案時，會員先前送出、仍是 `created` 的升級申請**一併結案**（`activation_source = admin`），會員中心的狀態才會從「待確認」變「已開通」。已向金流方請款中的訂單（`pending_payment`）不動，避免重複扣款。

### 公開讀取（不需登入）

| 方法 路徑 | 說明 |
|---|---|
| `GET /{club}/membership/plans?lang=` | 會籍方案（8.2 Membership Plans、升級頁）：`{ code, name, benefitNote, fee, cardQuota, jerseyQuota, midSeasonRule, seasonCode, startsOn, endsOn }`。只列**已上架且球季未結束**的方案。藍鯨目前沒有現行方案（空陣列）。 |
| `GET /{club}/membership/benefits?planCode=&lang=` | **權益對照表**（免費 vs 付費逐條對照，一份資料多處使用）：`{ planCode, planName, groups:[{ group, groupLabel, items:[{ name, description, freeValue, paidValue }] }] }`，分組依序 `member_card`／`store_discount`／`jersey`／`event`，只列上架條目。省略 `planCode`＝最早的現行方案（沒有 → 空表）；方案不存在 404。 |
| `GET /{club}/partner-stores?category=&region=&tier=&lang=` | **特約店家清單**（8.4）：本俱樂部專屬＋兩隊共同（`isShared`）、**已上架且在合作期間內**。`{ slug, name, category, region, address, lat, lng, phone, businessHours, offerContent, applicableTier("all"｜"fan_club")(+Label), mapUrl, websiteUrl, imageUrl, isShared }`；`lat`／`lng` 給 App 的附近地圖（距離計算在裝置端）。 |
| `GET /{club}/partner-stores/filters` | 目前有上架店家的 `categories`／`regions`（篩選項目）。 |
| `GET /{club}/partner-stores/{slug}?lang=` | 店家詳情；草稿、合作期間外、別隊店家 404。 |

### 08 文化

**8.1 漫畫 `/api/v1/{club}/comic/…`**（**全部免費、不需登入、沒有付費牆欄位**；🔴 **藍鯨一律 403**「台中藍鯨不設漫畫」，比照後台 F1；種子的 3 集都是草稿，所以公開列表目前是空的）：

| 方法 路徑 | 說明 |
|---|---|
| `GET /comic/about` | 企劃說明（世界觀）`{ title, body }`。 |
| `GET /comic/characters` | 角色卡牆 `{ id, name, description, imageUrl, imageThumbUrl, playerId }`（`playerId`＝原型球員，可為空）。 |
| `GET /comic/episodes` | 集數列表（**新→舊**）`{ episodeNo, title, coverUrl, coverThumbUrl, publishedOn, isLatest, pageCount }`。只含**已發布且發布日不晚於今天**的集數；**`isLatest` 讀取時即時判定**（可見集數中最大者），不信任資料庫欄位——到了發布日不必等後台重算。 |
| `GET /comic/episodes/latest` | 最新集數（首頁同步曝光、8.1 置頂區塊）；沒有可見集數 404。 |
| `GET /comic/episodes/{episodeNo}` | **線上閱讀器資料**：`{ episodeNo, title, coverUrl, publishedOn, isLatest, pages:[{ pageNo, imageUrl, imageThumbUrl, width, height }], previousEpisodeNo, nextEpisodeNo }`（分頁／捲動雙模式由前端決定）。草稿與未到發布日 404。 |
| `POST /comic/episodes/{episodeNo}/views` | 閱讀數＋1（資料庫端遞增），固定 204；前端閱讀器載入後另外呼叫一次。 |

**8.2 球迷會活動 `/api/v1/{club}/fan-events/…`**：

| 方法 路徑 | 說明 |
|---|---|
| `GET /fan-events?phase=upcoming｜past` | 活動列表：`{ slug, name, location, startsAt, endsAt, registrationDeadlineAt, capacity, spotsLeft, isPaidMembersOnly, isRegistrationOpen, isFull, phase, coverUrl, coverThumbUrl }`。`phase=past` 即「活動回顧」（新→舊）；省略＝全部。`spotsLeft`／`capacity` 為 null＝不限名額；**額滿時 `isRegistrationOpen` 仍為 true（報名會進候補）**，用 `isFull` 顯示「候補」。 |
| `GET /fan-events/{slug}` | `{ event:{…同上}, description, venueName, images:[…活動回顧圖集], articles:[{slug,title}]（關聯文章，只列已發布）, myRegistration:{status,statusLabel}｜null }`。**帶會員權杖時才有 `myRegistration`**（匿名永遠 null）。 |
| `POST /fan-events/{slug}/registrations` | `{ applicantName?, phone?, email?, note? }` → **201** `{ status("registered"｜"waitlist"), statusLabel, isWaitlisted }`。**會員**（帶權杖）只需 `note`，報名記到會員、不另存個資；**非會員**必須有 `applicantName` 與（`phone` 或 `email`）。**限付費會員的活動**：沒登入 → 401 `login_required`、沒有有效球迷會員會籍 → 403 `fan_club_required`。**名額已滿自動進候補**（交易內鎖活動列再數名額，並行不超收）；重複報名 409 `already_registered`（會員依 id，非會員依 Email 或電話）；截止或已開始 409 `registration_closed`。回應**不含任何個資**。限流 `public-member-write`。 |
| `DELETE /fan-events/{slug}/registrations/me` | 會員取消自己的報名（已報名或候補）→ 204；沒有有效報名 404。**取消已報名者不會自動遞補候補者**（同後台 F2，由客服人工處理）。 |

### 既有端點的異動

- `POST /api/v1/{club}/programs/sessions/{sessionId}/registrations`：**多了選用的會員權杖**——帶有效權杖才把 `registrations.member_id` 記為該會員（行動 App 的「我的報名」；App 規劃書 §9.2「會員身分時寫入 member_id」）。**沒帶（網頁前台）或權杖無效，行為與先前完全相同**，不報錯。
- 後台 `POST /api/v1/admin/{club}/memberships/activate`（K2 手動開通）：開通時一併結案會員的 `created` 升級申請（見上）。其餘後台端點、回應形狀**一行未改**。

### 資料庫綱要異動（`db/club-schema.sql`、migration `AlignSchemaE2`）

先改 `docs/12`（第 47 點）、`docs/12b`（§6.3、§6.5c）再改 DDL 與 EF；本機 `tcrfc_club` 已套用（`dotnet ef migrations has-pending-model-changes` 無待處理變更，`EfModelMatchesDatabaseTests` 綠燈，`db/club-schema.sql` 用 `SET PARSEONLY ON` 解析驗證）。**187 → 189 張**：
`members` 加 `failed_attempt_count`／`locked_until`／`line_user_id_hash`（篩選唯一索引）；新表 `member_refresh_tokens`、`membership_orders`；`membership_payments` 加 `membership_order_id`（篩選唯一索引）。EF 對映放在 `Data/ClubDbContextMemberFront.cs` 與 `Data/EfEntities/Member.FrontEnd.cs`（**不動 scaffold 產生檔**，重新 scaffold 不會被覆寫）。
**刻意不建表**：Email 驗證與密碼重設用 **Data Protection 簽章式、有時效的無狀態權杖**（purpose 互相隔離，跨用途不能互換）；LINE 的 state 與註冊票據同理。⚠️ 沿用 `DATA_PROTECTION_KEYS_PATH` 持久化（金鑰環遺失只讓「尚未使用的連結」失效，重寄即可，不是資料遺失）。

### 環境設定（新增）

| 變數 | 必填 | 說明 |
|---|---|---|
| `JWT_SIGNING_KEY_MEMBER` | 否（**正式環境建議設獨立的一把**，≥32 字元） | 會員存取權杖的簽章金鑰。沒設時由 `JWT_SIGNING_KEY_CLUB` 衍生。 |
| `LINE_LOGIN_CHANNEL_ID`／`LINE_LOGIN_CHANNEL_SECRET`／`LINE_LOGIN_REDIRECT_URIS` | 否 | LINE 一鍵登入。任一缺值 → LINE 端點回 503（不是 500）。`REDIRECT_URIS` 是逗號分隔的**前端回呼網址白名單**（需同時登記在 LINE Developers 的 Callback URL）。⛔ 不寫死、不進版控。 |
| `MEMBER_EMAIL_LINK_BASE_URL` | 否 | 信件連結的站台網址（預設 `https://{clubs.domain}`）。 |
| `EMAIL_SENDER`／`EMAIL_OUTBOX_PATH` | 否 | 開發環境預設把信寫成檔案（`EMAIL_OUTBOX_PATH` 指定目錄）；**Production 絕不註冊寫檔實作**，在寄信供應商選定前一律「尚未串接」。 |
| `PAYMENT_GATEWAY` | 否 | `fake`＝本機假金流（**Development 預設；`Production` 設了就啟動失敗**）；其餘＝「尚未串接」。 |
| `MEMBERSHIP_ACTIVATE_CREDENTIAL` | 否 | 內部開通端點的憑證（≥32 字元）。沒設＝端點停用。 |
| `MEMBER_AUTH_RATE_LIMIT_PERMITS`／`MEMBER_WRITE_RATE_LIMIT_PERMITS` | 否 | 兩個限流政策的額度覆寫（測試主機用寬鬆值）。 |

### 測試（`Tcrfc.Api.Tests`，新增 49 項，全套 775 項通過）

| 測試類別 | 項數 | 涵蓋 |
|---|---:|---|
| `MemberAuthTests` | 17 | 註冊（大小寫、弱密碼、重複、無此俱樂部）；Email 驗證（冪等、竄改、用途不符、驗證前不能登入、驗證後有會籍與卡）；忘記／重寄一律 202 且不寄信；登入失敗訊息一致；Cookie 模式（HttpOnly／Secure／SameSite=None／無 Domain／記住我）與 Body 模式；更新權杖輪替、**重放撤銷整批**、Cookie 續期、登出、登出全部；**5 次失敗鎖定**與解鎖；停用帳號權杖即時失效；重設密碼（一次性連結、全部登出、順便驗證）；變更密碼（撤銷其他裝置）；個人資料驗證；**刪除帳號欄位清除、卡作廢、可重新註冊**；**會員／後台權杖互不認帳**；金鑰衍生；LINE 未設定 503、登入／補 Email 註冊／不自動併入／解除綁定限制、綁定（state 不能替別人綁、一個 LINE 一個帳號）；**限流 429** |
| `MembershipOrderTests` | 13 | 建立訂單（金額由伺服器算、夾帶金額被忽略、冪等鍵必填、Email 未驗證 403）；冪等（同鍵同內容一張、同鍵不同內容 409、**並行 6 個同鍵只成立 1 張**、同方案不能兩張未完成）；別人的訂單 404；完整付款流程（請款冪等、**交易識別不符 400**、開通後會籍／付款紀錄雙欄位／會員卡／開通確認信、重複確認不重複開通、已是球迷會員不能再買）；免費升級沿用同一份會籍列；逾時；金流未串接 503；金流方失敗退回；**內部開通端點憑證保護**與**並行 12 個呼叫只開通一次**；開通失敗標 `activation_failed`；K2 開通結案申請；藍鯨方案已結束 |
| `MemberCenterTests` | 6 | 我的會籍（品牌、卡、可加入俱樂部、藍鯨 409）；續會提示與到期；**公開驗證頁只回約定欄位、`no-store`、過期／撤銷／停用 404**；重產 QR（舊 token 立即失效、別人的卡 404）；球衣登記（額度、並行不超收、寄出後鎖定、別人 404）；我的報名（帶權杖才歸戶、壞權杖視為訪客） |
| `MembershipPublicTests` | 3 | 方案、權益對照表（分組順序、下架條目、英文回退）、特約店家（共同／別隊／草稿／合作期間／篩選） |
| `CulturePublicTests` | 10 | 漫畫（藍鯨 403、草稿與未來發布日不見、最新集數即時判定、上下集導覽、閱讀數、角色與企劃）；活動（列表／詳情、非會員報名與候補、**並行 8 人報名不超收**、截止、限付費會員三種身分、取消報名） |

測試用 `CapturingEmailSender`（收信）與 `FakeLineLoginClient`（不連 LINE）取代 DI 註冊（`AdminWriteApiFixture`）；測試資料用 `mtest-`／9000 以上集數前綴，測完清除（含新表），不動種子。

### 只留介面、尚未串接

| 項目 | 現況 | 串接時 |
|---|---|---|
| **寄信供應商** | `IEmailSender`＋`NotConfiguredEmailSender`（正式）／`LocalFileEmailSender`（開發） | 實作 `SendAsync`、換 `Program.cs` 註冊；`emailSent` 會自動變 true。🔴 **在這之前正式環境無人能完成 Email 驗證**（見 `docs/17` §3） |
| **LINE Pay（會籍）** | `IPaymentGateway`＋`NotConfiguredPaymentGateway`（正式）／`LocalFakePaymentGateway`（開發） | 取得俱樂部商店號（B-10）→ 實作 `ReserveAsync`／`ConfirmAsync`、登記出口 IP（`docs/17` §3）、換註冊 |
| **LINE Login** | 程式完成，缺憑證 | 設 `LINE_LOGIN_*`，LINE Developers 登記 Callback URL |
| **到期前 30 天提醒信、到期通知** | 未做（需背景排程；五封信之外兩封） | 排程呼叫 `IEmailSender`＋`MemberEmailTemplates` 擴充 |
| **App 登入（AppDevice 權杖鏈）** | 未做（AP-3） | 沿用 `MemberTokenService`；更新權杖改掛 `app_devices`（`tokenDelivery=body` 已預留） |

### 規劃書沒寫、本批先採最保守做法（待裁決）

1. **網頁會籍站內結帳**：見上方「範圍提醒」。訂單／付款端點依 App 規劃書先做；網頁升級頁在使用者拍板（主站 §10 待確認）前只用 `created` 申請。**後台 K2 目前看不到這些申請**（`created` 訂單沒有清單畫面）——建議 K2 清單加「待確認申請」分頁，需後台畫面工作。
2. **註冊時 Email 已註冊回 409**（可被用來探測 Email 是否註冊過）：UX 上使用者需要知道該去登入，靠每 IP 5 分鐘 30 次限流壓住。若要完全不洩漏，改為一律 202 並寄「此 Email 已註冊」信（寄信通路未通前做不了）。
3. **LINE 登入不自動併入同 Email 的既有帳號**：規劃書寫「以 Email 或手機號碼比對後合併為單一帳號」，但 LINE 提供的 Email 不保證經過驗證，自動併入等於讓人用別人的 Email 接管帳號。改為：找不到 LINE 綁定時帶票據請使用者補 Email；Email 已有帳號 → 409，引導先用 Email 登入、再到設定綁定。後台 K1 的「合併帳號」仍可由客服處理。
4. **密碼強度**：規劃書沒寫。本批：8–128 字元、至少一個英文字母與一個數字、不得是常見弱密碼或等於 Email。
5. **`email_logs` 不寫**：該表 `type` 值域「會員 5＋商店 4」字面值未定（`db/club-schema.sql` 刻意不加 CHECK），本批寄信不寫紀錄。
6. **稽核**：會籍開通的「觸發來源、訂單編號、異動前後會籍狀態」（App 規劃書 §9.7）寫結構化日誌，不建稽核表（沿用 D 批待裁決 1）。
7. **未成年閘門與監護人同意**（App 規劃書 §4.5）是 App 的規定，主站規劃書 §3.14 沒有對應條文；本批 `birthOn` 選填、不擋註冊，App 端自行實作閘門。
8. **刪除帳號不清報名紀錄**：課程／試訓／活動的報名紀錄含報名者填的姓名電話，規劃書的清除範圍（App §4.6）只列 `Member`、裝置、會員卡 token，本批照辦；若要連報名者資料一併清除需另行裁決。
9. **不提供換 Email**（登入鍵、驗證流程都要重做，規劃書沒寫）。
10. **同一個更新權杖並行使用會被視為重放**：兩個分頁同時 refresh 會互踢，前端務必 single-flight。

---

## F 批：商店與前台缺口公開端點（2026-10-01，`backend-engineer`）

主站規劃書 §3.8 8.3 站內商店、§3.1 首頁、§3.3 3.1／§4.3 C2／C4、§3.11 11.1、§4.11 K2；藍鯨規劃書 §5.2／§5.3；對應 `STATUS.md` 的 `S3-5`（商店前台後端）、`S3-9`（積分榜與球員數據），並補上前端回報的四個缺口與 K2「待確認申請」。
沿用 E 批通則：路徑 `/api/v1/{club}/…`、錯誤一律 `ProblemDetails`（`title`／`status`／`detail` ＋機器可讀的 **`code`**，`detail` 是日常中文可直接顯示）、時間戳 UTC 帶 `Z`、`?lang=zh|en`（英文缺漏回退繁中）、公開寫入一律依 IP 限流。**給前端的人：只讀這一節就能串接，不需要看程式碼。**

### 端點一覽

| 方法 路徑 | 登入 | 說明 |
|---|:-:|---|
| `GET /{club}/shop/info` | — | 商店入口與政策（購物須知、運送說明、退換貨政策、交易條款，中英）、運費規則（固定運費＋免運門檻）、不配送地區、三種配送方式、**發票捐贈碼清單**、收款主體名稱、`paymentAvailable`（LINE Pay 是否已可用）、系列清單 |
| `GET /{club}/shop/collections` | — | 系列（Collection）＋品牌敘事（`narrative`）＋上架商品數 |
| `GET /{club}/shop/products` | — | 商品列表。`collection=<slug>`、`tag=`、`minPrice`／`maxPrice`（以**目前售價**）、`size=`、`colour=`、`isNew=true`、`sort=newest｜price_asc｜price_desc`（預設後台排序）、`page`／`pageSize`（預設 24、最多 60）。每筆：名稱、系列、標籤、`isNewArrival`、封面、`priceMin`／`priceMax`、`listPriceMin`＋`onSale`（促銷時畫刪除線）、`stockStatus`（`in_stock`／`low_stock`／`sold_out`）、`sizes`／`colours` |
| `GET /{club}/shop/products/{slug}` | — | 商品詳情：圖集、敘事、**尺碼表 `sizeChart`**（JSON，原樣）、SEO 欄位、規格清單（`id`、`sku`、`size`、`colour`、`label`、`listPrice`、`price`、`onSale`、`availableQty`（最高回報 99）、`purchasable`）。草稿或別隊商品 404 |
| `GET /{club}/shop/cart` | 會員或訪客 | 購物車（見下「購物車」） |
| `POST /{club}/shop/cart/items` | 會員或訪客 | `{ variantId, quantity }` 加入（數量累加） |
| `PUT /{club}/shop/cart/items/{variantId}` | 會員或訪客 | `{ quantity }` 設定數量（`0`＝移除） |
| `DELETE /{club}/shop/cart/items/{variantId}`／`DELETE /{club}/shop/cart` | 會員或訪客 | 移除一項／清空 |
| `POST /{club}/shop/cart/merge` | 會員 | 登入後把訪客購物車（`X-Cart-Token`）併入會員購物車 |
| `POST /{club}/shop/checkout` | 會員或訪客 | 結帳建立訂單（標頭 `Idempotency-Key` 必填）。**成立 201，冪等重送 200** |
| `GET /{club}/shop/orders/{orderNo}` | 會員本人或 `X-Order-Token` | 訂單詳情 |
| `POST /{club}/shop/orders/{orderNo}/pay` | 同上 | 向 LINE Pay 請款，回付款網址（冪等） |
| `POST /{club}/shop/orders/{orderNo}/confirm` | 同上 | `{ transactionId }` 付款導回後確認（伺服器向金流方確認，不信用戶端） |
| `POST /{club}/shop/orders/{orderNo}/cancel` | 同上 | 取消**待付款**訂單並釋回庫存（LINE Pay 取消導回頁用） |
| `POST /{club}/shop/orders/lookup` | — | 前台 `/order/lookup`：`{ orderNo, email }` 或 `{ token }`（信件連結） |
| `GET /{club}/shop/my-orders` | 會員 | 會員中心「我的訂單」（本人在該俱樂部的訂單，新→舊最多 50 筆） |
| `GET /{club}/standings?season=` | — | 賽事積分榜（見下） |
| `GET /{club}/stats/players?season=&team=&lang=` | — | 球員數據（自動彙總；見下） |
| `GET /{club}/players/{id}/stats` | — | 單一球員逐季數據（球員詳情頁） |
| `GET /{club}/home/core-values` | — | 首頁「五大核心價值」 |
| `GET /admin/{club}/membership-applications` | 後台（`member.membership.view`） | K2「待確認申請」（見下） |

兩個站台（`tcrfc`／`bw`）各自一份資料，**路徑上的 `{club}` 就是站台**；別隊的商品、規格、訂單一律 404。

### 通則（商店）

| 項目 | 規定 |
|---|---|
| **不得快取** | 🔴 商店所有回應 `Cache-Control: no-store`；`Features/Shop` 不注入快取服務（反射測試鎖定）。庫存、購物車、訂單、付款狀態每次直接查庫（docs/14 五類）。**Cloudflare 與前台 SSR 不得快取這些端點的回應。** |
| **兩種權杖** | **購物車權杖 `X-Cart-Token`**：訪客第一次加入商品時，回應的 `cartToken` 欄位帶出（**只有那一次**），前台存起來（localStorage／Cookie 皆可）並在之後每次購物車／結帳請求帶上；伺服器**只存雜湊**，遺失無法補發（等於新購物車）。**訂單權杖 `X-Order-Token`**：非會員結帳成立的那一次回應 `accessToken` 帶出（信件連結也帶它），之後用它操作這張訂單（付款、確認、取消、查詢）；**自訂單成立起 30 天有效**，過期後仍可用「訂單編號＋Email」查詢。有效的會員 `Authorization: Bearer` 優先於 `X-Cart-Token`。|
| **限流** | 購物車與訂單的寫入動作（含結帳、請款、確認、取消）：每 IP 每分鐘 60 次（`public-member-write`）；**訪客查單**：每 IP 5 分鐘 30 次（`public-member-auth`，防訂單編號枚舉）。超過回 **429**。 |
| **金額** | 🔴 **請求本文沒有任何金額欄位**。購物車與結帳的小計、運費、總額一律由伺服器依「現在的價格與規則」重算；購物車只存規格與數量，不存價格。 |
| **俱樂部** | 🔴 購物車以 `(club_id, 擁有者)` 區隔，**不得跨俱樂部混買**：拿另一隊的規格加入購物車回 404；同一個權杖換站台看到的是空車（切換站台即切換購物車）。訂單的賣方俱樂部（`selling_club_id`）＝站台，收款主體（`collecting_club_id`）＝系統設定的收款主體俱樂部（藍鯨商品也由本俱樂部收款，代收代付）。**不拆單**（B-8 未定案，現行禁止混買所以不會發生）。 |
| **常見 `code`** | `variant_not_found`（404，規格不存在／已下架／別隊）、`insufficient_stock`（409，帶「目前只剩 N 件」）、`quantity_limit`（409，單項上限 99）、`cart_full`（409，最多 50 種）、`cart_empty`（409）、`item_unavailable`（409）、`idempotency_key_required`（400）、`idempotency_key_reused`（409）、`invoice_required`／`invalid_carrier`／`invalid_tax_id`／`invalid_donation_code`／`invalid_email`／`invalid_phone`／`invalid_address`／`invalid_pickup_store`／`invalid_delivery_method`／`region_not_deliverable`（400）、`order_not_found`（404）、`order_not_payable`／`order_expired`／`order_not_cancellable`／`order_state_changed`／`payment_failed`／`payment_in_progress`（409）、`transaction_mismatch`（400）、`payment_not_configured`（503，LINE Pay 尚未串接）。 |

### 購物車

`GET …/shop/cart` 與所有購物車寫入端點都回同一種形狀：

```json
{
  "cartToken": "…只在新發出訪客購物車的那一次才有，否則 null…",
  "clubCode": "tcrfc",
  "items": [{
    "variantId": "…", "productSlug": "home-jersey-2026", "productName": "…", "variantLabel": "M／白",
    "sku": "HJ26-M", "imageThumbUrl": "…", "listPrice": 1980, "unitPrice": 1780, "onSale": true,
    "quantity": 2, "lineTotal": 3560, "availableQty": 5, "purchasable": true, "issue": null, "issueMessage": null
  }],
  "itemCount": 2, "subtotal": 3560,
  "shipping": { "fee": 0, "freeThreshold": 3000, "amountToFree": null },
  "canCheckout": true
}
```

- `issue`：`unavailable`（商品已下架或規格停售，須移除）／`insufficient_stock`（庫存不足，須調整數量）；有任何一列不可購買 → `canCheckout=false`。
- `shipping.fee` 是宅配／超商取貨的運費（已套免運門檻）；現場自取永遠 0（結帳回應會算好實際值）。
- 加入與設定數量時即檢查可售量與單項上限（99）；**購物車不保留庫存**，真正保留發生在結帳。
- 登入後：呼叫 `POST …/shop/cart/merge`（帶會員權杖＋原本的 `X-Cart-Token`），訪客購物車併入會員購物車（數量相加）並刪除，重複呼叫安全。會員購物車存帳號，跨裝置保留。
- 訪客購物車超過 30 天沒動過由維護作業清除。

### 結帳 → 付款 → 結果（時序）

1. `POST …/shop/checkout`，標頭 `Idempotency-Key`（8–64 字元的英數與 `-_:.`）＋會員權杖或 `X-Cart-Token`：

```json
{
  "email": "buyer@example.com", "recipientName": "王小明", "recipientPhone": "0912-345-678",
  "deliveryMethod": "home_delivery", "recipientAddress": "台中市…", "pickupStore": null,
  "customerNote": "…", "lang": "zh",
  "invoice": { "type": "mobile_barcode", "carrierId": "/ABC+123" }
}
```

   - `deliveryMethod`：`home_delivery`（必填 `recipientAddress`）／`cvs_pickup`（必填 `pickupStore`＝門市名稱或代碼，存進訂單的收件地址欄）／`onsite_pickup`（現場自取，免運）。聯絡電話一律必填。**會員可省略 `email`／`recipientName`／`recipientPhone`，伺服器用帳號資料補**；非會員必填 `email`。
   - 發票（必開電子發票，**四選一型態、三選一內容**）：`mobile_barcode`（`carrierId`：`/` 開頭共 8 碼）／`citizen_cert`（`carrierId`：2 英文＋14 數字）／`tax_id`（`taxId`：8 位數字，含**統編檢核碼**驗證）／`donation`（`donationCode`：須在 `shop/info.donationCodes` 清單內）。
   - 地址含後台 S6 設定的「不配送地區」→ 400 `region_not_deliverable`。
   - **成功 201**：回 `ShopOrder`（見下）；**下單即保留庫存**（可售量減少，庫存量不變）、購物車同一個交易內清空、寄「訂單成立」信（訪客的信帶具時效的查詢連結）。**非會員的回應含 `accessToken`**。
   - **冪等**：同一把 `Idempotency-Key` 重送 → **200 且回原訂單**（即使購物車已清空）；同鍵但內容不同、或別的擁有者拿同一把鍵 → 409 `idempotency_key_reused`。**並行**重送同一把鍵只會成立一張。
   - **庫存不足** → 409 `insufficient_stock`（訊息指出哪一項、剩幾件），**整張訂單不成立、購物車保留**，可調整數量重試。庫存被「已逾時但還沒被清掃」的待付款訂單占住時，結帳會先清掃再成立。
2. `POST …/shop/orders/{orderNo}/pay`（會員 Bearer 或 `X-Order-Token`）→ 回 `ShopOrder`，`paymentUrl` 是 LINE Pay 付款頁，前台把使用者導過去。**重複呼叫回同一個網址**。LINE Pay 未串接時 503 `payment_not_configured`（`shop/info.paymentAvailable=false`，前台應先告知）。**本機開發註冊假金流**：`paymentUrl` 是 `https://fake-linepay.invalid/pay/{orderNo}`，交易識別是 `FAKE-{orderNo}`。
3. 使用者在 LINE Pay 付完款導回前台的 `confirmUrl` 頁，前台呼叫 `POST …/shop/orders/{orderNo}/confirm`，本文 `{ "transactionId": "…" }`（LINE Pay 導回時帶的 `transactionId`）。伺服器**向金流方確認**後：訂單 `已付款`、**庫存扣減**（保留轉為售出）、開立電子發票、寄「付款完成」信。**重複／並行確認冪等**（庫存只扣一次、發票只開一張、信只寄一封）。交易識別不符 400 `transaction_mismatch`。
4. 使用者在 LINE Pay 按取消導回 `cancelUrl` 頁：呼叫 `POST …/shop/orders/{orderNo}/cancel`，釋回庫存；也可以什麼都不做——**待付款超過 S6「待付款保留時間」（預設 30 分鐘）自動取消並釋回庫存**（`status` 變 `已取消`、`paymentStatus` 變 `expired`；讀取訂單與背景作業都會換算）。**已付款的訂單不能取消**（409 `order_not_cancellable`，請走客服申請退換貨）。

### `ShopOrder`（結帳、請款、確認、取消、詳情、查詢共用）

`orderNo`（`TR-yyyyMMdd-XXXXXX`／藍鯨 `BW-…`）、`status`（**日常中文標籤，直接顯示**：待付款／已付款／備貨中／已出貨／已完成／已取消／退貨處理中／已退款；`?lang=en` 為英文）、`paymentStatus`＋`paymentStatusLabel`、`paymentMethodLabel`、`deliveryMethodLabel`、`subtotal`／`shippingFee`／`total`、`items[]`（建單當下的**快照**：`productName`、`variantLabel`、`sku`、`unitPrice`、`quantity`、`lineTotal`）、收件資料、`invoice`（`type`／`typeLabel`／`status`（`pending`／`issued`）／`statusLabel`／`invoiceNo`／`taxId`／`donationCode`；**開立失敗對顧客一律顯示「處理中」**，系統自動重試）、`shipment`（`carrier`／`trackingNo`／`shippedAt`／`pickupStatusLabel`／`pickupDeadlineOn`，出貨後才有）、`paymentUrl`（待付款且已請款才有）、`expiresAt`（待付款的保留期限）、`canPay`／`canCancel`、`createdAt`／`paidAt`、`isMasked`。
**遮罩**：以「訂單編號＋Email」查到的訪客只拿到**遮罩的收件人姓名、電話、地址與 Email**（`isMasked=true`）；持有 `X-Order-Token` 或會員本人拿完整值。**載具號碼永不回傳**（只存加密密文）。

### 訪客查單（前台 `/order/lookup`）

`POST …/shop/orders/lookup`：`{ "orderNo": "TR-…", "email": "…" }`（訂單編號大小寫、Email 大小寫不拘）或 `{ "token": "…" }`（訂單成立信的連結 `…/{lang}/order/lookup/?token=…`，**30 天有效**）。**找不到、Email 不符、權杖過期一律同一個 404 `order_not_found`**（不洩漏訂單是否存在）；只查本站台（另一隊站台查不到）。

### 賽事積分榜、球員數據

- `GET …/standings?season=2026-27` → `{ season: { code, startOn, endOn } | null, seasons: ["2026-27", …], items: [{ rank, teamName, played, points }], updatedAt }`。依名次排序，**沒有名次者墊底**。不帶 `season`：取「今天落在起訖內」的球季（沒有就取最新有資料的）；指定不存在的球季＝空榜（`season: null`）。積分榜資料只有 `rank／played／points`（C4 手動維護或匯入），**沒有勝負平與得失球**——規劃書沒要求，不自行加欄位。
- `GET …/stats/players?season=&team=D1` → `{ season, seasons, items: [{ playerId, name, teamCode, shirtNo, position, photoUrl, appearances, goals, assists, yellowCards, redCards, source }] }`，依進球、出賽、背號排序。**彙總規則**：只計狀態為「已結束」的賽事；進球＝進球筆數；黃／紅牌＝牌筆數；**出賽＝先發名單的場次＋雖列替補但該場有進球或牌的場次**（賽事紀錄沒有出場分鐘）；**助攻沒有資料來源，自動彙總時為 `null`**。後台若手動輸入了該球員該球季的數據（`player_season_stats`），**以手動為準**（`source=manual`）。`photoUrl` 受肖像同意 fail-closed 規則（未同意為 null）。
- `GET …/players/{id}/stats` → `{ playerId, seasons: [{ seasonCode, appearances, goals, assists, yellowCards, redCards, source }] }`（新→舊）；別隊球員 404。

### 首頁核心價值

`GET …/home/core-values` → 固定五項 `[{ code, nameZh, nameEn, sortOrder, learnMorePageSlug }]`，`code` 與文章的核心價值標籤同一組（`players_first`／`excellence`／`global_pathways`／`community`／`integrity`），`learnMorePageSlug`＝`about/philosophy`（2.3 足球理念）。圖示由前台依 `code` 對應。**規劃書 §3.1 資料來源欄寫「後台設定」，但後台沒有編輯這五項的畫面或欄位**——五項是規劃書 §1.2 定死的品牌主張，所以由伺服器固定目錄提供，兩站共用。區塊的開關與排序仍由 `GET …/home-sections`（`core_values`）決定。

### K2「待確認申請」（後台）

`GET /api/v1/admin/{club}/membership-applications?status=created&keyword=&page=&pageSize=`（權限碼 `member.membership.view`；俱樂部範圍；姓名／Email／電話依「完整個資」權限遮罩）→ `PagedResult`，每列：

`orderNo`、`memberId`、`memberNo`、`memberName`／`memberEmail`／`memberPhone`、`planId`／`planCode`／`planName`、`seasonId`／`seasonCode`、`amount`（伺服器依方案算的應收金額）、`status`／`statusLabel`、`expiresAt`、`activatedAt`、`createdAt`、`updatedAt`。

- 預設只列 `created`（＝網頁會員送出的升級申請，待客服核對款項）。`status` 可為 `created`／`pending_payment`／`paid`／`activated`／`expired`／`activation_failed`（已付款但開通失敗，**客服要處理**）／`cancelled`／`refunded`；不合法 400。待付款超過期限者以 `expired` 顯示。
- **畫面的徽章數字**＝`?status=created&pageSize=1` 回應的 `totalCount`。
- **開通動作沿用既有 `POST …/memberships/activate`**，本文帶本列的 `memberId`、`planId`、`amount`（再加 `paymentMethod`、`paidOn`）；開通成功時**會一併把同一份申請結案**（`status → activated`、`activation_source = admin`），清單上就不會再出現（E 批既有行為，F 批補測試）。

### 資料庫綱要異動（`db/club-schema.sql`、migration `AlignSchemaF1`）

先改 `docs/12`（第 48 點）、`docs/12b`（§6.7／§6.9）、`docs/12a` 再改 DDL 與 EF；本機 `tcrfc_club` 已套用（`dotnet ef migrations has-pending-model-changes` 無待處理變更，**表數不變**）。`orders` 加 `buyer_email`／`idempotency_key`／`request_fingerprint`／`payment_url`，`(club_id, idempotency_key)` 篩選唯一；`carts` 加兩條篩選唯一索引；`store_invoices.carrier_id_encrypted` 放寬為 `nvarchar(500)`。EF 對映放在 `Data/ClubDbContextShopFront.cs` 與 `Data/EfEntities/Order.ShopFront.cs`（**不動 scaffold 產生檔**）。**里程碑「分類」欄位沒有新增**——見下方「規格疑點」第 1 點。

### 環境設定（新增）

| 變數 | 必填 | 說明 |
|---|---|---|
| `INVOICE_ISSUER` | 否 | `fake`＝本機假發票（**Development 預設；Production 設了就啟動失敗**）；其餘＝「尚未串接」。 |
| `SHOP_JOBS_INTERVAL_SECONDS` | 否 | 商店維護作業間隔：**未設定時 Production 預設 60、其餘環境停用**；`0` 停用。內容：逾時訂單釋回、發票重試、清理待開立發票資料列與舊訪客購物車。 |

沿用 E 批的 `PAYMENT_GATEWAY`（`fake`＝本機假金流）、`EMAIL_SENDER`／`EMAIL_OUTBOX_PATH`、`MEMBER_EMAIL_LINK_BASE_URL`（信件連結的站台網址）。

### 測試（`Tcrfc.Api.Tests`，新增 17 項）

| 測試類別 | 項數 | 涵蓋 |
|---|---:|---|
| `ShopPublicTests` | 13 | 目錄（篩選、促銷價、新上市、缺貨判定、自動隱藏、草稿與別隊不可見、**庫存即時不快取**、`no-store`）；商店入口；購物車（訪客權杖、累加、庫存上限、**不跨俱樂部混買**、下架標示、會員購物車、登入合併）；**完整訪客流程**（金額重算、保留庫存、請款冪等、**並行 6 個確認只成立一次**：庫存只扣一次／發票一張／信一封、載具密文、查詢遮罩、權杖過期）；會員結帳歸戶與「我的訂單」、別人 404；結帳驗證（四種發票格式、統編檢核碼、捐贈碼、配送欄位、冪等鍵必填）；**冪等**（同鍵重送、同鍵異內容 409、別人拿同一把鍵 409、**並行 8 個同鍵只成立 1 張**）；**防超賣**（庫存 3、**10 個訪客同時結帳恰好 3 張成立**、其餘 409、保留量＝庫存；多規格**交錯下單不死結**）；取消與逾時（讀到時換算、庫存被逾時訂單占住時結帳先清掃再成立）；**維護作業**（逾時釋回、發票失敗後補字軌重試補開、清理、冪等重跑） |
| `FrontGapsPublicTests` | 4 | 首頁核心價值；積分榜（排序、無名次墊底、球季選擇、別隊看不到）；球員數據（**自動彙總規則**、未結束賽事不計、手動優先、肖像 fail-closed、隊別篩選、逐季、別隊 404）；**K2 待確認申請**（出現、遮罩、篩選、請款後換狀態、開通後結案、俱樂部範圍、需登入） |

測試資料用 `【測試】`／`【F測試】` 前綴與 `TST-F` 球季，測完清除（商品、訂單、發票、購物車、字軌通道、賽事、球季），不動種子；字軌通道若已存在就沿用（沒字軌則暫時補 `ZZ`，測完還原）。

### 只留介面、尚未串接

| 項目 | 現況 | 串接時 |
|---|---|---|
| **LINE Pay（商店）** | 沿用 E 批 `IPaymentGateway`（`NotConfigured`／`LocalFake`）。結帳、請款、確認、冪等、逾時釋回全部以假金流跑通 | 取得俱樂部商店號（B-10）→ 實作 `IPaymentGateway`、登記出口 IP（`docs/17` §3）、換註冊；**商店邏輯一行不用改**。退款仍走 C1 的 `ILinePayGateway`（未串接） |
| **電子發票開立** | 新介面 `IInvoiceIssuer`（`NotConfigured`／`LocalFake`）；付款確認時開立、失敗重試 | 選定發票服務、取得**俱樂部字軌**（寫入後台 S6 的電子發票設定）→ 實作 `IssueAsync`（須冪等）、換註冊。退貨作廢／折讓仍走 C1 的 `IEInvoiceService`（未串接） |
| **商店交易信（出貨通知、退款完成）** | 只做了「訂單成立」「付款完成」兩封（`ShopEmailTemplates`）；出貨與退款是後台動作的副作用，**尚未接** | 在 `ShopOrderLifecycle.ShipAsync`、退款執行處呼叫 `IEmailSender`；`IEmailSender` 本身供應商未定（E 批） |
| **發票補開後的通知** | 付款完成信寫「發票將於開立後顯示在訂單頁面」；重試補開成功**不另寄信**（規劃書四封之外會變第五封） | 待裁決，見下 |

### 自行判斷的事（規劃書沒逐條定義，採最保守做法）

1. **訂單權杖與信件連結 30 天有效**（規劃書只寫「具時效性」）；**待付款保留時間沿用 S6 設定**（預設 30 分鐘）；**單項數量上限 99、購物車最多 50 種規格、訪客購物車保留 30 天**。
2. **以「訂單編號＋Email」查單只給遮罩的收件資料**；持權杖才給完整值。**付款、確認、取消一律需要會員本人或訂單權杖**（光有訂單編號＋Email 不能付款）。
3. **缺貨且設定「自動隱藏」只從列表隱藏，詳情網址仍可開啟**並標示缺貨（避免已流通的連結突然 404；若要連詳情一起 404，改 `ShopCatalogRepository.GetProductAsync` 一行）。
4. **超商取貨的門市存進訂單的收件地址欄**（前綴「超商取貨門市：」），出貨時 S4 另填 `shipments.store_branch_code`——規劃書沒有為此定義欄位。
5. **LINE Pay 的 `Confirm` 就是「伺服器向金流方確認」的位置**，沒有另做「金流 webhook」端點（LINE Pay v3 沒有付款結果 webhook；回呼＝使用者導回 `confirmUrl`）。若串接時發現必須支援非同步回呼，再加 `POST /api/shop/payments/linepay/notify`（驗簽、冪等，內部走同一個 `ShopOrderLifecycle.ConfirmPaymentAsync`）。
6. **球員出賽的認定**（先發＋有事件的替補）與**助攻不彙總**：賽事紀錄沒有出場分鐘與助攻資料。若要精確出賽或助攻，要先改規格（賽事紀錄加欄位）。
7. **核心價值固定目錄**（見上）。

### 規格疑點與待裁決（需要使用者或客戶回覆）

1. **里程碑「分類」欄位（俱樂部／國際／榮譽）規劃書沒有**：主站規劃書 §4.3 C5 只有「日期、標題、描述、圖片、是否顯示於時間軸」，§3.2 2.8 只有「年份錨點、圖片、事件描述、年份篩選」。**本批不做**（`docs/12` 的 `milestones` 表也沒有此欄）。前台若要分類，**請先確認要改規格**（走同步鏈：規劃書 C5 → `docs/12` → DDL → migration → 後台 C5 表單）。
2. **11.1 慈善理念沒有新增端點**：規劃書 §3.11 11.1 寫「區塊編輯器排版」，B5 沒有對應內容型別——它是 **B1 頁面管理**的一頁。前台用既有 `GET /api/v1/tcrfc/pages/charity/commitment`（頁面 slug 沿用網址路徑，同 `about/philosophy`）。**目前種子沒有這一頁**（只有 `about/our-story`、`about/vision-mission`、`about/philosophy`、測試草稿頁），**需要補種子內容**（四大投入領域的區塊文案，客戶提供）或請後台人員在 B1 建立 `charity/commitment`（`published`）。
3. **退換貨申請沒有前台端點**：規劃書 §3.8 8.3 寫「退換貨政策頁與申請管道（**表單或客服信箱，不做專屬的線上退貨精靈**）」，S5 的案件由客服在後台建立（`POST /admin/{club}/shop/refunds` 既有）。前台「我的訂單」的「退換貨申請入口」＝連到政策頁與聯絡表單／客服信箱。**若要改成會員自助送出申請，是規格異動**。
4. **「訂單是否於結帳時依俱樂部拆單」（B-8）仍未定案**：本批維持**禁止混買、不拆單**。
5. **出貨通知與退款完成兩封信的接線**、**發票補開成功是否通知**（見上「只留介面」）。
6. **STATUS B-10 仍擋「正式付款」**：LINE Pay 商店號與發票服務／字軌未到位前，正式環境 `shop/info.paymentAvailable=false`、請款回 503；前台可完整展示與結帳至「建立訂單」，但無法付款。


---

## 慈善 CH-2／CH-3：捐款主幹公開端點＋慈善獨立後台 API（2026-10-01，`backend-engineer`）

**範圍**：慈善捐款平台（協會主辦與收款，[`docs/10`](../../docs/10-charity-donation-site.md)／[`docs/16`](../../docs/16-charity-schema.md)）的**後端 API**——CH-2 前台公開端點（掃碼落地 → 項目 → 建單（冪等）→ 付款 → 確認 → 結果 → 憑證 → 感謝信）與 CH-3 獨立後台（`N1` 店家與 QR、`N2` 項目、`N3` 捐款紀錄與異常佇列）。**不含前端**。實作全部在 [`CharityPlatform/`](CharityPlatform/)，與主站 `Features/` 完全分開。

### 一眼看懂

| 項目 | 內容 |
|---|---|
| 啟用條件 | 設定 `CHARITY_SQL_CONNECTION_STRING` 才註冊與對映（沒設＝慈善端點全部 404，主站完全不受影響）；啟用時 `JWT_SIGNING_KEY_CHARITY` 必填（缺值啟動失敗） |
| 資料庫 | **獨立的 `CharityDbContext`**（`tcrfc_charity`）；與 `ClubDbContext` 不共用連線、不共用 migration。**所有讀寫走 EF**，沒有 Dapper；🔴 **完全不碰 Redis／`IQueryCache`**（docs/16 §7：冪等與金流狀態不得讀快取，慈善整個不接快取） |
| 路徑 | 公開 `/api/v1/donation-platform/…`；後台 `/api/v1/donation-platform/admin/…`（避開主站的 `/api/v1/{club}/charity/…` 與 `/api/v1/admin/…`） |
| 帳號體系 | **完全獨立**：自己的 `admin_users`／角色／權限，自己的 JWT 方案（`CharityAdminBearer`，issuer／audience／金鑰都與主站不同）、自己的更新權杖 Cookie `__Host-tcrfc-charity-admin-rt`（🔴 與主站 `__Host-tcrfc-admin-rt` 不同名：兩個後台打同一個 API 網域，同名會互相蓋掉登入） |
| 時間 | 全部 UTC、JSON 帶 `Z`（既有慣例）；「期間」篩選與憑證「當期」判斷以台灣日期 |
| 錯誤格式 | 一律 `ProblemDetails`（`status`／`title`／`detail`，`detail` 是日常中文）；`429` 沒有 body |
| 型別層授權 | 後台每支端點先 `ICharityAdminAuthorizer.AuthorizeAsync(...)` 拿 `CharityAdminScope`，service 方法第一個參數就是它（`CharityAdminScope` 建構子 `internal`，唯一產生者是 `CharityAdminAuthorizer`；`ArchitectureTests` 已納入 Roslyn 語意掃描，`CharityArchitectureTests` 另掃端點與 service 簽章） |

### 外部服務接縫（目前只有本機假實作）

| 介面 | 位置 | 狀態 |
|---|---|---|
| `IPaymentGateway`（LINE Pay，協會商店號） | `CharityPlatform/Payments/` | 🟡 **只有 `FakePaymentGateway`**。協會商店號未申請（STATUS B-7）。付款網址指回前台既有的模擬頁 `/{lang}/pay/{orderNo}?transactionId=…`；交易識別碼 `FAKE-DECLINE-` 開頭視為金流拒絕（手動測失敗分支） |
| `IInvoiceIssuer`（電子發票／捐贈收據，協會字軌） | `CharityPlatform/Invoices/` | 🟡 **只有 `FakeInvoiceIssuer`**。廠商未指定、協會統編未取得。憑證號碼 `{字軌}-{單號}`（同單號同憑證，天然冪等）；字軌讀慈善庫 `payment_channels.invoice_prefix`（**不得與俱樂部共用**） |
| `IEmailSender`（四封系統信） | `CharityPlatform/Mail/` | 🟡 **只有 `FakeEmailSender`**（只寫日誌，不真的寄）。寄信服務未選定 |
| `ITurnstileVerifier` | `CharityPlatform/Security/` | 🟡 預設放行；設定 `TURNSTILE_SECRET_KEY_CHARITY` 就改走 Cloudflare siteverify（已實作，單元測試用假 HTTP 處理器驗證，未連網實測）。驗證服務本身壞掉時**放行**（第一道 IP 限流仍在） |
| `IPaymentReconciliationSource`（協會 LINE Pay 的每日交易明細，**CH-4 新增**） | `CharityPlatform/Payments/` | 🟡 **只有 `FakePaymentReconciliationSource`**（以本站自己的金流紀錄當明細，永遠一致，只驗證流程；只在 `CharityFakeGuard` 允許的環境運作）。正式串接卡 `B-7` |
| `ICharityImageStorage`（Logo／封面） | `CharityPlatform/Storage/` | ✅ 慈善自己的容器（`AZURE_BLOB_CONNECTION_STRING_CHARITY`／`AZURE_BLOB_CONTAINER_CHARITY`，預設 `charity-images`）；沒設用替身（讀取回無圖、上傳回 503）。圖片處理重用主站 `ImageProcessor`（WebP、2560、去 EXIF、四個衍生檔） |

🔴 **假實作的環境防線**（`CharityFakeGuard`）：假金流對任何交易回「扣款成功」、假發票編出像發票號碼的字串——在正式環境等於憑空確認收款、偽造憑證。所以三個假實作**只在 `Development`（或明確設 `CHARITY_ALLOW_FAKE_PROVIDERS=true`）運作**，其餘環境一律丟「尚未設定」（金流／發票回 503，寄信記成 `email_logs.status = 'failed'`），不會假裝成功。正式實作到位時，`CharityPlatformRegistration` 裡三行 `AddSingleton` 換掉即可，流程不動。

### 公開端點（不需要登入）

語系 `?lang=zh|en`（缺漏回退繁中，回應有 `isFallback`）。🔴 **公開回應絕不含**：分潤百分比、金流交易識別碼、`*_encrypted`、物件儲存的原始鍵（一律換成完整網址）、募款進度（規劃書 §3.2 v1.2 無進度條／目標／累計）；`CharityArchitectureTests` 用反射鎖住這些欄位名稱。

| 方法 路徑 | 說明 |
|---|---|
| `GET /settings` | 站台文案（首頁說明、感謝語、捐款須知、隱私權政策）、全站預設單筆金額範圍、徵信名單是否開放 |
| `GET /stores/{slug}` | 掃碼落地頁的店家識別。🔴 **對不到有效店家（不存在、已停止、不在合作期間）回 `200 { "store": null }`，不報錯**（規劃書 §2.2 第 5 點：視同無店家歸屬）。`logoUrl` 為 `null` 時前台降級為純文字店名，不留空框 |
| `GET /projects` | 已上架項目卡片牆（依排序）。無募款進度 |
| `GET /projects/{slug}` | 項目詳情：說明內文（區塊編輯器 JSON 原樣）、款項用途、金額選項、**生效的單筆金額範圍**（項目沒設就用全站預設）、`invoiceMode`、撥付對象與慈善計畫名稱快照 |
| `POST /donations` | **建立捐款單**。標頭 `Idempotency-Key`（16–64 個英數／底線／連字號，前端用 UUID 即可）**必填**。首次回 `201`，同一個鍵重複送出回 `200` 並沿用原單（`created: false`）；同一個鍵送了不同內容回 `409`。見下 |
| `POST /donations/{orderNo}/pay` | 發起付款，回 `paymentUrl` 要導向的網址。`created`／`failed`／`expired` 可（重）新發起，**沿用原單**；已有進行中的付款就沿用同一個網址，不再呼叫金流 |
| `POST /donations/{orderNo}/confirm` | 使用者從 LINE Pay 返回後呼叫，body `{ "transactionId": "…" }`（返回網址帶回的值）。**冪等**，回結果頁資料 |
| `POST /donations/{orderNo}/cancel` | 使用者在 LINE Pay 取消、返回本站。`pending → failed`（可重試），冪等 |
| `GET /donations/{orderNo}` | 結果頁資料（前台約每 3 秒輪詢）。🔴 姓名與 Email **遮罩**，不含完整個資 |

**建單請求**（`camelCase`）：

```json
{
  "projectSlug": "…", "storeSlug": "…或省略", "amount": 500,
  "donorName": "王小明", "donorEmail": "a@example.com", "isAnonymous": false, "consentPrivacy": true,
  "invoice": { "type": "mobile_carrier", "mobileCarrier": "/ABC+123" },
  "lang": "zh", "turnstileToken": "…（啟用後必填）"
}
```

`invoice` 依項目的 `invoiceMode` 填對應那組：

| `invoiceMode` | 欄位 |
|---|---|
| `b2c_invoice` | `type` 必填：`mobile_carrier`（`mobileCarrier`，`/` ＋ 7 碼）／`love_code`（`loveCode`，3–7 碼數字）／`tax_id`（`taxId` 8 碼並通過**檢核碼**＋`invoiceTitle` 必填） |
| `donation_receipt` | `receiptTitle`（省略就帶入捐款人姓名）、選填 `nationalId`（身分證字號，須通過檢核碼；**加密儲存**）、選填 `address`、`isAnnualSummary` |

驗證失敗一律 `400`（日常中文訊息）；金額超出範圍 `422`；項目不存在或未上架 `404`；`store` 無效**不報錯**；人機驗證不過 `422`。

**結果頁資料** `status`：`created`／`pending`／`paid`／`failed`／`expired`／`refunded`。`processing: true`（`pending`）時前台顯示「處理中」並輪詢，**文案不得讓人誤以為失敗而重複付款**；`canRetry: true`（`created`／`failed`／`expired`）時顯示重試按鈕（呼叫 `/pay`，不重新建單、不要求重填）。`invoiceStatus` 只有 `pending`／`issued`——開立失敗對外一律仍顯示「處理中」，失敗細節是協會內部的人工補開佇列。

**前端接法（建議流程）**

```
填表送出 → POST /donations（帶 Idempotency-Key；同一次送出沿用同一個鍵，使用者改了表單內容就換新鍵）
         → POST /donations/{orderNo}/pay → 導向 paymentUrl
LINE Pay 返回 {前台網址}/{lang}/result/{orderNo}?transactionId=…
         → POST /donations/{orderNo}/confirm {transactionId}（取消返回則帶 ?cancel=1 → POST …/cancel）
         → 結果頁：status=pending 時輪詢 GET /donations/{orderNo}
```

付款網址與返回網址的前台基底是 `CHARITY_PUBLIC_BASE_URL`（沒設就用 compose 給 api 的 `CHARITY_DOMAIN` 組出；本機預設 `http://charity.localhost`；非本機都沒有就回 503，不拿錯的網址去導向金流）。

### 🔴 冪等與「已扣款但 Confirm 失敗」

- **建單冪等**：資料庫沒有 `idempotency_key` 欄位，不自己發明——**單號由冪等鍵經 HMAC 決定性推導**（`CH` ＋16 碼，用 `JWT_SIGNING_KEY_CHARITY` 作秘密），同鍵同號，並發撞 `UQ_donations_order_no`，輸的回頭讀贏的那張（測試：8 個並發請求只建一張）。
- **付款冪等**：所有狀態轉移是條件式更新（`UPDATE … WHERE status IN (…)`），只有贏得轉移的請求才入帳、寄感謝信、開票；重複 `confirm` 只回傳目前結果（測試：重複 3 次、並發 6 個，金流只確認一次、憑證只開一次、感謝信只寄一次）。
- **Confirm 結果未知**（金流連線中斷、逾時）：捐款單**維持 `pending`**、最近一次付款標 `failed`——這個組合就是「待人工處理」的定義（金流明確失敗一律轉捐款單 `failed`，所以此組合只可能是結果未知）。此時：回應給使用者 `processing: true`（不是失敗）；**禁止重新發起付款**（`409`，避免重複扣款）；使用者按取消也不動；背景逾時工作**不會**轉成 `expired`；後台異常佇列列出，客服用「重新確認付款結果」（或使用者重新整理再 `confirm`）重新向金流確認，成功就走正常收尾。
- **`expired` 的單金流端晚到的成功仍會收下**（使用者在最後一刻付款）。
- 金流確認成功之後的所有資料庫寫入用 `CancellationToken.None`——錢已經動了，不能因為使用者關掉頁面而少記一半；寄信失敗與開票失敗都不會讓付款回應變成失敗。

### 限流（公開寫入端點一律依訪客 IP）

| 政策 | 額度（預設，可用環境變數覆寫） | 掛在 |
|---|---|---|
| `charity-public-write` | 30 次／10 分鐘／IP（`CHARITY_PUBLIC_WRITE_RATE_LIMIT_PERMITS`）。⚠️ 掃碼場景常見店家 Wi-Fi 同出口，所以不壓到個位數 | 建單、發起付款、確認、取消 |
| `charity-public-read` | 120 次／分鐘／IP（`CHARITY_PUBLIC_READ_RATE_LIMIT_PERMITS`） | 結果頁輪詢（擋大量枚舉單號） |
| `charity-admin-login`／`charity-admin-refresh` | 與主站共用設定鍵（`ADMIN_LOGIN_RATE_LIMIT_PERMIT_LIMIT`／`ADMIN_REFRESH_RATE_LIMIT_PERMIT_LIMIT`），政策與計數分開 | 後台登入、更新權杖 |

額度是「規劃書沒給數字、執行層自行決定」的最小可行防護，上線後依實際流量調整。`CharityArchitectureTests` 掃：所有公開非 GET 端點與結果頁輪詢一定掛限流。

### 慈善後台登入與個人檔案（`/api/v1/donation-platform/admin/auth/…`）

請求與回應形狀**刻意與主站 `/api/v1/admin/auth/…` 相同**（`LoginRequest`／`LoginResponse`），兩個後台前端的登入程式碼可以共用。

| 方法 路徑 | 說明 |
|---|---|
| `POST /login` | `{ username, password, totpCode? }`。成功回存取權杖（15 分鐘 JWT）並設更新權杖 Cookie；`401` 不區分帳號不存在或密碼錯（時序也補齊）；連續 5 次失敗鎖 15 分鐘（`423`）；已啟用 2FA 的帳號沒帶驗證碼回 `{ status: "totp_required" }` |
| `POST /refresh` | 讀 Cookie，輪替更新權杖；舊權杖被重放時撤銷該帳號全部有效權杖 |
| `POST /logout` | 撤銷更新權杖並清 Cookie |
| `POST /change-password` | `{ currentPassword, newPassword }`（≥ 9 字元、不得等於帳號） |
| `POST /2fa/setup`／`/2fa/confirm`／`/2fa/disable` | 選用（2026-09-30 裁決，不強制）；密鑰用慈善專屬用途加密 |
| `GET /me` | 個人檔案：角色與**持有的權限碼**（只給前端決定顯示或隱藏按鈕，介面不得顯示權限碼）。沒有俱樂部授權清單與站台切換器（單一法人） |

**本機種子帳號**（`db/seed/generate-charity-seed-sql.py` §4，🔴 只供本機開發，正式環境不得沿用）：

| `username` | 密碼 | 角色 |
|---|---|---|
| `sa@charity.local` | `Admin@123` | 系統管理員（`is_super_admin`） |
| `cs.admin@charity.local` | `ContentEditor@123` | 客服／行政 |
| `biz.admin@charity.local` | `ContentEditor@123` | 商務／贊助 |
| `viewer@charity.local` | `Viewer@123` | 檢視者 |

（CH-1b 時期建好的本機庫裡這四列是 `DEV-SEED-` 占位雜湊，重跑 `./db/seed/apply-charity-seed.sh` 會升級成真雜湊——只升級仍是占位的列，不覆蓋被改過的密碼。）

### 後台端點（全部需要登入，`Authorization: Bearer <存取權杖>`）

權限碼是種子 `permissions.code`（常數在 `CharityPlatform/Common/CharityPermissions.cs`）。**角色權限對照見 `db/seed/generate-charity-seed-sql.py` 的 `ROLE_PERMISSION_MAP`**：系統管理員全部；客服／行政＝N3 檢視／個資明文／匯出／重新確認、N5 檢視／開立／作廢、N6 檢視；商務／贊助＝N1 檢視／編輯、N2 檢視／編輯／上下架、N6 檢視；檢視者＝七個模組各一個唯讀碼；其餘五個角色在慈善後台沒有職能。

**N1 店家與 QR**（`/stores`）

| 方法 路徑 | 權限 | 說明 |
|---|---|---|
| `GET /stores?keyword&status&page&pageSize` | `n1.donation_store.view` | 列表，含**累計筆數、金額、應付回饋金**（只計 `paid`） |
| `GET /stores/{id}` | 同上 | 詳情，含 `qrTargetUrl` |
| `POST /stores`／`PUT /stores/{id}` | `…manage` | 建立／更新。`slug` **一律由系統產生**（80 bits 亂數，不可由編號推導，請求帶了也忽略）。🔴 **`storeSharePct` 實際發生變化時另需 `n1.donation_store.share_pct`（403），並在同一次儲存寫稽核**（新舊值）；省略＝不變 |
| `POST /stores/{id}/regenerate-slug` | `…manage` | body `{ "confirm": true }`（二次確認，否則 400）。**舊 QR 立即失效**（前台視同無店家歸屬），寫稽核記錄操作者 |
| `POST /stores/{id}/logo`／`DELETE …/logo` | `…manage` | multipart 欄位 `file`；新圖寫入成功才刪舊物件 |
| `GET /stores/{id}/qr?format=png\|svg&size=` | `…manage` 或 `…export` | QR 容錯等級 **Q**、四周留白 4 模組；內容只有 `{前台網址}/zh/s/{slug}`（不帶金額或分潤參數）。`size`＝每模組像素數（預設 10，PNG 碼區約 300 像素，印刷未達 3 公分請用 SVG 或調大）。🔴 **`format=pdf` 回 `501`**：印刷版需要協會標誌（資產未到位，規劃書明文不得自造）與中文字型 |
| `GET /stores/qr-export?format=png\|svg` | `n1.donation_store.export` | 全部「合作中」店家的 QR 打包成 zip |

**N2 捐款項目**（`/projects`）

| 方法 路徑 | 權限 | 說明 |
|---|---|---|
| `GET /projects?status=` | `n2.donation_project.view` | 列表。🔴 **刻意沒有累計金額或筆數**（累計數字只存在 N6 報表） |
| `GET /projects/charity-refs` | 同上 | 撥付對象與慈善計畫候選清單（主站主檔的**唯讀複本**） |
| `GET /projects/{id}` | 同上 | 詳情（雙語欄位、金額選項、快照） |
| `POST /projects`／`PUT /projects/{id}` | `…manage` | 建立（草稿）／更新（**整筆取代**；標註「省略代表不變」的欄位例外：`slug`、`amountOptions`、`projectSharePct`、`sortOrder`）。`slug` 省略時由英文名稱自動產生，重複 `409`。金額選項最多 12 組、由小到大存、必須落在單筆範圍內。撥付對象與慈善計畫只傳參照碼，名稱**值複製**成快照（只選計畫會自動帶出所屬公益團體；組合不一致 `400`）。🔴 **`projectSharePct` 變化時另需 `n2.donation_project.share_pct` 並寫稽核** |
| `POST /projects/{id}/publish`／`/unpublish` | `…publish` | 上下架 |
| `POST /projects/{id}/cover`／`DELETE …/cover` | `…manage` | 封面圖 |

🔴 **儲存時驗證店家分潤＋項目分潤 ≤ 100%**（規劃書 §6.2，超過 `400`）：捐款是「某店 × 某項目」的組合，所以兩邊儲存都擋——店家儲存時比對**所有項目中最高的項目分潤**，項目儲存時比對**合作中店家中最高的店家分潤**（已停止的店家不算，重新啟用時會被檢查）。

**N3 捐款紀錄與異常佇列**（`/donations`）

| 方法 路徑 | 權限 | 說明 |
|---|---|---|
| `GET /donations?from&to&status&projectId&storeId&noStore&invoiceStatus&amountMin&amountMax&keyword&page&pageSize` | `n3.donation.view` | 列表（`from`／`to` 是**台灣日期**，含頭尾兩天）。列表**不含捐款人資料** |
| `GET /donations/{id}?reveal=true` | `…view`；`reveal=true` 另需 `n3.donation.reveal`（沒權限回 `403`，**不會悄悄降級成遮罩**） | 詳情：金流交易（不含 `raw_response`）、分潤快照、憑證、退款、時間軸、`needsManualReview`。🔴 **個資預設在 API 層遮罩**：姓名（`王○○明`）、Email（`a***@…`）、收據抬頭（預設就是姓名）、身分證字號（`A******789`）、載具、地址；`reveal=true` 回明文並**寫稽核**（摘要不含個資） |
| `POST /donations/{id}/refund` | `n3.donation.refund`（**僅系統管理員**） | body `{ "reason": "…" }`（2–255 字）。僅全額、僅 `paid`。先對金流退款，成功才改本站狀態；連動憑證**當期作廢／跨期折讓**與退款通知信；🔴 寫稽核（金額＋原因，不含個資）。金流拒絕 `409`、中斷 `503`，本站狀態不動。🔴 同一筆的並發退款用**交易層級 `sp_getapplock`** 串行化（拿不到鎖回 `409`「退款處理中」；實測沒有鎖時 6 個並發請求打了 6 次金流退款） |
| `GET /donations/export?…同上篩選…&purpose=` | `n3.donation.export` | 含個資的明細 CSV（UTF-8 BOM、**CSV 公式注入防護**）。🔴 **`purpose`（用途備註，≥ 4 字）必填**，稽核記錄「篩選條件＋筆數＋用途備註」；單次上限 5 萬筆，超過回 `422`（不靜默截斷） |
| `GET /donations/anomalies?kind=` | `…view` | 異常佇列：`confirm_failed`（已扣款但確認失敗）／`invoice_failed`（憑證開立失敗）／`invoice_void_pending`（已退款但憑證未作廢）／`reconciliation`（對帳差異；CH-4 已實作每日對帳排程與手動對帳，見「慈善 CH-3 補完／CH-4／CH-5」） |
| `GET /donations/anomalies/counts` | 同上 | 四類各有幾筆（導覽徽章用） |
| `POST /donations/{id}/recheck-payment` | `n3.donation.recheck_payment`（**本輪新增**） | 對 `confirm_failed` 的單重新向金流確認；成功走正常收尾 |
| `POST /donations/{id}/resend-thanks` | `n3.donation.reveal` | 補寄感謝信（會寄到捐款人 Email，所以需要能看個資的角色） |
| `POST /donations/{id}/invoice/reissue` | `n5.donation_invoice.issue` | 開立失敗的重新開立；已開立 `409` |

稽核紀錄（`audit_logs`）的動作代碼見 `CharityAuditActions`：退款、分潤設定（店家／項目）、個資明文檢視、匯出、QR 網址重產、重寄感謝信、重開憑證、重新確認付款。**稽核與被稽核的變更在同一次 `SaveChanges` 提交**；`CharityAuditLogger` 只有 `Stage` 一個公開方法（append-only，沒有更新與刪除）。稽核紀錄查詢端點（`GET /audit-logs`，僅系統管理員）已於 2026-10-02 補上，見「慈善 CH-3 補完／CH-4／CH-5」。

### 背景維護（`CharityBackgroundService`）

每 60 秒（`CHARITY_WORKER_INTERVAL_SECONDS`）跑一輪：① 逾時（`created` 超過 30 分鐘、`pending` 最近一次付款發起超過 30 分鐘 → `expired`，`CHARITY_PAYMENT_TIMEOUT_MINUTES`；**不碰待人工處理的單**）② 憑證重試（付款後第一次開立失敗的，固定間隔重試，超過 10 分鐘仍失敗標 `failed` 並通知協會，`CHARITY_INVOICE_RETRY_MINUTES`）。🔴 **Development 預設關閉**（需明確設 `CHARITY_WORKERS_ENABLED=true`）：它會改動本機種子捐款的狀態；非 Development 預設開，設 `false` 可關。失敗只記錄，不讓背景服務終止（否則會拖垮同一個行程裡的俱樂部 API）。

### 環境變數

| 變數 | 用途 |
|---|---|
| `CHARITY_SQL_CONNECTION_STRING`（必）／`JWT_SIGNING_KEY_CHARITY`（必，≥ 32 字元） | 啟用慈善平台；本機見 `deploy/dev/charity.env.example`，`scripts/init-local-settings.sh` 會一併寫進 `appsettings.Development.json` |
| `CHARITY_PUBLIC_BASE_URL` | 前台公開網址（付款返回網址、QR 目標網址）。**沒設就由 compose 本來就給 api 的 `CHARITY_DOMAIN` 組出**（`https://{網域}`，`*.localhost` 用 `http://`），再沒有才是本機預設 `http://charity.localhost`；非本機三個都沒有時付款回 503 |
| `CHARITY_ASSOCIATION_NOTIFY_EMAIL` | 接收「憑證開立失敗通知」的協會信箱；沒設就只標記失敗進佇列，不寄信 |
| `CHARITY_ALLOW_FAKE_PROVIDERS` | `true` 才允許非 Development 使用假金流／假發票／假寄信（預備環境整合驗收用） |
| `TURNSTILE_SECRET_KEY_CHARITY` | 設定後建單要求通過 Cloudflare Turnstile |
| `AZURE_BLOB_CONNECTION_STRING_CHARITY`／`AZURE_BLOB_CONTAINER_CHARITY` | 慈善圖片儲存 |
| `AZURE_BLOB_PUBLIC_BASE_URL_CHARITY` | 慈善圖片的公開網址基底（CDN 子網域），規則同 `AZURE_BLOB_PUBLIC_BASE_URL`，但**慈善自己的設定、寫在 `charity.env`，不與俱樂部共用**；未設定回退容器網域，格式錯誤啟動即失敗 |
| `CHARITY_WORKERS_ENABLED`／`CHARITY_WORKER_INTERVAL_SECONDS`／`CHARITY_PAYMENT_TIMEOUT_MINUTES`／`CHARITY_INVOICE_RETRY_MINUTES` | 背景維護 |
| `CHARITY_RECONCILIATION_AFTER_HOUR` | 每日對帳排程的啟動時間（台灣時間整點，預設 4），見「慈善 CH-3 補完／CH-4／CH-5」 |
| `DATA_PROTECTION_KEYS_PATH` | 🔴 **比主站 2FA 嚴重**：遺失金鑰環＝已加密的捐款人身分證字號永久無法解密（捐款人不登入，無從補填）。正式環境務必持久化或改接 Key Vault |

### Migration

`CharityDbContext` 的 EF 基準（比照主站 E-45 流程）：

```bash
cd apps/api
dotnet ef migrations add <名稱> --context CharityDbContext -o CharityPlatform/Data/Migrations --namespace Tcrfc.Api.CharityPlatform.Data.Migrations
# ⚠️ 該指令會把 ModelSnapshot 放到 apps/api/Tcrfc/Api/CharityPlatform/Data/Migrations/ ——要手動移回
#    CharityPlatform/Data/Migrations/ 並刪掉多出來的 apps/api/Tcrfc/ 資料夾
dotnet ef database update --context CharityDbContext
```

| Migration | 內容 |
|---|---|
| `InitialBaseline`（`20261001011658`） | **已套用的空白基準**（`Up`／`Down` 刻意清空）：29 張表是 `db/charity-schema.sql` 手寫 DDL 建的 |
| `AddAdminRefreshTokens`（`20261001011736`） | 新增 `admin_refresh_tokens`（登入更新權杖的工作階段狀態）。`Up` 是 `IF OBJECT_ID … IS NULL` 守衛過的 SQL（冪等）：`db/charity-schema.sql`（2026-10-01 起）新建的庫已經有這張表，只有 CH-1 時期的本機庫缺它 |

Probe 驗收（`migrations add Probe` → `Up`／`Down` 空 → `migrations remove`）已做過，模型與 snapshot 一致。`dotnet ef dbcontext scaffold "Name=CHARITY_SQL_CONNECTION_STRING" …` 重新 scaffold 時，`AdminRefreshToken` 的實體與 `AdminUser.AdminRefreshTokens` 導覽屬性要保留（`CharityDbContextCustomizations.cs` 不受影響）。

### 測試（`Tcrfc.Api.Tests`，新增）

| 檔案 | 內容 |
|---|---|
| `CharityDonationRulesTests` | 分潤捨去與尾差（含 2000 組隨機金額三者必加總等於金額）、單號推導、統編／身分證／手機條碼／捐贈碼檢核、憑證當期判斷 |
| `CharityDonationFlowTests` | 建單（冪等重送、**並發 8 個同鍵只建一張**、同鍵不同內容 409、驗證）、店家歸屬、付款、**確認冪等（重複 3 次、並發 6 個）**、拒絕／取消／重試、**Confirm 結果未知整條路徑**、逾時與晚到確認、憑證失敗重試與通知、寄信失敗不影響收款 |
| `CharityPublicCatalogTests` | 落地頁、卡片牆、詳情、文案、回退與不洩漏欄位 |
| `CharityAdminAuthTests` | 登入、鎖定、輪替與重放偵測、**主站與慈善權杖互不接受**、2FA、改密碼 |
| `CharityAdminStoresTests`／`CharityAdminProjectsTests`／`CharityAdminDonationsTests` | 授權矩陣、分潤獨立授權＋稽核、100% 約束、slug 重產、QR、Logo、遮罩與明文稽核、退款三連動、匯出、異常佇列與處理動作 |
| `CharityArchitectureTests` | 授權不可繞過、限流必掛、**慈善與主站互不相依**、不讀快取、稽核 append-only、回應欄位、假實作的環境防線 |
| `CharityAbuseProtectionTests` | 限流 429（獨立主機，額度 3）、Turnstile、慈善未啟用時整個不存在 |
| `CharityEfModelMatchesDatabaseTests` | `CharityDbContext` 與慈善庫逐表逐欄一致 |

測試用 `CharityApiFixture`：真的 HTTP 管線＋真的 `tcrfc_charity`（`CharityTestDatabaseGuard` 硬性限定庫名），金流／發票／寄信換成可編排替身；測試資料用可辨識標記（帳號 `ct-*@charity-test.invalid`、項目 `ct-*`、店家 `CT店家*`、Email `*@charity-test.invalid`）並在每個測試後依標記清掉，**不碰種子資料**（背景維護的 `RunOnceAsync` 有 `onlyDonations` 測試接縫，整合測試只處理自己的捐款）。

### 本輪沒做（只留介面或不在範圍）——🟢 以下除印刷版 QR PDF 外，均已於 2026-10-02 補完，見後面的「慈善 CH-3 補完／CH-4／CH-5」

- **LINE Pay／電子發票／寄信**：只有假實作與介面（見上）。
- **含店名的印刷版 QR PDF**：`501`，待協會標誌資產與中文字型。
- **N4 回饋金結算、N5 發票管理頁、N6 報表、對帳排程**（CH-4）：退款的「回饋金沖回」只改狀態，結算引擎（CH-4）依狀態排除或以負項沖回；異常佇列的對帳差異目前讀種子資料。
- **徵信名單、成果回顧、英文版系統信、N7 站台設定**（CH-5）。
- **店家 CSV 批次匯入**（規劃書 §6.1「批次作業」）。
- **後台查詢稽核紀錄的端點**。

### 待裁決（規劃書與 docs/16 都答不到，詳見 [`docs/16`](../../docs/16-charity-schema.md) §10／§11）

1. **`admin_refresh_tokens`**：規劃書沒寫這個實作機制，比照主站先例新增（含 DDL、docs、migration）。
2. **徵信名單逐筆隱藏**、**Email 軟性比對會員**：資料模型沒有落點，沒做。
3. **「待人工處理」的定義**：用「`pending` ＋最近一次付款 `failed`」，沒有專屬欄位。
4. **憑證自動重試**：沒有嘗試次數欄位，固定間隔＋總期限，非指數退避。
5. **系統信語系**：沒有記錄捐款人語系，一律繁中。
6. **`carrier_type` 值域**：寫入用代碼，讀取相容舊中文標籤。
7. **公開端點限流額度**與**Turnstile 驗證服務壞掉時放行**：規劃書沒給數字／沒說，採最小可行。
8. **新增權限碼 `n3.donation.recheck_payment`**：異常佇列的處理動作。

---

## 慈善 CH-3 補完／CH-4 帳務與報表／CH-5 延伸：後端 API（2026-10-02，`backend-engineer`）

**範圍**：慈善後台缺口與 Phase B／C 的**後端**——`N1` 店家 CSV 匯入、稽核紀錄查詢、`N2` 內文編輯、`N4` 回饋金結算、每日對帳、`N5` 憑證管理、`N6` 捐款報表、`N7` 站台設定細項，以及公開的徵信名單與成果回顧端點。**畫面由前端另做**（`apps/admin-charity` 的 `N4`–`N7` 目前仍是示範畫面，`apps/web-charity` 的 `/donors/`、`/impact/` 尚未接）。**英文版內容不在範圍**（卡內容），但所有文案欄位都有 zh／en。路徑前綴同上：後台 `/api/v1/donation-platform/admin/…`、公開 `/api/v1/donation-platform/…`；錯誤格式、時間、遮罩、稽核、冪等慣例全部沿用。

### 權限碼（新增 3 個，其餘沿用種子）

| 權限碼 | 用途 | 持有者 |
|---|---|---|
| `n4.settlement.mark_paid` | 登記結算單「已付款」。**與 `n4.settlement.execute` 分開**——規劃書 §10「已付款登記需與執行匯款者分離」，docs/16 §4.2 以權限碼分離、不落資料表 | 系統管理員（可由客戶日後指派給財務角色） |
| `n7.audit_log.view` | 查詢稽核紀錄。`sysadmin_only`：連被指派了這個碼的其他角色也看不到 | 僅系統管理員 |
| `n3.donation.hide_credit` | N3「隱藏於徵信名單」 | 系統管理員、客服／行政 |

三個碼以 EF migration `AddCh4Ch5Permissions` 補進已建好的庫（冪等），新建庫則由 `db/seed/generate-charity-seed-sql.py` 的 `EXTRA_PERMISSIONS` 與 `db/prod/charity-reference-data.sql` 取得（同一組決定性 id）。⚠️ **種子角色只有系統管理員持有 N4 的 `execute`／`mark_paid`**——規劃書九個角色裡沒有「財務」，要讓「核對的人」與「登記付款的人」是不同人，需要客戶決定把哪個角色（或新增帳號）指派 `n4.settlement.execute` 與 `n4.settlement.mark_paid`（見「待決」）。

### N1 店家 CSV 匯入（規劃書 §6.1「批次匯入店家資料（CSV）」）

| 方法 路徑 | 權限 | 說明 |
|---|---|---|
| `GET /stores/import-template` | `n1.donation_store.manage` | 下載範本 CSV（UTF-8 BOM；表頭＋一列範例）。範本本身可直接匯入 |
| `POST /stores/import?skipDuplicates=` | `…manage`（分潤欄位非 0 另需 `…share_pct`） | body 是 **CSV 原始位元組**（`Content-Type: text/csv`，UTF-8，有無 BOM 皆可，上限 1 MB、500 列）。成功 `200 { importedCount, errors: [], skipped: [{rowNumber, reason}] }`；**任一列有錯整批不寫入**，回 `400` 同形狀（`importedCount: 0`，`errors: [{rowNumber, reason}]` 列出所有問題，列號與 Excel 一致，表頭是第 1 列） |

- **表頭（依序完全相符）**：`店家名稱（繁中）,店家名稱（英文）,類別,地址,聯絡人,電話,合作開始日,合作結束日,狀態,店家分潤（%）`。日期 `yyyy-MM-dd`（也收 `yyyy/M/d`）；狀態 `合作中`／`已停止`（空白＝合作中）；分潤空白＝0。`store_slug` **一律由系統產生**，檔案沒有這一欄。
- **重複判定與冪等**（規劃書只寫「可批次匯入」，下列是執行層決定）：因為 slug 由系統產生，沒有可靠的自然鍵能做更新，匯入**只做新增**；**繁中店名＋地址（去空白、不分大小寫）相同**視為重複，對照既有全部店家（含已停止）與檔案較前面的列。預設重複＝錯誤（整批不寫入），所以**同一份檔案重複匯入不會產生重複店家**；`skipDuplicates=true` 時重複的列改列入 `skipped`，其餘合格的照常匯入。
- 分潤非 0 的列：持有 `…share_pct` 才能匯入（否則整批 `403`），每家各寫一筆 `store.share_pct_set` 稽核；仍受「店家＋項目最高分潤 ≤ 100%」限制（該列報錯）。整批另寫一筆 `store.import_csv` 稽核。

### N2 內文編輯

| 方法 路徑 | 權限 | 說明 |
|---|---|---|
| `PUT /projects/{id}/content` | `n2.donation_project.manage` | **只編輯內文三項**（`oneLinerZh/En`、`descriptionZh/En`（區塊編輯器 JSON）、`fundUsageZh/En`）。**省略（`null`）的欄位不變**；說明內文送 `{}` 或 `[]`、其他欄位送 `""` 代表清空。不會碰分潤、金額選項、撥付對象，所以商務角色（沒有分潤權限）也能存。英文列不存在時以繁中名稱頂著（欄位必填，前台本來就會回退）。寫 `project.content_update` 稽核 |

### 稽核紀錄查詢

| 方法 路徑 | 權限 | 說明 |
|---|---|---|
| `GET /audit-logs?from&to&action&targetType&targetId&adminUserId&keyword&page&pageSize` | `n7.audit_log.view`（僅系統管理員） | 由新到舊。每筆：`occurredAt`、`adminName`、`action`＋`actionLabel`（日常中文）、`targetType`＋`targetTypeLabel`、`targetId`、`targetLabel`（捐款單號／店家名／項目名）、`changeSummary`、`purposeNote`、`sourceIp`。`from`／`to` 是台灣日期。摘要與用途備註**不含個資明文** |
| `GET /audit-logs/actions` | 同上 | 篩選下拉：所有動作代碼與中文標籤 |

只有查詢，**沒有任何寫入或刪除端點**（append-only，`DELETE`／`PUT`／`POST` 回 405）。

### N3 補充：徵信名單逐筆隱藏

| 方法 路徑 | 權限 | 說明 |
|---|---|---|
| `POST /donations/{id}/credit-visibility` | `n3.donation.hide_credit` | body `{ "hidden": true\|false }`，回 `{ donationId, isAnonymous, isCreditHidden }`。寫 `donation.credit_visibility` 稽核（沒變化的重複操作不重複寫）。N3 列表與詳情的 DTO 新增 `isCreditHidden` |

資料落點是 `donations.is_credit_hidden bit NOT NULL DEFAULT 0`（規劃書 §3.6／§6.3 明文要求逐筆隱藏、資料模型原本沒有落點，見 docs/16 §11）。

### N4 回饋金結算（規劃書 §6.4、§8）

狀態：`pending`（待結算＝草稿）→ `settled`（已結算＝鎖定金額）→ `paid`（已付款＝鎖定）。**店家與項目分開結算**（一份結算單一個對象）；只計 `paid` 的捐款、用捐款成立當下的**快照分潤金額**（改設定不追溯）；無店家歸屬的捐款只進項目結算。

| 方法 路徑 | 權限 | 說明 |
|---|---|---|
| `GET /settlements?status&payeeType&payeeId&from&to&page&pageSize` | `n4.settlement.view` | 列表。每筆含對象名稱、期間、正項筆數與捐款總額、**沖回負項筆數與金額**、`payableAmount`（含負項，可能為負）、匯款三項登記、`settledAt/By`、`paidRegisteredAt/By`（取自稽核） |
| `GET /settlements/{id}` | 同上 | 詳情＋逐筆明細（單號、付款時間、捐款金額、**分潤率快照**、應付、是否沖回、**沖回原因**）；正項在前、沖回負項在後 |
| `GET /settlements/{id}/export` | `n4.settlement.export` | 對帳單 CSV（UTF-8 BOM）。**不含任何捐款人資料**（對帳單會交給店家或撥付對象）。前台列印版用詳情 JSON 渲染 |
| `POST /settlements/run` | `n4.settlement.execute` | body `{ periodStart, periodEnd, payeeType?, payeeId? }`（台灣日期；`payeeType` 省略＝店家與項目都做）。為每個**有應付金額**的對象各產一份草稿，回 `{ created: [...], skipped: [{payeeType, payeeId, payeeName, reason}] }`。`periodEnd` 必須早於今天（`422`）。**冪等**：已進過結算單（含草稿）的捐款不會再進；同一對象期間與既有結算單重疊的略過並說明 |
| `POST /settlements/{id}/recalculate` | 同上 | 剔除已退款的捐款（回 `removedOrderNos`）；**草稿**另納入期間內新符合的捐款與新的沖回負項；**已結算**只扣除不新增；已付款 `409` |
| `POST /settlements/{id}/settle` | 同上 | 待結算 → 已結算。確認前自動重算一次，沒有任何明細回 `409` |
| `POST /settlements/{id}/mark-paid` | **`n4.settlement.mark_paid`** | body `{ remittedOn, remitMethod, remitNote? }`。已結算 → 已付款；**實際匯款在系統外執行，只登記日期、方式、備註**。日期不可晚於今天 |
| `DELETE /settlements/{id}` | `n4.settlement.execute` | 只能刪待結算草稿（`204`）；已結算與已付款是帳務紀錄，`409` |

**退款沖回（§8.5 三種情況）**：①尚未結算——草稿重算／確認結算時剔除；②已結算未付款——`recalculate` 從該期扣除、重出對帳單；③**已付款**——不追討，下一次為該對象產生結算單時自動納入**負項**（金額＝原分潤的反向、`isClawback: true`、`clawbackReason`＝「捐款單 {單號} 已退款：{退款原因}」），同一筆只沖回一次。

**並發**：產生／重算／確認／刪除草稿共用交易層級 `sp_getapplock`（等待上限 15 秒，拿不到回 `409`「結算處理中」），測試覆蓋 4 個並發請求只產生 1 份、明細無重複；資料層另有唯一索引 `(settlement_id, donation_id, is_clawback)`。每次狀態異動寫稽核（`settlement.run`／`recalculate`／`settle`／`mark_paid`／`delete_draft`）。

### 每日對帳（規劃書 §4.5；異常佇列「對帳差異」的資料來源）

來源介面 `IPaymentReconciliationSource`（`CharityPlatform/Payments/`，**另一個介面**，與 `IPaymentGateway` 同屬協會商店號）：`ListTransactionsAsync(taiwanDate)` 回金流端當天扣款成功的 `(TransactionId, Amount, OccurredAtUtc)`。🟡 **目前只有 `FakePaymentReconciliationSource`**（以本站自己的金流紀錄當明細，所以本機永遠一致；只在 `CharityFakeGuard` 允許的環境運作，正式環境丟「尚未設定」）。正式串接卡 `B-7`，換 `CharityPlatformRegistration` 一行 DI。

| 方法 路徑 | 權限 | 說明 |
|---|---|---|
| `GET /reconciliation/runs?from&to&onlyPending&page&pageSize` | `n3.donation.view` | 對帳批次列表（`status`: `completed`／`failed`；各批次的 `pendingCount` 是還沒處理的差異數） |
| `GET /reconciliation/runs/{id}` | 同上 | 批次＋差異明細（`type`: `site_only` 本站有金流無／`gateway_only` 金流有本站無／`amount_mismatch` 金額不符；連到捐款單號；待處理在前） |
| `POST /reconciliation/runs` | `n3.donation.recheck_payment` | body `{ date? }`（省略＝昨天；不可是未來）。**手動對帳，可重跑任一天**。取不到金流明細回 `503`（該日記為失敗批次，**不會把當天所有捐款判成差異**；已有成功批次不被覆蓋） |
| `POST /reconciliation/discrepancies/{id}/resolve` | 同上 | body `{ note }`（2–255 字必填）。待處理 → 已處理，只能處理一次（`409`）；**差異記錄不刪除** |

- 比對鍵是金流交易識別碼；本站側取「當天付款成立且狀態 `paid` 或 `refunded`」的捐款（當天付款後來退款的不誤判）。金流端剩下的若本站有同交易識別碼（例如「確認結果未知」的 pending 單），差異連到那張捐款單。
- **重跑冪等**：`(run_on, source)` 唯一，同一天重跑更新同一批次；新差異新增、既有差異保留（含人工已處理的決定）、先前待處理現在已一致的**自動標記已處理**（備註「重新對帳後兩邊已一致（系統自動標記）」）。同日同來源以 `sp_getapplock` 串行化。
- **排程**：`CharityBackgroundService` 每輪（預設 60 秒）檢查，台灣時間過了 `CHARITY_RECONCILIATION_AFTER_HOUR`（預設 4）後對**昨天**對帳一次；已有成功批次略過、失敗批次 30 分鐘後才重試。🔴 刻意**不放進** `CharityMaintenanceRunner.RunOnceAsync`（整合測試會直接呼叫它，對帳的寫入副作用不該進每個測試）。Development 預設背景工作關閉，需明確開。
- 對帳結果**保留供稽核**：批次與差異沒有任何刪除端點；差異只更新處理狀態。

### N5 憑證管理（規劃書 §6.5）

重新開立沿用 `POST /donations/{id}/invoice/reissue`（`n5.donation_invoice.issue`）。

| 方法 路徑 | 權限 | 說明 |
|---|---|---|
| `GET /invoices?from&to&issueStatus&voidStatus&invoiceType&projectId&keyword&page&pageSize` | `n5.donation_invoice.view` | 列表（期間依捐款付款時間；`keyword` 比對單號或憑證號碼開頭）。**不含捐款人姓名、Email、載具、身分證字號、地址**，統編發票另有統編與抬頭（商業登記公開資訊） |
| `POST /invoices/{id}/manual-number` | `n5.donation_invoice.issue` | body `{ invoiceNo, issuedOn?, reason }`。手動填入外部號碼：`pending`／`failed` → `issued`，寄出「憑證通知」信。號碼大寫英數與連字號 4–32 字，重複 `409`；已開立／已作廢 `409` |
| `POST /invoices/{id}/void` | `n5.donation_invoice.void` | body `{ reason }`。**限開立當期**（跨期 `409`，請折讓）；尚未開立的憑證直接標記作廢、不呼叫加值中心；加值中心連不上 `503`。記錄 `void_reason`／`voided_by`＋稽核 |
| `POST /invoices/{id}/allowance` | 同上 | body `{ reason }`。已開立才能折讓（全額；部分折讓不在範圍） |
| `GET /invoices/export?…同列表篩選…` | `n5.donation_invoice.view` | 供會計申報的明細 CSV（UTF-8 BOM）。**不含個資**，寫一筆 `invoice.export` 稽核。單次上限 5 萬筆 |

### N6 捐款報表（規劃書 §7）

五張固定報表，**共用同一個資料來源與投影**（不同報表的數字永遠對得上）。共通查詢參數：`from`、`to`（台灣日期，依**付款時間**）、`storeId`、`noStore=true`（只看無店家歸屬）、`projectId`、`paymentStatus`（`paid` 預設／`refunded`／`all`＝含已退款看毛額）、`invoiceType`（`b2c_invoice`／`donation_receipt`）、`amountMin`、`amountMax`。`format=csv` 匯出（UTF-8 BOM）需要 `n6.report.export`——**能看不等於能匯出**，沒有匯出權限回 `403`、不會悄悄降級成 JSON。**全部報表（含逐筆明細）不含捐款人個資**（§7.3「不含個資的彙總報表不受限」）；要個資走 N3 的含個資匯出（額外授權＋用途備註＋稽核）。

| 方法 路徑 | 權限 | 內容 |
|---|---|---|
| `GET /reports/overview?…&granularity=day\|month` | `n6.report.view` | 筆數、總額、平均單筆、**轉換率**（母體＝期間內**建單**的捐款單，分子＝其中曾成功付款〔`paid` 或後來退款〕）、`trend: [{period, count, amount}]`（台灣日期／月份） |
| `GET /reports/by-store` | 同上 | 每店：筆數、金額、佔比、**目前**分潤率、應付回饋金、**已結算／未結算**（只計已付款；「已結算」＝進了已確認或已付款的結算單）。無店家歸屬合併一列（`storeId: null`） |
| `GET /reports/by-project` | 同上 | 每項目：筆數、金額、佔比、平均單筆、目前分潤率、應撥付金額與已結算／未結算。⚠️ **不輸出「目標達成率」**：規劃書 §7.2 與 §1.2／§6.2 自相矛盾（明文不設目標金額），見 docs/16a |
| `GET /reports/invoice-status` | 同上 | 已開立／待開立／**失敗**（前台可連到 N5）／已作廢／已折讓／無憑證資料 的筆數 |
| `GET /reports/details?…&page&pageSize` | 同上 | 逐筆明細（單號、時間、狀態、金額、項目、來源店家、憑證狀態、具名／匿名、三方分潤金額）。單次 CSV 上限 20 萬筆，超過 `422` |

### N7 站台設定（規劃書 §6.7）

| 方法 路徑 | 權限 | 說明 |
|---|---|---|
| `GET /settings`／`PUT /settings` | `n7.setting.view`／`…manage` | 首頁說明、感謝語樣板、捐款須知、隱私權政策（各 zh／en：`homeIntroZh`…）、`clubSiteUrl`（俱樂部官網網址，成果回顧導回用）、`defaultMinAmount`／`defaultMaxAmount`（全站單筆金額預設範圍，1～10,000,000，上限不可小於下限）、`creditListEnabled`（徵信名單整站開關）。**`PUT` 局部更新：沒帶（`null`）的欄位不變，文案送 `""` 清空**；先全部驗證才寫入。稽核只記改了哪些欄位、不記文案 |
| `GET /email-templates`／`PUT /email-templates/{code}` | 同上 | 四封系統信（`donation_thanks`／`invoice_issued`／`invoice_failed`／`refund_notice`）：`label`、`isActive`、可用標記 `tokens`（`{order_no}` 等及說明）、`zh`／`en` 的主旨與本文。英文主旨與本文要同時填或同時空（同時空＝移除英文版）。**更新後下一封信立即套用**。目前系統信仍一律寄繁中（捐款單沒有語系欄位，見待決） |
| `GET /payment-channels` | `n7.payment_channel.manage`（僅系統管理員） | 兩個管道（`line_pay`／`einvoice`）：`activeEnvironment`、各環境（`sandbox`／`production`）是否已設定憑證、發票字軌、換金鑰時間。**永遠不回傳憑證內容** |
| `PUT /payment-channels/{channelType}/credential` | 同上 | body `{ environment, credential, invoicePrefix? }`。憑證以 Data Protection 專用用途加密後寫入（只寫不讀）；稽核不記憑證 |
| `PUT /payment-channels/{channelType}/environment` | 同上 | body `{ environment, confirm: true }`。切換「作用中」環境（測試／正式）。**切到正式前必須已設定該環境憑證**（電子發票另需字軌），否則 `409`；未二次確認 `400`。作用中環境存在 `settings`（`payment.active_environment.{channelType}`），沒設定時依執行環境推定；**電子發票開立讀的字軌已改為依作用中環境**（`CharityInvoiceService`）。假實作的環境防線（`CharityFakeGuard`）不因切到測試而放寬 |

### 公開端點（CH-5，不需登入）

| 方法 路徑 | 限流 | 說明 |
|---|---|---|
| `GET /credit-list?projectSlug&from&to&page&pageSize` | `charity-public-read` | 捐款徵信名單（規劃書 §3.6）。回 `{ enabled, names: string[], page, pageSize, totalCount }`——**只有姓名**：不含金額、Email、店家、單號、時間、捐款次數。公開條件缺一不可：`paid`、捐款人在表單明示選**具名**（規劃書 §11.1「以明示同意為前提」＝表單的具名／匿名單選）、後台沒有逐筆隱藏、站台沒有整站關閉（`enabled: false` 時 `names` 為空）。**同名去重、依姓名排序**（不洩漏捐款次數與時間先後）。預設每頁 100、最多 200 |
| `GET /impact?lang=zh` | 同上 | 成果回顧頁（`/{lang}/impact/`）：已上架且關聯了慈善計畫（或只關聯公益團體）的項目依計畫分組，`programs: [{ programRefCode, programName, charityName, projects: [{slug, name, oneLiner, coverUrl, coverAlt}] }]`＋`clubSiteUrl`（N7 設定，未設為 `null`＝前台不顯示導回連結）。名稱是項目的**快照**，不即時查主站（規劃書 §9.3）；成果數據與故事在主站，這裡只負責導流。英文缺漏回退繁中並標 `isFallback` |

兩個端點都掛依 IP 的「公開讀取」限流（徵信名單會成批輸出人名）；`CharityArchitectureTests` 掃：這兩條路由一定掛限流。

### Migration（本輪）

| Migration | 內容 |
|---|---|
| `AddCreditHiddenAndSettlementLineKey`（`20261002042845`） | `donations.is_credit_hidden`、唯一索引 `UQ_settlement_lines_settlement_donation_kind`。`IF COL_LENGTH／NOT EXISTS` 守衛（冪等）：`db/charity-schema.sql` 新建的庫已有 |
| `AddCh4Ch5Permissions`（`20261002043039`） | 三個新權限碼與系統管理員／客服角色對應（參考資料，`NOT EXISTS` 守衛）。`Down` 刻意為空：權限碼可能已被指派 |

### 環境變數（新增）

| 變數 | 用途 |
|---|---|
| `CHARITY_RECONCILIATION_AFTER_HOUR` | 每日對帳排程：台灣時間幾點（整點，0–23，預設 4）以後才對前一天對帳。只在背景工作啟用時運作 |

### 測試（新增）

`CharityAdminSettlementsTests`（12）、`CharityReconciliationTests`（8）、`CharityAdminStoreImportTests`（8）、`CharityAdminInvoicesTests`（7）、`CharityAdminReportsTests`（10）、`CharityAdminSettingsTests`（10）、`CharityCreditListAndImpactTests`（7，含獨立主機的限流測試）、`CharityAuditAndContentTests`（7）；`CharityArchitectureTests` 改為掃兩個端點檔與所有 `*AdminService`／`*QueryService`。測試用 `ScriptedReconciliationSource`（來源代號 `ct-recon`）編排金流明細；N7 測試**整組拍下 `settings`／`email_templates`／`payment_channels` 再原樣還原**（含 `updated_by`，否則測試帳號被外鍵卡住無法清理）。

### 本輪沒做 / 待裁決

- **沒做**：含店名的印刷版 QR PDF（仍 `501`）、定期定額捐款、年度彙總收據的批次開立（`is_annual_summary` 的年底作業）、英文版系統信寄送（捐款單沒有語系欄位）、「以 Email 軟性比對會員」（本庫不得持有主站連線，主站尚無唯讀 API）。
- **待決（規劃書與 docs/16 都答不到，詳見 [`docs/16`](../../docs/16-charity-schema.md) §11 的 CH-4／CH-5 補記）**：①誰持有 `n4.settlement.execute`／`mark_paid`（種子只有系統管理員）；②**淨額為負的結算單**（上期已付款的退款沖回大於本期應付）目前照常產生、允許登記「付款」（代表對象退款給協會），要不要改成遞延到下期？；③**金流手續費歸屬**（規劃書 §13 第 9 項）——結算以毛額計、手續費由協會留存吸收，對帳不處理手續費差；④N5 匯出 CSV 刻意不含個資，若會計申報捐贈收據需要捐款人身分證字號，需另決定走哪個授權；⑤作廢憑證後的「補開替代憑證」流程（現行重新開立會被「已作廢」擋下）；⑥對帳邊界：以 `paid_at` 的台灣日期對金流端交易日，跨午夜的交易可能出現在相鄰兩天的差異，由人工處理。

---

## 維運指令：`--hash-password`（正式庫第一個管理員，2026-10-01）

`dotnet Tcrfc.Api.dll --hash-password`（容器內：`docker run --rm -i <api 映像檔> --hash-password`）：從**標準輸入**讀一行密碼，把與後台登入驗證同一份 `PasswordHasher`（Argon2id）算出的雜湊印到標準輸出，**不啟動 Web 主機、不讀設定、不連資料庫**。退出碼 0 成功、1 密碼不合政策（至少 9 字元）或未提供、2 內部錯誤；錯誤訊息不含密碼。實作在 `Security/PasswordHashCli.cs`，`Program.cs` 最前面分流；測試 `Tcrfc.Api.Tests/PasswordHashCliTests.cs`。
呼叫端是 `deploy/prod-db-init.sh create-admin` 與 `reset-password`（`infra/README.md` §4.8）。🔴 要先 push 並部署新版 api 映像檔，這兩個子命令才接受 9 字元密碼（舊映像檔仍要求 10 字元）。**不要**改成接受命令列參數或環境變數的密碼——那會進 `ps`／shell 歷史。

---

## G 批（2026-10-02，`backend-engineer`）：S1-18 認證端點限流收尾、S0-7h 文章封面欄位組、S2-5 由地址定位、AP-3 App 更新權杖鏈

> 🔴 **本批的整合測試在撰寫當下沒有執行**：撰寫環境無法取得資料庫連線憑證（不得在指令列帶密碼、也不得自行載入 `.env`），所以只跑了不需要資料庫的測試（程式與測試皆編譯通過，`ArchitectureTests`、更新權杖編碼、地址定位替身、限流額度解析共 39 項通過）。**需要在有 `tcrfc_club` 的環境（`./db/seed/setup-club-db.sh` → `dotnet test`）完整跑一次；跑之前先對 `tcrfc_club` 套用 migration `AlignSchemaG1`（或執行 `db/club-schema.sql` 對應的三個欄位：`articles.cover_width`／`cover_height`、`articles_i18n.cover_alt`）**，否則文章相關的既有測試會因欄位不存在而失敗。新增約 35 項測試，見各節。

### 1. S1-18 認證端點限流收尾

盤點結果：`/login`、`/refresh` 的依 IP 限流**早已在 S1-18d（2026-09-29）完成**（`admin-login` 每 IP 每分鐘 5 次、`admin-refresh` 30 次；STATUS 的「後台登入端點尚無 IP 限流」是過期敘述，已更正，見 `docs/18` E-118）。本批補的是**漏網的三支「已登入後仍驗證密碼／TOTP」的端點**：

| 端點 | 政策 | 理由 |
|---|---|---|
| `POST /api/v1/admin/auth/change-password` | `admin-credential-check`（每 IP 每分鐘 10 次，`ADMIN_CREDENTIAL_CHECK_RATE_LIMIT_PERMIT_LIMIT` 可調，未設定／非正整數退回 10） | 持有被竊的短效存取權杖者可把它當「現行密碼」猜測神諭，帳號鎖定機制不會被這些端點觸發 |
| `POST …/2fa/confirm`（驗證 6 位數碼）、`POST …/2fa/disable`（驗證密碼） | 同上 | 同上 |

- **刻意豁免**（`ArchitectureTests` 明列）：`/logout`（只清 Cookie＋撤銷單筆權杖）、`/me`（唯讀、需 JWT）、`/2fa/setup`（需 JWT，只產生尚未啟用的密鑰、不驗證任何憑證）。
- **架構測試改成預設全擋**：`AdminAuth所有認證端點除明列豁免外都必須掛限流政策`——掃 `AdminAuthEndpoints.cs` 內所有 `Map*`，沒掛 `.RequireRateLimiting` 又不在豁免清單就紅燈；日後新增認證端點忘了掛會直接被擋。
- 測試：`AdminAuthRateLimitingTests` 加 3 項（三個端點各自超過額度回 429，Theory）、`AdminAuthRateLimitPoliciesTests` 加 1 項（預設額度與壞設定退回嚴格預設）；`TestRateLimitOverrides` 把一般 fixture 的新政策額度也放寬。
- 慈善後台（`CharityPlatform`）的同類端點在另一個資料庫那側，本批未動。

### 2. S0-7h `articles` 封面圖片欄位組

- **綱要**（先文件、再 DDL、再 migration）：`articles.cover_width`／`cover_height`（`int NULL`）、`articles_i18n.cover_alt`（`nvarchar(200) NULL`）。migration **`AlignSchemaG1`**（冪等：先 `COL_LENGTH` 查再加，正式庫是 DDL 建的、本機庫不是，兩邊都能套）。`docs/12` §12 第 49 點、`12a`、`12c`、`12d` §11／§12 已同步。
- **上傳流程**：`UploadedImageInfo.Width/Height`（主檔縮小後尺寸）寫回；新建、換圖、移除封面三態都與物件鍵同進同出（`CoverKeyUpdate` 帶寬高；移除封面一併清空寬高）；不夾檔案只改 Alt 時寬高維持。
- **後台契約**：`AdminArticleLocaleContent` 新增 `coverAlt`（zh／en 各一，**不要求必填**，規劃書未要求）；`AdminArticleDetailDto` 新增 `coverWidth`／`coverHeight`。→ **前端要做**：文章編輯頁封面圖片旁加「圖片說明（替代文字）」中英兩個輸入框，送出時放進 `content.zh.coverAlt`／`content.en.coverAlt`；封面預覽可用 `coverWidth`／`coverHeight` 預留版面。
- **公開契約**：`ArticleListItemDto`、`ArticleDetailDto` 新增 `coverWidth`／`coverHeight`／`coverAlt`（已依語系回退，英文空白回中文；無封面時三者皆 `null`）。→ **前台要做**：新聞卡片與詳情的封面 `<img>` 帶 `width`／`height`，`alt` 用 `coverAlt`，空值回退文章標題；`Article` JSON-LD 的 `image` 可帶寬高。OG 圖片優先序最後一層（封面回退）改為輸出封面的寬高與封面 Alt。
- **同規則其他表**：全表重掃見 `docs/12d` §12——**24 個圖片欄位缺寬高、30 個缺 Alt**，本批只補 `articles`（理由：每個缺口都要同時接上對應模組的上傳流程、DTO 與前台，只補欄位會留下永遠為空的假象；標誌／圖示類是否需要 Alt 是規格解讀，需確認）。
- **S0-7h 另兩項「待確認」**（置頂精選限 3 逐俱樂部、狀態轉換規則）**未動**，仍待客戶確認。
- 測試：`AdminNewsCoverFieldGroupTests` 5 項（建立寫回寬高與雙語 Alt、大圖存縮小後尺寸且依 EXIF 轉正、不換圖只改 Alt／換圖／移除封面三態、公開 API 帶出寬高與依語系回退的 Alt 與 OG 回退、無封面時一律 null）。

### 3. S2-5 特約店家「由地址定位」

- **供應商已定 Google Maps Geocoding API**（使用者 2026-10-02 拍板；規劃書只寫「由地址定位輔助按鈕，**人工確認後儲存**，不做執行期即時 geocoding」，供應商屬執行層）。比照 `IPaymentGateway` 慣例：`Features/Geocoding/IGeocoder`＋`GoogleGeocoder`（`GEOCODER=google`）＋`NotConfiguredGeocoder`（Production 預設）＋`LocalFakeGeocoder`（Development；`GEOCODER=fake` 在 Production 啟動即失敗）。金鑰 `GOOGLE_MAPS_GEOCODING_API_KEY`（`club.env`）缺值時**優雅降級**（比照 LINE 登入，預覽 503、存檔不阻擋，不讓啟動失敗）。使用者要做的 Google Cloud 設定與條款風險寫在 `docs/17` §3「G 批的接縫」。
- **Google 版行為**（`GoogleGeocoder.cs`）：請求帶 `region=tw`、`language=zh-TW`、`components=country:TW`，逾時 5 秒，用 `IHttpClientFactory` 具名用戶端。`OK`→取**第一筆**；`ZERO_RESULTS`→查無（404／`not_found`）；`OVER_QUERY_LIMIT`／`OVER_DAILY_LIMIT`／`REQUEST_DENIED`／`INVALID_REQUEST`／`UNKNOWN_ERROR`／HTTP 非 2xx／網路錯誤／逾時／無法解析→拋 `FeatureNotConfiguredException`（`geocoder_unavailable`，503／`unavailable`），存檔不阻擋。**精度規則**：DTO 只有 lat／lng、無法標註精度，所以 `location_type=APPROXIMATE`（只到行政區／路段中心），或 `partial_match=true` 且不是 `ROOFTOP`，一律**當查無**（寧可請管理者手動輸入，也不回誤導的點）。呼叫端自行取消不算供應商故障。`LocateAsync`（預覽）也把 `HttpRequestException`／`TimeoutException` 轉成 503（原本會 500）。
- 🔴 **金鑰不外洩**：Geocoding 只接受 `key` 查詢字串，`IHttpClientFactory` 預設會把完整 URL 寫進日誌，所以註冊用 `GoogleGeocoderRegistration.AddGoogleGeocoder`（內含 `RemoveAllLoggers()`）；程式只記狀態碼／狀態字串，不記 URL、`error_message`、例外訊息、地址原文，拋出的例外不帶 inner exception。
- **端點**（權限 `member.store.create` 或 `member.store.update` 任一）：
  - `POST /api/v1/admin/{club}/partner-stores/locate`，body `{ "address": "…" }` → `200 { lat, lng }`／`404`（查無，訊息為日常中文）／`400`（空地址、超過 500 字）／`503 geocoder_not_configured`。**只回候選座標，不寫入任何資料**——這是「人工確認後儲存」的按鈕。
  - 新增／更新店家（multipart `payload`）新增 `autoLocate: bool`（預設 `false`）：`true` 且**沒有手動座標**時，儲存時由伺服器依**中文地址**定位。**手動 `lat`／`lng` 永遠優先**（可覆寫任何自動結果）；定位失敗**不阻擋存檔**，座標留空。
  - 新增／更新的回應多一個 `autoLocateStatus`：`skipped`（沒要求或已手動填）／`located`（已自動填入，請管理者確認）／`not_found`（查無或沒有地址）／`unavailable`（服務未啟用或供應商故障）；`GET` 不帶此欄。
- → **前端要做**：K4 店家表單地址旁加「由地址定位」按鈕（呼叫 `/locate`，把結果填進緯度／經度欄讓人確認）、表單加「儲存時由地址定位」勾選；儲存後依 `autoLocateStatus` 顯示提示（用語見 `docs/06` 對照表）。
- **與任務敘述的差異**：交辦寫「後台存檔時自動定位」，規劃書寫「人工確認後儲存」。本批把自動定位做成**管理者明確勾選才發生**，預設不自動，兩者並存；是否要改成預設勾選屬產品決定。
- 地址不寫入日誌；App 與前台訪客的請求永遠不會觸發定位。
- 測試：`GoogleGeocoderTests` 27 項（不需資料庫、**不打真實 Google API**，假 `HttpMessageHandler`：各狀態、精度規則、HTTP 非 2xx、網路錯誤、逾時、呼叫端取消、無法解析、缺金鑰不發請求，以及日誌／例外不含金鑰與地址；已用拿掉 `RemoveAllLoggers()` 做負向對照確認會變紅）、`GeocoderTests` 3 項（不需資料庫）、`AdminPartnerStoreLocateTests` 4 項（權限、預覽不寫入與查無／空地址、自動定位的手動優先／未要求／查無／故障／更新、未串接時 503 與存檔仍成功）。

### 4. AP-3（後端部分）App 更新權杖鏈

完整設計決定見 `docs/19` §4「伺服器端實作」。重點：

- **掛在 `app_devices` 的既有四個欄位**，不動綱要。權杖格式 `ad1.{裝置列 id}.{簽發毫秒}.{亂數}.{HMAC 簽章}`——因為規劃書只給四欄、沒有「前一把雜湊」，簽章＋簽發時間讓「已被輪替掉的真權杖」與「亂猜的垃圾」可區分：後者無副作用拒絕，前者觸發重用偵測。
- **沿用既有會員端點，不另開一套**：`POST /api/v1/member/auth/login`（以及 `/auth/line/callback`、`/auth/line/complete`、`/auth/change-password`）新增選填 `deviceInstallId`——帶了就把鏈掛在該裝置並強制 body 交付（回應含 `refreshToken`／`refreshTokenExpiresAt`，`ad1.` 開頭）；`/auth/refresh`、`/auth/logout` 依前綴分流。裝置須先 `PUT /api/v1/app/devices/{id}` 註冊，否則 `400 device_not_registered`。
- **新端點**（會員 Bearer）：`GET /api/v1/member/devices`（自己的裝置：`deviceId`、`platform`、`osVersion`、`appVersion`、`lastActiveAt`、`hasActiveSession`，不含裝置識別碼與推播權杖）、`POST /api/v1/member/devices/{deviceId}/revoke`（`204`；別人的裝置一律 `404`；掛 `member-write` 限流）。**規劃書 §4.3 只硬性要求「登出全部裝置」，單一裝置撤銷是執行層補充，無畫面規格。**
- **撤銷涵蓋**：登出、登出全部裝置、改密碼、重設密碼、刪除帳號、會員被停用、重用偵測、會員自行撤銷某裝置；撤銷後解除 `member_id` 綁定（裝置列與推播訂閱保留，符合 App 規劃書 §4.4）。`MemberSessionService.RevokeAllAsync` 現在同時撤銷網頁鏈與全部裝置鏈。
- ⚠️ **已知取捨**：「伺服器已輪替、回應在途中遺失」的重試會被判為重用而登出該裝置（沒有前一把雜湊可做寬限）；細節與選項見 `docs/19` §4。
- 測試：`AppRefreshTokenCodecTests` 8 項（不需資料庫）、`AppDeviceSessionTests` 11 項（發放只存雜湊並綁定、輪替且時間遞增、重用偵測連新權杖一起失效與重新登入恢復、偽造簽章不登出任何人、登出、登出全部與改密碼、裝置撤銷與別人裝置 404、裝置未註冊、過期不算重用、會員停用、網頁鏈不受影響）。

### 本批的待決事項

1. ~~地址定位供應商~~ **已定 Google Maps（2026-10-02）**；剩**使用者申請並限制金鑰**、設預算告警與每日配額（`docs/17` §3），以及 🔴 **Google 條款對座標長期儲存（30 日）與「不得與非 Google 地圖併用」的法遵風險**（`docs/17` §7 風險 13，待使用者確認、未改設計）。
1a. ~~後台店家表單把任何 503 都當成「尚未串接」~~：**已修（2026-10-02）**——`PartnerStoreEditView.vue` 讀回應的 `code`，`geocoder_unavailable`（供應商暫時故障／額度用盡）只提示稍後再試、不停用按鈕；其餘 503（`geocoder_not_configured`）才停用。
2. **標誌／圖示類圖片要不要 Alt**，以及其餘 24／30 個圖片欄位何時補——`docs/12d` §12。
3. **自動定位要不要預設勾選**（目前預設不勾，符合「人工確認後儲存」）。
4. **S0-7h 兩項**：置頂精選限 3 是否逐俱樂部、`publish`／`schedule` 狀態轉換規則——仍待客戶確認。
5. **App 更新權杖「回應遺失重試」被登出**的容忍度——若實測太常發生，需向規劃書 §10.1 申請增列「前一把權杖雜湊」欄位。
6. **單一裝置撤銷端點**是否需要對應 App 畫面（規劃書只要求登出全部裝置）。

---

## H 批（2026-10-02，`backend-engineer`）：A 儀表板、P4 試訓公開報名、G-09 電子報訂閱、G-02 全站搜尋、I 網站設定其餘子模組

> 🔴 **本批的整合測試在撰寫當下沒有執行**：工作樹沒有 `appsettings.Development.json`、也不得載入 `.env`，依賴資料庫的新測試**只確認編譯通過**（`docs/18` `E-121` 同類情況，見該筆「升級」說明）。已實跑的是不需資料庫的測試——其中新增的 **`OfflineQueryTranslation` 離線翻譯冒煙工具**對本批所有 repository 的 EF 查詢做了「能翻譯成 SQL」的驗證（連線被拒＝通過，翻譯失敗＝紅燈，並有工具自我驗證）。**合併後先套 migration `AlignSchemaI1`（含 15 個新權限碼），再在有 `tcrfc_club` 的環境完整跑 `dotnet test`**；🔴 push 前先看 `git diff Remote_GitHub/master --stat -- apps/api/Data/Migrations`（`E-124`）——有新 migration 就先跑 `db-migrate.yml` 再讓 `deploy.yml` 換上新版 api。

### 0. 先讀這幾點

- **路徑慣例**：公開端點 `/api/v1/{club}/…`（`{club}` 是 `tcrfc`／`bw`），後台端點 `/api/v1/admin/{club}/…`。介面字串翻譯表與場地是全站共用主檔，路由仍掛 `{club}` 只為沿用俱樂部授權管線（同既有 `/venues`）。
- **語系參數** `lang=zh|en`（預設 `zh`）；回應的文字都已依語系挑選，要求語系空白回退繁中。
- **錯誤格式**同全站：`ProblemDetails`（`title`／`detail` 是日常中文，可直接顯示）；公開端點新增三個共用例外對應 400／404／409（`Common/PublicExceptions.cs`）。
- **三個新限流政策**（`Common/PublicRateLimitPolicies.cs`，都可由設定覆寫額度，測試主機用寬鬆值）：`public-search`（每 IP 每分鐘 30）、`public-newsletter`（每 IP 每 10 分鐘 10，訂閱與退訂共用）、`public-trial-registration`（每 IP 每 5 分鐘 20）。超過回 `429`。
- **權限碼 15 個新增**（module=I，`db/seed/generate-club-seed-sql.py`＋migration `AlignSchemaI1`，系統管理員全給）：

| 權限碼 | 子模組 | 誰有 | 說明 |
|---|---|---|---|
| `site.menu.view`／`update` | I2 選單管理 | 僅系統管理員（`sysadmin_only`） | 每俱樂部一份 |
| `site.global.view`／`update` | I3 全域設定 | 僅系統管理員 | Logo／Favicon／品牌色／政策頁／維護模式 |
| `site.locale.view`／`update` | I4 多語系管理 | 僅系統管理員 | 語系、備援規則、日期數字格式、翻譯狀態總覽 |
| `site.string.view`／`update`／`translate` | I4 字串翻譯表 | `sysadmin_only=0`；**翻譯人員被指派 `view`＋`translate`**（scope `translate_only`） | `update`＝新增／刪除／改繁中原文與分組；`translate`＝只能改非預設語系（伺服器強制） |
| `site.venue.view`／`create`／`update`／`delete` | I5 場地管理 | 僅系統管理員 | 全站共用主檔（不帶 `club_id`） |
| `site.edm.view`／`update` | I6 EDM 平台設定 | 僅系統管理員（受限） | 含金鑰，只寫不讀 |

  儀表板**沒有專屬權限碼**（見 §1）。

### 1. A 儀表板（規劃書 §4.1）

三支 GET，權限＝呼叫者在該俱樂部持有「儀表板會用到的任一檢視／建立權限」（`AdminDashboardRepository.AllCandidateCodes`，含詢問、報名、梯次、試訓、贊助、新聞、FAQ、賽事、行事曆、球迷活動、會籍、翻譯人員的字串權限與全部快速入口的建立權限）；一個都沒有 → `403`。**每個區塊再依對應模組權限決定有沒有**——沒權限的區塊是 `null`／不在清單，**不是 0**。全部限定目前俱樂部（含兩隊共同內容）；日期用台灣當地日期；**不走快取**。

| 端點 | 回應重點 |
|---|---|
| `GET /api/v1/admin/{club}/dashboard` | `todos[]`、`content`、`faq`、`upcoming[]`、`members`、`quickEntries[]`、`generatedAt` |
| `GET /api/v1/admin/{club}/dashboard/conversion?period=week\|month` | 轉換概況：`totals`＋`buckets[]`（週＝最近 8 週、月＝最近 6 個月，由舊到新）＋`forms[]`（各表單送出數）；`400` 週期不合法 |
| `GET /api/v1/admin/{club}/dashboard/traffic` | GA4 流量概況：`configured`／`message`／`from`／`to`／`overview`；**目前一律 `configured=false`**（接縫，見 `docs/17` §3） |

- **`todos[]`**（規劃書列的四項，各自需對應檢視權限）：`enquiries_new`（狀態「新進」，依 G2 類別授權過濾）、`registrations_pending`（狀態「待確認」；課程報名需 `program.registration.view`、試訓報名需 `program.trial_registration.view`，各算各的）、`sessions_closing_soon`（7 天內報名截止、狀態「開放／候補」的梯次，`items` 最多 5 筆）、`sponsor_contracts_expiring`（到期提醒日已到、合約未到期，與 E2 清單「提醒中」同判定，`items` 最多 5 筆）。每項 `{ code, label, count, hint, items[{id,title,date}] }`。
- **`content`**：`publishedThisMonth`／`draftCount`／`scheduledCount`（需 `content.article.view`，否則 `null`）；`untranslated[]`＝每個啟用的非預設語系一筆 `{ locale, localeName, count, byType[] }`（已有繁中、缺該語系；類別依各自檢視權限，**翻譯人員與語系管理者看全部類別**；目前只有英文，「日」未啟用所以不出現）。
- **`faq`**：`topQuestions[]`（瀏覽數 Top 10）、`negativeFeedback[]`（👎 ≥ 3 且多於 👍，最多 10 題）。
- **`upcoming[]`**（未來 14 天，最多 30 筆，依日期排序）：`{ source: match|session|trial|event|fan_event, id, date, time, title, teamCode, venueName, warnings[] }`。`warnings` 是日常中文：「尚未指派教練」（梯次的課程沒有教練）、「名額未滿（已報名 x／y）」、「尚未設定地點／場地」、「尚未設定開球時間」「尚未填寫對手」「尚未設定名額上限」…。行事曆自建事件只列起始時間落在區間內的（**不展開重複事件**）。
- **`members`**：`activeMemberships`／`activePaidMemberships`／`expiringIn30Days`／`pendingUpgrades`（需 `member.membership.view`）。
- **`quickEntries[]`**：`{ code, label }`，code＝`publish_news`／`add_match`／`add_session`／`add_faq`／`add_calendar_event`，只含呼叫者有建立權限的；前端依 code 對應畫面路由。
- **轉換 bucket** `{ start, enquiries, registrations, proposalDownloads, newMembers, newPaidMemberships, renewals }`（各序列依權限，沒權限為 `null`）：詢問＝不含提案下載的表單送出；提案下載＝提案下載表單的送出（需 `business.lead.view`）；新註冊會員＝在本俱樂部新建立的會籍數；**新加入付費會籍＝某會籍的第一筆付款、續會＝第二筆以後的付款**（以 `membership_payments` 計，後台手動調整層級而沒有付款紀錄的不算）。

### 2. P4 試訓公開端點（規劃書 §3.3「試訓場次列表 ＋ 線上報名」）

| 端點 | 說明 |
|---|---|
| `GET /api/v1/{club}/trials?teamCode=&lang=` | 未結束且日期未過的場次，依日期由近到遠，最多 100 筆。每筆 `{ id, trialOn, teamCode, teamName, audience, venueId, venueName, venueAddress, venueLat, venueLng, capacity, enrolledCount, deadlineOn, status, isSignupOpen, acceptsWaitlist }`。`isSignupOpen`＝狀態「開放」且未過截止日；`acceptsWaitlist`＝「額滿／候補」且未過截止日（前台顯示「額滿候補」）。 |
| `POST /api/v1/{club}/trials/{trialId}/registrations` | 限流 `public-trial-registration`。body `{ applicantName*, phone, email, birthOn, guardianName, guardianPhone, healthDeclaration, note }`（電話與 Email 至少一項；**未滿 18 歲家長姓名與電話必填**）。`200 { registrationNo, status }`，`status`＝`待確認`（已佔名額，佔滿時場次自動轉「額滿」）或 `候補`。`400` 驗證／`404` 場次不存在或屬於別的俱樂部／`409` 已結束、已過截止日（截止日當天仍可報名）、同姓名＋同電話或 Email 重複報名（取消的不算）。帶會員 Bearer 權杖會記 `member_id`（同課程報名）。 |

- 名額用**單一條件式 UPDATE** 搶（並行搶最後一個名額只有一人成功，其餘候補），寫入 `registrations`（`trial_id` 非空）；後台 P4 名單與狀態流程沿用既有。
- 個資：回應不含任何個資；健康聲明等欄位照既有課程報名存放（B-9 待決不阻擋）。**通知信未寄**（全系統仍無報名通知通路，同 P3）。

### 3. G-09 電子報前台訂閱（規劃書 §3.0 G-09、後台 G3）

| 端點 | 說明 |
|---|---|
| `POST /api/v1/{club}/newsletter/subscribe` | 限流 `public-newsletter`。body `{ email*, consent*: true, source?: footer\|home\|news\|app, website?: "" }` → `200 { status: "ok" }`。`consent` 非 true／信箱格式錯／來源不在白名單 → `400`。 |
| `POST /api/v1/{club}/newsletter/unsubscribe` | body `{ token }` → `200 { changed }`；憑證無效（含拿別的俱樂部的憑證）→ `400`。冪等，找不到名單列也回成功（不透露）。 |

- **決定（規劃書只寫「Footer 常駐，串接 EDM 平台」）**：① **單一確認**（勾選同意即訂閱）、不寄雙重確認信——規劃書沒要求，全系統寄信通路只承接五封會員信；② **退訂是黏著的**：曾退訂的信箱再從公開表單送出**不會**改回訂閱（回應與成功相同），因為沒有信箱驗證時任何人都能替別人重新訂閱，等於違反對方的退訂意願；要重新訂閱走後台（須註明原因）；③ 回應**不透露**名單狀態；④ 同意紀錄＝`subscribed_at` ＋ `source`（固定中文標籤：官網頁尾／官網首頁／官網新聞頁／行動 App）；⑤ 蜜罐欄位 `website`（有值就靜默丟棄）。
- **退訂憑證**：`NewsletterUnsubscribeTokens`（Data Protection，purpose `Tcrfc.Newsletter.Unsubscribe.v1`），內容＝俱樂部＋信箱，**不過期**、只能退訂。目前**沒有寄信的呼叫端**（EDM 寄送在外部平台）；EDM 供應商串接時由該類別為每位訂閱者產生專屬退訂連結（前端需一頁 `/{lang}/newsletter/unsubscribe?token=…` 呼叫上面端點）。
- EDM 同步仍是後台手動（`INewsletterEdmSync`，未串接）。

### 4. G-02 全站搜尋（規劃書 §3.0）

`GET /api/v1/{club}/search?q=&type=&lang=&page=&pageSize=`，限流 `public-search`，**只讀不寫**。

- **範圍**：新聞（已發布且到時間，本俱樂部＋共同）、FAQ（已發布）、課程（已發布，僅本俱樂部）、球員（僅本俱樂部）、教練與團隊成員（本俱樂部＋共同，同公開名單）、慈善（已發布計畫＋事蹟紀錄）。只比對標題／名稱／摘要／簡介／職稱這類公開文字，**不比對個資欄位，也不比對區塊內文 json**。球員與教練照片遵守肖像同意 fail-closed（未同意 `imageUrl=null`）。
- **參數**：`q` 必填（正規化：全形轉半形、小寫、壓縮空白；最多 100 字；拆成最多 5 個關鍵字，**全部都要命中**；非中日韓文字至少 2 個字元，否則 `400`）；`type` 選填 `news|faq|program|player|coach|charity`（其他 `400`）；`pageSize` 預設 20、最大 50。
- **回應** `{ query, tokens[], items[], page, pageSize, totalCount, facets[{type,label,count}], truncated, isEmpty }`。`items[]` 每筆 `{ type, subType, id, slug, title, snippet, date, categoryCode, teamCode, imageUrl, isFallbackLocale }`；`snippet` 約 120 字、圍繞第一個命中處、已去標記；**高亮由前端用 `tokens` 在標題與摘錄上做**。`facets` 是各分類**精確**命中數（不受 `type` 篩選影響，供頁籤顯示數字）；每個分類最多取前 100 筆供翻頁，超過時 `truncated=true`。排序：標題含全部關鍵字（2 分）在前、其餘（1 分）在後，同分依 新聞→FAQ→課程→球員→教練→慈善，再依各類原順序。
- **語系**：同時比對「要求語系」與「繁中」（英文版用繁中關鍵字也找得到未翻譯內容），顯示取要求語系、空白回退繁中並標 `isFallbackLocale=true`；繁中請求不會去比對英文欄位。
- **引擎取捨：`LIKE`（`Contains`，EF 翻成 `CHARINDEX`），不用 SQL Server 全文檢索、不引入外部搜尋服務**——全站量級是一個俱樂部官網（新聞數百、人員數十、FAQ 數十），毫秒級；全文檢索要額外目錄／索引 DDL、migration 不能在交易內、繁中斷詞品質還要另外驗證，得不償失。**改用全文檢索的觸發條件**：任一類別超過約 1 萬列或 p95 超過 300ms；換法只換各類別 `Where`，端點與回應不變。`%`、`_`、`[` 只是普通字元（`SearchPublicTests` 驗證）。
- **零結果**：`isEmpty=true` 時前台照既有流程呼叫 `POST /api/v1/{club}/faqs/search-misses` 記錄（本端點是 GET，**不寫入任何資料**；沒有把兩者整合成伺服器端自動寫入，避免 GET 有副作用與重複計數）。

### 5. I 網站設定其餘子模組（規劃書 §4.9）

**公開讀取**（全部 GET、不需登入；除場地外都有短暫快取，**後台儲存後立即失效**——維護模式不得延後生效）：

| 端點 | 回應重點 |
|---|---|
| `GET /api/v1/{club}/menus?lang=` | `{ main[], mega[], footer[] }`，每項 `{ id, label, url, isExternal, children[] }`（多層級，兩個語系都沒標籤的項目不輸出）。內部連結 `url` 是不含語系前綴的路徑（`/about/`），前台自己接 `/zh`／`/en`。 |
| `GET /api/v1/{club}/site-settings?lang=` | `brand{ logoLightUrl, logoDarkUrl, faviconUrl, brandColor, brandSecondaryColor }`、`maintenance{ enabled, message }`、`languages[{code,name,isDefault,fallbackCode}]`（啟用中）、`fallbackMode`（`show_default`／`hide`）、`formats{ dateFormat, numberFormat, thousandsSeparator, decimalSeparator }`、`policies[{code,title,hasContent}]` |
| `GET /api/v1/{club}/policies/{code}?lang=` | `code`＝`cookie`／`privacy`／`member-terms`；`{ code, title, body, updatedAt, isFallbackLocale }`；**`body` 是純文字（空行分段），前台必須用文字節點輸出，不得 `v-html`**；沒有內容或代碼不存在 → `404` |
| `GET /api/v1/ui-strings?lang=&group=` | 全站共用（不分俱樂部）：`{ locale, strings: { key: 文字 } }`，缺該語系回退繁中 |
| `GET /api/v1/{club}/venues?lang=` | 這個俱樂部用得到的場地（主場＋賽事／梯次／試訓引用者；不快取）：`{ id, name, address, directions, lat, lng, photoUrl, photoWidth, photoHeight, photoAlt, isHome }` |

**後台**：

| 端點 | 權限 | 說明 |
|---|---|---|
| `GET /api/v1/admin/{club}/menus` | `site.menu.view` | `{ locations: [{ location: main\|mega\|footer, label, items[tree] }] }`，item `{ id, labelZh, labelEn, url, isExternal, children[] }` |
| `PUT /api/v1/admin/{club}/menus/{location}` | `site.menu.update` | **整棵樹取代**：body `{ items: [{ id?, labelZh*, labelEn?, url?, isExternal, children? }] }`。有 `id` 沿用、沒有新增、既有不在請求裡就刪除；同層順序＝陣列順序。最多 3 層、每位置 100 項；葉節點必須有連結；內部連結須 `/` 開頭且不含空白／網域，外部連結須完整 http(s) 網址。`400` 不改動任何資料。後儲存者覆蓋先儲存者（無版本檢查） |
| `GET`／`PUT /api/v1/admin/{club}/global-settings` | `site.global.view`／`update` | `PUT` 是 **multipart**：`payload`（JSON：`brandColor`、`brandSecondaryColor`（`#RRGGBB`）、`removeLogoLight`／`removeLogoDark`／`removeFavicon`、`cookiePolicy`／`privacyPolicy`／`memberTerms`（`{bodyZh,bodyEn}`，純文字，每則 ≤ 50,000 字）、`maintenanceEnabled`、`maintenanceMessageZh`／`En`（≤ 500 字））＋選填檔案 `logoLight`／`logoDark`／`favicon`。**非圖片欄位整份取代**；圖片不帶檔案且未勾移除＝維持原圖。切換維護模式寫敏感操作日誌。🔴 維護模式**只是旗標與訊息**，不會自動攔截其他公開端點，維護頁（G-10）由前台依 `site-settings.maintenance.enabled` 顯示 |
| `GET /api/v1/admin/{club}/i18n/locales` | `site.locale.view`／`site.string.view`／`site.string.translate` 任一 | 語系清單 `{ code, name, isDefault, fallbackCode, isEnabled, sortOrder }` |
| `PUT /api/v1/admin/{club}/i18n/locales/{code}` | `site.locale.update` | body `{ name, isEnabled, fallbackCode?, sortOrder }`；預設語系不能停用／不能設備援；備援不能是自己、必須存在且啟用、不能成環；被別的語系當備援時不能停用。**不提供新增語系**（見待決 2） |
| `GET`／`PUT /api/v1/admin/{club}/i18n/settings` | `site.locale.view`／`update` | `{ fallbackMode: show_default\|hide, dateFormatZh/En, numberFormatZh/En }`。日期格式只收 `YYYY MMMM MMM MM M DD D` ＋ 分隔字元（空白 / - . , 年 月 日），且年月日都要有；數字格式只收範例字串（`1,234.56`／`1.234,56`／`1 234,56`）。**空白＝清除** |
| `GET /api/v1/admin/{club}/i18n/overview?type=&missing=&keyword=&page=&pageSize=` | `site.locale.view` 或字串翻譯表任一權限 | 翻譯狀態總覽矩陣：`locales[]`（矩陣欄）、`summary[{type,typeLabel,total,missing{locale:筆數}}]`、`items[{type,typeLabel,id,label,isShared,done{locale:bool}}]`、`page`／`pageSize`（預設 50、最大 100）／`totalCount`。涵蓋 **9 類**：新聞、FAQ、課程、球員、教練、慈善計畫、夥伴、贊助商、首頁輪播；「完成」＝該語系側表列存在且**主要文字欄位**（標題／名稱／問題）非空。`missing=en` 篩「缺英文」 |
| `GET /api/v1/admin/{club}/i18n/strings` | 字串翻譯表 `view`／`update`／`translate` 任一 | `?group=&keyword=&missing=en&page=&pageSize=`（預設 50、最大 100）→ `PagedResult<{ id, key, group, values{locale:文字}, updatedAt }>` |
| `GET …/i18n/strings/groups` | 同上 | 分組清單 |
| `POST …/i18n/strings` | `site.string.update`（`translate` 者 `403`） | body `{ key*, group?, values{ "zh-Hant"*, "en"? } }`；鍵格式 `^[a-z0-9][a-z0-9_-]*(\.[a-z0-9][a-z0-9_-]*)*$`（≤128）；重複 `409` |
| `PUT …/i18n/strings/{id}` | `update` 或 `translate` | body `{ group?, values{} }`：**只處理有出現的語系**，空白＝刪除該語系翻譯（繁中不能清空）。🔴 只有 `translate` 的人：改繁中原文或分組 → **整個請求 `403`**（原樣重送視為沒改） |
| `DELETE …/i18n/strings/{id}` | `site.string.update` | `204` |
| `GET /api/v1/admin/{club}/venues` | `site.fact.view`／`team.match.view`／`site.venue.view` 任一 | 清單，**新增** `lat`／`lng`／`photoUrl`（原欄位不變） |
| `GET /api/v1/admin/{club}/venues/{id}` | `site.venue.view` | 詳情 `{ id, lat, lng, photoUrl, photoWidth, photoHeight, sortOrder, zh{name,address,directions,photoAlt}, en?, usageCount, isHomeVenue, updatedAt }` |
| `POST`／`PUT /api/v1/admin/{club}/venues[/{id}]` | `site.venue.create`／`update` | **multipart**：`payload`（`{ zh{name*,address,directions,photoAlt}, en?{…}, lat, lng, sortOrder, removePhoto }`）＋選填檔案 `photo`。緯度經度同時填或同時空白，範圍 ±90／±180；英文名稱空白＝刪除英文版；建立時不可 `removePhoto` |
| `DELETE /api/v1/admin/{club}/venues/{id}` | `site.venue.delete` | 被賽事／梯次／試訓／行事曆事件／球迷會活動引用或登記為主場 → `409`（訊息說明原因），不連帶刪除 |
| `POST /api/v1/admin/{club}/venues/locate` | `site.venue.create`／`update` 任一 | 「由地址定位」預覽，body `{ address }` → `{ lat, lng }`／`404` 查無／`503` 服務未啟用或故障；**不寫入任何資料** |
| `GET`／`PUT /api/v1/admin/{club}/edm-settings` | `site.edm.view`／`update` | `{ enabled, provider, listId, senderEmail, apiKeyConfigured, integrationAvailable, updatedAt }`；`PUT` body 多 `apiKey`（留空＝維持原金鑰）、`clearApiKey`。🔴 **金鑰以 Data Protection 加密存放、只寫不讀**（任何回應都不含金鑰或其片段），設定／清除寫敏感操作日誌；啟用前必須有平台名稱與金鑰；`integrationAvailable=false`（供應商未定）時只是先存起來 |

- **綱要**（先文件、再 DDL、再 migration）：`venues` 補 `photo_width`／`photo_height`、`venues_i18n` 補 `photo_alt`（圖片欄位組，規劃書 v3.5 §4.0）；`menu_items`／`ui_strings`／`locales`／`settings`／`newsletter_subscribers`／`registrations`／`trials` 早已存在。其餘設定鍵走 `settings`／`settings_i18n`（`policy.*`、`maintenance.*`、`i18n.*`、`edm.*`，詞彙集中於 `Features/SiteSettings/SiteSettingKeys.cs`）。migration **`AlignSchemaI1`**：冪等補欄位＋15 個權限碼與角色指派（以業務自然鍵「不存在才新增」，與種子／`db/prod` 兩條路徑誰先到都不會重複）。`Down` 只還原欄位，**刻意不移除權限碼**。
- **Logo／Favicon／品牌色**：J4 俱樂部管理的圖片欄位維持唯讀，上傳入口在這裡（`clubs.logo_light_key`／`logo_dark_key`／`favicon_key`）。標誌類目前沒有寬高與 Alt 欄位（`docs/12d` §12 同一個既有落差）。

### 6. 測試

- **已實跑（不需資料庫）**：`SearchRepositoryOfflineTests`（關鍵字解析、萬用字元、摘錄、6 種類別查詢翻譯）、`SiteBackendOfflineTranslationTests`（儀表板全區塊與週／月轉換、試訓清單與報名驗證、前台選單／站台設定／政策／介面字串／場地、後台選單／全域設定／多語系／字串／EDM／場地查詢的 EF 翻譯，電子報驗證與退訂憑證往返、數字格式解析、**工具自我驗證**）、`PublicRateLimitPoliciesTests`（三個新政策）、`ArchitectureTests`（新公開 POST 端點都掛限流、公開 DTO 無孤立物件鍵）、`UserFacingMessageContentTests`（新例外訊息無權限碼／技術詞）。
- **僅編譯、未實跑（需要資料庫）**：`DashboardApiTests`、`TrialsPublicTests`、`NewsletterPublicTests`、`SearchPublicTests`、`SiteMenusAndGlobalSettingsTests`、`SiteI18nStringsVenuesEdmTests`（共 47 項）。共用設定（選單、政策、維護模式、多語系、EDM）改動前後一律快照還原（`E-81`／`E-119`）；翻譯人員用測試內臨時建立的帳號（`SiteSettingsTest.CreateTranslatorAsync`），不依賴種子。**Logo／Favicon／場地照片上傳需要 Azurite，未涵蓋**。
- 前端要做：儀表板畫面（`DashboardView.vue` 改接三支端點，移除 `data/dashboard.ts` 假資料）、`club/opportunities` 試訓表格與報名表單、頁尾電子報訂閱與退訂頁、全站搜尋頁（含高亮與分類頁籤）、後台 I 模組五個子畫面、前台選單／維護頁／政策頁／介面字串改讀 API。

### 7. 本批的待決事項

1. **雙重確認信（G-09）**：規劃書沒要求，目前單一確認＋退訂黏著。若要讓使用者能自行重新訂閱，需新增「待確認」狀態值（`newsletter_subscribers.status` 目前只有 `subscribed`／`unsubscribed`）並透過 `IEmailSender` 寄確認信。
2. **第三語系擴充（G-01「新增語系時不需改動程式」）**：`RequestLocale.ToDbLocale` 目前只認 `en`，後台也不提供新增語系，與規劃書目標有落差；要做需讓語系判斷改讀 `locales` 表。
3. **字型設定（I 多語系管理）**：規劃書只有「日期／數字格式與字型設定」一行字、沒有可選項目，字型又由設計系統決定——**本次不做**（日期／數字格式已做）。
4. **Fallback「隱藏該頁」的執行**：後端只保存並公開 `fallbackMode`；各內容端點**不會**自動依它隱藏，要由前台依各 DTO 的語系回退標記執行（部分 DTO 還沒有回退標記）。
5. **翻譯狀態總覽的涵蓋類別**：目前 9 類（見上）；頁面（區塊內文是雙語 JSON、沒有單一標題欄位）、賽事、商品、漫畫、行事曆事件等未納入，規劃書寫「每筆內容」，需確認要不要補。
6. **儀表板待辦的項目**：照規劃書四項；「待出貨訂單」「待確認會籍申請」「候補」不在規劃書儀表板定義內，未放進待辦（會籍申請數在 `members.pendingUpgrades`）。FAQ 負評門檻（👎 ≥ 3 且多於 👍）、「即將截止」＝7 天、「即將到期」沿用 E2 提醒日，皆為執行層決定。
7. **GA4 憑證**：服務帳戶與屬性 ID 未提供，流量區塊目前「尚未串接」（接縫 `IAnalyticsSource`，`docs/17` §3）。
8. **EDM 供應商**：未定；設定欄位先備好（平台名稱、名單識別、寄件者、加密金鑰）。
9. **試訓報名通知信**與課程報名一樣沒有寄送通路（全系統無報名通知）。


## 慈善後台帳號與角色管理（2026-10-03，`backend-engineer`）

補上 `docs/16` §2.3 五張表（`AdminUser`／`AdminRole`／`AdminUserRole`／`Permission`／`RolePermission`）原本沒有任何管理端點的缺口。**照主站 `Features/AdminAccounts`／`Features/AdminRoles` 搬**，程式在 `CharityPlatform/Admin/`（`CharityAdminAccessEndpoints.cs`、`CharityAdminAccountsService.cs`、`CharityAdminRolesService.cs`、`CharityAdminAccessDtos.cs`），不引用 `ClubDbContext` 或主站型別（只借 `AdminAuthService.ValidatePasswordPolicy`，與慈善登入同一處既有借用）。前綴 `/api/v1/donation-platform/admin`。

| 端點 | 權限碼 | 說明 |
|---|---|---|
| `GET /accounts?status&keyword&page&pageSize` | `n7.admin_account.view` | 預設 20 筆、上限 100；依帳號排序 |
| `GET /accounts/{id}` | 同上 | 詳情；找不到 404 |
| `POST /accounts` | `n7.admin_account.manage` | 201。建立者指定初始密碼（不寄信）；`mustChangePassword` 預設 `false` |
| `PUT /accounts/{id}` | 同上 | 基本資料＋整份取代角色；不含密碼／狀態／2FA |
| `POST /accounts/{id}/status` `{status}` | 同上 | `active`／`disabled`；停用立即撤銷全部更新權杖 |
| `POST /accounts/{id}/reset-password` `{newPassword}` | 同上 | 204；`mustChangePassword=true`（僅提示）、清鎖定、撤銷工作階段 |
| `POST /accounts/{id}/reset-totp` | 同上 | 204；清空 2FA 密鑰、撤銷工作階段 |
| `GET /roles/permissions` | `n7.admin_role.view` | 權限碼字典 |
| `GET /roles`、`GET /roles/{id}` | 同上 | 列表含 `assignedAccountCount`；詳情含權限指派 |
| `POST /roles` | `n7.admin_role.manage` | 201。`code` 只能小寫英數底線；建立者永遠 `isSystem=false` |
| `PUT /roles/{id}` | 同上 | 改名稱 |
| `DELETE /roles/{id}` | 同上 | 204；系統角色 403、仍有帳號 409 |
| `PUT /roles/{id}/permissions` | 同上 | 整份取代**非 `sysadmin_only`** 的指派；`sysadmin_only` 與未知碼 400（訊息只報筆數，不列權限碼） |

**四個權限碼全為 `sysadmin_only`**（種子 `EXTRA_PERMISSIONS`、`db/prod/charity-reference-data.sql`、migration `AddAdminAccessPermissions` 三處同一組 UUID）。守則：帳號名稱是一般字串（可中文，前後去空白、≤64、不得含空白、全域唯一）；密碼下限 `AdminAuthService.MinPasswordLength`（9）、不得與帳號相同、雜湊走 `PasswordHasher`（Argon2id，與登入同一個）；**不得停用或降級最後一位啟用中的系統管理員（含自己，409）**；沒有刪除帳號端點。稽核動作 `admin_account.create／update／status／reset_password／reset_totp`、`admin_role.create／update／delete／permissions`，與變更同一次 `SaveChanges`，摘要只含筆數與布林，不含密碼與權限碼。

**與主站形狀的差異**（慈善沒有的維度）：(1) 沒有 `/club-grants`、`/team-grants` 六支端點；(2) 帳號回應的 `primaryClubId`／`locale` 恆為 `null`、`clubGrants`／`teamGrants` 恆為 `[]`，請求裡的這兩欄接受但忽略；(3) 角色回應 `scopeMode` 恆為 `all_clubs`、`sortOrder` 取 `seq`、權限字典 `isClubScoped` 恆為 `false`，建立／更新角色的 `scopeMode` 可省略（忽略）；(4) `role_permissions.scope_type` 在慈善庫可為 `NULL`（種子即如此），回應補成 `all`，輸入沿用主站值域。測試：`CharityAdminAccessTests`（17 項）；測試角色代碼 `ctrole_*`，`CharityApiFixture.CleanupAsync` 清除並把被測試帳號寫入的 `admin_roles.created_by／updated_by` 設回 `NULL`。不在 App 契約內，`shared/scripts/gen-all.sh` 不必跑。

---

## 後台實機驗收缺陷修正（2026-10-03，`backend-engineer`）

> 來源：使用者用真實 API 走後台驗收（B-1〜B-14）。本節記錄行為與前端要用的參數／欄位；全部有測試。

| 編號 | 行為 | 前端要點 |
|---|---|---|
| **B-1** K2 會員選擇器搜不到 K1 現場建立的會員 | `GET /members` 新增 **`includeNoMembership=true`**：列出「本俱樂部有會籍者」＋「**任何俱樂部都還沒有會籍者**」。**不用 `crossClub`**：`crossClub` 對單一俱樂部資料範圍的角色只會擴到「你有授權的俱樂部」，而且不含無會籍帳號（僅系統管理員含），所以救不了這個情境。無會籍帳號不屬於任何俱樂部、輸出仍遮罩、關鍵字在沒有 `member.pii.reveal` 時只比對會員編號，且「只在他隊有會籍」者用全表判斷排除，不會洩漏他隊會員。與會籍相關的篩選（`tier`／`membershipStatus`／`seasonId`／`expiringWithinDays`／`clubCode`）同時使用時此旗標不生效；匯出一律忽略此旗標 | K2 選擇器改打 `?keyword=…&includeNoMembership=true&pageSize=10`（不需要 `crossClub`） |
| **B-2** B5 影響力數據負數 | `value < 0` → 400「數值不可為負數。」。規劃書 B5 的統計項目是件數／人數／金額累計，沒有負值情境 | 前端可加 `min=0` |
| **B-3** P4 代填驗證、P3 補驗 | P4 建立／修改：姓名必填、**電話與 Email 至少一項**、Email 格式（存小寫）、電話格式（數字、`+ - 空白 ()`，≥6 碼數字）、出生日期不可為未來／超過 100 年、**未滿 18 歲須填家長姓名與電話**（同前台 `TrialsRepository`）。例外：修改一筆**原本就沒有任何聯絡方式**的舊報名時不強制「至少一項」（否則舊資料連取消都不行），其他檢查照做。P3 建立／修改：Email 格式、電話格式、家長電話格式（不強制聯絡方式與未成年家長，維持現場代填彈性）。共用 `AdminInput.OptionalPhone` | 錯誤訊息是日常中文，直接顯示 |
| **B-4** 後台代填超額 | 規劃書 P3／P4 只寫「額滿自動關閉、候補遞補提醒」，**沒有禁止後台超額**，維持不擋。報名詳情（建立／修改／遞補／單筆讀取回應）新增 **`isOverCapacity`**（所屬梯次或試訓場次目前 `enrolledCount > capacity`；名額未設定＝`false`） | 建立／遞補後讀 `isOverCapacity` 顯示警示；列表頁可讀梯次／場次既有的 `capacity` 與 `enrolledCount` |
| **B-5** CSV 時間 | 全部改輸出**台灣時間（UTC+8）**，新增 `TaiwanClock.ToText`，表頭加「（台灣時間）」：P4 報名時間、P3 建立時間、G3 訂閱／退訂時間、F 表單詢問送出時間、E3 Lead 下載時間；日期欄（K1 註冊日期、K3 登記日期）改用台灣日期；L 行事曆匯出的**俱樂部活動**起訖改台灣時間（賽事與試訓本來就是當地日期，不轉換）。K5／S3 原本就是台灣時間 | 無 |
| **B-6** P3 匯出缺用途 | `GET /registrations/export` 加 **`purpose`**（必填，同 P4／K1／G3 的參數名）並寫稽核（見上表） | P3 匯出對話框要有「用途」欄位 |
| **B-7** P4 匯出檔名 | 後端 `Content-Disposition` 檔名原本是 `trial-registrations-{club}-{匯出日}.csv`（不含 GUID，但看不出是哪個場次）；含 GUID 的檔名是前端 `apps/admin/src/api/adminTrials.ts:167` 自己組的（`trial-registrations-${trialId}.csv`）。後端現在改為 `trial-registrations-{club}-{場次日期yyyyMMdd}-{匯出日期yyyyMMdd}.csv`；P3 檔名日期改台灣日期 | 前端改用回應的檔名（或自組 `trial-registrations-{場次日期}.csv`），不要再放 GUID |
| **B-9** K5 公布稿草稿刪除 500 | `DELETE /news/{id}` 遇到 `member_draws.announcement_article_id` 外鍵：活動為**已公布／已結案** → **409**「這篇文章是抽獎活動「{代碼}」的公布稿，活動已公布或已結案，不能刪除。」；其餘狀態（草稿、名單已鎖定、已抽出、作廢）→ **先解除關聯再刪**，之後可重新產生公布稿 | 409 訊息直接顯示 |
| **B-10** K5 | (a) 列表 `rosterVersion=1`＋`totalCount=null` **不是錯**：`roster_version` 預設 1，草稿尚未產生名單時 `total_count` 為 null（種子 `TEST-DRAW-02` 就是草稿），語意為「尚無名單」。(b) 名單 `GET /draws/{id}/roster` 原本有 `member.pii.reveal` 就自動回完整姓名並每翻一頁寫一筆稽核，與 K1「預設遮罩、`reveal=true` 才解除」不一致，現已對齊：新增 **`reveal`** 參數（預設 `false`）；`reveal=true` 無權限 → 403，有權限才回完整姓名並寫稽核；`isMasked` 反映實際。名單關鍵字搜尋姓名同樣須 `reveal=true` | 名單頁預設顯示遮罩，加「顯示完整資料」按鈕（重打 `reveal=true`）。**獎品發放頁 `/draws/{id}/fulfilments` 未改**（它是編輯收件資訊用，現行有權限即顯示完整值並寫稽核），是否一併改由使用者決定 |
| **B-11** 推播建立者／覆核者 | 推播列表項新增 **`createdByName`**；詳情新增 **`createdByName`**、**`reviewedByName`**（帳號顯示名稱；帳號已被移除或尚未覆核為 `null`） | 畫面顯示名稱，不顯示 GUID |
| **B-14** 廣告累計曝光不一致 | 根因是**種子資料**：`ad_campaigns.delivered_total` 寫死 3900，而 `ad_daily_stats` 的曝光加總是 4,674。執行期兩者同源（`ad_events` 寫入時同步累加 `delivered_total`，日聚合由同一批事件產生），口徑一致、無程式缺陷。種子改為由日聚合加總算出，並附一段只修正這筆種子檔期的 `UPDATE`（兩邊不一致才動）。**已灌過舊種子的資料庫需重跑種子或手動 `UPDATE ad_campaigns SET delivered_total = (SELECT SUM(impressions) FROM ad_daily_stats WHERE campaign_id = ad_campaigns.id) WHERE name LIKE N'【測試】%'`** | 無 |
| **上傳** 儲存未設定 | `Unavailable{Image,Video,Document}StorageService` 改丟 `FeatureNotConfiguredException` → **503**「檔案儲存尚未設定」（`code = storage_not_configured`，訊息不含變數名稱），不再是 500。刪除仍安靜略過 | 503 時提示「檔案儲存尚未設定」 |

**本機驗上傳要設定的變數**（只列名稱，不寫值；Azurite 是本機 Blob 模擬器，見「本機開發：Azurite」）：`AZURE_BLOB_CONNECTION_STRING`（連到 Azurite 的 Blob 端點，Azurite 官方文件的開發用連線字串）。其餘 `AZURE_BLOB_CONTAINER_*`（`images`／`videos`／`documents`／`proposals`）有預設值，容器由程式自行建立；`AZURE_BLOB_PUBLIC_BASE_URL` 可不設（回退容器網址，Azurite 的 `http://127.0.0.1:10000/…` 在 Development 放行）。設定後**重啟 API** 才會改注入真實儲存。

**同日追加（協調者回饋）**：(1) 廣告檔期詳情新增 `reviewedByName`（審核者顯示名稱）。(2) 後端產生的文字對齊 docs/06 §1：K5 公布稿標題活動名稱已含【】時不再重複加；診斷「API 錯誤」→「連線錯誤」（回報類型標籤、錯誤訊息、彙總說明 `ApiErrorNote`）；M5 連線檢查改「推播服務（Apple 與 Google）」「備援設定來源」，不再出現 APNs／FCM／Cloudflare／`M1／M5`；商店「貨號」→「商品規格編號」（驗證與衝突訊息、熱銷報表 CSV 欄名）；`AdminInput` 的欄位標籤以英文字母或數字結尾時與中文之間補空格（「Email 的格式不正確」）。M2 憑證種類顯示名稱改「Apple 推播金鑰」「Google 推播憑證」（內部代碼 `apns_key`／`fcm_credential` 不變）。**獎品發放頁 `/draws/{id}/fulfilments` 刻意維持現狀**：有 `member.pii.reveal` 就顯示完整收件資訊並寫稽核，因為這一頁要編輯收件人、電話與地址，遮罩值無法編輯。

**同日追加（第二輪重驗，2026-10-05，`backend-engineer`）**：(1) 抽獎公布稿標題：活動名稱內含任何【或】就不再外包【】，改成「{名稱} 中獎名單公布」（`AdminDrawsRepository.BuildAnnouncementTitle`；無括號仍為「【名稱】中獎名單公布」）。(2) `DELETE /news/{id}`、`DELETE /pages/{id}` 缺 `expectedUpdatedAt` 回 **400**「缺少資料的最後更新時間…」（參數改 `DateTime?`，`ConcurrencyInput.RequireExpectedUpdatedAt`）；全站最小 API 綁定失敗（`BadHttpRequestException`，例如 query 值不是日期）一律 400 中文，不再 500；`ArchitectureTests` 掃描禁止 handler 宣告必填 `DateTime`／`DateOnly` query。其他同樣把 `expectedUpdatedAt` 放 query 的端點只有這兩支，其餘都放請求本文（由 DTO `required` 驗證）。(3) CORS 加 `WithExposedHeaders("Content-Disposition", "Retry-After")`，後台可讀到下載檔名。(4) 限流 429 加 `Retry-After`（`OnRejected` 取 `MetadataName.RetryAfter`，無 metadata 就不帶；單位秒、至少 1）。慈善平台的限流註冊在同一個 `RateLimiterOptions` 上，一併生效。測試：`ApiBoundaryBehaviorTests`、`AdminAuthRateLimitingTests`、`ArchitectureTests`。不動 `shared/` 契約。

## 上傳端到端與驗收測資收尾（2026-10-05，`backend-engineer`）

- **跨模組上傳通則測試** `ImageUploadGeneralRuleAcrossModulesTests`（真實 Azurite、真實 HTTP 管線、真實 SQL Server，6 項）：用 3000×2000、帶相機廠牌／方向／GPS 的 JPEG 打
  夥伴 Logo（E1）、慈善團體 Logo 與事蹟圖片（B5）、媒體專區高解析圖（B6）、漫畫封面與內頁＋球迷活動封面（F1／F2）、商品圖集（S1）、廣告素材（E4–E6，16:9 的 3200×1800），
  再**直接從 Azurite 取回每個物件**斷言 §4.0：`image/webp` 且位元組真是 WebP、主檔長邊 = min(來源, 2560)、衍生檔長邊 1280／640／320、`-thumb` 為 160×160、
  除 `.webp` 外沒有任何物件（不留原檔）、Exif／IPTC／XMP／ICC 全為空（含 GPS）、每組恰 5 個物件。**未發現產品缺陷**。
- **本機替身**：見上方「本機開發：Azurite」。重點是 API 自建容器預設私有，本機要預建公開容器（`db/seed/seed-dev-blobs.py`），否則圖片網址 403。
- **驗收測資**：`backoffice_seed.py` 區段 60（球衣會員 `M900101`／`M900102`、漫畫第 101／102 集）；`DevAcceptanceSeedTests` 唯讀守門。
  新增的兩位球迷會員同樣符合抽獎資格，`AdminDrawsTests` 以 `DevAcceptanceEligible` 計入；`CulturePublicTests` 改為只看 9000 段集數。
- **AP-3 更新權杖鏈**：`AppDeviceSessionTests` 12 項全綠，新增「刪除帳號撤銷全部裝置的鏈並解除綁定」。
- **全套**：`dotnet test` 1330／1330 通過（4 分 57 秒）。
- **沒做**：整合測試在沒有 `azurite-blob` 時仍是拋例外（fixture 既有行為），沒有改成明確 skip——改動會影響約 20 個測試檔共用的 fixture，列為後續。

## App 契約補強：球員 slug、統一錯誤結構、Azurite 缺失時 skip（2026-10-05，`backend-engineer`）

### 1. 球員網址代稱 `players.slug`（App 規劃書 §2.3 `tcrfc://player/{slug}`）
- **資料表**（先 docs/12／12a／12b §11.1，再 `db/club-schema.sql`）：`slug nvarchar(160) NOT NULL`、`[a-z0-9]+(-[a-z0-9]+)*`、唯一鍵 **`(club_id, slug)`**（`UQ_players_club_slug`）、索引 `(slug, club_id)`。兩個俱樂部可各有同一個 slug。
- **EF migration `AddPlayerSlug`**：冪等（先查現況再動，正式庫是 DDL 建的）；既有列回填 `player-{row_seq}`。⚠️ 本機開發庫沒有 EF 歷史（DDL 建的），`dotnet ef database update` 會撞既有物件，已直接以同一組冪等陳述式套用；種子 `apply-seed.sh` 再把回填值換成漂亮代稱（只動「仍是回填值」的列，後台改過的不覆蓋）。
- **公開端點**：`GET /api/v1/{club}/players` 每筆帶 `slug`；新增 `GET /api/v1/{club}/players/{slug}?lang=`（`{slug}` 也接受球員 id，大小寫不敏感；別的俱樂部的球員一律 404）。
- **後台**（`/api/v1/admin/{club}/players`）：列表與詳情帶 `slug`；建立 `slug` 省略＝自動產生（英文姓名→`zz-slug-xxx`；沒有英文姓名→`隊別-背號`，撞名加序號），更新 `slug` 省略＝維持原值（不重新產生，避免深連結失效）；格式不合 400、同俱樂部重複 409「網址代稱重複」。介面用語「網址代稱」。
- **種子**：`generate-club-seed-sql.py` 的 `player_slug()`（英文姓名 slugify，沒有用「隊別代號-背號」，撞名加背號）；磐石 28＋藍鯨 28 位。`db/prod/club-content-seed.sql` 已重產。
- 測試：`AdminTeamsPlayersStaffTests` 新增 2 項（自動產生／指定／重複／格式／更新省略／跨俱樂部可重複；公開清單帶 slug、以 slug 與 id 查、別的俱樂部 404）。

### 2. 統一錯誤結構（App 規劃書 §9.5）
- 所有 ProblemDetails 加 `code`、`messageZh`（＝`detail`）、`messageEn`、`retryable`；**相容擴充**，`status`／`title`／`detail`／`instance`／既有 `code`／`lockedUntil` 不動。
- 三條產生路徑都走 `Common/ApiErrorEnvelope.Fill`：`ApiExceptionHandler`（例外）、`AddProblemDetails(CustomizeProblemDetails)`＋`UseStatusCodePages()`（空本文的 404／401／429 等，如 `Results.NotFound()`、限流中介軟體；回應是 `application/problem+json`）、`Results.Problem(extensions: ApiErrorEnvelope.Extensions(...))`（行事曆改期衝突，`schedule_conflict`）。例外處理器沿用既有 `application/json`，本文結構相同。
- `code`：有專屬代碼的沿用；其餘依狀態通用（400 `validation_failed`、401 `unauthenticated`、403 `forbidden`、404 `not_found`、405 `method_not_allowed`、409 `conflict`、413 `payload_too_large`、415 `unsupported_media_type`、423 `account_locked`、429 `rate_limited`、503 `service_unavailable`、其餘 5xx `server_error`）。
- `retryable`：5xx＝true、4xx（含 423、429）＝false（規劃書 §9.5）；外部服務尚未設定的 503（`ApiErrorMessages.NonRetryableServerCodes`）＝false。429 另帶 `Retry-After` 標頭，要不要等待後重試規劃書未定義。
- `messageEn`：`Common/ApiErrorMessages.cs` 登記的 84 個專屬代碼各有英文；沒登記的退回該狀態通用英文。英文不含動態值。**新增專屬 code 必須同步補英文**——`shared/scripts/gen-error-codes.mjs` 在缺漏時直接失敗（CI 的 shared-contract job 會紅燈）。
- 測試：`ApiErrorEnvelopeTests`（15 項：例外／空本文／會員一族／後台 401／各狀態 `Fill` 對照）、`AdminAuthRateLimitingTests` 加 429 本文斷言。

### 3. 沒有 Azurite 時明確 skip
- `Fixtures/AzuriteLocator.cs`：單一入口找 `azurite-blob`（`AZURITE_EXECUTABLE_PATH` 指到不存在的檔案＝視為沒有，方便驗證 skip 路徑）；`[AzuriteFact]`／`[AzuriteTheory]` 在沒有時於探索階段標 skip 並印出原因，`AdminWriteAzuriteEnabledApiFixture` 不再拋例外。13 個測試檔共 72 項改用（`AdminTeamsPlayersStaffTests` 只改上傳那一個類別）。新增需要真實 Azurite 的測試請用這兩個屬性。實測 `AZURITE_EXECUTABLE_PATH=/nonexistent`：72 項 skip、其餘照跑。

### 測試數字
- `dotnet test` 全套 **1347／1347 通過、0 失敗、0 skip**（5 分 19 秒；有 Azurite）。1330 → 1347：+15 錯誤結構、+2 球員 slug。

## App 契約缺口第三批（2026-10-05，`backend-engineer`；docs/19 §11c／§11d A1–A10）

| 項目 | 實作 |
|---|---|
| 賽事系列清單 | `Features/Competitions/`：`GET /api/v1/{club}/competitions?season=&lang=`，只回已發布；`CompetitionsRepository.CacheEntity="competitions"`，後台 J4 寫入後失效它與 `schedule` |
| 賽事 | `GET /api/v1/{club}/schedule/{id}`（Guid；別的俱樂部 404）；列表新增 `competition`（系列代碼）、`from`、`to`（含頭尾，from>to 回 400）；`MatchDto` 新增 `clubCode`、`competitionCode` |
| 球隊 | `TeamDto` 新增 `clubCode`；`type`／`gender` 值域寫進 DTO 註解與 `shared/enums.json` |
| ETag／304 | `Common/ConditionalGetMiddleware.cs`，白名單路徑（見 docs/19 §11e）；ETag＝回應本文 SHA-256，每次仍走完整管線，不新增快取層；CORS expose `ETag` |
| 裝置識別 | `X-Device-Install-Id` 標頭（`AppInput.ResolveOptionalDeviceId`），layout／notifications／ads 採用，有帶驗證格式、沒帶不拒絕 |
| layout | `AppLayoutItemDto.isExternal`；`shop`→`/{lang}/shop/`；`charity`→站台設定 `charity.donation_url`（未設定為 null）；`AppLayoutResponse.sponsorshipInquiryWebUrl` |
| 未翻譯標示 | `RequestLocale.IsFallback`；`isFallbackLocale` 加在 Club／Team／Player／Staff／Competition／Match／ArticleListItem／ArticleDetail／FaqListItem |
| shared | `image-derivatives.json`（解析 `apps/api/Images`）、`enums.json`（解析 DDL CHECK），`check-shared.py` 驗證 |
| 測試 | `AppContractGapsTests` 13 項（賽事系列、賽程新篩選與單場、未翻譯標示、球隊值域、ETag 命中／弱驗證／多值／`*`／未命中、不得讀快取端點不帶 ETag、白名單路徑、layout 外連、裝置識別標頭） |
| 全套 | `dotnet test` **1360／1360 通過**（6 分 38 秒） |
| 未做 | `ClubDto` 簡稱（無欄位依據）；時間語意（待決，docs/19 §11f）；`isFallbackLocale` 尚未涵蓋夥伴、特約店家、課程、站台設定 |

## App 契約缺口第四批（2026-10-05，`backend-engineer`；docs/19 §11h）

| 項目 | 實作 |
|---|---|
| `kickoffAt` | `TaiwanClock.KickoffToUtc`；`MatchDto.kickoffAt`（衍生、可空）。時間語意調查見 docs/19 §11f |
| 俱樂部簡稱 | docs/12／12a／12c → `db/club-schema.sql`（`clubs_i18n.short_name`）→ migration `AddClubShortNameGuardianConsent`（回填依俱樂部代碼與語系）→ 種子（磐石中英、藍鯨僅中文）→ `ClubDto.shortName`；`db/prod/club-reference-data.sql` 已重產 |
| 監護人同意 | `Features/MemberAuth/MemberAgeGate.cs`（`Evaluate`：生日必填→足歲→未成年驗 `guardianConsent`）；`MemberRegisterRequest`／`MemberLineCompleteRequest` 新增 `GuardianConsent`；`members.guardian_*` 四欄＋`CK_members_guardian_consent`；刪帳號清 `guardian_name`；K1 `AdminMemberDetailDto.guardianConsent`（姓名遮罩）；5 個新錯誤碼（`ApiErrorMessages` 已登記英文）。既有測試的註冊請求全部補 `birthOn` |
| 抽獎資訊 | `Features/MemberDraws/`：`GET /api/v1/member/draws`；不讀 `draw_rosters`，資格由 `memberships` 即時推得；`no-store` |
| 會員卡 | `MemberCardDto.serverTime` |
| 搜尋 | 球員結果 `slug` |
| 店家座標 | DDL `CK_partner_stores_coords`；後台驗證 0,0／半邊／超範圍；定位回 (0,0) 視為沒定位到；`PartnerStorePublicDto` 註解寫明 null＝未確認 |
| 未翻譯標示 | 夥伴、贊助商、贊助方案、課程（列表／詳情）、特約店家 |
| 測試 | `AppContractBatch4Tests` 18 項；`MemberAuthTests`／`MemberTestSupport` 補生日 |
| 全套 | `dotnet test` **1378／1378 通過**（5 分 17 秒） |
| 未做 | 後台編輯俱樂部簡稱；會籍方案／權益的 `isFallbackLocale`；後台「現場入會」不套年齡閘門 |

## App 契約缺口第五批（2026-10-05，`backend-engineer`；docs/19 §11j）

| 項目 | 實作 |
|---|---|
| 我的報名 | `MemberRegistrationDto` 新增 `kind`／`course`／`trial`／穩定狀態代碼；`GET /member/registrations?lang=` |
| 穩定狀態代碼 | `Common/EnrollmentStatus.cs`（中文字面值 → 代碼＋雙語標籤）；`ProgramSessionDto`、`TrialDto`、各報名結果 DTO 新增 `statusCode`／`statusLabelZh`／`statusLabelEn`；`shared/enums.json` 由它與 DDL CHECK 產生 |
| 廣告 | 標頭參數進 openapi（`[FromHeader]`）；`AppAdItemDto.imageVariants`；`GET /app/ads/prefetch`（`AdServingService.PrefetchAsync`） |
| 其他 | 方案與權益 `isFallbackLocale`；`AdminClubLocaleContent.shortName`（讀寫、清快取）；`PlayerDto`／`StaffDto.portraitConsented`；`shared/news-body-blocks.json` |
| **E-170 防呆** | `MigrationsOnBlankDatabaseTests`：拋棄式資料庫（`tcrfc_migprobe_<guid>`）＋最新 DDL → 冪等契約內 migration 套用／回滾／再套用／冪等重跑；`TestDatabaseGuard` 規則的唯一刻意例外（只刪自己建的）。需要能建立資料庫的帳號（本機 sa、CI 的拋棄式 SQL Server 容器皆可） |
| 順帶修 | 課程詳情對有夥伴／有日期的課程 500（`E-171`）；`AddClubShortNameGuardianConsent` 的 `Down`（匿名 CHECK、`EXEC`＋`QUOTENAME` 語法） |
| 測試 | `AppContractBatch5Tests` 7 項、`MigrationsOnBlankDatabaseTests` 1 項、`AdminAdCreativeUploadTests` 補衍生檔網址斷言 |
| 全套 | `dotnet test` **1386／1386 通過**（5 分 20 秒）。⚠️ 第一次全套曾有 `MembershipOrderTests.冪等鍵…` 並行競態偶發失敗一次——確認為產品競態，已修正，見 `E-172` 與本檔結尾「會籍訂單冪等鍵並行競態」節 |
| 未做 | 肖像同意「涵蓋範圍」（待客戶 §16.2 #14）；`MemberRegistrationDto` 的繳費金額以外的繳費欄位（無資料依據） |

## 會籍訂單冪等鍵並行競態（2026-10-05，`E-172`）

- 現象：同一 `Idempotency-Key` 並行建立訂單時，偶發回 409 `open_order_exists`（應為 201 一次、其餘 200）。
- 原因：「查冪等鍵」與「查同方案未完成訂單」之間有空窗，快的請求在此插入訂單，慢的請求把它當成別張未完成訂單。
- 修正：`MembershipOrderService.CreateAsync` 在丟 `open_order_exists` 前，若未完成訂單中有同一冪等鍵者，回該訂單（`Created=false`→200）。唯一索引（`member_id`＋`idempotency_key`）撞號的既有 `catch` 保留。
- 測試：`MembershipOrderTests.冪等鍵_高強度並行…`（16 並行×15 輪，每輪恰一個 201、其餘 200、只一張訂單）。
- 規則：冪等路徑上「發現已存在就拒絕」的檢查，拒絕前先確認是否同一冪等鍵造成。

### 冪等／防重複端點全面壓測（2026-10-05，`E-172` 升級）

| 端點 | 機制 | 壓測結果 |
|---|---|---|
| 會籍訂單建立 | 冪等鍵＋唯一索引＋「未完成訂單」前再認同鍵 | 修好（見上節） |
| 商店結帳 | 冪等鍵＋指紋＋唯一索引 | 80 輪無競態；加固 `cart_empty` 前再查冪等鍵 |
| 慈善捐款建單 | 單號由冪等鍵推導＋唯一鍵 | 15 輪無競態 |
| **試訓報名** | （原本無）→ 交易起手 `UPDLOCK` 鎖場次列 | **真競態已修**：同一人並行重複送出會多筆並重複扣名額 |
| 活動報名 | 既有 `UPDLOCK` 活動列 | 15 輪無競態 |
| 加入俱樂部 | 唯一鍵＋catch 讀現有 | 10 輪無競態 |
| 課程梯次報名、表單送出 | 無「重複擋下」語意 | 不適用 |

測試：`MembershipOrderTests`、`ShopPublicTests`、`CharityDonationFlowTests`、`TrialsPublicTests`、`CulturePublicTests`、`MemberCenterTests` 各一支「高強度」測試。規則見 `docs/14-invariants.md`。

### 搶有限資源的並行壓測（2026-10-05）

| 資源 | 語意依據 | 測試 | 結果 |
|---|---|---|---|
| 商店庫存 | 硬上限；超過回 409 `insufficient_stock`；庫存不得讀快取（docs/14、docs/17） | `ShopPublicTests` 剩 1 件、16 個不同購物車×10 輪；取消／逾時清掃與並行結帳交錯×6 輪 | 恰一張成立、庫存不為負、保留量＝待付款訂單件數 |
| 課程梯次名額 | 規劃書 P2：額滿自動關閉、候補（硬上限，多的進候補） | `ProgramSessionCapacityRaceTests`（直接並行呼叫 `ProgramsRepository`，因公開端點有 20 次／5 分鐘的 IP 限流） | 恰一個待確認、其餘候補、已報名數＝上限、狀態額滿 |
| 試訓場次名額 | 同上 | `TrialsPublicTests` 16×10 輪 | 同上 |
| 活動名額 | 硬上限＋候補 | `CulturePublicTests` 16×10 輪 | 恰一個已報名、其餘候補 |
| 會籍方案 | 無名額／限量欄位 | 略過 | — |

結論：未發現超賣或名額錯算，只補測試，未改產品程式。

## 英文化後端兩件（2026-10-05，`E-220`／`E-221`）

- **賽程對手英文不需改結構**：`matches_i18n` 的 `en` 列存 `opponent`／`venue`，`matches.opponent` 是繁中預設與回退來源；`MatchDto.isFallbackLocale` 在英文 `opponent` 為空時為 true（現行行為正確）。後台 C4 `opponentEn`／`venueEn`（新增、修改、CSV）早已接好。缺的是英文資料，無來源不音譯，沒有 migration。
- **`ApplicableTierLabel`**：`lang=en` 時為 `All members`／`Paid Fan Club members only`，以 `docs/06` §1.1 對照表為準；測試在 `MembershipPublicTests`。

## 後台欄位串接稽核的後端修正（2026-10-06，`docs/23` A-1／A-2／A-3／A-4／A-6／A-12／B-12／B-13／B-16／B-22／E-1／E-2）

測試：`Tcrfc.Api.Tests/AuditWiringFixesTests.cs`（60 項）；`AdminFormsEnquiriesTests`、`AdminTeamsPlayersStaffTests` 各改一支以符合新行為。全套 1,498 項全綠。`shared/` 已重產（`PlayerDto.status`、`PublicFormDto.redirectPath`、`SubmitFormRequest.lang`）。

| 項 | 行為 | 檔案 |
|---|---|---|
| **A-1 烏龍球** | 自動彙總（`Standings/StandingsRepository.AutoTotalsAsync`）排除烏龍球：出賽仍算、進球不算。`match_goals.goal_type` 原本是自由文字，後台寫入改為**固定值域**：`header`（頭槌）／`penalty`（點球）／`free_kick`（自由球）／`own_goal`（烏龍球）／`other`（其他），**空＝一般進球**；寫入時接受常見中文與英文同義詞並正規化成代碼（`頭槌`→`header`、`烏龍球`→`own_goal`…），不認得的值回 400 `goals[i].goalType`。讀取端對**舊資料的自由文字**（含「烏龍」「own goal」）也認得為烏龍球，不需要資料遷移 | `AdminMatches/MatchGoalTypes.cs`、`AdminMatchesRepository.ApplyGoalsAsync` |
| **A-2 球員狀態** | 公開 `players` 列表、詳情（slug 或 id）與全站搜尋**只回現役**（`status = 'active'` 或 NULL）；離隊／外借／海外發展一律不輸出（詳情 404）。`PlayerDto` 新增 `status`（目前恆為 `active`；型別可空，避免 App 舊快取解碼失敗）。球員數據榜（`/stats/players`）不過濾（歷史賽季數據仍需呈現離隊球員） | `Players/PlayersRepository.cs`、`Search/SearchRepository.cs` |
| **A-3 表單欄位鎖定** | `FormCatalog.FieldLockedCodes`＝10.1–10.7 七類＋`proposal_download`（提案下載由 `Features/Proposals` 以固定鍵送出）。`donation_enquiry` **不鎖**（規劃書未定義欄位、無任何程式以固定鍵送出）。鎖定表單：`POST .../fields` → 400 `fieldKey`；`DELETE .../fields/{id}` → 400 `fieldId`；`PUT .../fields/{id}` 若 `fieldKey`／`fieldType`／`isRequired`／`validationRule`／`options` 任一與現況不同 → 400，鍵依序為 `fieldKey`／`fieldType`／`isRequired`／`validationRule`／`options`。仍可改：表單層的 `notifyEmails`／`captchaEnabled`／`redirectPath`／`autoReplyBodyZh|En`，欄位層的 `labelZh|En`、`optionLabelsEn`、`isSummary`、`sortOrder`（前提是結構欄位原樣送回） | `Forms/FormCatalog.cs`、`AdminForms/AdminFormsRepository.cs` |
| **A-4 表單通知信** | `FormsRepository.SubmitAsync` 在 commit 後（honeypot 命中不寄）：① **收件通知**寄給 `notify_emails`（逗號／分號分隔、去重複，每人一封；內容＝題目＋答案，不含同意條款欄位）；② **自動回覆**寄給送件者：Email 取答案中的 `email`、`contact` 欄位值，**值不像 Email（例如電話）就不寄**；內文取 `forms_i18n.auto_reply_body`，依 `lang` 選 zh／en，該語系沒填回退 zh，兩者都空就不寄。寄信失敗只記 log，不影響送出。`SubmitFormRequest` 新增選填 `lang`（`zh`／`en`，也可用查詢字串 `?lang=`，body 優先）。信件 Kind：`form_notify`／`form_auto_reply`（`Features/Email/FormEmailTemplates.cs`）。目前寄信是本機假實作（已知） | `Forms/FormsRepository.cs` |
| **B-16 導向頁** | `PublicFormDto.redirectPath`：只輸出站內相對路徑（`/` 開頭、非 `//`、非 `/\`），不合法或未設定為 `null`。後台 `redirectPath` 驗證同步收緊為**只收站內路徑**（原本也收 `http(s)://`），不合 400 `redirectPath` | `Forms/FormDtos.cs`、`FormsRepository.SafeRedirectPath` |
| **A-6 訂單詳情** | `AdminOrderDetailDto.buyerEmail`（無 `shop.order.reveal` 回遮罩 `b***@example.com`）；`invoice` 新增 `type`（`mobile_barcode`／`citizen_cert`／`tax_id`／`donation`，皆無為 `null`）、`typeLabel`（手機條碼載具／自然人憑證載具／公司戶（統一編號）／捐贈發票）、`carrierId`（無權限遮罩 `/A****23`，只留前 2 後 2）、`taxId`、`donationCode`（公開資訊不遮罩）、`issueStatusLabel`（待開立／已開立／開立失敗（待重試））、`voidStatusLabel`（未作廢／已作廢／已折讓）。列表未加 | `AdminShop/AdminShopOrdersRepository.cs`、`Common/PiiMasking.MaskCarrier` |
| **A-12 賽季管理** | 新 `Features/AdminSeasons`，權限碼 `team.match.view|create|update|delete`（清單另接受 `team.competition.view`）；**寫入另要求球隊列級授權為整個俱樂部**（學院限定／個別球隊帳號 403）。規則：`code` 同俱樂部唯一（不分大小寫，409 `code`）、格式英數與 `/ - _` 且 ≤16（400 `code`）、`endOn` 須晚於 `startOn`（400 `endOn`）、**同俱樂部期間不可重疊**（409 `startOn`，首尾相接日期也算重疊）、被 competitions／matches／standings／achievements／player_season_stats／memberships／membership_plans 任一引用不能刪（409，訊息列出被誰引用）。寫入後失效 `schedule`／`competitions`／`honors`／`calendar` 快取。原 `AdminCompetitions` 底下的 `GET .../seasons` 移到這裡（網址與 `id`／`code`／`startOn`／`endOn` 不變） | `Features/AdminSeasons/` |
| **B-13 球員賽季數據** | `GET /api/v1/admin/{club}/players/{id}/season-stats`（`team.player.view`）；`PUT .../season-stats/{seasonId}`、`DELETE ...`（`team.player.update`＋球員的球隊列級授權）。寫入 `player_season_stats`——**這張表沒有 `source` 欄位，「這一列存在」就是手動**（公開端 `source=manual` 的判斷），所以「清除」＝刪除該列，公開端自動回到賽事彙總。DELETE 對沒有手動數據的賽季也回 204 | `AdminPlayers/` |
| **B-12** | 公開賽程的 `competitionName`／`competitionCode` 只取 `status = 'published'` 的賽事系列；草稿系列的賽事仍列出，但兩欄為空 | `Schedule/MatchesRepository.cs` |
| **B-22 快取** | `AdminForms`（`forms`）、`AdminSeo` 設定（`seo-settings`）／轉址（`seo-redirects`）／llms（`seo-llms-content`）／爬蟲（`seo-crawler-settings`）、`AdminSiteFacts`（`site-facts`）寫入後 `InvalidateAsync`；`AdminTeams` 修改時一併失效 `players`／`staff`／`schedule`／`honors`／`calendar` | 各 `Admin*Repository` |
| **E-1 追蹤碼** | `AdminSeoSettingsRepository` 白名單：GA4 `^G-[A-Z0-9]{4,20}$`、GTM `^GTM-[A-Z0-9]{4,12}$`（兩者自動轉大寫）、Meta Pixel `^[0-9]{5,20}$`、LINE Tag `^[A-Za-z0-9-]{1,64}$`（官方沒公布更嚴格格式）；空白＝清除；不符 400，鍵 `ga4MeasurementId`／`gtmContainerId`／`metaPixelId`／`lineTagId` | `AdminSeo/AdminSeoSettingsRepository.cs` |
| **E-2 行事曆連結** | 自建活動 `ctaUrl` 只收 `http://`／`https://` 完整網址或 `/` 開頭站內路徑（`//`、`/\` 不收），不符 400 `ctaUrl`；新增共用 `AdminInput.OptionalHttpOrSitePath` | `Common/AdminInput.cs` |

新增的欄位鍵（補進上方「已補鍵清單」）：賽季 `code`／`startOn`／`endOn`；球員賽季數據 `appearances`／`goals`／`assists`／`yellowCards`／`redCards`；進球 `goals[i].goalType`；表單欄位鎖定 `fieldKey`／`fieldId`／`fieldType`／`isRequired`／`validationRule`／`options`；`redirectPath`；追蹤碼四鍵；`ctaUrl`。

## 後台欄位串接稽核 C-2：聯絡 Email、社群連結、部門窗口、頁尾簡介（2026-10-06）

不新增資料表，沿用 `settings`／`settings_i18n`（`setting_group='site'`，依俱樂部各一份）。測試：`SiteFactsTests` 新增 C-2 共 13 項；全套 1,516 項全綠；`shared/` 已重產。

| setting key | 形狀 | 後台欄位 |
|---|---|---|
| `site.contact_email` | 單一值 | `contactEmail` |
| `site.social_facebook`／`_instagram`／`_youtube`／`_line` | 單一值（https） | `facebookUrl`／`instagramUrl`／`youtubeUrl`／`lineUrl` |
| `site.contact_departments` | 單一值，JSON 陣列 `[{nameZh,nameEn,email,phoneExtension}]` | `departments[]` |
| `site.footer_blurb` | 逐語系 | `footerBlurbZh`／`footerBlurbEn` |

- 整份取代語意不變：`PUT` 省略欄位＝清空。社群網域白名單（主機等於或為子網域）：Facebook `facebook.com`／`fb.com`／`fb.me`；Instagram `instagram.com`／`instagr.am`；YouTube `youtube.com`／`youtu.be`；LINE `line.me`／`lin.ee`；不收 http、帳密、他站網域。
- 部門窗口最多 20 筆；每筆 `nameZh` 必填，`email`／`phoneExtension` 至少一項；分機只收數字與 `+ - # 空白 ()`。填英文簡介時中文必填。
- 欄位錯誤鍵：`contactEmail`、`facebookUrl`、`instagramUrl`、`youtubeUrl`、`lineUrl`、`departments`（超過筆數）、`departments[i].nameZh|nameEn|email|phoneExtension`、`footerBlurbZh|footerBlurbEn`。
- 公開 `GET /api/v1/{club}/site-facts?lang=`：新增 `contact.email`、`contact.departments[]{name,email,phoneExtension}`（`name` 依 lang，缺英文回退中文）、`social{facebook,instagram,youtube,line}`、`footerBlurb`（依 lang）。全部未設定時為 `null`／空陣列，前台應保留既有預設。寫入後已失效 `site-facts` 快取。
- 共用：`Features/SiteFacts/SiteContactSupport.cs`（鍵與 JSON 解析，壞 JSON 視為空）。

## 稽核 D 類雙語缺口＋移除後台品牌設定（2026-10-06）

**資料庫**（對照 `db/club-schema.sql`、遷移 SQL 在 `db/migrations/20261006_*`）：新側表 `standings_i18n(team_name)`、`achievements_i18n(competition_name, placing)`、`proposals_i18n(title)`；`programs_i18n.audience`（64）、`membership_plans_i18n.mid_season_rule`（255）；主表舊欄位 `standings.team_name`、`achievements.competition_name／placing`、`programs.audience`、`membership_plans.mid_season_rule`、`proposals.title` 與 `clubs` 的 `logo_light_key／logo_dark_key／favicon_key／brand_color／brand_secondary_color` 刪除。locale 值沿用側表慣例（`zh-Hant`／`en`）。

**三支 EF migration（展開—收縮，`docs/20` §5）**：

| migration | 性質 | 內容 |
|---|---|---|
| `20261006090843_DBilingualGapsExpand` | 展開（可隨新版 api 一起上） | 建三張側表、補兩個側表欄位、把主表中文值搬進 `zh-Hant` 列（冪等；SQL 與 `db/migrations/20261006_d-bilingual-gaps_1-expand.sql` 同源） |
| `20261006090900_ClubBrandDropContract` | **收縮（走 `production-db` 核准關卡）** | 刪 `clubs` 五個品牌欄位 |
| `20261006091446_DBilingualGapsContract` | **收縮（走 `production-db` 核准關卡）** | 刪五個舊主表欄位；刪除前逐表核對舊值與 `zh-Hant` 側表一致，不一致 `THROW` 整批回滾 |

🔴 **兩支收縮型 migration 必須在新版 api 部署並驗證之後才套用**（舊版 api 仍 SELECT 這些欄位，先套用會 500）。`db-migrate.yml` 會套用**全部待套用**的 migration，所以**建議收縮型兩支另開後續 PR 才合併到 master**，展開型先上。`Designer.cs`／snapshot 以最終模型為準；`MigrationsOnBlankDatabaseTests` 已涵蓋三支的 Up／Down／冪等重跑。

**讀寫改走側表**（公開 DTO 欄位名不變；公開端依 `lang` 用 `RequestLocale.Pick` 回退）：

| 模組 | 公開 | 後台新增／改變 |
|---|---|---|
| 榮譽（`Honors`／`AdminHonors`） | 賽事名稱、名次依 `lang` 回退 | `competitionNameEn`、`placingEn`；名次上限 32→64（鍵 `placing`、`placingEn`、`competitionNameEn`） |
| 積分榜（`Standings`／`AdminStandings`） | `GET /{club}/standings` **新增選填 `?lang=`**，`teamName` 依語系回退（排序在回退後） | `teamNameEn`（建立／更新／清單／詳情；鍵 `teamNameEn`）；**CSV 匯入**：五欄檔照舊可匯（英文留空），可在最後多一欄 `球隊名稱（英文）`；整季替換，所以不帶英文欄＝該季英文名稱清空 |
| 課程（`Programs`／`AdminPrograms`） | `audience` 依 `lang` 回退 | `audienceEn`（清單／詳情／建立／更新）；`audience` 上限 64（鍵 `audience`、`audienceEn`）；只填英文適合對象時英文列仍建立（`en` 內容區塊維持 `null`） |
| 會籍方案（`MembershipPublic`／`AdminMemberships`） | `midSeasonRule` 依 `lang` 回退 | `midSeasonRuleEn`（詳情／更新）；鍵 `midSeasonRule`、`midSeasonRuleEn`；英文列在「有英文名稱或有英文規則」時存在 |
| 提案（`Proposals`／`AdminProposals`／`AdminLeads`） | `GET /{club}/proposals` **新增選填 `?lang=`**（快取依語系分區），`title` 依語系回退 | `titleEn`（清單／詳情／建立／更新；鍵 `titleEn`）；Lead 的 `proposalTitle` 一律繁中 |

**移除後台品牌設定（主站規劃書 v3.20）**：兩站標誌、Favicon、品牌主色與輔色由前台靜態資產與 CSS 定義。
- `AdminGlobalSettings`：回應刪除 `brand`；`PUT` 刪除 `brandColor`／`brandSecondaryColor`／`removeLogoLight`／`removeLogoDark`／`removeFavicon` 與三個檔案欄位（`logoLight`／`logoDark`／`favicon`）。**仍是 `multipart/form-data`（欄位 `payload`，不附檔）**，舊版客戶端夾帶的品牌欄位被忽略、不報錯。
- `AdminClubs`：`AdminClubDetailDto` 刪 `logoLightKey`／`logoDarkKey`／`faviconKey`／`brandColor`／`brandSecondaryColor`；建立／更新請求刪 `brandColor`／`brandSecondaryColor`。`og_image_*` 保留。
- 公開 `Clubs`：`ClubDto` 刪 `logoLightKey`／`logoDarkKey`／`faviconKey`／`brandColor`／`brandSecondaryColor`／`logoUrl`／`logoDarkUrl`／`faviconUrl`；`SchemaEligible` 改為只看名稱與網域（標誌不是資料庫必填欄位，兩隊恆為 `true`）。`TeamDto` 刪 `logoUrl`（原為球隊 Hero 或俱樂部隊徽回退），`SchemaEligible` 同樣只看隊名與網域；`HeroUrl` 不變。
- 公開 `SiteSettings`：`PublicSiteSettingsDto` 刪 `brand`（維護頁的 `logoLightUrl`／`brandColor` 等）。
- 公開會員中心：`MemberClubBrandDto` 刪 `logoLightUrl`／`logoDarkUrl`／`brandColor`／`brandSecondaryColor`（只剩 `code`／`name`，App 內建兩隊標誌與品牌色、以 `code` 對應）。
- `UploadSlotPolicy`：`clubs` 只剩 `ogImage`。`SchemaRequiredFields`：Organization／SportsTeam 必填由 name＋url＋logo 改為 name＋url；後台 Schema 完整度報表同步。

**測試**：全套 1,514 項（含展開／收縮 migration 的空白庫 Up／Down 重跑）；新增或改寫的重點：榮譽／積分榜（含 CSV 六欄）／課程／會籍方案／提案的中英讀寫與回退、公開 DTO 不再含品牌欄位、全域設定 `PUT` 容忍舊版品牌欄位。`shared/` 已重產。
