#!/usr/bin/env node
/**
 * check-shop-lib.mjs — 站內商店（S3-5）、Product 結構化資料（S3-5a）、積分榜與球員數據（S3-9）的純函式固定資料驗證。
 *
 * 比照 `check-schema-batch2.mjs`：不啟動任何服務，直接對 `shared/utils/*.ts` 的純函式做單元測試，掛進 `npm run lint`
 * （`docs/18-work-errors.md` E-34：會被伺服器可用性影響的檢查不能掛進 lint）。驗證的是「規則是否照規格」而不是畫面：
 *
 *   ① 統一編號檢核碼、載具格式、結帳表單檢核（與 `apps/api/Features/Shop/ShopOrderService.cs` 同一規則）；
 *   ② 結帳請求本文**沒有任何金額欄位**（金額一律後端重算，F 批契約）、空欄位不送、載具號碼轉大寫；
 *   ③ 金額格式化（NT$、千分位、非有限數字不輸出 NaN）；
 *   ④ 尺碼表 JSON 容錯解析（認得兩種形狀，其餘回 null，不輸出原始 JSON）；
 *   ⑤ Product／Offer 結構化資料（GEO-05「資料不足時不輸出」、唯一 `@id`、供貨狀態、AggregateOffer）；
 *   ⑥ 積分榜與球員數據顯示規則（助攻 `null` 顯示「—」不是 0；0 是真的 0）；
 *   ⑦ 首頁五大核心價值（API 回空退回備援、依 sortOrder 排序、認不得的 code 不編說明）；
 *   ⑧ B1 區塊內容正規化（只輸出純文字節點、連結白名單、`javascript:` 被拒）。
 *
 * 用法：node scripts/check-shop-lib.mjs　　離開碼：任一斷言失敗 → 1；全部通過 → 0。
 */
import {
  isValidTaxId, validateCheckout, buildCheckoutBody, formatPrice, formatPriceRange, parseSizeChart,
  variantBuyable, newIdempotencyKey, checkoutReturnUrl, isPaidStatus,
} from '../shared/utils/shop.ts'
import { buildProductSchemaNode, isProductSchemaEligible, variantAvailability } from '../shared/utils/shop-schema.ts'
import { statCell, rankCell, safeSeasonParam } from '../shared/utils/standings.ts'
import { buildCoreValueViews, coreValueLearnMorePath, CORE_VALUES_FALLBACK } from '../shared/utils/core-values.ts'
import { normalizePageBlocks, safeBlockHref } from '../shared/utils/page-blocks.ts'

let failures = 0
const eq = (actual, expected, label) => {
  const a = JSON.stringify(actual)
  const e = JSON.stringify(expected)
  if (a !== e) { failures++; console.error(`✗ ${label}\n    預期：${e}\n    實際：${a}`) }
  else console.log(`✓ ${label}`)
}
const ok = (cond, label) => {
  if (!cond) { failures++; console.error(`✗ ${label}`) }
  else console.log(`✓ ${label}`)
}

// ── ① 統編／載具／表單檢核 ──────────────────────────────────────────────
console.log('── ① 檢核規則 ─────────────')
ok(isValidTaxId('04595257'), '統編 04595257（財政部範例）通過')
ok(isValidTaxId('10458575'), '統編 10458575（第 7 碼為 7，總和＋1 例外）通過')
ok(!isValidTaxId('04595258'), '統編檢核碼錯誤不通過')
ok(!isValidTaxId('1234567'), '統編少於 8 碼不通過')
ok(!isValidTaxId('1234567a'), '統編含非數字不通過')

