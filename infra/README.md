# infra/ — Azure 基礎設施（Bicep）與部署手冊

> 🔵 **執行層文件，不是規格。** 拓撲與選型的依據是 [`docs/17-deployment.md`](../docs/17-deployment.md)，
> CI/CD 的依據是 [`docs/20-cicd.md`](../docs/20-cicd.md)。本檔與 `docs/17` 衝突時以 `docs/17` 為準並回頭修正本檔。
>
> 🔴 **狀態（2026-10-01）：Bicep 與 workflow 已寫、已用 `az bicep build`／`lint`／`actionlint` 驗證，尚未對任何真實 Azure 訂閱部署。**
> 第一次部署前請依「§3 一次性手動步驟」完成部署身分與 GitHub Environment 設定。

---

## 1. 這個目錄有什麼

| 路徑 | 內容 |
|---|---|
| [`main.bicep`](main.bicep) | 入口，範圍是 resource group `rg-tcrfc-prod` |
| [`main.bicepparam`](main.bicepparam) | 參數檔：Cloudflare IP 段寫在這裡；機密與個人化的值由環境變數帶入 |
| [`provision-secrets.sh`](provision-secrets.sh) | 在 Mac 執行：把機密檔與金鑰環目錄寫進 VM 並驗證（§4.3） |
| [`upload-site-images.sh`](upload-site-images.sh) | 在 Mac 執行：把前台的站台照片處理成 WebP 並上傳 Blob（§4.9） |
| [`../deploy/prod-db-init.sh`](../deploy/prod-db-init.sh) | 在 VM 上執行：正式庫首次初始化（建表、參照資料、migration 歷史、第一個管理員；§4.8） |
| [`../.github/workflows/db-migrate.yml`](../.github/workflows/db-migrate.yml)、[`../deploy/db-migrate.sh`](../deploy/db-migrate.sh) | 正式庫之後的 migration：預覽 → VM 唯讀比對 → `production-db` 核准 → 套用（§6「資料庫 migration」） |
| [`cloud-init.yaml`](cloud-init.yaml) | VM 首次開機：安裝 Docker ＋ Compose、建 `runner` 使用者與 `/opt/tcrfc/` 目錄 |
| [`modules/network.bicep`](modules/network.bicep) | VNet、`snet-app`、NSG、靜態 Public IP（含鎖） |
| [`modules/compute.bicep`](modules/compute.bicep) | NIC、VM |
| [`modules/sql.bicep`](modules/sql.bicep) | Azure SQL 伺服器 ＋ 兩個 Basic 資料庫（含鎖） |
| [`modules/storage.bicep`](modules/storage.bicep) | 儲存體帳戶（俱樂部、慈善各呼叫一次，含鎖） |
| [`modules/monitoring.bicep`](modules/monitoring.bicep) | Action Group、七個指標告警（兩庫資料空間 1.5 GB、兩庫各兩級 DTU、VM CPU 額度）、月預算 |
| [`../.github/workflows/infra.yml`](../.github/workflows/infra.yml) | push `master`（`infra/**` 有變動）→ what-if → deploy |
| [`../.github/workflows/infra-validate.yml`](../.github/workflows/infra-validate.yml) | PR 與 `infra.yml` 共用的 `bicep lint`／`build`，不需任何憑證 |

**為什麼範圍是 resource group 而不是訂閱**：資源群組由人工一次性建立，部署身分只被授權到這個群組
（最小權限），不需要訂閱層級的任何權限。代價只有一條 `az group create`（見 §3 步驟 2）。

**部署模式一律 Incremental。** ⛔ 絕對不要用 `--mode Complete`：它會刪除群組內「模板沒寫」的資源
（例如手動建的部署身分 `id-tcrfc-deploy`）。同理：**從 Bicep 刪掉某個資源，Incremental 部署不會刪它**，
要刪得人工處理（並先移除鎖）。

---

## 2. 資源與命名

`<uniq>` ＝ `take(uniqueString(resourceGroup().id), 6)`，對同一個資源群組是穩定值，只用在需要全球唯一的名稱上。

| 資源 | 名稱 | 規格／重點 |
|---|---|---|
| 資源群組（人工建） | `rg-tcrfc-prod` | Japan East |
| 部署身分（人工建） | `id-tcrfc-deploy` | user-assigned managed identity ＋ federated credential |
| VNet | `vnet-tcrfc-prod` | `10.20.0.0/16` |
| 子網 | `snet-app` | `10.20.1.0/24`；Service Endpoint `Microsoft.Sql`、`Microsoft.Storage`；`defaultOutboundAccess=false` |
| NSG | `nsg-tcrfc-prod-app` | 入站 443／80 僅 Cloudflare 段、22 僅 `SSH_ALLOWED_CIDR`、其餘明確拒絕；綁在 `snet-app` |
| Public IP | `pip-tcrfc-prod` | **Standard、Static、IPv4，獨立資源**；🔒 CanNotDelete |
| NIC | `nic-tcrfc-prod-vm` | 刪 VM 時保留（`deleteOption: Detach`） |
| VM | `vm-tcrfc-prod` | `Standard_B2ms`、Ubuntu 24.04 LTS（Gen2、Trusted Launch）、SSH 金鑰登入、密碼登入停用 |
| OS 磁碟 | `vm-tcrfc-prod-osdisk` | Premium SSD 64 GB；刪 VM 時保留 |
| SQL 伺服器 | `sql-tcrfc-prod-<uniq>` | 僅 VNet 規則（無 IP 規則、無「允許 Azure 服務」）；TLS 1.2；🔒 CanNotDelete |
| 資料庫 | `tcrfc_club`、`tcrfc_charity` | **Basic（5 DTU／2 GB）**、PITR 7 天、**備份儲存冗餘 Local**、不設 LTR |
| 儲存體（俱樂部） | `sttcrfcclub<uniq>` | 容器 `images`／`videos`／`documents`（**匿名 blob 讀取**）、`proposals`（私有）；帳戶允許公開網路；🔒 CanNotDelete |
| 儲存體（慈善） | `sttcrfccharity<uniq>` | 容器 `charity-images`（**匿名 blob 讀取**，慈善前台要顯示封面與 Logo）；帳戶允許公開網路；🔒 CanNotDelete |
| Action Group | `ag-tcrfc-prod-ops` | Email → `ALERT_EMAIL` |
| 告警 | `alert-tcrfc-prod-tcrfc_club-storage-1_5gb`、`alert-tcrfc-prod-tcrfc_charity-storage-1_5gb`、`alert-tcrfc-prod-vm-cpu-credits-low` | 資料空間 ≥ 1.5 GB（`storage` 指標）；`alert-tcrfc-prod-<庫>-dtu-80`／`-dtu-95`（`dtu_consumption_percent` 平均 ≥ 80%／95%，S0-10 壓測建議值，見 [`deploy/loadtest/README.md`](../deploy/loadtest/README.md)）；CPU Credits Remaining < 100（可調） |
| 預算 | `budget-tcrfc-prod-monthly` | 資源群組範圍、每月 US$100（帳單幣別為美元）、實際花費 80% 與 100% 寄信 |
| 鎖 | `lock-pip-tcrfc-prod`、`lock-sql-tcrfc-prod-<uniq>`、`lock-sttcrfcclub<uniq>`、`lock-sttcrfccharity<uniq>` | 皆 `CanNotDelete` |

兩個儲存體帳戶：`Standard_LRS`、`allowBlobPublicAccess=true`（只有上表標「匿名 blob 讀取」的容器 `publicAccess=Blob`，**可憑完整網址讀單一檔案、不能列舉容器**）、
防火牆**允許公開網路**（匿名讀取的必要條件，見 §8 風險 1 的取捨）、
blob 軟刪除／容器軟刪除／版本控制各 14 天，**另有生命週期規則在 14 天後刪除舊版本**（版本控制本身不會自動過期，
不加規則舊版本會無限累積計費）。

**刻意不開**：ACR（映像檔放 GHCR，`docs/20` §2）、Key Vault（機密放 VM 的 `/opt/tcrfc/secrets/`，`docs/20` §7.2）、
Azure Cache for Redis（Redis 是 compose 容器，`docs/17` §1）、Private Endpoint（用免費的 Service Endpoint，`docs/17` §2）、
staging 環境（全專案只有本機與正式兩套，`docs/20` §1）。

**不寫死任何網域。** 網域只存在 VM 的 `/opt/tcrfc/.env`（`docs/17` §10）。

---

## 3. 一次性手動步驟（第一次部署前，依序）

> 以下需要訂閱 **Owner**（或同等：能建資源群組、建自訂角色、指派角色）的人在自己的終端機做一次。
> 這些步驟是「部署管線自己的地基」，不能由管線自己建。
>
> 🔵 **步驟 0–5 已合併成 [`bootstrap.sh`](bootstrap.sh)**：`bash infra/bootstrap.sh`。可重跑（已存在的會略過），
> 機密在執行時輸入、直接寫進 GitHub secret 不落地，並在寫 secrets 前確認 `production` 只允許 `master`。
> 只剩步驟 5a（Fork PR 核准）要到網頁設定。下面逐步說明保留作為參考與疑難排解用。

### 步驟 0：工具與登入

```bash
az login
az account show --query '{name:name, id:id, tenant:tenantId}' -o table   # 確認是要用的訂閱
az account set --subscription '<訂閱 ID 或名稱>'                          # 不對就切換
```

### 步驟 1：註冊資源提供者（訂閱層級，只需一次）

部署身分在資源群組層級沒有 `*/register/action` 權限，未註冊的提供者會讓部署失敗（`MissingSubscriptionRegistration`）。

```bash
for ns in Microsoft.Network Microsoft.Compute Microsoft.Sql Microsoft.Storage \
          Microsoft.Insights Microsoft.Consumption Microsoft.ManagedIdentity Microsoft.Authorization; do
  az provider register --namespace "$ns"
done
# 等全部變成 Registered
az provider list --query "[?contains('Microsoft.Network Microsoft.Compute Microsoft.Sql Microsoft.Storage Microsoft.Insights Microsoft.Consumption Microsoft.ManagedIdentity Microsoft.Authorization', namespace)].{ns:namespace, state:registrationState}" -o table
```

### 步驟 2：建資源群組

```bash
RG=rg-tcrfc-prod
az group create --name "$RG" --location japaneast --tags project=tcrfc env=prod
```

### 步驟 3：建部署身分（user-assigned managed identity）＋ federated credential

選 **managed identity 而不是 App Registration** 的理由：不需要 Entra 目錄權限（建 App Registration 需要）、
身分本身是資源群組內的一個 ARM 資源、沒有任何 client secret 可洩漏。兩者對 GitHub OIDC 登入的效果相同。

