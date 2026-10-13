#!/usr/bin/env python3
"""deploy/schema-diff.py — 比對兩份 deploy/schema-snapshot.sql 的輸出，印出人看得懂的差異清單。

用法：schema-diff.py --a-label "DDL" --b-label "EF 模型" --mode ef|same-origin A.txt B.txt
結束碼：0 一致、1 有差異。標準庫即可，不需 pip。

--mode ef          B 側是由 EF 模型 `dotnet ef dbcontext script` 建出來的庫。EF 描述不到的東西走 EF_EXCLUDES。
--mode same-origin 兩邊都是 DDL 建的（例如本機 tcrfc_club vs 原樣 DDL），不排除任何東西。

🔴 排除清單的紀律（docs/20 §3「schema-drift」）：每一條都必須寫明「為什麼 EF 本來就不描述它」，
並且在報告裡印出「被排除了幾行」，不得悄悄吞掉。排除清單只准排「整個物件類別」，
不准排個別表或個別欄位——個別的真差異一律要報。
"""
import argparse
import re
import sys
from collections import defaultdict

# (前綴正規式, 理由)。比對對象是快照的每一行。
EF_EXCLUDES = [
    (r"^VIEW\|", "視圖（calendar_events 等）：EF 只能把視圖當 keyless entity 對映（ToView），不會建立視圖本體；視圖由 DDL 獨有，"
                 "存在性與欄位由 same-origin 模式（本機庫 vs DDL）與 prod-db-init.sh verify 的視圖數核對"),
    (r"^VIEWCOL\|", "同上（視圖輸出欄位）"),
]

# 單向容忍：只在 A（DDL）有、EF 側沒有的這幾類，不算差異，但一定印出數量與理由。
# 理由：EF 模型不描述它們（慈善 CharityDbContext 沒有任何 HasCheckConstraint；兩個 context 的 bit／字串欄位多半沒宣告
# HasDefaultValue），DDL 比模型更嚴格／更完整是設計上的常態，EF 寫入時一律明確帶值，不依賴資料庫預設。
# 反方向（EF 有、DDL 沒有）與「兩邊都有但內容不同」（預設值被配成 changed）仍然照報——那才是『程式以為有、資料庫沒有』。
# ⚠️ 已知盲點：只在 DDL、也不在模型的 CHECK／預設值被悄悄改掉，這裡看不出來（本機庫落後用 local-vs-ddl 模式發現）。
A_ONLY_TOLERATED = [
    (r"^CK\|", "CHECK 約束：EF 模型多半不描述（DDL 獨有）"),
    (r"^NAME\|CK\|", "CHECK 約束名稱：同上"),
    (r"^DEFAULT\|", "欄位預設值：EF 模型未宣告 HasDefaultValue 的欄位（DDL 獨有）"),
]

LABELS = {
    "TABLE": "資料表", "COL": "欄位（型別／可空／identity／計算欄位）", "DEFAULT": "預設值",
    "PK": "主鍵", "IDX": "索引（含唯一約束）", "NAME": "索引／約束名稱",
    "FK": "外鍵", "CK": "CHECK 約束", "VIEW": "視圖", "VIEWCOL": "視圖欄位", "OTHER": "其他物件（觸發程序／預存程序／函式／序列）",
}


def canon_expr(e):
    """預設值／CHECK 定義的正規化（兩邊同一套）：SQL Server 會把 EF 與手寫 DDL 的同一個意思存成不同排版。
    - n'x' → 'x'（EF 對字串常值加 N 前綴）
    - convert(bit,X) → X（EF 對 bit 預設值加的轉型）
    - 純數字外的多餘括號：(1) → 1；最外層成對括號反覆去掉
    只做這幾條『不改變語意』的改寫；不碰運算子與欄位名，所以真的不同的 CHECK／預設值仍會報。"""
    prev = None
    while prev != e:
        prev = e
        e = re.sub(r"(?<![a-z0-9_])n'", "'", e)
        e = re.sub(r"convert\(bit,(.+?)\)(?=$|[)o])", r"\1", e) if e.startswith(("(convert(bit", "convert(bit")) else e
        e = re.sub(r"\((-?\d+(?:\.\d+)?)\)", r"\1", e)
        while e.startswith("(") and e.endswith(")") and balanced(e[1:-1]):
            e = e[1:-1]
    return e


def balanced(s):
    d = 0
    for ch in s:
        d += ch == "("
        d -= ch == ")"
        if d < 0:
            return False
    return d == 0


