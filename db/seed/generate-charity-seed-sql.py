#!/usr/bin/env python3
# db/seed/generate-charity-seed-sql.py — 產生慈善捐款平台庫（tcrfc_charity_dev）的種子 T-SQL
#
# 🔴🔴 本檔產生的所有捐款人／店家聯絡人／統編／身分證等個資性質欄位，全部是虛構測試資料 🔴🔴
#   ——不是真人姓名、不是真實 Email、不是真實電話、不是真實統一編號。
#   姓名一律用「測試用．王小明」這種一眼看得出是假的格式；Email 一律 @example.test
#  （RFC 2606 保留給文件與測試用的網域，永遠不會被真正註冊）；電話與統編一律用連續 0
#   這種明顯不可能存在的號碼；「加密」欄位（national_id_encrypted 等）本檔沒有真的加密
#  （加密是應用層職責，尚未實作），一律填入清楚標明「非真實」的占位字串，不假裝是密文。
#
# 🔴 慈善庫是獨立法人邊界（主辦與收款主體是台灣足球策略發展協會，不是台中磐石足球俱樂部）。
#   本檔與 db/seed/generate-club-seed-sql.py 分開維護是刻意的——見 db/seed/apply-charity-seed.sh
#   檔頭「為什麼另開一支腳本」。本檔不得被 apply-seed.sh 呼叫，也不得反過來納入主站的種子。
#
# 為什麼這裡不像 generate-club-seed-sql.py 那樣「讀 JSON 產生 SQL」：
#   主站的種子資料含球員／教練真實姓名（個資），所以邏輯與資料分離、資料只活在不進版控的
#   JSON／產出檔裡。本檔的資料**從一開始就是虛構測試資料**，不是真人個資，直接寫在本檔內
#   反而更容易一眼看出「這些名字為什麼看起來假假的」，不需要额外一層 JSON 中間格式。
#
# 冪等策略：與 generate-club-seed-sql.py 完全相同——每個實體用一個業務自然鍵判斷是否已存在
#（IF NOT EXISTS 才 INSERT），可重複執行不會炸主鍵、不會長出重複資料。GO 隔開每個批次、
# 父列一律用純量子查詢引用，不依賴變數跨批次存活。
#
# 用法：
#   python3 db/seed/generate-charity-seed-sql.py > db/seed/.generated/charity-seed.local.sql
#   （或直接執行 db/seed/apply-charity-seed.sh，會自動呼叫本腳本）

import hashlib
import json
import re
import uuid
from datetime import date, datetime
from decimal import ROUND_FLOOR, Decimal

# 固定命名空間，與 generate-club-seed-sql.py 的 NS 不同——兩庫的種子資料互不相干，
# 用不同命名空間避免任何人誤以為兩邊的 GUID 有對應關係。
NS = uuid.UUID("2b6a7e3c-9f4d-4a1e-8c3b-7d5a1e9f2c40")


def new_id(*parts: str) -> str:
    """由業務鍵決定性地衍生一個 GUID 字面量（UUIDv5），方便重跑時人工比對，不影響冪等邏輯本身。"""
    return str(uuid.uuid5(NS, "|".join(parts)))


def fake_slug(prefix: str, seed: str) -> str:
    """依業務鍵決定性地產生一個『看起來隨機、不可由編號推導』的 slug（docs/16 §4.3 對
    DonationStore.store_slug 的要求：全站唯一、系統產生、不可由編號推導）。仍是決定性
    （同樣的 seed 永遠算出同一個值），純粹方便本機重跑時人工比對——正式環境的 slug 由
    後台在建立當下呼叫真正的隨機數產生器產生，這裡只是種子資料的權宜作法，不代表正式
    的產生演算法。DonationProject.project_slug 沒有「不可由編號推導」的規定，但沿用
    同一支函式維持風格一致。"""
    digest = hashlib.sha256(seed.encode("utf-8")).hexdigest()
    return f"{prefix}-{digest[:10]}"


def esc(value) -> str:
    """T-SQL 字面量：None → NULL；字串一律 N'...'，單引號用兩個單引號跳脫。"""
    if value is None:
        return "NULL"
    if isinstance(value, bool):
        return "1" if value else "0"
    if isinstance(value, Decimal):
        return str(value)
    if isinstance(value, (int, float)):
        return str(value)
    if isinstance(value, (date, datetime)):
        return f"'{value.isoformat()}'"
    s = str(value).replace("'", "''")
    return f"N'{s}'"


out = []


def emit(text: str = ""):
    out.append(text)


def block(sql: str):
    """一個獨立批次：內容 + GO。若內容符合『IF @id IS NULL BEGIN ... END』的防呆插入樣式，
    自動包一層 BEGIN TRANSACTION／COMMIT TRANSACTION（原因同 generate-club-seed-sql.py
    檔頭的說明：避免「父列已插入、子列因後續語句出錯而沒插入」的半殘資料，且冪等判斷
    用的自然鍵與這個區塊真正該保證存在的東西必須是同一組）。"""
    text = sql.strip()
    if "\nBEGIN\n" in text and text.endswith("END"):
        text = re.sub(r"\nBEGIN\n", "\nBEGIN\n  BEGIN TRANSACTION;\n", text, count=1)
        text = re.sub(r"\nEND$", "\n  COMMIT TRANSACTION;\nEND", text, count=1)
    emit(text)
    emit("GO")
    emit()


# ── 父列的純量子查詢（不依賴變數，任何批次都能安全引用） ──────────────────
def admin_sq(username: str) -> str:
    return f"(SELECT id FROM admin_users WHERE username = N'{username}')"


def role_sq(code: str) -> str:
    return f"(SELECT id FROM admin_roles WHERE code = N'{code}')"


def perm_sq(code: str) -> str:
    return f"(SELECT id FROM permissions WHERE code = N'{code}')"


def store_sq(slug: str) -> str:
    return f"(SELECT id FROM donation_stores WHERE store_slug = N'{slug}')"


def project_sq(slug: str) -> str:
    return f"(SELECT id FROM donation_projects WHERE project_slug = N'{slug}')"


def donation_sq(order_no: str) -> str:
    return f"(SELECT id FROM donations WHERE order_no = N'{order_no}')"


def charity_ref_sq(ref_code: str) -> str:
    return f"(SELECT id FROM charity_refs WHERE ref_code = N'{ref_code}')"


def program_ref_sq(ref_code: str) -> str:
    return f"(SELECT id FROM charity_program_refs WHERE ref_code = N'{ref_code}')"


def template_sq(code: str) -> str:
    return f"(SELECT id FROM email_templates WHERE code = N'{code}')"


emit("-- ============================================================================")
emit("-- db/seed/.generated/charity-seed.local.sql — 自動產生，請勿手動編輯")
emit("-- 產生自：db/seed/generate-charity-seed-sql.py")
emit("--")
emit("-- 🔴🔴 本檔全部資料為本機開發用之虛構測試資料 🔴🔴")
emit("-- 捐款人姓名／Email／電話／統編／身分證字號等一律為虛構值，不對應任何真實個人或")
emit("-- 企業；『加密』欄位（*_encrypted）沒有經過真的加密，是清楚標明非真實的占位字串。")
emit("-- 慈善協會的統一編號與法人登記尚未確定（STATUS.md B-7），本檔的 CharityRef／")
emit("-- CharityProgramRef 快照資料同樣是虛構占位，不得沿用到任何正式環境。")
emit("--")
emit("-- 冪等：可重複執行，每個實體用業務自然鍵判斷是否已存在。")
emit("-- 目標資料庫：tcrfc_charity_dev（本機既有 sqlserver 容器內）。")
emit("-- 🔴 不得對到 tcrfc_club_dev，也不得對到同一個 instance 裡其他專案的資料庫。")
emit("-- 🔴 不得與 tcrfc_club_dev 做任何跨庫 JOIN——本庫是完全獨立的法人邊界。")
emit("-- ============================================================================")
emit()
emit("SET XACT_ABORT ON;")
emit("GO")
emit()
emit("-- ⚠️ 必要，不是樣板：donation_invoices.invoice_no 是篩選唯一索引")
emit("-- （UX_donation_invoices_invoice_no，WHERE invoice_no IS NOT NULL，見 db/charity-schema.sql")
emit("-- 第二段），對有篩選索引／計算欄位索引的資料表 INSERT 時，session 層級的 QUOTED_IDENTIFIER")
emit("-- 必須是 ON，否則會出現 Msg 1934。sqlcmd 預設不是 ON，SET 選項是連線層級、跨 GO 批次仍然有效。")
emit("SET ANSI_NULLS ON;")
emit("SET QUOTED_IDENTIFIER ON;")
emit("GO")
emit()

