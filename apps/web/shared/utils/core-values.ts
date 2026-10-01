// shared/utils/core-values.ts — 首頁「五大核心價值」（主站規劃書 §1.2／§3.1）的前台文案與備援
//
// 後端 `GET /{club}/home/core-values`（apps/api README「F 批」）只給固定五項的 `code`／`nameZh`／`nameEn`／`sortOrder`／
// `learnMorePageSlug`（`about/philosophy`＝2.3 足球理念），**沒有說明文字，圖示由前台依 `code` 對應**。
// 說明文字是規劃書 §1.2 定死的品牌主張文案（兩站共用同一組 `code`，但藍鯨版的標籤文字尚待客戶確認——
// 藍鯨規劃書 §10 第 13 點，所以首頁區塊目前仍只對磐石顯示，見 `app/pages/zh/index.vue`）。
// 後端打不到或回空時，退回與固定目錄同順序的備援（五項是品牌主張，不是會變動的內容）。

export interface CoreValueDto {
  code: string
  nameZh: string
  nameEn: string
  sortOrder: number
  learnMorePageSlug: string | null
}

export interface CoreValueView {
  code: string
  num: string
  nameZh: string
  nameEn: string
  desc: string
}

const DESCRIPTIONS: Record<string, string> = {
  players_first: '所有訓練規劃與資源配置，皆以球員的長期發展與福祉為核心考量。',
  excellence: '建立專業化訓練與教練體系，協助選手邁向職業舞台所需的實力與態度。',
  global_pathways: '從台中出發、放眼世界，透過海外交流建立選手與職業舞台接軌的路徑。',
  community: '紮根台中在地，成為社區認同與榮耀的來源，與球迷共同成長。',
  integrity: '以誠信治理與專業制度，支撐俱樂部長期穩健發展。',
}

/** 後端不可用時的備援：與後端固定目錄同一組 code、同一順序。 */
export const CORE_VALUES_FALLBACK: readonly CoreValueDto[] = [
  { code: 'players_first', nameZh: '以球員為本', nameEn: 'Players First', sortOrder: 1, learnMorePageSlug: 'about/philosophy' },
  { code: 'excellence', nameZh: '追求卓越', nameEn: 'Excellence', sortOrder: 2, learnMorePageSlug: 'about/philosophy' },
  { code: 'global_pathways', nameZh: '國際發展', nameEn: 'Global Pathways', sortOrder: 3, learnMorePageSlug: 'about/philosophy' },
  { code: 'community', nameZh: '社區共好', nameEn: 'Community', sortOrder: 4, learnMorePageSlug: 'about/philosophy' },
  { code: 'integrity', nameZh: '誠信專業', nameEn: 'Integrity', sortOrder: 5, learnMorePageSlug: 'about/philosophy' },
]

/** 依 `sortOrder` 排序並補上編號與說明；認不得的 `code` 沒有說明文字就留空（不編造）。 */
export function buildCoreValueViews(items: readonly CoreValueDto[] | null | undefined): CoreValueView[] {
  const source = items && items.length > 0 ? items : CORE_VALUES_FALLBACK
  return [...source]
    .sort((a, b) => a.sortOrder - b.sortOrder)
    .map((v, i) => ({
      code: v.code,
      num: String(i + 1).padStart(2, '0'),
      nameZh: v.nameZh,
      nameEn: v.nameEn,
      desc: DESCRIPTIONS[v.code] ?? '',
    }))
}

/** `learnMorePageSlug`（如 `about/philosophy`）→ 站內路徑 `/zh/about/philosophy/`；形狀不合就回 null（不輸出連結）。 */
export function coreValueLearnMorePath(items: readonly CoreValueDto[] | null | undefined): string | null {
  const slug = items?.find(v => v.learnMorePageSlug)?.learnMorePageSlug ?? CORE_VALUES_FALLBACK[0]!.learnMorePageSlug
  return slug && /^[a-z0-9][a-z0-9/-]{0,100}$/.test(slug) ? `/zh/${slug.replace(/\/+$/, '')}/` : null
}
