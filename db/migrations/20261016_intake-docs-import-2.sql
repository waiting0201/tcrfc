/* ============================================================================
   客戶 Google 文件內容匯入 第二批（資料遷移，C-1／B-3）
   2026-10-16｜來源：客戶 Google Drive 收件夾（逐字原文存於 content/tcrfc/_raw/gdocs/，盤點見該目錄 _inventory.md）。
   使用者 2026-10-16 裁決：全部照客戶原文上網，只做格式處理；英文為開發端翻譯（用詞照 docs/06-conventions.md §1.1，
   沒有官方英文名的人名、機構不自行音譯，保留中文）。全部是台中磐石（tcrfc）的資料，不動藍鯨。
   接續 20261016_intake-docs-import.sql（受贈公益團體 charities 沿用該檔所建，須先套用）。

   ── 本檔匯入的範圍（預期筆數，結尾逐項斷言，不符即 THROW 回滾）──
     ① 設定（settings／settings_i18n）：
          policy.privacy 隱私權政策 1 則 × 中英（10 隱私權政策）｜site.contact_phone 聯絡電話（10 聯絡資訊）｜
          site.social_facebook／instagram／youtube 社群連結 3 個（00 Link）。
     ② 合作學校 2 筆 → partners（類型「教育夥伴」，2 筆 × 中英）。
     ③ 慈善事蹟 3 筆 → impact_records（3 筆 × 中英；受贈單位用上一批的 charities 3 筆；所屬計畫掛上一批的 charity_programs）。
     ④ 活動回顧 2 篇 → articles（5.1 台中磐石足球節、5.4 成人足球班；草稿、分類 camps-events「營隊與活動」、不設發布日，2 篇 × 中英）。
     ⑤ 官方商品 2 件 → products（草稿；2 件 × 中英）＋ product_variants 1 個（球衣）。
   ── 不匯入（本檔未處理，原因見交付回報）──
     4.4 訓練課程與課綱（規格沒有對應的內容容器）｜聯絡地址（前台讀主場場地地址、沒有獨立設定鍵，且既有測試鎖定種子地址，需先決定）｜2.3 核心價值／4.3 發展路徑／8.2 球迷會／5.2 NAMA 營隊（規格異動中，由另一個工作處理）。

   ── 逐字修正的錯字（其餘一律不動）──
     神棲市→神栖市（11.3 第一筆；本檔寫成「日本茨城縣神栖市」）。
   ── 格式處理（不改意）──
     去 Markdown 記號（粗體、標題井字、表格、項目符號）；去工具殘留的引用標記「nics.nat+1」（隱私權政策 2 處）；
     去 Facebook 連結的追蹤參數（__cft__、__tn__，連結本身因新聞內文不支援超連結而不放）；
     去內部查證欄：11.3 的「建議佐證」「公開依據」、5.5 的「可驗證的合作內容／判斷」（含「判斷」欄）、10 聯絡資訊電話後的「公開球會資料所列，建議撥打前再向俱樂部核實」；
     表格欄位轉成「欄位名：內容」的文字行；文件標題不重複放進內文（隱私權政策標題、活動回顧標題改放標題欄）。
   ── 原樣保留 ──
     隱私權政策權利行使的 Email／電話／地址「＿＿＿＿＿＿＿＿」空白；未核實的電話（+886 4 2221 3108）；
     球衣「NT$1,200.00 促銷價格」（原價不明，price 放 1200、sale_price 留空）。

   ── 欄位對應的判斷 ──
     • 慈善事蹟：事蹟紀錄沒有標題欄，前台顯示「公益團體名稱＋捐助內容＋地點＋說明」。受益單位＝受贈公益團體（charities）；
       支持內容／捐贈內容／主要內容 → donation_content；活動地點 → location；其餘欄位（事蹟名稱、日期區間、受益對象、公益類型、主辦…）
       逐行寫入 brief_description（單一日期與 happened_on 重複，不再寫入）。活動圖片 image_key 為 NULL（資料層可空，後台新增時才必填），客戶未提供，不捏造。
     • 商品：球衣只有一個規格（SKU 為開發端編號、無尺寸、庫存 0）；機能襪原文無價格，price 為 NOT NULL，故不建規格。商品與規格皆不上架。
     • 聯絡電話：種子的測試值 04-0000-0000 視同空值。

   ── 只補空值、不覆蓋後台已編輯的內容 ──
     設定：政策內文、電話、社群連結只在列不存在或值為空（電話含種子測試值）時寫入；其餘各表以 slug／（公益團體＋日期）判斷，已存在就整筆不動。
   • 🔴 冪等，可重複執行；單一交易，任何斷言失敗整批回滾。

   執行（本機）：docker cp 進容器後 sqlcmd -S localhost -U sa -C -d tcrfc_club -b -I -f 65001 -i <檔案>
   ============================================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @club uniqueidentifier = (SELECT id FROM clubs WHERE code = N'tcrfc');
IF @club IS NULL THROW 50101, N'找不到 tcrfc 俱樂部（clubs.code = tcrfc）', 1;
DECLARE @zh nvarchar(10) = N'zh-Hant', @en nvarchar(10) = N'en';
DECLARE @sid uniqueidentifier, @n int;


/* ───────── ① 設定：隱私權政策、聯絡電話、社群連結 ───────── */

