# 簡易記帳 App 開發規格書

## 1. 專案目標

開發一套簡單、快速、適合個人使用的記帳 App。

核心使用流程為：

**開啟 App → 快速新增一筆交易 → 查看帳目 → 查看每月統計**

第一版以 MVP 為原則，不追求完整財務管理功能。

主要目標：

- 手機操作優先
- 一筆交易可在 5～10 秒內完成
- 可記錄收入與支出
- 可自行管理分類
- 可管理不同付款帳戶
- 可修改與刪除交易
- 可查看每月收入、支出與結餘
- 可查看支出分類統計
- 可查看月份支出趨勢
- 資料永久保存
- UI 簡潔、現代化

---

# 2. 技術架構

第一版採用：

## Backend

- .NET 8
- ASP.NET Core Web API
- Entity Framework Core 8
- SQLite

## Frontend

- React
- TypeScript
- Vite
- Tailwind CSS
- Recharts

## App 型態

Responsive Web App + PWA。

主要使用情境為手機，但桌面瀏覽器也必須正常使用。

PWA 至少支援：

- 安裝至手機主畫面
- App Icon
- Manifest
- 基本 Service Worker

第一版不需要實作完整離線資料同步。

---

# 3. Solution 結構

建立以下專案結構：

```text
SimpleExpenseTracker/
│
├── backend/
│   ├── SimpleExpenseTracker.Api/
│   ├── SimpleExpenseTracker.Application/
│   ├── SimpleExpenseTracker.Domain/
│   └── SimpleExpenseTracker.Infrastructure/
│
├── frontend/
│   └── simple-expense-tracker-web/
│
├── README.md
└── .gitignore
```

Backend 採簡化版 Clean Architecture。

不要為了符合架構而過度抽象。

---

# 4. 核心資料模型

第一版主要包含：

- Transaction
- Category
- Account

三個 Entity。

---

# 5. Transaction

代表一筆收入或支出。

```text
Transaction

Id
Type
Amount
CategoryId
AccountId
TransactionDate
Note
CreatedAt
UpdatedAt
```

欄位：

```text
Id                  INTEGER / Guid
Type                Expense / Income
Amount              decimal
CategoryId          FK
AccountId           FK
TransactionDate     DateTime
Note                string nullable
CreatedAt           DateTime
UpdatedAt           DateTime
```

規則：

- Amount 必須 > 0
- 不使用負數代表支出
- 收入／支出由 Type 判斷
- Category 必須存在
- Account 必須存在
- Category.Type 必須與 Transaction.Type 相同
- TransactionDate 預設今天
- Note 可空白

---

# 6. Category

```text
Category

Id
Name
Type
Icon
SortOrder
IsActive
CreatedAt
UpdatedAt
```

Type：

```text
Expense
Income
```

預設支出分類：

```text
餐飲
交通
購物
娛樂
居家
水電
醫療
教育
保險
訂閱
其他
```

預設收入分類：

```text
薪資
獎金
投資
兼職
其他收入
```

Icon 可以先使用 Emoji。

例如：

```text
🍜 餐飲
🚗 交通
🛒 購物
🎮 娛樂
🏠 居家
💡 水電
🏥 醫療
📚 教育
🛡️ 保險
📱 訂閱
💰 薪資
```

分類支援：

- 新增
- 修改
- 停用
- 排序

若 Category 已被 Transaction 使用，不允許真正刪除。

改為：

```text
IsActive = false
```

---

# 7. Account

代表資金來源或付款方式。

```text
Account

Id
Name
Type
InitialBalance
IsActive
CreatedAt
UpdatedAt
```

Account Type：

```text
Cash
Bank
CreditCard
EWallet
Other
```

預設帳戶：

```text
現金
銀行帳戶
信用卡
```

使用者可以自行新增：

```text
台新銀行
玉山銀行
國泰信用卡
LINE Pay
街口支付
```

若帳戶已有 Transaction，不允許真正刪除。

改為停用。

---

# 8. 第一版不實作 Account Balance

InitialBalance 先保留。

第一版首頁不需要計算：

```text
銀行還有多少錢
信用卡欠多少錢
總資產多少錢
```

Account 第一版主要用途是記錄：

> 「這筆錢從哪個帳戶支付？」

避免 Account Balance、信用卡帳單週期等邏輯讓 MVP 過度複雜。

---

# 9. REST API

Base URL：

```text
/api
```