# ============================================================================
# 0. locales：zh-Hant（預設）＋ en，供所有 *_i18n 側表的 FK 參照
#    ⚠️ 本庫的 locales 表欄位是 name_zh／name_en（並排雙欄），與主站 locales 表的單一
#    name 欄不同構——db/charity-schema.sql 與 db/club-schema.sql 各自獨立設計，本檔照
#    charity-schema.sql 實際欄位寫，不套用主站那份的欄位假設。
# ============================================================================
emit("-- ── 0. locales：zh-Hant（預設）＋ en ──────────────────────────────────")
block(f"""
IF NOT EXISTS (SELECT 1 FROM locales WHERE code = N'zh-Hant')
  INSERT INTO locales (code, name_zh, name_en, is_default, sort_order) VALUES (N'zh-Hant', N'繁體中文', N'Traditional Chinese', 1, 0);
IF NOT EXISTS (SELECT 1 FROM locales WHERE code = N'en')
  INSERT INTO locales (code, name_zh, name_en, is_default, sort_order) VALUES (N'en', N'英文', N'English', 0, 1);
""")

# ============================================================================
# 1. admin_roles：沿用主站規劃書 §6 的九個角色（docs/16 §2.3／§5 明文「不新增第十個」——
#    本庫沒有「合作球隊管理」這個第十種角色，因為沒有多俱樂部維度）。
#    code 是本次的工程判斷（規劃書與 docs/16 都只給中文角色名稱，沒有給程式碼用的代號），
#    命名沿用「看得懂在講什麼」原則，不是規格明文規定的字面。
# ============================================================================
ROLES = [
    ("system_admin", "系統管理員", "System Administrator", True),
    ("content_editor", "內容編輯", "Content Editor", True),
    ("team_competition", "競技／球隊管理", "Team & Competition Manager", True),
    ("academy_program", "學院／課程管理", "Academy & Program Manager", True),
    ("business_sponsorship", "商務／贊助", "Business & Sponsorship", True),
    ("pr_media", "公關／媒體", "PR & Media", True),
    ("customer_service_admin", "客服／行政", "Customer Service & Admin", True),
    ("translator", "翻譯人員", "Translator", True),
    ("viewer", "檢視者", "Viewer", True),
]

emit("-- ── 1. admin_roles：沿用主站九個角色（docs/16 §5：本庫不新增第十個「合作球隊管理」） ──")
for code, name_zh, name_en, is_system in ROLES:
    role_id = new_id("admin_role", code)
    block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM admin_roles WHERE code = {esc(code)};
IF @id IS NULL
BEGIN
  SET @id = {esc(role_id)};
  INSERT INTO admin_roles (id, code, name_zh, name_en, is_system)
  VALUES (@id, {esc(code)}, {esc(name_zh)}, {esc(name_en)}, {esc(is_system)});
END
""")

# ============================================================================
# 2. permissions：N1–N7 七個子模組各給 2–3 個權限碼。
#    ⚠️ docs/16 §10 明文「Permission.domain 的值域待權限碼盤點後再定」，DDL 也沒有對
#    domain 加 CHECK 約束——以下 domain 值是本次盤點的工程判斷，不是規格明文，若日後
#    正式盤點結果不同，改 docs/16 再回頭改本檔，不要直接改這裡的字面值。
#    is_restricted：docs/16 §5「三類操作需獨立授權且每次寫 AuditLog」——退款、分潤
#    百分比設定、含個資的明細匯出。sysadmin_only：比照主站權限矩陣「退款……僅限系統
#    管理員」「金流憑證僅系統管理員」的慣例（output/TCRFC_前後台功能規劃書.md 行 1616）。
# ============================================================================
PERMISSIONS = [
    # code, module(N1..N7), domain, action, name_zh, name_en, is_restricted, sysadmin_only
    ("n1.donation_store.view", "N1", "store", "view", "檢視捐款店家", "View Donation Stores", False, False),
    ("n1.donation_store.manage", "N1", "store", "update", "編輯捐款店家", "Manage Donation Stores", False, False),
    ("n1.donation_store.share_pct", "N1", "store", "execute", "設定店家分潤比例", "Set Store Share %", True, False),
    ("n1.donation_store.export", "N1", "store", "export", "匯出店家與 QR 清單", "Export Stores & QR", False, False),
    ("n2.donation_project.view", "N2", "project", "view", "檢視捐款項目", "View Donation Projects", False, False),
    ("n2.donation_project.manage", "N2", "project", "update", "編輯捐款項目", "Manage Donation Projects", False, False),
    ("n2.donation_project.publish", "N2", "project", "publish", "上下架捐款項目", "Publish Donation Projects", False, False),
    ("n2.donation_project.share_pct", "N2", "project", "execute", "設定項目分潤比例", "Set Project Share %", True, False),
    ("n3.donation.view", "N3", "donation", "view", "檢視捐款紀錄（遮罩）", "View Donations (Masked)", False, False),
    ("n3.donation.reveal", "N3", "donation", "reveal", "檢視捐款人完整個資", "Reveal Donor PII", True, False),
    ("n3.donation.refund", "N3", "donation", "execute", "執行人工退款", "Execute Manual Refund", True, True),
    ("n3.donation.export", "N3", "donation", "export", "匯出含個資之捐款明細", "Export Donation PII", True, False),
    ("n4.settlement.view", "N4", "settlement", "view", "檢視結算單", "View Settlements", False, False),
    ("n4.settlement.execute", "N4", "settlement", "execute", "執行結算與登記匯款", "Execute Settlement", False, False),
    ("n4.settlement.export", "N4", "settlement", "export", "匯出對帳單", "Export Settlement Reports", False, False),
    ("n5.donation_invoice.view", "N5", "invoice", "view", "檢視發票與收據", "View Invoices", False, False),
    ("n5.donation_invoice.issue", "N5", "invoice", "execute", "開立或補開發票", "Issue Invoice", False, False),
    ("n5.donation_invoice.void", "N5", "invoice", "execute", "作廢或折讓發票", "Void / Allowance Invoice", False, False),
    ("n6.report.view", "N6", "report", "view", "檢視捐款報表", "View Donation Reports", False, False),
    ("n6.report.export", "N6", "report", "export", "匯出捐款報表", "Export Donation Reports", False, False),
    ("n7.setting.view", "N7", "setting", "view", "檢視站台設定", "View Site Settings", False, False),
    ("n7.setting.manage", "N7", "setting", "update", "編輯站台設定與文案", "Manage Site Settings", False, False),
    ("n7.payment_channel.manage", "N7", "setting", "update", "管理金流與發票憑證", "Manage Payment Channels", True, True),
]

emit("-- ── 2. permissions：N1–N7 共 23 筆（domain 值域為本次工程判斷，見上方註解） ──────")
for code, submodule, domain, action, name_zh, name_en, is_restricted, sysadmin_only in PERMISSIONS:
    perm_id = new_id("permission", code)
    block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM permissions WHERE code = {esc(code)};
IF @id IS NULL
BEGIN
  SET @id = {esc(perm_id)};
  INSERT INTO permissions (id, code, module_code, submodule_code, domain, action, name_zh, name_en, is_restricted, sysadmin_only)
  VALUES (@id, {esc(code)}, N'N', {esc(submodule)}, {esc(domain)}, {esc(action)}, {esc(name_zh)}, {esc(name_en)}, {esc(is_restricted)}, {esc(sysadmin_only)});
END
""")

# ============================================================================
# 3. role_permissions：系統管理員給全部（雖然 is_super_admin 已經略過權限檢查，這裡仍
#    插入完整清單，讓後台「角色與權限」畫面的 mockup 有東西可顯示，不是空白表格）。
#    其餘角色依角色職能給對應子集合。⚠️ 內容編輯／競技球隊管理／學院課程管理／公關媒體／
#    翻譯人員這五個角色在慈善後台**沒有對應職能**（docs/16 §5 沿用九個角色是為了角色代碼
#    與治理一致，不代表每個角色在本庫都有實際工作），本次刻意不掛任何權限給它們，
#    不臆測分配——這是刻意留白，見本次回報。
# ============================================================================
ALL_PERM_CODES = [p[0] for p in PERMISSIONS]

ROLE_PERMISSION_MAP = {
    "system_admin": ALL_PERM_CODES,
    "customer_service_admin": [
        "n3.donation.view", "n3.donation.reveal", "n3.donation.export",
        "n5.donation_invoice.view", "n5.donation_invoice.issue", "n5.donation_invoice.void",
        "n6.report.view",
    ],
    "business_sponsorship": [
        "n1.donation_store.view", "n1.donation_store.manage",
        "n2.donation_project.view", "n2.donation_project.manage", "n2.donation_project.publish",
        "n6.report.view",
    ],
    "viewer": [
        "n1.donation_store.view", "n2.donation_project.view", "n3.donation.view",
        "n4.settlement.view", "n5.donation_invoice.view", "n6.report.view", "n7.setting.view",
    ],
}
# 檢視者的 n3.donation.view 額外標記 scope_type='masked'，比照主站 docs/12b §7.4 用 scope_type
# 表達矩陣裡不是布林的格子（見 db/charity-schema.sql role_permissions 表定義註解）。
VIEWER_MASKED_PERMS = {"n3.donation.view"}

