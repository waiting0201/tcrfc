// modules/sql.bicep — Azure SQL 邏輯伺服器 ＋ 兩個 Basic 資料庫（docs/17 §2、§6）
//
// 網路：只開 VNet 規則（snet-app 的 Service Endpoint），不開任何公開 IP 規則，也不開「Allow Azure services」。
//   ⚠️ 服務端點的運作前提是 publicNetworkAccess=Enabled（Disabled 代表只剩 Private Endpoint）；
//      「Enabled」在這裡的實際效果是：只有防火牆規則（此處只有 VNet 規則）放行的來源連得進來。
// 備份：Basic、PITR 7 天、備份儲存冗餘 Local（使用者 2026-10-01 決定不做異地）、不設 LTR。
// 🔴 兩個資料庫是兩個獨立單庫，不啟用 Elastic Query／External Table（docs/17 §5 的硬邊界）。

@description('部署區域')
param location string

@description('SQL 邏輯伺服器名稱（全球唯一）')
param serverName string

@description('SQL 管理員登入名稱')
param sqlAdminLogin string

@secure()
@description('SQL 管理員密碼')
param sqlAdminPassword string

@description('Entra 管理員的顯示名稱（UPN 或群組名稱）；空字串＝不設定 Entra 管理員')
param entraAdminLogin string

@description('Entra 管理員的物件 ID；空字串＝不設定 Entra 管理員')
param entraAdminObjectId string

@description('snet-app 的資源 ID')
param subnetId string

@description('資料庫定序——🔴 建庫後不可改')
param databaseCollation string

@description('資料庫名稱：俱樂部')
param clubDatabaseName string

@description('資料庫名稱：慈善')
param charityDatabaseName string

var databases = [
  clubDatabaseName
  charityDatabaseName
]

resource server 'Microsoft.Sql/servers@2023-08-01' = {
  name: serverName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    restrictOutboundNetworkAccess: 'Disabled'
  }
}

// Entra 管理員（可選）。azureADOnlyAuthentication 維持 false：api 以 SQL 驗證連線字串連入（docs/20 §7.2）。
resource entraAdmin 'Microsoft.Sql/servers/administrators@2023-08-01' = if (!empty(entraAdminObjectId) && !empty(entraAdminLogin)) {
  parent: server
  name: 'ActiveDirectory'
  properties: {
    administratorType: 'ActiveDirectory'
    login: entraAdminLogin
    sid: entraAdminObjectId
    tenantId: tenant().tenantId
  }
}

// 只允許 snet-app。ignoreMissingVnetServiceEndpoint=false：子網沒有 Microsoft.Sql 端點就讓部署失敗，不要靜默通過。
resource vnetRule 'Microsoft.Sql/servers/virtualNetworkRules@2023-08-01' = {
  parent: server
  name: 'allow-snet-app'
  properties: {
    virtualNetworkSubnetId: subnetId
    ignoreMissingVnetServiceEndpoint: false
  }
}

resource dbs 'Microsoft.Sql/servers/databases@2023-08-01' = [
  for dbName in databases: {
    parent: server
    name: dbName
    location: location
    sku: {
      name: 'Basic'
      tier: 'Basic'
      capacity: 5
    }
    properties: {
      collation: databaseCollation
      maxSizeBytes: 2147483648 // 2 GB（Basic 硬上限，寫滿即寫入失敗——儲存告警在 1.5 GB，見 monitoring.bicep）
      requestedBackupStorageRedundancy: 'Local'
      zoneRedundant: false
      readScale: 'Disabled'
    }
  }
]

// PITR 保留 7 天（Basic 上限）。LTR 不設（使用者決定）。
resource pitr 'Microsoft.Sql/servers/databases/backupShortTermRetentionPolicies@2023-08-01' = [
  for (dbName, i) in databases: {
    parent: dbs[i]
    name: 'default'
    properties: {
      retentionDays: 7
    }
  }
]

resource serverLock 'Microsoft.Authorization/locks@2020-05-01' = {
  name: 'lock-${serverName}'
  scope: server
  properties: {
    level: 'CanNotDelete'
    notes: '含兩個正式資料庫（子資源同受保護）。要刪請先由擁有者移除此鎖，並確認 PITR 還原點已另行處理。'
  }
}

output serverId string = server.id
output serverFqdn string = server.properties.fullyQualifiedDomainName
output clubDatabaseId string = dbs[0].id
output charityDatabaseId string = dbs[1].id