DECLARE @privZh nvarchar(max) = N'最後更新日期：2026年8月1日

台中磐石足球俱樂部（以下稱「本俱樂部」）重視您的隱私權與個人資料保護。本隱私權政策說明您使用本俱樂部官方網站、相關網頁及線上服務時，本俱樂部如何蒐集、處理、利用及保護您的個人資料。

本俱樂部將依中華民國《個人資料保護法》及相關法令處理個人資料。個資法要求蒐集資料前告知蒐集者、目的、資料類別、利用期間／地區／對象／方式、當事人權利與不提供資料的影響。

資料蒐集與使用

當您瀏覽本網站時，本網站可能基於網站運作、安全維護、流量統計及服務優化，自動記錄下列資訊：

您的 IP 位址、瀏覽器類型、裝置資訊、作業系統及瀏覽時間。
您瀏覽的頁面、點擊紀錄、來源網站及停留時間。
Cookies 或其他類似技術所產生的網站使用紀錄。
您主動透過聯絡表單、報名頁面、電子郵件或其他方式提供的姓名、電話、電子郵件及訊息內容。

本俱樂部將在必要範圍內使用前述資料，以提供網站服務、回覆您的詢問、改善網站內容、維護資訊安全、分析使用情形及辦理俱樂部相關活動通知。

若您使用本網站的報名、試訓、課程、會員、購票、商品購買或其他需填寫個人資料之服務，本俱樂部將於該表單頁面另行提供個資蒐集、處理及利用告知事項與同意欄位。

第三方服務與連結

本網站可能使用或連結至第三方所提供的服務、網站、社群平台或工具，包括但不限於 Facebook、Instagram、YouTube、LINE、Google Maps、Google Analytics、Google Forms、活動報名平台、金流服務及其他合作服務。

當您點擊本網站連結、使用嵌入內容，或透過第三方平台與本俱樂部互動時，您可能離開本網站，或由第三方直接蒐集您的資料。此等第三方網站、應用程式或服務並非由本俱樂部控制，其個人資料蒐集、Cookies、帳號登入、付款及其他處理行為，應依該第三方的隱私權政策及使用條款辦理。

本俱樂部建議您在使用任何第三方服務前，先閱讀該第三方之隱私權政策。本俱樂部不對第三方網站、平台、服務、內容或其資料保護措施負責。台灣網站常見的隱私條款亦會明確表示：經由本站連結進入的第三方獨立網站，適用其自身隱私與安全政策。

Cookies 政策

本網站可能使用 Cookies 及類似技術，以維持基本網站功能、記錄使用偏好、進行流量統計、分析網站效能及改善使用者體驗。

部分 Cookies 可能由本俱樂部或第三方服務供應商設定，例如網站分析、嵌入式影音、地圖、社群媒體或廣告服務。您可透過瀏覽器設定管理、拒絕或刪除 Cookies；惟拒絕 Cookies 後，網站部分功能可能無法正常運作。第三方分析服務使用 Cookies 的情況，通常亦應依其各自的隱私權政策處理。

個資提供與第三方揭露

除法律規定、政府機關依法要求、保障您或他人生命身體安全、履行契約或提供您所申請服務之必要情形外，本俱樂部原則上不會出售、交換或出租您的個人資料予第三方。

如因網站維護、雲端系統、金流、活動報名、行銷工具、物流、保險、賽事或其他服務需要，而委託第三方處理您的個人資料時，本俱樂部將於必要範圍內提供資料，並要求受託廠商採取適當的資料安全措施。

個人資料權利

依《個人資料保護法》第 3 條，您可就本俱樂部所保有的個人資料行使下列權利：

查詢或請求閱覽。
請求提供複製本。
請求補充或更正。
請求停止蒐集、處理或利用。
請求刪除。

如欲行使上述權利，請透過以下方式與本俱樂部聯繫：

電子郵件：＿＿＿＿＿＿＿＿
聯絡電話：＿＿＿＿＿＿＿＿
聯絡地址：＿＿＿＿＿＿＿＿

本俱樂部得為確認您的身分及保障資料安全，要求您提供必要證明文件。

政策修訂

本俱樂部得因法令變動、網站服務調整或營運需求修訂本隱私權政策。修訂後內容將公告於本網站，並自公告日起生效。';
DECLARE @privEn nvarchar(max) = N'Last updated: August 1, 2026

Taichung Rock FC (hereinafter "the Club") values your privacy and the protection of your personal data. This Privacy Policy explains how the Club collects, processes, uses and protects your personal data when you use the Club''s official website, related web pages and online services.

