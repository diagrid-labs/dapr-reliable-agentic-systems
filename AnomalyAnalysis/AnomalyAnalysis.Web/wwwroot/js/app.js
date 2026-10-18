const POLL_MS = 2000;
const RETRY_MS = 5000;
const MAX_NOT_FOUND = 15;

const $ = (id) => document.getElementById(id);
const stageItems = [...document.querySelectorAll("#stage-list li")];
const TERMINAL = ["Completed", "Failed", "Terminated"];

const PRESETS = {
  wormhole: {
    coordinates: "Sector 001, Alpha Quadrant",
    detectedAt: "2370-06-15T14:32:00Z",
    subspaceFrequency: 47.3,
    gravimetricDistortion: 8.2,
    temporalVariance: 0.003,
    radiationLevel: 2.1,
    rawSensorData: "SUBSPACE_SIGNATURE: 47.3 THz | GRAVIMETRIC_DISTORTION: 8.2 tesla | TEMPORAL_VARIANCE: 0.003% | SPATIAL_COORDINATES: Sector 001 | QUANTUM_FLUCTUATION: High | BARYON_SWEEP: Negative",
  },
  "temporal-rift": {
    coordinates: "Sector 214, Beta Quadrant",
    detectedAt: "2370-08-02T03:10:00Z",
    subspaceFrequency: 12.9,
    gravimetricDistortion: 3.1,
    temporalVariance: 41.7,
    radiationLevel: 1.4,
    rawSensorData: "SUBSPACE_SIGNATURE: 12.9 THz | GRAVIMETRIC_DISTORTION: 3.1 tesla | TEMPORAL_VARIANCE: 41.7% | SPATIAL_COORDINATES: Sector 214 | QUANTUM_FLUCTUATION: Moderate | CHRONITON_SWEEP: Positive",
  },
  "quantum-flux": {
    coordinates: "Sector 009, Gamma Quadrant",
    detectedAt: "2370-11-23T21:45:00Z",
    subspaceFrequency: 188.4,
    gravimetricDistortion: 96.5,
    temporalVariance: 12.8,
    radiationLevel: 940,
    rawSensorData: "SUBSPACE_SIGNATURE: 188.4 THz | GRAVIMETRIC_DISTORTION: 96.5 tesla | TEMPORAL_VARIANCE: 12.8% | SPATIAL_COORDINATES: Sector 009 | QUANTUM_FLUCTUATION: Extreme | RADIATION: 940 rads | HULL_STRESS: Increasing",
  },
};

const fields = ["coordinates", "detectedAt", "subspaceFrequency", "gravimetricDistortion", "temporalVariance", "radiationLevel", "rawSensorData"];

let runToken = 0;
let pollTimer;
let clockTimer;
let startedAt;
let submitting = false;

function setView(view) {
  document.body.dataset.view = view;
}

function applyPreset(name) {
  const preset = PRESETS[name];
  for (const field of fields) $(field).value = preset[field];
  $("form-error").hidden = true;
  updateStardate();
}

// TNG stardates count 1000 per year from 2323 (41000 = 2364)
function updateStardate() {
  const date = new Date($("detectedAt").value);
  if (Number.isNaN(date.getTime())) {
    $("stardate").textContent = "--";
    return;
  }
  const year = date.getUTCFullYear();
  const dayOfYear = (date - Date.UTC(year, 0, 1)) / 86400000;
  const daysInYear = (Date.UTC(year + 1, 0, 1) - Date.UTC(year, 0, 1)) / 86400000;
  $("stardate").textContent = (1000 * (year - 2323) + (1000 * dayOfYear) / daysInYear).toFixed(1);
}

function buildAnomaly() {
  const detectedAt = new Date($("detectedAt").value);
  if (Number.isNaN(detectedAt.getTime())) throw new Error("Detected at is not a valid date and time");
  const numbers = {};
  for (const field of ["subspaceFrequency", "gravimetricDistortion", "temporalVariance", "radiationLevel"]) {
    const value = Number.parseFloat($(field).value);
    if (Number.isNaN(value)) throw new Error("Sensor readings must be numbers");
    numbers[field] = value;
  }
  const coordinates = $("coordinates").value.trim();
  const rawSensorData = $("rawSensorData").value.trim();
  if (!coordinates || !rawSensorData) throw new Error("Coordinates and raw sensor log are required");
  return {
    anomalyId: crypto.randomUUID(),
    rawSensorData,
    spatialCoordinates: coordinates,
    detectedAt: detectedAt.toISOString(),
    sensorReadings: numbers,
  };
}

