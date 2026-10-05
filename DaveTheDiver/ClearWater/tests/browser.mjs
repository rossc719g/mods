import { chromium } from "playwright";
import assert from "node:assert/strict";
import { mkdir, readFile } from "node:fs/promises";

const expectDiagnostics = process.env.EXPECT_RENDER_DIAGNOSTICS === "1";
const screenshotPrefix = expectDiagnostics ? "diagnostics-" : "";
const browser = await chromium.launch({ executablePath: process.env.CHROME_PATH || "/usr/bin/google-chrome", headless: true, args: ["--no-sandbox"] });
await mkdir("test-results", { recursive: true });
try {
  for (const [name, viewport] of [["desktop", { width: 1280, height: 1000 }], ["phone", { width: 390, height: 844 }]]) {
    const page = await browser.newPage({ viewport });
    const failures = [];
    page.on("pageerror", (e) => failures.push(e.message));
    await page.goto("http://localhost:18781");
    const icon = await page.locator('link[rel="icon"]').getAttribute("href");
    const iconResponse = await page.request.get(new URL(icon, page.url()).href);
    assert.equal(iconResponse.status(), 200, "embedded favicon is available");
    assert.match(iconResponse.headers()["content-type"], /^image\/svg\+xml\b/, "favicon is served as an SVG image");
    await page.waitForFunction(() => document.querySelectorAll('input[role="switch"]').length === 22);
    const master = page.locator("#master");
    await master.setChecked(true);
    await page.waitForFunction(() => document.querySelector("#connection").textContent.includes("settings saved"));
    assert.equal(await page.locator("#diagnostics").count(), expectDiagnostics ? 1 : 0, "statistics panel follows the build capability");
    if (expectDiagnostics) {
      await page.locator("#diagnostics-panel summary").click();
      assert.match(await page.locator("#diagnostics").textContent(), /UI test fixture · frame 42/, "diagnostic build displays its snapshot");
      assert.match(await page.locator("#diagnostics").textContent(), /Chromatic aberration: 0.2 → 0/, "diagnostic build displays effect values");
    }
    await page.locator("#defaults").click();
    await page.waitForFunction(() => document.querySelector("#effect-colorSplit").checked);
    await page.locator("#all-off").click();
    await page.waitForFunction(() => [...document.querySelectorAll('#groups input')].every((input) => !input.checked));
    assert.equal(await master.isChecked(), true, "all-off preserves master");
    await page.locator("#effect-chromaticAberration").check();
    await page.waitForFunction(() => document.querySelector("#connection").textContent.includes("settings saved"));
    assert.equal(await page.locator('#groups input:checked').count(), 1, "one effect can be tested alone");
    await page.locator("#defaults").click();
    await page.waitForFunction(() => document.querySelector("#effect-colorSplit").checked);
    await page.locator("#effect-colorSplit").uncheck();
    await page.waitForFunction(() => document.querySelector("#connection").textContent.includes("settings saved"));
    await page.reload();
    await page.locator("#effect-colorSplit").waitFor();
    assert.equal(await page.locator("#effect-colorSplit").isChecked(), false, "individual choice survives refresh");
    await master.uncheck();
    await page.waitForFunction(() => document.querySelector("#master-help").textContent.includes("Original effects"));
    await master.check();
    await page.waitForFunction(() => document.querySelector("#connection").textContent.includes("settings saved"));
    assert.equal(await page.locator("#effect-colorSplit").isChecked(), false, "master preserves individual choice");
    assert.equal(await page.locator("#effect-bloom").isChecked(), false, "bloom preserved");
    await page.route("**/api/server/stop", (route) => route.fulfill({ status: 503, body: "Could not save the configuration" }));
    await page.locator("#stop-web").click();
    await page.waitForFunction(() => document.querySelector("#error").textContent.includes("Could not save"));
    assert.equal(await master.isEnabled(), true, "failed shutdown leaves controls available");
    assert.equal(await page.locator("#stop-result").isHidden(), true, "failed shutdown is not presented as success");
    await page.unroute("**/api/server/stop");
    await page.locator("#defaults").click();
    await page.waitForFunction(() => document.querySelector("#connection").textContent.includes("settings saved"));
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true, "no horizontal overflow");
    await page.screenshot({ path: `test-results/${screenshotPrefix}${name}.png`, fullPage: true });
    assert.deepEqual(failures, [], "no browser errors");
    await page.route("**/api/state", (route) => route.abort());
    await page.waitForFunction(() => document.querySelector("#connection").textContent.includes("Disconnected"));
    assert.equal(await master.isDisabled(), true, "offline controls disabled");
    await page.unroute("**/api/state");
    await page.waitForFunction(() => !document.querySelector("#master").disabled);
    // Save errors remain visible after rendering diagnostics have been removed.
    await page.route("**/api/state", async (route) => {
      const response = await route.fetch();
      const state = await response.json();
      state.persistenceError = "Configuration is read-only";
      await route.fulfill({ response, json: state });
    });
    await page.waitForFunction(() => document.querySelector("#connection").textContent.includes("saving failed"));
    assert.equal(await master.isEnabled(), true, "save failure leaves switches usable");
    await page.unroute("**/api/state");
    await page.waitForFunction(() => document.querySelector("#connection").textContent.includes("settings saved"));
    // A fresh game process starts revisions again; the open page must accept it.
    await page.route("**/api/state", async (route) => {
      const response = await route.fetch();
      const state = await response.json();
      state.sessionId = "new-game-session";
      state.settings.revision = state.persistedRevision = 1;
      state.settings.enabled = false;
      await route.fulfill({ response, json: state });
    });
    await page.waitForFunction(() => !document.querySelector("#master").checked);
    await page.unroute("**/api/state");
    await page.close();
    console.log(`${name}: switches, refresh, master, glow, layout, failed shutdown, offline and reconnect passed`);
  }
  const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
  await page.goto("http://localhost:18781");
  await page.waitForFunction(() => !document.querySelector("#stop-web").disabled);
  await page.locator("#master").check();
  await page.locator("#effect-colorSplit").uncheck();
  await page.waitForFunction(() => document.querySelector("#connection").textContent.includes("settings saved"));
  let polls = 0;
  page.on("request", (request) => { if (request.url().endsWith("/api/state")) polls++; });
  await page.locator("#stop-web").click();
  await page.waitForFunction(() => document.querySelector("#connection").textContent.includes("Web controls stopped"));
  assert.equal(await page.locator("#master").isChecked(), true, "stopping the server keeps the filter active");
  assert.equal(await page.locator("#master").isDisabled(), true, "closed controls disabled");
  assert.equal(await page.locator("#stop-result").isVisible(), true, "shutdown acknowledgement visible");
  const pollsAtStop = polls;
  await page.waitForTimeout(2200);
  assert.equal(polls, pollsAtStop, "page stops polling after intentional shutdown");
  await assert.rejects(fetch("http://localhost:18781/api/health"), "listener is actually stopped");
  const cfg = await readFile("test-results/browser-fixture.cfg", "utf8");
  assert.match(cfg.split("[Visuals]")[0], /Enabled = false/, "server disabled on disk");
  assert.match(cfg.split("[Visuals]")[1].split("[Visuals.RemoveEffects]")[0], /Enabled = true/, "filter stays enabled on disk");
  assert.match(cfg, /ColorSplit = false/, "selected effects saved before shutdown");
  await page.screenshot({ path: `test-results/${screenshotPrefix}stopped.png`, fullPage: true });
  await page.close();
  console.log("Save and stop: confirmation, saved choices, closed listener and stopped polling passed");
} finally { await browser.close(); }
