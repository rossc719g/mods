"use strict";
const el = (id) => document.getElementById(id);
const controls = new Map();
let latest = null;
let busy = false;
let requestedRevision = 0;
let lastSession = null;
let stopping = false;
let stopped = false;
let pollTimer;
let pollRunning = false;

function error(message) { el("error").textContent = message; el("error").hidden = !message; }
function lock(value) {
  busy = value;
  const disabled = value || stopping || stopped || !latest;
  el("master").disabled = disabled;
  el("all-off").disabled = disabled;
  el("defaults").disabled = disabled;
  el("stop-web").disabled = disabled || !latest?.capabilities?.stopWeb;
  for (const input of controls.values()) input.disabled = disabled;
}
function build(effects) {
  const groups = new Map();
  el("groups").replaceChildren();
  controls.clear();
  for (const effect of effects) {
    if (!groups.has(effect.group)) {
      const section = document.createElement("section");
      const heading = document.createElement("h2");
      heading.textContent = effect.group;
      const cards = document.createElement("div"); cards.className = "cards";
      section.append(heading, cards); el("groups").append(section); groups.set(effect.group, cards);
    }
    const card = document.createElement("div"); card.className = "effect";
    const label = document.createElement("label"); label.className = "switch-row";
    const text = document.createElement("span");
    const name = document.createElement("strong"); name.textContent = effect.label;
    const description = document.createElement("span"); description.className = "description"; description.textContent = effect.description;
    description.id = "description-" + effect.id;
    const input = document.createElement("input"); input.type = "checkbox"; input.setAttribute("role", "switch");
    input.setAttribute("aria-label", "Remove " + effect.label); input.setAttribute("aria-describedby", description.id);
    input.id = "effect-" + effect.id;
    input.addEventListener("change", () => change({ effects: { [effect.id]: input.checked } }));
    text.append(name, description); label.append(text, input); card.append(label); groups.get(effect.group).append(card);
    controls.set(effect.id, input);
  }
  el("groups").setAttribute("aria-busy", "false");
}

function renderDiagnostics(state) {
  let panel = el("diagnostics-panel");
  if (!state.capabilities?.renderDiagnostics) { panel?.remove(); return; }
  if (!panel) {
    panel = document.createElement("details"); panel.id = "diagnostics-panel";
    const summary = document.createElement("summary"); summary.textContent = "Rendering stats";
    const content = document.createElement("pre"); content.id = "diagnostics";
    panel.append(summary, content);
    document.querySelector("footer").before(panel);
  }
  const stats = state.diagnostics;
  if (!stats) { el("diagnostics").textContent = "Waiting for a rendering snapshot…"; return; }
  el("diagnostics").textContent = [
    `Scene: ${stats.scene || "loading"} · frame ${stats.frame}`,
    `Snapshot: ${stats.capturedAtUtc}`,
    `Settings: requested ${Math.max(state.settings.revision, requestedRevision)}, rendered ${stats.renderedRevision}, saved ${state.persistedRevision}`,
    `Camera updates: ${stats.hooks.camerasProcessed} · render passes checked: ${stats.hooks.passesSeen}`,
    ...stats.hooks.errors,
    ...(stats.hooks.lastError ? [stats.hooks.lastError] : []),
    "",
    ...state.effects.map((effect) => {
      const value = stats.effects[effect.id];
      return `${effect.label}: ${value.volumeFound ? `${value.originalValue} → ${value.effectiveValue}` : "no volume observed"}; ${value.valuesSuppressed} values suppressed, ${value.passesSuppressed} screen passes skipped${value.error ? "; ERROR: " + value.error : ""}`;
    }),
  ].join("\n");
}

