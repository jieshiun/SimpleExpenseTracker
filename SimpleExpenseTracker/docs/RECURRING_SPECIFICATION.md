# Simple Expense Tracker 4.x－週期性固定收支功能開發規格

## 1. 功能目標

目前 Simple Expense Tracker 已支援一般收入／支出記帳與 Family 家庭成員功能。

本次新增「週期性固定收支（Recurring Transactions）」功能，讓使用者可以預先設定固定或週期性發生的收入與支出，例如：

- 手機費
- 網路費
- Netflix、Spotify、ChatGPT 等訂閱
- 房租／房貸
- 保險
- 幼兒園／學費
- 薪資
- 網域續約費
- 每年固定繳納的費用

系統依照設定的週期，自動建立實際 Transaction。

核心設計原則：

> RecurringTransaction 是「交易範本與排程規則」，Transaction 才是真正納入帳務統計的交易。

不得直接將 RecurringTransaction 納入既有收支統計，避免重複計算。

---

# 2. 使用情境

例如使用者建立：

```text
名稱：中華電信手機費
類型：支出
分類：電信費
金額：599
金額類型：固定
週期：每月
執行日：每月 10 日
開始日期：2026-10-10
結束日期：無
歸屬：Ivan
狀態：啟用
```

系統應依序建立：

```text
2026-10-10  中華電信手機費  -599
2026-11-10  中華電信手機費  -599
2026-12-10  中華電信手機費  -599
```

每一筆都是真正的 Transaction，並保留來源 RecurringTransactionId。

---

# 3. 功能範圍

第一階段支援：

- 固定收入
- 固定支出
- 每週
- 每月
- 每年
- 每 N 個週期執行一次
- 固定金額
- 浮動／預估金額
- 開始日期
- 結束日期
- 無限期
- Family
- 指定家庭成員
- 家庭共同支出
- 啟用／停用
- 自動產生 Transaction
- 下一次執行日期
- 防止重複產生 Transaction
- 本月固定支出統計
- 年度固定支出統計
- 即將發生項目

第一階段暫不實作：

- Email / Push Notification
- 銀行 API
- 信用卡 API
- 自動辨識訂閱
- AI 分析
- 信用卡帳單同步
- 複雜 Cron Expression
- 每月第 N 個星期幾
- 國定假日順延

---

# 4. Database Schema

新增：

```text
RecurringTransactions
```

建議 Entity：

```csharp
public class RecurringTransaction
{
    public int Id { get; set; }

    public int FamilyId { get; set; }

    // null 代表家庭共同
    public int? MemberId { get; set; }

    public string Name { get; set; } = string.Empty;

    public TransactionType Type { get; set; }

    public int CategoryId { get; set; }

    public decimal Amount { get; set; }

    public AmountType AmountType { get; set; }

    public RecurringFrequency Frequency { get; set; }

    // 每 N 個週期
    public int Interval { get; set; } = 1;

    // Monthly 使用
    public int? DayOfMonth { get; set; }

    // Weekly 使用
    public DayOfWeek? DayOfWeek { get; set; }

    // Yearly 使用
    public int? MonthOfYear { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public DateOnly NextRunDate { get; set; }

    public DateOnly? LastGeneratedDate { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
```

---

# 5. Enum

```csharp
public enum RecurringFrequency
{
    Weekly,
    Monthly,
    Yearly
}

public enum AmountType
{
    Fixed,
    Variable
}
```

既有 TransactionType：

```csharp
public enum TransactionType
{
    Expense,
    Income
}
```

若目前專案已經存在 TransactionType，直接沿用，不要重複建立。

---

# 6. Transaction 修改

既有 Transaction 增加：

```csharp
public int? RecurringTransactionId { get; set; }

public DateOnly? RecurringOccurrenceDate { get; set; }
```

用途：

```text
RecurringTransaction
        │
        ├── Transaction 2026/10/10
        ├── Transaction 2026/11/10
        └── Transaction 2026/12/10
```

一般手動新增 Transaction：

```text
RecurringTransactionId = null
RecurringOccurrenceDate = null
```

週期性產生：

```text
RecurringTransactionId = 15
RecurringOccurrenceDate = 2026-10-10
```

---

# 7. 防止重複建立

