import { $, clean, uuid } from "./util.js";

const PRESETS = {
  vulcan: {
    alienSpecies: "Vulcan",
    context: "Diplomatic",
    originalText: "Dif-tor heh smusma. Kal-tor pan shar. Taluhk nash-veh k'dular.",
    knownVocabulary: {
      "Dif-tor heh smusma": "Live long and prosper (formal greeting)",
      "Kal-tor": "infinite diversity",
      Taluhk: "cherish/treasure",
    },
    culturalNotes: "Vulcans value logic and precision. Greetings often reference longevity and prosperity. IDIC (Infinite Diversity in Infinite Combinations) is a core philosophy.",
  },
  klingon: {
    alienSpecies: "Klingon",
    context: "Literary",
    originalText: "Qapla'! qaStaHvIS wa' ram loS SaD Hugh SIjlaH qetbogh loD.",
    knownVocabulary: {
      "Qapla'": "Success (battle cry)",
      qaStaHvIS: "while/during",
      SIjlaH: "can imagine",
      qetbogh: "running",
    },
    culturalNotes: "Klingon poetry emphasizes honor, battle prowess, and glory in death. Metaphors often involve warfare and physical strength. Direct translation may miss warrior ethos.",
  },
  romulan: {
    alienSpecies: "Romulan",
    context: "Casual",
    originalText: "Jolan'tru. Mnei sahe nnea. Lhhei s'dena khir.",
    knownVocabulary: {
      "Jolan'tru": "hello (casual)",
      "Mnei sahe": "my friend",
      Lhhei: "until",
      khir: "stars",
    },
    culturalNotes: "Romulans value secrecy and cunning. Even casual speech contains layers of meaning. References to stars and night are common in farewells.",
  },
};

const DEFAULT_THRESHOLD = 8;
const DEFAULT_MAX_ITERATIONS = 5;
const SCORES = [
  ["accuracyScore", "Accuracy"],
  ["culturalNuanceScore", "Cultural nuance"],
  ["idiomaticScore", "Idiomatic"],
  ["overallQuality", "Overall quality"],
];
const SVG_NS = "http://www.w3.org/2000/svg";

const board = $("iteration-board");
const processingRow = $("processing-row");
let threshold = DEFAULT_THRESHOLD;

const asArray = (value) => (Array.isArray(value) ? value : []);
const objects = (value) => asArray(value).filter((item) => item && typeof item === "object");
const make = (tag, className, text) => {
  const element = document.createElement(tag);
  if (className) element.className = className;
  if (text !== undefined) element.textContent = text;
  return element;
};
// scores run 0 to 10; anything else is clamped, non-numbers and blank strings give null
const score = (value) => {
  if (typeof value === "string" && value.trim() === "") return null;
  const number = typeof value === "number" || typeof value === "string" ? Number(value) : Number.NaN;
  return Number.isFinite(number) ? Math.min(10, Math.max(0, number)) : null;
};
// the board is a live region: write only what changed so an unchanged poll mutates nothing
const setText = (element, text) => { if (element.textContent !== text) element.textContent = text; };
const setClass = (element, name) => { if (element.className !== name) element.className = name; };
const setVar = (element, name, value) => { if (element.style.getPropertyValue(name) !== value) element.style.setProperty(name, value); };
const percent = (value) => `${(value ?? 0) * 10}%`;

function addVocab(phrase = "", meaning = "") {
  const row = make("div", "vocab-row");
  const phraseInput = make("input", "v-phrase");
  phraseInput.type = "text";
  phraseInput.placeholder = "Phrase";
  phraseInput.setAttribute("aria-label", "Vocabulary phrase");
  phraseInput.value = phrase;
  const meaningInput = make("input", "v-meaning");
  meaningInput.type = "text";
  meaningInput.placeholder = "Meaning";
  meaningInput.setAttribute("aria-label", "Vocabulary meaning");
  meaningInput.value = meaning;
  const remove = make("button", "pill p-salmon remove-vocab", "Remove");
  remove.type = "button";
  remove.addEventListener("click", () => row.remove());
  row.append(phraseInput, meaningInput, remove);
  $("vocabulary").append(row);
}

function toggleOther() {
  const other = $("alienSpecies").value === "Other";
  $("other-species-label").hidden = !other;
  if (!other) $("otherSpecies").value = "";
}