```bash
SUB=$(az account show --query id -o tsv)
TENANT=$(az account show --query tenantId -o tsv)

az identity create --resource-group "$RG" --name id-tcrfc-deploy --location japaneast
CLIENT_ID=$(az identity show -g "$RG" -n id-tcrfc-deploy --query clientId -o tsv)
PRINCIPAL_ID=$(az identity show -g "$RG" -n id-tcrfc-deploy --query principalId -o tsv)

# subject 綁死「repo + environment:production」。本 repo 啟用 GitHub「不可變 subject」，前綴帶 owner／repo 數字 ID，
# 以 `gh api repos/waiting0201/tcrfc/actions/oidc/customization/sub` 的 sub_claim_prefix 為準（bootstrap.sh 會自動查）
# ⚠️ subject 區分大小寫，必須與 GitHub 上的 owner/repo 大小寫一致
az identity federated-credential create \
  --resource-group "$RG" \
  --identity-name id-tcrfc-deploy \
  --name github-production \
  --issuer https://token.actions.githubusercontent.com \
  --subject 'repo:waiting0201@5709750/tcrfc@1334739698:environment:production' \
  --audiences api://AzureADTokenExchange
```

這代表：**只有 `waiting0201/tcrfc` 這個 repo、且 job 進入了 `production` Environment 的 run** 才換得到 Azure token。
fork 的 PR、非 `master` 分支（被 Environment 的分支限制擋下）、其他 Environment 都換不到。

### 步驟 4：授權（範圍僅 `rg-tcrfc-prod`，最小權限）

```bash
SCOPE="/subscriptions/$SUB/resourceGroups/$RG"

# (a) Contributor：建立／更新資源群組內的資源、跑 what-if
az role assignment create --assignee-object-id "$PRINCIPAL_ID" --assignee-principal-type ServicePrincipal \
  --role Contributor --scope "$SCOPE"

# (b) 鎖：Contributor 排除了 Microsoft.Authorization/*/write，所以建 resource lock 需要額外授權。
#     不用 User Access Administrator（它能改任何角色指派＝權限過大），改建只含 locks/* 的自訂角色。
cat > /tmp/tcrfc-lock-role.json <<JSON
{
  "Name": "TCRFC Lock Manager",
  "IsCustom": true,
  "Description": "只能讀寫 resource lock，供 infra 部署管線建立 CanNotDelete 鎖。",
  "Actions": ["Microsoft.Authorization/locks/*"],
  "NotActions": [],
  "AssignableScopes": ["$SCOPE"]
}
JSON
az role definition create --role-definition /tmp/tcrfc-lock-role.json
rm /tmp/tcrfc-lock-role.json

# 自訂角色建立後要等約 1–2 分鐘才能指派
az role assignment create --assignee-object-id "$PRINCIPAL_ID" --assignee-principal-type ServicePrincipal \
  --role "TCRFC Lock Manager" --scope "$SCOPE"
```

**預算資源**（`Microsoft.Consumption/budgets`）掛在資源群組範圍，Contributor 通常就夠。
若第一次部署在預算那一步回 403，補指派 `Cost Management Contributor`（同一個資源群組範圍）即可。

> ⚠️ **鎖防的是人為誤刪，不是被入侵的管線**：部署身分既有 Contributor 又能管鎖，理論上能先移除鎖再刪資源。
> 這個風險由「Environment 只允許 `master`、federated credential 綁 Environment、能推 `master` 的只有受邀協作者」控管（`docs/20` §4）。

### 步驟 5：GitHub 設定

**5a. Repo 設定（🔴 防護鏈的地基，`docs/20` §4 第 0 條）**
Settings → Actions → General → **Fork pull request workflows from outside collaborators** →
**Require approval for all outside collaborators**。

**5b. 建 Environment `production`**（若 `docs/20` §9 第 6 項已建過就沿用）
Settings → Environments → New environment → `production` → **Deployment branches and tags** →
*Selected branches and tags* → 只加 `master`。

**5c. 在 `production` 填下列 variables 與 secrets**（Settings → Environments → production）：

| 類型 | 名稱 | 值 | 為什麼放這型 |
|---|---|---|---|
| Variable | `AZURE_CLIENT_ID` | 步驟 3 的 `$CLIENT_ID` | 非機密，OIDC 登入用 |
| Variable | `AZURE_TENANT_ID` | `$TENANT` | 同上 |
| Variable | `AZURE_SUBSCRIPTION_ID` | `$SUB` | 同上 |
| Variable | `SSH_PUBLIC_KEY` | 你的 SSH **公鑰**（`cat ~/.ssh/id_ed25519.pub`） | 公鑰不是機密；不進版控只因為是個人化的值 |
| Variable（可選） | `ENTRA_ADMIN_LOGIN` | Entra 管理員的 UPN 或群組名稱 | 與下一列成對；都不填＝不設定 Entra 管理員 |
| Secret | `SQL_ADMIN_PASSWORD` | SQL 管理員密碼（≥16 字元，含大小寫數字符號） | 機密 |
| Secret | `SSH_ALLOWED_CIDR` | 你的固定來源 IP，格式 `x.x.x.x/32`（值只放 GitHub secret，**不寫進任何版控檔案**） | 🔴 **公開 repo 的 log 人人可看**；secret 值在 log 中會被遮蔽，what-if 輸出才不會洩漏你的家用／辦公室 IP |
| Secret | `ALERT_EMAIL` | 告警與預算通知收件 Email | 同上（個資） |
| Secret（可選） | `ENTRA_ADMIN_OBJECT_ID` | Entra 管理員的物件 ID（`az ad user show --id <upn> --query id -o tsv`） | 同上 |

SQL 管理員密碼請另存密碼管理器——`provision-secrets.sh`（§4.3）要你再輸入一次來組連線字串。

### 步驟 6：第一次部署

1. 先 `workflow_dispatch` 跑 **Infra Deploy**，勾選 **只跑 what-if、不部署**，看 job summary 的預覽：
   第一次應該全是「Create」。確認沒有意外（沒有 Delete）。
2. 再手動跑一次（不勾選）或直接 push 一個 `infra/**` 的變更，正式部署。
3. 部署成功後（約 10–15 分鐘，VM 與 SQL 最慢）：
   ```bash
   az deployment group list -g rg-tcrfc-prod -o table
   az network public-ip show -g rg-tcrfc-prod -n pip-tcrfc-prod --query '{ip:ipAddress, sku:sku.name, alloc:publicIPAllocationMethod}' -o table
   az deployment group show -g rg-tcrfc-prod -n <上面列出的最新 infra-* 名稱> --query properties.outputs
   ```

> 🔴 **LINE Pay 登記的出口 IP ＝ `pip-tcrfc-prod` 的 `ipAddress`。** 這個值一旦登記給 LINE Pay，
> 就**不得**讓 IP 資源被刪除或重建（有鎖保護）。登記動作見 `docs/17` §3。

---

## 4. 部署後：VM 上的一次性設定

SSH 登入（只有 `SSH_ALLOWED_CIDR` 那個來源連得進來）：

```bash
IP=$(az network public-ip show -g rg-tcrfc-prod -n pip-tcrfc-prod --query ipAddress -o tsv)
ssh azureuser@"$IP"
```

### 4.1 確認 cloud-init 完成

```bash
cloud-init status --wait                       # 預期 status: done
test -f /var/lib/cloud/instance/tcrfc-bootstrap-done && echo OK
docker version && docker compose version
id runner                                       # 應在 docker 群組
ls -ld /opt/tcrfc /opt/tcrfc/secrets            # secrets 應為 drwx------，擁有者 runner
# 失敗時看：sudo tail -n 200 /var/log/cloud-init-output.log
```

### 4.2 註冊 GitHub Actions self-hosted runner

> 這是 `docs/20` §4 方案 B：VM 主動連 GitHub，NSG 不需要為 CI 開任何 inbound。
> **這個 runner 與 `infra.yml` 無關**——`infra.yml` 跑在 GitHub-hosted runner（建 VM 的時候 VM 還不存在）。
> 此 runner 給 `deploy.yml` 的部署 job 與 `rollback.yml` 用（另 `db-migrate.yml` 的 `pending`／`apply` 兩個 job，§6「資料庫 migration」；`docs/20` §4a、§9 第 1 項）。

1. GitHub → repo Settings → Actions → Runners → **New self-hosted runner** → Linux / x64，
   照頁面給的「下載」指令取得**當下的最新版本連結**與一次性 **註冊 token**（token 約 1 小時失效，不是常駐機密）。
2. 在 VM 上以 `runner` 使用者身分操作：

```bash
sudo -iu runner
cd /opt/tcrfc/actions-runner
# 依 GitHub 頁面顯示的連結下載並解壓縮（版本以頁面為準）
curl -fsSLo runner.tar.gz '<GitHub 頁面給的下載連結>'
tar xzf runner.tar.gz && rm runner.tar.gz
./config.sh --unattended \
  --url https://github.com/waiting0201/tcrfc \
  --token '<一次性註冊 token>' \
  --name vm-tcrfc-prod \
  --labels tcrfc-vm \
  --work /opt/tcrfc/actions-runner/_work
exit   # 回到 azureuser
cd /opt/tcrfc/actions-runner
sudo ./svc.sh install runner      # 以 runner 使用者身分跑 systemd 服務（只在 docker 群組，無 sudo）
sudo ./svc.sh start
sudo ./svc.sh status
```

GitHub Runners 頁面應顯示 `vm-tcrfc-prod` 為 Idle、label `tcrfc-vm`。

### 4.3 機密檔與設定鍵（`api` 在正式環境讀的每一個鍵）

