// infra/main.bicep — TCRFC 正式環境的 Azure 基礎設施（docs/17「基礎設施即程式碼」）
//
// 範圍：resource group（rg-tcrfc-prod）。資源群組本身由人工一次性建立（infra/README.md 步驟 1），
//   理由：部署身分只授權到該資源群組（最小權限），不需要訂閱層級的權限。
// 部署模式：一律 Incremental（az deployment group 預設值，workflow 仍明確指定）。
//   ⛔ 絕對不要用 Complete mode——它會刪除群組內「模板沒寫」的資源（例如手動建的 managed identity）。
// 🔴 本檔不寫死任何網域。網域只存在 VM 的 /opt/tcrfc/.env（docs/17 §10）。
//
// 不開的資源（刻意）：ACR（映像檔放 GHCR）、Key Vault（機密放 VM 本機 env 檔）、Azure Cache for Redis（Redis 是 compose 容器）、
//   Private Endpoint（用免費的 Service Endpoint）、staging 環境（全專案只有本機與正式兩套）。

targetScope = 'resourceGroup'

// ── 一般參數（有預設值，通常不用動）────────────────────────────────────────
@description('部署區域')
param location string = resourceGroup().location

@description('名稱前綴')
param namePrefix string = 'tcrfc'

@description('環境代號（全專案只有一套正式環境）')
param environmentName string = 'prod'

@description('VNet 位址空間')
param vnetAddressPrefix string = '10.20.0.0/16'

@description('snet-app 位址範圍')
param subnetPrefix string = '10.20.1.0/24'

@description('VM 規格')
param vmSize string = 'Standard_B2ms'

@description('VM 管理員使用者名稱')
param vmAdminUsername string = 'azureuser'

@description('OS 磁碟大小 GB（Premium SSD）')
param osDiskSizeGb int = 64

@description('SQL 管理員登入名稱')
param sqlAdminLogin string = 'tcrfcsqladmin'

@description('資料庫名稱：俱樂部')
param clubDatabaseName string = 'tcrfc_club'

@description('資料庫名稱：慈善（獨立資料庫）')
param charityDatabaseName string = 'tcrfc_charity'

@description('VM CPU Credits Remaining 低於此值告警。100 是保守起點，上線後依實際曲線調整')
param cpuCreditsLowThreshold int = 100

@description('每月預算金額（帳單幣別；帳單幣別非 USD 時請換算）')
param budgetAmount int = 100

@description('預算起算日（必須是 1 日）')
param budgetStartDate string = '2026-10-01'

@description('Cloudflare IPv4 段。來源 https://www.cloudflare.com/ips-v4 ；Cloudflare 變更時要同步更新')
param cloudflareCidrs array

// ── 需要使用者提供的值（不進版控，由 main.bicepparam 從環境變數讀入）──────────
@description('SSH 22 允許的來源 CIDR，例如 x.x.x.x/32')
param sshAllowedCidr string

@description('VM 的 SSH 公鑰')
param sshPublicKey string

@secure()
@description('SQL 管理員密碼')
param sqlAdminPassword string

@description('Entra 管理員顯示名稱；空字串＝不設定')
param entraAdminLogin string = ''

@description('Entra 管理員物件 ID；空字串＝不設定')
param entraAdminObjectId string = ''

@description('資料庫定序——🔴 建庫後不可改。使用者 2026-10-01 定案 SQL_Latin1_General_CP1_CI_AS（由 main.bicepparam 帶入）')
param databaseCollation string

@description('告警與預算通知的收件 Email')
param alertEmail string

// ── 命名 ───────────────────────────────────────────────────────────────────
var suffix = '${namePrefix}-${environmentName}'
// 全球唯一名稱（SQL 伺服器、儲存體）加上由資源群組 ID 決定的穩定後綴
var uniq = take(uniqueString(resourceGroup().id), 6)

