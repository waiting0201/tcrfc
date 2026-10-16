/* ============================================================================
   客戶 Google 文件內容匯入（資料遷移，C-1／B-3）
   2026-10-16｜來源：客戶 Google Drive 收件夾 36 份非新聞文稿（逐字原文存於 content/tcrfc/_raw/gdocs/，盤點見該目錄 _inventory.md），
   以 claude.ai Google Drive 連接器讀取。中文逐字、只做格式處理；英文為開發端翻譯（用詞照 docs/06-conventions.md §1.1）。
   全部是台中磐石（tcrfc）的資料，不動藍鯨。

   ── 本檔匯入的範圍（預期筆數，結尾逐項斷言，不符即 THROW 回滾）──
     ① B1 固定頁 3 頁（只填版型已有的區塊，不新增頁面或欄位）：
          about/our-story（2.1 我們的故事，1 個文字區塊）、about/vision-mission（2.2 願景與使命，2 列步驟）、
          charity/commitment（11.1 慈善理念，文字 2 ＋ 4 列步驟）。中英雙語寫在區塊 JSON 內。
     ② 教練 4 位（Staff／staff_i18n／staff_teams 掛一線隊 D1）：2.4 與 3.1 的四份文件逐字相同，每人只取一份。
     ③ 里程碑 49 筆（Milestone／milestones_i18n）：2024＝16、2025＝19、2026＝14。
     ④ FAQ 15 題（faqs／faqs_i18n／faq_category_links）。
     ⑤ 慈善計畫 3 個（charity_programs／_i18n）＋其受贈公益團體 3 筆（charities／_i18n，charity_id 為必填外鍵）。
     ⑥ 球員故事 3 篇 → 文章（分類 player-stories，一律草稿、不設發布日）；球員存在且姓名唯一時掛 article_relations。
   ── 不匯入（待客戶裁決或屬草稿）──
     2.3 核心價值／足球理念、4.3、4.4、5.x（足球節、NAMA 營隊、成人班、合作學校）、8.2 球迷會、8.3 商品、10 聯絡資訊、
     隱私權政策、11.3 慈善事蹟（缺活動圖片）、00 社群連結。

   ── 使用者裁決（2026-10-16）──
     • 年齡不寫死：教練簡歷的「出生日期／36 歲／43 歲」整行不放（staff 表也沒有出生日期欄位）。
     • 肖像同意：四位教練與里程碑提到的球員都已取得——教練 portrait_consent_status 由 not_consented 補為 consented
       （只改仍是預設值的列；目前這四位都沒有照片，此值只在日後上傳照片時才影響前台）。
     • 英文由開發端翻譯；沒有英文來源的台灣人名／學校／機構名不自行音譯，保留中文（見交付回報清單）。

   ── 逐字修正的錯字（其餘一律不動）──
     捐增→捐贈（2024 里程碑兩筆）｜前住→前往（2026-06-06）｜中南珂缔缘→中南珂締緣（Juliano 簡歷，繁簡混用）｜
     總教練英文名以內文為準：Matino Sofia→Martino Sofia（staff_i18n 現值為 Matino 時才改）｜
     FUTORO→FUTURO（2025-03-14；全專案賽程資料與官方社群帳號 tcrfuturo 皆為 FUTURO，故統一，待客戶確認）｜
     RC ALCOBENDAS→RC Alcobendas（里程碑 2024-11-04，與 2.1 內文一致）。
     「神棲市→神栖市」只出現在 11.3（本次不匯入），故本檔無此修正。
   ── 格式處理（不改意）──
     去 Markdown 記號與研究引用連結（[gogoal](…)、[tcrfc](…)）、分段；里程碑原文用「_」分隔的速記改為「：」，
     英數與中文間補半形空格、日期區間／僅標月份的事件把原文日期寫進描述（前台時間軸只顯示起始日）。
     里程碑 2024 第二筆（鳳凰國小）原文沒有日期，暫依相鄰一筆記為 2024-06-17，待客戶確認。

   ── 只補空值、不覆蓋後台已編輯的內容 ──
     • 頁面：區塊內容已等於本檔內容→略過；頁面版本號已大於 1（代表後台人員存過）→略過並 PRINT；否則換成本檔內容並新增一筆版本。
       搜尋與分享說明（SEO）只在仍是種子原文時才換（拿掉已不成立的「完整內文正在整理中」與舊的四大領域）。
     • 教練：bio／職稱只在 NULL 時補；英文名只在 NULL 或 Matino 錯字時改。
     • 里程碑／FAQ／慈善計畫／公益團體／文章：以固定 id 或 slug 判斷，已存在就整筆不動。
   • 🔴 冪等，可重複執行；單一交易，任何斷言失敗整批回滾。

   執行（種子→本檔的順序；本機）：sqlcmd -S <server> -d tcrfc_club -b -I -f 65001 -i db/migrations/20261016_intake-docs-import.sql
   ============================================================================ */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @club uniqueidentifier = (SELECT id FROM clubs WHERE code = N'tcrfc');
IF @club IS NULL THROW 50001, N'找不到 tcrfc 俱樂部（clubs.code = tcrfc）', 1;
DECLARE @zh nvarchar(10) = N'zh-Hant', @en nvarchar(10) = N'en';
DECLARE @n int, @n2 int, @id uniqueidentifier, @slug nvarchar(160), @msg nvarchar(400);


/* ───────── ① B1 固定頁 ───────── */
DECLARE @nb TABLE (sort_order int NOT NULL, block_type nvarchar(32) NOT NULL, content nvarchar(max) NOT NULL);
DECLARE @blocksJson nvarchar(max), @snap nvarchar(max), @cur nvarchar(max), @same bit, @pgUpdated int = 0, @pgAlready int = 0, @pgSkipped int = 0;

-- ── about/our-story
SET @slug = N'about/our-story';
SET @id = (SELECT id FROM pages WHERE club_id = @club AND slug = @slug);
IF @id IS NULL THROW 50010, N'找不到固定頁 about/our-story（請先套用 20261007_page-templates.sql，或進後台「頁面管理」一次讓系統補建骨架）', 1;
DELETE FROM @nb;
INSERT INTO @nb (sort_order, block_type, content) VALUES
  (0, N'text', N'{"body":{"zh":"台中磐石足球俱樂部成立於2024年。「磐石」對我們來說不只是一個隊名，是一個承諾：以台中為根、以足球為路，為更多台灣球員、孩子、家庭還有熱愛足球的人，建立一塊能夠長久站穩的基石。\n\n台灣足球的成長，不會靠一支球隊或一座獎盃完成。我們一直在想的是：喜歡踢球的孩子，在不同年齡階段都有地方可以繼續嗎？有潛力的年輕球員，能不能在念書和追夢之間不必太早放棄其中一個？成年球員在台灣，有沒有機會進入更專業、更有系統的競技環境？這些問題，從俱樂部成立第一天起，就跟比賽成績一樣重要。\n\n2024年夏天，我們踏出青訓的第一步。潭秀國中與嶺東高中球員組成U16聯隊，代表台中磐石遠赴日本茨城縣參加FELICE CUP。讓年輕球員走進不同的足球文化和競爭環境，看見更大的世界，知道自己還可以走得更遠。\n\n同一年，一線隊迎來創隊初期的重要考驗。台中磐石投入台灣乙級足球聯賽，最終在2024年12月7日最後一輪，以9比0擊敗MCU Desafio，拿下台乙冠軍，取得升上企甲的資格。\n\n升上企甲不是終點，是走向職業化的起點。從訓練品質、球員照顧、教練培育到青訓架構，俱樂部一步一步把制度建起來。2024年底，台中磐石與西班牙馬德里球隊RC Alcobendas建立合作，推動球員與教練交流、球探資訊共享及人才培育，讓俱樂部更有系統地向海外足球學習，也讓球員有機會接觸不同的訓練方式和比賽觀念。\n\n2025年，台中磐石正式進入企甲，面對台灣男子足球最高層級的競爭。我們一直在思考：怎麼不只是「參賽」，而是真的建立長期競爭力？方向之一，是把運動科學帶進日常訓練。俱樂部與Pulse Fit運動檢測中心合作，導入個人化訓練與數據追蹤，讓訓練更貼近每位球員的身體狀況和競賽需求。練得聰明，比練得辛苦更重要。\n\n我們也相信，足球需要交流才能看見新的可能。2025年7月，台中磐石主辦首屆「台中磐石國際足球盃」，邀請香港、德國與台灣球隊齊聚西屯足球場，設有成人組與青少年組，讓足球成為這座城市對外交流的語言，讓更多人願意走進球場、認識足球、支持在地球隊。\n\n在國際合作上，俱樂部先後與德國Rot Weiss Ahlen、義甲球會Hellas Verona建立合作，涵蓋球員交流、教練培訓、技術研習與國際賽事參與等面向。這些連結不是為了貼標籤，而是希望為台灣球員和教練創造更多真實接觸職業足球的機會。\n\n青訓始終是我們最看重的長期工程。台中磐石自營U12梯隊，並與潭秀國中合作銜接U14、U16，再延伸至高中體系的U19。我們不希望青少年足球只用成績來衡量價值。比起過早追冠軍，更重要的是讓孩子保持對足球的熱愛，在安全、專業又有挑戰的環境中慢慢成長，同時顧好學業和人生選擇。\n\n未來，台中磐石會繼續以台中為家，紮根社區，培育本土球員，把從青訓到成人隊的路慢慢走完整。我們珍惜每一場比賽、每一次訓練、每一位支持我們的球迷，還有每一個還在追夢的孩子。\n\n真正的磐石，不是靠一時的成就撐起來的，是日復一日累積出來的。不是一支球隊單獨在跑，而是一座城市和所有熱愛足球的人，一起把夢想踢得更遠。","en":"Taichung Rock FC was founded in 2024. To us, \"Rock\" is more than a team name; it is a commitment: rooted in Taichung, with football as our path, to build a foundation that can stand firm for the long term for more Taiwanese players, children, families and everyone who loves football.\n\nThe growth of Taiwanese football will not be achieved by one team or one trophy. The questions we keep asking are these: can children who love playing football find somewhere to keep playing at every age? Can promising young players avoid having to give up either their studies or their dreams too early? Can adult players in Taiwan get the chance to enter a more professional, more systematic competitive environment? Since the day the club was founded, these questions have mattered as much as match results.\n\nIn the summer of 2024 we took our first step in youth development. A U16 joint squad of players from 潭秀國中 and 嶺東高中 travelled to Ibaraki Prefecture, Japan, representing Taichung Rock FC at the FELICE CUP. It let young players step into a different football culture and competitive environment, see a bigger world, and learn that they can go further.\n\nThat same year, the first team faced an important early test. Taichung Rock FC entered the National Second Division and, on the final matchday on 7 December 2024, beat MCU Desafio 9-0 to win the Second Division title and earn promotion to the Enterprise Premier League.\n\nPromotion to the top flight is not the finish line but the starting point of professionalisation. From training quality, player welfare and coach development to the youth structure, the club has built its systems step by step. In late 2024, Taichung Rock FC established a partnership with RC Alcobendas, a Madrid club in Spain, promoting player and coach exchanges, sharing of scouting information and talent development, so that the club can learn from football overseas more systematically and players can experience different training methods and approaches to the game.\n\nIn 2025, Taichung Rock FC formally entered the Enterprise Premier League and faced the highest level of competition in Taiwanese men''s football. We kept asking ourselves how to do more than simply \"take part\" and build real long-term competitiveness. One direction was to bring sports science into everyday training. The club partnered with the Pulse Fit sports testing centre to introduce individualised training and data tracking, so that training fits each player''s physical condition and competitive demands more closely. Training smart matters more than training hard.\n\nWe also believe football needs exchange to see new possibilities. In July 2025, Taichung Rock FC hosted the first Taichung Rock FC International Cup, inviting teams from Hong Kong, Germany and Taiwan to Xitun Football Field, with adult and youth divisions, so that football became the language through which this city reaches out to the world and more people are willing to step into the stadium, get to know football and support their local team.\n\nOn the international front, the club has built partnerships with Germany''s Rot Weiss Ahlen and Serie A club Hellas Verona, covering player exchanges, coach education, technical workshops and participation in international tournaments. These links are not about adding labels; they aim to create more real opportunities for Taiwanese players and coaches to engage with professional football.\n\nYouth development has always been our most valued long-term undertaking. Taichung Rock FC runs its own U12 squad and works with 潭秀國中 to bridge into U14 and U16, extending further to U19 within the senior high school system. We do not want youth football to be measured by results alone. More important than chasing titles too early is keeping children''s love of the game, letting them grow gradually in a safe, professional and challenging environment while looking after their studies and life choices.\n\nIn the future, Taichung Rock FC will continue to make Taichung home, take root in the community, develop homegrown players, and gradually complete the path from youth development to the adult team. We cherish every match, every training session, every supporter, and every child who is still chasing a dream.\n\nA true rock is not held up by a moment of achievement; it is built up day after day. It is not one team running alone, but a city and everyone who loves football kicking a dream further together."}}');
