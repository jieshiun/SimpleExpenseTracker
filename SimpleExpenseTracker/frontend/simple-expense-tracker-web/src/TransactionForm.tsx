import { useState, type FormEvent } from "react";
import {
  api,
  save,
  type Account,
  type Category,
  type Transaction,
  type TransactionType,
} from "./api";
import { localDate } from "./format";
import Modal from "./Modal";
export default function TransactionForm({
  transaction,
  categories,
  accounts,
  onClose,
  onSaved,
}: {
  transaction?: Transaction;
  categories: Category[];
  accounts: Account[];
  onClose: () => void;
  onSaved: (message: string) => void;
}) {
  const [type, setType] = useState<TransactionType>(
    transaction?.type ?? "Expense",
  );
  const [amount, setAmount] = useState(
    transaction ? String(transaction.amount) : "",
  );
  const [categoryId, setCategory] = useState(
    transaction?.categoryId ??
      categories.find((c) => c.isActive && c.type === "Expense")?.id ??
      0,
  );
  const [accountId, setAccount] = useState(
    transaction?.accountId ?? accounts.find((a) => a.isActive)?.id ?? 0,
  );
  const [date, setDate] = useState(
    transaction?.transactionDate.slice(0, 10) ?? localDate(),
  );
  const [note, setNote] = useState(transaction?.note ?? "");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [confirm, setConfirm] = useState(false);
  const changeType = (next: TransactionType) => {
    setType(next);
    setCategory(categories.find((c) => c.isActive && c.type === next)?.id ?? 0);
  };
  async function submit(e: FormEvent) {
    e.preventDefault();
    setError("");
    if (
      !/^\d+(\.\d{1,2})?$/.test(amount) ||
      Number(amount) <= 0 ||
      Number(amount) > 999999999999.99
    ) {
      setError("請輸入大於零的金額，最多兩位小數。");
      return;
    }
    if (!categoryId || !accountId || !date) {
      setError("請選擇分類、帳戶與日期。");
      return;
    }
    setBusy(true);
    try {
      await save(
        "/transactions",
        {
          type,
          amount: Number(amount),
          categoryId,
          accountId,
          transactionDate: date,
          note: note.trim() || null,
        },
        transaction?.id,
      );
      onSaved(transaction ? "已儲存修改" : "已新增一筆交易");
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  async function remove() {
    setBusy(true);
    setError("");
    try {
      await api(`/transactions/${transaction!.id}`, { method: "DELETE" });
      onSaved("已刪除交易");
    } catch (e) {
      setConfirm(false);
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <Modal
      title={transaction ? "編輯交易" : "記下一筆"}
      onClose={onClose}
      busy={busy}
    >
      <form onSubmit={submit} className="transaction-form">
        <fieldset disabled={busy}>
          <div className="segmented">
            <button
              type="button"
              aria-pressed={type === "Expense"}
              className={type === "Expense" ? "selected" : ""}
              onClick={() => changeType("Expense")}
            >
              支出
            </button>
            <button
              type="button"
              aria-pressed={type === "Income"}
              className={type === "Income" ? "selected" : ""}
              onClick={() => changeType("Income")}
            >
              收入
            </button>
          </div>
          <label className="amount-label" htmlFor="amount">
            金額 <small>NT$</small>
          </label>
          <div className="amount-input">
            <span>$</span>
            <input
              id="amount"
              inputMode="decimal"
              autoFocus
              placeholder="0"
              value={amount}
              onChange={(e) => setAmount(e.target.value)}
              autoComplete="off"
              required
            />
          </div>
          <p className="field-label">分類</p>
          <div className="category-grid" role="group" aria-label="分類">
            {categories
              .filter(
                (c) =>
                  c.type === type &&
                  (c.isActive || c.id === transaction?.categoryId),
              )
              .map((c) => (
                <button
                  type="button"
                  key={c.id}
                  aria-pressed={categoryId === c.id}
                  className={categoryId === c.id ? "selected" : ""}
                  onClick={() => setCategory(c.id)}
                >
                  <span>{c.icon}</span>
                  {c.name}
                  {!c.isActive && <small>已停用</small>}
                </button>
              ))}
          </div>
          {!categories.some((c) => c.isActive && c.type === type) && (
            <p className="muted">沒有可用分類，請先至設定新增。</p>
          )}
          <label>
            帳戶
            <select
              value={accountId}
              onChange={(e) => setAccount(Number(e.target.value))}
              required
            >
              <option value={0} disabled>
                選擇帳戶
              </option>
              {accounts
                .filter((a) => a.isActive || a.id === transaction?.accountId)
                .map((a) => (
                  <option value={a.id} key={a.id}>
                    {a.name}
                    {!a.isActive ? "（已停用）" : ""}
                  </option>
                ))}
            </select>
          </label>
          <label>
            日期
            <input
              aria-label="日期"
              type="date"
              max="9998-12-31"
              value={date}
              onChange={(e) => setDate(e.target.value)}
              required
            />
          </label>
          <label>
            備註 <span className="muted">選填</span>
            <input
              placeholder="這筆花費的小記錄…"
              value={note}
              onChange={(e) => setNote(e.target.value)}
              maxLength={500}
            />
          </label>
          {error && (
            <p role="alert" className="error">
              {error}
            </p>
          )}
          <button className="primary wide" type="submit">
            {busy ? "儲存中…" : transaction ? "儲存修改" : "儲存交易"}
          </button>
          {transaction && (
            <button
              type="button"
              className="danger wide"
              onClick={() => setConfirm(true)}
            >
              刪除這筆交易
            </button>
          )}
        </fieldset>
      </form>
      {confirm && (
        <Modal
          title="確定要刪除這筆交易嗎？"
          onClose={() => setConfirm(false)}
          busy={busy}
        >
          <p className="muted">刪除後無法復原。</p>
          <div className="dialog-actions">
            <button disabled={busy} onClick={() => setConfirm(false)}>
              取消
            </button>
            <button disabled={busy} className="danger-fill" onClick={remove}>
              {busy ? "刪除中…" : "刪除"}
            </button>
          </div>
        </Modal>
      )}
    </Modal>
  );
}
