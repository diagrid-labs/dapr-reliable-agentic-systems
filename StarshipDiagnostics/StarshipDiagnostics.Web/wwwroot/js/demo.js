import { $, clean, levelFrom } from "./util.js";

const tiles = [...document.querySelectorAll("#scan-board .tile")];

const PRESETS = {
  "enterprise-d": {
    shipId: "NCC-1701-D", name: "USS Enterprise", class: "Galaxy", yearsInService: 15, lastMaintenance: "2424-12-01T00:00:00Z",
    telemetry: { hullStressLevel: 0.67, reactorTemperature: 1250, shieldIntegrity: 0.85, oxygenLevel: 0.98, navigationAccuracy: 0.95, weaponsPower: 0.88 },
  },
  "enterprise-original": {
    shipId: "NCC-1701", name: "USS Enterprise (Original)", class: "Constitution", yearsInService: 40, lastMaintenance: "2420-06-15T00:00:00Z",
    telemetry: { hullStressLevel: 0.89, reactorTemperature: 1450, shieldIntegrity: 0.62, oxygenLevel: 0.91, navigationAccuracy: 0.88, weaponsPower: 0.75 },
  },
  "enterprise-e": {
    shipId: "NCC-1701-E", name: "USS Enterprise-E", class: "Sovereign", yearsInService: 3, lastMaintenance: "2425-01-01T00:00:00Z",
    telemetry: { hullStressLevel: 0.15, reactorTemperature: 1100, shieldIntegrity: 0.98, oxygenLevel: 1.0, navigationAccuracy: 0.99, weaponsPower: 0.97 },
  },
};

const SLIDERS = ["hullStressLevel", "shieldIntegrity", "oxygenLevel", "navigationAccuracy", "weaponsPower"];
const STATUS_LEVELS = ["CRITICAL", "WARNING", "NOMINAL", "OK"];
const CLASS_FOR_LEVEL = { CRITICAL: "critical", WARNING: "warning", NOMINAL: "ok", OK: "ok" };

// unknown wording gets the neutral class
const statusClass = (level) => (level && Object.hasOwn(CLASS_FOR_LEVEL, level) ? CLASS_FOR_LEVEL[level] : "neutral");
// a level word negated by "not"/"no" (e.g. "NOT OK") says nothing about that level, so it is dropped and the status stays neutral
const NEGATED_LEVEL = new RegExp(`\\b(?:not|no)\\s+(?:${STATUS_LEVELS.join("|")})\\b`, "gi");
const levelOf = (status) => levelFrom(clean(status).replace(NEGATED_LEVEL, " "), STATUS_LEVELS);
const words = (text) => clean(text).replaceAll("_", " ");
const MAX_TEXT = 120;
// nested answers show as compact JSON text, clamped, never "[object Object]"
function answerText(answer) {
  const text = answer !== null && typeof answer === "object" ? clean(JSON.stringify(answer)) : words(answer);
  return text.length > MAX_TEXT ? `${text.slice(0, MAX_TEXT)}…` : text;
}
const asArray = (value) => (Array.isArray(value) ? value : []);

function percent(value) {
  const number = Number(value);
  return Number.isFinite(number) ? Math.round(Math.min(100, Math.max(0, number))) : null;
}

const percentText = (value) => (percent(value) === null ? "--" : `${percent(value)}%`);

function updateReadout(id) {
  document.querySelector(`output[for="${id}"]`).textContent = Number($(id).value).toFixed(2);
}

function build() {
  const text = (id) => $(id).value.trim();
  if (!text("shipId") || !text("name") || !text("class")) throw new Error("Ship id, name and class are required");
  const lastMaintenance = new Date(text("lastMaintenance"));
  if (Number.isNaN(lastMaintenance.getTime())) throw new Error("Last maintenance is not a valid date and time");
  const years = text("yearsInService") === "" ? Number.NaN : Number(text("yearsInService"));
  if (!Number.isInteger(years) || years < 0) throw new Error("Years in service must be a whole number");
  const temperature = text("reactorTemperature") === "" ? Number.NaN : Number(text("reactorTemperature"));
  if (!Number.isFinite(temperature)) throw new Error("Reactor temperature must be a number");

  const telemetry = { reactorTemperature: temperature };
  for (const id of SLIDERS) telemetry[id] = Number($(id).value);
  return {
    shipId: text("shipId"),
    name: text("name"),
    class: text("class"),
    yearsInService: years,
    lastMaintenance: lastMaintenance.toISOString(),
    telemetry,
  };
}

