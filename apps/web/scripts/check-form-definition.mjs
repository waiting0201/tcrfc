#!/usr/bin/env node
/**
 * check-form-definition.mjs — 前台表單照後台表單設計器的定義顯示（docs/23 W-3／W-24）。
 *
 * ① shared/utils/form-definition.ts 的取值規則：定義有值用定義、缺欄位或取不到用退路、英文頁定義回退成中文時用退路、
 *    選項「顯示文字」與「送出值」同位置對應。
 * ② 頁面寫死的選項送出值（partnership 的洽詢類型退路、academy 的 ENROLLMENT_CATEGORY_MAP）必須是種子裡
 *    後端 select 欄位的選項字面值——改種子或改頁面其一，這裡就會失敗，不必等到送出被 400。
 * 離開碼：失敗 1／通過 0。
 */
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { hasCjk, resolveFieldLabel, resolveFieldOptions, resolveOptionLabel } from '../shared/utils/form-definition.ts'

let failed = 0
const t = (name, fn) => {
  try { fn(); console.log('✓', name) }
  catch (e) { failed++; console.error(`✗ ${name}\n    ${e.message}`) }
}

const def = {
  fields: [
    { fieldKey: 'name', label: 'Applicant', options: null },
    { fieldKey: 'untranslated', label: '尚未翻譯的題目' },
    { fieldKey: 'blank', label: '   ' },
    { fieldKey: 'type', options: ['合作夥伴', '贊助', '兩者'], optionLabels: ['Partnership', '贊助', 'Both'] },
  ],
}

t('標籤：定義有值用定義', () => {
  assert.equal(resolveFieldLabel(def, 'name', '姓名', 'zh'), 'Applicant')
  assert.equal(resolveFieldLabel(def, 'name', 'Name', 'en'), 'Applicant')
})
t('標籤：定義取不到（null／undefined／沒有 fields）用退路', () => {
  assert.equal(resolveFieldLabel(null, 'name', '姓名', 'zh'), '姓名')
  assert.equal(resolveFieldLabel(undefined, 'name', '姓名', 'zh'), '姓名')
  assert.equal(resolveFieldLabel({}, 'name', '姓名', 'zh'), '姓名')
})
t('標籤：定義沒有這個欄位鍵、或題目是空白用退路', () => {
  assert.equal(resolveFieldLabel(def, 'missing', '退路', 'zh'), '退路')
  assert.equal(resolveFieldLabel(def, 'blank', '退路', 'zh'), '退路')
})
t('標籤：英文頁的定義文字是回退的中文 → 用英文退路，不印出中文；中文頁照用', () => {
  assert.equal(resolveFieldLabel(def, 'untranslated', 'Fallback', 'en'), 'Fallback')
  assert.equal(resolveFieldLabel(def, 'untranslated', '退路', 'zh'), '尚未翻譯的題目')
})
t('選項：顯示文字與送出值同位置對應', () => {
  const opts = resolveFieldOptions(def, 'type', [], 'en')
  assert.deepEqual(opts.map((o) => o.value), ['合作夥伴', '贊助', '兩者'])
  assert.equal(opts[0].label, 'Partnership')
  assert.equal(opts[2].label, 'Both')
})
t('選項：英文頁某項顯示文字是回退的中文 → 取退路中同送出值那一項的英文', () => {
  const fb = [{ value: '贊助', label: 'A sponsorship package' }]
  const opts = resolveFieldOptions(def, 'type', fb, 'en')
  assert.equal(opts[1].value, '贊助')
  assert.equal(opts[1].label, 'A sponsorship package')
  assert.ok(!hasCjk(opts[1].label))
})
t('選項：中文頁顯示文字就是定義值（optionLabels 與 options 相同）', () => {
  const zh = { fields: [{ fieldKey: 'type', options: ['贊助', '兩者'], optionLabels: ['贊助', '兩者'] }] }
  assert.deepEqual(resolveFieldOptions(zh, 'type', [], 'zh'), [{ value: '贊助', label: '贊助' }, { value: '兩者', label: '兩者' }])
})
t('選項：定義取不到或欄位沒有選項 → 整份退路（複本）', () => {
  const fb = [{ value: 'a', label: 'A' }]
  assert.deepEqual(resolveFieldOptions(null, 'type', fb, 'zh'), fb)
  assert.deepEqual(resolveFieldOptions(def, 'name', fb, 'zh'), fb)
  assert.deepEqual(resolveFieldOptions(def, 'missing', fb, 'zh'), fb)
  assert.notEqual(resolveFieldOptions(null, 'type', fb, 'zh')[0], fb[0])
})
t('單一選項顯示文字：以送出值指定；取不到、值不在定義內、英文回退中文都用退路', () => {
  assert.equal(resolveOptionLabel(def, 'type', '合作夥伴', 'fb', 'en'), 'Partnership')
  assert.equal(resolveOptionLabel(def, 'type', '贊助', 'fb', 'en'), 'fb')
  assert.equal(resolveOptionLabel(def, 'type', '不存在', 'fb', 'en'), 'fb')
  assert.equal(resolveOptionLabel(null, 'type', '合作夥伴', 'fb', 'zh'), 'fb')
})

// ── ② 頁面寫死的選項送出值對得上種子 ──
const seed = readFileSync(new URL('../../../db/seed/generate-club-seed-sql.py', import.meta.url), 'utf8')
const listOf = (text) => [...text.matchAll(/"([^"]+)"/g)].map((m) => m[1])
function seedOptions(fieldKey) {
  const m = new RegExp(`field\\("${fieldKey}",\\s*"select",[\\s\\S]*?options=\\[([^\\]]*)\\]`).exec(seed)
  assert.ok(m, `種子找不到 select 欄位 ${fieldKey}`)
  return listOf(m[1])
}
const page = (p) => readFileSync(new URL(`../app/pages/zh/join/${p}/index.vue`, import.meta.url), 'utf8')

t('partnership：洽詢類型退路的送出值＝種子 enquiry_type 選項', () => {
  const src = page('partnership')
  const block = /fieldOptions\('enquiry_type', \[([\s\S]*?)\]\)\)/.exec(src)
  assert.ok(block, '找不到 enquiryTypeOptions 的退路清單')
  const values = [...block[1].matchAll(/value:\s*'([^']+)'/g)].map((m) => m[1])
  assert.deepEqual(values, seedOptions('enquiry_type'))
})
t('academy：ENROLLMENT_CATEGORY_MAP 的送出值都是種子 enrollment_category 的選項（預設或磐石覆寫）', () => {
  const src = page('academy')
  const block = /const ENROLLMENT_CATEGORY_MAP[^{]*\{([\s\S]*?)\n\}/.exec(src)
  assert.ok(block, '找不到 ENROLLMENT_CATEGORY_MAP')
  const values = [...block[1].matchAll(/:\s*'([^']+)'/g)].map((m) => m[1])
  assert.ok(values.length >= 10, `只解析到 ${values.length} 個送出值`)
  const ov = /FORM_FIELD_OVERRIDES = \{[\s\S]*?\(\s*\[([^\]]*)\]/.exec(seed)
  assert.ok(ov, '種子找不到 FORM_FIELD_OVERRIDES')
  const allowed = new Set([...seedOptions('enrollment_category'), ...listOf(ov[1])])
  const bad = values.filter((v) => !allowed.has(v))
  assert.deepEqual(bad, [], `頁面送出值不在種子選項內：${bad.join('、')}`)
})

if (failed) { console.error(`\n${failed} 項失敗`); process.exit(1) }
console.log('\nform-definition 檢查全數通過')
