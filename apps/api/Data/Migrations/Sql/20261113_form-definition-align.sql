/* ============================================================================
   表單定義對齊前台頁面措辭與必填（資料遷移）
   2026-11-13｜依據：docs/23 W-3（前台七張表單改讀後台表單定義）與主站規劃書 v3.35 4.7 G1
   （後台可改題目文字與選項顯示文字，中／英；前台依設定顯示）。前台改讀定義後，畫面會顯示種子的文字，
   必須先讓種子／資料庫的文字與頁面原本定稿的措辭一致，客戶才不會看到差異。**以頁面為準。**

   ── 做的事（全部冪等；只動「仍等於舊種子值」的列，營運人員在後台改過的內容一律不動）──
     ① 題目文字（form_fields_i18n.label，zh-Hant 與 en 各自判斷）：目前值＝舊種子值才換成頁面措辭。
     ② 選項顯示文字：
          en 列 options_json：目前值（比對 JSON 陣列內容，忽略空白與 \uXXXX 跳脫差異）＝舊種子值才換；
          zh-Hant 列 options_json：原本沒有這個欄位的值（一律 NULL，後台尚無法設定中文選項顯示文字），
          且 form_fields.options_json（送出值）＝預期的送出值時才補。
        🔴 選項**送出值**（form_fields.options_json）一律不動：收件匣既有資料、後端驗證、
           前台 scripts/check-form-definition.mjs 都依賴它。
     ③ 必填（form_fields.is_required）：頁面標必填、定義選填的欄位改為必填（共 16 列，磐石與藍鯨各一半）。
        八張表單的欄位結構在後台是鎖定的（FormCatalog.FieldLockedCodes），營運人員改不了必填，
        所以只要目前是 0 就改成 1。
   ── 沒有做的事 ──
     • 不動選項送出值、欄位代碼、型別、排序、驗證規則、內容摘要來源。
     • 不動 proposal_download、general_contact（頁面與定義本來就一致）。
   ── 反方向（定義必填、頁面選填）：盤點結果為 0 項，沒有需要放寬的欄位。 ──

   套用順序：不改結構，**不需先 migrate 再 deploy**；與新版 API（後台可存中文選項顯示文字）互不相依，任何時間可套用。
   🔴 冪等；每一步只動符合條件的列；事後斷言失敗即 THROW（單一交易，不留半套）。
   🔴 含中文：手動套用請用 sqlcmd 18／go-sqlcmd 加 -f 65001（sqlcmd 13 會把 UTF-8 切壞，E-296／E-330）；
      正式庫走 deploy/db-migrate.sh（已轉 UTF-16）。套用後跑 db/tools/scan-replacement-chars.sql 確認 U+FFFD 為 0。
   ============================================================================ */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF (SELECT COUNT(*) FROM clubs WHERE code IN (N'tcrfc', N'bw')) <> 2
  THROW 51401, N'找不到 tcrfc 或 bw 俱樂部（clubs.code）', 1;
GO

BEGIN TRAN;