emit("-- ── 3. role_permissions：系統管理員全給；五個與慈善無對應職能的角色刻意不掛任何權限 ──")
for role_code, perm_codes in ROLE_PERMISSION_MAP.items():
    for perm_code in perm_codes:
        scope_type = "masked" if (role_code == "viewer" and perm_code in VIEWER_MASKED_PERMS) else None
        block(f"""
IF NOT EXISTS (SELECT 1 FROM role_permissions WHERE admin_role_id = {role_sq(role_code)} AND permission_id = {perm_sq(perm_code)})
  INSERT INTO role_permissions (admin_role_id, permission_id, scope_type)
  VALUES ({role_sq(role_code)}, {perm_sq(perm_code)}, {esc(scope_type)});
""")

# ============================================================================
# 4. admin_users：一組種子超管 ＋ 三個對應不同角色的測試帳號，讓後台「帳號與角色」
#    畫面的 mockup 有多筆可看。密碼雜湊全部是明顯的測試占位字串，不是真的雜湊——
#    這幾個帳號目前無法用來真的登入（後端尚未實作雜湊驗證），must_change_password 全設
#    為 1，提醒未來接上真的認證機制前不得沿用這批雜湊值。username 是登入識別，看起來
#    像 Email 但不是（比照主站 docs/12 慣例：種子超管 sa@system.local）。
# ============================================================================
ADMIN_USERS = [
    ("sa@charity.local", None, "系統管理員（測試帳號）", "system_admin", True),
    ("cs.admin@charity.local", None, "客服／行政（測試帳號）", "customer_service_admin", False),
    ("biz.admin@charity.local", None, "商務／贊助（測試帳號）", "business_sponsorship", False),
    ("viewer@charity.local", None, "唯讀檢視（測試帳號）", "viewer", False),
]

emit("-- ── 4. admin_users：種子超管 ＋ 三個角色測試帳號 ─────────────────────────")
emit("-- ⚠️ password_hash 全部是明顯的測試占位字串（DEV-SEED- 開頭），不是真的雜湊值，")
emit("-- 目前無法用來登入。正式接上認證機制後，這批帳號的密碼必須重設，不得沿用。")
for username, email, display_name, role_code, is_super in ADMIN_USERS:
    user_id = new_id("admin_user", username)
    fake_hash = f"DEV-SEED-NOT-A-REAL-HASH::{username}"
    block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM admin_users WHERE username = {esc(username)};
IF @id IS NULL
BEGIN
  SET @id = {esc(user_id)};
  INSERT INTO admin_users (id, username, email, display_name, password_hash, must_change_password, is_super_admin, status)
  VALUES (@id, {esc(username)}, {esc(email)}, {esc(display_name)}, {esc(fake_hash)}, 1, {esc(is_super)}, N'active');
END
""")
    block(f"""
IF NOT EXISTS (SELECT 1 FROM admin_user_roles WHERE admin_user_id = {admin_sq(username)} AND admin_role_id = {role_sq(role_code)})
  INSERT INTO admin_user_roles (admin_user_id, admin_role_id) VALUES ({admin_sq(username)}, {role_sq(role_code)});
""")

# ============================================================================
# 5. charity_refs／charity_program_refs：主站 Charity／CharityProgram 的唯讀複本。
#    🔴 主站目前也還沒有真實的公益團體種子資料（mockup 階段），且協會統編未定
#    （STATUS.md B-7），本檔一律用清楚標明「僅供本機開發顯示」的虛構名稱，不得沿用到
#    任何正式環境。ref_code／source 格式是本次的工程判斷，規劃書未定義匯入格式。
# ============================================================================
CHARITY_REFS = [
    ("TEST-CHARITY-A", "測試用．公益機構Ａ（僅供本機開發顯示）"),
    ("TEST-CHARITY-B", "測試用．公益機構Ｂ（僅供本機開發顯示）"),
]

emit("-- ── 5a. charity_refs：主站公益團體主檔的虛構唯讀複本（2 筆） ─────────────")
for ref_code, name in CHARITY_REFS:
    ref_id = new_id("charity_ref", ref_code)
    block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM charity_refs WHERE ref_code = {esc(ref_code)};
IF @id IS NULL
BEGIN
  SET @id = {esc(ref_id)};
  INSERT INTO charity_refs (id, ref_code, name, source)
  VALUES (@id, {esc(ref_code)}, {esc(name)}, N'manual_test_seed');
END
""")

CHARITY_PROGRAM_REFS = [
    ("TEST-PROGRAM-A1", "TEST-CHARITY-A", "測試用．公益計畫Ａ－１（僅供本機開發顯示）"),
    ("TEST-PROGRAM-A2", "TEST-CHARITY-A", "測試用．公益計畫Ａ－２（僅供本機開發顯示）"),
    ("TEST-PROGRAM-B1", "TEST-CHARITY-B", "測試用．公益計畫Ｂ－１（僅供本機開發顯示）"),
]

emit("-- ── 5b. charity_program_refs：主站慈善計畫主檔的虛構唯讀複本（3 筆） ──────")
for ref_code, charity_ref_code, name in CHARITY_PROGRAM_REFS:
    program_id = new_id("charity_program_ref", ref_code)
    block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM charity_program_refs WHERE ref_code = {esc(ref_code)};
IF @id IS NULL
BEGIN
  SET @id = {esc(program_id)};
  INSERT INTO charity_program_refs (id, ref_code, charity_ref_id, name)
  VALUES (@id, {esc(ref_code)}, {charity_ref_sq(charity_ref_code)}, {esc(name)});
END
""")

# ============================================================================
# 6. donation_stores：3 家，涵蓋不同 category、有／無 Logo、active／inactive。
#    store_slug 用 fake_slug()：不可由業務鍵字面推導出（要求見 docs/16 §4.3）。
# ============================================================================
STORES = [
    dict(
        key="store-a", name_zh="測試用．早安豆漿店", name_en="Test Breakfast Diner",
        category="餐飲", address="臺中市西區測試路 1 號（虛構地址）",
        contact_name="測試用．店長甲", contact_phone="04-00000001",
        share_pct=Decimal("5.00"), has_logo=True, status="active",
        start_on=date(2026, 6, 1), end_on=None,
    ),
    dict(
        key="store-b", name_zh="測試用．巷口咖啡", name_en="Test Corner Cafe",
        category="飲料", address="臺中市北區測試街 2 號（虛構地址）",
        contact_name="測試用．店長乙", contact_phone="04-00000002",
        share_pct=Decimal("8.00"), has_logo=False, status="active",
        start_on=date(2026, 6, 15), end_on=None,
    ),
    dict(
        key="store-c", name_zh="測試用．已停止合作服飾行", name_en="Test Ended Apparel Shop",
        category="零售", address="臺中市南區測試巷 3 號（虛構地址）",
        contact_name="測試用．店長丙", contact_phone="04-00000003",
        share_pct=Decimal("3.00"), has_logo=False, status="inactive",
        start_on=date(2026, 1, 1), end_on=date(2026, 7, 31),
    ),
]

emit("-- ── 6. donation_stores：3 家，涵蓋不同類別／有無 Logo／合作中或已停止 ───────")
for s in STORES:
    slug = fake_slug("store", s["key"])
    store_id = new_id("donation_store", s["key"])
    logo_key = f"dev-seed/donation-stores/{s['key']}.webp" if s["has_logo"] else None
    logo_w = 640 if s["has_logo"] else None
    logo_h = 640 if s["has_logo"] else None
    block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM donation_stores WHERE store_slug = {esc(slug)};
IF @id IS NULL
BEGIN
  SET @id = {esc(store_id)};
  INSERT INTO donation_stores (id, store_slug, category, address, contact_name, contact_phone, store_share_pct, logo_key, logo_width, logo_height, start_on, end_on, status)
  VALUES (@id, {esc(slug)}, {esc(s["category"])}, {esc(s["address"])}, {esc(s["contact_name"])}, {esc(s["contact_phone"])}, {esc(s["share_pct"])}, {esc(logo_key)}, {esc(logo_w)}, {esc(logo_h)}, {esc(s["start_on"])}, {esc(s["end_on"])}, {esc(s["status"])});
  INSERT INTO donation_stores_i18n (donation_store_id, locale, name, logo_alt) VALUES (@id, N'zh-Hant', {esc(s["name_zh"])}, {esc("測試占位 Logo" if s["has_logo"] else None)});
  INSERT INTO donation_stores_i18n (donation_store_id, locale, name, logo_alt) VALUES (@id, N'en', {esc(s["name_en"])}, {esc("Dev seed placeholder logo" if s["has_logo"] else None)});
END
""")

