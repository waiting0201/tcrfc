/* ============================================================================
   客戶 Google 文件內容匯入 第三批（資料遷移，C-1／B-3）
   2026-10-16｜來源：客戶 Google Drive 收件夾（逐字原文存於 content/tcrfc/_raw/gdocs/，盤點見該目錄 _inventory.md）。
   前兩批（20261016_intake-docs-import.sql／-2.sql）因規格未定而暫緩的文稿，使用者 2026-10-16 裁決「照客戶文件」、規格已改為
   主站規劃書 v3.29／藍鯨 v1.12／App v3.22 後在此匯入。中文照客戶原文只做格式處理；英文為開發端翻譯（用詞照 docs/06-conventions.md §1.1）。
   全部是台中磐石（tcrfc）的資料，不動藍鯨。

   ── 本檔匯入的範圍（預期筆數，結尾逐項斷言，不符即 THROW 回滾）──
     ① B1 固定頁 2 頁（只填版型已有的區塊）：
          about/philosophy（2.3 足球理念與四大核心價值：文字 1 ＋ 4 列步驟，原版型 5 列改 4 列）、
          academy/curriculum（4.4 訓練課程與課綱，新版型 9 個區塊；頁面不存在就建立並發布）。
     ② 球迷會方案 2 個（8.2）：磐石會員（standard，建議年費 NT$400，含球衣 0）、尊尚會員（premium，NT$2,400，含球衣 1），
          商店會員折扣各 10%（規劃書 §3.8 8.3）；權益對照條目磐石會員 7 條、尊尚會員 16 條（各 × 中英）。
          舊的【測試】方案 single／family 改為下架（草稿），不刪除。
     ③ 2026 NAMA 菁英足球表現營（5.2）：課程 1 筆（夏令營、中英）、梯次 4 筆（狀態「已結束」）、合作夥伴 NAMA 1 筆（類型「教育夥伴」，中英）、
          課程與夥伴的關聯 1 筆；場地「安和國中」不存在時新增 1 筆。
   ── 不匯入 ──
     4.3 學院發展路徑：沒有資料容器——前台「路徑圖」版型寫死三階段（培育 U10、U12／發展 U14、U16／職業 U18、一線隊，主站規劃書 §3.4 4.3），
     客戶文件的內容已完整體現在隊別代號與前台版型，無資料可寫。學院梯隊（U10…U18）要建立哪幾隊以 C1 球隊資料為準，不在本檔。

   ── 原文處理 ──
     • 檢查 2.3 兩份、4.3、4.4、5.2、8.2 的原文：沒有工具殘留的引用標記（如「nics.nat+1」），也沒有內部查證欄（如「公開依據」「判斷」），
       所以這兩類都是 0 處；只做了 Markdown 記號（粗體、標題井字、表格、項目符號）轉換、去全形縮排空白、移除取回註解列。
     • 2.3「足球理念」開頭重列的四個價值名稱與「四大核心價值」重複，不重複放入；其餘逐字。
     • 4.4 版型欄位對應：理念＝首段；環境說明＝「台中磐石會提供…」；願景四項（客戶原文「培養菁英 Shaping elites」照留）；培訓方式；
       賽事規劃三列（國內賽事／國際賽事／移地訓練）；參賽原則與代表權＝兩段；足球與教育；行為規範三項；訓練時間與地點（表格，U12 兩列）。
     • 4.4 原文「未來仍持續參與本俱樂部青訓課程與球隊動之球員」疑漏字（球隊「活」動），照原文不改，待客戶確認。
     • 8.2 權益名稱與內容照客戶原文（含「球會服飾」「WhatsApp」用字）；「特約店家折扣」「抽獎資格」兩條不在客戶表裡，是規劃書 §3.14 明列的兩級共同權益，
       為對照表完整而一併列出；尊尚會員「包含磐石會員全部權益」，所以磐石會員的條目同樣掛在尊尚會員底下。
     • 5.2 梯次年份依文件「2026 年暑假」寫為 2026-07；營隊費用每週 NT$12,000 寫在課程內容與梯次價格；適合對象放 programs_i18n.audience。
       課程、梯次不設報名名額（文件未提供）。NAMA 英文名只用縮寫 NAMA（文件沒有官方英文全名，不自行翻譯成官方名）。

   ── 只補空值、不覆蓋後台已編輯的內容 ──
     頁面：區塊內容已等於本檔內容→略過；頁面版本號已大於 1（後台人員存過）且結構符合新版型→略過並 PRINT；
          about/philosophy 若仍是舊版型（5 列）則一律換成新版型內容（舊結構已不合版型）。
     方案、權益條目、課程、梯次、夥伴、場地：以自然鍵判斷（方案＝俱樂部＋球季＋代碼；條目＝方案＋條目代碼；課程／夥伴＝slug；梯次＝課程＋起日；場地＝中文名稱），已存在就整筆不動。
   • 🔴 冪等，可重複執行；單一交易，任何斷言失敗整批回滾。
   • 前提：20261016_client-doc-alignment_1-expand／_2-contract 已套用（方案要有 tier 等欄位）。

   執行（本機）：docker cp 進容器後 sqlcmd -S localhost -U sa -C -d tcrfc_club -b -I -i <檔案>
   ============================================================================ */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @club uniqueidentifier = (SELECT id FROM clubs WHERE code = N'tcrfc');
IF @club IS NULL THROW 50301, N'找不到 tcrfc 俱樂部（clubs.code = tcrfc）', 1;
IF COL_LENGTH(N'membership_plans', N'tier') IS NULL OR COL_LENGTH(N'membership_benefits', N'benefit_code') IS NULL
  THROW 50302, N'請先套用 20261016_client-doc-alignment_1-expand／_2-contract（缺 membership_plans.tier 或 membership_benefits.benefit_code），中止', 1;
DECLARE @zh nvarchar(10) = N'zh-Hant', @en nvarchar(10) = N'en';
DECLARE @n int, @id uniqueidentifier, @slug nvarchar(160);
DECLARE @nb TABLE (sort_order int NOT NULL, block_type nvarchar(32) NOT NULL, content nvarchar(max) NOT NULL);
DECLARE @blocksJson nvarchar(max), @snap nvarchar(max), @same bit, @types nvarchar(max);
DECLARE @pgCreated int = 0, @pgUpdated int = 0, @pgAlready int = 0, @pgSkipped int = 0;


-- ── about/philosophy
SET @slug = N'about/philosophy';
SET @id = (SELECT id FROM pages WHERE club_id = @club AND slug = @slug);
DELETE FROM @nb;
INSERT INTO @nb (sort_order, block_type, content) VALUES
  (0, N'text', N'{"body":{"zh":"台中磐石足球俱樂部的足球理念，可概括為：以台中在地化為根基，以本土球員培育為核心，以完整青訓、職業化管理、運動科學及國際交流為方法，推動可持續的台灣足球發展。這不是只追求短期成績的球隊定位，而是試圖把俱樂部建成連結社區、學校、青訓與成人競技隊的足球發展平台。以專業模式培育球員，協助其追求卓越、發揮潛能。透過實際行動展示職業足球理念，推動台灣足球的永續發展。\n\n在地化與社區責任\n以台中為發展基地，建立地方認同，並把俱樂部定位為推動社區足球、青少年足球與城市運動文化的力量。\n\n完整發展路徑\n建立由兒童、學校青訓到成年隊的培養通道，避免球員在特定年齡階段失去銜接機會。\n\n科學化與個別化訓練\n用檢測、數據與個人化負荷設計取代只憑經驗的訓練，提升球員體能表現、訓練效率與競技穩定度。\n\n國際視野\n學習歐洲、日本與亞洲職業俱樂部的制度與方法，但依台灣教育文化、訓練時間與球員生活現實做在地調整。\n\n建設足球生態\n俱樂部不只負責比賽成績，也必須把制度、資源、訓練與基層發展真正帶進球場，主動承擔足球生態建設責任。","en":"The football philosophy of Taichung Rock Football Club can be summed up as follows: rooted in Taichung, centred on developing local players, and using a complete youth system, professional management, sports science and international exchange as its methods, to promote the sustainable development of football in Taiwan. This is not a club positioning built only on short-term results, but an attempt to build the club into a football development platform that connects the community, schools, youth development and the adult competitive team. We develop players through a professional model, helping them pursue excellence and fulfil their potential, and we demonstrate professional football principles through concrete action to promote the lasting development of Taiwan football.\n\nLocal roots and community responsibility\nTaking Taichung as our base, we build local identity and position the club as a force for community football, youth football and the city''s sporting culture.\n\nA complete development pathway\nWe build a pipeline from children''s and school youth football up to the adult team, so that players do not lose the link to the next stage at any particular age.\n\nScientific and individualised training\nWe replace experience-only training with testing, data and individualised load design, improving players'' physical performance, training efficiency and competitive consistency.\n\nInternational outlook\nWe learn from the systems and methods of professional clubs in Europe, Japan and elsewhere in Asia, and adapt them locally to Taiwan''s education and culture, training hours and the realities of players'' lives.\n\nBuilding the football ecosystem\nThe club is responsible not only for match results. It must also bring systems, resources, training and grassroots development into football in a real way, and actively take on the responsibility of building the football ecosystem."}}'),
  (1, N'steps', N'{"items":[{"title":{"zh":"啟發球員","en":"Inspiring players"},"description":{"zh":"足球的價值不只是贏球。台中磐石相信，更重要的是讓每一位球員看見自己的可能。\n\n從第一次碰球的孩子，到站上成人賽場的球員，我們希望給每個人一條清楚的路、一個能持續成長的環境。紀律、責任、團隊合作、面對挫折、尊重對手，這些東西跟技術一樣重要，甚至更難練。\n\n真正的冠軍，不只存在於比分牌上。","en":"Football''s value goes beyond winning. Taichung Rock believes that what matters more is helping every player see what they could become.\n\nFrom a child touching a ball for the first time to a player stepping onto the adult pitch, we want to give everyone a clear path and an environment in which they can keep growing. Discipline, responsibility, teamwork, facing setbacks and respecting opponents matter as much as technique, and are often harder to train.\n\nTrue champions do not exist only on the scoreboard."}},{"title":{"zh":"塑造冠軍","en":"Shaping champions"},"description":{"zh":"冠軍不是一夜練成的，也不只由名次決定。\n\n它來自每天準時到場、每一次不敷衍的訓練、每一個撐下去的決定，還有在團隊裡彼此扛起的責任。贏了之後的謙遜、輸了之後的韌性、壓力來了的冷靜，這些東西，才是我們真正想培養的。\n\n從青訓到一線隊，台中磐石希望建立一套共同的訓練語言和比賽標準。冠軍不是終點，是每天願意比昨天再好一點點的態度。","en":"A champion is not made overnight, nor decided by league position alone.\n\nIt comes from turning up on time every day, from training that is never half-hearted, from every decision to keep going, and from the responsibility players shoulder for one another in the team. Humility in victory, resilience in defeat and calm under pressure: these are what we truly want to develop.\n\nFrom the youth teams to the First Team, Taichung Rock wants to build a shared training language and shared match standards. A championship is not an endpoint; it is the attitude of being willing to get a little better than yesterday, every day."}},{"title":{"zh":"在地驕傲","en":"Local pride"},"description":{"zh":"台中磐石選擇以台中為家，不只是地點上的選擇，而是一個承諾。\n\n一支真正有力量的球隊，跟所在的城市、社區、學校、家庭和球迷是連在一起的。我們持續投入青少年足球、校園合作和社區活動，讓更多孩子有機會碰到球，讓更多家庭走進球場，讓更多台中人覺得，這支隊是我們自己的。\n\n在地驕傲不只是球衣上的城市名稱。是球員為台中拚的那口氣，是看台上的那聲吶喊，也是大家一起相信的那件事：台中的足球，可以更好。","en":"Taichung Rock chose Taichung as its home, and that is a commitment, not just a choice of location.\n\nA team with real strength is bound up with its city, its communities, its schools, its families and its fans. We keep investing in youth football, school partnerships and community activities, so that more children get the chance to play, more families come into the stadium, and more people in Taichung feel that this team is their own.\n\nLocal pride is more than the name of the city on a shirt. It is the effort players give for Taichung, the cheer from the stands, and something we all believe together: football in Taichung can be better."}},{"title":{"zh":"撼動全球","en":"Global influence"},"description":{"zh":"我們從台中出發，但眼光不只放在台中。\n\n足球是跨越語言和國界的語言。台中磐石透過國際合作、海外交流和教練培訓，讓球員看見世界不同的足球文化，也讓世界慢慢認識台中。\n\n跟不同國家和職業俱樂部的交流，不是為了貼標籤，而是真的想知道高水平足球在要求什麼，細節、紀律、恢復、戰術、人才培育，每一個環節都是功課。我們希望把這些帶回來，轉化成適合台灣球員的東西。\n\n讓每一位從台中磐石出發的球員，都有機會站上更大的舞台。讓每一場在台中踢出的比賽，都成為台灣足球走向世界的下一步。","en":"We start from Taichung, but our eyes are not only on Taichung.\n\nFootball is a language that crosses languages and borders. Through international cooperation, overseas exchange and coach education, Taichung Rock lets players see different football cultures around the world, and lets the world slowly get to know Taichung.\n\nExchange with clubs in other countries, including professional clubs, is not about attaching labels; it is about genuinely finding out what high-level football demands. Detail, discipline, recovery, tactics and talent development are all lessons to learn. We hope to bring these back and turn them into something that suits Taiwanese players.\n\nMay every player who sets out from Taichung Rock have the chance to step onto a bigger stage, and may every match played in Taichung become the next step for Taiwan football towards the world."}}]}');
