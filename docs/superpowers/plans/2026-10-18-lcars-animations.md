# LCARS Animations for the Six Demo UIs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a shared durable-timeline (crash and resume) animation plus one signature animation per demo to the six LCARS web UIs, driven only by real status data.

**Architecture:** The shared pieces (`engine.js`, `util.js`, `lcars.css`, YARP `appsettings.json`) are extended once in AnomalyAnalysis, tested there, then copied byte-identically into the other five projects. Each demo then adds its own signature animation in its own `index.html`, `js/demo.js` and `css/demo.css`. No ApiService, AppHost or workflow changes.

**Tech Stack:** Vanilla JS (ES modules), CSS animations/custom properties, small inline SVG where needed, YARP (`HttpRequest.ActivityTimeout`), headless Chrome + puppeteer-core for verification.

**Spec:** `docs/superpowers/specs/2026-10-18-lcars-animations-design.md` (read it with this plan; it holds the decisions, the crash finding and the settled calls). Visual references (live loops, not production code) existed in `.superpowers/brainstorm/39253-1792356246/content/` but were deleted when the brainstorm server was stopped: the spec's descriptions are authoritative.

## Global Constraints

- Vanilla JS and CSS only, no libraries, no canvas engine. Server text only via `textContent` and `clean()`, never `innerHTML`.
- Solutions stay independent; shared files are copied into each of the six projects and must be **byte-identical** after Tasks 1-2 (`md5` check): `wwwroot/js/engine.js`, `wwwroot/js/util.js`, `wwwroot/css/lcars.css`, `appsettings.json`. After that, only each demo's `index.html`, `js/demo.js`, `css/demo.css` and README change (Tasks 3-8). If a later task needs a shared-file change, apply it to all six copies and re-run the md5 check.
- Real data only: nothing moves on a timer to imply progress (indeterminate shimmer/sweep while a thing is running is allowed; percentages, counters and bars must come from data). No server/ApiService/AppHost/workflow changes.
- Captions must be accurate: after a crash, finished steps are not repeated but the step that was running starts again. Use "resumed with N of M checkpoints saved" (M omitted when unknown; N = 0: "run continues (no checkpoint saved yet)"), never "no work repeated".
- Poll model unchanged (setTimeout chain, 2 s / 5 s retry, 15 consecutive 404s give up), except the approved fast-failure fix: each poll gets a ~5 s timeout (`AbortController`) that is treated like a network error, and YARP `HttpRequest.ActivityTimeout` of `00:00:05` in each Web `appsettings.json`.
- `prefers-reduced-motion`: final state immediately, no sweeps/pings/pulses/orbits/reveals.
- Animation only for items that appear in a render after the first successful render of a run; the first render shows what is there instantly. Items appearing right after a link-lost gap animate.
- No animation or timer survives a run reset (restart/new run) or a finished run (the final reveal may complete); strip and final states stay visible, frozen, after a run ends.
- Live regions must not be mutated character by character; decorative animation is `aria-hidden` and outside live regions; write DOM text only when it changed.
- Existing contracts stay valid: required/optional ids in the `engine.js` header comment, `window.renderResult`, positional row reuse (open `<details>` stay open), `demo.messages`, `levelFrom` prefix semantics.
- Code style per `AGENTS.md`; comments only for non-obvious logic. `.gitattributes` `eol=lf`.
- Do not run `git add`, `git commit` or `git rm`; the user stages and commits (saved preference).
- Wait ~10 s between `aspire stop` and the next `aspire run` (Valkey container name clash).

## Procedures used by several tasks

**Procedure A: local LLM for live checks** (as in the earlier plan): without `OPENAI_API_KEY` the sidecar exits. Move `<Name>.AppHost/Resources/conversation-openai.yaml` into the scratchpad without reading it, create a temporary `Resources/conversation.yaml` (`conversation.ollama`, component name `conversation`, model `glm-5.3-flash:cloud`), `aspire run` from the demo folder in the background (output to a file), wait until `wf-app-dapr-cli` is Running/Healthy plus ~15 s. Afterwards `aspire stop`, wait ~10 s, delete the temporary file, move the OpenAI file back, confirm `git status` shows nothing under `Resources/`.

**Procedure B: fabricated checks:** serve one Web project alone (`dotnet run --project <Name>/<Name>.Web`, kill only that process afterwards) and drive it with puppeteer-core (scratchpad e2e folder, Chrome `/Applications/Google Chrome.app/Contents/MacOS/Google Chrome`, headless 1280x900, earlier scripts there to adapt) with request interception to fabricate `/api/...` responses; screenshots are taken and looked at.

