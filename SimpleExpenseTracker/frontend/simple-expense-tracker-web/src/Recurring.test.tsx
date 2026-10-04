import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import RecurringForm from "./RecurringForm";
import { frequencyLabel } from "./recurring";

beforeEach(() => { HTMLDialogElement.prototype.showModal = function () { this.setAttribute("open", ""); }; HTMLDialogElement.prototype.close = vi.fn(); });
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });
it("displays Chinese interval labels", () => {
  expect(frequencyLabel("Weekly", 2)).toBe("每 2 週");
  expect(frequencyLabel("Monthly", 1)).toBe("每月");
  expect(frequencyLabel("Yearly", 3)).toBe("每 3 年");
});
it("rejects zero and submits a variable weekly template with an account and shared ownership", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, status: 201, json: async () => ({ id: 1 }) });
  vi.stubGlobal("fetch", fetchMock); const saved = vi.fn();
  render(<RecurringForm categories={[{ id: 1, name: "生活", icon: "家", type: "Expense", isActive: true, sortOrder: 0 }]} accounts={[{ id: 1, name: "現金", type: "Cash", initialBalance: 0, isActive: true }]} members={[]} onClose={vi.fn()} onReload={vi.fn()} onSaved={saved} />);
  fireEvent.change(screen.getByLabelText("名稱"), { target: { value: "電費" } });
  fireEvent.change(screen.getByLabelText("金額（NT$）"), { target: { value: "0" } });
  fireEvent.click(screen.getByRole("button", { name: "儲存固定收支" }));
  expect(screen.getByRole("alert")).toHaveTextContent("大於零"); expect(fetchMock).not.toHaveBeenCalled();
  fireEvent.change(screen.getByLabelText("金額（NT$）"), { target: { value: "123.45" } });
  fireEvent.change(screen.getByLabelText("金額類型"), { target: { value: "Variable" } });
  fireEvent.change(screen.getByLabelText("週期"), { target: { value: "Weekly" } });
  fireEvent.change(screen.getByLabelText("執行星期"), { target: { value: "Monday" } });
  fireEvent.click(screen.getByRole("button", { name: "儲存固定收支" }));
  await waitFor(() => expect(saved).toHaveBeenCalled());
  expect(JSON.parse(fetchMock.mock.calls[0][1].body)).toMatchObject({ amount: 123.45, amountType: "Variable", frequency: "Weekly", dayOfWeek: "Monday", dayOfMonth: null, monthOfYear: null, accountId: 1, memberId: null });
});