The Club will process personal data in accordance with the Personal Data Protection Act of the Republic of China (Taiwan) and related laws and regulations. The Personal Data Protection Act requires that, before collecting data, the collector inform the data subject of the collector''s identity, the purpose, the categories of data, the period, area, recipients and manner of use, the data subject''s rights, and the effect of not providing the data.

Collection and Use of Data

When you browse this website, it may automatically record the following information for the purposes of website operation, security maintenance, traffic statistics and service improvement:

Your IP address, browser type, device information, operating system and browsing time.
The pages you view, your click records, referring websites and time spent.
Website usage records generated by cookies or other similar technologies.
Your name, phone number, email address and message content that you provide voluntarily through the contact form, registration pages, email or other means.

The Club will use the above data within the necessary scope to provide website services, respond to your enquiries, improve website content, maintain information security, analyse usage and handle notifications about Club-related activities.

If you use this website''s registration, trial, course, membership, ticketing, merchandise purchase or other services that require you to provide personal data, the Club will separately provide a notice on the collection, processing and use of personal data, together with a consent field, on the relevant form page.

Third-Party Services and Links

This website may use or link to services, websites, social platforms or tools provided by third parties, including but not limited to Facebook, Instagram, YouTube, LINE, Google Maps, Google Analytics, Google Forms, event registration platforms, payment services and other partner services.

When you click links on this website, use embedded content, or interact with the Club through a third-party platform, you may leave this website, or the third party may collect your data directly. Such third-party websites, applications or services are not controlled by the Club. Their collection of personal data, cookies, account login, payment and other processing activities are governed by that third party''s privacy policy and terms of use.

The Club recommends that you read a third party''s privacy policy before using any third-party service. The Club is not responsible for third-party websites, platforms, services, content or their data protection measures. Privacy terms commonly seen on Taiwanese websites also state clearly that independent third-party websites reached through links on the site are subject to their own privacy and security policies.

Cookies Policy

This website may use cookies and similar technologies to maintain basic website functions, record usage preferences, compile traffic statistics, analyse website performance and improve the user experience.

Some cookies may be set by the Club or by third-party service providers, for example for website analytics, embedded video, maps, social media or advertising services. You can manage, refuse or delete cookies through your browser settings; however, if you refuse cookies, some website functions may not work properly. The use of cookies by third-party analytics services is usually also governed by their own privacy policies.

Provision of Personal Data and Disclosure to Third Parties

Except where required by law, requested by a government agency in accordance with the law, necessary to protect the life, body or safety of you or others, or necessary to perform a contract or provide the service you have applied for, the Club will in principle not sell, exchange or rent your personal data to third parties.

Where the Club entrusts a third party to process your personal data for website maintenance, cloud systems, payment, event registration, marketing tools, logistics, insurance, competitions or other services, the Club will provide the data within the necessary scope and require the contractor to adopt appropriate data security measures.

Your Rights Regarding Personal Data

Under Article 3 of the Personal Data Protection Act, you may exercise the following rights over the personal data held by the Club:

Inquire about or request to review your data.
Request a copy.
Request supplementation or correction.
Request that collection, processing or use be stopped.
Request deletion.

To exercise these rights, please contact the Club through the following:

Email: ＿＿＿＿＿＿＿＿
Phone: ＿＿＿＿＿＿＿＿
Address: ＿＿＿＿＿＿＿＿

To verify your identity and protect data security, the Club may require you to provide the necessary supporting documents.

Policy Amendments

The Club may amend this Privacy Policy because of changes in law, adjustments to website services or operational needs. Amended content will be announced on this website and takes effect from the date of announcement.';
SET @sid = (SELECT id FROM settings WHERE club_id = @club AND setting_key = N'policy.privacy');
IF @sid IS NULL
BEGIN
  SET @sid = NEWID();
  INSERT INTO settings (id, club_id, setting_key, setting_group) VALUES (@sid, @club, N'policy.privacy', N'policy');
END
IF NOT EXISTS (SELECT 1 FROM settings_i18n WHERE setting_id = @sid AND locale = @zh) INSERT INTO settings_i18n (setting_id, locale, value) VALUES (@sid, @zh, @privZh);
IF NOT EXISTS (SELECT 1 FROM settings_i18n WHERE setting_id = @sid AND locale = @en) INSERT INTO settings_i18n (setting_id, locale, value) VALUES (@sid, @en, @privEn);
-- 只補空值：後台已編輯過的政策內文不覆蓋
UPDATE settings_i18n SET value = @privZh WHERE setting_id = @sid AND locale = @zh AND (value IS NULL OR LTRIM(RTRIM(value)) = N'');
UPDATE settings_i18n SET value = @privEn WHERE setting_id = @sid AND locale = @en AND (value IS NULL OR LTRIM(RTRIM(value)) = N'');

