// app/utils/club.ts — 品牌切換的靜態資產表（紀律 2，docs/13-blue-whale-site.md §6）
//
// 🔴 club 專屬靜態資產（favicon、OG 圖、apple-touch-icon、標誌）兩份都打包進同一映像檔，
// 依 runtime 的 NUXT_PUBLIC_CLUB 選路徑，不得寫死單一品牌的路徑。
//
// Nuxt 的 app/utils/ 目錄下的具名匯出會自動被引入（auto-import），不需要在使用處手動 import。

export type ClubCode = 'tcrfc' | 'bw'

export interface ClubAssets {
  code: ClubCode
  /** 中文簡稱，見 docs/14-invariants.md「名稱寫法」——台中磐石／台中藍鯨，不單獨簡稱 */
  nameZh: string
  themeColor: string
  favicon: { href: string; type: string; sizes?: string }[]
  appleTouchIcon: string
  ogImage: string
  headerMark: { src: string; width: number; height: number }
  footerMark: { src: string; width: number; height: number }
}

const CLUB_ASSETS: Record<ClubCode, ClubAssets> = {
  tcrfc: {
    code: 'tcrfc',
    nameZh: '台中磐石足球俱樂部',
    themeColor: '#E0218A',
    favicon: [
      { href: '/assets/favicon.ico', type: 'image/x-icon', sizes: '16x16 32x32 48x48' },
      { href: '/assets/favicon.svg', type: 'image/svg+xml' },
    ],
    appleTouchIcon: '/assets/apple-touch-icon.png',
    ogImage: '/assets/brand/social/og-image.png',
    headerMark: { src: '/assets/brand/svg/tcrfc-mark-pink.svg', width: 42, height: 44 },
    footerMark: { src: '/assets/brand/svg/tcrfc-full-white.svg', width: 97, height: 120 },
  },
  bw: {
    code: 'bw',
    nameZh: '台中藍鯨',
    themeColor: '#2196D5',
    // 🔴 藍鯨只有點陣隊徽主檔（brand/blue-whale/），沒有向量、沒有專屬 favicon／OG 設計。
    // 以下是由官方點陣主檔「裁切＋縮放」產生的網頁圖示（brand/blue-whale/README.md 明列
    // 「網頁、App 圖示（1024 以內綽綽有餘）」為可用情境），不是自行造標、不是描摹、
    // 也不是把點陣圖放大充當向量。OG 圖直接重用隊徽點陣圖，不是正式設計稿——
    // 缺什麼、為什麼先這樣接，列在 apps/web/README.md「藍鯨資產缺口」。
    favicon: [{ href: '/assets/brand/bw/favicon-48.png', type: 'image/png', sizes: '48x48' }],
    appleTouchIcon: '/assets/brand/bw/apple-touch-icon.png',
    ogImage: '/assets/brand/bw/bw-crest-512.png',
    headerMark: { src: '/assets/brand/bw/bw-crest-512.png', width: 42, height: 41 },
    footerMark: { src: '/assets/brand/bw/bw-crest-512.png', width: 97, height: 95 },
  },
}

export function getClubAssets(club: string): ClubAssets {
  return CLUB_ASSETS[club === 'bw' ? 'bw' : 'tcrfc']
}
