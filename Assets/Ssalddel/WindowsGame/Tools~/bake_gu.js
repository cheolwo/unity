// 구 지도 바탕을 굽는다: 전체(가로 4096점)와 세 구 상세(1점 = 2.5m).
// 사용: node bake_gu.js <중간 자료 폴더> — 그 폴더에 gu.js, roads.json, cover_shaded.png가 있어야 한다.
// Playwright(Chromium)가 필요하다. 결과: base_overview.png, base_detail.png
const { chromium } = require('playwright'); const fs = require('fs'); const path = require('path');
const dir = path.resolve(process.argv[2]);
const DETAIL = { x0: 7100, y0: 0, x1: 15950, y1: 11115, ppm: 0.4 };       // 중랑·동대문·광진의 법정동이 모두 들어오는 범위
(async () => {
  fs.copyFileSync(path.join(__dirname, 'bake_gu.html'), path.join(dir, 'bake_gu.html'));
  fs.writeFileSync(path.join(dir, 'roads.js'), 'window.GU_ROADS = ' + JSON.stringify({ outside: JSON.parse(fs.readFileSync(path.join(dir, 'roads.json'), 'utf8')).outside }) + ';');
  const browser = await chromium.launch({ args: ['--allow-file-access-from-files'] });
  const jobs = [['base_overview.png', '?w=4096'],
    ['base_detail.png', `?w=${Math.round((DETAIL.x1 - DETAIL.x0) * DETAIL.ppm)}&x0=${DETAIL.x0}&y0=${DETAIL.y0}&x1=${DETAIL.x1}&y1=${DETAIL.y1}`]];
  for (const [name, query] of jobs) {
    const page = await browser.newPage({ viewport: { width: 1200, height: 800 } });
    page.on('pageerror', e => console.log('pageerror:', e.message));
    await page.goto('file://' + path.join(dir, 'bake_gu.html') + query);
    await page.waitForFunction(() => document.title === 'done', null, { timeout: 300000 });
    const size = await page.evaluate('window.SIZE'); const data = await page.evaluate('window.EXPORT()');
    fs.writeFileSync(path.join(dir, name), Buffer.from(data.split(',')[1], 'base64')); console.log(name, size.join('×'));
    await page.close();
  }
  await browser.close();
})();
