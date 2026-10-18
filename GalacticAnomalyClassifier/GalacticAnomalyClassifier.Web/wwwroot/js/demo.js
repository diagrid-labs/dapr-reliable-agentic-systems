import { $, clean, levelFrom, uuid } from "./util.js";

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
  $("classifier-node").className = `node ${state}`;
  $("classifier-node").querySelector(".stage-state").textContent = { done: "Complete", active: "Classifying", pending: "Standby" }[state];
}

function setLane(item, state) {
  item.className = `lane ${state}`;
  item.querySelector(".stage-state").textContent = { active: "Analyzing", done: "Complete", skipped: "Not routed", pending: "Standby" }[state];
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
  $("routing-note").hidden = !unrouted;
  $("routing-note").textContent = unrouted ? "No specialist route for this anomaly — it will need manual review" : "";
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
  renderResult,
  messages: {
    refused: (status) => `The ship's computer refused the request (${status})`,
    notFound: "The classification instance could not be found.",
    ended: (status) => `The classification ended with status ${status}.`,
  },
};
