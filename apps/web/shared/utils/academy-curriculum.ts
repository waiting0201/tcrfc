// shared/utils/academy-curriculum.ts — 4.4 訓練課程與課綱（磐石，主站規劃書 v3.29 §3.4／§4.2 B1）的種子內容與前台備援
//
// 內容＝客戶 4.4 文件原文（content/tcrfc/_raw/gdocs/ 下 04_ACADEMY 的 4.4 訓練課程與課綱；英文為開發端翻譯初稿，C-6）。
// 區塊結構與順序必須與 apps/api/Features/AdminPages/PageTemplates.cs 的 `academy/curriculum` 版型一致（九個區塊）：
//   0 訓練理念（text）／1 環境說明（text）／2 願景（steps，固定 4 項）／3 培訓方式與訓練架構（text）／4 賽事規劃（steps，可增刪列）／
//   5 參賽原則與賽事代表權（text）／6 足球與教育的結合（text）／7 行為規範（steps，固定 3 項）／8 各梯隊訓練時間與地點（table，可增刪列）。
//
// 兩個用途：
//   1. 前台 app/pages/zh/academy/curriculum.vue 讀 CMS 頁面（GET /{club}/pages/academy/curriculum）；API 打不到或頁面尚無區塊時，
//      以本檔內容當備援（與 about/philosophy 等固定頁同一模式）。頁面標題、區段小標由前台固定（ACADEMY_CURRICULUM_HEADINGS），
//      後台只填內容。
//   2. db/seed/page_seed_content.extract.ts 以本檔產生 `academy/curriculum` 的種子（page_seed_content.json）。
// 🔴 改字要重跑 extract 並請後端重產 db/prod 種子（generate-prod-content-sql.py），否則種子與前台備援會分岐。
// 藍鯨不設此頁（規劃書 B1 僅磐石 13 頁）；藍鯨路由維持原本的通用頁面，不使用本檔。

export interface CurriculumBilingual { zh: string, en: string }
export type CurriculumSeedBlock
  = { type: 'text', content: { body: CurriculumBilingual } }
    | { type: 'steps', content: { items: Array<{ title: CurriculumBilingual, description?: CurriculumBilingual }> } }
    | { type: 'table', content: { headers: CurriculumBilingual[], rows: string[][] } }

/** 九個區塊的固定小標（與版型欄位標籤同名，前後台同名）。 */
export const ACADEMY_CURRICULUM_HEADINGS: ReadonlyArray<{ zh: string, en: string }> = [
  { zh: '訓練理念', en: 'Training philosophy' },
  { zh: '環境說明', en: 'Training environment' },
  { zh: '願景', en: 'Vision' },
  { zh: '培訓方式與訓練架構', en: 'Training approach and structure' },
  { zh: '賽事規劃', en: 'Competition plan' },
  { zh: '參賽原則與賽事代表權', en: 'Participation principles and competition representation' },
  { zh: '足球與教育的結合', en: 'Football and education' },
  { zh: '行為規範', en: 'Codes of conduct' },
  { zh: '各梯隊訓練時間與地點', en: 'Squad training times and venues' },
]

