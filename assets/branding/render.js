// Renders SVG files to PNG with headless Chromium (Playwright).
// Usage: node render.js jobs.json   where jobs.json = [["in.svg", "out.png", width, height], ...]
const { chromium } = require('playwright');
const fs = require('fs');
(async () => {
  const jobs = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
  const browser = await chromium.launch();
  const page = await browser.newPage();
  for (const [src, out, w, h] of jobs) {
    const svg = fs.readFileSync(src, 'utf8');
    await page.setViewportSize({ width: w, height: h });
    await page.setContent(`<html><body style="margin:0;background:transparent"><img src="data:image/svg+xml;base64,${Buffer.from(svg).toString('base64')}" style="display:block;width:${w}px;height:${h}px"></body></html>`);
    await page.waitForLoadState('networkidle');
    await page.screenshot({ path: out, omitBackground: true, clip: { x: 0, y: 0, width: w, height: h } });
  }
  await browser.close();
})();
