// db/seed/page_seed_content.extract.ts — 產生 page_seed_content.json（B1 固定頁的種子內容，2026-10-07）
//
// 內容一律取自前台寫死的備用內容（apps/web/shared/utils/club-copy*.ts、site-facts.ts 與各頁 .vue 內的字面值），
// 不新寫文案；區塊結構必須與 apps/api/Features/AdminPages/PageTemplates.cs 的版型一致
// （由 apps/api 測試 AdminPageTemplatesTests 對資料庫逐頁核對）。
// 用法：cd apps/web && npx vite-node ../../db/seed/page_seed_content.extract.ts > ../../db/seed/page_seed_content.json
// （前台備用文案改了才需要重跑；json 是 backoffice_seed.py 與 en_backfill_seed.py 的輸入。）
import * as c from '../../apps/web/shared/utils/club-copy'
import * as e1 from '../../apps/web/shared/utils/club-copy-en-core'
import * as e2 from '../../apps/web/shared/utils/club-copy-en-club'
import * as e3 from '../../apps/web/shared/utils/club-copy-en-biz'
import * as h from '../../apps/web/shared/utils/bw-history-en'
import { getSiteFacts } from '../../apps/web/shared/utils/site-facts'

const ft = getSiteFacts('tcrfc'), fb = getSiteFacts('bw')
const bi = (zh: string, en: string | null = null) => ({ zh, en })
const BWEN = c.BW_NAME_EN
const ven = ft.venues.find((v: any) => v.isHomeGround) ?? ft.venues[0]

const seo = (zh: any, en: any) => ({ zh: { title: zh.title, description: zh.description }, en: en ? { title: en.title, description: en.description } : null })

// ── 磐石 our-story：成立年份、頭銜、主場取自 site-facts（與前台備用內容同一個來源）
const storyT = bi(
  `台中磐石足球俱樂部（Taichung Rock FC）於 ${ft.foundedYear} 年在台中成立，同年即拿下${ft.foundingTitleZh}。俱樂部主場設於${ven.nameZh}，以「在地扎根．放眼世界」為品牌主張，逐步建立起一線隊、學院與課程並行的發展體系。`,
  `Taichung Rock FC was founded in Taichung in ${ft.foundedYear} and won the ${ft.foundingTitleEn} title in its first year. The club's home ground is ${ven.nameEn ?? ven.nameZh}. With “LOCAL ROOTS. GLOBAL PATHWAYS.” as its brand promise, it has steadily built a development system in which the First Team, the Academy and our programs run side by side.`)

const modules = [
  ['技術戰術分析', 'Technical & Tactical Analysis'], ['體能訓練', 'Physical Fitness Training'], ['比賽判讀', 'Match Reading'], ['心理韌性', 'Mental Resilience'],
  ['影片分析', 'Video Analysis'], ['IDP 個人發展計畫', 'Individual Development Plan'], ['營養與生活', 'Nutrition & Lifestyle'], ['教育與語言', 'Education & Language'],
]
const prep = bi('詳細內容準備中，稍後將於本頁公開。', 'Details are being prepared and will be published on this page soon.')
const modulesBlock = { type: 'steps', content: { items: modules.map(([zh, en]) => ({ title: bi(zh, en), description: prep })) } }

const coreValues = [
  ['以球員為本', 'Players First', '所有訓練規劃與資源配置，皆以球員的長期發展與福祉為核心考量。', e1.CORE_VALUE_DESCRIPTIONS_EN.players_first],
  ['追求卓越', 'Excellence', '建立專業化訓練與教練體系，協助選手邁向職業舞台所需的實力與態度。', e1.CORE_VALUE_DESCRIPTIONS_EN.excellence],
  ['國際發展', 'Global Pathways', '從台中出發、放眼世界，透過海外交流建立選手與職業舞台接軌的路徑。', e1.CORE_VALUE_DESCRIPTIONS_EN.global_pathways],
  ['社區共好', 'Community', '紮根台中在地，成為社區認同與榮耀的來源，與球迷共同成長。', e1.CORE_VALUE_DESCRIPTIONS_EN.community],
  ['誠信專業', 'Integrity', '以誠信治理與專業制度，支撐俱樂部長期穩健發展。', e1.CORE_VALUE_DESCRIPTIONS_EN.integrity],
]

