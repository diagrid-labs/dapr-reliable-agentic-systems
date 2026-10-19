export const $ = (id) => document.getElementById(id);

// the engine sets animate to false on every run reset and to true after the first successful render of a run,
// so whatever is already there when a run is first shown appears instantly
export const runState = { animate: false };

// reveal timing, exported so demos that time something to a reveal use the same numbers:
// REVEAL_STEP_MS per character (the default `ms`), never longer than REVEAL_MAX_MS in total, redrawn every REVEAL_TICK_MS
export const REVEAL_MAX_MS = 2000;
export const REVEAL_STEP_MS = 20;
export const REVEAL_TICK_MS = 30;
const GLYPHS = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789#%&*+<>/=";
const pendingReveals = new Map();

// a function, not a constant: it reads the user's current prefers-reduced-motion setting on every call
export const reducedMotion = () => window.matchMedia?.("(prefers-reduced-motion: reduce)").matches ?? false;
const glyph = () => GLYPHS[Math.floor(Math.random() * GLYPHS.length)];

// Writes text into container, animated when a run is being watched. Screen readers get the final text once
// (a visually hidden span); the moving copy is aria-hidden. Returns a function that ends the reveal at once.
export function reveal(container, text, { mode = "type", ms = REVEAL_STEP_MS } = {}) {
  if (!container) return () => {};
  pendingReveals.get(container)?.();
  const final = String(text ?? "");
  if (!runState.animate || reducedMotion() || !final || !container.isConnected) {
    // plain path: one bare text node (none for empty text), written only when it differs
    const bare = container.childNodes.length === 1 && container.firstChild.nodeType === Node.TEXT_NODE;
    if (final ? !bare || container.textContent !== final : container.firstChild) container.textContent = final;
    return () => {};
  }

  const chars = Array.from(final);
  const visual = document.createElement("span");
  visual.className = "reveal";
  visual.setAttribute("aria-hidden", "true");
  const spoken = document.createElement("span");
  spoken.className = "sr-only";
  spoken.textContent = final;
  container.replaceChildren(visual, spoken);

  // ms per character, but never longer than REVEAL_MAX_MS in total
  const step = Number.isFinite(ms) && ms > 0 ? ms : REVEAL_STEP_MS;
  const duration = Math.min(chars.length * step, REVEAL_MAX_MS);
  const startedAt = performance.now();
  let timer;

  const finish = () => {
    clearInterval(timer);
    if (pendingReveals.get(container) === finish) pendingReveals.delete(container);
    // the spoken span becomes the visible text, so the final text is never written (or announced) twice
    if (spoken.parentNode !== container) return;
    visual.remove();
    spoken.removeAttribute("class");
  };

  const tick = () => {
    // replaced by someone else, or the container left the page: stop at once
    if (spoken.parentNode !== container || !container.isConnected) return finish();
    const shown = Math.ceil((chars.length * (performance.now() - startedAt)) / duration);
    if (shown >= chars.length) return finish();
    const head = chars.slice(0, shown).join("");
    // decode keeps the full length (scrambled tail, whitespace kept) so the layout does not jump
    visual.textContent = mode === "decode"
      ? head + chars.slice(shown).map((c) => (/\s/.test(c) ? c : glyph())).join("")
      : head;
  };

  pendingReveals.set(container, finish);
  tick();
  timer = setInterval(tick, REVEAL_TICK_MS);
  return finish;
}

// completes every running reveal with its final text (used on run reset)
export function finishReveals() {
  for (const finish of [...pendingReveals.values()]) finish();
}

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
