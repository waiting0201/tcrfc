// deploy/loadtest/read.js — 唯讀壓測：主站與藍鯨前台熱門頁、公開 API、慈善唯讀頁、商店目錄。
//
// 只送 GET，不寫任何資料，也不會觸發 IP 限流（news／schedule／faqs／shop 目錄的 GET 沒掛限流政策；
// 慈善只有 GET /donations/{orderNo} 有 120 次／分鐘／IP，本腳本不打它）。
//
//   k6 run -e PROFILE=smoke deploy/loadtest/read.js
//   k6 run -e PROFILE=load  --summary-export=read-load.json deploy/loadtest/read.js
//
// 預設目標是 *.4webdemo.com 測試站，見 lib.js 的網域防呆。

import http from 'k6/http';
import { check, sleep } from 'k6';
import {
  TARGETS, CLUB_TCRFC, CLUB_BW, HEADERS, assertSafeTargets, buildScenarios, thresholdsFor, pick,
} from './lib.js';

assertSafeTargets();

// 場景權重（加總 1）與門檻種類。權重是「官網日常流量」的粗估：主站最多、藍鯨次之。
const SCENARIOS = {
  web_main: { weight: 0.35, exec: 'webMain', kind: 'page' },
  web_bw: { weight: 0.15, exec: 'webBw', kind: 'page' },
  api_public: { weight: 0.25, exec: 'apiPublic', kind: 'api' },
  charity_read: { weight: 0.1, exec: 'charityRead', kind: 'page' },
  shop_catalog: { weight: 0.15, exec: 'shopCatalog', kind: 'api' },
};

export const options = {
  scenarios: buildScenarios(SCENARIOS),
  thresholds: thresholdsFor(Object.fromEntries(Object.entries(SCENARIOS).map(([k, v]) => [k, v.kind]))),
  summaryTrendStats: ['avg', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

// 頁面清單；藍鯨不設部分單元，路徑可用環境變數覆寫（逗號分隔）。先跑 smoke 確認每一條都是 200。
const WEB_PATHS = (__ENV.WEB_PATHS || '/zh/,/zh/news/,/zh/schedule/,/zh/faq/,/zh/shop/,/zh/about/,/zh/academy/,/zh/join/').split(',');
const BW_PATHS = (__ENV.BW_PATHS || '/zh/,/zh/news/,/zh/schedule/,/zh/faq/,/zh/shop/').split(',');
const CHARITY_PATHS = (__ENV.CHARITY_PATHS || '/zh/,/en/,/zh/donors/,/zh/privacy/,/zh/terms/').split(',');

// setup：抓一次動態內容的 slug，讓詳情頁有真實網址可打。抓不到就退回只打列表（不因為沒資料而中止）。
export function setup() {
  const slugs = { news: [], products: [], projects: [] };
  const grab = (url, into) => {
    const res = http.get(url, { headers: HEADERS, tags: { name: 'setup' } });
    if (res.status !== 200) return;
    try {
      const body = res.json();
      const items = Array.isArray(body) ? body : body.items || [];
      for (const it of items.slice(0, 20)) if (it.slug) into.push(it.slug);
    } catch (_) { /* 非 JSON 就略過 */ }
  };
  grab(`${TARGETS.api}/api/v1/${CLUB_TCRFC}/news?pageSize=20`, slugs.news);
  grab(`${TARGETS.api}/api/v1/${CLUB_TCRFC}/shop/products?pageSize=20`, slugs.products);
  grab(`${TARGETS.api}/api/v1/donation-platform/projects?lang=zh`, slugs.projects);
  return slugs;
}

function getPage(base, path, name) {
  const res = http.get(`${base}${path}`, { headers: HEADERS, tags: { name } });
  check(res, {
    '頁面 200': (r) => r.status === 200,
    '頁面有內容': (r) => !!r.body && r.body.length > 500,
  });
  return res;
}

function getApi(path, name) {
  const res = http.get(`${TARGETS.api}${path}`, { headers: { ...HEADERS, Accept: 'application/json' }, tags: { name } });
  check(res, { 'API 200': (r) => r.status === 200 });
  return res;
}

// 使用者在頁面間的停留時間；不加 sleep 等於每個 VU 無間隔洗請求，結果不真實
const think = () => sleep(1 + Math.random() * 3);

export function webMain(data) {
  const path = pick(WEB_PATHS);
  getPage(TARGETS.web, path, `web ${path}`);
  if (path === '/zh/news/' && data.news.length) getPage(TARGETS.web, `/zh/news/${pick(data.news)}/`, 'web news detail');
  think();
}

export function webBw() {
  const path = pick(BW_PATHS);
  getPage(TARGETS.bw, path, `bw ${path}`);
  think();
}

export function apiPublic(data) {
  const r = Math.random();
  if (r < 0.3) getApi(`/api/v1/${CLUB_TCRFC}/news?pageSize=20&lang=zh`, 'api news list');
  else if (r < 0.45 && data.news.length) getApi(`/api/v1/${CLUB_TCRFC}/news/${pick(data.news)}?lang=zh`, 'api news detail');
  else if (r < 0.7) getApi(`/api/v1/${CLUB_TCRFC}/schedule?pageSize=20&lang=zh`, 'api schedule');
  else if (r < 0.85) getApi(`/api/v1/${CLUB_BW}/schedule?pageSize=20&lang=zh`, 'api schedule bw');
  else getApi(`/api/v1/${CLUB_TCRFC}/faqs?pageSize=50&lang=zh`, 'api faqs');
  sleep(0.5 + Math.random() * 1.5);
}

export function charityRead(data) {
  const r = Math.random();
  if (r < 0.5) {
    getPage(TARGETS.charity, pick(CHARITY_PATHS), 'charity page');
  } else if (r < 0.75 && data.projects.length) {
    getPage(TARGETS.charity, `/zh/p/${pick(data.projects)}/`, 'charity project page');
  } else if (__ENV.CHARITY_STORE_SLUG) {
    // 掃碼落地頁：店家 slug 無法從公開 API 列舉，需由使用者提供一個真實有效的
    getPage(TARGETS.charity, `/zh/s/${__ENV.CHARITY_STORE_SLUG}/`, 'charity store landing');
  } else {
    getApi('/api/v1/donation-platform/projects?lang=zh', 'api charity projects');
  }
  think();
}

export function shopCatalog(data) {
  const r = Math.random();
  if (r < 0.15) getApi(`/api/v1/${CLUB_TCRFC}/shop/info?lang=zh`, 'api shop info');
  else if (r < 0.3) getApi(`/api/v1/${CLUB_TCRFC}/shop/collections?lang=zh`, 'api shop collections');
  else if (r < 0.7) getApi(`/api/v1/${CLUB_TCRFC}/shop/products?pageSize=24&lang=zh`, 'api shop products');
  else if (data.products.length) getApi(`/api/v1/${CLUB_TCRFC}/shop/products/${pick(data.products)}?lang=zh`, 'api shop product');
  else getPage(TARGETS.web, '/zh/shop/', 'web shop');
  think();
}
