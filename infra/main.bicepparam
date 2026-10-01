using './main.bicep'

// 非機密、可進版控的值直接寫在這裡；機密與個人化的值由環境變數帶入
// （CI：GitHub Environment secrets／variables；本機手動：export 後再執行 az，見 infra/README.md）。

// Cloudflare IPv4 段。來源 https://www.cloudflare.com/ips-v4 ，2026-10-01 取得。
// Cloudflare 異動時更新此處並重新部署（見 infra/README.md「Cloudflare IP 段更新」）。
param cloudflareCidrs = [
  '173.245.48.0/20'
  '103.21.244.0/22'
  '103.22.200.0/22'
  '103.31.4.0/22'
  '141.101.64.0/18'
  '108.162.192.0/18'
  '190.93.240.0/20'
  '188.114.96.0/20'
  '197.234.240.0/22'
  '198.41.128.0/17'
  '162.158.0.0/15'
  '104.16.0.0/13'
  '104.24.0.0/14'
  '172.64.0.0/13'
  '131.0.72.0/22'
]

// ── 以下來自環境變數（沒設會在編譯期失敗，這是刻意的）────────────────────────
// 🔴 公開 repo 的 Actions log 人人可看：SSH 來源 IP、Email、物件 ID 一律用 GitHub secret 儲存
//    （secret 值在 log 中會被遮蔽），不要進這個檔案。
param sshAllowedCidr = readEnvironmentVariable('SSH_ALLOWED_CIDR')
param sshPublicKey = readEnvironmentVariable('SSH_PUBLIC_KEY')
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD')
param alertEmail = readEnvironmentVariable('ALERT_EMAIL')
// 定序：建庫後不可改。使用者 2026-10-01 定案（與本機 SQL Server 容器的預設一致）。
param databaseCollation = 'SQL_Latin1_General_CP1_CI_AS'
// Entra 管理員可選：兩個都留空＝不設定
param entraAdminLogin = readEnvironmentVariable('ENTRA_ADMIN_LOGIN', '')
param entraAdminObjectId = readEnvironmentVariable('ENTRA_ADMIN_OBJECT_ID', '')
