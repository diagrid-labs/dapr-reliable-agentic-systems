import { $, clean, reducedMotion, reveal, REVEAL_MAX_MS, runState, uuid } from "./util.js";

const PRESETS = {
  leo: { orbitalZone: "LEO", maxDebrisPieces: 5, fuelBudget: 100.0, maxMissionHours: 8, requireHumanApproval: true },
  meo: { orbitalZone: "MEO", maxDebrisPieces: 10, fuelBudget: 50.0, maxMissionHours: 4, requireHumanApproval: false },
  geo: { orbitalZone: "GEO", maxDebrisPieces: 3, fuelBudget: 150.0, maxMissionHours: 12, requireHumanApproval: true },
};

const DEFAULT_MAX_STEPS = 15;
const APPROVAL_TIMEOUT_MS = 5 * 60 * 1000; // the workflow denies after 5 minutes without an answer
const MAX_TEXT = 600;
const TYPE_MS = 15; // per character (the util.js default is REVEAL_STEP_MS = 20)
const MISSION_ID = /^[A-Za-z0-9_-]+$/;
const GENERATED_ID = /^(LEO|MEO|GEO)-[0-9a-f-]{36}$/;
const ACTION_PILLS = {
  SCAN_DEBRIS_FIELD: "p-sky",
  ANALYZE_DEBRIS: "p-sky",
  MOVE_TO_LOCATION: "p-blue",
  CHECK_FUEL: "p-blue",
  CAPTURE_DEBRIS: "p-orange",
  REQUEST_HUMAN_APPROVAL: "p-lilac",
  COMPLETE_MISSION: "p-orange",
};

const log = $("decision-log");
const approvalPanel = $("approval-panel");
let approvalSince = null;
let approvalDeadline = null;
let countdownTimer = null;
let approvalSending = false;
let approvalSent = false;
let approvalRound = 0;
// decisions already shown in this run, and the first decision of the newest batch (only that batch keeps its preview)
let shown = 0;
let batchStart = 0;

const asArray = (value) => (Array.isArray(value) ? value : []);
const isObject = (value) => value !== null && typeof value === "object" && !Array.isArray(value);
const objects = (value) => asArray(value).filter(isObject);
const make = (tag, className, text) => {
  const element = document.createElement(tag);
  if (className) element.className = className;
  if (text !== undefined) element.textContent = text;
  return element;
};
// finite numbers only; null, "" and other types give null instead of 0
const num = (value) => {
  if (typeof value === "string" && value.trim() === "") return null;
  const number = typeof value === "number" || typeof value === "string" ? Number(value) : Number.NaN;
  return Number.isFinite(number) ? number : null;
};
const hasEntries = (value) => (value !== null && typeof value === "object" ? Object.keys(value).length > 0 : Boolean(value));
const clamp = (text, max = MAX_TEXT) => (text.length > max ? `${text.slice(0, max)}…` : text);
const json = (value, max) => {
  try {
    return clamp(JSON.stringify(value) ?? "", max);
  } catch {
    return "";
  }
};
// request values are plain JSON: strings as they are, everything else as JSON
const display = (value) => clamp(typeof value === "string" ? value : json(value), 200);
const pillFor = (action) => (Object.hasOwn(ACTION_PILLS, action) ? ACTION_PILLS[action] : "p-blue");
const setText = (element, text) => {
  if (element.textContent !== text) element.textContent = text;
};
const setHidden = (element, hidden) => {
  if (element.hidden !== hidden) element.hidden = hidden;
};
const newMissionId = (zone) => `${zone}-${uuid()}`;

function build() {
  const missionId = $("missionId").value.trim();
  if (!missionId) throw new Error("Mission id is required");
  if (!MISSION_ID.test(missionId)) throw new Error("Mission id may only contain letters, digits, '-' and '_'");
  const maxDebrisPieces = num($("maxDebrisPieces").value);
  if (!Number.isInteger(maxDebrisPieces) || maxDebrisPieces < 1) throw new Error("Max debris pieces must be a whole number of at least 1");
  const fuelBudget = num($("fuelBudget").value);
  if (fuelBudget === null || fuelBudget <= 0) throw new Error("Fuel budget must be greater than 0");
  const maxMissionHours = num($("maxMissionHours").value);
  if (!Number.isInteger(maxMissionHours) || maxMissionHours < 1) throw new Error("Max mission hours must be a whole number of at least 1");
  return {
    missionId,
    orbitalZone: $("orbitalZone").value,
    maxDebrisPieces,
    fuelBudget,
    maxMissionHours,
    requireHumanApproval: $("requireHumanApproval").checked,
  };
}

