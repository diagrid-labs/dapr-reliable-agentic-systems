import { $, clean, runState } from "./util.js";

const STAGES = ["AnalyzingPlanet", "DeterminingStructures", "PlanningStructures", "Synthesizing", "Optimizing"];
const nodes = [...document.querySelectorAll(".node")];
const board = $("worker-board");
const scene = $("colony-scene");
const MAX_DOTS = 8;
const BLOCKS = 6;
// lanes whose structure was already seen done in this run; cleared by resetUi (engine calls it on every reset)
const finished = new Set();

const PRESETS = {
  arrakis: {
    planetId: "AR-558", planetName: "Arrakis Minor",
    gravity: 0.9, atmosphereType: "Thin", temperature: 45, radiationLevel: 0.5, hasWater: false, dayLength: 1.2,
    metals: true, rareEarths: true, resWater: false, organics: false, uranium: false, soilQuality: "Poor",
    initialPopulation: 100, targetPopulation: 5500, purpose: "Mining", yearsToComplete: 5,
  },
  glacius: {
    planetId: "HOTH-7", planetName: "Glacius Prime",
    gravity: 1.1, atmosphereType: "Breathable", temperature: -40, radiationLevel: 0.1, hasWater: true, dayLength: 0.8,
    metals: false, rareEarths: false, resWater: true, organics: true, uranium: false, soilQuality: "None",
    initialPopulation: 50, targetPopulation: 500, purpose: "Research", yearsToComplete: 3,
  },
  "agri-world": {
    planetId: "NEW-ED-3", planetName: "Eden Prime",
    gravity: 1.0, atmosphereType: "Breathable", temperature: 21, radiationLevel: 0.1, hasWater: true, dayLength: 1.1,
    metals: false, rareEarths: false, resWater: true, organics: true, uranium: false, soilQuality: "Good",
    initialPopulation: 200, targetPopulation: 8000, purpose: "Agriculture", yearsToComplete: 6,
  },
};

const CHECKBOXES = ["hasWater", "metals", "rareEarths", "resWater", "organics", "uranium"];
const PRIORITY_CLASS = { Critical: "critical", High: "high", Medium: "medium", Low: "low" };

const asArray = (value) => (Array.isArray(value) ? value : []);
const objects = (value) => asArray(value).filter((item) => item && typeof item === "object");
const priorityClass = (priority) => (Object.hasOwn(PRIORITY_CLASS, priority) ? PRIORITY_CLASS[priority] : "neutral");
// HabitatDome -> Habitat Dome
const spaced = (text) => clean(text).replace(/([a-z])([A-Z])/g, "$1 $2");
// write only when the value changed, so identical polls cause no DOM mutations in the live regions
const put = (element, property, value) => {
  if (element[property] !== value) element[property] = value;
};
const make = (tag, className, text) => {
  const element = document.createElement(tag);
  if (className) element.className = className;
  if (text !== undefined) element.textContent = text;
  return element;
};

function number(id, label) {
  const raw = $(id).value.trim();
  const value = raw === "" ? Number.NaN : Number(raw);
  if (!Number.isFinite(value)) throw new Error(`${label} must be a number`);
  return value;
}

function build() {
  const planetId = $("planetId").value.trim();
  const name = $("planetName").value.trim();
  if (!planetId || !name) throw new Error("Planet id and name are required");
  const gravity = number("gravity", "Gravity");
  const temperature = number("temperature", "Temperature");
  const radiationLevel = number("radiationLevel", "Radiation");
  const dayLength = number("dayLength", "Day length");
  const initialPopulation = number("initialPopulation", "Initial population");
  const targetPopulation = number("targetPopulation", "Target population");
  const yearsToComplete = number("yearsToComplete", "Years to complete");
  if (!Number.isInteger(initialPopulation) || !Number.isInteger(targetPopulation)) throw new Error("Populations must be whole numbers");
  if (initialPopulation < 0) throw new Error("Initial population cannot be negative");
  if (targetPopulation < initialPopulation) throw new Error("Target population must be at least the initial population");
  if (!Number.isInteger(yearsToComplete) || yearsToComplete < 1) throw new Error("Years to complete must be a whole number of at least 1");

  return {
    planet: {
      planetId,
      name,
      conditions: {
        gravity,
        atmosphereType: $("atmosphereType").value,
        temperature,
        radiationLevel,
        hasWater: $("hasWater").checked,
        dayLength,
      },
      resources: {
        metals: $("metals").checked,
        rareEarths: $("rareEarths").checked,
        water: $("resWater").checked,
        organics: $("organics").checked,
        uranium: $("uranium").checked,
        soilQuality: $("soilQuality").value,
      },
    },
    requirements: { initialPopulation, targetPopulation, purpose: $("purpose").value, yearsToComplete },
  };
}