SET @blocksJson = N'[{"blockType":"text","content":{"body":{"zh":"台中磐石足球俱樂部的足球理念，可概括為：以台中在地化為根基，以本土球員培育為核心，以完整青訓、職業化管理、運動科學及國際交流為方法，推動可持續的台灣足球發展。這不是只追求短期成績的球隊定位，而是試圖把俱樂部建成連結社區、學校、青訓與成人競技隊的足球發展平台。以專業模式培育球員，協助其追求卓越、發揮潛能。透過實際行動展示職業足球理念，推動台灣足球的永續發展。\n\n在地化與社區責任\n以台中為發展基地，建立地方認同，並把俱樂部定位為推動社區足球、青少年足球與城市運動文化的力量。\n\n完整發展路徑\n建立由兒童、學校青訓到成年隊的培養通道，避免球員在特定年齡階段失去銜接機會。\n\n科學化與個別化訓練\n用檢測、數據與個人化負荷設計取代只憑經驗的訓練，提升球員體能表現、訓練效率與競技穩定度。\n\n國際視野\n學習歐洲、日本與亞洲職業俱樂部的制度與方法，但依台灣教育文化、訓練時間與球員生活現實做在地調整。\n\n建設足球生態\n俱樂部不只負責比賽成績，也必須把制度、資源、訓練與基層發展真正帶進球場，主動承擔足球生態建設責任。","en":"The football philosophy of Taichung Rock Football Club can be summed up as follows: rooted in Taichung, centred on developing local players, and using a complete youth system, professional management, sports science and international exchange as its methods, to promote the sustainable development of football in Taiwan. This is not a club positioning built only on short-term results, but an attempt to build the club into a football development platform that connects the community, schools, youth development and the adult competitive team. We develop players through a professional model, helping them pursue excellence and fulfil their potential, and we demonstrate professional football principles through concrete action to promote the lasting development of Taiwan football.\n\nLocal roots and community responsibility\nTaking Taichung as our base, we build local identity and position the club as a force for community football, youth football and the city''s sporting culture.\n\nA complete development pathway\nWe build a pipeline from children''s and school youth football up to the adult team, so that players do not lose the link to the next stage at any particular age.\n\nScientific and individualised training\nWe replace experience-only training with testing, data and individualised load design, improving players'' physical performance, training efficiency and competitive consistency.\n\nInternational outlook\nWe learn from the systems and methods of professional clubs in Europe, Japan and elsewhere in Asia, and adapt them locally to Taiwan''s education and culture, training hours and the realities of players'' lives.\n\nBuilding the football ecosystem\nThe club is responsible not only for match results. It must also bring systems, resources, training and grassroots development into football in a real way, and actively take on the responsibility of building the football ecosystem."}}},{"blockType":"steps","content":{"items":[{"title":{"zh":"啟發球員","en":"Inspiring players"},"description":{"zh":"足球的價值不只是贏球。台中磐石相信，更重要的是讓每一位球員看見自己的可能。\n\n從第一次碰球的孩子，到站上成人賽場的球員，我們希望給每個人一條清楚的路、一個能持續成長的環境。紀律、責任、團隊合作、面對挫折、尊重對手，這些東西跟技術一樣重要，甚至更難練。\n\n真正的冠軍，不只存在於比分牌上。","en":"Football''s value goes beyond winning. Taichung Rock believes that what matters more is helping every player see what they could become.\n\nFrom a child touching a ball for the first time to a player stepping onto the adult pitch, we want to give everyone a clear path and an environment in which they can keep growing. Discipline, responsibility, teamwork, facing setbacks and respecting opponents matter as much as technique, and are often harder to train.\n\nTrue champions do not exist only on the scoreboard."}},{"title":{"zh":"塑造冠軍","en":"Shaping champions"},"description":{"zh":"冠軍不是一夜練成的，也不只由名次決定。\n\n它來自每天準時到場、每一次不敷衍的訓練、每一個撐下去的決定，還有在團隊裡彼此扛起的責任。贏了之後的謙遜、輸了之後的韌性、壓力來了的冷靜，這些東西，才是我們真正想培養的。\n\n從青訓到一線隊，台中磐石希望建立一套共同的訓練語言和比賽標準。冠軍不是終點，是每天願意比昨天再好一點點的態度。","en":"A champion is not made overnight, nor decided by league position alone.\n\nIt comes from turning up on time every day, from training that is never half-hearted, from every decision to keep going, and from the responsibility players shoulder for one another in the team. Humility in victory, resilience in defeat and calm under pressure: these are what we truly want to develop.\n\nFrom the youth teams to the First Team, Taichung Rock wants to build a shared training language and shared match standards. A championship is not an endpoint; it is the attitude of being willing to get a little better than yesterday, every day."}},{"title":{"zh":"在地驕傲","en":"Local pride"},"description":{"zh":"台中磐石選擇以台中為家，不只是地點上的選擇，而是一個承諾。\n\n一支真正有力量的球隊，跟所在的城市、社區、學校、家庭和球迷是連在一起的。我們持續投入青少年足球、校園合作和社區活動，讓更多孩子有機會碰到球，讓更多家庭走進球場，讓更多台中人覺得，這支隊是我們自己的。\n\n在地驕傲不只是球衣上的城市名稱。是球員為台中拚的那口氣，是看台上的那聲吶喊，也是大家一起相信的那件事：台中的足球，可以更好。","en":"Taichung Rock chose Taichung as its home, and that is a commitment, not just a choice of location.\n\nA team with real strength is bound up with its city, its communities, its schools, its families and its fans. We keep investing in youth football, school partnerships and community activities, so that more children get the chance to play, more families come into the stadium, and more people in Taichung feel that this team is their own.\n\nLocal pride is more than the name of the city on a shirt. It is the effort players give for Taichung, the cheer from the stands, and something we all believe together: football in Taichung can be better."}},{"title":{"zh":"撼動全球","en":"Global influence"},"description":{"zh":"我們從台中出發，但眼光不只放在台中。\n\n足球是跨越語言和國界的語言。台中磐石透過國際合作、海外交流和教練培訓，讓球員看見世界不同的足球文化，也讓世界慢慢認識台中。\n\n跟不同國家和職業俱樂部的交流，不是為了貼標籤，而是真的想知道高水平足球在要求什麼，細節、紀律、恢復、戰術、人才培育，每一個環節都是功課。我們希望把這些帶回來，轉化成適合台灣球員的東西。\n\n讓每一位從台中磐石出發的球員，都有機會站上更大的舞台。讓每一場在台中踢出的比賽，都成為台灣足球走向世界的下一步。","en":"We start from Taichung, but our eyes are not only on Taichung.\n\nFootball is a language that crosses languages and borders. Through international cooperation, overseas exchange and coach education, Taichung Rock lets players see different football cultures around the world, and lets the world slowly get to know Taichung.\n\nExchange with clubs in other countries, including professional clubs, is not about attaching labels; it is about genuinely finding out what high-level football demands. Detail, discipline, recovery, tactics and talent development are all lessons to learn. We hope to bring these back and turn them into something that suits Taiwanese players.\n\nMay every player who sets out from Taichung Rock have the chance to step onto a bigger stage, and may every match played in Taichung become the next step for Taiwan football towards the world."}}]}}]';
IF @id IS NULL THROW 50310, N'找不到固定頁 about/philosophy（請先套用 20261007_page-templates.sql，或進後台「頁面管理」一次讓系統補建骨架）', 1;

