import { afterEach, expect, it, vi } from "vitest";
import { api } from "./api";
afterEach(() => vi.unstubAllGlobals());
it("explains connectivity failures without pretending writes succeeded", async () => {
  vi.stubGlobal(
    "fetch",
    vi.fn().mockRejectedValue(new TypeError("Failed to fetch")),
  );
  await expect(api("/transactions", { method: "POST" })).rejects.toThrow(
    "無法連線",
  );
});
it("displays the safe backend validation message", async () => {
  vi.stubGlobal(
    "fetch",
    vi
      .fn()
      .mockResolvedValue({
        ok: false,
        json: async () => ({ title: "分類不存在。" }),
      }),
  );
  await expect(api("/transactions")).rejects.toThrow("分類不存在。");
});
it("rejects stale pages after a ledger restore and sends the original generation on writes", async () => {
  vi.resetModules();
  const { api: isolatedApi } = await import("./api");
  const fetchMock = vi.fn()
    .mockResolvedValueOnce({ ok: true, status: 200, headers: new Headers({ "X-Ledger-Generation": "before" }), json: async () => [] })
    .mockResolvedValueOnce({ ok: true, status: 200, headers: new Headers({ "X-Ledger-Generation": "after" }), json: async () => [] })
    .mockResolvedValueOnce({ ok: false, status: 409, json: async () => ({ title: "帳本已還原" }) });
  vi.stubGlobal("fetch", fetchMock);
  await isolatedApi("/members");
  await expect(isolatedApi("/members")).rejects.toThrow("帳本已還原");
  await expect(isolatedApi("/transactions", { method: "POST" })).rejects.toThrow("帳本已還原");
  expect(fetchMock.mock.calls[2][1].headers.get("X-Ledger-Generation")).toBe("before");
});