SET @blocksJson = N'[{"blockType":"text","content":{"body":{"zh":"台中磐石足球俱樂部成立於2024年。「磐石」對我們來說不只是一個隊名，是一個承諾：以台中為根、以足球為路，為更多台灣球員、孩子、家庭還有熱愛足球的人，建立一塊能夠長久站穩的基石。\n\n台灣足球的成長，不會靠一支球隊或一座獎盃完成。我們一直在想的是：喜歡踢球的孩子，在不同年齡階段都有地方可以繼續嗎？有潛力的年輕球員，能不能在念書和追夢之間不必太早放棄其中一個？成年球員在台灣，有沒有機會進入更專業、更有系統的競技環境？這些問題，從俱樂部成立第一天起，就跟比賽成績一樣重要。\n\n2024年夏天，我們踏出青訓的第一步。潭秀國中與嶺東高中球員組成U16聯隊，代表台中磐石遠赴日本茨城縣參加FELICE CUP。讓年輕球員走進不同的足球文化和競爭環境，看見更大的世界，知道自己還可以走得更遠。\n\n同一年，一線隊迎來創隊初期的重要考驗。台中磐石投入台灣乙級足球聯賽，最終在2024年12月7日最後一輪，以9比0擊敗MCU Desafio，拿下台乙冠軍，取得升上企甲的資格。\n\n升上企甲不是終點，是走向職業化的起點。從訓練品質、球員照顧、教練培育到青訓架構，俱樂部一步一步把制度建起來。2024年底，台中磐石與西班牙馬德里球隊RC Alcobendas建立合作，推動球員與教練交流、球探資訊共享及人才培育，讓俱樂部更有系統地向海外足球學習，也讓球員有機會接觸不同的訓練方式和比賽觀念。\n\n2025年，台中磐石正式進入企甲，面對台灣男子足球最高層級的競爭。我們一直在思考：怎麼不只是「參賽」，而是真的建立長期競爭力？方向之一，是把運動科學帶進日常訓練。俱樂部與Pulse Fit運動檢測中心合作，導入個人化訓練與數據追蹤，讓訓練更貼近每位球員的身體狀況和競賽需求。練得聰明，比練得辛苦更重要。\n\n我們也相信，足球需要交流才能看見新的可能。2025年7月，台中磐石主辦首屆「台中磐石國際足球盃」，邀請香港、德國與台灣球隊齊聚西屯足球場，設有成人組與青少年組，讓足球成為這座城市對外交流的語言，讓更多人願意走進球場、認識足球、支持在地球隊。\n\n在國際合作上，俱樂部先後與德國Rot Weiss Ahlen、義甲球會Hellas Verona建立合作，涵蓋球員交流、教練培訓、技術研習與國際賽事參與等面向。這些連結不是為了貼標籤，而是希望為台灣球員和教練創造更多真實接觸職業足球的機會。\n\n青訓始終是我們最看重的長期工程。台中磐石自營U12梯隊，並與潭秀國中合作銜接U14、U16，再延伸至高中體系的U19。我們不希望青少年足球只用成績來衡量價值。比起過早追冠軍，更重要的是讓孩子保持對足球的熱愛，在安全、專業又有挑戰的環境中慢慢成長，同時顧好學業和人生選擇。\n\n未來，台中磐石會繼續以台中為家，紮根社區，培育本土球員，把從青訓到成人隊的路慢慢走完整。我們珍惜每一場比賽、每一次訓練、每一位支持我們的球迷，還有每一個還在追夢的孩子。\n\n真正的磐石，不是靠一時的成就撐起來的，是日復一日累積出來的。不是一支球隊單獨在跑，而是一座城市和所有熱愛足球的人，一起把夢想踢得更遠。","en":"Taichung Rock FC was founded in 2024. To us, \"Rock\" is more than a team name; it is a commitment: rooted in Taichung, with football as our path, to build a foundation that can stand firm for the long term for more Taiwanese players, children, families and everyone who loves football.\n\nThe growth of Taiwanese football will not be achieved by one team or one trophy. The questions we keep asking are these: can children who love playing football find somewhere to keep playing at every age? Can promising young players avoid having to give up either their studies or their dreams too early? Can adult players in Taiwan get the chance to enter a more professional, more systematic competitive environment? Since the day the club was founded, these questions have mattered as much as match results.\n\nIn the summer of 2024 we took our first step in youth development. A U16 joint squad of players from 潭秀國中 and 嶺東高中 travelled to Ibaraki Prefecture, Japan, representing Taichung Rock FC at the FELICE CUP. It let young players step into a different football culture and competitive environment, see a bigger world, and learn that they can go further.\n\nThat same year, the first team faced an important early test. Taichung Rock FC entered the National Second Division and, on the final matchday on 7 December 2024, beat MCU Desafio 9-0 to win the Second Division title and earn promotion to the Enterprise Premier League.\n\nPromotion to the top flight is not the finish line but the starting point of professionalisation. From training quality, player welfare and coach development to the youth structure, the club has built its systems step by step. In late 2024, Taichung Rock FC established a partnership with RC Alcobendas, a Madrid club in Spain, promoting player and coach exchanges, sharing of scouting information and talent development, so that the club can learn from football overseas more systematically and players can experience different training methods and approaches to the game.\n\nIn 2025, Taichung Rock FC formally entered the Enterprise Premier League and faced the highest level of competition in Taiwanese men''s football. We kept asking ourselves how to do more than simply \"take part\" and build real long-term competitiveness. One direction was to bring sports science into everyday training. The club partnered with the Pulse Fit sports testing centre to introduce individualised training and data tracking, so that training fits each player''s physical condition and competitive demands more closely. Training smart matters more than training hard.\n\nWe also believe football needs exchange to see new possibilities. In July 2025, Taichung Rock FC hosted the first Taichung Rock FC International Cup, inviting teams from Hong Kong, Germany and Taiwan to Xitun Football Field, with adult and youth divisions, so that football became the language through which this city reaches out to the world and more people are willing to step into the stadium, get to know football and support their local team.\n\nOn the international front, the club has built partnerships with Germany''s Rot Weiss Ahlen and Serie A club Hellas Verona, covering player exchanges, coach education, technical workshops and participation in international tournaments. These links are not about adding labels; they aim to create more real opportunities for Taiwanese players and coaches to engage with professional football.\n\nYouth development has always been our most valued long-term undertaking. Taichung Rock FC runs its own U12 squad and works with 潭秀國中 to bridge into U14 and U16, extending further to U19 within the senior high school system. We do not want youth football to be measured by results alone. More important than chasing titles too early is keeping children''s love of the game, letting them grow gradually in a safe, professional and challenging environment while looking after their studies and life choices.\n\nIn the future, Taichung Rock FC will continue to make Taichung home, take root in the community, develop homegrown players, and gradually complete the path from youth development to the adult team. We cherish every match, every training session, every supporter, and every child who is still chasing a dream.\n\nA true rock is not held up by a moment of achievement; it is built up day after day. It is not one team running alone, but a city and everyone who loves football kicking a dream further together."}}}]';
IF ISNULL((SELECT STRING_AGG(CAST(block_type AS nvarchar(max)), N',') WITHIN GROUP (ORDER BY sort_order) FROM page_blocks WHERE page_id = @id), N'') <> N'text' THROW 50011, N'固定頁 about/our-story 的區塊結構與版型（text）不符，不能匯入', 1;
SET @same = CASE WHEN EXISTS (SELECT 1 FROM @nb n JOIN page_blocks b ON b.page_id = @id AND b.sort_order = n.sort_order WHERE CAST(b.content AS nvarchar(max)) <> n.content) THEN 0 ELSE 1 END;
IF @same = 1 SET @pgAlready += 1;
ELSE IF ISNULL((SELECT MAX(version_no) FROM page_versions WHERE page_id = @id), 0) > 1
BEGIN
  SET @pgSkipped += 1;
  PRINT N'略過（後台已編輯過，版本號 > 1）：about/our-story';
END
ELSE
BEGIN
  UPDATE b SET content = n.content, updated_at = SYSUTCDATETIME() FROM page_blocks b JOIN @nb n ON n.sort_order = b.sort_order WHERE b.page_id = @id;
  UPDATE pages_i18n SET seo_description = N'台中磐石足球俱樂部（TCRFC）於 2024 年在台中成立。這裡收錄俱樂部從創立至今的沿革故事。' WHERE page_id = @id AND locale = @zh AND seo_description = N'台中磐石足球俱樂部（TCRFC）於 2024 年在台中成立。這裡收錄俱樂部從創立至今的沿革故事，完整內文正在整理中。';
  UPDATE pages_i18n SET seo_description = N'Taichung Rock FC (TCRFC) was founded in Taichung in 2024. This page tells the club''s story from its founding to today.' WHERE page_id = @id AND locale = @en AND seo_description = N'Taichung Rock FC (TCRFC) was founded in Taichung in 2024. This page will tell the club''s story from its founding to today; the full text is still being prepared.';
  SET @snap = (SELECT TOP 1 CAST(snapshot AS nvarchar(max)) FROM page_versions WHERE page_id = @id ORDER BY version_no DESC);
  IF @snap IS NULL OR ISJSON(@snap) = 0 SET @snap = N'{"seo":{"zh":{},"en":{}},"blocks":[]}';
  SET @snap = JSON_MODIFY(@snap, '$.blocks', JSON_QUERY(@blocksJson));
  IF JSON_QUERY(@snap, '$.seo') IS NULL SET @snap = JSON_MODIFY(@snap, '$.seo', JSON_QUERY(N'{}'));
  IF JSON_QUERY(@snap, '$.seo.zh') IS NULL SET @snap = JSON_MODIFY(@snap, '$.seo.zh', JSON_QUERY(N'{}'));
  IF JSON_QUERY(@snap, '$.seo.en') IS NULL SET @snap = JSON_MODIFY(@snap, '$.seo.en', JSON_QUERY(N'{}'));
  SET @snap = JSON_MODIFY(@snap, '$.seo.zh.seoTitle', (SELECT seo_title FROM pages_i18n WHERE page_id = @id AND locale = @zh));
  SET @snap = JSON_MODIFY(@snap, '$.seo.zh.seoDescription', (SELECT seo_description FROM pages_i18n WHERE page_id = @id AND locale = @zh));
  SET @snap = JSON_MODIFY(@snap, '$.seo.en.seoTitle', (SELECT seo_title FROM pages_i18n WHERE page_id = @id AND locale = @en));
  SET @snap = JSON_MODIFY(@snap, '$.seo.en.seoDescription', (SELECT seo_description FROM pages_i18n WHERE page_id = @id AND locale = @en));
  INSERT INTO page_versions (id, page_id, version_no, snapshot, preview_token)
    VALUES (NEWID(), @id, ISNULL((SELECT MAX(version_no) FROM page_versions WHERE page_id = @id), 0) + 1, @snap, LOWER(CONVERT(varchar(64), CRYPT_GEN_RANDOM(32), 2)));
  UPDATE pages SET updated_at = SYSUTCDATETIME() WHERE id = @id;
  SET @pgUpdated += 1;
END

-- ── about/vision-mission
SET @slug = N'about/vision-mission';
SET @id = (SELECT id FROM pages WHERE club_id = @club AND slug = @slug);
IF @id IS NULL THROW 50010, N'找不到固定頁 about/vision-mission（請先套用 20261007_page-templates.sql，或進後台「頁面管理」一次讓系統補建骨架）', 1;
DELETE FROM @nb;
INSERT INTO @nb (sort_order, block_type, content) VALUES
  (0, N'steps', N'{"items":[{"title":{"zh":"願景","en":"Vision"},"description":{"zh":"致力發掘與培育具潛力的台灣足球選手，提供專業的訓練資源，幫助他們邁向職業足球的舞台。\n透過不同活動與社區互動，將台中磐石塑造成為地方居民的共同驕傲，並促進社區認同與凝聚力。\n除了深耕在地，台中磐石也以國際化為目標，藉由足球運動傳遞台灣文化與精神，成為國際舞台的焦點。","en":"Discover and develop promising Taiwanese football players, and provide professional training resources to help them reach the professional stage.\nThrough a range of activities and community engagement, make Taichung Rock FC a shared source of pride for local residents and strengthen community identity and cohesion.\nBeyond putting down local roots, Taichung Rock FC also aims to be international, using football to share Taiwanese culture and spirit and to step onto the world stage."}},{"title":{"zh":"使命","en":"Mission"},"description":{"zh":"透過專業模式，培育選手追求卓越，發揮自身潛力。\n在台灣以行動展示專業足球理念，促進足球運動的永續發展。\n擔社會責任，成為社區積極的正面力量，創造和諧與進步的未來。","en":"Through a professional model, develop players to pursue excellence and fulfil their potential.\nDemonstrate a professional approach to football through action in Taiwan, and promote the sustainable development of the sport.\nTake on social responsibility and be an active, positive force in the community, creating a harmonious and progressive future."}}]}');
SET @blocksJson = N'[{"blockType":"steps","content":{"items":[{"title":{"zh":"願景","en":"Vision"},"description":{"zh":"致力發掘與培育具潛力的台灣足球選手，提供專業的訓練資源，幫助他們邁向職業足球的舞台。\n透過不同活動與社區互動，將台中磐石塑造成為地方居民的共同驕傲，並促進社區認同與凝聚力。\n除了深耕在地，台中磐石也以國際化為目標，藉由足球運動傳遞台灣文化與精神，成為國際舞台的焦點。","en":"Discover and develop promising Taiwanese football players, and provide professional training resources to help them reach the professional stage.\nThrough a range of activities and community engagement, make Taichung Rock FC a shared source of pride for local residents and strengthen community identity and cohesion.\nBeyond putting down local roots, Taichung Rock FC also aims to be international, using football to share Taiwanese culture and spirit and to step onto the world stage."}},{"title":{"zh":"使命","en":"Mission"},"description":{"zh":"透過專業模式，培育選手追求卓越，發揮自身潛力。\n在台灣以行動展示專業足球理念，促進足球運動的永續發展。\n擔社會責任，成為社區積極的正面力量，創造和諧與進步的未來。","en":"Through a professional model, develop players to pursue excellence and fulfil their potential.\nDemonstrate a professional approach to football through action in Taiwan, and promote the sustainable development of the sport.\nTake on social responsibility and be an active, positive force in the community, creating a harmonious and progressive future."}}]}}]';
IF ISNULL((SELECT STRING_AGG(CAST(block_type AS nvarchar(max)), N',') WITHIN GROUP (ORDER BY sort_order) FROM page_blocks WHERE page_id = @id), N'') <> N'steps' THROW 50011, N'固定頁 about/vision-mission 的區塊結構與版型（steps）不符，不能匯入', 1;
SET @same = CASE WHEN EXISTS (SELECT 1 FROM @nb n JOIN page_blocks b ON b.page_id = @id AND b.sort_order = n.sort_order WHERE CAST(b.content AS nvarchar(max)) <> n.content) THEN 0 ELSE 1 END;
IF @same = 1 SET @pgAlready += 1;
ELSE IF ISNULL((SELECT MAX(version_no) FROM page_versions WHERE page_id = @id), 0) > 1
BEGIN
  SET @pgSkipped += 1;
  PRINT N'略過（後台已編輯過，版本號 > 1）：about/vision-mission';
END
ELSE
BEGIN
  UPDATE b SET content = n.content, updated_at = SYSUTCDATETIME() FROM page_blocks b JOIN @nb n ON n.sort_order = b.sort_order WHERE b.page_id = @id;
  SET @snap = (SELECT TOP 1 CAST(snapshot AS nvarchar(max)) FROM page_versions WHERE page_id = @id ORDER BY version_no DESC);
  IF @snap IS NULL OR ISJSON(@snap) = 0 SET @snap = N'{"seo":{"zh":{},"en":{}},"blocks":[]}';
  SET @snap = JSON_MODIFY(@snap, '$.blocks', JSON_QUERY(@blocksJson));
  IF JSON_QUERY(@snap, '$.seo') IS NULL SET @snap = JSON_MODIFY(@snap, '$.seo', JSON_QUERY(N'{}'));
  IF JSON_QUERY(@snap, '$.seo.zh') IS NULL SET @snap = JSON_MODIFY(@snap, '$.seo.zh', JSON_QUERY(N'{}'));
  IF JSON_QUERY(@snap, '$.seo.en') IS NULL SET @snap = JSON_MODIFY(@snap, '$.seo.en', JSON_QUERY(N'{}'));
  SET @snap = JSON_MODIFY(@snap, '$.seo.zh.seoTitle', (SELECT seo_title FROM pages_i18n WHERE page_id = @id AND locale = @zh));
  SET @snap = JSON_MODIFY(@snap, '$.seo.zh.seoDescription', (SELECT seo_description FROM pages_i18n WHERE page_id = @id AND locale = @zh));
  SET @snap = JSON_MODIFY(@snap, '$.seo.en.seoTitle', (SELECT seo_title FROM pages_i18n WHERE page_id = @id AND locale = @en));
  SET @snap = JSON_MODIFY(@snap, '$.seo.en.seoDescription', (SELECT seo_description FROM pages_i18n WHERE page_id = @id AND locale = @en));
  INSERT INTO page_versions (id, page_id, version_no, snapshot, preview_token)
    VALUES (NEWID(), @id, ISNULL((SELECT MAX(version_no) FROM page_versions WHERE page_id = @id), 0) + 1, @snap, LOWER(CONVERT(varchar(64), CRYPT_GEN_RANDOM(32), 2)));
  UPDATE pages SET updated_at = SYSUTCDATETIME() WHERE id = @id;
  SET @pgUpdated += 1;