BEGIN
  SET @types = ISNULL((SELECT STRING_AGG(CAST(block_type AS nvarchar(max)), N',') WITHIN GROUP (ORDER BY sort_order) FROM page_blocks WHERE page_id = @id), N'');
  IF @types <> N'text,steps' THROW 50311, N'固定頁 about/philosophy 的區塊結構與版型（text,steps）不符，不能匯入', 1;
  SET @same = CASE WHEN EXISTS (SELECT 1 FROM @nb n JOIN page_blocks b ON b.page_id = @id AND b.sort_order = n.sort_order WHERE CAST(b.content AS nvarchar(max)) <> n.content) THEN 0 ELSE 1 END;
  IF @same = 1 SET @pgAlready += 1;
  ELSE IF ISNULL((SELECT MAX(version_no) FROM page_versions WHERE page_id = @id), 0) > 1 AND CASE WHEN EXISTS (SELECT 1 FROM page_blocks pb WHERE pb.page_id = @id AND pb.block_type = N'steps' AND (SELECT COUNT(*) FROM OPENJSON(CAST(pb.content AS nvarchar(max)), '$.items')) <> 4) THEN 1 ELSE 0 END = 0
  BEGIN
    SET @pgSkipped += 1;
    PRINT N'略過（後台已編輯過，版本號 > 1）：about/philosophy';
  END
  ELSE
  BEGIN
    UPDATE b SET content = n.content, updated_at = SYSUTCDATETIME() FROM page_blocks b JOIN @nb n ON n.sort_order = b.sort_order WHERE b.page_id = @id;
  UPDATE pages_i18n SET seo_description = N'台中磐石足球俱樂部的足球理念與四大核心價值：啟發球員、塑造冠軍、在地驕傲、撼動全球。' WHERE page_id = @id AND locale = @zh AND seo_description = N'台中磐石足球俱樂部的足球理念與五大核心價值：以球員為本、追求卓越、國際發展、社區共好、誠信專業。';
  UPDATE pages_i18n SET seo_description = N'The football philosophy and four core values of Taichung Rock FC: Inspiring players, Shaping champions, Local pride and Global influence.' WHERE page_id = @id AND locale = @en AND seo_description = N'The football philosophy and five core values of Taichung Rock FC: Players First, Excellence, Global Pathways, Community and Integrity.';
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
END


-- ── academy/curriculum
SET @slug = N'academy/curriculum';
SET @id = (SELECT id FROM pages WHERE club_id = @club AND slug = @slug);
DELETE FROM @nb;
INSERT INTO @nb (sort_order, block_type, content) VALUES
  (0, N'text', N'{"body":{"zh":"我們認為青少年足球培育是一項長期且循序漸進的發展工程，從個人技術、小組配合到整體戰術逐步建立能力，並依不同年齡階段均衡發展技術、戰術、生理與心理。同時鼓勵球員主動思考、勇於面對與解決問題，培養成熟的球場判斷力與團隊精神。","en":"We believe that youth football development is a long-term, step-by-step undertaking: ability is built from individual technique and small-group combinations up to overall team tactics, and the technical, tactical, physical and psychological sides are developed in balance at each age stage. We also encourage players to think for themselves and to face and solve problems, building mature on-pitch judgement and team spirit."}}'),
  (1, N'text', N'{"body":{"zh":"台中磐石會提供一個自由、自主且快樂的足球環境，讓球員在不受體育班制度限制的情況下持續發展，也不因學制而被迫中斷足球道路。","en":"Taichung Rock provides a free, self-directed and enjoyable football environment, so that players can keep developing without being bound by the sports-class system, and without having their football path interrupted by the school system."}}'),
  (2, N'steps', N'{"items":[{"title":{"zh":"啟發球員","en":"Inspiring players"}},{"title":{"zh":"培養菁英","en":"Shaping elites"}},{"title":{"zh":"在地驕傲","en":"Local pride"}},{"title":{"zh":"撼動全球","en":"Global influence"}}]}'),
  (3, N'text', N'{"body":{"zh":"採用週期化訓練（年度→月度→週計畫），主題式戰術發展，結合技術、戰術、體能與心理四大面向，並定期評估與個人化回饋，追蹤球員進步軌跡，以此方式循序漸進提升每一位選手。","en":"We use periodised training (annual, monthly and weekly plans) with theme-based tactical development that combines the four dimensions of technique, tactics, fitness and psychology. Regular assessment and individualised feedback track each player''s progress, so that every player improves step by step."}}'),
  (4, N'steps', N'{"items":[{"title":{"zh":"國內賽事","en":"Domestic competitions"},"description":{"zh":"參加中華足協主辦認可之盃賽或聯賽，U11/12勝利聯賽","en":"Cups and leagues organised or recognised by the CTFA, and the U11/12 Victory League"}},{"title":{"zh":"國際賽事","en":"International competitions"},"description":{"zh":"預計 安排一次海外賽事","en":"One overseas competition is planned"}},{"title":{"zh":"移地訓練","en":"Training camps away from home"},"description":{"zh":"依照訓練計畫安排國內與海外移地訓練","en":"Domestic and overseas training camps arranged according to the training plan"}}]}'),
  (5, N'text', N'{"body":{"zh":"本俱樂部核心理念視青訓發展為長期計劃，「未來仍持續參與本俱樂部青訓課程與球隊動之球員」為優先考量出國參賽對象。我們相信，出國比賽只是長期培訓的一個里程碑，真正重要的是孩子在平日訓練中持續累積的態度、習慣與能力發展。我們也希望在整體培訓規劃下，讓每一位孩子都能有機會參與比賽與國際交流，累積寶貴的實戰與成長經驗。\n\n凡加入本俱樂部培訓體系之青訓球員，賽事代表權皆歸屬於「台中磐石足球俱樂部」。為確保團隊戰術演練之完整性與整體培訓品質，球員須代表本俱樂部參與國內各項賽事（含足協主辦或認可之賽事）。在學期間，賽事註冊與出賽皆以本俱樂部名義為唯一代表，暫不開放跨單位或代表學校參賽，敬請家長與球員理解與配合。","en":"The club''s core philosophy treats youth development as a long-term plan, and players who \"will continue to take part in the club''s youth programmes and team activities\" are given priority for overseas competitions. We believe that playing abroad is only one milestone in long-term development; what truly matters is the attitude, habits and ability that children build up in everyday training. Within the overall training plan, we also hope every child gets the chance to take part in matches and international exchange and to gain valuable match and growth experience.\n\nEvery youth player who joins the club''s training system is represented in competition by \"Taichung Rock Football Club\". To keep team tactical work complete and the overall training quality high, players must represent the club in domestic competitions (including those organised or recognised by the CTFA). While they are in school, competition registration and appearances are made solely in the club''s name; representing other organisations or schools is not open for now. We ask parents and players for your understanding and cooperation."}}'),
  (6, N'text', N'{"body":{"zh":"未來將與教育單位合作，向政府申請實驗教育計畫，將足球專業訓練與學業教育整合，術科與學科並重，在生活實作學習環境中，培育更具競爭力的新世代。","en":"In future we will work with education partners and apply to the government for an experimental education programme, integrating professional football training with academic education, giving equal weight to practical and academic subjects, and developing a more competitive new generation in a learning environment of hands-on living."}}'),
  (7, N'steps', N'{"items":[{"title":{"zh":"球員規範","en":"Players'' code"},"description":{"zh":"尊重教練、隊友與對手，展現良好運動家精神，準時參加訓練與比賽，請假須提前告知，積極投入訓練，主動學習與自我要求","en":"Respect coaches, teammates and opponents and show good sportsmanship; attend training and matches on time and give notice in advance when absent; commit actively to training; learn proactively and set high standards for yourself."}},{"title":{"zh":"家長規範","en":"Parents'' code"},"description":{"zh":"場邊支持但不干預指揮，信任教練團專業判斷，不在比賽或訓練時對球員或裁判情緒性喊叫\n以身作則，示範良好運動家精神。","en":"Support from the sideline without interfering with coaching instructions; trust the coaching staff''s professional judgement; do not shout emotionally at players or referees during matches or training.\nLead by example and demonstrate good sportsmanship."}},{"title":{"zh":"共同原則","en":"Shared principles"},"description":{"zh":"遵守訓練與比賽紀律，維護團隊正向氛圍，理解發展性足球理念：過程重於結果，成長重於勝負。","en":"Observe discipline in training and matches and maintain a positive team atmosphere; understand the developmental football philosophy: process matters more than outcome, and growth matters more than winning or losing."}}]}'),
  (8, N'table', N'{"headers":[{"zh":"梯隊","en":"Squad"},{"zh":"星期與時段","en":"Day and time"},{"zh":"地點","en":"Venue"}],"rows":[["U12","週三晚間19:00-20:30","協和國小"],["U12","週五晚間19:30-21:00","太原足球場"]]}');
