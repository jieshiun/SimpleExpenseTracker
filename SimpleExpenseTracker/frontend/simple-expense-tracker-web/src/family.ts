import type { Transaction } from "./api";

const actorKey = "expense-family-actor";
export function rememberedActor(): number {
  try {
    return Number(localStorage.getItem(actorKey)) || 0;
  } catch {
    return 0;
  }
}
export function rememberActor(id: number) {
  try {
    localStorage.setItem(actorKey, String(id));
  } catch {
    /* Device storage may be disabled. */
  }
}
export function familyQuery(value: string): string {
  if (!value) return "";
  return value.startsWith("member:")
    ? `&ownership=Personal&ownerMemberId=${Number(value.slice(7))}`
    : `&ownership=${value}`;
}
export function ownerLabel(t: Transaction): string {
  return t.ownership === "Personal"
    ? (t.ownerMemberName ?? "成員")
    : t.ownership === "Shared"
      ? "家庭共同"
      : "歸屬待確認";
}
export function requestId(): string {
  // getRandomValues also works on a household HTTP network without randomUUID.
  const bytes = crypto.getRandomValues(new Uint8Array(16));
  bytes[6] = (bytes[6] & 15) | 64;
  bytes[8] = (bytes[8] & 63) | 128;
  const hex = Array.from(bytes, (b) => b.toString(16).padStart(2, "0")).join(
    "",
  );
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}
