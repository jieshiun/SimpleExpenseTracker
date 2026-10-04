# 完整資料庫備份與還原

設定頁的「資料備份與還原」提供完整 SQLite 匯出與整本還原。任何能連到 App 的人都可以操作，沒有管理密碼，也不需要選擇操作人。

## 匯出

按「匯出完整備份」，確認瀏覽器已保存 `.db` 檔案，再複製到其他裝置或儲存媒體。

檔案包含全部交易（含已刪除）、分類、帳戶、成員、歸屬、操作紀錄、交易版本、重送識別碼、索引與 Migration 紀錄。裝置常用操作人存在瀏覽器 localStorage，不包含在資料庫內。

匯出使用 `SqliteConnection.BackupDatabase` 建立一致性快照，包含尚未 checkpoint 的 WAL 資料，不直接複製使用中的主資料庫。匯出時仍可使用帳本；之後新增的資料不包含在該次快照中。檔名時間使用 UTC。

## 匯入與還原

1. 選擇本版本匯出的 `.db` 檔案，最大 100 MB。
2. 系統檢查資料庫結構、Migration 版本、SQLite 完整性、外鍵與應用程式資料規則。
3. 檢視交易筆數、已刪除筆數、分類、帳戶、成員與日期範圍。
4. 輸入「還原」，按「確認取代並還原」。預覽有效 30 分鐘，過期或服務重啟後需重新上傳。
5. 系統自動保存原帳本，再還原選定備份。成功後重新載入頁面。

**還原會取代整本帳，備份之後的資料不會保留；不提供資料合併。** 不相容、損壞或關聯不完整的備份會被拒絕，不修改目前帳本。第一版要求結構與目前版本完全一致，不自動升級舊備份，也不執行上傳的 SQL 腳本。

還原等待正在處理的 API 完成，新帳目操作暫時收到 503。成功或失敗回復後更新帳本識別碼，舊頁面的寫入收到 409，需重新整理整個頁面，包含其他手機仍開著的表單。常用操作人若不存在或停用，需重新選擇。

若連線中斷，先重新整理檢查帳本與自動備份清單，再決定是否重試；瀏覽器中斷不會取消已開始的還原。服務在還原中途終止時，下次啟動會在 Migration 與開放服務之前回復原帳本。若回復失敗，服務不會開放資料操作。

## 自動備份與容器儲存

還原前自動備份正常情況保留最近 5 份，設定頁提供下載。要回到還原前的帳本，下載該份備份再重新匯入。

```text
/data/expense-tracker.db
/data/expense-tracker.db.backups/
  generation.txt                  帳本識別碼，正常重啟不變
  before-restore-*.db              還原前備份
  restore-pending.json            未完成還原的恢復標記
  staging/                        上傳預覽與匯出暫存
```

全部存於現有 `expense-data` volume，不需新增掛載。自訂資料庫路徑時，備份目錄為「資料庫完整路徑 + `.backups`」，容器執行使用者需有寫入權限。API 不接受使用者指定伺服器檔案路徑。

上傳時清理過期預覽，啟動時清理全部預覽；匯出暫存於下載串流關閉後移除。自動備份與正式資料在同一 volume，無法抵禦整個 volume 遺失，請另存下載檔。不要在服務執行期間手動刪除恢復標記或備份目錄。

此功能以單一 App 容器為前提。不要讓多個 App 容器或外部程式同時寫入同一份 SQLite，維護控制只涵蓋此服務的 API。

## 更新容器

從 `SimpleExpenseTracker/` 執行，沿用原 Compose 專案名稱與 volume：

```sh
docker compose up -d --build
```

更新後關閉舊 PWA 視窗／分頁，再重新開啟。新版寫入 API 要求帳本識別碼，舊前端會收到 409，需要重新整理。

## API

| 路徑 | 用途 |
| --- | --- |
| `GET /api/backups/export` | 下載完整 SQLite 快照 |
| `POST /api/backups/preview` | multipart 上傳，欄位 `file`，回傳預覽與 token |
| `POST /api/backups/restore` | JSON `{ "token": "預覽識別碼", "confirmation": "還原" }` |
| `GET /api/backups` | 還原前自動備份清單 |
| `GET /api/backups/saved/{name}` | 下載指定自動備份 |

資料 API 回應設定 `Cache-Control: no-store`，並包含 `X-Ledger-Generation`。所有非 GET／HEAD 資料 API 請帶相同 header，還原成功後會改變。這是防止舊帳本頁面誤寫的版本檢查，不是權限或登入驗證。

技術依據：[SQLite Backup API](https://www.sqlite.org/backup.html)、[Microsoft.Data.Sqlite BackupDatabase](https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlite.sqliteconnection.backupdatabase?view=msdata-sqlite-8.0.0)。
