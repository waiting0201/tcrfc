import type { NewsArticle } from '@/types/news'

/**
 * 假資料。⛔ 刻意不接 apps/api——那支 API 目前唯讀、只回已發布內容，接了就驗證不到
 * 草稿／排程發布／已停用三種非公開狀態（任務交付說明第 2 條）。
 *
 * 封面圖用純前端產生的色塊 SVG data URI 代替真實照片，避免這份 mockup 依賴外部圖床
 * 或客戶尚未交付、有肖像權疑慮的真實照片（CLAUDE.md 全域規定第 7 條）。
 */
function placeholderCover(seed: string, hue: number): string {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="320" height="320">
    <rect width="320" height="320" fill="hsl(${hue},45%,88%)"/>
    <text x="50%" y="50%" font-family="sans-serif" font-size="20" fill="hsl(${hue},35%,40%)"
      text-anchor="middle" dominant-baseline="middle">${seed}</text>
  </svg>`
  return `data:image/svg+xml;utf8,${encodeURIComponent(svg)}`
}

export const NEWS_ARTICLES: NewsArticle[] = [
  {
    id: 'n1',
    title: { zh: '台中磐石作客特倫欽友誼賽 2:1 逆轉取勝', en: 'TCRFC beats Trenčín 2-1 away' },
    urlName: 'tcrfc-vs-trencin-2026',
    category: 'match',
    coverImageUrl: placeholderCover('封面 1', 330),
    coverImageAlt: { zh: '球員慶祝進球', en: 'Players celebrating a goal' },
    status: 'published',
    statusAt: '2026-05-20 09:00',
    isSharedContent: false,
    updatedAt: '2026-05-20 09:00',
    content: {
      zh: '台中磐石本次歐洲移地訓練的第三場熱身賽，在客場以 2:1 逆轉擊敗特倫欽……（內文略）',
      en: '',
    },
    noIndex: false,
    canonicalUrl: '',
  },
  {
    id: 'n2',
    title: { zh: 'U15 青訓營開放報名', en: 'U15 Youth Camp registration is open' },
    urlName: 'u15-camp-2026-registration',
    category: 'academy',
    coverImageUrl: placeholderCover('封面 2', 200),
    coverImageAlt: { zh: '青訓營訓練畫面', en: 'Youth camp training session' },
    status: 'scheduled',
    statusAt: '2026-09-25 08:00',
    isSharedContent: false,
    updatedAt: '2026-09-18 14:22',
    content: { zh: '2026 年冬季 U15 青訓營即將開放報名……（內文略）', en: '' },
    noIndex: false,
    canonicalUrl: '',
  },
  {
    id: 'n3',
    title: { zh: '(草稿) 週報草稿', en: '' },
    urlName: 'weekly-digest-draft',
    category: 'general',
    coverImageUrl: null,
    coverImageAlt: { zh: '', en: '' },
    status: 'draft',
    isSharedContent: false,
    updatedAt: '2026-09-20 11:05',
    content: { zh: '本週賽事與活動摘要（尚未完稿）……', en: '' },
    noIndex: true,
    canonicalUrl: '',
  },
  {
    id: 'n4',
    title: { zh: '台中磐石與台中藍鯨共同宣布 2026/27 賽季合作計畫', en: 'TCRFC and Taichung Blue Whale announce 2026/27 partnership plan' },
    urlName: 'tcrfc-bw-2026-27-partnership',
    category: 'club',
    coverImageUrl: placeholderCover('封面 4', 25),
    coverImageAlt: { zh: '雙方代表合影', en: 'Representatives from both clubs' },
    status: 'published',
    statusAt: '2026-08-01 10:00',
    isSharedContent: true,
    updatedAt: '2026-08-01 10:00',
    content: { zh: '台中磐石與台中藍鯨今日共同宣布……（內文略）', en: 'TCRFC and Taichung Blue Whale today jointly announced...' },
    noIndex: false,
    canonicalUrl: '',
  },
  {
    id: 'n5',
    title: { zh: '2025 年度慈善公益回顧', en: '2025 Charity & Impact Year in Review' },
    urlName: '2025-charity-impact-review',
    category: 'charity',
    coverImageUrl: placeholderCover('封面 5', 150),
    coverImageAlt: { zh: '球員參與公益活動', en: 'Players at a charity event' },
    status: 'disabled',
    statusAt: '2026-06-01',
    statusBy: '王小明',
    isSharedContent: false,
    updatedAt: '2026-06-01 16:40',
    content: { zh: '2025 年度，台中磐石共參與 12 場公益活動……（內文略）', en: '' },
    noIndex: false,
    canonicalUrl: '',
  },
  {
    id: 'n6',
    title: { zh: '漫畫第三集：逆轉之刻 正式上線', en: 'Comic Episode 3: The Turning Point is now live' },
    urlName: 'manga-episode-3-launch',
    category: 'culture',
    coverImageUrl: placeholderCover('封面 6', 45),
    coverImageAlt: { zh: '漫畫第三集封面', en: 'Comic episode 3 cover' },
    status: 'published',
    statusAt: '2026-07-15 09:00',
    isSharedContent: false,
    updatedAt: '2026-07-15 09:00',
    content: { zh: '《台中磐石漫畫》第三集「逆轉之刻」正式上線……（內文略）', en: '' },
    noIndex: false,
    canonicalUrl: '',
  },
  {
    id: 'n7',
    title: { zh: '商業夥伴專訪：與在地企業攜手前行', en: '' },
    urlName: 'partner-interview-local-business',
    category: 'business',
    coverImageUrl: null,
    coverImageAlt: { zh: '', en: '' },
    status: 'draft',
    isSharedContent: false,
    updatedAt: '2026-09-10 09:30',
    content: { zh: '本篇專訪對象尚未確認，內容待補……', en: '' },
    noIndex: true,
    canonicalUrl: '',
  },
  {
    id: 'n8',
    title: { zh: '2026 企業甲級聯賽 第 12 輪賽後報導', en: '2026 Enterprise Div. 1 Round 12 Match Report' },
    urlName: '2026-enterprise-league-round-12',
    category: 'match',
    coverImageUrl: placeholderCover('封面 8', 330),
    coverImageAlt: { zh: '賽事精采瞬間', en: 'Match highlight moment' },
    status: 'published',
    statusAt: '2026-09-14 20:00',
    isSharedContent: false,
    updatedAt: '2026-09-14 20:00',
    content: { zh: '第 12 輪賽事，台中磐石主場迎戰……（內文略）', en: '' },
    noIndex: false,
    canonicalUrl: '',
  },
]