> 🔵 **一鍵做法：在自己的 Mac 執行 [`provision-secrets.sh`](provision-secrets.sh)。** 它用 `az` 取連線字串、用 `openssl` 產生 JWT 金鑰與 Redis 密碼、
> 互動輸入 SQL 管理員密碼與 Let's Encrypt 信箱，**經 ssh stdin 管線**寫進 VM 的 `club.env`／`charity.env`／`/opt/tcrfc/.env`（本機不落地、不進命令列、不印出），
> 並建立 Data Protection 金鑰環目錄，最後在 VM 上列出鍵名（有值／空）並實際連一次兩個資料庫。可重跑；已存在的檔案會先問是否覆寫，
> **已有的 JWT 金鑰與 Redis 密碼預設沿用**，只有你選擇才重生。
>
> ```bash
> bash infra/provision-secrets.sh
> ```
>
> 前置：`az login`、`ssh` 連得到 VM（來源 IP 在 NSG 名單內）、VM 上已有 `/opt/tcrfc/secrets`（§4.1）。SQL 管理員密碼不得含 `;` `'` `"` `\` 或前後空白（連線字串無法安全表示）。
> 🔴 **檔案都放在 VM 的 `/opt/tcrfc/`，不進 git、不貼進任何對話或 log。** 俱樂部與協會的憑證分兩個檔案（`docs/20` §7.2、`docs/17` §5）。
> 值一律用**單引號**包起來：compose 的 `env_file` 與 `.env` 會把未加引號的 `$` 當變數展開（bcrypt 雜湊與密碼常含 `$`）；手動編輯時請維持單引號。

#### 設定鍵盤點（依 `apps/api` 程式逐一核對，2026-10-01）

**來源三處**：`club.env`（俱樂部）、`charity.env`（協會）、compose（`docker-compose.yml` 的 `api.environment`，值來自 `/opt/tcrfc/.env` 或固定值）。
「未設時的行為」是**正式環境（`ASPNETCORE_ENVIRONMENT=Production`，Dockerfile 寫死）**下的實際行為。

**A. 必須有（缺了起不來，或功能無聲消失）**

| 鍵 | 放哪 | 必填？ | 未設／錯誤時的行為 | 備註 |
|---|---|---|---|---|
| `CLUB_SQL_CONNECTION_STRING` | club.env | ✅ | 啟動丟例外 | 腳本以 `az` 查到的 FQDN／管理員登入名＋你輸入的密碼組出 |
| `CHARITY_SQL_CONNECTION_STRING` | charity.env | ✅（實質） | ⚠️ **整個慈善平台不註冊，無聲關閉**（`/readyz` 的 `charity_db` 只顯示 `not_configured`，不擋 ready） | 漏設不會有錯誤，只會 404 |
| `JWT_SIGNING_KEY_CLUB` | club.env | ✅ | 缺或 **< 32 字元** → 啟動失敗 | 腳本產 64 字元 hex。⚠️ 也是行事曆訂閱 token 的 HMAC 金鑰：重生會讓既有訂閱連結失效 |
| `JWT_SIGNING_KEY_CHARITY` | charity.env | ✅ | 慈善啟用時缺或 < 32 字元 → 啟動失敗 | 與俱樂部不同值；慈善捐款單號也用它推導，**請勿輪替**（`CharityOptions.ResolveOrderNoSecret`） |
| `JWT_SIGNING_KEY_MEMBER` | club.env | 強烈建議 | 未設 → 由 `JWT_SIGNING_KEY_CLUB` 衍生（可運作，但兩個身分體系共用一把根金鑰）；設了但 < 32 字元 → 啟動失敗 | 必須與 CLUB 不同值 |
| `DATA_PROTECTION_KEYS_PATH` | compose（固定 `/var/lib/tcrfc/data-protection`） | 🔴 ✅ | **不報錯**；金鑰環只在容器可寫層，**容器一重建，2FA 密鑰、慈善身分證字號與載具、推播權杖、提案下載連結全部永久無法解密**（E-109） | 對應 bind mount `/opt/tcrfc/data-protection`，見下方「金鑰環」。🔵 `api` 在 Production 缺值、目錄不存在或不可寫會**啟動失敗**（`Common/DataProtectionKeyRing.cs`） |
| `REDIS_HOST`／`REDIS_PASSWORD` | compose | ✅ | 缺 `REDIS_HOST` → 無快取（no-op，仍可跑）；密碼不符 → 快取 fail-open | 密碼來自 `.env` 的 `REDIS_PASSWORD`；**不要**再寫進 club.env（compose 的 `environment` 會蓋掉它） |
| `CORS_ALLOWED_ORIGINS` | compose（由五個網域組出） | ✅ | Production 缺 → **沒有任何來源被允許**，瀏覽器端全部被擋 | 不需手動設，改 `.env` 的網域即可 |
| `TRUSTED_PROXY_IPS` | compose（`172.28.238.2,.3,.4`） | ✅ | 缺 → 不掛 `UseForwardedHeaders`，限流把所有人當成同一個來源 | 與 compose 固定 IP 成對，勿單改一邊 |
| 六個 `*_DOMAIN`（含 `CHARITY_DOMAIN`） | compose | ✅ | `CHARITY_DOMAIN` 缺 → 付款返回網址解析不出來，付款端點回 503 | 來自 `.env` |

**B. 外部憑證／服務：到位前正式環境該怎麼填**

| 鍵 | 放哪 | 外部憑證未到位時 | 行為與理由 |
|---|---|---|---|
| `AZURE_BLOB_CONNECTION_STRING` | club.env | **填**（Bicep 已建帳戶） | 未設 → 上傳功能「不可用」（呼叫時才丟例外，不影響啟動） |
| `AZURE_BLOB_CONNECTION_STRING_CHARITY` | charity.env | **填** | 未設 → 慈善圖片上傳不可用 |
| `AZURE_BLOB_CONTAINER_IMAGES`／`_VIDEOS`／`_DOCUMENTS`／`_PROPOSALS`／`_CHARITY` | — | **不設** | 預設值（`images`／`videos`／`documents`／`proposals`／`charity-images`）就是 Bicep 建的容器 |
| `AZURE_BLOB_PUBLIC_BASE_URL`／`AZURE_BLOB_PUBLIC_BASE_URL_CHARITY` | club.env／charity.env | **不設**，等 Cloudflare 圖片網域實測通過（§4.7）再填 | 未設 → 回退儲存體網址（可用）；設了但不是絕對 https → **啟動失敗** |
| `PAYMENT_GATEWAY` | club.env | **不設** | 未設 → 會籍付款「尚未串接」，端點如實回報；🔴 設成 `fake` → **Production 啟動失敗**（刻意）。LINE Pay 商店號（B-10）到位、實作換掉 DI 註冊後才有真值 |
| `INVOICE_ISSUER` | club.env | **不設** | 同上，`fake` 在 Production 啟動失敗；商店發票「尚未串接」 |
| `EMAIL_SENDER`／`EMAIL_OUTBOX_PATH` | — | **不設** | Production **永遠**用「尚未串接」實作（`localfile` 在 Production 不註冊，因信件含一次性權杖）：驗證信、重設密碼信不會寄出，呼叫端如實回報。寄信供應商（B-16）決定後才有新鍵 |
| `GEOCODER`／`GOOGLE_MAPS_GEOCODING_API_KEY` | club.env | **不設**，使用者在 Google Cloud 建好**限定 Geocoding API＋限定 VM 出口 IP** 的金鑰後兩個一起填（`GEOCODER=google`） | 未設 → 後台「由地址定位」回 503、存檔不阻擋；🔴 `GEOCODER=fake` 在 Production 啟動失敗；只有 `GEOCODER=google` 缺金鑰＝同樣優雅降級。金鑰不得貼進對話或進版控。步驟與條款風險見 `docs/17` §3「G 批的接縫」 |
| `LINE_LOGIN_CHANNEL_ID`／`_SECRET`／`LINE_LOGIN_REDIRECT_URIS` | club.env | **不設** | 三項缺一 → LINE 登入端點回 **503**（不假成功）。到位後三個一起填；`REDIRECT_URIS` 逗號分隔，須與 LINE Developers 登記的 Callback URL 逐字一致 |
| `CHARITY_ALLOW_FAKE_PROVIDERS` | charity.env | **不設（絕不填 `true`）** | 未設 → 慈善金流／發票／寄信一律「尚未設定」，捐款頁顯示服務暫時無法使用；`true` 會讓假金流對任何交易回扣款成功、**把假捐款寫進正式資料庫**（`docs/17` §5） |
| `TURNSTILE_SECRET_KEY` | club.env | **不設** | ⚠️ 主站與藍鯨公開表單的人機驗證（Cloudflare Turnstile，兩站共用這一把；與慈善的 `TURNSTILE_SECRET_KEY_CHARITY` 不同鍵）。未設 → **一律放行**，只剩 IP 限流＋honeypot。設了之後只驗「`captcha_enabled = true` 的表單」（後台表單設計器的開關，種子預設開），缺 token／驗證失敗回 **422 `captcha_failed`**；Cloudflare 本身異常時放行。🔴 **必須與前台一起設**：在 `/opt/tcrfc/.env` 加 `TURNSTILE_SITE_KEY=<site key>`（compose 轉給 `nuxt-tcrfc`／`nuxt-bw` 的 `NUXT_PUBLIC_TURNSTILE_SITE_KEY`）——**只設後端不設前端 → 沒有 token → 所有開啟驗證的表單送不出**。設好後 `docker compose --env-file /opt/tcrfc/.env up -d api nuxt-tcrfc nuxt-bw` |
| `TURNSTILE_SECRET_KEY_CHARITY` | charity.env | **不設** | ⚠️ 這一項**不是**「顯示尚未設定」：未設 → 人機驗證**一律放行**，只剩 IP 限流。設了之後前台必須同時有網站金鑰：在 `/opt/tcrfc/.env` 加 `TURNSTILE_SITE_KEY_CHARITY=<site key>`（compose 轉給 `nuxt-charity` 的 `NUXT_PUBLIC_TURNSTILE_SITE_KEY`，執行期設定、不必重建映像檔）——**只設後端不設前端 → 沒有 token → 所有捐款被擋**。兩邊設好後 `docker compose --env-file /opt/tcrfc/.env up -d api nuxt-charity` 重建這兩個容器 |
| `MEMBERSHIP_ACTIVATE_CREDENTIAL` | club.env | **不設** | 未設 → 內部會籍開通端點停用（設的話 ≥32 字元） |
| `MEMBER_EMAIL_LINK_BASE_URL` | — | **不設** | 它是**單一值**，設了會讓主站與藍鯨兩個俱樂部的信件連結都指向同一站；不設則用資料庫 `clubs.domain`（確認該欄位是 stg 網域或之後的正式網域） |
| `CHARITY_ASSOCIATION_NOTIFY_EMAIL` | charity.env | **不設** | 未設 → 發票開立失敗不寄通知，仍標記失敗進後台佇列。是個人 Email，不進公開 repo；且目前沒有寄信管道 |
| `CHARITY_PUBLIC_BASE_URL` | — | **不設** | 由 `CHARITY_DOMAIN` 組出 `https://{網域}` |
| `LINE_PAY_*`、`INVOICE_SERVICE_API_KEY_*`、`APNS_KEY_ID`、`FCM_SERVICE_ACCOUNT_JSON` | — | **不放任何檔案** | 🔵 **程式不讀這些鍵**（grep 無命中）：商店與慈善的 LINE Pay／發票憑證存在資料庫（後台設定頁，Data Protection 加密，所以**更依賴金鑰環**）；APNs／FCM 傳輸尚未實作。`docs/20` §7.2 舊表列的這幾個名稱是規劃階段示意 |

**C. 調校旋鈕（預設值即可，不要設）**

| 鍵 | 預設 | 鍵 | 預設 |
|---|---|---|---|
| `ADMIN_LOGIN_RATE_LIMIT_PERMIT_LIMIT` | 5 | `CHARITY_WORKERS_ENABLED` | Production 開（設 `false` 才關） |
| `ADMIN_REFRESH_RATE_LIMIT_PERMIT_LIMIT` | 30 | `CHARITY_WORKER_INTERVAL_SECONDS` | 60 |
| `APP_PUBLIC_RATE_LIMIT_PERMITS` | 120 | `CHARITY_PAYMENT_TIMEOUT_MINUTES` | 30 |
| `MEMBER_AUTH_RATE_LIMIT_PERMITS` | 30 | `CHARITY_INVOICE_RETRY_MINUTES` | 10 |
| `MEMBER_WRITE_RATE_LIMIT_PERMITS` | 60 | `CHARITY_PUBLIC_WRITE_RATE_LIMIT_PERMITS`／`_READ_` | 30／120 |
| `APP_JOBS_INTERVAL_SECONDS` | 60（≤0 停用） | `SHOP_JOBS_INTERVAL_SECONDS` | Production 60（0 停用） |
| `SCHEDULED_PUBLISH_INTERVAL_SECONDS` | 60 | `QUERY_CACHE_TTL_SECONDS`／`REDIS_PORT` | 300／6379 |