**Procedure C: real crash:** with Procedure A running, start a run from the UI, `aspire resource wf-app stop` mid-run (wait about 10 s), `aspire resource wf-app start`, and watch the page: link-lost caption within ~5 s of the stop, beads frozen red, restored caption after the start, run continues to completion.

## Review Focus

- Outage before any bead is saved, or link lost before the first successful poll: red state, caption and wording ("no checkpoint saved yet") are correct and nothing throws.
- Run finishes (or turns Failed/Terminated) while the link was down: caption cleared, strip frozen in the right final state, no stale "lost" styling.
- Restart/new run during an outage or while the "resumed" caption timer or a flash/reveal is running: all timers cleared, nothing leaks into the next run.
- Several new items arriving in one poll (or the run ending right after): reveals never leave partial text; the final text is always complete and correct.
- Done count above total (more decisions/iterations than the ghost limit, or `total` unknown): the strip clamps and never throws.
- Screen reader noise: live regions (decision log, iteration board, scan board, stage lists, durable caption) are not rewritten per animation frame; the caption is announced once per event.
- Slow-but-alive responses: the 5 s poll timeout does not make the start POST or a normal poll report link lost (check a poll that takes 3-4 s).

---

## File Structure

| File | Responsibility |
|------|----------------|
| `<Name>.Web/wwwroot/js/util.js` | `runState`, `reveal()` text reveal helper (copied) |
| `<Name>.Web/wwwroot/js/engine.js` | durable timeline rendering, outage tracking, poll timeout (copied) |
| `<Name>.Web/wwwroot/css/lcars.css` | durable strip, caption, shared keyframes, reduced-motion (copied) |
| `<Name>.Web/appsettings.json` | YARP cluster `HttpRequest.ActivityTimeout` 5 s (copied) |
| `<Name>.Web/wwwroot/index.html` | `#durable` + `#durable-caption` markup, per-demo decoration markup |
| `<Name>.Web/wwwroot/js/demo.js` | `checkpoints()` hook, signature animation logic |
| `<Name>.Web/wwwroot/css/demo.css` | signature animation styles (AnomalyAnalysis gets a new `demo.css` + `<link>`) |

---

### Task 1: Shared foundation in AnomalyAnalysis (durable timeline, reveal helper, fast failure)

**Files:**
- Modify: `AnomalyAnalysis/AnomalyAnalysis.Web/wwwroot/js/util.js`, `js/engine.js`, `css/lcars.css`, `index.html`, `js/demo.js`, `appsettings.json`

**Interfaces:**
- Produces in `util.js`:
  - `export const runState = { animate: false }`: engine sets it to `false` on every reset and to `true` after the first successful render of a run; demos read it to decide between animating and instant rendering.
  - `export function reveal(container, text, options?: { mode?: "type" | "decode", ms?: number }): () => void`: renders `text` into `container` using only `textContent`/DOM nodes. When `runState.animate` is false or reduced motion is on: one plain text node, returns a no-op. Otherwise it creates an `aria-hidden` visual span that animates (type: left to right; decode: scrambled glyphs resolving left to right; `ms` capped so long texts finish within about 2 s) plus a visually hidden (`.sr-only`) span holding the final text, written once. The returned function cancels the animation and writes the complete final text (also called automatically when `reveal` runs again on the same container, so a re-run or reset never leaves partial text). Pending reveals are tracked so the engine can finish them on reset.
- Produces in `engine.js` (all optional, null-safe): element `#durable` (strip container, `aria-hidden="true"`), element `#durable-caption` (a `role="status"` paragraph outside `#durable`), optional demo hook `checkpoints(status, progress): { done: number, total: number | null }`.
- Engine behaviour (new):
  - **Beads:** after each successful poll, call `demo.checkpoints`; render `done` saved beads, then (when `total` is a number) ghost beads up to `total` (clamp `done` to `total` when known, never throw on bad numbers), plus one leading "next" bead after the saved ones whenever `total` is unknown, `done` is 0, or the run is still active. Beads and segments are `li`/`span` elements in `#durable`; container `data-state` is `running` | `lost` | `ended`. A bead flashes `just-saved` for ~1.5 s when `done` increased versus the previous render of this run (not on the first render). Write only what changed.
  - **Outage tracking:** a failed or timed-out poll (existing link-lost condition) sets `#durable[data-state=lost]`, records `lostAt`, shows caption "Subspace link lost: state is safe" and a ticking `down mm:ss` in an `aria-hidden` span inside `#durable` (never inside the live caption). The next successful poll while the status is not terminal clears the lost state and shows the caption "Link restored: resumed with N of M checkpoints saved" (wording rules in Global Constraints) for ~6 s, then clears it. A terminal status clears the caption and sets `data-state=ended`.
  - **Reset:** `resetRun` clears all timers (flash, caption, down counter), empties the strip and caption and resets `runState`.
  - **Fast failure:** every status poll `fetch` carries an `AbortSignal` that aborts after `POLL_TIMEOUT_MS = 5000`; an abort is handled like the existing network-error path (`setLink(false)`, retry after `RETRY_MS`). The start POST has no timeout.