const pathway = [['在地', 'Local Roots'], ['國內職業', 'Domestic Pro'], ['海外訓練', 'Overseas Training'], ['海外俱樂部', 'Clubs Abroad']]
const pathwayBlock = { type: 'steps', content: { items: pathway.map(([zh, en]) => ({ title: bi(zh, en) })) } }

const audienceNote = bi('完整受眾與社群數據報告可於洽談合作時提供，歡迎聯絡商務部索取最新資料。',
  'The full audience and social media data report is available when we discuss a partnership. Please contact our Partnerships Department for the latest figures.')

const partnersT = [
  ['為何合作', 'Why Partner', '台中磐石是台中在地唯一的企業甲級足球俱樂部，是品牌走進運動與社區場域的直接管道。', "Taichung Rock FC is the only club in Taichung competing in the top-tier corporate league, giving brands a direct route into sport and the local community."],
  ['受眾分析', 'Audience Analysis', '涵蓋一線隊球迷、學院學員家庭與賽事現場觀眾，詳細數據見下方。', 'Covers First Team fans, Academy student families and matchday crowds. Detailed figures are below.'],
  ['品牌曝光', 'Brand Exposure', '球衣、場邊看板、官網與社群通路曝光，讓品牌與職業足球賽事同框。', 'Exposure on the kit, pitchside boards, our website and social media channels puts your brand alongside professional football fixtures.'],
  ['社會影響力', 'Social Impact', '結合社區足球日與慈善活動，讓品牌參與有意義的在地社會實踐。', 'Community football days and charity activities let your brand take part in meaningful local social action.'],
  ['中介影響力', 'Community Reach', '透過學院與課程與家庭、學校建立長期信任關係，擴大品牌口碑觸及。', "Through the Academy and our programs, build long-term trust with families and schools and widen your brand's word-of-mouth reach."],
  ['國際影響力', 'International Impact', '藉由國際交流與海外拓展計畫，讓品牌隨台中磐石球員的腳步走向世界。', "Through international exchange and overseas expansion programs, let your brand travel the world with Taichung Rock FC's players."],
]
const pb = e3.PARTNERS_BECOME_VALUES_EN_BW
const partnersB = [
  ['為何合作', 'Why Partner', '台中藍鯨是台灣木蘭聯賽的球隊之一，是品牌走進女子足球與社區場域的直接管道。', pb.why],
  ['受眾分析', 'Audience Analysis', '涵蓋一線隊球迷、青年隊學員家庭與賽事現場觀眾，詳細數據見下方。', pb.audience],
  ['品牌曝光', 'Brand Exposure', partnersT[2][2], partnersT[2][3]],
  ['社會影響力', 'Social Impact', '結合社區足球推廣活動，讓品牌參與有意義的在地社會實踐。', pb.social],
  ['中介影響力', 'Community Reach', '透過青年隊與推廣活動與家庭、學校建立長期信任關係，擴大品牌口碑觸及。', pb.community],
  ['國際影響力', 'International Impact', '藉由國際交流與海外拓展計畫，讓品牌隨台中藍鯨球員的腳步走向世界。', pb.international],
]
const cardsBlock = (rows: any[]) => ({ type: 'steps', content: { items: rows.map(([zh, en, dzh, den]) => ({ title: bi(zh, en), description: bi(dzh, den) })) } })

const commitment = bi(
  `台中磐石足球俱樂部自 ${ft.foundedYear} 年成立以來，將「Community 社區共好」列為五大核心價值之一，相信足球能為社區帶來的影響不只在球場上。我們相信優質的足球資源不該只集中在少數人身上，因此持續尋找機會，把訓練、場地與人才帶到需要的地方。`,
  `Since Taichung Rock FC was founded in ${ft.foundedYear}, Community has been one of our five core values. We believe the impact football can have on a community goes well beyond the pitch. We also believe that good football resources should not be concentrated in the hands of a few, so we keep looking for opportunities to bring training, facilities and talent to the places that need them.`)