SET @blocksJson = N'[{"blockType":"text","content":{"body":{"zh":"我們認為青少年足球培育是一項長期且循序漸進的發展工程，從個人技術、小組配合到整體戰術逐步建立能力，並依不同年齡階段均衡發展技術、戰術、生理與心理。同時鼓勵球員主動思考、勇於面對與解決問題，培養成熟的球場判斷力與團隊精神。","en":"We believe that youth football development is a long-term, step-by-step undertaking: ability is built from individual technique and small-group combinations up to overall team tactics, and the technical, tactical, physical and psychological sides are developed in balance at each age stage. We also encourage players to think for themselves and to face and solve problems, building mature on-pitch judgement and team spirit."}}},{"blockType":"text","content":{"body":{"zh":"台中磐石會提供一個自由、自主且快樂的足球環境，讓球員在不受體育班制度限制的情況下持續發展，也不因學制而被迫中斷足球道路。","en":"Taichung Rock provides a free, self-directed and enjoyable football environment, so that players can keep developing without being bound by the sports-class system, and without having their football path interrupted by the school system."}}},{"blockType":"steps","content":{"items":[{"title":{"zh":"啟發球員","en":"Inspiring players"}},{"title":{"zh":"培養菁英","en":"Shaping elites"}},{"title":{"zh":"在地驕傲","en":"Local pride"}},{"title":{"zh":"撼動全球","en":"Global influence"}}]}},{"blockType":"text","content":{"body":{"zh":"採用週期化訓練（年度→月度→週計畫），主題式戰術發展，結合技術、戰術、體能與心理四大面向，並定期評估與個人化回饋，追蹤球員進步軌跡，以此方式循序漸進提升每一位選手。","en":"We use periodised training (annual, monthly and weekly plans) with theme-based tactical development that combines the four dimensions of technique, tactics, fitness and psychology. Regular assessment and individualised feedback track each player''s progress, so that every player improves step by step."}}},{"blockType":"steps","content":{"items":[{"title":{"zh":"國內賽事","en":"Domestic competitions"},"description":{"zh":"參加中華足協主辦認可之盃賽或聯賽，U11/12勝利聯賽","en":"Cups and leagues organised or recognised by the CTFA, and the U11/12 Victory League"}},{"title":{"zh":"國際賽事","en":"International competitions"},"description":{"zh":"預計 安排一次海外賽事","en":"One overseas competition is planned"}},{"title":{"zh":"移地訓練","en":"Training camps away from home"},"description":{"zh":"依照訓練計畫安排國內與海外移地訓練","en":"Domestic and overseas training camps arranged according to the training plan"}}]}},{"blockType":"text","content":{"body":{"zh":"本俱樂部核心理念視青訓發展為長期計劃，「未來仍持續參與本俱樂部青訓課程與球隊動之球員」為優先考量出國參賽對象。我們相信，出國比賽只是長期培訓的一個里程碑，真正重要的是孩子在平日訓練中持續累積的態度、習慣與能力發展。我們也希望在整體培訓規劃下，讓每一位孩子都能有機會參與比賽與國際交流，累積寶貴的實戰與成長經驗。\n\n凡加入本俱樂部培訓體系之青訓球員，賽事代表權皆歸屬於「台中磐石足球俱樂部」。為確保團隊戰術演練之完整性與整體培訓品質，球員須代表本俱樂部參與國內各項賽事（含足協主辦或認可之賽事）。在學期間，賽事註冊與出賽皆以本俱樂部名義為唯一代表，暫不開放跨單位或代表學校參賽，敬請家長與球員理解與配合。","en":"The club''s core philosophy treats youth development as a long-term plan, and players who \"will continue to take part in the club''s youth programmes and team activities\" are given priority for overseas competitions. We believe that playing abroad is only one milestone in long-term development; what truly matters is the attitude, habits and ability that children build up in everyday training. Within the overall training plan, we also hope every child gets the chance to take part in matches and international exchange and to gain valuable match and growth experience.\n\nEvery youth player who joins the club''s training system is represented in competition by \"Taichung Rock Football Club\". To keep team tactical work complete and the overall training quality high, players must represent the club in domestic competitions (including those organised or recognised by the CTFA). While they are in school, competition registration and appearances are made solely in the club''s name; representing other organisations or schools is not open for now. We ask parents and players for your understanding and cooperation."}}},{"blockType":"text","content":{"body":{"zh":"未來將與教育單位合作，向政府申請實驗教育計畫，將足球專業訓練與學業教育整合，術科與學科並重，在生活實作學習環境中，培育更具競爭力的新世代。","en":"In future we will work with education partners and apply to the government for an experimental education programme, integrating professional football training with academic education, giving equal weight to practical and academic subjects, and developing a more competitive new generation in a learning environment of hands-on living."}}},{"blockType":"steps","content":{"items":[{"title":{"zh":"球員規範","en":"Players'' code"},"description":{"zh":"尊重教練、隊友與對手，展現良好運動家精神，準時參加訓練與比賽，請假須提前告知，積極投入訓練，主動學習與自我要求","en":"Respect coaches, teammates and opponents and show good sportsmanship; attend training and matches on time and give notice in advance when absent; commit actively to training; learn proactively and set high standards for yourself."}},{"title":{"zh":"家長規範","en":"Parents'' code"},"description":{"zh":"場邊支持但不干預指揮，信任教練團專業判斷，不在比賽或訓練時對球員或裁判情緒性喊叫\n以身作則，示範良好運動家精神。","en":"Support from the sideline without interfering with coaching instructions; trust the coaching staff''s professional judgement; do not shout emotionally at players or referees during matches or training.\nLead by example and demonstrate good sportsmanship."}},{"title":{"zh":"共同原則","en":"Shared principles"},"description":{"zh":"遵守訓練與比賽紀律，維護團隊正向氛圍，理解發展性足球理念：過程重於結果，成長重於勝負。","en":"Observe discipline in training and matches and maintain a positive team atmosphere; understand the developmental football philosophy: process matters more than outcome, and growth matters more than winning or losing."}}]}},{"blockType":"table","content":{"headers":[{"zh":"梯隊","en":"Squad"},{"zh":"星期與時段","en":"Day and time"},{"zh":"地點","en":"Venue"}],"rows":[["U12","週三晚間19:00-20:30","協和國小"],["U12","週五晚間19:30-21:00","太原足球場"]]}}]';
IF @id IS NULL
BEGIN
  SET @id = NEWID();
  INSERT INTO pages (id, club_id, slug, status, published_at) VALUES (@id, @club, @slug, N'published', SYSUTCDATETIME());
  INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description) VALUES (@id, @zh, N'訓練課程與課綱 Training & Curriculum｜台中磐石足球學院｜台中磐石足球俱樂部', N'台中磐石足球學院的訓練理念、培訓方式與訓練架構、賽事規劃與參賽原則、足球與教育的結合、行為規範，以及各梯隊的訓練時間與地點。');
  INSERT INTO pages_i18n (page_id, locale, seo_title, seo_description) VALUES (@id, @en, N'Training & Curriculum | Academy | Taichung Rock FC', N'The Taichung Rock FC Academy''s training philosophy, training structure, competition plan and participation principles, football and education, codes of conduct, and training times and venues for each squad.');
  INSERT INTO page_blocks (id, page_id, block_type, content, sort_order)
    SELECT NEWID(), @id, n.block_type, n.content, n.sort_order FROM @nb n;
  INSERT INTO page_versions (id, page_id, version_no, snapshot, preview_token)
    VALUES (NEWID(), @id, 1, N'{"seo":{"zh":{"seoTitle":"訓練課程與課綱 Training & Curriculum｜台中磐石足球學院｜台中磐石足球俱樂部","seoDescription":"台中磐石足球學院的訓練理念、培訓方式與訓練架構、賽事規劃與參賽原則、足球與教育的結合、行為規範，以及各梯隊的訓練時間與地點。"},"en":{"seoTitle":"Training & Curriculum | Academy | Taichung Rock FC","seoDescription":"The Taichung Rock FC Academy''s training philosophy, training structure, competition plan and participation principles, football and education, codes of conduct, and training times and venues for each squad."}},"blocks":[{"blockType":"text","content":{"body":{"zh":"我們認為青少年足球培育是一項長期且循序漸進的發展工程，從個人技術、小組配合到整體戰術逐步建立能力，並依不同年齡階段均衡發展技術、戰術、生理與心理。同時鼓勵球員主動思考、勇於面對與解決問題，培養成熟的球場判斷力與團隊精神。","en":"We believe that youth football development is a long-term, step-by-step undertaking: ability is built from individual technique and small-group combinations up to overall team tactics, and the technical, tactical, physical and psychological sides are developed in balance at each age stage. We also encourage players to think for themselves and to face and solve problems, building mature on-pitch judgement and team spirit."}}},{"blockType":"text","content":{"body":{"zh":"台中磐石會提供一個自由、自主且快樂的足球環境，讓球員在不受體育班制度限制的情況下持續發展，也不因學制而被迫中斷足球道路。","en":"Taichung Rock provides a free, self-directed and enjoyable football environment, so that players can keep developing without being bound by the sports-class system, and without having their football path interrupted by the school system."}}},{"blockType":"steps","content":{"items":[{"title":{"zh":"啟發球員","en":"Inspiring players"}},{"title":{"zh":"培養菁英","en":"Shaping elites"}},{"title":{"zh":"在地驕傲","en":"Local pride"}},{"title":{"zh":"撼動全球","en":"Global influence"}}]}},{"blockType":"text","content":{"body":{"zh":"採用週期化訓練（年度→月度→週計畫），主題式戰術發展，結合技術、戰術、體能與心理四大面向，並定期評估與個人化回饋，追蹤球員進步軌跡，以此方式循序漸進提升每一位選手。","en":"We use periodised training (annual, monthly and weekly plans) with theme-based tactical development that combines the four dimensions of technique, tactics, fitness and psychology. Regular assessment and individualised feedback track each player''s progress, so that every player improves step by step."}}},{"blockType":"steps","content":{"items":[{"title":{"zh":"國內賽事","en":"Domestic competitions"},"description":{"zh":"參加中華足協主辦認可之盃賽或聯賽，U11/12勝利聯賽","en":"Cups and leagues organised or recognised by the CTFA, and the U11/12 Victory League"}},{"title":{"zh":"國際賽事","en":"International competitions"},"description":{"zh":"預計 安排一次海外賽事","en":"One overseas competition is planned"}},{"title":{"zh":"移地訓練","en":"Training camps away from home"},"description":{"zh":"依照訓練計畫安排國內與海外移地訓練","en":"Domestic and overseas training camps arranged according to the training plan"}}]}},{"blockType":"text","content":{"body":{"zh":"本俱樂部核心理念視青訓發展為長期計劃，「未來仍持續參與本俱樂部青訓課程與球隊動之球員」為優先考量出國參賽對象。我們相信，出國比賽只是長期培訓的一個里程碑，真正重要的是孩子在平日訓練中持續累積的態度、習慣與能力發展。我們也希望在整體培訓規劃下，讓每一位孩子都能有機會參與比賽與國際交流，累積寶貴的實戰與成長經驗。\n\n凡加入本俱樂部培訓體系之青訓球員，賽事代表權皆歸屬於「台中磐石足球俱樂部」。為確保團隊戰術演練之完整性與整體培訓品質，球員須代表本俱樂部參與國內各項賽事（含足協主辦或認可之賽事）。在學期間，賽事註冊與出賽皆以本俱樂部名義為唯一代表，暫不開放跨單位或代表學校參賽，敬請家長與球員理解與配合。","en":"The club''s core philosophy treats youth development as a long-term plan, and players who \"will continue to take part in the club''s youth programmes and team activities\" are given priority for overseas competitions. We believe that playing abroad is only one milestone in long-term development; what truly matters is the attitude, habits and ability that children build up in everyday training. Within the overall training plan, we also hope every child gets the chance to take part in matches and international exchange and to gain valuable match and growth experience.\n\nEvery youth player who joins the club''s training system is represented in competition by \"Taichung Rock Football Club\". To keep team tactical work complete and the overall training quality high, players must represent the club in domestic competitions (including those organised or recognised by the CTFA). While they are in school, competition registration and appearances are made solely in the club''s name; representing other organisations or schools is not open for now. We ask parents and players for your understanding and cooperation."}}},{"blockType":"text","content":{"body":{"zh":"未來將與教育單位合作，向政府申請實驗教育計畫，將足球專業訓練與學業教育整合，術科與學科並重，在生活實作學習環境中，培育更具競爭力的新世代。","en":"In future we will work with education partners and apply to the government for an experimental education programme, integrating professional football training with academic education, giving equal weight to practical and academic subjects, and developing a more competitive new generation in a learning environment of hands-on living."}}},{"blockType":"steps","content":{"items":[{"title":{"zh":"球員規範","en":"Players'' code"},"description":{"zh":"尊重教練、隊友與對手，展現良好運動家精神，準時參加訓練與比賽，請假須提前告知，積極投入訓練，主動學習與自我要求","en":"Respect coaches, teammates and opponents and show good sportsmanship; attend training and matches on time and give notice in advance when absent; commit actively to training; learn proactively and set high standards for yourself."}},{"title":{"zh":"家長規範","en":"Parents'' code"},"description":{"zh":"場邊支持但不干預指揮，信任教練團專業判斷，不在比賽或訓練時對球員或裁判情緒性喊叫\n以身作則，示範良好運動家精神。","en":"Support from the sideline without interfering with coaching instructions; trust the coaching staff''s professional judgement; do not shout emotionally at players or referees during matches or training.\nLead by example and demonstrate good sportsmanship."}},{"title":{"zh":"共同原則","en":"Shared principles"},"description":{"zh":"遵守訓練與比賽紀律，維護團隊正向氛圍，理解發展性足球理念：過程重於結果，成長重於勝負。","en":"Observe discipline in training and matches and maintain a positive team atmosphere; understand the developmental football philosophy: process matters more than outcome, and growth matters more than winning or losing."}}]}},{"blockType":"table","content":{"headers":[{"zh":"梯隊","en":"Squad"},{"zh":"星期與時段","en":"Day and time"},{"zh":"地點","en":"Venue"}],"rows":[["U12","週三晚間19:00-20:30","協和國小"],["U12","週五晚間19:30-21:00","太原足球場"]]}}]}', LOWER(CONVERT(varchar(64), CRYPT_GEN_RANDOM(32), 2)));
  SET @pgCreated += 1;
