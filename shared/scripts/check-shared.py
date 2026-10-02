#!/usr/bin/env python3
"""shared/ 一致性檢查（docs/19 §9「CI 必跑四件事」之 ③）。

不產生任何檔案，只驗證 shared/ 裡手寫與產生的契約檔彼此自洽：
  1. 所有 JSON 能解析。
  2. cache-schema.sql：migration 區塊連號、能在記憶體 SQLite 依序套用、不含禁止欄位（token／座標／member_id…）。
  3. cache-policy.json：與 App 規劃書 §2.4 的 10 列一一對應、ttl 型別正確。
  4. deeplinks.json：8 列路由、universalLinkPaths 與 webPath 一致、parseCases 通過參考解析器。
  5. ad-viewability-cases.json：以本檔的參考實作逐案跑一遍，預期事件必須完全相符。
  6. error-codes.json：代碼格式與狀態碼合理。
  7. openapi.json：OpenAPI 3.1、不含後台／伺服器內部路徑、所有 $ref 可解析。

用法：python3 shared/scripts/check-shared.py   （任何一項失敗 exit 1）
僅用標準函式庫，不連網。
"""

from __future__ import annotations

import json
import re
import sqlite3
import sys
from pathlib import Path

SHARED = Path(__file__).resolve().parent.parent
errors: list[str] = []


def fail(msg: str) -> None:
    errors.append(msg)


def load(name: str):
    path = SHARED / name
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception as exc:  # noqa: BLE001 - 任何解析失敗都要回報檔名
        fail(f"{name}：無法解析 JSON（{exc}）")
        return None


# ── 2. cache-schema.sql ───────────────────────────────────────────────────────
FORBIDDEN_COLUMNS = {
    "token", "member_token", "qr", "qr_content",  # 會員卡 token 只能在安全儲存區
    "lat", "lng", "latitude", "longitude", "coordinate", "coordinates",  # 座標只存在記憶體
    "member_id", "ip", "ip_address", "advertising_id", "idfa", "gaid",  # 廣告事件不得帶個資
}


