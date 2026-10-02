// deploy/loadtest/lib.js — 壓測共用：目標網址、網域防呆、負載曲線、通過門檻。
// 對應 docs/17 §6「資料庫層級與容量」，操作手冊在 deploy/loadtest/README.md。
//
// 🔴 目標預設是測試站（*.4webdemo.com），而且「只允許」測試站與本機：
//    網域防呆沒有覆寫旗標——要打別的網域請先改這支檔案並讓人審查，不要靠環境變數繞過。

// ── 目標（可用環境變數覆寫；覆寫後仍受網域防呆限制） ───────────────────────────
export const TARGETS = {
  web: __ENV.WEB_BASE || 'https://tcrfc.4webdemo.com',
  bw: __ENV.BW_BASE || 'https://tcrfc-bw.4webdemo.com',
  charity: __ENV.CHARITY_BASE || 'https://tcrfc-charity.4webdemo.com',
  api: __ENV.API_BASE || 'https://tcrfc-api.4webdemo.com',
};

export const CLUB_TCRFC = __ENV.CLUB_TCRFC || 'tcrfc';
export const CLUB_BW = __ENV.CLUB_BW || 'bw';

const ALLOWED_SUFFIXES = ['.4webdemo.com', '.localhost'];
const ALLOWED_HOSTS = ['localhost', '127.0.0.1'];

function hostOf(url) {
  const m = /^https?:\/\/([^/:?#]+)/i.exec(url);
  return m ? m[1].toLowerCase() : '';
}

/** 在 init 階段呼叫：任何目標不是測試站或本機就直接丟例外，k6 不會送出任何請求。 */
export function assertSafeTargets() {
  for (const [name, url] of Object.entries(TARGETS)) {
    const host = hostOf(url);
    const ok = ALLOWED_HOSTS.includes(host) || ALLOWED_SUFFIXES.some((s) => host.endsWith(s));
    if (!ok) {
      throw new Error(
        `拒絕執行：${name} 目標 "${url}" 不是 *.4webdemo.com 或本機。壓測不得打正式網域（docs/17 §6、deploy/loadtest/README.md）。`,
      );
    }
  }
}

// ── 負載曲線 ──────────────────────────────────────────────────────────────
// 峰值併發虛擬使用者數（全部場景合計），再依各場景權重分配。
// 數字是執行層預設：俱樂部官網的日常併發遠低於此，stress 用來找拐點而不是模擬日常。
const PROFILES = {
  // 冒煙：確認每個網址都 200、腳本沒寫錯。不算壓測。
  smoke: { peak: 2, ramp: '10s', hold: '30s', down: '5s' },
  // 一般負載：預期的活動高峰（賽事公告、開賣當下）。
  load: { peak: 40, ramp: '2m', hold: '8m', down: '1m' },
  // 壓力：找拐點，CPU 額度與 DTU 會被吃光，執行時要盯 README 的指標。
  stress: { peak: 120, ramp: '5m', hold: '5m', down: '2m' },
  // 浸泡：中等負載跑久一點，看記憶體、連線池、CPU 額度是否緩慢流失。
  soak: { peak: 25, ramp: '2m', hold: '30m', down: '1m' },
};

export const PROFILE_NAME = __ENV.PROFILE || 'smoke';

export function profile() {
  const p = PROFILES[PROFILE_NAME];
  if (!p) {
    throw new Error(`未知的 PROFILE "${PROFILE_NAME}"，可用：${Object.keys(PROFILES).join(', ')}`);
  }
  const scale = Number(__ENV.PEAK_SCALE || '1'); // 例如 0.5 先打一半
  return { ...p, peak: Math.max(1, Math.round(p.peak * scale)) };
}

/** 依權重建立 ramping-vus 場景。weights: { 場景名: { weight, exec } }，weight 總和以 1 為準。 */
export function buildScenarios(weights) {
  const p = profile();
  const scenarios = {};
  for (const [name, { weight, exec }] of Object.entries(weights)) {
    const target = Math.max(1, Math.ceil(p.peak * weight));
    scenarios[name] = {
      executor: 'ramping-vus',
      exec,
      startVUs: 0,
      stages: [
        { duration: p.ramp, target },
        { duration: p.hold, target },
        { duration: p.down, target: 0 },
      ],
      gracefulRampDown: '15s',
      gracefulStop: '30s',
    };
  }
  return scenarios;
}

// ── 通過門檻（README「通過標準」同步，改這裡要一起改文件） ──────────────────────
// 前台 SSR 頁：p95 < 1.5s、p99 < 3s；公開 API：p95 < 500ms、p99 < 1.5s；失敗率 < 1%。
// 注意：經 Cloudflare，數字含壓測機到邊緣的網路時間。
export function thresholdsFor(kinds) {
  const t = {
    http_req_failed: ['rate<0.01'],
    checks: ['rate>0.99'],
  };
  for (const [s, kind] of Object.entries(kinds)) {
    const [p95, p99] = kind === 'api' ? [500, 1500] : [1500, 3000];
    t[`http_req_duration{scenario:${s}}`] = [`p(95)<${p95}`, `p(99)<${p99}`];
    // 失敗率爆表就別再繼續打了（冒煙除外）
    t[`http_req_failed{scenario:${s}}`] = [
      { threshold: 'rate<0.05', abortOnFail: PROFILE_NAME !== 'smoke', delayAbortEval: '1m' },
    ];
  }
  return t;
}

export function pick(list) {
  return list[Math.floor(Math.random() * list.length)];
}

/** 標準請求標頭：帶可辨識的 User-Agent，事後在 Caddy／Cloudflare 日誌容易過濾。 */
export const HEADERS = {
  'User-Agent': 'tcrfc-loadtest/1 (k6)',
  Accept: 'text/html,application/json',
};