-- 變更清單：(俱樂部, 表單, 欄位) × 舊值／新值；NULL＝該項沒有變動。
-- canon_opts＝選項送出值（form_fields.options_json 的預期值，只作比對守衛）。
DECLARE @chg TABLE (
  club_code nvarchar(20) NOT NULL, form_code nvarchar(64) NOT NULL, field_key nvarchar(64) NOT NULL,
  old_zh nvarchar(255) NULL, new_zh nvarchar(255) NULL, old_en nvarchar(255) NULL, new_en nvarchar(255) NULL,
  canon_opts nvarchar(1000) NULL, new_opt_zh nvarchar(1000) NULL, old_opt_en nvarchar(1000) NULL, new_opt_en nvarchar(1000) NULL,
  set_required bit NULL
);
INSERT INTO @chg VALUES
  (N'bw',N'academy_children_training',N'birth_date',N'學員生日',N'學員出生日期',N'Student Date of Birth',N'Student''s date of birth',NULL,NULL,NULL,NULL,NULL),
  (N'bw',N'academy_children_training',N'enrollment_category',N'報名項目',N'想報名的項目',N'Enrollment Category',N'Program you would like to join',N'["學院 U12", "學院 U14", "學院 U15", "兒童混齡班", "兒童初學班", "兒童技巧發展班", "專項訓練"]',N'["U12 青年隊", "U14 青年隊", "U15 青年隊", "混齡班", "初學班", "技巧發展班", "專項訓練"]',N'["Academy U12", "Academy U14", "Academy U15", "Children Mixed-Age Class", "Children Beginner Class", "Children Skill Development Class", "Specialist Training"]',N'["U12 Youth team", "U14 Youth team", "U15 Youth team", "Mixed-age class", "Beginner class", "Skills development class", "Specialist training"]',NULL),
  (N'bw',N'academy_children_training',N'experience',N'足球經歷',N'足球或運動經歷',N'Football Experience',N'Football or sports experience',NULL,NULL,NULL,NULL,NULL),
  (N'bw',N'academy_children_training',N'health_status',N'健康狀況',N'特殊健康狀況說明',N'Health Status',N'Special health conditions',NULL,NULL,NULL,NULL,NULL),
  (N'bw',N'academy_children_training',N'location_preference',N'地點偏好',N'偏好受訓地點',N'Preferred Location',N'Preferred training location',NULL,NULL,NULL,NULL,NULL),
  (N'bw',N'camp_registration',N'birth_date',N'學員生日',N'學員出生日期',N'Student Date of Birth',N'Student''s date of birth',NULL,NULL,NULL,NULL,NULL),
  (N'bw',N'camp_registration',N'name',NULL,NULL,N'Student Name',N'Student''s name',NULL,NULL,NULL,NULL,NULL),
  (N'bw',N'camp_registration',N'session_choice',N'營隊梯次',N'希望報名的梯次',N'Camp Session',N'Preferred session',NULL,NULL,NULL,NULL,NULL),
  (N'bw',N'international_player_enquiry',N'experience',N'足球經歷',N'足球經歷簡述',N'Football Experience',N'Career Summary',NULL,NULL,NULL,NULL,1),
  (N'bw',N'international_player_enquiry',N'name',NULL,NULL,N'Name',N'Full Name',NULL,NULL,NULL,NULL,NULL),
  (N'bw',N'international_player_enquiry',N'video_url',NULL,NULL,N'Video Link',N'Video Highlight Link',NULL,NULL,NULL,NULL,1),
  (N'bw',N'international_player_enquiry',N'visa_status',NULL,NULL,N'Visa Status',N'Current Visa Status',NULL,NULL,NULL,NULL,1),
  (N'bw',N'join_player',N'birth_date',N'生日',N'出生日期',N'Date of Birth',N'Date of birth',NULL,NULL,NULL,NULL,NULL),
  (N'bw',N'join_player',N'experience',N'足球經歷',N'足球經歷簡述',N'Football Experience',N'Football experience',NULL,NULL,NULL,NULL,1),
  (N'bw',N'join_player',N'position',NULL,NULL,N'Playing Position',N'Playing position',NULL,NULL,NULL,NULL,1),
  (N'bw',N'join_player',N'video_url',N'影片連結',N'比賽或訓練影片連結',N'Video Link',N'Match or training video link',NULL,NULL,NULL,NULL,1),
  (N'bw',N'media_enquiry',N'deadline',N'截稿日',N'期望採訪日期／截稿日',N'Deadline',N'Preferred interview date / deadline',NULL,NULL,NULL,NULL,1),
  (N'bw',N'media_enquiry',N'media_name',N'媒體名稱',N'媒體／機構名稱',N'Media Outlet',N'Media outlet / organisation name',NULL,NULL,NULL,NULL,NULL),
  (N'bw',N'media_enquiry',N'name',N'記者姓名',N'記者／聯絡人姓名',N'Reporter Name',N'Journalist / contact name',NULL,NULL,NULL,NULL,NULL),
  (N'bw',N'media_enquiry',N'topic',N'採訪主題',N'採訪主題／需求說明',N'Interview Topic',N'Interview topic / requirements',NULL,NULL,NULL,NULL,NULL),
  (N'bw',N'partnership_sponsorship',N'budget_range',NULL,NULL,N'Budget Range',N'Budget range',NULL,NULL,NULL,NULL,NULL),
  (N'bw',N'partnership_sponsorship',N'company',N'公司名稱',N'公司／機構名稱',N'Company Name',N'Company / organisation name',NULL,NULL,NULL,NULL,NULL),
  (N'bw',N'partnership_sponsorship',N'cooperation_direction',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,1),
  (N'bw',N'partnership_sponsorship',N'enquiry_type',N'洽詢類型',N'這次想洽詢的是',N'Enquiry Type',N'This enquiry is about',N'["合作夥伴", "贊助", "兩者"]',N'["合作夥伴關係", "贊助方案", "兩者都想了解"]',N'["Partnership", "Sponsorship", "Both"]',N'["A partnership", "A sponsorship package", "Both"]',NULL),
  (N'bw',N'partnership_sponsorship',N'name',NULL,NULL,N'Contact Person',N'Contact name',NULL,NULL,NULL,NULL,NULL),
  (N'tcrfc',N'academy_children_training',N'birth_date',N'學員生日',N'學員出生日期',N'Student Date of Birth',N'Student''s date of birth',NULL,NULL,NULL,NULL,NULL),
  (N'tcrfc',N'academy_children_training',N'enrollment_category',N'報名項目',N'想報名的項目',N'Enrollment Category',N'Program you would like to join',N'["學院 U10", "學院 U12", "學院 U14", "學院 U16", "學院 U18", "兒童混齡班", "兒童初學班", "兒童技巧發展班", "專項訓練"]',N'["U10 梯隊", "U12 梯隊", "U14 梯隊", "U16 梯隊", "U18 梯隊", "混齡班", "初學班", "技巧發展班", "專項訓練"]',N'["Academy U10", "Academy U12", "Academy U14", "Academy U16", "Academy U18", "Children Mixed-Age Class", "Children Beginner Class", "Children Skill Development Class", "Specialist Training"]',N'["U10 squad", "U12 squad", "U14 squad", "U16 squad", "U18 squad", "Mixed-age class", "Beginner class", "Skills development class", "Specialist training"]',NULL),
  (N'tcrfc',N'academy_children_training',N'experience',N'足球經歷',N'足球或運動經歷',N'Football Experience',N'Football or sports experience',NULL,NULL,NULL,NULL,NULL),
  (N'tcrfc',N'academy_children_training',N'health_status',N'健康狀況',N'特殊健康狀況說明',N'Health Status',N'Special health conditions',NULL,NULL,NULL,NULL,NULL),
  (N'tcrfc',N'academy_children_training',N'location_preference',N'地點偏好',N'偏好受訓地點',N'Preferred Location',N'Preferred training location',NULL,NULL,NULL,NULL,NULL),
  (N'tcrfc',N'camp_registration',N'birth_date',N'學員生日',N'學員出生日期',N'Student Date of Birth',N'Student''s date of birth',NULL,NULL,NULL,NULL,NULL),
  (N'tcrfc',N'camp_registration',N'name',NULL,NULL,N'Student Name',N'Student''s name',NULL,NULL,NULL,NULL,NULL),
  (N'tcrfc',N'camp_registration',N'session_choice',N'營隊梯次',N'希望報名的梯次',N'Camp Session',N'Preferred session',NULL,NULL,NULL,NULL,NULL),
  (N'tcrfc',N'international_player_enquiry',N'experience',N'足球經歷',N'足球經歷簡述',N'Football Experience',N'Career Summary',NULL,NULL,NULL,NULL,1),
  (N'tcrfc',N'international_player_enquiry',N'name',NULL,NULL,N'Name',N'Full Name',NULL,NULL,NULL,NULL,NULL),
  (N'tcrfc',N'international_player_enquiry',N'video_url',NULL,NULL,N'Video Link',N'Video Highlight Link',NULL,NULL,NULL,NULL,1),
  (N'tcrfc',N'international_player_enquiry',N'visa_status',NULL,NULL,N'Visa Status',N'Current Visa Status',NULL,NULL,NULL,NULL,1),
  (N'tcrfc',N'join_player',N'birth_date',N'生日',N'出生日期',N'Date of Birth',N'Date of birth',NULL,NULL,NULL,NULL,NULL),
  (N'tcrfc',N'join_player',N'experience',N'足球經歷',N'足球經歷簡述',N'Football Experience',N'Football experience',NULL,NULL,NULL,NULL,1),
  (N'tcrfc',N'join_player',N'position',NULL,NULL,N'Playing Position',N'Playing position',NULL,NULL,NULL,NULL,1),
  (N'tcrfc',N'join_player',N'video_url',N'影片連結',N'比賽或訓練影片連結',N'Video Link',N'Match or training video link',NULL,NULL,NULL,NULL,1),
  (N'tcrfc',N'media_enquiry',N'deadline',N'截稿日',N'期望採訪日期／截稿日',N'Deadline',N'Preferred interview date / deadline',NULL,NULL,NULL,NULL,1),
  (N'tcrfc',N'media_enquiry',N'media_name',N'媒體名稱',N'媒體／機構名稱',N'Media Outlet',N'Media outlet / organisation name',NULL,NULL,NULL,NULL,NULL),
  (N'tcrfc',N'media_enquiry',N'name',N'記者姓名',N'記者／聯絡人姓名',N'Reporter Name',N'Journalist / contact name',NULL,NULL,NULL,NULL,NULL),
  (N'tcrfc',N'media_enquiry',N'topic',N'採訪主題',N'採訪主題／需求說明',N'Interview Topic',N'Interview topic / requirements',NULL,NULL,NULL,NULL,NULL),
  (N'tcrfc',N'partnership_sponsorship',N'budget_range',NULL,NULL,N'Budget Range',N'Budget range',NULL,NULL,NULL,NULL,NULL),
  (N'tcrfc',N'partnership_sponsorship',N'company',N'公司名稱',N'公司／機構名稱',N'Company Name',N'Company / organisation name',NULL,NULL,NULL,NULL,NULL),
  (N'tcrfc',N'partnership_sponsorship',N'cooperation_direction',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,1),
  (N'tcrfc',N'partnership_sponsorship',N'enquiry_type',N'洽詢類型',N'這次想洽詢的是',N'Enquiry Type',N'This enquiry is about',N'["合作夥伴", "贊助", "兩者"]',N'["合作夥伴關係", "贊助方案", "兩者都想了解"]',N'["Partnership", "Sponsorship", "Both"]',N'["A partnership", "A sponsorship package", "Both"]',NULL),
  (N'tcrfc',N'partnership_sponsorship',N'name',NULL,NULL,N'Contact Person',N'Contact name',NULL,NULL,NULL,NULL,NULL);

