// Loads the published Aion.Web site in headless Chrome and fails unless the app shell renders.
// A green `dotnet publish` doesn't prove the app boots: in September 2026 a missing
// _framework/blazor.webassembly.js left the deployed site on its splash screen with CI passing.
//
// Usage: node wasm-boot-smoke.mjs <url>
// Env:   CHROME_PATH  Chrome/Chromium binary (defaults to the ubuntu-latest runner's google-chrome)
import { chromium } from 'playwright-core';

const url = process.argv[2] ?? 'http://localhost:8080/';
const executablePath = process.env.CHROME_PATH ?? '/usr/bin/google-chrome';
const timeoutMs = 120000;

const browser = await chromium.launch({ executablePath, args: ['--no-sandbox'] });
const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });

const failures = [];
const warnings = [];
page.on('response', r => {
  if (r.status() < 400) return;
  const line = `HTTP ${r.status()} ${r.url()}`;
  (new URL(r.url()).pathname.includes('/_framework/') ? failures : warnings).push(line);
});
page.on('pageerror', e => failures.push(`pageerror: ${e.message}`));
page.on('console', m => {
  // Blazor reports unhandled render and startup exceptions as "crit:" console errors.
  if (m.type() === 'error' && /crit:|Unhandled exception/i.test(m.text())) failures.push(`console: ${m.text().slice(0, 500)}`);
});

let booted = false;
try {
  await page.goto(url, { waitUntil: 'load', timeout: timeoutMs });
  await page.waitForFunction(
    () => !document.getElementById('aion-splash') && !!document.querySelector('.aion-tab-bar'),
    null,
    { timeout: timeoutMs });
  await page.getByText('Welcome to Aion').waitFor({ timeout: 30000 });
  booted = true;
} catch (e) {
  failures.push(`app shell did not render: ${e.message.split('\n')[0]}`);
}

warnings.forEach(w => console.log(`warning: ${w}`));
await browser.close();

if (!booted || failures.length) {
  failures.forEach(f => console.error(`FAIL ${f}`));
  process.exit(1);
}
console.log(`Aion.Web booted at ${url}: app shell and onboarding dialog rendered`);