const base = {
  email: 'a@b.co', recipientName: '王小明', recipientPhone: '0912-345-678', deliveryMethod: 'home_delivery',
  recipientAddress: '台中市西屯區…', pickupStore: '', customerNote: '', invoiceType: 'mobile_barcode',
  carrierId: '/ABC+123', taxId: '', donationCode: '',
}
eq(validateCheckout(base), {}, '完整宅配＋手機條碼表單無錯誤')
eq(Object.keys(validateCheckout({ ...base, email: 'bad' })), ['email'], 'Email 格式錯誤')
eq(Object.keys(validateCheckout({ ...base, email: '', recipientName: '', recipientPhone: '' })), ['email', 'recipientName', 'recipientPhone'], '非會員必填三項')
eq(validateCheckout({ ...base, email: '', recipientName: '', recipientPhone: '' }, true), {}, '會員可留空（由伺服器補）')
eq(Object.keys(validateCheckout({ ...base, email: 'bad' }, true)), ['email'], '會員有填就要符合格式')
eq(Object.keys(validateCheckout({ ...base, recipientAddress: '  ' })), ['recipientAddress'], '宅配必填地址')
eq(validateCheckout({ ...base, deliveryMethod: 'onsite_pickup', recipientAddress: '' }), {}, '現場自取不需地址')
eq(Object.keys(validateCheckout({ ...base, deliveryMethod: 'cvs_pickup', recipientAddress: '' })), ['pickupStore'], '超商取貨必填門市')
eq(Object.keys(validateCheckout({ ...base, carrierId: 'ABC1234' })), ['carrierId'], '手機條碼須斜線開頭共 8 碼')
eq(validateCheckout({ ...base, carrierId: '/abc+123' }), {}, '手機條碼小寫視為合法（送出時轉大寫）')
eq(validateCheckout({ ...base, invoiceType: 'citizen_cert', carrierId: 'AB12345678901234' }), {}, '自然人憑證載具 2 英文＋14 數字')
eq(Object.keys(validateCheckout({ ...base, invoiceType: 'citizen_cert', carrierId: 'A123' })), ['carrierId'], '自然人憑證格式錯誤')
eq(Object.keys(validateCheckout({ ...base, invoiceType: 'tax_id', taxId: '04595258' })), ['taxId'], '統編檢核碼錯誤擋下')
eq(validateCheckout({ ...base, invoiceType: 'tax_id', taxId: '04595257' }), {}, '統編正確通過')
eq(Object.keys(validateCheckout({ ...base, invoiceType: 'donation', donationCode: '' })), ['donationCode'], '捐贈碼必選')
eq(Object.keys(validateCheckout({ ...base, customerNote: 'x'.repeat(501) })), ['customerNote'], '備註上限 500 字')

// ── ② 請求本文 ────────────────────────────────────────────────────────
console.log('\n── ② 結帳請求本文 ─────────────')
const body = buildCheckoutBody({ ...base, carrierId: '/abc+123' }, 'zh')
eq(body, {
  deliveryMethod: 'home_delivery', lang: 'zh', invoice: { type: 'mobile_barcode', carrierId: '/ABC+123' },
  email: 'a@b.co', recipientName: '王小明', recipientPhone: '0912-345-678', recipientAddress: '台中市西屯區…',
}, '宅配本文欄位正確、載具轉大寫、空備註不送')
const AMOUNT_KEYS = ['price', 'amount', 'total', 'subtotal', 'shippingFee', 'unitPrice', 'lineTotal']
ok(!Object.keys(body).some(k => AMOUNT_KEYS.includes(k)), '🔴 本文沒有任何金額欄位（金額一律後端重算）')
eq(buildCheckoutBody({ ...base, email: '', recipientName: '', recipientPhone: '' }, 'en').email, undefined, '會員留空的欄位不送')
eq(buildCheckoutBody({ ...base, deliveryMethod: 'cvs_pickup', pickupStore: ' 7-11 某某 ' }, 'zh').pickupStore, '7-11 某某', '超商取貨送 pickupStore、不送 recipientAddress')
eq('recipientAddress' in buildCheckoutBody({ ...base, deliveryMethod: 'cvs_pickup', pickupStore: 'x' }, 'zh'), false, '超商取貨不帶宅配地址')
eq(buildCheckoutBody({ ...base, invoiceType: 'tax_id', taxId: ' 04595257 ' }, 'zh').invoice, { type: 'tax_id', taxId: '04595257' }, '統編發票本文')
eq(buildCheckoutBody({ ...base, invoiceType: 'donation', donationCode: '123' }, 'zh').invoice, { type: 'donation', donationCode: '123' }, '捐贈發票本文')
const key = newIdempotencyKey()
ok(/^[A-Za-z0-9_\-:.]{8,64}$/.test(key), `冪等鍵符合後端形狀（${key.length} 字元）`)
ok(newIdempotencyKey() !== key, '每次產生不同的冪等鍵')
eq(checkoutReturnUrl('https://x.tw', 'zh', 'TR-1', 'confirm'), 'https://x.tw/zh/checkout/complete/?orderNo=TR-1', '付款導回網址（確認）')
eq(checkoutReturnUrl('https://x.tw', 'en', 'TR-1', 'cancel'), 'https://x.tw/en/checkout/complete/?orderNo=TR-1&cancel=1', '付款導回網址（取消）')
ok(isPaidStatus('paid') && !isPaidStatus('pending') && !isPaidStatus('failed') && !isPaidStatus('expired'), '只有 paymentStatus=paid 才算已付款')

