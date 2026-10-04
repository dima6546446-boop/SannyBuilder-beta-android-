import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { DRACOLoader } from 'three/addons/loaders/DRACOLoader.js';

/**
 * Библиотека моделей машин из Blender (public/models/<id>.glb, Draco).
 * В каждом GLB три уровня детализации: LOD_hi (игрок), LOD0 (трафик вблизи), LOD1 (вдали).
 * Каждый уровень — набор подмешей по материалам (paint, glass, chrome, …): материал
 * подменяется игровым по имени. Если файла нет — CarFactory использует процедурную модель.
 */
class Library {
  constructor() {
    this.models = {};
    this.ready = false;
  }

  async load(keys, onProgress) {
    const draco = new DRACOLoader().setDecoderPath('./draco/');
    const loader = new GLTFLoader().setDRACOLoader(draco);
    let done = 0;
    await Promise.all(keys.map(async (key) => {
      try {
        const gltf = await loader.loadAsync(`./models/${key}.glb`);
        const lods = {};
        gltf.scene.traverse((o) => {
          const lod = ['LOD_hi', 'LOD0', 'LOD1'].find((n) => o.name === n || o.name.startsWith(`${n}_`) || o.parent?.name === n);
          if (!o.isMesh || !lod) return;
          (lods[lod] ||= []).push({ geometry: o.geometry, material: o.material.name || 'paint' });
        });
        if (lods.LOD_hi && lods.LOD0 && lods.LOD1) this.models[key] = { hi: lods.LOD_hi, lod0: lods.LOD0, lod1: lods.LOD1 };
      } catch (e) {
        console.warn('Модель не загружена:', key, e?.message);
      }
      done++;
      onProgress?.(done / keys.length);
    }));
    draco.dispose();
    this.ready = true;
    return this;
  }

  get(key) { return this.models[key] || null; }
}

export const ModelLibrary = new Library();

/**
 * В карту теней идут только части, которые формируют силуэт тени (кузов, днище, бамперы,
 * шины). Хром (~10k треугольников), фонари, номера и решётка тень почти не меняют,
 * а стоили 8–10 лишних draw calls в проходе теней.
 */
export const SHADOW_PARTS = new Set(['paint', 'under', 'black', 'rubber']);

/** Подмеши уровня как объект Three (материалы — из словаря mats по имени). */
export function meshesFromLod(parts, mats, fallback) {
  const g = new THREE.Group();
  for (const p of parts) {
    const m = new THREE.Mesh(p.geometry, mats[p.material] || fallback);
    m.name = p.material;
    m.castShadow = SHADOW_PARTS.has(p.material);
    m.receiveShadow = p.material === 'paint';
    g.add(m);
  }
  return g;
}
