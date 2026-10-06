// Копирует Draco-декодер (для сжатых GLB-моделей машин) из three в public/draco.
import { cpSync, mkdirSync } from 'node:fs';
mkdirSync('public/draco', { recursive: true });
for (const f of ['draco_decoder.js', 'draco_decoder.wasm', 'draco_wasm_wrapper.js']) cpSync(`node_modules/three/examples/jsm/libs/draco/gltf/${f}`, `public/draco/${f}`);
console.log('Draco decoder → public/draco');