function resetRun() {
  runToken++;
  clearTimeout(pollTimer);
  clearInterval(clockTimer);
  document.body.classList.remove("red-alert");
  $("alert-banner").hidden = true;
  $("link-lost").hidden = true;
  $("run-note").hidden = true;
  $("findings-note").hidden = true;
  setLink(true);
  renderProgress("Pending", null);
  $("run-status").textContent = "--";
}

async function startAnalysis() {
  if (submitting) return;
  $("form-error").hidden = true;
  let anomaly;
  try {
    anomaly = buildAnomaly();
  } catch (error) {
    $("form-error").textContent = error.message;
    $("form-error").hidden = false;
    return;
  }

  submitting = true;
  $("begin").disabled = true;
  try {
    const response = await fetch("/api/anomaly/analyze", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(anomaly),
    });
    if (!response.ok) throw new Error(`The ship's computer refused the request (${response.status})`);
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
  try {
    response = await fetch(`/api/anomaly/status/${encodeURIComponent(instanceId)}`);
  } catch {
    if (token !== runToken) return;
    setLink(false);
    schedule(instanceId, token, RETRY_MS, notFound);
    return;
  }
  if (token !== runToken) return;

  if (response.status === 404) {
    // the workflow may not be visible yet right after scheduling
    if (notFound + 1 >= MAX_NOT_FOUND) return finish("Failed", null, "The analysis instance could not be found.");
    schedule(instanceId, token, POLL_MS, notFound + 1);
    return;
  }
  if (!response.ok) {
    setLink(false);
    schedule(instanceId, token, RETRY_MS, notFound);
    return;
  }

  setLink(true);
  const state = await response.json();
  if (token !== runToken) return;
  renderProgress(state.status, state.progress);
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
    renderResult(result);
    setView("completed");
    return;
  }
  $("run-note").textContent = note ?? `The analysis ended with status ${status}.`;
  $("run-note").hidden = false;
  setView("failed");
}

function renderProgress(status, progress) {
  const done = new Map((progress ?? []).map((stage) => [stage.stageName, stage]));
  const running = status === "Running" || status === "Pending";
  let activeAssigned = false;

  for (const item of stageItems) {
    const stage = done.get(item.dataset.stage);
    const details = item.querySelector(".stage-output");
    let state = "pending";
    if (stage) {
      state = "done";
    } else if (running && !activeAssigned) {
      state = "active";
      activeAssigned = true;
    }
    item.className = state;
    item.querySelector(".stage-state").textContent = { done: "Complete", active: "Processing", pending: "Standby" }[state];
    details.hidden = !stage;
    if (stage) item.querySelector("pre").textContent = clean(stage.output);
  }
}

// the LLM answers in markdown-flavoured text; show it as plain text
const clean = (text) => String(text ?? "").replace(/\*\*/g, "").replace(/^#+\s*/gm, "").trim();

// riskLevel is a full assessment: pick the level out of it for the pill
function riskLabel(text) {
  const labelled = /risk level\b.{0,24}?\b(critical|high|moderate|medium|low)\b/is.exec(text);
  const known = /\b(critical|high|moderate|medium|low)\b/i.exec(text);
  return (labelled?.[1] ?? known?.[1] ?? "assessed").toUpperCase();
}

function renderResult(result) {
  $("anomaly-type").textContent = clean(result.anomalyType).replace(/^classification:\s*/i, "");
  $("scientific-analysis").textContent = clean(result.scientificAnalysis);
  $("risk-text").textContent = clean(result.riskLevel);
  $("tactical-recommendation").textContent = clean(result.tacticalRecommendation);
  $("risk-level").textContent = riskLabel(result.riskLevel);

  const critical = $("risk-level").textContent === "CRITICAL";
  document.body.classList.toggle("red-alert", critical);
  $("alert-banner").hidden = !critical;

  const unknown = clean(result.anomalyType) === "UNKNOWN";
  $("findings-note").textContent = "Analysis incomplete — manual review required";
  $("findings-note").hidden = !unknown;
}

window.renderResult = renderResult;

$("anomaly-form").addEventListener("submit", (event) => {
  event.preventDefault();
  startAnalysis();
});
for (const button of document.querySelectorAll("[data-preset]")) {
  button.addEventListener("click", () => applyPreset(button.dataset.preset));
}
$("detectedAt").addEventListener("input", updateStardate);
for (const button of document.querySelectorAll("#new-analysis, #restart")) {
  button.addEventListener("click", () => {
    resetRun();
    setView("idle");
  });
}

applyPreset("wormhole");