⚠️ `ASPNETCORE_ENVIRONMENT`（Dockerfile 寫死 `Production`）不要在任何 env 檔覆寫；上述「未設時的行為」全部以它為前提。

**compose 本身用的 `/opt/tcrfc/.env`**：`GHCR_OWNER`、`IMAGE_TAG`、`SITE_ENV`（缺則 compose 報錯）、六個 `*_DOMAIN`、`ACME_EMAIL`、`CADDYFILE`、`REDIS_PASSWORD`、**`MEDIA_BASE_URL`**（站台照片的 Blob 網址基底，compose 轉成 `NUXT_PUBLIC_MEDIA_BASE_URL` 給兩個 Nuxt 前台；`provision-secrets.sh` 自動推出，見 §4.9；沒設＝空字串，不阻擋啟動）。
腳本寫入 `SITE_ENV=prelaunch`、`CADDYFILE=./deploy/Caddyfile.prelaunch` 與 `.env.example` 的六個 stg 網域；測試站**沒有 Basic Auth**（2026-10-02 使用者決定拿掉）；舊 `.env` 若還有 `PRELAUNCH_BASIC_AUTH_USER`／`_HASH`，覆寫時不會寫回（無害，compose 已不讀）。
不要填 `MSSQL_DEV_SA_PASSWORD`（只給本機開發）。**切正式網址時**手動編輯此檔（§5），之後腳本會因 `SITE_ENV=production` 拒絕改動它。

> ✅ `/opt/tcrfc/.env` 是**固定位置**（2026-10-02 定案）：CD（`deploy/cd-deploy.sh`）以 `--env-file /opt/tcrfc/.env` 讀它，不複製進 checkout、不改寫它。
> `.env` 的 `IMAGE_TAG` 只是**備援預設**（手動 `docker compose up -d` 用）；CD 以行程環境變數 `TAG_NUXT_CLUB`／`TAG_NUXT_CHARITY`／`TAG_ADMIN_WEB`／`TAG_ADMIN_CHARITY`／`TAG_API` 逐一覆寫，見 `docs/20` §4a。
> `CADDYFILE=./deploy/Caddyfile.prelaunch` 的相對路徑是**相對於 compose 專案目錄（runner 的 checkout 目錄）**。

#### 金鑰環（Data Protection）——🔴 沒有它，已加密的資料永久無法解密

- **位置**：VM 的 `/opt/tcrfc/data-protection`（擁有者 uid／gid `1654`＝aspnet 映像檔內建的 `app` 使用者，權限 `700`），bind mount 進 `api` 容器的 `/var/lib/tcrfc/data-protection`，由 `DATA_PROTECTION_KEYS_PATH` 指向。
  選 bind mount 而非具名 volume：`docker compose down -v` 刪不掉它、路徑固定好備份；`create_host_path: false` 讓「目錄不存在」變成 compose 的明確錯誤，而不是 docker 自動建一個 root 擁有、api 寫不進去的空目錄。
- **目錄由 `provision-secrets.sh` 建立**（不能放 cloud-init：VM 建立後 `customData` 不可變，改了會讓 `infra.yml` 部署失敗）。**重建 VM 後要重跑腳本，並先把備份還原進這個目錄。**
- 金鑰檔是**明文 XML**（Linux 上沒有 DPAPI），只靠目錄權限保護；備份檔等同機密，**不進 repo、不寄信**。
- **金鑰每 90 天自動輪替**（舊金鑰保留、新增一把）。所以備份必須**定期重做**——只備份一次，輪替後還原出來會少一把新金鑰，新資料解不開。
- **備份**（在 Mac 執行，輸出檔存進密碼管理器附件或加密磁碟，之後每季一次，或每次輪替後）：

  ```bash
  IP=$(az network public-ip show -g rg-tcrfc-prod -n pip-tcrfc-prod --query ipAddress -o tsv)
  ssh azureuser@"${IP}" 'sudo tar -C /opt/tcrfc -cz data-protection' > "tcrfc-dp-keys-$(date +%Y%m%d).tgz"
  ```

- **還原**（新 VM，api 尚未啟動前）：

  ```bash
  ssh azureuser@"${IP}" 'sudo tar -C /opt/tcrfc -xz && sudo chown -R 1654:1654 /opt/tcrfc/data-protection && sudo chmod 700 /opt/tcrfc/data-protection' < tcrfc-dp-keys-YYYYMMDD.tgz
  ```

- **驗證 uid**（換 .NET 映像檔大版本時重驗）：`docker run --rm --entrypoint id mcr.microsoft.com/dotnet/aspnet:10.0 app`（2026-10-01 實測 `uid=1654`；`apps/api/Dockerfile` 舊註解寫的 64198 是錯的，已更正）。
- **待使用者決定的備份方式**：手動每季 tar（現行建議，零成本）／VM 上 cron 打包後上傳私有 blob（需要憑證與一個新容器）／程式改用 Azure Blob＋Key Vault 持久化金鑰（要改 `apps/api`，且 Key Vault 目前刻意不開，`docs/17`）。
- **`api` 啟動檢查**（2026-10-01，E-109）：Production 下 `DATA_PROTECTION_KEYS_PATH` 未設、空白、目錄不存在或不可寫（啟動時實際寫入並刪除探測檔）一律丟例外、容器起不來；錯誤訊息指向本節。看到 `api` 容器反覆重啟且日誌有此訊息，先查目錄是否存在、擁有者是否 `1654:1654`、權限 `700`。
- **驗證容器真的寫入了金鑰環**（第一次 `api` 起來並用到加密功能之後）：`sudo ls -l /opt/tcrfc/data-protection` 應有 `key-*.xml`。目錄一直是空的＝路徑沒對上，立刻查。

⚠️ 兩個資料庫的 SQL 防火牆只放行 `snet-app`：**從你的電腦（含 SSMS／Azure Data Studio）連不上是預期行為**
（`docs/17` §9 驗證 4）。首次建庫（`db/club-schema.sql`／`db/charity-schema.sql`，`docs/20` §9 第 8 項）要從 VM 上執行，
例如在 VM 起一個含 `sqlcmd` 的容器。管理用的 SQL 管理員帳號只用於建庫與緊急處理；
`api` 日常連線改用權限較小的資料庫使用者是建議的後續強化（目前文件未定案，列於 §8 待決 4）。

### 4.4 compose 用的 `.env`

已併入 §4.3（`provision-secrets.sh` 一併產生 `/opt/tcrfc/.env`，鍵清單見該節末段）。手動建立時以 [`/.env.example`](../.env.example) 為範本。

### 4.5 Cloudflare DNS（手動）

登入 Cloudflare → `tcrfc.tw` zone → DNS → 新增六筆 **A** 記錄，內容都是 `pip-tcrfc-prod` 的 IP，**Proxy status：Proxied（橘雲）**：

| 名稱 | 對應服務（`.env` 變數） |
|---|---|
| `stg` | 主站前台（`TCRFC_DOMAIN`） |
| `bw-stg` | 藍鯨前台（`BW_DOMAIN`） |
| `charity-stg` | 慈善前台（`CHARITY_DOMAIN`） |
| `admin-stg` | 官網後台（`ADMIN_WEB_DOMAIN`） |
| `admin-charity-stg` | 慈善後台（`ADMIN_CHARITY_DOMAIN`） |
| `api-stg` | API（`API_DOMAIN`） |

- 因為 NSG 只放行 Cloudflare 段，**DNS 必須是 Proxied**；灰雲（DNS only）的流量會被 NSG 擋掉。
- Caddy 首次簽憑證與 SSL 模式的順序見 [`deploy/README.md`](../deploy/README.md)「首次…」與 `deploy/Caddyfile` 第 35 行附近註解（HTTP-01 驗證經 Cloudflare 轉進 80 埠）。
- 上線前兩層防護（`X-Robots-Tag` 標頭、`robots.txt` 全擋；後台另建議 Cloudflare Access）見 `docs/17` §10.4，**DNS 一指過去就要先到位**。

### 4.6 Cloudflare IP 段更新

Cloudflare 偶爾會調整 IP 段（來源 <https://www.cloudflare.com/ips/>）。更新方式：

1. 到 <https://www.cloudflare.com/ips-v4> 取得最新清單。
2. 修改 [`main.bicepparam`](main.bicepparam) 的 `cloudflareCidrs`，同時對照更新 `deploy/Caddyfile` 的 `trusted_proxies`（兩處必須一致，否則真實 IP 判讀與 NSG 會不同步）。
3. 開 PR → 合併到 `master` → `infra.yml` 自動部署（先看 what-if 只動到 NSG 規則）。

本檔只放 IPv4：VM 只有 IPv4 的 Public IP，Cloudflare 以 IPv4 回源。

---

### 4.7 圖片與文件的公開網域（Cloudflare 前置）——🔴 部分待驗證

**目標**：訪客從 Cloudflare 取得圖片，不直連 `*.blob.core.windows.net`；網域如 `img-stg.tcrfc.tw`（正式期換正式網域）。

**現況（2026-10-01 程式已完成）**：`BlobImagePublicUrlResolver`、`BlobVideoPublicUrlResolver`、`BlobDocumentPublicUrlResolver` 與慈善的 `BlobCharityImageStorage` 原本都用 `BlobContainerClient.Uri` 組網址；現已新增「公開網址基底」設定（做法 A）：

| 設定 | 放在 | 說明 |
|---|---|---|
| `AZURE_BLOB_PUBLIC_BASE_URL` | VM `club.env` | 俱樂部圖片／影片／documents 的公開網址基底，如 `https://img-stg.tcrfc.tw`；網址組成 `{base}/{容器}/{key}` |
| `AZURE_BLOB_PUBLIC_BASE_URL_CHARITY` | VM `charity.env` | 慈善 `charity-images`，**不與俱樂部共用**（可指到另一個子網域） |

未設定＝回退連線字串的網域（本機 Azurite 不受影響）；須為絕對 https URL（Development 放行 http），不得含帳密／query／fragment，格式錯誤 API 啟動即失敗。上傳與刪除仍走連線字串；`proposals` 私有容器不受影響。⚠️ 這兩個鍵**不要**寫進 `docker-compose.yml` 的 `environment:`（寫了會以空字串覆蓋 `env_file` 的值），一律寫在 env 檔。

**原評估的兩條路**（保留供對照，已採 A）：

| 做法 | 評估 |
|---|---|
| **A（已採用，程式已完成）**：新增設定 `AZURE_BLOB_PUBLIC_BASE_URL`、`AZURE_BLOB_PUBLIC_BASE_URL_CHARITY`，公開網址解析器改用它組網址；上傳仍走原連線字串 | 上傳與公開讀取分開，最乾淨 |
| B（不改程式）：連線字串改成 `BlobEndpoint=https://img-stg…;AccountName=…;AccountKey=…` | ⚠️ 上傳也會繞 Cloudflare、受其請求大小與逾時限制，且自訂網域下的簽章行為**未驗證**；不建議 |