這是必要功能。

Database 建立 Unique Index：

```text
RecurringTransactionId
+
RecurringOccurrenceDate
```

只針對 RecurringTransactionId 非 NULL 的資料生效。

目的：

即使 Backend：

- Docker 重啟
- BackgroundService 重複執行
- API 被重複呼叫
- Application crash 後重新執行

同一個週期仍只能建立一筆 Transaction。

例如：

```text
RecurringTransactionId = 15
RecurringOccurrenceDate = 2026-10-10
```

只能存在一次。

除了程式邏輯檢查之外，Database 層級也必須建立 Unique Constraint / Unique Index 作最後一道保護。

---

# 8. API

Base URL：

```text
/api/recurring-transactions
```

## 8.1 查詢全部

```http
GET /api/recurring-transactions
```

支援：

```text
familyId
memberId
type
isActive
```

例如：

```http
GET /api/recurring-transactions?familyId=1&isActive=true
```

---

## 8.2 查詢單筆

```http
GET /api/recurring-transactions/{id}
```

---

## 8.3 新增

```http
POST /api/recurring-transactions
```

Request：

```json
{
  "familyId": 1,
  "memberId": 2,
  "name": "中華電信手機費",
  "type": "Expense",
  "categoryId": 5,
  "amount": 599,
  "amountType": "Fixed",
  "frequency": "Monthly",
  "interval": 1,
  "dayOfMonth": 10,
  "startDate": "2026-10-10",
  "endDate": null,
  "note": ""
}
```

Backend 自動計算：

```text
NextRunDate
CreatedAt
UpdatedAt
```

---

## 8.4 修改

```http
PUT /api/recurring-transactions/{id}
```

修改規則只影響「未來交易」。

已經產生的 Transaction 不得自動跟著修改。

例如：

```text
Netflix
原本：390

2026/10 已產生：390

11 月修改週期設定：
390 → 460
```

結果：

```text
10 月 Transaction = 390
11 月 Transaction = 460
12 月 Transaction = 460
```

歷史帳務不得被改寫。

---

## 8.5 刪除

```http
DELETE /api/recurring-transactions/{id}
```

刪除 RecurringTransaction 時：

不得刪除已經產生的 Transaction。

建議實際行為優先採：

```text
IsActive = false
```

若 UI 提供真正刪除功能，也必須保留既有 Transaction。

---

## 8.6 啟用／停用

```http
PATCH /api/recurring-transactions/{id}/status
```

Request：

```json
{
  "isActive": false
}
```

停用後：

```text
不再產生新的 Transaction
```

歷史 Transaction 保留。

---

# 9. Summary API

新增：

```http
GET /api/recurring-transactions/summary
```

Response：

```json
{
  "monthlyExpense": 4287,
  "monthlyIncome": 0,
  "annualExpense": 63440,
  "annualIncome": 0,
  "activeCount": 8
}
```

年度金額應依照週期換算。

例如：

```text
599 / 月
→ 599 × 12

18000 / 年
→ 18000

500 / 週
→ 依實際週期計算年度預估
```

這些數字屬於「預估固定收支」，不得與實際 Transaction 報表混為一談。

---

# 10. Upcoming API

新增：

```http
GET /api/recurring-transactions/upcoming
```

Query：

```text
days=30
```

例如：

```http
GET /api/recurring-transactions/upcoming?days=30
```

Response：

```json
[
  {
    "id": 3,
    "name": "網路費",
    "nextRunDate": "2026-10-05",
    "amount": 999
  },
  {
    "id": 5,
    "name": "手機費",
    "nextRunDate": "2026-10-10",
    "amount": 599
  }
]
```

依 NextRunDate ASC 排序。

---

# 11. Backend 排程

建立：

```text
RecurringTransactionBackgroundService
```

使用：

```csharp
BackgroundService
```

Application 啟動後執行，之後固定週期檢查。

不需要精準在 00:00 執行。

建議每小時檢查一次即可。

核心條件：

```text
IsActive = true
AND
NextRunDate <= Today
AND
(EndDate IS NULL OR NextRunDate <= EndDate)
```

---

# 12. 產生 Transaction 流程

流程：

