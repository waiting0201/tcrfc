// shared/utils/charity-commitment.ts — 11.1 慈善理念（磐石，主站規劃書 §3.11／§4.2 B1 `charity/commitment`）的種子內容與前台備援
//
// 內容＝客戶 11.1 慈善理念原文（中英），與 db/migrations/20261016_intake-docs-import.sql 匯入正式庫的區塊逐字相同
// （2 段 text ＋ 4 列 steps：青少年足球培育／國際教育與運動交流／兒少教育資源／社區與家庭足球）。
// 區塊結構與順序必須與 apps/api/Features/AdminPages/PageTemplates.cs 的 `charity/commitment` 版型一致（text,text,steps）。
//
// 用途：
//   1. db/seed/page_seed_content.extract.ts 以本檔產生 `charity/commitment` 的種子（page_seed_content.json）。
//   2. 前台 app/pages/zh/charity/commitment.vue 的備援（API 打不到或頁面尚無區塊時）應改讀本檔，
//      不要再寫死舊的「Community 社區共好／五大核心價值」與「青少年扶助／偏鄉足球／弱勢家庭／公益義賽」四領域。
// 🔴 改字要重跑 extract 並請後端重產 db/prod 種子（generate-prod-content-sql.py），否則種子與前台備援會分岐。

export interface CharityCommitmentBilingual { zh: string, en: string }
export type CharityCommitmentSeedBlock
  = { type: 'text', content: { body: CharityCommitmentBilingual } }
    | { type: 'steps', content: { items: Array<{ title: CharityCommitmentBilingual, description: CharityCommitmentBilingual }> } }

/** SEO 描述（標題維持原樣，不含舊核心價值敘述）。 */
export const CHARITY_COMMITMENT_SEO_DESCRIPTION: CharityCommitmentBilingual = {
  "zh": "台中磐石足球俱樂部的慈善理念與四大投入領域：青少年足球培育、國際教育與運動交流、兒少教育資源、社區與家庭足球。",
  "en": "The charitable philosophy and four focus areas of Taichung Rock FC: youth football development, international education and sports exchange, educational resources for children, and community and family football."
}

export const CHARITY_COMMITMENT_BLOCKS: CharityCommitmentSeedBlock[] = [
  {
    "type": "text",
    "content": {
      "body": {
        "zh": "台中磐石足球俱樂部立足台中，致力推廣足球運動、培育本地選手、深化社區互動及推動青少年足球。俱樂部相信，足球不只是競賽，更是讓兒童與青少年建立自信、紀律、團隊精神及國際視野的重要教育媒介。",
        "en": "Taichung Rock FC is rooted in Taichung and is committed to promoting football, developing local players, deepening community engagement and advancing youth football. The club believes that football is more than competition: it is an important educational medium through which children and young people build confidence, discipline, teamwork and an international outlook."
      }
    }
  },
  {
    "type": "text",
    "content": {
      "body": {
        "zh": "台中磐石以實際行動支持基層足球與在地教育：一方面協助具潛力的青少年球員取得跨國交流與訓練機會；另一方面結合企業夥伴，把學習資源帶進校園與幼兒園。俱樂部期望在追求競技成績的同時，承擔社會責任，成為社區中積極、正面的力量。",
        "en": "Taichung Rock FC supports grassroots football and local education through concrete action: on one hand, helping promising young players gain international exchange and training opportunities; on the other, working with corporate partners to bring learning resources into schools and kindergartens. While pursuing competitive success, the club aims to take on social responsibility and be an active, positive force in the community."
      }
    }
  },
  {
    "type": "steps",
    "content": {
      "items": [
        {
          "title": {
            "zh": "青少年足球培育",
            "en": "Youth football development"
          },
          "description": {
            "zh": "提供基層球員訓練、競賽與國際交流機會。\n全額支持潭秀國中與嶺東高中聯隊共 18 名球員、2 名教練赴日本參加第七屆 Felice CUP U16。",
            "en": "Providing grassroots players with training, competition and international exchange opportunities.\nFully funded 18 players and 2 coaches from the joint 潭秀國中 and 嶺東高中 squad to attend the 7th Felice CUP U16 in Japan."
          }
        },
        {
          "title": {
            "zh": "國際教育與運動交流",
            "en": "International education and sports exchange"
          },
          "description": {
            "zh": "透過海外賽事及訓練，擴展青年球員視野與競技經驗。\nFelice CUP U16 於日本茨城縣舉行，行程包含 J 聯盟教練指導及後續賽事。",
            "en": "Broadening young players' horizons and competitive experience through overseas tournaments and training.\nThe Felice CUP U16 was held in Ibaraki Prefecture, Japan, and included coaching by J.League coaches followed by tournament matches."
          }
        },
        {
          "title": {
            "zh": "兒少教育資源",
            "en": "Educational resources for children"
          },
          "description": {
            "zh": "以書籍及學習資源支持幼兒教育與成長。\n與 Subkarma 捐贈《Cabbage & Tyler》英語幼童學習書籍予潭秀非營利幼兒園。",
            "en": "Supporting early childhood education and growth with books and learning resources.\nTogether with Subkarma, donated the English children's learning books Cabbage & Tyler to 潭秀非營利幼兒園."
          }
        },
        {
          "title": {
            "zh": "社區與家庭足球",
            "en": "Community and family football"
          },
          "description": {
            "zh": "透過賽事與互動活動，增加兒少及家庭接觸足球的機會。\n2025 台中磐石盃設 U6、U8、U10、U12 組別，並規劃親子與球員互動活動，吸引超過 45 支少年隊伍參加。",
            "en": "Increasing children's and families' access to football through matches and interactive events.\nThe 2025 Taichung Rock FC Cup had U6, U8, U10 and U12 divisions, with parent-child and player-interaction activities planned, and attracted more than 45 youth teams."
          }
        }
      ]
    }
  }
]
