export const $ = (id) => document.getElementById(id);

// the LLM answers in markdown-flavoured text; show it as plain text
export const clean = (text) => String(text ?? "").replace(/\*\*/g, "").replace(/^#+\s*/gm, "").trim();

// pick a known level out of free text. `label` is a prefix pattern (e.g. /risk level\b.{0,24}?/i)
// that, when it matches, wins over the first level word found anywhere in the text.
// The label's own flags are ignored: the combined pattern always uses "is" (case-insensitive, dotAll).
export function levelFrom(text, levels, label) {
  const words = levels.join("|");
  const labelled = label && new RegExp(`${label.source}\\b(${words})\\b`, "is").exec(text);
  const known = new RegExp(`\\b(${words})\\b`, "i").exec(text);
  return (labelled?.[1] ?? known?.[1])?.toUpperCase() ?? null;
}

// crypto.randomUUID only exists in secure contexts (https, localhost); plain http on a LAN IP needs the fallback
export function uuid() {
  if (typeof crypto?.randomUUID === "function") return crypto.randomUUID();
  const bytes = new Uint8Array(16);
  if (typeof crypto?.getRandomValues === "function") crypto.getRandomValues(bytes);
  else for (let i = 0; i < 16; i++) bytes[i] = Math.floor(Math.random() * 256);
  bytes[6] = (bytes[6] & 0x0f) | 0x40;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  const hex = Array.from(bytes, (b) => b.toString(16).padStart(2, "0")).join("");
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}