const preset = (name) => () => {
  for (const [id, value] of Object.entries(PRESETS[name])) {
    if (CHECKBOXES.includes(id)) $(id).checked = value;
    else $(id).value = value;
  }
};

// today's day of the year mapped onto 2368; TNG stardates count 1000 per year from 2323 (41000 = 2364)
function stardate() {
  const now = new Date();
  const year = now.getUTCFullYear();
  const dayOfYear = (now - Date.UTC(year, 0, 1)) / 86400000;
  const daysInYear = (Date.UTC(year + 1, 0, 1) - Date.UTC(year, 0, 1)) / 86400000;
  return (1000 * (2368 - 2323) + (1000 * dayOfYear) / daysInYear).toFixed(1);
}

// a bar is done once a later stage is reached (or the run completed) and active while it is the current stage
function stateOf(index, current, status, running) {
  if (status === "Completed" || index < current) return "done";
  return running && index === current ? "active" : "pending";
}

function setNode(node, state) {
  put(node, "className", `node ${state}`);
  put(node.querySelector(".stage-state"), "textContent", { done: "Complete", active: "Processing", pending: "Standby" }[state]);
}

function laneElement() {
  const lane = make("li", "lane");
  const blocks = make("span", "blocks");
  blocks.setAttribute("aria-hidden", "true");
  blocks.append(...Array.from({ length: BLOCKS }, () => make("i")));
  lane.append(make("span", "stage-name"), make("span", "prio"), make("span", "qty"), make("span", "stage-bar"), make("span", "stage-state"), blocks);
  return lane;
}

// lanes are positional and reused so the sweep animation does not restart on every poll.
// fill: true = blocks fill with a stagger, false = instantly, null = keep what is there
function setLane(lane, entry, running, fill) {
  const state = entry.done === true ? "done" : running ? "active" : "pending";
  put(lane, "className", `lane ${state}`);
  put(lane.querySelector(".stage-name"), "textContent", spaced(entry.type) || "Structure");
  const prio = lane.querySelector(".prio");
  put(prio, "className", `prio ${priorityClass(entry.priority)}`);
  put(prio, "textContent", clean(entry.priority).slice(0, 10).toUpperCase() || "--");
  const quantity = Number(entry.quantity);
  put(lane.querySelector(".qty"), "textContent", Number.isFinite(quantity) ? `×${quantity}` : "");
  put(lane.querySelector(".stage-state"), "textContent", { done: "Complete", active: "Planning", pending: "Standby" }[state]);
  if (fill !== null) put(lane.querySelector(".blocks"), "className", fill ? "blocks fill" : "blocks");
}

// one orbiting dot per structure (capped), positional like the lanes; a dot moves while its structure is being planned
function dotElement(index) {
  const orbit = make("span", "orbit");
  orbit.style.setProperty("--i", index);
  orbit.style.setProperty("--d", `${40 + 16 * index}px`);
  orbit.style.setProperty("--dur", `${5 + 1.4 * index}s`);
  const dot = make("i", "dot");
  orbit.append(dot);
  return orbit;
}

function renderScene(structures, running) {
  const dots = structures.slice(0, MAX_DOTS);
  while (scene.children.length - 1 > dots.length) scene.lastChild.remove();
  while (scene.children.length - 1 < dots.length) scene.append(dotElement(scene.children.length - 1));
  put(scene, "hidden", dots.length === 0);
  if (scene.style.getPropertyValue("--n") !== String(dots.length)) scene.style.setProperty("--n", dots.length);
  dots.forEach((entry, index) => {
    put(scene.children[index + 1], "className", `orbit${entry.done === true ? " done" : running ? " go" : ""}`);
  });
}

// planet analysis and structure selection, one checkpoint per structure, then synthesis and optimization
function checkpoints(status, progress) {
  const structures = objects(progress?.structures);
  const count = structures.length;
  const total = count > 0 ? 2 + count + 2 : null;
  if (status === "Completed") return { done: 2 + count + 2, total: 2 + count + 2 };
  const planned = structures.filter((entry) => entry.done === true).length;
  const done = { AnalyzingPlanet: 0, DeterminingStructures: 1, PlanningStructures: 2 + planned, Synthesizing: 2 + count, Optimizing: 2 + count + 1 }[progress?.stage];
  return { done: done ?? 0, total };
}

// the lanes, dots and blocks are derived from progress, so only the bookkeeping needs clearing (idempotent)
function resetUi() {
  finished.clear();
}

