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
