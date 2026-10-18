import { $, clean, levelFrom, uuid } from "./util.js";

const stageItems = [...document.querySelectorAll("#stage-list li")];

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
const RISK_LEVELS = ["critical", "high", "moderate", "medium", "low"];

function build() {
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
    anomalyId: uuid(),
    rawSensorData,
    spatialCoordinates: coordinates,
    detectedAt: detectedAt.toISOString(),
    sensorReadings: numbers,
  };
}

const preset = (name) => () => {
  for (const field of fields) $(field).value = PRESETS[name][field];
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

// riskLevel is a full assessment: pick the level out of it for the pill
function riskLabel(text) {
  const single = /^\w{1,12}$/.exec(text.trim());
  return levelFrom(text, RISK_LEVELS, /risk level\b.{0,24}?/) ?? single?.[0].toUpperCase() ?? "ASSESSED";
}

function renderResult(result) {
  $("anomaly-type").textContent = clean(result.anomalyType).replace(/^classification:\s*/i, "");
  $("scientific-analysis").textContent = clean(result.scientificAnalysis);
  $("risk-text").textContent = clean(result.riskLevel);
  $("tactical-recommendation").textContent = clean(result.tacticalRecommendation);
  const label = riskLabel(result.riskLevel);
  $("risk-level").textContent = label;

  const unknown = clean(result.anomalyType) === "UNKNOWN";
  return { alert: label === "CRITICAL", note: unknown ? "Analysis incomplete — manual review required" : undefined };
}

export default {
  start: { path: "/api/anomaly/analyze", build },
  statusPath: (instanceId) => `/api/anomaly/status/${encodeURIComponent(instanceId)}`,
  presets: { wormhole: preset("wormhole"), "temporal-rift": preset("temporal-rift"), "quantum-flux": preset("quantum-flux") },
  initialPreset: "wormhole",
  stardate,
  renderProgress,
  renderResult,
  messages: {
    refused: (status) => `The ship's computer refused the request (${status})`,
    notFound: "The analysis instance could not be found.",
    ended: (status) => `The analysis ended with status ${status}.`,
  },
};
