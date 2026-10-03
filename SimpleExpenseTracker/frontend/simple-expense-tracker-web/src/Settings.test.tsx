import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import Settings from "./Settings";
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
it("can disable a category without deleting its history", async () => {
  const request = vi
    .fn()
    .mockResolvedValue({ ok: true, status: 200, json: async () => ({}) });
  vi.stubGlobal("fetch", request);
  const changed = vi.fn();
  render(
    <Settings
      categories={[
        {
          id: 1,
          name: "餐飲",
          type: "Expense",
          icon: "🍜",
          isActive: true,
          sortOrder: 0,
        },
      ]}
      accounts={[]}
      onChanged={changed}
    />,
  );
  fireEvent.click(screen.getByRole("button", { name: /餐飲/ }));
  fireEvent.click(screen.getByLabelText("啟用分類"));
  fireEvent.click(screen.getByText("儲存設定"));
  await waitFor(() => expect(changed).toHaveBeenCalled());
  expect(request.mock.calls[0][0]).toBe("/api/categories/1");
  expect(request.mock.calls[0][1].method).toBe("PUT");
  expect(JSON.parse(request.mock.calls[0][1].body)).toMatchObject({
    isActive: false,
    name: "餐飲",
  });
});
