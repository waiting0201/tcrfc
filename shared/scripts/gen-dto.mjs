#!/usr/bin/env node
// shared/scripts/gen-dto.mjs — 由 shared/openapi.json 產生 Swift／Kotlin 的 DTO（只有資料型別，不產 client）。
//
// 為什麼自己寫、不用 swift-openapi-generator／openapi-generator（決定與理由見 docs/19 §2）：
//   - 兩者都會連 client 一起產，而 docs/19 §2 紀律 1 要求「只產 DTO」；
//   - swift-openapi-generator 需要 Swift 工具鏈，無法在 GitHub-hosted ubuntu runner 上便宜地跑漂移檢查；
//   - 本 API 的 schema 形狀很單純（物件、陣列、字串／數字／布林、可為空、字典，沒有 enum／繼承），
//     約 200 行可以涵蓋；遇到沒處理過的形狀會直接報錯，不會默默產出錯的型別。
//   - 零相依、離線、輸出完全決定性（只看 shared/openapi.json）。
//
// 型別對應：
//   string/uuid/date/date-time -> String（時間不在 DTO 層解析：伺服器的 UTC 字串格式由 App 的時間工具統一處理）
//   integer int32 -> Int（Swift）／Int（Kotlin）；int64 -> Int64／Long；number -> Double；boolean -> Bool／Boolean
//   可為空或非必填 -> Optional（Swift）／可為 null 且預設 null（Kotlin）
//   任意 JSON（schema 為 {}）-> JSONValue（Swift，本檔內建）／JsonElement（Kotlin）
//
// 用法：node gen-dto.mjs   （讀 shared/openapi.json，寫 shared/generated/{swift,kotlin}/）

import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const sharedDir = join(here, '..');

/** Kotlin 套件名稱。App 客戶端 repo 建立（AP-7）後若要改，只改這一行再重新產生。 */
const KOTLIN_PACKAGE = 'tw.tcrfc.app.api.dto';

const SWIFT_KEYWORDS = new Set(
  `associatedtype class deinit enum extension fileprivate func import init inout internal let open operator private protocol public rethrows static struct subscript typealias var break case continue default defer do else fallthrough for guard if in repeat return switch where while as Any catch false is nil super self Self throw throws true try`.split(
    /\s+/,
  ),
);
const KOTLIN_KEYWORDS = new Set(
  `as break class continue do else false for fun if in interface is null object package return super this throw true try typealias typeof val var when while`.split(
    /\s+/,
  ),
);

const doc = JSON.parse(readFileSync(join(sharedDir, 'openapi.json'), 'utf8'));
const schemas = doc.components?.schemas ?? {};

/** schema 名稱必須是合法識別字且不得與語言內建型別相撞。 */
const RESERVED_TYPE_NAMES = new Set(['Any', 'String', 'Int', 'Int64', 'Double', 'Bool', 'Boolean', 'Long', 'Result', 'Error', 'Task', 'Date', 'Data', 'URL', 'JSONValue']);