```text
BackgroundService
       │
       ▼
取得到期 RecurringTransactions
       │
       ▼
檢查是否已存在 Transaction
       │
       ▼
不存在
       │
       ▼
建立 Transaction
       │
       ▼
寫入 RecurringTransactionId
       │
       ▼
寫入 RecurringOccurrenceDate
       │
       ▼
更新 LastGeneratedDate
       │
       ▼
計算 NextRunDate
```

---

# 13. Catch-up 機制

必須考慮 Docker / Server 可能停止數天。

例如：

```text
每月 1 日產生房租

Server：
10/1～10/3 停機

10/4 啟動
```

系統仍必須補產生：

```text
10/1 房租
```

因此判斷條件不可使用：

```csharp
NextRunDate == Today
```

必須使用：

```csharp
NextRunDate <= Today
```

而且如果 Server 停止數個週期，例如：

```text
NextRunDate = 2026/07/01
Today       = 2026/10/04
```

系統應依序處理：

```text
07/01
08/01
09/01
10/01
```

每個 occurrence 都必須檢查是否已存在。

---

# 14. NextRunDate 計算

Weekly：

```text
NextRunDate + (7 × Interval) days
```

Monthly：

```text
NextRunDate + Interval months
```

Yearly：

```text
NextRunDate + Interval years
```

---

# 15. 月底特殊處理

必須處理：

```text
DayOfMonth = 31
```

遇到：

```text
2 月
4 月
6 月
9 月
11 月
```

沒有 31 日。

規則：

> 如果該月份不存在指定日期，使用該月份最後一天。

例如：

```text
每月 31 日
```

產生：

```text
2026/01/31
2026/02/28
2026/03/31
2026/04/30
```

但下一個月份仍應回到 31 日。

因此不能單純：

```csharp
date.AddMonths(1)
```

後就永遠沿用 28 日。

必須根據原始：

```text
DayOfMonth
```

重新計算。

---

# 16. Fixed / Variable

## Fixed

例如：

```text
Netflix
390 / 月
```

自動建立：

```text
Transaction.Amount = 390
```

---

## Variable

例如：

```text
手機費
預估 599 / 月
```

自動建立：

```text
Transaction.Amount = 599
```

但允許使用者之後修改：

```text
2026/10 實際手機費 = 632
```

修改 Transaction 不得反向修改 RecurringTransaction.Amount。

RecurringTransaction.Amount 永遠代表：

```text
預設／預估金額
```

---

# 17. Family

沿用目前 Family 架構。

RecurringTransaction 必須屬於：

```text
Family
```

MemberId：

```text
null
→ 家庭共同

有值
→ 指定家庭成員
```

例如：

```text
房貸        家庭共同
Netflix     家庭共同
手機費      Ivan
手機費      Family Member B
```

API 必須驗證 Member 確實屬於指定 Family。

---

# 18. React UI

新增主要頁面：

```text
RecurringTransactionsPage
```

中文顯示：

```text
固定收支
```

---

# 19. 固定收支首頁

畫面：

```text
固定收支

┌─────────────────┐
│ 本月固定支出     │
│ $4,287           │
└─────────────────┘

┌─────────────────┐
│ 年度固定支出     │
│ $63,440          │
└─────────────────┘

即將發生

10/05  網路費       $999
10/10  手機費       $599
10/15  Netflix      $390
10/20  ChatGPT      $660

固定項目

手機費
$599 / 月
Ivan
下一次：10/10

Netflix
$390 / 月
家庭共同
下一次：10/15

汽車保險
$18,000 / 年
家庭共同
下一次：2027/03/15

[ ＋ 新增固定收支 ]
```

---

# 20. 新增／編輯畫面

欄位：

```text
名稱

類型
○ 支出
○ 收入

分類

金額

金額類型
● 固定
○ 浮動／預估

週期
○ 每週
● 每月
○ 每年

每 [ 1 ] 個月

執行日期
每月 [ 10 ] 日

開始日期

結束
● 無限期
○ 指定日期

歸屬
○ 家庭共同
○ Ivan
○ Family Member B

備註

狀態
● 啟用
○ 停用

[ 儲存 ]
```

---

# 21. 顯示文字

Frequency 不直接顯示 Enum。

例如：

