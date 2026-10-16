/* ============================================================================
   磐石學院梯隊代號對齊客戶 4.3 發展路徑、聯絡地址獨立設定（資料遷移）
   2026-10-16｜依據：使用者 2026-10-16 裁決「照客戶文件」；主站規劃書 v3.29（§3.4 4.2／4.3、§3.13、§4.9 I 聯絡資訊、§5.1 Team）。
   全部是台中磐石（tcrfc）的資料，不動藍鯨（BW-U15／BW-U12 維持原樣）。

   ── 做的事（全部冪等；只在值仍是舊種子原文時才換，不覆蓋後台已編輯的內容）──
     ① 站台事實 site.squad_codes：U15,U14,U12 → U10,U12,U14,U16,U18。
     ② 站台事實 site.squad_structure_summary（中英）與 llms.txt 事實摘要 geo.llms_facts_summary（中英）裡的梯隊敘述。
     ③ 報名表單 academy_children_training 的「報名項目」選項（form_fields／form_fields_i18n.options_json）：
        學院 U12／U14／U15 → 學院 U10／U12／U14／U16／U18（中英）。
     ④ 聯絡地址（新設定鍵 site.contact_address，中英）：昌平路二段敬固巷11弄41號。⚠️ 與場地地址（崇平路二段景谷巷）是兩件事，場地地址不動。
        只在不存在或為空時寫入（客戶 10 聯絡資訊的地址、英文地址原文）。
   ── 沒有做的事 ──
     • 不建立任何 teams 列：磐石從未建過 U15／U14／U12，正式庫沒有舊代碼要遷移（CF-0 查證）；學院梯隊由 C1 球隊資料實際建立哪幾隊為準。
     • 行事曆訂閱 feed 的隊別代號 U15 從未對外發出，不需轉址（CF-6）。

   🔴 冪等；單一交易。執行：sqlcmd -S <server> -d tcrfc_club -b -I -i db/migrations/20261016_academy-squads-and-contact-address.sql
   ============================================================================ */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @club uniqueidentifier = (SELECT id FROM clubs WHERE code = N'tcrfc');
IF @club IS NULL THROW 50201, N'找不到 tcrfc 俱樂部（clubs.code = tcrfc）', 1;
DECLARE @n int, @sid uniqueidentifier;

-- ① site.squad_codes
UPDATE settings SET setting_value = N'U10,U12,U14,U16,U18', updated_at = SYSUTCDATETIME()
WHERE club_id = @club AND setting_key = N'site.squad_codes' AND setting_value = N'U15,U14,U12';
SET @n = @@ROWCOUNT; PRINT CONCAT(N'site.squad_codes 更新 ', @n, N' 筆');

-- ② 梯隊敘述（中英）
UPDATE si SET value = N'一線隊與足球學院（U10／U12／U14／U16／U18）並行的發展體系'
FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
WHERE s.club_id = @club AND s.setting_key = N'site.squad_structure_summary' AND si.locale = N'zh-Hant'
  AND si.value = N'一線隊與足球學院（U15／U14／U12）三個梯隊並行的發展體系';
SET @n = @@ROWCOUNT; PRINT CONCAT(N'site.squad_structure_summary（中）更新 ', @n, N' 筆');
UPDATE si SET value = N'A development pathway with the first team and the Academy (U10 / U12 / U14 / U16 / U18) running in parallel'
FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
WHERE s.club_id = @club AND s.setting_key = N'site.squad_structure_summary' AND si.locale = N'en'
  AND si.value = N'A development pathway with the first team and the Academy (U15 / U14 / U12) running in parallel';
SET @n = @@ROWCOUNT; PRINT CONCAT(N'site.squad_structure_summary（英）更新 ', @n, N' 筆');

UPDATE si SET value = REPLACE(si.value, N'一線隊與足球學院（U15／U14／U12）三個梯隊並行的發展體系', N'一線隊與足球學院（U10／U12／U14／U16／U18）並行的發展體系')
FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
WHERE s.club_id = @club AND s.setting_key = N'geo.llms_facts_summary' AND si.locale = N'zh-Hant'
  AND si.value LIKE N'%一線隊與足球學院（U15／U14／U12）三個梯隊並行的發展體系%';
