import * as THREE from 'three';

/**
 * Внешний тюнинг поверх модели: российские номера с кавказскими регионами («блатные» тоже)
 * и неоновая подсветка днища. Общий для машины игрока и 3D-гаража.
 */

const _cache = new Map();

/** Номер по ГОСТ на canvas 520×112: белое поле, буквы-цифры, справа — регион, «RUS» и флаг. */
export function drawPlate(g, text, region) {
  g.fillStyle = '#111'; g.fillRect(0, 0, 520, 112);
  g.fillStyle = '#f4f4f0'; g.fillRect(4, 4, 512, 104);
  g.strokeStyle = '#111'; g.lineWidth = 4; g.strokeRect(8, 8, 504, 96);
  g.beginPath(); g.moveTo(398, 8); g.lineTo(398, 104); g.stroke();
  g.fillStyle = '#111'; g.textBaseline = 'alphabetic'; g.textAlign = 'center';
  // «А 777 АА»: первая буква, три цифры, две буквы (цифры крупнее)
  const [l1, num, l2] = [text[0], text.slice(1, 4), text.slice(4, 6)];
  g.font = 'bold 66px "Arial Narrow", Arial, sans-serif'; g.fillText(l1, 52, 88);
  g.font = 'bold 84px "Arial Narrow", Arial, sans-serif'; g.fillText(num, 170, 92);
  g.font = 'bold 66px "Arial Narrow", Arial, sans-serif'; g.fillText(l2, 316, 88);
  // трёхзначный регион ужимаем по ширине, чтобы не залезал на рамку
  g.font = 'bold 60px "Arial Narrow", Arial, sans-serif'; g.fillText(region, 457, 70, 104);
  g.font = 'bold 18px Arial'; g.fillText('RUS', 440, 96);
  const fy = 82;
  [['#fff', 0], ['#1c3fa8', 1], ['#d52b1e', 2]].forEach(([col, i]) => { g.fillStyle = col; g.fillRect(468, fy + i * 5, 28, 5); });
  g.strokeStyle = '#111'; g.lineWidth = 1; g.strokeRect(468, fy, 28, 15);
}

function makePlateTexture(c) {
  const t = new THREE.CanvasTexture(c);
  t.colorSpace = THREE.SRGBColorSpace;
  t.anisotropy = 4;
  return t;
}

/** Текстура номера (кэш по тексту: машины игрока и гаража делят одну). */
export function plateTexture(text, region) {
  const key = text + region;
  if (_cache.has(key)) return _cache.get(key);
  const c = document.createElement('canvas'); c.width = 520; c.height = 112;
  drawPlate(c.getContext('2d'), text, region);
  const t = makePlateTexture(c);
  _cache.set(key, t);
  return t;
}

let _preview = null;
/** Одна текстура для живого предпросмотра в редакторе: перерисовывается, а не плодит новые. */
export function platePreviewTexture(text, region) {
  if (!_preview) { const c = document.createElement('canvas'); c.width = 520; c.height = 112; _preview = makePlateTexture(c); }
  drawPlate(_preview.image.getContext('2d'), text, region);
  _preview.needsUpdate = true;
  return _preview;
}

/** Номерные знаки: находим плашки 'plate' модели и кладём поверх них текстуру. */
export function attachPlates(body, plate) {
  let mesh = null;
  body.traverse((m) => { if (!mesh && m.isMesh && m.name === 'plate') mesh = m; });
  if (!mesh || !plate) return null;
  const pos = mesh.geometry.attributes.position, nrm = mesh.geometry.attributes.normal;
  const group = new THREE.Group();
  group.name = 'Plates';
  const mat = new THREE.MeshLambertMaterial({ map: plateTexture(plate.text, plate.region) });
  for (const sign of [1, -1]) {
    // лицевая грань плашки: вершины своей половины машины с нормалью «наружу»
    const c = new THREE.Vector3(), n = new THREE.Vector3();
    let k = 0;
    for (let i = 0; i < pos.count; i++) {
      if (Math.sign(pos.getZ(i)) !== sign || nrm.getZ(i) * sign < 0.5) continue;
      c.x += pos.getX(i); c.y += pos.getY(i); c.z += pos.getZ(i);
      n.x += nrm.getX(i); n.y += nrm.getY(i); n.z += nrm.getZ(i);
      k++;
    }
    if (!k) continue;
    c.multiplyScalar(1 / k); n.normalize();
    const q = new THREE.Mesh(new THREE.PlaneGeometry(0.515, 0.112), mat);
    q.position.copy(c).addScaledVector(n, 0.002);
    q.quaternion.setFromUnitVectors(new THREE.Vector3(0, 0, 1), n);
    group.add(q);
  }
  body.add(group);
  return group;
}

let _neonTex = null;
function neonTexture() {
  if (_neonTex) return _neonTex;
  const c = document.createElement('canvas'); c.width = 128; c.height = 128;
  const g = c.getContext('2d');
  const grd = g.createRadialGradient(64, 64, 8, 64, 64, 64);
  grd.addColorStop(0, 'rgba(255,255,255,1)'); grd.addColorStop(0.55, 'rgba(255,255,255,.45)'); grd.addColorStop(1, 'rgba(255,255,255,0)');
  g.fillStyle = grd; g.fillRect(0, 0, 128, 128);
  _neonTex = new THREE.CanvasTexture(c);
  return _neonTex;
}

/** Неон под днищем: светящееся пятно на асфальте + трубки под порогами. */
export function attachNeon(root, def, color) {
  if (color == null) return null;
  const d = def.dims;
  const group = new THREE.Group();
  group.name = 'Neon';
  const len = d.front - d.rear, zc = (d.front + d.rear) / 2;
  const glowMat = new THREE.MeshBasicMaterial({ map: neonTexture(), color, transparent: true, opacity: 0.6, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false });
  const glow = new THREE.Mesh(new THREE.PlaneGeometry(d.W + 1.4, len + 1.0).rotateX(-Math.PI / 2), glowMat);
  glow.position.set(0, 0.035, zc);
  glow.renderOrder = 2;
  const tubeMat = new THREE.MeshBasicMaterial({ color, toneMapped: false });
  for (const sx of [1, -1]) {
    const t = new THREE.Mesh(new THREE.BoxGeometry(0.03, 0.03, len * 0.62), tubeMat);
    t.position.set(sx * (d.W / 2 - 0.12), d.sill - 0.1, zc);
    group.add(t);
  }
  group.add(glow);
  group.userData = { glowMat, color: new THREE.Color(color) };
  root.add(group);
  return group;
}
