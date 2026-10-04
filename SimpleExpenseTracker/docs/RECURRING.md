# 週期性固定收支

新增「固定收支」頁，可設定每週／每月／每年，以及每 N 個週期的收入或支出。每個範本指定分類、帳戶、正數金額、固定或浮動金額、起訖日期、共同或成員歸屬。浮動金額先依預估值入帳，再到帳目頁修改實際值；修改實際帳目不會改變範本。

## 本次採用的規則

- 依使用者確認沿用單一家庭共用帳本，沒有 FamilyId、多家庭隔離或登入。MemberId 為 null 表示家庭共同；有值必須是帳本中存在的成員。
- 金額沿用既有規則：0.01 至 999999999999.99，最多兩位小數。間隔為 1 至 120。
- 必須指定帳戶，確保自動帳目符合現有交易模型。
- 每月 31 日遇到短月使用月底，下一個月回到原指定日；每年 2/29 在非閏年使用 2/28。
- 起訖日包含當天；結束後 NextRunDate 為 null。新增過去開始的範本會補產生到今天。停用後重新啟用從今天重新對齊原規則，不補停用期間。
- 修改啟用範本前先用舊設定補齊到期帳目。變更排程從今天或最後產生日的翌日起重新計算；已產生帳目不變。
- 分類、帳戶或成員停用時，保留範本、顯示原因並暫停產生。恢復相關項目後可補產生積欠帳目。
- 系統產生帳目沒有人工操作人成員，畫面顯示「系統自動產生」。後續人工編輯仍沿用操作人與版本檢查。

## Schema 與 Migration

Migration：`20261004151951_AddRecurringTransactions`。

新增 RecurringTransactions：名稱、類型、分類、帳戶、成員、金額／金額類型、週期／間隔／指定星期與月日、開始／結束日、下一次／最後產生日、狀態、備註、建立／更新時間、版本與建立重送識別碼／雜湊。Version 是 EF concurrency token；ClientRequestId 有唯一索引；(IsActive, NextRunDate) 有查詢索引。

Transactions 增加 nullable RecurringTransactionId FK 與 RecurringOccurrenceDate，既有交易保持 null。建立過濾唯一索引 `(RecurringTransactionId, RecurringOccurrenceDate) WHERE RecurringTransactionId IS NOT NULL`。來源與目錄外鍵採 Restrict，停用範本不會刪除歷史帳目。

## API

基底 `/api/recurring-transactions`，沿用 ProblemDetails 與 X-Ledger-Generation 寫入識別碼。

| 方法 | 路徑 | 用途 |
| --- | --- | --- |
| GET | `/` | 清單，memberId、ownership、type、isActive 篩選 |
| GET | `/{id}` | 詳細範本與版本／警告 |
| POST | `/` | 新增，選填 ClientRequestId 防重送 |
| PUT | `/{id}` | 修改，必須帶目前 version |
| PATCH | `/{id}/status` | `{ "isActive": false, "version": 1 }` |
| DELETE | `/{id}` | body `{ "version": 1 }`，實際為停用 |
| GET | `/summary` | 預估，支援 year、month 與清單篩選 |
| GET | `/upcoming` | days 預設 30（1–366），依下次日排序 |
| POST | `/generate` | 立即補產生，回傳 generated／duplicates／blocked／hasMore |

範例（ID 應從現有目錄取得）：

```json
{
  "name": "手機費", "type": "Expense", "categoryId": 1, "accountId": 1,
  "memberId": null, "amount": 599, "amountType": "Variable",
  "frequency": "Monthly", "interval": 1, "dayOfMonth": 10,
  "dayOfWeek": null, "monthOfYear": null,
  "startDate": "2026-10-10", "endDate": null, "isActive": true, "note": null
}
```

Summary 是目前啟用範本在指定日曆月／年的實際排程次數乘預估金額，考慮開始、結束與間隔；包含 Fixed 和 Variable。它不是歷史實際報表，範本金額變更會改變預估。既有摘要與圖表只統計 Transactions。ActiveCount 是啟用範本數，可能包含未開始或已結束範本。Upcoming 每個範本顯示一個下一次日期，也包含待補的過期項目。

## 背景執行與防重複

RecurringTransactionBackgroundService 啟動後立即執行，再每小時檢查。ILocalDateProvider 使用 ApplicationTimeZone（預設 Asia/Taipei）判斷今天，稽核時間保留 UTC。測試可關閉 `Recurring:Enabled`；容器環境變數是 `Recurring__Enabled` 與 `ApplicationTimeZone`。

