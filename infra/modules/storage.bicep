// modules/storage.bicep — 一個儲存體帳戶（供俱樂部與慈善各呼叫一次）
//
// 兩種模式（由 publicContainerNames 是否為空決定）：
//   · 沒有公開容器：allowBlobPublicAccess=false、網路預設拒絕、只放行 snet-app（VNet 規則）。
//   · 有公開容器（使用者 2026-10-01 決定「公開容器＋Cloudflare」）：帳戶 allowBlobPublicAccess=true，
//     只有 publicContainerNames 內的容器設 publicAccess=Blob（可憑完整網址匿名讀單一 blob，**不能列舉容器**），
//     其餘容器維持私有。🔴 匿名存取要成立，儲存體防火牆必須允許公開網路（defaultAction=Allow），
//     所以 VNet 規則在這個模式下不再提供網路層隔離；私有容器的保護只剩「沒有匿名存取 ＋ 共用金鑰保密」。
// 軟刪除與版本控制 14 天：blob 軟刪除、容器軟刪除、版本控制，並以生命週期規則在 14 天後清除舊版本
// （版本控制本身不會自動過期，不加規則舊版本會無限累積計費）。

@description('部署區域')
param location string

@description('儲存體帳戶名稱（3–24 碼小寫英數，全球唯一）')
param accountName string

@description('私有容器名稱清單（publicAccess=None）')
param containerNames array

@description('公開讀取的容器名稱清單（publicAccess=Blob，只能憑網址讀單一 blob，不能列舉）；空陣列＝帳戶完全私有')
param publicContainerNames array = []

@description('snet-app 的資源 ID')
param subnetId string

@description('軟刪除與版本保留天數')
param retentionDays int = 14

var hasPublicContainers = !empty(publicContainerNames)

resource account 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: accountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: hasPublicContainers
    // 共用金鑰維持開啟：api 以連線字串存取（AZURE_BLOB_CONNECTION_STRING*，docs/20 §7.2）
    allowSharedKeyAccess: true
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    publicNetworkAccess: 'Enabled' // 服務端點需要；實際放行由下方 networkAcls 決定
    networkAcls: {
      defaultAction: hasPublicContainers ? 'Allow' : 'Deny'
      bypass: 'AzureServices'
      ipRules: []
      virtualNetworkRules: [
        {
          id: subnetId
          action: 'Allow'
        }
      ]
    }
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: account
  name: 'default'
  properties: {
    isVersioningEnabled: true
    deleteRetentionPolicy: {
      enabled: true
      days: retentionDays
    }
    containerDeleteRetentionPolicy: {
      enabled: true
      days: retentionDays
    }
  }
}

resource containers 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = [
  for name in containerNames: {
    parent: blobService
    name: name
    properties: {
      publicAccess: 'None'
    }
  }
]

resource publicContainers 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = [
  for name in publicContainerNames: {
    parent: blobService
    name: name
    properties: {
      publicAccess: 'Blob'
    }
  }
]

resource lifecycle 'Microsoft.Storage/storageAccounts/managementPolicies@2023-05-01' = {
  parent: account
  name: 'default'
  properties: {
    policy: {
      rules: [
        {
          name: 'delete-old-versions'
          enabled: true
          type: 'Lifecycle'
          definition: {
            filters: {
              blobTypes: [
                'blockBlob'
              ]
            }
            actions: {
              version: {
                delete: {
                  daysAfterCreationGreaterThan: retentionDays
                }
              }
            }
          }
        }
      ]
    }
  }
}

resource accountLock 'Microsoft.Authorization/locks@2020-05-01' = {
  name: 'lock-${accountName}'
  scope: account
  properties: {
    level: 'CanNotDelete'
    notes: '正式環境的物件儲存（圖片／影片／文件）。要刪請先由擁有者移除此鎖。'
  }
}

output accountId string = account.id
output accountName string = account.name
output blobEndpoint string = account.properties.primaryEndpoints.blob
