import { chromium } from "playwright";
import { fileURLToPath } from "node:url";

// Render the layout with the original screenshots. Crops use source pixels at 1:1.
const browser = await chromium.launch({
  executablePath: process.env.CHROME_PATH || "/usr/bin/google-chrome",
  headless: true,
  args: ["--no-sandbox"],
});
try {
  const page = await browser.newPage({
    viewport: { width: 2560, height: 1600 },
    deviceScaleFactor: 1,
  });
  await page.goto(new URL("comparison.html", import.meta.url).href);
  await page.evaluate(() => window.mediaReady);
  await page.locator(".card").screenshot({
    path: fileURLToPath(new URL("clear-waters-comparison.png", import.meta.url)),
  });
  console.log("Saved media/clear-waters-comparison.png (2560 × 1600)");
} finally {
  await browser.close();
}
