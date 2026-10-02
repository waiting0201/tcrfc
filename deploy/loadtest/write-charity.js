// deploy/loadtest/write-charity.js — 慈善捐款「寫入」流程壓測：建單 → 發起付款 → 確認 → 查結果。
//
// 🔴 這支會在資料庫寫入真實的捐款資料列（donor_name 以 LOADTEST 開頭、Email 用 example.invalid），
//    且測試站與正式 VM 是同一台（docs/17 §10.1，全專案只有本機與正式兩套環境），所以：
//    ① 沒有明確旗標一律拒絕執行：必須 -e LOADTEST_ENABLE_WRITES=yes
//    ② 只走假金流 FakePaymentGateway（交易識別碼 FAKE- 開頭，不碰 LINE Pay）。伺服器端要先設
//       CHARITY_ALLOW_FAKE_PROVIDERS=true，否則 /pay 會回 503，本腳本會偵測到並中止，不會假裝成功
//    ③ 目標限 *.4webdemo.com 與本機（lib.js）
//    跑完要依 README「清除壓測資料」處理。
//
// 🔴 IP 限流：POST 類 30 次／10 分鐘／IP，一輪（建單＋付款＋確認）吃 3 次，單一 IP 一輪 10 分鐘最多 10 筆。
//    預設（PROFILE=smoke）刻意只跑在額度內，驗的是「流程正確」；要驗併發，須先依 README 暫時調高伺服器的
//    CHARITY_PUBLIC_WRITE_RATE_LIMIT_PERMITS／CHARITY_PUBLIC_READ_RATE_LIMIT_PERMITS，跑完改回。
//
//   k6 run -e LOADTEST_ENABLE_WRITES=yes deploy/loadtest/write-charity.js
//   k6 run -e LOADTEST_ENABLE_WRITES=yes -e PROFILE=load -e ALLOW_RATE_LIMIT_RAISED=yes deploy/loadtest/write-charity.js

import http from 'k6/http';
import { check, fail, sleep } from 'k6';
import { Counter } from 'k6/metrics';
import { TARGETS, HEADERS, assertSafeTargets, buildScenarios, PROFILE_NAME } from './lib.js';

assertSafeTargets();

if (__ENV.LOADTEST_ENABLE_WRITES !== 'yes') {
  throw new Error('拒絕執行：寫入類壓測需要明確旗標 -e LOADTEST_ENABLE_WRITES=yes（會在資料庫建立捐款資料列，見檔頭說明）。');
}

// smoke 以外的曲線會超過單一 IP 的限流額度，除非使用者聲明已暫時調高伺服器限流
const RAISED = __ENV.ALLOW_RATE_LIMIT_RAISED === 'yes';
if (PROFILE_NAME !== 'smoke' && !RAISED) {
  throw new Error('PROFILE 不是 smoke 會超過單一 IP 的寫入限流（30 次／10 分鐘），請先照 README 暫時調高伺服器限流，再加 -e ALLOW_RATE_LIMIT_RAISED=yes。');
}

const API = `${TARGETS.api}/api/v1/donation-platform`;
const rateLimited = new Counter('loadtest_rate_limited_429');
const donationsCreated = new Counter('loadtest_donations_created');