## Transaction

```text
GET    /api/transactions
GET    /api/transactions/{id}
POST   /api/transactions
PUT    /api/transactions/{id}
DELETE /api/transactions/{id}
```

GET transactions 支援：

```text
?year=2026
&month=10
&type=Expense
&categoryId=
&accountId=
&page=1
&pageSize=50
```

預設：

```text
TransactionDate DESC
CreatedAt DESC
```

---

# 10. Category API

```text
GET    /api/categories
GET    /api/categories/{id}
POST   /api/categories
PUT    /api/categories/{id}
DELETE /api/categories/{id}
```

DELETE 若已有 Transaction 使用：

不要真正 DELETE。

改為：

```text
IsActive = false
```

---

# 11. Account API

```text
GET    /api/accounts
GET    /api/accounts/{id}
POST   /api/accounts
PUT    /api/accounts/{id}
DELETE /api/accounts/{id}
```

同樣採 Soft Delete / Disable。

---

# 12. Dashboard API

建立：

```text
GET /api/dashboard/summary?year=2026&month=10
```

回傳：

```json
{
  "income": 65000,
  "expense": 28520,
  "balance": 36480
}
```

其中：

```text
balance = income - expense
```

---

# 13. 分類統計 API

```text
GET /api/statistics/categories?year=2026&month=10&type=Expense
```

例如：

```json
[
  {
    "categoryId": 1,
    "categoryName": "餐飲",
    "amount": 8200,
    "percentage": 28.75
  },
  {
    "categoryId": 2,
    "categoryName": "交通",
    "amount": 4300,
    "percentage": 15.08
  }
]
```

percentage 可由 Backend 計算。

---

# 14. 月份趨勢 API

```text
GET /api/statistics/monthly?months=6
```

例如：

```json
[
  {
    "year": 2026,
    "month": 5,
    "income": 65000,
    "expense": 31200
  },
  {
    "year": 2026,
    "month": 6,
    "income": 65000,
    "expense": 28500
  }
]
```

---

# 15. 前端主要頁面

底部 Navigation：

```text
首頁
帳目
＋
統計
設定
```

手機版固定在畫面底部。

中央「＋」按鈕應特別明顯，用於快速新增交易。

---

# 16. 首頁 Dashboard

首頁頂端顯示目前月份：

```text
‹    2026 年 10 月    ›
```

可切換：

```text
上一個月
下一個月
```

顯示三個主要數字：

```text
本月收入
$65,000

本月支出
$28,520

本月結餘
$36,480
```

接著顯示：

```text
支出分類
```

以 Donut Chart 顯示。

再顯示：

```text
最近交易
```

只顯示最近 5 筆。

例如：

```text
10/03

🍜 午餐
餐飲 · 信用卡
-$350

🚗 停車
交通 · LINE Pay
-$60
```

---

# 17. 新增交易頁面

使用者按下：

```text
＋
```

開啟新增交易畫面。

頂端：

```text
[ 支出 ] [ 收入 ]
```

預設：

```text
支出
```

欄位順序：

```text
金額

分類

帳戶

日期

備註
```

金額輸入框應該是整個畫面的主要視覺焦點。

例如：

```text
$ 350
```

手機開啟 Numeric Keyboard。

---

# 18. 分類選擇

不要使用普通 HTML Select。

使用適合手機操作的 Grid。

例如：

```text
🍜
餐飲

🚗
交通

🛒
購物

🎮
娛樂

🏠
居家
```

點擊即可選擇。

只顯示與目前 Type 相同的 Category。

例如：

```text
Type = Expense
```

不能顯示：

```text
薪資
獎金
```

---

# 19. 儲存交易

按下：

```text
儲存
```

成功後：

1. 顯示簡短成功提示。
2. 回到上一頁或首頁。
3. Dashboard 自動重新取得資料。

不要要求使用者額外確認。

---

# 20. 帳目頁面

預設顯示目前月份。

例如：

```text
2026 年 10 月
```

按照日期 Group。

例如：

```text
10 月 3 日

🍜 午餐
餐飲 · 信用卡
                     -$350

🚗 停車
交通 · LINE Pay
                      -$60


10 月 2 日

🛒 全聯
購物 · 信用卡
                   -$1,280
```

收入：

```text
+$65,000
```

支出：

```text
-$350
```

---

# 21. 編輯交易

點擊 Transaction：

進入詳細／編輯頁面。

