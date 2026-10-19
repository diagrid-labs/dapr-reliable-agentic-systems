import { $, clean, levelFrom, runState, uuid } from "./util.js";

const lanes = [...document.querySelectorAll("#routing-board li")];

const PRESETS = {
  "temporal-rift": {
    sensorData: "Chronal flux readings off the scale. Time dilation factor: 847.2. Causality loops detected in 3-dimensional space. Tachyon particles streaming from epicenter.",
    coordinates: "Sector 7-G, Quadrant Epsilon",
    detectedAt: "2425-03-15T10:22:00Z",
    measurements: { chronalFlux: 847.2, timeDialation: 3.4, tachyonDensity: 0.00042, causalityIndex: -2.1 },
  },
  "dark-matter": {
    sensorData: "Massive gravitational anomaly. No visible matter detected. Gravitational lensing severe. Estimated mass: 10^6 solar masses in 5 cubic light-years.",
    coordinates: "Outer rim, Nebula X-42",
    detectedAt: "2425-03-15T11:45:00Z",
    measurements: { gravitationalPull: 9.8e12, mass: 1.989e36, volume: 1.18e50, lensingFactor: 4.7 },
  },
  "alien-artifact": {
    sensorData: "Geometric structure detected. Composed of unknown alloy. Emitting low-frequency EM pulses in prime number sequences. Surface temperature: -270C despite no heat source nearby.",
    coordinates: "Derelict orbit around pulsar PSR J0737",
    detectedAt: "2425-03-15T13:10:00Z",
    measurements: { size: 47.5, temperature: -270, emFrequency: 1.42e9, materialDensity: 22.6 },
  },
  "stellar-phenomenon": {
    sensorData: "Supernova remnant detected. Massive energy output. Neutron star core visible. Expanding shock wave at 0.1c. High gamma radiation detected.",
    coordinates: "Sagittarius Arm, Sector 12",
    detectedAt: "2425-03-15T14:30:00Z",
    measurements: { energyOutput: 1.5e44, expansionVelocity: 30000000, gammaRadiation: 850, coreTemperature: 1.2e9 },
  },
  "dimensional-tear": {
    sensorData: "Spacetime fabric showing signs of rupture. Exotic particles leaking from breach. Reality distortion measured at 78%. Parallel universe signatures detected.",
    coordinates: "Deep space, Void Region ZX-9",
    detectedAt: "2425-03-15T16:00:00Z",
    measurements: { realityDistortion: 0.78, exoticParticleDensity: 0.0012, dimensionalBreach: 12.4, stabilityIndex: -0.89 },
  },
};

const THREAT_LEVELS = ["CRITICAL", "SEVERE", "HIGH", "MODERATE", "LOW"];

function addMeasurement(name = "", value = "") {
  const row = document.createElement("div");
  row.className = "measure-row";
  const nameInput = document.createElement("input");
  nameInput.type = "text";
  nameInput.className = "m-name";
  nameInput.placeholder = "Name";
  nameInput.setAttribute("aria-label", "Measurement name");
  nameInput.value = name;
  const valueInput = document.createElement("input");
  valueInput.type = "number";
  valueInput.step = "any";
  valueInput.className = "m-value";
  valueInput.placeholder = "Value";
  valueInput.setAttribute("aria-label", "Measurement value");
  valueInput.value = value;
  const remove = document.createElement("button");
  remove.type = "button";
  remove.className = "pill p-salmon remove-measurement";
  remove.textContent = "Remove";
  remove.addEventListener("click", () => {
    // at least one row is required
    if ($("measurements").children.length > 1) row.remove();
  });
  row.append(nameInput, valueInput, remove);
  $("measurements").append(row);
}

function build() {
  const detectedAt = new Date($("detectedAt").value);
  if (Number.isNaN(detectedAt.getTime())) throw new Error("Detected at is not a valid date and time");
  const sensorData = $("sensorData").value.trim();
  const coordinates = $("coordinates").value.trim();
  if (!sensorData || !coordinates) throw new Error("Coordinates and sensor data are required");

  if ($("measurements").children.length === 0) throw new Error("Add at least one measurement");
  const measurements = Object.create(null);
  for (const row of $("measurements").children) {
    const name = row.querySelector(".m-name").value.trim();
    const text = row.querySelector(".m-value").value.trim();
    const value = text === "" ? Number.NaN : Number(text);
    if (!name || Number.isNaN(value)) throw new Error("Every measurement needs a name and a numeric value");
    if (Object.hasOwn(measurements, name)) throw new Error(`Measurement "${name}" is listed twice`);
    measurements[name] = value;
  }
  return { anomalyId: "CLASS-" + uuid(), sensorData, coordinates, detectedAt: detectedAt.toISOString(), measurements };
}