-- site.contact_phone：種子的測試值 04-0000-0000 視同空值（種子註記為「一看就知道是測試值」，正式資料上線前要換）
SET @sid = (SELECT id FROM settings WHERE club_id = @club AND setting_key = N'site.contact_phone');
IF @sid IS NULL INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group) VALUES (NEWID(), @club, N'site.contact_phone', N'+886 4 2221 3108', N'site');
ELSE UPDATE settings SET setting_value = N'+886 4 2221 3108', updated_at = SYSUTCDATETIME() WHERE id = @sid AND (setting_value IS NULL OR LTRIM(RTRIM(setting_value)) = N'' OR setting_value = N'04-0000-0000');

DECLARE @soc TABLE (k nvarchar(128) NOT NULL, v nvarchar(500) NOT NULL);
INSERT INTO @soc (k, v) VALUES
  (N'site.social_facebook', N'https://www.facebook.com/TCRFC2024'),
  (N'site.social_instagram', N'https://www.instagram.com/tcr_fc_2024/'),
  (N'site.social_youtube', N'https://www.youtube.com/@TCRFC-2024');
INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
  SELECT NEWID(), @club, s.k, s.v, N'site' FROM @soc s WHERE NOT EXISTS (SELECT 1 FROM settings x WHERE x.club_id = @club AND x.setting_key = s.k);
UPDATE x SET setting_value = s.v, updated_at = SYSUTCDATETIME() FROM settings x JOIN @soc s ON s.k = x.setting_key
  WHERE x.club_id = @club AND (x.setting_value IS NULL OR LTRIM(RTRIM(x.setting_value)) = N'');


IF (SELECT COUNT(*) FROM settings_i18n i JOIN settings s ON s.id = i.setting_id WHERE s.club_id = @club AND s.setting_key = N'policy.privacy' AND NULLIF(LTRIM(RTRIM(i.value)), N'') IS NOT NULL) <> 2 THROW 50110, N'隱私權政策雙語列不是 2', 1;
IF NOT EXISTS (SELECT 1 FROM settings WHERE club_id = @club AND setting_key = N'site.contact_phone' AND NULLIF(setting_value, N'') IS NOT NULL AND setting_value <> N'04-0000-0000') THROW 50111, N'聯絡電話仍是空值或測試值', 1;
IF (SELECT COUNT(*) FROM @soc s JOIN settings x ON x.club_id = @club AND x.setting_key = s.k AND NULLIF(x.setting_value, N'') IS NOT NULL) <> 3 THROW 50112, N'社群連結不是 3 個', 1;
PRINT N'settings 完成（隱私權政策 2 語系、電話、社群 3 個）';


/* ───────── ② 合作學校 2 筆 → partners（教育夥伴） ───────── */
DECLARE @pt TABLE (slug nvarchar(160) NOT NULL, sort_order int NOT NULL, zh_name nvarchar(128) NOT NULL, en_name nvarchar(128) NOT NULL, zh_content nvarchar(max) NOT NULL, en_content nvarchar(max) NOT NULL);
INSERT INTO @pt (slug, sort_order, zh_name, en_name, zh_content, en_content) VALUES
  (N'tantzu-junior-high-school', 100, N'台中市立潭秀國民中學', N'台中市立潭秀國民中學', N'台中磐石的 U14、U16 梯隊與潭秀國中合作，作為俱樂部青訓銜接的一部分。', N'Taichung Rock FC''s U14 and U16 squads cooperate with 潭秀國中 as part of the club''s youth development pathway.'),
  (N'lingtung-senior-high-school', 110, N'台中市私立嶺東高級中學', N'台中市私立嶺東高級中學', N'2024 年與潭秀國中共同組成聯隊，以台中磐石名義赴日本參加 FELICE CUP U16；磐石全額支援該次參賽費用。', N'In 2024 the school formed a joint squad with 潭秀國中, which travelled to Japan in the name of Taichung Rock FC to compete in the FELICE CUP U16; Taichung Rock FC fully covered the costs of the trip.');
INSERT INTO partners (id, club_id, slug, partner_type, sort_order)
  SELECT NEWID(), @club, p.slug, N'教育夥伴', p.sort_order FROM @pt p WHERE NOT EXISTS (SELECT 1 FROM partners x WHERE x.club_id = @club AND x.slug = p.slug);
INSERT INTO partners_i18n (partner_id, locale, name, content)
  SELECT x.id, @zh, p.zh_name, p.zh_content FROM @pt p JOIN partners x ON x.club_id = @club AND x.slug = p.slug
  WHERE NOT EXISTS (SELECT 1 FROM partners_i18n i WHERE i.partner_id = x.id AND i.locale = @zh);
INSERT INTO partners_i18n (partner_id, locale, name, content)
  SELECT x.id, @en, p.en_name, p.en_content FROM @pt p JOIN partners x ON x.club_id = @club AND x.slug = p.slug
  WHERE NOT EXISTS (SELECT 1 FROM partners_i18n i WHERE i.partner_id = x.id AND i.locale = @en);