module network 'modules/network.bicep' = {
  name: 'network'
  params: {
    location: location
    vnetName: 'vnet-${suffix}'
    vnetAddressPrefix: vnetAddressPrefix
    subnetPrefix: subnetPrefix
    nsgName: 'nsg-${suffix}-app'
    publicIpName: 'pip-${suffix}'
    cloudflareCidrs: cloudflareCidrs
    sshAllowedCidr: sshAllowedCidr
  }
}

module compute 'modules/compute.bicep' = {
  name: 'compute'
  params: {
    location: location
    vmName: 'vm-${suffix}'
    nicName: 'nic-${suffix}-vm'
    vmSize: vmSize
    adminUsername: vmAdminUsername
    sshPublicKey: sshPublicKey
    osDiskSizeGb: osDiskSizeGb
    subnetId: network.outputs.subnetId
    publicIpId: network.outputs.publicIpId
  }
}

module sql 'modules/sql.bicep' = {
  name: 'sql'
  params: {
    location: location
    serverName: 'sql-${suffix}-${uniq}'
    sqlAdminLogin: sqlAdminLogin
    sqlAdminPassword: sqlAdminPassword
    entraAdminLogin: entraAdminLogin
    entraAdminObjectId: entraAdminObjectId
    subnetId: network.outputs.subnetId
    databaseCollation: databaseCollation
    clubDatabaseName: clubDatabaseName
    charityDatabaseName: charityDatabaseName
  }
}

// 俱樂部儲存體：容器名稱對應 apps/api 的 AZURE_BLOB_CONTAINER_IMAGES／VIDEOS／DOCUMENTS／PROPOSALS 預設值。
// 使用者 2026-10-01 決定：images／videos／documents 匿名讀取（前面接 Cloudflare），proposals 私有
// （贊助提案 PDF 只能經 API 串流，docs/17 §6）。取捨見 modules/storage.bicep 與 infra/README.md §8。
module storageClub 'modules/storage.bicep' = {
  name: 'storage-club'
  params: {
    location: location
    accountName: 'st${namePrefix}club${uniq}'
    publicContainerNames: [
      'images'
      'videos'
      'documents'
    ]
    containerNames: [
      'proposals'
    ]
    subnetId: network.outputs.subnetId
  }
}

// 慈善獨立儲存體（使用者 2026-10-01 決定另開，比照資料庫的獨立原則）。
// 容器名稱對應 apps/api 的 AZURE_BLOB_CONTAINER_CHARITY 預設值 charity-images。
module storageCharity 'modules/storage.bicep' = {
  name: 'storage-charity'
  params: {
    location: location
    accountName: 'st${namePrefix}charity${uniq}'
    // 慈善前台公開頁會顯示專案封面與店家 Logo（CharityPublicCatalog 回傳圖片網址），故公開讀取
    publicContainerNames: [
      'charity-images'
    ]
    containerNames: []
    subnetId: network.outputs.subnetId
  }
}

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    namePrefix: suffix
    alertEmail: alertEmail
    databaseIds: [
      sql.outputs.clubDatabaseId
      sql.outputs.charityDatabaseId
    ]
    databaseNames: [
      clubDatabaseName
      charityDatabaseName
    ]
    vmId: compute.outputs.vmId
    cpuCreditsLowThreshold: cpuCreditsLowThreshold
    budgetAmount: budgetAmount
    budgetStartDate: budgetStartDate
  }
}

// ── 輸出（不含機密）。🔴 workflow 不會把輸出印到 log（公開 repo 的 log 人人可看），請到入口網站或用 az 查 ──
output vmName string = compute.outputs.vmName
output sqlServerFqdn string = sql.outputs.serverFqdn
output storageClubName string = storageClub.outputs.accountName
output storageCharityName string = storageCharity.outputs.accountName
output storageClubBlobEndpoint string = storageClub.outputs.blobEndpoint
output storageCharityBlobEndpoint string = storageCharity.outputs.blobEndpoint
