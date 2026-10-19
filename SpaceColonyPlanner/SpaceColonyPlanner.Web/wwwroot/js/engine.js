// Generic run lifecycle: start, poll, link state, views, focus. Everything demo-specific lives in demo.js.
// Every write a poll can repeat is compare-then-write, so a poll that yields the same state mutates nothing
// (aria-live regions such as #run-status and #durable-caption, and the #link-lost banner, are not announced again).
// Intentional exceptions: the elapsed clock and the "down" clock, #begin.disabled, the strip rebuild and reset,
// focus, and the form error, which is cleared before it is shown so an identical message is announced again.
// Demo hooks called on every poll (renderProgress, onPolled) should be compare-then-write too; a hook that throws
// is logged with console.error and the poll carries on.
//
// Required ids: demo-form, begin, form-error, link-lost, link-indicator, run-status, instance-id,
//   elapsed, run-note, restart, form-panel, progress-panel, findings-panel (each panel has an h2),
//   plus buttons with data-preset.
// Optional ids (skipped when missing): stardate, alert-banner, new-analysis, findings-note,
//   durable (checkpoint strip, aria-hidden), durable-caption (role="status", outside #durable),
//   tokens-total, tokens-detail (token readout in the progress panel, aria-hidden), findings-tokens (final total with prompt/completion detail, plain text).
//   The readout comes from progress.usage { promptTokens, completionTokens, totalTokens }; missing or null means 0 ("--").
// Optional demo hooks: stardate(), onPolled(state), resetUi(),
//   messages: { refused(status), notFound, ended(status) } (each a string or a function),
//   checkpoints(status, progress): { done: number, total: number | null } (the strip and its captions need it).
import demo from "./demo.js";
import { $, runState, finishReveals, formatTokens, countTo, clearCount } from "./util.js";

const POLL_MS = 2000;
const RETRY_MS = 5000;
const POLL_TIMEOUT_MS = 5000;
const MAX_NOT_FOUND = 15;
const FLASH_MS = 1500;
const RESTORED_MS = 6000;
const MAX_BEADS = 30;
const TERMINAL = ["Completed", "Failed", "Terminated"];
const MESSAGES = {
  refused: (status) => `The request was refused (${status})`,
  notFound: "The instance could not be found.",
  ended: (status) => `The run ended with status ${status}.`,
};
const message = (key, ...args) => {
  const text = demo.messages?.[key] ?? MESSAGES[key];
  return typeof text === "function" ? text(...args) : text;
};
// compare-then-write: an unchanged value must not touch the DOM, or aria-live regions are announced again
const setText = (element, text) => {
  const value = String(text ?? "");
  if (element.textContent !== value) element.textContent = value;
};
const setHidden = (element, hidden) => {
  if (element.hidden !== hidden) element.hidden = hidden;
};
const show = (id, text) => {
  const element = $(id);
  if (!element) return;
  setText(element, text ? text : "");
  setHidden(element, !text);
};
const ERROR_FALLBACK = "The request could not be started.";
const errorText = (error) => {
  const text = typeof error === "string" ? error : error?.message;
  return typeof text === "string" && text.trim() ? text : ERROR_FALLBACK;
};
let errorFrame;
// user-initiated only: clearing the text first makes an identical message a new alert for screen readers
const clearError = () => {
  cancelAnimationFrame(errorFrame);
  setHidden($("form-error"), true);
  $("form-error").textContent = "";
};
const showError = (text) => {
  clearError();
  setText($("form-error"), text);
  setHidden($("form-error"), false);
};
// the validation error answers a click synchronously: insert it two frames later, so the cleared state is
// rendered (and seen by the accessibility tree) first and an identical message is a real change
const showErrorNextFrame = (text) => {
  clearError();
  errorFrame = requestAnimationFrame(() => {
    errorFrame = requestAnimationFrame(() => showError(text));
  });
};
const VIEW_HEADING = { idle: "form-panel", running: "progress-panel", failed: "progress-panel", completed: "findings-panel" };

let runToken = 0;
let pollTimer;
let clockTimer;
let startedAt;
let submitting = false;
let lastProgress = null;
let pollAbort;
// checkpoint strip of the current run: prev = done count of the previous successful render (null before the first)
let durable = { prev: null, done: 0, total: null, active: true, lostAt: null };
// bead index -> timer that ends its "saved" flash, so each bead's flash runs its full FLASH_MS
const flashTimers = new Map();
let captionTimer;
let downTimer;
// highest usage seen in this run: a stale or lower poll never moves the readout down
let usage = { total: 0, prompt: 0, completion: 0 };

function setView(view) {
  if (document.body.dataset.view !== view) document.body.dataset.view = view;
  // the focused button was just hidden; keep keyboard and screen reader users oriented
  $(VIEW_HEADING[view]).querySelector("h2").focus();
}