- Produces in `appsettings.json`: cluster `workflow` gets `"HttpRequest": { "ActivityTimeout": "00:00:05" }`.
- Produces in AnomalyAnalysis `demo.js`: `checkpoints(status, progress) => ({ done: stages in progress (array length, 0 for non-arrays), total: 5 })`; wired into the default export. `index.html` gets `<div id="durable" class="durable" aria-hidden="true"></div><p id="durable-caption" class="durable-caption" role="status"></p>` in the progress panel above `#stage-list`.

- [ ] **Step 1: Prove the problem first (RED).** Run Procedure A for AnomalyAnalysis and Procedure C steps on the unmodified code (a short run is enough): confirm that after `aspire resource wf-app stop` no link-lost banner appears for a long time (poll requests hang). Record the observation. Stop the app; keep the temporary component for later steps or restore per Procedure A.
- [ ] **Step 2: Implement** `runState` and `reveal` in `util.js`, the beads/outage/timeout behaviour in `engine.js`, the strip and caption styles and a reduced-motion block in `lcars.css` (flash, red lost state, frozen ended state, caption fade; keyframes named `durable-*`), the `appsettings.json` timeout, and the AnomalyAnalysis `checkpoints` hook and markup. Update the `engine.js` header comment with the new optional id/hook list.
- [ ] **Step 3: Fabricated checks (Procedure B)** on AnomalyAnalysis.Web: beads advance 0 to 5 with fabricated progress; an intercepted poll that never answers (and one answering 503) turns the strip red with the caption within ~5 s and the down counter ticks; restored polls show "resumed with N of 5 checkpoints saved" for ~6 s then clear it; with N = 0 the "no checkpoint saved yet" wording; terminal status after an outage clears the caption and freezes the strip; restart during an outage, during the 6 s caption and during a flash leaves nothing behind (check DOM and timers); a poll answering after 3-4 s does NOT count as link lost; `reveal` unit-level probes via `page.evaluate`: reduced motion and `runState.animate=false` give a single plain text node; animated mode ends with exactly the final text; calling it twice on one container leaves the second text complete; cancelling mid-way leaves the full final text.
  Expected: every check PASS, no page errors.
- [ ] **Step 4: Real crash (Procedure C)** on AnomalyAnalysis with the fix: link-lost caption appears within ~5-6 s of the stop (compare with Step 1), beads frozen red, restored caption after the start, run completes. Record timings. Restore Procedure A.
- [ ] **Step 5: Build and screenshots.** `dotnet build AnomalyAnalysis/AnomalyAnalysis.sln` → `Build succeeded.`; screenshots of idle, running with beads, lost, restored, completed (strip frozen) are looked at; fix visual defects.

---

### Task 2: Roll the shared files out to the other five demos and add the strip to each

