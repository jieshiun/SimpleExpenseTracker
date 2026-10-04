import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import BackupSettings from "./BackupSettings";

beforeEach(() => {
  HTMLDialogElement.prototype.showModal = function () { this.setAttribute("open", ""); };
  HTMLDialogElement.prototype.close = vi.fn();
});
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });
const details = { token: "preview-token", transactions: 12, deletedTransactions: 3, categories: 16, accounts: 3, members: ["家人一", "家人二"], firstDate: "2026/01/01", lastDate: "2026/10/04", expiresAt: "2026-10-04T12:00:00Z" };
const response = (data: unknown) => ({ ok: true, status: 200, json: async () => data });
it("previews a complete backup and requires explicit confirmation without writing first", async () => {
  const fetchMock = vi.fn().mockResolvedValueOnce(response([])).mockResolvedValueOnce(response(details));
  vi.stubGlobal("fetch", fetchMock);
  render(<BackupSettings />);
  await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1));
  fireEvent.change(screen.getByLabelText("匯入備份檔案"), { target: { files: [new File([new Uint8Array(200)], "backup.db")] } });
  const dialog = await screen.findByRole("dialog");
  expect(within(dialog).getByText(/交易 12 筆/)).toBeInTheDocument();
  expect(within(dialog).getByText(/家人一、家人二/)).toBeInTheDocument();
  const restore = within(dialog).getByRole("button", { name: "確認取代並還原" });
  expect(restore).toBeDisabled();
  fireEvent.change(within(dialog).getByLabelText("還原確認文字"), { target: { value: "還原" } });
  expect(restore).toBeEnabled();
  expect(fetchMock.mock.calls[1][1].body).toBeInstanceOf(FormData);
  expect(fetchMock.mock.calls[1][1].headers.has("Content-Type")).toBe(false);
  fireEvent.click(within(dialog).getByRole("button", { name: "取消" }));
  expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  expect(fetchMock).toHaveBeenCalledTimes(2);
});
it("rejects oversized files locally and reports backend validation failures", async () => {
  const fetchMock = vi.fn().mockResolvedValueOnce(response([])).mockResolvedValueOnce({ ok: false, status: 400, json: async () => ({ title: "備份資料庫結構或版本不相容。" }) });
  vi.stubGlobal("fetch", fetchMock);
  render(<BackupSettings />);
  await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1));
  const large = new File(["x"], "large.db"); Object.defineProperty(large, "size", { value: 101 * 1024 * 1024 });
  fireEvent.change(screen.getByLabelText("匯入備份檔案"), { target: { files: [large] } });
  expect(screen.getByRole("alert")).toHaveTextContent("100 MB");
  expect(fetchMock).toHaveBeenCalledTimes(1);
  fireEvent.change(screen.getByLabelText("匯入備份檔案"), { target: { files: [new File([new Uint8Array(200)], "backup.db")] } });
  await waitFor(() => expect(screen.getByRole("alert")).toHaveTextContent("版本不相容"));
  expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
});
it("keeps confirmation and preview after an ambiguous restore failure", async () => {
  const fetchMock = vi.fn().mockResolvedValueOnce(response([])).mockResolvedValueOnce(response(details)).mockRejectedValueOnce(new TypeError("network interrupted"));
  vi.stubGlobal("fetch", fetchMock); render(<BackupSettings />);
  await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1));
  fireEvent.change(screen.getByLabelText("匯入備份檔案"), { target: { files: [new File([new Uint8Array(200)], "backup.db")] } });
  const dialog = await screen.findByRole("dialog");
  fireEvent.change(within(dialog).getByLabelText("還原確認文字"), { target: { value: "還原" } });
  fireEvent.click(within(dialog).getByRole("button", { name: "確認取代並還原" }));
  await waitFor(() => expect(within(dialog).getByRole("alert")).toHaveTextContent("無法連線"));
  expect(within(dialog).getByLabelText("還原確認文字")).toHaveValue("還原");
  expect(JSON.parse(fetchMock.mock.calls[2][1].body)).toEqual({ token: details.token, confirmation: "還原" });
});

it("shows that a legacy backup was upgraded only in a staging copy", async () => {
  const fetchMock = vi.fn().mockResolvedValueOnce(response([])).mockResolvedValueOnce(response({ ...details, wasUpgraded: true, sourceMigration: "20261004050755_FamilyLedger" }));
  vi.stubGlobal("fetch", fetchMock); render(<BackupSettings />);
  await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1));
  fireEvent.change(screen.getByLabelText("匯入備份檔案"), { target: { files: [new File([new Uint8Array(200)], "legacy.db")] } });
  const dialog = await screen.findByRole("dialog");
  expect(within(dialog).getByText(/已在暫存副本升級/)).toHaveTextContent("原始備份檔與目前帳本尚未變更");
  expect(within(dialog).getByRole("button", { name: "確認取代並還原" })).toBeDisabled();
  expect(fetchMock).toHaveBeenCalledTimes(2);
});
