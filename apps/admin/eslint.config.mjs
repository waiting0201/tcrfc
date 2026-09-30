// @ts-check
import js from '@eslint/js'
import vue from 'eslint-plugin-vue'
import vueTsEslintConfig from '@vue/eslint-config-typescript'
import globals from 'globals'

const DATE_MESSAGE = '時間一律走 @/utils/dateTime（UTC 解析、台灣時間顯示與輸入、送出帶時區的 ISO），不要自己處理時區。'
const DATE_RESTRICTIONS = [
  {
    selector: "CallExpression[callee.property.name=/^(toISOString|toLocaleDateString|toLocaleTimeString|getHours|getMinutes|getSeconds)$/]",
    message: DATE_MESSAGE,
  },
  {
    // 單一參數的 new Date(字串)：沒有時區記號的字串會被當成瀏覽器本地時間
    selector: "NewExpression[callee.name='Date'][arguments.length=1]",
    message: DATE_MESSAGE,
  },
  {
    selector: "CallExpression[callee.object.name='Date'][callee.property.name='parse']",
    message: DATE_MESSAGE,
  },
]

export default [
  { ignores: ['dist/**', 'node_modules/**'] },
  js.configs.recommended,
  ...vue.configs['flat/recommended'],
  ...vueTsEslintConfig(),
  {
    rules: {
      // 後台 fixture／型別檔案偶爾用得到 any 過渡假資料，先不列為 error
      '@typescript-eslint/no-explicit-any': 'warn',
      // 這兩條是純排版風格規則（每行只能放一個屬性、單行元素內文要不要強制換行），
      // 跟這份專案既有的排版習慣（apps/web 的寫法）衝突太多，關掉不影響可讀性或正確性
      'vue/max-attributes-per-line': 'off',
      'vue/singleline-html-element-content-newline': 'off',
    },
  },
  {
    // 🔴 時區規則（apps/api/README.md C1 通則）：後端時間戳是 UTC 帶 `Z`、輸入不帶時區一律當 UTC。
    // 這幾種寫法都會依「瀏覽器所在時區」運作，或送出不帶時區的字串，全站只准在 `src/utils/dateTime.ts` 內使用；
    // 其他地方請用該檔的 formatDateTime／taipeiInputToUtc／pickerDateToUtc 等函式（見 apps/admin/README.md「時區規則」）。
    files: ['src/**/*.{ts,vue}'],
    ignores: ['src/utils/dateTime.ts'],
    rules: {
      'no-restricted-syntax': ['error', ...DATE_RESTRICTIONS],
      'vue/no-restricted-syntax': ['error', ...DATE_RESTRICTIONS],
    },
  },
  {
    files: ['scripts/**/*.mjs'],
    languageOptions: {
      globals: { ...globals.node },
    },
  },
]
