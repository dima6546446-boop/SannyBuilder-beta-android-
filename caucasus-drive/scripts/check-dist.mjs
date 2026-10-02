// Проверка, что веб-сборка готова для упаковки в APK (android/app копирует ../dist).
import { existsSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';

if (!existsSync('dist/index.html')) {
  console.error('dist/index.html не найден — сначала npm run build');
  process.exit(1);
}
let total = 0;
const walk = (d) => readdirSync(d).forEach((f) => {
  const p = join(d, f);
  const s = statSync(p);
  if (s.isDirectory()) walk(p); else total += s.size;
});
walk('dist');
console.log(`dist готов: ${(total / 1024).toFixed(0)} КБ. Теперь: cd android && ./gradlew assembleDebug`);