/** 回傳 { swift, kotlin, optional } */
function resolveType(s, where) {
  if (s === true || (s && typeof s === 'object' && Object.keys(s).length === 0)) {
    return { swift: 'JSONValue', kotlin: 'JsonElement', optional: false };
  }
  if (s.$ref) {
    const name = s.$ref.replace('#/components/schemas/', '');
    return named(name);
  }
  if (s.oneOf || s.anyOf) {
    const alts = s.oneOf ?? s.anyOf;
    const nonNull = alts.filter((x) => x.type !== 'null');
    if (nonNull.length === 1 && alts.length === 2) {
      return { ...resolveType(nonNull[0], where), optional: true };
    }
    throw new Error(`${where}：不支援的 oneOf/anyOf 形狀（${JSON.stringify(s).slice(0, 160)}）`);
  }
  if (s.allOf) {
    if (s.allOf.length === 1) return resolveType(s.allOf[0], where);
    throw new Error(`${where}：不支援多成員 allOf`);
  }
  let types = s.type;
  let optional = false;
  if (Array.isArray(types)) {
    optional = types.includes('null');
    types = types.filter((t) => t !== 'null');
    // .NET 對數值會輸出 ["integer","string"]（允許字串形式的數字）；以數值為準
    if (types.includes('integer')) types = 'integer';
    else if (types.includes('number')) types = 'number';
    else if (types.length === 1) types = types[0];
    else throw new Error(`${where}：不支援的型別組合 ${JSON.stringify(s.type)}`);
  }
  switch (types) {
    case 'string':
      return { swift: 'String', kotlin: 'String', optional };
    case 'boolean':
      return { swift: 'Bool', kotlin: 'Boolean', optional };
    case 'integer':
      return s.format === 'int64'
        ? { swift: 'Int64', kotlin: 'Long', optional }
        : { swift: 'Int', kotlin: 'Int', optional };
    case 'number':
      return { swift: 'Double', kotlin: 'Double', optional };
    case 'array': {
      const item = resolveType(s.items ?? {}, `${where}[]`);
      const itemOpt = item.optional;
      return {
        swift: `[${item.swift}${itemOpt ? '?' : ''}]`,
        kotlin: `List<${item.kotlin}${itemOpt ? '?' : ''}>`,
        optional,
      };
    }
    case 'object': {
      if (s.properties) throw new Error(`${where}：不支援內嵌的具名屬性物件，請在後端抽成具名型別`);
      const ap = s.additionalProperties;
      const valueType = ap === undefined || ap === true ? {} : ap;
      const v = resolveType(valueType, `${where}{}`);
      return {
        swift: `[String: ${v.swift}${v.optional ? '?' : ''}]`,
        kotlin: `Map<String, ${v.kotlin}${v.optional ? '?' : ''}>`,
        optional,
      };
    }
    case undefined:
      return { swift: 'JSONValue', kotlin: 'JsonElement', optional };
    default:
      throw new Error(`${where}：未知 type ${JSON.stringify(types)}`);
  }
}

function named(name) {
  if (name === 'JsonElement') return { swift: 'JSONValue', kotlin: 'JsonElement', optional: false };
  return { swift: name, kotlin: name, optional: false };
}

// ── 先收集每個 schema 的屬性 ──────────────────────────────────────────────
const models = [];
for (const [name, s] of Object.entries(schemas)) {
  if (name === 'JsonElement') continue; // 對應內建的任意 JSON 型別
  if (!/^[A-Za-z][A-Za-z0-9]*$/.test(name)) throw new Error(`schema 名稱不是合法識別字：${name}`);
  if (RESERVED_TYPE_NAMES.has(name)) throw new Error(`schema 名稱 ${name} 與語言內建型別相撞，請在後端改名`);
  if (s.type !== 'object' || !s.properties) {
    throw new Error(`schema ${name} 不是具名屬性物件（${JSON.stringify(s).slice(0, 120)}），請擴充 gen-dto.mjs`);
  }
  const required = new Set(s.required ?? []);
  const props = Object.entries(s.properties).map(([pn, ps]) => {
    if (!/^[A-Za-z_][A-Za-z0-9_]*$/.test(pn)) throw new Error(`${name}.${pn} 不是合法識別字`);
    const t = resolveType(ps, `${name}.${pn}`);
    return { name: pn, ...t, optional: t.optional || !required.has(pn) };
  });
  models.push({ name, props });
}

// ── Swift ────────────────────────────────────────────────────────────────
function swiftId(n) {
  return SWIFT_KEYWORDS.has(n) ? `\`${n}\`` : n;
}