```text
Weekly  + Interval 1
→ 每週

Weekly + Interval 2
→ 每 2 週

Monthly + Interval 1
→ 每月

Monthly + Interval 3
→ 每 3 個月

Yearly + Interval 1
→ 每年

Yearly + Interval 2
→ 每 2 年
```

---

# 22. Transaction UI

由 RecurringTransaction 產生的 Transaction，在明細中增加識別。

例如：

```text
10/10

中華電信手機費
電信費

$599

↻ 固定收支
```

可以使用小 icon / badge：

```text
↻ 固定
```

讓使用者知道這筆是系統自動建立。

但仍然允許：

```text
修改金額
修改備註
修改分類
```

修改 Transaction 不得修改 RecurringTransaction。

---

# 23. 刪除 Transaction

如果使用者手動刪除由週期產生的 Transaction：

```text
RecurringTransaction 本身不得被刪除
```

而且 BackgroundService 不應再次把同一 occurrence 建回來。

因此建議不要只依靠 Transaction 是否存在判斷。

可以新增：

```text
RecurringOccurrences
```

若希望第一版保持簡單，也可以 Transaction 採 Soft Delete，讓 Unique Index 與 occurrence 記錄仍然存在。

若目前 Transaction 已有 Soft Delete 機制，優先沿用。

---

# 24. 建議的進階資料模型

若目前架構允許，推薦建立：

```text
RecurringOccurrences
```

Schema：

```text
Id
RecurringTransactionId
OccurrenceDate
TransactionId nullable
Status
CreatedAt
```

Status：

```text
Generated
Skipped
Deleted
```

如此未來就能支援：

```text
略過本期
只修改這一次
刪除這一次
```

而不會破壞 RecurringTransaction。

如果第一階段希望降低複雜度，可以暫時不做，但程式架構不要把未來擴充堵死。

---

# 25. Validation

Backend 必須驗證：

```text
Name required

Amount >= 0

Interval >= 1

StartDate required

EndDate >= StartDate

Category exists

Family exists

Member belongs to Family
```

Monthly：

```text
DayOfMonth = 1～31
```

Weekly：

```text
DayOfWeek required
```

Yearly：

```text
MonthOfYear = 1～12
DayOfMonth = 1～31
```

---

# 26. Time Zone

排程不得直接假設 UTC 日期就是使用者日期。

目前系統以台灣使用為主，日期型交易應以 Application 設定的 Local Time Zone 判斷 Today。

建議集中封裝：

```csharp
IDateTimeProvider
```

例如：

```csharp
DateOnly Today { get; }
DateTime UtcNow { get; }
```

不要讓各 Service 到處直接使用：

```csharp
DateTime.Now
DateTime.UtcNow
```

方便未來測試與 Time Zone 調整。

---

# 27. Service Layer

建議建立：

```text
IRecurringTransactionService

RecurringTransactionService

IRecurringTransactionGenerator

RecurringTransactionGenerator

RecurringTransactionBackgroundService
```

責任分離：

```text
RecurringTransactionService
→ CRUD

RecurringTransactionGenerator
→ 判斷到期
→ 建立 Transaction
→ 計算下一次日期

BackgroundService
→ 定期觸發 Generator
```

不要把所有邏輯直接寫在 Controller 或 BackgroundService。

---

# 28. Logging

產生 Transaction 時記錄：

```text
RecurringTransactionId
OccurrenceDate
TransactionId
Amount
```

例如：

```text
Generated recurring transaction.
RecurringTransactionId=15
OccurrenceDate=2026-10-10
TransactionId=382
Amount=599
```

若因 Unique Constraint 發現已存在：

```text
Skip duplicated recurring transaction.
```

若失敗：

```text
Failed to generate recurring transaction.
```

並記錄 Exception。

---

# 29. Transaction / Concurrency

建立 Transaction 與更新：

```text
LastGeneratedDate
NextRunDate
```

應放在同一 Database Transaction。

必須考慮 BackgroundService 可能重複執行或多 Instance 同時處理。

Database Unique Index 必須作為最後一道防線。

不得只依靠：

```csharp
if (!exists)
{
    Insert();
}
```

因為存在 Race Condition。

---

# 30. Migration

建立 EF Core Migration，例如：

```text
AddRecurringTransactions
```

內容至少包含：