const preset = (name) => () => {
  const { orbitalZone, maxDebrisPieces, fuelBudget, maxMissionHours, requireHumanApproval } = PRESETS[name];
  $("orbitalZone").value = orbitalZone;
  $("maxDebrisPieces").value = maxDebrisPieces;
  $("fuelBudget").value = fuelBudget;
  $("maxMissionHours").value = maxMissionHours;
  $("requireHumanApproval").checked = requireHumanApproval;
  $("missionId").value = newMissionId(orbitalZone);
};

// the instance id is CLEAN-<missionId>, so every run needs its own mission id
$("orbitalZone").addEventListener("change", () => {
  if (GENERATED_ID.test($("missionId").value)) $("missionId").value = newMissionId($("orbitalZone").value);
});

// today's day of the year mapped onto 2368; TNG stardates count 1000 per year from 2323 (41000 = 2364)
function stardate() {
  const now = new Date();
  const year = now.getUTCFullYear();
  const dayOfYear = (now - Date.UTC(year, 0, 1)) / 86400000;
  const daysInYear = (Date.UTC(year + 1, 0, 1) - Date.UTC(year, 0, 1)) / 86400000;
  return (1000 * (2368 - 2323) + (1000 * dayOfYear) / daysInYear).toFixed(1);
}

// log row: step number, action or tool pill, expandable detail; the summary also holds a visible preview
// that types out the reasoning of a decision that arrives while the run is watched
function entryElement() {
  const item = make("li", "entry");
  const details = make("details", "entry-detail");
  const summary = make("summary");
  summary.append(make("span", "entry-label"), make("span", "entry-preview"));
  details.append(summary, make("pre"));
  item.append(make("span", "entry-step"), make("span", "pill"), details);
  return item;
}

function setEntry(item, step, label, pillClass, summary, text) {
  setText(item.querySelector(".entry-step"), String(step).padStart(2, "0"));
  const pill = item.querySelector(".pill");
  if (pill.dataset.tone !== pillClass) {
    if (pill.dataset.tone) pill.classList.remove(pill.dataset.tone);
    pill.classList.add(pillClass);
    pill.dataset.tone = pillClass;
  }
  setText(pill, label);
  setText(item.querySelector(".entry-label"), summary);
  setText(item.querySelector("pre"), text);
}

// rows are positional and reused so an expanded reasoning stays open across polls
function renderLog(decisions) {
  while (log.children.length > decisions.length) log.lastChild.remove();
  while (log.children.length < decisions.length) log.append(entryElement());
  const arrivals = runState.animate && decisions.length > shown;
  batchStart = arrivals ? shown : Math.min(batchStart, decisions.length);
  decisions.forEach((decision, index) => {
    const item = log.children[index];
    const step = num(decision.step) ?? index + 1;
    const action = clean(decision.action) || "UNKNOWN";
    const reasoning = clean(decision.reasoning);
    setEntry(item, step, action, pillFor(action), "Reasoning", reasoning);
    const preview = item.querySelector(".entry-preview");
    const previewText = clamp(reasoning, 300);
    if (arrivals && index >= shown) {
      reveal(preview, previewText, { mode: "type", ms: TYPE_MS });
      pulseAfter(item.querySelector(".pill"), Math.min(Array.from(previewText).length * TYPE_MS, REVEAL_MAX_MS));
    } else if (index < batchStart && preview.firstChild) {
      reveal(preview, "");
    }
  });
  shown = decisions.length;
}

// the pill pulses once when the reasoning has been typed; the class goes away on animationend
function pulseAfter(pill, delayMs) {
  if (reducedMotion()) return;
  pill.style.setProperty("--pulse-delay", `${delayMs}ms`);
  pill.classList.remove("pulse-once");
  void pill.offsetWidth;
  pill.classList.add("pulse-once");
}
log.addEventListener("animationend", (event) => event.target.classList.remove("pulse-once"));