const focusIntro = bi('以下四大領域為規劃書明列的公益投入方向，各領域詳細計畫請見 11.2 慈善計畫。',
  'The four areas below are our public-interest priorities. For the programs in each area, see 11.2 Charity Programs.')
const focus = [
  ['青少年扶助', 'Youth Support', '支持經濟弱勢或資源不足地區的青少年參與足球運動，降低參與門檻。', 'Supporting young people from economically disadvantaged or under-resourced areas to take part in football, and lowering the barriers to participation.'],
  ['偏鄉足球', 'Rural Football', '將教練資源與訓練機會帶到偏遠地區的學校與社區，拉近城鄉足球資源落差。', 'Bringing coaching resources and training opportunities to schools and communities in remote areas, narrowing the gap in football resources between cities and the countryside.'],
  ['弱勢家庭', 'Family Support', '與在地社福單位合作，提供弱勢家庭孩子參與足球活動的機會與必要物資。', 'Working with local social welfare organisations to give children from disadvantaged families the chance to take part in football activities, along with the necessary supplies.'],
  ['公益義賽', 'Charity Matches', '舉辦或參與公益義賽，將活動收益或關注度轉化為對特定公益團體的實質支持。', 'Organising or taking part in charity matches, turning event proceeds or attention into real support for specific charities.'],
]

const text = (zh: string, en: string | null) => ({ type: 'text', content: { body: bi(zh, en) } })
const quote = (zh: string, en: string | null, azh: string, aen: string) => ({ type: 'quote', content: { text: bi(zh, en), attribution: bi(azh, aen) } })
const stepsFromVision = (zhItems: any[], enItems: any[]) => ({ type: 'steps', content: { items: zhItems.map((it, i) => ({ title: bi(it.titleZh, enItems[i].titleZh), description: bi(it.textZh, enItems[i].textZh) })) } })

const govNote = bi('目前尚無可供下載的公開文件。', 'There are no public documents available to download at the moment.')

const out: any = { tcrfc: [], bw: [] }
const add = (club: string, slug: string, seoCopy: any, blocks: any[]) => out[club].push({ slug, seo: seoCopy, blocks })

const nameZh = { tcrfc: '台中磐石足球俱樂部', bw: '台中藍鯨女子足球隊' } as any

// ---- tcrfc
add('tcrfc', 'about/our-story', seo(c.getOurStorySeo('tcrfc', ft), e1.getOurStorySeoEn(ft)), [text(storyT.zh, storyT.en)])
add('tcrfc', 'about/vision-mission', seo(c.VISION_MISSION_SEO.tcrfc, e1.VISION_MISSION_SEO_EN), [stepsFromVision(c.VISION_ITEMS.tcrfc, e1.VISION_ITEMS_EN)])
add('tcrfc', 'about/philosophy', seo(c.PHILOSOPHY_SEO.tcrfc, e1.PHILOSOPHY_SEO_EN), [
  text('同時作為全站內容標籤，貫穿俱樂部各項訓練規劃與對外溝通。', 'They also serve as content tags across the site, running through all of the club\'s training plans and external communications.'),
  { type: 'steps', content: { items: coreValues.map(([zh, en, dzh, den]) => ({ title: bi(zh, en), description: bi(dzh, den) })) } }])
add('tcrfc', 'about/governance', seo(c.GOVERNANCE_SEO.tcrfc, e1.GOVERNANCE_SEO_EN), [text(govNote.zh, govNote.en)])
add('tcrfc', 'about/history', seo(c.getHistorySeo('tcrfc', ft), e1.getHistorySeoEn(ft)), [text(
  `台中磐石足球俱樂部（Taichung Rock FC）於 ${ft.foundedYear} 年在台中成立，成立當年即拿下${ft.foundingTitleZh}，並持續擴展一線隊、學院與國際交流網絡。`,
  `Taichung Rock FC was founded in Taichung in ${ft.foundedYear} and won the ${ft.foundingTitleEn} title in its founding year. The club has since continued to expand the First Team, the Academy and its international exchange network.`)])
