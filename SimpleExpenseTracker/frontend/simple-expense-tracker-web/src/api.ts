export type TransactionType = "Expense" | "Income";
export type AccountType = "Cash" | "Bank" | "CreditCard" | "EWallet" | "Other";
export interface Category {
  id: number;
  name: string;
  type: TransactionType;
  icon: string;
  sortOrder: number;
  isActive: boolean;
}
export interface Account {
  id: number;
  name: string;
  type: AccountType;
  initialBalance: number;
  isActive: boolean;
}
export interface Member {
  id: number;
  name: string;
  isActive: boolean;
}
export type Ownership = "Unknown" | "Personal" | "Shared";
export class ApiError extends Error {
  constructor(
    message: string,
    public status: number,
  ) {
    super(message);
  }
}
export interface TransactionInput {
  ownership: Ownership;
  ownerMemberId: number | null;
  operatorId?: number;
  version?: number;
  clientRequestId?: string;
  type: TransactionType;
  amount: number;
  categoryId: number;
  accountId: number;
  transactionDate: string;
  note: string | null;
}
export interface Transaction extends TransactionInput {
  id: number;
  categoryName: string;
  categoryIcon: string;
  accountName: string;
  ownerMemberName: string | null;
  createdByName: string | null;
  updatedByName: string | null;
  deletedByName: string | null;
  deletedAt: string | null;
  isDeleted: boolean;
  version: number;
  createdAt: string;
  updatedAt: string;
  recurringTransactionId?: number | null;
  recurringOccurrenceDate?: string | null;
}
export interface Page<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}
let ledgerGeneration = "";
export async function request(
  path: string,
  options: RequestInit = {},
  acceptRestoredLedger = false,
): Promise<Response> {
  let response: Response;
  try {
    const headers = new Headers(options.headers);
    if (!(options.body instanceof FormData)) headers.set("Content-Type", "application/json");
    if (ledgerGeneration) headers.set("X-Ledger-Generation", ledgerGeneration);
    response = await fetch(`/api${path}`, {
      ...options,
      headers,
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError")
      throw error;
    throw new ApiError("無法連線，請確認網路與記帳服務已啟動。", 0);
  }
  if (!response.ok) {
    const problem: unknown = await response.json().catch(() => null);
    const title =
      problem &&
      typeof problem === "object" &&
      "title" in problem &&
      typeof problem.title === "string"
        ? problem.title
        : "操作未完成，請稍後再試。";
    throw new ApiError(title, response.status);
  }
  const generation = response.headers?.get("X-Ledger-Generation");
  if (generation) {
    if (ledgerGeneration && ledgerGeneration !== generation && !acceptRestoredLedger)
      throw new ApiError("帳本已還原，請重新整理整個頁面再操作。", 409);
    ledgerGeneration = generation;
  }
  return response;
}
export async function api<T>(path: string, options: RequestInit = {}): Promise<T> {
  const response = await request(path, options);
  return response.status === 204
    ? (undefined as T)
    : (response.json() as Promise<T>);
}
export const save = <T>(path: string, value: unknown, id?: number) =>
  api<T>(`${path}${id ? `/${id}` : ""}`, {
    method: id ? "PUT" : "POST",
    body: JSON.stringify(value),
  });
