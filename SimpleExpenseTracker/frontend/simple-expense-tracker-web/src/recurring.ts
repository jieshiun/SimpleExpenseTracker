import type { TransactionType } from "./api";
export type Frequency = "Weekly" | "Monthly" | "Yearly";
export type AmountKind = "Fixed" | "Variable";
export const weekdays = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"] as const;
export const weekdayNames = ["星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六"];
export interface RecurringInput {
  name: string; type: TransactionType; categoryId: number; accountId: number; memberId: number | null;
  amount: number; amountType: AmountKind; frequency: Frequency; interval: number;
  dayOfMonth: number | null; dayOfWeek: string | null; monthOfYear: number | null;
  startDate: string; endDate: string | null; isActive: boolean; note: string | null;
  version?: number; clientRequestId?: string;
}
export interface Recurring extends RecurringInput {
  id: number; categoryName: string; categoryIcon: string; accountName: string; memberName: string | null;
  nextRunDate: string | null; lastGeneratedDate: string | null; version: number; warning: string | null;
}
export interface RecurringSummary {
  monthlyExpense: number; monthlyIncome: number; annualExpense: number; annualIncome: number; activeCount: number; year: number; month: number;
}
export interface Upcoming { id: number; name: string; nextRunDate: string; amount: number; type: TransactionType; amountType: AmountKind; memberId: number | null; memberName: string | null }
export interface Generated { generated: number; duplicates: number; blocked: number; hasMore: boolean }
export function frequencyLabel(frequency: Frequency, interval: number): string {
  if (interval === 1) return { Weekly: "每週", Monthly: "每月", Yearly: "每年" }[frequency];
  return frequency === "Weekly" ? `每 ${interval} 週` : frequency === "Monthly" ? `每 ${interval} 個月` : `每 ${interval} 年`;
}
export function scheduleLabel(r: Recurring): string {
  const rule = r.frequency === "Weekly" ? weekdayNames[weekdays.indexOf(r.dayOfWeek as typeof weekdays[number])] : r.frequency === "Yearly" ? `${r.monthOfYear} 月 ${r.dayOfMonth} 日` : `${r.dayOfMonth} 日`;
  return `${frequencyLabel(r.frequency, r.interval)} · ${rule}`;
}
