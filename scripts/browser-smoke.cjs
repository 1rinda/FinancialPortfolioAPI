// Optional: npm install --prefix .local/browser --no-audit --no-fund playwright
// Start scripts/start-local.ps1 first. Uses installed Microsoft Edge headlessly.
const { chromium } = require('../.local/browser/node_modules/playwright');
const fs = require('node:fs');
const assert = require('node:assert/strict');
(async () => {
    const browser = await chromium.launch({ channel: 'msedge', headless: true });
    try {
        const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
        const errors = [];
        page.on('pageerror', e => errors.push(e.message));
        await page.goto('http://localhost:5080/');
        await page.locator('#api-key').fill(fs.readFileSync('.local/api-key.txt', 'utf8').trim());
        await page.getByRole('button', { name: 'Connect', exact: true }).click();
        await page.waitForFunction(() => document.querySelector('#notice').textContent.startsWith('Connected'));
        await page.locator('#new-portfolio').click();
        await page.locator('#portfolio-name').fill('Browser verification');
        await page.locator('#client-name').fill('Test fixture');
        await page.locator('#client-id').fill('BROWSER-TEST');
        await page.getByRole('button', { name: 'Create portfolio', exact: true }).click();
        await page.waitForFunction(() => document.querySelector('#detail-dialog').open);
        await page.getByText('Edit portfolio', { exact: true }).click();
        await page.locator('#edit-name').fill('Browser verification updated');
        await page.locator('#edit-form button[type=submit], #edit-form button:not([type])').click();
        await page.waitForFunction(() => document.querySelector('#notice').textContent === 'Portfolio updated.');
        assert.equal(await page.locator('#detail-title').textContent(), 'Browser verification updated');
        await page.screenshot({ path: '.local/browser-desktop.png', fullPage: true });
        page.once('dialog', dialog => dialog.accept());
        await page.locator('#delete').click();
        await page.waitForFunction(() => document.querySelector('#notice').textContent.startsWith('Portfolio deleted.'));
        await page.setViewportSize({ width: 390, height: 844 });
        assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'mobile layout overflows');
        await page.screenshot({ path: '.local/browser-mobile.png', fullPage: true });
        await page.goto('http://localhost:5080/api-docs');
        await page.waitForFunction(() => document.querySelector('#endpoint').options.length === 14);
        await page.locator('#key').fill(fs.readFileSync('.local/api-key.txt', 'utf8').trim());
        await page.locator('#send').click();
        await page.waitForFunction(() => document.querySelector('#result-status').textContent.includes('HTTP 200'));
        assert.deepEqual(errors, []);
        console.log('PASS: browser connect, create, edit, soft-delete, mobile layout and API explorer; no page errors.');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
