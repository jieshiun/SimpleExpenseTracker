export type TransactionType = 'Expense' | 'Income';
export type AccountType = 'Cash' | 'Bank' | 'CreditCard' | 'EWallet' | 'Other';
export interface Category { id: number; name: string; type: TransactionType; icon: string; sortOrder: number; isActive: boolean }
export interface Account { id: number; name: string; type: AccountType; initialBalance: number; isActive: boolean }
export interface TransactionInput { type: TransactionType; amount: number; categoryId: number; accountId: number; transactionDate: string; note: string | null }
export interface Transaction extends TransactionInput { id: number; categoryName: string; categoryIcon: string; accountName: string; createdAt: string; updatedAt: string }
export interface Page<T> { items: T[]; total: number; page: number; pageSize: number }
export async function api<T>(path: string, options: RequestInit = {}): Promise<T> {
  let response: Response;
  try { response = await fetch(`/api${path}`, { ...options, headers: { 'Content-Type': 'application/json', ...options.headers } }); }
  catch (error) { if (error instanceof DOMException && error.name === 'AbortError') throw error; throw new Error('無法連線，請確認網路與記帳服務已啟動。'); }
  if (!response.ok) {
    const problem: unknown = await response.json().catch(() => null);
    const title = problem && typeof problem === 'object' && 'title' in problem && typeof problem.title === 'string' ? problem.title : '操作未完成，請稍後再試。';
    throw new Error(title);
  }
  return response.status === 204 ? undefined as T : response.json() as Promise<T>;
}
export const save = <T>(path: string, value: unknown, id?: number) => api<T>(`${path}${id ? `/${id}` : ''}`, { method: id ? 'PUT' : 'POST', body: JSON.stringify(value) });