```text
Create RecurringTransactions

Add Transaction.RecurringTransactionId

Add Transaction.RecurringOccurrenceDate

Create FK

Create Index

Create Unique Index
```

既有 Transaction 資料不得受到影響。

---

# 31. Unit Tests

至少測試：

```text
每週週期計算

每月週期計算

每年週期計算

Interval = 2

每月 31 日

2 月最後一天

閏年 2/29

EndDate

Inactive 不產生

Catch-up

避免 Duplicate

Variable Amount

修改 RecurringTransaction 不修改歷史 Transaction
```

特別測試：

```text
2026/01/31
→ 2026/02/28
→ 2026/03/31
```

以及：

```text
2028/02/29
```

---

# 32. API Integration Tests

至少驗證：

```text
POST recurring transaction

GET list

GET detail

PUT

DELETE / Disable

Summary

Upcoming

Background generation
```

並確認：

```text
Family A 不得讀取／修改 Family B 的 RecurringTransaction。
```

---

# 33. 驗收條件

## AC01

Given：

```text
手機費
599
每月 10 日
```

When：

```text
日期到達 10 日
```

Then：

自動產生一筆 $599 Transaction。

---

## AC02

同一：

```text
RecurringTransactionId
OccurrenceDate
```

不得產生兩筆 Transaction。

---

## AC03

Docker/Application 停止數日後重新啟動，可以補產生遺漏的週期交易。

---

## AC04

停用 RecurringTransaction 後，不得再產生新的 Transaction。

---

## AC05

修改 RecurringTransaction 金額不得修改歷史 Transaction。

---

## AC06

刪除／停用 RecurringTransaction 不得刪除歷史 Transaction。

---

## AC07

Variable Amount 產生 Transaction 後，可以修改實際金額，而且不得修改 RecurringTransaction 的預估金額。

---

## AC08

每月 31 日在沒有 31 日的月份，自動使用月底。

---

## AC09

Family 可以建立：

```text
家庭共同固定支出
```

以及：

```text
指定 Member 固定支出
```

---

## AC10

固定收支頁可以看到：

```text
本月固定支出
年度固定支出
即將發生
全部固定項目
```

---

## AC11

Transaction 明細可以辨識：

```text
一般交易
週期性自動產生交易
```

---

## AC12

Family A 不得存取 Family B 的 RecurringTransaction。

---

# 34. Codex 實作要求

請先分析目前 Repository 架構，不要直接假設 Entity、DTO、Repository、Service、Controller 或 React component 的命名。

優先沿用目前專案既有：

- Architecture
- Coding Style
- DTO Pattern
- API Response Pattern
- Authentication / Authorization
- Family Context
- EF Core DbContext
- Error Handling
- React Components
- Tailwind CSS Style
- State Management
- API Client
- Date / Currency Formatter

不要為此功能重新建立另一套架構。

---

# 35. Codex 建議施工順序

依照以下順序實作：

```text
1. 分析既有專案

2. 建立 RecurringTransaction Entity / Enum

3. 修改 Transaction Entity

4. EF Core Migration

5. RecurringTransaction CRUD Service

6. RecurringTransaction API

7. NextRunDate Calculator

8. RecurringTransaction Generator

9. Duplicate Protection

10. Catch-up Logic

11. BackgroundService

12. Summary / Upcoming API

13. React 固定收支首頁

14. 新增／編輯 UI

15. Transaction 固定收支 Badge

16. Unit Tests

17. Integration Tests

18. Build

19. 執行測試

20. 修正問題
```

每完成一個階段都應確保既有功能沒有 Regression。

---

# 36. 最終要求

完成後請輸出：

```text
1. 修改／新增的檔案清單
2. Database Schema 變更
3. Migration 名稱
4. 新增 API 清單
5. BackgroundService 執行方式
6. 防止重複交易的實作方式
7. React 新增／修改頁面
8. Unit Test 結果
9. Integration Test 結果
10. Build 結果
11. 尚未實作或需要人工確認的項目
```

如果發現目前 Repository 的實際架構與本規格不同：

> 優先配合現有架構調整實作方式，但不得任意刪減本規格的核心功能。

如需修改既有 Database Schema、API contract 或 Family 權限模型，請先說明影響範圍，再進行修改。