#!/usr/bin/env bash
# apps/api/scripts/dev-azurite.sh
#
# 本機開發用 Azure Blob 替身（Azurite）。圖片／文件／影片上傳端點在沒有 AZURE_BLOB_CONNECTION_STRING 時
# 一律回「檔案儲存尚未設定」（正確行為），要在本機實際驗證上傳，先起這個容器，再用
# `dotnet run --launch-profile http-azurite` 啟動 API（見 apps/api/README.md「本機開發：Azurite」）。
#
# 用法：
#   apps/api/scripts/dev-azurite.sh up       # 啟動（已在跑就略過；容器停止了就接著跑；不存在就建立）
#   apps/api/scripts/dev-azurite.sh status   # 顯示狀態與連接埠
#   apps/api/scripts/dev-azurite.sh down     # 停止並移除容器（資料留在具名 volume，下次 up 還在）
#   apps/api/scripts/dev-azurite.sh reset    # 連同 volume 一併刪除（本機上傳的圖片全部消失）
#
# 設計決定：
#   - 映像檔版本與 docker-compose.dev.yml 的 azurite 服務一致（3.35.0），加 --skipApiVersionCheck
#     （SDK 預設送的 API 版本可能比 Azurite 認得的新，見 docs/17-deployment.md「本機開發物件儲存」）。
#   - 只發布 127.0.0.1:10000（Blob）。與 compose 的 azurite 服務**搶同一個連接埠**：兩者擇一，
#     compose 全套跑著時不要再 up 這個（本腳本偵測到 10000 被別的容器占用會拒絕並說明）。
#   - 帳號金鑰是 Azurite 專案公開文件的固定值，不是機密，連線字串因此可以直接寫在文件裡。
#   - 容器名稱 tcrfc-azurite、volume tcrfc_azurite_dev 專屬於本專案，只會動這兩個名字，不碰別的容器。
set -euo pipefail

NAME="tcrfc-azurite"
VOLUME="tcrfc_azurite_dev"
IMAGE="mcr.microsoft.com/azure-storage/azurite:3.35.0"
PORT="10000"

state() { local s; s="$(docker ps -a --filter "name=^${NAME}\$" --format "{{.State}}")"; echo "${s:-absent}"; }

port_owner() {
  docker ps --filter "publish=${PORT}" --format '{{.Names}}' | grep -vx "$NAME" || true
}

cmd="${1:-up}"
case "$cmd" in
  up)
    owner="$(port_owner)"
    if [[ -n "$owner" ]]; then
      echo "連接埠 ${PORT} 已被容器「${owner}」占用（可能是 docker-compose.dev.yml 的 azurite 服務）。" >&2
      echo "它同樣是 Azurite，API 直接連 127.0.0.1:${PORT} 即可，不需要再起這個。" >&2
      exit 1
    fi
    case "$(state)" in
      running) echo "已在執行：${NAME}" ;;
      absent)
        docker run -d --name "$NAME" --restart unless-stopped \
          -p "127.0.0.1:${PORT}:10000" -v "${VOLUME}:/data" "$IMAGE" \
          azurite-blob --blobHost 0.0.0.0 --blobPort 10000 --location /data --skipApiVersionCheck >/dev/null
        echo "已啟動：${NAME}（127.0.0.1:${PORT}）"
        ;;
      *) docker start "$NAME" >/dev/null; echo "已重新啟動：${NAME}" ;;
    esac
    echo "接著：cd apps/api && dotnet run --launch-profile http-azurite"
    ;;
  status)
    echo "${NAME}: $(state)"
    docker ps --filter "name=^${NAME}\$" --format '{{.Ports}}'
    ;;
  down)
    docker rm -f "$NAME" >/dev/null 2>&1 || true
    echo "已移除容器 ${NAME}（volume ${VOLUME} 保留）"
    ;;
  reset)
    docker rm -f "$NAME" >/dev/null 2>&1 || true
    docker volume rm "$VOLUME" >/dev/null 2>&1 || true
    echo "已移除容器與 volume（本機上傳的物件全部清除）"
    ;;
  *)
    echo "用法：$0 {up|status|down|reset}" >&2
    exit 2
    ;;
esac
