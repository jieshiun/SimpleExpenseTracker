import { useState, useRef, type FormEvent } from "react";
import {
  api,
  ApiError,
  type Member,
  save,
  type Account,
  type Category,
  type Transaction,
  type TransactionType,
} from "./api";
import { localDate } from "./format";
import Modal from "./Modal";
import { requestId } from "./family";
export default function TransactionForm({
  transaction,
  categories,
  accounts,
  members,
  defaultActor,
  onReload,
  onClose,
  onSaved,
}: {
  transaction?: Transaction;
  categories: Category[];
  accounts: Account[];
  members: Member[];
  defaultActor: number;
  onReload?: (transaction: Transaction) => void;
  onClose: () => void;
  onSaved: (message: string) => void;
}) {
  const [actor, setActor] = useState(defaultActor);
  const [owner, setOwner] = useState(
    transaction
      ? transaction.ownership === "Personal"
        ? String(transaction.ownerMemberId)
        : transaction.ownership
      : defaultActor
        ? String(defaultActor)
        : "",
  );
  const [key] = useState(requestId);
  const submitting = useRef(false);
  const [conflict, setConflict] = useState(false);
  const validActor = members.some((m) => m.id === actor && m.isActive);
  function failed(e: unknown) {
    setError((e as Error).message);
    setConflict(
      e instanceof ApiError && (e.status === 409 || e.status === 404),
    );
  }
  async function reload() {
    if (!transaction) return;
    setBusy(true);
    try {
      onReload?.(await api<Transaction>(`/transactions/${transaction.id}`));
    } catch (e) {
      failed(e);
    } finally {
      setBusy(false);
    }
  }
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
    if (submitting.current) return;
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
    if (!validActor || !owner) {
      setError("請選擇本次操作人與收支歸屬。");
      return;
    }
    submitting.current = true;
    setBusy(true);
    try {
      await save(
        "/transactions",
        {
          ownership:
            owner === "Shared" || owner === "Unknown" ? owner : "Personal",
          ownerMemberId:
            owner === "Shared" || owner === "Unknown" ? null : Number(owner),
          operatorId: actor,
          version: transaction?.version,
          clientRequestId: key,
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
      failed(e);
    } finally {
      submitting.current = false;
      setBusy(false);
    }
  }
  async function remove() {
    if (submitting.current || !validActor) return;
    submitting.current = true;
    setBusy(true);
    setError("");
    try {
      await api(`/transactions/${transaction!.id}`, {
        method: "DELETE",
        body: JSON.stringify({
          operatorId: actor,
          version: transaction!.version,
        }),
      });
      onSaved("已移至已刪除帳目，可隨時復原");
    } catch (e) {
      setConfirm(false);
      failed(e);
    } finally {
      submitting.current = false;
      setBusy(false);
    }
  }
  async function restore() {
    if (submitting.current || !validActor) return;
    submitting.current = true;
    setBusy(true);
    setError("");
    try {
      await api(`/transactions/${transaction!.id}/restore`, {
        method: "POST",
        body: JSON.stringify({
          operatorId: actor,
          version: transaction!.version,
        }),
      });
      onSaved("已復原交易");
    } catch (e) {
      failed(e);
    } finally {
      submitting.current = false;
      setBusy(false);
    }
  }
  const stamp = (value: string) =>
    new Date(value.endsWith("Z") ? value : value + "Z").toLocaleString("zh-TW");
  return (
    <Modal
      title={
        transaction?.isDeleted
          ? "已刪除交易"
          : transaction
            ? "編輯交易"
            : "記下一筆"
      }
      onClose={onClose}
      busy={busy}
    >
      <form onSubmit={submit} className="transaction-form">
        <fieldset disabled={busy}>
          <label>
            本次操作人
            <select
              aria-label="本次操作人"
              value={validActor ? actor : 0}
              onChange={(e) => {
                const id = Number(e.target.value);
                setActor(id);
                if (!transaction && !owner) setOwner(String(id));
              }}
              required
            >
              <option value={0} disabled>
                請選擇操作人
              </option>
              {members
                .filter((m) => m.isActive)
                .map((m) => (
                  <option key={m.id} value={m.id}>
                    {m.name}
                  </option>
                ))}
            </select>
          </label>
          <label>
            收支歸屬
            <select
              aria-label="收支歸屬"
              value={owner}
              disabled={transaction?.isDeleted}
              onChange={(e) => setOwner(e.target.value)}
              required
            >
              <option value="" disabled>
                請選擇歸屬
              </option>
              {members
                .filter(
                  (m) => m.isActive || m.id === transaction?.ownerMemberId,
                )
                .map((m) => (
                  <option key={m.id} value={m.id}>
                    {m.name}
                    {!m.isActive ? "（已停用）" : ""}
                  </option>
                ))}
              <option value="Shared">家庭共同</option>
              {transaction?.ownership === "Unknown" && (
                <option value="Unknown">歸屬待確認</option>
              )}
            </select>
          </label>
          <p className="muted">兩人都能查看所有收支；操作人只作紀錄。</p>
          <fieldset disabled={transaction?.isDeleted}>
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
          </fieldset>
          {transaction && (
            <div className="audit-info">
              <p>
                建立：{transaction.createdByName ?? "未記錄"} ·{" "}
                {stamp(transaction.createdAt)}
              </p>
              <p>
                最後操作：{transaction.updatedByName ?? "未記錄"} ·{" "}
                {stamp(transaction.updatedAt)}
              </p>
              {transaction.isDeleted && transaction.deletedAt && (
                <p>
                  刪除：{transaction.deletedByName ?? "未記錄"} ·{" "}
                  {stamp(transaction.deletedAt)}
                </p>
              )}
            </div>
          )}
          {error && (
            <p role="alert" className="error">
              {error}
            </p>
          )}
          {conflict && transaction && !transaction.isDeleted && (
            <button type="button" onClick={reload}>
              捨棄本次修改，載入最新內容
            </button>
          )}
          {conflict && (
            <p className="muted">
              也可以關閉視窗後重新整理帳目，確認最新儲存結果。
            </p>
          )}
          {transaction?.isDeleted ? (
            <button
              type="button"
              className="primary wide"
              disabled={!validActor}
              onClick={restore}
            >
              {busy ? "復原中…" : "復原交易"}
            </button>
          ) : (
            <button className="primary wide" type="submit">
              {busy ? "儲存中…" : transaction ? "儲存修改" : "儲存交易"}
            </button>
          )}
          {transaction && !transaction.isDeleted && (
            <button
              type="button"
              className="danger wide"
              disabled={!validActor}
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
          <p className="muted">刪除後不計入統計，可到「已刪除帳目」復原。</p>
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
