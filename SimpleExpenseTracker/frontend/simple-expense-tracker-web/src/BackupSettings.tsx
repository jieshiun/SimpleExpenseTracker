import { useEffect, useRef, useState } from "react";
import { api, request } from "./api";
import Modal from "./Modal";

interface Preview {
  token: string;
  transactions: number;
  deletedTransactions: number;
  categories: number;
  accounts: number;
  members: string[];
  firstDate: string | null;
  lastDate: string | null;
  expiresAt: string;
  recurringTransactions?: number;
  wasUpgraded?: boolean;
  sourceMigration?: string | null;
}
interface SafetyBackup { name: string; createdAt: string; size: number }
const limit = 100 * 1024 * 1024;

export default function BackupSettings() {
  const [busy, setBusy] = useState("");
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [preview, setPreview] = useState<Preview | null>(null);
  const [confirmation, setConfirmation] = useState("");
  const [saved, setSaved] = useState<SafetyBackup[]>([]);
  const working = useRef(false);
  useEffect(() => {
    const controller = new AbortController();
    api<SafetyBackup[]>("/backups", { signal: controller.signal }).then(setSaved).catch((e: Error) => { if (e.name !== "AbortError") setError(e.message); });
    return () => controller.abort();
  }, []);
  async function run(label: string, action: () => Promise<void>) {
    if (working.current) return;
    working.current = true; setBusy(label); setError(""); setMessage("");
    try { await action(); } catch (e) { setError((e as Error).message); }
    finally { working.current = false; setBusy(""); }
  }
  async function download(path: string, fallback: string) {
    const response = await request(path);
    const blob = await response.blob();
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = response.headers.get("Content-Disposition")?.match(/filename="?([^";]+)"?/)?.[1] ?? fallback;
    document.body.append(link); link.click(); link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 60000);
    setMessage("備份已送交瀏覽器下載，請確認檔案已儲存。");
  }
  async function upload(file: File) {
    setPreview(null); setConfirmation("");
    if (file.size < 100 || file.size > limit) { setError("請選擇有效的 SQLite 備份，檔案上限為 100 MB。"); return; }
    await run("正在檢查備份…", async () => {
      const body = new FormData(); body.append("file", file);
      setPreview(await api<Preview>("/backups/preview", { method: "POST", body }));
    });
  }
  return (
    <section className="card backup-settings">
      <h2>資料備份與還原</h2>
      <p className="muted">完整保存交易、已刪除帳目、分類、帳戶、成員與操作紀錄。任何能連到服務的人都可以操作。</p>
      <div className="backup-actions">
        <button className="primary" disabled={!!busy} onClick={() => void run("正在匯出備份…", () => download("/backups/export", "daily-expense.db"))}>匯出完整備份</button>
        <label>匯入 SQLite 備份（最多 100 MB）
          <input aria-label="匯入備份檔案" type="file" accept=".db,.sqlite,.sqlite3" disabled={!!busy} onChange={(e) => { const file = e.target.files?.[0]; e.target.value = ""; if (file) void upload(file); }} />
        </label>
      </div>
      <p className="management-hint">匯入會取代整本帳，不合併資料。裝置常用操作人不包含在備份內。建議將下載檔保存到其他裝置。</p>
      {busy && <p role="status">{busy}</p>}
      {message && <p role="status">{message}</p>}
      {error && <div className="error" role="alert">{error}{error.includes("帳本已還原") && <button onClick={() => window.location.reload()}>重新整理頁面</button>}</div>}
      {saved.length > 0 && <>
        <h3>還原前自動備份</h3>
        <p className="muted">保留最近 5 份，存於容器資料 volume。可下載後重新匯入，以回到還原前的帳本。</p>
        <ul className="backup-list">{saved.map((item) => <li key={item.name}>
          <span>{new Date(item.createdAt).toLocaleString("zh-TW")} · {(item.size / 1024).toFixed(0)} KB</span>
          <button disabled={!!busy} onClick={() => void run("正在下載自動備份…", () => download(`/backups/saved/${encodeURIComponent(item.name)}`, item.name))}>下載</button>
        </li>)}</ul>
      </>}
      {preview && <Modal title="確認還原整本帳" busy={!!busy} onClose={() => { setPreview(null); setError(""); }}>
        <p>交易 {preview.transactions} 筆（含已刪除 {preview.deletedTransactions} 筆）、分類 {preview.categories} 個、帳戶 {preview.accounts} 個。</p>
        <p>固定收支範本 {preview.recurringTransactions ?? 0} 個，排程與已產生來源會一起還原。</p>
        <p className="backup-summary">成員：{preview.members.join("、") || "無"}</p>
        <p>交易日期：{preview.firstDate ? `${preview.firstDate} ～ ${preview.lastDate}` : "無交易"}</p>
        {preview.wasUpgraded && <p className="family-notice">此為舊版備份（{preview.sourceMigration}），已在暫存副本升級至目前版本。原始備份檔與目前帳本尚未變更。</p>}
        <p className="error">還原會取代目前全部帳目，備份之後的資料不會保留。系統會先自動備份目前帳本。</p>
        <p className="muted">確認後暫停帳目操作。預覽有效至 {new Date(preview.expiresAt).toLocaleTimeString("zh-TW")}。若連線中斷，請先重新整理確認結果，再決定是否重試。</p>
        <label>輸入「還原」確認<input aria-label="還原確認文字" value={confirmation} disabled={!!busy} onChange={(e) => setConfirmation(e.target.value)} autoComplete="off" /></label>
        {error && <p className="error" role="alert">{error}</p>}
        <div className="dialog-actions">
          <button disabled={!!busy} onClick={() => { setPreview(null); setError(""); }}>取消</button>
          <button className="danger-fill" disabled={!!busy || confirmation !== "還原"} onClick={() => void run("正在還原，請勿關閉頁面…", async () => {
            await request("/backups/restore", { method: "POST", body: JSON.stringify({ token: preview.token, confirmation }) }, true);
            window.location.reload();
          })}>{busy ? "還原中…" : "確認取代並還原"}</button>
        </div>
      </Modal>}
    </section>
  );
}