END
ELSE
BEGIN
  SET @types = ISNULL((SELECT STRING_AGG(CAST(block_type AS nvarchar(max)), N',') WITHIN GROUP (ORDER BY sort_order) FROM page_blocks WHERE page_id = @id), N'');
  IF @types <> N'text,text,steps,text,steps,text,text,steps,table' THROW 50311, N'固定頁 academy/curriculum 的區塊結構與版型（text,text,steps,text,steps,text,text,steps,table）不符，不能匯入', 1;
  SET @same = CASE WHEN EXISTS (SELECT 1 FROM @nb n JOIN page_blocks b ON b.page_id = @id AND b.sort_order = n.sort_order WHERE CAST(b.content AS nvarchar(max)) <> n.content) THEN 0 ELSE 1 END;
  IF @same = 1 SET @pgAlready += 1;
  ELSE IF ISNULL((SELECT MAX(version_no) FROM page_versions WHERE page_id = @id), 0) > 1 AND 0 = 0
  BEGIN
    SET @pgSkipped += 1;
    PRINT N'略過（後台已編輯過，版本號 > 1）：academy/curriculum';
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
END

/* ───────── ② 球迷會方案與權益（8.2） ───────── */
DECLARE @season uniqueidentifier = (SELECT id FROM seasons WHERE club_id = @club AND code = N'2026-27');
IF @season IS NULL THROW 50320, N'找不到磐石 2026-27 球季（seasons.code = 2026-27），無法建立會籍方案', 1;
DECLARE @planId uniqueidentifier, @planNew int = 0, @benNew int = 0;

-- 方案 standard（磐石會員）
SET @planId = (SELECT id FROM membership_plans WHERE club_id = @club AND season_id = @season AND code = N'standard');
IF @planId IS NULL
BEGIN
  SET @planId = NEWID();
  INSERT INTO membership_plans (id, club_id, season_id, code, tier, shop_discount_percent, fee, card_quota, jersey_quota, sort_order, status)
    VALUES (@planId, @club, @season, N'standard', N'standard', 10, 400, 1, 0, 0, N'published');
  INSERT INTO membership_plans_i18n (membership_plan_id, locale, name) VALUES (@planId, @zh, N'磐石會員');
  INSERT INTO membership_plans_i18n (membership_plan_id, locale, name) VALUES (@planId, @en, N'Rock Member');
  SET @planNew += 1;
END
IF NOT EXISTS (SELECT 1 FROM membership_benefits WHERE membership_plan_id = @planId AND benefit_code = N'member_card')
BEGIN
  DECLARE @bs_member_card uniqueidentifier = NEWID();
  INSERT INTO membership_benefits (id, membership_plan_id, benefit_code, benefit_group, sort_order, status) VALUES (@bs_member_card, @planId, N'member_card', N'member_card', 0, N'published');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bs_member_card, @zh, N'電子會員卡', N'專屬會員電子卡', N'會員卡', N'✓');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bs_member_card, @en, N'Digital membership card', N'Exclusive digital membership card', N'Membership card', N'✓');
  SET @benNew += 1;
END
IF NOT EXISTS (SELECT 1 FROM membership_benefits WHERE membership_plan_id = @planId AND benefit_code = N'welcome_gift')
BEGIN
  DECLARE @bs_welcome_gift uniqueidentifier = NEWID();
  INSERT INTO membership_benefits (id, membership_plan_id, benefit_code, benefit_group, sort_order, status) VALUES (@bs_welcome_gift, @planId, N'welcome_gift', N'gift', 2, N'published');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bs_welcome_gift, @zh, N'歡迎禮', N'老帽、行李牌', N'禮品', N'✓');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bs_welcome_gift, @en, N'Welcome gift', N'Cap and luggage tag', N'Gifts', N'✓');
  SET @benNew += 1;
END
IF NOT EXISTS (SELECT 1 FROM membership_benefits WHERE membership_plan_id = @planId AND benefit_code = N'shop_discount')
BEGIN
  DECLARE @bs_shop_discount uniqueidentifier = NEWID();
  INSERT INTO membership_benefits (id, membership_plan_id, benefit_code, benefit_group, sort_order, status) VALUES (@bs_shop_discount, @planId, N'shop_discount', N'discount', 5, N'published');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bs_shop_discount, @zh, N'購物折扣', N'球會服飾、周邊及指定活動享 10% 折扣', N'折扣與優惠', N'10%');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bs_shop_discount, @en, N'Shopping discount', N'10% off club apparel, merchandise and selected events', N'Discounts & offers', N'10%');
  SET @benNew += 1;
END
IF NOT EXISTS (SELECT 1 FROM membership_benefits WHERE membership_plan_id = @planId AND benefit_code = N'partner_discount')
BEGIN
  DECLARE @bs_partner_discount uniqueidentifier = NEWID();
  INSERT INTO membership_benefits (id, membership_plan_id, benefit_code, benefit_group, sort_order, status) VALUES (@bs_partner_discount, @planId, N'partner_discount', N'discount', 7, N'published');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bs_partner_discount, @zh, N'特約店家折扣', N'依各特約店家設定的適用層級享有優惠', N'折扣與優惠', N'✓');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bs_partner_discount, @en, N'Partner store discounts', N'Offers at partner stores, according to the tier each store applies to', N'Discounts & offers', N'✓');
  SET @benNew += 1;
END
IF NOT EXISTS (SELECT 1 FROM membership_benefits WHERE membership_plan_id = @planId AND benefit_code = N'member_vote')
BEGIN
  DECLARE @bs_member_vote uniqueidentifier = NEWID();
  INSERT INTO membership_benefits (id, membership_plan_id, benefit_code, benefit_group, sort_order, status) VALUES (@bs_member_vote, @planId, N'member_vote', N'event', 8, N'published');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bs_member_vote, @zh, N'會員投票', N'可參與年度最佳球員、最佳進球等投票', N'活動', N'✓');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bs_member_vote, @en, N'Member voting', N'Vote in polls such as Player of the Year and Goal of the Year', N'Events', N'✓');
  SET @benNew += 1;
END
IF NOT EXISTS (SELECT 1 FROM membership_benefits WHERE membership_plan_id = @planId AND benefit_code = N'priority_signup')
BEGIN
  DECLARE @bs_priority_signup uniqueidentifier = NEWID();
  INSERT INTO membership_benefits (id, membership_plan_id, benefit_code, benefit_group, sort_order, status) VALUES (@bs_priority_signup, @planId, N'priority_signup', N'event', 9, N'published');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bs_priority_signup, @zh, N'優先報名', N'球迷活動、夏令營或講座可提早報名', N'活動', N'✓');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bs_priority_signup, @en, N'Priority registration', N'Register early for fan events, summer camps and talks', N'Events', N'✓');
  SET @benNew += 1;