DECLARE @n int;

-- ① 題目文字（中）
UPDATE fi SET label = c.new_zh
FROM form_fields_i18n fi
JOIN form_fields ff ON ff.id = fi.form_field_id
JOIN forms f ON f.id = ff.form_id
JOIN clubs cl ON cl.id = f.club_id
JOIN @chg c ON c.club_code = cl.code AND c.form_code = f.form_code AND c.field_key = ff.field_key
WHERE fi.locale = N'zh-Hant' AND c.new_zh IS NOT NULL
  AND fi.label COLLATE Latin1_General_100_BIN2 = c.old_zh COLLATE Latin1_General_100_BIN2;
SET @n = @@ROWCOUNT; PRINT CONCAT(N'form_fields_i18n 題目文字（中）更新 ', @n, N' 筆');

-- ① 題目文字（英）
UPDATE fi SET label = c.new_en
FROM form_fields_i18n fi
JOIN form_fields ff ON ff.id = fi.form_field_id
JOIN forms f ON f.id = ff.form_id
JOIN clubs cl ON cl.id = f.club_id
JOIN @chg c ON c.club_code = cl.code AND c.form_code = f.form_code AND c.field_key = ff.field_key
WHERE fi.locale = N'en' AND c.new_en IS NOT NULL
  AND fi.label COLLATE Latin1_General_100_BIN2 = c.old_en COLLATE Latin1_General_100_BIN2;