可以修改：

```text
Type
Amount
Category
Account
TransactionDate
Note
```

底部提供：

```text
儲存修改

刪除這筆交易
```

刪除前必須顯示確認 Dialog：

```text
確定要刪除這筆交易嗎？

取消
刪除
```

---

# 22. 統計頁面

提供月份切換：

```text
‹ 2026 年 10 月 ›
```

第一區：

```text
收入
支出
結餘
```

第二區：

```text
支出分類
```

使用 Donut Chart。

下面列出：

```text
餐飲
$8,200
28.8%

購物
$6,500
22.8%

交通
$4,300
15.1%
```

第三區：

```text
最近 6 個月支出
```

使用 Bar Chart 或 Line Chart。

---

# 23. 設定頁面

第一版設定：

```text
分類管理
帳戶管理
```

暫時不要加入大量沒有實際功能的設定項目。

---

# 24. 分類管理頁面

分成：

```text
支出分類
收入分類
```

支援：

```text
新增
修改
停用
```

顯示：

```text
🍜 餐飲
🚗 交通
🛒 購物
```

新增分類：

```text
名稱
Icon
Type
```

---

# 25. 帳戶管理

顯示：

```text
現金
台新銀行
玉山銀行
國泰信用卡
LINE Pay
```

可以：

```text
新增
修改
停用
```

---

# 26. UI / UX 原則

整體風格：

- Minimal
- Modern
- Mobile First
- Card-based UI
- 足夠留白
- 不要過度使用陰影
- 不要塞滿資訊
- 不要做成企業 ERP 風格
- 不要做成傳統 Bootstrap 後台管理介面

手機寬度：

```text
375px
390px
412px
```

都必須正常顯示。

桌面版最大內容寬度控制在合理範圍，不要將手機 Layout 直接拉滿整個螢幕。

---

# 27. 金額格式

所有金額預設使用：

```text
NT$
```

UI 可以簡化顯示：

```text
$1,280
$65,000
```

Backend 一律使用 decimal。

禁止使用：

```text
float
double
```

儲存金額。

---

# 28. 日期

UI 使用：

```text
yyyy/MM/dd
```

例如：

```text
2026/10/03
```

月份：

```text
2026 年 10 月
```

資料庫與 API 日期處理需保持一致。

---

# 29. SQLite

資料庫：

```text
expense-tracker.db
```

第一次啟動時自動建立 Database。

使用 EF Core Migration。

必須提供：

```text
InitialCreate
```

Migration。

---

# 30. Seed Data

第一次建立 Database 時建立預設 Category。

Expense：

```text
餐飲
交通
購物
娛樂
居家
水電
醫療
教育
保險
訂閱
其他
```

Income：

```text
薪資
獎金
投資
兼職
其他收入
```

Account：

```text
現金
銀行帳戶
信用卡
```

Seed 必須具備 Idempotent 特性。

重啟程式不得重複建立。

---

# 31. Validation

Backend 必須驗證：

```text
Amount > 0

Category exists

Account exists

Category.Type == Transaction.Type

TransactionDate != null
```

Frontend 也需要基本 Validation。

但 Backend Validation 才是最終依據。

---

# 32. Error Handling

API 統一使用合理 HTTP Status Code：

```text
200 OK
201 Created
204 No Content
400 Bad Request
404 Not Found
500 Internal Server Error
```

Validation Error 回傳結構保持一致。

Frontend 必須顯示使用者可以理解的錯誤訊息。

不要直接顯示：

```text
System.Exception
SQL Exception
Stack Trace
```

---

# 33. Logging

ASP.NET Core 使用內建 ILogger。

至少記錄：

```text
Application startup
Database migration
Unhandled exception
API error
```

不要記錄敏感資料。

---

# 34. Testing

Backend 建立 Unit / Integration Test。

至少測試：

```text
新增支出
新增收入
Amount = 0 被拒絕
Category Type 不符合 Transaction Type 被拒絕
修改 Transaction
刪除 Transaction
Dashboard 月份統計
Category 統計
Monthly Statistics
```

Frontend 至少對核心 utility 與重要互動建立基本測試。

---

# 35. README

建立完整 README.md。

至少包含：

```text
專案介紹

Screenshots（先保留位置）

Technology Stack

Prerequisites

Backend 啟動方法

Frontend 啟動方法

Database Migration

Project Structure

API Overview

Development Notes
```