END
IF NOT EXISTS (SELECT 1 FROM membership_benefits WHERE membership_plan_id = @planId AND benefit_code = N'draw_eligibility')
BEGIN
  DECLARE @bs_draw_eligibility uniqueidentifier = NEWID();
  INSERT INTO membership_benefits (id, membership_plan_id, benefit_code, benefit_group, sort_order, status) VALUES (@bs_draw_eligibility, @planId, N'draw_eligibility', N'event', 12, N'published');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bs_draw_eligibility, @zh, N'抽獎資格', N'會籍有效期間自動具備，不需登記', N'活動', N'✓');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bs_draw_eligibility, @en, N'Prize draw eligibility', N'Automatic while the membership is valid, no sign-up needed', N'Events', N'✓');
  SET @benNew += 1;
END

-- 方案 premium（尊尚會員）
SET @planId = (SELECT id FROM membership_plans WHERE club_id = @club AND season_id = @season AND code = N'premium');
IF @planId IS NULL
BEGIN
  SET @planId = NEWID();
  INSERT INTO membership_plans (id, club_id, season_id, code, tier, shop_discount_percent, fee, card_quota, jersey_quota, sort_order, status)
    VALUES (@planId, @club, @season, N'premium', N'premium', 10, 2400, 1, 1, 1, N'published');
  INSERT INTO membership_plans_i18n (membership_plan_id, locale, name) VALUES (@planId, @zh, N'尊尚會員');
  INSERT INTO membership_plans_i18n (membership_plan_id, locale, name) VALUES (@planId, @en, N'Prestige Member');
  SET @planNew += 1;
END
IF NOT EXISTS (SELECT 1 FROM membership_benefits WHERE membership_plan_id = @planId AND benefit_code = N'member_card')
BEGIN
  DECLARE @bp_member_card uniqueidentifier = NEWID();
  INSERT INTO membership_benefits (id, membership_plan_id, benefit_code, benefit_group, sort_order, status) VALUES (@bp_member_card, @planId, N'member_card', N'member_card', 0, N'published');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bp_member_card, @zh, N'電子會員卡', N'專屬會員電子卡', N'會員卡', N'✓');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bp_member_card, @en, N'Digital membership card', N'Exclusive digital membership card', N'Membership card', N'✓');
  SET @benNew += 1;
END
IF NOT EXISTS (SELECT 1 FROM membership_benefits WHERE membership_plan_id = @planId AND benefit_code = N'includes_standard')
BEGIN
  DECLARE @bp_includes_standard uniqueidentifier = NEWID();
  INSERT INTO membership_benefits (id, membership_plan_id, benefit_code, benefit_group, sort_order, status) VALUES (@bp_includes_standard, @planId, N'includes_standard', N'member_card', 1, N'published');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bp_includes_standard, @zh, N'包含磐石會員全部權益', N'磐石會員內容完整保留', N'會員卡', N'✓');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bp_includes_standard, @en, N'Includes all Rock Member benefits', N'All Rock Member benefits are kept in full', N'Membership card', N'✓');
  SET @benNew += 1;
END
IF NOT EXISTS (SELECT 1 FROM membership_benefits WHERE membership_plan_id = @planId AND benefit_code = N'welcome_gift')
BEGIN
  DECLARE @bp_welcome_gift uniqueidentifier = NEWID();
  INSERT INTO membership_benefits (id, membership_plan_id, benefit_code, benefit_group, sort_order, status) VALUES (@bp_welcome_gift, @planId, N'welcome_gift', N'gift', 2, N'published');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bp_welcome_gift, @zh, N'歡迎禮', N'老帽、行李牌', N'禮品', N'✓');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bp_welcome_gift, @en, N'Welcome gift', N'Cap and luggage tag', N'Gifts', N'✓');
  SET @benNew += 1;
END
IF NOT EXISTS (SELECT 1 FROM membership_benefits WHERE membership_plan_id = @planId AND benefit_code = N'premium_gift_box')
BEGIN
  DECLARE @bp_premium_gift_box uniqueidentifier = NEWID();
  INSERT INTO membership_benefits (id, membership_plan_id, benefit_code, benefit_group, sort_order, status) VALUES (@bp_premium_gift_box, @planId, N'premium_gift_box', N'gift', 3, N'published');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bp_premium_gift_box, @zh, N'限量尊尚禮盒', N'球衣、老帽、行李牌或限量紀念品', N'禮品', N'✓');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bp_premium_gift_box, @en, N'Limited-edition Prestige gift box', N'Jersey, cap, luggage tag or a limited-edition keepsake', N'Gifts', N'✓');
  SET @benNew += 1;
END
IF NOT EXISTS (SELECT 1 FROM membership_benefits WHERE membership_plan_id = @planId AND benefit_code = N'referral_reward')
BEGIN
  DECLARE @bp_referral_reward uniqueidentifier = NEWID();
  INSERT INTO membership_benefits (id, membership_plan_id, benefit_code, benefit_group, sort_order, status) VALUES (@bp_referral_reward, @planId, N'referral_reward', N'gift', 4, N'published');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bp_referral_reward, @zh, N'推薦獎勵', N'成功推薦新學員或會員，可獲商店券、活動抵扣或升級禮遇', N'禮品', N'✓');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bp_referral_reward, @en, N'Referral rewards', N'Successfully refer a new student or member to earn a shop voucher, an event credit or an upgrade perk', N'Gifts', N'✓');
  SET @benNew += 1;
END
IF NOT EXISTS (SELECT 1 FROM membership_benefits WHERE membership_plan_id = @planId AND benefit_code = N'shop_discount')
BEGIN
  DECLARE @bp_shop_discount uniqueidentifier = NEWID();
  INSERT INTO membership_benefits (id, membership_plan_id, benefit_code, benefit_group, sort_order, status) VALUES (@bp_shop_discount, @planId, N'shop_discount', N'discount', 5, N'published');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bp_shop_discount, @zh, N'購物折扣', N'球會服飾、周邊及指定活動享 10% 折扣', N'折扣與優惠', N'10%');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bp_shop_discount, @en, N'Shopping discount', N'10% off club apparel, merchandise and selected events', N'Discounts & offers', N'10%');
  SET @benNew += 1;
END
IF NOT EXISTS (SELECT 1 FROM membership_benefits WHERE membership_plan_id = @planId AND benefit_code = N'training_discount')
BEGIN
  DECLARE @bp_training_discount uniqueidentifier = NEWID();
  INSERT INTO membership_benefits (id, membership_plan_id, benefit_code, benefit_group, sort_order, status) VALUES (@bp_training_discount, @planId, N'training_discount', N'discount', 6, N'published');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bp_training_discount, @zh, N'訓練／課程優惠', N'學院課程、假期營隊、個人訓練或合作商戶享 10%–15% 優惠', N'折扣與優惠', N'10%–15%');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bp_training_discount, @en, N'Training and programme offers', N'10%–15% off Academy programmes, holiday camps, individual training or at partner merchants', N'Discounts & offers', N'10%–15%');
  SET @benNew += 1;
END
IF NOT EXISTS (SELECT 1 FROM membership_benefits WHERE membership_plan_id = @planId AND benefit_code = N'partner_discount')
BEGIN
  DECLARE @bp_partner_discount uniqueidentifier = NEWID();
  INSERT INTO membership_benefits (id, membership_plan_id, benefit_code, benefit_group, sort_order, status) VALUES (@bp_partner_discount, @planId, N'partner_discount', N'discount', 7, N'published');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bp_partner_discount, @zh, N'特約店家折扣', N'依各特約店家設定的適用層級享有優惠', N'折扣與優惠', N'✓');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bp_partner_discount, @en, N'Partner store discounts', N'Offers at partner stores, according to the tier each store applies to', N'Discounts & offers', N'✓');
  SET @benNew += 1;
END
IF NOT EXISTS (SELECT 1 FROM membership_benefits WHERE membership_plan_id = @planId AND benefit_code = N'member_vote')
BEGIN
  DECLARE @bp_member_vote uniqueidentifier = NEWID();
  INSERT INTO membership_benefits (id, membership_plan_id, benefit_code, benefit_group, sort_order, status) VALUES (@bp_member_vote, @planId, N'member_vote', N'event', 8, N'published');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bp_member_vote, @zh, N'會員投票', N'可參與年度最佳球員、最佳進球等投票', N'活動', N'✓');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bp_member_vote, @en, N'Member voting', N'Vote in polls such as Player of the Year and Goal of the Year', N'Events', N'✓');
  SET @benNew += 1;
END
IF NOT EXISTS (SELECT 1 FROM membership_benefits WHERE membership_plan_id = @planId AND benefit_code = N'priority_signup')
BEGIN
  DECLARE @bp_priority_signup uniqueidentifier = NEWID();
  INSERT INTO membership_benefits (id, membership_plan_id, benefit_code, benefit_group, sort_order, status) VALUES (@bp_priority_signup, @planId, N'priority_signup', N'event', 9, N'published');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bp_priority_signup, @zh, N'優先報名', N'球迷活動、夏令營或講座可提早報名', N'活動', N'✓');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bp_priority_signup, @en, N'Priority registration', N'Register early for fan events, summer camps and talks', N'Events', N'✓');
  SET @benNew += 1;
END
IF NOT EXISTS (SELECT 1 FROM membership_benefits WHERE membership_plan_id = @planId AND benefit_code = N'priority_tickets')
BEGIN
  DECLARE @bp_priority_tickets uniqueidentifier = NEWID();
  INSERT INTO membership_benefits (id, membership_plan_id, benefit_code, benefit_group, sort_order, status) VALUES (@bp_priority_tickets, @planId, N'priority_tickets', N'event', 10, N'published');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bp_priority_tickets, @zh, N'優先購票／保留名額', N'對外賽事、表演賽、晚宴、海外交流或特別活動', N'活動', N'✓');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bp_priority_tickets, @en, N'Priority tickets and reserved places', N'For public matches, exhibition matches, dinners, overseas exchanges and special events', N'Events', N'✓');
  SET @benNew += 1;
