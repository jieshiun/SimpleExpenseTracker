import type { Transaction } from "./api";
import { money } from "./format";
import Icon from "./Icon";
import { ownerLabel } from "./family";
export default function TransactionList({
  items,
  onEdit,
}: {
  items: Transaction[];
  onEdit: (t: Transaction) => void;
}) {
  if (!items.length)
    return (
      <div className="empty">
        <span className="empty-icon">
          <Icon name="wallet" size={32} />
        </span>
        <h3>留下一筆生活記錄</h3>
        <p>目前條件下沒有帳目。</p>
      </div>
    );
  let previous = "";
  return (
    <div>
      {items.map((t) => {
        const day = t.transactionDate.slice(0, 10);
        const header = previous !== day;
        previous = day;
        return (
          <section key={t.id}>
            {header && (
              <h3 className="date-heading">{day.replaceAll("-", "/")}</h3>
            )}
            <button className="transaction-row" onClick={() => onEdit(t)}>
              <span
                className={`transaction-icon ${t.type === "Income" ? "income-bg" : ""}`}
              >
                {t.categoryIcon}
              </span>
              <span className="transaction-copy">
                <strong>{t.note || t.categoryName}</strong>
                <small>
                  {t.categoryName} · {t.accountName} · {ownerLabel(t)}
                </small>
              </span>
              <strong
                className={`transaction-amount ${t.type === "Income" ? "income" : ""}`}
              >
                {t.type === "Income" ? "+" : "−"}
                {money(t.amount)}
              </strong>
              <span className="row-arrow">
                <Icon name="right" size={16} />
              </span>
            </button>
          </section>
        );
      })}
    </div>
  );
}
