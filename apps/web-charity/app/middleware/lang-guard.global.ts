// app/middleware/lang-guard.global.ts — [lang] 只接受 zh／en（規劃書 §2.1：「雙語以 /zh/、/en/
// 區隔，架構預留第三語系」——預留是資料模型層級的事，不代表路由要接受任何字串當語系）。
// 未知語系一律導向對應的繁中版本，不是硬 404（不確定使用者是不是打錯字，繁中是預設語系）。
export default defineNuxtRouteMiddleware((to) => {
  const lang = to.params.lang
  if (typeof lang === 'string' && lang !== 'zh' && lang !== 'en') {
    const rest = to.fullPath.split('/').slice(2).join('/')
    return navigateTo(`/zh/${rest}`)
  }
})