const preset = (name) => () => {
  const { sensorData, coordinates, detectedAt, measurements } = PRESETS[name];
  $("sensorData").value = sensorData;
  $("coordinates").value = coordinates;
  $("detectedAt").value = detectedAt;
  $("measurements").replaceChildren();
  for (const [key, value] of Object.entries(measurements)) addMeasurement(key, value);
};

// TNG stardates count 1000 per year from 2323 (41000 = 2364)
function stardate() {
  const date = new Date($("detectedAt").value);
  if (Number.isNaN(date.getTime())) return null;
  const year = date.getUTCFullYear();
  const dayOfYear = (date - Date.UTC(year, 0, 1)) / 86400000;
  const daysInYear = (Date.UTC(year + 1, 0, 1) - Date.UTC(year, 0, 1)) / 86400000;
  return (1000 * (year - 2323) + (1000 * dayOfYear) / daysInYear).toFixed(1);
}

function setNode(state) {
  const node = $("classifier-node");
  const className = `node ${state}`;
  if (node.className !== className) node.className = className;
  const text = { done: "Complete", active: "Classifying", pending: "Standby" }[state];
  const label = node.querySelector(".stage-state");
  if (label.textContent !== text) label.textContent = text;
}

function setLane(item, state) {
  const className = `lane ${state}`;
  if (item.className !== className) item.className = className;
  const text = { active: "Analyzing", done: "Complete", skipped: "Not routed", pending: "Standby" }[state];
  const label = item.querySelector(".stage-state");
  if (label.textContent !== text) label.textContent = text;
}

// what the decorative arc and lever show now: confidence 0-1 (null = empty) and the chosen lane (-1 = centred)
const gauge = { confidence: null, lane: -1 };

// an animated aim takes the lever delay plus its throw (see --lever-delay and --lever-move in demo.css)
const AIM_MS = 1600;
let animatingUntil = 0;

function aimLever() {
  const box = $("switch").getBoundingClientRect();
  // the lever pivots 6px from the left edge and reaches across the column and its gap to the lane
  const reach = Math.max(box.width + 2, 0);
  let rise = 0;
  if (gauge.lane >= 0 && box.height > 0) {
    const lane = lanes[gauge.lane].getBoundingClientRect();
    rise = lane.top + lane.height / 2 - (box.top + box.height / 2);
  }
  $("lever").style.setProperty("--len", `${Math.hypot(reach, rise).toFixed(1)}px`);
  $("lever").style.setProperty("--angle", `${Math.atan2(rise, reach).toFixed(4)}rad`);
  $("lever").classList.toggle("thrown", gauge.lane >= 0);
}

function showGauge(confidence, lane, animate) {
  for (const element of [$("arc"), $("lever")]) element.classList.toggle("snap", !animate);
  if (animate) animatingUntil = performance.now() + AIM_MS;
  gauge.confidence = confidence;
  gauge.lane = lane;
  // the routed lane lights up after the lever lands, only when the lever actually moves
  lanes.forEach((item, index) => item.toggleAttribute("data-follow", animate && index === lane));
  $("arc-fill").style.strokeDashoffset = String(100 - 100 * (confidence ?? 0));
  const text = confidence === null ? "--" : `${Math.round(confidence * 100)}%`;
  if ($("arc-text").textContent !== text) $("arc-text").textContent = text;
  aimLever();
}

// the switch changes size when the progress panel appears and when the lanes wrap
// (a resize during an animated aim leaves it running; later ones re-aim without the delay)
new ResizeObserver(() => {
  if (performance.now() >= animatingUntil) $("lever").classList.add("snap");
  aimLever();
}).observe($("switch"));