add('tcrfc', 'club/player-development', seo(c.getPlayerDevelopmentSeo('tcrfc'), e2.getPlayerDevelopmentSeoEn()), [modulesBlock,
  text('八大模組，一套完整體系\n\n八大模組共同構成一線隊與各梯隊球員的養成框架，銜接學院訓練與國際發展通道，是選手邁向職業舞台的核心支持系統。',
       'Eight modules, one complete system\n\nThe eight modules together form the development framework for First Team and age-group players, connecting Academy training with International Pathways as the core support system for players heading to the professional stage.')])
add('tcrfc', 'club/opportunities', seo(c.getPlayerOpportunitiesSeo('tcrfc'), e2.getPlayerOpportunitiesSeoEn()), [text(c.getJoinFirstTeamBody('tcrfc', ft), e2.getJoinFirstTeamBodyEn(ft))])
add('tcrfc', 'club/international-pathways', seo(c.getInternationalPathwaysSeo('tcrfc'), e2.getInternationalPathwaysSeoEn()), [pathwayBlock])
add('tcrfc', 'club/player-stories', seo(c.getPlayerStoriesSeo('tcrfc'), e2.getPlayerStoriesSeoEn()), [
  { type: 'steps', content: { items: [
    { title: bi('孫恩祈'), description: bi('一線隊 · 後衛 DF · 背號 6', 'First Team · Defender (DF) · No. 6') },
    { title: bi('山內大空'), description: bi('一線隊 · 前鋒 FW · 背號 44', 'First Team · Forward (FW) · No. 44') },
    { title: bi('楊朝景'), description: bi('海外 · 中場 MF · 背號 11 · 現效力香港九龍城', 'Overseas · Midfielder (MF) · No. 11 · currently playing for Kowloon City in Hong Kong') },
  ] } },
  text(c.getPlayerStoriesEmptyNote('tcrfc'), 'There are no player stories in the Academy or Women\'s categories yet.')])
add('tcrfc', 'womens', seo(
  { title: "女子足球 Women's Football｜台中磐石足球俱樂部", description: '台中藍鯨女子隊的介紹與官網入口。完整球員名單、教練陣容、賽程與成績請至台中藍鯨官方網站。' }, e2.CLUB_WOMENS_SEO_EN),
  [text(c.OUR_STORY_BODY_BW, e2.WOMENS_STORY_BODY_EN)])
add('tcrfc', 'partners/become-a-partner', seo(
  { title: `成為合作夥伴 Become a Partner｜合作夥伴與贊助｜${nameZh.tcrfc}`, description: `了解與${nameZh.tcrfc}合作的六大價值：品牌曝光、受眾觸及、社會影響力與國際發展網絡。` },
  { title: 'Become a Partner | Partners & Sponsors | Taichung Rock FC', description: 'Learn about the six values of partnering with Taichung Rock FC: brand exposure, audience reach, social impact and an international development network.' }),
  [cardsBlock(partnersT), text(audienceNote.zh, audienceNote.en)])
add('tcrfc', 'charity/commitment', seo(
  { title: '慈善理念與投入領域 Our Commitment｜慈善與社會影響｜台中磐石足球俱樂部', description: '台中磐石足球俱樂部的慈善理念與四大投入領域：青少年扶助、偏鄉足球、弱勢家庭與公益義賽，實踐 Community 社區共好核心價值。' },
  { title: 'Our Commitment and Focus Areas | Charity & Impact | Taichung Rock FC', description: 'The charitable philosophy and four focus areas of Taichung Rock FC: youth support, rural football, disadvantaged families and charity matches, putting our core value of Community into practice.' }),
  [text(commitment.zh, commitment.en), text(focusIntro.zh, focusIntro.en), cardsBlock(focus)])

