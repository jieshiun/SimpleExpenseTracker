import {
  fireEvent,
  render,
  screen,
  waitFor,
  cleanup,
} from "@testing-library/react";
import { beforeEach, afterEach, expect, it, vi } from "vitest";
import TransactionForm from "./TransactionForm";
const categories = [
  {
    id: 7,
    name: "餐飲",
    icon: "🍜",
    type: "Expense" as const,
    sortOrder: 0,
    isActive: true,
  },
  {
    id: 8,
    name: "薪資",
    icon: "💰",
    type: "Income" as const,
    sortOrder: 0,
    isActive: true,
  },
];
const accounts = [
  {
    id: 9,
    name: "現金",
    type: "Cash" as const,
    initialBalance: 0,
    isActive: true,
  },
];
beforeEach(() => {
  HTMLDialogElement.prototype.showModal = function () {
    this.setAttribute("open", "");
  };
  HTMLDialogElement.prototype.close = vi.fn();
});
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});
it("switches category grids with transaction type", () => {
  render(
    <TransactionForm
      members={[{ id: 1, name: "成員一", isActive: true }]}
      defaultActor={1}
      categories={categories}
      accounts={accounts}
      onClose={vi.fn()}
      onSaved={vi.fn()}
    />,
  );
  expect(screen.queryByText("薪資")).not.toBeInTheDocument();
  fireEvent.click(screen.getByRole("button", { name: "收入" }));
  expect(screen.getByText("薪資")).toBeInTheDocument();
  expect(screen.queryByText("餐飲")).not.toBeInTheDocument();
});
it("rejects zero then submits one valid transaction without confirmation", async () => {
  const fetchMock = vi.fn().mockResolvedValue({
    ok: true,
    status: 201,
    json: async () => ({ id: 1 }),
  });
  vi.stubGlobal("fetch", fetchMock);
  const saved = vi.fn();
  render(
    <TransactionForm
      members={[{ id: 1, name: "成員一", isActive: true }]}
      defaultActor={1}
      categories={categories}
      accounts={accounts}
      onClose={vi.fn()}
      onSaved={saved}
    />,
  );
  fireEvent.change(screen.getByLabelText(/金額/), { target: { value: "0" } });
  fireEvent.click(screen.getByText("儲存交易"));
  expect(screen.getByRole("alert")).toHaveTextContent("大於零");
  expect(fetchMock).not.toHaveBeenCalled();
  fireEvent.change(screen.getByLabelText(/金額/), { target: { value: "350" } });
  fireEvent.click(screen.getByText("儲存交易"));
  await waitFor(() => expect(saved).toHaveBeenCalledWith("已新增一筆交易"));
  expect(JSON.parse(fetchMock.mock.calls[0][1].body)).toMatchObject({
    amount: 350,
    categoryId: 7,
    accountId: 9,
    type: "Expense",
  });
});

it("requires an active actor before writing", async () => {
  const fetchMock = vi.fn();
  vi.stubGlobal("fetch", fetchMock);
  render(
    <TransactionForm
      members={[{ id: 1, name: "停用成員", isActive: false }]}
      defaultActor={1}
      categories={categories}
      accounts={accounts}
      onClose={vi.fn()}
      onSaved={vi.fn()}
    />,
  );
  fireEvent.change(screen.getByLabelText(/金額/), { target: { value: "25" } });
  fireEvent.click(screen.getByText("儲存交易"));
  expect(fetchMock).not.toHaveBeenCalled();
});
it("retries an ambiguous network failure with the same creation key", async () => {
  const fetchMock = vi
    .fn()
    .mockRejectedValueOnce(new TypeError("network"))
    .mockResolvedValue({
      ok: true,
      status: 201,
      json: async () => ({ id: 1 }),
    });
  vi.stubGlobal("fetch", fetchMock);
  const saved = vi.fn();
  render(
    <TransactionForm
      members={[{ id: 1, name: "成員一", isActive: true }]}
      defaultActor={1}
      categories={categories}
      accounts={accounts}
      onClose={vi.fn()}
      onSaved={saved}
    />,
  );
  fireEvent.change(screen.getByLabelText(/金額/), { target: { value: "25" } });
  fireEvent.change(screen.getByLabelText("收支歸屬"), {
    target: { value: "Shared" },
  });
  fireEvent.click(screen.getByText("儲存交易"));
  await screen.findByRole("alert");
  fireEvent.click(screen.getByText("儲存交易"));
  await waitFor(() => expect(saved).toHaveBeenCalled());
  expect(fetchMock.mock.calls[0][1].body).toBe(fetchMock.mock.calls[1][1].body);
  expect(JSON.parse(fetchMock.mock.calls[1][1].body)).toMatchObject({
    ownership: "Shared",
    ownerMemberId: null,
    operatorId: 1,
  });
});
it("keeps edit attribution and version while offering explicit reload on conflict", async () => {
  const transaction = {
    id: 5,
    type: "Expense" as const,
    amount: 30,
    categoryId: 7,
    accountId: 9,
    transactionDate: "2026-10-04",
    note: "",
    ownership: "Shared" as const,
    ownerMemberId: null,
    ownerMemberName: null,
    categoryName: "餐飲",
    categoryIcon: "🍜",
    accountName: "現金",
    createdByName: "成員一",
    updatedByName: "成員一",
    deletedByName: null,
    deletedAt: null,
    isDeleted: false,
    version: 3,
    createdAt: "2026-10-04T00:00:00Z",
    updatedAt: "2026-10-04T00:00:00Z",
  };
  const fetchMock = vi
    .fn()
    .mockResolvedValueOnce({
      ok: false,
      status: 409,
      json: async () => ({ title: "已被更新" }),
    })
    .mockResolvedValueOnce({
      ok: true,
      status: 200,
      json: async () => ({ ...transaction, version: 4 }),
    });
  vi.stubGlobal("fetch", fetchMock);
  const reload = vi.fn();
  render(
    <TransactionForm
      transaction={transaction}
      members={[{ id: 2, name: "成員二", isActive: true }]}
      defaultActor={2}
      categories={categories}
      accounts={accounts}
      onClose={vi.fn()}
      onSaved={vi.fn()}
      onReload={reload}
    />,
  );
  fireEvent.click(screen.getByText("儲存修改"));
  await screen.findByRole("alert");
  expect(JSON.parse(fetchMock.mock.calls[0][1].body)).toMatchObject({
    ownership: "Shared",
    operatorId: 2,
    version: 3,
  });
  fireEvent.click(screen.getByText("捨棄本次修改，載入最新內容"));
  await waitFor(() =>
    expect(reload).toHaveBeenCalledWith(
      expect.objectContaining({ version: 4 }),
    ),
  );
});
