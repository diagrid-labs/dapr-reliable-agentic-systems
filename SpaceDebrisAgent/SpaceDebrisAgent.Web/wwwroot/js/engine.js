// Generic run lifecycle: start, poll, link state, views, focus. Everything demo-specific lives in demo.js.
//
// Required ids: demo-form, begin, form-error, link-lost, link-indicator, run-status, instance-id,
//   elapsed, run-note, restart, form-panel, progress-panel, findings-panel (each panel has an h2),
//   plus buttons with data-preset.
// Optional ids (skipped when missing): stardate, alert-banner, new-analysis, findings-note.
// Optional demo hooks: stardate(), onPolled(state), resetUi(),
//   messages: { refused(status), notFound, ended(status) } (each a string or a function).
import demo from "./demo.js";
import { $ } from "./util.js";

const POLL_MS = 2000;
const RETRY_MS = 5000;
const MAX_NOT_FOUND = 15;
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
const show = (id, text) => {
  const element = $(id);
  if (!element) return;
  element.textContent = text ?? "";
  element.hidden = !text;
};
const VIEW_HEADING = { idle: "form-panel", running: "progress-panel", failed: "progress-panel", completed: "findings-panel" };

let runToken = 0;
let pollTimer;
let clockTimer;
let startedAt;
let submitting = false;
let lastProgress = null;

function setView(view) {
  document.body.dataset.view = view;
  // the focused button was just hidden; keep keyboard and screen reader users oriented
  $(VIEW_HEADING[view]).querySelector("h2").focus();
}

function updateStardate() {
  if (demo.stardate && $("stardate")) $("stardate").textContent = demo.stardate() ?? "--";
}

function applyPreset(name) {
  demo.presets[name]();
  $("form-error").hidden = true;
  updateStardate();
}

function showAlert(on) {
  document.body.classList.toggle("red-alert", on);
  if ($("alert-banner")) $("alert-banner").hidden = !on;
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
  lastProgress = null;
  showAlert(false);
  $("link-lost").hidden = true;
  $("run-note").hidden = true;
  show("findings-note", "");
  setLink(true);
  demo.renderProgress("Pending", null);
  demo.resetUi?.();
  $("run-status").textContent = "--";
}

async function startRun() {
  if (submitting) return;
  $("form-error").hidden = true;
  let payload;
  try {
    payload = demo.start.build();
  } catch (error) {
    $("form-error").textContent = error.message;
    $("form-error").hidden = false;
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
    $("instance-id").textContent = instanceId;
    $("run-status").textContent = "Pending";
    startedAt = Date.now();
    $("elapsed").textContent = "00:00";
    clockTimer = setInterval(updateElapsed, 1000);
    setView("running");
    poll(instanceId, token, 0);
  } catch (error) {
    $("form-error").textContent = error.message;
    $("form-error").hidden = false;
  } finally {
    submitting = false;
    $("begin").disabled = false;
  }
}

function updateElapsed() {
  const seconds = Math.floor((Date.now() - startedAt) / 1000);
  $("elapsed").textContent = `${String(Math.floor(seconds / 60)).padStart(2, "0")}:${String(seconds % 60).padStart(2, "0")}`;
}

function setLink(ok) {
  $("link-lost").hidden = ok;
  $("link-indicator").classList.toggle("retry", !ok);
}

function schedule(instanceId, token, delay, notFound) {
  pollTimer = setTimeout(() => poll(instanceId, token, notFound), delay);
}

async function poll(instanceId, token, notFound) {
  let response;
  let state;
  try {
    response = await fetch(demo.statusPath(instanceId));
    if (response.ok) state = await response.json();
  } catch {
    if (token !== runToken) return;
    setLink(false);
    schedule(instanceId, token, RETRY_MS, notFound);
    return;
  }
  if (token !== runToken) return;

  if (response.status === 404) {
    // the workflow may not be visible yet right after scheduling
    if (notFound + 1 >= MAX_NOT_FOUND) {
      // let the demo drop open panels and animations before the failed view
      try {
        demo.renderProgress("Failed", lastProgress);
      } catch (error) {
        console.error(error);
      }
      return finish("Failed", null, message("notFound"));
    }
    schedule(instanceId, token, POLL_MS, notFound + 1);
    return;
  }
  if (!response.ok) {
    setLink(false);
    schedule(instanceId, token, RETRY_MS, notFound);
    return;
  }

  setLink(true);
  lastProgress = state.progress ?? lastProgress;
  demo.renderProgress(state.status, lastProgress);
  demo.onPolled?.(state);
  $("run-status").textContent = state.status;

  if (TERMINAL.includes(state.status)) {
    finish(state.status, state.result);
  } else {
    schedule(instanceId, token, POLL_MS, 0);
  }
}

function finish(status, result, note) {
  clearInterval(clockTimer);
  $("run-status").textContent = status;
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
  $("run-note").textContent = note ?? message("ended", status);
  $("run-note").hidden = false;
  setView("failed");
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