function updateStardate() {
  if (demo.stardate && $("stardate")) setText($("stardate"), demo.stardate() ?? "--");
}

function applyPreset(name) {
  demo.presets[name]();
  clearError();
  updateStardate();
}

function showAlert(on) {
  document.body.classList.toggle("red-alert", on); // with `force` this only writes when the class changes
  if ($("alert-banner")) setHidden($("alert-banner"), !on);
}

function showResult(result) {
  const outcome = demo.renderResult(result);
  showAlert(outcome.alert);
  show("findings-note", outcome.note);
}

function resetRun() {
  runToken++;
  clearTimeout(pollTimer);
  clearInterval(clockTimer);
  pollAbort?.abort();
  cancelAnimationFrame(errorFrame);
  finishReveals();
  runState.animate = false;
  resetDurable();
  resetUsage();
  lastProgress = null;
  showAlert(false);
  setHidden($("link-lost"), true);
  setHidden($("run-note"), true);
  show("findings-note", "");
  setLink(true);
  guard(() => demo.renderProgress("Pending", null));
  guard(() => demo.resetUi?.());
  setText($("run-status"), "--");
}

async function startRun() {
  if (submitting) return;
  clearError();
  let payload;
  try {
    payload = demo.start.build();
  } catch (error) {
    showErrorNextFrame(errorText(error));
    return;
  }

  submitting = true;
  $("begin").disabled = true;
  try {
    const response = await fetch(demo.start.path, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });
    if (!response.ok) throw new Error(message("refused", response.status));
    const { instanceId } = await response.json();

    resetRun();
    const token = runToken;
    setText($("instance-id"), instanceId);
    setText($("run-status"), "Pending");
    startedAt = Date.now();
    setText($("elapsed"), "00:00");
    clockTimer = setInterval(updateElapsed, 1000);
    setView("running");
    poll(instanceId, token, 0);
  } catch (error) {
    showError(errorText(error));
  } finally {
    submitting = false;
    $("begin").disabled = false;
  }
}

function updateElapsed() {
  $("elapsed").textContent = mmss(Date.now() - startedAt);
}

function setLink(ok) {
  setHidden($("link-lost"), ok);
  $("link-indicator").classList.toggle("retry", !ok); // with `force` this only writes when the class changes
}

function schedule(instanceId, token, delay, notFound) {
  pollTimer = setTimeout(() => poll(instanceId, token, notFound), delay);
}

// a hung request (the app behind the proxy is down) counts as a lost link after POLL_TIMEOUT_MS
async function poll(instanceId, token, notFound) {
  let response;
  let state;
  const controller = new AbortController();
  pollAbort = controller;
  const timeout = setTimeout(() => controller.abort(), POLL_TIMEOUT_MS);
  try {
    response = await fetch(demo.statusPath(instanceId), { signal: controller.signal });
    if (response.ok) state = await response.json();
  } catch {
    if (token !== runToken) return;
    linkLost();
    schedule(instanceId, token, RETRY_MS, notFound);
    return;
  } finally {
    clearTimeout(timeout);
  }
  if (token !== runToken) return;

  if (response.status === 404) {
    // a 404 is an answer, so the link is back; the workflow may not be visible yet right after scheduling
    setLink(true);
    if (notFound + 1 >= MAX_NOT_FOUND) {
      // let the demo drop open panels and animations before the failed view
      guard(() => demo.renderProgress("Failed", lastProgress));
      finish("Failed", null, message("notFound"));
      runState.animate = false;
      return;
    }
    if (durable.lostAt !== null) {
      restoreDurable(false);
      setStripState("running");
      drawStrip();
    }
    schedule(instanceId, token, POLL_MS, notFound + 1);
    return;
  }
  if (!response.ok) {
    linkLost();
    schedule(instanceId, token, RETRY_MS, notFound);
    return;
  }

  setLink(true);
  lastProgress = state.progress ?? lastProgress;
  guard(() => demo.renderProgress(state.status, lastProgress));
  guard(() => demo.onPolled?.(state));
  renderDurable(state.status, lastProgress);
  guard(() => renderUsage(lastProgress, TERMINAL.includes(state.status)));
  setText($("run-status"), state.status);

  // only renders after the first successful one animate (that includes its finish); nothing animates once ended
  if (TERMINAL.includes(state.status)) {
    finish(state.status, state.result);
    runState.animate = false;
  } else {
    schedule(instanceId, token, POLL_MS, 0);
    runState.animate = true;
  }
}

// a throwing demo hook must not stop the poll loop
function guard(hook) {
  try {
    hook();
  } catch (error) {
    console.error(error);
  }
}