**Files:**
- Modify (copy from AnomalyAnalysis, then verify identical): `<Name>.Web/wwwroot/js/util.js`, `js/engine.js`, `css/lcars.css`, `appsettings.json` for AlienTranslator, GalacticAnomalyClassifier, SpaceColonyPlanner, SpaceDebrisAgent, StarshipDiagnostics
- Modify (per demo): `index.html` (add `#durable` and `#durable-caption` in the progress panel above the demo's main board), `js/demo.js` (add `checkpoints` and export it)

**Interfaces:**
- Consumes: Task 1's engine contract (`#durable`, `#durable-caption`, `checkpoints(status, progress)`, `runState`).
- Produces: `checkpoints` per demo, using the existing `progress` shapes (read each `demo.js` for the exact field names; unknown shapes must give `{ done: 0, total: ... }`, never throw):
  - Galactic: `total` 2; done 0 while `stage` is Classifying or progress is null, 1 for Analyzing, 2 when status is Completed.
  - Starship: `total` 6 (five scans plus the report bead); done = `scans.length` (cap 5), 6 when Completed.
  - Colony: `total` null until `structures` is known, then `2 + structures.length + 2`; done = pre-steps passed by `stage` + number of structures with `done === true` + post-steps passed; all complete on Completed.
  - Alien: `total` = `progress.maxIterations` (5 when missing); done = `iterations.length`.
  - Debris: `total` = `progress.maxSteps` (15 when missing); done = `decisions.length`.
- Note for Debris and Alien (loops): when `done` exceeds `total` the engine clamps; do not change the demo data.

- [ ] **Step 1: Copy the four shared files** from AnomalyAnalysis into the five projects with `cp`; run `md5 -q */*.Web/wwwroot/js/engine.js | sort -u | wc -l` (and util.js, lcars.css, and `appsettings.json` for the Web projects) → `1` each.
- [ ] **Step 2: Add the markup and `checkpoints` hook** to each demo as specified; update nothing else in those files.
- [ ] **Step 3: Fabricated checks (Procedure B), per demo:** beads follow fabricated progress including partial/malformed progress and null; the outage caption/red state and the restored caption behave as in Task 1 (one outage scenario per demo is enough); the demo's existing check script that works without the backend (`galactic-demo.mjs`, `starship-demo.mjs`, `colony-demo.mjs`, `alien-demo.mjs`, `debris-demo.mjs`) still passes. Debris approval panel and Alien/Debris live regions are unaffected.
- [ ] **Step 4: Build all five solutions** (`dotnet build <Name>/<Name>.sln`, 0 errors) and re-run the md5 check. Expected: `Build succeeded.` x5, md5 distinct count 1 for each shared file.

---

### Task 3: AnomalyAnalysis conduit packet

**Files:**
- Modify: `AnomalyAnalysis/AnomalyAnalysis.Web/wwwroot/index.html`, `js/demo.js`; Create: `css/demo.css` (+ `<link>` after `lcars.css`)

**Interfaces:**
- Consumes: Task 1 (`runState`, `reveal`); `progress` = `[{ stageName, output, ... }]` in stage order.
- Produces: an `aria-hidden` conduit above the stage list: five nodes on a line; a packet sits at the node of the current stage (first stage not yet in `progress`) and moves to the next when a stage completes; a completed node lights; the first ~24 characters of that stage's cleaned `output` decode in beneath the node (`reveal` with `mode: "decode"`), only for stages that appear in a render after the first of the run. While the run is not Running/Pending (failed/terminated/completed) the packet stops (no animation), and on Completed it rests at the last node.

- [ ] **Step 1: Implement** markup (nodes `data-stage` matching the five activity names), styles and the logic in `renderProgress` (reuse positional elements, write DOM only when changed).
- [ ] **Step 2: Verify (Procedure B)** with fabricated progress: packet moves node by node, outputs decode once and are complete afterwards, a run whose first render already holds 3 stages shows them instantly, a Failed status stops the packet, restart clears everything, reduced motion shows final states, the stage list and its live region are untouched (MutationObserver on `#stage-list` shows no extra mutations from the animation). Take and look at screenshots (running, completed).
  Expected: all PASS; no console errors.
- [ ] **Step 3: Build** `dotnet build AnomalyAnalysis/AnomalyAnalysis.sln` → `Build succeeded.`

---

### Task 4: GalacticAnomalyClassifier confidence arc and track switch

**Files:**
- Modify: `GalacticAnomalyClassifier/GalacticAnomalyClassifier.Web/wwwroot/index.html`, `js/demo.js`, `css/demo.css`

**Interfaces:**
- Consumes: `progress` = `{ stage: "Classifying" | "Analyzing", type?, confidence? }` (confidence 0-1); the existing lane elements keyed by `data-type`.
- Produces: an `aria-hidden` arc beside the classifier node whose fill animates to `confidence` (CSS custom property on the arc; `@property` registered with a plain fallback) when `stage` becomes Analyzing; a lever element between the classifier and the lanes that rotates to the chosen lane's row (existing lane highlight stays); an unknown or unmatched `type` leaves the lever centred; while the run is not Running/Pending the animations stop in their final state; confidence outside 0-1 or non-numeric is clamped/ignored without throwing.

- [ ] **Step 1: Implement** markup, styles and logic (no change to the lane/node state logic and its live regions).
- [ ] **Step 2: Verify (Procedure B)** for each of the five types, Unknown, confidence 0, 0.92, 1.4, `"x"`, null progress, a Failed run after Analyzing; arc and lever end in the right state; first-render-instant rule; restart clears; reduced motion. Screenshots looked at (mid-run and done).
  Expected: all PASS.
- [ ] **Step 3: Build** `dotnet build GalacticAnomalyClassifier/GalacticAnomalyClassifier.sln` → `Build succeeded.`

---

### Task 5: StarshipDiagnostics radar tiles and ping

**Files:**
- Modify: `StarshipDiagnostics/StarshipDiagnostics.Web/wwwroot/js/demo.js`, `css/demo.css` (and `index.html` only if a decorative element is needed)

**Interfaces:**
- Consumes: the existing tiles (`data-activity`), `setTile(tile, scan, running)` states, `progress.scans[]`.
- Produces: a tile that is waiting for its scan while the run is Running/Pending shows a rotating radar sweep (pure CSS, decorative, stops when the scan arrives or the run ends); when a tile's scan first appears in a render after the first of the run, a one-shot ping ripple plays and the health bar width animates (CSS transition from 0 to the value) instead of snapping; the first render of a run shows tiles instantly; tile order of completion is whatever the data says. The vote panel is unchanged.

- [ ] **Step 1: Implement** the sweep, ping and bar transition without rewriting existing text per poll (write only on change; the `#scan-board` live region must not get extra mutations).
- [ ] **Step 2: Verify (Procedure B)** with fabricated progress where tiles complete in scrambled order, two scans arriving in one poll, a Failed run mid-scan (sweeps stop), a restart (no ping/transition class left), reduced motion; MutationObserver on `#scan-board` shows no mutations on identical polls. Screenshots looked at.
  Expected: all PASS.
- [ ] **Step 3: Build** `dotnet build StarshipDiagnostics/StarshipDiagnostics.sln` → `Build succeeded.`

---

### Task 6: SpaceColonyPlanner orbiting workers and building blocks

**Files:**
- Modify: `SpaceColonyPlanner/SpaceColonyPlanner.Web/wwwroot/index.html`, `js/demo.js`, `css/demo.css`

**Interfaces:**
- Consumes: `progress.structures[]` (`done` flags, any order), the existing positional worker lanes and `stage`.
- Produces: an `aria-hidden` mini scene (planet plus one orbiting dot per structure, capped at 8 dots, hidden while there are no structures) where a dot stops and turns solid when its structure is `done`; each worker lane gains a row of 6 blocks: while its worker is planning and the run is Running/Pending an indeterminate shimmer moves across the blocks (no percentage), on `done` the blocks fill with a short stagger (only for a lane completing in a later render than the first; otherwise instantly); lane/stage live regions get no extra mutations; Failed/Terminated runs stop shimmer and orbit.

- [ ] **Step 1: Implement** scene markup/styles/logic keyed positionally to the existing lane reuse.
- [ ] **Step 2: Verify (Procedure B)** with 0, 3, 6 and 11 structures, done flags flipping in any order, malformed entries, a Failed run mid-planning, restart; blocks never show a fake percentage; reduced motion; MutationObserver on the worker board. Screenshots looked at.
  Expected: all PASS.
- [ ] **Step 3: Build** `dotnet build SpaceColonyPlanner/SpaceColonyPlanner.sln` → `Build succeeded.`

---

### Task 7: AlienTranslator decode reveal and quality gauge

**Files:**
- Modify: `AlienTranslator/AlienTranslator.Web/wwwroot/js/demo.js`, `css/demo.css`, `index.html` (if the translation text needs a container)

**Interfaces:**
- Consumes: `reveal` (decode mode), `runState`, `progress.iterations[]` (`translatedText`, `overallQuality`, `meetsStandards`), the existing quality bar custom properties and threshold marker, and the result view's final translation.
- Produces: when an iteration first appears in a render after the first of the run, its translation text decodes from scrambled glyphs into the final text (inside the expandable text element; the live region `#iteration-board` is not mutated per frame: the accessible text is written once through `reveal`'s `.sr-only` span); the quality bar width animates to the score (CSS transition) and the verdict (`MEETS STANDARD` / `BELOW STANDARD`) appears after the bar settles (CSS delay, no JS timer); the final translation in the result view decodes once when the completed view is shown. Earlier iterations render instantly; reduced motion shows everything final.

- [ ] **Step 1: Implement** (keep the existing guarded `setText/setClass/setVar` writes; reveal only on first appearance, keyed by iteration index).
- [ ] **Step 2: Verify (Procedure B):** fabricated 1-5 iterations arriving one per poll and two at once, a first render with 3 iterations (instant), a long text (reveal duration capped, final text complete), restart mid-reveal (no partial text), Failed run mid-iteration, `result` view reveal; MutationObserver on `#iteration-board` shows no per-frame mutations; screen-reader text appears exactly once per iteration. Screenshots looked at.
  Expected: all PASS.
- [ ] **Step 3: Build** `dotnet build AlienTranslator/AlienTranslator.sln` → `Build succeeded.`

---

### Task 8: SpaceDebrisAgent thought-to-action trace

**Files:**
- Modify: `SpaceDebrisAgent/SpaceDebrisAgent.Web/wwwroot/js/demo.js`, `css/demo.css`

**Interfaces:**
- Consumes: `reveal` (type mode), `runState`, `progress.decisions[]` (`step`, `action`, `reasoning`) rendered positionally by `renderLog`/`setEntry`, the existing thinking row and approval panel.
- Produces: when a decision first appears in a render after the first of the run, its reasoning is typed out (speed scaled to length, about 2 s max, via `reveal` so the live region `#decision-log` gets the full text once) and then the action pill pulses once (CSS animation class removed after it ends, no JS timer leak); older entries render instantly; the thinking indicator, approval panel (focus, announcement, countdown, single timer) and result view are unchanged; a Failed/Terminated run or a reset finishes any running reveal with the full text.

- [ ] **Step 1: Implement** without changing the approval panel logic or the positional reuse (open `<details>` stay open).
- [ ] **Step 2: Verify (Procedure B):** fabricated decisions arriving one per poll and several at once, a first render with 6 decisions (instant), a very long reasoning, an unknown action, reset mid-typing, Failed run; approval panel checks from `debris-demo.mjs` still pass (59 expected); MutationObserver on `#decision-log`: no per-frame mutations.
  Expected: all PASS.
- [ ] **Step 3: Build** `dotnet build SpaceDebrisAgent/SpaceDebrisAgent.sln` → `Build succeeded.`

---

### Task 9: Docs and final verification

**Files:**
- Modify: each demo's `README.md` ("Use the web UI" section: the durable timeline, the demo's signature animation, the crash demo steps), root `README.md` (one sentence), `AGENTS.md` (front-end rule: animations are data-driven, reduced-motion safe, shared files copied identically)

**Interfaces:** none (documentation).

- [ ] **Step 1: Update the docs** accurately (wording must match the real UI: names, what moves, how to stage the crash: stop `wf-app` in the Aspire dashboard mid-run, start it again; mention the ~5 s detection).
- [ ] **Step 2: Final checks:** `for p in AlienTranslator AnomalyAnalysis GalacticAnomalyClassifier SpaceColonyPlanner SpaceDebrisAgent StarshipDiagnostics; do dotnet build $p/$p.sln; done` → `Build succeeded.` x6; md5 distinct count 1 for `engine.js`, `util.js`, `lcars.css` and `appsettings.json` across the six Web projects; every `Resources/` folder holds only `conversation-openai.yaml`, `env-secret.yaml`, `statestore.yaml` with no diff; no `bin/obj` untracked; no running processes left.
- [ ] **Step 3: Real crash on two more demos** (Procedure A and C): SpaceDebrisAgent (long loop, ghost beads) and StarshipDiagnostics (parallel scans); record link-lost and restored timings and what the captions said; note any demo not run live (the flaky local model may fail a run: retry, and say so).

---

## Self-Review Notes

- **Spec coverage:** shared durable timeline with crash/resume captions, frozen end state, red state without an active bead, count wording and first-render rule (Tasks 1-2); fast-failure fix (Task 1, YARP and engine); signature animations for all six demos (Tasks 3-8); reasoning reveals for Debris (A), Alien (C) and Anomaly stage outputs (Task 3); accessibility/reduced-motion and live-region constraints (Global Constraints, every task's checks); verification incl. real crash (Tasks 1 and 9); docs (Task 9). The "replay run" idea and retry visualisation are deliberately not planned (spec non-goals).
- **Type consistency:** `runState`, `reveal`, `#durable`, `#durable-caption` and `checkpoints(status, progress)` are defined in Task 1 and used unchanged in Tasks 2-8; the shared files stay byte-identical after Task 2.
- **Tests:** the repo has no test projects; each task ends in fabricated browser checks (and real runs where stated) with expected results, plus a build.
