import { createApp } from 'vue'
import ElementPlus from 'element-plus'
import 'element-plus/dist/index.css'
// Element Plus 官方深色模式變數表（docs/21-admin-ui.md §7.1／§7.9）。載入後在 <html> 掛 class="dark"
// 即可啟用內建元件的深色版外觀，本專案再用 admin-theme.css 覆寫這組變數的「值」。
import 'element-plus/theme-chalk/dark/css-vars.css'
// 繁體中文語系。不設的話 Element Plus 自帶文案會是英文預設（分頁器的 Total／page、
// 表格空資料的 No Data、ElMessageBox 的 OK／Cancel……），違反規劃書 §4.0「介面一律
// 日常中文、不得出現英文技術詞」。這些字串來自元件庫內部，check-forbidden-terms.mjs
// 掃不到（它只看 .vue 的 <template>），所以只能靠這裡一次設定，見 docs/18 E-33。
import zhTw from 'element-plus/es/locale/lang/zh-tw'
import * as ElementPlusIconsVue from '@element-plus/icons-vue'

import App from './App.vue'
import router from './router'
import './styles/admin-theme.css'

// 深色是唯一主題，不做主題切換開關（docs/21 §7.0 使用者已拍板）。固定掛在掛載前，
// 避免任何元件在 class 生效前先用預設（淺色）token 畫出一次再閃一次深色。
document.documentElement.classList.add('dark')

const app = createApp(App)

// 圖示元件全域註冊，模組數量多、各處都會用到，個別 import 太瑣碎
for (const [key, component] of Object.entries(ElementPlusIconsVue)) {
  app.component(key, component)
}

app.use(ElementPlus, { locale: zhTw })
app.use(router)
app.mount('#app')
