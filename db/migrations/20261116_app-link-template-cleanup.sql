/* ============================================================================
   App 連結樣板清理（資料遷移）
   2026-11-16｜依據：docs/23 H-3 M-3／M-5（後台給 App 使用者點的連結不得是帶佔位符的樣板）

   背景：深連結對照表（app_deep_links）存的是「路由規則」，含 {id}／{slug} 是正常的；但快捷入口、「更多」項目、
   推播、公告條、廣告素材與版位備援連結是「給使用者點的連結」，不能是樣板——兩個 App 都會把含 { } 的連結視為不可用，
   後台設了等於沒設。新版 API 在寫入時擋下樣板（連結選擇器改成「先選要前往哪裡、再選對象」）。

   ── 做的事（冪等；只動「值含大括號」的使用者連結，一律改為 NULL＝沒有連結）──
     ① app_layout_items.deep_link_id：指向「app_link 含大括號」那一列的項目 → NULL
        （種子的快捷入口／更多「課程報名」；App 對代碼 programs 固定走內建課程列表，所以行為不變）。
     ② push_messages.deep_link、app_announcements.link_url、ad_creatives.click_url、ad_slots.fallback_link
        值含 { 或 } → NULL。App 對這些值本來就視為不可用（推播回首頁、公告變純文字），清掉不改變任何使用者看得到的行為。
   ── 沒有做的事 ──
     • 不動 app_deep_links（深連結對照表本身仍可存樣板）。
     • 不動不含大括號的任何連結（含 https 網址與完整的 tcrfc:// 連結）。

   沒套用這支遷移的庫：新版 API 不會因為舊樣板擋住「不相關欄位」的儲存（連結沒改就不重驗），
   但樣板仍留在資料裡、App 端照舊把它當不可用；快捷入口「課程報名」仍綁著樣板列（顯示行為與先前相同）。
   在畫面上編輯那個項目時會看到「連結需要重新選擇」的提示。
   🔴 冪等；事後斷言失敗即 THROW（單一交易）。不含中文字面值，sqlcmd 版本不拘。
   ============================================================================ */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRAN;

UPDATE i SET deep_link_id = NULL, updated_at = SYSUTCDATETIME()
FROM app_layout_items i
JOIN app_deep_links l ON l.id = i.deep_link_id
WHERE l.app_link LIKE N'%{%' OR l.app_link LIKE N'%}%';

UPDATE push_messages SET deep_link = NULL WHERE deep_link LIKE N'%{%' OR deep_link LIKE N'%}%';
UPDATE app_announcements SET link_url = NULL, updated_at = SYSUTCDATETIME() WHERE link_url LIKE N'%{%' OR link_url LIKE N'%}%';
UPDATE ad_creatives SET click_url = NULL WHERE click_url LIKE N'%{%' OR click_url LIKE N'%}%';
UPDATE ad_slots SET fallback_link = NULL WHERE fallback_link LIKE N'%{%' OR fallback_link LIKE N'%}%';

IF EXISTS (SELECT 1 FROM app_layout_items i JOIN app_deep_links l ON l.id = i.deep_link_id WHERE l.app_link LIKE N'%{%' OR l.app_link LIKE N'%}%')
   OR EXISTS (SELECT 1 FROM push_messages WHERE deep_link LIKE N'%{%' OR deep_link LIKE N'%}%')
   OR EXISTS (SELECT 1 FROM app_announcements WHERE link_url LIKE N'%{%' OR link_url LIKE N'%}%')
   OR EXISTS (SELECT 1 FROM ad_creatives WHERE click_url LIKE N'%{%' OR click_url LIKE N'%}%')
   OR EXISTS (SELECT 1 FROM ad_slots WHERE fallback_link LIKE N'%{%' OR fallback_link LIKE N'%}%')
  THROW 51410, N'清理後仍有含大括號的使用者連結', 1;

COMMIT;
GO
