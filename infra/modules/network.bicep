// modules/network.bicep — VNet、snet-app、NSG、靜態 Public IP（docs/17 §2）
//
// 🔴 Public IP 是「獨立資源」，與 VM 生命週期解耦：換 VM、重建 VM 都重新掛同一個 IP。
//    LINE Pay 白名單綁的就是它，換 IP ＝ 改白名單 ＝ 停機事件（docs/17 §2、§7 風險 4）。
//    因此另外加 CanNotDelete 鎖（lock-pip-*）。

@description('部署區域')
param location string

@description('VNet 名稱')
param vnetName string

@description('VNet 位址空間')
param vnetAddressPrefix string

@description('應用子網 snet-app 的位址範圍')
param subnetPrefix string

@description('NSG 名稱')
param nsgName string

@description('Public IP 名稱')
param publicIpName string

@description('入站 80／443 允許的來源（Cloudflare IPv4 段）。來源：https://www.cloudflare.com/ips-v4')
param cloudflareCidrs array

@description('入站 SSH 22 允許的來源 CIDR（單一固定來源，例如 x.x.x.x/32）')
param sshAllowedCidr string

// ── NSG ─────────────────────────────────────────────────────────────────────
// 入站只有三條允許規則 ＋ 一條明確的全拒絕。出站維持 Azure 預設（允許），
// 因為 VM 要連 LINE Pay、GitHub、ghcr.io、Let's Encrypt、Azure SQL／Storage。
resource nsg 'Microsoft.Network/networkSecurityGroups@2024-05-01' = {
  name: nsgName
  location: location
  properties: {
    securityRules: [
      {
        name: 'allow-https-from-cloudflare'
        properties: {
          priority: 100
          direction: 'Inbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourceAddressPrefixes: cloudflareCidrs
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '443'
        }
      }
      {
        // 80：Caddy 的 HTTP→HTTPS 轉址與 ACME HTTP-01 驗證（經 Cloudflare 轉進來，見 deploy/Caddyfile）
        name: 'allow-http-from-cloudflare'
        properties: {
          priority: 110
          direction: 'Inbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourceAddressPrefixes: cloudflareCidrs
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '80'
        }
      }
      {
        // SSH 僅限人工緊急登入。部署走 self-hosted runner（VM 主動連 GitHub），CI 不需要這條（docs/20 §4）
        name: 'allow-ssh-from-admin'
        properties: {
          priority: 200
          direction: 'Inbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourceAddressPrefix: sshAllowedCidr
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '22'
        }
      }
      {
        name: 'deny-all-inbound'
        properties: {
          priority: 4000
          direction: 'Inbound'
          access: 'Deny'
          protocol: '*'
          sourceAddressPrefix: '*'
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '*'
        }
      }
    ]
  }
}

// ── VNet ＋ snet-app ────────────────────────────────────────────────────────
// Service Endpoint：Microsoft.Sql、Microsoft.Storage（同區域）。免費；Private Endpoint 刻意不開（docs/17 §2）。
// ⚠️ 服務端點只改「往 SQL／Storage」的路由，往網際網路（LINE Pay）仍從靜態 Public IP 出去，出口 IP 不變。
// defaultOutboundAccess=false：Azure 預設出站已退場（2026-03-31），出站必須是刻意配置——這裡由 Public IP 提供。
resource vnet 'Microsoft.Network/virtualNetworks@2024-05-01' = {
  name: vnetName
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        vnetAddressPrefix
      ]
    }
    subnets: [
      {
        name: 'snet-app'
        properties: {
          addressPrefix: subnetPrefix
          defaultOutboundAccess: false
          networkSecurityGroup: {
            id: nsg.id
          }
          serviceEndpoints: [
            {
              service: 'Microsoft.Sql'
              locations: [
                location
              ]
            }
            {
              service: 'Microsoft.Storage'
              locations: [
                location
              ]
            }
          ]
        }
      }
    ]
  }
}

// ── 靜態 Public IP（Standard SKU、Static、IPv4、獨立資源）───────────────────
resource publicIp 'Microsoft.Network/publicIPAddresses@2024-05-01' = {
  name: publicIpName
  location: location
  sku: {
    name: 'Standard'
    tier: 'Regional'
  }
  properties: {
    publicIPAllocationMethod: 'Static'
    publicIPAddressVersion: 'IPv4'
    idleTimeoutInMinutes: 4
  }
}

resource publicIpLock 'Microsoft.Authorization/locks@2020-05-01' = {
  name: 'lock-${publicIpName}'
  scope: publicIp
  properties: {
    level: 'CanNotDelete'
    notes: 'LINE Pay 白名單綁定的出口 IP，刪除＝換 IP＝停機事件（docs/17 §2）。要刪請先由擁有者移除此鎖。'
  }
}

output subnetId string = '${vnet.id}/subnets/snet-app'
output publicIpId string = publicIp.id
