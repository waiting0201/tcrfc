// shared/utils/core-values.ts — 首頁／2.3 足球理念「四大核心價值」（主站規劃書 v3.29 §1.2／§3.1）的前台文案與備援
//
// 後端 `GET /{club}/home/core-values`（apps/api README「F 批」）只給固定四項的 `code`／`nameZh`／`nameEn`／`sortOrder`／
// `learnMorePageSlug`（`about/philosophy`＝2.3 足球理念），**沒有說明文字，圖示由前台依 `code` 對應**。
// 說明文字＝客戶 2.3「四大核心價值」原文（逐字，段落以空行分隔；2026-10-16 使用者裁決照客戶文件）：啟發球員 Inspiring players／
// 塑造冠軍 Shaping champions／在地驕傲 Local pride／撼動全球 Global influence。舊五項（players_first／excellence／global_pathways／
// community／integrity）已淘汰，看到一律是舊規格（docs/14）。兩站共用同一組 `code`，但藍鯨版的標籤文字尚待客戶確認——
// 藍鯨規劃書 §10 第 13 點，所以首頁區塊目前仍只對磐石顯示，見 `app/pages/zh/index.vue`。
// 首頁卡片只放每項的第一段（`desc`），2.3 足球理念頁放全文（`paragraphs`）。英文見 club-copy-en-core.ts `CORE_VALUE_DESCRIPTIONS_EN`。
// 後端打不到或回空時，退回與固定目錄同順序的備援（四項是品牌主張，不是會變動的內容）。
// 🔴 本檔的文案同時是 db/seed/page_seed_content.extract.ts 產生 `about/philosophy` 種子的來源——改字要重跑 extract 並請後端重產 db/prod 種子。

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
  /** 首頁卡片用：說明的第一段。 */
  desc: string
  /** 2.3 足球理念頁用：說明全文（客戶原文的各段）。 */
  paragraphs: string[]
}

/** 客戶 2.3「四大核心價值」原文（逐字）。 */
const DESCRIPTIONS: Record<string, string> = {
  inspiring_players: "足球的價值不只是贏球。台中磐石相信，更重要的是讓每一位球員看見自己的可能。\n\n從第一次碰球的孩子，到站上成人賽場的球員，我們希望給每個人一條清楚的路、一個能持續成長的環境。紀律、責任、團隊合作、面對挫折、尊重對手，這些東西跟技術一樣重要，甚至更難練。\n\n真正的冠軍，不只存在於比分牌上。",
  shaping_champions: "冠軍不是一夜練成的，也不只由名次決定。\n\n它來自每天準時到場、每一次不敷衍的訓練、每一個撐下去的決定，還有在團隊裡彼此扛起的責任。贏了之後的謙遜、輸了之後的韌性、壓力來了的冷靜，這些東西，才是我們真正想培養的。\n\n從青訓到一線隊，台中磐石希望建立一套共同的訓練語言和比賽標準。冠軍不是終點，是每天願意比昨天再好一點點的態度。",
  local_pride: "台中磐石選擇以台中為家，不只是地點上的選擇，而是一個承諾。\n\n一支真正有力量的球隊，跟所在的城市、社區、學校、家庭和球迷是連在一起的。我們持續投入青少年足球、校園合作和社區活動，讓更多孩子有機會碰到球，讓更多家庭走進球場，讓更多台中人覺得，這支隊是我們自己的。\n\n在地驕傲不只是球衣上的城市名稱。是球員為台中拚的那口氣，是看台上的那聲吶喊，也是大家一起相信的那件事：台中的足球，可以更好。",
  global_influence: "我們從台中出發，但眼光不只放在台中。\n\n足球是跨越語言和國界的語言。台中磐石透過國際合作、海外交流和教練培訓，讓球員看見世界不同的足球文化，也讓世界慢慢認識台中。\n\n跟不同國家和職業俱樂部的交流，不是為了貼標籤，而是真的想知道高水平足球在要求什麼，細節、紀律、恢復、戰術、人才培育，每一個環節都是功課。我們希望把這些帶回來，轉化成適合台灣球員的東西。\n\n讓每一位從台中磐石出發的球員，都有機會站上更大的舞台。讓每一場在台中踢出的比賽，都成為台灣足球走向世界的下一步。",
}