function finish(status, result, note) {
  clearInterval(clockTimer);
  endDurable();
  setText($("run-status"), status);
  if (status === "Completed" && result) {
    try {
      showResult(result);
      setView("completed");
      return;
    } catch (error) {
      console.error(error);
      showAlert(false);
      show("findings-note", "");
      note = "The result could not be displayed.";
    }
  }
  setText($("run-note"), note ?? message("ended", status));
  setHidden($("run-note"), false);
  setView("failed");
}

// --- durable timeline: saved checkpoints as beads, red while the link is lost ---

const count = (value) => (typeof value === "number" && Number.isFinite(value) && value >= 0 ? Math.floor(value) : null);
const mmss = (ms) => {
  const seconds = Math.floor(ms / 1000);
  return `${String(Math.floor(seconds / 60)).padStart(2, "0")}:${String(seconds % 60).padStart(2, "0")}`;
};

function readCheckpoints(status, progress) {
  try {
    const value = demo.checkpoints(status, progress);
    // a total of 0 says nothing about the run: treat it as unknown so an active run still shows its next bead
    const total = count(value?.total) || null;
    const done = count(value?.done) ?? 0;
    return { done: total === null ? done : Math.min(done, total), total };
  } catch (error) {
    console.error(error);
    return { done: 0, total: null };
  }
}

// the lost caption is not announced (the #link-lost alert already is): it sits in an aria-hidden span, so it is
// visible but outside the accessibility tree; any other caption is a plain text node, announced as a normal addition
function setCaption(text, tone) {
  const caption = $("durable-caption");
  if (!caption) return;
  const hidden = tone === "lost" && text !== "";
  const node = caption.firstChild;
  const same = caption.textContent === text && caption.childNodes.length === (text ? 1 : 0)
    && (!text || (hidden ? node.nodeType === Node.ELEMENT_NODE : node.nodeType === Node.TEXT_NODE));
  if (!same && hidden) {
    const span = document.createElement("span");
    span.setAttribute("aria-hidden", "true");
    span.textContent = text;
    caption.replaceChildren(span);
  } else if (!same) caption.textContent = text;
  if (tone) {
    if (caption.dataset.tone !== tone) caption.dataset.tone = tone;
  } else if ("tone" in caption.dataset) delete caption.dataset.tone;
}

function setStripState(state) {
  const strip = $("durable");
  if (strip && strip.dataset.state !== state) strip.dataset.state = state;
}

function clearFlashes() {
  for (const timer of flashTimers.values()) clearTimeout(timer);
  flashTimers.clear();
}

function flashBeads(from, to) {
  for (let i = from; i < Math.min(to, MAX_BEADS); i++) {
    clearTimeout(flashTimers.get(i));
    flashTimers.set(i, setTimeout(() => {
      flashTimers.delete(i);
      drawStrip();
    }, FLASH_MS));
  }
}

function resetDurable() {
  clearFlashes();
  clearTimeout(captionTimer);
  clearInterval(downTimer);
  durable = { prev: null, done: 0, total: null, active: true, lostAt: null };
  $("durable")?.replaceChildren();
  delete $("durable")?.dataset.state;
  setCaption("");
}

// beads: `done` saved, one leading "next" bead while the run is active and more can come, ghosts up to a known total.
// The head (next bead, else the last saved one) is the bead that turns red while the link is lost.
function beadClasses() {
  const { done, total, active } = durable;
  const saved = Math.min(done, MAX_BEADS);
  const hasNext = active && (total === null || done < total);
  const length = Math.min(Math.max(total ?? 0, saved + (hasNext ? 1 : 0)), MAX_BEADS);
  const head = hasNext && saved < length ? saved : saved - 1;
  return Array.from({ length }, (_, i) => {
    let kind = i < saved ? "saved" : i === saved && hasNext ? "next" : "ghost";
    if (flashTimers.has(i)) kind += " just-saved";
    return `durable-bead ${kind}${i === head ? " head" : ""}`;
  });
}

function drawStrip() {
  const strip = $("durable");
  if (!strip) return;
  let track = strip.querySelector(".durable-track");
  if (!track) {
    track = document.createElement("ol");
    track.className = "durable-track";
    const down = document.createElement("span");
    down.className = "durable-down";
    down.hidden = true;
    strip.replaceChildren(track, down);
  }
  const classes = beadClasses();
  while (track.children.length > classes.length) track.lastElementChild.remove();
  classes.forEach((name, i) => {
    let bead = track.children[i];
    if (!bead) {
      bead = document.createElement("li");
      for (const part of i ? ["durable-seg", "durable-dot"] : ["durable-dot"]) {
        const span = document.createElement("span");
        span.className = part;
        bead.append(span);
      }
      track.append(bead);
    }
    if (bead.className !== name) bead.className = name;
  });
}

