/* ============================================================================
   「球迷會員」改名（資料遷移）
   2026-10-16｜依據：使用者 2026-10-16 裁決「照客戶文件」；主站規劃書 v3.29（新制沒有「球迷會員」這一級）。
   K5「球迷會員抽獎」→「會員抽獎」（Member Draw）；泛指持有會籍者→「會員」。

   ── 做的事（全部冪等；只換字面值，不動結構）──
     ① 標籤 slug=member-draw 的中文名稱：球迷會員抽獎 → 會員抽獎（後台「產生公布稿草稿」建立標籤時只在不存在時才寫名稱，
        所以已建過標籤的庫不會自己改名；英文 Member Draw 不變）。標籤以 slug 辨識，推播守衛（PushContentGuard）不受名稱影響。
     ② 種子殘留的測試資料文案（方案名稱、活動說明、推播公告標題、抽獎辦法）：「球迷會員」→「會員」「有效球迷會員會籍」→「有效會籍」，
        英文 Fan Club → Member。只在欄位仍含舊字面值時才換，後台改過的內容不會被蓋掉（沒有舊字面值就不動）。
   ── 沒有做的事 ──
     • 🔴 不動 draw_rosters.tier_snapshot（凍結名單，名單雜湊含層級字串，見 docs/18 E-331）；後台對舊值顯示「球迷會員（舊制）」是刻意保留。
     • 不動 draw_rosters／member_draws 的任何其他欄位，不動 slug、代碼、旗標。

   套用順序：與其他 20261016 資料遷移互不相依，可在 client-doc-alignment_1-expand 之後任何時間套用（不改結構、不需先 migrate 再 deploy）。
   🔴 冪等；單一交易。含中文：手動套用請用 sqlcmd 18／go-sqlcmd 加 -f 65001（sqlcmd 13 會把 UTF-8 切壞，見 docs/14）；
      正式庫走 deploy/db-migrate.sh（已轉 UTF-16）。
   ============================================================================ */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @n int, @total int = 0;

-- ① 標籤
UPDATE ti SET name = N'會員抽獎'
FROM tags_i18n ti JOIN tags t ON t.id = ti.tag_id
WHERE t.slug = N'member-draw' AND ti.locale = N'zh-Hant' AND ti.name = N'球迷會員抽獎';
SET @n = @@ROWCOUNT; SET @total += @n; PRINT CONCAT(N'tags_i18n 更新 ', @n, N' 筆');

-- ② 測試資料文案（逐欄位、先換較長的詞，再換剩餘的「球迷會員」）
UPDATE membership_plans_i18n SET name = REPLACE(name, N'球迷會員', N'會員') WHERE name LIKE N'%球迷會員%';
SET @n = @@ROWCOUNT; SET @total += @n; PRINT CONCAT(N'membership_plans_i18n.name（中）更新 ', @n, N' 筆');
UPDATE membership_plans_i18n SET name = REPLACE(name, N'Fan Club', N'Member') WHERE locale = N'en' AND name LIKE N'%Fan Club%';
SET @n = @@ROWCOUNT; SET @total += @n; PRINT CONCAT(N'membership_plans_i18n.name（英）更新 ', @n, N' 筆');

UPDATE fan_events_i18n SET description = REPLACE(description, N'付費球迷會員', N'會員') WHERE description LIKE N'%付費球迷會員%';
SET @n = @@ROWCOUNT; SET @total += @n; PRINT CONCAT(N'fan_events_i18n.description 更新 ', @n, N' 筆');
UPDATE fan_events_i18n SET name = REPLACE(name, N'(fan club members only)', N'(members only)') WHERE locale = N'en' AND name LIKE N'%(fan club members only)%';
SET @n = @@ROWCOUNT; SET @total += @n; PRINT CONCAT(N'fan_events_i18n.name（英）更新 ', @n, N' 筆');

UPDATE push_messages_i18n SET title = REPLACE(title, N'球迷會員', N'會員') WHERE title LIKE N'%球迷會員%';
SET @n = @@ROWCOUNT; SET @total += @n; PRINT CONCAT(N'push_messages_i18n.title 更新 ', @n, N' 筆');

UPDATE member_draws_i18n SET rules = REPLACE(REPLACE(rules, N'有效球迷會員會籍', N'有效會籍'), N'球迷會員', N'會員') WHERE rules LIKE N'%球迷會員%';
SET @n = @@ROWCOUNT; SET @total += @n; PRINT CONCAT(N'member_draws_i18n.rules（中）更新 ', @n, N' 筆');
UPDATE member_draws_i18n SET rules = REPLACE(rules, N'valid fan club membership', N'valid membership') WHERE locale = N'en' AND rules LIKE N'%valid fan club membership%';
SET @n = @@ROWCOUNT; SET @total += @n; PRINT CONCAT(N'member_draws_i18n.rules（英）更新 ', @n, N' 筆');

PRINT CONCAT(N'member-draw-rename：共更新 ', @total, N' 筆');

-- 斷言：上述欄位與標籤不再含舊名稱
IF EXISTS (SELECT 1 FROM tags_i18n ti JOIN tags t ON t.id = ti.tag_id WHERE t.slug = N'member-draw' AND ti.name LIKE N'%球迷會員%')
  OR EXISTS (SELECT 1 FROM membership_plans_i18n WHERE name LIKE N'%球迷會員%')
  OR EXISTS (SELECT 1 FROM member_draws_i18n WHERE rules LIKE N'%球迷會員%')
  THROW 50301, N'會員抽獎改名後仍殘留「球迷會員」，中止', 1;

COMMIT TRAN;