// one checkpoint per decision; the limit is a ghost target, the agent can finish earlier
function checkpoints(status, progress) {
  const limit = num(progress?.maxSteps);
  return { done: objects(progress?.decisions).length, total: Number.isInteger(limit) && limit > 0 ? limit : DEFAULT_MAX_STEPS };
}

function renderProgress(status, progress) {
  const running = status === "Running" || status === "Pending";
  const decisions = objects(progress?.decisions);
  const step = num(progress?.step);
  const limit = num(progress?.maxSteps);
  const maxSteps = Number.isInteger(limit) && limit > 0 ? limit : DEFAULT_MAX_STEPS;

  if (status === "Pending" && !progress) shown = batchStart = 0;
  setText($("step-meter"), `Step ${step ?? "--"} / ${maxSteps}`);
  const share = `${step === null ? 0 : Math.min(100, Math.max(0, (step / maxSteps) * 100))}%`;
  if ($("step-track").style.getPropertyValue("--width") !== share) $("step-track").style.setProperty("--width", share);

  renderLog(decisions);
  setHidden($("thinking-row"), !(running && progress?.thinking === true));
  setText($("thinking-name"), `Step ${step ?? decisions.length + 1}`);

  const wait = progress?.awaitingApproval;
  if (running && isObject(wait)) openApproval(wait);
  else if (!approvalPanel.hidden) closeApproval();
}

// since is an ISO UTC string; a missing offset is read as UTC
function parseSince(since) {
  if (typeof since !== "string") return null;
  const time = Date.parse(/(Z|[+-]\d\d:?\d\d)$/i.test(since) ? since : `${since}Z`);
  return Number.isNaN(time) ? null : time + APPROVAL_TIMEOUT_MS;
}

function setApprovalButtons(disabled) {
  $("approve").disabled = disabled;
  $("deny").disabled = disabled;
}

// round guards against a POST that settles after the panel closed or a new request opened
function resetApprovalState() {
  approvalRound++;
  approvalSending = false;
  approvalSent = false;
  $("approval-error").hidden = true;
  $("approval-error").textContent = "";
  $("approval-reason").value = "";
  setApprovalButtons(false);
}

function tickCountdown() {
  if (approvalDeadline === null) {
    $("approval-countdown").textContent = "--:--";
    $("approval-timeout").hidden = true;
    return;
  }
  // the client clock may be skewed: keep the display between 00:00 and 05:00
  const remaining = Math.min(APPROVAL_TIMEOUT_MS, Math.max(0, approvalDeadline - Date.now()));
  const seconds = Math.ceil(remaining / 1000);
  $("approval-countdown").textContent = `${String(Math.floor(seconds / 60)).padStart(2, "0")}:${String(seconds % 60).padStart(2, "0")}`;
  $("approval-timeout").hidden = remaining > 0;
}

function renderRequest(request) {
  const entries = isObject(request) ? Object.entries(request) : [["request", request]];
  const rows = entries.flatMap(([key, value]) => [make("dt", "", clamp(String(key), 60)), make("dd", "", display(value))]);
  $("approval-request").replaceChildren(...rows);
}

function openApproval(wait) {
  const since = typeof wait.since === "string" ? wait.since : null;
  if (approvalPanel.hidden || since !== approvalSince) {
    resetApprovalState();
    approvalSince = since;
    approvalDeadline = parseSince(since);
    approvalPanel.hidden = false;
    // announced once per approval; a fresh element makes a repeated text speak again
    $("approval-status").replaceChildren(make("span", "", "Human approval required. The agent is waiting for your answer."));
    $("approval-title").focus();
  }
  renderRequest(wait.request);
  countdownTimer ??= setInterval(tickCountdown, 1000);
  tickCountdown();
}

function closeApproval() {
  clearInterval(countdownTimer);
  countdownTimer = null;
  approvalSince = null;
  approvalDeadline = null;
  resetApprovalState();
  approvalPanel.hidden = true;
  $("approval-status").replaceChildren();
  $("approval-request").replaceChildren();
}

