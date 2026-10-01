// modules/compute.bicep — NIC ＋ VM（docs/17 §1）
//
// 🔴 osProfile 在 VM 建立後是「不可變」的：之後修改 cloud-init.yaml（customData）、
//    admin 使用者名稱或 SSH 公鑰，incremental 部署會被 ARM 拒絕（Changing property ... is not allowed）。
//    要改只能重建 VM（OS 磁碟與 NIC 設為 Detach，Public IP 本來就獨立，所以重建不會換 IP）。
//    已在執行中的 VM 要加 SSH 金鑰，請直接 ssh 進去改 ~/.ssh/authorized_keys。

@description('部署區域')
param location string

@description('VM 名稱（同時作為 Linux hostname）')
param vmName string

@description('NIC 名稱')
param nicName string

@description('VM 規格')
param vmSize string

@description('管理員使用者名稱（僅 SSH 金鑰登入，密碼登入已停用）')
param adminUsername string

@description('SSH 公鑰內容（公鑰，不是私鑰）')
param sshPublicKey string

@description('OS 磁碟大小 GB（Premium SSD）')
param osDiskSizeGb int

@description('snet-app 的資源 ID')
param subnetId string

@description('靜態 Public IP 的資源 ID')
param publicIpId string

resource nic 'Microsoft.Network/networkInterfaces@2024-05-01' = {
  name: nicName
  location: location
  properties: {
    ipConfigurations: [
      {
        name: 'ipconfig1'
        properties: {
          privateIPAllocationMethod: 'Dynamic'
          subnet: {
            id: subnetId
          }
          publicIPAddress: {
            id: publicIpId
            properties: {
              deleteOption: 'Detach' // 刪 VM／NIC 時 Public IP 保留
            }
          }
        }
      }
    ]
  }
}

resource vm 'Microsoft.Compute/virtualMachines@2024-07-01' = {
  name: vmName
  location: location
  properties: {
    hardwareProfile: {
      vmSize: vmSize
    }
    storageProfile: {
      imageReference: {
        publisher: 'Canonical'
        offer: 'ubuntu-24_04-lts'
        sku: 'server' // Gen2
        version: 'latest'
      }
      osDisk: {
        name: '${vmName}-osdisk'
        createOption: 'FromImage'
        caching: 'ReadWrite'
        diskSizeGB: osDiskSizeGb
        deleteOption: 'Detach' // 刪 VM 時磁碟保留（Docker volume 含 Data Protection 金鑰環，遺失無法復原）
        managedDisk: {
          storageAccountType: 'Premium_LRS'
        }
      }
    }
    osProfile: {
      computerName: vmName
      adminUsername: adminUsername
      customData: base64(loadTextContent('../cloud-init.yaml'))
      linuxConfiguration: {
        disablePasswordAuthentication: true
        provisionVMAgent: true
        ssh: {
          publicKeys: [
            {
              path: '/home/${adminUsername}/.ssh/authorized_keys'
              keyData: sshPublicKey
            }
          ]
        }
      }
    }
    networkProfile: {
      networkInterfaces: [
        {
          id: nic.id
          properties: {
            primary: true
            deleteOption: 'Detach'
          }
        }
      ]
    }
    securityProfile: {
      securityType: 'TrustedLaunch'
      uefiSettings: {
        secureBootEnabled: true
        vTpmEnabled: true
      }
    }
    diagnosticsProfile: {
      bootDiagnostics: {
        enabled: true // 受控儲存，不需另開儲存體帳戶
      }
    }
  }
}

output vmId string = vm.id
output vmName string = vm.name