END

-- ── charity/commitment
SET @slug = N'charity/commitment';
SET @id = (SELECT id FROM pages WHERE club_id = @club AND slug = @slug);
IF @id IS NULL THROW 50010, N'找不到固定頁 charity/commitment（請先套用 20261007_page-templates.sql，或進後台「頁面管理」一次讓系統補建骨架）', 1;
DELETE FROM @nb;
INSERT INTO @nb (sort_order, block_type, content) VALUES
  (0, N'text', N'{"body":{"zh":"台中磐石足球俱樂部立足台中，致力推廣足球運動、培育本地選手、深化社區互動及推動青少年足球。俱樂部相信，足球不只是競賽，更是讓兒童與青少年建立自信、紀律、團隊精神及國際視野的重要教育媒介。","en":"Taichung Rock FC is rooted in Taichung and is committed to promoting football, developing local players, deepening community engagement and advancing youth football. The club believes that football is more than competition: it is an important educational medium through which children and young people build confidence, discipline, teamwork and an international outlook."}}'),
  (1, N'text', N'{"body":{"zh":"台中磐石以實際行動支持基層足球與在地教育：一方面協助具潛力的青少年球員取得跨國交流與訓練機會；另一方面結合企業夥伴，把學習資源帶進校園與幼兒園。俱樂部期望在追求競技成績的同時，承擔社會責任，成為社區中積極、正面的力量。","en":"Taichung Rock FC supports grassroots football and local education through concrete action: on one hand, helping promising young players gain international exchange and training opportunities; on the other, working with corporate partners to bring learning resources into schools and kindergartens. While pursuing competitive success, the club aims to take on social responsibility and be an active, positive force in the community."}}'),
  (2, N'steps', N'{"items":[{"title":{"zh":"青少年足球培育","en":"Youth football development"},"description":{"zh":"提供基層球員訓練、競賽與國際交流機會。\n全額支持潭秀國中與嶺東高中聯隊共 18 名球員、2 名教練赴日本參加第七屆 Felice CUP U16。","en":"Providing grassroots players with training, competition and international exchange opportunities.\nFully funded 18 players and 2 coaches from the joint 潭秀國中 and 嶺東高中 squad to attend the 7th Felice CUP U16 in Japan."}},{"title":{"zh":"國際教育與運動交流","en":"International education and sports exchange"},"description":{"zh":"透過海外賽事及訓練，擴展青年球員視野與競技經驗。\nFelice CUP U16 於日本茨城縣舉行，行程包含 J 聯盟教練指導及後續賽事。","en":"Broadening young players'' horizons and competitive experience through overseas tournaments and training.\nThe Felice CUP U16 was held in Ibaraki Prefecture, Japan, and included coaching by J.League coaches followed by tournament matches."}},{"title":{"zh":"兒少教育資源","en":"Educational resources for children"},"description":{"zh":"以書籍及學習資源支持幼兒教育與成長。\n與 Subkarma 捐贈《Cabbage & Tyler》英語幼童學習書籍予潭秀非營利幼兒園。","en":"Supporting early childhood education and growth with books and learning resources.\nTogether with Subkarma, donated the English children''s learning books Cabbage & Tyler to 潭秀非營利幼兒園."}},{"title":{"zh":"社區與家庭足球","en":"Community and family football"},"description":{"zh":"透過賽事與互動活動，增加兒少及家庭接觸足球的機會。\n2025 台中磐石盃設 U6、U8、U10、U12 組別，並規劃親子與球員互動活動，吸引超過 45 支少年隊伍參加。","en":"Increasing children''s and families'' access to football through matches and interactive events.\nThe 2025 Taichung Rock FC Cup had U6, U8, U10 and U12 divisions, with parent-child and player-interaction activities planned, and attracted more than 45 youth teams."}}]}');
SET @blocksJson = N'[{"blockType":"text","content":{"body":{"zh":"台中磐石足球俱樂部立足台中，致力推廣足球運動、培育本地選手、深化社區互動及推動青少年足球。俱樂部相信，足球不只是競賽，更是讓兒童與青少年建立自信、紀律、團隊精神及國際視野的重要教育媒介。","en":"Taichung Rock FC is rooted in Taichung and is committed to promoting football, developing local players, deepening community engagement and advancing youth football. The club believes that football is more than competition: it is an important educational medium through which children and young people build confidence, discipline, teamwork and an international outlook."}}},{"blockType":"text","content":{"body":{"zh":"台中磐石以實際行動支持基層足球與在地教育：一方面協助具潛力的青少年球員取得跨國交流與訓練機會；另一方面結合企業夥伴，把學習資源帶進校園與幼兒園。俱樂部期望在追求競技成績的同時，承擔社會責任，成為社區中積極、正面的力量。","en":"Taichung Rock FC supports grassroots football and local education through concrete action: on one hand, helping promising young players gain international exchange and training opportunities; on the other, working with corporate partners to bring learning resources into schools and kindergartens. While pursuing competitive success, the club aims to take on social responsibility and be an active, positive force in the community."}}},{"blockType":"steps","content":{"items":[{"title":{"zh":"青少年足球培育","en":"Youth football development"},"description":{"zh":"提供基層球員訓練、競賽與國際交流機會。\n全額支持潭秀國中與嶺東高中聯隊共 18 名球員、2 名教練赴日本參加第七屆 Felice CUP U16。","en":"Providing grassroots players with training, competition and international exchange opportunities.\nFully funded 18 players and 2 coaches from the joint 潭秀國中 and 嶺東高中 squad to attend the 7th Felice CUP U16 in Japan."}},{"title":{"zh":"國際教育與運動交流","en":"International education and sports exchange"},"description":{"zh":"透過海外賽事及訓練，擴展青年球員視野與競技經驗。\nFelice CUP U16 於日本茨城縣舉行，行程包含 J 聯盟教練指導及後續賽事。","en":"Broadening young players'' horizons and competitive experience through overseas tournaments and training.\nThe Felice CUP U16 was held in Ibaraki Prefecture, Japan, and included coaching by J.League coaches followed by tournament matches."}},{"title":{"zh":"兒少教育資源","en":"Educational resources for children"},"description":{"zh":"以書籍及學習資源支持幼兒教育與成長。\n與 Subkarma 捐贈《Cabbage & Tyler》英語幼童學習書籍予潭秀非營利幼兒園。","en":"Supporting early childhood education and growth with books and learning resources.\nTogether with Subkarma, donated the English children''s learning books Cabbage & Tyler to 潭秀非營利幼兒園."}},{"title":{"zh":"社區與家庭足球","en":"Community and family football"},"description":{"zh":"透過賽事與互動活動，增加兒少及家庭接觸足球的機會。\n2025 台中磐石盃設 U6、U8、U10、U12 組別，並規劃親子與球員互動活動，吸引超過 45 支少年隊伍參加。","en":"Increasing children''s and families'' access to football through matches and interactive events.\nThe 2025 Taichung Rock FC Cup had U6, U8, U10 and U12 divisions, with parent-child and player-interaction activities planned, and attracted more than 45 youth teams."}}]}}]';
IF ISNULL((SELECT STRING_AGG(CAST(block_type AS nvarchar(max)), N',') WITHIN GROUP (ORDER BY sort_order) FROM page_blocks WHERE page_id = @id), N'') <> N'text,text,steps' THROW 50011, N'固定頁 charity/commitment 的區塊結構與版型（text,text,steps）不符，不能匯入', 1;
SET @same = CASE WHEN EXISTS (SELECT 1 FROM @nb n JOIN page_blocks b ON b.page_id = @id AND b.sort_order = n.sort_order WHERE CAST(b.content AS nvarchar(max)) <> n.content) THEN 0 ELSE 1 END;
IF @same = 1 SET @pgAlready += 1;
ELSE IF ISNULL((SELECT MAX(version_no) FROM page_versions WHERE page_id = @id), 0) > 1
BEGIN
  SET @pgSkipped += 1;
  PRINT N'略過（後台已編輯過，版本號 > 1）：charity/commitment';
END
ELSE
BEGIN
  UPDATE b SET content = n.content, updated_at = SYSUTCDATETIME() FROM page_blocks b JOIN @nb n ON n.sort_order = b.sort_order WHERE b.page_id = @id;
  UPDATE pages_i18n SET seo_description = N'台中磐石足球俱樂部的慈善理念與四大投入領域：青少年足球培育、國際教育與運動交流、兒少教育資源、社區與家庭足球。' WHERE page_id = @id AND locale = @zh AND seo_description = N'台中磐石足球俱樂部的慈善理念與四大投入領域：青少年扶助、偏鄉足球、弱勢家庭與公益義賽，實踐 Community 社區共好核心價值。';
  UPDATE pages_i18n SET seo_description = N'The charitable philosophy and four focus areas of Taichung Rock FC: youth football development, international education and sports exchange, educational resources for children, and community and family football.' WHERE page_id = @id AND locale = @en AND seo_description = N'The charitable philosophy and four focus areas of Taichung Rock FC: youth support, rural football, disadvantaged families and charity matches, putting our core value of Community into practice.';
  SET @snap = (SELECT TOP 1 CAST(snapshot AS nvarchar(max)) FROM page_versions WHERE page_id = @id ORDER BY version_no DESC);
  IF @snap IS NULL OR ISJSON(@snap) = 0 SET @snap = N'{"seo":{"zh":{},"en":{}},"blocks":[]}';
  SET @snap = JSON_MODIFY(@snap, '$.blocks', JSON_QUERY(@blocksJson));
  IF JSON_QUERY(@snap, '$.seo') IS NULL SET @snap = JSON_MODIFY(@snap, '$.seo', JSON_QUERY(N'{}'));
  IF JSON_QUERY(@snap, '$.seo.zh') IS NULL SET @snap = JSON_MODIFY(@snap, '$.seo.zh', JSON_QUERY(N'{}'));
  IF JSON_QUERY(@snap, '$.seo.en') IS NULL SET @snap = JSON_MODIFY(@snap, '$.seo.en', JSON_QUERY(N'{}'));
  SET @snap = JSON_MODIFY(@snap, '$.seo.zh.seoTitle', (SELECT seo_title FROM pages_i18n WHERE page_id = @id AND locale = @zh));
  SET @snap = JSON_MODIFY(@snap, '$.seo.zh.seoDescription', (SELECT seo_description FROM pages_i18n WHERE page_id = @id AND locale = @zh));
  SET @snap = JSON_MODIFY(@snap, '$.seo.en.seoTitle', (SELECT seo_title FROM pages_i18n WHERE page_id = @id AND locale = @en));
  SET @snap = JSON_MODIFY(@snap, '$.seo.en.seoDescription', (SELECT seo_description FROM pages_i18n WHERE page_id = @id AND locale = @en));
  INSERT INTO page_versions (id, page_id, version_no, snapshot, preview_token)
    VALUES (NEWID(), @id, ISNULL((SELECT MAX(version_no) FROM page_versions WHERE page_id = @id), 0) + 1, @snap, LOWER(CONVERT(varchar(64), CRYPT_GEN_RANDOM(32), 2)));
  UPDATE pages SET updated_at = SYSUTCDATETIME() WHERE id = @id;
  SET @pgUpdated += 1;
END

PRINT CONCAT(N'pages: updated=', @pgUpdated, N' already=', @pgAlready, N' skipped_edited=', @pgSkipped);
IF @pgUpdated + @pgAlready + @pgSkipped <> 3 THROW 50019, N'固定頁處理數不是 3', 1;


/* ───────── ② 教練 4 位 ───────── */
DECLARE @sid uniqueidentifier, @d1 uniqueidentifier = (SELECT TOP 1 t.id FROM teams t WHERE t.club_id = @club AND t.code = N'D1');
DECLARE @stCreated int = 0, @stBio int = 0, @stBioEn int = 0, @stFound int = 0;

-- ── 儒利亞諾・羅德里格斯
SET @sid = (SELECT TOP 1 s.id FROM staff s JOIN staff_i18n i ON i.staff_id = s.id AND i.locale = @zh WHERE s.club_id = @club AND i.name = N'儒利亞諾・羅德里格斯');
IF @sid IS NULL
BEGIN
  SET @sid = NEWID();
  INSERT INTO staff (id, club_id) VALUES (@sid, @club);
  INSERT INTO staff_i18n (staff_id, locale, name, title) VALUES (@sid, @zh, N'儒利亞諾・羅德里格斯', N'守門員教練');
  SET @stCreated += 1;
END
SET @stFound += 1;
UPDATE staff_i18n SET title = N'守門員教練' WHERE staff_id = @sid AND locale = @zh AND title IS NULL;
IF NOT EXISTS (SELECT 1 FROM staff_i18n WHERE staff_id = @sid AND locale = @en)
  INSERT INTO staff_i18n (staff_id, locale, name, title) VALUES (@sid, @en, N'Juliano Rodrigues', N'Goalkeeper Coach');
UPDATE staff_i18n SET name = N'Juliano Rodrigues' WHERE staff_id = @sid AND locale = @en AND (name IS NULL OR name = N'');
UPDATE staff_i18n SET title = N'Goalkeeper Coach' WHERE staff_id = @sid AND locale = @en AND (title IS NULL OR title = N'');
UPDATE staff_i18n SET bio = N'Juliano Rodrigues 是一名巴西守門員教練，具有亞洲青訓、職業梯隊與職業一隊的工作經歷。他先後在中國中南珂締緣足球俱樂部、上海申花 U16/U20，以及上海申花一隊任職；2025–26 年則於柬埔寨頂級聯賽的 Angkor Tiger FC 工作。