/** 後端不可用時的備援：與後端固定目錄同一組 code、同一順序。 */
export const CORE_VALUES_FALLBACK: readonly CoreValueDto[] = [
  { code: 'inspiring_players', nameZh: '啟發球員', nameEn: 'Inspiring players', sortOrder: 1, learnMorePageSlug: 'about/philosophy' },
  { code: 'shaping_champions', nameZh: '塑造冠軍', nameEn: 'Shaping champions', sortOrder: 2, learnMorePageSlug: 'about/philosophy' },
  { code: 'local_pride', nameZh: '在地驕傲', nameEn: 'Local pride', sortOrder: 3, learnMorePageSlug: 'about/philosophy' },
  { code: 'global_influence', nameZh: '撼動全球', nameEn: 'Global influence', sortOrder: 4, learnMorePageSlug: 'about/philosophy' },
]

/** 依 `sortOrder` 排序並補上編號與說明；認不得的 `code` 沒有說明文字就留空（不編造）。 */
export function buildCoreValueViews(items: readonly CoreValueDto[] | null | undefined): CoreValueView[] {
  const source = items && items.length > 0 ? items : CORE_VALUES_FALLBACK
  return [...source]
    .sort((a, b) => a.sortOrder - b.sortOrder)
    .map((v, i) => {
      const paragraphs = (DESCRIPTIONS[v.code] ?? '').split(/\r?\n\s*\r?\n/).map(p => p.trim()).filter(Boolean) // 與 plain-text.ts splitParagraphs 同規則（不 import：scripts 以 node 直接載入本檔）
      return {
        code: v.code,
        num: String(i + 1).padStart(2, '0'),
        nameZh: v.nameZh,
        nameEn: v.nameEn,
        desc: paragraphs[0] ?? '',
        paragraphs,
      }
    })
}

/** `learnMorePageSlug`（如 `about/philosophy`）→ 站內路徑 `/zh/about/philosophy/`；形狀不合就回 null（不輸出連結）。 */
export function coreValueLearnMorePath(items: readonly CoreValueDto[] | null | undefined): string | null {
  const slug = items?.find(v => v.learnMorePageSlug)?.learnMorePageSlug ?? CORE_VALUES_FALLBACK[0]!.learnMorePageSlug
  return slug && /^[a-z0-9][a-z0-9/-]{0,100}$/.test(slug) ? `/zh/${slug.replace(/\/+$/, '')}/` : null
}

/** 2.3 足球理念本文（客戶 2.3「足球理念」原文，逐字；段落以空行分隔）。英文見 club-copy-en-core.ts `PHILOSOPHY_BODY_EN`。 */
export const PHILOSOPHY_BODY_TCRFC = "台中磐石足球俱樂部的足球理念，可概括為：以台中在地化為根基，以本土球員培育為核心，以完整青訓、職業化管理、運動科學及國際交流為方法，推動可持續的台灣足球發展。這不是只追求短期成績的球隊定位，而是試圖把俱樂部建成連結社區、學校、青訓與成人競技隊的足球發展平台。以專業模式培育球員，協助其追求卓越、發揮潛能。透過實際行動展示職業足球理念，推動台灣足球的永續發展。\n\n在地化與社區責任\n以台中為發展基地，建立地方認同，並把俱樂部定位為推動社區足球、青少年足球與城市運動文化的力量。\n\n完整發展路徑\n建立由兒童、學校青訓到成年隊的培養通道，避免球員在特定年齡階段失去銜接機會。\n\n科學化與個別化訓練\n用檢測、數據與個人化負荷設計取代只憑經驗的訓練，提升球員體能表現、訓練效率與競技穩定度。\n\n國際視野\n學習歐洲、日本與亞洲職業俱樂部的制度與方法，但依台灣教育文化、訓練時間與球員生活現實做在地調整。\n\n建設足球生態\n俱樂部不只負責比賽成績，也必須把制度、資源、訓練與基層發展真正帶進球場，主動承擔足球生態建設責任。"