SET @n = @@ROWCOUNT; PRINT CONCAT(N'form_fields_i18n 題目文字（英）更新 ', @n, N' 筆');

-- ② 選項英文顯示文字（目前值＝舊種子值）。比對的是 JSON 陣列的**內容**（逐項、依序），不是字串：後台存檔會把 JSON
--    壓成緊湊格式、並把非 ASCII 轉成 \uXXXX 跳脫，字串比對會誤判成「被改過」。
UPDATE fi SET options_json = c.new_opt_en
FROM form_fields_i18n fi
JOIN form_fields ff ON ff.id = fi.form_field_id
JOIN forms f ON f.id = ff.form_id
JOIN clubs cl ON cl.id = f.club_id
JOIN @chg c ON c.club_code = cl.code AND c.form_code = f.form_code AND c.field_key = ff.field_key
WHERE fi.locale = N'en' AND c.new_opt_en IS NOT NULL
  AND (CASE WHEN ISJSON(fi.options_json) = 1 THEN (SELECT STRING_AGG(CAST(j.[value] AS nvarchar(max)), NCHAR(31)) WITHIN GROUP (ORDER BY CAST(j.[key] AS int)) FROM OPENJSON(fi.options_json) j) END) COLLATE Latin1_General_100_BIN2
      = (CASE WHEN ISJSON(c.old_opt_en) = 1 THEN (SELECT STRING_AGG(CAST(j.[value] AS nvarchar(max)), NCHAR(31)) WITHIN GROUP (ORDER BY CAST(j.[key] AS int)) FROM OPENJSON(c.old_opt_en) j) END) COLLATE Latin1_General_100_BIN2;