國籍：巴西
主要資格：CBF Academy A級教練證、CBF Academy A級守門員教練證
學歷：體育教育學位
語言：葡萄牙文、西班牙文、英文
曾任職球隊：Yongho High School FC U18（韓國）→ 中南珂締緣（中國）→ 上海綠地申花 U16/U20 → 上海申花 → Angkor Tiger FC（柬埔寨）
主要榮譽：中國足協盃冠軍（2023）' WHERE staff_id = @sid AND locale = @zh AND bio IS NULL;
SET @stBio += @@ROWCOUNT;
UPDATE staff_i18n SET bio = N'Juliano Rodrigues is a Brazilian goalkeeper coach with experience in Asian youth development, professional squads and a professional first team. He has worked at the Chinese football club 中南珂締緣, with Shanghai Shenhua U16/U20 and with the Shanghai Shenhua first team; in 2025–26 he worked at Angkor Tiger FC in Cambodia''s top-flight league.

Nationality: Brazil
Key qualifications: CBF Academy A Licence coach; CBF Academy A Licence goalkeeper coach
Education: Degree in physical education
Languages: Portuguese, Spanish, English
Previous clubs: Yongho High School FC U18 (South Korea) → 中南珂締緣 (China) → Shanghai Shenhua U16/U20 → Shanghai Shenhua → Angkor Tiger FC (Cambodia)
Honours: Chinese FA Cup winner (2023)' WHERE staff_id = @sid AND locale = @en AND bio IS NULL;
SET @stBioEn += @@ROWCOUNT;
UPDATE staff SET portrait_consent_status = N'consented', updated_at = SYSUTCDATETIME() WHERE id = @sid AND portrait_consent_status = N'not_consented';
IF @d1 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM staff_teams WHERE staff_id = @sid AND team_id = @d1) INSERT INTO staff_teams (staff_id, team_id) VALUES (@sid, @d1);

-- ── 托馬斯・卡斯泰洛
SET @sid = (SELECT TOP 1 s.id FROM staff s JOIN staff_i18n i ON i.staff_id = s.id AND i.locale = @zh WHERE s.club_id = @club AND i.name = N'托馬斯・卡斯泰洛');
IF @sid IS NULL
BEGIN
  SET @sid = NEWID();
  INSERT INTO staff (id, club_id) VALUES (@sid, @club);
  INSERT INTO staff_i18n (staff_id, locale, name, title) VALUES (@sid, @zh, N'托馬斯・卡斯泰洛', N'教練');
  SET @stCreated += 1;
END
SET @stFound += 1;
UPDATE staff_i18n SET title = N'教練' WHERE staff_id = @sid AND locale = @zh AND title IS NULL;
IF NOT EXISTS (SELECT 1 FROM staff_i18n WHERE staff_id = @sid AND locale = @en)
  INSERT INTO staff_i18n (staff_id, locale, name, title) VALUES (@sid, @en, N'Thomas Castello', N'Coach');
UPDATE staff_i18n SET name = N'Thomas Castello' WHERE staff_id = @sid AND locale = @en AND (name IS NULL OR name = N'');
UPDATE staff_i18n SET title = N'Coach' WHERE staff_id = @sid AND locale = @en AND (title IS NULL OR title = N'');
UPDATE staff_i18n SET bio = N'Thomas Castello 是一名專注於培養的義大利教練，擁有近二十年的守門員球員經驗，以及超過五年的守門員訓練、賽事分析與助教實務。

國籍：義大利
主要資格：UEFA C級教練證、UEFA B級守門員教練證、青訓學院守門員教練證
語言：義大利文、英文、西班牙文
曾任職球隊：Castelnuovo D/G Youth Academy → Castelnuovo D/G Regional League → Baldo Junior Team' WHERE staff_id = @sid AND locale = @zh AND bio IS NULL;
SET @stBio += @@ROWCOUNT;
UPDATE staff_i18n SET bio = N'Thomas Castello is an Italian coach focused on development, with nearly twenty years of experience as a goalkeeper and more than five years of practical work in goalkeeper training, match analysis and assistant coaching.

Nationality: Italy
Key qualifications: UEFA C Licence coach; UEFA B Licence goalkeeper coach; youth academy goalkeeper coaching certificate
Languages: Italian, English, Spanish
Previous clubs: Castelnuovo D/G Youth Academy → Castelnuovo D/G Regional League → Baldo Junior Team' WHERE staff_id = @sid AND locale = @en AND bio IS NULL;
SET @stBioEn += @@ROWCOUNT;
UPDATE staff SET portrait_consent_status = N'consented', updated_at = SYSUTCDATETIME() WHERE id = @sid AND portrait_consent_status = N'not_consented';
IF @d1 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM staff_teams WHERE staff_id = @sid AND team_id = @d1) INSERT INTO staff_teams (staff_id, team_id) VALUES (@sid, @d1);

-- ── 瑪蒂諾
SET @sid = (SELECT TOP 1 s.id FROM staff s JOIN staff_i18n i ON i.staff_id = s.id AND i.locale = @zh WHERE s.club_id = @club AND i.name = N'瑪蒂諾');
IF @sid IS NULL
BEGIN
  SET @sid = NEWID();
  INSERT INTO staff (id, club_id) VALUES (@sid, @club);
  INSERT INTO staff_i18n (staff_id, locale, name, title) VALUES (@sid, @zh, N'瑪蒂諾', N'總教練');
  SET @stCreated += 1;
END
SET @stFound += 1;
UPDATE staff_i18n SET title = N'總教練' WHERE staff_id = @sid AND locale = @zh AND title IS NULL;
IF NOT EXISTS (SELECT 1 FROM staff_i18n WHERE staff_id = @sid AND locale = @en)
  INSERT INTO staff_i18n (staff_id, locale, name, title) VALUES (@sid, @en, N'Martino Sofia', N'Head Coach');
UPDATE staff_i18n SET name = N'Martino Sofia' WHERE staff_id = @sid AND locale = @en AND (name IS NULL OR name = N'' OR name = N'Matino Sofia');
UPDATE staff_i18n SET title = N'Head Coach' WHERE staff_id = @sid AND locale = @en AND (title IS NULL OR title = N'');
UPDATE staff_i18n SET bio = N'Martino Sofia 是一名經驗的義大利 UEFA A級教練。他曾於義甲 Chievo Verona、義乙 Spezia、Vicenza、Novara、Cittadella，以及義丙 SPAL、Pordenone任職。除職業隊經驗外，他曾在 Padova 大學與 Florence 大學相關課程授課。

國籍：義大利
主要資格：UEFA A級教練證
學歷：運動科學與技術學位；英國 Liverpool John Moores University 表現分析及球探碩士
曾任職球隊：A.S. Cittadella Youth → A.S. Cittadella → Spezia Calcio → Novara Calcio → A.C. Chievo Verona → L.R. Vicenza → S.P.A.L. → Pordenone → Spezia Calcio' WHERE staff_id = @sid AND locale = @zh AND bio IS NULL;
SET @stBio += @@ROWCOUNT;
UPDATE staff_i18n SET bio = N'Martino Sofia is an experienced Italian UEFA A Licence coach. He has worked at Serie A club Chievo Verona; at Serie B clubs Spezia, Vicenza, Novara and Cittadella; and at Serie C clubs SPAL and Pordenone. Besides his experience at professional clubs, he has taught on related courses at the University of Padova and the University of Florence.

Nationality: Italy
Key qualifications: UEFA A Licence coach
Education: Degree in sport science and technology; Master''s in performance analysis and scouting, Liverpool John Moores University (UK)
Previous clubs: A.S. Cittadella Youth → A.S. Cittadella → Spezia Calcio → Novara Calcio → A.C. Chievo Verona → L.R. Vicenza → S.P.A.L. → Pordenone → Spezia Calcio' WHERE staff_id = @sid AND locale = @en AND bio IS NULL;
SET @stBioEn += @@ROWCOUNT;
UPDATE staff SET portrait_consent_status = N'consented', updated_at = SYSUTCDATETIME() WHERE id = @sid AND portrait_consent_status = N'not_consented';
IF @d1 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM staff_teams WHERE staff_id = @sid AND team_id = @d1) INSERT INTO staff_teams (staff_id, team_id) VALUES (@sid, @d1);

-- ── 江奕璠
SET @sid = (SELECT TOP 1 s.id FROM staff s JOIN staff_i18n i ON i.staff_id = s.id AND i.locale = @zh WHERE s.club_id = @club AND i.name = N'江奕璠');
IF @sid IS NULL
BEGIN
  SET @sid = NEWID();
  INSERT INTO staff (id, club_id) VALUES (@sid, @club);
  INSERT INTO staff_i18n (staff_id, locale, name, title) VALUES (@sid, @zh, N'江奕璠', N'體能教練');
  SET @stCreated += 1;
END
SET @stFound += 1;
UPDATE staff_i18n SET title = N'體能教練' WHERE staff_id = @sid AND locale = @zh AND title IS NULL;
IF NOT EXISTS (SELECT 1 FROM staff_i18n WHERE staff_id = @sid AND locale = @en)
  INSERT INTO staff_i18n (staff_id, locale, name, title) VALUES (@sid, @en, N'Yi-Fan Chiang', N'Fitness Coach');
UPDATE staff_i18n SET name = N'Yi-Fan Chiang' WHERE staff_id = @sid AND locale = @en AND (name IS NULL OR name = N'');
UPDATE staff_i18n SET title = N'Fitness Coach' WHERE staff_id = @sid AND locale = @en AND (title IS NULL OR title = N'');
UPDATE staff_i18n SET bio = N'江奕璠是一名具跨項目實務背景的肌力與體能教練，工作經驗涵蓋足球、橄欖球、棒球與籃球，具有青少年、校隊、企業隊及代表隊等不同競技環境的訓練經驗。在橄欖球領域，亦曾擔任 2022 亞洲橄欖球總會 U19 錦標賽及 2023 東南亞七人制橄欖球錦標賽中華隊肌力與體能教練。

其訓練專業聚焦於力量、爆發力、速度、減速與變向能力，以及以速度為依據的訓練與離心肌力訓練，能協助球隊建立從青訓到成人競技層級的體能發展、動作能力、負荷管理與運動表現追蹤系統。

國籍：台灣
職位：肌力與體能教練
主要資格：World Rugby 肌力與體能教練 Level 1、中華民國運動教練協會丙級肌力與體能教練、CTFA D級／C級足球教練證
學歷：中國文化大學運動教練研究所；輔仁大學西班牙語文學系
語言：中文、英文、西班牙文
曾任職球隊／單位：Mars 桃園戰神男女足球隊 → 桃園國際男子足球隊 → 臺北市立大學足球隊' WHERE staff_id = @sid AND locale = @zh AND bio IS NULL;
SET @stBio += @@ROWCOUNT;
UPDATE staff_i18n SET bio = N'Yi-Fan Chiang is a strength and conditioning coach with a cross-sport background. His experience spans football, rugby, baseball and basketball, and covers training in youth, school-team, corporate-team and representative-team settings. In rugby, he has also served as strength and conditioning coach for the Chinese Taipei team at the 2022 Asia Rugby U19 Championship and the 2023 Southeast Asian Rugby Sevens Championship.

His training expertise focuses on strength, power, speed, deceleration and change of direction, as well as velocity-based training and eccentric strength training. He helps teams build systems for physical development, movement capability, load management and performance tracking, from youth development to adult competition.

Nationality: Taiwan
Role: Strength and conditioning coach
Key qualifications: World Rugby Strength and Conditioning Coach Level 1; Class C strength and conditioning coach, Chinese Taipei Sports Coaches Association; CTFA D and C Licence football coaching certificates
Education: Graduate Institute of Sports Coaching, Chinese Culture University; Department of Spanish Language and Literature, Fu Jen Catholic University
Languages: Chinese, English, Spanish
Previous clubs and organisations: Mars 桃園戰神 men''s and women''s football teams → 桃園國際 men''s football team → University of Taipei football team' WHERE staff_id = @sid AND locale = @en AND bio IS NULL;
SET @stBioEn += @@ROWCOUNT;
UPDATE staff SET portrait_consent_status = N'consented', updated_at = SYSUTCDATETIME() WHERE id = @sid AND portrait_consent_status = N'not_consented';
IF @d1 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM staff_teams WHERE staff_id = @sid AND team_id = @d1) INSERT INTO staff_teams (staff_id, team_id) VALUES (@sid, @d1);

PRINT CONCAT(N'staff: matched=', @stFound, N' created=', @stCreated, N' bio_zh_filled=', @stBio, N' bio_en_filled=', @stBioEn);
IF @stFound <> 4 THROW 50029, N'教練處理數不是 4', 1;
IF @d1 IS NULL THROW 50028, N'找不到一線隊 D1，無法掛教練關聯', 1;
IF (SELECT COUNT(*) FROM staff_teams st JOIN staff_i18n i ON i.staff_id = st.staff_id AND i.locale = @zh WHERE st.team_id = @d1 AND i.name IN (N'儒利亞諾・羅德里格斯', N'托馬斯・卡斯泰洛', N'瑪蒂諾', N'江奕璠')) <> 4 THROW 50027, N'四位教練未全部掛在一線隊 D1', 1;


