import { useEffect, useState } from "react";
import {
  api,
  type Member,
  type Account,
  type Category,
  type Page,
  type Transaction,
} from "./api";
import Icon, { type IconName } from "./Icon";
import TransactionForm from "./TransactionForm";
import TransactionList from "./TransactionList";
import Settings from "./Settings";
import FamilySettings from "./FamilySettings";
import BackupSettings from "./BackupSettings";
import { familyQuery, rememberedActor, rememberActor } from "./family";
import {
  SummaryCards,
  CategoryChart,
  MonthlyChart,
  type Summary,
  type CategoryStat,
  type MonthlyStat,
} from "./Statistics";
type Tab = "home" | "list" | "chart" | "settings";
const tabs: { id: Tab; title: string; icon: IconName }[] = [
  { id: "home", title: "首頁", icon: "home" },
  { id: "list", title: "帳目", icon: "list" },
  { id: "chart", title: "統計", icon: "chart" },
  { id: "settings", title: "設定", icon: "settings" },
];
export default function App() {
  const [members, setMembers] = useState<Member[]>([]);
  const [actor, setActor] = useState(rememberedActor);
  const [ownership, setOwnership] = useState("");
  const [deleted, setDeleted] = useState(false);
  const [loaded, setLoaded] = useState(false);
  const validActor = members.some((m) => m.id === actor && m.isActive);
  const family = familyQuery(ownership);
  const [tab, setTab] = useState<Tab>("home");
  const [month, setMonth] = useState(
    () => new Date(new Date().getFullYear(), new Date().getMonth(), 1),
  );
  const [categories, setCategories] = useState<Category[]>([]);
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [transactions, setTransactions] = useState<Page<Transaction>>({
    items: [],
    total: 0,
    page: 1,
    pageSize: 50,
  });
  const [page, setPage] = useState(1);
  const [filter, setFilter] = useState("");
  const [revision, setRevision] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [toast, setToast] = useState("");
  const [form, setForm] = useState<{ transaction?: Transaction } | null>(null);
  const [summary, setSummary] = useState<Summary>({
    income: 0,
    expense: 0,
    balance: 0,
  });
  const [categoryStats, setCategoryStats] = useState<CategoryStat[]>([]);
  const [monthlyStats, setMonthlyStats] = useState<MonthlyStat[]>([]);
  const query = `year=${month.getFullYear()}&month=${month.getMonth() + 1}${family}`;
  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError("");
    const options = { signal: controller.signal };
    Promise.all([
      api<Member[]>("/members", options),
      api<Category[]>("/categories", options),
      api<Account[]>("/accounts", options),
      api<Page<Transaction>>(
        `/transactions?${tab === "list" && deleted ? `deleted=true${family}` : query}&page=${tab === "home" ? 1 : page}&pageSize=${tab === "home" ? 5 : 50}${tab === "list" && filter ? `&type=${filter}` : ""}`,
        options,
      ),
      api<Summary>(`/dashboard/summary?${query}`, options),
      api<CategoryStat[]>(
        `/statistics/categories?${query}&type=Expense`,
        options,
      ),
      api<MonthlyStat[]>(`/statistics/monthly?months=6${family}`, options),
    ])
      .then(([m, c, a, t, s, cs, ms]) => {
        if (controller.signal.aborted) return;
        setMembers(m);
        setLoaded(true);
        setCategories(c);
        setAccounts(a);
        setTransactions(t);
        setSummary(s);
        setCategoryStats(cs);
        setMonthlyStats(ms);
      })
      .catch((e: Error) => {
        if (e.name !== "AbortError") setError(e.message);
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [query, page, tab, filter, revision, deleted, family]);
  useEffect(() => {
    if (!toast) return;
    const timer = setTimeout(() => setToast(""), 3000);
    return () => clearTimeout(timer);
  }, [toast]);
  useEffect(() => {
    const refresh = () => {
      if (!document.hidden) setRevision((v) => v + 1);
    };
    window.addEventListener("focus", refresh);
    document.addEventListener("visibilitychange", refresh);
    return () => {
      window.removeEventListener("focus", refresh);
      document.removeEventListener("visibilitychange", refresh);
    };
  }, []);
  function switchMonth(delta: number) {
    setMonth((d) => new Date(d.getFullYear(), d.getMonth() + delta, 1));
    setPage(1);
  }
  function saved(message: string) {
    setForm(null);
    setToast(message);
    setRevision((v) => v + 1);
    setPage(1);
  }
  return (
    <div className="app-shell">
      <header className="site-header">
        <a
          href="#"
          onClick={(e) => {
            e.preventDefault();
            setTab("home");
          }}
          className="brand"
        >
          <span className="brand-mark">
            <Icon name="wallet" size={23} />
          </span>
          <span>
            日常記帳<small>把日子，記得剛剛好。</small>
          </span>
        </a>
        <span className="header-note">一筆一筆，讓生活更清楚</span>
        <button
          className="primary desktop-add"
          onClick={() => setForm({})}
          disabled={loading || !!error}
        >
          <Icon name="plus" size={18} />
          記一筆
        </button>
      </header>
      <main>
        <div className="page-heading">
          <div>
            <p className="eyebrow">
              {tab === "home"
                ? "YOUR DAILY OVERVIEW"
                : tab === "list"
                  ? "LIFE IN NUMBERS"
                  : tab === "chart"
                    ? "A LITTLE MORE CLARITY"
                    : "MAKE IT YOURS"}
            </p>
            <h1>
              {tab === "home"
                ? "每一筆，都是生活。"
                : tab === "list"
                  ? "家庭帳目"
                  : tab === "chart"
                    ? "收支統計"
                    : "設定"}
            </h1>
            <p className="muted">
              {tab === "home"
                ? "從小小的記錄，慢慢掌握自己的步調。"
                : tab === "list"
                  ? "日常的收入與花費，都好好記在這裡。"
                  : tab === "chart"
                    ? "看看這個月，錢都花在哪裡。"
                    : "打造適合自己的記帳習慣。"}
            </p>
          </div>
          {tab !== "settings" && !(tab === "list" && deleted) && (
            <div className="month-switch">
              <button aria-label="上一個月" onClick={() => switchMonth(-1)}>
                <Icon name="left" size={18} />
              </button>
              <span>
                {month.getFullYear()} 年 {month.getMonth() + 1} 月
              </span>
              <button aria-label="下一個月" onClick={() => switchMonth(1)}>
                <Icon name="right" size={18} />
              </button>
            </div>
          )}
        </div>
        <section className="family-toolbar" aria-label="家庭記帳選項">
          <label>
            這台裝置常用操作人
            <select
              value={validActor ? actor : 0}
              onChange={(e) => {
                const id = Number(e.target.value);
                setActor(id);
                rememberActor(id);
              }}
            >
              <option value={0}>請選擇操作人</option>
              {members
                .filter((m) => m.isActive)
                .map((m) => (
                  <option key={m.id} value={m.id}>
                    {m.name}
                  </option>
                ))}
            </select>
          </label>
          {tab !== "settings" && (
            <label>
              收支歸屬篩選
              <select
                value={ownership}
                onChange={(e) => {
                  setOwnership(e.target.value);
                  setPage(1);
                }}
              >
                <option value="">全家全部收支</option>
                {members.map((m) => (
                  <option key={m.id} value={`member:${m.id}`}>
                    {m.name}
                    {!m.isActive && "（已停用）"}
                  </option>
                ))}
                <option value="Shared">家庭共同</option>
                <option value="Unknown">歸屬待確認</option>
              </select>
            </label>
          )}
          <button disabled={loading} onClick={() => setRevision((v) => v + 1)}>
            {loading ? "更新中…" : "重新整理"}
          </button>
        </section>
        {!validActor && loaded && (
          <p className="family-notice">
            首次使用請選擇常用操作人，也可以在新增或修改時選擇。成員名稱可到設定修改。
          </p>
        )}
        {tab !== "settings" && ownership && (
          <p className="muted">
            目前清單與統計只顯示所選歸屬；共同帳目不拆分計算。
          </p>
        )}
        {tab === "list" && (
          <div className="segmented ledger-mode">
            <button
              className={!deleted ? "selected" : ""}
              onClick={() => {
                setDeleted(false);
                setPage(1);
              }}
            >
              目前帳目
            </button>
            <button
              className={deleted ? "selected" : ""}
              onClick={() => {
                setDeleted(true);
                setPage(1);
              }}
            >
              已刪除帳目（全部月份）
            </button>
          </div>
        )}
        {error ? (
          <div role="alert" className="error">
            {error}
            <button onClick={() => error.includes("帳本已還原") ? window.location.reload() : setRevision((v) => v + 1)}>重新載入</button>
          </div>
        ) : loading && !loaded ? (
          <div className="loading" role="status">
            正在整理你的帳目…
          </div>
        ) : (
          <>
            {(tab === "home" || tab === "chart") && (
              <SummaryCards data={summary} />
            )}
            <div
              className={
                tab === "home" || tab === "chart" ? "content-grid" : ""
              }
            >
              {(tab === "home" || tab === "chart") && (
                <CategoryChart data={categoryStats} total={summary.expense} />
              )}
              {(tab === "home" || tab === "list") && (
                <section className="card">
                  <div className="section-heading">
                    <h2>
                      {tab === "home"
                        ? "最近交易"
                        : deleted
                          ? "已刪除帳目"
                          : "收支明細"}
                    </h2>
                    {tab === "home" ? (
                      <button
                        className="text-button"
                        onClick={() => setTab("list")}
                      >
                        查看全部 <Icon name="right" size={16} />
                      </button>
                    ) : (
                      <select
                        aria-label="交易類型篩選"
                        value={filter}
                        onChange={(e) => {
                          setFilter(e.target.value);
                          setPage(1);
                        }}
                      >
                        <option value="">全部收支</option>
                        <option value="Expense">支出</option>
                        <option value="Income">收入</option>
                      </select>
                    )}
                  </div>
                  <TransactionList
                    items={transactions.items}
                    onEdit={(transaction) => setForm({ transaction })}
                  />
                  {tab === "list" && transactions.total > 50 && (
                    <div className="pagination">
                      <button
                        disabled={page === 1}
                        onClick={() => setPage((p) => p - 1)}
                      >
                        上一頁
                      </button>
                      <span>
                        {page} / {Math.ceil(transactions.total / 50)}
                      </span>
                      <button
                        disabled={page * 50 >= transactions.total}
                        onClick={() => setPage((p) => p + 1)}
                      >
                        下一頁
                      </button>
                    </div>
                  )}
                </section>
              )}
              {tab === "chart" && <MonthlyChart data={monthlyStats} />}
            </div>
            {tab === "settings" && (
              <>
                <FamilySettings members={members} onChanged={saved} />
                <BackupSettings />
                <Settings
                  categories={categories}
                  accounts={accounts}
                  onChanged={saved}
                />
              </>
            )}
          </>
        )}
        <p className="page-footnote">簡單記錄，把心力留給生活。</p>
      </main>
      <nav className="bottom-nav" aria-label="主要導覽">
        {tabs.map((item, index) => (
          <div className="nav-slot" key={item.id}>
            {index === 2 && (
              <button
                className="add-button"
                aria-label="新增交易"
                disabled={loading || !!error}
                onClick={() => setForm({})}
              >
                <Icon name="plus" size={29} />
              </button>
            )}
            <button
              aria-current={tab === item.id ? "page" : undefined}
              className={tab === item.id ? "active" : ""}
              onClick={() => {
                setTab(item.id);
                setPage(1);
              }}
            >
              <Icon name={item.icon} />
              <span>{item.title}</span>
            </button>
          </div>
        ))}
      </nav>
      {form && (
        <TransactionForm
          key={
            form.transaction
              ? `${form.transaction.id}:${form.transaction.version}`
              : "new"
          }
          members={members}
          defaultActor={validActor ? actor : 0}
          onReload={(transaction) => setForm({ transaction })}
          transaction={form.transaction}
          categories={categories}
          accounts={accounts}
          onClose={() => setForm(null)}
          onSaved={saved}
        />
      )}
      {toast && (
        <div className="toast" role="status">
          ✓ {toast}
        </div>
      )}
    </div>
  );
}