STORE_SLUGS = {s["key"]: fake_slug("store", s["key"]) for s in STORES}

# ============================================================================
# 7. donation_projects：3 個，涵蓋已上架 × 2（不同 invoice_mode）＋ 已下架 × 1。
#    ⚠️ 已知綱要缺口（回報用）：donation_projects 沒有 start_on／end_on 之類的期間欄位，
#    schema 的 status 也只有 draft／published 兩值（CK_donation_projects_status），
#    因此**無法區分「已下架」與「已結束」**——兩者在目前的資料模型裡是同一個 status
#    值（draft）。本檔用 draft 表示「已下架（含已結束的情形）」，不新增 status 值也不
#    新增欄位（CLAUDE.md 全域規定 2：不得引入規劃書沒有的新規格），這個限制在本次回報
#    中列為缺口，供人工確認是否要回頭補 docs/16。
# ============================================================================
PROJECTS = [
    dict(
        key="project-a", name_zh="測試用．兒童足球獎學金計畫", name_en="Test Youth Football Scholarship",
        one_liner_zh="讓偏鄉孩子也能踢上球（測試文案）", one_liner_en="Helping children in rural areas play football (dev seed copy)",
        min_amount=100, max_amount=100000, share_pct=Decimal("10.00"), invoice_mode="b2c_invoice",
        charity_ref="TEST-CHARITY-A", program_ref="TEST-PROGRAM-A1",
        status="published", sort_order=0,
        fund_usage_zh="款項用於球具採購與交通補助（測試占位文字）",
        fund_usage_en="Funds are used for equipment and transport subsidies (dev seed placeholder).",
    ),
    dict(
        key="project-b", name_zh="測試用．偏鄉球場整建計畫", name_en="Test Rural Pitch Renovation",
        one_liner_zh="修一座能安全踢球的場地（測試文案）", one_liner_en="Rebuilding a safe pitch (dev seed copy)",
        min_amount=200, max_amount=200000, share_pct=Decimal("12.00"), invoice_mode="donation_receipt",
        charity_ref="TEST-CHARITY-B", program_ref="TEST-PROGRAM-B1",
        status="published", sort_order=1,
        fund_usage_zh="款項用於場地整地與圍網修繕（測試占位文字）",
        fund_usage_en="Funds are used for pitch grading and fence repair (dev seed placeholder).",
    ),
    dict(
        key="project-c", name_zh="測試用．已下架示範項目", name_en="Test Unpublished Sample Project",
        one_liner_zh="示範已下架狀態（測試文案）", one_liner_en="Demonstrates an unpublished project (dev seed copy)",
        min_amount=100, max_amount=50000, share_pct=Decimal("6.00"), invoice_mode="b2c_invoice",
        charity_ref="TEST-CHARITY-A", program_ref="TEST-PROGRAM-A2",
        status="draft", sort_order=2,
        fund_usage_zh="（已下架，款項用途說明僅供介面示範）",
        fund_usage_en="(Unpublished; description shown for UI demo only.)",
    ),
]

emit("-- ── 7. donation_projects：已上架 2（不同 invoice_mode）＋ 已下架 1 ──────────")
emit("-- ⚠️ 已知綱要缺口：schema 沒有『已結束』狀態值、也沒有起訖日欄位，見上方註解，已列入回報。")
for p in PROJECTS:
    slug = fake_slug("project", p["key"])
    project_id = new_id("donation_project", p["key"])
    charity_ref = next(c for c in CHARITY_REFS if c[0] == p["charity_ref"])
    program_ref = next(c for c in CHARITY_PROGRAM_REFS if c[0] == p["program_ref"])
    description_zh = json.dumps(
        {"blocks": [{"type": "paragraph", "text": f"{p['name_zh']}的詳細說明（開發測試用占位內文，非正式文案）。"}]},
        ensure_ascii=False,
    )
    description_en = json.dumps(
        {"blocks": [{"type": "paragraph", "text": f"Placeholder body copy for {p['name_en']} (dev seed only)."}]},
        ensure_ascii=False,
    )
    block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM donation_projects WHERE project_slug = {esc(slug)};
IF @id IS NULL
BEGIN
  SET @id = {esc(project_id)};
  INSERT INTO donation_projects (id, project_slug, min_amount, max_amount, project_share_pct, invoice_mode, charity_ref_code, charity_name_snapshot, charity_program_ref_code, charity_program_name_snapshot, sort_order, status)
  VALUES (@id, {esc(slug)}, {esc(p["min_amount"])}, {esc(p["max_amount"])}, {esc(p["share_pct"])}, {esc(p["invoice_mode"])}, {esc(charity_ref[0])}, {esc(charity_ref[1])}, {esc(program_ref[0])}, {esc(program_ref[2])}, {esc(p["sort_order"])}, {esc(p["status"])});
  INSERT INTO donation_projects_i18n (donation_project_id, locale, name, one_liner, description, fund_usage) VALUES (@id, N'zh-Hant', {esc(p["name_zh"])}, {esc(p["one_liner_zh"])}, {esc(description_zh)}, {esc(p["fund_usage_zh"])});
  INSERT INTO donation_projects_i18n (donation_project_id, locale, name, one_liner, description, fund_usage) VALUES (@id, N'en', {esc(p["name_en"])}, {esc(p["one_liner_en"])}, {esc(description_en)}, {esc(p["fund_usage_en"])});
END
""")

PROJECT_SLUGS = {p["key"]: fake_slug("project", p["key"]) for p in PROJECTS}

# ── donation_amount_options：每個項目 3–4 個金額選項卡 ──────────────────────
AMOUNT_OPTIONS = {
    "project-a": [300, 500, 1000, 3000],
    "project-b": [500, 1000, 2000, 5000],
    "project-c": [200, 500],
}

emit("-- ── 7b. donation_amount_options：每個項目 3–4 檔金額選項卡 ──────────────")
for project_key, amounts in AMOUNT_OPTIONS.items():
    slug = PROJECT_SLUGS[project_key]
    for i, amount in enumerate(amounts):
        block(f"""
IF NOT EXISTS (SELECT 1 FROM donation_amount_options WHERE donation_project_id = {project_sq(slug)} AND amount = {esc(amount)})
  INSERT INTO donation_amount_options (donation_project_id, amount, sort_order)
  VALUES ({project_sq(slug)}, {esc(amount)}, {i});