/* ───────── ③ 里程碑 49 筆 ───────── */
DECLARE @m TABLE (id uniqueidentifier NOT NULL, happened_on date NOT NULL, sort_order int NOT NULL, zh_title nvarchar(200) NOT NULL, zh_desc nvarchar(max) NULL, en_title nvarchar(200) NOT NULL, en_desc nvarchar(max) NULL);
INSERT INTO @m (id, happened_on, sort_order, zh_title, zh_desc, en_title, en_desc) VALUES
  ('c8612b90-46c7-5fb3-ae80-2da8dea9cec7', '2024-06-17', 0, N'南投縣信義鄉雙龍國民小學：捐贈足球裝備', NULL, N'Football equipment donated to 南投縣信義鄉雙龍國民小學', NULL),
  ('4b806679-d106-55bc-96e5-542025b163cd', '2024-06-17', 1, N'南投縣鹿谷鄉鳳凰國民小學：捐贈足球裝備', NULL, N'Football equipment donated to 南投縣鹿谷鄉鳳凰國民小學', NULL),
  ('a9ec9d14-7fb8-5879-b44e-218751a45280', '2024-07-01', 2, N'U16 梯隊：赴日本參加邀請賽', N'7 月', N'U16 squad travels to Japan for an invitational tournament', N'July'),
  ('0226b7ac-cdf3-5f37-81da-10b123d320b8', '2024-07-17', 3, N'與臺中市立潭秀國民中學簽訂合作備忘錄', NULL, N'Signs a memorandum of cooperation with 臺中市立潭秀國民中學', NULL),
  ('c0115a8c-f232-52a4-8e77-56c419ca4178', '2024-08-23', 4, N'與嶺東高中簽訂合作備忘錄', NULL, N'Signs a memorandum of cooperation with 嶺東高中', NULL),
  ('8c2e6ee1-226a-50cc-82c1-10ce42dab605', '2024-08-24', 5, N'企業乙級聯賽：開賽', NULL, N'National Second Division season kicks off', NULL),
  ('149a72e5-2b34-5013-a4ab-df863ec54682', '2024-09-01', 6, N'U15 青年聯賽／U18 青年聯賽：開賽', N'9 月', N'U15 and U18 youth league seasons kick off', N'September'),
  ('2b6527e0-e9c9-5fa7-ab37-2ac5649fb393', '2024-09-05', 7, N'潭秀國中：日文課補習開始', NULL, N'Japanese-language classes begin at 潭秀國中', NULL),
  ('1caa19cc-8978-5b4d-bdf8-47bec833bd7e', '2024-09-14', 8, N'嶺東高中：日文課補習開始', NULL, N'Japanese-language classes begin at 嶺東高中', NULL),
  ('0554ee9d-2377-561c-8e53-0379fb913724', '2024-10-21', 9, N'TCRFC 一隊：香港移地訓練', N'10 月 21 日至 25 日', N'First Team training camp in Hong Kong', N'21–25 October'),
  ('2a5e0735-9670-5082-95fb-114fd8253c83', '2024-11-01', 10, N'贊助臺中市立潭秀國民中學：參加 2024 年青少年盃', N'11 月', N'Sponsors 臺中市立潭秀國民中學 to take part in the 2024 Youth Cup', N'November'),
  ('038bc6e8-0cc9-5402-b9c6-71d2ca023f45', '2024-11-04', 11, N'與西班牙 RC Alcobendas 簽訂國際足球合作備忘錄', NULL, N'Signs an international football cooperation memorandum with Spain''s RC Alcobendas', NULL),
  ('f055b054-0089-5928-9810-33289ede6d9d', '2024-11-16', 12, N'一隊：管理自我財富課程', NULL, N'First Team attends a personal wealth management course', NULL),
  ('8e4b5d3d-f892-5015-bbc4-c00a06c0ac64', '2024-11-19', 13, N'南投縣信義鄉雙龍國民小學：增設獎學金', NULL, N'Scholarship added at 南投縣信義鄉雙龍國民小學', NULL),
  ('4312ed5d-e70b-52f4-bec9-013c8d016455', '2024-12-07', 14, N'企業乙級聯賽：冠軍', NULL, N'National Second Division champions', NULL),
  ('728dd810-28f9-5c85-9160-ce3a68fa7465', '2024-12-13', 15, N'企業甲級俱樂部：認證通過', NULL, N'Top-tier club accreditation approved', NULL),
  ('124ed86b-be65-56bd-b0ef-1aa7f0d759bc', '2025-01-06', 16, N'一隊 12 位球員：赴香港超級聯賽（東方＆九龍城）測試及訓練', N'1 月 6 日至 11 日', N'12 First Team players go to Hong Kong for trials and training with Hong Kong Premier League clubs (東方 and 九龍城)', N'6–11 January'),
  ('3acb1e94-3404-5709-b1ae-7cecf317b0e8', '2025-01-07', 17, N'與台中市政府運動局達成長期合作共識', NULL, N'Reaches a long-term cooperation agreement with the Taichung City Government Sports Bureau', NULL),
  ('b52a018d-1726-5c97-8625-db3cc3c9acf2', '2025-01-10', 18, N'裁判講習 16 人', N'1 月 10 日至 12 日', N'Referee workshop for 16 participants', N'10–12 January'),
  ('e3e2a3c4-3af6-5f3b-9d15-b99174c34270', '2025-01-17', 19, N'教練講習 16 人', N'1 月 17 日至 19 日', N'Coaching workshop for 16 participants', N'17–19 January'),
  ('1edc7d70-7562-50bd-8ba2-719dd132e9d9', '2025-02-08', 20, N'TCRFC 一隊：日本關東移地訓練', N'2 月 8 日至 16 日', N'First Team training camp in the Kanto region, Japan', N'8–16 February'),
  ('7da583c9-c751-5ad7-a97d-a6ed0da7c994', '2025-02-22', 21, N'季前友誼賽 vs 台灣鋼鐵', NULL, N'Pre-season friendly vs 台灣鋼鐵', NULL),
  ('e1d8a391-8164-5ff0-b960-c24c5c4764c6', '2025-02-28', 22, N'公開選拔測試', NULL, N'Open selection trials', NULL),
  ('3e153186-d031-55f6-b421-d53e3916ad9b', '2025-03-14', 23, N'季前 FUTURO 友誼賽', NULL, N'Pre-season friendly vs FUTURO', NULL),
  ('748a9ad3-083e-5d70-b2b4-a7537939ccd2', '2025-03-15', 24, N'企業乙級：冠軍慶功宴', NULL, N'National Second Division championship celebration dinner', NULL),
  ('d9359657-a5a8-5188-acdc-14de522469a3', '2025-03-16', 25, N'一隊 4 位球員赴日本東京農大學訓練', N'3 月 16 日至 31 日', N'4 First Team players train at Tokyo University of Agriculture, Japan', N'16–31 March'),
  ('5455bdd2-9b26-5531-8bae-00d77852b3ae', '2025-03-16', 26, N'一隊 2 位球員赴西班牙 RC Alcobendas 訓練', N'3 月 16 日至 4 月 8 日', N'2 First Team players train at Spain''s RC Alcobendas', N'16 March – 8 April'),
  ('5e63a9d7-f747-5c92-9dbc-0fc3ca3e9eda', '2025-05-17', 27, N'晉身總統盃八強賽', NULL, N'Reaches the President''s Cup quarter-finals', NULL),
  ('dd5d9133-308a-5bc1-be41-cb7b0768603d', '2025-06-12', 28, N'出席南投縣雙龍國小畢業典禮', NULL, N'Attends the graduation ceremony at 南投縣雙龍國小', NULL),
  ('8ad7f383-7bfb-5b09-8a23-584cebf7aed9', '2025-06-20', 29, N'TCRFC × PULSE FIT：啟動與 pulsefit tw 的合作計劃', NULL, N'TCRFC × PULSE FIT: launches a cooperation program with pulsefit tw', NULL),
  ('0a5143e3-3512-560f-90c2-0b6cfa36e074', '2025-07-25', 30, N'參加在新加坡 Speed7 七人足球國際賽事，挺進盃賽八強', NULL, N'Reaches the Cup quarter-finals at the Speed7 seven-a-side international tournament in Singapore', NULL),
  ('6b819d9e-9a72-5319-adc3-9039acc6fbe7', '2025-07-27', 31, N'主辦首屆台中磐石國際足球盃', NULL, N'Hosts the first Taichung Rock FC International Cup', NULL),
  ('fed6f5e7-7fc6-5ed0-a3b7-3fef8e3875dc', '2025-07-28', 32, N'建立德國 Rot Weiss Ahlen 合作', NULL, N'Establishes a partnership with Germany''s Rot Weiss Ahlen', NULL),
  ('41b92103-a7f7-5247-a5ad-551a299ec678', '2025-07-29', 33, N'與義甲維羅納 Hellas Verona FC 建立合作，簽署 MoU', NULL, N'Signs an MoU establishing a partnership with Serie A club Hellas Verona FC', NULL),
  ('f372fecf-a523-5ec2-9c6b-b761c0be74be', '2025-11-01', 34, N'宣布陳曉明出任技術顧問，規劃青訓學院', NULL, N'Announces 陳曉明 as Technical Adviser to plan the youth academy', NULL),
  ('89eb8172-7df3-5e9b-b636-7b7d6d9a8c23', '2026-02-10', 35, N'U12 梯隊公開選拔', NULL, N'Open selection trials for the U12 squad', NULL),
  ('0fb2110d-9f9e-53c6-9c75-6bdf47df9402', '2026-04-19', 36, N'企甲第 16 週面對台中 FUTURO，台中磐石以 1 比 0 勝出，隊史首場台中德比勝利', NULL, N'Week 16 of the Enterprise Premier League: Taichung Rock FC beat 台中 FUTURO 1-0, the club''s first Taichung derby win', NULL),
  ('4fd9785c-93f9-5973-aa3c-e9b170395264', '2026-05-24', 37, N'球隊 19 戰積 27 分、排名第 5，完成 2025/26 企甲賽季', NULL, N'Finishes the 2025/26 Enterprise Premier League season in 5th place with 27 points from 19 matches', NULL),
  ('85445551-719b-5eb3-8fe3-14cee0e6f7ec', '2026-05-24', 38, N'台中磐石足球節圓滿落幕，10 支來自中部各地的球隊參賽', NULL, N'Taichung Rock FC Football Festival concludes successfully, with 10 teams from across central Taiwan taking part', NULL),
  ('6cb109db-9cd1-5c1e-8e73-56a458ed1356', '2026-06-05', 39, N'台中磐石首屆成人足球班已圓滿落幕', NULL, N'The first Taichung Rock FC adult football class concludes successfully', NULL),
  ('ff86bb1c-8098-55bb-8e2d-185cc2378b1a', '2026-06-06', 40, N'U12 獲恆山建設支持，前往德國訓練兩星期，參加 JoPa Summer Cup', NULL, N'U12 squad, supported by 恆山建設, travels to Germany for two weeks of training and the JoPa Summer Cup', NULL),
  ('e01e7bac-8372-5607-9769-8f841d17bc4c', '2026-06-09', 41, N'確認續留 2026/27 企甲', NULL, N'Confirmed to remain in the Enterprise Premier League for 2026/27', NULL),
  ('3a5aa861-b274-5505-98e2-61559ed80fdb', '2026-06-14', 42, N'U12 在銀牌組以第八名完成德國 JoPa Summer Cup', NULL, N'U12 squad finishes eighth in the Silver Group at the JoPa Summer Cup in Germany', NULL),
  ('ef9180a5-5c1e-5fe5-93b4-e66335159646', '2026-07-08', 43, N'劉建緯、山內大空、施靖堂和胡淯翔前往捷克，加入合作俱樂部 MFK Vítkovice 的訓練', NULL, N'劉建緯, 山內大空, 施靖堂 and 胡淯翔 travel to the Czech Republic to train with partner club MFK Vítkovice', NULL),
  ('013584d2-1d7d-53c9-b603-e80edc5d1fb2', '2026-07-18', 44, N'音樂節台秋祭 × 台中磐石｜三對三足球擂台王爭霸戰', NULL, N'Music festival 台秋祭 × Taichung Rock FC: 3-on-3 football showdown', NULL),
  ('d5c69a06-a363-5bc5-b8ce-7492709210c0', '2026-07-30', 45, N'北美國際蒙特梭利教育機構與台中磐石青訓學院協辦的「北美足球夏令營」圓滿落幕', NULL, N'The "North America Football Summer Camp", co-organised by 北美國際蒙特梭利教育機構 and the Taichung Rock FC youth academy, concludes successfully', NULL),
  ('d4d85a16-bfb9-5900-aeeb-8917cc600cda', '2026-08-03', 46, N'2026 安聯人壽－安聯小小世界盃 U12 全國亞軍', NULL, N'Runners-up at the 2026 安聯人壽 – 安聯小小世界盃 U12 national tournament', NULL),
  ('414dcc58-cc81-5d42-8cc0-50ccdf82b10b', '2026-08-04', 47, N'U12 前往斯洛伐克交流一週，與當地職業俱樂部 AS Trenčín 就青訓體系、教練培育及球員發展等面向進行深度交流', NULL, N'U12 squad spends a week in Slovakia, holding in-depth exchanges with local professional club AS Trenčín on youth development systems, coach education and player development', NULL),
  ('255ba97e-e791-5d60-a39c-b7a30bf241ca', '2026-08-23', 48, N'新光人壽 × 台中藍鯨 × 台中磐石 2026 新光小小足球嘉年華', NULL, N'2026 Shin Kong Little Football Carnival with 新光人壽 × Taichung Blue Whale × Taichung Rock FC', NULL);
DECLARE @msNew int, @msI18n int;
INSERT INTO milestones (id, club_id, happened_on, sort_order, is_visible)
  SELECT m.id, @club, m.happened_on, m.sort_order, 1 FROM @m m WHERE NOT EXISTS (SELECT 1 FROM milestones x WHERE x.id = m.id);
SET @msNew = @@ROWCOUNT;
INSERT INTO milestones_i18n (milestone_id, locale, title, description)
  SELECT m.id, @zh, m.zh_title, m.zh_desc FROM @m m WHERE EXISTS (SELECT 1 FROM milestones x WHERE x.id = m.id)
    AND NOT EXISTS (SELECT 1 FROM milestones_i18n i WHERE i.milestone_id = m.id AND i.locale = @zh);
SET @msI18n = @@ROWCOUNT;
INSERT INTO milestones_i18n (milestone_id, locale, title, description)
  SELECT m.id, @en, m.en_title, m.en_desc FROM @m m WHERE EXISTS (SELECT 1 FROM milestones x WHERE x.id = m.id)
    AND NOT EXISTS (SELECT 1 FROM milestones_i18n i WHERE i.milestone_id = m.id AND i.locale = @en);
SET @msI18n += @@ROWCOUNT;
PRINT CONCAT(N'milestones: new=', @msNew, N' i18n_new=', @msI18n);
IF (SELECT COUNT(*) FROM @m m JOIN milestones x ON x.id = m.id AND x.club_id = @club) <> 49 THROW 50039, N'里程碑未全部就位（預期 49 筆）', 1;
IF (SELECT COUNT(*) FROM @m m JOIN milestones_i18n i ON i.milestone_id = m.id) <> 98 THROW 50038, N'里程碑雙語列不是 98', 1;