async function sendApproval(approved) {
  if (approvalSending || approvalSent) return;
  approvalSending = true;
  setApprovalButtons(true);
  $("approval-error").hidden = true;
  const round = approvalRound;
  const instanceId = $("instance-id").textContent;
  try {
    const response = await fetch(`/api/mission/${encodeURIComponent(instanceId)}/approval`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ approved, reason: $("approval-reason").value.trim() }),
    });
    if (!response.ok) throw new Error(`Mission control refused the answer (${response.status})`);
    if (round === approvalRound) approvalSent = true; // stays disabled until the panel closes
  } catch (error) {
    if (round !== approvalRound) return;
    $("approval-error").textContent = error instanceof TypeError ? "Could not reach mission control" : error.message;
    $("approval-error").hidden = false;
    setApprovalButtons(false);
  } finally {
    if (round === approvalRound) approvalSending = false;
  }
}

function resetUi() {
  shown = batchStart = 0;
  closeApproval();
  $("missionId").value = newMissionId($("orbitalZone").value);
}

function counter(id, value, unit = "") {
  const number = num(value);
  $(id).textContent = number === null ? "--" : `${number}${unit}`;
}

function renderResult(result) {
  const success = result.success === true;
  $("mission-outcome").textContent = success ? "Mission complete" : "Mission aborted";
  counter("debris-captured", result.debrisCaptured);
  const fuel = num(result.fuelUsed);
  counter("fuel-used", fuel === null ? null : Number(fuel.toFixed(1)), " kg");
  counter("total-steps", result.totalSteps);
  $("summary").textContent = clean(result.summary);
  const lessons = asArray(result.lessonsLearned).map((text) => clean(text)).filter(Boolean);
  $("lessons").replaceChildren(...(lessons.length ? lessons : ["None recorded"]).map((text) => make("li", "", text)));

  const decisions = objects(result.decisions).map((decision, index) => {
    const item = entryElement();
    const step = (num(decision.stepNumber) ?? index) + 1; // stepNumber is 0-based here
    const action = clean(decision.chosenAction) || "UNKNOWN";
    const text = [
      clean(decision.reasoning),
      decision.expectedOutcome ? `Expected outcome: ${clean(decision.expectedOutcome)}` : "",
      hasEntries(decision.actionParameters) ? `Parameters: ${json(decision.actionParameters)}` : "",
    ].filter(Boolean).join("\n");
    setEntry(item, step, action, pillFor(action), "Reasoning", text);
    return item;
  });
  $("result-decisions").replaceChildren(...decisions);

  const tools = objects(result.toolCalls).map((call, index) => {
    const item = entryElement();
    const ok = call.success === true;
    const text = [
      hasEntries(call.parameters) ? `Parameters: ${json(call.parameters)}` : "",
      `Result: ${typeof call.result === "string" ? clamp(clean(call.result)) : json(call.result)}`,
      call.errorMessage ? `Error: ${clamp(clean(call.errorMessage))}` : "",
    ].filter(Boolean).join("\n");
    setEntry(item, index + 1, clean(call.toolName) || "UNKNOWN", ok ? "p-sky" : "p-salmon", ok ? "Succeeded" : "Failed", text);
    item.classList.toggle("failed", !ok);
    return item;
  });
  $("result-tools").replaceChildren(...tools);

  return { alert: !success, note: success ? undefined : "Mission aborted — see summary" };
}

$("approve").addEventListener("click", () => sendApproval(true));
$("deny").addEventListener("click", () => sendApproval(false));

export default {
  start: { path: "/api/mission/start", build },
  statusPath: (instanceId) => `/api/mission/status/${encodeURIComponent(instanceId)}`,
  presets: Object.fromEntries(Object.keys(PRESETS).map((name) => [name, preset(name)])),
  initialPreset: "leo",
  stardate,
  renderProgress,
  checkpoints,
  renderResult,
  resetUi,
  messages: {
    refused: (status) => `Mission control refused the request (${status})`,
    notFound: "The mission could not be found.",
    ended: (status) => `The mission ended with status ${status}.`,
  },
};
