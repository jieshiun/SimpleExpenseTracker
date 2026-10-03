import {
  PieChart,
  Pie,
  Cell,
  ResponsiveContainer,
  BarChart,
  Bar,
  XAxis,
  YAxis,
  Tooltip,
  CartesianGrid,
} from "recharts";
import { money } from "./format";
export interface Summary {
  income: number;
  expense: number;
  balance: number;
}
export interface CategoryStat {
  categoryId: number;
  categoryName: string;
  amount: number;
  percentage: number;
}
export interface MonthlyStat {
  year: number;
  month: number;
  income: number;
  expense: number;
}
const colors = [
  "#38735b",
  "#91b48b",
  "#d6b975",
  "#ce987b",
  "#b6c9a6",
  "#7ba1a0",
  "#a597ba",
  "#d0aaaf",
  "#819b60",
  "#bac6ba",
  "#97aeba",
];
export function SummaryCards({ data }: { data: Summary }) {
  return (
    <div className="summary-grid">
      <section className="summary-card">
        <span className="summary-label">
          <span className="metric-icon">↙</span>本月收入
        </span>
        <strong className="income">{money(data.income)}</strong>
        <small>每份努力，都值得記錄</small>
      </section>
      <section className="summary-card">
        <span className="summary-label">
          <span className="metric-icon warm">↗</span>本月支出
        </span>
        <strong>{money(data.expense)}</strong>
        <small>生活裡的每一份選擇</small>
      </section>
      <section className="summary-card balance-card">
        <span className="summary-label">
          <span className="metric-icon">≈</span>本月結餘
        </span>
        <strong>{money(data.balance)}</strong>
        <small>收入減去支出的當月結餘</small>
      </section>
    </div>
  );
}
export function CategoryChart({
  data,
  total,
}: {
  data: CategoryStat[];
  total: number;
}) {
  return (
    <section className="card category-card">
      <div className="section-heading">
        <h2>支出分類</h2>
        <span className="tag">本月花費</span>
      </div>
      {data.length ? (
        <div className="category-layout">
          <div className="donut-wrap">
            <ResponsiveContainer width="100%" height={220}>
              <PieChart>
                <Pie
                  data={data}
                  dataKey="amount"
                  nameKey="categoryName"
                  innerRadius={74}
                  outerRadius={96}
                  paddingAngle={3}
                  stroke="none"
                  isAnimationActive={false}
                >
                  {data.map((d, i) => (
                    <Cell key={d.categoryId} fill={colors[i % colors.length]} />
                  ))}
                </Pie>
                <Tooltip formatter={(value) => money(Number(value))} />
              </PieChart>
            </ResponsiveContainer>
            <div className="donut-label">
              <small>總支出</small>
              <strong>{money(total)}</strong>
            </div>
          </div>
          <div className="legend">
            {data.map((d, i) => (
              <div className="legend-row" key={d.categoryId}>
                <span
                  className="legend-dot"
                  style={{ background: colors[i % colors.length] }}
                />
                <span className="legend-name">{d.categoryName}</span>
                <strong>{money(d.amount)}</strong>
                <span className="legend-percent">
                  {d.percentage.toFixed(1)}%
                </span>
              </div>
            ))}
          </div>
        </div>
      ) : (
        <div className="empty chart-empty">
          <div className="empty-ring">
            <span>NT$ 0</span>
          </div>
          <p>這個月還沒有支出，從第一筆開始記錄。</p>
        </div>
      )}
    </section>
  );
}
export function MonthlyChart({ data }: { data: MonthlyStat[] }) {
  return (
    <section className="card trend-card">
      <div className="section-heading">
        <h2>最近 6 個月支出</h2>
        <span className="tag">含目前月份</span>
      </div>
      <div className="bar-wrap">
        <ResponsiveContainer width="100%" height={240}>
          <BarChart
            data={data.map((d) => ({
              ...d,
              label: `${d.year}/${String(d.month).padStart(2, "0")}`,
            }))}
            margin={{ left: 0, right: 4, top: 15, bottom: 5 }}
          >
            <CartesianGrid
              strokeDasharray="3 5"
              vertical={false}
              stroke="#e7ece3"
            />
            <XAxis
              dataKey="label"
              tickLine={false}
              axisLine={false}
              tick={{ fontSize: 10, fill: "#899486" }}
              tickFormatter={(v) => String(v).slice(5) + " 月"}
            />
            <YAxis
              width={48}
              axisLine={false}
              tickLine={false}
              tick={{ fontSize: 10, fill: "#899486" }}
              tickFormatter={(v) =>
                Number(v) >= 1000 ? `${Number(v) / 1000}k` : String(v)
              }
            />
            <Tooltip
              formatter={(v) => [money(Number(v)), "支出"]}
              cursor={{ fill: "#f1f5ed" }}
            />
            <Bar
              dataKey="expense"
              fill="#88aa7c"
              radius={[6, 6, 0, 0]}
              maxBarSize={38}
            />
          </BarChart>
        </ResponsiveContainer>
      </div>
      <details className="chart-table">
        <summary>查看月份明細</summary>
        <table>
          <thead>
            <tr>
              <th>月份</th>
              <th>收入</th>
              <th>支出</th>
            </tr>
          </thead>
          <tbody>
            {data.map((d) => (
              <tr key={`${d.year}-${d.month}`}>
                <td>
                  {d.year}/{String(d.month).padStart(2, "0")}
                </td>
                <td>{money(d.income)}</td>
                <td>{money(d.expense)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </details>
    </section>
  );
}