**Cloudflare 端做法（待驗證，不確定的都標出來）**：

1. DNS：`img-stg`（或正式網域）建 **CNAME → `<帳戶>.blob.core.windows.net`**，Proxied。
2. 🔴 **Host header 問題**：Azure blob 以 Host 判斷帳戶，Cloudflare 回源時預設帶的是訪客請求的 Host（`img-stg…`），Azure 會回 400。兩種解法：
   - **Origin Rules 覆寫 Host header** 為 `<帳戶>.blob.core.windows.net`。覆寫 Host 在哪些方案可用、**SNI 是否同步改寫（SNI 覆寫為 Enterprise 功能）會決定 TLS 握手是否成功**——這點我無法確定，**必須實測**（`curl -I https://img-stg…/images/<已知 blob>`）。
   - **改用 Cloudflare Worker**（`fetch('https://<帳戶>.blob.core.windows.net' + path)`）代理並設快取。不依賴 Host／SNI 覆寫，免費方案有每日請求額度限制（以官方定價為準）。若 Origin Rules 實測不通，退到這個。
3. 快取規則：對 `/images/*`、`/videos/*`、`/documents/*` 設 Cache Everything 與適當 TTL；影片較大，確認方案的單檔快取上限（以 Cloudflare 官方文件為準，未查證）。
4. 驗證：不經 Cloudflare 的 blob 直連網址也會成功（因為匿名公開），要擋掉直連需另做（例如 Worker 驗證來源）；目前**不擋**，流量與費用會落在儲存體。

### 4.8 正式庫首次初始化（建表 → 參照資料 → migration 歷史 → 第一個管理員）

> 🔴 **只做一次。** 對象是 §2 部署出來的兩個**空**資料庫 `tcrfc_club`、`tcrfc_charity`。腳本 [`deploy/prod-db-init.sh`](../deploy/prod-db-init.sh) 會在目標庫「有任何使用者物件」時拒絕執行，所以重跑是安全的（只會被擋下），但**請不要用它對已有資料的庫做任何事**。
> 設計理由（為什麼是人工腳本而不是 `db-migrate.yml`、DDL 與 EF migration 的對照結果、json 型別）見 [`docs/20`](../docs/20-cicd.md) §5「正式庫首次初始化」；灌哪些資料、哪些不灌見 [`db/seed/README.md`](../db/seed/README.md)「正式庫的參照資料」。

**前置（缺一不可）**

1. 含本節腳本的 commit 已 push 到 `master`，且 `deploy.yml` 已把 `ghcr.io/waiting0201/tcrfc-api:master` 建好並設為 **Public**（`create-admin` 要用該映像檔算密碼雜湊——必須是含 `--hash-password` 的版本）。
2. VM 上 §4.3 的 `club.env`、`charity.env`、`/opt/tcrfc/.env` 都已就緒（`provision-secrets.sh` 已通過連線測試）。`/opt/tcrfc/.env` 的 `TCRFC_DOMAIN`／`BW_DOMAIN` 會被寫進 `clubs.domain`——**目前是 stg 網域，這就是暫用網址階段該有的值**；切正式網址（§5）時要到後台「俱樂部」改 domain。
3. 以 **runner 使用者**執行（機密檔擁有者是 runner、權限 600）：`sudo -iu runner`。runner 已在 docker 群組。

**步驟**

```bash
# 在 VM 上，以 runner 身分
cd /opt/tcrfc/actions-runner/_work/tcrfc/tcrfc                             # runner 的 checkout＝最近一次部署的 commit（~/tcrfc-src 已退役，docs/20 §4a）；只讀用，不要 pull／checkout／compose up
git log -1 --format='%h %s'                                                 # 確認是預期的 commit
docker pull ghcr.io/waiting0201/tcrfc-api:master                            # 取最新映像檔

# 1. 唯讀預檢：連線、庫名、現況應為 empty
./deploy/prod-db-init.sh preflight club
./deploy/prod-db-init.sh preflight charity

# 2. 初始化（各自要手動輸入「INIT <庫名>」確認）；結尾自動驗證，全綠才算完成
./deploy/prod-db-init.sh init club
./deploy/prod-db-init.sh init charity

# 3. 第一個管理員（互動輸入帳號、顯示名稱、Email、密碼兩次；密碼不顯示、不進命令列與 log）
./deploy/prod-db-init.sh create-admin club
./deploy/prod-db-init.sh create-admin charity    # 慈善後台是獨立帳號體系，由協會指定的人建立
# 登入帳號是一般字串（可用中文；不得含空白、最長 64 字元）；密碼至少 9 字元

# 4. 事後可隨時唯讀複查
./deploy/prod-db-init.sh verify club
./deploy/prod-db-init.sh verify charity
```

之後再依 §5 之前的流程起容器（`docker compose up -d`）；`api` 的 `/readyz` 應回 `club_db: ok`、`charity_db: ok`。用剛建的帳號登入主站後台與慈善後台各一次。

**預期結果（以 DDL 與 `db/prod/*.sql` 實際為準，腳本自動核對）**：主站 **189 表／482 外鍵／1 視圖**、`__EFMigrationsHistory` **21 筆**；慈善 **30 表／67 外鍵／0 視圖**、**2 筆**；`admin_users` 各 1 筆。參照資料筆數寫在 SQL 檔頭的 `-- MANIFEST` 行（主站角色 10、權限碼 275、角色權限 799、固定表單 18、表單欄位 114、首頁區塊 18…；慈善角色 9、權限碼 24、角色權限 45）。

**第一個管理員**

- 建的是**系統管理員**（`is_super_admin`，與 `system_admin` 角色）：不需另外授權俱樂部就能管理兩個俱樂部、能新增其他帳號。之後的帳號一律在後台「帳號與角色」建立；`create-admin` 只在 `admin_users` 為空時可用。
- 密碼只存在你的腦中／密碼管理器：Argon2id 雜湊由 `api` 映像檔（與登入驗證同一份程式）計算，密碼只走標準輸入。**不建立 `must_change_password`**：此旗標只是「管理員代為重設」的提示，不強制（`docs/14`），本人輸入的密碼不需要它。
- 拒絕測試帳號命名（`*.test`、`*.local`、`@example.*`）。**`sa@system.local` 例外允許**（2026-10-03 使用者裁決，可當正式管理員帳號名；它也是種子超管的名字、公開 repo 看得到，所以密碼一定要是重新設定的）。🔴 **種子的 `Admin@123` 等種子密碼絕不能出現在正式庫**：`verify` 改為比對「`admin_users.password_hash` 是否等於 `db/seed/generate-*-seed-sql.py` 裡任何一個種子雜湊」，命中即失敗。
- 🟡 兩階段驗證：後台目前不提供設定入口（`docs/14`，v3.17 裁決），所以第一個管理員只有密碼保護。密碼請用高強度並存進密碼管理器。
- 🟡 **唯一管理員忘記密碼（或登入不進去）**：後台沒有「忘記密碼」流程給管理員（只能由另一位系統管理員重設）。因此**建議上線後盡快再建第二個系統管理員**。只有一個又登不進去時，用 **`reset-password`**（2026-10-03 新增，不必手寫 SQL）：

  ```bash
  sudo -iu runner
  cd /opt/tcrfc/actions-runner/_work/tcrfc/tcrfc        # 部署目錄，已是最新部署的 commit，不要 pull
  git log -1 --format='%h %s'
  docker pull ghcr.io/waiting0201/tcrfc-api:master      # 🔴 必須是含 9 字元政策的新版映像檔
  ./deploy/prod-db-init.sh reset-password club          # 慈善後台改 charity
  ```

  流程：列出 `admin_users`（登入帳號、顯示名稱、狀態、是否鎖定、失敗次數，不顯示雜湊）→ 輸入要重設的登入帳號 → 可選：新的登入帳號（Enter＝不改）→ 帳號被停用時可選擇一併啟用 → 輸入庫名（`tcrfc_club`／`tcrfc_charity`）確認 → 新密碼輸入兩次（不顯示、不進命令列與 log）。腳本以 API 映像檔算 Argon2id 雜湊，更新 `password_hash`／`password_changed_at`、清除 `locked_until` 與 `failed_attempt_count`、撤銷該帳號所有更新權杖，整段同一交易，最後自動核對。

**失敗與回復**

| 狀況 | 怎麼辦 |
|---|---|
| `preflight` 連不上 | 確認以 runner 身分、在 VM 內執行；SQL 防火牆只放行 `snet-app`，從別處連不上是預期（§4.3 末段）。`SQL_ADMIN_PASSWORD` 與 `club.env` 內不一致時重跑 `provision-secrets.sh` |
| `init` 說「不是空的」 | 狀態 `initialized`＝已經做完了，改跑 `verify`；`partial`＝上次中途失敗或有人手動建過表，看輸出判斷，確認是 `init` 中斷後用 `wipe-partial <club\|charity>`（要輸入「WIPE <庫名>」；**有 `__EFMigrationsHistory` 或 `admin_users` 有資料一律拒絕**），再重跑 `init` |
| 驗證有 `[不符]` | 腳本以非零離開、不會自動修。把完整輸出貼給 `backend-engineer`；**不要手動補資料讓它變綠** |
| `create-admin`／`reset-password` 失敗 | 整段在同一交易內，已回滾，修正後直接重跑 |
| `--hash-password` 失敗 | 映像檔不是含此功能的版本——確認 `deploy.yml` 的 `build-api` 已對該 commit 成功、`docker pull` 到最新 |
| Azure SQL 回報 `json` 相關錯誤 | 先查資料庫相容性層級（`SELECT compatibility_level FROM sys.databases`）；本機 SQL Server 2025 在 160 與 170 都能建 `json` 欄位，但正式庫未實測（`docs/20` §5） |

✅ **原生 `json` 不相容已於 2026-10-01 修正（`docs/20` §5、`docs/18` `E-111`）**：營業時間改存 `{"text":"…"}`，其餘 json 欄位寫入端以 `Common/JsonColumn.cs` 守門（純量回 400）。新聞內文 `body` 純文字寫入時包成 `{"text":"…"}`、讀取還原，對外不變，後台新聞照常可儲存；**初始化後請勿用舊版 api 映像檔**（舊版仍會把營業時間寫成字串純量而 500），以 `build-api` 在本修正之後建出的映像檔為準。

⚠️ **這支腳本使用 SQL 管理員帳號**（連線字串來自 `club.env`／`charity.env`），與 `api` 日常連線相同（§8 待決 4：之後建議改用最小權限的資料庫使用者）。

**之後的結構變更不再用這支腳本**，走 §6「資料庫 migration」（`db-migrate.yml`）。分工：`prod-db-init.sh`＝庫完全是空的時候做一次的「創世」；`db-migrate.yml`＝庫已初始化之後的每一支新 migration（它遇到沒有 `__EFMigrationsHistory` 的庫會拒絕並指回這裡）。