END
IF NOT EXISTS (SELECT 1 FROM membership_benefits WHERE membership_plan_id = @planId AND benefit_code = N'exclusive_events')
BEGIN
  DECLARE @bp_exclusive_events uniqueidentifier = NEWID();
  INSERT INTO membership_benefits (id, membership_plan_id, benefit_code, benefit_group, sort_order, status) VALUES (@bp_exclusive_events, @planId, N'exclusive_events', N'event', 11, N'published');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bp_exclusive_events, @zh, N'專屬活動', N'每年至少一次教練分享會、球員見面會、訓練參觀或會員日', N'活動', N'✓');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bp_exclusive_events, @en, N'Exclusive events', N'At least one coach talk, player meet-up, training visit or members'' day each year', N'Events', N'✓');
  SET @benNew += 1;
END
IF NOT EXISTS (SELECT 1 FROM membership_benefits WHERE membership_plan_id = @planId AND benefit_code = N'draw_eligibility')
BEGIN
  DECLARE @bp_draw_eligibility uniqueidentifier = NEWID();
  INSERT INTO membership_benefits (id, membership_plan_id, benefit_code, benefit_group, sort_order, status) VALUES (@bp_draw_eligibility, @planId, N'draw_eligibility', N'event', 12, N'published');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bp_draw_eligibility, @zh, N'抽獎資格', N'會籍有效期間自動具備，不需登記', N'活動', N'✓');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bp_draw_eligibility, @en, N'Prize draw eligibility', N'Automatic while the membership is valid, no sign-up needed', N'Events', N'✓');
  SET @benNew += 1;
END
IF NOT EXISTS (SELECT 1 FROM membership_benefits WHERE membership_plan_id = @planId AND benefit_code = N'behind_the_scenes')
BEGIN
  DECLARE @bp_behind_the_scenes uniqueidentifier = NEWID();
  INSERT INTO membership_benefits (id, membership_plan_id, benefit_code, benefit_group, sort_order, status) VALUES (@bp_behind_the_scenes, @planId, N'behind_the_scenes', N'content', 13, N'published');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bp_behind_the_scenes, @zh, N'幕後內容', N'戰術分析、青訓發展報告、教練訪談', N'內容與溝通', N'✓');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bp_behind_the_scenes, @en, N'Behind-the-scenes content', N'Tactical analysis, youth development reports and coach interviews', N'Content & community', N'✓');
  SET @benNew += 1;
END
IF NOT EXISTS (SELECT 1 FROM membership_benefits WHERE membership_plan_id = @planId AND benefit_code = N'priority_channel')
BEGIN
  DECLARE @bp_priority_channel uniqueidentifier = NEWID();
  INSERT INTO membership_benefits (id, membership_plan_id, benefit_code, benefit_group, sort_order, status) VALUES (@bp_priority_channel, @planId, N'priority_channel', N'content', 14, N'published');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bp_priority_channel, @zh, N'優先溝通渠道', N'會員專屬 WhatsApp／社群群組或季度問答會', N'內容與溝通', N'✓');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bp_priority_channel, @en, N'Priority communication channel', N'A members-only WhatsApp / community group or quarterly Q&A sessions', N'Content & community', N'✓');
  SET @benNew += 1;
END
IF NOT EXISTS (SELECT 1 FROM membership_benefits WHERE membership_plan_id = @planId AND benefit_code = N'name_display')
BEGIN
  DECLARE @bp_name_display uniqueidentifier = NEWID();
  INSERT INTO membership_benefits (id, membership_plan_id, benefit_code, benefit_group, sort_order, status) VALUES (@bp_name_display, @planId, N'name_display', N'content', 15, N'published');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bp_name_display, @zh, N'名字展示', N'可選擇在官方網站或年度感謝牆列名', N'內容與溝通', N'✓');
  INSERT INTO membership_benefits_i18n (membership_benefit_id, locale, name, description, group_label, value) VALUES (@bp_name_display, @en, N'Name display', N'Optionally have your name listed on the official website or the annual thank-you wall', N'Content & community', N'✓');
  SET @benNew += 1;
END

-- 舊的【測試】方案（single／family）下架：新方案存在才動，已下架者不動
UPDATE membership_plans SET status = N'draft', updated_at = SYSUTCDATETIME()
WHERE club_id = @club AND season_id = @season AND code IN (N'single', N'family') AND status = N'published'
  AND EXISTS (SELECT 1 FROM membership_plans x WHERE x.club_id = @club AND x.season_id = @season AND x.code = N'standard' AND x.status = N'published');
SET @n = @@ROWCOUNT; PRINT CONCAT(N'舊【測試】方案下架 ', @n, N' 個');


/* ───────── ③ 2026 NAMA 菁英足球表現營（5.2） ───────── */
DECLARE @venue uniqueidentifier = (SELECT TOP 1 v.id FROM venues v JOIN venues_i18n i ON i.venue_id = v.id AND i.locale = @zh WHERE i.name = N'安和國中');
DECLARE @venueNew int = 0;
IF @venue IS NULL
BEGIN
  SET @venue = NEWID();
  INSERT INTO venues (id, sort_order) VALUES (@venue, 0);
  INSERT INTO venues_i18n (venue_id, locale, name) VALUES (@venue, @zh, N'安和國中');
  INSERT INTO venues_i18n (venue_id, locale, name) VALUES (@venue, @en, N'Anhe Junior High School');
  SET @venueNew = 1;
END

DECLARE @partner uniqueidentifier = (SELECT id FROM partners WHERE club_id = @club AND slug = N'nama');
DECLARE @partnerNew int = 0, @progNew int = 0, @sesNew int = 0, @linkNew int = 0;
IF @partner IS NULL
BEGIN
  SET @partner = NEWID();
  INSERT INTO partners (id, club_id, slug, partner_type, sort_order) VALUES (@partner, @club, N'nama', N'教育夥伴', 90);
  INSERT INTO partners_i18n (partner_id, locale, name, content) VALUES (@partner, @zh, N'北美國際蒙特梭利教育機構（NAMA）', N'2026 NAMA 菁英足球表現營的合作單位。');
  INSERT INTO partners_i18n (partner_id, locale, name, content) VALUES (@partner, @en, N'NAMA', N'Partner of the 2026 NAMA Elite Soccer Camp.');
  SET @partnerNew = 1;
END