必須讓另一位 Developer Clone Repository 後，可以按照 README 成功執行專案。

---

# 36. 開發階段

不要一次完成所有功能。

按照以下順序開發。

## Phase 1

建立 Solution 與基礎架構。

完成：

```text
.NET Solution
React Project
SQLite
EF Core
Entity
Migration
Seed Data
```

確認 Backend 與 Frontend 都可以啟動。

---

## Phase 2

完成：

```text
Category CRUD
Account CRUD
Transaction CRUD
Validation
```

並建立 API Test。

---

## Phase 3

完成主要 UI：

```text
Bottom Navigation
Dashboard
Transaction List
Add Transaction
Edit Transaction
```

先確保完整記帳流程可用。

---

## Phase 4

完成：

```text
Dashboard Summary
Category Statistics
Monthly Statistics
Charts
```

---

## Phase 5

完成：

```text
Category Management
Account Management
PWA
Responsive Design
Error Handling
Loading State
Empty State
```

---

## Phase 6

進行：

```text
Refactor
Testing
README
Bug Fix
```

---

# 37. MVP 驗收條件

以下情境全部成功才算第一版完成。

### Scenario 1

使用者可以新增：

```text
支出
350
餐飲
信用卡
2026/10/03
午餐
```

儲存後帳目頁面立即看到：

```text
🍜 午餐
餐飲 · 信用卡
-$350
```

---

### Scenario 2

新增：

```text
收入
65000
薪資
銀行帳戶
2026/10/01
10 月薪資
```

Dashboard：

```text
收入 $65,000
```

---

### Scenario 3

同月份存在：

```text
收入 65,000
支出 28,520
```

Dashboard 必須顯示：

```text
收入    $65,000
支出    $28,520
結餘    $36,480
```

---

### Scenario 4

可以修改既有 Transaction。

修改後 Dashboard 與 Statistics 必須立即反映新數字。

---

### Scenario 5

刪除 Transaction 後：

```text
Transaction List
Dashboard
Statistics
```

全部正確更新。

---

### Scenario 6

Category Statistics 所有百分比總和應約為：

```text
100%
```

允許因四捨五入產生微小差異。

---

### Scenario 7

手機：

```text
375 × 667
390 × 844
412 × 915
```

皆可正常操作，不應發生主要 UI 水平 Overflow。

---

# 38. 第一版明確不實作

不要自行加入以下功能：

```text
使用者登入
Google Login
Apple Login
多人帳本
家庭共享
銀行 API
電子發票 API
OCR
AI 自動分類
股票
基金
加密貨幣
多幣別
信用卡帳單
信用卡結帳日
資產負債表
雲端同步
通知
預算
週期性交易
轉帳
CSV Import
Excel Import
```

這些全部留到後續版本。

---

# 39. Coding Guidelines

請遵守：

- 使用 async / await
- Entity 不直接作為 API Request / Response Model
- 使用 DTO
- 啟用 Nullable Reference Types
- TypeScript 開啟 strict
- 避免 any
- 避免不必要的 Repository Pattern
- 避免過度工程化
- 保持 Controller / Service 職責清楚
- Business Logic 不應全部寫在 Controller
- 所有 API 都應支援 CancellationToken
- 金額使用 decimal
- 時間處理方式保持一致
- 不要在程式中 Hard Code Category ID

---

# 40. Codex 執行方式

請先閱讀完整規格。

不要一次產生整個專案後宣稱完成。

按照 Phase 逐步實作。

每完成一個 Phase：

1. Build Backend。
2. 執行 Backend Tests。
3. Build Frontend。
4. 執行 Frontend Tests。
5. 修正所有 Error。
6. 確認功能可以實際執行。
7. Commit 該 Phase。

Commit Message 例如：

```text
feat: initialize expense tracker architecture

feat: implement transaction management

feat: add mobile transaction interface

feat: add dashboard statistics

feat: add category and account management

test: complete MVP integration tests
```

遇到規格中沒有明確定義的小型技術細節，可以採用合理且簡單的實作方式。

如果某個決策會：

- 改變資料模型
- 增加新的第三方服務
- 增加新的主要 Dependency
- 大幅增加專案複雜度
- 改變既定架構

則不要自行擴充，先說明問題與建議方案。

最終目標不是展示複雜架構，而是完成一個：

**簡單、快速、穩定，而且真的可以每天使用的個人記帳 App。**