function build() {
  const alienSpecies = $("alienSpecies").value === "Other" ? $("otherSpecies").value.trim() : $("alienSpecies").value;
  if (!alienSpecies) throw new Error("Species name is required");
  const originalText = $("originalText").value.trim();
  if (!originalText) throw new Error("Original text is required");

  // null prototype: a phrase such as "__proto__" or "constructor" stays plain data
  const knownVocabulary = Object.create(null);
  for (const row of $("vocabulary").children) {
    const phrase = row.querySelector(".v-phrase").value.trim();
    const meaning = row.querySelector(".v-meaning").value.trim();
    if (!phrase && !meaning) continue;
    if (!phrase || !meaning) throw new Error("Every vocabulary row needs both a phrase and a meaning");
    if (Object.hasOwn(knownVocabulary, phrase)) throw new Error(`Phrase "${phrase}" is listed twice`);
    knownVocabulary[phrase] = meaning;
  }

  // the instance id is TRANS-<textId>, so every run needs its own textId
  const slug = alienSpecies.toUpperCase().replace(/[^A-Z0-9]/g, "") || "ALIEN";
  return {
    textId: `${slug}-${uuid()}`,
    alienSpecies,
    originalText,
    context: $("context").value,
    knownVocabulary,
    culturalNotes: $("culturalNotes").value.trim(),
  };
}

const preset = (name) => () => {
  const { alienSpecies, context, originalText, knownVocabulary, culturalNotes } = PRESETS[name];
  $("alienSpecies").value = alienSpecies;
  toggleOther();
  $("context").value = context;
  $("originalText").value = originalText;
  $("vocabulary").replaceChildren();
  for (const [phrase, meaning] of Object.entries(knownVocabulary)) addVocab(phrase, meaning);
  $("culturalNotes").value = culturalNotes;
};

// today's day of the year mapped onto 2368; TNG stardates count 1000 per year from 2323 (41000 = 2364)
function stardate() {
  const now = new Date();
  const year = now.getUTCFullYear();
  const dayOfYear = (now - Date.UTC(year, 0, 1)) / 86400000;
  const daysInYear = (Date.UTC(year + 1, 0, 1) - Date.UTC(year, 0, 1)) / 86400000;
  return (1000 * (2368 - 2323) + (1000 * dayOfYear) / daysInYear).toFixed(1);
}

function iterationElement() {
  const item = make("li", "iter");
  const bar = make("span", "q-bar");
  bar.append(make("span", "q-track"), make("span", "q-score"));
  const text = make("details", "iter-text");
  text.append(make("summary", "", "Translation"), make("p", "prose"));
  item.append(make("span", "stage-name"), bar, make("span", "verdict"), text);
  return item;
}

// rows are positional and reused so an expanded translation stays open across polls;
// the processing row is the board's last child, iteration rows are inserted before it
function setIteration(item, entry, index) {
  const quality = score(entry.overallQuality);
  const meets = entry.meetsStandards === true;
  setClass(item, `iter ${meets ? "meets" : "below"}`);
  const number = entry.iteration;
  setText(item.querySelector(".stage-name"), `Iteration ${Number.isInteger(number) && number > 0 ? number : index + 1}`);
  const track = item.querySelector(".q-track");
  setVar(track, "--width", percent(quality));
  setVar(track, "--mark", percent(threshold));
  setText(item.querySelector(".q-score"), quality === null ? "--" : quality.toFixed(1));
  setText(item.querySelector(".verdict"), meets ? "Meets standard" : "Below standard");
  setText(item.querySelector(".iter-text p"), clean(entry.translatedText));
}

function renderProgress(status, progress) {
  const running = status === "Running" || status === "Pending";
  const iterations = objects(progress?.iterations);
  const limit = Number(progress?.maxIterations);
  const maxIterations = Number.isInteger(limit) && limit > 0 ? limit : DEFAULT_MAX_ITERATIONS;
  const level = Number(progress?.threshold);
  threshold = Number.isFinite(level) && level > 0 ? score(level) : DEFAULT_THRESHOLD;

  while (board.children.length - 1 > iterations.length) processingRow.previousElementSibling.remove();
  while (board.children.length - 1 < iterations.length) processingRow.before(iterationElement());
  iterations.forEach((entry, index) => setIteration(board.children[index], entry, index));

  const next = Math.min(iterations.length + 1, maxIterations);
  if (processingRow.hidden === running) processingRow.hidden = !running;
  setText($("processing-name"), `Processing iteration ${next}`);
  $("iteration-footer").textContent = `Iteration ${running ? next : iterations.length} / ${maxIterations}`;
}

