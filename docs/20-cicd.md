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
> 🔴 **Azure 資源目前一個都還沒開**（STATUS S0-6／S0-7 暫緩）。本檔設計成**開通前就能把 workflow 寫好、
> 開通後只填 IP 與 secrets 就能跑**——凡是依賴實際 Azure 資源的步驟都標了「待補」，收在 §9。
> 🔴 **這個 repo 是公開的**（`waiting0201/tcrfc`），所有設計以此為最高前提。
>
> ✅ **CI 段已實作**（S0-7c，2026-09-22）：`.github/workflows/` 已有 `ci.yml`／`deploy.yml`（含兩份
> 內部可重用 workflow）、`.node-version`／`scripts/check-node-version.mjs`（S0-9h 版本防漂移）。
> **CD（部署到正式 VM）段仍是 §9 那張表——workflow 檔裡的部署 job 目前 `if: false` 停用**，
> 細節見 **§9a「實作進度」**。

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
| **DB 遷移關卡** | **EF Core Migrations**，套用一律走獨立、需人工核准的 GitHub Environment（`production-db`，Required reviewers），**與例行部署完全脫鉤** | 「push 就自動改 prod 的資料庫結構」對 2 GB 硬上限、單機無備援的架構風險太高 |
| **失敗與回滾** | 映像檔一律以 **git SHA** 為 tag；部署後跑健康檢查，失敗自動退回上一個 SHA；人工回滾＝重跑 `workflow_dispatch` 指定舊 SHA | 不需要 blue-green，只要「有上一版可以退」 |
| **Secrets 存放原則** | **能在 VM 本機解決的，不進 GitHub Secrets**——資料庫連線字串、LINE Pay 憑證、Redis 密碼全部放 VM 上的 `.env` 檔，只有「GitHub-hosted 步驟自己要用」的東西（如 Cloudflare 快取清除 token）才是 GitHub Secret | self-hosted runner 讓部署發生在 VM 本機，大部分機密**從來不需要離開 VM**——這是對公開 repo 最重要的降險設計 |
| **部署後動作** | 健康檢查通過才清 Cloudflare 快取（依變動的站台選擇性清） | 內容頁多半在 Cloudflare 邊緣快取，不清舊版會留著 |

---

## 1. 觸發與分支

| 事件 | Workflow | 動作 | 跑在哪 |
|---|---|---|---|
| push → `master` | `deploy.yml` | build（僅變動的應用）→ push ghcr → 部署 → 健康檢查 → 失敗自動回滾 → 清快取 | build 用 hosted；部署用 self-hosted |
| `pull_request` → `master`（含 fork） | `ci.yml` | lint ＋ unit test ＋ `docker build`（**不 push**）＋ OpenAPI 漂移檢查（若後台前端有型別產生器） | 一律 hosted |
| 手動 | `db-migrate.yml` | 套用 EF Core migration，需 `production-db` 環境核准 | self-hosted |
| 手動 | `rollback.yml` | 指定 SHA 重新部署舊映像檔 | self-hosted |

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

## 5. 🔴 資料庫遷移關卡

### 現況與怎麼演進

現有 `db/club-schema.sql`（144 表）與 `db/charity-schema.sql`（29 表）是**整份建表腳本**，本機已用 SQL Server 容器實測通過（S0-6b），**但這不是給例行部署跑的東西**——它只在資料庫第一次誕生時跑一次。

| 階段 | 用什麼 | 誰跑、怎麼跑 |
|---|---|---|
| **建庫（僅一次）** | 現有 `db/*.sql` 整份腳本 | **人工執行**，不進 CI／CD（STATUS S0-6，暫緩中）。這是「創世」不是「部署」 |
| **建庫之後的每次結構變更** | **EF Core Migrations** | 見下方流程，**與例行程式部署脫鉤，走獨立核准關卡** |