### 4.9 站台照片（Blob）

> 🔵 **使用者 2026-10-02 決定**：主站前台約 150 張客戶照片（`apps/web/public/assets/img/`，不納版控，含未成年學員）**不進 repo、不進映像檔**，改由 Azure Blob 提供。前台程式端的改法見 `STATUS.md` S0-9o；本節是上傳與設定。

**契約（前台程式與本節一致，不得單方更改）**

| 項目 | 值 |
|---|---|
| 儲存體 | 俱樂部帳戶 `sttcrfcclub<uniq>`（腳本用 `az` 查出，必須恰好一個） |
| 容器 | `images`（匿名 blob 讀取，見 §2） |
| 物件鍵 | `site/<public/assets/img 底下的相對路徑，副檔名改 .webp>`，例：`hero-01.jpg` → `site/hero-01.webp`；`.svg` 不上傳（隨前台建置） |
| 前台設定 | `NUXT_PUBLIC_MEDIA_BASE_URL` ＝ `https://<帳戶>.blob.core.windows.net/images`（日後可換 CDN 網域）。compose 從 `/opt/tcrfc/.env` 的 **`MEDIA_BASE_URL`** 帶入兩個 Nuxt 前台（主站、藍鯨） |

**處理規則（依主站規劃書 §4.0 圖片上傳通則，與後台「上傳即縮圖」的主檔規則一致）**

1. 依 EXIF 方向轉正（先轉正再去 EXIF，否則方向資訊先被丟掉，照片會躺著）。
2. 長邊超過 **2560px** 才等比縮小，不放大。
3. 去除**全部**中繼資料（EXIF 含 GPS、拍攝裝置與時間、XMP、IPTC、內嵌 ICC）。腳本對每一張輸出立即驗證（WebP 格式、長邊 ≤ 2560、無任何 profile／EXIF），任何一張不過就中止、不上傳。
4. 轉 **WebP 品質 82**（`-define webp:method=6`，壓縮最細；alpha 品質 100）。82 是照片的常用折衷：肉眼幾乎看不出與原圖差異，體積約為原 JPEG 的四成（現有素材約 59 MB → 約 25 MB）。要調整用 `QUALITY=<1–100>`；配方變了，已上傳的物件會因配方標記不同而被重傳。
5. **不做** 1280／640／320 衍生檔與 160px 縮圖：那是後台上傳資料列圖片時的規則（物件鍵由主檔推導）；站台照片是靜態版面素材，一張一個物件，由前台直接引用。

**工具**：容器化的 ImageMagick 7（預設 `dpokidov/imagemagick:latest`，可用 `IM_IMAGE` 換）。你只需要 Docker Desktop 與 `az`，不用另外安裝 ImageMagick。來源資料夾以**唯讀**掛載，腳本不寫、不刪、不改來源檔；處理結果寫在 `$TMPDIR` 下的暫存目錄，結束（含失敗）時一律清除。

**Blob 屬性**：`Content-Type: image/webp`；`Cache-Control: public, max-age=604800`（7 天）。為什麼不是 `immutable` 加一年：物件鍵不含內容雜湊，同一把鍵日後可能被換圖，長快取會讓換圖最久一年看不到；7 天到期後瀏覽器用 ETag 重新驗證（未變動回 304，不重傳）。要讓換圖立即生效，日後走 Cloudflare 時清該網址的快取。

**授權（只做一次）**：預設用你的 `az login` 身分上傳（`--auth-mode login`），需要帳戶上的 **Storage Blob Data Contributor**。訂閱 Owner **沒有**資料平面權限，所以多半要先授權。腳本讀不到容器時會停在上傳前並印出完整指令，形式如下（由你自己執行；角色約 1–5 分鐘生效）：

```bash
SA=$(az storage account list -g rg-tcrfc-prod --query "[?starts_with(name, 'sttcrfcclub')].name" -o tsv)
az role assignment create \
  --assignee "$(az ad signed-in-user show --query id -o tsv)" \
  --role 'Storage Blob Data Contributor' \
  --scope "$(az storage account show -g rg-tcrfc-prod -n "${SA}" --query id -o tsv)"
```

不想授權也可改用帳戶金鑰：`AUTH_MODE=key bash infra/upload-site-images.sh`（az 自己取金鑰，不會印出或進命令列；需要你對帳戶有 listKeys 權限，Owner／Contributor 有）。

**步驟**

```bash
# 1. 先看會傳什麼（不處理、不上傳；不需要 docker）
bash infra/upload-site-images.sh --dry-run

# 2. 實際處理並上傳（約 1–3 分鐘；結尾會匿名 GET 一個物件確認公開可讀）
bash infra/upload-site-images.sh

# 3. 把網址基底寫進 VM 的 /opt/tcrfc/.env（見下），再部署前台
```

照片範圍＝`apps/web/scripts/site-images.txt`（前台實際引用的照片，由前台的 `lint:site-images` 維護）。清單不存在時退回「整個資料夾」並警告——那會把前台沒用到的照片（含未成年學員素材）也傳上去，請先確認清單在。`--all` 強制處理整個資料夾、`--list <檔案>` 指定別的清單、`--force` 無視雲端現況全部重傳。

**重跑**：每個物件的 metadata（`srchash`）記著「處理配方＋來源檔 SHA-256」。重跑時來源與配方都沒變的略過，只傳有變的；中途失敗（網路、權限）直接重跑即可，已成功的不重傳。**腳本永遠不刪雲端物件**：清單縮小或改名後，舊物件留在 `site/` 底下，要清得自己用 `az storage blob delete` 或入口網站刪。

**`MEDIA_BASE_URL` 寫進 VM**：重跑 [`provision-secrets.sh`](provision-secrets.sh)（選擇覆寫 `/opt/tcrfc/.env`）會自動從 `az` 推出預設值並寫入，手改過的值（例如換成 CDN 網域）會沿用。若 `/opt/tcrfc/.env` 已是 `SITE_ENV=production`，該腳本拒絕改它，請手動加一行 `MEDIA_BASE_URL=https://<帳戶>.blob.core.windows.net/images`，再 `docker compose up -d` 重建兩個 Nuxt 容器（這是**執行期**設定，不必重建映像檔）。

**驗證**

```bash
curl -sI "$(grep '^MEDIA_BASE_URL=' /opt/tcrfc/.env | cut -d= -f2-)/site/hero-01.webp" | head -8
# 預期：HTTP/2 200、content-type: image/webp、cache-control: public, max-age=604800
```

**日後被後台內容取代**：站台照片是「上線前讓前台版面有真實照片」的過渡素材。正式內容（球員、新聞、課程等）走後台上傳後，圖片屬於各自的資料列、物件鍵由 `ImageProcessor` 產生（`images/` 下的其他前綴），前台改讀資料列的圖片欄位；對應位置不再引用 `site/…`。屆時 `site/` 底下用不到的物件可手動清除，本腳本與契約不需修改。

**取捨（使用者已接受，2026-10-02）**：`images` 是**匿名公開唯讀**容器——**知道網址就讀得到**，**不需任何帳密**（測試站本身也無帳密，2026-10-02）。物件鍵可預測（如 `site/academy/life-01.webp`），其中含未成年學員照片；容器**不能列舉**，但無法防止有人猜網址或被轉貼。正式上線前若要收緊，做法見 §4.7（Cloudflare 圖片網域）與 §8 風險 1。

**疑難排解**

| 現象 | 原因與處理 |
|---|---|
| `AuthorizationPermissionMismatch`／「You do not have the required permissions」 | 沒有 Storage Blob Data Contributor，照上面授權；剛授權完等幾分鐘再試 |
| `docker 沒有在執行` | 開啟 Docker Desktop。`--dry-run` 不需要 docker |
| 處理失敗：某張「輸出仍含中繼資料」或「長邊超過」 | 不上傳任何東西；把檔名連同訊息回報（來源未被動到） |
| 警告「內嵌色彩設定檔不是 sRGB」 | 去除設定檔後顏色可能偏移；現有素材皆為 sRGB 或無設定檔，出現代表有新素材，請先確認色彩 |
| 匿名 GET 不是 200 | 容器是否匿名 blob 讀取、帳戶是否允許公開網路（§2、§4.7） |
| 前台網址仍是本機路徑 | VM `.env` 沒有 `MEDIA_BASE_URL` 或容器未重建（`docker compose config \| grep MEDIA`） |

## 5. 從測試網址切到正式網址

🔴 **基礎設施一個字都不用改。** Bicep 不含任何網域；IP、NSG、SQL、儲存體在切換前後完全相同。
切換只發生在 VM 的 `/opt/tcrfc/.env`、Cloudflare DNS 與（若有新網域）Caddy 憑證。
完整策略、風險與驗證項在 [`docs/17-deployment.md` §10](../docs/17-deployment.md)（尤其 §10.4 兩層防護、§10.6 主站切換、§10.7 時程表、§9 驗證 13–15），**以那邊為準，不在此重寫**。操作清單：

1. **確認前提**：正式網域的 DNS 控制權已到手（藍鯨 `B-4`、慈善 `B-7`、主站 apex／`www` 決定見 §10.9）；未到位的服務保持暫用網址，可分批切。
2. 在 Cloudflare 為每個**正式網域**新增指向 `pip-tcrfc-prod` IP 的 Proxied **A** 記錄（`stg` 系列先不要刪）。
3. 編輯 VM 上的 `/opt/tcrfc/.env`：把六個 `*_DOMAIN` 改成正式值；**`SITE_ENV=production`**；**註解掉 `CADDYFILE=`**（回到正式版 `deploy/Caddyfile`，拿掉上線前的 `X-Robots-Tag`）。
4. 在 VM 上 `docker compose up -d`（或觸發 `deploy.yml`，若部署段已啟用）重建受影響容器；Caddy 會為新網域自動簽憑證。
5. 驗證（`docs/17` §9 第 15 項）：`robots.txt` 不再是 `Disallow: /`、`<head>` 無 `noindex`、`llms.txt` 可存取、`docker compose config` 顯示 proxy 掛的是 `deploy/Caddyfile`。
6. 兩個後台網址永遠帶 `X-Robots-Tag: noindex, nofollow, noarchive`（`deploy/Caddyfile` 已永久設定），切換後也要確認。
7. 舊 `stg` 子網域：確認無流量後再刪 DNS 記錄（或保留作為內部測試入口由使用者決定）。

---

## 6. 日常維運

### 修改基礎設施

改 `infra/**` → PR（`infra-validate.yml` 自動跑 lint／build）→ 合併 `master` → `infra.yml` 自動 what-if ＋ deploy。
要只預覽：Actions → Infra Deploy → Run workflow → 勾選 **只跑 what-if**。

### 日常部署（CD，2026-10-02 起）

**平常什麼都不用做**：合併／push `master` → `deploy.yml` 在 GitHub 上建置有變動的映像檔 → VM 上的 runner 拉映像檔、`docker compose up -d --wait`、健康檢查 → 失敗自動退回上一版 → 成功才記錄版本。看結果：Actions → Deploy → 最新一次 run → **deploy job 的 Summary**（版本、五個映像檔標籤、健康檢查結果、是否退回、是否清了快取）。