// ── ③ 金額 ────────────────────────────────────────────────────────────
console.log('\n── ③ 金額格式化 ─────────────')
eq(formatPrice(1200), 'NT$1,200', 'NT$1,200')
eq(formatPrice(0), 'NT$0', '0 元')
eq(formatPrice(null), '—', 'null 顯示 —')
eq(formatPrice(NaN), '—', 'NaN 不輸出 NT$NaN')
eq(formatPriceRange(120, 120), 'NT$120', '單一價')
eq(formatPriceRange(120, 180), 'NT$120–NT$180', '價格區間')
eq(formatPriceRange(null, null), null, '沒有價格回 null')

// ── ④ 尺碼表 ──────────────────────────────────────────────────────────
console.log('\n── ④ 尺碼表 ─────────────')
eq(parseSizeChart({ columns: ['尺碼', '胸圍'], rows: [['M', 102], ['L', 106]], unit: 'cm' }),
  { caption: null, headers: ['尺碼', '胸圍'], rows: [['M', '102'], ['L', '106']], note: '單位：cm' }, '欄名＋陣列列（unit 轉備註）')
eq(parseSizeChart([{ 尺碼: 'M', 胸圍: 102 }, { 尺碼: 'L', 胸圍: 106 }]),
  { caption: null, headers: ['尺碼', '胸圍'], rows: [['M', '102'], ['L', '106']], note: null }, '物件陣列：欄名取第一列鍵')
eq(parseSizeChart(null), null, 'null → null')
eq(parseSizeChart({ foo: 1 }), null, '認不得的形狀 → null（不輸出原始 JSON）')
eq(parseSizeChart({ rows: [['M', 1]] }), null, '陣列列沒有欄名 → null')
eq(parseSizeChart('not json'), null, '非 JSON 字串 → null')

// ── ⑤ Product／Offer ──────────────────────────────────────────────────
console.log('\n── ⑤ Product／Offer 結構化資料 ─────────────')
const v = (sku, price, qty, purchasable = true) => ({ id: `id-${sku}`, sku, size: null, colour: null, label: sku, listPrice: price, price, onSale: false, availableQty: qty, purchasable })
const product = (variants, extra = {}) => ({
  slug: 'socks', name: '厚底緩震機能襪', narrative: '<b>柔軟</b>舒適', seoTitle: null, seoDescription: null, collectionSlug: null, collectionName: null,
  tags: [], isNewArrival: false, sizeChart: null, images: [{ url: 'https://cdn.example.com/a.webp', thumbUrl: 'https://cdn.example.com/a-t.webp', width: 1, height: 1 }],
  variants, priceMin: 120, priceMax: 120, listPriceMin: null, onSale: false, stockStatus: 'in_stock', stockStatusLabel: '供貨中', ...extra,
})
const opts = { brandName: '台中磐石', pageUrl: 'https://tcrfc.tw/zh/shop/socks/', sellerName: '台中磐石足球俱樂部' }

