// Копирует транскодер Basis Universal (нужен KTX2Loader для сжатых текстур)
// из node_modules/three в public/basis. Запуск: npm run copy-basis
import { cpSync, existsSync, mkdirSync } from 'node:fs';
import { resolve } from 'node:path';

const src = resolve('node_modules/three/examples/jsm/libs/basis');
const dst = resolve('public/basis');
if (!existsSync(src)) {
  console.error('three не установлен: выполните npm install');
  process.exit(1);
}
mkdirSync(dst, { recursive: true });
cpSync(src, dst, { recursive: true });
console.log('Basis transcoder скопирован в', dst);
