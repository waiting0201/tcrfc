// shared/utils/bw-history-en.ts — 藍鯨「俱樂部歷程」2014–2025 的英文版（B-5，2026-10-05）
//
// 對應 club-copy.ts 的 HISTORY_YEARS_BW（逐字節錄 content/blue-whale/club-profile.md §4）。
// 只翻譯、不增刪、不改寫，**條目順序與數量與中文版逐條相同**（中文原文已知的屆數矛盾——2017 寫「第三屆」、
// 2021 寫「第八屆」——照原樣保留，頁面以一句來源說明提示待客戶確認）。
//
// 🔴 本檔刻意**不**命名為 `club-copy-en-*.ts`：人名（教練、球員）沒有英文來源，依 B-5 約定維持中文原字；
// 機構／學校名同理保留中文並附英文描述（待客戶確認，不自創看似官方的英文名）。
// `check-en-copy.mjs` 只掃 `club-copy-en-*.ts`，不含本檔；渲染後的中文人名由 `check-en-pages.mjs` 白名單處理。
import { BW_NAME_EN } from './club-copy'

export interface HistoryYearEn {
  year: string
  items: string[]
}

const MULAN_PROGRAMME = 'Took on the Sports iTaiwan sports hotspot promotion programme'

export const HISTORY_YEARS_EN_BW: HistoryYearEn[] = [
  { year: '2014', items: [`Formed the ${BW_NAME_EN} women's football team to compete in the Mulan League`, 'Took part in the 1st Taiwan Mulan Football League', 'Facebook followers reached 1,200', 'Small-sided artificial-turf football pitch completed at the National Taiwan Sport University'] },
  { year: '2015', items: ['Took part in the 2nd Taiwan Mulan Football League', "Helped establish the girls' football team at 五權國中, a junior high school in Taichung", 'Took on the organisation of a D-level coaching course'] },
  { year: '2016', items: ['Took part in the 3rd Taiwan Mulan Football League', "Established the Central Taiwan Elite Women's Training Station", "Held the AFC Women's Football Day for the first time"] },
  {
    year: '2017',
    items: [
      'Took part in the 3rd Taiwan Mulan Football League',
      'Held the 1st Blue Whale Cup football tournament',
      "Renamed the Elite Women's Football Training Station the Central Training Station",
      'Appointed JFA S-level coach 堀野博幸 as First Team head coach',
      'Held a regional coaching course',
      'Taichung Beitun Taiyuan Football Field opened',
      'First Taiwan Mulan League title in club history',
      'Facebook followers reached 6,500',
    ],
  },
  {
    year: '2018',
    items: [
      'Took part in the 4th Taiwan Mulan Football League',
      `Founded the ${BW_NAME_EN} Football School`,
      'Renamed the Central Training Station the Blue Whale Central Football Training Station',
      `First professional player 包欣玄 joined ${BW_NAME_EN}`,
      "Helped establish the girls' football team at 惠文高中, a senior high school in Taichung",
      'Second Taiwan Mulan League title in club history',
    ],
  },
  {
    year: '2019',
    items: [
      'Took part in the 5th Taiwan Mulan Football League',
      'Goalkeeper 蔡明容 successfully moved abroad to Japan',
      'First Japanese player 田中麻帆 joined',
      'Campaigned for the construction of a Taichung football park',
      "Formed an esports squad to compete in the PES2020 World Cup",
      'Third Taiwan Mulan League title in club history',
      'First time the club sold tickets for a league home match',
      'Passed the AFC Club Licence accreditation',
      'Head coach 呂桂花 won the AFC 2019 Grassroots Leader Award',
      'First full season of themed days at home matches',
      'Founded the Blue Whale Girls cheerleading squad',
      MULAN_PROGRAMME,
    ],
  },
  {
    year: '2020',
    items: [
      'Took part in the 6th Taiwan Mulan Football League',
      'First Hong Kong player 吳卓蔚 joined',
      'First American player 瑪芮兒 joined',
      'Goalkeeper 程思瑜 successfully moved abroad to Japan',
      'Player 蘇育萱 successfully moved abroad to Japan',
      'Sold tickets for league home matches',
      `The ${BW_NAME_EN} U15 girls' team took part in the 1st Taiwan Youth League`,
      'First Taiwan Mulan League runners-up finish in club history',
      MULAN_PROGRAMME,
    ],
  },
  {
    year: '2021',
    items: [
      'Took part in the 8th Taiwan Mulan Football League',
      'Second Japanese player 日高偉織 joined',
      'First Thai player 皮薩邁頌賽 joined',
      'First Thai goalkeeper 納塔魯亞牧塔納維奇 joined',
      'Fourth Taiwan Mulan League title in club history',
      'First Taiwan Mulan League Cup (MLC) title in club history',
      `The ${BW_NAME_EN} U15 girls' team took part in the 2nd Taiwan Youth League`,
      `The ${BW_NAME_EN} U18 girls' team took part in the 2nd Taiwan Youth League`,
      MULAN_PROGRAMME,
    ],
  },
  {
    year: '2022',
    items: [
      'Took part in the 9th Taiwan Mulan Football League',
      "Represented Taiwan at the AFC Women's Club Championship (Thailand)",
      'Third Thai player in club history, 席拉萬茵樂敏, joined',
      'After successfully managing the pandemic, held the first top-level football opening match',
      `The ${BW_NAME_EN} U15 girls' team took part in the 3rd Taiwan Youth League`,
      `The ${BW_NAME_EN} U18 girls' team took part in the 3rd Taiwan Youth League`,
      `The ${BW_NAME_EN} U15 team won its first Taiwan Youth League U15 girls' title`,
      'Second Taiwan Mulan League runners-up finish in club history',
      MULAN_PROGRAMME,
    ],
  },
  {
    year: '2023',
    items: [
      'Took part in the 10th Taiwan Mulan Football League',
      'Broke ground on the Taichung football park and was invited to the groundbreaking ceremony',
      'Player 蘇育萱 successfully moved abroad to China',
      'Fourth Thai player in club history, 席菲拉萬茵樂敏, joined',
      'Fifth Thai player in club history, 薩瓦拉克彭甘, joined',
      'Facebook followers reached 16,500',
      'Fifth Taiwan Mulan League title in club history',
      MULAN_PROGRAMME,
    ],
  },
  {
    year: '2024',
    items: [
      'Took part in the 11th Taiwan Mulan Football League',
      `Founded the ${BW_NAME_EN} U10 girls' team`,
      `The ${BW_NAME_EN} U10 girls' team took part in the Taichung Mayor's Cup for the first time`,
      'Invited to the 陽信盃 international invitational tournament and won the title',
      "Represented Taiwan in the 2024/25 AFC Women's Champions League and advanced from the group stage",
      'Third Taiwan Mulan League runners-up finish in club history',
      'Sixth Thai player in club history and second Thai goalkeeper, 瓦拉邦汶廷, joined',
      "The club's second foreign player, 薩瓦拉克彭甘, won the Taiwan Mulan Football League Golden Boot of the year",
      MULAN_PROGRAMME,
    ],
  },
  {
    year: '2025',
    items: [
      'Took part in the 12th Taiwan Mulan Football League',
      "Represented Taiwan in the 2024-25 AFC Women's Champions League quarter-finals and finished among the top 8 in Asia",
      "Runners-up at the 2025 Taiwan President's Cup football tournament",
      'Seventh Thai player in club history and third Thai goalkeeper, 邱瑪尼-通蒙戈, joined',
      'Second Thai player in club history, 冼仲意, joined',
      'Head coach 呂桂花 was nominated for the AFC Asian Best Women\'s Team Coach award',
      'For the first time, the team shirt carried a public-interest organisation: 台中惠明盲校, a school for the blind in Taichung',
      MULAN_PROGRAMME,
      'First interview with the British magazine World Soccer',
    ],
  },
]
