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
  const fetchMock = vi
    .fn()
    .mockResolvedValue({
      ok: true,
      status: 201,
      json: async () => ({ id: 1 }),
    });
  vi.stubGlobal("fetch", fetchMock);
  const saved = vi.fn();
  render(
    <TransactionForm
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