""")

# ============================================================================
# 8. donations：10 筆，涵蓋 CK_donations_status 全部 6 個值：
#    created／pending／paid（×5）／failed／expired／refunded（×1，原為 paid）。
#    分潤三欄依 docs/16 §4.1「無條件捨去至整數元」在 Python 端算好，確保
#    CK_donations_amount_split（三者相加＝amount）與 CK_donations_share_pct_snapshot
#    （兩個百分比相加 ≤100）在任何狀態下都成立——這兩條 CHECK 約束沒有排除未付款的列，
#    所以即使是 created／pending／failed／expired 的捐款單，也要在建單當下就算好分潤
#    快照（真正決定「要不要拿去結算」的是 Settlement 只計 status='paid' 那條規則，
#    不是這裡的欄位有沒有值）。
#    donor_name／donor_email 全部虛構，Email 一律 @example.test（RFC 2606 保留測試網域）。
# ============================================================================


def split_amount(amount: int, store_pct: Decimal, project_pct: Decimal):
    """依 store_pct／project_pct（decimal(5,2)，單位％）與 amount（int，元）算出三段
    分潤，無條件捨去至整數元，尾差全部併入協會留存（association_amount），滿足
    CK_donations_amount_split。"""
    store_amt = int((Decimal(amount) * store_pct / Decimal(100)).to_integral_value(rounding=ROUND_FLOOR))
    project_amt = int((Decimal(amount) * project_pct / Decimal(100)).to_integral_value(rounding=ROUND_FLOOR))
    association_amt = amount - store_amt - project_amt
    return store_amt, project_amt, association_amt


ZERO_PCT = Decimal("0.00")

DONATIONS = [
    dict(
        key="D01", order_no="DEVTEST-DN-0001", store="store-a", project="project-a", amount=500,
        status="paid", donor_name="測試用．王小明", donor_email="donor01@example.test", is_anonymous=False,
        created_at=datetime(2026, 8, 3, 10, 0, 0), paid_at=datetime(2026, 8, 3, 10, 2, 30),
    ),
    dict(
        key="D02", order_no="DEVTEST-DN-0002", store=None, project="project-a", amount=1000,
        status="paid", donor_name="測試用．匿名捐款人", donor_email="donor02@example.test", is_anonymous=True,
        created_at=datetime(2026, 8, 5, 14, 0, 0), paid_at=datetime(2026, 8, 5, 14, 3, 10),
    ),
    dict(
        key="D03", order_no="DEVTEST-DN-0003", store="store-b", project="project-b", amount=2000,
        status="pending", donor_name="測試用．陳小華", donor_email="donor03@example.test", is_anonymous=False,
        created_at=datetime(2026, 9, 20, 9, 0, 0), paid_at=None,
    ),
    dict(
        key="D04", order_no="DEVTEST-DN-0004", store="store-a", project="project-b", amount=300,
        status="created", donor_name="測試用．林小美", donor_email="donor04@example.test", is_anonymous=False,
        created_at=datetime(2026, 9, 21, 16, 30, 0), paid_at=None,
    ),
    dict(
        key="D05", order_no="DEVTEST-DN-0005", store="store-b", project="project-a", amount=1500,
        status="failed", donor_name="測試用．張小強", donor_email="donor05@example.test", is_anonymous=False,
        created_at=datetime(2026, 8, 10, 11, 0, 0), paid_at=None,
    ),
    dict(
        key="D06", order_no="DEVTEST-DN-0006", store=None, project="project-b", amount=800,
        status="expired", donor_name="測試用．李小芳", donor_email="donor06@example.test", is_anonymous=False,
        created_at=datetime(2026, 8, 12, 8, 0, 0), paid_at=None,
    ),
    dict(
        key="D07", order_no="DEVTEST-DN-0007", store="store-a", project="project-a", amount=5000,
        status="refunded", donor_name="測試用．黃小龍", donor_email="donor07@example.test", is_anonymous=False,
        created_at=datetime(2026, 8, 15, 13, 0, 0), paid_at=datetime(2026, 8, 15, 13, 4, 0),
        refund_reason="捐款人誤捐重複扣款，經核可全額退款（測試情境）", refunded_by="cs.admin@charity.local",
    ),
    dict(
        key="D08", order_no="DEVTEST-DN-0008", store="store-b", project="project-b", amount=1000,
        status="paid", donor_name="測試用．吳小婷", donor_email="donor08@example.test", is_anonymous=False,
        created_at=datetime(2026, 8, 18, 15, 0, 0), paid_at=datetime(2026, 8, 18, 15, 2, 0),
    ),
    dict(
        key="D09", order_no="DEVTEST-DN-0009", store="store-a", project="project-b", amount=600,
        status="paid", donor_name="測試用．周小傑", donor_email="donor09@example.test", is_anonymous=False,
        created_at=datetime(2026, 8, 20, 9, 30, 0), paid_at=datetime(2026, 8, 20, 9, 33, 0),
    ),
    dict(
        key="D10", order_no="DEVTEST-DN-0010", store=None, project="project-a", amount=2500,
        status="paid", donor_name="測試用．鄭小玲", donor_email="donor10@example.test", is_anonymous=True,
        created_at=datetime(2026, 8, 25, 17, 0, 0), paid_at=datetime(2026, 8, 25, 17, 5, 0),
    ),
]

# 分潤快照百分比：店家用該店家 store_share_pct 當下的值；項目用 project-a／project-b
# **建單當下**的值（project-a 目前是 10.00%，但 D01/D02/D07/D10 建立時用的是舊值
# 8.00%——刻意示範 docs/16 §4.2「改設定不追溯」：§9 的 audit_logs 種了一筆
# 「project_share_pct 由 8.00% 調整為 10.00%」的稽核紀錄，這批舊捐款單的快照不因此改變）。
STORE_PCT = {"store-a": Decimal("5.00"), "store-b": Decimal("8.00"), None: ZERO_PCT}
PROJECT_PCT_AT_DONATION_TIME = {"project-a": Decimal("8.00"), "project-b": Decimal("12.00")}

emit("-- ── 8. donations：10 筆，涵蓋 created／pending／paid×5／failed／expired／refunded ──")
emit("-- project-a 的 project_share_pct 目前是 10.00%，但下面的捐款單快照用 8.00%——")
emit("-- 刻意示範『改設定不追溯』（docs/16 §4.2），呼應 §12 稽核紀錄的那筆分潤調整。")
for d in DONATIONS:
    store_pct = STORE_PCT[d["store"]]
    project_pct = PROJECT_PCT_AT_DONATION_TIME[d["project"]]
    store_amt, project_amt, assoc_amt = split_amount(d["amount"], store_pct, project_pct)
    store_id_sql = store_sq(STORE_SLUGS[d["store"]]) if d["store"] else "NULL"
    project_slug = PROJECT_SLUGS[d["project"]]
    invoice_mode = next(p["invoice_mode"] for p in PROJECTS if p["key"] == d["project"])
    refund_reason = d.get("refund_reason")
    refunded_by_sql = admin_sq(d["refunded_by"]) if d.get("refunded_by") else "NULL"
    donation_id = new_id("donation", d["key"])
    block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM donations WHERE order_no = {esc(d["order_no"])};
IF @id IS NULL
BEGIN
  SET @id = {esc(donation_id)};
  INSERT INTO donations (id, order_no, donation_project_id, donation_store_id, amount, status, created_at, paid_at, donor_name, donor_email, is_anonymous, store_share_pct_snapshot, project_share_pct_snapshot, store_amount, project_amount, association_amount, invoice_mode, refund_reason, refunded_by)
  VALUES (@id, {esc(d["order_no"])}, {project_sq(project_slug)}, {store_id_sql}, {esc(d["amount"])}, {esc(d["status"])}, {esc(d["created_at"])}, {esc(d["paid_at"])}, {esc(d["donor_name"])}, {esc(d["donor_email"])}, {esc(d["is_anonymous"])}, {esc(store_pct)}, {esc(project_pct)}, {esc(store_amt)}, {esc(project_amt)}, {esc(assoc_amt)}, {esc(invoice_mode)}, {esc(refund_reason)}, {refunded_by_sql});
END
""")

DONATION_SPLITS = {}
for d in DONATIONS:
    store_pct = STORE_PCT[d["store"]]
    project_pct = PROJECT_PCT_AT_DONATION_TIME[d["project"]]
    DONATION_SPLITS[d["key"]] = split_amount(d["amount"], store_pct, project_pct)

# ============================================================================
# 9. donation_payments：與 donations 對應（D04「created」尚未起動金流，刻意不建
#    payment 列，示範「捐款單已建立但金流尚未 Request」這個中間狀態）。
#    status 用 DDL 定案的四值：requested／confirmed／failed／cancelled。
# ============================================================================
PAYMENTS = {
    "D01": dict(status="confirmed", requested_at=datetime(2026, 8, 3, 10, 0, 5), confirmed_at=datetime(2026, 8, 3, 10, 2, 30)),
    "D02": dict(status="confirmed", requested_at=datetime(2026, 8, 5, 14, 0, 5), confirmed_at=datetime(2026, 8, 5, 14, 3, 10)),
    "D03": dict(status="requested", requested_at=datetime(2026, 9, 20, 9, 0, 5), confirmed_at=None),
    # D04：created，尚未呼叫 LINE Pay Request，刻意不建 payment 列。
    "D05": dict(status="failed", requested_at=datetime(2026, 8, 10, 11, 0, 5), confirmed_at=None),
    "D06": dict(status="cancelled", requested_at=datetime(2026, 8, 12, 8, 0, 5), confirmed_at=None),
    "D07": dict(status="confirmed", requested_at=datetime(2026, 8, 15, 13, 0, 5), confirmed_at=datetime(2026, 8, 15, 13, 4, 0)),
    "D08": dict(status="confirmed", requested_at=datetime(2026, 8, 18, 15, 0, 5), confirmed_at=datetime(2026, 8, 18, 15, 2, 0)),
    "D09": dict(status="confirmed", requested_at=datetime(2026, 8, 20, 9, 30, 5), confirmed_at=datetime(2026, 8, 20, 9, 33, 0)),
    "D10": dict(status="confirmed", requested_at=datetime(2026, 8, 25, 17, 0, 5), confirmed_at=datetime(2026, 8, 25, 17, 5, 0)),
}

