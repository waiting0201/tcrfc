import { createApp } from 'vue'
import ElementPlus from 'element-plus'
import 'element-plus/dist/index.css'
// 繁體中文語系。不設的話 Element Plus 自帶文案會是英文預設（分頁器的 Total／page、
// 表格空資料的 No Data、ElMessageBox 的 OK／Cancel……），違反規劃書 §4.0「介面一律
// 日常中文、不得出現英文技術詞」。這些字串來自元件庫內部，check-forbidden-terms.mjs
// 掃不到（它只看 .vue 的 <template>），所以只能靠這裡一次設定，見 docs/18 E-33。
import zhTw from 'element-plus/es/locale/lang/zh-tw'
import * as ElementPlusIconsVue from '@element-plus/icons-vue'

import App from './App.vue'
import router from './router'
import './styles/charity-admin-theme.css'

// 淺色是唯一主題（docs/22-charity-ui.md §1.3），不做主題切換開關、不掛 class="dark"。

const app = createApp(App)

for (const [key, component] of Object.entries(ElementPlusIconsVue)) {
  app.component(key, component)
}

app.use(ElementPlus, { locale: zhTw })
app.use(router)
app.mount('#app')
