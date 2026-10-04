import { useState, type FormEvent } from "react";
import { save, type Member } from "./api";
import Modal from "./Modal";

export default function FamilySettings({
  members,
  onChanged,
}: {
  members: Member[];
  onChanged: (message: string) => void;
}) {
  const [editing, setEditing] = useState<Member | null>(null);
  const [name, setName] = useState("");
  const [active, setActive] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  async function submit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError("");
    try {
      await save(
        "/members",
        { name: name.trim(), isActive: active },
        editing?.id || undefined,
      );
      setEditing(null);
      onChanged("已更新家庭成員");
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  function open(member: Member) {
    setEditing(member);
    setName(member.name);
    setActive(member.isActive);
    setError("");
  }
  return (
    <section className="card family-settings">
      <div className="section-heading">
        <h2>家庭成員</h2>
        <button onClick={() => open({ id: 0, name: "", isActive: true })}>
          新增成員
        </button>
      </div>
      <p className="muted">
        所有收支彼此公開。名稱只用來標記歸屬與操作人，沒有登入驗證。
      </p>
      <div className="family-members">
        {members.map((m) => (
          <button key={m.id} onClick={() => open(m)}>
            {m.name}
            {!m.isActive && "（已停用）"} · 編輯
          </button>
        ))}
      </div>
      {editing && (
        <Modal
          title="編輯家庭成員"
          onClose={() => setEditing(null)}
          busy={busy}
        >
          <form className="transaction-form" onSubmit={submit}>
            <fieldset disabled={busy}>
              <label>
                名稱
                <input
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  required
                  maxLength={50}
                />
              </label>
              <label>
                狀態
                <select
                  value={String(active)}
                  onChange={(e) => setActive(e.target.value === "true")}
                >
                  <option value="true">啟用</option>
                  <option value="false">停用</option>
                </select>
              </label>
              <p className="muted">
                停用後保留歷史帳目及操作紀錄，之後可重新啟用。
              </p>
              {error && (
                <p role="alert" className="error">
                  {error}
                </p>
              )}
              <button className="primary wide" disabled={!name.trim()}>
                {busy ? "儲存中…" : "儲存成員"}
              </button>
            </fieldset>
          </form>
        </Modal>
      )}
    </section>
  );
}