| 想做的事 | 做法 |
|---|---|
| 強制重建並部署全部 | Actions → Deploy → Run workflow（`master`）：五個映像檔全部重建後部署 |
| 手動回滾到某一版 | Actions → **Rollback** → Run workflow，輸入 40 字元 git SHA（`master`）。SHA 從 Actions 的 Deploy 紀錄或 VM 上 `cat /opt/tcrfc/deploy-history.log` 取得（每行：時間、SHA、五個映像檔標籤、`mode`、`result=ok`）。**只退映像檔**，不退 `deploy/`、compose 設定、資料庫；設定有問題用 `git revert` 再 push |
| 看目前部署的版本 | VM：`cat /opt/tcrfc/deploy-state.env` |
| 自動退回也失敗（job 結束碼 2、summary 寫「自動退回也失敗」） | VM 上 `cd /opt/tcrfc/actions-runner/_work/tcrfc/tcrfc`；`docker compose --env-file /opt/tcrfc/.env ps`、`logs <服務>`；依 `deploy-state.env` 帶齊五個 `TAG_*` 再 `docker compose --env-file /opt/tcrfc/.env up -d --pull never`。**不要刪 `/opt/tcrfc/data-protection`** |

**第一次 CD 部署（一次性）**：專案目錄從 `/home/runner/tcrfc-src`（手動起容器時的目錄）換成 runner 的 checkout 目錄，compose 會把**八個容器全部重建一次**（約 1–2 分鐘整站中斷，Redis 快取清空；`api` 金鑰環與 Caddy 憑證 volume 不受影響）。建議離峰時段。首次成功後在 VM 上 `rm -rf /home/runner/tcrfc-src`，**之後不要再從舊目錄執行 `docker compose`**。首次成功前的「手動 `:master` 那一版」只在失敗時以本機 `:cd-prev` 標籤退回，成功後不再可回滾。

**Cloudflare 清快取 token（選配，沒設就略過清快取，不影響部署）**
1. Cloudflare → My Profile → API Tokens → Create Token → Custom：權限 **Zone → Cache Purge → Purge**，Zone Resources 只選 `4webdemo.com`（日後換正式網域再加對應 zone）。
2. GitHub → repo Settings → Environments → `production` → Environment secrets 加 `CLOUDFLARE_API_TOKEN`。
3. 同處 Environment variables 加 `CF_ZONE_ID_TCRFC`／`CF_ZONE_ID_BW`／`CF_ZONE_ID_CHARITY`（zone ID 在 Cloudflare 網域 Overview 右下角；暫用網域期間三個值相同）。
腳本依主機名稱清除（只清 `tcrfc-*.4webdemo.com` 這幾個主機，不動同一 zone 的其他網站），且只在前台映像檔有換時才清。

### 資料庫 migration（`db-migrate.yml`，2026-10-02 起）

> 設計理由、防呆清單、演練結果：[`docs/20`](../docs/20-cicd.md) §5「`db-migrate.yml` 實作」。程式碼：[`db-migrate.yml`](../.github/workflows/db-migrate.yml)、[`deploy/db-migrate.sh`](../deploy/db-migrate.sh)。

**什麼時候用**：改了 Entity、PR 合併進 `master`、其中**帶有新的 migration**（`apps/api/Data/Migrations/` 或 `apps/api/CharityPlatform/Data/Migrations/`）時。**順序永遠是先 migrate、後 deploy**（`docs/20` §5）：`deploy.yml` 只換映像檔、不碰資料庫；結構變更用「展開—收縮」，舊版 api 要能在新結構上跑，因為 `rollback.yml` 也只退映像檔、不退結構。

**一次性前置：建立 `production-db` 環境（🔴 必須在第一次執行前建好）**

GitHub 對「不存在的 environment」會在 job 引用時**自動建立一個沒有任何保護的同名環境**——核准關卡就被靜默繞過。所以先建好；`db-migrate.yml` 的 `preview` job 在 `dry_run=false` 時也會檢查它存在、有 Required reviewers、Deployment branches 僅 `master`，不符就失敗。指令（由 repo 擁有者在自己的電腦執行，需要 repo 管理權限）：

```bash
# 1. 建環境：Required reviewers＝repo 擁有者 waiting0201（user id 5709750；查法 gh api users/waiting0201 --jq .id）。
#    prevent_self_review=false：單人 repo，觸發的人也是核准的人，設 true 就沒有人能核准（有第二位維運者後再改 true）。
#    can_admins_bypass=false：連管理員也不能跳過核准。
gh api -X PUT repos/waiting0201/tcrfc/environments/production-db --input - <<'JSON'
{
  "reviewers": [{ "type": "User", "id": 5709750 }],
  "prevent_self_review": false,
  "can_admins_bypass": false,
  "deployment_branch_policy": { "protected_branches": false, "custom_branch_policies": true }
}
JSON

# 2. Deployment branches 只允許 master
gh api -X POST repos/waiting0201/tcrfc/environments/production-db/deployment-branch-policies -f name=master -f type=branch

# 3. 驗證（預期：reviewers 只有 waiting0201、custom_branch_policies=true、分支清單只有 master）
gh api repos/waiting0201/tcrfc/environments/production-db \
  --jq '{reviewers:[.protection_rules[]|select(.type=="required_reviewers")|.reviewers[].reviewer.login], branch_policy:.deployment_branch_policy}'
gh api repos/waiting0201/tcrfc/environments/production-db/deployment-branch-policies --jq '[.branch_policies[].name]'
```

（`production` 環境已存在，僅 `master`、無 reviewers；`db-migrate.yml` 的唯讀比對 job 掛在它上面。）

**怎麼跑**

1. **先預覽（`dry_run=true`，預設）**：Actions → **DB Migrate** → Run workflow（分支 `master`）→ `target`（`club`／`charity`／`both`）→ 保持勾選 `dry_run`。沒有核准關卡、沒有任何寫入。
2. 看 **「比對正式庫（唯讀）」job 的 Summary**：每個庫的歷史筆數、repo 內 migration 數、**待套用名單**與**每一支的 SQL**。沒有待套用就到此為止。
3. 要真的套用：再跑一次，**取消勾選 `dry_run`**。`apply` job 會停在 **`production-db` 的核准關卡**——Run 頁面出現「Review deployments」→ 勾選 `production-db` → 讀完上面的 SQL → **Approve and deploy**。
4. 核准後 `apply` 會**再比對一次**（名單與你核准時看到的不同就拒絕、什麼都不執行），然後以 sqlcmd 執行**經 SHA-256 鎖定、與預覽相同的** SQL，結束後驗證「歷史表筆數＝repo 筆數、待套用 0」。結果在 `apply` job 的 Summary。
5. 再讓 `deploy.yml` 部署使用新結構的 api。

**不打算核准就按 Reject 或取消那次 run**：`apply` 掛著等核准時占住 `cd-production` 群組，後面的部署會排在它後面。

**核准者要看什麼**：① 待套用名單是不是你預期的那幾支（不多不少）；② SQL 有沒有 `DROP`／`DELETE`／改型別這類破壞性操作（有就確認走了展開—收縮）；③ `target` 是不是對的庫。Azure SQL Basic 層 **PITR 只有 7 天**，這是唯一的救命索，不是盲按。

**結果與行為**

| 情形 | 行為 |
|---|---|
| 沒有待套用 | `apply` 整個跳過，不要求核准；Summary 寫「沒有待套用」 |
| 套用成功 | 每支 migration 各自一個交易；Summary 寫「歷史 N 筆＝repo N 筆」 |
| **套用失敗** | **不自動重試、不自動回復。** 失敗的那一支已被交易回滾；在它之前已提交的維持已套用（不是整批全成全敗）。Summary 貼出 sqlcmd 的錯誤、現況（歷史筆數與仍待套用名單）與下一步 |
| 庫沒初始化（沒有 `__EFMigrationsHistory`） | 拒絕，指回 §4.8 的 `prod-db-init.sh` |
| 歷史表有 repo 不認得的 migration | 拒絕（你可能從舊 commit 觸發，或有人動過歷史表）；用 `master` 最新 commit 重跑 |
| 核准後名單變了 | 拒絕、什麼都沒執行；重新觸發讓核准者重看 |

**失敗怎麼辦**

1. 讀 `apply` job Summary 的錯誤與「目前狀態」。多半是 migration 的 SQL 與正式庫現況不符（記得**正式庫是 DDL 建的、不是 migration 長出來的**，`docs/20` §5「migration 注意事項」）。
2. 修正走 **PR → master → 重新觸發**（冪等：已套用的會自動跳過）。**不要手動改 `__EFMigrationsHistory`**，除非你已確認那支的結果完整存在。
3. 在問題釐清前，**不要部署依賴新結構的 api**；必要時用 `rollback.yml` 退回映像檔（它不動資料庫）。
4. 資料已受損、無法就地修復 → **PITR 還原成新庫**（只能還原成新庫、不能就地覆蓋；Basic 只保留 7 天、備份僅 Local 冗餘，`docs/17` §6）：
   ```bash
   RG=rg-tcrfc-prod
   SQLSRV=$(az sql server list -g $RG --query '[0].name' -o tsv)
   # 還原時間點用 UTC；選「套用 migration 之前」的時間
   az sql db restore -g $RG -s $SQLSRV -n tcrfc_club --dest-name tcrfc_club_restored --time "2026-10-03T01:00:00Z"
   ```
   還原完成後核對資料，再依 `docs/17` §6 切換連線字串（改 VM 上 `/opt/tcrfc/secrets/club.env` 的 `Database=`，重啟 `api`；**LINE Pay 白名單只綁 VM 出口 IP，換庫不影響**）。庫名改變會讓 `db-migrate.sh` 的「庫名必須是 `tcrfc_club`」檢查拒絕——還原後要嘛把舊庫改名、把還原庫改回 `tcrfc_club`，要嘛暫時只靠人工處理，不要放寬腳本的檢查。

**上線後第一次驗證（預期 pending 為零）**：建好 `production-db` 後，先跑一次 `target=both`、`dry_run=true`。正式庫已由 `prod-db-init.sh` 寫入全部 migration（club 21 筆、charity 2 筆），所以 Summary 應顯示兩個庫都是「**待套用 0 支**」、結尾「沒有待套用的 migration」，且**不會出現核准關卡**。這同時驗證了 runner 讀得到 env 檔、容器連得上庫、歷史表解析正常。若出現「待套用」或「歷史表有 repo 不認得的 migration」，先別往下做，看名單判斷是哪邊落後。

**第一次 `dry_run=false` 的注意**：`preview` job 會用 `GITHUB_TOKEN`（`actions: read`）呼叫 `gh api …/environments/production-db` 做守門檢查，這一步**尚未在 GitHub 上實測**；若因 token 權限被誤擋，錯誤訊息會帶 API 回應，確認環境確實建好後再回頭調整 workflow 的 `permissions`。

### 驗證（對照 `docs/17` §9）