emit("-- ── 9. donation_payments：D04 刻意不建（金流尚未起動），其餘涵蓋四種狀態 ──────")
for d in DONATIONS:
    if d["key"] not in PAYMENTS:
        continue
    pay = PAYMENTS[d["key"]]
    transaction_id = f"LP-TEST-TXN-{d['key']}"
    raw_response = json.dumps(
        {"note": "dev seed fixture, not a real LINE Pay response", "returnCode": "0000" if pay["status"] == "confirmed" else "9999"},
        ensure_ascii=False,
    )
    block(f"""
IF NOT EXISTS (SELECT 1 FROM donation_payments WHERE donation_id = {donation_sq(d["order_no"])})
  INSERT INTO donation_payments (donation_id, transaction_id, requested_at, confirmed_at, amount, status, raw_response)
  VALUES ({donation_sq(d["order_no"])}, {esc(transaction_id)}, {esc(pay["requested_at"])}, {esc(pay["confirmed_at"])}, {esc(d["amount"])}, {esc(pay["status"])}, {esc(raw_response)});
""")

# ============================================================================
# 10. donation_invoices：涵蓋已開立／待開立／已作廢（voided）／折讓（allowance）／
#     開立失敗，且涵蓋 DonationInvoice.carrier_type 規劃書給的三個值域（手機條碼載具／
#     捐贈發票／統一編號，見 docs/16a §6）。national_id_encrypted／carrier_id_encrypted
#     全部是清楚標明「非真實」的占位字串，不是真的加密（加密尚未實作，見檔頭聲明）。
# ============================================================================
FAKE_ENC = lambda label: f"ENC-PLACEHOLDER-NOT-REAL::{label}"  # noqa: E731

INVOICES = {
    "D01": dict(
        invoice_type="b2c_invoice", invoice_no="DEVTEST-INV-0001", issued_at=datetime(2026, 8, 3, 10, 5, 0),
        carrier_type="手機條碼載具", carrier_id_encrypted=FAKE_ENC("D01-carrier"), issue_status="issued", void_status="none",
    ),
    "D02": dict(
        invoice_type="b2c_invoice", invoice_no="DEVTEST-INV-0002", issued_at=datetime(2026, 8, 5, 14, 6, 0),
        carrier_type="統一編號", tax_id="00000001", invoice_title="測試用有限公司（虛構統編）",
        issue_status="issued", void_status="none",
    ),
    "D07": dict(
        invoice_type="b2c_invoice", invoice_no="DEVTEST-INV-0007", issued_at=datetime(2026, 8, 15, 13, 6, 0),
        carrier_type="捐贈發票", issue_status="issued", void_status="voided",
        void_reason="捐款人申請退款，原發票配合作廢（測試情境）", voided_by="cs.admin@charity.local",
    ),
    "D08": dict(
        invoice_type="donation_receipt", invoice_no=None, issued_at=None,
        national_id_encrypted=FAKE_ENC("D08-national-id"), receipt_address="臺中市西區測試路 8 號（虛構地址）",
        receipt_title="測試用．吳小婷", issue_status="pending", void_status="none",
    ),
    "D09": dict(
        invoice_type="donation_receipt", invoice_no=None, issued_at=None,
        national_id_encrypted=FAKE_ENC("D09-national-id"), receipt_address="臺中市西區測試路 9 號（虛構地址）",
        receipt_title="測試用．周小傑", issue_status="failed", void_status="none",
    ),
    "D10": dict(
        invoice_type="b2c_invoice", invoice_no="DEVTEST-INV-0010", issued_at=datetime(2026, 8, 25, 17, 6, 0),
        carrier_type="捐贈發票", issue_status="issued", void_status="allowance",
        void_reason="金額誤植，開立折讓後將重新開立正確金額發票（測試情境）", voided_by="sa@charity.local",
        is_annual_summary=True,
    ),
}

emit("-- ── 10. donation_invoices：已開立／待開立／開立失敗／已作廢／折讓，涵蓋三種 carrier_type ──")
for order_no_key, inv in INVOICES.items():
    d = next(x for x in DONATIONS if x["key"] == order_no_key)
    voided_by_sql = admin_sq(inv["voided_by"]) if inv.get("voided_by") else "NULL"
    cols = dict(
        invoice_type=inv["invoice_type"],
        invoice_no=inv.get("invoice_no"),
        issued_at=inv.get("issued_at"),
        carrier_type=inv.get("carrier_type"),
        carrier_id_encrypted=inv.get("carrier_id_encrypted"),
        tax_id=inv.get("tax_id"),
        national_id_encrypted=inv.get("national_id_encrypted"),
        receipt_address=inv.get("receipt_address"),
        invoice_title=inv.get("invoice_title"),
        receipt_title=inv.get("receipt_title"),
        is_annual_summary=inv.get("is_annual_summary", False),
        issue_status=inv["issue_status"],
        void_status=inv["void_status"],
        void_reason=inv.get("void_reason"),
    )
    block(f"""
IF NOT EXISTS (SELECT 1 FROM donation_invoices WHERE donation_id = {donation_sq(d["order_no"])})
  INSERT INTO donation_invoices (donation_id, invoice_type, invoice_no, issued_at, carrier_type, carrier_id_encrypted, tax_id, national_id_encrypted, receipt_address, invoice_title, receipt_title, is_annual_summary, issue_status, void_status, void_reason, voided_by)
  VALUES ({donation_sq(d["order_no"])}, {esc(cols["invoice_type"])}, {esc(cols["invoice_no"])}, {esc(cols["issued_at"])}, {esc(cols["carrier_type"])}, {esc(cols["carrier_id_encrypted"])}, {esc(cols["tax_id"])}, {esc(cols["national_id_encrypted"])}, {esc(cols["receipt_address"])}, {esc(cols["invoice_title"])}, {esc(cols["receipt_title"])}, {esc(cols["is_annual_summary"])}, {esc(cols["issue_status"])}, {esc(cols["void_status"])}, {esc(cols["void_reason"])}, {voided_by_sql});
""")

# ============================================================================
# 11. settlements／settlement_lines：一個完整結算週期（2026-08，含 6 筆 paid 捐款分別
#     結給 2 家店與 2 個項目）＋ 下一期（2026-09 上半）只含 D07 退款產生的沖回負項——
#     這組資料完整示範 docs/16 §4.2「只計 paid 捐款、改設定不追溯、退款以負項沖回下期」
#     三條規則同時成立的情形。
# ============================================================================
PERIOD1_START, PERIOD1_END = date(2026, 8, 1), date(2026, 8, 31)
PERIOD2_START, PERIOD2_END = date(2026, 9, 1), date(2026, 9, 15)

# (payee_type, payee_key, period, [ (donation_key, is_clawback) ])
SETTLEMENT_PLAN = [
    ("store", "store-a", (PERIOD1_START, PERIOD1_END), [("D01", False), ("D07", False), ("D09", False)],
     "paid", date(2026, 9, 5), "銀行轉帳", "已電匯至店家指定帳戶（測試資料，帳戶資訊未實際存在）"),
    ("store", "store-b", (PERIOD1_START, PERIOD1_END), [("D08", False)],
     "settled", None, None, None),
    ("project", "project-a", (PERIOD1_START, PERIOD1_END), [("D01", False), ("D02", False), ("D07", False), ("D10", False)],
     "paid", date(2026, 9, 6), "銀行轉帳", "已撥付予公益計畫執行單位（測試資料，帳戶資訊未實際存在）"),
    ("project", "project-b", (PERIOD1_START, PERIOD1_END), [("D08", False), ("D09", False)],
     "settled", None, None, None),
    ("store", "store-a", (PERIOD2_START, PERIOD2_END), [("D07", True)],
     "pending", None, None, None),
    ("project", "project-a", (PERIOD2_START, PERIOD2_END), [("D07", True)],
     "pending", None, None, None),
]

CLAWBACK_REASON = "原捐款單 {order_no} 經核可全額退款，依規則不追討，改以負項計入下一期結算（測試情境）"

emit("-- ── 11. settlements／settlement_lines：2026-08 完整結算週期 ＋ 2026-09 上半的退款沖回 ──")
for payee_type, payee_key, (period_start, period_end), lines, status, remitted_on, remit_method, remit_note in SETTLEMENT_PLAN:
    payee_id_sql = store_sq(STORE_SLUGS[payee_key]) if payee_type == "store" else project_sq(PROJECT_SLUGS[payee_key])
    donation_count = len(lines)
    donation_total = sum(next(x for x in DONATIONS if x["key"] == dk)["amount"] for dk, _ in lines)
    payable_amount = 0
    for dk, is_clawback in lines:
        store_amt, project_amt, _ = DONATION_SPLITS[dk]
        share = store_amt if payee_type == "store" else project_amt
        payable_amount += (-share if is_clawback else share)
    settlement_id = new_id("settlement", payee_type, payee_key, period_start.isoformat())
    block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM settlements WHERE payee_type = {esc(payee_type)} AND payee_id = {payee_id_sql} AND period_start = {esc(period_start)} AND period_end = {esc(period_end)};