export const ACADEMY_CURRICULUM_BLOCKS: readonly CurriculumSeedBlock[] = [
  {
    "type": "text",
    "content": {
      "body": {
        "zh": "我們認為青少年足球培育是一項長期且循序漸進的發展工程，從個人技術、小組配合到整體戰術逐步建立能力，並依不同年齡階段均衡發展技術、戰術、生理與心理。同時鼓勵球員主動思考、勇於面對與解決問題，培養成熟的球場判斷力與團隊精神。",
        "en": "We believe that youth football development is a long-term, step-by-step undertaking: ability is built from individual technique and small-group combinations up to overall team tactics, and the technical, tactical, physical and psychological sides are developed in balance at each age stage. We also encourage players to think for themselves and to face and solve problems, building mature on-pitch judgement and team spirit."
      }
    }
  },
  {
    "type": "text",
    "content": {
      "body": {
        "zh": "台中磐石會提供一個自由、自主且快樂的足球環境，讓球員在不受體育班制度限制的情況下持續發展，也不因學制而被迫中斷足球道路。",
        "en": "Taichung Rock provides a free, self-directed and enjoyable football environment, so that players can keep developing without being bound by the sports-class system, and without having their football path interrupted by the school system."
      }
    }
  },
  {
    "type": "steps",
    "content": {
      "items": [
        {
          "title": {
            "zh": "啟發球員",
            "en": "Inspiring players"
          }
        },
        {
          "title": {
            "zh": "培養菁英",
            "en": "Shaping elites"
          }
        },
        {
          "title": {
            "zh": "在地驕傲",
            "en": "Local pride"
          }
        },
        {
          "title": {
            "zh": "撼動全球",
            "en": "Global influence"
          }
        }
      ]
    }
  },
  {
    "type": "text",
    "content": {
      "body": {
        "zh": "採用週期化訓練（年度→月度→週計畫），主題式戰術發展，結合技術、戰術、體能與心理四大面向，並定期評估與個人化回饋，追蹤球員進步軌跡，以此方式循序漸進提升每一位選手。",
        "en": "We use periodised training (annual, monthly and weekly plans) with theme-based tactical development that combines the four dimensions of technique, tactics, fitness and psychology. Regular assessment and individualised feedback track each player's progress, so that every player improves step by step."
      }
    }
  },
  {
    "type": "steps",
    "content": {
      "items": [
        {
          "title": {
            "zh": "國內賽事",
            "en": "Domestic competitions"
          },
          "description": {
            "zh": "參加中華足協主辦認可之盃賽或聯賽，U11/12勝利聯賽",
            "en": "Cups and leagues organised or recognised by the CTFA, and the U11/12 Victory League"
          }
        },
        {
          "title": {
            "zh": "國際賽事",
            "en": "International competitions"
          },
          "description": {
            "zh": "預計 安排一次海外賽事",
            "en": "One overseas competition is planned"
          }
        },
        {
          "title": {
            "zh": "移地訓練",
            "en": "Training camps away from home"
          },
          "description": {
            "zh": "依照訓練計畫安排國內與海外移地訓練",
            "en": "Domestic and overseas training camps arranged according to the training plan"
          }
        }
      ]
    }
  },
  {
    "type": "text",
    "content": {
      "body": {
        "zh": "本俱樂部核心理念視青訓發展為長期計劃，「未來仍持續參與本俱樂部青訓課程與球隊動之球員」為優先考量出國參賽對象。我們相信，出國比賽只是長期培訓的一個里程碑，真正重要的是孩子在平日訓練中持續累積的態度、習慣與能力發展。我們也希望在整體培訓規劃下，讓每一位孩子都能有機會參與比賽與國際交流，累積寶貴的實戰與成長經驗。\n\n凡加入本俱樂部培訓體系之青訓球員，賽事代表權皆歸屬於「台中磐石足球俱樂部」。為確保團隊戰術演練之完整性與整體培訓品質，球員須代表本俱樂部參與國內各項賽事（含足協主辦或認可之賽事）。在學期間，賽事註冊與出賽皆以本俱樂部名義為唯一代表，暫不開放跨單位或代表學校參賽，敬請家長與球員理解與配合。",
        "en": "The club's core philosophy treats youth development as a long-term plan, and players who \"will continue to take part in the club's youth programmes and team activities\" are given priority for overseas competitions. We believe that playing abroad is only one milestone in long-term development; what truly matters is the attitude, habits and ability that children build up in everyday training. Within the overall training plan, we also hope every child gets the chance to take part in matches and international exchange and to gain valuable match and growth experience.\n\nEvery youth player who joins the club's training system is represented in competition by \"Taichung Rock Football Club\". To keep team tactical work complete and the overall training quality high, players must represent the club in domestic competitions (including those organised or recognised by the CTFA). While they are in school, competition registration and appearances are made solely in the club's name; representing other organisations or schools is not open for now. We ask parents and players for your understanding and cooperation."
      }
    }
  },
  {
    "type": "text",
    "content": {
      "body": {
        "zh": "未來將與教育單位合作，向政府申請實驗教育計畫，將足球專業訓練與學業教育整合，術科與學科並重，在生活實作學習環境中，培育更具競爭力的新世代。",
        "en": "In future we will work with education partners and apply to the government for an experimental education programme, integrating professional football training with academic education, giving equal weight to practical and academic subjects, and developing a more competitive new generation in a learning environment of hands-on living."
      }
    }
  },
  {
    "type": "steps",
    "content": {
      "items": [
        {
          "title": {
            "zh": "球員規範",
            "en": "Players' code"
          },
          "description": {
            "zh": "尊重教練、隊友與對手，展現良好運動家精神，準時參加訓練與比賽，請假須提前告知，積極投入訓練，主動學習與自我要求",
            "en": "Respect coaches, teammates and opponents and show good sportsmanship; attend training and matches on time and give notice in advance when absent; commit actively to training; learn proactively and set high standards for yourself."
          }
        },
        {
          "title": {
            "zh": "家長規範",
            "en": "Parents' code"
          },
          "description": {
            "zh": "場邊支持但不干預指揮，信任教練團專業判斷，不在比賽或訓練時對球員或裁判情緒性喊叫\n以身作則，示範良好運動家精神。",
            "en": "Support from the sideline without interfering with coaching instructions; trust the coaching staff's professional judgement; do not shout emotionally at players or referees during matches or training.\nLead by example and demonstrate good sportsmanship."
          }
        },
        {
          "title": {
            "zh": "共同原則",
            "en": "Shared principles"
          },
          "description": {
            "zh": "遵守訓練與比賽紀律，維護團隊正向氛圍，理解發展性足球理念：過程重於結果，成長重於勝負。",
            "en": "Observe discipline in training and matches and maintain a positive team atmosphere; understand the developmental football philosophy: process matters more than outcome, and growth matters more than winning or losing."
          }
        }
      ]
    }
  },
  {
    "type": "table",
    "content": {
      "headers": [
        {
          "zh": "梯隊",
          "en": "Squad"
        },
        {
          "zh": "星期與時段",
          "en": "Day and time"
        },
        {
          "zh": "地點",
          "en": "Venue"
        }
      ],
      "rows": [
        [
          "U12",
          "週三晚間19:00-20:30",
          "協和國小"
        ],
        [
          "U12",
          "週五晚間19:30-21:00",
          "太原足球場"
        ]
      ]
    }
  }
]