const single = buildProductSchemaNode(product([v('S-1', 120, 10)]), opts)
ok(single !== null, '單一規格合格')
eq(single?.['@id'], 'product-socks', 'Product @id 唯一且由 slug 決定')
eq(single?.offers?.['@type'], 'Offer', '單一規格 → 單一 Offer')
eq(single?.offers?.priceCurrency, 'TWD', '幣別 TWD')
eq(single?.offers?.price, 120, '價格為數字')
eq(single?.offers?.availability, 'https://schema.org/InStock', '可售量充足 → InStock')
eq(single?.description, '柔軟舒適', '描述去除 HTML 標籤')
eq(single?.offers?.['@id'], 'https://tcrfc.tw/zh/shop/socks/#offer-S-1', 'Offer @id 為含 sku 的絕對網址，與 Product 不同')
eq(single?.offers?.seller, { '@type': 'Organization', name: '台中磐石足球俱樂部' }, '有收款主體才輸出 seller')
eq(buildProductSchemaNode(product([v('S-1', 120, 10)]), { ...opts, sellerName: null })?.offers?.seller, undefined, '沒有收款主體不輸出 seller')

const multi = buildProductSchemaNode(product([v('A', 120, 10), v('B', 180, 2), v('C', 150, 0), v('D', 160, 5, false)]), opts)
eq(multi?.offers?.['@type'], 'AggregateOffer', '多規格 → AggregateOffer')
eq([multi?.offers?.lowPrice, multi?.offers?.highPrice, multi?.offers?.offerCount], [120, 180, 4], 'low／high／count')
eq(multi?.offers?.offers.map(o => o.availability), [
  'https://schema.org/InStock', 'https://schema.org/LimitedAvailability', 'https://schema.org/OutOfStock', 'https://schema.org/OutOfStock',
], '供貨狀態：充足／緊張(<5)／0 件／不可購買')
eq(multi?.offers?.availability, 'https://schema.org/InStock', 'AggregateOffer 任一規格可購買 → InStock')
const allSold = buildProductSchemaNode(product([v('A', 120, 0), v('B', 180, 0)]), opts)
eq(allSold?.offers?.availability, 'https://schema.org/OutOfStock', '🔴 全部規格售完 → AggregateOffer 明確 OutOfStock（不讓模組補預設 InStock）')
const ids = [multi?.['@id'], multi?.offers?.['@id'], ...multi.offers.offers.map(o => o['@id'])]
ok(new Set(ids).size === ids.length, '🔴 同頁所有節點 @id 唯一（E-96）')
eq(variantAvailability({ purchasable: true, availableQty: 5 }), 'https://schema.org/InStock', '剛好 5 件仍是 InStock')

ok(!isProductSchemaEligible(product([])), '🔴 沒有任何規格 → 不輸出')
ok(buildProductSchemaNode(product([v('Z', 0, 5)]), opts) === null, '🔴 價格為 0 → 不輸出（不編價格）')
ok(buildProductSchemaNode(product([v('Z', NaN, 5)]), opts) === null, '價格非有限數字 → 不輸出')
ok(buildProductSchemaNode(product([v('Z', 100, 5)], { name: '  ' }), opts) === null, '沒有名稱 → 不輸出')
eq(buildProductSchemaNode(product([v('Z', 100, 5)], { images: [] }), opts)?.image, undefined, '沒有圖片不帶 image')
eq(buildProductSchemaNode(product([v('Z', 100, 5)], { images: [{ url: 'javascript:alert(1)', thumbUrl: 'x', width: 1, height: 1 }] }), opts)?.image, undefined, '非 http(s) 圖片網址不輸出')
ok(!('description' in (buildProductSchemaNode(product([v('Z', 100, 5)], { narrative: null }), opts) ?? {})), '沒有描述不帶 description')
ok(variantBuyable(v('Q', 1, 1)) && !variantBuyable(v('Q', 1, 0)) && !variantBuyable(v('Q', 1, 3, false)), 'variantBuyable：可購買且有庫存才算')