function scoreRow(label, value) {
  const row = make("div", "score");
  const track = make("span", "q-track");
  track.style.setProperty("--width", percent(value));
  row.append(make("span", "score-name", label), track, make("span", "score-value", value === null ? "--" : value.toFixed(1)));
  return row;
}

function svg(tag, attributes) {
  const element = document.createElementNS(SVG_NS, tag);
  for (const [name, value] of Object.entries(attributes)) element.setAttribute(name, value);
  return element;
}

// values is indexed by iteration; a null (not scored) leaves a gap so x positions keep matching the iterations
function sparkline(values) {
  const scored = values.flatMap((value, index) => (value === null ? [] : [[index, value]]));
  if (scored.length === 0) return make("p", "trend-empty", "No evaluations recorded");
  const width = 200;
  const height = 60;
  const pad = 8;
  const x = (index) => (values.length === 1 ? width / 2 : pad + (index * (width - 2 * pad)) / (values.length - 1));
  const y = (value) => height - pad - (value / 10) * (height - 2 * pad);

  const chart = svg("svg", { viewBox: `0 0 ${width} ${height}`, role: "img" });
  chart.setAttribute("aria-label", `Overall quality per iteration: ${values.map((value) => (value === null ? "not scored" : value.toFixed(1))).join(", ")}`);
  chart.append(
    svg("line", { class: "threshold", x1: pad, x2: width - pad, y1: y(threshold), y2: y(threshold) }),
    svg("polyline", { points: scored.map(([index, value]) => `${x(index)},${y(value)}`).join(" ") }),
    ...scored.map(([index, value]) => svg("circle", { cx: x(index), cy: y(value), r: 3 })),
  );
  return chart;
}

function renderResult(result) {
  const finalTranslation = result.finalTranslation && typeof result.finalTranslation === "object" ? result.finalTranslation : {};
  const finalEvaluation = result.finalEvaluation && typeof result.finalEvaluation === "object" ? result.finalEvaluation : {};
  const reasoning = clean(finalTranslation.translatorReasoning);

  // the result does not carry the original text; it is still in the form
  $("original-text").textContent = $("originalText").value.trim();
  $("final-translation").textContent = clean(finalTranslation.translatedText);
  $("reasoning-text").textContent = reasoning;
  $("reasoning").hidden = !reasoning;
  $("scores").replaceChildren(...SCORES.map(([key, label]) => scoreRow(label, score(finalEvaluation[key]))));
  $("strengths").replaceChildren(...asArray(finalEvaluation.strengths).map((text) => make("li", "", clean(text))));
  $("weaknesses").replaceChildren(...asArray(finalEvaluation.weaknesses).map((text) => make("li", "", clean(text))));
  $("feedback").textContent = clean(finalEvaluation.detailedFeedback);

  const summary = clean(result.improvementSummary);
  $("summary").textContent = summary;
  const qualities = objects(result.evaluations).map((evaluation) => score(evaluation.overallQuality));
  $("trend").replaceChildren(sparkline(qualities));

  return { alert: false, note: /max iterations reached/i.test(summary) ? "MANUAL REVIEW RECOMMENDED" : undefined };
}

$("add-vocab").addEventListener("click", () => addVocab());
$("alienSpecies").addEventListener("change", toggleOther);

export default {
  start: { path: "/api/translate", build },
  statusPath: (instanceId) => `/api/translate/${encodeURIComponent(instanceId)}`,
  presets: Object.fromEntries(Object.keys(PRESETS).map((name) => [name, preset(name)])),
  initialPreset: "vulcan",
  stardate,
  renderProgress,
  renderResult,
  messages: {
    refused: (status) => `The ship's computer refused the request (${status})`,
    notFound: "The translation run could not be found.",
    ended: (status) => `The translation ended with status ${status}.`,
  },
};
