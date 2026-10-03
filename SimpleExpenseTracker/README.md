# 日常記帳 SimpleExpenseTracker

手機優先的個人記帳 App，使用 React / TypeScript 與 ASP.NET Core Web API，資料永久儲存在本機 SQLite。目標功能為收支記錄、分類與帳戶管理及每月統計，不包含登入、雲端同步或帳戶餘額計算。

原始需求請見 [完整開發規格書](docs/SPECIFICATION.md)，保留最初提供的 40 節規格內容；目前實作與啟動方式以本 README 為準，測試結果見 [驗收紀錄](docs/VALIDATION.md)。

## 功能與開發狀態

目前功能包含交易新增／編輯／確認刪除、依日期分組的帳目、月份與收支類型篩選、分頁、分類新增／修改／停用／排序、帳戶管理、每月摘要與統計圖表。首頁最近交易顯示所選月份的最新 5 筆；趨勢固定呈現伺服器目前月份往前共 6 個月，不隨頁面選定月份改變。

- Phase 1：Solution、分層架構、EF Core InitialCreate、預設資料，已驗證並提交。
- Phase 2：交易／分類／帳戶 API、驗證、API 測試，已驗證並提交。
- Phase 3：手機記帳流程、帳目清單、新增／編輯／刪除，已驗證並提交。
- Phase 4：每月摘要、分類統計、六個月趨勢與圖表已實作。已使用系統安裝的 .NET 8.0.425 完成建置與後端測試，前端建置與測試亦通過。
- Phase 5：分類／帳戶管理、PWA、響應式版面與狀態處理已完成。
- Phase 6：完成重構、15 個後端測試、7 個前端測試及 7 個 MVP 情境驗收。

儲存庫已包含 `Dockerfile`、`compose.yaml` 與 `.dockerignore`，提供前後端整合的容器建置及 SQLite 資料保存設定。Docker 說明依這些設定檔整理，尚未實際建置或啟動驗證。

## Screenshots

以下截圖使用獨立驗收資料庫的示範資料，正式資料庫不會加入示範交易。

![桌面首頁](docs/screenshots/home-desktop.png)

[手機首頁](docs/screenshots/home-mobile.png) · [手機交易編輯](docs/screenshots/transaction-mobile.png) · [手機統計](docs/screenshots/statistics-mobile.png)

## Technology Stack

- .NET 8、ASP.NET Core Web API、EF Core 8、SQLite
- React 19、TypeScript strict、Vite 6、Tailwind CSS 3、Recharts 3
- xUnit、ASP.NET Core WebApplicationFactory、Vitest、Testing Library

## Prerequisites

本機開發需要：

- .NET 8 SDK（`global.json` 允許 8.0 的新版 feature band）
- Node.js 22 或以上、pnpm 11（可使用 `npm install -g pnpm@11`）
- Git

已使用系統 .NET SDK 8.0.425、ASP.NET Core 8.0.31 與 Node.js 24 驗證。請在此目錄執行 `dotnet --version`，確認 global.json 選到 8.0 SDK。

使用 Docker 時只需 Docker Engine／Docker Desktop（Linux 容器）及 Compose v2，主機不必另外安裝 Node.js、pnpm 或 .NET SDK。除非特別註明，下方命令都從本 README 所在的 `SimpleExpenseTracker/` 執行。

## Docker Compose 啟動

```sh
docker compose up -d --build
docker compose ps
docker compose logs -f app
```

Dockerfile 分三階段建置：Node.js 24 + pnpm 11 編譯前端、.NET 8 SDK 發佈 API，最後以 .NET 8 ASP.NET runtime 的非 root `app` 使用者執行。前端靜態檔案與 API 由同一個容器提供。

| 設定 | 目前值與用途 |
| --- | --- |
| 服務名稱 | `app` |
| 容器 HTTP port | `8080` |
| 主機 port | `5080` 與 `8080`，都連到同一個容器的 8080 |
| 環境 | `Production` |
| 連線字串 | `Data Source=/data/expense-tracker.db` |
| 資料保存 | Compose 具名 volume `expense-data` 掛載至 `/data` |
| 時區 | `Asia/Taipei` |
| Host 過濾 | `AllowedHosts=*` |
| 重啟策略 | `unless-stopped` |

