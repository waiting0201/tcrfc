import { ref } from 'vue'
import { CURRENT_USER } from './session'

/**
 * 目前站台切換器選到的俱樂部代碼（`tcrfc`／`bw`），單例、跨元件共用。
 *
 * 這個狀態原本只存在 SiteSwitcher.vue 的元件內部 ref，畫面上看起來可以切換，但沒有任何地方
 * 讀得到「現在選的是哪一隊」。後台新聞要打 `/api/v1/admin/{club}/news` 這種依俱樂部區分的端點，
 * 一定得知道目前選的俱樂部，所以把這個狀態拉成模組層級的單例（跟 data/newsStore.ts 拿掉之前
 * 同樣的手法：不是要引入 Pinia，純粹是目前只有一處需要跨元件共用）。
 */
export const activeClubId = ref(CURRENT_USER.authorizedClubs[0]?.id ?? 'tcrfc')
