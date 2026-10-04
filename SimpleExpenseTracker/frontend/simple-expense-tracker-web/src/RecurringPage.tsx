import { useEffect, useRef, useState } from "react";
import { api, type Account, type Category, type Member } from "./api";
import { money } from "./format";
import RecurringForm from "./RecurringForm";
import { scheduleLabel, type Generated, type Recurring, type RecurringSummary, type Upcoming } from "./recurring";

export default function RecurringPage({ categories, accounts, members, month, ownership, revision, onChanged }: {
  categories: Category[]; accounts: Account[]; members: Member[]; month: Date; ownership: string; revision: number; onChanged: (message: string) => void;
}) {
  const [items, setItems] = useState<Recurring[]>([]);
  const [summary, setSummary] = useState<RecurringSummary>();
  const [upcoming, setUpcoming] = useState<Upcoming[]>([]);
  const [status, setStatus] = useState(""); const [type, setType] = useState("");
  const [error, setError] = useState(""); const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false); const lock = useRef(false);
  const [form, setForm] = useState<{ item?: Recurring } | null>(null);
  const owner = ownership.startsWith("member:") ? `memberId=${ownership.slice(7)}` : ownership ? `ownership=${ownership}` : "";
  useEffect(() => {
    const controller = new AbortController(); const options = { signal: controller.signal };
    setLoading(true); setError("");
    Promise.all([
      api<Recurring[]>(`/recurring-transactions?${owner}${status ? `&isActive=${status}` : ""}${type ? `&type=${type}` : ""}`, options),
      api<RecurringSummary>(`/recurring-transactions/summary?${owner}&year=${month.getFullYear()}&month=${month.getMonth() + 1}`, options),
      api<Upcoming[]>(`/recurring-transactions/upcoming?${owner}&days=30`, options),
    ]).then(([rows, totals, next]) => { if (!controller.signal.aborted) { setItems(rows); setSummary(totals); setUpcoming(next); } })
      .catch((e: Error) => { if (e.name !== "AbortError") setError(e.message); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [owner, status, type, month, revision]);
  async function action(item?: Recurring) {
    if (lock.current) return;
    if (item?.isActive && !window.confirm(`停用「${item.name}」？已產生的帳目會保留。`)) return;
    lock.current = true; setBusy(true); setError("");
    try {
      if (item) {
        await api(`/recurring-transactions/${item.id}/status`, { method: "PATCH", body: JSON.stringify({ isActive: !item.isActive, version: item.version }) });
        onChanged(item.isActive ? "已停用固定收支" : "已啟用固定收支，從今天起繼續排程");
      } else {
        const result = await api<Generated>("/recurring-transactions/generate", { method: "POST" });
        onChanged(`已補產生 ${result.generated} 筆帳目${result.blocked ? `，${result.blocked} 個範本因停用項目暫停` : ""}${result.hasMore ? "；尚有待處理項目，請再次補產生" : ""}`);
      }
    } catch (e) { setError((e as Error).message); }
    finally { lock.current = false; setBusy(false); }
  }
  return <div className="recurring-page">
    <p className="family-notice">以下為範本預估，實際收支統計只計入已產生的帳目。不固定金額會先使用預估值，產生後可逐筆修改。</p>
    {summary && <div className="recurring-summary">{[
      ["本月預估支出", summary.monthlyExpense], ["本月預估收入", summary.monthlyIncome],
      ["全年預估支出", summary.annualExpense], ["全年預估收入", summary.annualIncome],
    ].map(([label, amount]) => <section className="card" key={String(label)}><p className="muted">{label}</p><h2>{money(Number(amount))}</h2></section>)}</div>}
    {error && <p className="error" role="alert">{error} <button onClick={() => error.includes("帳本已還原") ? window.location.reload() : onChanged("已重新載入")}>重新載入</button></p>}
    <section className="card recurring-section"><div className="section-heading"><h2>未來 30 天與待補帳目</h2><button disabled={busy || loading} onClick={() => void action()}>立即補產生</button></div>
      {!upcoming.length && <p className="muted">沒有即將到期的固定收支。</p>}
      {upcoming.map(r => <div className="recurring-row" key={r.id}><div><strong>{r.name}</strong><p className="muted">{r.nextRunDate} · {r.memberName ?? "家庭共同"} · {r.amountType === "Variable" ? "預估金額" : "固定金額"}</p></div><strong>{r.type === "Income" ? "+" : "−"}{money(r.amount)}</strong></div>)}
    </section>
    <section className="card recurring-section"><div className="section-heading"><h2>固定收支範本</h2><button className="primary" onClick={() => setForm({})}>新增固定收支</button></div>
      <div className="recurring-filters"><select aria-label="固定收支狀態" value={status} onChange={e => setStatus(e.target.value)}><option value="">全部狀態</option><option value="true">啟用</option><option value="false">停用</option></select><select aria-label="固定收支類型" value={type} onChange={e => setType(e.target.value)}><option value="">全部收支</option><option value="Expense">支出</option><option value="Income">收入</option></select></div>
      {loading && <p role="status">正在讀取固定收支…</p>}
      {!loading && !items.length && <p className="muted">尚無符合篩選的固定收支。</p>}
      {items.map(r => <article className="recurring-row" key={r.id}><div><strong>{r.name}</strong><span className="recurring-badge">{!r.isActive ? "停用" : r.nextRunDate ? "啟用" : "已結束"}</span><p>{scheduleLabel(r)} · {r.memberName ?? "家庭共同"}</p><p className="muted">{r.categoryName} · {r.accountName} · {r.amountType === "Variable" ? "不固定／預估" : "固定"} {money(r.amount)}</p><p className="muted">下次：{r.nextRunDate ?? "無"} · 上次產生：{r.lastGeneratedDate ?? "尚未產生"}</p>{r.warning && <p role="status" className="error">{r.warning}</p>}</div><div className="recurring-actions"><button onClick={() => setForm({ item: r })}>編輯</button><button disabled={busy} onClick={() => void action(r)}>{r.isActive ? "停用" : "啟用"}</button></div></article>)}
    </section>
    {form && <RecurringForm key={form.item ? `${form.item.id}:${form.item.version}` : "new"} item={form.item} categories={categories} accounts={accounts} members={members} onClose={() => setForm(null)} onReload={item => setForm({ item })} onSaved={message => { setForm(null); onChanged(message); }} />}
  </div>;
}
