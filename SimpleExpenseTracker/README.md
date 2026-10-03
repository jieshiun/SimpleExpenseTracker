# 日常記帳 SimpleExpenseTracker

手機優先的個人記帳 App，使用 React / TypeScript 與 ASP.NET Core Web API，資料永久儲存在本機 SQLite。目標功能為收支記錄、分類與帳戶管理及每月統計，不包含登入、雲端同步或帳戶餘額計算。

## 開發狀態

- Phase 1：Solution、分層架構、EF Core InitialCreate、預設資料，已驗證並提交。
- Phase 2：交易／分類／帳戶 API、驗證、API 測試，已驗證並提交。
- Phase 3：手機記帳流程、帳目清單、新增／編輯／刪除，已驗證並提交。
- Phase 4：每月摘要、分類統計、六個月趨勢與圖表已實作。已使用系統安裝的 .NET 8.0.425 完成建置與後端測試，前端建置與測試亦通過。
- Phase 5：設定管理畫面與 PWA 尚未實作。
- Phase 6：完整驗收、重構與最終測試尚未完成。

## Screenshots

預留最終版首頁、記帳表單與統計截圖位置。目前的開發中手機統計截圖為 `phase4-mobile.png`。

## Technology Stack

- .NET 8、ASP.NET Core Web API、EF Core 8、SQLite
- React 19、TypeScript strict、Vite 6、Tailwind CSS 3、Recharts 3
- xUnit、ASP.NET Core WebApplicationFactory、Vitest、Testing Library

## Prerequisites

- .NET 8 SDK（`global.json` 允許 8.0 的新版 feature band）
- Node.js 22 或以上、pnpm 11（可使用 `npm install -g pnpm@11`）
- Git

此環境原本只有 .NET 10，因此另將 .NET 8 安裝於 repository 根目錄的 `.tools/dotnet`，未納入 Git。一般開發者安裝 .NET 8 後直接使用以下命令。

## Backend 啟動

在本 README 所在的 `SimpleExpenseTracker` 目錄執行：

```sh
dotnet restore
dotnet build
dotnet run --project backend/SimpleExpenseTracker.Api --urls http://127.0.0.1:5080
```

第一次啟動自動套用 Migration、建立 `backend/SimpleExpenseTracker.Api/expense-tracker.db` 並加入 16 個分類及 3 個帳戶。重啟不會新增重複資料。健康檢查：`GET http://127.0.0.1:5080/api/health`。

本機專用 SDK 的 PowerShell 啟動方式：

```powershell
$env:DOTNET_ROOT = (Resolve-Path ../.tools/dotnet).Path
& "$env:DOTNET_ROOT/dotnet.exe" run --project backend/SimpleExpenseTracker.Api --urls http://127.0.0.1:5080
```

## Frontend 啟動

開啟另一個 Terminal：

```sh
cd frontend/simple-expense-tracker-web
pnpm install --frozen-lockfile
pnpm dev
```

開啟 `http://127.0.0.1:5173`。Vite 將 `/api` 代理至 5080，因此需同時啟動後端。

```sh
pnpm build
pnpm test
```

目前 production build 的 `dist` 需要由具備 `/api` 反向代理的同源伺服器提供，尚未加入正式發佈設定。

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

API 測試使用獨立暫存 SQLite 檔，不會碰觸開發資料。Phase 3 曾完整通過 6 個後端測試、4 個前端測試，並以 Edge 實際完成 390px 手機新增／編輯／刪除。

2026/10/03 的 Phase 4 測試遭 Windows Smart App Control 封鎖 `SimpleExpenseTracker.Tests.dll`（CodeIntegrity Event 3077、錯誤 0x800711C7）。此時 `dotnet test` 可能回傳 exit code 0，但顯示未探索到任何測試，**不表示通過**。需在允許此開發程式碼的環境重新執行；未修改作業系統安全政策。後續安裝系統 .NET 8 SDK 8.0.425 後已重新成功執行全部 13 個測試（含 5 個繼承重複案例），先前阻擋已解除。

## Development Notes

- 金額以 C# decimal 與 SQLite TEXT 精確保存。輸入範圍 0.01–999999999999.99，最多兩位小數。統計在 C# decimal 中加總，不以 SQLite 浮點數 SUM 計算。
- 交易日期是無時區的日曆日期；API 接受 `yyyy-MM-dd`，不做 UTC 轉換。CreatedAt／UpdatedAt 使用 UTC。
- 類別與帳戶已被使用時，DELETE 改為停用並保留歷史關聯；未被使用則真正刪除。既有交易可保留原停用項目，新交易不可選擇。
- 已被交易使用的分類不得修改收入／支出類型，避免破壞歷史一致性。
- InitialBalance 保留但不參與摘要；摘要結餘只代表當月收入減支出。
- API 無登入，預設僅供本機個人開發使用；尚未建立公開服務部署。
- Git 未設定使用者作者，因此階段提交使用 `Codex <codex@localhost>`，未修改全域 Git 設定。

