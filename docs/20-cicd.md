# 20 — CI/CD 規劃

> 🔵 **這份是執行層決定，不是規格。** 規劃書四份 §1.3 明文排除技術選型與部署方式，CI/CD 屬於同一類，
> 不進規劃書，只記在這裡。本檔與規劃書衝突時一律以規劃書為準（但 CI/CD 本來就不會跟規劃書衝突）。
>
> **這份是 [`17-deployment.md`](17-deployment.md) 的下游**：拓撲、容器清單、VM 規格、網路、LINE Pay 固定
> 出口 IP、快取策略全部沿用 `17`，本檔不重複定義，只處理「程式怎麼從 push 變成跑在那個拓撲上」。
> `17` §8 列的「CI 管線未定」與 STATUS S0-1 的待辦，本檔是它們的答案。
> **App 的 CI 已在 [`19-app-tech-stack.md`](19-app-tech-stack.md) §9 定案，是兩個獨立 private repo，
> 與本檔完全不相交**——本檔只管官網三前台、兩後台、API 這六個應用共用的這個（公開）repo。
>
> 🔴 **Azure 資源目前一個都還沒開**（2026-10-01 使用者已解除暫緩、**Bicep 與 `infra.yml` 已寫但尚未部署**，見 §10 與 [`17`](17-deployment.md) §13）。
> 本檔設計成**開通前就能把 workflow 寫好、開通後只填 IP 與 secrets 就能跑**——凡是依賴實際 Azure 資源的步驟都標了「待補」，收在 §9。
> 🔴 **這個 repo 是公開的**（`waiting0201/tcrfc`），所有設計以此為最高前提。
>
> ✅ **CI 段已實作**（S0-7c，2026-09-22）：`.github/workflows/` 已有 `ci.yml`／`deploy.yml`（含兩份
> 內部可重用 workflow）、`.node-version`／`scripts/check-node-version.mjs`（S0-9h 版本防漂移）。
> ✅ **CD 段已實作**（2026-10-02）：`deploy.yml` 的部署 job、`rollback.yml`、`deploy/cd-deploy.sh`、
> `deploy/cd-purge-cache.sh` 已寫成並以假 docker／curl 在 `/bin/bash` 3.2 下驗證（`deploy/test-cd.sh`）；
> 設計見 **§4a**，回滾見 **§6**，實作進度與「第一次部署」見 **§9a**。**`db-migrate.yml` 已於 2026-10-02 實作**（§5「`db-migrate.yml` 實作」）。

---

## 0. 一分鐘理解

| 議題 | 決定 | 一句話理由 |
|---|---|---|
| **觸發** | push `master` → build → deploy；PR → build ＋ test，**不部署** | 目前只有 `master`，單人／小團隊，不養第二個常駐環境 |
| **Staging** | **不建持久 staging 環境**；用「CI runner 上跑一份完整 compose 做整合測試」取代 | B2ms 已經吃緊，再放一份 staging 容器會跟正式排資源；CI 上跑用完即丟，零常駐成本 |
| **Fork PR** | 一律 `pull_request`（不用 `pull_request_target`），跑在 GitHub-hosted runner，**不給 secrets、不碰 self-hosted runner** | GitHub 對公開 repo 的內建保護＋本檔額外顯式關閉 |
| **映像檔登錄** | **GitHub Container Registry（ghcr.io），套件設為公開** | 公開 repo 上免費（$0／月）；比 ACR 省至少約 US$5–6／月；公開套件讓 VM **不需要任何登入憑證**就能 pull |
| **建置環境** | 一律 **GitHub-hosted runner**（`ubuntu-latest`），**VM 與 self-hosted runner 都不 build** | 保護 B2ms 的叢發 CPU 額度；公開 repo 的 hosted runner 分鐘數**無限免費** |
| **五個映像檔** | `nuxt-club`（給 `nuxt-tcrfc`／`nuxt-bw` 共用，環境變數決定品牌）、`nuxt-charity`、`admin-web`、`admin-charity`、`api` | 主站／藍鯨「同一套網站只換配色」→ 一個映像檔兩個容器；慈善前台與兩個後台功能本質不同 → 各自一個 |
| **部署方式** | **VM 上跑一個 GitHub Actions self-hosted runner**，deploy job 在 VM 本機執行 `docker compose pull && up -d` | **這是解掉「SSH 來源 IP 是浮動的」這題的方法**——不用 SSH 進 VM，VM 自己主動連 GitHub，NSG 完全不用為 CI 開洞 |
| **DB 遷移關卡** | **EF Core Migrations**，套用一律走獨立、需人工核准的 GitHub Environment（`production-db`，Required reviewers），**與例行部署完全脫鉤**。✅ **`db-migrate.yml` 2026-10-02 已實作**（預覽 → VM 唯讀比對 → 核准 → 套用，§5「`db-migrate.yml` 實作」） | 「push 就自動改 prod 的資料庫結構」對 2 GB 硬上限、單機無備援的架構風險太高 |
| **失敗與回滾** | 映像檔一律以 **git SHA** 為 tag；部署後跑健康檢查，失敗自動退回上一個 SHA；人工回滾＝重跑 `workflow_dispatch` 指定舊 SHA | 不需要 blue-green，只要「有上一版可以退」 |
| **Secrets 存放原則** | **能在 VM 本機解決的，不進 GitHub Secrets**——資料庫連線字串、LINE Pay 憑證、Redis 密碼全部放 VM 上的 `.env` 檔，只有「GitHub-hosted 步驟自己要用」的東西（如 Cloudflare 快取清除 token）才是 GitHub Secret | self-hosted runner 讓部署發生在 VM 本機，大部分機密**從來不需要離開 VM**——這是對公開 repo 最重要的降險設計 |
| **部署後動作** | 健康檢查通過才清 Cloudflare 快取（依變動的站台選擇性清） | 內容頁多半在 Cloudflare 邊緣快取，不清舊版會留著 |

---

## 1. 觸發與分支

| 事件 | Workflow | 動作 | 跑在哪 |
|---|---|---|---|
| push → `master` | `deploy.yml` | build（僅變動的應用）→ push ghcr → 部署 → 健康檢查 → 失敗自動回滾 → 清快取（選配） | build 用 hosted；部署用 self-hosted（label `tcrfc-vm`，`environment: production`） |
| 手動（`workflow_dispatch`） | `deploy.yml` | **五個映像檔全部重建並推送**（不看變動範圍）。首次部署、映像檔遺失或要強制重建時用（2026-10-02 加，E-112）。映像檔標籤只有 `:master` 與 git SHA，**沒有 `:latest`**——`IMAGE_TAG` 預設 `master` | hosted |
| `pull_request` → `master`（含 fork） | `ci.yml` | lint ＋ unit test ＋ `docker build`（**不 push**）＋ **`shared/` 契約漂移檢查**（`shared-contract` job，AP-8，見 §3；後台前端若日後有型別產生器再加另一個漂移檢查） | 一律 hosted |
| 手動 | `db-migrate.yml` | 套用 EF Core migration，需 `production-db` 環境核准 | self-hosted |
| 手動 | `rollback.yml` ✅ | 輸入 SHA，把正式 VM 退回那一版（映像檔標籤；不重建、不 migrate），§6 | self-hosted |

**不建 staging 分支／環境。** 理由：

> 🔵 **與 [`17-deployment.md`](17-deployment.md) §10「上線前的暫用網址」的關係（2026-09-20 補充，
> 2026-09-21 更新）**：這裡講的「不建 staging」是**不另建一套基礎設施**做 CI 用的一次性整合測試；
> `17` §10 講的是**這一套正式基礎設施本身**，在藍鯨／慈善正式網域到位前、主站尚未從 Wix
> 切換前，先用 `tcrfc.tw` 子網域跑的過渡階段——同一台 VM、同一批容器、同一個資料庫，
> 只是網域環境變數還沒換成最終值。兩者不衝突，也不是同一件事。
>
> 🔴 **兩邊都不會產生一個叫 staging 的環境。全專案只有兩套環境：本機開發
> （`docker-compose.dev.yml`）與正式 VM（`docker-compose.yml`）。** `17` §10 那個過渡階段
> **沒有自己的 compose 檔**，差異全部在 `.env` 的值（`SITE_ENV=prelaunch`、`CADDYFILE` 指向
> `deploy/Caddyfile.prelaunch`、六個暫用網域）——2026-09-21 定案，原本規劃過的
> `docker-compose.staging.yml` 已撤銷，理由見 [`18-work-errors.md`](18-work-errors.md) `E-13`。

- VM 是 `Standard_B2ms`（2 vCPU／8 GB），八個正式容器已經要盯 CPU 額度（`17` §1）；再放一份 staging 容器組，不是搶資源就是要開第二台 VM（雙倍 Azure SQL／VM／IP 成本，且**多一個出口 IP 要不要也登記 LINE Pay** 又是一個決定）。
- 用 **CI runner 本身當一次性 staging**：`ci.yml` 在 hosted runner 上用 `docker compose -f deploy/docker-compose.ci.yml up` 起一份完整堆疊（`api` ＋ 一個用完即丟的 SQL Server 容器，比照 S0-6b 本機驗證 DDL 的做法），跑整合測試後整組銷毀。**零常駐成本，且每次 PR 都測，比一個手動維護的 staging 環境更常被驗證。**
- 前端純視覺變更可用 **Cloudflare Pages 的 PR 預覽**（免費、每個 PR 自動出一個網址）快速看畫面，但那是輔助工具不是正式 staging——它沒有 API／DB，SSR 資料層看不到。

### Fork PR 隔離

| 措施 | 做法 |
|---|---|
| 事件 | 只用 `pull_request`，**絕不用 `pull_request_target`**（後者會把 workflow 檔本身的權限用在 fork 的程式碼上，是最常見的公開 repo CI 漏洞） |
| 權限 | `ci.yml` 頂層宣告 `permissions: contents: read`，不給 `packages: write`、不給部署相關權限 |
| Secrets | fork 觸發的 `pull_request` run **本來就拿不到 repo secrets**（GitHub 內建行為）；本檔再加一道——`ci.yml` 全程不 `push` 映像檔、不觸碰 `production`／`production-db` 這兩個 Environment，即使不小心加了 secret 進 workflow 也用不到 |
| Runner | fork PR **一律 `runs-on: ubuntu-latest`**（hosted）。`self-hosted` 這個 label 只出現在 `deploy.yml`／`db-migrate.yml`／`rollback.yml`，這三個都不被 `pull_request` 觸發 |
| 資源濫用防線 | Repo 設定把「Fork pull request workflows」改成 **Require approval for all outside collaborators**（比 GitHub 預設的「僅首次貢獻者」更嚴）——本專案目前沒有外部協作者，這條純粹防呆 |
| 分支保護 | 建議 `master` 開 **Require pull request before merging**。目前是單人開發，非強制，但**觸及 `apps/api/Migrations/**` 或 `deploy/**` 的變更建議一律走 PR**，讓變更留下 diff 紀錄——這比擋 push 本身更重要 |

---

## 2. 映像檔要放哪裡

| 方案 | 月費 | 備註 |
|---|---|---|
| **GHCR（GitHub Container Registry），套件設公開** | **US$0** | 公開套件的儲存與流量在公開 repo 下不計費。VM 匿名 `docker pull`，**不需要任何登入憑證** |
| GHCR，套件設私有 | 視用量，公開 repo 下私有套件仍有免費額度（約 500 MB 儲存／1 GB 流量），超過小額計費（約 US$0.25／GB 儲存、US$0.50／GB 流量） | 本專案五個映像檔量級不大，通常在免費額度內，但要多管一組 VM 用的 PAT |
| Azure Container Registry Basic | **約 US$5.1／月**（US$0.167／天）＋ 超量儲存 | 與 VM 同區（Japan East）拉取免流量費，但 base fee 免不掉；可用 VM 的 Managed Identity 免密碼拉取（比 GHCR 私有套件的 PAT 更安全），但公開套件的 GHCR 兩邊都不用密碼，這項優勢就不成立了 |

**決定：GHCR，套件公開。**

- 🔵 **為什麼公開沒問題**：原始碼本來就在公開 repo，映像檔內容不會洩漏原始碼以外的東西；**只要建置時不把真正的機密（連線字串、LINE Pay 金鑰）當 build arg 烤進 image layer**，公開映像檔的風險就只剩「別人看得到你用什麼版本的相依套件」，這本來就從 `package.json`／`*.csproj` 看得到。
- ⛔ **這條前提是紀律**：Dockerfile 一律**只在 `docker run` 時吃環境變數**，不接受 `ARG` 傳入任何 secret；Nuxt 的 `runtimeConfig.public.*` 之外的值不得出現在建置輸出裡。建置流程要有一步**掃過建好的 image layer 找像是連線字串／金鑰格式的字串**（`trufflehog`／`gitleaks` 對 image 也能掃），PR 與 push 都跑。
- **推送用 `secrets.GITHUB_TOKEN`**（workflow 內建，`permissions: packages: write`），不另外開 PAT——少一個要保管、要輪替的憑證。
- 每個映像檔打兩種 tag：`ghcr.io/waiting0201/tcrfc-<app>:<git-sha>`（不可變，部署永遠用這個）與 `:master`（浮動 tag，方便手動 `docker pull` 除錯用，**部署 workflow 不得依賴它**）。

---

## 3. 建置

### 五個映像檔，不是六個

| 應用 | 映像檔 | 判斷 |
|---|---|---|
| `nuxt-tcrfc` ＋ `nuxt-bw` | **同一個 `nuxt-club` 映像檔**，兩個容器、不同環境變數 | 藍鯨規劃書 §1.3 總則「與主站同一套網站，只有配色不同」——**這句話本身就是在說「一份程式碼」**。品牌色作為 CSS custom properties，由 `NUXT_PUBLIC_CLUB=tcrfc\|bw` 這個 runtime 環境變數在**執行期**（不是建置期）決定套用哪組 `:root` 變數／favicon／`theme-color`。一個映像檔兩個容器保證兩站**不可能出現「改了主站忘記改藍鯨」的版本漂移**，且建置與儲存成本減半 |
| `nuxt-charity` | 獨立映像檔 | 獨立網域、獨立內容、獨立後台對應的獨立前台，功能本質不同，不是配色差異 |
| `admin-web` | 獨立映像檔 | 官網共用後台（`club_id` 分資料，站台切換器） |
| `admin-charity` | 獨立映像檔 | `17` §5 明文「獨立後台、獨立帳號體系、獨立 2FA」，功能模組（`N1–N7`）與官網後台（`J/B/C/...`）不重疊 |
| `api` | 獨立映像檔 | 拓撲上本來就是一個 .NET 行程持兩個 `DbContext`（`17` §1），沒有拆的理由 |