def check_cache_schema() -> None:
    sql = (SHARED / "cache-schema.sql").read_text(encoding="utf-8")
    markers = [(m.start(), int(m.group(1))) for m in re.finditer(r"(?m)^-- migration: (\d+) \S+", sql)]
    if not markers:
        fail("cache-schema.sql：找不到任何 `-- migration: <N> <名稱>` 區塊")
        return
    numbers = [n for _, n in markers]
    if numbers != list(range(1, len(numbers) + 1)):
        fail(f"cache-schema.sql：migration 編號必須從 1 連號遞增，目前是 {numbers}")
    conn = sqlite3.connect(":memory:")
    try:
        for i, (start, _) in enumerate(markers):
            end = markers[i + 1][0] if i + 1 < len(markers) else len(sql)
            conn.executescript(sql[start:end])
    except sqlite3.Error as exc:
        fail(f"cache-schema.sql：套用失敗（{exc}）")
        return
    tables = [r[0] for r in conn.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'")]
    for table in tables:
        for col in conn.execute(f'PRAGMA table_info("{table}")'):
            if col[1].lower() in FORBIDDEN_COLUMNS:
                fail(f"cache-schema.sql：表 {table} 出現禁止欄位 {col[1]}（見檔頭『絕對禁止出現的欄位』）")
    required = {"cache_meta", "cache_item", "member_card", "follow_preference", "ad_event_queue", "ad_event_drop_counter"}
    missing = required - set(tables)
    if missing:
        fail(f"cache-schema.sql：缺少表 {sorted(missing)}")
    meta_cols = [c[1] for c in conn.execute("PRAGMA table_info(cache_meta)")]
    for c in ("cache_key", "etag", "fetched_at_utc", "ttl_sec", "server_time_utc"):
        if c not in meta_cols:
            fail(f"cache-schema.sql：cache_meta 缺欄位 {c}（docs/19 §3）")
    queue_cols = {c[1] for c in conn.execute("PRAGMA table_info(ad_event_queue)")}
    for c in ("id", "type", "creative_id", "campaign_id", "slot_code", "presentation_id", "occurred_at_utc", "platform", "lang", "club_id", "send_state"):
        if c not in queue_cols:
            fail(f"cache-schema.sql：ad_event_queue 缺欄位 {c}（docs/19 §6.4）")
    conn.close()


# ── 3. cache-policy.json ──────────────────────────────────────────────────────
PLAN_2_4_KEYS = {
    "member_card", "matches", "news_list", "news_detail", "partner_stores",
    "teams_players", "follow_preferences", "membership_status", "ad_creatives", "program_sessions",
}


def check_cache_policy(policy) -> None:
    if not policy:
        return
    keys = [e["key"] for e in policy["entries"]]
    if len(keys) != len(set(keys)):
        fail("cache-policy.json：key 重複")
    if set(keys) != PLAN_2_4_KEYS:
        fail(f"cache-policy.json：key 必須與規劃書 §2.4 的 10 列一一對應，差異：{sorted(set(keys) ^ PLAN_2_4_KEYS)}")
    strategies = set(policy["strategies"])
    for e in policy["entries"]:
        if e["strategy"] not in strategies:
            fail(f"cache-policy.json：{e['key']} 的 strategy {e['strategy']} 未定義")
        ttl = e["ttlSec"]
        if ttl is not None and (not isinstance(ttl, int) or ttl < 0):
            fail(f"cache-policy.json：{e['key']} 的 ttlSec 必須是 null 或非負整數")
        if e["cacheMetaKey"] and not (isinstance(ttl, int) and ttl > 0):
            fail(f"cache-policy.json：{e['key']} 走 cache_meta，ttlSec 必須是正整數")
    if policy["memberCard"]["staleReminderDays"] != 7:
        fail("cache-policy.json：memberCard.staleReminderDays 必須是 7（規劃書 §2.4 硬規則 2）")
    q = policy["adEventQueue"]
    if q["serverRejectOlderThanHours"] != 24:
        fail("cache-policy.json：adEventQueue.serverRejectOlderThanHours 必須是 24（規劃書 §7.5）")


# ── 4. deeplinks.json ─────────────────────────────────────────────────────────
def template_to_regex(template: str, params: dict) -> re.Pattern[str]:
    def sub(m: re.Match[str]) -> str:
        spec = params.get(m.group(1)) or {}
        if "enum" in spec:
            return "(?P<%s>%s)" % (m.group(1), "|".join(re.escape(v) for v in spec["enum"]))
        return "(?P<%s>[^/]+)" % m.group(1)

    out = ""
    last = 0
    for m in re.finditer(r"\{(\w+)\}", template):
        out += re.escape(template[last:m.start()]) + sub(m)
        last = m.end()
    out += re.escape(template[last:])
    return re.compile("^" + out.rstrip("/") + "/?$")


def parse_link(routes, url: str):
    """參考解析器：回傳 (route_id, params) 或 None。兩端的 deep link 解析必須與此一致（以 parseCases 驗證）。"""
    for r in routes:
        if url.startswith("tcrfc://"):
            m = template_to_regex(r["pattern"], r["params"]).match(url)
        elif r["webPath"]:
            # 官網網址的參數名稱以 webPath 為準（match／store 的深連結叫 id、官網叫 slug；parseCases 目前只涵蓋名稱相同的列）
            url_path = re.match(r"^https://[^/]+(/.*)$", url)
            m = template_to_regex(r["webPath"], r["params"]).match(url_path.group(1)) if url_path else None
        else:
            m = None
        if m:
            return r["id"], m.groupdict()
    return None


def check_deeplinks(dl) -> None:
    if not dl:
        return
    routes = dl["routes"]
    if len(routes) != 8:
        fail(f"deeplinks.json：規劃書 §2.3 對照表是 8 列，目前 {len(routes)}")
    ids = [r["id"] for r in routes]
    if len(ids) != len(set(ids)):
        fail("deeplinks.json：route id 重複")
    for r in routes:
        for link in r["deepLinks"]:
            if not link.startswith(dl["scheme"] + "://"):
                fail(f"deeplinks.json：{r['id']} 的深連結 {link} 不是 {dl['scheme']}:// 開頭")
    # universalLinkPaths 由 webPath 把 {參數} 換成 *（schedule 的結尾 /{team}/ 變成 /*）
    derived = []
    for r in routes:
        if not r["webPath"]:
            continue
        p = re.sub(r"\{\w+\}/?$", "*", r["webPath"])
        p = re.sub(r"\{\w+\}", "*", p)
        if p not in derived:
            derived.append(p)
    if sorted(derived) != sorted(dl["universalLinkPaths"]):
        fail(f"deeplinks.json：universalLinkPaths 與 routes[].webPath 不一致。應為 {sorted(derived)}，實際 {sorted(dl['universalLinkPaths'])}")
    for case in dl["parseCases"]:
        got = parse_link(routes, case["input"])
        want = (case["expect"]["route"], case["expect"]["params"])
        if got != want:
            fail(f"deeplinks.json：parseCase {case['input']} 預期 {want}，參考解析器得到 {got}")


# ── 5. 廣告可見度量測參考實作 ─────────────────────────────────────────────────
def evaluate_impressions(constants: dict, presentations: dict, samples: list) -> list[dict]:
    """shared/ad-viewability.md §3 的參考實作。輸入取樣序列，輸出應發出的曝光事件。"""
    min_ratio = constants["minVisibleRatio"]
    min_ms = constants["minContinuousMs"]
    streak: dict[str, int | None] = {}
    done: set[str] = set()
    events: list[dict] = []
    prev_pid = None
    for t, ratio, app, pid in samples:
        if app != "active":  # 規則 3：全部歸零
            streak = {k: None for k in streak}
            prev_pid = pid
            continue
        if prev_pid is not None and prev_pid != pid:  # 規則 4：切換素材
            streak[prev_pid] = None
        prev_pid = pid
        meta = presentations[pid]
        if meta["isFallback"]:  # 規則 1
            continue
        decoded = meta["imageDecodedAtMs"]
        if decoded is None or t < decoded:  # 規則 2
            streak[pid] = None
            continue
        if pid in done:  # 規則 5
            continue
        if ratio >= min_ratio:  # 規則 6
            if streak.get(pid) is None:
                streak[pid] = t
            if t - streak[pid] >= min_ms:
                events.append({"presentationId": pid, "atMs": t})
                done.add(pid)
        else:
            streak[pid] = None
    return events


def evaluate_clicks(constants: dict, clicks: list) -> list[int]:
    last_sent: dict[str, int] = {}
    sent: list[int] = []
    for i, (t, creative) in enumerate(clicks):
        if creative in last_sent and t - last_sent[creative] < constants["clickDedupMs"]:
            continue
        last_sent[creative] = t
        sent.append(i)
    return sent


def check_viewability(cases) -> None:
    if not cases:
        return
    c = cases["constants"]
    ids = set()
    for case in cases["impressionCases"]:
        if case["id"] in ids:
            fail(f"ad-viewability-cases.json：案例 id 重複 {case['id']}")
        ids.add(case["id"])
        samples = case["samples"]
        if [s[0] for s in samples] != sorted(s[0] for s in samples):
            fail(f"ad-viewability-cases.json：{case['id']} 的取樣時間未遞增")
        got = evaluate_impressions(c, case["presentations"], samples)
        if got != case["expect"]:
            fail(f"ad-viewability-cases.json：{case['id']} 預期 {case['expect']}，參考實作得到 {got}")
    for case in cases["clickCases"]:
        got = evaluate_clicks(c, case["clicks"])
        if got != case["expectSentIndexes"]:
            fail(f"ad-viewability-cases.json：{case['id']} 預期 {case['expectSentIndexes']}，參考實作得到 {got}")


# ── 6. error-codes.json ───────────────────────────────────────────────────────
def check_error_codes(ec) -> None:
    if not ec:
        return
    seen = set()
    for item in ec["codes"]:
        code = item["code"]
        if not re.fullmatch(r"[a-z][a-z0-9]*(_[a-z0-9]+)*", code):
            fail(f"error-codes.json：代碼格式不合 {code}")
        if code in seen:
            fail(f"error-codes.json：代碼重複 {code}")
        seen.add(code)
        for s in item["httpStatus"]:
            if not 400 <= s <= 599:
                fail(f"error-codes.json：{code} 的狀態碼 {s} 不是錯誤狀態")
    if not ec["codes"]:
        fail("error-codes.json：沒有任何代碼（掃描器可能壞了）")


# ── 7. openapi.json ───────────────────────────────────────────────────────────
def check_openapi(doc) -> None:
    if not doc:
        return
    if not str(doc.get("openapi", "")).startswith("3.1"):
        fail(f"openapi.json：必須是 OpenAPI 3.1.x，目前 {doc.get('openapi')}")
    for p in doc["paths"]:
        if p.startswith("/api/v1/admin") or p.startswith("/api/v1/donation-platform") or p == "/api/membership/activate":
            fail(f"openapi.json：不應包含 {p}（後台／慈善／伺服器內部端點不給 App）")
    schemas = doc["components"]["schemas"]
    refs = set(re.findall(r'"\$ref":\s*"#/components/schemas/([^"]+)"', json.dumps(doc)))
    for r in refs:
        if r not in schemas:
            fail(f"openapi.json：$ref 指向不存在的 schema {r}")
    for must in ("/api/v1/app/config", "/api/v1/app/ads/events", "/api/v1/member/cards"):
        if must not in doc["paths"]:
            fail(f"openapi.json：缺少 App 必要端點 {must}")


def main() -> int:
    policy = load("cache-policy.json")
    dl = load("deeplinks.json")
    cases = load("ad-viewability-cases.json")
    ec = load("error-codes.json")
    oa = load("openapi.json")
    check_cache_schema()
    check_cache_policy(policy)
    check_deeplinks(dl)
    check_viewability(cases)
    check_error_codes(ec)
    check_openapi(oa)
    if errors:
        print(f"shared/ 一致性檢查失敗（{len(errors)} 項）：", file=sys.stderr)
        for e in errors:
            print(f"  - {e}", file=sys.stderr)
        return 1
    print("shared/ 一致性檢查通過")
    return 0


if __name__ == "__main__":
    sys.exit(main())