IF (SELECT COUNT(*) FROM @pt p JOIN partners x ON x.club_id = @club AND x.slug = p.slug JOIN partners_i18n i ON i.partner_id = x.id) <> 4 THROW 50120, N'合作學校（2 筆 × 雙語）未全部就位', 1;
PRINT N'partners 完成（2 筆）';


/* ───────── ③ 慈善事蹟 3 筆 → impact_records（受贈單位沿用上一批建立的 charities） ───────── */
DECLARE @ir TABLE (happened_on date NOT NULL, charity_slug nvarchar(160) NOT NULL, program_slug nvarchar(160) NOT NULL, zh_don nvarchar(max), en_don nvarchar(max), zh_loc nvarchar(128), en_loc nvarchar(128), zh_brief nvarchar(max), en_brief nvarchar(max));
INSERT INTO @ir (happened_on, charity_slug, program_slug, zh_don, en_don, zh_loc, en_loc, zh_brief, en_brief) VALUES
  (N'2024-07-23', N'tantzu-lingtung-joint-squad', N'youth-football-international-exchange', N'日本參賽費用由台中磐石足球俱樂部全額提供', N'The costs of the trip to Japan were fully covered by Taichung Rock FC', N'日本茨城縣神栖市', N'Kamisu City, Ibaraki Prefecture, Japan', N'事蹟名稱：全額支持潭秀國中、嶺東高中聯隊赴日參加 Felice CUP U16
日期：2024-07-23 至 2024-07-28
受益對象：18 名球員、2 名教練
公益類型：青少年運動培育、國際交流、教育支持
活動內容：前兩日接受 J 聯盟教練訓練，後續參與第七屆 Felice CUP U16 賽事', N'Record title: Fully supporting the 潭秀國中 and 嶺東高中 joint squad on their trip to Japan for the Felice CUP U16
Dates: 2024-07-23 to 2024-07-28
Beneficiaries: 18 players and 2 coaches
Type of charity: Youth sports development, international exchange, educational support
Activities: Training with J.League coaches on the first two days, followed by the 7th Felice CUP U16 tournament'),
  (N'2025-05-03', N'tcrfc-cup-youth-teams', N'taichung-rock-fc-cup-grassroots', N'分齡足球賽事、磐石球員表演與交流、親子互動活動', N'Age-group football matches, performances and interaction with Taichung Rock FC players, and parent-child activities', NULL, NULL, N'事蹟名稱：2025 台中磐石盃足球邀請賽
受益對象：U6、U8、U10、U12 少年足球隊伍及參與家庭
公益／社區類型：基層足球推廣、兒少運動參與、親子社區活動
主辦單位：台中磐石足球俱樂部
指導單位：台中市政府運動局
承辦／協辦：亞瑟運動俱樂部／台中惠文高中
參與規模：超過 45 支少年足球隊', N'Record title: 2025 Taichung Rock FC Cup Invitational
Beneficiaries: U6, U8, U10 and U12 youth football teams and participating families
Type (charity / community): Grassroots football promotion, youth sports participation, parent-child community activities
Organiser: Taichung Rock FC
Supervising body: Taichung City Government Sports Bureau
Host / co-host: 亞瑟運動俱樂部 / 台中惠文高中
Scale: More than 45 youth football teams'),
  (N'2026-01-12', N'tantzu-nonprofit-kindergarten', N'child-education-resources', N'英語幼童學習書籍《Cabbage & Tyler》', N'English learning books for young children, 《Cabbage & Tyler》', N'潭秀國中所屬潭秀非營利幼兒園', N'潭秀非營利幼兒園, affiliated with 潭秀國中', N'事蹟名稱：台中磐石攜手 Subkarma 捐贈英語幼童學習書籍
合作夥伴：Subkarma
公益類型：幼兒教育、閱讀推廣、英語學習、社區關懷
行動目的：支持孩子的學習與成長，將公益資源帶進在地校園', N'Record title: Taichung Rock FC and Subkarma donate English learning books for young children
Partner: Subkarma
Type of charity: Early childhood education, reading promotion, English learning, community care
Purpose: To support children''s learning and growth and bring charitable resources into local schools');
IF (SELECT COUNT(*) FROM @ir r JOIN charities ch ON ch.club_id = @club AND ch.slug = r.charity_slug) <> 3 THROW 50130, N'找不到上一批建立的受贈公益團體（charities 3 筆，請先套用 20261016_intake-docs-import.sql）', 1;
INSERT INTO impact_records (id, club_id, charity_program_id, charity_id, happened_on)
  SELECT NEWID(), @club, cp.id, ch.id, r.happened_on
  FROM @ir r JOIN charities ch ON ch.club_id = @club AND ch.slug = r.charity_slug
  LEFT JOIN charity_programs cp ON cp.club_id = @club AND cp.slug = r.program_slug
  WHERE NOT EXISTS (SELECT 1 FROM impact_records x WHERE x.club_id = @club AND x.charity_id = ch.id AND x.happened_on = r.happened_on);
INSERT INTO impact_records_i18n (impact_record_id, locale, donation_content, location, brief_description)
  SELECT x.id, @zh, r.zh_don, r.zh_loc, r.zh_brief
  FROM @ir r JOIN charities ch ON ch.club_id = @club AND ch.slug = r.charity_slug
  JOIN impact_records x ON x.club_id = @club AND x.charity_id = ch.id AND x.happened_on = r.happened_on
  WHERE NOT EXISTS (SELECT 1 FROM impact_records_i18n i WHERE i.impact_record_id = x.id AND i.locale = @zh);
INSERT INTO impact_records_i18n (impact_record_id, locale, donation_content, location, brief_description)
  SELECT x.id, @en, r.en_don, r.en_loc, r.en_brief
  FROM @ir r JOIN charities ch ON ch.club_id = @club AND ch.slug = r.charity_slug
  JOIN impact_records x ON x.club_id = @club AND x.charity_id = ch.id AND x.happened_on = r.happened_on
  WHERE NOT EXISTS (SELECT 1 FROM impact_records_i18n i WHERE i.impact_record_id = x.id AND i.locale = @en);
IF (SELECT COUNT(*) FROM @ir r JOIN charities ch ON ch.club_id = @club AND ch.slug = r.charity_slug
      JOIN impact_records x ON x.club_id = @club AND x.charity_id = ch.id AND x.happened_on = r.happened_on
      JOIN impact_records_i18n i ON i.impact_record_id = x.id) <> 6 THROW 50131, N'慈善事蹟（3 筆 × 雙語）未全部就位', 1;
-- 活動圖片：資料層可空（image_key NULL），客戶未提供；未捏造任何圖片
PRINT N'impact_records 完成（3 筆，無活動圖片）';


/* ───────── ④ 活動回顧 2 篇 → 新聞文章（草稿，分類 camps-events） ───────── */
DECLARE @a TABLE (slug nvarchar(160) NOT NULL, zh_title nvarchar(200) NOT NULL, en_title nvarchar(200) NOT NULL, zh_body nvarchar(max) NOT NULL, en_body nvarchar(max) NOT NULL);
INSERT INTO @a (slug, zh_title, en_title, zh_body, en_body) VALUES
  (N'taichung-rock-football-festival-recap', N'台中磐石足球節圓滿落幕', N'Taichung Rock FC Football Festival Concludes Successfully', N'{"blocks": [{"type": "paragraph", "text": "首屆「台中磐石足球節」在5月24日於太原足球場順利舉行，感謝10支來自中部各地的球隊齊聚一堂，共同譜寫這段美好的足球記憶！"}, {"type": "paragraph", "text": "本屆參賽球隊： 台中磐石、仁美國小、協和國小、立人國小、賴厝國小A、賴厝國小B 、永靖國小A、永靖國小B、鐵山羊、埔里山城U12"}, {"type": "paragraph", "text": "10支球隊、5塊場地、9輪單循環交流賽，小球員們在草皮上全力拚搏，展現了屬於這個年紀最純粹的足球熱情"}, {"type": "paragraph", "text": "活動不設排名、只論交流，場上笑聲與加油聲不斷，正是我們舉辦足球節的初衷，以球會友，讓足球在社區生根。"}, {"type": "paragraph", "text": "感謝所有參賽球員、教練、家長，以及每一位在背後默默付出的工作人員。"}, {"type": "paragraph", "text": "同時特別感謝本次活動贊助商 恆山建設 Hessian Development 的大力支持，讓這場足球節得以順利實現！有您的支持，台中的足球夢才能走得更遠。"}]}', N'{"blocks": [{"type": "paragraph", "text": "The first Taichung Rock FC Football Festival was held smoothly at Taiyuan Football Field on May 24. Thank you to the 10 teams from across central Taiwan for gathering together to write this wonderful football memory!"}, {"type": "paragraph", "text": "Participating teams this year: Taichung Rock FC, 仁美國小, 協和國小, 立人國小, 賴厝國小A, 賴厝國小B, 永靖國小A, 永靖國小B, 鐵山羊, 埔里山城U12"}, {"type": "paragraph", "text": "10 teams, 5 pitches and 9 rounds of single round-robin exchange matches: the young players gave their all on the grass and showed the purest passion for football at their age."}, {"type": "paragraph", "text": "There were no rankings, only exchange. The laughter and cheering on the pitch are exactly why we hold the Football Festival: making friends through football and letting football take root in the community."}, {"type": "paragraph", "text": "Thank you to all the players, coaches and parents, and to every staff member who worked quietly behind the scenes."}, {"type": "paragraph", "text": "Special thanks also go to the event sponsor 恆山建設 Hessian Development for its generous support, which made this Football Festival possible. With your support, Taichung''s football dream can go further."}]}'),
  (N'adult-football-class-recap', N'台中磐石成人足球班', N'Taichung Rock FC Adult Football Class', N'{"blocks": [{"type": "paragraph", "text": "2026/6/5"}, {"type": "paragraph", "text": "台中磐石首屆成人足球班已圓滿落幕，感謝所有學員的熱情參與！"}, {"type": "paragraph", "text": "課程期間，一線隊教練團與球員也親自下場同樂，希望大家不只學到足球技巧，更能感受足球帶來的樂趣。"}, {"type": "paragraph", "text": "展望未來，我們正規劃成人足球班，讓更多人有機會持續接觸足球、享受運動的美好。"}]}', N'{"blocks": [{"type": "paragraph", "text": "2026/6/5"}, {"type": "paragraph", "text": "Taichung Rock FC''s first adult football class has come to a successful close. Thank you to all participants for their enthusiastic involvement!"}, {"type": "paragraph", "text": "During the course, the First Team coaching staff and players joined in on the pitch themselves, hoping everyone would not only learn football skills but also feel the joy that football brings."}, {"type": "paragraph", "text": "Looking ahead, we are planning adult football classes so that more people have the chance to keep playing football and enjoy the good things about sport."}]}');
DECLARE @acat uniqueidentifier = (SELECT id FROM article_categories WHERE code = N'camps-events');
IF @acat IS NULL THROW 50140, N'找不到文章分類 camps-events', 1;
INSERT INTO articles (id, club_id, slug, article_category_id, status)
  SELECT NEWID(), @club, a.slug, @acat, N'draft' FROM @a a WHERE NOT EXISTS (SELECT 1 FROM articles x WHERE x.slug = a.slug);
INSERT INTO articles_i18n (article_id, locale, title, body)
  SELECT x.id, @zh, a.zh_title, a.zh_body FROM @a a JOIN articles x ON x.slug = a.slug
  WHERE NOT EXISTS (SELECT 1 FROM articles_i18n i WHERE i.article_id = x.id AND i.locale = @zh);
INSERT INTO articles_i18n (article_id, locale, title, body)
  SELECT x.id, @en, a.en_title, a.en_body FROM @a a JOIN articles x ON x.slug = a.slug
  WHERE NOT EXISTS (SELECT 1 FROM articles_i18n i WHERE i.article_id = x.id AND i.locale = @en);
IF (SELECT COUNT(*) FROM @a a JOIN articles x ON x.slug = a.slug JOIN articles_i18n i ON i.article_id = x.id) <> 4 THROW 50141, N'活動回顧（2 篇 × 雙語）未全部就位', 1;
IF EXISTS (SELECT 1 FROM @a a JOIN articles x ON x.slug = a.slug WHERE x.status <> N'draft' AND x.published_at IS NULL) THROW 50142, N'活動回顧必須是草稿', 1;
PRINT N'articles 完成（2 篇草稿）';


/* ───────── ⑤ 官方商品 2 件 → products（草稿）；球衣 1 個規格、機能襪尚無規格 ───────── */
DECLARE @pr TABLE (slug nvarchar(160) NOT NULL, zh_name nvarchar(128) NOT NULL, en_name nvarchar(128) NOT NULL, zh_narr nvarchar(max) NOT NULL, en_narr nvarchar(max) NOT NULL, sku nvarchar(64) NULL, price int NULL);
INSERT INTO @pr (slug, zh_name, en_name, zh_narr, en_narr, sku, price) VALUES
  (N'thick-cushion-performance-socks', N'厚底緩震機能襪', N'Thick-Sole Cushioned Performance Socks', N'由台中磐石足球俱樂部在日常訓練與比賽中實際使用，針對穿著過程中的感受持續調整與優化。

在反覆測試中，我們特別關注襪子的包覆感、穩定度與透氣表現，並依據實際穿著經驗，調整襪長比例、腳底厚度與整體貼合度，希望在運動與日常使用之間，找到一個更平衡、舒服的狀態。

不追求誇張的設計，而是回到穿著本身，讓每一步都能更自然、穩定。

中筒包覆設計
提供穩定支撐，不易滑落，減少運動中調整襪子的頻率，整體穿著更順暢安心

吸濕排汗毛巾底
腳底採毛巾底織法，提升緩衝與吸汗表現，長時間穿著也能維持乾爽，降低悶熱與不適感

貼合不勒腿
襪口彈性經過調整，在包覆與舒適之間取得平衡，不易鬆脫，也不會過度壓迫小腿

結構穩定不易位移
整體編織與版型設計，著重貼合度與穩定性，減少行走或運動時產生滑動與摩擦

運動與日常皆適用
不只適合訓練與比賽，日常穿搭也能輕鬆搭配，一雙即可應對不同情境需求', N'Used by Taichung Rock FC in everyday training and matches, and continuously adjusted and refined based on how they feel to wear.

Through repeated testing, we paid particular attention to the socks'' snug fit, stability and breathability, and adjusted the sock length, sole thickness and overall fit according to real wearing experience, hoping to find a more balanced and comfortable state between sport and everyday use.

We do not chase exaggerated design. We return to the wearing itself, so that every step feels more natural and stable.

Mid-calf wrap design
Provides stable support and does not slip down easily, reducing how often you need to adjust your socks during sport, for a smoother and more reassuring wear overall

Moisture-wicking terry sole
The sole uses a terry-loop knit that improves cushioning and sweat absorption, keeping feet dry through long wear and reducing stuffiness and discomfort

Snug fit without squeezing the calf
The cuff elasticity has been tuned to balance hold and comfort: it does not loosen easily and does not press too hard on the calf

Stable structure that resists shifting
The overall knit and pattern design focus on fit and stability, reducing sliding and friction when walking or playing sport

For sport and everyday wear
Suitable not only for training and matches but also easy to match with everyday outfits, so one pair handles different situations', NULL, NULL),
  (N'tcrfc-2026-home-jersey', N'台中磐石主場球衣｜2026賽季', N'Taichung Rock FC Home Jersey | 2026 Season', N'展現球隊精神與城市力量的象徵。台中磐石主場球衣以球隊標誌性的桃紅色為主調，配搭藍色細節設計，延續台中磐石鮮明而充滿活力的視覺風格，完美體現球隊堅毅、進取的精神。

由Joma生產的球衣採用輕量透氣物料製作，具備良好排汗與舒適性能，無論是在球場上激烈比賽，還是在日常穿搭與觀賽時穿著，都能保持乾爽與自在。

胸前印有台中磐石隊徽，象徵球隊與球迷之間緊密的連結。每一件球衣，不只是比賽裝備，更代表對台中磐石的支持與認同。', N'A symbol of the team''s spirit and the power of the city. The Taichung Rock FC home jersey takes the team''s signature pink as its main colour, paired with blue detailing, continuing Taichung Rock FC''s vivid and energetic visual style and perfectly embodying the team''s resilient and determined spirit.

Made by Joma, the jersey uses lightweight, breathable material with good sweat-wicking and comfort, keeping you dry and at ease whether you are playing hard on the pitch, or wearing it in daily outfits and when watching matches.

The Taichung Rock FC crest is printed on the chest, symbolising the close bond between the team and its fans. Every jersey is not just match kit, but a statement of support for and identification with Taichung Rock FC.', N'TCRFC-HOME-2026', 1200);
INSERT INTO products (id, club_id, slug, status)
  SELECT NEWID(), @club, p.slug, N'draft' FROM @pr p WHERE NOT EXISTS (SELECT 1 FROM products x WHERE x.club_id = @club AND x.slug = p.slug);
INSERT INTO products_i18n (product_id, locale, name, narrative)
  SELECT x.id, @zh, p.zh_name, p.zh_narr FROM @pr p JOIN products x ON x.club_id = @club AND x.slug = p.slug
  WHERE NOT EXISTS (SELECT 1 FROM products_i18n i WHERE i.product_id = x.id AND i.locale = @zh);
INSERT INTO products_i18n (product_id, locale, name, narrative)
  SELECT x.id, @en, p.en_name, p.en_narr FROM @pr p JOIN products x ON x.club_id = @club AND x.slug = p.slug
  WHERE NOT EXISTS (SELECT 1 FROM products_i18n i WHERE i.product_id = x.id AND i.locale = @en);
-- 規格：只有球衣有價格（客戶只給「NT$1,200.00 促銷價格」，原價不明，暫以 price = 1200、sale_price 留空）；無尺寸（size NULL）、庫存 0。
-- 機能襪原文無價格（price 為 NOT NULL），不建規格，後台新增規格後才能上架。
INSERT INTO product_variants (id, club_id, product_id, sku, price)
  SELECT NEWID(), @club, x.id, p.sku, p.price FROM @pr p JOIN products x ON x.club_id = @club AND x.slug = p.slug
  WHERE p.sku IS NOT NULL AND NOT EXISTS (SELECT 1 FROM product_variants v WHERE v.sku = p.sku);
IF (SELECT COUNT(*) FROM @pr p JOIN products x ON x.club_id = @club AND x.slug = p.slug JOIN products_i18n i ON i.product_id = x.id) <> 4 THROW 50150, N'官方商品（2 件 × 雙語）未全部就位', 1;
IF (SELECT COUNT(*) FROM @pr p JOIN products x ON x.club_id = @club AND x.slug = p.slug JOIN product_variants v ON v.product_id = x.id) <> 1 THROW 50151, N'商品規格不是 1 個（球衣）', 1;
IF EXISTS (SELECT 1 FROM @pr p JOIN products x ON x.club_id = @club AND x.slug = p.slug WHERE x.status <> N'draft') THROW 50152, N'官方商品必須是草稿', 1;
PRINT N'products 完成（2 件草稿、1 個規格）';

COMMIT;
PRINT N'20261016_intake-docs-import-2 完成';
