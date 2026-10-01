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
| [`cloud-init.yaml`](cloud-init.yaml) | VM 首次開機：安裝 Docker ＋ Compose、建 `runner` 使用者與 `/opt/tcrfc/` 目錄 |
| [`modules/network.bicep`](modules/network.bicep) | VNet、`snet-app`、NSG、靜態 Public IP（含鎖） |
| [`modules/compute.bicep`](modules/compute.bicep) | NIC、VM |
| [`modules/sql.bicep`](modules/sql.bicep) | Azure SQL 伺服器 ＋ 兩個 Basic 資料庫（含鎖） |
| [`modules/storage.bicep`](modules/storage.bicep) | 儲存體帳戶（俱樂部、慈善各呼叫一次，含鎖） |
| [`modules/monitoring.bicep`](modules/monitoring.bicep) | Action Group、三個指標告警、月預算 |
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
| 告警 | `alert-tcrfc-prod-tcrfc_club-storage-1_5gb`、`alert-tcrfc-prod-tcrfc_charity-storage-1_5gb`、`alert-tcrfc-prod-vm-cpu-credits-low` | 資料空間 ≥ 1.5 GB（`storage` 指標）；CPU Credits Remaining < 100（可調） |
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

SQL 管理員密碼請另存密碼管理器——之後 `api` 的連線字串要用到（步驟 7）。

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
> 此 runner 日後給 `deploy.yml`／`db-migrate.yml`／`rollback.yml` 用（`docs/20` §9 第 1 項）。

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

### 4.3 建機密檔 `/opt/tcrfc/secrets/*.env`

> 變數清單與持有人見 [`docs/20-cicd.md`](../docs/20-cicd.md) §7.2；實際命名以 [`apps/api/README.md`](../apps/api/README.md)
> 與 [`deploy/dev/club.env.example`](../deploy/dev/club.env.example)／[`charity.env.example`](../deploy/dev/charity.env.example) 為準。
> 🔴 **俱樂部與協會的憑證分兩個檔案**，權限 `600`、擁有者 `runner`，**不進 git、不貼進任何對話或 log**。

連線字串的來源（在**自己的電腦**用 `az` 取，不要印在公開的地方）：

| 變數 | 來源 |
|---|---|
| `CLUB_SQL_CONNECTION_STRING`（`club.env`） | `Server=tcp:<SQL FQDN>,1433;Database=tcrfc_club;User ID=<sqlAdminLogin>;Password=<SQL_ADMIN_PASSWORD>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;`。FQDN 見部署輸出 `sqlServerFqdn` |
| `CHARITY_SQL_CONNECTION_STRING`（`charity.env`） | 同上，`Database=tcrfc_charity` |
| `AZURE_BLOB_CONNECTION_STRING`（`club.env`） | `az storage account show-connection-string -g rg-tcrfc-prod -n sttcrfcclub<uniq> --query connectionString -o tsv` |
| `AZURE_BLOB_CONNECTION_STRING_CHARITY`（`charity.env`） | 同上，帳戶換成 `sttcrfccharity<uniq>`；容器預設 `charity-images`（`AZURE_BLOB_CONTAINER_CHARITY` 不必設） |
| `AZURE_BLOB_CONTAINER_*`（`club.env`） | 預設值 `images`／`videos`／`documents`／`proposals` 與 Bicep 建的容器一致，不必設 |

在 VM 上建檔（編輯時不要讓值進 shell history）：

```bash
sudo -iu runner
umask 077
nano /opt/tcrfc/secrets/club.env      # 貼上；存檔
nano /opt/tcrfc/secrets/charity.env
chmod 600 /opt/tcrfc/secrets/*.env && ls -l /opt/tcrfc/secrets
```

⚠️ 兩個資料庫的 SQL 防火牆只放行 `snet-app`：**從你的電腦（含 SSMS／Azure Data Studio）連不上是預期行為**
（`docs/17` §9 驗證 4）。首次建庫（`db/club-schema.sql`／`db/charity-schema.sql`，`docs/20` §9 第 8 項）要從 VM 上執行，
例如在 VM 起一個含 `sqlcmd` 的容器。管理用的 SQL 管理員帳號只用於建庫與緊急處理；
`api` 日常連線改用權限較小的資料庫使用者是建議的後續強化（目前文件未定案，列於 §9 待決 4）。

### 4.4 建 compose 用的 `.env`