/* ───────── ④ FAQ 15 題 ───────── */
DECLARE @f TABLE (slug nvarchar(160) NOT NULL, sort_order int NOT NULL, zh_q nvarchar(500) NOT NULL, zh_a nvarchar(max) NOT NULL, en_q nvarchar(500) NOT NULL, en_a nvarchar(max) NOT NULL);
DECLARE @fc TABLE (slug nvarchar(160) NOT NULL, cat nvarchar(160) NOT NULL);
INSERT INTO @f (slug, sort_order, zh_q, zh_a, en_q, en_a) VALUES
  (N'about-the-club', 10, N'台中磐石足球俱樂部是什麼樣的俱樂部？', N'台中磐石足球俱樂部（Taichung Rock FC）是立足台中、致力於推動在地足球發展的足球俱樂部。球隊名稱「磐石」象徵穩固的基石與長期深耕的精神；俱樂部於 2024 年成立，並在該年度奪下台灣乙級足球聯賽冠軍。', N'What kind of club is Taichung Rock FC?', N'Taichung Rock FC is a football club based in Taichung and dedicated to developing local football. The name "Rock" stands for a solid foundation and a spirit of long-term commitment. The club was founded in 2024 and won the National Second Division title in that same year.'),
  (N'competitive-teams', 11, N'台中磐石是否有職業或競技隊伍？', N'有。俱樂部設有成年隊（一線隊）並參與中華民國足球協會台灣企業甲級足球聯賽，也持續發展預備隊與青少年培訓體系。最新賽程、賽果、球員資訊及球隊消息，請以官方網站「比賽」及「球隊」頁面公告為準。', N'Does Taichung Rock FC have professional or competitive teams?', N'Yes. The club has an adult team (the First Team) that competes in the Enterprise Premier League organised by the Chinese Taipei Football Association, and it continues to develop a reserve team and a youth development system. For the latest fixtures, results, player information and team news, please refer to the announcements on the Schedule and Football Club pages of the official website.'),
  (N'development-direction', 12, N'俱樂部的發展方向是什麼？', N'台中磐石以在地化、長期人才培育與運動科學導向的足球發展為核心。俱樂部已推動青訓學院，規劃長期培訓課程及內部教練培育制度。', N'What is the club''s development direction?', N'Taichung Rock FC is built around localisation, long-term talent development and sports-science-led football development. The club has launched a youth academy and is planning long-term training programs and an in-house coach education system.'),
  (N'how-to-register', 13, N'如何報名青訓、足球課程或營隊？', N'青訓課程、營隊、體驗活動及梯隊招生，將依各期計畫於官方網站及社群平台公告。請留意活動頁面的年齡資格、訓練地點、時段、費用、名額、報名截止日及取消／退費規定；如有疑問，請透過官方聯絡管道洽詢。', N'How do I register for youth development, football courses or camps?', N'Youth development courses, camps, trial activities and squad recruitment will be announced on the official website and social media platforms according to each program. Please check the age eligibility, training venue, schedule, fees, number of places, registration deadline and cancellation/refund rules on the activity page; if you have questions, please contact us through the official contact channels.'),
  (N'academy-regular-enrolment', 14, N'俱樂部目前是否已開放青訓學院常態招生？', N'俱樂部已公開青訓學院及長期培訓方向，但常態班別、年齡分組、收費方式與正式招生時程，仍應以官方網站及官方社群的最新公告為準。請勿以非官方轉傳訊息作為報名或付款依據。', N'Has the club opened regular enrolment for the youth academy?', N'The club has publicly announced the youth academy and its long-term development direction, but regular classes, age groups, fee structure and the formal enrolment schedule are still subject to the latest announcements on the official website and official social media. Please do not rely on unofficial forwarded messages for registration or payment.'),
  (N'player-trial-application', 15, N'想參加球員試訓，該如何申請？', N'如俱樂部開放成年隊、預備隊或青訓梯隊試訓，將公告申請資格、報名方式、試訓日期、地點及應備資料。一般而言，申請者可能需提供基本資料、足球經歷、目前所屬球隊或學校、比賽影片及緊急聯絡資訊；實際要求仍以當次試訓公告為準。', N'How do I apply for a player trial?', N'If the club opens trials for the adult team, reserve team or youth squads, it will announce the eligibility, how to apply, trial dates, venue and required documents. In general, applicants may be asked to provide basic personal information, football background, current team or school, match footage and emergency contact details; the actual requirements are subject to the announcement for each trial.'),
  (N'no-match-experience', 16, N'沒有正式比賽經歷，也可以參與課程或活動嗎？', N'可以。不同課程、營隊或選拔活動的入門門檻不同。建議家長或球員先詳閱活動公告，確認年齡、經驗與程度要求；如為初學者，可優先選擇標示為體驗、基礎或普及足球導向的課程。', N'Can I join courses or activities without formal match experience?', N'Yes. The entry level differs between courses, camps and selection events. We recommend that parents or players read the activity announcement first to confirm the age, experience and level requirements; beginners can choose courses marked as introductory, foundation or mass-participation oriented.'),
  (N'minors-parental-consent', 17, N'未成年球員報名需要家長同意嗎？', N'需要。未成年球員參與課程、營隊、試訓、比賽或其他活動時，原則上須由法定代理人完成報名、個資同意、緊急聯絡及健康資訊確認；如活動涉及保險、住宿、跨縣市或海外交流，可能另需補充文件。', N'Do minors need parental consent to register?', N'Yes. When a minor takes part in a course, camp, trial, match or other activity, the legal guardian must generally complete the registration, personal-data consent, emergency contact and health information confirmation. If an activity involves insurance, accommodation, travel between cities or overseas exchange, additional documents may be required.'),
  (N'adult-team-schedule', 18, N'如何查詢成年隊賽程？', N'請至官方網站「比賽」頁面、最新消息或官方社群查詢。賽程可能因主辦單位安排、天候、場地或轉播需求調整，建議於出發前再次確認比賽日期、開球時間、場地與入場資訊。', N'How can I check the adult team''s schedule?', N'Please check the Schedule page on the official website, the latest news or the official social media. Fixtures may change due to organiser arrangements, weather, venue or broadcast requirements, so we recommend confirming the match date, kick-off time, venue and admission information again before you set out.'),
  (N'hosted-tournaments', 19, N'台中磐石是否主辦青少年或國際足球賽事？', N'是。俱樂部曾主辦台中磐石盃足球邀請賽，並辦理台中磐石國際足球盃相關活動。', N'Does Taichung Rock FC host youth or international football tournaments?', N'Yes. The club has hosted the Taichung Rock FC Cup invitational tournament and has organised events related to the Taichung Rock FC International Cup.'),
  (N'match-tickets', 20, N'比賽是否需要購票？', N'各賽事的入場方式、票價、索票、購票連結與觀賽規範，將依主辦單位及場地規定公告。有些賽事可能免費入場，有些則可能採購票、邀請或登記方式，請以該場次公告為準。', N'Do I need a ticket to attend matches?', N'Admission arrangements, ticket prices, ticket collection, purchase links and spectator rules for each event will be announced according to the organiser''s and venue''s regulations. Some matches may be free to enter, while others may require tickets, invitations or registration; please follow the announcement for each match.'),
  (N'bring-items-to-stadium', 21, N'可以攜帶相機、旗幟或加油用品進場嗎？', N'請依各比賽場地及主辦單位規定辦理。一般而言，禁止攜帶危險物品、煙火、雷射筆、玻璃瓶、違禁標語或可能影響賽事安全之物品；專業攝影、商業拍攝或大型器材，可能需事先取得核准。', N'Can I bring cameras, flags or supporter items into the stadium?', N'Please follow the rules of each venue and organiser. In general, dangerous items, fireworks, laser pointers, glass bottles, prohibited banners or anything that could affect match safety may not be brought in; professional photography, commercial filming or large equipment may require prior approval.'),
  (N'contact-the-club', 22, N'如何聯絡台中磐石足球俱樂部？', N'您可透過俱樂部官方網站、Facebook 或 Instagram 提供的官方聯絡管道洽詢。俱樂部官方頁面所列地址為：台中市北屯區昌平路二段敬固巷11弄41號。聯絡窗口、服務時間及 Email 如有更新，將以官方網站公告為準。', N'How can I contact Taichung Rock FC?', N'You can reach us through the official contact channels provided on the club''s official website, Facebook or Instagram. The address listed on the club''s official page is 台中市北屯區昌平路二段敬固巷11弄41號, Taichung. Any update to contact persons, service hours or email will be announced on the official website.'),
  (N'partnership-sponsorship-charity', 23, N'如何申請企業合作、贊助或公益活動？', N'歡迎企業、學校、政府機關、運動單位及社區組織提出合作構想。請透過官方聯絡管道提供合作目的、預計期間、活動形式、預算或資源需求及聯絡人資訊；俱樂部將依合作內容評估回覆。', N'How do I apply for corporate partnership, sponsorship or charity activities?', N'Businesses, schools, government agencies, sports organisations and community groups are welcome to propose partnership ideas. Please send us the purpose of the partnership, expected period, form of activity, budget or resource needs and contact details through the official contact channels; the club will assess and reply based on the proposal.'),
  (N'media-filming-brand-use', 24, N'媒體採訪、拍攝或使用俱樂部識別，應如何申請？', N'媒體採訪、商業拍攝、球員訪問、肖像使用、隊徽使用、商標授權及比賽影像使用，均應事先取得俱樂部書面同意。未經授權，請勿使用俱樂部隊徽、名稱、球員肖像、官方照片、影片或其他品牌素材從事商業用途。', N'How do I apply for media interviews, filming or use of the club''s identity?', N'Media interviews, commercial filming, player interviews, use of likenesses, use of the club crest, trademark licensing and use of match footage all require the club''s prior written consent. Without authorisation, please do not use the club''s crest, name, player likenesses, official photos, videos or other brand assets for commercial purposes.');
INSERT INTO @fc (slug, cat) VALUES
  (N'about-the-club', N'other'),
  (N'competitive-teams', N'other'),
  (N'development-direction', N'other'),
  (N'how-to-register', N'academy-admission'),
  (N'how-to-register', N'programs-camps'),
  (N'academy-regular-enrolment', N'academy-admission'),
  (N'player-trial-application', N'trials'),
  (N'player-trial-application', N'join-team'),
  (N'no-match-experience', N'programs-camps'),
  (N'minors-parental-consent', N'programs-camps'),
  (N'minors-parental-consent', N'trials'),
  (N'adult-team-schedule', N'other'),
  (N'hosted-tournaments', N'other'),
  (N'match-tickets', N'other'),
  (N'bring-items-to-stadium', N'other'),
  (N'contact-the-club', N'other'),
  (N'partnership-sponsorship-charity', N'partnerships-sponsorship'),
  (N'media-filming-brand-use', N'partnerships-sponsorship');
DECLARE @fqNew int, @fqI18n int, @fqLink int;
IF (SELECT COUNT(DISTINCT cat) FROM @fc c JOIN faq_categories fc ON fc.slug = c.cat) <> (SELECT COUNT(DISTINCT cat) FROM @fc) THROW 50040, N'FAQ 分類 slug 在 faq_categories 找不到', 1;
DECLARE @newf TABLE (id uniqueidentifier NOT NULL, slug nvarchar(160) NOT NULL);
INSERT INTO faqs (id, club_id, slug, sort_order, status)
  OUTPUT inserted.id, inserted.slug INTO @newf (id, slug)
  SELECT NEWID(), @club, f.slug, f.sort_order, N'published' FROM @f f WHERE NOT EXISTS (SELECT 1 FROM faqs x WHERE x.club_id = @club AND x.slug = f.slug);
SET @fqNew = @@ROWCOUNT;
INSERT INTO faqs_i18n (faq_id, locale, question, answer)
  SELECT x.id, @zh, f.zh_q, f.zh_a FROM @f f JOIN faqs x ON x.club_id = @club AND x.slug = f.slug
  WHERE NOT EXISTS (SELECT 1 FROM faqs_i18n i WHERE i.faq_id = x.id AND i.locale = @zh);
SET @fqI18n = @@ROWCOUNT;
INSERT INTO faqs_i18n (faq_id, locale, question, answer)
  SELECT x.id, @en, f.en_q, f.en_a FROM @f f JOIN faqs x ON x.club_id = @club AND x.slug = f.slug
  WHERE NOT EXISTS (SELECT 1 FROM faqs_i18n i WHERE i.faq_id = x.id AND i.locale = @en);
SET @fqI18n += @@ROWCOUNT;
-- 分類關聯只替「本次新建」的題目補（已存在的題目，分類可能已被後台調整過，不動）
INSERT INTO faq_category_links (faq_id, faq_category_id)
  SELECT nf.id, fc.id FROM @fc c JOIN @newf nf ON nf.slug = c.slug JOIN faq_categories fc ON fc.slug = c.cat;
SET @fqLink = @@ROWCOUNT;
PRINT CONCAT(N'faqs: new=', @fqNew, N' i18n_new=', @fqI18n, N' links_new=', @fqLink);
IF (SELECT COUNT(*) FROM @f f JOIN faqs x ON x.club_id = @club AND x.slug = f.slug) <> 15 THROW 50049, N'FAQ 未全部就位（預期 15 題）', 1;
IF (SELECT COUNT(*) FROM @f f JOIN faqs x ON x.club_id = @club AND x.slug = f.slug JOIN faqs_i18n i ON i.faq_id = x.id) <> 30 THROW 50048, N'FAQ 雙語列不是 30', 1;
IF (SELECT COUNT(*) FROM @f f JOIN faqs x ON x.club_id = @club AND x.slug = f.slug WHERE NOT EXISTS (SELECT 1 FROM faq_category_links l WHERE l.faq_id = x.id)) > 0 THROW 50047, N'有 FAQ 沒有任何分類', 1;


/* ───────── ⑤ 慈善計畫 3 個（＋受贈公益團體 3 筆） ───────── */
DECLARE @ch TABLE (slug nvarchar(160) NOT NULL, zh_name nvarchar(128) NOT NULL, en_name nvarchar(128) NOT NULL);
INSERT INTO @ch (slug, zh_name, en_name) VALUES
  (N'tantzu-lingtung-joint-squad', N'潭秀國中暨嶺東高中聯隊', N'潭秀國中 and 嶺東高中 joint squad'),
  (N'tantzu-nonprofit-kindergarten', N'潭秀非營利幼兒園', N'潭秀非營利幼兒園'),
  (N'tcrfc-cup-youth-teams', N'台中磐石盃少年足球隊伍', N'Taichung Rock FC Cup youth football teams');
DECLARE @chNew int, @chI18n int, @cpNew int, @cpI18n int;
INSERT INTO charities (id, club_id, slug)
  SELECT NEWID(), @club, c.slug FROM @ch c WHERE NOT EXISTS (SELECT 1 FROM charities x WHERE x.club_id = @club AND x.slug = c.slug);
SET @chNew = @@ROWCOUNT;
INSERT INTO charities_i18n (charity_id, locale, name)
  SELECT x.id, @zh, c.zh_name FROM @ch c JOIN charities x ON x.club_id = @club AND x.slug = c.slug
  WHERE NOT EXISTS (SELECT 1 FROM charities_i18n i WHERE i.charity_id = x.id AND i.locale = @zh);
SET @chI18n = @@ROWCOUNT;
INSERT INTO charities_i18n (charity_id, locale, name)
  SELECT x.id, @en, c.en_name FROM @ch c JOIN charities x ON x.club_id = @club AND x.slug = c.slug
  WHERE NOT EXISTS (SELECT 1 FROM charities_i18n i WHERE i.charity_id = x.id AND i.locale = @en);
SET @chI18n += @@ROWCOUNT;
IF (SELECT COUNT(*) FROM @ch c JOIN charities x ON x.club_id = @club AND x.slug = c.slug) <> 3 THROW 50050, N'公益團體未全部就位（預期 3 筆）', 1;

