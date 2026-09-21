// @ts-check
import js from '@eslint/js'
import vue from 'eslint-plugin-vue'
import vueTsEslintConfig from '@vue/eslint-config-typescript'
import globals from 'globals'

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
    files: ['scripts/**/*.mjs'],
    languageOptions: {
      globals: { ...globals.node },
    },
  },
]
