# LCARS animations for the six demo UIs: design

**Date:** 2026-10-18
**Scope:** Add meaningful animation to the Star Trek TNG (LCARS) web UIs that already exist in the six Aspire demos (AlienTranslator, AnomalyAnalysis, GalacticAnomalyClassifier, SpaceColonyPlanner, SpaceDebrisAgent, StarshipDiagnostics).

Background: each demo has a `<Name>.Web` project (vanilla JS, served on its own port 5501-5506) that starts the workflow, polls the status endpoint every 2 s and renders progress and the result. The shared parts are copies of `engine.js`, `util.js` and `lcars.css`; each demo owns `index.html`, `js/demo.js` and `css/demo.css`. See `docs/superpowers/specs/2026-10-18-aspire-demos-frontends-design.md` and the `progress` shape each ApiService publishes (readable in each `demo.js`).

## Intent and why

The demos are shown live to an audience. The UI should make the two points of the talk visible without anyone explaining them:

1. **Durability.** Dapr Workflow saves the state of a run after every step, so when the application dies in the middle of a run, nothing that already finished is lost and the run carries on when the application comes back.
2. **Reasoning.** In the demos where an LLM decides or evaluates something (the debris agent's decisions, the translator's quality loop, the stage outputs), the audience should see the reasoning arrive and what it led to.

Each demo also gets one signature animation that shows its workflow pattern (sequence, routing, parallel scans, dynamic fan-out, evaluator loop, agent loop). Every animation must be driven by real data from the status endpoint.

## How the presenter demos the crash (decided)

The presenter stops only the ApiService resource (`wf-app`) from the Aspire dashboard mid-run, waits, and starts it again. The web page and the Dapr sidecar stay up, so the page keeps polling and sees failed polls (the proxy answers with an error) until the app is back. The page must tell this story: link lost, state is safe, link restored, run continues. The presenter does not kill the whole `aspire run`.

## Goals

- A shared **durable timeline** (checkpoint beads with crash and resume behaviour) in all six demos.
- One **signature animation** per demo (catalogue below).
- Reasoning reveals in the demos that have reasoning text (Debris, Alien, Anomaly stage outputs).
- All of it subtle enough not to distract from the content, and gone (no motion) once a run has ended, apart from the final reveal finishing.

## Non-goals

- No sound, no new libraries (vanilla CSS/JS, small inline SVG allowed), no canvas engine.
- No server or workflow changes (ApiServices and their progress shapes stay as they are). Retry visualisation is out because Dapr does not report retry attempts in the status.
- No "replay run" button (an idea for later).
- No change to what the demos compute or to the result views.
- The AppHost, ports and Dapr components are untouched.

## Anti-goals (failure even if it technically works)

- **Faking progress.** No percentage bars or counters that move on a timer. Indeterminate "working" indicators are fine; anything that implies a measured value must come from data.
- **Claiming durability the UI cannot observe.** Captions must be accurate: after a crash, steps that already finished are not repeated, but the step that was running at the moment of the crash starts again. Say "resumed after checkpoint N", never "no work repeated".
- Animations that keep running after a run ended, leak timers across runs, or fight with the live-region and focus handling already in place.
- Anything that makes a screen reader announce text repeatedly (the iteration board in AlienTranslator already needed a fix for this; write DOM text only when it changed).

## Constraints

- Vanilla JS and CSS. Server text still only via `textContent` and `clean()`, never `innerHTML`.
- Respect `prefers-reduced-motion`: show the final state immediately, no animation.
- Keep the polling model (setTimeout chain). Animations are visual only and must not change when or how often the engine polls.
- Solutions stay independent (duplication across the six projects is intended). Shared helpers are copied into each project like `engine.js` is today.
- Existing demo checks and conventions stay valid: `window.renderResult`, the required and optional element ids listed in the `engine.js` header comment, positional row reuse so open `<details>` stay open.

## Decisions (made by the presenter)

| Area | Choice |
|------|--------|
| Durability visual | **Crash and resume**: a timeline of beads, one per workflow step, "saved" flash on each new bead; link lost freezes the beads (red), link restored continues from the last bead with a "resumed" caption. In all six demos. |
| Reasoning visual | **A. Thought to action trace** for SpaceDebrisAgent: each step's reasoning types out, then the chosen action pill lights up; **C. Decode reveal with a quality gauge** for AlienTranslator. |
| AnomalyAnalysis | **Conduit packet**: a packet travels through the five stages; each stage lights up when done and its output decodes in beneath it. |
| GalacticAnomalyClassifier | **Confidence arc + track switch**: the confidence fills as an arc, then a rail switch throws to the chosen specialist track. |
| StarshipDiagnostics | **Radar tiles + ping**: each subsystem tile sweeps while scanning; on completion a ping ripple and the health bar fills; tiles finish in any order. |
| SpaceColonyPlanner | **Orbiting workers + building blocks**: one orbiting dot per structure chosen (count varies per planet); each lane builds blocks and finishes at its own pace. |

