// GET /api/charity/settings — 站台文案（首頁說明、感謝語樣板、捐款須知、隱私權政策）。
// 內容全部是 db/seed 種子裡明講的開發測試占位文字（「正式文案待客戶與協會確認」），
// 頁面端原樣呈現，不得自行改寫成看起來更像正式文案的版本（README 已註記此限制）。
export default defineEventHandler(() => ({
  homeIntro: getSettingText('donation.home_intro'),
  thankYouTemplate: getSettingText('donation.thank_you_message_template'),
  notice: getSettingText('donation.notice'),
  privacyPolicy: getSettingText('donation.privacy_policy'),
}))
