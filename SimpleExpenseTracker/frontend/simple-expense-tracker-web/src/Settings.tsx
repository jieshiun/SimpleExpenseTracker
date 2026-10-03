import { useState, type FormEvent } from "react";
import {
  save,
  type Account,
  type AccountType,
  type Category,
  type TransactionType,
} from "./api";
import Modal from "./Modal";
import Icon from "./Icon";

const accountTypes: Record<AccountType, string> = {
  Cash: "現金",
  Bank: "銀行",
  CreditCard: "信用卡",
  EWallet: "電子錢包",
  Other: "其他",
};
type Editor =
  | { kind: "category"; item?: Category }
  | { kind: "account"; item?: Account };

export default function Settings({
  categories,
  accounts,
  onChanged,
}: {
  categories: Category[];
  accounts: Account[];
  onChanged: (message: string) => void;
}) {
  const [type, setType] = useState<TransactionType>("Expense");
  const [editor, setEditor] = useState<Editor | null>(null);
  return (
    <div className="content-grid settings-grid">
      <section className="card">
        <div className="section-heading">
          <h2>分類管理</h2>
          <button
            className="text-button"
            onClick={() => setEditor({ kind: "category" })}
          >
            <Icon name="plus" size={17} />
            新增分類
          </button>
        </div>
        <div className="segmented">
          <button
            className={type === "Expense" ? "selected" : ""}
            aria-pressed={type === "Expense"}
            onClick={() => setType("Expense")}
          >
            支出分類
          </button>
          <button
            className={type === "Income" ? "selected" : ""}
            aria-pressed={type === "Income"}
            onClick={() => setType("Income")}
          >
            收入分類
          </button>
        </div>
        <p className="management-hint">點選分類即可修改、排序或停用。</p>
        {categories
          .filter((c) => c.type === type)
          .map((c) => (
            <button
              key={c.id}
              className="management-row"
              onClick={() => setEditor({ kind: "category", item: c })}
            >
              <span className="transaction-icon">{c.icon}</span>
              <span className="management-name">{c.name}</span>
              {!c.isActive && <span className="tag">已停用</span>}
              <Icon name="right" size={17} />
            </button>
          ))}
        {!categories.some((c) => c.type === type) && (
          <p className="empty">還沒有分類，新增一個開始吧。</p>
        )}
      </section>
      <section className="card">
        <div className="section-heading">
          <h2>帳戶管理</h2>
          <button
            className="text-button"
            onClick={() => setEditor({ kind: "account" })}
          >
            <Icon name="plus" size={17} />
            新增帳戶
          </button>
        </div>
        <p className="management-hint">記錄付款方式與收入來源。</p>
        {accounts.map((a) => (
          <button
            key={a.id}
            className="management-row"
            onClick={() => setEditor({ kind: "account", item: a })}
          >
            <span className="transaction-icon income-bg">
              <Icon name="wallet" />
            </span>
            <span className="management-name">
              {a.name}
              <small>{accountTypes[a.type]}</small>
            </span>
            {!a.isActive && <span className="tag">已停用</span>}
            <Icon name="right" size={17} />
          </button>
        ))}
        {!accounts.length && (
          <p className="empty">還沒有帳戶，新增一個開始吧。</p>
        )}
      </section>
      {editor && (
        <ItemEditor
          editor={editor}
          defaultType={type}
          onClose={() => setEditor(null)}
          onSaved={(message) => {
            setEditor(null);
            onChanged(message);
          }}
        />
      )}
    </div>
  );
}

function ItemEditor({
  editor,
  defaultType,
  onClose,
  onSaved,
}: {
  editor: Editor;
  defaultType: TransactionType;
  onClose: () => void;
  onSaved: (message: string) => void;
}) {
  const item = editor.item;
  const [name, setName] = useState(item?.name ?? "");
  const [active, setActive] = useState(item?.isActive ?? true);
  const [categoryType, setCategoryType] = useState<TransactionType>(
    editor.kind === "category"
      ? (editor.item?.type ?? defaultType)
      : defaultType,
  );
  const [accountType, setAccountType] = useState<AccountType>(
    editor.kind === "account" ? (editor.item?.type ?? "Cash") : "Cash",
  );
  const [icon, setIcon] = useState(
    editor.kind === "category" ? (editor.item?.icon ?? "📌") : "",
  );
  const [order, setOrder] = useState(
    editor.kind === "category" ? (editor.item?.sortOrder ?? 0) : 0,
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const title = `${item ? "編輯" : "新增"}${editor.kind === "category" ? "分類" : "帳戶"}`;
  async function submit(e: FormEvent) {
    e.preventDefault();
    setError("");
    if (!name.trim() || (editor.kind === "category" && !icon.trim())) {
      setError("請填寫名稱與分類圖示。");
      return;
    }
    setBusy(true);
    try {
      if (editor.kind === "category")
        await save(
          "/categories",
          {
            name: name.trim(),
            type: categoryType,
            icon: icon.trim(),
            sortOrder: order,
            isActive: active,
          },
          item?.id,
        );
      else
        await save(
          "/accounts",
          {
            name: name.trim(),
            type: accountType,
            initialBalance: editor.item?.initialBalance ?? 0,
            isActive: active,
          },
          item?.id,
        );
      onSaved("已儲存設定");
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <Modal title={title} onClose={onClose} busy={busy}>
      <form onSubmit={submit}>
        <fieldset disabled={busy}>
          <label>
            名稱
            <input
              value={name}
              autoFocus
              maxLength={50}
              required
              onChange={(e) => setName(e.target.value)}
              placeholder={
                editor.kind === "category" ? "例如：咖啡" : "例如：玉山銀行"
              }
            />
          </label>
          {editor.kind === "category" ? (
            <>
              <label>
                圖示
                <input
                  value={icon}
                  onChange={(e) => setIcon(e.target.value)}
                  maxLength={16}
                  required
                />
              </label>
              <label>
                類型
                <select
                  value={categoryType}
                  onChange={(e) =>
                    setCategoryType(e.target.value as TransactionType)
                  }
                >
                  <option value="Expense">支出</option>
                  <option value="Income">收入</option>
                </select>
              </label>
              <label>
                排序
                <input
                  type="number"
                  min={0}
                  max={10000}
                  step={1}
                  value={order}
                  onChange={(e) => setOrder(Number(e.target.value))}
                  required
                />
              </label>
              <p className="management-hint">
                數字越小越靠前；已使用分類不能更改收支類型。
              </p>
            </>
          ) : (
            <label>
              帳戶類型
              <select
                value={accountType}
                onChange={(e) => setAccountType(e.target.value as AccountType)}
              >
                {Object.entries(accountTypes).map(([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </select>
            </label>
          )}
          <label className="check-label">
            <input
              type="checkbox"
              checked={active}
              onChange={(e) => setActive(e.target.checked)}
            />
            啟用{editor.kind === "category" ? "分類" : "帳戶"}
          </label>
          <p className="management-hint">
            停用後保留歷史帳目，新增交易時不再顯示。
          </p>
          {error && (
            <p className="error" role="alert">
              {error}
            </p>
          )}
          <button className="primary wide" type="submit">
            {busy ? "儲存中…" : "儲存設定"}
          </button>
        </fieldset>
      </form>
    </Modal>
  );
}
