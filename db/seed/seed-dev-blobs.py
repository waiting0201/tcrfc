#!/usr/bin/env python3
"""db/seed/seed-dev-blobs.py — 把本機開發需要的 Blob 容器與占位圖準備進 Azurite（只給本機）。

做兩件事：
  1. 建立 images／videos／documents（公開讀取）與 proposals（私有）。
     🔴 為什麼要預先建：API 自己建立容器時一律 PublicAccessType.None（正式環境的公開讀取由 Bicep 設定，
     見 docs/17 §13）。本機若讓 API 自建，瀏覽器直接讀圖片網址會 404／403，後台縮圖與前台漫畫閱讀器都看不到圖。
     容器已存在時 API 不會改它的存取層級，所以這支腳本要在第一次上傳之前跑（已建成私有的可用 --fix-access 改）。
  2. 把 db/seed 區段 60 的漫畫第 101、102 集占位圖（封面＋3 頁，各含 1280／640／320／thumb 衍生檔）傳進 images 容器，
     物件鍵與 comic_pages.image_key／comic_episodes.cover_key 一致（seed-dev/comic/{集}/{頁}.webp）。

用法（先 apps/api/scripts/dev-azurite.sh up）：
  python3 db/seed/seed-dev-blobs.py            # 建容器＋傳占位圖（冪等，可重跑）
  python3 db/seed/seed-dev-blobs.py --fix-access   # 把已存在的 images／videos／documents 改為公開讀取

需要：pip install azure-storage-blob pillow。連線字串預設 UseDevelopmentStorage=true（Azurite 的固定公開帳號，非機密）；
可用 AZURE_BLOB_CONNECTION_STRING 覆寫，但本腳本拒絕非本機端點，避免誤對正式儲存體動手。
"""
import io
import os
import sys

from azure.core.exceptions import ResourceExistsError
from azure.storage.blob import BlobServiceClient, ContentSettings
from PIL import Image, ImageDraw

CONN = os.environ.get("AZURE_BLOB_CONNECTION_STRING", "UseDevelopmentStorage=true")
if "UseDevelopmentStorage=true" not in CONN and "127.0.0.1" not in CONN and "localhost" not in CONN and "azurite" not in CONN:
    sys.exit("拒絕執行：連線字串看起來不是本機 Azurite。這支腳本只給本機開發用。")

PUBLIC = ["images", "videos", "documents"]
PRIVATE = ["proposals"]
LONG_EDGES = [1280, 640, 320]


def placeholder(label: str, w: int, h: int, color: tuple) -> Image.Image:
    img = Image.new("RGB", (w, h), color)
    d = ImageDraw.Draw(img)
    d.rectangle([10, 10, w - 11, h - 11], outline=(255, 255, 255), width=6)
    d.text((w // 2 - 60, h // 2 - 10), label, fill=(255, 255, 255))
    return img


def webp_bytes(img: Image.Image) -> bytes:
    buf = io.BytesIO()
    img.save(buf, "WEBP", quality=80)
    return buf.getvalue()


def upload_set(container, main_key: str, img: Image.Image) -> None:
    """依 ImageObjectKey 的規則寫五個物件：主檔、-1280／-640／-320、-thumb（160 方形）。"""
    stem = main_key[: -len(".webp")]
    settings = ContentSettings(content_type="image/webp", cache_control="public, max-age=31536000, immutable")
    container.upload_blob(main_key, webp_bytes(img), overwrite=True, content_settings=settings)
    for edge in LONG_EDGES:
        scale = min(1.0, edge / max(img.size))
        sized = img.resize((max(1, round(img.width * scale)), max(1, round(img.height * scale))))
        container.upload_blob(f"{stem}-{edge}.webp", webp_bytes(sized), overwrite=True, content_settings=settings)
    side = min(img.size)
    square = img.crop(((img.width - side) // 2, (img.height - side) // 2, (img.width + side) // 2, (img.height + side) // 2)).resize((160, 160))
    container.upload_blob(f"{stem}-thumb.webp", webp_bytes(square), overwrite=True, content_settings=settings)


def main() -> None:
    fix_access = "--fix-access" in sys.argv
    svc = BlobServiceClient.from_connection_string(CONN)

    for name in PUBLIC + PRIVATE:
        access = "blob" if name in PUBLIC else None
        try:
            svc.create_container(name, public_access=access)
            print(f"已建立容器 {name}（{'公開讀取' if access else '私有'}）")
        except ResourceExistsError:
            if fix_access and access:
                svc.get_container_client(name).set_container_access_policy(signed_identifiers={}, public_access=access)
                print(f"已改為公開讀取：{name}")
            else:
                print(f"容器已存在：{name}（存取層級不變；要改成公開讀取請加 --fix-access）")

    images = svc.get_container_client("images")
    palette = {101: (40, 90, 160), 102: (160, 70, 60)}
    for episode, color in palette.items():
        upload_set(images, f"seed-dev/comic/{episode}/cover.webp", placeholder(f"EP{episode} COVER", 800, 1200, color))
        for page in (1, 2, 3):
            shade = tuple(min(255, c + page * 25) for c in color)
            upload_set(images, f"seed-dev/comic/{episode}/{page}.webp", placeholder(f"EP{episode} P{page}", 800, 1200, shade))
        print(f"已上傳第 {episode} 集占位圖（封面＋3 頁，各 5 個物件）")


if __name__ == "__main__":
    main()