```bash
RG=rg-tcrfc-prod
# 第 2 項：Public IP 獨立、Standard、Static
az network public-ip show -g $RG -n pip-tcrfc-prod --query '{sku:sku.name, alloc:publicIPAllocationMethod, ip:ipAddress, nic:ipConfiguration.id}' -o json

# 鎖都在
az lock list -g $RG --query '[].{name:name, level:level}' -o table      # 預期 4 筆 CanNotDelete

# SQL：無 IP 規則、只有 VNet 規則
SQLSRV=$(az sql server list -g $RG --query '[0].name' -o tsv)
az sql server firewall-rule list -g $RG -s $SQLSRV -o table              # 預期空
az sql server vnet-rule list -g $RG -s $SQLSRV -o table                  # 預期 allow-snet-app

# 資料庫：Basic、Local 備份冗餘
az sql db list -g $RG -s $SQLSRV --query '[].{name:name, sku:sku.name, redundancy:requestedBackupStorageRedundancy, maxGB:maxSizeBytes}' -o table

# 儲存體：兩個帳戶都應 publicBlob=true、defaultAction=Allow；用下一行逐容器確認 proposals 為私有
for sa in $(az storage account list -g $RG --query '[].name' -o tsv); do
  az storage account show -g $RG -n $sa --query '{name:name, publicBlob:allowBlobPublicAccess, defaultAction:networkRuleSet.defaultAction, vnetRules:length(networkRuleSet.virtualNetworkRules)}' -o json
done

# 告警與預算
az monitor metrics alert list -g $RG --query '[].{name:name, sev:severity, enabled:enabled}' -o table
```

`docs/17` §9 第 1、3 項（出口 IP 與服務端點後不變）在 VM 上驗：
`docker run --rm curlimages/curl -s https://ifconfig.me`，結果必須等於 Public IP。
第 4 項：從 VM 以外（你的電腦）用同一組連線字串連 SQL，**必須被拒**。

### 回滾

| 情境 | 做法 |
|---|---|
| 一次 infra 變更出問題 | `git revert` 該 commit → push `master` → 管線以 Incremental 重新套用舊內容。**注意**：舊模板沒有的資源不會被刪，新增的資源要手動清掉（先移除鎖） |
| VM 壞掉／要重建 | Public IP 與 NIC 是獨立資源、OS 磁碟 `deleteOption: Detach`：刪 VM 後重新部署即可掛回同一個 IP。**IP 不變，LINE Pay 白名單不受影響**。重建後要重做 §4（cloud-init 自動、runner 註冊手動、機密檔與金鑰環目錄重跑 `provision-secrets.sh`；**金鑰環要先從備份還原**，否則已加密資料永久無法解密，§4.3「金鑰環」） |
| 資料庫誤刪／損毀 | PITR 還原成新庫（`tcrfc_club_restored`），再切連線字串，詳見 `docs/17` §6「備份與還原」。Basic 只保留 7 天，**且備份僅 Local 冗餘（無異地）** |
| 誤刪 Blob | 軟刪除／版本控制 14 天內可還原（入口網站或 `az storage blob undelete`） |

### 觀測

| 想看什麼 | 去哪 |
|---|---|
| 告警（資料空間、CPU 額度） | Azure Portal → Monitor → Alerts；通知寄到 `ALERT_EMAIL` |
| VM CPU 額度曲線 | `vm-tcrfc-prod` → Monitoring → Metrics → `CPU Credits Remaining`／`CPU Credits Consumed`。不夠就換 `D2s_v5`（`docs/17` §1） |
| SQL 儲存與 DTU | `tcrfc_club`／`tcrfc_charity` → Monitoring → Metrics → `Data space used`、`DTU percentage` |
| 費用與預算 | Cost Management → Budgets → `budget-tcrfc-prod-monthly`；Cost analysis 篩選資源群組 |
| VM 開機問題 | `vm-tcrfc-prod` → Boot diagnostics；VM 內 `/var/log/cloud-init-output.log` |
| 部署紀錄 | Actions → Infra Deploy（基礎設施）、Deploy（應用程式，看 job Summary）、Rollback；Azure：資源群組 → Deployments；VM：`/opt/tcrfc/deploy-history.log` |
| 容器 log | VM 上 `docker compose logs <service>`（已設輪替，每容器最多約 30 MB） |

---

## 7. 疑難排解

| 症狀 | 原因與處理 |
|---|---|
| `azure/login` 失敗 `AADSTS700213`／`No matching federated identity record` | subject 不符。本 repo 啟用 GitHub「不可變 subject」，格式帶 owner／repo 的數字 ID（查：`gh api repos/waiting0201/tcrfc/actions/oidc/customization/sub` 的 `sub_claim_prefix`）；`bootstrap.sh` 會自動查並修正。另確認 job 有 `environment: production`、repo 大小寫與 `repo:waiting0201@5709750/tcrfc@1334739698:environment:production` 完全一致；從非 `master` 分支跑會被 Environment 分支限制擋掉 |
| `AuthorizationFailed` ... `Microsoft.Authorization/locks/write` | 步驟 4(b) 的自訂角色沒指派或還沒生效（等 1–2 分鐘） |
| `AuthorizationFailed` ... `Microsoft.Consumption/budgets/write` | 補指派 `Cost Management Contributor`（資源群組範圍） |
| `MissingSubscriptionRegistration` | 步驟 1 沒做完或某個命名空間漏了 |
| 編譯期 `BCP427 Environment variable ... does not exist` | 該 secret／variable 沒填（`SSH_ALLOWED_CIDR`、`SSH_PUBLIC_KEY` 等）。workflow 第一步的檢查會先列出缺的 |
| `SkuNotAvailable`／配額不足（`Standard_B2ms` 於 Japan East） | 該訂閱該區域 B 系列 vCPU 配額不足或暫時缺貨：到 Quotas 提高配額，或與使用者討論換規格 |
| `Changing property 'osProfile.customData' is not allowed`（或 `linuxConfiguration.ssh.publicKeys`） | osProfile 建立後不可變（見 `modules/compute.bicep` 檔頭）。要改只能重建 VM；已運作的 VM 要加 SSH 金鑰請登入後改 `authorized_keys` |
| 名稱已被使用（SQL／儲存體） | 全球唯一名稱撞名；`uniqueString` 後綴是依資源群組 ID 決定的，換資源群組會換後綴 |
| 要刪被鎖的資源失敗 `ScopeLocked` | 預期行為。由擁有者 `az lock delete --name <lock> --resource-group rg-tcrfc-prod --resource <…>` 移除後再刪，**刪 Public IP 前務必確認 LINE Pay 白名單的處理** |
| 從瀏覽器開圖片網址 `AuthorizationFailure` | 見 §8 風險 1（儲存體僅允許 snet-app） |
| 部署成功但 VM 上沒有 docker | `cloud-init status` 與 `/var/log/cloud-init-output.log`；常見是出站被擋或 apt 暫時失敗，修正後可手動 `sudo cloud-init clean --logs && sudo reboot` 重跑 |

---

## 8. 風險、成本與待決

### 風險

1. 🔴 **圖片與文件改為公開容器（使用者 2026-10-01 決定「公開容器＋Cloudflare」）的取捨**：
   - 俱樂部帳戶 `images`／`videos`／`documents` 與慈善帳戶 `charity-images` 為匿名 blob 讀取；**`proposals` 維持私有**。
   - 匿名存取要成立，**帳戶防火牆必須允許公開網路**，所以兩個儲存體帳戶**不再有 VNet 層的隔離**（`snet-app` 的服務端點與 VNet 規則在儲存體這邊實質上失效，Bicep 仍保留端點無害）。
   - **`proposals`（贊助提案 PDF）的保護因此只剩兩件事**：容器無匿名存取、**共用金鑰（連線字串）不外洩**。`apps/api` 目前以連線字串存取（`AZURE_BLOB_CONNECTION_STRING*`），所以帳戶維持 `allowSharedKeyAccess=true`；改用 Managed Identity 需要改程式與給 VM 指派身分，**本次不做、維持連線字串**，列為後續強化。連線字串只放 VM 的 `club.env`／`charity.env`，一旦外洩要立刻輪替金鑰。
   - 慈善：`CharityPublicCatalog` 會回傳 `charity-images` 的圖片網址給公開頁面（專案封面、店家 Logo），所以同樣公開。**慈善後台上傳的圖片一律視為公開素材**，不得上傳不能公開的東西（例如收據、證件）。
   - 匿名可讀的 blob 網址只要知道完整路徑就讀得到（物件鍵含 GUID、不可猜，但不是機密）；不能列舉。
2. **資料庫備份只有 Local 冗餘、PITR 7 天、無 LTR**（使用者決定）：區域性災難或發現太晚的誤刪無法回復。
3. **Data Protection 金鑰環在 VM 磁碟上**（`/opt/tcrfc/data-protection` bind mount，`docs/17` §5、本檔 §4.3「金鑰環」）：OS 磁碟為單點，遺失＝已加密資料（身分證字號、載具號碼、2FA、推播權杖、商店與慈善的金流／發票憑證）無法解密。**磁碟層沒有自動備份、金鑰每 90 天輪替**，需定期手動備份（待決：是否改自動化）。
4. **預算 100 可能偏緊**（見下方成本）。

### 成本概估（粗估，單位 US$／月，以 Azure 定價計算機核實）

| 項目 | 約略 |
|---|---|
| VM `Standard_B2ms`（Japan East，Linux，常駐） | 70–80 |
| Premium SSD 64 GB（P6） | 9–11 |
| Standard 靜態 Public IP | 3–4 |
| Azure SQL Basic × 2 | 約 10（各約 5） |
| 儲存體 × 2、Log／指標 | 1–5（依用量） |
| **合計** | **約 95–110** |

⚠️ 這個量級**貼近甚至略超過 US$100 預算**，預期 80% 通知會在月中就觸發；預算金額以帳單幣別計；**已確認為美元**，維持 100。
可優化：用 1 年或 3 年 Reserved Instance／Savings Plan 降低 VM 成本、Public IP 無法省。

### 待決（需要使用者決定）

1. ~~`DB_COLLATION`~~ ✅ 2026-10-01 定案 `SQL_Latin1_General_CP1_CI_AS`，寫死在 `main.bicepparam`。
2. ~~公開圖片配送方式~~ ✅ 已決定（公開容器＋Cloudflare），**待做**：Cloudflare 端設定與在 env 檔填入公開網址基底（`apps/api` 程式已完成），見 §4.7。
3. ~~預算金額與帳單幣別~~ ✅ 已確認帳單幣別為美元，預算維持 100。
4. **`api` 使用的資料庫帳號**：目前文件預設以 SQL 管理員連線；建議另建最小權限的資料庫使用者（`docs/20` §7.2 未定案）。
5. **Data Protection 金鑰環的備份方式**：見風險 3 與 §4.3「金鑰環」（現行建議每季手動 tar，是否自動化待決）。
6. **失敗通知管道**（`docs/20` §8，沿用既有待決）：infra workflow 失敗時目前只有 GitHub 預設的 Email 通知。
