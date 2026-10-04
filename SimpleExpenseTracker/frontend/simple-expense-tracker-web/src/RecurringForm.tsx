import { useRef, useState, type FormEvent } from "react";
import { api, ApiError, save, type Account, type Category, type Member, type TransactionType } from "./api";
import Modal from "./Modal";
import { localDate } from "./format";
import { requestId } from "./family";
import { weekdays, weekdayNames, type AmountKind, type Frequency, type Recurring, type RecurringInput } from "./recurring";

export default function RecurringForm({ item, categories, accounts, members, onClose, onSaved, onReload }: {
  item?: Recurring; categories: Category[]; accounts: Account[]; members: Member[];
  onClose: () => void; onSaved: (message: string) => void; onReload: (item: Recurring) => void;
}) {
  const [name, setName] = useState(item?.name ?? "");
  const [type, setType] = useState<TransactionType>(item?.type ?? "Expense");
  const [categoryId, setCategory] = useState(item?.categoryId ?? categories.find(c => c.isActive && c.type === "Expense")?.id ?? 0);
  const [accountId, setAccount] = useState(item?.accountId ?? accounts.find(a => a.isActive)?.id ?? 0);
  const [memberId, setMember] = useState(item?.memberId ?? 0);
  const [amount, setAmount] = useState(item ? String(item.amount) : "");
  const [amountType, setAmountType] = useState<AmountKind>(item?.amountType ?? "Fixed");
  const [frequency, setFrequency] = useState<Frequency>(item?.frequency ?? "Monthly");
  const [interval, setInterval] = useState(item?.interval ?? 1);
  const [day, setDay] = useState(item?.dayOfMonth ?? new Date().getDate());
  const [weekday, setWeekday] = useState(item?.dayOfWeek ?? weekdays[new Date().getDay()]);
  const [month, setMonth] = useState(item?.monthOfYear ?? new Date().getMonth() + 1);
  const [startDate, setStart] = useState(item?.startDate ?? localDate());
  const [endDate, setEnd] = useState(item?.endDate ?? "");
  const [hasEnd, setHasEnd] = useState(!!item?.endDate);
  const [active, setActive] = useState(item?.isActive ?? true);
  const [note, setNote] = useState(item?.note ?? "");
  const [key] = useState(requestId);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [conflict, setConflict] = useState(false);
  const submitting = useRef(false);
  async function submit(event: FormEvent) {
    event.preventDefault(); if (submitting.current) return;
    setError(""); setConflict(false);
    if (!name.trim() || !/^\d+(\.\d{1,2})?$/.test(amount) || Number(amount) <= 0 || Number(amount) > 999999999999.99 || !categoryId || !accountId || !startDate || !Number.isInteger(interval) || interval < 1 || interval > 120 || (hasEnd && (!endDate || endDate < startDate))) {
      setError("請檢查名稱、金額（大於零，最多兩位小數）、帳戶、分類、間隔與日期。"); return;
    }
    const input: RecurringInput = { name: name.trim(), type, categoryId, accountId, memberId: memberId || null, amount: Number(amount), amountType, frequency, interval,
      dayOfMonth: frequency === "Weekly" ? null : day, dayOfWeek: frequency === "Weekly" ? weekday : null, monthOfYear: frequency === "Yearly" ? month : null,
      startDate, endDate: hasEnd ? endDate : null, isActive: active, note: note.trim() || null, version: item?.version, clientRequestId: key };
    submitting.current = true; setBusy(true);
    try { await save("/recurring-transactions", input, item?.id); onSaved(item ? "已更新固定收支，歷史帳目保持原樣" : "已新增固定收支"); }
    catch (e) { setError((e as Error).message); setConflict(e instanceof ApiError && e.status === 409); }
    finally { submitting.current = false; setBusy(false); }
  }
  async function reload() {
    if (!item) return; setBusy(true);
    try { onReload(await api<Recurring>(`/recurring-transactions/${item.id}`)); }
    catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }
  return <Modal title={item ? "編輯固定收支" : "新增固定收支"} busy={busy} onClose={onClose}>
    <form onSubmit={submit} className="transaction-form"><fieldset disabled={busy}>
      <label>名稱<input value={name} onChange={e => setName(e.target.value)} maxLength={100} required autoFocus placeholder="例如：手機費、薪資" /></label>
      <label>類型<select value={type} onChange={e => { const next = e.target.value as TransactionType; setType(next); setCategory(categories.find(c => c.isActive && c.type === next)?.id ?? 0); }}><option value="Expense">支出</option><option value="Income">收入</option></select></label>
      <label>分類<select value={categoryId} onChange={e => setCategory(Number(e.target.value))} required><option value={0} disabled>選擇分類</option>{categories.filter(c => c.type === type && (c.isActive || c.id === item?.categoryId)).map(c => <option key={c.id} value={c.id}>{c.icon} {c.name}{!c.isActive && "（已停用）"}</option>)}</select></label>
      <label>帳戶<select value={accountId} onChange={e => setAccount(Number(e.target.value))} required><option value={0} disabled>選擇帳戶</option>{accounts.filter(a => a.isActive || a.id === item?.accountId).map(a => <option key={a.id} value={a.id}>{a.name}{!a.isActive && "（已停用）"}</option>)}</select></label>
      <label>金額（NT$）<input inputMode="decimal" value={amount} onChange={e => setAmount(e.target.value)} required placeholder="0" /></label>
      <label>金額類型<select value={amountType} onChange={e => setAmountType(e.target.value as AmountKind)}><option value="Fixed">固定金額</option><option value="Variable">浮動／預估金額</option></select></label>
      <p className="management-hint">自動產生的金額使用範本預設值；可在帳目修改實際金額，不會改變範本。</p>
      <label>週期<select value={frequency} onChange={e => setFrequency(e.target.value as Frequency)}><option value="Weekly">每週</option><option value="Monthly">每月</option><option value="Yearly">每年</option></select></label>
      <label>每幾個週期<input type="number" min={1} max={120} step={1} value={interval} onChange={e => setInterval(Number(e.target.value))} required /></label>
      {frequency === "Weekly" ? <label>執行星期<select value={weekday} onChange={e => setWeekday(e.target.value)}>{weekdays.map((w,i) => <option key={w} value={w}>{weekdayNames[i]}</option>)}</select></label> : <>
        {frequency === "Yearly" && <label>執行月份<select value={month} onChange={e => setMonth(Number(e.target.value))}>{Array.from({length:12},(_,i) => <option key={i} value={i+1}>{i+1} 月</option>)}</select></label>}
        <label>執行日<input type="number" min={1} max={31} step={1} value={day} onChange={e => setDay(Number(e.target.value))} required /></label>
        <p className="management-hint">當月沒有指定日期時使用月底，下一個月仍按原日期計算。</p>
      </>}
      <label>開始日期<input type="date" value={startDate} onChange={e => setStart(e.target.value)} max="9998-12-31" required /></label>
      <label>結束方式<select value={hasEnd ? "date" : "none"} onChange={e => setHasEnd(e.target.value === "date")}><option value="none">無限期</option><option value="date">指定日期</option></select></label>
      {hasEnd && <label>結束日期<input type="date" value={endDate} onChange={e => setEnd(e.target.value)} min={startDate} max="9998-12-31" required /></label>}
      <label>歸屬<select value={memberId} onChange={e => setMember(Number(e.target.value))}><option value={0}>家庭共同</option>{members.filter(m => m.isActive || m.id === item?.memberId).map(m => <option key={m.id} value={m.id}>{m.name}{!m.isActive && "（已停用）"}</option>)}</select></label>
      <label>備註<input value={note} onChange={e => setNote(e.target.value)} maxLength={500} /></label>
      <label>狀態<select value={String(active)} onChange={e => setActive(e.target.value === "true")}><option value="true">啟用</option><option value="false">停用</option></select></label>
      <p className="management-hint">開始日期設於過去會補產生到今天的各期帳目。停用後重新啟用從今天起排程，不補停用期間。修改啟用範本前會先依原設定補產生到期帳目。</p>
      {item?.warning && <p className="family-notice">{item.warning}</p>}
      {error && <p className="error" role="alert">{error}</p>}
      {conflict && item && <button type="button" onClick={reload}>捨棄本次修改，載入最新內容</button>}
      <button type="submit" className="primary wide">{busy ? "儲存中…" : "儲存固定收支"}</button>
    </fieldset></form>
  </Modal>;
}