def normalize_line(line):
    p = line.split("|")
    if p[0] == "DEFAULT" and len(p) >= 4:
        p[3] = canon_expr("|".join(p[3:])); p = p[:4]
    elif p[0] == "CK" and len(p) >= 3:
        p[2] = canon_expr("|".join(p[2:])); p = p[:3]
    return "|".join(p)


def load(path):
    out = set()
    with open(path, encoding="utf-8-sig") as f:
        for line in f:
            line = line.rstrip("\r\n").strip()
            if not line or line.startswith("(") and line.endswith("affected)"):
                continue
            out.add(normalize_line(line))
    return out


def kind(line):
    return line.split("|", 1)[0]


def pair_key(line):
    """欄位與預設值用 (類別, 表, 欄位) 配對，讓「型別改了」顯示成一筆 A→B，而不是兩筆孤立的差異。"""
    p = line.split("|")
    k = p[0]
    if k in ("COL", "DEFAULT") and len(p) >= 3:
        return (k, p[1], p[2])
    return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--a-label", required=True)
    ap.add_argument("--b-label", required=True)
    ap.add_argument("--mode", choices=["ef", "same-origin"], required=True)
    ap.add_argument("a")
    ap.add_argument("b")
    args = ap.parse_args()

    A, B = load(args.a), load(args.b)
    if not A or not B:
        print("快照是空的（連線失敗或資料庫沒有任何物件）——不能當作『一致』。", file=sys.stderr)
        return 2

    if args.mode == "ef":
        print("排除清單（EF 本來就不描述的物件類別；數量如下，不是悄悄略過）：")
        for pat, why in EF_EXCLUDES:
            rx = re.compile(pat)
            na, nb = sum(1 for x in A if rx.search(x)), sum(1 for x in B if rx.search(x))
            print(f"  - /{pat}/  {args.a_label} {na} 行、{args.b_label} {nb} 行  ← {why}")
            A = {x for x in A if not rx.search(x)}
            B = {x for x in B if not rx.search(x)}
        print()

    only_a, only_b = sorted(A - B), sorted(B - A)
    b_by_key = defaultdict(list)
    for x in only_b:
        k = pair_key(x)
        if k:
            b_by_key[k].append(x)
    tolerated = []
    if args.mode == "ef":
        trx = [(re.compile(p), why) for p, why in A_ONLY_TOLERATED]
        keep = []
        for x in only_a:
            k = pair_key(x)
            if k and b_by_key.get(k):          # 預設值：EF 側對同一欄位有不同的值 → 真差異，不容忍
                keep.append(x)
                continue
            if any(rx.search(x) for rx, _ in trx):
                tolerated.append(x)
            else:
                keep.append(x)
        only_a = keep
        if tolerated:
            print("單向容忍（只在 DDL 有、EF 模型不描述；不算差異，列出數量以免悄悄吞掉）：")
            for pat, why in A_ONLY_TOLERATED:
                n = sum(1 for x in tolerated if re.search(pat, x))
                print(f"  - /{pat}/  {n} 行  ← {why}")
            print()
    if not only_a and not only_b:
        print(f"一致：{args.a_label} 與 {args.b_label} 共 {len(A)} 項結構事實完全相同。")
        return 0

    # 配對欄位／預設值的變更
    changed, a_left = [], []
    used_b = set()
    for x in only_a:
        k = pair_key(x)
        if k and b_by_key.get(k):
            y = b_by_key[k].pop(0)
            used_b.add(y)
            changed.append((k, x, y))
        else:
            a_left.append(x)
    b_left = [x for x in only_b if x not in used_b]

    print(f"🔴 發現差異：{args.a_label} 與 {args.b_label} 不一致"
          f"（改變 {len(changed)}、只在 {args.a_label} {len(a_left)}、只在 {args.b_label} {len(b_left)}）")
    print()

    def section(title, items, fmt):
        by = defaultdict(list)
        for it in items:
            by[kind(it[1]) if isinstance(it, tuple) else kind(it)].append(it)
        if not by:
            return
        print(title)
        for k in sorted(by):
            print(f"  [{LABELS.get(k, k)}] {len(by[k])} 項")
            for it in by[k]:
                print("    " + fmt(it))
        print()

    section("── 兩邊都有、但內容不同 ──", changed,
            lambda t: f"{t[0][1]}.{t[0][2]}\n        {args.a_label}: {'|'.join(t[1].split('|')[3:]) or t[1]}\n        {args.b_label}: {'|'.join(t[2].split('|')[3:]) or t[2]}")
    section(f"── 只在 {args.a_label} 有 ──", a_left, lambda s: s)
    section(f"── 只在 {args.b_label} 有 ──", b_left, lambda s: s)
    return 1


if __name__ == "__main__":
    sys.exit(main())
