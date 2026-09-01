#!/bin/bash
# 從客戶收件夾重建 site/src/assets/img/。
#
# 為什麼照片不進版控：收件夾素材涉及個資與肖像權（含未成年學員照片），
# 授權尚未逐項確認前不放進有遠端的 repo（見 CLAUDE.md §7、site/README.md）。
# 照片是從收件夾轉檔產生的衍生物，用這支腳本重建即可。
#
# 用法：bash site/tools/build-images.sh
# 需求：macOS 的 sips；收件夾需存在於專案根目錄。

set -uo pipefail
cd "$(dirname "$0")/../.." || exit 1

IN="TCRFC_資料收件夾"
OUT="site/src/assets/img"

if [ ! -d "$IN" ]; then
  echo "找不到 $IN／" >&2
  echo "請先向客戶取得收件夾並放到專案根目錄。" >&2
  exit 1
fi

mkdir -p "$OUT"/{news,programs,academy,club,fanclub,merch,partners-intl}

# 轉檔：$1 來源 $2 目的 $3 長邊上限
conv() {
  [ -f "$1" ] || return 0
  sips -s format jpeg -Z "${3:-1600}" "$1" --out "$2" >/dev/null 2>&1
}

echo "▸ 首頁主視覺與導覽用圖"
HERO="$IN/01_HOME_首頁★/01_Hero主視覺（橫向大圖或影片，長邊2400px以上）"
# 首頁 hero 輪播（規劃書 §3.1：最多 5 則）。客戶放了 6 張，其中 3 張為直式、
# 在寬版 hero 會裁切過度，因此只取橫式三張。
conv "$HERO/260308_1938_TCRFC_MS103557.jpg" "$OUT/hero-01.jpg" 2400
conv "$HERO/260308_1909_TCRFC_MS103383.jpg" "$OUT/hero-02.jpg" 2400
conv "$HERO/260308_1931_TCRFC_MS103465.jpg" "$OUT/hero-03.jpg" 2400
i=0
for f in "$IN/00_品牌與共用素材★/03_通用照片（球場、訓練、觀眾、空拍）"/*.jpg; do
  case $i in
    0) n=nav-about;;  1) n=nav-club;;     2) n=nav-academy;; 3) n=nav-programs;;
    4) n=nav-news;;   5) n=nav-culture;;  6) n=nav-partners;; *) break;;
  esac
  conv "$f" "$OUT/$n.jpg" 880
  i=$((i+1))
done

echo "▸ 新聞封面（依 site/src/data/news.json 的 cover_web 對應）"
python3 - <<'PY'
import json, os, shlex, subprocess
IN = "TCRFC_資料收件夾/07_NEWS_新聞與故事★"
CAT = {
    'club': '7.1_俱樂部新聞（每篇一資料夾：YYYY-MM-DD_標題）', 'match': '7.2_比賽',
    'academy': '7.3_學院新聞', 'player': '7.4_球員故事', 'international': '7.5_國際動態',
    'camps': '7.6_營隊與活動', 'community': '7.7_社區活動', 'intcup': '7.9_台中磐石國際足球盃',
}
news = json.load(open('site/src/data/news.json'))
ok = miss = 0
for n in news:
    if not n.get('cover_web'):
        continue
    src = os.path.join(IN, CAT[n['category']], n['source_folder'], n['cover'])
    dst = os.path.join('site/src/assets/img/news', n['slug'] + '.jpg')
    if os.path.exists(src):
        subprocess.run(['sips', '-s', 'format', 'jpeg', '-Z', '1600', src, '--out', dst],
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        ok += 1
    else:
        miss += 1
print(f"  新聞封面 {ok} 張" + (f"（{miss} 張來源不存在）" if miss else ""))
PY

echo "▸ 各單元照片"
# 目的檔名前綴 ← 來源資料夾（依序編號）
copy_dir() {
  local prefix="$1" subdir="$2" srcdir="$3" limit="${4:-99}"
  local i=1
  find "$srcdir" -maxdepth 1 -type f \( -iname '*.jpg' -o -iname '*.png' -o -iname '*.avif' -o -iname '*.webp' \) \
    2>/dev/null | sort | while read -r f; do
      [ "$i" -gt "$limit" ] && break
      conv "$f" "$OUT/$subdir/$(printf '%s-%02d.jpg' "$prefix" "$i")"
      i=$((i+1))
    done
}

copy_dir childrens   programs "$IN/05_PROGRAMS_課程與活動/5.1_兒童足球訓練★（課表、地點、分級說明、照片）/台中磐石足球節"
copy_dir summer-camp programs "$IN/05_PROGRAMS_課程與活動/5.2_夏令營★（簡章、日期地點、費用、往年花絮）"
copy_dir specialist  programs "$IN/05_PROGRAMS_課程與活動/5.4_專項訓練（守門員／前鋒／後衛／中場／體能／高階）/台中磐石成人足球班"
copy_dir life        academy  "$IN/04_ACADEMY_磐石足球學院/4.6_學生生活（訓練日常、比賽、活動照片）"
copy_dir fanclub-event fanclub "$IN/08_CULTURE_磐石文化/8.2_球迷會（方案、福利對照、活動照片）"

echo "▸ mockup 沿用的素材（已在版控內，直接複製）"
for f in news-mcu news-trencin news-w20 \
         player-09-liu player-11-yang player-27-shi player-44-yamauchi player-77-lin \
         trencin-01 trencin-02 trencin-03 trencin-04 trencin-05; do
  cp "mockup/assets/$f.jpg" "$OUT/$f.jpg" 2>/dev/null
done

echo "▸ 一線隊、學院教練、官方商品"
conv "$IN/03_FOOTBALL_CLUB_一線隊與球員發展/3.1_一線隊D1★/01_球隊介紹與合照/260510_1756_TCRFC_MS103332.jpg" "$OUT/club/first-team-01-squad.jpg" 1920
conv "$IN/03_FOOTBALL_CLUB_一線隊與球員發展/3.1_一線隊D1★/05_榮譽紀錄與獎盃照/MS101724.jpg"                    "$OUT/club/first-team-02-trophy.jpg" 1920
conv "$IN/04_ACADEMY_磐石足球學院/4.5_學院教練團（每人一資料夾：姓名）/青訓教練_許志傑/2507181490_edited.avif" "$OUT/academy/coach-hsu-chih-chieh.jpg" 800

# 襪子色卡順序不可弄錯（頁面 alt 標了色名）：
# file.jpg = 六色排列 → 01；file (1)~(6) = 向日黃／經典紅／櫻桃紅／海軍藍／極簡黑／純淨白 → 02~07
SOCK="$IN/08_CULTURE_磐石文化/8.3_官方商品（商品照、系列介紹、Shopify連結）/厚底緩震機能襪"
conv "$SOCK/file.jpg" "$OUT/merch/merch-socks-01.jpg"
for n in 1 2 3 4 5 6; do
  conv "$SOCK/file ($n).jpg" "$OUT/merch/merch-socks-0$((n+1)).jpg"
done
conv "$IN/08_CULTURE_磐石文化/8.3_官方商品（商品照、系列介紹、Shopify連結）/台中磐石主場球衣｜2026賽季/file.jpg" "$OUT/merch/merch-jersey-01.jpg"

echo "▸ 國際合作俱樂部標誌"
D="$IN/03_FOOTBALL_CLUB_一線隊與球員發展/3.3_國際發展通道（歐洲／日本／香港、合作俱樂部Logo）"
cp "$D/Hellas_Verona_FC_logo_(2020).svg.webp" "$OUT/partners-intl/partner-intl-01-hellas-verona.webp" 2>/dev/null
cp "$D/Rayo_Ciudad_Alcobendas_CF.png"          "$OUT/partners-intl/partner-intl-02-rayo-alcobendas.png" 2>/dev/null
cp "$D/Rot_Weiss_Ahlen.svg.webp"               "$OUT/partners-intl/partner-intl-03-rot-weiss-ahlen.webp" 2>/dev/null

echo
echo "完成。共 $(find "$OUT" -type f | wc -l | tr -d ' ') 個檔案，$(du -sh "$OUT" | cut -f1)"
echo
echo "注意：這些照片含未成年學員肖像。上線前須逐項確認授權，"
echo "      未完成前不得移除 site/src/_headers 的 noindex（見 site/README.md）。"
