/* ============================================================================
   menu_items／menu_items_i18n 刪表（contract）
   2026-10-07｜依據：主站規劃書 v3.22——前台選單（主選單／Mega Menu／Footer）固定在前台版型，後台不提供選單管理（STATUS S2-24）。

   🔴 先確認新版 api 已上線（EF 已無 MenuItem 實體、已無 /api/v1/{club}/menus 與 /api/v1/admin/{club}/menus 端點）再跑：
      舊版 api 仍會查這兩張表，跑了會立刻 500。
   🔴 刪表無法找回（PITR 只有 7 天，docs/20 §5）；表內是後台曾維護的選單樹，前台改讀版型寫死的選單，視為可丟棄。
      需要留底的話先自行匯出：SELECT * FROM menu_items; SELECT * FROM menu_items_i18n;
   🔴 冪等：表不存在就跳過。先刪 i18n（FK 指向 menu_items）；menu_items 的自我參照外鍵隨表一併移除。
   🔴 同時刪除四個權限碼（site.menu.view／update、content.page.create／delete，S2-24／S2-23）；刪除無法還原指派，需要時由種子產生器重建。
   執行：sqlcmd -S <server> -d tcrfc_club -b -I -i db/migrations/20261007_menu-items-drop_2-contract.sql
   ============================================================================ */
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'menu_items_i18n', N'U') IS NOT NULL DROP TABLE menu_items_i18n;
IF OBJECT_ID(N'menu_items', N'U') IS NOT NULL DROP TABLE menu_items;

-- 權限碼清理：選單管理（I2）與頁面新增／刪除（B1，v3.21 固定頁）已無端點使用。role_permissions 以 FK ON DELETE CASCADE 隨之移除。
DELETE FROM permissions WHERE code IN (N'site.menu.view', N'site.menu.update', N'content.page.create', N'content.page.delete');

COMMIT TRANSACTION;