const preset = (name) => () => {
  const { telemetry, ...ship } = PRESETS[name];
  for (const [id, value] of Object.entries({ ...ship, ...telemetry })) $(id).value = value;
  for (const id of SLIDERS) updateReadout(id);
};

// TNG stardates count 1000 per year from 2323 (41000 = 2364)
function stardate() {
  const date = new Date($("lastMaintenance").value);
  if (Number.isNaN(date.getTime())) return null;
  const year = date.getUTCFullYear();
  const dayOfYear = (date - Date.UTC(year, 0, 1)) / 86400000;
  const daysInYear = (Date.UTC(year + 1, 0, 1) - Date.UTC(year, 0, 1)) / 86400000;
  return (1000 * (year - 2323) + (1000 * dayOfYear) / daysInYear).toFixed(1);
}

function setTile(tile, scan, running) {
  const state = scan ? "done" : running ? "scanning" : "standby";
  const level = scan ? levelOf(scan.status) : null;
  tile.className = `tile ${state}${scan ? ` ${statusClass(level)}` : ""}`;
  tile.dataset.state = state;
  tile.querySelector(".tile-health").textContent = scan ? percentText(scan.healthPercentage) : "--";
  tile.querySelector(".tile-state").textContent = { done: "Complete", scanning: "Scanning", standby: "Standby" }[state];
  tile.querySelector(".tile-status").textContent = scan ? (level ?? clean(scan.status).slice(0, 14).toUpperCase()) : "";
}

function voteRow(category, vote) {
  const row = document.createElement("li");
  row.className = vote ? "vote-row done" : "vote-row pending";
  const name = document.createElement("span");
  name.className = "vote-name";
  name.textContent = category;
  const state = document.createElement("span");
  state.className = "vote-state";
  state.textContent = vote ? words(vote.consensus) : "Voting";
  const agreement = document.createElement("span");
  agreement.className = "vote-agreement";
  agreement.textContent = vote ? `${percentText(Number(vote.agreementLevel) * 100)} agreement` : "";
  row.append(name, state, agreement);
  return row;
}

function renderVotePanel(progress, scans) {
  const votes = asArray(progress?.votes).filter((vote) => vote && typeof vote === "object");
  const voteFor = (category) => votes.find((vote) => clean(vote.category) === category);
  const voting = progress?.stage === "Voting";
  const criticalScans = scans.filter((scan) => levelOf(scan.status) === "CRITICAL").map((scan) => clean(scan.subsystem));

  const rows = [];
  const seen = new Set();
  for (const category of criticalScans) {
    const vote = voteFor(category);
    if (!vote && !voting) continue;
    seen.add(category);
    rows.push(voteRow(category, vote));
  }
  for (const vote of votes) {
    const category = clean(vote.category);
    if (!seen.has(category)) rows.push(voteRow(category, vote));
  }
  $("vote-rows").replaceChildren(...rows);
  $("vote-panel").hidden = rows.length === 0;
}

function renderProgress(status, progress) {
  const scans = asArray(progress?.scans).filter((scan) => scan && typeof scan === "object");
  const byActivity = new Map(scans.map((scan) => [scan.activity, scan]));
  const running = status === "Running" || status === "Pending";
  for (const tile of tiles) setTile(tile, byActivity.get(tile.dataset.activity), running);
  renderVotePanel(progress, scans);
}

function textList(items) {
  return asArray(items).map((text) => {
    const item = document.createElement("li");
    item.textContent = clean(text);
    return item;
  });
}

