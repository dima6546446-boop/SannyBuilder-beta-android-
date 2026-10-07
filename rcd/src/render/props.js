// Внешние ассеты: Kenney «Car Kit» (CC0, https://kenney.nl/assets/car-kit) — только для припаркованных машин.
// Модели объединяются в одну геометрию и рисуются через InstancedMesh (1 вызов отрисовки на тип машины).
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';

export const PARKED_MODELS = ['sedan', 'sedan-sports', 'hatchback-sports', 'suv', 'suv-luxury', 'taxi', 'van', 'race'];
const cache = new Map();

function loadMerged(name, base) {
  if (cache.has(name)) return cache.get(name);
  const p = new GLTFLoader().loadAsync(`${base}assets/kenney/${name}.glb`).then((gltf) => {
    gltf.scene.updateMatrixWorld(true);
    const geos = []; let material = null;
    gltf.scene.traverse((o) => {
      if (!o.isMesh) return;
      const g = o.geometry.clone(); g.applyMatrix4(o.matrixWorld);
      for (const k of Object.keys(g.attributes)) if (!['position', 'normal', 'uv'].includes(k)) g.deleteAttribute(k);
      geos.push(g); material = material || o.material;
    });
    const geo = mergeGeometries(geos); geos.forEach((g) => g.dispose());
    geo.computeBoundingBox();
    const size = geo.boundingBox.getSize(new THREE.Vector3());
    const s = 4.3 / Math.max(size.z, 0.01);                       // приводим длину к ~4.3 м
    geo.scale(s, s, s);
    return { geo, material };
  });
  cache.set(name, p);
  return p;
}

/**
 * Заменяет упрощённые «коробки» припаркованных машин моделями Kenney.
 * parked — массив {x, z, rot, color}; возвращает группу или null при ошибке (остаются запасные коробки).
 */
export async function buildParkedCars(parked, base = import.meta.env.BASE_URL || '/') {
  try {
    const models = await Promise.all(PARKED_MODELS.map((n) => loadMerged(n, base)));
    const group = new THREE.Group();
    const buckets = models.map(() => []);
    parked.forEach((p, i) => buckets[(i * 7 + Math.floor(Math.abs(p.x + p.z))) % models.length].push(p));
    const o = new THREE.Object3D(), col = new THREE.Color();
    buckets.forEach((list, mi) => {
      if (!list.length) return;
      const { geo, material } = models[mi];
      const mat = material.clone(); mat.roughness = 0.55; mat.metalness = 0.15;
      const mesh = new THREE.InstancedMesh(geo, mat, list.length);
      list.forEach((p, k) => {
        o.position.set(p.x, 0, -p.z); o.rotation.set(0, -p.rot + (k % 2 ? 0 : Math.PI), 0); o.scale.set(1, 1, 1); o.updateMatrix(); mesh.setMatrixAt(k, o.matrix);
        col.setHSL(((k * 53 + mi * 17) % 100) / 100, 0.18, 0.88); mesh.setColorAt(k, col);   // лёгкая тонировка для разнообразия
      });
      mesh.castShadow = true; mesh.receiveShadow = true;
      group.add(mesh);
    });
    return group;
  } catch (e) {
    console.warn('Не удалось загрузить модели Kenney, остаются упрощённые:', e);
    return null;
  }
}