// ---- bw
add('bw', 'about/our-story', seo(c.getOurStorySeo('bw', fb), e1.getOurStorySeoEnBw(fb)), [text(c.OUR_STORY_BODY_BW, e1.OUR_STORY_BODY_EN_BW)])
add('bw', 'about/vision-mission', seo(c.VISION_MISSION_SEO.bw, e1.VISION_MISSION_SEO_EN_BW), [stepsFromVision(c.VISION_ITEMS.bw, e1.VISION_ITEMS_EN_BW)])
add('bw', 'about/philosophy', seo(c.PHILOSOPHY_SEO.bw, e1.PHILOSOPHY_SEO_EN_BW), [
  quote(c.PHILOSOPHY_QUOTES_BW.crestZh, e1.PHILOSOPHY_QUOTES_EN_BW.crestZh, '隊徽理念', 'Crest Concept'),
  quote(c.PHILOSOPHY_QUOTES_BW.sloganZh, e1.PHILOSOPHY_QUOTES_EN_BW.sloganZh, '俱樂部口號', 'Club Slogan'),
  quote(c.PHILOSOPHY_QUOTES_BW.spiritZh, e1.PHILOSOPHY_QUOTES_EN_BW.spiritZh, '培訓精神', 'Training Spirit')])
add('bw', 'about/governance', seo(c.GOVERNANCE_SEO.bw, e1.GOVERNANCE_SEO_EN_BW), [text(govNote.zh, govNote.en)])
add('bw', 'about/history', seo(c.getHistorySeo('bw', fb), e1.getHistorySeoEnBw()), [
  { type: 'timeline', content: { items: c.HISTORY_YEARS_BW.map((y: any, i: number) => ({
    date: y.year, title: bi(`${y.year} 年`, y.year), description: bi(y.itemsZh.join('\n'), h.HISTORY_YEARS_EN_BW[i].items.join('\n')) })) } },
  text('上列沿革整理自舊官網公開內容，部分年度屆數標示與其他資料有出入，正式版本將於俱樂部確認後更新。', e1.HISTORY_NOTE_EN_BW)])
add('bw', 'club/player-development', seo(c.getPlayerDevelopmentSeo('bw'), e2.getPlayerDevelopmentSeoEnBw()), [modulesBlock,
  text(`八大面向，持續培育選手\n\n八大面向共同支持一線隊與青年隊球員的成長，銜接青年隊訓練與國際發展通道，協助選手持續進步。`,
       `${e2.CLUB_PLAYER_DEVELOPMENT_PAGE_EN_BW.summaryTitle}\n\n${e2.CLUB_PLAYER_DEVELOPMENT_PAGE_EN_BW.summaryDesc}`)])
add('bw', 'club/opportunities', seo(c.getPlayerOpportunitiesSeo('bw'), e2.getPlayerOpportunitiesSeoEnBw()), [text(c.getJoinFirstTeamBody('bw', fb), e2.getJoinFirstTeamBodyEnBw(fb))])
add('bw', 'club/international-pathways', seo(c.getInternationalPathwaysSeo('bw'), e2.getInternationalPathwaysSeoEnBw()), [pathwayBlock])
add('bw', 'club/player-stories', seo(c.getPlayerStoriesSeo('bw'), e2.getPlayerStoriesSeoEnBw()), [text(c.getPlayerStoriesEmptyNote('bw'), e2.CLUB_PLAYER_STORIES_EMPTY_NOTE_EN_BW)])
add('bw', 'partners/become-a-partner', seo(
  { title: `成為合作夥伴 Become a Partner｜合作夥伴與贊助｜${nameZh.bw}`, description: `了解與${nameZh.bw}合作的六大價值：品牌曝光、受眾觸及、社會影響力與國際發展網絡。` },
  { title: `Become a Partner | Partners & Sponsors | ${BWEN}`, description: `Learn about the six values of partnering with ${BWEN}: brand exposure, audience reach, social impact and an international development network.` }),
  [cardsBlock(partnersB), text(audienceNote.zh, audienceNote.en)])

console.log(JSON.stringify(out, null, 1))