function render(state) {
  if (stopping || stopped) return;
  if (!state.ready) { el("connection").textContent = "Waiting for the controls…"; return; }
  if (state.sessionId !== lastSession) {
    lastSession = state.sessionId;
    requestedRevision = 0;
  }
  latest = state;
  el("web-controls").hidden = !state.capabilities?.stopWeb;
  if (controls.size === 0) build(state.effects);
  if (!busy && state.settings.revision >= requestedRevision) {
    el("master").checked = state.settings.enabled;
    for (const effect of state.effects) controls.get(effect.id).checked = effect.remove;
  }
  el("master-help").textContent = el("master").checked
    ? "Checked effects below are removed while this is on."
    : "Original effects are allowed. Your individual choices are saved.";
  let message = "Connected · settings saved";
  let level = "ok";
  if (state.persistenceError) { message = "Settings changed, but saving failed: " + state.persistenceError; level = "warn"; }
  else if (busy || state.settings.revision < requestedRevision || state.persistedRevision < Math.max(state.settings.revision, requestedRevision)) {
    message = "Connected · saving settings…"; level = "warn";
  }
  el("connection").textContent = message; el("connection").dataset.level = level;
  el("version").textContent = state.version;
  renderDiagnostics(state);
  lock(busy);
}

async function change(patch) {
  lock(true); error("");
  try {
    const response = await fetch("/api/settings", { method: "PATCH", headers: { "Content-Type": "application/json", "X-ClearWaters": "1" }, body: JSON.stringify(patch), signal: AbortSignal.timeout(5000) });
    if (!response.ok) throw new Error(await response.text());
    const answer = await response.json(); requestedRevision = answer.settings.revision;
    el("master").checked = answer.settings.enabled;
    for (const [id, remove] of Object.entries(answer.settings.effects)) controls.get(id).checked = remove;
  } catch (e) { error("Could not change the switches: " + e.message); requestedRevision = 0; }
  finally { lock(false); if (latest) render(latest); }
}

el("master").addEventListener("change", () => change({ enabled: el("master").checked }));
el("all-off").addEventListener("click", () => {
  if (latest) change({ effects: Object.fromEntries(latest.effects.map((e) => [e.id, false])) });
});
el("defaults").addEventListener("click", () => {
  if (latest) change({ effects: Object.fromEntries(latest.effects.map((e) => [e.id, e.defaultRemove])) });
});
el("stop-web").addEventListener("click", async () => {
  stopping = true; clearTimeout(pollTimer); lock(true); error("");
  el("connection").textContent = "Saving settings and stopping web controls…";
  try {
    const response = await fetch("/api/server/stop", { method: "POST", headers: { "Content-Type": "application/json", "X-ClearWaters": "1" }, body: "{}", signal: AbortSignal.timeout(5000) });
    if (!response.ok) throw new Error(await response.text());
    const answer = await response.json();
    if (!answer.stopped) throw new Error("The game did not confirm shutdown.");
    stopped = true;
    el("connection").textContent = "Web controls stopped · settings saved";
    el("connection").dataset.level = "ok";
    el("stop-result").textContent = "Your settings are saved. The game continues with your chosen effects. You can close this page.";
    el("stop-result").hidden = false;
    el("stop-web").textContent = "Web controls stopped";
  } catch (e) {
    error("Could not confirm that the web controls stopped: " + e.message);
  } finally {
    stopping = false; lock(false);
    if (!stopped) poll();
  }
});

async function poll() {
  if (stopping || stopped || pollRunning) return;
  pollRunning = true;
  try {
    const response = await fetch("/api/state", { cache: "no-store", signal: AbortSignal.timeout(4000) });
    if (!response.ok) throw new Error("HTTP " + response.status);
    render(await response.json());
  } catch (_) {
    if (stopping || stopped) return;
    el("connection").textContent = "Disconnected · start Dave the Diver to reconnect";
    el("connection").dataset.level = "warn";
    el("master").disabled = true; el("all-off").disabled = true; el("defaults").disabled = true; el("stop-web").disabled = true;
    for (const input of controls.values()) input.disabled = true;
  } finally {
    pollRunning = false;
    clearTimeout(pollTimer);
    if (!stopping && !stopped) pollTimer = setTimeout(poll, 1000);
  }
}
poll();
