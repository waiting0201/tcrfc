// modules/monitoring.bicep — Action Group、指標告警、預算（docs/17 §6、§7 風險 6）

@description('Action Group 與告警／預算通知的收件 Email')
param alertEmail string

@description('兩個資料庫的資源 ID（儲存空間告警）')
param databaseIds array

@description('資料庫顯示名稱（與 databaseIds 同順序，只用於告警命名）')
param databaseNames array

@description('VM 資源 ID（CPU Credits Remaining 告警）')
param vmId string

@description('VM CPU Credits Remaining 低於此值即告警（Average）')
param cpuCreditsLowThreshold int

@description('預算金額（以帳單幣別計；帳單幣別非 USD 時要自行換算）')
param budgetAmount int

@description('預算起算日（必須是當月 1 日；固定值，避免每次部署都改動既有預算）')
param budgetStartDate string

@description('資源名稱前綴，例如 tcrfc-prod')
param namePrefix string

// 2 GB 硬上限的 75% ＝ 1.5 GB（docs/17 §6）
var storageAlertBytes = 1610612736

resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: 'ag-${namePrefix}-ops'
  location: 'global'
  properties: {
    groupShortName: 'tcrfcops'
    enabled: true
    emailReceivers: [
      {
        name: 'ops-email'
        emailAddress: alertEmail
        useCommonAlertSchema: true
      }
    ]
  }
}

resource dbStorageAlerts 'Microsoft.Insights/metricAlerts@2018-03-01' = [
  for (dbName, i) in databaseNames: {
    name: 'alert-${namePrefix}-${dbName}-storage-1_5gb'
    location: 'global'
    properties: {
      description: '${dbName} 資料空間已達 1.5 GB（Basic 硬上限 2 GB，寫滿即寫入失敗）。立即升級 S0 或清理資料。'
      severity: 1
      enabled: true
      scopes: [
        databaseIds[i]
      ]
      evaluationFrequency: 'PT15M'
      windowSize: 'PT1H'
      criteria: {
        'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
        allOf: [
          {
            criterionType: 'StaticThresholdCriterion'
            name: 'storage-bytes'
            metricNamespace: 'Microsoft.Sql/servers/databases'
            metricName: 'storage'
            operator: 'GreaterThanOrEqual'
            threshold: storageAlertBytes
            timeAggregation: 'Maximum'
          }
        ]
      }
      autoMitigate: true
      actions: [
        {
          actionGroupId: actionGroup.id
        }
      ]
    }
  }
]

// DTU 告警（S0-10 壓測建議值，docs/17 §6）：Basic 只有 5 DTU，DTU 不足「只是變慢」而非失敗，
// 所以分兩級：持續偏高先預警（warning）、接近滿載再升級（critical）。升級是線上作業，見 deploy/loadtest/README.md。
var dtuAlerts = [
  { suffix: 'dtu-80', severity: 2, threshold: 80, window: 'PT15M', freq: 'PT5M', note: '平均 DTU 使用率 ≥ 80% 持續 15 分鐘；規劃升級 S0（10 DTU）' }
  { suffix: 'dtu-95', severity: 1, threshold: 95, window: 'PT10M', freq: 'PT5M', note: '平均 DTU 使用率 ≥ 95% 持續 10 分鐘；請線上升級 S0（az sql db update --service-objective S0）' }
]

resource dbDtuAlerts 'Microsoft.Insights/metricAlerts@2018-03-01' = [
  for (item, k) in flatten(map(range(0, length(databaseNames)), i => map(dtuAlerts, a => union(a, { dbIndex: i })))): {
    name: 'alert-${namePrefix}-${databaseNames[item.dbIndex]}-${item.suffix}'
    location: 'global'
    properties: {
      description: '${databaseNames[item.dbIndex]}：${item.note}'
      severity: item.severity
      enabled: true
      scopes: [
        databaseIds[item.dbIndex]
      ]
      evaluationFrequency: item.freq
      windowSize: item.window
      criteria: {
        'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
        allOf: [
          {
            criterionType: 'StaticThresholdCriterion'
            name: 'dtu-consumption-percent'
            metricNamespace: 'Microsoft.Sql/servers/databases'
            metricName: 'dtu_consumption_percent'
            operator: 'GreaterThanOrEqual'
            threshold: item.threshold
            timeAggregation: 'Average'
          }
        ]
      }
      autoMitigate: true
      actions: [
        {
          actionGroupId: actionGroup.id
        }
      ]
    }
  }
]

// B 系列叢發型 VM：SSR 持續高載會耗盡 CPU 額度（docs/17 §1「VM 規格」）
resource vmCpuCreditsAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-${namePrefix}-vm-cpu-credits-low'
  location: 'global'
  properties: {
    description: 'VM CPU 額度餘額偏低；持續下去效能會被壓回基準線。考慮換 D2s_v5（docs/17 §1）。'
    severity: 2
    enabled: true
    scopes: [
      vmId
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          criterionType: 'StaticThresholdCriterion'
          name: 'cpu-credits-remaining'
          metricNamespace: 'Microsoft.Compute/virtualMachines'
          metricName: 'CPU Credits Remaining'
          operator: 'LessThan'
          threshold: cpuCreditsLowThreshold
          timeAggregation: 'Average'
        }
      ]
    }
    autoMitigate: true
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
}

// 預算：資源群組範圍、每月、實際花費達 80% 與 100% 時寄信。
resource budget 'Microsoft.Consumption/budgets@2023-05-01' = {
  name: 'budget-${namePrefix}-monthly'
  properties: {
    category: 'Cost'
    amount: budgetAmount
    timeGrain: 'Monthly'
    timePeriod: {
      startDate: budgetStartDate
    }
    notifications: {
      actual_80_percent: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 80
        thresholdType: 'Actual'
        contactEmails: [
          alertEmail
        ]
      }
      actual_100_percent: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 100
        thresholdType: 'Actual'
        contactEmails: [
          alertEmail
        ]
      }
    }
  }
}

output actionGroupId string = actionGroup.id