DECLARE @prog uniqueidentifier = (SELECT id FROM programs WHERE club_id = @club AND slug = N'2026-nama-elite-soccer-camp');
IF @prog IS NULL
BEGIN
  SET @prog = NEWID();
  INSERT INTO programs (id, club_id, slug, program_type, status) VALUES (@prog, @club, N'2026-nama-elite-soccer-camp', N'summer_camp', N'published');
  INSERT INTO programs_i18n (program_id, locale, name, intro, audience, content) VALUES (@prog, @zh, N'2026 NAMA 菁英足球表現營', N'北美國際蒙特梭利教育機構（NAMA）於 2026 年暑假推出「菁英足球表現營」，專為國小一年級至六年級學生設計。課程將專業足球訓練、沉浸式英語、主題文化探索與創意表達結合，讓孩子不只提升足球能力，也能在真實互動與團隊活動中建立自信、溝通力與紀律。', N'國小一年級至六年級學生', N'[{"blockType":"text","content":{"body":"Elite Soccer Camp｜夏令營完整介紹\n\n營隊以「Train Smart. Play Hard. Speak with Confidence.」為核心理念，提供兼具運動、語言與個人成長的完整暑期學習體驗。"}},{"blockType":"text","content":{"body":"營隊特色\n\n・專業足球訓練：安排技術、體能、團隊合作與比賽策略等訓練內容，並將英語融入足球情境之中。\n\n・沉浸式英語學習：透過文化、創作、口說及表達活動，讓孩子在自然、有趣的情境下使用英文。\n\n・英語文化與創意專題：包含文化探索、手作與藝術創作，以及英語口說與表達活動。\n\n・每週賽事成果發表：每梯次最後一天舉辦 Tournament Showcase，讓學員實際參與競賽、展現訓練成果。\n\n・個人與團隊影片紀錄：營隊將製作成果影片，記錄孩子的足球表現、學習投入與團隊成長。"}},{"blockType":"text","content":{"body":"培養能力\n\n透過每日的足球、英語與團隊任務，學員將逐步建立：\n\n・自信與舞台感\n\n・英語溝通能力\n\n・團隊合作精神\n\n・壓力下的表現能力\n\n・專注力與紀律\n\n・足球技術、體能與比賽理解能力\n\n・創意表達與主題式學習能力"}},{"blockType":"text","content":{"body":"日期與梯次\n\n營隊共設四個梯次，每梯次為七天："}},{"blockType":"table","content":{"headers":["梯次","日期"],"rows":[["Week 1","7 月 1 日（週三）至 7 月 7 日（週二）"],["Week 2","7 月 8 日（週三）至 7 月 14 日（週二）"],["Week 3","7 月 15 日（週三）至 7 月 21 日（週二）"],["Week 4","7 月 22 日（週三）至 7 月 28 日（週二）"]]}},{"blockType":"text","content":{"body":"每一梯次的最後一天均為 Tournament Showcase，讓孩子透過競賽呈現一週所學。"}},{"blockType":"text","content":{"body":"地點\n\n安和國中｜專業足球場與教室\n\nProfessional Training Venue at Anhe Junior High School\n\n課程同時使用專業足球訓練空間及教室，讓足球實作與英語文化學習能在合適的環境中進行。"}},{"blockType":"text","content":{"body":"每日課程安排"}},{"blockType":"table","content":{"headers":["時間","活動","內容說明"],"rows":[["09:00–09:30","Arrival & Snack","報到與早點心"],["09:30–11:00","Creative English Lab","英語沉浸學習，結合文化、創作與表達"],["11:00–12:00","English Culture & Project","主題式英語文化與專題活動"],["12:00–13:30","Lunch","午餐與休息"],["13:30–16:30","Professional Soccer Training + Language Application","專業足球訓練，結合足球情境英語應用"],["16:30–17:00","Snack","下午點心"],["17:00","Dismiss","放學"]]}},{"blockType":"text","content":{"body":"實際課程將依教學現況彈性調整，以提供學員最佳學習體驗。"}},{"blockType":"text","content":{"body":"核心學習架構\n\n英語文化與創作表達\n\n孩子將從文化探索、創意手作、藝術創作及英語口說活動中，提升理解、表達與溝通能力。課程不只著重語言知識，更強調在互動、任務及成果展示中實際使用英文。\n\n專業足球訓練與情境英語\n\n足球課程涵蓋技術與體能訓練、團隊合作與比賽策略，並加入運動情境中的英語應用。孩子能在訓練、比賽及團隊溝通中，練習以英文理解指令、表達想法及與隊友合作。"}},{"blockType":"text","content":{"body":"費用與包含項目\n\n費用：每週新台幣 12,000 元（All Inclusive）\n\n費用包含：\n\n・全課程活動\n\n・教材與材料\n\n・午餐\n\n・早點心與下午點心\n\n・足球訓練與英語文化課程\n\n・每週 Tournament Showcase\n\n・個人及團隊成果影片紀錄"}},{"blockType":"text","content":{"body":"適合對象\n\n本營隊適合希望在暑假中：\n\n・提升足球技術與運動表現的孩子\n\n・在自然情境中練習英語溝通的孩子\n\n・建立自信、紀律與團隊精神的孩子\n\n・喜歡競賽、創作、活動式學習與成果展示的孩子\n\n・希望獲得兼具運動、語言及全人發展體驗的國小學生"}}]');
  INSERT INTO programs_i18n (program_id, locale, name, intro, audience, content) VALUES (@prog, @en, N'2026 NAMA Elite Soccer Camp', N'In summer 2026, the North American International Montessori education institution (NAMA) launches the Elite Soccer Camp, designed for students in grades 1 to 6 of primary school. The camp combines professional football training, immersive English, themed cultural exploration and creative expression, so that children not only improve their football but also build confidence, communication skills and discipline through real interaction and team activities.', N'Students in grades 1 to 6 of primary school', N'[{"blockType":"text","content":{"body":"Elite Soccer Camp | Full camp introduction\n\nThe camp''s core idea is \"Train Smart. Play Hard. Speak with Confidence.\", offering a complete summer learning experience that combines sport, language and personal growth."}},{"blockType":"text","content":{"body":"Camp features\n\n・Professional football training: sessions cover technique, fitness, teamwork and match strategy, with English woven into football situations.\n\n・Immersive English learning: through culture, creative and speaking activities, children use English naturally and enjoyably.\n\n・English culture and creative projects: cultural exploration, crafts and art, and English speaking and expression activities.\n\n・Weekly tournament showcase: on the last day of each session a Tournament Showcase lets participants compete and show the results of their training.\n\n・Personal and team video records: the camp produces a video recording each child''s football performance, engagement in learning and team growth."}},{"blockType":"text","content":{"body":"Skills developed\n\nThrough daily football, English and team tasks, participants gradually build:\n\n・Confidence and stage presence\n\n・English communication skills\n\n・Teamwork\n\n・Performance under pressure\n\n・Focus and discipline\n\n・Football technique, fitness and understanding of the game\n\n・Creative expression and thematic learning skills"}},{"blockType":"text","content":{"body":"Dates and sessions\n\nThe camp runs in four sessions of seven days each:"}},{"blockType":"table","content":{"headers":["Session","Dates"],"rows":[["Week 1","1 July (Wed) to 7 July (Tue)"],["Week 2","8 July (Wed) to 14 July (Tue)"],["Week 3","15 July (Wed) to 21 July (Tue)"],["Week 4","22 July (Wed) to 28 July (Tue)"]]}},{"blockType":"text","content":{"body":"The last day of every session is the Tournament Showcase, where children show what they learned during the week through competition."}},{"blockType":"text","content":{"body":"Venue\n\n安和國中 (Anhe Junior High School)｜Football pitch and classrooms\n\nProfessional Training Venue at Anhe Junior High School\n\nThe camp uses both a professional football training area and classrooms, so that football practice and English culture learning each take place in a suitable environment."}},{"blockType":"text","content":{"body":"Daily schedule"}},{"blockType":"table","content":{"headers":["Time","Activity","Description"],"rows":[["09:00–09:30","Arrival & Snack","Check-in and morning snack"],["09:30–11:00","Creative English Lab","Immersive English learning combining culture, creativity and expression"],["11:00–12:00","English Culture & Project","Themed English culture and project activities"],["12:00–13:30","Lunch","Lunch and rest"],["13:30–16:30","Professional Soccer Training + Language Application","Professional football training with English applied to football situations"],["16:30–17:00","Snack","Afternoon snack"],["17:00","Dismiss","Dismissal"]]}},{"blockType":"text","content":{"body":"The actual programme will be adjusted flexibly to the teaching situation to give participants the best learning experience."}},{"blockType":"text","content":{"body":"Core learning structure\n\nEnglish culture and creative expression\n\nChildren improve their understanding, expression and communication through cultural exploration, creative crafts, art and English speaking activities. The programme goes beyond language knowledge and emphasises actually using English in interaction, tasks and showcases.\n\nProfessional football training and situational English\n\nThe football programme covers technique and fitness training, teamwork and match strategy, and adds English used in sporting situations. In training, matches and team communication, children practise understanding instructions in English, expressing their ideas and cooperating with teammates."}},{"blockType":"text","content":{"body":"Fee and what is included\n\nFee: NT$12,000 per week (All Inclusive)\n\nThe fee includes:\n\n・All programme activities\n\n・Teaching materials\n\n・Lunch\n\n・Morning and afternoon snacks\n\n・Football training and English culture classes\n\n・A weekly Tournament Showcase\n\n・Personal and team video records"}},{"blockType":"text","content":{"body":"Who it is for\n\nThis camp suits children who want, over the summer, to:\n\n・Improve their football technique and sporting performance\n\n・Practise English communication in natural situations\n\n・Build confidence, discipline and team spirit\n\n・Enjoy competition, creative work, activity-based learning and showcases\n\n・Gain an experience that combines sport, language and all-round development, as primary school students"}}]');
  SET @progNew = 1;
END
IF NOT EXISTS (SELECT 1 FROM program_partners WHERE program_id = @prog AND partner_id = @partner)
BEGIN
  INSERT INTO program_partners (program_id, partner_id) VALUES (@prog, @partner);
  SET @linkNew = 1;
END

DECLARE @ses TABLE (start_on date NOT NULL, end_on date NOT NULL);
INSERT INTO @ses (start_on, end_on) VALUES
  (N'2026-07-01', N'2026-07-07'),
  (N'2026-07-08', N'2026-07-14'),
  (N'2026-07-15', N'2026-07-21'),
  (N'2026-07-22', N'2026-07-28');
INSERT INTO sessions (id, club_id, program_id, venue_id, start_on, end_on, enrolled_count, price, status)
  SELECT NEWID(), @club, @prog, @venue, s.start_on, s.end_on, 0, 12000, N'已結束' FROM @ses s
  WHERE NOT EXISTS (SELECT 1 FROM sessions x WHERE x.program_id = @prog AND x.start_on = s.start_on);
SET @sesNew = @@ROWCOUNT;


/* ───────── 斷言與摘要 ───────── */
PRINT CONCAT(N'頁面：新建 ', @pgCreated, N'、更新 ', @pgUpdated, N'、已相同 ', @pgAlready, N'、略過 ', @pgSkipped);
PRINT CONCAT(N'方案新增 ', @planNew, N'、權益條目新增 ', @benNew, N'；場地新增 ', @venueNew, N'、夥伴新增 ', @partnerNew, N'、課程新增 ', @progNew, N'、梯次新增 ', @sesNew, N'、關聯新增 ', @linkNew);

IF (SELECT COUNT(*) FROM membership_plans WHERE club_id = @club AND season_id = @season AND code IN (N'standard', N'premium') AND status = N'published') <> 2
  THROW 50330, N'磐石會員／尊尚會員兩個方案沒有全部存在（已上架），中止', 1;
IF (SELECT COUNT(*) FROM membership_benefits b JOIN membership_plans p ON p.id = b.membership_plan_id WHERE p.club_id = @club AND p.season_id = @season AND p.code = N'standard' AND b.status = N'published') <> 7
  THROW 50331, N'磐石會員的權益條目筆數不是 7，中止', 1;
IF (SELECT COUNT(*) FROM membership_benefits b JOIN membership_plans p ON p.id = b.membership_plan_id WHERE p.club_id = @club AND p.season_id = @season AND p.code = N'premium' AND b.status = N'published') <> 16
  THROW 50332, N'尊尚會員的權益條目筆數不是 16，中止', 1;
IF (SELECT COUNT(*) FROM membership_benefits_i18n i JOIN membership_benefits b ON b.id = i.membership_benefit_id JOIN membership_plans p ON p.id = b.membership_plan_id
    WHERE p.club_id = @club AND p.season_id = @season AND p.code IN (N'standard', N'premium')) <> 46
  THROW 50333, N'權益條目的中英文側表筆數不是 46，中止', 1;
IF (SELECT COUNT(*) FROM sessions x JOIN programs g ON g.id = x.program_id WHERE g.club_id = @club AND g.slug = N'2026-nama-elite-soccer-camp') <> 4
  THROW 50334, N'NAMA 營隊的梯次不是 4 筆，中止', 1;
IF (SELECT COUNT(*) FROM programs_i18n i JOIN programs g ON g.id = i.program_id WHERE g.club_id = @club AND g.slug = N'2026-nama-elite-soccer-camp') <> 2
  THROW 50335, N'NAMA 營隊的中英文課程內容不是 2 筆，中止', 1;
IF NOT EXISTS (SELECT 1 FROM program_partners l JOIN programs g ON g.id = l.program_id JOIN partners p ON p.id = l.partner_id WHERE g.club_id = @club AND g.slug = N'2026-nama-elite-soccer-camp' AND p.slug = N'nama')
  THROW 50336, N'NAMA 營隊與合作夥伴 NAMA 沒有關聯，中止', 1;
IF (SELECT COUNT(*) FROM pages p WHERE p.club_id = @club AND p.slug IN (N'about/philosophy', N'academy/curriculum')) <> 2
  THROW 50337, N'about/philosophy 與 academy/curriculum 兩頁沒有全部存在，中止', 1;
IF EXISTS (SELECT 1 FROM page_blocks b JOIN pages p ON p.id = b.page_id WHERE p.club_id = @club AND p.slug = N'about/philosophy' AND b.block_type = N'steps'
           AND (SELECT COUNT(*) FROM OPENJSON(CAST(b.content AS nvarchar(max)), '$.items')) <> 4)
  THROW 50338, N'about/philosophy 的核心價值不是 4 列，中止', 1;

COMMIT TRAN;
