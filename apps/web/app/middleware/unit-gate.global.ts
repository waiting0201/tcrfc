// app/middleware/unit-gate.global.ts — 單元開關呼叫點 1／4
//
// 頁面用 definePageMeta({ unit: '06' }) 宣告自己屬於哪個單元，這支 middleware
// 在每次導覽時檢查該單元對目前 club 是否開放；不開放就丟 404，不做靜默轉址
// （轉址會讓使用者以為連結搬家了，404 才是誠實的狀態）。
//
// 沒有宣告 unit 的頁面（目前只有首頁重導頁）一律放行。
export default defineNuxtRouteMiddleware((to) => {
  const unit = to.meta.unit as string | undefined
  if (!unit) return

  const club = useRuntimeConfig().public.club
  if (!isUnitEnabledForClub(unit, club)) {
    throw createError({ statusCode: 404, statusMessage: 'Not Found' })
  }
})
