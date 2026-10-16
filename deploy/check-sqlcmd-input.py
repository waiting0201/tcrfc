#!/usr/bin/env python3
"""deploy/check-sqlcmd-input.py — 靜態檢查：任何 sqlcmd 呼叫不得以 stdin 讀 SQL（E-296／E-330）。

為什麼：sqlcmd 以固定大小分塊讀 stdin，多位元組 UTF-8 字元剛好跨塊就被切成 U+FFFD（亂碼），結束碼 0、無警告。
規則：把 SQL 檔交給 sqlcmd 一律用 `-i <檔>`（先 docker cp 或掛載進容器），不得用 `< 檔`、`<<` heredoc、`cat 檔 | … sqlcmd`。
      含中文的檔另須經 deploy/lib/sqlcmd-guard.sh 的 sqlcmd_to_utf16（sqlcmd 13 讀 UTF-8 檔本身也會切壞，見該檔註解）。
掃描：repo 內所有 *.sh、*.yml／*.yaml（續行 `\\` 先接起來）。違規 exit 1。
例外：該行（或接起來的整段）含 `# sqlcmd-stdin-ok: 理由` 才放行（純 ASCII 且理由寫明）。
"""
import re
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
files = subprocess.run(["git", "ls-files", "*.sh", "*.yml", "*.yaml"], cwd=REPO, capture_output=True, text=True, check=True).stdout.split()
SELF = "deploy/check-sqlcmd-input.py"
bad = []
for rel in files:
    if rel == SELF:
        continue
    p = REPO / rel
    if not p.is_file():
        continue
    lines = p.read_text(encoding="utf-8", errors="replace").split("\n")
    i = 0
    while i < len(lines):
        start = i
        stmt = lines[i]
        while stmt.rstrip().endswith("\\") and i + 1 < len(lines):
            i += 1
            stmt = stmt.rstrip()[:-1] + " " + lines[i].strip()
        i += 1
        code = re.sub(r"^\s*#.*$", "", stmt)  # 整行註解不算
        if "sqlcmd" not in code or "sqlcmd-stdin-ok" in stmt:
            continue
        # 只看「真的在呼叫 sqlcmd」的敘述：排除 echo／ok／info／say／die／printf／warn 等訊息輸出
        if re.match(r"\s*(echo|ok|info|say|die|printf|warn|fail|log)\b", code):
            continue
        if not re.search(r"(^|[\s/(])sqlcmd(_container)?(\s|$)", code):
            continue
        # 只看 sqlcmd 之後的部分是否有 stdin 重導向；以及 sqlcmd 之前是否有管線餵入
        after = code.split("sqlcmd", 1)[1]
        before = code.split("sqlcmd", 1)[0]
        stdin_redirect = re.search(r"(^|\s)<(?![<(])\s*\S", after) or re.search(r"<<-?\s*['\"]?\w+", after)
        piped_in = re.search(r"(?<!\|)\|(?!\|)", before)
        if stdin_redirect or piped_in:
            bad.append(f"{rel}:{start + 1}: {stmt.strip()[:160]}")
# 另一半規則：會把含中文的 SQL 檔交給 sqlcmd 13 的正式庫腳本，必須呼叫 sqlcmd_to_utf16（E-330）
for rel in ("deploy/prod-db-init.sh", "deploy/prod-seed-import.sh", "deploy/db-migrate.sh"):
    if "sqlcmd_to_utf16" not in (REPO / rel).read_text(encoding="utf-8"):
        bad.append(f"{rel}: 沒有呼叫 sqlcmd_to_utf16（sqlcmd 13 讀 UTF-8 檔會切壞中文，E-330）")
if bad:
    print("違規：sqlcmd 不得以 stdin 讀 SQL（E-296／E-330；改用 docker cp＋-i，見 deploy/lib/sqlcmd-guard.sh）：", file=sys.stderr)
    print("\n".join(bad), file=sys.stderr)
    sys.exit(1)
print(f"OK：{len(files)} 個 sh／yml 檔，沒有 sqlcmd 以 stdin 讀 SQL")