function subsystemRow(scan) {
  const level = levelOf(scan.status);
  const details = document.createElement("details");
  details.className = `subsystem ${statusClass(level)}`;

  const summary = document.createElement("summary");
  const name = document.createElement("span");
  name.className = "subsystem-name";
  name.textContent = clean(scan.subsystemName) || "Subsystem";
  const bar = document.createElement("span");
  bar.className = "health-bar";
  bar.style.setProperty("--health", `${percent(scan.healthPercentage) ?? 0}%`);
  const health = document.createElement("span");
  health.className = "subsystem-health";
  health.textContent = percentText(scan.healthPercentage);
  const status = document.createElement("span");
  status.className = "subsystem-status";
  status.textContent = level ?? (clean(scan.status).slice(0, 14).toUpperCase() || "--");
  summary.append(name, bar, health, status);

  const body = document.createElement("div");
  body.className = "subsystem-body";
  const lists = [["Issues", scan.issues], ["Recommendations", scan.recommendations]];
  for (const [title, items] of lists) {
    const entries = textList(items);
    if (entries.length === 0) continue;
    const heading = document.createElement("h4");
    heading.textContent = title;
    const list = document.createElement("ul");
    list.append(...entries);
    body.append(heading, list);
  }
  if (clean(scan.detailedAnalysis)) {
    const heading = document.createElement("h4");
    heading.textContent = "Detailed analysis";
    const analysis = document.createElement("p");
    analysis.className = "analysis";
    analysis.textContent = clean(scan.detailedAnalysis);
    body.append(heading, analysis);
  }
  details.append(summary, body);
  return details;
}

function voteResult(vote) {
  const block = document.createElement("div");
  block.className = "vote-result";
  const head = document.createElement("div");
  head.className = "vote-head";
  const category = document.createElement("strong");
  category.textContent = clean(vote.category);
  const consensus = document.createElement("span");
  consensus.className = "vote-consensus";
  consensus.textContent = words(vote.consensus);
  const agreement = document.createElement("span");
  agreement.className = "vote-agreement";
  agreement.textContent = `${percentText(Number(vote.agreementLevel) * 100)} agreement`;
  head.append(category, consensus, agreement);

  const voters = document.createElement("dl");
  voters.className = "voters";
  const ballots = vote.votes;
  const isPlain = ballots !== null && typeof ballots === "object" && Object.getPrototypeOf(ballots) === Object.prototype;
  const answers = isPlain ? Object.entries(ballots) : [];
  for (const [voter, answer] of answers) {
    const term = document.createElement("dt");
    term.textContent = clean(voter);
    const definition = document.createElement("dd");
    definition.textContent = answerText(answer);
    voters.append(term, definition);
  }
  block.append(head, voters);
  return block;
}

function renderResult(result) {
  const clear = result.clearForDeparture === true;
  const departure = $("departure");
  departure.textContent = clear ? "CLEAR FOR DEPARTURE" : "DEPARTURE HELD";
  departure.className = `departure ${clear ? "clear" : "held"}`;

  const level = levelOf(result.overallStatus);
  $("overall-status").textContent = level ?? (clean(result.overallStatus).slice(0, 14).toUpperCase() || "ASSESSED");

  $("subsystems").replaceChildren(...asArray(result.subsystemScans).filter((scan) => scan && typeof scan === "object").map(subsystemRow));

  const votes = asArray(result.criticalVotes).filter((vote) => vote && typeof vote === "object");
  $("votes").replaceChildren(...votes.map(voteResult));
  $("votes-section").hidden = votes.length === 0;

  const repairs = textList(result.requiredRepairs);
  if (repairs.length === 0) {
    const none = document.createElement("li");
    none.textContent = "None required";
    repairs.push(none);
  }
  $("repairs").replaceChildren(...repairs);
  const hours = Number(result.estimatedRepairHours);
  $("repair-hours").textContent = Number.isFinite(hours) ? `${hours} h` : "--";

  return { alert: !clear };
}

for (const id of SLIDERS) $(id).addEventListener("input", () => updateReadout(id));

export default {
  start: { path: "/api/ship/diagnose", build },
  statusPath: (instanceId) => `/api/ship/report/${encodeURIComponent(instanceId)}`,
  presets: Object.fromEntries(Object.keys(PRESETS).map((name) => [name, preset(name)])),
  initialPreset: "enterprise-d",
  stardate,
  renderProgress,
  renderResult,
  messages: {
    refused: (status) => `The ship's computer refused the request (${status})`,
    notFound: "The diagnostic run could not be found.",
    ended: (status) => `The diagnostics ended with status ${status}.`,
  },
};