function renderSwitch(status, progress, routed) {
  const running = status === "Running" || status === "Pending";
  if (progress?.stage !== "Analyzing") {
    if (running && (gauge.confidence !== null || gauge.lane >= 0)) showGauge(null, -1, false);
    return;
  }
  const value = Number.isFinite(progress.confidence)
    ? Math.min(1, Math.max(0, progress.confidence))
    : null;
  const lane = routed ? lanes.indexOf(routed) : -1;
  // a finished run shows its final state without moving
  const animate = running && runState.animate;
  if (value === gauge.confidence && lane === gauge.lane) {
    for (const element of [$("arc"), $("lever")]) element.classList.toggle("snap", !animate);
    return;
  }
  showGauge(value, lane, animate);
}

// two checkpoints: the classification and the specialist analysis
function checkpoints(status, progress) {
  if (status === "Completed") return { done: 2, total: 2 };
  return { done: progress?.stage === "Analyzing" ? 1 : 0, total: 2 };
}

function renderProgress(status, progress) {
  const analyzing = progress?.stage === "Analyzing";
  const routed = analyzing ? lanes.find((lane) => lane.dataset.type === progress.type) : undefined;
  const finished = status === "Completed";
  const running = status === "Running" || status === "Pending";

  if (analyzing || finished) setNode("done");
  else setNode(running ? "active" : "pending");

  for (const lane of lanes) {
    if (routed === lane) setLane(lane, finished ? "done" : running ? "active" : "pending");
    else setLane(lane, analyzing ? "skipped" : "pending");
  }

  const unrouted = analyzing && !routed;
  if ($("routing-note").hidden === unrouted) $("routing-note").hidden = !unrouted;
  const note = unrouted ? "No specialist route for this anomaly — it will need manual review" : "";
  if ($("routing-note").textContent !== note) $("routing-note").textContent = note;

  renderSwitch(status, progress, routed);
}

const MAX_TEXT = 160;
// objects and arrays show as compact JSON text, clamped, never "[object Object]"
function plain(value) {
  const text = value !== null && typeof value === "object" ? clean(JSON.stringify(value)) : clean(value);
  return text.length > MAX_TEXT ? `${text.slice(0, MAX_TEXT)}…` : text;
}

function formatMetric(value) {
  return typeof value === "number" ? String(Number(value.toPrecision(4))) : plain(value);
}

function renderResult(result) {
  $("anomaly-type").textContent = clean(result.type);
  $("classification").textContent = clean(result.classification);
  $("specialized-analysis").textContent = clean(result.specializedAnalysis);

  const label = levelFrom(clean(result.threatLevel), THREAT_LEVELS, /threat level\b.{0,24}?/);
  $("threat-level").textContent = label ?? (clean(result.threatLevel).slice(0, 14).toUpperCase() || "ASSESSED");

  const rows = Object.entries(result.metrics ?? {}).map(([name, value]) => {
    const row = document.createElement("tr");
    const th = document.createElement("th");
    th.scope = "row";
    th.textContent = name;
    const td = document.createElement("td");
    td.textContent = formatMetric(value);
    row.append(th, td);
    return row;
  });
  $("metrics").replaceChildren(...rows);

  $("recommendations").replaceChildren(...(Array.isArray(result.recommendations) ? result.recommendations : []).map((text) => {
    const item = document.createElement("li");
    item.textContent = plain(text);
    return item;
  }));

  const unknown = clean(result.type).toLowerCase() === "unknown";
  return { alert: label === "CRITICAL" || label === "SEVERE", note: unknown ? "Analysis incomplete — manual review required" : undefined };
}

$("add-measurement").addEventListener("click", () => addMeasurement());

export default {
  start: { path: "/api/anomaly/analyze", build },
  statusPath: (instanceId) => `/api/anomaly/status/${encodeURIComponent(instanceId)}`,
  presets: Object.fromEntries(Object.keys(PRESETS).map((name) => [name, preset(name)])),
  initialPreset: "temporal-rift",
  stardate,
  renderProgress,
  checkpoints,
  renderResult,
  messages: {
    refused: (status) => `The ship's computer refused the request (${status})`,
    notFound: "The classification instance could not be found.",
    ended: (status) => `The classification ended with status ${status}.`,
  },
};