開啟 [http://localhost:5080](http://localhost:5080) 或 [http://localhost:8080](http://localhost:8080)。健康檢查為 `/api/health`。這裡使用的是正式整合版本，不需要 Vite 的 5173 port。

更新程式後再次執行 `docker compose up -d --build`。停止並移除容器可執行 `docker compose down`，具名 volume 會保留；**不要加上 `-v`，否則會一併刪除帳目資料**。第一次執行會建立資料庫、套用 Migration 並加入預設分類與帳戶。

目前兩個 port 映射沒有綁定 loopback，Docker 可能將服務提供給主機其他網路介面。App 沒有登入驗證；若只需本機使用，可將映射改成 `127.0.0.1:5080:8080`，並移除不需要的第二個映射。`AllowedHosts` 是 Host 過濾設定，不是登入或存取權限控制。

若遇到 port 已被占用，請先停止使用 5080 的本機 API，或調整 Compose 的主機 port。可先執行 `docker compose config` 檢查設定，再執行 `docker compose up -d --build`。目前尚無容器建置與啟動的驗證紀錄。

### Docker 資料與備份

Compose 不會自動匯入本機開發的 `backend/SimpleExpenseTracker.Api/expense-tracker.db`；容器使用自己的具名 volume。`.dockerignore` 會排除資料庫、`.env`、依賴與建置輸出，避免將它們加入 image。

以下範例以 PowerShell 停止寫入後備份整個 `/data`（含可能存在的 SQLite WAL 檔案）至「文件」目錄，再恢復服務：

```powershell
$backupDir = Join-Path ([Environment]::GetFolderPath('MyDocuments')) "ExpenseTrackerBackups/$(Get-Date -Format yyyyMMdd-HHmmss)"
New-Item -ItemType Directory -Force $backupDir | Out-Null
docker compose stop app
docker compose cp app:/data/. $backupDir
# 確認上一行複製成功，再重新啟動。
docker compose start app
```

備份包含私人帳目，請勿加入 Git。更換 Compose 專案名稱或目錄可能建立不同名稱的 volume；更新部署時應維持相同的 Compose 專案名稱。

## Backend 啟動

在本 README 所在的 `SimpleExpenseTracker` 目錄執行：

```sh
dotnet restore
dotnet build
dotnet run --project backend/SimpleExpenseTracker.Api --urls http://127.0.0.1:5080
```

第一次啟動自動套用 Migration、建立 `backend/SimpleExpenseTracker.Api/expense-tracker.db` 並加入 16 個分類及 3 個帳戶。重啟不會新增重複資料。健康檢查：`GET http://127.0.0.1:5080/api/health`。

預設資料僅在分類與帳戶資料表皆為空時建立，不會逐項補回使用者刪除的預設項目。啟動紀錄中的 migration 訊息可確認資料庫是否正常初始化。

## Frontend 啟動

開啟另一個 Terminal：

```sh
cd frontend/simple-expense-tracker-web
pnpm install --frozen-lockfile
pnpm dev
```

開啟 `http://127.0.0.1:5173`。Vite 將 `/api` 代理至 5080，因此需同時啟動後端。

`pnpm preview` 只預覽前端建置結果，目前未設定 preview 的 API 代理；完整功能與 PWA 驗證請使用下方整合發佈方式或 Docker Compose。

```sh
pnpm build
pnpm test
```

## PWA 與正式發佈

先執行前端 `pnpm build`，它會產生靜態資源與版本化 Service Worker。再從本 README 所在目錄執行：

```sh
dotnet publish backend/SimpleExpenseTracker.Api -c Release -o publish
cd publish
dotnet SimpleExpenseTracker.Api.dll --urls http://127.0.0.1:5080
```

發佈流程自動將 frontend/dist 複製到 wwwroot；開啟 `http://127.0.0.1:5080` 即可使用整合版前後端，不需要另外啟動 Vite。若未先建置前端，publish 會明確失敗。

- Service Worker 僅於 production build 註冊，開發模式不註冊。
- 提供 192／512px PNG Icon、Apple Touch Icon 與 Manifest。支援的瀏覽器可從選單安裝；iOS Safari 使用「分享 → 加入主畫面」。
- 安裝與 Service Worker 需要安全來源：電腦本機 localhost 可用，手機連線需透過具受信任憑證的 HTTPS 網址，並設定 `AllowedHosts` 為使用的主機名稱。
- App 安裝後仍需要連到後端；資料儲存在後端 SQLite，不是在手機內獨立建立帳本。離線只快取程式外殼，顯示無法連線，不接受或排程交易寫入。
- `/api` 回應不進 Service Worker 快取。靜態資源更新在舊 App 視窗關閉後啟用，避免混用新舊版本。
- 正式使用建議將 `ConnectionStrings__Default` 指向發佈目錄外的固定絕對路徑，以免更新發佈檔時影響資料。

## Database Migration

```sh
dotnet tool restore
dotnet ef database update --project backend/SimpleExpenseTracker.Infrastructure --startup-project backend/SimpleExpenseTracker.Api
```

新增 Migration：

```sh
dotnet ef migrations add MigrationName --project backend/SimpleExpenseTracker.Infrastructure --startup-project backend/SimpleExpenseTracker.Api
```

可用 `ConnectionStrings__Default` 環境變數指定絕對資料庫路徑，例如 `Data Source=C:/data/expense-tracker.db`。預設相對路徑以 API 工作目錄為準。備份時先停止後端，再複製資料庫；不要刪除資料庫以「重置」程式。

手動執行 EF CLI 與使用 `dotnet run` 時，請確認工作目錄與連線字串指向同一份資料庫，避免在另一個目錄誤建空白帳本。可先在 PowerShell 設定：

```powershell
$databasePath = Join-Path (Get-Location) 'backend/SimpleExpenseTracker.Api/expense-tracker.db'
$env:ConnectionStrings__Default = "Data Source=$databasePath"
```

應在執行 `database update` 前設定；上述環境變數只影響目前 Terminal。Docker 已指定 `/data` 絕對路徑，通常透過容器啟動時自動套用 Migration 即可。

## Project Structure

```text
SimpleExpenseTracker/
  backend/
    SimpleExpenseTracker.Api/              HTTP controllers、DI、錯誤處理
    SimpleExpenseTracker.Application/      DTO、服務介面
    SimpleExpenseTracker.Domain/           Transaction、Category、Account
    SimpleExpenseTracker.Infrastructure/   DbContext、Migration、服務實作
    SimpleExpenseTracker.Tests/            SQLite 與 API 整合測試
  frontend/simple-expense-tracker-web/     React App
    public/                               Manifest、PWA 圖示
    scripts/build-sw.mjs                   產生版本化靜態快取
  docs/                                   驗收紀錄與截圖
  Dockerfile                              多階段建置
  compose.yaml                            容器、port 與資料 volume
  .dockerignore                           Docker build 排除清單
  SimpleExpenseTracker.sln
  README.md
```

## API Overview

| 路徑 | 方法／用途 |
| --- | --- |
| `/api/health` | GET：健康檢查 |
| `/api/transactions` | GET 分頁查詢、POST 新增 |
| `/api/transactions/{id}` | GET、PUT、DELETE |
| `/api/categories` | GET 全部分類（含停用）、POST |
| `/api/categories/{id}` | GET、PUT、DELETE |
| `/api/accounts` | GET 全部帳戶（含停用）、POST |
| `/api/accounts/{id}` | GET、PUT、DELETE |
| `/api/dashboard/summary?year=2026&month=10` | GET 月份收入／支出／結餘 |
| `/api/statistics/categories?year=2026&month=10&type=Expense` | GET 分類金額與百分比 |
| `/api/statistics/monthly?months=6` | GET 截至伺服器目前月份的連續月份統計，含零交易月份 |

交易查詢接受 `year, month, type, categoryId, accountId, page, pageSize`；指定 month 時必須提供 year。分頁預設 1／50，pageSize 最大 100。回傳 `{ items, total, page, pageSize }`，排序為日期、建立時間及 ID 遞減。

交易 JSON 範例（分類／帳戶 ID 需先 GET 查詢，不可假設固定值）：

```json
{
  "type": "Expense",
  "amount": 350,
  "categoryId": 1,
  "accountId": 3,
  "transactionDate": "2026-10-03",
  "note": "午餐"
}
```

成功使用 200／201／204；錯誤使用 400／404／500 與 `{ status, title, traceId }` ProblemDetails 格式。

## Testing

```sh
dotnet test
cd frontend/simple-expense-tracker-web
pnpm test
```

API 測試使用獨立暫存 SQLite 檔，不會碰觸開發資料。2026/10/03 的原生環境 MVP 驗收紀錄：15 個後端測試、7 個前端測試通過，後端建置零警告／零錯誤。這些結果不包含 Docker 容器建置與啟動驗證。

已透過 Edge 瀏覽器驗證新增收入／支出、修改、取消刪除、確認刪除、摘要更新、分類百分比、設定管理、PWA 靜態快取、離線錯誤與重新連線。375×667、390×844、412×915 與 1200×900 均無主要畫面水平溢位。詳細紀錄見 [驗收紀錄](docs/VALIDATION.md)。

曾出現 Windows Smart App Control 阻擋測試 DLL；安裝系統 .NET 8 SDK 後已成功重新執行全部測試。如果環境顯示「未探索到任何測試」，即使 exit code 為 0 也不能視為通過。

## Development Notes

- 金額以 C# decimal 與 SQLite TEXT 精確保存。輸入範圍 0.01–999999999999.99，最多兩位小數。統計在 C# decimal 中加總，不以 SQLite 浮點數 SUM 計算。
- 交易日期是無時區的日曆日期；API 接受 `yyyy-MM-dd`，不做 UTC 轉換。CreatedAt／UpdatedAt 使用 UTC。
- 類別與帳戶已被使用時，DELETE 改為停用並保留歷史關聯；未被使用則真正刪除。既有交易可保留原停用項目，新交易不可選擇。
- 已被交易使用的分類不得修改收入／支出類型，避免破壞歷史一致性。
- InitialBalance 保留但不參與摘要；摘要結餘只代表當月收入減支出。
- API 無登入，預設僅供本機個人開發使用；尚未建立公開服務部署。
- Git 未設定使用者作者，因此階段提交使用 `Codex <codex@localhost>`，未修改全域 Git 設定。