function emitSwift() {
  const out = [];
  out.push(`// 自動產生，請勿手改。來源：shared/openapi.json；產生器：shared/scripts/gen-dto.mjs（docs/19 §2）。`);
  out.push(`// 只有資料型別（DTO），沒有 client：權杖續期、冪等鍵、離線佇列、退避重試由 App 的網路層自己負責。`);
  out.push(`// 時間欄位一律是 String（伺服器的 UTC ISO 8601 字串），解析交給 App 的時間工具。`);
  out.push('');
  out.push('import Foundation');
  out.push('');
  out.push(`/// 任意 JSON 值（OpenAPI schema 為 {} 的欄位）。`);
  out.push('public enum JSONValue: Codable, Equatable, Sendable {');
  out.push('    case null');
  out.push('    case bool(Bool)');
  out.push('    case number(Double)');
  out.push('    case string(String)');
  out.push('    case array([JSONValue])');
  out.push('    case object([String: JSONValue])');
  out.push('');
  out.push('    public init(from decoder: Decoder) throws {');
  out.push('        let c = try decoder.singleValueContainer()');
  out.push('        if c.decodeNil() { self = .null }');
  out.push('        else if let v = try? c.decode(Bool.self) { self = .bool(v) }');
  out.push('        else if let v = try? c.decode(Double.self) { self = .number(v) }');
  out.push('        else if let v = try? c.decode(String.self) { self = .string(v) }');
  out.push('        else if let v = try? c.decode([JSONValue].self) { self = .array(v) }');
  out.push('        else { self = .object(try c.decode([String: JSONValue].self)) }');
  out.push('    }');
  out.push('');
  out.push('    public func encode(to encoder: Encoder) throws {');
  out.push('        var c = encoder.singleValueContainer()');
  out.push('        switch self {');
  out.push('        case .null: try c.encodeNil()');
  out.push('        case .bool(let v): try c.encode(v)');
  out.push('        case .number(let v): try c.encode(v)');
  out.push('        case .string(let v): try c.encode(v)');
  out.push('        case .array(let v): try c.encode(v)');
  out.push('        case .object(let v): try c.encode(v)');
  out.push('        }');
  out.push('    }');
  out.push('}');
  for (const m of models) {
    out.push('');
    out.push(`public struct ${m.name}: Codable, Equatable, Sendable {`);
    for (const p of m.props) {
      out.push(`    public var ${swiftId(p.name)}: ${p.swift}${p.optional ? '?' : ''}`);
    }
    out.push('');
    const params = m.props.map((p) => `${swiftId(p.name)}: ${p.swift}${p.optional ? '?' : ''}${p.optional ? ' = nil' : ''}`);
    if (params.length === 0) {
      out.push('    public init() {}');
    } else {
      out.push('    public init(');
      out.push(params.map((x) => `        ${x}`).join(',\n'));
      out.push('    ) {');
      for (const p of m.props) out.push(`        self.${swiftId(p.name)} = ${swiftId(p.name)}`);
      out.push('    }');
    }
    out.push('}');
  }
  return `${out.join('\n')}\n`;
}

// ── Kotlin ───────────────────────────────────────────────────────────────
function kotlinId(n) {
  return KOTLIN_KEYWORDS.has(n) ? `\`${n}\`` : n;
}

function emitKotlin() {
  const out = [];
  out.push(`// 自動產生，請勿手改。來源：shared/openapi.json；產生器：shared/scripts/gen-dto.mjs（docs/19 §2）。`);
  out.push(`// 只有資料型別（DTO），沒有 client：權杖續期、冪等鍵、離線佇列、退避重試由 App 的網路層自己負責。`);
  out.push(`// 時間欄位一律是 String（伺服器的 UTC ISO 8601 字串），解析交給 App 的時間工具。`);
  out.push(`// 可為空的欄位預設 null；Json 設定請用 ignoreUnknownKeys = true，讓後端新增欄位時舊版 App 不會解析失敗。`);
  out.push('');
  out.push(`package ${KOTLIN_PACKAGE}`);
  out.push('');
  out.push('import kotlinx.serialization.Serializable');
  out.push('import kotlinx.serialization.json.JsonElement');
  for (const m of models) {
    out.push('');
    if (m.props.length === 0) {
      out.push('@Serializable');
      out.push(`class ${m.name}`);
      continue;
    }
    out.push('@Serializable');
    out.push(`data class ${m.name}(`);
    out.push(
      m.props
        .map((p) => `    val ${kotlinId(p.name)}: ${p.kotlin}${p.optional ? '?' : ''}${p.optional ? ' = null' : ''}`)
        .join(',\n') + ',',
    );
    out.push(')');
  }
  return `${out.join('\n')}\n`;
}

mkdirSync(join(sharedDir, 'generated/swift'), { recursive: true });
mkdirSync(join(sharedDir, 'generated/kotlin'), { recursive: true });
writeFileSync(join(sharedDir, 'generated/swift/TcrfcApiModels.swift'), emitSwift());
writeFileSync(join(sharedDir, 'generated/kotlin/TcrfcApiModels.kt'), emitKotlin());
console.log(`dto: ${models.length} 個型別 → shared/generated/{swift,kotlin}/TcrfcApiModels.*`);
