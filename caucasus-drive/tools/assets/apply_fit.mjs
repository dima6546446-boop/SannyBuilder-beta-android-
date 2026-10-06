// После импорта моделей (tools/assets/reports/*.json): подогнать колею, радиус и ширину колёс, габариты
// в cars.js (веб) и CarDefs.cs (Unity) под реальные колёса модели, и собрать титры авторов (CC-BY).
//   node tools/assets/apply_fit.mjs
import { readFileSync, writeFileSync, existsSync, readdirSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, '../..');
const assets = JSON.parse(readFileSync(join(here, 'assets.json'), 'utf8'));
const repDir = join(here, 'reports');
const reports = existsSync(repDir) ? readdirSync(repDir).filter((f) => f.endsWith('.json')).map((f) => JSON.parse(readFileSync(join(repDir, f), 'utf8'))) : [];
const r3 = (v) => Math.round(v * 1000) / 1000;

const carsPath = join(root, 'src/config/cars.js');
let cars = readFileSync(carsPath, 'utf8');
const defsPath = join(root, '../caucasus-unity/Assets/CaucasusDrive/Scripts/Vehicles/CarDefs.cs');
let defs = existsSync(defsPath) ? readFileSync(defsPath, 'utf8') : null;
const imported = [];

for (const rep of reports) {
  const fw = rep.fitted_wheels;
  if (!fw) { console.log(`${rep.cid}: колёса не найдены — размеры не меняю`); continue; }
  imported.push(rep.cid);
  const fit = { track: fw.track, wheelR: fw.wheelR, wheelW: fw.wheelW, archR: r3(fw.wheelR + 0.08) };
  if (rep.front_rear) { fit.front = rep.front_rear[0]; fit.rear = rep.front_rear[1]; }
  // --- cars.js: блок машины от «id: '<cid>'» до следующего «dims: {…}»
  const at = cars.indexOf(`id: '${rep.cid}'`);
  if (at >= 0) {
    const ds = cars.indexOf('dims: {', at), de = cars.indexOf('}', ds);
    let block = cars.slice(ds, de);
    for (const [k, v] of Object.entries(fit)) block = block.replace(new RegExp(`\\b${k}: -?[\\d.]+`), `${k}: ${v}`);
    cars = cars.slice(0, ds) + block + cars.slice(de);
  }
  // --- CarDefs.cs: D(W, front, rear, axleF, axleR, track, wheelR, wheelW, sill)
  if (defs) {
    const a = defs.indexOf(`id = "${rep.cid}"`);
    if (a >= 0) {
      const ds = defs.indexOf('D(', a), de = defs.indexOf(')', ds);
      const args = defs.slice(ds + 2, de).split(',').map((s) => s.trim());
      const set = (i, v) => { args[i] = `${v}f`; };
      if (fit.front != null) { set(1, fit.front); set(2, fit.rear); }
      set(5, fit.track); set(6, fit.wheelR); set(7, fit.wheelW);
      defs = defs.slice(0, ds + 2) + args.join(', ') + defs.slice(de);
    }
  }
  console.log(`${rep.cid}: колея ${fit.track}, R ${fit.wheelR}, ширина ${fit.wheelW}, перёд/зад ${fit.front}/${fit.rear}`);
}
writeFileSync(carsPath, cars);
if (defs) writeFileSync(defsPath, defs);

// --- титры: только реально встроенные модели
const credits = assets.cars.filter((c) => imported.includes(c.id) || existsSync(join(repDir, `${c.id}.json`)))
  .map((c) => ({ id: c.id, name: c.name, author: c.author, url: c.url, license: c.license }));
writeFileSync(join(root, 'src/config/credits.js'),
  `// Сгенерировано tools/assets/apply_fit.mjs — авторы моделей машин (лицензия CC-BY требует указывать автора)\nexport const MODEL_CREDITS = ${JSON.stringify(credits, null, 2)};\n`);
const md = ['# Авторы моделей машин', '', 'Модели взяты с Sketchfab под лицензией Creative Commons Attribution (CC BY 4.0) и доработаны',
  '(масштаб, удалены колёса, упрощены LOD, перекрашиваемый кузов). Спасибо авторам!', '',
  ...credits.map((c) => `- **${c.name}** — ${c.author}, ${c.license}: ${c.url}`), ''];
writeFileSync(join(root, 'CREDITS.md'), md.join('\n'));
const csPath = join(root, '../caucasus-unity/Assets/CaucasusDrive/Scripts/Core/Credits.cs');
if (existsSync(dirname(csPath))) {
  const lines = credits.map((c) => `            "${c.name.replace(/"/g, "'")} — ${c.author} (${c.license})",`).join('\n');
  writeFileSync(csPath, `namespace CaucasusDrive\n{\n    /// <summary>Сгенерировано tools/assets/apply_fit.mjs: авторы моделей машин (CC-BY).</summary>\n    public static class Credits\n    {\n        public static readonly string[] Models = {\n${lines}\n        };\n    }\n}\n`);
}
console.log(`Титры: ${credits.length} моделей`);