Visual references (live animations, not production code) are in `.superpowers/brainstorm/39253-1792356246/content/`: `durability.html` (option B), `reasoning.html` (A and C), `others-1.html` (Anomaly 1, Galactic 4), `others-2.html` (Starship 1, Colony 3). They no longer exist on disk (deleted when the brainstorm server was stopped): the descriptions in this spec are authoritative.

## Design: shared durable timeline

- A strip of beads joined by segments, rendered by the engine into an optional element (suggested id `durable`, placed in the progress panel above the stage list; markup in each `index.html`). The engine is null-safe when it is absent, like the other optional ids.
- Each demo supplies an optional hook that tells the engine how many checkpoints are done and how many there are (my call on the shape: `checkpoints(status, progress)` returning `{ done, total }`, with `total` null when unknown and extra "ghost" beads when the maximum is known).
- A new bead lights with a short "saved" flash (about 1.5 s), only when the UI observes the checkpoint appearing during the run.
- **Crash and resume (engine-level, no server change):** when polls fail (the existing link-lost condition), the active bead turns red, the segment stops, a caption reads "link lost, state is safe" with a running "down mm:ss". When polling succeeds again while the run is still Running, the caption reads "link restored, resumed after checkpoint N" for a few seconds, then fades; beads continue. If the run turns out to have finished while the link was down, show the normal finished state.
- The strip resets on a new run and on restart.
- Checkpoint mapping per demo (my calls): Anomaly: 5 beads, done = number of stages in progress. Galactic: 2 beads (classify, specialist). Starship: 5 scan beads plus a report bead (votes are shown in the vote panel, not as beads). Colony: 2 pre-steps + one bead per structure + 2 post-steps, total unknown until the structures are determined. Alien: one bead per iteration, ghost beads up to `maxIterations`. Debris: one bead per decision, ghost beads up to `maxSteps`.

## Design: signature animations (data sources are the existing `progress` and `result`)

- **AnomalyAnalysis, conduit packet.** Five nodes on a line. A packet sits at the current stage and moves to the next node when a stage completes; each completed node lights and the first part of that stage's `output` (cleaned) decodes in beneath it (the full text stays in the existing expandable rows).
- **Galactic, confidence arc and track switch.** When `progress.stage` becomes Analyzing, an arc beside the classifier node fills to `progress.confidence`; a lever then throws to the lane for `progress.type`, which lights; the other lanes stay dim. Unknown type: the arc fills, the lever stays centred.
- **Starship, radar tiles.** A tile with no result yet shows a rotating radar sweep (only while the run is Running/Pending); when its scan appears in `progress.scans` the sweep stops, a ping ripple plays once and the health bar animates to its value. Tiles are keyed by `activity`, as today.
- **Colony, orbiting workers.** A small planet with one orbiting dot per entry in `progress.structures`; the dot stops and turns solid when its structure is `done`. Each lane shows a row of blocks: while planning, an indeterminate shimmer moves across them (no fake percentage); on completion the blocks fill with a short stagger.
- **Alien, decode and quality gauge.** When a new iteration appears, its translation text reveals by decoding from scrambled glyphs, and the quality bar animates to the score with the existing threshold marker; the verdict appears after the bar settles. The final result view reveals the final translation the same way.
- **Debris, thought to action trace.** When a new decision appears, its reasoning is revealed (typewriter, speed scaled to length, capped around 2 s), then the action pill pulses once. Older entries render instantly. The existing "thinking" indicator stays. The approval panel behaviour (focus, announcement, countdown) does not change.

Reveal effects only play for data that arrives while watching: when a page renders an already finished list (for example after a restart of the view), show the final text immediately. The reasoning text only exists once a step has finished, so the reveal is an animation of finished text, not live token streaming.

## Calls settled after the builder review (my calls, marked as such)