SET @n = @@ROWCOUNT; PRINT CONCAT(N'geo.llms_facts_summary（中）更新 ', @n, N' 筆');
UPDATE si SET value = REPLACE(si.value, N'Squads: the first team plus the Academy (U15 / U14 / U12).', N'Squads: the first team plus the Academy (U10 / U12 / U14 / U16 / U18).')
FROM settings_i18n si JOIN settings s ON s.id = si.setting_id
WHERE s.club_id = @club AND s.setting_key = N'geo.llms_facts_summary' AND si.locale = N'en'
  AND si.value LIKE N'%Squads: the first team plus the Academy (U15 / U14 / U12).%';
SET @n = @@ROWCOUNT; PRINT CONCAT(N'geo.llms_facts_summary（英）更新 ', @n, N' 筆');

-- ③ 報名表單「報名項目」選項
DECLARE @oldZh nvarchar(1000) = N'["學院 U12", "學院 U14", "學院 U15", "兒童混齡班", "兒童初學班", "兒童技巧發展班", "專項訓練"]';
DECLARE @newZh nvarchar(1000) = N'["學院 U10", "學院 U12", "學院 U14", "學院 U16", "學院 U18", "兒童混齡班", "兒童初學班", "兒童技巧發展班", "專項訓練"]';
DECLARE @oldEn nvarchar(1000) = N'["Academy U12", "Academy U14", "Academy U15", "Children Mixed-Age Class", "Children Beginner Class", "Children Skill Development Class", "Specialist Training"]';
DECLARE @newEn nvarchar(1000) = N'["Academy U10", "Academy U12", "Academy U14", "Academy U16", "Academy U18", "Children Mixed-Age Class", "Children Beginner Class", "Children Skill Development Class", "Specialist Training"]';
UPDATE ff SET options_json = @newZh, updated_at = SYSUTCDATETIME()
FROM form_fields ff JOIN forms f ON f.id = ff.form_id
WHERE f.club_id = @club AND f.form_code = N'academy_children_training' AND ff.field_key = N'enrollment_category' AND ff.options_json = @oldZh;
SET @n = @@ROWCOUNT; PRINT CONCAT(N'form_fields 報名項目選項更新 ', @n, N' 筆');
UPDATE fi SET options_json = @newEn
FROM form_fields_i18n fi JOIN form_fields ff ON ff.id = fi.form_field_id JOIN forms f ON f.id = ff.form_id
WHERE f.club_id = @club AND f.form_code = N'academy_children_training' AND ff.field_key = N'enrollment_category' AND fi.locale = N'en' AND fi.options_json = @oldEn;
SET @n = @@ROWCOUNT; PRINT CONCAT(N'form_fields_i18n（英）報名項目選項更新 ', @n, N' 筆');

-- ④ 聯絡地址（獨立設定鍵）
SET @sid = (SELECT id FROM settings WHERE club_id = @club AND setting_key = N'site.contact_address');
IF @sid IS NULL
BEGIN
  SET @sid = NEWID();
  INSERT INTO settings (id, club_id, setting_key, setting_group) VALUES (@sid, @club, N'site.contact_address', N'site');
END
IF NOT EXISTS (SELECT 1 FROM settings_i18n WHERE setting_id = @sid AND locale = N'zh-Hant')
  INSERT INTO settings_i18n (setting_id, locale, value) VALUES (@sid, N'zh-Hant', N'台中市北屯區昌平路二段敬固巷11弄41號');
IF NOT EXISTS (SELECT 1 FROM settings_i18n WHERE setting_id = @sid AND locale = N'en')
  INSERT INTO settings_i18n (setting_id, locale, value) VALUES (@sid, N'en', N'No. 41, Alley 11, Jinggu Lane, Section 2, Changping Road, Beitun District, Taichung City, Taiwan');
UPDATE settings_i18n SET value = N'台中市北屯區昌平路二段敬固巷11弄41號' WHERE setting_id = @sid AND locale = N'zh-Hant' AND (value IS NULL OR LTRIM(RTRIM(value)) = N'');
UPDATE settings_i18n SET value = N'No. 41, Alley 11, Jinggu Lane, Section 2, Changping Road, Beitun District, Taichung City, Taiwan' WHERE setting_id = @sid AND locale = N'en' AND (value IS NULL OR LTRIM(RTRIM(value)) = N'');

-- 斷言：聯絡地址兩語都有值
IF (SELECT COUNT(*) FROM settings_i18n WHERE setting_id = @sid AND locale IN (N'zh-Hant', N'en') AND LTRIM(RTRIM(ISNULL(value, N''))) <> N'') <> 2
  THROW 50202, N'聯絡地址（site.contact_address）中英文未全部寫入，中止', 1;

COMMIT TRAN;