IF @id IS NULL
BEGIN
  SET @id = {esc(settlement_id)};
  INSERT INTO settlements (id, period_start, period_end, payee_type, payee_id, donation_count, donation_total, payable_amount, status, remitted_on, remit_method, remit_note)
  VALUES (@id, {esc(period_start)}, {esc(period_end)}, {esc(payee_type)}, {payee_id_sql}, {esc(donation_count)}, {esc(donation_total)}, {esc(payable_amount)}, {esc(status)}, {esc(remitted_on)}, {esc(remit_method)}, {esc(remit_note)});
END
""")
    settlement_sq = (
        f"(SELECT id FROM settlements WHERE payee_type = {esc(payee_type)} AND payee_id = {payee_id_sql} "
        f"AND period_start = {esc(period_start)} AND period_end = {esc(period_end)})"
    )
    for dk, is_clawback in lines:
        d = next(x for x in DONATIONS if x["key"] == dk)
        store_amt, project_amt, _ = DONATION_SPLITS[dk]
        share = store_amt if payee_type == "store" else project_amt
        share_amount = -share if is_clawback else share
        clawback_reason = CLAWBACK_REASON.format(order_no=d["order_no"]) if is_clawback else None
        block(f"""
IF NOT EXISTS (SELECT 1 FROM settlement_lines WHERE settlement_id = {settlement_sq} AND donation_id = {donation_sq(d["order_no"])})
  INSERT INTO settlement_lines (settlement_id, donation_id, share_amount, is_clawback, clawback_reason)
  VALUES ({settlement_sq}, {donation_sq(d["order_no"])}, {esc(share_amount)}, {esc(is_clawback)}, {esc(clawback_reason)});
""")

# ============================================================================
# 12. reconciliation_runs／reconciliation_discrepancies：一次乾淨對帳 ＋ 一次有三種
#     差異類型的對帳（site_only／gateway_only／amount_mismatch，docs/16 §2.1b 三值）。
# ============================================================================
# 🔴 2026-09-22：本段原本把對帳與稽核的資料直接寫成 SQL 字面值，導致
# emit-charity-fixtures.py 匯不出來，慈善後台只好「逐字轉錄」種子腳本裡的數字自己存一份
# ——那正是 fixtures 管線要防止的第二份真實來源。改成模組常數後由匯出腳本統一涵蓋。
RECONCILIATION_RUNS = [
    {
        "key": "clean",
        "run_on": date(2026, 8, 21),
        "source": "linepay",
        "compared_count": 5,
        "matched_count": 5,
        "discrepancy_count": 0,
        "status": "completed",
    },
    {
        "key": "with-diff",
        "run_on": date(2026, 9, 16),
        "source": "linepay",
        "compared_count": 10,
        "matched_count": 7,
        "discrepancy_count": 3,
        "status": "completed",
    },
]

_D07 = next(x for x in DONATIONS if x["key"] == "D07")
_D08 = next(x for x in DONATIONS if x["key"] == "D08")
_D09 = next(x for x in DONATIONS if x["key"] == "D09")

# 三種差異類型各一筆，全部屬於 with-diff 那次對帳（docs/16 §2.1b 的三個值域）。
RECONCILIATION_DISCREPANCIES = [
    {
        "run_key": "with-diff",
        "discrepancy_type": "site_only",
        "donation_key": "D09",
        "donation_order_no": _D09["order_no"],
        "gateway_transaction_id": None,
        "site_amount": _D09["amount"],
        "gateway_amount": None,
        "resolution_status": "pending",
        "resolved_by": None,
        "resolve_note": None,
    },
    {
        "run_key": "with-diff",
        "discrepancy_type": "gateway_only",
        "donation_key": None,
        "donation_order_no": None,
        "gateway_transaction_id": "LP-TEST-GW-ORPHAN-0001",
        "site_amount": None,
        "gateway_amount": 350,
        "resolution_status": "pending",
        "resolved_by": None,
        "resolve_note": None,
    },
    {
        "run_key": "with-diff",
        "discrepancy_type": "amount_mismatch",
        "donation_key": "D08",
        "donation_order_no": _D08["order_no"],
        "gateway_transaction_id": "LP-TEST-GW-0008",
        "site_amount": _D08["amount"],
        "gateway_amount": 990,
        "resolution_status": "resolved",
        "resolved_by": "sa@charity.local",
        "resolve_note": "人工核對為金流端先行扣除之銀行手續費，屬正常誤差已認列（測試資料）",
    },
]

emit("-- ── 12a. reconciliation_runs：一次乾淨、一次有差異 ─────────────────────")
for _r in RECONCILIATION_RUNS:
    block(
        "\nIF NOT EXISTS (SELECT 1 FROM reconciliation_runs WHERE run_on = "
        + esc(_r["run_on"]) + " AND source = " + esc(_r["source"]) + ")\n"
        "  INSERT INTO reconciliation_runs (run_on, source, compared_count, matched_count, discrepancy_count, status)\n"
        "  VALUES (" + esc(_r["run_on"]) + ", " + esc(_r["source"]) + ", "
        + str(_r["compared_count"]) + ", " + str(_r["matched_count"]) + ", "
        + str(_r["discrepancy_count"]) + ", " + esc(_r["status"]) + ");\n"
    )


def recon_run_sq(run_key: str) -> str:
    r = next(x for x in RECONCILIATION_RUNS if x["key"] == run_key)
    return ("(SELECT id FROM reconciliation_runs WHERE run_on = " + esc(r["run_on"])
            + " AND source = " + esc(r["source"]) + ")")


emit("-- ── 12b. reconciliation_discrepancies：三種類型各一筆 ────────────────────")
for _d in RECONCILIATION_DISCREPANCIES:
    _run = recon_run_sq(_d["run_key"])
    _don = donation_sq(_d["donation_order_no"]) if _d["donation_order_no"] else "NULL"
    # 冪等鍵：同一次對帳內，同一種差異類型對同一筆捐款／同一個金流交易編號只會有一筆。
    _key_cond = ("donation_id = " + _don) if _d["donation_order_no"] else (
        "gateway_transaction_id = " + esc(_d["gateway_transaction_id"]))
    block(
        "\nIF NOT EXISTS (SELECT 1 FROM reconciliation_discrepancies WHERE reconciliation_run_id = "
        + _run + " AND discrepancy_type = " + esc(_d["discrepancy_type"]) + " AND " + _key_cond + ")\n"
        "  INSERT INTO reconciliation_discrepancies (reconciliation_run_id, discrepancy_type, donation_id, "
        "gateway_transaction_id, site_amount, gateway_amount, resolution_status, resolved_by, resolve_note)\n"
        "  VALUES (" + _run + ", " + esc(_d["discrepancy_type"]) + ", " + _don + ", "
        + esc(_d["gateway_transaction_id"]) + ", " + esc(_d["site_amount"]) + ", "
        + esc(_d["gateway_amount"]) + ", " + esc(_d["resolution_status"]) + ", "
        + (admin_sq(_d["resolved_by"]) if _d["resolved_by"] else "NULL") + ", "
        + esc(_d["resolve_note"]) + ");\n"
    )

# ============================================================================
# 13. audit_logs：append-only，涵蓋 docs/16 §5 三類須稽核的操作：退款、分潤百分比
#     設定、含個資的明細匯出。source_ip 一律用 RFC 5737 保留給文件用途的網段
#    （203.0.113.0/24），不是任何真實伺服器的 IP。
# ============================================================================
AUDIT_LOGS = [
    {
        "admin_username": "cs.admin@charity.local",
        "occurred_at": datetime(2026, 8, 16, 10, 0, 0),
        "action": "refund",
        "target_type": "donation",
        "target_donation_order_no": _D07["order_no"],
        "target_project_key": None,
        "change_summary": "退款金額 5000 元，捐款人誤捐重複扣款（測試情境）",
        "purpose_note": None,
        "source_ip": "203.0.113.10",
    },
    {
        "admin_username": "sa@charity.local",
        "occurred_at": datetime(2026, 9, 1, 9, 0, 0),
        "action": "update_share_pct",
        "target_type": "donation_project",
        "target_donation_order_no": None,
        "target_project_key": "project-a",
        "change_summary": "project_share_pct 由 8.00% 調整為 10.00%（測試情境，此前建立的捐款單快照不受影響）",
        "purpose_note": None,
        "source_ip": "203.0.113.11",
    },
    {
        "admin_username": "cs.admin@charity.local",
        "occurred_at": datetime(2026, 9, 18, 15, 0, 0),
        "action": "export_personal_data",
        "target_type": "donation_list_export",
        "target_donation_order_no": None,
        "target_project_key": None,
        "change_summary": "匯出含個資之捐款明細共 10 筆（測試情境）",
        "purpose_note": "國稅局申報之捐款明細核對需求（測試占位用途）",
        "source_ip": "203.0.113.12",
    },
]

emit("-- ── 13. audit_logs：退款／分潤設定／個資匯出三類法遵稽核紀錄 ────────────")
for _a in AUDIT_LOGS:
    _admin = admin_sq(_a["admin_username"])
    if _a["target_donation_order_no"]:
        _target = donation_sq(_a["target_donation_order_no"])
    elif _a["target_project_key"]:
        _target = project_sq(PROJECT_SLUGS[_a["target_project_key"]])
    else:
        _target = "NULL"
    _target_cond = (" AND target_id = " + _target) if _target != "NULL" else ""
    block(
        "\nIF NOT EXISTS (SELECT 1 FROM audit_logs WHERE admin_user_id = " + _admin
        + " AND action = " + esc(_a["action"]) + " AND target_type = " + esc(_a["target_type"])
        + _target_cond + ")\n"
        "  INSERT INTO audit_logs (admin_user_id, occurred_at, action, target_type, target_id, "
        "change_summary, purpose_note, source_ip)\n"
        "  VALUES (" + _admin + ", " + esc(_a["occurred_at"]) + ", " + esc(_a["action"]) + ", "
        + esc(_a["target_type"]) + ", " + _target + ", " + esc(_a["change_summary"]) + ", "
        + esc(_a["purpose_note"]) + ", " + esc(_a["source_ip"]) + ");\n"
    )

# ============================================================================
# 14. settings／settings_i18n：文案類走 i18n 側表，非文案的規則性數值直接存 value。
# ============================================================================
SETTINGS_PLAIN = [
    ("donation.default_min_amount", "100"),
    ("donation.default_max_amount", "1000000"),
    ("donation.credit_list_display_rule", "named_unless_anonymous"),
]

emit("-- ── 14a. settings：非文案類規則性數值（3 筆） ────────────────────────")
for key, value in SETTINGS_PLAIN:
    block(f"""