function updateDown() {
  const down = $("durable")?.querySelector(".durable-down");
  if (!down) return;
  const text = durable.lostAt === null ? "" : `down ${mmss(Date.now() - durable.lostAt)}`;
  setText(down, text);
  setHidden(down, !text);
}

function renderDurable(status, progress) {
  if (!demo.checkpoints) return;
  const { done, total } = readCheckpoints(status, progress);
  const terminal = TERMINAL.includes(status);
  const wasLost = durable.lostAt !== null;
  // flash only checkpoints that appeared while watching: not on the first render of a run, not once it ended
  if (durable.prev !== null && done > durable.prev && !terminal) flashBeads(durable.prev, done);
  Object.assign(durable, { prev: done, done, total, active: !terminal });
  if (wasLost) restoreDurable(terminal);
  setStripState(terminal ? "ended" : "running");
  drawStrip();
}

// any answer from the server after an outage (also a 404): stop the down clock and, while the run goes on,
// say how much was saved
function restoreDurable(terminal) {
  durable.lostAt = null;
  clearInterval(downTimer);
  updateDown();
  if (terminal) return;
  setCaption(restoredCaption(durable.done, durable.total), "restored");
  clearTimeout(captionTimer);
  captionTimer = setTimeout(() => setCaption(""), RESTORED_MS);
}

function restoredCaption(done, total) {
  if (done === 0) return "Link restored: run continues (no checkpoint saved yet)";
  const saved = total === null ? `${done} checkpoint${done === 1 ? "" : "s"}` : `${done} of ${total} checkpoints`;
  return `Link restored: resumed with ${saved} saved`;
}

function linkLost() {
  setLink(false);
  if (!demo.checkpoints || durable.lostAt !== null) return;
  // link lost before any successful poll: draw the strip from what the demo reports for an empty run
  if (durable.prev === null) Object.assign(durable, readCheckpoints("Pending", lastProgress));
  durable.lostAt = Date.now();
  clearTimeout(captionTimer);
  setCaption("Subspace link lost: state is safe", "lost");
  setStripState("lost");
  drawStrip();
  updateDown();
  downTimer = setInterval(updateDown, 1000);
}

// the run is over: no timers, no caption, the strip stays visible but frozen
function endDurable() {
  clearFlashes();
  clearTimeout(captionTimer);
  clearInterval(downTimer);
  if (!demo.checkpoints) return;
  Object.assign(durable, { active: false, lostAt: null });
  setCaption("");
  // a run that never showed its strip (e.g. not found) keeps it empty
  if (!$("durable")?.querySelector(".durable-track")) return;
  setStripState("ended");
  drawStrip();
  updateDown();
}

// --- token readout: counts up to the last polled total, never down ---

function resetUsage() {
  usage = { total: 0, prompt: 0, completion: 0 };
  for (const id of ["tokens-total", "tokens-detail", "findings-tokens"]) {
    const element = $(id);
    if (element) clearCount(element);
  }
  if ($("tokens-detail")) setText($("tokens-detail"), "");
  show("findings-tokens", "");
  if ($("tokens-total")) setText($("tokens-total"), "--");
}

function renderUsage(progress, terminal) {
  const read = progress?.usage;
  const prompt = count(read?.promptTokens) ?? 0;
  const completion = count(read?.completionTokens) ?? 0;
  const total = count(read?.totalTokens) || prompt + completion;
  if (total > usage.total) usage = { total, prompt, completion };
  const detail = usage.total ? `prompt ${formatTokens(usage.prompt)} · completion ${formatTokens(usage.completion)}` : "";
  if ($("tokens-total")) {
    if (usage.total) countTo($("tokens-total"), usage.total, terminal ? { ms: 0 } : undefined);
    else setText($("tokens-total"), "--");
  }
  if ($("tokens-detail")) setText($("tokens-detail"), detail);
  // plain text, set once the run ended, so it is announced together with the findings
  if (terminal) show("findings-tokens", usage.total ? `${formatTokens(usage.total)} (${detail})` : "--");
}

// also exposed for manual checks from the browser console
window.renderResult = showResult;

$("demo-form").addEventListener("submit", (event) => {
  event.preventDefault();
  startRun();
});
$("demo-form").addEventListener("input", updateStardate);
for (const button of document.querySelectorAll("[data-preset]")) {
  button.addEventListener("click", () => applyPreset(button.dataset.preset));
}
for (const button of document.querySelectorAll("#new-analysis, #restart")) {
  button.addEventListener("click", () => {
    resetRun();
    setView("idle");
  });
}

applyPreset(demo.initialPreset);
