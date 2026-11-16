#!/usr/bin/env node
/**
 * check-no-static-fallback.mjs — 官網「後台沒資料就顯示寫死內容」的後備不得被加回來
 * （docs/23 H-1 B 類與 C-5；2026-11-13 使用者拍板；原始案例 docs/18 E-291）。
 *
 * 原則：後台可管理的內容，前台的空資料狀態只能是「不顯示／空狀態文案」，不得放後台看不到、改不到的寫死內容。
 * 本腳本只擋「這次拿掉的那批後備」的識別字與特徵寫法（字面比對，不做語意判斷）：
 *   - 已移除的常數／旗標：FALLBACK_INTL_TILES、SCHOOL_PARTNERS_BW、SCHOOL_PARTNER_NO_YEAR_EN、STATIC_YEARS、STATIC_ORGS、
 *     showStaticReview、getAcademyTeamTabsEn、PARTNERS_PLAN_YOUTH_EN_BW、MILESTONES_API_LEDE_EN_BW、foundingHonourDescEn
 *   - milestones.vue 的 `usingApi` 旗標（寫死時間軸分支）
 *   - 首頁 Hero 新聞卡 `heroNews.length || isTcrfc`、商店入口 `isTcrfc.value || shopEntryProducts`
 *   - 磐石寫死的國際夥伴隊徽檔案（partners-intl/）、球迷會活動照（fanclub-event-01…03）、場地占位卡（Training Base／Academy Ground）
 *   - 內部口吻：「待客戶提供各梯隊賽程表」
 *   - applySiteFactsToIdentity 的 `|| identity.footerBlurb`、`|| identity.social.email`（API 成功但值為空必須不顯示）
 *
 * 刻意保留（不在此檢查）：first-team-02-trophy.jpg（固定版面裝飾圖）、fanclub-event-04.jpg（頁首／文化首頁固定版面圖）、
 * useSiteFacts 的靜態快照降級（API 打不到時的 fail-open）。
 */
import { readFileSync, readdirSync, statSync, existsSync } from 'node:fs'
import { join, sep } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = fileURLToPath(new URL('..', import.meta.url))
const SCAN = ['app', 'shared', 'server']
const EXT = /\.(vue|ts|mjs|js)$/

function walk(p, out = []) {
  const full = join(ROOT, p)
  if (!existsSync(full)) return out
  const st = statSync(full)
  if (st.isFile()) { if (EXT.test(p)) out.push(p); return out }
  for (const n of readdirSync(full)) {
    if (n === 'node_modules' || n === '.nuxt' || n === '.output') continue
    walk(join(p, n), out)
  }
  return out
}

/** [pattern, 說明, 只掃的檔案（相對路徑片段，省略＝全部）] */
const RULES = [
  [/FALLBACK_INTL_TILES/, '國際夥伴隊徽寫死後備', null],
  [/SCHOOL_PARTNERS_BW|SCHOOL_PARTNER_NO_YEAR_EN|SchoolPartnerRow/, '藍鯨合作學校寫死清單', null],
  [/\bSTATIC_YEARS\b|\bSTATIC_ORGS\b/, '慈善事蹟／團體寫死後備', null],
  [/showStaticReview/, '球迷會活動回顧寫死照片', null],
  [/getAcademyTeamTabsEn/, '學院隊伍頁空殼分頁後備', null],
  [/PARTNERS_PLAN_YOUTH_EN_BW|getPartnersPlanYouthTitleEn/, '贊助方案靜態卡標題', null],
  [/MILESTONES_API_LEDE_EN_BW/, '里程碑 usingApi 分支導言', null],
  [/foundingHonourDescEn/, '一線隊榮譽寫死後備', null],
  [/\busingApi\b/, '里程碑寫死時間軸旗標', 'pages/zh/about/milestones.vue'],
  [/heroNews\.length\s*\|\|\s*isTcrfc/, '首頁 Hero 寫死新聞卡', null],
  [/isTcrfc\.value\s*\|\|\s*shopEntryProducts/, '首頁商店入口寫死球衣照', null],
  [/store-visual/, '首頁商店入口寫死球衣照', 'pages/zh/index.vue'],
  [/partners-intl\//, '磐石寫死的國際夥伴隊徽', null],
  [/fanclub-event-0[123]\b/, '球迷會寫死活動照', null],
  [/>\s*Training Base\s*<|>\s*Academy Ground\s*</, '場地占位卡', null],
  [/待客戶提供各梯隊賽程表/, '行事曆空狀態內部口吻', null],
  [/\|\|\s*identity\.footerBlurb|\|\|\s*identity\.social\.email/, '頁尾簡介／Email 寫死退路', null],
  [/first-team-01-squad/, '一線隊寫死合影', null],
]

const failures = []
for (const root of SCAN) {
  for (const f of walk(root)) {
    const rel = f.split(sep).join('/')
    const text = readFileSync(join(ROOT, f), 'utf8')
    for (const [re, why, only] of RULES) {
      if (only && !rel.endsWith(only)) continue
      const lines = text.split('\n')
      lines.forEach((line, i) => {
        if (re.test(line)) failures.push(`${rel}:${i + 1}  ${why}  → ${line.trim().slice(0, 120)}`)
      })
    }
  }
}

if (failures.length) {
  console.error(`✗ 發現已移除的寫死後備又出現（docs/23 B 類／C-5、docs/18 E-291）：\n${failures.map((x) => `  ${x}`).join('\n')}`)
  process.exit(1)
}
console.log(`✓ check-no-static-fallback：${RULES.length} 條規則，無已移除的寫死後備`)