IF NOT EXISTS (SELECT 1 FROM settings WHERE setting_key = {esc(key)})
  INSERT INTO settings (setting_key, value) VALUES ({esc(key)}, {esc(value)});
""")

SETTINGS_I18N = [
    ("donation.home_intro", "（開發測試用首頁說明文案，正式文案待客戶與協會確認）", "(Dev seed placeholder home intro copy, pending client/association confirmation.)"),
    ("donation.thank_you_message_template", "感謝您的愛心捐款！（開發測試用感謝語樣板）", "Thank you for your generous donation! (dev seed placeholder)"),
    ("donation.notice", "本平台捐款一經完成，對外恕不受理退款申請（開發測試用捐款須知占位文字）。", "Donations are final and non-refundable via the public site (dev seed placeholder notice)."),
    ("donation.privacy_policy", "（開發測試用隱私權政策占位文字，正式內容待法務確認）", "(Dev seed placeholder privacy policy, pending legal review.)"),
]

emit("-- ── 14b. settings：文案類（4 筆，含 zh-Hant／en） ───────────────────────")
for key, value_zh, value_en in SETTINGS_I18N:
    setting_id = new_id("setting", key)
    block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM settings WHERE setting_key = {esc(key)};
IF @id IS NULL
BEGIN
  SET @id = {esc(setting_id)};
  INSERT INTO settings (id, setting_key, value) VALUES (@id, {esc(key)}, NULL);
  INSERT INTO settings_i18n (setting_id, locale, value) VALUES (@id, N'zh-Hant', {esc(value_zh)});
  INSERT INTO settings_i18n (setting_id, locale, value) VALUES (@id, N'en', {esc(value_en)});
END
""")

# ============================================================================
# 15. email_templates／email_templates_i18n：四封系統信（規劃書 §3.5，docs/16 §2.5）。
# ============================================================================
EMAIL_TEMPLATES = [
    ("donation_thanks", "感謝您的捐款（開發測試用樣板主旨）", "感謝您對本次公益計畫的支持，這是開發測試用的樣板內文，正式文案待協會確認。",
     "Thank you for your donation (dev seed placeholder subject)", "Thank you for supporting this program. This is a dev seed placeholder body; final copy pending association review."),
    ("invoice_issued", "您的電子發票／收據已開立（開發測試用樣板主旨）", "您的憑證已開立，這是開發測試用的樣板內文，正式文案待協會確認。",
     "Your invoice/receipt has been issued (dev seed placeholder subject)", "Your document has been issued. This is a dev seed placeholder body; final copy pending association review."),
    ("invoice_failed", "發票開立失敗通知（開發測試用樣板主旨）", "您的憑證開立失敗，我們將盡快處理，這是開發測試用的樣板內文。",
     "Invoice issuance failed (dev seed placeholder subject)", "Your document failed to issue and will be retried. This is a dev seed placeholder body."),
    ("refund_notice", "退款通知（開發測試用樣板主旨）", "您的捐款已完成退款，這是開發測試用的樣板內文，正式文案待協會確認。",
     "Refund notice (dev seed placeholder subject)", "Your donation has been refunded. This is a dev seed placeholder body; final copy pending association review."),
]

emit("-- ── 15. email_templates／email_templates_i18n：四封系統信 ───────────────")
for code, subj_zh, body_zh, subj_en, body_en in EMAIL_TEMPLATES:
    template_id = new_id("email_template", code)
    block(f"""
DECLARE @id uniqueidentifier;
SELECT @id = id FROM email_templates WHERE code = {esc(code)};
IF @id IS NULL
BEGIN
  SET @id = {esc(template_id)};
  INSERT INTO email_templates (id, code, is_active) VALUES (@id, {esc(code)}, 1);
  INSERT INTO email_templates_i18n (email_template_id, locale, subject, body) VALUES (@id, N'zh-Hant', {esc(subj_zh)}, {esc(body_zh)});
  INSERT INTO email_templates_i18n (email_template_id, locale, subject, body) VALUES (@id, N'en', {esc(subj_en)}, {esc(body_en)});
END
""")

# ============================================================================
# 16. email_logs：4 筆寄送紀錄，涵蓋 sent／failed 兩種狀態。
# ============================================================================
EMAIL_LOGS = [
    ("donation_thanks", "D01", "sent", datetime(2026, 8, 3, 10, 3, 0)),
    ("invoice_issued", "D01", "sent", datetime(2026, 8, 3, 10, 6, 0)),
    ("invoice_failed", "D09", "failed", datetime(2026, 8, 20, 9, 34, 0)),
    ("refund_notice", "D07", "sent", datetime(2026, 8, 16, 10, 5, 0)),
]

emit("-- ── 16. email_logs：4 筆，涵蓋 sent／failed ─────────────────────────────")
for template_code, donation_key, status, sent_at in EMAIL_LOGS:
    d = next(x for x in DONATIONS if x["key"] == donation_key)
    block(f"""
IF NOT EXISTS (SELECT 1 FROM email_logs WHERE type = {esc(template_code)} AND recipient_email = {esc(d["donor_email"])} AND sent_at = {esc(sent_at)})
  INSERT INTO email_logs (email_template_id, type, recipient_email, sent_at, status)
  VALUES ({template_sq(template_code)}, {esc(template_code)}, {esc(d["donor_email"])}, {esc(sent_at)}, {esc(status)});
""")

# ============================================================================
# 17. payment_channels：協會的 LINE Pay 與電子發票憑證各一筆，皆為 sandbox（沒有正式
#     憑證，也不假裝有——credential_encrypted 是清楚標明「非真實」的占位字串）。
# ============================================================================
emit("-- ── 17. payment_channels：LINE Pay／電子發票各一筆，皆為 sandbox 占位憑證 ──")
block(f"""
IF NOT EXISTS (SELECT 1 FROM payment_channels WHERE channel_type = N'line_pay' AND environment = N'sandbox')
  INSERT INTO payment_channels (channel_type, environment, credential_encrypted, invoice_prefix)
  VALUES (N'line_pay', N'sandbox', {esc(FAKE_ENC("line_pay-sandbox"))}, NULL);
""")
block(f"""
IF NOT EXISTS (SELECT 1 FROM payment_channels WHERE channel_type = N'einvoice' AND environment = N'sandbox')
  INSERT INTO payment_channels (channel_type, environment, credential_encrypted, invoice_prefix)
  VALUES (N'einvoice', N'sandbox', {esc(FAKE_ENC("einvoice-sandbox"))}, N'TEST');
""")

emit("-- ============================================================================")
emit("-- 檔案結束。ui_strings／ui_string_translations 刻意留空——本次任務範圍未列，")
emit("-- 且尚未有實際介面字串內容可種（比照主站種子腳本同樣未種這兩張表的慣例）。")
emit("-- ============================================================================")

print("\n".join(out))