function renderProgress(status, progress) {
  const running = status === "Running" || status === "Pending";
  const known = STAGES.indexOf(progress?.stage);
  // before the first status the run has only just been accepted
  const current = known < 0 && running ? 0 : known;
  for (const node of nodes) setNode(node, stateOf(STAGES.indexOf(node.dataset.stage), current, status, running));

  const structures = objects(progress?.structures);
  while (board.children.length > structures.length) board.lastChild.remove();
  while (board.children.length < structures.length) board.append(laneElement());
  for (const index of [...finished]) if (index >= structures.length) finished.delete(index);
  structures.forEach((entry, index) => {
    const isDone = entry.done === true;
    // only a lane that completes in a render after the first of the run fills with a stagger
    const fill = isDone ? (finished.has(index) ? null : runState.animate) : false;
    if (isDone) finished.add(index);
    else finished.delete(index);
    setLane(board.children[index], entry, running, fill);
  });
  renderScene(structures, running);

  put($("worker-placeholder"), "hidden", structures.length > 0);
  const complete = structures.filter((entry) => entry.done === true).length;
  put($("worker-count"), "textContent", structures.length > 0 ? `${complete} / ${structures.length} complete` : "");
}

function textList(items) {
  return asArray(items).map((text) => make("li", "", clean(text)));
}

function timelineRow(phase, longest) {
  const row = make("li", "phase");
  const days = Number(phase.durationDays);
  const width = longest > 0 && Number.isFinite(days) ? Math.min(100, Math.max(0, (days / longest) * 100)) : 0;
  const bar = make("span", "phase-bar");
  bar.style.setProperty("--width", `${width}%`);
  const phaseNo = Number(phase.phaseNumber);
  const name = make("span", "phase-name", `${Number.isFinite(phaseNo) ? `${phaseNo} ` : ""}${clean(phase.name) || "Phase"}`);
  const structures = asArray(phase.structures).map((type) => spaced(type)).filter(Boolean).join(", ");
  if (structures) name.append(make("small", "", structures));
  row.append(name, bar, make("span", "phase-days", Number.isFinite(days) ? `${days} d` : "--"));
  return row;
}

function structureRow(structure) {
  const details = make("details", "structure");
  const quantity = Number(structure.quantity);
  const days = Number(structure.constructionDays);
  const hours = Number(structure.workerHours);
  const summary = make("summary");
  summary.append(
    make("span", "structure-name", `${spaced(structure.structureType) || "Structure"}${Number.isFinite(quantity) ? ` ×${quantity}` : ""}`),
    make("span", "structure-days", Number.isFinite(days) ? `${days} days` : "--"),
    make("span", "structure-hours", Number.isFinite(hours) ? `${hours} h` : "--"),
  );

  const body = make("div", "structure-body");
  for (const [title, items] of [["Materials", structure.materials], ["Prerequisites", structure.prerequisites]]) {
    const entries = textList(items);
    if (entries.length === 0) continue;
    const list = make("ul");
    list.append(...entries);
    body.append(make("h4", "", title), list);
  }
  if (clean(structure.detailedSpecification)) {
    body.append(make("h4", "", "Specification"), make("p", "prose", clean(structure.detailedSpecification)));
  }
  details.append(summary, body);
  return details;
}

function materialRows(materials) {
  // only name/quantity objects make a table; an array would show its index as the name
  const entries = materials && typeof materials === "object" && !Array.isArray(materials) ? Object.entries(materials) : [];
  return entries.map(([name, quantity]) => {
    const row = make("tr");
    const amount = Number(quantity);
    row.append(make("td", "", clean(name)), make("td", "", Number.isFinite(amount) ? amount.toLocaleString("en-US") : "--"));
    return row;
  });
}

function renderResult(result) {
  const structures = objects(result.structures);
  const days = Number(result.totalConstructionDays);
  $("total-days").textContent = Number.isFinite(days) ? `${days} days` : "--";
  $("structure-count").textContent = String(structures.length);

  const phases = objects(result.timeline);
  const longest = Math.max(0, ...phases.map((phase) => Number(phase.durationDays)).filter(Number.isFinite));
  $("timeline").replaceChildren(...phases.map((phase) => timelineRow(phase, longest)));
  $("structures").replaceChildren(...structures.map(structureRow));
  $("materials").tBodies[0].replaceChildren(...materialRows(result.materialsRequired));
  $("success-factors").textContent = clean(result.successFactors);
  $("risk-assessment").textContent = clean(result.riskAssessment);
  return { alert: false };
}

export default {
  start: { path: "/api/colony/plan", build },
  statusPath: (instanceId) => `/api/colony/plan/${encodeURIComponent(instanceId)}`,
  presets: Object.fromEntries(Object.keys(PRESETS).map((name) => [name, preset(name)])),
  initialPreset: "arrakis",
  stardate,
  renderProgress,
  resetUi,
  checkpoints,
  renderResult,
  messages: {
    refused: (status) => `The ship's computer refused the request (${status})`,
    notFound: "The planning run could not be found.",
    ended: (status) => `The colony planning ended with status ${status}.`,
  },
};