> ⚠️ **`nuxt-club` 走 runtime 切換這件事，實作細節（CSS 變數怎麼在執行期換、favicon 怎麼依 host 選）是前端架構決定，不是本檔決定。**
> 本檔只定「部署層假設是一個映像檔、跑期環境變數切換」，請 `frontend-architect` 依此設計 Nuxt 專案結構；
> 若技術上真的窒礎難行（例如 Nuxt 的某些建置期常數無法做成 runtime config），**退路是兩個映像檔**，
> 屆時回來改本節，不影響其他四個映像檔的設計。

### Job 切法

**全部平行、無相依**，用 `paths-filter`（如 `dorny/paths-filter`）依目錄變動決定哪些映像檔要重建：

```
apps/web/**            → 建 nuxt-club   （nuxt-tcrfc／nuxt-bw 共用）
apps/web-charity/**    → 建 nuxt-charity
apps/admin/**          → 建 admin-web
apps/admin-charity/**  → 建 admin-charity
apps/api/**            → 建 api
deploy/**              → 不建映像檔，但要跑部署 job（compose／proxy 設定變了）
```

> ✅ **`apps/*` 目錄結構已於 S0-7a（2026-09-20）建立骨架**（[`apps/README.md`](../apps/README.md) 有完整對照表）。
> 原規劃的路徑是 `apps/nuxt-club`／`apps/admin-web`，本節當時已明文「實際建立專案骨架時可調整」——
> S0-7a 實際採用上表這組更短的名稱（`web`／`web-charity`／`admin`／`admin-charity`／`api`），
> 已回填本節；**目錄一對一映射映像檔**這個原則有保留，`paths-filter` 依然切得乾淨。

### `shared-contract`：App 契約漂移檢查（AP-8，2026-10-02）

`shared/`（[`docs/19`](19-app-tech-stack.md) §2）裡有四類**產生檔**——`openapi.json`、Swift／Kotlin DTO、`error-codes.json`——來源是 `apps/api`。
後端改了 API 卻沒重新產生並提交，App 與後端的契約就悄悄分岔，所以 `ci.yml` 有一個獨立 job：

| 項目 | 內容 |
|---|---|
| 觸發 | `paths-filter` 的 `shared` 輸出：`shared/**`、`apps/api/**`、`.node-version` 任一有變動 |
| 跑在哪 | `ubuntu-latest`（**GitHub-hosted**）；`permissions: contents: read`；**不使用任何 secrets、不推送、不碰 self-hosted**——符合 §4 防護鏈，與其他 CI job 相同 |
| 步驟 | `setup-dotnet`＋`setup-node`（`.node-version`）→ `dotnet restore --locked-mode` → `shared/scripts/gen-all.sh`（建置期產生 OpenAPI → 產生 DTO → 掃描錯誤碼 → `check-shared.py` 一致性檢查）→ `git add --intent-to-add shared` ＋ `git diff --exit-code -- shared apps/api/packages.lock.json` |
| 為什麼不需要資料庫 | OpenAPI 由 `Microsoft.Extensions.ApiDescription.Server` 在**建置期**以記憶體內的 host 讀端點中繼資料，不開 Kestrel、不開連線。`gen-openapi.sh` 寫死的連線字串與簽章金鑰是**假值**（連不上任何東西、不是任何環境的憑證），只為通過 `Program.cs` 的啟動檢查 |
| 失敗時 | 在本機執行 `./shared/scripts/gen-all.sh`，把變更的產生檔一起提交 |
| 與正式環境的關係 | `Tcrfc.Api.csproj` 的 `OpenApiGenerateDocuments` 預設 `false`（只在腳本傳 `-p:OpenApiGenerateDocuments=true` 時產生），Docker 建置與一般建置不受影響；**正式環境依然不公開 OpenAPI／swagger**（`Program.cs` 只在 `Development` 掛 `MapOpenApi`，[`17`](17-deployment.md) 風險表第 7 項） |

> ⚠️ 這條漂移檢查只守本 repo 這一側（產生檔 = 後端現況）。**App 端（`tcrfc-app-ios`／`tcrfc-app-android`，AP-7）的檢查是另一件事**：它們固定在某個 `shared/` 版本，CI 比對自己取用的那份是否等於上游，見 `docs/19` §2。

**PR（`ci.yml`）**：`lint` → `unit test` → `docker build`（`push: false`，只驗證 Dockerfile 能建成）→ 整合測試（見 §1 的「CI 當 staging」）。
**Push master（`deploy.yml`）**：同樣先 build，成功才 `push: true` 到 ghcr，再進部署 job。

### 快取

