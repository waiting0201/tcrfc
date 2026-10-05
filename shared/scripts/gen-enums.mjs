#!/usr/bin/env node
// shared/scripts/gen-enums.mjs — 把 App 會遇到的「封閉值域」從資料庫 CHECK 約束寫成契約（缺口 A10 等），兩端不必從種子資料猜值域。
// 來源（不發明規格）：db/club-schema.sql 的 CHECK (… IN (…))。下列每一項在 DDL 找不到就失敗（DDL 改動時這裡會紅燈）。
// 用法：node gen-enums.mjs

import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(here, '../..');
const ddl = readFileSync(join(repoRoot, 'db/club-schema.sql'), 'utf8');

/** 取出 `CREATE TABLE {table} ( … );` 本體。 */
function tableBody(table) {
  const start = ddl.search(new RegExp(`CREATE TABLE ${table}\\s*\\(`));
  if (start < 0) throw new Error(`DDL 找不到資料表 ${table}`);
  const end = ddl.indexOf('\n);', start);
  return ddl.slice(start, end);
}

/** 取出該資料表內 `{column} … CHECK ({column} IN ('a','b'))`（欄位內嵌）或 `CONSTRAINT … CHECK ({column} IN (…))` 的值清單。 */
function checkValues(table, column) {
  const body = tableBody(table);
  const re = new RegExp(`CHECK\\s*\\(\\s*${column}\\s+IN\\s*\\(([^)]*)\\)\\s*\\)`);
  const m = body.match(re);
  if (!m) throw new Error(`DDL 找不到 ${table}.${column} 的 CHECK ... IN (...)`);
  return [...m[1].matchAll(/'([^']+)'/g)].map((x) => x[1]);
}

/** 解析 apps/api/Common/EnrollmentStatus.cs 的兩個字典（中文字面值 → 穩定代碼＋雙語標籤，Android 缺口 C2）。 */
function enrollmentMap(name) {
  const src = readFileSync(join(repoRoot, 'apps/api/Common/EnrollmentStatus.cs'), 'utf8');
  const block = src.match(new RegExp(`${name}\\s*=\\s*new Dictionary[\\s\\S]*?\\{([\\s\\S]*?)\\};`))?.[1];
  if (!block) throw new Error(`EnrollmentStatus.cs 找不到 ${name} 字典（格式被改動？）`);
  const out = [];
  for (const m of block.matchAll(/\["([^"]+)"\]\s*=\s*\("([a-z_]+)",\s*"([^"]+)",\s*"([^"]+)"\)/g)) {
    out.push({ code: m[2], literal: m[1], labelZh: m[3], labelEn: m[4] });
  }
  if (out.length === 0) throw new Error(`EnrollmentStatus.${name} 解析不到任何項目`);
  return out;
}

const doc = {
  $comment:
    '自動產生，請勿手改。產生器：shared/scripts/gen-enums.mjs（docs/19 §2）。來源是 db/club-schema.sql 的 CHECK 約束，不是規格；用戶端遇到不在清單內的值時須容錯（視為未知、不得當機）。',
  enums: {
    'TeamDto.type': {
      values: checkValues('teams', 'type'),
      meaning: { first_team: '一線隊', academy: '學院／青年梯隊' },
      note: '客戶端：非 first_team 一律視為學院梯隊。',
    },
    'TeamDto.gender': {
      values: checkValues('teams', 'gender'),
      meaning: { men: '男子', women: '女子', mixed: '混合' },
    },
    'MatchDto.status': {
      values: checkValues('matches', 'status'),
      meaning: { scheduled: '未開賽', live: '進行中', played: '已完賽', postponed: '延賽', cancelled: '取消' },
      note: '資料庫欄位可為空（舊資料）；null 視為 scheduled。',
    },
    'ProgramSessionDto.statusCode／TrialDto.statusCode（梯次與試訓場次）': {
      values: checkValues('sessions', 'status').map((v) => enrollmentMap('Slot').find((e) => e.literal === v)?.code ?? (() => { throw new Error(`sessions.status「${v}」沒有對應代碼`); })()),
      items: enrollmentMap('Slot'),
      note: '`status` 欄位仍是中文字面值（相容保留），新用戶端依 `statusCode` 分流、依語系顯示 `statusLabelZh`／`statusLabelEn`。DDL 值域與代碼表不一致時產生器失敗。',
    },
    'RegistrationDto.statusCode（課程與試訓報名：MemberRegistrationDto、*RegistrationSubmittedDto）': {
      values: checkValues('registrations', 'status').map((v) => enrollmentMap('Registration').find((e) => e.literal === v)?.code ?? (() => { throw new Error(`registrations.status「${v}」沒有對應代碼`); })()),
      items: enrollmentMap('Registration'),
      note: '課程報名沒有獨立付款資料表：`paid`／`completed` 即已繳費（由後台確認款項後更新）。',
    },
    'CompetitionDto.status（只回 published）': {
      values: checkValues('competitions', 'status'),
      note: '公開端點只回 published，此列僅供對照後台。',
    },
  },
};

writeFileSync(join(repoRoot, 'shared/enums.json'), `${JSON.stringify(doc, null, 2)}\n`);
console.log(`enums: ${Object.keys(doc.enums).length} 組封閉值域 → shared/enums.json`);
