// Экспорт габаритов и параметров моделей из config/cars.js в JSON для Blender-скрипта
// (единый источник правды для колёсной базы, колеи, радиуса колёс, положения фар).
import { writeFileSync } from 'node:fs';
import { allRenderModels } from '../../src/config/cars.js';

const out = {};
for (const m of allRenderModels()) {
  out[m.key] = {
    id: m.id, base: m.base ?? m.id, name: m.name, dims: m.dims, head: m.head, tail: m.tail, grille: m.grille,
    bumper: m.bumper, trim: m.trim, pillars: m.pillars, glassBottom: m.glassBottom,
    fixedColor: m.fixedColor ?? null, police: !!m.police, taxi: !!m.taxi, rails: !!m.rails,
  };
}
writeFileSync(new URL('./cars_dims.json', import.meta.url), JSON.stringify(out, null, 1));
console.log('cars_dims.json:', Object.keys(out).length, 'моделей');