// ── ⑥ 積分榜與球員數據 ────────────────────────────────────────────────
console.log('\n── ⑥ 積分榜與球員數據顯示 ─────────────')
eq(statCell(null), '—', '助攻 null 顯示 —')
eq(statCell(undefined), '—', 'undefined 顯示 —')
eq(statCell(0), '0', '真實的 0 顯示 0（不是 —）')
eq(statCell(7), '7', '一般數字')
eq(rankCell(null), '—', '沒有名次顯示 —')
eq(safeSeasonParam('2026-27'), '2026-27', '球季 2026-27')
eq(safeSeasonParam('2026/27'), '2026/27', '球季 2026/27')
eq(safeSeasonParam('<script>'), '', '不合法球季參數視為沒指定')
eq(safeSeasonParam(['a']), '', '陣列參數視為沒指定')

// ── ⑦ 核心價值 ────────────────────────────────────────────────────────
console.log('\n── ⑦ 首頁五大核心價值 ─────────────')
eq(buildCoreValueViews([]).map(x => x.code), CORE_VALUES_FALLBACK.map(x => x.code), 'API 回空 → 備援同順序五項')
eq(buildCoreValueViews(null).length, 5, 'null → 備援')
const shuffled = [
  { code: 'integrity', nameZh: '誠信專業', nameEn: 'Integrity', sortOrder: 5, learnMorePageSlug: 'about/philosophy' },
  { code: 'players_first', nameZh: '以球員為本', nameEn: 'Players First', sortOrder: 1, learnMorePageSlug: 'about/philosophy' },
  { code: 'new_code', nameZh: '新價值', nameEn: 'New', sortOrder: 3, learnMorePageSlug: null },
]
const views = buildCoreValueViews(shuffled)
eq(views.map(x => [x.num, x.code]), [['01', 'players_first'], ['02', 'new_code'], ['03', 'integrity']], '依 sortOrder 排序並重新編號')
eq(views[1].desc, '', '認不得的 code 不編說明文字')
ok(views[0].desc.length > 0, '已知 code 有說明文字')
eq(coreValueLearnMorePath(shuffled), '/zh/about/philosophy/', '了解更多連結 → 2.3 足球理念')
eq(coreValueLearnMorePath([{ ...shuffled[0], learnMorePageSlug: 'javascript:alert(1)' }]), null, '不合法 slug 不輸出連結')

// ── ⑧ B1 區塊 ─────────────────────────────────────────────────────────
console.log('\n── ⑧ B1 區塊正規化 ─────────────')
const nodes = normalizePageBlocks([
  { blockType: 'cta', sortOrder: 3, content: { text: '支持我們', buttonLabel: '前往', buttonUrl: '/zh/charity/' } },
  { blockType: 'text', sortOrder: 1, content: { body: '第一段\n\n第二段' } },
  { blockType: 'gallery', sortOrder: 2, content: { images: [] } },
  { blockType: 'cta', sortOrder: 4, content: { text: 'x', buttonLabel: '壞', buttonUrl: 'javascript:alert(1)' } },
  { blockType: 'text', sortOrder: 5, content: { body: '   ' } },
  { blockType: 'table', sortOrder: 6, content: { headers: ['A'], rows: [[1, 'b']] } },
])
eq(nodes.map(n => n.kind), ['text', 'cta', 'table'], '依 sortOrder 排序；不渲染型別、空文字、危險連結的區塊被略過')
eq(nodes[0].paragraphs, ['第一段', '第二段'], '文字區塊依空行分段')
eq(nodes[2].rows, [['1', 'b']], '表格儲存格轉字串')
eq([safeBlockHref('/zh/x/'), safeBlockHref('//evil.com'), safeBlockHref('https://a.tw'), safeBlockHref('http://a.tw'), safeBlockHref('data:text/html,1')], ['/zh/x/', null, 'https://a.tw', null, null], '連結白名單：站內路徑與 https')
eq(normalizePageBlocks(undefined), [], 'undefined → 空')

console.log(`\n${failures === 0 ? '✓' : '✗'} 共 ${failures} 項斷言失敗。`)
if (failures > 0) process.exit(1)
