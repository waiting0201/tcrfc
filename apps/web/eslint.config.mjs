// @ts-check
import withNuxt from './.nuxt/eslint.config.mjs'

// 沿用 @nuxt/eslint 的預設規則組，只關掉兩條跟這個專案的既定事實衝突的規則
// （骨架階段不加其餘自訂規則；verify.mjs 六項檢查移植為 lint／test 的工作
// 留給 S0-9 完整搬遷時處理，見 STATUS.md S0-9）：
//
//  - vue/no-multiple-template-root：Vue 2 相容規則，Vue 3／Nuxt 4 本來就支援
//    多根節點樣板。mockup 的 header／footer partials 本來就是好幾個平行的
//    <div>／<header>（utility-bar、site-header、mobile-nav、mobile-cta-bar
//    四個同層級），硬包一個外層 <div> 才能符合這條規則，但那樣會改動
//    docs/14-invariants.md 明訂「不得動」的 DOM 結構——兩者衝突時以不變動
//    DOM 結構為準，改關掉這條規則。
//  - no-irregular-whitespace：預設會把全形空格（U+3000）當成錯誤，但 mockup
//    內文本來就用全形空格做中文排版斷句（例如「楠梓足球場　賽程以官方公告為準」），
//    是搬遷時要逐字保留的原始文字內容，不是誤植的空白字元。
export default withNuxt().override('nuxt/vue/single-root', {
  rules: {
    'vue/no-multiple-template-root': 'off',
  },
}).override('nuxt/javascript', {
  rules: {
    'no-irregular-whitespace': 'off',
  },
})