DECLARE @cp TABLE (slug nvarchar(160) NOT NULL, charity_slug nvarchar(160) NOT NULL, sort_order int NOT NULL, zh_name nvarchar(128) NOT NULL, en_name nvarchar(128) NOT NULL, zh_content nvarchar(max) NOT NULL, en_content nvarchar(max) NOT NULL);
INSERT INTO @cp (slug, charity_slug, sort_order, zh_name, en_name, zh_content, en_content) VALUES
  (N'youth-football-international-exchange', N'tantzu-lingtung-joint-squad', 10, N'青少年足球國際交流支持計畫', N'Youth Football International Exchange Support Program', N'{"blocks":[{"type":"paragraph","text":"計畫定位：支持台中基層球員參與海外足球訓練及競賽，增加國際交流經驗，並提升青少年球員的技術、比賽理解與職涯視野。"},{"type":"paragraph","text":"已公開執行內容：2024 年，台中磐石全額贊助由潭秀國中與嶺東高中組成的聯隊赴日本茨城縣參加第七屆 Felice CUP U16。參賽團共 18 名球員、2 名教練；因應 U16 賽事年齡條件，另由嶺東高中借調 5 名菁英球員同行。賽程為 7 月 23 日至 28 日，前兩日由 J 聯盟教練帶領訓練，後續進行賽事交流。"}]}', N'{"blocks":[{"type":"paragraph","text":"Program aim: Supporting grassroots players in Taichung to take part in overseas football training and competition, broadening their international experience and strengthening young players'' technical skills, understanding of the game and career outlook."},{"type":"paragraph","text":"Publicly reported activity: In 2024, Taichung Rock FC fully sponsored a joint squad of 潭秀國中 and 嶺東高中 to attend the 7th Felice CUP U16 in Ibaraki Prefecture, Japan. The delegation comprised 18 players and 2 coaches; to meet the U16 age requirement, 5 elite players were also loaned from 嶺東高中. The trip ran from 23 to 28 July: the first two days were training led by J.League coaches, followed by tournament matches."}]}'),
  (N'child-education-resources', N'tantzu-nonprofit-kindergarten', 20, N'兒少教育資源支持計畫', N'Educational Resources for Children Support Program', N'{"blocks":[{"type":"paragraph","text":"計畫定位：結合企業合作夥伴及在地教育單位，支持幼兒與兒童的閱讀、語言學習及成長需求。"},{"type":"paragraph","text":"已公開執行內容：台中磐石與 Subkarma 前往潭秀國中所屬的潭秀非營利幼兒園，捐贈英語幼童學習書籍《Cabbage & Tyler》。行動旨在回饋在地校園，為孩子的學習與成長提供支持與陪伴。"}]}', N'{"blocks":[{"type":"paragraph","text":"Program aim: Working with corporate partners and local educational institutions to support young children''s reading, language learning and growth."},{"type":"paragraph","text":"Publicly reported activity: Taichung Rock FC and Subkarma visited 潭秀非營利幼兒園, which is affiliated with 潭秀國中, to donate the English children''s learning books Cabbage & Tyler. The initiative aims to give back to local schools and to offer support and companionship for children''s learning and growth."}]}'),
  (N'taichung-rock-fc-cup-grassroots', N'tcrfc-cup-youth-teams', 30, N'台中磐石盃基層足球推廣計畫', N'Taichung Rock FC Cup Grassroots Football Promotion Program', N'{"blocks":[{"type":"paragraph","text":"計畫定位：以台中磐石盃為平台，提供少年球隊交流與比賽機會，並藉由家庭參與、球員互動及社區合作推廣基層足球。"},{"type":"paragraph","text":"已公開執行內容：2025 台中磐石盃於 5 月 3 日在台中太平區運動場舉行，由台中市政府運動局指導、台中磐石主辦、亞瑟運動俱樂部承辦、台中惠文高中協辦。活動設 U6、U8、U10、U12 組別，超過 45 支少年隊伍參加，並結合磐石球員表演及親子活動。"},{"type":"paragraph","text":"與台中藍鯨女子足球隊共同協助新光人壽舉辦2026新光小小足球嘉年華，讓運動從小扎根，引導孩子踏進球場、鼓勵家長共同參與，逐步將運動融入日常生活。"}]}', N'{"blocks":[{"type":"paragraph","text":"Program aim: Using the Taichung Rock FC Cup as a platform to give youth teams opportunities to compete and exchange, and promoting grassroots football through family participation, player interaction and community cooperation."},{"type":"paragraph","text":"Publicly reported activity: The 2025 Taichung Rock FC Cup took place on 3 May at the Taiping District sports ground in Taichung, guided by the Taichung City Government Sports Bureau, hosted by Taichung Rock FC, organised by 亞瑟運動俱樂部 and co-organised by 台中惠文高中. The event had U6, U8, U10 and U12 divisions, with more than 45 youth teams taking part, along with a performance by Rock FC players and parent-child activities."},{"type":"paragraph","text":"Together with the Taichung Blue Whale women''s football team, helped 新光人壽 stage the 2026 Shin Kong Little Football Carnival, rooting sport in children from an early age, drawing them onto the pitch and encouraging parents to take part, gradually making sport part of everyday life."}]}');
INSERT INTO charity_programs (id, club_id, slug, charity_id, status, sort_order)
  SELECT NEWID(), @club, p.slug, ch.id, N'published', p.sort_order
  FROM @cp p JOIN charities ch ON ch.club_id = @club AND ch.slug = p.charity_slug
  WHERE NOT EXISTS (SELECT 1 FROM charity_programs x WHERE x.club_id = @club AND x.slug = p.slug);
SET @cpNew = @@ROWCOUNT;
INSERT INTO charity_programs_i18n (charity_program_id, locale, name, content)
  SELECT x.id, @zh, p.zh_name, p.zh_content FROM @cp p JOIN charity_programs x ON x.club_id = @club AND x.slug = p.slug
  WHERE NOT EXISTS (SELECT 1 FROM charity_programs_i18n i WHERE i.charity_program_id = x.id AND i.locale = @zh);
SET @cpI18n = @@ROWCOUNT;
INSERT INTO charity_programs_i18n (charity_program_id, locale, name, content)
  SELECT x.id, @en, p.en_name, p.en_content FROM @cp p JOIN charity_programs x ON x.club_id = @club AND x.slug = p.slug
  WHERE NOT EXISTS (SELECT 1 FROM charity_programs_i18n i WHERE i.charity_program_id = x.id AND i.locale = @en);
SET @cpI18n += @@ROWCOUNT;
PRINT CONCAT(N'charities: new=', @chNew, N' i18n_new=', @chI18n, N' | charity_programs: new=', @cpNew, N' i18n_new=', @cpI18n);
IF (SELECT COUNT(*) FROM @cp p JOIN charity_programs x ON x.club_id = @club AND x.slug = p.slug) <> 3 THROW 50059, N'慈善計畫未全部就位（預期 3 個）', 1;
IF (SELECT COUNT(*) FROM @cp p JOIN charity_programs x ON x.club_id = @club AND x.slug = p.slug JOIN charity_programs_i18n i ON i.charity_program_id = x.id) <> 6 THROW 50058, N'慈善計畫雙語列不是 6', 1;