SET @n = @@ROWCOUNT; PRINT CONCAT(N'form_fields_i18n 選項顯示文字（英）更新 ', @n, N' 筆');

-- ② 選項中文顯示文字（zh-Hant 列；原本一律 NULL；送出值必須是預期值才補，避免顯示文字與送出值錯位）
UPDATE fi SET options_json = c.new_opt_zh
FROM form_fields_i18n fi
JOIN form_fields ff ON ff.id = fi.form_field_id
JOIN forms f ON f.id = ff.form_id
JOIN clubs cl ON cl.id = f.club_id
JOIN @chg c ON c.club_code = cl.code AND c.form_code = f.form_code AND c.field_key = ff.field_key
WHERE fi.locale = N'zh-Hant' AND c.new_opt_zh IS NOT NULL AND fi.options_json IS NULL
  AND (CASE WHEN ISJSON(ff.options_json) = 1 THEN (SELECT STRING_AGG(CAST(j.[value] AS nvarchar(max)), NCHAR(31)) WITHIN GROUP (ORDER BY CAST(j.[key] AS int)) FROM OPENJSON(ff.options_json) j) END) COLLATE Latin1_General_100_BIN2
      = (CASE WHEN ISJSON(c.canon_opts) = 1 THEN (SELECT STRING_AGG(CAST(j.[value] AS nvarchar(max)), NCHAR(31)) WITHIN GROUP (ORDER BY CAST(j.[key] AS int)) FROM OPENJSON(c.canon_opts) j) END) COLLATE Latin1_General_100_BIN2;
SET @n = @@ROWCOUNT; PRINT CONCAT(N'form_fields_i18n 選項顯示文字（中）補入 ', @n, N' 筆');

-- ③ 必填
UPDATE ff SET is_required = 1, updated_at = SYSUTCDATETIME()
FROM form_fields ff
JOIN forms f ON f.id = ff.form_id
JOIN clubs cl ON cl.id = f.club_id
JOIN @chg c ON c.club_code = cl.code AND c.form_code = f.form_code AND c.field_key = ff.field_key
WHERE c.set_required = 1 AND ff.is_required = 0;
SET @n = @@ROWCOUNT; PRINT CONCAT(N'form_fields 必填更新 ', @n, N' 筆');

-- 斷言：凡是有選項顯示文字的列，筆數必須等於送出值筆數（中、英皆然），否則前台會錯位
IF EXISTS (
  SELECT 1
  FROM form_fields_i18n fi
  JOIN form_fields ff ON ff.id = fi.form_field_id
  WHERE fi.options_json IS NOT NULL AND ff.options_json IS NOT NULL
    AND (SELECT COUNT(*) FROM OPENJSON(fi.options_json)) <> (SELECT COUNT(*) FROM OPENJSON(ff.options_json))
)
  THROW 51402, N'有選項顯示文字的筆數與送出值不一致，中止', 1;

COMMIT TRAN;
GO
