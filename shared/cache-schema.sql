-- shared/cache-schema.sql — App 本機 SQLite 快取的共用 DDL（手寫，兩端共用同一份）
--
-- 消費者：iOS GRDB.swift 的 DatabaseMigrator／Android SQLDelight 的 .sq／.sqm（docs/19 §3）。
-- 來源：docs/19 §3（cache_meta）、§4（member_card）、§6.4（ad_event_queue）；App 規劃書 §2.4 時效表。
-- 這是執行層產物，不是規格：要新增欄位語意先改規劃書。
--
-- ── Migration 規則 ──────────────────────────────────────────────────────────
--   1. 本檔由「migration 區塊」組成；每個區塊以單獨一行 `-- migration: <N> <名稱>` 起頭，N 從 1 連續遞增。
--   2. 已發佈的區塊不得修改；要改結構一律新增下一個區塊（ALTER TABLE／建新表搬資料）。
--   3. 兩端把區塊依序註冊為 migration（GRDB 用名稱 `m<N>_<名稱>`；SQLDelight 轉成 <N-1>.sqm）。
--   4. shared/scripts/check-shared.py 會在記憶體 SQLite 依序套用全部區塊並檢查表／欄位（CI 漂移檢查的一部分）。
--
-- ── 絕對禁止出現在本檔的欄位 ────────────────────────────────────────────────
--   token／qr／member_token：會員卡 token 是撤銷憑證，只能放安全儲存區（Keychain／Tink），不得進 SQLite 明文（docs/19 §4）。
--   lat／lng／latitude／longitude／coordinate：座標只存在記憶體（docs/19 §7、規劃書 §3.8 規則 4）。
--   member_id／ip／advertising_id／idfa／gaid：廣告事件不得帶個資與廣告識別碼（docs/19 §6.4、規劃書 §7.8）。
--   shared/scripts/check-shared.py 會掃描欄位名稱，出現即失敗。
--
-- 時間一律存 UTC ISO 8601 文字（TEXT，例如 2026-10-02T04:30:00.000Z）；單調時鐘存毫秒整數（INTEGER）。

-- migration: 1 init

-- 統一承載規劃書 §2.4 的時效表。TTL 值不寫死在程式，由 shared/cache-policy.json 產生常數。
CREATE TABLE cache_meta (
  cache_key        TEXT    NOT NULL PRIMARY KEY,   -- 例：matches、news_list、partner_stores（見 cache-policy.json 的 key）
  etag             TEXT,                           -- 伺服器 ETag；收 304 時只更新 fetched_at_utc
  fetched_at_utc   TEXT    NOT NULL,               -- 最後一次成功取得的『伺服器時間』（取自回應的 Date 標頭，不是裝置時鐘）
  ttl_sec          INTEGER,                        -- NULL＝永久；0＝不快取
  server_time_utc  TEXT                            -- 該次回應的伺服器時間（供 server_skew 與逾時判斷）
) WITHOUT ROWID;

-- 內容快取的通用載體：一筆＝一個列表或一則內容的 JSON（DTO 序列化後的字串）。
-- 內容欄位維持 JSON 而不拆欄，是因為 DTO 以 shared/openapi.json 為真實來源，欄位一變就要改 DDL 太脆。
CREATE TABLE cache_item (
  cache_key        TEXT    NOT NULL,               -- 對應 cache_meta.cache_key（列表）或內容類別（詳情，例：news_detail）
  item_key         TEXT    NOT NULL,               -- 列表用固定字串 'list'；詳情用 slug 或 id
  lang             TEXT    NOT NULL DEFAULT 'zh',  -- zh／en（內容有雙語欄位時分開存）
  payload_json     TEXT    NOT NULL,
  fetched_at_utc   TEXT    NOT NULL,
  PRIMARY KEY (cache_key, item_key, lang)
) WITHOUT ROWID;

CREATE INDEX idx_cache_item_fetched ON cache_item (cache_key, fetched_at_utc);

-- 電子會員卡：每份會籍一張卡，各自有自己的 last_verified_at（docs/19 §4）。
-- 🔴 沒有 token 欄位——token 存安全儲存區。QR 內容 ＝ {官網 base}/m/{token}，完全在本機組出。
CREATE TABLE member_card (
  card_id                     TEXT    NOT NULL PRIMARY KEY,  -- MemberCardDto.id
  membership_id               TEXT    NOT NULL,              -- MemberCardDto.membershipId
  club_code                   TEXT    NOT NULL,              -- 俱樂部代碼（卡面標誌與品牌色依此選）
  member_no                   TEXT    NOT NULL,
  holder_name                 TEXT    NOT NULL,
  tier                        TEXT    NOT NULL,              -- registered／fan_club
  tier_label                  TEXT,
  valid_until                 TEXT,                          -- 有效期限（日期）
  status                      TEXT    NOT NULL,              -- 伺服器回傳的狀態代碼；顯示時必須與最後同步時間並陳
  status_label                TEXT,
  last_verified_server_utc    TEXT    NOT NULL,              -- 『伺服器回傳會籍狀態成功的那一刻』，取自回應中的伺服器時間
  last_verified_monotonic_ms  INTEGER NOT NULL,              -- 當時的單調時鐘（CLOCK_MONOTONIC／SystemClock.elapsedRealtime()）
  updated_at_utc              TEXT    NOT NULL
) WITHOUT ROWID;

CREATE INDEX idx_member_card_membership ON member_card (membership_id);

-- 追蹤偏好：本機儲存，登入後與帳號同步（規劃書 §2.4）。
CREATE TABLE follow_preference (
  target_type      TEXT    NOT NULL,               -- club／team／category 等追蹤對象種類
  target_code      TEXT    NOT NULL,
  enabled          INTEGER NOT NULL DEFAULT 1,     -- 0／1
  updated_at_utc   TEXT    NOT NULL,
  PRIMARY KEY (target_type, target_code)
) WITHOUT ROWID;

-- 廣告事件離線佇列（docs/19 §6.4）。🔴 不得放 member_id、完整 IP、座標、廣告識別碼。
CREATE TABLE ad_event_queue (
  id               INTEGER PRIMARY KEY AUTOINCREMENT,
  type             TEXT    NOT NULL CHECK (type IN ('impression', 'click')),
  creative_id      TEXT    NOT NULL,
  campaign_id      TEXT,
  slot_code        TEXT    NOT NULL,
  presentation_id  TEXT,                           -- 曝光必填、點擊可空（對應 AppAdEventInput.presentationId）
  occurred_at_utc  TEXT    NOT NULL,               -- 單調時鐘推算 ＋ server_skew（docs/19 §6.1 時鐘校正）
  platform         TEXT    NOT NULL CHECK (platform IN ('ios', 'android')),
  lang             TEXT    NOT NULL,
  club_id          TEXT,
  send_state       TEXT    NOT NULL DEFAULT 'pending' CHECK (send_state IN ('pending', 'inflight')),
  inflight_since_ms INTEGER                        -- 單調時鐘；inflight 超過 60 秒回 pending
);

CREATE INDEX idx_ad_event_queue_state ON ad_event_queue (send_state, id);

-- 佇列溢位時的丟棄計數（FIFO 丟棄時累加，隨診斷回報送出後歸零）。單列表。
CREATE TABLE ad_event_drop_counter (
  id               INTEGER NOT NULL PRIMARY KEY CHECK (id = 1),
  dropped_count    INTEGER NOT NULL DEFAULT 0,
  updated_at_utc   TEXT
);

INSERT INTO ad_event_drop_counter (id, dropped_count) VALUES (1, 0);
