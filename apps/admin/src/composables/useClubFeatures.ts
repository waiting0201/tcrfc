import { computed } from 'vue'
import { activeClubId } from '@/auth/clubAccess'

/** 台中藍鯨站台代碼（藍鯨規劃書：與主站同一套網站，只有配色與少數單元不同）。 */
export const BLUE_WHALE_CLUB_CODE = 'bw'

/**
 * 依目前站台判斷「這個俱樂部有沒有這項功能」。
 * 藍鯨不設漫畫（規劃書 §4.6 F1、藍鯨規劃書：不設 8.1 漫畫）：後端對藍鯨的漫畫端點一律回 403
 * 「此功能不適用」，畫面先擋下、不呼叫。這只是介面便利，真正的把關在後端。
 */
export function useClubFeatures() {
  return {
    comicAvailable: computed(() => activeClubId.value !== BLUE_WHALE_CLUB_CODE),
  }
}