範本是 [`/.env.example`](../.env.example)：複製成 VM 上的 `.env`，填 `GHCR_OWNER`、`IMAGE_TAG`、六個 `*_DOMAIN`、
`SITE_ENV`、`CADDYFILE`、`ACME_EMAIL`、`PRELAUNCH_BASIC_AUTH_*`、`REDIS_PASSWORD`。
（不要填 `MSSQL_DEV_SA_PASSWORD`，那只給本機開發。）

⚠️ **`.env` 要放在哪、deploy job 怎麼讀到它**，`docs/20` §9a「CD 段還缺什麼」尚未定案
（`deploy.yml` 部署段目前 `if: false`）。暫定放 `/opt/tcrfc/.env`，CD 段實作時以 `--env-file /opt/tcrfc/.env`
或複製進 checkout 目錄處理，屆時回頭更新本節。

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
- 上線前三層防護（Basic Auth、`noindex`、後台 Cloudflare Access）見 `docs/17` §10.4，**DNS 一指過去就要先到位**。

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

## 5. 從測試網址切到正式網址

🔴 **基礎設施一個字都不用改。** Bicep 不含任何網域；IP、NSG、SQL、儲存體在切換前後完全相同。
切換只發生在 VM 的 `/opt/tcrfc/.env`、Cloudflare DNS 與（若有新網域）Caddy 憑證。
完整策略、風險與驗證項在 [`docs/17-deployment.md` §10](../docs/17-deployment.md)（尤其 §10.4 三層防護、§10.6 主站切換、§10.7 時程表、§9 驗證 13–15），**以那邊為準，不在此重寫**。操作清單：

1. **確認前提**：正式網域的 DNS 控制權已到手（藍鯨 `B-4`、慈善 `B-7`、主站 apex／`www` 決定見 §10.9）；未到位的服務保持暫用網址，可分批切。
2. 在 Cloudflare 為每個**正式網域**新增指向 `pip-tcrfc-prod` IP 的 Proxied **A** 記錄（`stg` 系列先不要刪）。
3. 編輯 VM 上的 `/opt/tcrfc/.env`：把六個 `*_DOMAIN` 改成正式值；**`SITE_ENV=production`**；**註解掉 `CADDYFILE=`**（回到正式版 `deploy/Caddyfile`，拿掉 Basic Auth）。
4. 在 VM 上 `docker compose up -d`（或觸發 `deploy.yml`，若部署段已啟用）重建受影響容器；Caddy 會為新網域自動簽憑證。
5. 驗證（`docs/17` §9 第 15 項）：`robots.txt` 不再是 `Disallow: /`、`<head>` 無 `noindex`、`llms.txt` 可存取、`docker compose config` 顯示 proxy 掛的是 `deploy/Caddyfile`。
6. 兩個後台網址永遠帶 `X-Robots-Tag: noindex, nofollow, noarchive`（`deploy/Caddyfile` 已永久設定），切換後也要確認。
7. 舊 `stg` 子網域：確認無流量後再刪 DNS 記錄（或保留作為內部測試入口並維持 Basic Auth，由使用者決定）。

---

## 6. 日常維運

### 修改基礎設施

改 `infra/**` → PR（`infra-validate.yml` 自動跑 lint／build）→ 合併 `master` → `infra.yml` 自動 what-if ＋ deploy。
要只預覽：Actions → Infra Deploy → Run workflow → 勾選 **只跑 what-if**。

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
| VM 壞掉／要重建 | Public IP 與 NIC 是獨立資源、OS 磁碟 `deleteOption: Detach`：刪 VM 後重新部署即可掛回同一個 IP。**IP 不變，LINE Pay 白名單不受影響**。重建後要重做 §4（cloud-init 自動、runner 註冊與機密檔手動） |
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
| 部署紀錄 | Actions → Infra Deploy；Azure：資源群組 → Deployments |
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
3. **Data Protection 金鑰環在 VM 磁碟上**（`DATA_PROTECTION_KEYS_PATH` 的 volume，`docs/17` §5）：OS 磁碟為單點，遺失＝已加密資料（身分證字號、載具號碼、2FA、推播權杖）無法解密。**磁碟層沒有自動備份**，建議另行備份該 volume（待決）。
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
5. **Data Protection 金鑰環的備份方式**：見風險 3。
6. **失敗通知管道**（`docs/20` §8，沿用既有待決）：infra workflow 失敗時目前只有 GitHub 預設的 Email 通知。
