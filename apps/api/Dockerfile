# apps/api/Dockerfile — api（單一 .NET 行程，持兩個 DbContext）
#
# 🔴 本檔待 apps/api 專案建立後才能 build（S0-7a 階段目錄是空的，見 apps/api/README.md）。
# 對應決定：docs/17-deployment.md §0（API：.NET／C#，EF Core ＋ Dapper，單一 instance）、
#           docs/20-cicd.md §3（五個映像檔之一）。
#
# ⚠️ .NET 版本（10 LTS）：docs/17／docs/20 未明文指定 .NET 版本，這是本檔自行選定的預設值
# （2025-11 發布的 LTS，於本任務執行時點—2026-09—是現行 LTS）。backend-engineer 建立專案時
# 若選用其他版本，請同步改這裡的兩個 FROM tag（sdk／aspnet 要同版）。
#
# 連線字串、LINE Pay 憑證等一律 docker run 階段用環境變數／env_file 注入（docs/20-cicd.md §7.2），
# 本檔全程不接受任何機密相關的 ARG——映像檔會推到公開的 ghcr.io（docs/20-cicd.md §2）。

# ---- 1. build：還原 + 編譯 ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 先只複製專案檔以最大化還原快取命中率；NuGet 鎖定用 packages.lock.json（docs/20-cicd.md §3 快取策略）。
# 假設專案已開 <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile> 並提交 packages.lock.json
# ——這個檔案還不存在，此步驟會在專案建立前失敗，見檔頭「待專案建立後才能 build」。
COPY *.csproj packages.lock.json ./
RUN dotnet restore --locked-mode

COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# ---- 2. runtime：ASP.NET Core 執行環境，非 root ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# mcr.microsoft.com/dotnet 系列映像檔自 .NET 8 起內建非 root 使用者 "app"（uid 64198），
# 官方範本本就是 `USER app`，不需要自行建立使用者。
# 基底是 Debian slim，預設不含 curl，healthcheck 要用就得裝——體積換取「用一般人看得懂的方式探活」。
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0

COPY --from=build --chown=app:app /app/publish .
USER app
EXPOSE 8080

# /readyz：真的檢查兩個 DbContext 能連線；Redis 連線失敗算警告不算失敗（docs/17 §4「Redis 掛掉不得讓請求失敗」）
# 端點本身由應用程式實作（backend-engineer 建專案時補上），這裡只負責打它，docs/20-cicd.md §6。
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
  CMD curl -fsS http://127.0.0.1:8080/readyz || exit 1

# ⚠️ placeholder：主專案組件檔名待 apps/api/*.csproj 建立後確認，目前假設專案名為 Tcrfc.Api。
# 若 backend-engineer 採用不同名稱，這一行要同步改。
ENTRYPOINT ["dotnet", "Tcrfc.Api.dll"]
