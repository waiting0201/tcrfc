#!/usr/bin/env python3
# db/seed/generate-page-template-migration.py — 產生 db/migrations/20261007_page-templates.sql（2026-10-07）
#
# B1 頁面管理改為「固定頁＋固定欄位」後，既有資料庫裡已存在的頁面要對齊版型
# （apps/api/Features/AdminPages/PageTemplates.cs；內容取自 page_seed_content.json，與種子同一份）：
#   ① 刪除測試草稿頁 test-draft-page（版型外的舊頁，後台已無法編輯）
#   ② 藍鯨 about/vision 改名為 about/vision-mission（兩俱樂部 slug 統一；目標不存在才改；目標已存在則刪除舊的孤兒頁）
#   ③ 版型內的頁面若區塊類型順序與版型不同（例：舊種子的 steps＋quote＋cta），整份換成版型的內容並新增一個版本、
#      狀態改為已發布（內容就是前台備用文案）；結構已相符者一律不動（保留後台編輯過的內容）
#   ④ 版型內但資料庫還沒有的頁面：以種子內容建立（已發布；內容與前台備用文案相同，所以對外看起來不變，只是從此可在後台編輯）。
#      這一步讓「既有資料庫」不需重跑種子就建齊固定頁；後台清單的補建骨架頁（AdminPagesRepository.EnsureTemplatePagesAsync）
#      只是第二道保險，正常不會觸發。
# 冪等、單一交易、可重複執行。結構比對只看區塊類型順序（固定列數由 API 在寫入時檢查，種子本身符合）。
# 執行：sqlcmd -S <server> -d tcrfc_club -b -i db/migrations/20261007_page-templates.sql
# 用法：python3 db/seed/generate-page-template-migration.py [--check]

import hashlib
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
OUT = HERE.parents[0] / "migrations" / "20261007_page-templates.sql"
DATA = json.loads((HERE / "page_seed_content.json").read_text(encoding="utf-8"))


def esc(v):
    return "N'" + str(v).replace("'", "''") + "'"


def jdump(o):
    return json.dumps(o, ensure_ascii=False, separators=(",", ":"))