| 快取 | 做法 |
|---|---|
| npm | `actions/setup-node` 內建 `cache: 'npm'`，key 依各應用自己的 `package-lock.json`（五個應用各自快取，不共用）。🔴 **`node-version` 必須與四個 Node 應用 `Dockerfile` 的 base image 版本一致**（[`17-deployment.md` §12](17-deployment.md#12-前端建置用的-nodejs-版本) 已定案 `24.13.1`）——`lint`／`unit test` 這兩個 CI 步驟不是在容器裡跑，若 `setup-node` 版本落後於 `Dockerfile`，會重演「本機／CI 環境比建置映像檔舊，直到 `docker build` 才炸」的同一種落差，只是把炸點從「開發機」換成「CI」，沒有解決根因 |
| NuGet | `actions/setup-dotnet` 搭 `actions/cache`，key 依 `packages.lock.json`（`api` 專案要開 `--use-lock-file`） |
| Docker layer | `docker/build-push-action` 的 `cache-from/cache-to: type=gha`。⚠️ GitHub Actions cache 每個 repo 上限約 10 GB 會被自動淘汰最舊的，五個映像檔共用這個額度，**上線後留意快取命中率，必要時分開 scope（`scope: <app名>`）避免互相擠掉** |

---

## 4. 部署

### 🔴 D 的核心問題：GitHub-hosted runner 沒有固定來源 IP，SSH 白名單開不出來

`17` §2 的 NSG 設計是「SSH 限固定來源」，但 GitHub-hosted runner 的 IP 來自一個巨大、共享、會變動的區段（GitHub 有公告但範圍太大，等於沒有防護意義）。**兩條路**：

| 方案 | 做法 | 風險 |
|---|---|---|
| A．SSH push（GitHub 主動連 VM） | 把整段 GitHub Actions IP range 放進 NSG，或另建一台固定 IP 的跳板機 | 前者等於沒有 IP 限制；後者多一台機器、多一筆成本，且仍要在 GitHub Secrets 放 SSH 私鑰——**私鑰躺在公開 repo 的 Secrets 裡，是最敏感的一類外洩風險** |
| **B．VM 上跑 self-hosted runner（VM 主動連 GitHub）**✅ | 在 VM 裝一個 GitHub Actions self-hosted runner（常駐背景服務，outbound 連 GitHub，NSG **不需要為此開任何 inbound**），部署 job 直接 `runs-on: [self-hosted, tcrfc-vm]`，在 VM 本機執行 `docker compose pull && up -d` | 見下方「風險與防護」 |

**採方案 B。**

**為什麼 B 可行、而且比 A 更安全**：

- **NSG 完全不用為 CI 開洞**——`17` §2 那條「SSH 限固定來源」可以維持給人工緊急登入用（Tim 自己的固定 IP 或 Azure Bastion），跟部署管線完全脫鉤，**不再需要為了 CI 放寬它**。
- **GitHub Secrets 裡不需要放 SSH 私鑰**——self-hosted runner 的註冊 token 是一次性、短效的，註冊完就不需要再用，不是長期存放的憑證。
- 部署 job 的 `actions/checkout` 直接把 repo checkout 到 VM 本機（runner 的 work 目錄本來就在 VM 上）——**`docker-compose.yml`、Caddy 設定檔等版控內容自動就在 VM 上，不需要另外 scp／rsync**。

**GitHub 官方明文建議公開 repo 不要用 self-hosted runner**，原因是惡意 fork 的 PR 可能讓 workflow 在你的機器上跑任意程式碼。**本檔的防護對應如下，五條缺一不可，而且第 0 條是地基**：

| # | 防護 | 說明 |
|---|---|---|
| **0** | 🔴 **Repo 設定 → Actions → Fork pull request workflows 設為「Require approval for all outside collaborators」** | **這是唯一真正擋住它的控制項，其餘三條都建立在它之上。** ⚠️ **不要以為「我們的 workflow 沒給 fork 用 self-hosted」就安全**——fork PR 跑的是**該 fork 版本的 workflow 檔**，惡意 fork 可以自己加上 `runs-on: self-hosted`。GitHub 公開 repo 的預設只擋「首次貢獻者」，**要手動改成 all outside collaborators** |
| 1 | **self-hosted 這個 label 只出現在被 `push`／`workflow_dispatch` 觸發的 job**，絕不出現在 `pull_request` 觸發的 job | 減少誤觸面，讓正常流程不依賴核准。⚠️ **這條本身擋不住惡意 fork**（見第 0 條），它防的是自己人改錯 |
| 2 | 部署／遷移／回滾三個 workflow 都用 `environment: production`（或 `production-db`），Environment 設 **Deployment branches：僅 `master`** | 擋的是**機密外洩**不是程式碼執行——Environment 的分支限制讓非 `master` 的 run 拿不到該環境的 secrets。⚠️ **不參照 Environment 的 job 仍可能跑在 runner 上**，那一層靠第 0 條 |
| 3 | 能推到 `master` 的只有受邀協作者（目前只有 Tim）——**push 到 master 本身就是既有的信任邊界**，self-hosted runner 沒有新增風險面，只是把「誰能讓程式碼跑在 VM 上」的判準從「有沒有 SSH 私鑰」換成「能不能推 master」，後者本來就等於「是不是專案維護者」 | 公開 repo 只開放 fork＋PR，不代表任何人都能推 `master` |
| 4 | self-hosted runner 的服務帳號**只加入 `docker` 群組**，不給 sudo／不跑在 root（`docker` 群組本身等同主機控制權，這是刻意接受的範圍——它就是拿來管 compose 的） | 出事時的爆炸半徑限制在「能操作這台 VM 的容器」，跟原本設計的職責一致 |

### `docker compose up -d` 的實際行為

- Compose 比對每個 service 的映像檔／設定 hash，**只重建變了的容器**——這次部署若只有 `api` 換了 tag，`redis`／`nuxt-*` 不會被動到。
- 對「有變」的容器：**先停舊容器、再起新容器**，中間有數秒到數十秒的空窗（.NET 冷啟動＋EF Core 初始化通常是最慢的一個）。**這不是零停機部署**——plain Compose 沒有滾動更新能力，Swarm／K8s 才有，而本專案明確不用那兩個。
- **可接受的理由**：這是一個俱樂部官網，不是高併發 SaaS；停機窗口以秒計，且可安排在離峰（台灣時間清晨）。要做到真零停機需要在同一台 B2ms 上同時養兩份容器做藍綠切換，**資源上划不來**，列入 §8「本檔不決定」以備日後升級。

### 部署後：清 Cloudflare 快取

健康檢查通過後，依**哪個映像檔換了**決定清哪個 zone：`nuxt-club` 換了 → 清 tcrfc 與藍鯨兩個 zone；`nuxt-charity` 換了 → 清慈善 zone；只有 `api`／`admin-*` 換了 → 不清（後台與 API 不經 Cloudflare 快取內容）。用一個 **Cloudflare API Token，僅 `Zone.Cache Purge` 權限、限定這幾個 zone**，存 GitHub Secret（這是少數真的需要進 GitHub Secrets 的東西，因為 purge 呼叫的是 Cloudflare API 不是 VM 本機資源）。

---

### 4a. CD 實作（2026-10-02）

> 🔵 **本節是 §4 的落地**：`deploy.yml` 的 `deploy` job、`rollback.yml`、[`deploy/cd-deploy.sh`](../deploy/cd-deploy.sh)、
> [`deploy/cd-purge-cache.sh`](../deploy/cd-purge-cache.sh)。邏輯放在腳本、不寫在 workflow 的 `run:` 裡——可以用假 docker 測
> （[`deploy/test-cd.sh`](../deploy/test-cd.sh)，`/bin/bash deploy/test-cd.sh`，60 項斷言），且 workflow 只傳 `env:`，不把任何輸入內插進 shell 字串。

**流程**

```
push master / dispatch ─▶ changes ─▶ build-*（hosted，只建有變動的）─▶ deploy（self-hosted，environment: production）
                                                                         │ actions/checkout（persist-credentials: false）
                                                                         │ deploy/cd-deploy.sh
                                                                         │   1 取鎖、前置檢查（SHA 格式、.env、compose config）
                                                                         │   2 讀 deploy-state.env 的上一版標籤
                                                                         │   3 算本次五個映像檔的標籤
                                                                         │   4 缺的從 ghcr 拉；浮動的 master 先另存 cd-prev
                                                                         │   5 docker compose up -d --wait；Caddyfile 變了就重建 proxy
                                                                         │   6 健康檢查（逾時 300 秒）
                                                                         │   7 失敗→退回上一版並再檢查｜成功→寫 state／history
                                                                         ▼ deploy/cd-purge-cache.sh（選配，只在成功時）
```

**何時部署**：`needs` 全部 build job 與 `changes`；**任何一個失敗或被取消就不部署**（舊版繼續跑）；被 skip 的 build 不擋（`!cancelled()`）。
只有「至少一個映像檔重建」或「`deploy/**`／`docker-compose.yml` 有變」才部署（`workflow_dispatch` 全部視為有變）。
`github.repository`、`github.ref == refs/heads/master` 與事件種類另在 job 條件式再擋一次。`concurrency: cd-production`（**不可取消**，與 `rollback.yml` 共用群組）。

**在哪個目錄跑：runner 的 checkout 目錄，`/home/runner/tcrfc-src` 退役**

| 考量 | 決定 |
|---|---|
| 唯一來源 | compose 專案目錄＝`/opt/tcrfc/actions-runner/_work/tcrfc/tcrfc`（`actions/checkout` 的固定路徑）。`docker-compose.yml`、`deploy/Caddyfile*` 永遠等於本次提交，不需要另一份 git clone 去 `pull`（那份會有「忘了 pull」「髒工作樹」「與 workflow 版本不同步」三種漂移） |
| compose 專案名 | `docker-compose.yml` 頂端有 `name: tcrfc`，與現行容器前綴 `tcrfc-` 一致；**專案名不依目錄名**，不會起第二套容器撞埠。**不要**在 CD 或手動指令加 `-p` 或設 `COMPOSE_PROJECT_NAME` |
| `.env` | 以 `--env-file /opt/tcrfc/.env` 讀（不複製進 checkout，checkout 的 `git clean` 也碰不到它）。🔴 compose 只在**沒給** `--env-file` 時才自動載入專案目錄的 `.env`，所以 checkout 目錄裡有沒有 `.env` 都不影響 |
| `CADDYFILE` | `.env` 的 `./deploy/Caddyfile.prelaunch` 是**相對於 compose 專案目錄**，解析為 `<checkout>/deploy/Caddyfile.prelaunch`，是本次提交的版本 |
| 🔴 **首次切換會把八個容器全部重建一次** | 容器的設定 hash 含 `build.context` 與 bind mount 的**絕對路徑**；專案目錄從 `/home/runner/tcrfc-src` 換成 checkout 目錄，路徑變了，compose 判定每個服務都要重建。**預期會有一次約 1–2 分鐘的整站中斷**（`up --wait` 依 `depends_on` 順序起）。Redis 快取清空（可接受，cache-aside）；`api` 的金鑰環是 bind mount `/opt/tcrfc/data-protection`，**不受影響**；`caddy_data` 具名 volume 保留，憑證不會重領。之後專案目錄固定，不再發生。**建議安排在離峰時段做第一次** |
| `tcrfc-src` 退役 | 第一次 CD 部署**成功**後，在 VM 上 `rm -rf /home/runner/tcrfc-src`（使用者手動，本工作不碰 VM）。⚠️ 在那之前、之後都**不要再從那個目錄執行 `docker compose up`**——那會把專案目錄換回去，再重建一次整站，並讓 proxy 掛到舊 Caddyfile |

**映像檔標籤（本節最容易做錯的地方）**

`deploy.yml` 只重建「有變動」的映像檔，所以**沒重建的映像檔沒有該次 git SHA 的標籤**；全部共用一個 `IMAGE_TAG=<sha>` 會拉不到。因此：

- `docker-compose.yml` 改為**每個映像檔各一個變數**：`TAG_NUXT_CLUB`／`TAG_NUXT_CHARITY`／`TAG_ADMIN_WEB`／`TAG_ADMIN_CHARITY`／`TAG_API`，沒設才退回 `IMAGE_TAG`，再退回 `master`（`${TAG_API:-${IMAGE_TAG:-master}}`）。手動 `docker compose up -d` 不必設，沿用 `.env` 的 `IMAGE_TAG='master'`，行為與現在相同。
- CD 以**行程環境變數**逐一指定（環境變數優先於 `--env-file`），**不改寫 `/opt/tcrfc/.env`**。
- 本次標籤＝**有重建的用 `<git sha>`，其餘沿用 `deploy-state.env` 記錄的上一版**；沒有紀錄的視為 `master`。
- `deploy-state.env` 記**最後一次成功部署**的五個標籤；`deploy-history.log` 每次成功追加一行（含 `mode=deploy|rollback`），人工回滾靠它找回「那一版當時的完整標籤組合」。

> ⚠️ **已知限制：被 GitHub 取消的排隊中 run**。GitHub 的 `concurrency` 即使 `cancel-in-progress: false`，**同群組只保留一個排隊中的 run，更舊的排隊 run 會被取消**。若兩次 push 間隔極短，被取消的那次的映像檔**根本沒建**，而下一次 push 的 paths-filter 只比對自己這次的變動——被跳過的那次若改了 `apps/api` 而下一次沒碰，`api` 會停在舊版而不報錯。單人開發、push 間隔通常夠長，風險低；**防線**：Actions 手動 Run workflow（`deploy.yml`）會**全部重建並部署**，連續快速 push 後跑一次即可對齊。job summary 的映像檔表可核對每個映像檔實際標籤。

**健康檢查**（逾時 `CD_HEALTH_TIMEOUT`＝300 秒，每 5 秒重試，三關全過才算成功）

1. `docker compose ps -a -q` 的容器數 ＝ compose 服務數（8），且每個 `running` 且 healthy（沒定義 healthcheck 的只要 running）。
2. `docker compose exec -T api curl http://127.0.0.1:8080/readyz` 回 `"status":"ready"`（`club_db`／`charity_db` 失敗＝不就緒；`redis: degraded` 只在 summary 警告，不算失敗，呼應 `17` §4）。
3. 六個網址經 **VM 本機**以 `curl --resolve <網域>:443:127.0.0.1` 打 Caddy，要 200（五個前台／後台打 `/`，API 打 `/readyz`）。**不走公開 IP／Cloudflare**：NSG 只放 Cloudflare 段，VM 自連自己的公開 IP 會被擋或繞一圈；`--resolve` 保留 SNI 與憑證驗證，測得到「proxy 依 Host 分流到上游」，測不到 Cloudflare 那一段（那段由 §9 第 10 項首次演練與 `17` §9 驗證負責）。

**Caddyfile 是單一檔案 bind mount 的陷阱**：git 換檔會產生新 inode，容器仍抱著舊檔，且 compose 看不出（來源路徑字串沒變）。所以 `up` 之後腳本直接比對「執行中 proxy 讀到的 `/etc/caddy/Caddyfile` 雜湊」與 checkout 裡的檔案，不同就 `up -d --no-deps --force-recreate proxy`（TLS 憑證在 `caddy_data` volume，不會重領）。

**失敗與回滾**：任何一關失敗（含 `up --wait` 逾時）→ 先把「上一版標籤」的映像檔確認在本機（沒有就拉）→ 以上一版標籤再 `up -d --wait` 與同一套健康檢查 → job 標記失敗（結束碼 1＝已退回；2＝退回也失敗）。**退回只退映像檔，不退 `deploy/`、`docker-compose.yml`**（checkout 是新版）；若問題出在設定檔，`git revert` 後 push。`deploy-state.env` 失敗時**不更新**。

**首次銜接（`deploy-state.env` 是空的、容器是手動以 `:master` 起的）**

- 空狀態＝每個映像檔的「上一版」視為 `master`。因為 `:master` 是浮動標籤、build 已把它指向新版，**單靠標籤退不回「手動啟動的那一版」**，所以對「將被換掉、且上一版是 `master`」的映像檔，腳本先用 `docker inspect` 取出**現在正在跑的映像檔 ID**，在本機另打 `…:cd-prev` 標籤，退回時用它。這個標籤只在本機，**不會、也不需要推到 ghcr**（退回用 `--pull never`）。
- 首次部署成功後 state 才有紀錄，**下一次起**退回目標是真正的 SHA 標籤。「手動 `:master` 的那一版」在首次成功後就不再可回滾（本機 `cd-prev` 標籤還在，但不在 history 裡）——這是刻意的，首次銜接前沒有可追溯的版本。
- 沒重建的映像檔在 state 裡記 `master`，保持目前跑的那份不動（腳本不會對它們 `pull`）。

**清 Cloudflare 快取（選配）**：`deploy/cd-purge-cache.sh`，只在部署成功後跑。**沒設 `CLOUDFLARE_API_TOKEN` 就略過並在 summary 註明；呼叫失敗也只警告，結束碼恆為 0，部署不會因此失敗。** 依「哪個映像檔換了」決定：`nuxt_club` → 主站與藍鯨主機；`nuxt_charity` → 慈善主機；只換 `api`／`admin-*` 不清。用 **依主機名稱清除（`{"hosts":[…]}`）**，不是 `purge_everything`——暫用網域 `4webdemo.com` 與其他網站共用同一個 zone，整區清會連帶清掉別人的快取。token 經 `curl -K -` 從 stdin 餵，不出現在命令列。需要：secret `CLOUDFLARE_API_TOKEN`（權限 **Zone → Cache Purge → Purge**，限定對應 zone）、Actions Variables `CF_ZONE_ID_TCRFC`／`CF_ZONE_ID_BW`／`CF_ZONE_ID_CHARITY`（暫用網域期間三個是同一個 zone ID）。

**不做的事**：不執行任何 migration（§5）；不 build（hosted runner 做完了）；不改寫 `/opt/tcrfc/.env` 與 `/opt/tcrfc/secrets/*`；不刪除 `/opt/tcrfc/data-protection`；不 `docker compose down`、不 `--remove-orphans`、不 `docker system prune`。失敗通知管道仍待使用者選定（§8）。

---

## 5. 🔴 資料庫遷移關卡

### 現況與怎麼演進

現有 `db/club-schema.sql`（144 表）與 `db/charity-schema.sql`（29 表）是**整份建表腳本**，本機已用 SQL Server 容器實測通過（S0-6b），**但這不是給例行部署跑的東西**——它只在資料庫第一次誕生時跑一次。

| 階段 | 用什麼 | 誰跑、怎麼跑 |
|---|---|---|
| **建庫（僅一次）** | 現有 `db/*.sql` 整份腳本 ＋ `db/prod/*-reference-data.sql` 參照資料 ＋ 手寫 `__EFMigrationsHistory` | **人工在 VM 上執行 [`deploy/prod-db-init.sh`](../deploy/prod-db-init.sh)**，不進 CI／CD。這是「創世」不是「部署」。做法與理由見下方「🔵 正式庫首次初始化」 |
| **建庫之後的每次結構變更** | **EF Core Migrations** | ✅ [`db-migrate.yml`](../.github/workflows/db-migrate.yml)（2026-10-02 已實作，見下方「`db-migrate.yml` 實作」），**與例行程式部署脫鉤，走獨立核准關卡** |

**EF Core 與現有手寫 DDL 怎麼接軌**：`api` 專案第一次建立時，對著已經用 `db/*.sql` 建好的資料庫跑 `dotnet ef dbcontext scaffold`（reverse engineer），產出 Entity 類別，並建立一個**標記為已套用的空白基準 migration**（`InitialBaseline`，`Up()`／`Down()` 刻意清空）。✅ 這步驟已完成（兩個 context 各有 `InitialBaseline`），之後進入「每次改動都是一個新 migration」的常態。

> 🔴 **正式庫「標記為已套用」不能用 `dotnet ef database update`**（2026-10-01）：`InitialBaseline` 之後的 migration 有真的 `ALTER`／`CHECK`／`UPDATE`（例如 `AlignSchemaS17a`、`AlignSchemaB1`），對「已經用最新 `db/club-schema.sql` 建好」的庫執行它們會重複套用而失敗或改壞。正確做法是**直接在 `__EFMigrationsHistory` 寫入全部 migration**，由 `deploy/prod-db-init.sh` 完成（見下節）。**往後新增的 migration 才由 `db-migrate.yml` 走核准關卡套用。**

### 關卡設計

```
開發者改 Entity → dotnet ef migrations add <Name> → migration 檔進 PR（人工 code review）
                                                            │
                                                            ▼
                                      合併進 master（不會自動套用到 prod）
                                                            │
                                                            ▼
                              手動觸發 db-migrate.yml（workflow_dispatch：target＝club／charity／both、dry_run 預設 true）
                                                            │
                                                            ▼
              preview（GitHub-hosted）：dotnet ef migrations script --idempotent ＋ 每支 migration 單獨的 SQL，SHA-256 鎖定
                                                            │
                                                            ▼
              pending（VM，唯讀，environment: production）：讀 __EFMigrationsHistory，與 repo 比對，列出「待套用」與其 SQL
                                                            │  dry_run=true 或沒有待套用 → 到此結束，沒有核准關卡
                                                            ▼
                    ⛔ GitHub Environment「production-db」— Required reviewers（人工在 GitHub UI 按下 Approve）
                                                            │
                                                            ▼
                  apply（VM）：再比對一次（名單與核准時不同就拒絕）→ sqlcmd 容器執行同一份 SQL → 驗證歷史表
                                                            │
                                                            ▼
                                        成功 → 記錄套用的 migration 名稱；失敗 → 停在原地，不自動重試
```

**兩道關卡，不是一道**：① 一般 PR review 審過 migration 產生的 SQL（`dotnet ef migrations script` 可以先跑出可讀 SQL 附在 PR 描述）；② `production-db` Environment 的 Required reviewers 是最後一道人工按鈕。**GitHub 的 Environment protection rule（含 Required reviewers）在公開 repo 上是免費功能**，不需要升級方案。

**與例行部署脫鉤的意思**：`deploy.yml`（push master 就跑的那條）**只換容器映像檔，永遠不執行任何 migration 指令**。要讓新版 `api` 用到新欄位，操作順序是：

1. 先跑 `db-migrate.yml`（新增欄位／表，走人工核准）；
2. 再讓 `deploy.yml` 部署使用該欄位的新版 `api`。

**新增欄位、新增表這類「擴張」操作，舊版 `api` 程式碼完全不受影響（用不到新欄位，不會出錯）**，所以①②兩步順序反過來也不會馬上壞，但仍建議先 migrate 後 deploy，養成習慣。

**破壞性變更（刪欄位、改型別）用展開—收縮模式（expand-contract）**，不能一次到位：

1. 先部署「不再使用舊欄位」的 `api`（展開期，新舊欄位並存）；
2. 觀察沒問題後，另開一個 migration 真的刪掉舊欄位（收縮期，走同一套核准關卡）。

⚠️ **Azure SQL Basic 層的自動備份（PITR）只保留 7 天**，這是唯一的救命索——`db-migrate.yml` 的 job summary 把**待套用的每一支 migration 的 SQL** 與 `idempotent.sql` 全文貼出來，讓核准者在按 Approve 前真的看得到要跑什麼 SQL，不是盲按（做法見下節）。

### 🔵 正式庫首次初始化（2026-10-01，`backend-engineer`）

**走哪條路：人工 SSH 到 VM 執行 `deploy/prod-db-init.sh`，不做成 `db-migrate.yml` 的首次模式。** 理由：

| 考量 | 說明 |
|---|---|
| 一次性、需要人在場 | 要手動輸入庫名確認，還要輸入第一個管理員的密碼。GitHub `workflow_dispatch` 的 inputs 會出現在 run 頁面與 log，**密碼不能走 input**；做成 secret 又等於把正式管理員密碼放進 GitHub |
| 與「核准關卡」的分工 | `production-db` Environment 的 Required reviewers 管的是**例行、可重複**的 migration（每次改綱要都要有人看過 SQL 再按）。首次建庫不是 migration，是創世：沒有「前一版」可比，核准者看不到有意義的 diff |
| 與 `db-migrate.yml` 的分工 | `db-migrate.yml`（已實作）只處理「已初始化的庫」的後續 migration，依賴 self-hosted runner 的 checkout 與已存在的歷史表；首次建庫不走它 |
| 防護在腳本本身 | 只對 `*.database.windows.net`、庫名必須是 `tcrfc_club`／`tcrfc_charity`、**目標庫完全沒有使用者物件才執行**、輸入庫名才動手 |

**`db-migrate.yml` 的責任（✅ 已兌現：`read_state` 檢查，不存在就拒絕並指向本腳本）**：開頭先檢查 `__EFMigrationsHistory` 存在；不存在就**拒絕並指向 `prod-db-init.sh`**（不要在空庫上跑 `dotnet ef database update` ——它會從空 `InitialBaseline` 開始、跑出一個缺表的資料庫）。

**順序（`init <club|charity>`，每個庫各跑一次、互不讀對方的設定檔）**

1. **建表**：`db/club-schema.sql`／`db/charity-schema.sql` **原樣**（含 Azure SQL 原生 `json` 型別，不做 `nvarchar(max)` 轉換——那是 `deploy/local-ddl.sh` 給本機 SQL Server 2022 的副本）。
2. **參照資料**：`db/prod/club-reference-data.sql`／`charity-reference-data.sql`（由 `db/seed/generate-prod-reference-sql.py` 自原種子產生器篩出，**不是**種子資料；清單與界線見 [`db/seed/README.md`](../db/seed/README.md)「正式庫的參照資料」）。
3. **`__EFMigrationsHistory`**：自 `apps/api/**/Migrations/*.Designer.cs` 的 `[Migration("…")]` 取出全部 ID 寫入，`ProductVersion` 取自 `ModelSnapshot`（目前 `10.0.0`，與 `Microsoft.EntityFrameworkCore` 套件版本一致）。🔴 **這一步放最後，因為它就是「初始化完成」的標記**：有歷史表＝已完成（`init` 與 `wipe-partial` 都拒絕再動）；沒有歷史表但有物件＝中途失敗的半成品，只能用 `wipe-partial` 清掉重來。
4. **驗證**：資料表／外鍵／視圖數（**自 DDL 去註解後計數**，不寫死：目前主站 **189／482／1**、慈善 **30／67／0**；STATUS S0-6b 當時的 144／380 與 29／65 已隨後來新增的表過期）、歷史筆數（主站 21、慈善 2；自 `Designer.cs` 動態計數，不寫死）、參照資料各表筆數（讀 SQL 檔頭的 `-- MANIFEST` 行）、中文編碼、沒有測試帳號、`clubs.domain` 不是佔位值。
5. **第一個管理員**：`create-admin <club|charity>`，互動輸入，詳見 `infra/README.md` §4.8。

**DDL 是否等於「所有 migration 套用後」？（2026-10-01 逐項比對）**

方法：用 EF 對**目前模型**產生 `GenerateCreateScript()`（`has-pending-model-changes` 已確認模型＝snapshot，所以這等於「所有 migration 套用後」），在兩個空庫分別建模型版與 DDL 版，比對 `sys.*` 目錄（欄位型別／長度／精度／可空／identity／計算欄位／定序、索引與鍵、外鍵含刪除動作、CHECK、視圖）。（EF 無法從空 `InitialBaseline` 重建綱要，所以不能「從零套 migration」比對。）

- **完全一致**：主站 189 表、1,879 欄、482 外鍵；慈善 30 表、322 欄、67 外鍵——欄位屬性、外鍵、慈善的索引**逐項相同**；migration 裡新增的每一個 CHECK 在 DDL 都有對應（見下，名稱可能不同）。
- **差異（EF 模型 ≠ DDL）——第 1、2 項已於 2026-10-01 裁決並對齊（migration `AlignIndexesWithDdl2`，依「DDL／`docs/12` 為準」與 STATUS B-12）**：
  1. ✅ **索引 4 項已對齊**：`IX_form_fields_i18n_locale`、`IX_sponsor_activations_i18n_locale`——`docs/12b` §11.2 寫「所有 `*_i18n` 建 `(locale)` 索引」，**文件有寫所以以文件為準**，**DDL 補上**這兩個索引（`db/club-schema.sql`）；前者 EF 本來就有、後者 EF 與 DDL 原本都漏了（EF 補在 `ClubDbContextIndexAlignment.cs`）；`UQ_form_fields_one_summary_per_form`（篩選唯一）與 `IX_registrations_trial_status`——文件沒寫、**以 DDL 為準**，**EF 模型補上**（`Data/ClubDbContextIndexAlignment.cs`）。`docs/12b` §11.1／§11.2 同步補列。
  2. ✅ **5 個 `UNIQUE (club_id, slug)` 的篩選條件已拿掉**：`charities`／`charity_programs`／`faqs`／`partner_stores`／`press_resources`，EF 模型的 `WHERE club_id IS NOT NULL` 以 `HasFilter(null)` 移除，與 DDL 的無篩選 `UNIQUE` 約束一致（B-12：SQL Server 唯一索引把 NULL 視為相等，共同內容的 slug 也要唯一）。
  - 🔴 **`AlignIndexesWithDdl2` 的 `Up()` 為什麼是「先查後做」的冪等 SQL，而不是 `CreateIndex`／`DropIndex`**：正式庫是 DDL 建的，那 5 個唯一鍵是**約束**，對它們 `DROP INDEX` 會失敗；DDL 建的庫上本來就符合目標狀態，所以 `Up()` 必須是無操作。只有「EF 模型建出來的庫」（本機實驗、舊開發庫）才會真的改動。`Down()` 刻意無操作（還原成舊 EF 狀態會讓正式庫偏離 DDL）。
  - ✅ **驗收（2026-10-01，SQL Server 2025 容器，用「正式庫的實際做法」）**：`db/club-schema.sql` 原樣建庫 → 手寫 `__EFMigrationsHistory`（前 20 支）→ `dotnet ef database update` 只套新的一支：**A．DDL 建的庫**：9 個相關索引的 `sys.indexes` 快照前後逐行相同（無操作）；**B．模擬 EF 建的庫**（5 個改成帶篩選的唯一索引、另 4 個索引刪掉）：套用後與 A 的 DDL 狀態逐行相同（唯一鍵由約束變成唯一索引，效果等價）。`has-pending-model-changes` 乾淨；`Probe` 空 `Up()／Down()`。
  3. **EF 不認識 DDL 的 default 與 CHECK**：111（主站）／27（慈善）個欄位的 `DEFAULT` 只在 DDL；CHECK 約束 DDL 有 97／29 個、EF 模型 3／0 個；視圖 `calendar_events` 只在 DDL。這些是 DDL 比 EF 嚴格，正常。
  4. **CHECK 名稱**：migration 用 `CK_<表>_<欄>` 命名，DDL 有不少是 SQL Server 自動命名（`CK__ad_campai__goal___51DA19CB`）。**未來若有 migration 要 `DROP CONSTRAINT <名稱>`，對正式庫會找不到**——寫法要改為先用目錄查名稱。
     - **影響評估（2026-10-01）**：①**既有 migration 不受影響**——`prod-db-init.sh` 把它們全標為已套用，不會在正式庫重跑，所以它們裡面的 `CK_*` 名稱不會撞到；②**只有「未來的 migration」會踩到**，形狀有三：(a) `DropCheckConstraint("CK_x")`／`DROP CONSTRAINT CK_x`——正式庫沒有這個名字，直接失敗；(b) `AlterColumn` 改型別或長度——欄位上若有 CHECK（不論叫什麼名字）SQL Server 會拒絕，要先拆掉再重建；(c) `AddCheckConstraint("CK_x")` 想**取代**舊條件——正式庫的舊 CHECK 名稱不同，舊的還在，兩條並存而不報錯，**新條件被舊條件擋住**（例如放寬值域卻被舊 CHECK 拒絕）。③**不必現在改**，但寫在下面「migration 注意事項」成為必守規則。
- **對首次初始化的影響**：沒有。差異都不影響「標記為已套用」，也不影響目前的查詢行為；但第 1、2 項會讓**下一支碰到這些索引的 migration** 與正式庫不一致，建議由系統分析師裁決「以哪邊為準」後補一支對齊的 migration（比照 `AlignIndexesWithDdl`）。
- **migration 裡沒有任何參照資料的 `INSERT`**（只有舊資料的 `UPDATE ... SET status`），所以「標記為已套用」不會漏掉該有的資料。

**json 型別（正式庫用原樣 DDL 的原生 `json`，本機 2022 與預設整合測試是 `nvarchar(max)`）——✅ 2026-10-01 已裁決並修正：選項 A（改程式，保留 `docs/17` §6 第 2 項原生 `json`）**

`deploy/local-ddl.sh` 的轉換只動 12 個欄位型別（主站 10：`page_blocks.content`、`page_versions.snapshot`、`articles_i18n.body`、`programs_i18n.content`、`sessions.weekly_schedule`、`partner_stores.business_hours`、`products.size_chart`、`charity_programs_i18n.content`、`push_messages.audience_team_codes`、`app_settings.setting_value`；慈善 2：`donation_payments.raw_response`、`donation_projects_i18n.description`）。原生 `json` **只接受 JSON 物件或陣列**（實測：`"abc"`、`123`、`true`、`null`、一般文字、`""` 全部被拒，`Msg 13609`；`[]`、`{...}`、`[1,2]` 可），`nvarchar(max)` 什麼都收，所以本機與測試看不到這類錯誤（`docs/18` `E-111`）。

- **前一輪演練**（SQL Server 2025 容器＋原樣 DDL＋整套測試）：1,016／1,019 通過，3 項失敗——2 項是營業時間寫成 JSON 字串純量（`partner_stores.business_hours`，特約店家建立回 500）、1 項是 `MembershipOrderTests` 依賴本機被手動改過的種子列（見下）。`prod-db-init.sh` 全流程、整份開發種子、`/readyz`、後台登入、`Microsoft.Data.SqlClient 7.1.0` 讀寫 json 欄位都正常。
- **修正內容**（12 個欄位逐一盤點，寫入端全部確認）：
  - **統一守門** `apps/api/Common/JsonColumn.cs`：`IsObjectOrArray`（根必須是物件或陣列）、`WrapText`／`UnwrapText`（自由文字 ⇄ `{"text":"…"}`，讀取相容舊的字串純量與非 JSON 純文字）、`CoerceToObject`（外部原始回應非物件時包成 `{"raw":"…"}`）。
  - **營業時間** `partner_stores.business_hours`：改存 `{"text":"週一至週五 11:00–21:00"}`；後台與前台、App 公開端點**對外仍是字串**（讀取端 `ReadHours` 解包，舊資料相容），**前端不需要改**。
  - **輸入驗證改成「必須是物件或陣列」並回 400**（中文訊息，不含「JSON」字樣）：`AdminInput.OptionalJson`（慈善項目 `charity_programs_i18n.content` 等）、`AdminProgramsRepository.ValidateContentJson`（課程 `programs_i18n.content` 與梯次 `sessions.weekly_schedule`）、`AdminShopProductsRepository.SerializeSizeChart`（`products.size_chart`）、`CharityProjectsAdminService.JsonText`（`donation_projects_i18n.description`）。
  - **已確認本來就安全**：`page_blocks.content`（`PageBlockContentProcessor` 要求 `JsonObject`）、`page_versions.snapshot`（程式組出物件）、`push_messages.audience_team_codes`（陣列）、`app_settings.setting_value`（`StoredMaintenance`／推播規則皆為物件）、`donation_payments.raw_response`（物件；另對金流回應加 `CoerceToObject` 保險，未來接真實金流不會因回應形狀中斷付款確認）。
  - **新聞內文 `articles_i18n.body`（比照營業時間，對外契約不變）**：後台目前送的是純文字（`apps/admin` 新聞編輯畫面是文字框），**寫入時純文字用 `JsonColumn.NormalizeTextOrStructured` 包成 `{"text":"…"}`（空白→`NULL`），已是物件或陣列的輸入原樣保存**（為日後區塊編輯器保留）；**讀取一律 `UnwrapText` 還原**：後台詳情（`AdminArticlesRepository.ToDetailDto`）、公開單篇端點（`ArticlesRepository`，`bodyJson` 欄位，App 共用同一端點）。其餘讀 `body` 的地方只有 `AdminSeoReportRepository` 的孤兒頁連結掃描（子字串比對，`WrapText` 用寬鬆編碼不把中文與 `&` 轉義，不受影響）；RSS／`llms.txt`／Schema 目前不讀內文。`apps/web` 目前**沒有任何頁面讀新聞 `bodyJson`**（grep 無命中），`apps/admin` 不用改。`ArticleBodyJsonColumnTests` 鎖定：純文字→資料庫為物件→後台與公開端點讀回原文字、物件輸入原樣、清空→`NULL`。
- **可重跑的原生 json 測試**：[`apps/api/scripts/native-json-test.sh`](../apps/api/scripts/native-json-test.sh)（`up`／`test`／`reset`／`down`，獨立容器與埠 14335，用**原樣 DDL**＋既有種子腳本；見 `apps/api/README.md`「原生 json 測試」）。`JsonColumnTests` 在 2022 與 2025 上行為一致。結果（2026-10-01，含新聞內文包裝後）：**2025 原生 json 1,046 項全綠；2022 本機 1,046 項全綠**（修正前同一套測試在 2025 上 1,016／1,019）。
- **CI 要不要改用 2025 映像檔（評估，未改 CI）**：**建議改**。`ci.yml` 的 `api` job 目前接一個用完即丟的 SQL Server 2022 容器、灌 `deploy/local-ddl.sh` 轉換後的 DDL；正式環境是原生 `json`，只要 CI 不跑原生 json，E-111 這一類錯就會重演。做法最小：把 `api` job 的映像檔換成 `mcr.microsoft.com/mssql/server:2025-latest`、**灌原樣 `db/*.sql`（不經 `local-ddl.sh`）**；本機實測 2025 容器相容性層級 160／170 都能建 `json` 欄位。代價：映像檔較大（拉取多約 30 秒）、2025 的行為若與 Azure SQL 有細微差異要另外留意（Azure SQL 才是最終真相）。若暫不改，至少把 `native-json-test.sh` 納入發版前檢查。**這是 `deployment-engineer` 的事，本輪未動。**
- ⚠️ 仍無法在本機驗證的前提：Azure SQL **資料庫相容性層級**（本機 2025 在 160 與 170 都通過，Azure 若停在更低層級才有差異）。建庫後請 `SELECT compatibility_level FROM sys.databases` 確認。
- ⚪ **`MembershipOrderTests.已付款但開通出錯…`（與 json 無關，已修）**：原本斷言種子的 tcrfc `single` 方案 `ends_on IS NULL`，那是既有開發庫被手動改過的結果（新建庫是 `2027-05-02`），在任何新建庫上必失敗。改成**測試自己準備狀態**：先快照方案原本的 `ends_on`、改成過去日期觸發開通失敗、`finally` 原樣還原。

### 🔵 正式庫的內容種子匯入與清除（2026-10-03，`deployment-engineer`；使用者決定）

**為什麼**：正式庫初始化後內容表全是 0 筆，前後台串接無法在正式機驗收（全專案只有本機與正式兩套，`docs/14`）。使用者決定把本機種子的**內容**匯入正式庫、驗收完清除。這是**暫時性的驗收資料**，不是上線資料。

**程序**：[`deploy/prod-seed-import.sh`](../deploy/prod-seed-import.sh)（在 VM 以 runner 執行；`source` 了 `prod-db-init.sh`，連線解析、`*.database.windows.net` 與庫名白名單、密碼只走環境變數、輸入庫名確認全部沿用，不另抄）。SQL 由 [`db/seed/generate-prod-content-sql.py`](../db/seed/generate-prod-content-sql.py) 自原種子產生器**按區段篩出**（`db/prod/club-content-seed.sql`、`charity-content-seed.sql`，納版控供審查；`--check` 可掛 CI）。

| 子命令 | 做什麼 |
|---|---|
| `preflight <club\|charity>` | 唯讀：庫已初始化、尚無標記、**擁有的表全空**、藍鯨簡介為空 |
| `import <club\|charity>` | 輸入「IMPORT 庫名」；整份 SQL 在**單一交易**（失敗整批回滾）；末尾寫資料庫延伸屬性 `tcrfc.seed_import`（庫名\|UTC 時間\|SQL sha256 前 12 碼）；匯入前後比對「帳號與假個資表」筆數必須不變；再跑 `verify` |
| `verify` / `status` | `verify`：標記、107／11 張表逐表筆數＝`*-content-manifest.tsv`、`clubs.domain` 仍等於 `.env` 現值、再呼叫 `prod-db-init.sh verify`（含「沒有測試帳號」）。`status`：只印現況與和匯入時的差異 |
| `clean` | 輸入「CLEAN 庫名」；單一交易，只動「擁有的表」，逐表整表刪除、被外鍵擋住就下一輪重試；最後還原藍鯨簡介、移除標記，並 `verify` |
| `record-manifest` | 只給本機演練：把演練庫匯入後的實際筆數與 SQL sha256 寫入 manifest |

**界線（每個種子區段必須明確歸類，新區段未歸類產生器就失敗）**：
- **匯入（內容）**：球隊／球季／賽事／場地／球員／教練／賽程賽果／積分榜／新聞與標籤／里程碑／夥伴／贊助與方案／提案草稿／頁面／輪播／FAQ／課程梯次／行事曆／301 轉址／SEO 與站台事實設定／慈善與社會影響／媒體專區／榮譽／試訓場次／會籍方案與權益／特約店家／漫畫／球迷會活動／商店設定與商品庫存／App 深連結版面功能開關版本／廣告版位與檔期；慈善庫：公益團體與計畫唯讀複本、店家、項目與金額選項、設定、信件範本。
- **參照資料**：已在正式庫，不重複插入（與 `generate-prod-reference-sql.py` 的 ALLOW 清單強制一致）。`clubs` 一列都不寫，`clubs.domain` 維持現值；只補藍鯨簡介。
- **絕不匯入（帳號）**：`admin_users`／`admin_user_roles`／`admin_user_clubs`。
- **預設不匯入（假個資／假交易）**：會員／會籍／會員卡／付款／球衣、試訓與活動報名、Lead、抽獎名單、電子報名單、訂單／出貨／退款、App 示範裝置／推播／診斷；慈善庫的捐款／金流／發票／結算／對帳／稽核／寄信紀錄與 `payment_channels`（sandbox 占位憑證）。
- `charity.donation_url` 的本機占位值改為 `https://<CHARITY_DOMAIN>/`（取自 VM `.env`）。

**清除的語意與限制**：
1. 標記放延伸屬性而非資料表（不影響 `verify` 的表數核對）；擁有權是**表層級**——匯入前擁有的表必須全空，所以匯入後表內每一列都算這次匯入的。
2. 驗收期間在這些表**手動新增的內容會一起被刪**（`clean` 會先列出筆數與匯入時不同的表）；子資料（`*_i18n`、賽事紀錄、圖片關聯）由外鍵 CASCADE 一併刪除。
3. 若**擁有的表以外**的資料仍參照這些列（例如前台真有人填的試訓報名參照了匯入的試訓場次），`clean` 整個中止、不刪任何東西，並指出是哪個外鍵；先處理那些資料再清（已於演練驗證）。
4. 本機演練（SQL Server 2022 轉換版 DDL 與 2025 原生 `json`＋原樣 DDL 各一次）：init→import→verify→clean→verify、重複匯入被拒、被外鍵擋住時回滾，皆通過。
5. 匯入走資料庫直寫，**不經 API 的 write-invalidate**：Redis 內快取的空列表要等 TTL 或手動清（`docker exec` 進 redis 容器用其環境變數 `redis-cli -a "$REDIS_PASSWORD" FLUSHALL`，密碼不經過命令列）；Cloudflare 若快取了前台 HTML 另清（`cd-purge-cache.sh` 的做法）。

**VM 上怎麼跑**（腳本還沒 push 前，用 DDL／migrations 與部署 commit 一致的暫存目錄；push 並部署後可改用 `~/tcrfc-src`）：以 `tar` 把 `deploy/prod-db-init.sh`、`deploy/prod-seed-import.sh` 與 `db/prod/*-content-*` 疊在 runner checkout 的 `db/`、`apps/api/**/Migrations` 之上。

```bash
sudo -iu runner
cd ~/seed-import-src            # 或 repo 根目錄
./deploy/prod-seed-import.sh preflight club && ./deploy/prod-seed-import.sh import club
./deploy/prod-seed-import.sh preflight charity && ./deploy/prod-seed-import.sh import charity
./deploy/prod-seed-import.sh verify club        # 隨時複查；status 看與匯入時的差異
# 驗收結束：
./deploy/prod-seed-import.sh clean club && ./deploy/prod-seed-import.sh clean charity
```

⚠️ 匯入內容含真人姓名（球員、教練，來源 JSON 已在版控）與【測試】標示資料；測試站目前無帳密、僅 `noindex`，驗收完請盡快 `clean`。

### 🔵 `db-migrate.yml` 實作（2026-10-02，`deployment-engineer`）

> 檔案：[`.github/workflows/db-migrate.yml`](../.github/workflows/db-migrate.yml)、[`deploy/db-migrate.sh`](../deploy/db-migrate.sh)（`generate`／`pending`／`apply` 三個子命令，邏輯全在腳本，workflow 只傳 `env:`）、
> [`deploy/test-db-migrate.sh`](../deploy/test-db-migrate.sh)（假 docker 模擬 sqlcmd 回應，`/bin/bash` 3.2，88 項斷言）。操作手冊：[`infra/README.md`](../infra/README.md)「資料庫 migration」。

**三個 job**

| job | 在哪跑 | environment | 做什麼 |
|---|---|---|---|
| `preview` | GitHub-hosted | —（無需核准） | `dotnet ef migrations script --idempotent`（會被執行的那一份）＋對每支 migration 各產一份單獨的 SQL（`script <前一支> <這一支>`，給人讀）＋ `SHA256SUMS`；upload artifact `migrate-sql`；SHA256SUMS 的雜湊以 job output 傳下去。`dry_run=false` 時先用 API 確認 `production-db` 存在、有 Required reviewers、Deployment branches 僅 `master`（見下「環境自動建立的陷阱」）。**不連資料庫** |
| `pending` | VM（`[self-hosted, tcrfc-vm]`） | `production`（僅 `master`，**無 reviewers**） | **唯讀**：連正式庫讀 `__EFMigrationsHistory`，與 checkout 內的 migration（以 `*.Designer.cs` 的 `[Migration("…")]` 為準）比對，把**待套用名單、名單雜湊與每支的 SQL** 寫進 job summary，輸出 `has_pending`／`<target>_hash`。這就是核准者要讀的頁面 |
| `apply` | VM | **`production-db`（Required reviewers）** | 只在 `dry_run=false` **且** `has_pending=true` 時存在；核准後**再比對一次**，名單雜湊與核准時看到的不同就拒絕；以 sqlcmd 容器執行 artifact 裡的 `idempotent.sql`；結束後驗證 `歷史表筆數＝repo 筆數、待套用 0` |

`pending`、`apply` 兩個 job 都與 `deploy.yml`／`rollback.yml` 共用 `concurrency: cd-production`（不可取消）。⚠️ `apply` 在**等核准期間**可能占住群組，把後面的部署排在後面：不打算核准就按 Reject 或取消那次 run。

**為什麼是「idempotent SQL ＋ sqlcmd 容器」，不是 efbundle**（任務允許兩者擇一）

1. **核准者讀到的就是會被執行的東西。** `apply` 執行的位元組＝`preview` 產出的位元組（`SHA256SUMS` 的雜湊經 job output 傳遞並逐檔核對）；`efbundle` 是不透明的二進位，審的 SQL 與執行的程式碼之間只能「相信 EF 做同一件事」。
2. **不新增任何要維護的東西**：不必在 VM 裝 .NET SDK（本來就不行）、不必改 api 映像檔（它是 aspnet 執行環境，沒有 SDK／`dotnet-ef`）、不必另建 bundle artifact；sqlcmd 容器與 `prod-db-init.sh` 同一個映像檔、同一種連線方式（env 檔 → `docker run -e NAME` 不帶值），已在正式庫實測過。
3. **冪等**：EF 的 `--idempotent` 為每支 migration 包 `IF NOT EXISTS (… __EFMigrationsHistory …)`，已套用的自動跳過；上次中途失敗後重跑是安全的。
- 代價：`idempotent.sql` 在 `preview` 時就定案，不知道資料庫現況；所以另有 `pending` 把「現況」補上。核准與套用之間若有人動了歷史表，名單雜湊對不上，`apply` 拒絕。

**交易與失敗行為（EF 預設，沒有被改）**：EF 對每支 migration 開一個 `BEGIN TRANSACTION … COMMIT`；`sqlcmd -b` 遇錯即中止並斷線，未提交的交易由 SQL Server 回滾。所以**失敗的那一支完全沒套用，在它之前已提交的維持已套用**，**不是**「整批要嘛全成要嘛全敗」。含 `suppressTransaction` 的操作（例如 `ALTER DATABASE`）不在交易內。**不自動重試、不自動回復**；summary 寫明失敗輸出、現況（歷史筆數與仍待套用名單）與下一步。救命索是 PITR（Basic 只有 **7 天**，還原成新庫，見 [`infra/README.md`](../infra/README.md)「資料庫 migration」與 `17` §6）。

**sqlcmd 的旗標（本機演練實測出來的）**：用 `-b -I`（`-I`＝`QUOTED_IDENTIFIER ON`，有篩選索引的 DDL 需要）。🔴 **不要加 `-f 65001`**：Linux 的 `mssql-tools` sqlcmd 不支援 `-f`（回 `Unknown Option`），預設已是 UTF-8；EF 產出的檔案開頭有 UTF-8 BOM，由 `generate` 移除（`E-116`）。

**防呆（`deploy/db-migrate.sh`）**

| 防呆 | 位置 |
|---|---|
| 正式模式只允許 `*.database.windows.net`；資料庫名必須等於目標（`club`→`tcrfc_club`、`charity`→`tcrfc_charity`），防止 club 的 migration 打進 charity | `load_connection` |
| 演練覆寫（`DBM_REHEARSAL`／`DBM_TRUST_CERT`／`DBM_EXPECT_DB_*`）只能一起用，且演練時拒絕連 Azure SQL | `target_defaults`、`load_connection` |
| 連線資訊只經環境變數進容器（`docker run -e NAME` 不帶值），**不印伺服器主機名稱**（公開 repo 的 log 人人可看）；密碼不進命令列、log、summary | `sqlcmd_container`、測試「機密不外洩」 |
| artifact 完整性：`SHA256SUMS` 的雜湊＝`preview` job output，每個檔案雜湊相符、沒有多出來的檔案；待套用的每一支都必須在 `idempotent.sql` 與 `per-migration/` 裡 | `verify_artifact`、`inspect_target` |
| 歷史表不存在（庫沒初始化）→ 拒絕並指向 `prod-db-init.sh`；歷史表有 repo 不認得的 migration（從舊 commit 觸發）→ 拒絕；歷史表筆數與讀到的 ID 數不符（輸出被截斷）→ 拒絕 | `read_state` |
| `DBM_DRY_RUN` 必須明確是 `true`／`false`；`target`、雜湊、路徑一律驗證格式 | `cmd_apply`、`target_list`、`require_abs_dir` |
| `both` 時兩個庫**先全部預檢通過才動手**，依序套用（club → charity），一個失敗就停，後面的不碰 | `cmd_apply` |

**🔴 環境自動建立的陷阱**：workflow 引用**不存在**的 environment 時，GitHub 會自動建立一個**沒有任何保護**的同名環境——核准關卡被靜默繞過。所以 `production-db` 必須**事先**建好（指令在 `infra/README.md`「資料庫 migration」）；`preview` job 在 `dry_run=false` 時以 `gh api repos/…/environments/production-db` 檢查存在、Required reviewers ≥ 1、Deployment branches 剛好只有 `master`，否則失敗。⚠️ 這個 API 呼叫用的是 `GITHUB_TOKEN`（`actions: read`），**第一次真的跑時要確認這一步沒有因權限而誤擋**（尚未在 GitHub 上實測）。

**驗證（2026-10-02）**：`/bin/bash deploy/test-db-migrate.sh` 88 項通過（假 docker 模擬 sqlcmd 的回應形狀：歷史表已存在且 pending 為零、有 pending、兩支 pending 第二支失敗、both 其一失敗、核准後名單變了、庫名不符、非 Azure 主機、artifact 被改、歷史比 repo 多、輸出被截斷、歷史表不存在、輸入驗證、密碼與主機不外洩；另對 `-I`、雜湊比對、庫名檢查各做一次變異測試，皆能讓測試變紅）；`shellcheck -S warning`、`actionlint` 通過。**另對本機 SQL Server 2022 容器的拋棄式資料庫、以真實 `mssql-tools` sqlcmd 容器與真實 `dotnet ef` 產出的 SQL 演練**：慈善庫 pending 1 → dry_run 不寫入 → 套用成功（資料表建立、歷史 2 筆）→ 再跑 pending 為零；手造一支中途出錯的腳本 → 交易回滾（半成品表不存在、歷史不變）、summary 寫明狀態與下一步；主站庫對「DDL 不存在」的空庫套用 `AlignIndexesWithDdl2` 如預期失敗並回報。**未實測**：對真實 Azure SQL 與真實 VM（本機 2022 沒有 `json` 型別，無法用 `db/club-schema.sql` 建主站演練庫）、GitHub 上的核准流程。

**上線後第一次驗證**：先跑 `dry_run=true`、`target=both`——**預期兩個庫「待套用 0 支」**（正式庫已由 `prod-db-init.sh` 寫入全部 migration：club 21 筆、charity 2 筆），summary 顯示「沒有待套用的 migration」、不出現核准關卡。這同時驗證了 runner 能讀 env 檔、容器能連庫、歷史表解析正常。

### 🔴 migration 注意事項（`AlignIndexesWithDdl2` 起生效）

1. **正式庫是 DDL 建的，不是 migration 長出來的**（`prod-db-init.sh` 把全部 migration 寫進歷史表）。**之後每支新 migration 的 `Up()` 都必須在「DDL 建的庫」上正確執行**——驗收要用這個形狀：`db/*.sql` 原樣建庫 → 手寫歷史表（除新 migration 外全部）→ `dotnet ef database update`（做法見 `AlignIndexesWithDdl2` 的驗收段）。只在 EF 模型建的庫上通過不算數。
2. **索引與約束的名稱與型態以 DDL 為準**：DDL 的唯一鍵多半是**約束**（`ALTER TABLE … ADD CONSTRAINT … UNIQUE`），`DROP INDEX` 對它無效，要用 `DROP CONSTRAINT`；寫成「先查 `sys.indexes`／`sys.key_constraints` 再決定」最穩。
3. **CHECK 約束不要用名稱**（見上「CHECK 名稱」影響評估）：要拆舊 CHECK 就用 `sys.check_constraints`（`parent_object_id`＋`parent_column_id` 或 `definition`）動態查出名稱再 `DROP`；要 `AlterColumn` 的欄位先查有沒有 CHECK 依賴它。
4. **寫入 json 欄位的程式一律只寫物件或陣列**（`docs/14`，`E-111`）；新增 json 欄位要在 `Common/JsonColumn.cs` 的守門之下，並在 `JsonColumnTests` 補測。
5. ✅ **`db-migrate.yml` 已實作（2026-10-02，見上節）**。正式庫之後要套用新 migration 走這條：手動觸發 → `pending` 唯讀比對 → `production-db` 核准 → `apply`。`AlignIndexesWithDdl2` 在正式庫的實際效果＝補上 `IX_form_fields_i18n_locale`、`IX_sponsor_activations_i18n_locale`（若 DDL 是補上前建的庫），其餘無操作，所以**不套用也不影響執行**，不是阻塞；**正式庫已於 2026-10-02 初始化，歷史表已含該筆（21 筆），第一次 `db-migrate` 預期待套用為零**。

### 🔴 新增 migration 的驗收：一定要跑一次「Probe」確認基準沒有偏移（`docs/18-work-errors.md` E-45）

**背景**：`InitialBaseline`（S0-7f）建立時只 commit 了 `.cs`／`.Designer.cs`，`ClubDbContextModelSnapshot.cs`
從沒進版控。少了 snapshot，`dotnet ef migrations add` 會拿**空模型**當比較基準，產出一個把整份綱要
重建一遍的 migration——而且這個問題**不會讓任何測試變紅**（既有測試接的是用 `db/*.sql` 直接建好的
資料庫，不經過 migration），純看 git 裡有沒有 `.cs` 檔完全看不出基準有沒有壞掉。之後兩次改綱要
（`AdminRefreshToken`／`matches.original_*`，S0-7j 修復前）都因此繞過 migration，改用手改 scaffold
檔＋手動 `ALTER TABLE`，讓「每次改動都是一個新 migration」這條路徑名存實亡。

**驗收動作（每次新增 migration 都要做，不是只在修 E-45 這次做）**：

```bash
cd apps/api
dotnet tool restore   # 本機工具清單 .config/dotnet-tools.json 釘住 dotnet-ef 版本，不依賴全域安裝
dotnet ef migrations add <暫名 Probe> --context ClubDbContext -o Data/Migrations
# 檢查產出的 <時間戳>_Probe.cs：Up()／Down() 必須是空的方法主體
# 空的才代表「目前的 Entity／OnModelCreatingPartial」與「snapshot 記的模型」完全一致，沒有基準偏移
dotnet ef migrations remove --context ClubDbContext
```

只看 git 裡有沒有 migration 檔不算驗證過——**要真的跑一次 `add`，看到空 `Up()/Down()`，再刪掉**，
這才是在驗證「基準成立」。`docs/12-database-schema.md` 若同時有異動，要先確認 `db/club-schema.sql`
與這裡產生的 migration 逐欄一致（型別、長度、NULL、預設值、索引、外鍵）——**不一致要回報給
系統分析師或使用者裁決，不要自己決定哪邊對**（S0-7j 就實際挖到一個這種落差：EF 的
`ForeignKeyIndexConvention` 會替沒有顯式設定索引的可為空外鍵欄位自動加一個非叢集索引，
`admin_refresh_tokens.replaced_by_id` 因此在 migration／snapshot 裡多出
`IX_admin_refresh_tokens_replaced_by_id`，但 `db/club-schema.sql` 沒有這個索引；掃過整個 snapshot
後發現同一個慣例還替既有 143 張表另外約 281 個外鍵欄位——`created_by` 85、`updated_by` 85、
`club_id` 33、其餘業務外鍵約 78——自動加了 `db/club-schema.sql` 沒有的索引）。

**S0-7l（2026-09-24）已裁決「依綱要為準」並全面修復**：`apps/api/Data/ClubDbContextCustomizations.cs`
的 `ConfigureConventions` 改成 `configurationBuilder.Conventions.Remove(typeof(ForeignKeyIndexConvention))`
——**整條移除**這個慣例，取代 S0-7k 一開始「繼承慣例、對單一屬性回傳 null」的子類別做法（例外只有
1 筆時子類別還算精準，例外多達 282 筆時本身就是另一種落差來源）。移除後用一支獨立腳本比對
「模型索引清單」與「`db/club-schema.sql` 解析出的索引／唯一鍵清單」：兩邊都是 210 筆、互相沒有
「只在一邊有」的項目。新增了一支 `AlignIndexesWithDdl` migration，`Up()`／`Down()` 比照
`InitialBaseline` 刻意清空（這 281 個索引從未真的建到任何資料庫，對它們下 `DropIndex` 會直接失敗）
——這支 migration 唯一的作用是讓 `ClubDbContextModelSnapshot.cs` 更新為正確模型。

**給下一個要新增外鍵欄位的人**：**不用再手動處理這個慣例**——它已經整條移除，EF 不會再替任何
新的外鍵欄位自動加索引。這表示：**外鍵要不要加索引，現在完全由 `db/club-schema.sql` 決定**——
`docs/12b` §11.2 要索引就在 DDL 用 `CREATE INDEX` 明確宣告（scaffold 會撈進來變成顯式
`HasIndex().HasDatabaseName(...)`），不宣告就是刻意不要，`dotnet ef migrations add` 產出的
migration 也不會多出非預期的 `CreateIndex`。**這不影響 EF 查詢行為**——索引只是儲存層 metadata，
不參與 LINQ 查詢轉換；拿掉這個慣例不會讓任何既有查詢變慢或變快，唯一影響的是「EF 認為資料庫
長什麼樣子」跟「`db/club-schema.sql` 實際長什麼樣子」是否一致。

**🔴🔴🔴 `dotnet ef migrations remove --force` 對一支「已標記為套用」的 migration 會真的執行
`Down()`，不是只刪檔案（S0-7k 實測踩到）**：上面的 Probe 流程本身安全（`add`／`remove` 一支
從沒套用過的 migration 不會碰資料庫）；但如果要移除的是一支 `__EFMigrationsHistory` 已經有紀錄
的既有 migration，`dotnet ef migrations remove` 預設會拒絕並提示先 revert；**加上 `--force` 之後，
它會直接對連線中的資料庫執行該 migration 的 `Down()`（真的跑 `ALTER TABLE ... DROP COLUMN`／
`DROP TABLE`），成功後才刪歷史紀錄與本機檔案**。S0-7k 實際在共用的本機開發庫（當時名為 `tcrfc_club_dev`，現為 `tcrfc_club`）上重現過：
對已套用的 `AddMatchOriginalSchedule` 用 `--force` 移除，直接把另一個 agent 正在用的
`matches.original_kickoff`／`original_match_on` 兩欄砍掉，發現後用重新 `add` 同名 migration
＋`dotnet ef database update` 補回去才復原。**下手前務必確認：這支 migration 的 `Down()`
會不會刪掉別人正在依賴的表或欄位；會的話，改成直接手改既有 migration／Designer／snapshot 三個
檔案（讓檔案內容對齊資料庫現況），不要用 `--force` 硬刪重建**——`tcrfc_club` 常有多個 agent
同時在用，這條路徑的風險不是理論上的。

### 🔵 慈善庫的 migration（CH-3，2026-10-01）

慈善庫有自己的 `CharityDbContext`（`apps/api/CharityPlatform/Data/`），**獨立的 migration 歷史**，流程同上，只是換 context 與輸出目錄：
`dotnet ef migrations add <名稱> --context CharityDbContext -o CharityPlatform/Data/Migrations --namespace Tcrfc.Api.CharityPlatform.Data.Migrations`，
`dotnet ef database update --context CharityDbContext`。⚠️ **陷阱**：`--namespace` 會讓 `CharityDbContextModelSnapshot.cs` 被放到 `apps/api/Tcrfc/Api/CharityPlatform/Data/Migrations/`（依命名空間推路徑，不是 `-o` 的目錄），要**手動移回** `CharityPlatform/Data/Migrations/` 並刪掉多出來的 `apps/api/Tcrfc/`，否則下一次 `add` 拿空模型當基準（E-45 同一種形狀）。
目前兩支：`InitialBaseline`（空 `Up`／`Down`，已套用基準）與 `AddAdminRefreshTokens`（`IF OBJECT_ID … IS NULL` 守衛的冪等 `Up`——`db/charity-schema.sql` 新建的庫已有這張表）。
`ci.yml` 另有一道同款健檢（`has-pending-model-changes --context CharityDbContext`）。**正式環境首次建庫**（本檔「首次建庫」清單第 8 項）改走 `deploy/prod-db-init.sh init charity`（見下節）：先跑 `db/charity-schema.sql`、灌參照資料，再**手寫** `__EFMigrationsHistory`（含 `InitialBaseline` 與 `AddAdminRefreshTokens` 兩筆）。~~原寫「用 `dotnet ef database update` 標記基準」~~ 已作廢：新 DDL 已含 `admin_refresh_tokens`，標記為已套用即可，不需要（也不應該）讓 EF 再執行一次 migration。

### 🔴 CI 防呆：`ci.yml` 的 `api` job 擋掉基準偏移進 PR

`.github/workflows/ci.yml` 的 `api` job 在 `dotnet build` 之後、`dotnet test` 之前加了一步
「EF Core migrations 基準健檢」，兩道檢查缺一都會讓 PR 變紅：

1. **存在性**：`apps/api/Data/Migrations/` 有任何 `*.Designer.cs` 卻找不到
   `ClubDbContextModelSnapshot.cs`——代表 snapshot 被刪掉或忘記 commit，直接擋下（這是 E-45
   最初的錯誤形狀，「檔案有沒有進 git」層級的防呆）。
2. **模型一致性**：`dotnet ef migrations has-pending-model-changes --context ClubDbContext`——
   比對目前程式碼的 Entity／`OnModelCreatingPartial` 與最後一個 migration＋snapshot 描述的模型，
   有差異就代表某次改了 Entity 卻忘記補 migration（`e67ef26`／S0-9l 那兩次的錯誤形狀）。**這道指令
   不需要真的連得上資料庫**（只比對記憶體中的模型物件），但建置 DbContext 仍需要
   `CLUB_SQL_CONNECTION_STRING` 這個設定鍵存在（否則 `Program.cs` 會在比對邏輯之前就丟例外），
   CI 沿用同一個測試用連線字串。

**驗收（用真的錯誤形狀測過會紅，兩種都測過）**：① 暫時刪掉 `ClubDbContextModelSnapshot.cs` →
存在性檢查失敗退出碼 1；復原後綠。② 在 `ClubDbContextCustomizations.cs` 的
`OnModelCreatingPartial` 暫時加一行 `modelBuilder.Entity<Article>().HasIndex(...)`（改模型但不加
migration）→ `has-pending-model-changes` 印出「Changes have been made to the model since the
last migration.」且退出碼 1；刪掉那一行、確認 `git diff` 乾淨後恢復綠。**這道 CI 步驟需要
`dotnet ef` 這個工具**——`apps/api/.config/dotnet-tools.json`（S0-7j 新增的本機工具清單）釘住
版本，CI 步驟本身先跑 `dotnet tool restore` 再呼叫，不假設 runner 上已經有全域安裝的
`dotnet-ef`。

---

> 🔴 **手改 scaffold 檔（`Data/EfEntities/*.cs`、`ClubDbContext.cs`）的換行陷阱**（`E-50` 連帶紀錄，第三次遇到）：這些檔案是 CRLF 和 LF **混雜**的，陳述式結尾是 `\r\n`，空行與 fluent chain 的續行是 `\n`。用一般編輯工具整檔改寫會把換行全部正規化，`git diff` 就會變成數千行的假差異。
> **做法**：以 `git show HEAD:<path>` 取出原始位元組，找一個唯一的錨點，在那裡做位元組級插入，不要重打任何既有的行。改完用 `git diff --stat` 確認只有新增的行數。

## 6. 失敗與回滾

| 環節 | 設計 |
|---|---|
| **映像檔 tag** | 有重建的映像檔用 **git SHA**（不可變），沒重建的沿用上一版標籤（每個映像檔各自一個 `TAG_*` 變數，§4a）。回滾＝重新指向舊標籤的映像檔，**不需要重新建置**（映像檔已經在 ghcr 上） |
| **部署後健康檢查** | ✅ 已實作（§4a）：容器全 healthy ＋ `api` `/readyz` 為 `ready`（兩個 `DbContext` 能連線、Redis 失敗只警告，呼應 `17` §4）＋ 六個網址經 VM 本機 `--resolve` 回 200（`curl -L` 跟隨轉址，前台 `/` 會 302 到 `/zh/`，E-115）；逾時 300 秒、每 5 秒重試 |
| **失敗自動回滾** | ✅ 健康檢查逾時或 `up` 失敗 → 腳本把五個映像檔標籤改回 `deploy-state.env` 記錄的上一個成功版本，`up -d --wait` 後再跑一次健康檢查；workflow 標記失敗（結束碼 1＝已退回、2＝退回也失敗）。**只退映像檔，不退 `deploy/`／compose 設定**。首次銜接的退路是本機 `:cd-prev` 標籤（§4a） |
| **資料庫 migration 失敗** | `db-migrate.yml` 不自動重試、不自動回復；失敗的那支已被交易回滾、先前已提交的維持已套用。summary 寫明現況與下一步；修正後走 PR → master → 重新觸發（冪等）。救命索是 PITR（Basic 7 天）。見 §5「`db-migrate.yml` 實作」。**`rollback.yml` 與 `deploy.yml` 都不碰資料庫**，退回映像檔不會退回結構（所以結構變更一律用展開—收縮，舊版 api 要能在新結構上跑） |
| **成功記錄** | ✅ 健康檢查通過後寫 `/opt/tcrfc/deploy-state.env`（`LAST_GOOD_SHA`、`LAST_GOOD_AT`、五個 `TAG_*`，不進 git；失敗不更新），並在 `/opt/tcrfc/deploy-history.log` 追加一行（時間、SHA、五個映像檔標籤、`mode`、`result=ok`） |
| **人工回滾** | ✅ `rollback.yml`（Actions → Rollback → Run workflow，輸入 40 字元 SHA）。標籤來源：① `deploy-history.log` 裡那一版的完整標籤組合（最準）；② 歷史沒有就用「ghcr 上存在 `:<sha>` 的映像檔用該標籤，其餘沿用目前」；兩者都找不到則中止。同樣 self-hosted、同樣健康檢查，失敗退回「回滾前」的版本，成功更新 state 並在 history 記一筆 `mode=rollback`。**不重建、不 migrate、不退 `deploy/` 設定** |
| **人工處理（自動退回也失敗，結束碼 2）** | VM 狀態不確定：在 VM 上 `cd /opt/tcrfc/actions-runner/_work/tcrfc/tcrfc`，`docker compose --env-file /opt/tcrfc/.env ps` 與 `logs <服務>` 看哪個不健康；確認 `cat /opt/tcrfc/deploy-state.env` 的上一版標籤，手動 `TAG_API=<標籤> … docker compose --env-file /opt/tcrfc/.env up -d --pull never`（五個 `TAG_*` 依 state）。**不要刪 `/opt/tcrfc/data-protection`** |
| **通知** | 失敗（部署失敗、健康檢查失敗、自動回滾發生）都要通知——用什麼管道（Email／LINE Notify／Slack）**待使用者選**；目前失敗會讓 workflow 紅燈（GitHub 預設寄信給 repo 擁有者）並在 job summary 寫明原因與是否已退回，`deploy.yml` 留有註解佔位 |

---

## 7. Secrets 清單

**設計原則：能在 VM 本機解決的機密，不進 GitHub Secrets。** self-hosted runner 讓部署動作發生在 VM 上，資料庫連線字串、LINE Pay 憑證這類「跑起來的應用程式要用、但 GitHub 端的 workflow 步驟本身不需要看到」的機密，**直接放 VM 的 `.env` 檔即可，從未離開過 VM，公開 repo 的 Secrets 外洩風險與它們無關。**

### 7.1 GitHub Secrets（GitHub 端步驟真的要用到的）

| Secret | 用途 | 誰用 |
|---|---|---|
| `CLOUDFLARE_API_TOKEN`（**選配**，✅ 腳本已支援；沒設就略過清快取） | 部署後清快取，僅 `Zone.Cache Purge` 權限，限定官網／藍鯨／慈善三個 zone（暫用網域期間只有 `4webdemo.com` 一個 zone） | `deploy.yml`（`deploy/cd-purge-cache.sh`） |
| （選用）`NOTIFY_WEBHOOK_URL` | 部署失敗／回滾通知 | 所有 workflow 的失敗通知 step |

> `ghcr.io` 推送用內建 `secrets.GITHUB_TOKEN`（`permissions: packages: write`），**不另外開 PAT**。
> self-hosted runner 註冊 token 是一次性的，**不是常駐 secret**，註冊完即棄用。
> Cloudflare zone ID（tcrfc／藍鯨／慈善）不是機密，放 **Actions Variables**（`CF_ZONE_ID_TCRFC`／`CF_ZONE_ID_BW`／`CF_ZONE_ID_CHARITY`）不放 Secrets。

### 7.2 VM 本機 `.env`（不進 GitHub，任何形式都不進 git）

> 存放路徑建議 `/opt/tcrfc/secrets/`，**在 `actions/checkout` 的工作目錄之外**（checkout 目錄每次 run 可能被清乾淨，機密檔必須是獨立、持久的路徑），`docker-compose.yml` 用絕對路徑的 `env_file:` 引用。檔案權限 `600`，擁有者是 runner 的服務帳號。
> ⚠️ **俱樂部與協會的憑證分屬不同主體**（`17` §5），即使同放一台 VM，**建議實體上拆成兩個檔案**（`club.env`／`charity.env`），存取與輪替各自由各自的持有人負責，不要混在一個檔案裡——這是治理上的區隔，不只是技術上的。

> 🔵 **2026-10-01 起以 [`infra/README.md`](../infra/README.md) §4.3「設定鍵盤點」為準**（依 `apps/api` 程式逐鍵核對：必填／選填／預設、歸屬哪個檔、外部憑證未到位時該填什麼）。機密檔由 [`infra/provision-secrets.sh`](../infra/provision-secrets.sh) 產生並寫入 VM。下表只留「持有人」這個治理資訊與重點：

| 鍵 | 檔案 | 持有人 | 重點 |
|---|---|---|---|
| `CLUB_SQL_CONNECTION_STRING`、`AZURE_BLOB_CONNECTION_STRING`、`JWT_SIGNING_KEY_CLUB`、`JWT_SIGNING_KEY_MEMBER`（≥32 字元，兩者不同值） | `club.env` | 俱樂部（系統管理） | 必填；缺值或過短啟動失敗 |
| `CHARITY_SQL_CONNECTION_STRING`、`AZURE_BLOB_CONNECTION_STRING_CHARITY`、`JWT_SIGNING_KEY_CHARITY` | `charity.env` | **協會**（不得與俱樂部共用任何值） | `CHARITY_SQL_CONNECTION_STRING` 漏設＝慈善平台無聲關閉 |
| `REDIS_PASSWORD`、六個 `*_DOMAIN`、`SITE_ENV`、`CADDYFILE`、`ACME_EMAIL`、`GHCR_OWNER`、`IMAGE_TAG` | `/opt/tcrfc/.env`（compose 用，暫定位置，見 §9a） | 俱樂部（系統管理） | `REDIS_PASSWORD` 不要再寫進 club.env |
| `DATA_PROTECTION_KEYS_PATH` | compose 固定值（非機密）＋ VM `/opt/tcrfc/data-protection` bind mount | 俱樂部（系統管理） | 🔴 金鑰環，遺失＝已加密資料永久無法解密 |
| 選用且**正式環境未到位時不設**：`PAYMENT_GATEWAY`／`INVOICE_ISSUER`（設 `fake` 會讓 Production 啟動失敗）、`EMAIL_SENDER`、`LINE_LOGIN_*`、`GEOCODER`＋`GOOGLE_MAPS_GEOCODING_API_KEY`（Google 金鑰，限 Geocoding API＋VM 出口 IP；`GEOCODER=fake` 同樣會讓 Production 啟動失敗）、`CHARITY_ALLOW_FAKE_PROVIDERS`、`TURNSTILE_SECRET_KEY_CHARITY`、`MEMBERSHIP_ACTIVATE_CREDENTIAL`、`AZURE_BLOB_PUBLIC_BASE_URL(_CHARITY)` | 對應檔案 | 俱樂部／協會 | 各鍵的未設行為見 `infra/README.md` §4.3 |
| 🔵 **程式不讀**：`LINE_PAY_*`、`INVOICE_SERVICE_API_KEY_*`、`APNS_KEY_ID`／`.p8`／`FCM_SERVICE_ACCOUNT_JSON` | — | — | 商店與慈善的金流／發票憑證存在資料庫（後台設定頁，Data Protection 加密）；APNs／FCM 傳輸尚未實作。**舊表列的這些名稱是規劃階段示意，勿照填** |


---

## 8. 本檔不決定的事

- **`nuxt-club` 品牌切換的實作細節**（CSS 變數怎麼在 runtime 換、favicon 怎麼依 host 選）——部署層假設是「一個映像檔、環境變數切換」，實作交給 `frontend-architect`；技術上真的做不到才退回兩個映像檔（見 §3）
- **失敗通知的實際管道**（Email／LINE Notify／Slack）——workflow 先留 webhook 佔位
- **`apps/*` 目錄結構的確切命名**——本檔用的路徑是規劃慣例，實際建專案骨架時可調整（S0-7a／S0-9）
- ~~慈善平台圖片儲存體是否需要獨立 Storage Account~~ ✅ **2026-10-01 已決定獨立**（見 §7.2、`17` §13）
- **`master` 是否強制 PR review**——目前單人開發非強制，建議但不列為硬性關卡（DB migration 的關卡已經是硬性的）
- **是否要做零停機部署（藍綠）**——現在的秒級空窗被判定可接受；量到使用者有感再升級，升級成本是雙倍容器資源
- ~~db-migrate.yml 的自動觸發時機~~ ✅ **2026-10-02 定案：完全手動 `workflow_dispatch`**（不偵測 `Migrations/**` 自動起草）。理由：套用時機要由人決定（先 migrate 後 deploy 的順序、離峰、PITR 只有 7 天），自動起草會多出一個「有 run 掛著等核准、占住 `cd-production` 群組」的狀態
- **App 端（`tcrfc-app-ios`／`tcrfc-app-android`）的 CI**——已在 `19-app-tech-stack.md` §9 定案，與本檔的 self-hosted runner 無關，**App CI 不得共用這台 VM 的 runner**（macOS/Android 建置資源需求不同，也不該讓行動端建置佔用部署用的 runner）

---

## 9a. 實作進度（S0-7c，2026-09-22）

> 對應 STATUS.md S0-7c。本節記錄「本檔設計的東西，實際落地到哪一步」，設計本身沒有變，
> 只是把 §0–§8 的規劃兌現成檔案；發現需要偏離設計之處，都記在下面對應小節。

### 已建立的檔案

| 檔案 | 對應本檔哪一節 | 狀態 |
|---|---|---|
| [`.node-version`](../.node-version) | §3「快取」的 node-version 交叉參照 | ✅ 單一事實來源，見 [`17-deployment.md` §12](17-deployment.md#12-前端建置用的-nodejs-版本) |
| [`scripts/check-node-version.mjs`](../scripts/check-node-version.mjs) | 同上 | ✅ 掛進四個 `package.json` 的 `lint`，S0-9h |
| [`.github/workflows/ci.yml`](../.github/workflows/ci.yml) | §1「觸發與分支」的 `ci.yml` | ✅ `pull_request` → 五個應用的 lint／build／docker build（`push: false`）／dotnet test |
| `ci.yml` 的 `shared-contract` job、[`shared/scripts/`](../shared/scripts/) | §3「`shared-contract`」 | ✅ 2026-10-02（AP-8）：重新產生 `shared/` 後 `git diff --exit-code`；已用 actionlint 檢查 |
| [`.github/workflows/_node-app.yml`](../.github/workflows/_node-app.yml) | 同上（內部用） | ✅ 四個 Node 應用共用的可重用 workflow（`push: false` 版） |
| [`.github/workflows/_node-app-deploy.yml`](../.github/workflows/_node-app-deploy.yml) | §1／§2「映像檔要放哪裡」 | ✅ 四個 Node 應用共用的可重用 workflow（`push: true` 版，供 `deploy.yml` 呼叫） |
| [`.github/workflows/deploy.yml`](../.github/workflows/deploy.yml) | §1「觸發與分支」的 `deploy.yml` | ✅ build＋push 段（S0-7c）＋ **部署 job（2026-10-02 啟用，§4a）** |
| [`.github/workflows/rollback.yml`](../.github/workflows/rollback.yml) | §6「人工回滾」 | ✅ 2026-10-02 |
| [`deploy/cd-deploy.sh`](../deploy/cd-deploy.sh)、[`deploy/cd-purge-cache.sh`](../deploy/cd-purge-cache.sh) | §4a | ✅ 2026-10-02：部署／回滾／健康檢查／state 與 history；清 Cloudflare 快取（選配） |
| [`deploy/test-cd.sh`](../deploy/test-cd.sh) | §4a | ✅ 以假 docker／curl／sha256sum 在 `/bin/bash` 3.2 跑 60 項斷言（首次銜接、只換 api、只改設定、proxy 重建、失敗→退回成功、退回也失敗＝結束碼 2、前置檢查失敗、人工回滾兩種標籤來源、鎖、清快取三種情境） |
| [`.github/actionlint.yaml`](../.github/actionlint.yaml) | — | `tcrfc-vm` 自訂 runner label，讓 `actionlint .github/workflows/*.yml` 通過 |

| [`.github/workflows/db-migrate.yml`](../.github/workflows/db-migrate.yml)、[`deploy/db-migrate.sh`](../deploy/db-migrate.sh) | §5「`db-migrate.yml` 實作」 | ✅ 2026-10-02：預覽 → VM 唯讀比對 → `production-db` 核准 → 套用 |
| [`deploy/test-db-migrate.sh`](../deploy/test-db-migrate.sh) | 同上 | ✅ 以假 docker 在 `/bin/bash` 3.2 跑 88 項斷言；另以本機 SQL Server 2022 ＋真實 sqlcmd 容器演練過 |

**尚未建立**：無（`rollback.yml`、`db-migrate.yml` 均已建立）。

### CI 段的設計決定（比 §0–§8 原文多出的細節）

1. **`apps/api` 的 `dotnet test` 真的接一個用完即丟的 SQL Server 容器，不引入「沒資料庫就略過」
   的語意**——回答了 `apps/api/README.md`「測試」一節留給接 CI 的人的那句話（「若之後接上 CI 且
   CI 固定會提供資料庫，這個決定可以重新評估」）。用 GitHub Actions 原生 `services:`（給
   `--name mssql-ci` 讓它有可預期的 docker container 名稱），**不是另外寫一份
   `docker-compose.ci.yml`**——效果與 §1「CI 當 staging」設想的一致（用完即丟、零常駐成本），
   但少維護一份 compose 檔；`deploy/local-ddl.sh --apply`／`db/seed/apply-seed.sh`／
   `db/seed/apply-charity-seed.sh` 三支腳本本來就用 `LOCAL_MSSQL_CONTAINER` 環境變數指定目標
   容器名稱（本機開發指向既有的 `sqlserver` 容器，CI 這裡換成指向 `mssql-ci`），**同一套腳本
   兩種場景直接沿用，沒有另外寫一份 CI 專用的建庫邏輯**。
   ✅ **已實測**（2026-09-22，本機用一個獨立於既有 `sqlserver` 容器之外的臨時容器驗證整條路徑：
   `local-ddl.sh --apply` → 兩支種子腳本 → `dotnet test apps/api/Tcrfc.Api.Tests`，Debug 組態
   下 78/78 全綠）。⚠️ **Release 組態的同一輪驗證跑到一半，`apps/api` 因為另一個 agent同時在改
   上傳端點（`Program.cs` 參照到尚未建立的 `UnavailableImageStorageService`）而編譯失敗**——
   這是暫時性的、與本次 CI/CD 任務無關的併發編輯狀態，不是 workflow 設計的問題；`ci.yml` 的
   `api` job 語法已用 `actionlint` 驗證過，實際跑動需要等 `apps/api` 那頭的變更完成或合併。
   🔴 **2026-09-30 更新（使用者裁決，取代 2026-09-25 `S0-13` 的獨立測試庫）**：本機與 CI 的資料庫
   名稱對齊為 `tcrfc_club`（網站）／`tcrfc_charity`（慈善），`dotnet test` 直接連 `tcrfc_club`，
   不再有 `tcrfc_club_test`。`ci.yml` 的 `api` job：`./deploy/local-ddl.sh --apply` 建兩庫 →
   `./db/seed/setup-club-db.sh --recreate` 把 `tcrfc_club` 重建成乾淨的 DDL＋種子（容器指向
   `mssql-ci`）→ `./db/seed/apply-charity-seed.sh` → 兩處 `CLUB_SQL_CONNECTION_STRING` 皆
   `Database=tcrfc_club`，**本機與 CI 用同一批腳本**。CI 的 SQL Server 是用完即丟的容器，
   同名不會與本機互相干擾；**正式環境 Azure SQL 的資料庫名稱與本機相同（`tcrfc_club`／`tcrfc_charity`，2026-10-01 起，原稱 `sqldb-*`）**，
   部署 workflow 沒有引用任何本機庫名。原因與代價見 `docs/14-invariants.md` `S0-13` 一條。
2. **兩個測試 fixture（`RedisEnabledApiFixture`／`AdminWriteRedisEnabledApiFixture`）需要真正的
   `redis-server` 執行檔**——`ci.yml` 的 `api` job 加一步 `command -v redis-server || apt-get
   install -y redis-server`，不假設 `ubuntu-latest` 一定內建。
3. **`api` job 的 SA 密碼直接寫在 workflow 裡（`Ci_Throwaway_Str0ng!24`），沒有進 GitHub
   Secrets**——它只活在這個 job 專屬、跑完即銷毀的服務容器裡，不是任何環境的真實憑證，跟
   §7「Secrets 存放原則」的精神一致（能在「用完即丟的地方」解決的機密不需要進 Secrets），
   只是這裡「用完即丟的地方」換成「這個 job」而不是「VM」。
4. **四個 Node 應用抽成兩份可重用 workflow（`_node-app.yml`／`_node-app-deploy.yml`）**，
   `ci.yml`／`deploy.yml` 分別呼叫四次——避免同一段 lint／docker build 步驟複製四份。
   🔴 **刻意不用 `strategy.matrix` 讓四個應用共用一個 job**：GitHub 官方文件裡
   `jobs.<job_id>.if` 可用的 context 清單不含 `matrix`（`matrix` context 只保證在 steps 層級的
   `if:` 可用），若寫成 `if: matrix.app_dir == 'apps/web'` 這種 job 層級條件式，**語法上會通過
   actionlint，但語意上不保證讀得到值**——這類「看起來對、實際上賭一個未保證行為」的寫法本身
   就是本專案要避免的那種「防護的實際效力與它給人的信心不相稱」（[`18-work-errors.md`](18-work-errors.md)
   E-34／E-35 那一族教訓的同一個精神，只是換了個技術細節）。改成四個各自獨立的 job／可重用
   workflow 呼叫，每個都用 `needs.changes.outputs.X`（job 層級保證可用的 context）判斷。
5. **`dorny/paths-filter@v3`** 偵測變動範圍，五個應用一對一輸出布林值，對應 §3「五個映像檔」；
   `ci.yml`／`deploy.yml` 各自維護一份幾乎相同的 `changes` job（沒有抽成第三份可重用
   workflow）——兩邊觸發的事件與下游動作不同（`ci.yml` 不需要 `deploy_config` 這個輸出），
   抽出去要多傳一個「要不要這個輸出」的參數，判斷是不值得為了省 20 行再多一層間接。

### CD 段（2026-10-02 已實作）——還剩什麼

已兌現：拿掉 `if: false`、`runs-on: [self-hosted, tcrfc-vm]`、`environment: production`、不可取消的 `concurrency`、
部署目錄（runner 的 checkout，`tcrfc-src` 退役）、`--env-file /opt/tcrfc/.env`、健康檢查、失敗自動回滾、`deploy-state.env`、
Cloudflare 清快取（選配）、`rollback.yml`。細節與取捨全在 **§4a**。

**還沒做／待使用者**：

1. **第一次 push 後的觀察**（§9 第 10 項首次演練）：會有一次整站約 1–2 分鐘中斷（專案目錄換路徑，八個容器全部重建）；見 `infra/README.md` §6「日常部署」。
2. **`tcrfc-src` 的清除**：首次 CD 成功後使用者在 VM 上 `rm -rf /home/runner/tcrfc-src`。
3. **Cloudflare 清快取 token**（選配）：建立後存 secret `CLOUDFLARE_API_TOKEN`＋三個 Actions Variables（§4a）；沒建立不影響部署。
4. **失敗通知管道**（§8）。
5. **失敗路徑尚未在真實 VM 演練**：`test-cd.sh` 以假 docker 涵蓋退回路徑，真實 VM 上的第一次失敗要靠觀察（job summary 會寫原因與是否已退回）；成功路徑與 `rollback.yml` 建議在首次 CD 成功後立刻各跑一次（§9 第 10 項）。
6. ✅ **`db-migrate.yml`**：2026-10-02 已實作（§5「`db-migrate.yml` 實作」）。**待使用者建立 `production-db` 環境**（`infra/README.md`「資料庫 migration」的 `gh api` 指令）並跑一次 `dry_run=true` 驗證。
7. **§4 防護鏈第 0 條**：✅ 已確認（2026-10-02 `gh api` 唯讀查詢：Fork PR 核准政策為 `all_external_contributors`；Environment `production` 的 Deployment branches 為自訂分支策略）。

---

## 9. 開通 Azure 後要補的設定清單

> 這份是「Azure 資源一開通，回來把這些填一填就能跑」的清單，對應 `17` §9 的驗證程序，本檔只列**跟 CI/CD 直接相關**的部分。

| # | 項目 | 動作 |
|---|---|---|
| 1 | **VM 建好、Docker 裝好** | 在 VM 上安裝 GitHub Actions self-hosted runner（`./config.sh` 用一次性註冊 token，設定 label `tcrfc-vm`），設成 systemd 服務常駐 |
| 2 | **VM 上建 `.env` 檔** | 🔵 執行 [`infra/provision-secrets.sh`](../infra/provision-secrets.sh)（建 `club.env`／`charity.env`／`/opt/tcrfc/.env`／金鑰環目錄並驗證，見 `infra/README.md` §4.3） |
| 3 | **VM 本機建 `deploy-state.env`** | ✅ 空檔即可（cloud-init 已建），首次 CD 部署成功後由 `deploy/cd-deploy.sh` 寫入；同時建立 `deploy-history.log`（runner 擁有 `/opt/tcrfc`，腳本自行建立） |
| 4 | **NSG** | **確認 CI/CD 不需要新增任何 inbound 規則**——這是方案 B 的重點驗證項，回頭核對 `17` §9 驗證 1–3 不受影響 |
| 5 | **ghcr 套件建立** | 第一次 `deploy.yml` 跑完會自動建立五個套件；手動把它們的 visibility 設為 **Public**（新套件預設常常是 private，要手動切） |
| 6 | **GitHub Environments** | 建立 `production`（Deployment branches：僅 `master`；✅ 已建）與 `production-db`（同上 ＋ Required reviewers，至少 1 人；指令見 `infra/README.md`「資料庫 migration」）。🔴 `production-db` 要**先建**再跑 `db-migrate.yml`，否則 GitHub 會自動建立無保護的同名環境 |
| 7 | **Cloudflare API Token**（選配） | 建立僅 `Zone.Cache Purge` 權限、限定對應 zone 的 token，存進 GitHub Secret `CLOUDFLARE_API_TOKEN`，並設 Actions Variables `CF_ZONE_ID_TCRFC`／`CF_ZONE_ID_BW`／`CF_ZONE_ID_CHARITY`。**沒設就略過清快取，不影響部署**（§4a） |
| 8 | **首次建庫** | 🔵 在 VM 上以 runner 使用者執行 [`deploy/prod-db-init.sh`](../deploy/prod-db-init.sh)（`init`／`create-admin`／`verify`，步驟見 [`infra/README.md`](../infra/README.md) §4.8；設計見本檔 §5「正式庫首次初始化」）。EF 基準 migration 早已建立，這步只負責把它們寫進 `__EFMigrationsHistory` |
| 9 | **LINE Pay 出口 IP 驗證** | 依 `17` §9 驗證 1，**這步驟獨立於 CI/CD，部署管線建好後跑一次即可**，之後除非換 VM 不必重跑 |
| 10 | **首次部署演練** | 先在**非 LINE Pay 正式串接前**（即 §3.6 商店結帳上線前）完整跑一次 push → build → deploy → 健康檢查 → （刻意製造一次失敗）驗證自動回滾真的會動作 |

---

## 10. 基礎設施部署 workflow（`infra.yml`，2026-10-01）

> 🔵 **與本檔其餘部分的 workflow 是兩條不同的線**：`ci.yml`／`deploy.yml` 處理**應用程式**（build → ghcr → VM 上的 self-hosted runner 部署）；
> `infra.yml` 處理 **Azure 資源本身**（Bicep），**跑在 GitHub-hosted runner**——它要建出 VM，所以不能依賴 VM 上的 runner。
> 資源清單與決定見 [`17`](17-deployment.md) §13，操作手冊見 [`../infra/README.md`](../infra/README.md)。

| 項目 | 設計 |
|---|---|
| 觸發 | push `master` 且 `infra/**`（或兩支 infra workflow 檔）有變動；`workflow_dispatch`（可勾「只跑 what-if」）。**沒有 `pull_request`／`pull_request_target`** |
| PR 檢查 | `infra-validate.yml`（`pull_request`，只讀、無 secrets、無 `id-token`）：`az bicep lint`／`build`／`build-params`。同一支以 `workflow_call` 被 `infra.yml` 先行呼叫 |
| 步驟 | validate → 檢查必要設定都已填 → `azure/login`（OIDC）→ `what-if`（輸出貼進 job summary）→ `deploy`（`--mode Incremental`，**絕不用 Complete**） |
| 執行環境 | GitHub-hosted `ubuntu-latest`；**不使用 self-hosted runner**，與 §4 完全分開 |
| 登入 | **OIDC（federated credential），不存 client secret**。部署身分是 user-assigned managed identity `id-tcrfc-deploy`，federated credential 的 subject 綁 `repo:waiting0201@5709750/tcrfc@1334739698:environment:production` |
| 授權範圍 | 只有資源群組 `rg-tcrfc-prod`：Contributor ＋ 只含 `Microsoft.Authorization/locks/*` 的自訂角色（建 `CanNotDelete` 鎖用）。沒有任何訂閱層級權限 |
| Environment | 沿用 `production`（Deployment branches 僅 `master`）。`id-token: write` 只開在 deploy job |
| Secrets／Variables | secrets：`SQL_ADMIN_PASSWORD`、`SSH_ALLOWED_CIDR`、`ALERT_EMAIL`、（可選）`ENTRA_ADMIN_OBJECT_ID`；variables：`AZURE_CLIENT_ID`／`AZURE_TENANT_ID`／`AZURE_SUBSCRIPTION_ID`、`SSH_PUBLIC_KEY`、（可選）`ENTRA_ADMIN_LOGIN`。完整表格見 `infra/README.md` §3 步驟 5c |
| 防護鏈（公開 repo） | 與 §4 同一套：**第 0 條（Fork PR 需核准）是地基**；federated credential 綁 Environment ＋ Environment 僅 `master` → 非 `master` 的 run 拿不到 Azure token；workflow 內另有 `if: github.ref == 'refs/heads/master'`。**SSH 來源 IP、Email 一律放 secret（log 中會被遮蔽）；部署輸出不印到 log**（公開 repo 的 log 人人可看） |
| 與 §7.1 的關係 | 這些 secrets／variables 只給 `infra.yml` 用；應用程式的機密仍遵守 §7「能在 VM 本機解決的不進 GitHub」 |
| 鎖的效果 | Public IP、SQL 伺服器、兩個儲存體帳戶加 `CanNotDelete`。防的是人為誤刪；部署身分本身能管鎖，所以**防不了被入侵的管線**——靠上列防護鏈控管 |