/* ───────── ⑥ 球員故事 3 篇（草稿） ───────── */
DECLARE @a TABLE (slug nvarchar(160) NOT NULL, player_zh nvarchar(64) NOT NULL, zh_title nvarchar(200) NOT NULL, en_title nvarchar(200) NOT NULL, zh_body nvarchar(max) NOT NULL, en_body nvarchar(max) NOT NULL);
INSERT INTO @a (slug, player_zh, zh_title, en_title, zh_body, en_body) VALUES
  (N'player-story-sun-enqi', N'孫恩祈', N'孫恩祈｜從台東出發的足球路', N'孫恩祈 | A football journey that started in Taitung', N'{"blocks":[{"type":"heading","text":"從台東基層開始"},{"type":"paragraph","text":"孫恩祈的足球故事從台東開始。"},{"type":"paragraph","text":"2016年，他代表台東區參加中華足協菁英計畫對抗賽，在U12組拿下最優秀球員獎，當時的報導特別提到他在盤球和射門上的表現。一個從台東基層出來的孩子，就這樣開始被台灣足球圈注意到。"},{"type":"paragraph","text":"進入豐田國中之後，他擔任球隊隊長。2020年中等學校足球聯賽國男組冠軍賽，豐田國中在延長賽和PK戰中擊敗尋求五連霸的黎明國中，為台東拿下隊史第一座全國冠軍。那場比賽，延長賽打到球員體能接近極限，孫恩祈一度被抬離場，但他還是想站起來，想繼續跟隊友一起踢完。"},{"type":"paragraph","text":"他後來分享過，嚴格的球隊管理讓他學會怎麼管理自己、約束自己，以及怎麼帶著一群人朝同一個方向走。技術可以練，但責任感、紀律、在逆境裡撐住的能力，這些東西，是另一種養成。"},{"type":"heading","text":"加入台中磐石"},{"type":"paragraph","text":"進入台中磐石後，孫恩祈以後衛身分加入一線隊體系。"},{"type":"paragraph","text":"2025年3月，俱樂部安排他和梁顥騰前往西班牙RC Alcobendas訓練，這是台中磐石與RC Alcobendas策略合作的一部分。這次不是走馬看花式的參訪，而是直接進入一線隊的日常訓練，更快的節奏、更強的身體對抗、更高的戰術要求和溝通壓力，全部一起來。"},{"type":"heading","text":"在西班牙爭取留下"},{"type":"paragraph","text":"到了西班牙，孫恩祈先在RC Alcobendas適應了一週，之後轉往西協丙球隊Mexico FC，以練習生身分參與訓練和戰術演練。"},{"type":"paragraph","text":"兩週下來，他的表現和態度獲得教練團肯定，Mexico FC正式邀請他留隊到該賽季結束。3月27日的隊內對抗賽，他用短傳策動進攻並製造進球，不是什麼轟動的大消息，但是在一個陌生環境裡，用場上的表現讓教練和隊友信任自己，這件事本身就不簡單。"},{"type":"paragraph","text":"這段時間他也看見了台灣和西班牙在訓練文化上的差距。西班牙的日常訓練通常先做重量訓練，再接兩個小時的正式足球課，其他時間由球員自己安排加強。更關鍵的是，每個人每天都在用表現證明自己，跑位、對抗、訓練細節，沒有人在混，因為位置不是給的，是搶的。"}]}', N'{"blocks":[{"type":"heading","text":"Starting out in grassroots Taitung"},{"type":"paragraph","text":"孫恩祈''s football story begins in Taitung."},{"type":"paragraph","text":"In 2016 he represented the Taitung region in the Chinese Taipei Football Association''s Elite Program matches and won the Most Valuable Player award in the U12 division; reports at the time singled out his dribbling and shooting. A child from grassroots Taitung began to be noticed by Taiwan''s football community."},{"type":"paragraph","text":"After entering 豐田國中 he became team captain. In the final of the 2020 secondary-school football league boys'' division, 豐田國中 beat 黎明國中, who were going for a fifth straight title, in extra time and a penalty shoot-out, winning Taitung''s first national championship. In that match, extra time pushed the players close to their physical limits and 孫恩祈 was carried off the pitch at one point, but he still wanted to stand up and keep playing with his teammates to the end."},{"type":"paragraph","text":"He later shared that the team''s strict management taught him how to manage and discipline himself, and how to lead a group in the same direction. Technique can be practised, but a sense of responsibility, discipline and the ability to hold firm in adversity are developed in a different way."},{"type":"heading","text":"Joining Taichung Rock FC"},{"type":"paragraph","text":"After joining Taichung Rock FC, 孫恩祈 entered the First Team system as a defender."},{"type":"paragraph","text":"In March 2025 the club arranged for him and 梁顥騰 to train at RC Alcobendas in Spain, as part of the strategic partnership between Taichung Rock FC and RC Alcobendas. This was not a brief look-around visit but direct entry into the first team''s daily training: a faster tempo, tougher physical contests, higher tactical demands and communication pressure, all at once."},{"type":"heading","text":"Fighting to stay in Spain"},{"type":"paragraph","text":"In Spain, 孫恩祈 first spent a week adjusting at RC Alcobendas, then moved to Mexico FC, a club at the 西協丙 level of Spanish football, where he joined training and tactical drills as a trainee."},{"type":"paragraph","text":"After two weeks, his performance and attitude earned the approval of the coaching staff, and Mexico FC formally invited him to stay until the end of the season. In the intra-squad match on 27 March he started attacks with short passes and created a goal. It was hardly sensational news, but earning the trust of coaches and teammates through performances on the pitch in an unfamiliar environment is no small thing."},{"type":"paragraph","text":"During this time he also saw the differences in training culture between Taiwan and Spain. Daily training in Spain usually starts with weight training, followed by a two-hour football session, with players arranging extra work themselves for the rest of the time. More importantly, everyone proves themselves through their performance every day, in their movement, their duels and the details of training. Nobody coasts, because a place is not given; it is won."}]}'),
  (N'player-story-yamauchi', N'山內大空', N'山內大空', N'山內大空', N'{"blocks":[{"type":"paragraph","text":"山內大空的案例，呈現台中磐石透過引進具日本高中、大學與成人俱樂部經驗的年輕外籍球員，提升一線隊競技強度，同時促進跨文化交流與職業標準的建立。他從日本福島尚志高中、青山學院大學與 Aventura Kawaguchi 出發，2025 年夏天赴台加入台中磐石，並在企業甲級聯賽以關鍵進球快速展現影響力。"},{"type":"heading","text":"在日本校園足球建立基礎"},{"type":"paragraph","text":"山內大空出身東京八王子，早期在FC杉野接受培養，之後進入日本具足球傳統的尚志高中。公開資料顯示，他在尚志高中曾穿上10號球衣，並在2019年全國高中綜合體育大會獲得優秀選手與得分王雙重肯定。這段高中階段的經歷，讓他在高競爭的日本校園足球體系中累積進球能力、比賽抗壓性與進攻球員應有的終結意識。"},{"type":"paragraph","text":"高中畢業後，山內大空進入青山學院大學足球隊，延續競技足球生涯。大學階段不只是技術與比賽的延伸，也讓他在求學與競技訓練之間累積時間管理、自我要求及團隊責任等能力。青山學院大學足球部與 Aventura Kawaguchi 的官方資訊，均確認他從尚志高中進入青山學院大學，之後於 2024 賽季加入 Aventura Kawaguchi。"},{"type":"heading","text":"從日本成人隊走向海外挑戰"},{"type":"paragraph","text":"大學畢業後，山內大空加盟埼玉縣的 Aventura Kawaguchi，進入日本成人社會人足球體系。球隊在他加盟公告中，將其列為前鋒，並確認他於2024賽季加入一線隊；這是他從學校足球過渡至成人競技的重要階段。"},{"type":"paragraph","text":"2025 年夏天，他選擇離開熟悉的日本環境，前往台灣加盟台中磐石。這個選擇代表他把生涯推向新的挑戰：不只是轉換球隊，而是必須適應新的生活文化、語言、隊友關係、訓練節奏與聯賽風格。根據公開報導，他在2025年7月決定赴台展開職業足球挑戰。"},{"type":"paragraph","text":"這種跨國發展的價值，在於球員必須離開既有支持系統，靠專業態度與場上表現建立新位置。對年輕球員而言，這也是從「在熟悉環境中競爭」進一步走向「在陌生環境中證明自己」的重要能力轉換。"},{"type":"heading","text":"以關鍵時刻回應信任"},{"type":"paragraph","text":"山內大空在台中磐石的故事，很快進入正式比賽考驗。2025/26 企甲聯賽對陽信北競一役，台中磐石開賽不久獲得十二碼機會，山內大空親自主罰破門，幫助球隊取得領先。比賽後段，球隊再次取得十二碼，他再度穩定操刀命中，完成梅開二度，最終雙方以2比2握手言和。"},{"type":"paragraph","text":"這兩球的意義不僅是進球數字。前鋒在關鍵壓力下是否能做出正確判斷、穩定執行，是競技層級的重要分水嶺。山內大空以兩次十二碼展現冷靜與責任感，也以賽後「應該贏下比賽、需要繼續改進」的態度，表達他對競技要求並不只停留在個人表現。"},{"type":"paragraph","text":"山內大空的故事說明，球員發展不只是在熟悉環境中累積成績，更在於勇於跨出舒適圈，在新的文化與競技舞台中，以表現贏得信任。"}]}', N'{"blocks":[{"type":"paragraph","text":"山內大空''s case shows how Taichung Rock FC raises the competitive intensity of the First Team by bringing in young foreign players with experience in Japanese high school, university and adult clubs, while also promoting cross-cultural exchange and the establishment of professional standards. Starting from 福島尚志高中 in Japan, 青山學院大學 and Aventura Kawaguchi, he came to Taiwan in the summer of 2025 to join Taichung Rock FC and quickly made his mark in the Enterprise Premier League with key goals."},{"type":"heading","text":"Building a foundation in Japanese school football"},{"type":"paragraph","text":"山內大空 grew up in Hachioji, Tokyo, was developed early on at FC Sugino, and then entered 尚志高中, a school with a strong football tradition in Japan. Public information shows that he wore the number 10 shirt at 尚志高中 and, at the 2019 national high school sports championships, was recognised both as an outstanding player and as top scorer. This high school period let him build goalscoring ability, resilience under match pressure and the finishing instinct an attacking player needs within Japan''s highly competitive school football system."},{"type":"paragraph","text":"After high school, 山內大空 joined the 青山學院大學 football team and continued his competitive football career. The university years were not only an extension of technique and match play; balancing study with competitive training also built his time management, self-discipline and sense of team responsibility. Official information from the 青山學院大學 football club and Aventura Kawaguchi both confirms that he moved from 尚志高中 to 青山學院大學 and then joined Aventura Kawaguchi for the 2024 season."},{"type":"heading","text":"From a Japanese adult team to an overseas challenge"},{"type":"paragraph","text":"After graduating from university, 山內大空 joined Aventura Kawaguchi in Saitama Prefecture and entered Japan''s adult club football system. In the announcement of his signing the team listed him as a forward and confirmed that he joined the first team for the 2024 season; this was an important stage in his transition from school football to adult competition."},{"type":"paragraph","text":"In the summer of 2025 he chose to leave the familiar environment of Japan and join Taichung Rock FC in Taiwan. The choice meant pushing his career towards a new challenge: not just changing teams, but adapting to a new way of life, language, relationships with teammates, training rhythm and league style. According to public reports, he decided in July 2025 to move to Taiwan and take on a professional football challenge."},{"type":"paragraph","text":"The value of this kind of international move lies in the fact that a player must leave his existing support system and establish a new place through a professional attitude and performances on the pitch. For a young player, it is also an important shift in capability, from \"competing in a familiar environment\" to \"proving yourself in an unfamiliar one\"."},{"type":"heading","text":"Repaying trust in the key moments"},{"type":"paragraph","text":"山內大空''s story at Taichung Rock FC was soon tested in competitive matches. In the 2025/26 Enterprise Premier League match against 陽信北競, Taichung Rock FC won a penalty shortly after kick-off, and 山內大空 took it himself and scored, putting the team ahead. Late in the match the team won another penalty and he again converted it calmly to score twice; the match ended in a 2-2 draw."},{"type":"paragraph","text":"The significance of these two goals goes beyond the numbers. Whether a forward can make the right decision and execute steadily under key pressure is an important dividing line at the competitive level. With two penalties 山內大空 showed composure and a sense of responsibility, and his post-match attitude, that the team \"should have won and needs to keep improving\", showed that his standards are not limited to his own performance."},{"type":"paragraph","text":"山內大空''s story shows that player development is not only about accumulating results in a familiar environment, but about daring to step out of the comfort zone and win trust through performance on a new cultural and competitive stage."}]}'),
  (N'player-story-yang-chaojing', N'楊朝景', N'楊朝景｜從台中校園到港超的前鋒之路', N'楊朝景 | A forward''s path from Taichung school football to the Hong Kong Premier League', N'{"blocks":[{"type":"heading","text":"從校園足球開始"},{"type":"paragraph","text":"楊朝景的足球路，從台中的校園系統走起。"},{"type":"paragraph","text":"就讀惠文高中期間，他協助球隊拿下中等學校11人制足球聯賽三連冠。高中畢業後升讀國立臺灣體育大學，同時進入台中磐石成人隊。這條路線讓他先在青少年賽事累積贏球的經驗和習慣，再去面對成人聯賽在速度、對抗和決策上完全不同層級的要求。"},{"type":"heading","text":"2024台乙：金靴、冠軍、升班"},{"type":"paragraph","text":"加入台中磐石之後，楊朝景在俱樂部創隊的第一個賽季就扮演了關鍵角色。"},{"type":"paragraph","text":"2024年台灣乙級足球聯賽，他出賽8場攻進9球，拿下聯賽金靴，也幫助球隊奪下台乙冠軍、升上企甲。19歲的前鋒，在俱樂部最重要的賽季裡扛起進攻核心的責任——這不只是個人數據的問題，而是在升班壓力下，學著承擔的過程。"},{"type":"heading","text":"國家隊的舞台"},{"type":"paragraph","text":"俱樂部賽事之外，楊朝景也逐步踏上國家隊舞台。"},{"type":"paragraph","text":"2024年3月，他首次代表中華台北成人代表隊出賽，累計2場國際A級賽經驗；U20隊也有4次出賽紀錄，對尼泊爾的比賽攻進1球。"},{"type":"paragraph","text":"對一個年輕球員來說，成人國家隊的徵召意義不只是榮譽。更重要的是提早進到更高強度的環境——更快的節奏、更強的對抗、更嚴格的戰術要求——在自己還沒完全準備好的時候，先去看看差距在哪裡。楊朝景的成長軌跡也說明了一件事：球員的發展從來不是直線，而是在校隊、俱樂部、青年國家隊和成人國家隊之間，一層一層被不同的挑戰打磨出來的。"},{"type":"heading","text":"去香港，靠表現爭取"},{"type":"paragraph","text":"旅外的機會不是從天上掉下來的。"},{"type":"paragraph","text":"台中磐石在香港移地訓練期間，安排多名球員與當地球隊接觸。楊朝景在測試中的表現獲得港超球隊九龍城的青睞，三方達成協議後，他正式前往香港發展。"},{"type":"paragraph","text":"出發前，他仍繼續跟著台中磐石訓練和比賽，維持競技狀態。這個細節其實很重要：海外機會要能抓住，前提是你在原本的球隊就沒有鬆懈，身體狀態、出賽節奏都在，才有辦法到了新環境馬上跟上。"},{"type":"heading","text":"在港超累積職業經驗"},{"type":"paragraph","text":"2024/25賽季，楊朝景在九龍城出場8次，包括3次先發、5次替補，合計343分鐘，交出2球3助攻的成績，對傑志和東方都有進球，最後一場港超比賽也完成助攻。"},{"type":"paragraph","text":"對一個第一次旅外的年輕前鋒來說，這些數字說明他不是去陪練的，而是真的在正式職業聯賽裡出賽、先發、進球。港超更快的節奏、更強的身體對抗、不同背景球員帶來的各種比賽要求，讓他更具體地理解，職業足球對前鋒的跑位、壓迫、決策速度和終結能力，要求的是什麼。"},{"type":"paragraph","text":"2026年，台中磐石再次安排楊朝景以租借方式回到九龍城，繼續在港超發展。俱樂部對外說明得很直接：讓他繼續累積競技經驗，為職業生涯打好更穩的底。"}]}', N'{"blocks":[{"type":"heading","text":"Starting in school football"},{"type":"paragraph","text":"楊朝景''s football path began in Taichung''s school system."},{"type":"paragraph","text":"While at 惠文高中 he helped the team win three consecutive titles in the secondary-school 11-a-side football league. After graduating he went on to 國立臺灣體育大學 while also joining the Taichung Rock FC adult team. This route let him first build the experience and habit of winning in youth competitions before facing the completely different demands of the adult league in speed, physical contests and decision-making."},{"type":"heading","text":"2024 National Second Division: Golden Boot, title, promotion"},{"type":"paragraph","text":"After joining Taichung Rock FC, 楊朝景 played a key role in the club''s very first season."},{"type":"paragraph","text":"In the 2024 National Second Division he scored 9 goals in 8 appearances, won the league Golden Boot and helped the team win the Second Division title and earn promotion to the Enterprise Premier League. A 19-year-old forward, he took on the responsibility of being the attacking core in the club''s most important season — and this was not just a matter of personal statistics but a process of learning to shoulder responsibility under the pressure of promotion."},{"type":"heading","text":"On the national team stage"},{"type":"paragraph","text":"Beyond club football, 楊朝景 has also gradually stepped onto the national team stage."},{"type":"paragraph","text":"In March 2024 he made his first appearance for the Chinese Taipei senior national team, and has since gained 2 international A-match caps; he also has 4 appearances for the U20 team, scoring 1 goal against Nepal."},{"type":"paragraph","text":"For a young player, a call-up to the senior national team is about more than honour. More important is entering a higher-intensity environment early — a faster tempo, tougher duels, stricter tactical demands — and seeing where the gaps are before you are fully ready. 楊朝景''s development also shows that a player''s growth is never a straight line, but is polished layer by layer by different challenges across the school team, the club, the youth national team and the senior national team."},{"type":"heading","text":"Going to Hong Kong, earning it through performance"},{"type":"paragraph","text":"Opportunities abroad do not fall from the sky."},{"type":"paragraph","text":"During the club''s training camp in Hong Kong, Taichung Rock FC arranged for several players to meet local teams. 楊朝景''s performance in the trials won the favour of Hong Kong Premier League club 九龍城, and after a three-party agreement he formally went to Hong Kong to continue his development."},{"type":"paragraph","text":"Before leaving, he continued to train and play with Taichung Rock FC to keep his competitive form. This detail is actually important: to seize an opportunity overseas, you must not have let up in your original team, and your fitness and match rhythm must be there, so that you can keep up straight away in the new environment."},{"type":"heading","text":"Building professional experience in the Hong Kong Premier League"},{"type":"paragraph","text":"In the 2024/25 season, 楊朝景 made 8 appearances for 九龍城, including 3 starts and 5 substitute appearances, totalling 343 minutes, with 2 goals and 3 assists. He scored against both 傑志 and 東方, and also provided an assist in his last Hong Kong Premier League match."},{"type":"paragraph","text":"For a young forward playing abroad for the first time, these numbers show that he was not there just to make up the numbers; he genuinely played, started and scored in a professional league. The Hong Kong Premier League''s faster tempo, tougher physical contests and the varied demands brought by players from different backgrounds let him understand more concretely what professional football requires of a forward in movement, pressing, speed of decision and finishing."},{"type":"paragraph","text":"In 2026, Taichung Rock FC again arranged for 楊朝景 to return to 九龍城 on loan to continue his development in the Hong Kong Premier League. The club''s public explanation was very direct: to let him keep accumulating competitive experience and lay a steadier foundation for his professional career."}]}');
DECLARE @acat uniqueidentifier = (SELECT id FROM article_categories WHERE code = N'player-stories');
IF @acat IS NULL THROW 50060, N'找不到文章分類 player-stories', 1;
DECLARE @arNew int, @arI18n int, @arRel int, @arRelSkipped int = 0;
DECLARE @newa TABLE (id uniqueidentifier NOT NULL, slug nvarchar(160) NOT NULL);
INSERT INTO articles (id, club_id, slug, article_category_id, status)
  OUTPUT inserted.id, inserted.slug INTO @newa (id, slug)
  SELECT NEWID(), @club, a.slug, @acat, N'draft' FROM @a a WHERE NOT EXISTS (SELECT 1 FROM articles x WHERE x.slug = a.slug);
SET @arNew = @@ROWCOUNT;
INSERT INTO articles_i18n (article_id, locale, title, body)
  SELECT x.id, @zh, a.zh_title, a.zh_body FROM @a a JOIN articles x ON x.slug = a.slug
  WHERE NOT EXISTS (SELECT 1 FROM articles_i18n i WHERE i.article_id = x.id AND i.locale = @zh);
SET @arI18n = @@ROWCOUNT;
INSERT INTO articles_i18n (article_id, locale, title, body)
  SELECT x.id, @en, a.en_title, a.en_body FROM @a a JOIN articles x ON x.slug = a.slug
  WHERE NOT EXISTS (SELECT 1 FROM articles_i18n i WHERE i.article_id = x.id AND i.locale = @en);
SET @arI18n += @@ROWCOUNT;
-- 關聯球員：只替「本次新建」的文章補（已存在的文章，關聯可能已被後台調整）；球員姓名在本俱樂部唯一才掛
INSERT INTO article_relations (article_id, target_type, target_id)
  SELECT na.id, N'player', pl.player_id
  FROM @a a JOIN @newa na ON na.slug = a.slug
  CROSS APPLY (SELECT MIN(p.id) AS player_id, COUNT(*) AS cnt FROM players p JOIN players_i18n pi ON pi.player_id = p.id AND pi.locale = @zh
               WHERE p.club_id = @club AND pi.name = a.player_zh) pl
  WHERE pl.cnt = 1;
SET @arRel = @@ROWCOUNT;
PRINT CONCAT(N'articles: new=', @arNew, N' i18n_new=', @arI18n, N' player_relations_new=', @arRel, N'（球員資料不存在或同名時不掛）');
IF (SELECT COUNT(*) FROM @a a JOIN articles x ON x.slug = a.slug) <> 3 THROW 50069, N'球員故事未全部就位（預期 3 篇）', 1;
IF (SELECT COUNT(*) FROM @a a JOIN articles x ON x.slug = a.slug JOIN articles_i18n i ON i.article_id = x.id) <> 6 THROW 50068, N'球員故事雙語列不是 6', 1;
IF (SELECT COUNT(*) FROM @a a JOIN articles x ON x.slug = a.slug WHERE x.status <> N'draft' AND x.published_at IS NULL) > 0 THROW 50067, N'球員故事必須是草稿', 1;

COMMIT;
PRINT N'20261016_intake-docs-import 完成';