def build() -> str:
    out = [f"""/* ============================================================================
   B1 頁面管理「固定頁＋固定欄位」遷移（2026-10-07）——由 db/seed/generate-page-template-migration.py 產生，勿手改
   ① 刪除 test-draft-page ② 藍鯨 about/vision → about/vision-mission ③ 結構與版型不符的既有頁整份換成版型內容（新增版本、已發布）
   ④ 缺頁以種子內容建立（已發布）。冪等、單一交易。
   執行：sqlcmd -S <server> -d tcrfc_club -b -i db/migrations/20261007_page-templates.sql
   ============================================================================ */
SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- ① 版型外的測試草稿頁（FK 為 ON DELETE CASCADE：i18n／區塊／版本隨頁面刪除）
DELETE FROM pages WHERE slug = N'test-draft-page';

-- ② 藍鯨願景頁 slug 統一
UPDATE p SET slug = N'about/vision-mission', updated_at = SYSUTCDATETIME()
FROM pages p JOIN clubs c ON c.id = p.club_id
WHERE c.code = N'bw' AND p.slug = N'about/vision'
  AND NOT EXISTS (SELECT 1 FROM pages x WHERE x.club_id = p.club_id AND x.slug = N'about/vision-mission');

-- ②b 目標已存在（例如改名後又重灌種子、或缺頁補齊先建了）時，舊 slug 是版型外的孤兒頁（後台詳情會 500），刪除（FK CASCADE）
DELETE p FROM pages p JOIN clubs c ON c.id = p.club_id
WHERE c.code = N'bw' AND p.slug = N'about/vision'
  AND EXISTS (SELECT 1 FROM pages x WHERE x.club_id = p.club_id AND x.slug = N'about/vision-mission');
"""]
    for club, pages in DATA.items():
        for page in pages:
            types = ",".join(b["type"] for b in page["blocks"])
            snapshot = {
                "seo": {"zh": {"seoTitle": page["seo"]["zh"]["title"], "seoDescription": page["seo"]["zh"]["description"]}},
                "blocks": [{"blockType": b["type"], "content": b["content"]} for b in page["blocks"]],
            }
            if page["seo"].get("en"):
                snapshot["seo"]["en"] = {"seoTitle": page["seo"]["en"]["title"], "seoDescription": page["seo"]["en"]["description"]}
            blocks_sql = "\n".join(
                f"  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (NEWID(), @id, {esc(b['type'])}, {esc(jdump(b['content']))}, {i});"
                for i, b in enumerate(page["blocks"]))
            en = page["seo"].get("en")
            en_sql = ""
            if en:
                en_sql = f"""
  IF NOT EXISTS (SELECT 1 FROM pages_i18n WHERE page_id = @id AND locale = N'en')
    INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description) VALUES (@id, N'en', {esc(en['title'])}, {esc(en['description'])});"""
            out.append(f"""
-- ③④ {club} / {page["slug"]}
BEGIN
  DECLARE @id uniqueidentifier = (SELECT p.id FROM pages p JOIN clubs c ON c.id = p.club_id WHERE c.code = N'{club}' AND p.slug = {esc(page["slug"])});
  IF @id IS NULL
  BEGIN
    SET @id = NEWID();
    INSERT INTO pages (id, club_id, slug, status, published_at)
      VALUES (@id, (SELECT id FROM clubs WHERE code = N'{club}'), {esc(page["slug"])}, N'published', SYSUTCDATETIME());
    INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description) VALUES (@id, N'zh-Hant', {esc(page["seo"]["zh"]["title"])}, {esc(page["seo"]["zh"]["description"])});{en_sql}
{blocks_sql}
    INSERT INTO page_versions (id, page_id, version_no, snapshot, preview_token)
      VALUES (NEWID(), @id, 1, {esc(jdump(snapshot))}, LOWER(CONVERT(varchar(64), CRYPT_GEN_RANDOM(32), 2)));
  END
  ELSE IF ISNULL((SELECT STRING_AGG(CAST(block_type AS nvarchar(max)), N',') WITHIN GROUP (ORDER BY sort_order) FROM page_blocks WHERE page_id = @id), N'') <> N'{types}'
     OR EXISTS (SELECT 1 FROM page_blocks WHERE page_id = @id AND ISJSON(CAST(content AS nvarchar(max))) = 0) -- 內容不是合法 JSON（後台詳情會 500）也視為不符
  BEGIN
    DELETE FROM page_blocks WHERE page_id = @id;
{blocks_sql}
    IF NOT EXISTS (SELECT 1 FROM pages_i18n WHERE page_id = @id AND locale = N'zh-Hant')
      INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description) VALUES (@id, N'zh-Hant', {esc(page["seo"]["zh"]["title"])}, {esc(page["seo"]["zh"]["description"])});
    UPDATE pages_i18n SET seo_title = ISNULL(NULLIF(seo_title, N''), {esc(page["seo"]["zh"]["title"])}),
                          seo_description = ISNULL(NULLIF(seo_description, N''), {esc(page["seo"]["zh"]["description"])})
      WHERE page_id = @id AND locale = N'zh-Hant';{en_sql}
    INSERT INTO page_versions (id, page_id, version_no, snapshot, preview_token)
      VALUES (NEWID(), @id, ISNULL((SELECT MAX(version_no) FROM page_versions WHERE page_id = @id), 0) + 1, {esc(jdump(snapshot))}, LOWER(CONVERT(varchar(64), CRYPT_GEN_RANDOM(32), 2)));
    UPDATE pages SET status = N'published', published_at = ISNULL(published_at, SYSUTCDATETIME()), updated_at = SYSUTCDATETIME() WHERE id = @id;
  END
END
GO
""")
    out.append("\nCOMMIT TRANSACTION;\n")
    return "".join(out)


if __name__ == "__main__":
    text = build()
    if "--check" in sys.argv[1:]:
        ok = OUT.exists() and OUT.read_text(encoding="utf-8") == text
        print("一致" if ok else "不一致：請重新產生 db/migrations/20261007_page-templates.sql")
        sys.exit(0 if ok else 1)
    OUT.write_text(text, encoding="utf-8")
    print(f"已寫出 {OUT}（{hashlib.sha256(text.encode()).hexdigest()[:12]}）")