**EF Core 與現有手寫 DDL 怎麼接軌**：`api` 專案第一次建立時，對著已經用 `db/*.sql` 建好的資料庫跑 `dotnet ef dbcontext scaffold`（reverse engineer），產出 Entity 類別，並建立一個**標記為已套用的空白基準 migration**（`dotnet ef migrations add InitialBaseline`，然後 `dotnet ef migrations add InitialBaseline --context ... ` 標記為已執行，不實際重跑 DDL）。**這步驟是一次性的 handoff，交給 `backend-engineer` 在建立 `api` 專案時做**，之後才進入「每次改動都是一個新 migration」的常態。

### 關卡設計

```
開發者改 Entity → dotnet ef migrations add <Name> → migration 檔進 PR（人工 code review）
                                                            │
                                                            ▼
                                      合併進 master（不會自動套用到 prod）
                                                            │
                                                            ▼
                              手動觸發 db-migrate.yml（或偵測到 apps/api/Migrations/** 有新檔自動起草，但暫停待核准）
                                                            │
                                                            ▼
                    ⛔ GitHub Environment「production-db」— Required reviewers（人工在 GitHub UI 按下 Approve）
                                                            │
                                                            ▼
                         self-hosted runner 執行 dotnet ef database update（讀 VM 本機 .env 的連線字串）
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

⚠️ **Azure SQL Basic 層的自動備份（PITR）只保留 7 天**，這是唯一的救命索——`db-migrate.yml` 的 job summary 要把 `dotnet ef migrations script --idempotent` 的輸出貼出來，讓核准者在按 Approve 前真的看得到要跑什麼 SQL，不是盲按。

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
`DROP TABLE`），成功後才刪歷史紀錄與本機檔案**。S0-7k 實際在共用的 `tcrfc_club_dev` 上重現過：
對已套用的 `AddMatchOriginalSchedule` 用 `--force` 移除，直接把另一個 agent 正在用的
`matches.original_kickoff`／`original_match_on` 兩欄砍掉，發現後用重新 `add` 同名 migration
＋`dotnet ef database update` 補回去才復原。**下手前務必確認：這支 migration 的 `Down()`
會不會刪掉別人正在依賴的表或欄位；會的話，改成直接手改既有 migration／Designer／snapshot 三個
檔案（讓檔案內容對齊資料庫現況），不要用 `--force` 硬刪重建**——`tcrfc_club_dev` 常有多個 agent
同時在用，這條路徑的風險不是理論上的。

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
| **映像檔 tag** | 一律用 **git SHA**（不可變）。回滾＝重新指向舊 SHA 的映像檔，**不需要重新建置**（映像檔已經在 ghcr 上） |
| **部署後健康檢查** | 不是「容器有沒有活著」，是**應用層探針**：`api` 提供 `/readyz`（真的檢查兩個 `DbContext` 能連線、Redis 連線失敗算警告不算失敗——呼應 `17` §4「Redis 掛掉不得讓請求失敗」）；`nuxt-*`／`admin-*` 提供 `/healthz`（至少確認 SSR 行程存活＋能打到 `api`）。部署 job 對每個換了的容器 retry 檢查（如 10 次、間隔 5 秒，給 .NET 冷啟動時間） |
| **失敗自動回滾** | 健康檢查連續失敗 → deploy job 自動把該 service 的映像檔 tag 改回**上一次成功部署記錄的 SHA**，重跑 `docker compose up -d`，**workflow 標記為失敗並通知**（不會安靜吞掉） |
| **成功記錄** | 每次健康檢查通過後，把 `<service>=<sha>` 寫進 VM 本機一個 `/opt/tcrfc/deploy-state.env` 檔（不進 git）。下次部署失敗要回滾時，讀的就是這個檔的上一行 |
| **人工回滾** | 重跑 `rollback.yml`（`workflow_dispatch`，輸入要回滾到的 SHA），同樣走 self-hosted runner、同樣跑健康檢查 |
| **通知** | 失敗（部署失敗、健康檢查失敗、自動回滾發生）都要通知——用什麼管道（Email／LINE Notify／Slack）待使用者選，先在 workflow 留一個 `on: failure` 的通知 step 佔位 |

---

## 7. Secrets 清單

**設計原則：能在 VM 本機解決的機密，不進 GitHub Secrets。** self-hosted runner 讓部署動作發生在 VM 上，資料庫連線字串、LINE Pay 憑證這類「跑起來的應用程式要用、但 GitHub 端的 workflow 步驟本身不需要看到」的機密，**直接放 VM 的 `.env` 檔即可，從未離開過 VM，公開 repo 的 Secrets 外洩風險與它們無關。**

### 7.1 GitHub Secrets（GitHub 端步驟真的要用到的）

| Secret | 用途 | 誰用 |
|---|---|---|
| `CLOUDFLARE_API_TOKEN` | 部署後清快取，僅 `Zone.Cache Purge` 權限，限定官網／藍鯨／慈善三個 zone | `deploy.yml` |
| （選用）`NOTIFY_WEBHOOK_URL` | 部署失敗／回滾通知 | 所有 workflow 的失敗通知 step |

> `ghcr.io` 推送用內建 `secrets.GITHUB_TOKEN`（`permissions: packages: write`），**不另外開 PAT**。
> self-hosted runner 註冊 token 是一次性的，**不是常駐 secret**，註冊完即棄用。
> Cloudflare zone ID（tcrfc／藍鯨／慈善）不是機密，放 **Actions Variables** 不放 Secrets。

### 7.2 VM 本機 `.env`（不進 GitHub，任何形式都不進 git）

> 存放路徑建議 `/opt/tcrfc/secrets/`，**在 `actions/checkout` 的工作目錄之外**（checkout 目錄每次 run 可能被清乾淨，機密檔必須是獨立、持久的路徑），`docker-compose.yml` 用絕對路徑的 `env_file:` 引用。檔案權限 `600`，擁有者是 runner 的服務帳號。
> ⚠️ **俱樂部與協會的憑證分屬不同主體**（`17` §5），即使同放一台 VM，**建議實體上拆成兩個檔案**（`club.env`／`charity.env`），存取與輪替各自由各自的持有人負責，不要混在一個檔案裡——這是治理上的區隔，不只是技術上的。

| 變數（示意，實際命名待 `backend-engineer` 定案） | 用途 | 持有人 |
|---|---|---|
| `CLUB_SQL_CONNECTION_STRING` | `api` 連 `sqldb-club` | 俱樂部（系統管理） |
| `CHARITY_SQL_CONNECTION_STRING` | `api` 連 `sqldb-charity` | **協會**（`17` §5：獨立資料庫） |
| `REDIS_PASSWORD` | `api` 連 `redis` 容器 | 俱樂部（系統管理） |
| `LINE_PAY_CLUB_CHANNEL_ID` / `_SECRET` | 官網商店結帳＋藍鯨代收代付 | 俱樂部 |
| `LINE_PAY_ASSOCIATION_CHANNEL_ID` / `_SECRET` | 慈善捐款 | **協會**（不得與俱樂部共用，`14` 已明文） |
| `INVOICE_SERVICE_API_KEY_CLUB` | 電子發票（俱樂部字軌） | 俱樂部 |
| `INVOICE_SERVICE_API_KEY_CHARITY` | 電子發票（協會字軌，**不得共用字軌**，STATUS B-10） | **協會** |
| `JWT_SIGNING_KEY_CLUB` | 官網前後台登入權杖簽章 | 俱樂部（系統管理） |
| `JWT_SIGNING_KEY_CHARITY` | 慈善後台獨立帳號體系的權杖簽章（`17` §5 獨立 2FA） | 協會 |
| `LINE_LOGIN_CHANNEL_ID` / `_SECRET` | 會員 LINE 一鍵登入 | 俱樂部 |
| `AZURE_BLOB_CONNECTION_STRING`（club） | 圖片上傳 | 俱樂部 |
| ⚠️ `AZURE_BLOB_CONNECTION_STRING`（charity，**是否需要獨立 Storage Account 待確認**） | 若慈善也走「圖片欄位直傳」，比照資料庫的獨立原則，**存放體很可能也該分兩個 Storage Account**——`17` 未明文，建議與 §5 一併確認 | 協會 |
| `APNS_KEY_ID` / `.p8` 內容 / `FCM_SERVICE_ACCOUNT_JSON` | `api` 呼叫推播（後台 `M3`） | 俱樂部（App 帳號主體是俱樂部，`19` §9） |

> ⚠️ 以上變數名為規劃階段示意，**實際命名由建立 `api` 專案時的 `backend-engineer` 定案**，本檔不強制欄位名，只強制「這些東西是什麼、放哪裡、誰是持有人」三件事。

---

## 8. 本檔不決定的事

- **`nuxt-club` 品牌切換的實作細節**（CSS 變數怎麼在 runtime 換、favicon 怎麼依 host 選）——部署層假設是「一個映像檔、環境變數切換」，實作交給 `frontend-architect`；技術上真的做不到才退回兩個映像檔（見 §3）
- **失敗通知的實際管道**（Email／LINE Notify／Slack）——workflow 先留 webhook 佔位
- **`apps/*` 目錄結構的確切命名**——本檔用的路徑是規劃慣例，實際建專案骨架時可調整（S0-7a／S0-9）
- **慈善平台圖片儲存體是否需要獨立 Storage Account**——`17` 未明文，見 §7.2 的待確認項
- **`master` 是否強制 PR review**——目前單人開發非強制，建議但不列為硬性關卡（DB migration 的關卡已經是硬性的）
- **是否要做零停機部署（藍綠）**——現在的秒級空窗被判定可接受；量到使用者有感再升級，升級成本是雙倍容器資源
- **db-migrate.yml 的自動觸發時機**——用「偵測到 `apps/api/Migrations/**` 有新檔就起草待核准」還是「完全手動 `workflow_dispatch`」，留給實作時依團隊習慣決定，兩者都符合「與例行部署脫鉤＋人工核准」這個硬性要求
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
| [`.github/workflows/_node-app.yml`](../.github/workflows/_node-app.yml) | 同上（內部用） | ✅ 四個 Node 應用共用的可重用 workflow（`push: false` 版） |
| [`.github/workflows/_node-app-deploy.yml`](../.github/workflows/_node-app-deploy.yml) | §1／§2「映像檔要放哪裡」 | ✅ 四個 Node 應用共用的可重用 workflow（`push: true` 版，供 `deploy.yml` 呼叫） |
| [`.github/workflows/deploy.yml`](../.github/workflows/deploy.yml) | §1「觸發與分支」的 `deploy.yml` | 🟡 **只做了 build＋push 段**（現在能跑，不需要 VM）；**部署段整個 `if: false` 停用**，見下方「CD 段還缺什麼」 |

**尚未建立**：`db-migrate.yml`、`rollback.yml`（§5／§6 設計的兩個手動觸發 workflow）——
兩者都預設「self-hosted runner 已存在」，VM 隨 STATUS.md S0-6 暫緩，**沒有先做出兩個一定會失敗
或永遠不會被觸發的空殼 workflow**，等 VM 就緒、`deploy.yml` 的部署段解禁時一併補上。

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
   🔴 **2026-09-25（`S0-13`）更新**：`dotnet test` 改連專用的 `tcrfc_club_test`，不再連
   `tcrfc_club_dev`——本機開發同樣改用 `db/seed/setup-test-db.sh`（串接
   `deploy/local-ddl.sh --apply-test-db`＋`db/seed/apply-seed.sh`）建立這個資料庫，`ci.yml` 的
   `api` job 直接沿用同一支腳本（`./db/seed/setup-test-db.sh --recreate`，容器指向
   `mssql-ci`），**本機與 CI 對齊同一套流程，沒有另外寫一份 CI 專用的測試庫建置邏輯**。
   `./deploy/local-ddl.sh --apply` 這一步仍保留，只為了建立慈善庫 `tcrfc_charity_dev`；
   它連帶建立的 `tcrfc_club_dev` 在 CI 這個用完即丟的容器裡沒有任何步驟會用到，留著沒有風險
   （容器隨 job 結束銷毀），拆開兩支腳本反而增加維護成本。原因見 `docs/14-invariants.md`
   `S0-13` 一條、`apps/api/README.md`「怎麼跑」：`dotnet test` 與無頭瀏覽器實走原本共用
   `tcrfc_club_dev`，同一天發生三次互相干擾。
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

### CD 段還缺什麼（`deploy.yml` 的部署 job，`if: false`）

- **VM 與 self-hosted runner 本身**（STATUS.md S0-6，Azure 資源尚未開通）——這是唯一的硬阻塞，
  其餘都是「VM 就緒後把佔位內容填實」的文書工作：
  1. 拿掉 `if: false`，`runs-on` 換成 `[self-hosted, tcrfc-vm]`；
  2. `docker compose pull && up -d` 的實際部署目錄路徑（`deploy/README.md` 或屆時的 VM 佈署慣例）；
  3. `/healthz`／`/readyz` 健康檢查的 retry 迴圈（§6 已設計，未寫成 shell）；
  4. 失敗自動回滾讀 `/opt/tcrfc/deploy-state.env` 的實際邏輯；
  5. Cloudflare 快取清除（需要 `CLOUDFLARE_API_TOKEN`，見 §7.1，此 secret 目前也還沒建立）；
  6. 失敗通知的實際管道（§8「本檔不決定的事」，webhook URL 待使用者選）。
- **`db-migrate.yml`／`rollback.yml`**：完全未開始，同樣卡在 self-hosted runner。
- **§4 防護鏈第 0 條（GitHub repo 設定）**：「Actions → Fork pull request workflows → Require
  approval for all outside collaborators」——這是**唯一不需要等 VM、現在就能做**的防護，因為它
  是 repo 層級設定、不是 workflow 檔案能表達的東西。**本次任務沒有代為變更 GitHub repo 設定**
  （不確定的帳號權限操作，且不在「撰寫 workflow 檔」的授權範圍內），留給使用者手動到
  repo 的 Settings → Actions → General 確認並勾選。**建議現在就做，不必等 S0-6**——反正還沒有
  self-hosted runner，這條設定現在生效與否對現況沒有實質差異，但晚做不如早做，之後忘記的風險
  比現在花一分鐘設定的成本高。

---

## 9. 開通 Azure 後要補的設定清單

> 這份是「Azure 資源一開通，回來把這些填一填就能跑」的清單，對應 `17` §9 的驗證程序，本檔只列**跟 CI/CD 直接相關**的部分。

| # | 項目 | 動作 |
|---|---|---|
| 1 | **VM 建好、Docker 裝好** | 在 VM 上安裝 GitHub Actions self-hosted runner（`./config.sh` 用一次性註冊 token，設定 label `tcrfc-vm`），設成 systemd 服務常駐 |
| 2 | **VM 上建 `.env` 檔** | 依 §7.2 建立 `/opt/tcrfc/secrets/club.env`、`charity.env`，權限 `600` |
| 3 | **VM 本機建 `deploy-state.env`** | 空檔即可，首次部署後自動寫入 |
| 4 | **NSG** | **確認 CI/CD 不需要新增任何 inbound 規則**——這是方案 B 的重點驗證項，回頭核對 `17` §9 驗證 1–3 不受影響 |
| 5 | **ghcr 套件建立** | 第一次 `deploy.yml` 跑完會自動建立五個套件；手動把它們的 visibility 設為 **Public**（新套件預設常常是 private，要手動切） |
| 6 | **GitHub Environments** | 建立 `production`（Deployment branches：僅 `master`）與 `production-db`（同上 ＋ Required reviewers，至少 1 人） |
| 7 | **Cloudflare API Token** | 建立僅 `Zone.Cache Purge` 權限、限定三個 zone 的 token，存進 GitHub Secret `CLOUDFLARE_API_TOKEN` |
| 8 | **首次建庫** | 人工執行 `db/club-schema.sql`／`db/charity-schema.sql`（不進 CI），完成後跑 `dotnet ef dbcontext scaffold` 建立 EF Core 基準 migration（§5） |
| 9 | **LINE Pay 出口 IP 驗證** | 依 `17` §9 驗證 1，**這步驟獨立於 CI/CD，部署管線建好後跑一次即可**，之後除非換 VM 不必重跑 |
| 10 | **首次部署演練** | 先在**非 LINE Pay 正式串接前**（即 §3.6 商店結帳上線前）完整跑一次 push → build → deploy → 健康檢查 → （刻意製造一次失敗）驗證自動回滾真的會動作 |