export const options = {
  scenarios: PROFILE_NAME === 'smoke'
    // 額度內：每 70 秒一輪、最多 8 輪（共 24 次寫入，< 30 次／10 分鐘）
    ? { donate_flow: { executor: 'constant-arrival-rate', rate: 1, timeUnit: '70s', duration: '9m', preAllocatedVUs: 2, maxVUs: 2, exec: 'donateFlow' } }
    : buildScenarios({ donate_flow: { weight: 1, exec: 'donateFlow' } }),
  thresholds: {
    // 429 在預設額度內不該出現；出現代表限流被打到（也可能是別人共用同一個出口 IP）
    loadtest_rate_limited_429: PROFILE_NAME === 'smoke' ? ['count==0'] : ['count<1'],
    'http_req_duration{name:create}': ['p(95)<1500'],
    'http_req_duration{name:pay}': ['p(95)<1500'],
    'http_req_duration{name:confirm}': ['p(95)<1500'],
    'http_req_duration{name:result}': ['p(95)<800'],
    checks: ['rate>0.99'],
  },
  summaryTrendStats: ['avg', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

const JSON_HEADERS = { ...HEADERS, 'Content-Type': 'application/json', Accept: 'application/json' };

export function setup() {
  const list = http.get(`${API}/projects?lang=zh`, { headers: JSON_HEADERS });
  if (list.status !== 200) fail(`取不到項目清單（HTTP ${list.status}）`);
  const projects = list.json();
  if (!Array.isArray(projects) || projects.length === 0) fail('沒有已上架的捐款項目，無法壓測捐款流程');
  const slug = __ENV.CHARITY_PROJECT_SLUG || projects[0].slug;
  const detail = http.get(`${API}/projects/${slug}?lang=zh`, { headers: JSON_HEADERS });
  if (detail.status !== 200) fail(`取不到項目 ${slug}（HTTP ${detail.status}）`);
  const d = detail.json();
  return { slug, amount: d.minAmount, invoiceMode: d.invoiceMode, storeSlug: __ENV.CHARITY_STORE_SLUG || null };
}

function invoiceFor(mode) {
  // 兩種憑證模式各給最小必要欄位；載具條碼為格式合法的假值
  if (mode === 'donation_receipt') return { receiptTitle: 'LOADTEST' };
  return { type: 'mobile_carrier', mobileCarrier: '/LT00000' };
}

export function donateFlow(data) {
  const tag = `${Date.now()}-${__VU}-${__ITER}`;
  const create = http.post(
    `${API}/donations`,
    JSON.stringify({
      projectSlug: data.slug,
      storeSlug: data.storeSlug,
      amount: data.amount,
      donorName: `LOADTEST ${tag}`,
      donorEmail: `loadtest+${tag}@example.invalid`,
      isAnonymous: true,
      consentPrivacy: true,
      invoice: invoiceFor(data.invoiceMode),
      lang: 'zh',
      // Turnstile 尚未啟用時可省略；啟用後壓測機會被擋，需另行處理（README 疑難排解）
    }),
    { headers: { ...JSON_HEADERS, 'Idempotency-Key': `loadtest-${tag}` }, tags: { name: 'create' } },
  );
  if (create.status === 429) { rateLimited.add(1); return; }
  if (!check(create, { '建單 201': (r) => r.status === 201 })) return;
  donationsCreated.add(1);
  const orderNo = create.json().orderNo;

  const pay = http.post(`${API}/donations/${orderNo}/pay`, JSON.stringify({ lang: 'zh' }), { headers: JSON_HEADERS, tags: { name: 'pay' } });
  if (pay.status === 429) { rateLimited.add(1); return; }
  if (pay.status === 503) fail('發起付款回 503：伺服器沒有設定 CHARITY_ALLOW_FAKE_PROVIDERS=true（假金流未啟用），中止壓測，不會假裝成功');
  if (!check(pay, { '發起付款 200': (r) => r.status === 200 })) return;

  const url = pay.json().paymentUrl || '';
  const m = /transactionId=([^&]+)/.exec(url);
  const transactionId = m ? decodeURIComponent(m[1]) : null;
  if (!check(transactionId, { '付款網址帶假交易識別碼': (t) => !!t && t.startsWith('FAKE-') })) {
    // 不是假金流的交易識別碼＝可能接到真金流，立刻中止，絕不確認
    fail('付款網址的交易識別碼不是 FAKE- 開頭，疑似接到真實金流，立即中止');
  }

  sleep(1);
  const confirm = http.post(`${API}/donations/${orderNo}/confirm`, JSON.stringify({ transactionId }), { headers: JSON_HEADERS, tags: { name: 'confirm' } });
  if (confirm.status === 429) { rateLimited.add(1); return; }
  check(confirm, { '確認 200': (r) => r.status === 200 });

  const result = http.get(`${API}/donations/${orderNo}?lang=zh`, { headers: JSON_HEADERS, tags: { name: 'result' } });
  if (result.status === 429) { rateLimited.add(1); return; }
  check(result, { '結果頁 200': (r) => r.status === 200 });
}