- **Debris ghost beads:** the step limit is already published: `progress.maxSteps` (15) in the status. Use it. Alien uses `progress.maxIterations`. (Answered by the project.)
- **Where the strip lives and what it does at the end:** it sits in the progress panel, which stays visible below the findings in the completed view and alone in the failed view, so the strip stays visible, frozen (all beads saved, no animation), after a run ends. The durability story stays on screen at the end of the demo.
- **Red state with no active bead (my call):** the engine always draws one leading "next" bead after the saved ones when `total` is unknown, when nothing is done yet, or when everything is done; the link-lost red state goes on that bead (or on the last bead when all are done). The caption is always shown. With nothing saved yet the resumed caption reads "link restored, run continues (no checkpoint saved yet)".
- **Parallel finishes (my call):** checkpoints are counts, not an ordered sequence. The caption says "link restored, resumed with N of M checkpoints saved" (M omitted when unknown), never "after checkpoint N", for Starship (scans) and Colony (structures) and, for consistency, for all demos.
- **What counts as "arrives while watching" (my call):** the first successful render of a run shows what is there without animation (for example a run that finished between two polls). Every later render animates items that are new compared with the previous render of the same run, including items that appear right after a link-lost gap (beads flash, text reveals play).
- **Checkpoint hook (my call):** the engine owns the new-bead detection: it calls the demo's `checkpoints(status, progress)` after every successful poll, compares `done` with the previous value, and resets on a new run or restart.

## Decided: the crash must be noticed quickly (found by testing; the presenter approved including the fix)

**Decision:** include the fast-failure fix. The presenter did not choose between the two ways, so my call is to do both: the YARP `HttpRequest.ActivityTimeout` of about 5 s in each Web project's `appsettings.json` (the primary fix, no JavaScript change) and an `AbortController` timeout of about 5 s on each poll in `engine.js` as a safety net. Both treat a slow or hung request as a failed poll, using the existing link-lost path. The builder verifies it with a real `wf-app` stop and start and checks that the start POST and normal polls are unaffected.

**What was found:**

I tested the presenter's crash scenario for real (`aspire resource wf-app stop` / `start` on AnomalyAnalysis, with the Web page's proxy):

- While `wf-app` is stopped, an API call through the Web proxy **does not fail**: it hangs (no response in 40 s). The endpoint 5500 stays open (Aspire's endpoint proxy accepts the connection and waits), so the YARP forwarder just waits for its default 100 s activity timeout. The engine's polls therefore stall instead of failing, and **the link-lost banner (and with it the whole crash story) would not appear for up to about 100 s**.
- After `wf-app` starts again, new calls work immediately (404 for an unknown id, as normal): no stale Running status and no 404 for existing runs was observed.
- Recommended fix, small and not an animation: make a hung poll count as a failed poll. Two ways, either or both: (a) in each Web project's `appsettings.json`, set the YARP cluster's `HttpRequest.ActivityTimeout` to about 5 seconds, so the proxy answers with a 504 quickly and the existing link-lost path is used unchanged; (b) in `engine.js`, give each poll an `AbortController` timeout of about 5 seconds so a hung request is treated like a network error. (a) changes no JavaScript; (b) works regardless of the proxy. The builder should verify the chosen setting with a real stop and start of `wf-app`, and that normal calls (including the start POST) are unaffected.
- This is a prerequisite for the durability animation to show anything during the demo. It changes polling behaviour slightly (a request that takes more than about 5 s is reported as link lost); the presenter approved this.
- This is the one deliberate exception to the "animations must not change polling" constraint above: it is a fix, not an animation, and it belongs to the durability feature.

## Accessibility and performance

- `prefers-reduced-motion`: reveal helpers write the final text immediately, no sweeps, pings, pulses or orbits.
- Live regions stay quiet: write DOM text only when it changed; animated decoration is `aria-hidden` and outside live regions.
- Timers and intervals started by an animation are cleared on run reset and when a run ends.
- CSS animation preferred over JS where possible; JS only for text reveals and state changes.

## Verification (suggested)

The existing approach works: serve a Web project alone, drive it with headless Chrome and fabricate status responses, take screenshots. Specific checks: beads advance with progress; fabricated poll failures freeze the beads and show the caption, and restored polls continue and show "resumed after checkpoint N"; reduced-motion renders final states; no animation or timer survives a finished run or a restart; the demos' existing checks still pass. At least one real run per demo with the actual crash (stop `wf-app` from the Aspire dashboard mid-run, start it again) is worth doing before the demo.

## Left to the builder

Exact timings, colours, easing, markup, class and helper names, the exact checkpoint hook signature, where the shared reveal helpers live (my suggestion: a small addition to `util.js`, copied into all six projects like today), how to draw the ring/arc/orbit (CSS or inline SVG), and any extra polish within the constraints above.