每個範本在 SQLite 寫入交易鎖取得後重讀，帳目與最後／下一次日期在同一 database transaction 提交。逐期檢查所有來源帳目，包括軟刪除資料；唯一索引作最後防線。程序中斷未提交即回滾，重啟重新補產生。記錄來源 ID、期別、帳目 ID、金額與例外。

每個範本每次最多補 1000 期，hasMore 表示下次檢查繼續，或再次按「立即補產生」。還原維護期間背景工作暫停，下一次檢查恢復；避免排程與整本還原互相干擾。

## 備份與容器更新

完整匯出與匯入包含範本、來源期別與排程進度，預覽顯示範本數。可直接匯入已知的原始個人版及家庭版備份：先核對歷史 schema 與 migration，再在暫存副本升級、驗證與預覽。確認還原後才取代帳本，詳見 BACKUP.md。

部署前保留資料 volume，匯出現有帳本並另外保存。重新建置容器後，API 啟動會套用 migration，隨即補產生到期帳目。部署指令見 README 的「更新既有容器」。本機未安裝 Docker CLI，此輪未更新實際容器或驗證 Docker 建置。

## 驗證與人工確認

2026/10/04：後端 Release 測試涵蓋日曆計算、台灣時區、API CRUD／版本／預估／即將發生、並行補帳、軟刪除不重建、浮動實際金額、停用參考、背景啟動及再次重啟、範本與來源備份還原。前端測試涵蓋中文週期、零金額拒絕與浮動每週表單送出。完整結果見 VALIDATION.md。

尚需在部署機器確認容器重建／volume 保存、實際每小時執行，以及手機頁面與 PWA 更新。此輪沒有完成新頁面的瀏覽器視覺驗收。通知、銀行／信用卡串接、Cron、假日順延、第 N 個星期與進階 RecurringOccurrences 不在第一階段範圍；目前以既有軟刪除保存期別。

## 本次修改與新增檔案

以下相對於 SimpleExpenseTracker；../README.md 為儲存庫首頁。

```text
../README.md
backend/SimpleExpenseTracker.Api/BackupService.cs
backend/SimpleExpenseTracker.Api/Program.cs
backend/SimpleExpenseTracker.Api/RecurringController.cs
backend/SimpleExpenseTracker.Api/RecurringTransactionBackgroundService.cs
backend/SimpleExpenseTracker.Application/Contracts.cs
backend/SimpleExpenseTracker.Application/RecurringContracts.cs
backend/SimpleExpenseTracker.Application/RecurringSchedule.cs
backend/SimpleExpenseTracker.Domain/Entities.cs
backend/SimpleExpenseTracker.Domain/RecurringEnums.cs
backend/SimpleExpenseTracker.Domain/RecurringTransaction.cs
backend/SimpleExpenseTracker.Infrastructure/ExpenseDbContext.cs
backend/SimpleExpenseTracker.Infrastructure/ExpenseService.cs
backend/SimpleExpenseTracker.Infrastructure/LocalDateProvider.cs
backend/SimpleExpenseTracker.Infrastructure/Migrations/20261004151951_AddRecurringTransactions.cs
backend/SimpleExpenseTracker.Infrastructure/Migrations/20261004151951_AddRecurringTransactions.Designer.cs
backend/SimpleExpenseTracker.Infrastructure/Migrations/ExpenseDbContextModelSnapshot.cs
backend/SimpleExpenseTracker.Infrastructure/RecurringRules.cs
backend/SimpleExpenseTracker.Infrastructure/RecurringTransactionGenerator.cs
backend/SimpleExpenseTracker.Infrastructure/RecurringTransactionService.cs
backend/SimpleExpenseTracker.Tests/ApiTests.cs
backend/SimpleExpenseTracker.Tests/BackupTests.cs
backend/SimpleExpenseTracker.Tests/LocalDateProviderTests.cs
backend/SimpleExpenseTracker.Tests/RecurringApiTests.cs
backend/SimpleExpenseTracker.Tests/RecurringScheduleTests.cs
docs/RECURRING_SPECIFICATION.md
docs/RECURRING.md
docs/VALIDATION.md
frontend/simple-expense-tracker-web/src/api.ts
frontend/simple-expense-tracker-web/src/App.tsx
frontend/simple-expense-tracker-web/src/BackupSettings.tsx
frontend/simple-expense-tracker-web/src/Recurring.test.tsx
frontend/simple-expense-tracker-web/src/recurring.ts
frontend/simple-expense-tracker-web/src/RecurringForm.tsx
frontend/simple-expense-tracker-web/src/RecurringPage.tsx
frontend/simple-expense-tracker-web/src/style.css
frontend/simple-expense-tracker-web/src/TransactionForm.tsx
frontend/simple-expense-tracker-web/src/TransactionList.tsx
README.md
```
