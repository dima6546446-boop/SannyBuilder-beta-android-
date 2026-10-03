import * as THREE from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import { CITY, coord } from './RoadGraph.js';

/**
 * Магазины «для реализма»: подходишь пешком, покупаешь еду, персонаж ест/пьёт.
 * Никаких систем голода — только деньги и анимация.
 *  - двери магазинов «Продукты», «Хлеб», «Гастроном», «Кафе» на первых этажах (City.shopFronts)
 *    и кафе при АЗС;
 *  - уличные ларьки «24 часа» и «Шаурма» у остановок (строятся здесь, 1 draw call + вывески).
 */

export const ITEMS = {
  sandwich: { name: 'Сэндвич с ветчиной', icon: '🥪', price: 150, bites: 4, drink: false },
  cola: { name: 'Кола 0,5 л', icon: '🥤', price: 90, bites: 3, drink: true },
  shawarma: { name: 'Шаурма', icon: '🌯', price: 230, bites: 5, drink: false },
  chips: { name: 'Чипсы «Хрустим»', icon: '🍟', price: 110, bites: 4, drink: false },
  seeds: { name: 'Семечки «От Мартина»', icon: '🌻', price: 60, bites: 5, drink: false },
  coffee: { name: 'Кофе 3 в 1', icon: '☕', price: 120, bites: 3, drink: true },
  icecream: { name: 'Пломбир в стаканчике', icon: '🍦', price: 70, bites: 4, drink: false },
  kvass: { name: 'Квас «Очаковский»', icon: '🍺', price: 80, bites: 3, drink: true },
  pie: { name: 'Пирожок с капустой', icon: '🥟', price: 50, bites: 3, drink: false },
};

const MENUS = {
  kiosk: ['shawarma', 'sandwich', 'cola', 'chips', 'seeds', 'kvass'],
  shawarma: ['shawarma', 'cola', 'kvass', 'coffee'],
  grocery: ['sandwich', 'cola', 'chips', 'seeds', 'icecream', 'kvass'],
  bakery: ['pie', 'sandwich', 'kvass', 'coffee'],
  cafe: ['coffee', 'sandwich', 'pie', 'icecream', 'cola'],
};
const TITLES = { kiosk: 'Ларёк «24 часа»', shawarma: 'Шаурма у Ашота', grocery: 'Продукты', bakery: 'Хлеб · выпечка', cafe: 'Кафе «Лада»' };

const THANKS = ['Приходи ещё, брат!', 'Спасибо за покупку!', 'Сдачи нет, извини', 'На здоровье!', 'Хорошего дня!'];
const YUM = ['Вкусно!', 'Самое то!', 'Жить стало лучше', 'Кайф', 'Ещё бы одну…'];

// ------------------------------------------------------------------ геометрия
const _c = new THREE.Color();
function col(geo, hex) {
  geo = geo.index ? geo.toNonIndexed() : geo;
  for (const k of Object.keys(geo.attributes)) if (k !== 'position' && k !== 'normal') geo.deleteAttribute(k);
  const n = geo.attributes.position.count, c = new Float32Array(n * 3);
  _c.setHex(hex);
  for (let i = 0; i < n; i++) { c[i * 3] = _c.r; c[i * 3 + 1] = _c.g; c[i * 3 + 2] = _c.b; }
  geo.setAttribute('color', new THREE.BufferAttribute(c, 3));
  return geo;
}

/** Еда/напиток в руке: маленький меш с цветами вершин. Ось Y — «вверх» предмета. */
export function buildItemMesh(kind) {
  const P = [];
  const add = (g, hex, x = 0, y = 0, z = 0, rx = 0, ry = 0, rz = 0) => {
    g.rotateX(rx); g.rotateY(ry); g.rotateZ(rz); g.translate(x, y, z); P.push(col(g, hex));
  };
  switch (kind) {
    case 'sandwich':
      add(new THREE.BoxGeometry(0.11, 0.022, 0.09), 0xe8c27a, 0, 0.026, 0);
      add(new THREE.BoxGeometry(0.115, 0.012, 0.095), 0xd06a6a, 0, 0.011, 0);
      add(new THREE.BoxGeometry(0.118, 0.006, 0.098), 0x6fbf4a, 0, 0.002, 0);
      add(new THREE.BoxGeometry(0.11, 0.022, 0.09), 0xe8c27a, 0, -0.014, 0);
      break;
    case 'cola':
      add(new THREE.CylinderGeometry(0.033, 0.033, 0.115, 12), 0xc4161c, 0, 0, 0);
      add(new THREE.CylinderGeometry(0.03, 0.033, 0.012, 12), 0xc8c8c8, 0, 0.063, 0);
      add(new THREE.BoxGeometry(0.068, 0.02, 0.01), 0xf2f2f2, 0, 0.01, 0.03);
      break;
    case 'shawarma':
      add(new THREE.CylinderGeometry(0.036, 0.032, 0.11, 10), 0xf4f2ea, 0, -0.03, 0);
      add(new THREE.CylinderGeometry(0.034, 0.036, 0.08, 10), 0xd9b277, 0, 0.06, 0);
      add(new THREE.SphereGeometry(0.034, 10, 6, 0, Math.PI * 2, 0, Math.PI / 2), 0xb86b3c, 0, 0.1, 0);
      break;
    case 'chips':
      add(new THREE.BoxGeometry(0.12, 0.16, 0.035), 0xf2c21a, 0, 0, 0);
      add(new THREE.BoxGeometry(0.08, 0.05, 0.037), 0xd2381a, 0, 0.01, 0);
      break;
    case 'seeds':
      add(new THREE.BoxGeometry(0.07, 0.1, 0.025), 0x1a1a1a, 0, 0, 0);
      add(new THREE.CylinderGeometry(0.022, 0.022, 0.027, 10), 0xf2c21a, 0, 0.005, 0, Math.PI / 2);
      break;
    case 'coffee':
      add(new THREE.CylinderGeometry(0.036, 0.027, 0.1, 12), 0xf2f2f2, 0, 0, 0);
      add(new THREE.CylinderGeometry(0.038, 0.038, 0.012, 12), 0x5a3a24, 0, 0.055, 0);
      add(new THREE.CylinderGeometry(0.037, 0.03, 0.03, 12), 0x8a5a34, 0, -0.005, 0);
      break;
    case 'icecream':
      add(new THREE.CylinderGeometry(0.032, 0.026, 0.07, 12), 0xe0b36a, 0, 0, 0);
      add(new THREE.SphereGeometry(0.034, 10, 8), 0xfbf6ea, 0, 0.045, 0);
      break;
    case 'kvass':
      add(new THREE.CylinderGeometry(0.03, 0.032, 0.17, 10), 0x5a2a10, 0, 0, 0);
      add(new THREE.CylinderGeometry(0.012, 0.02, 0.05, 8), 0x5a2a10, 0, 0.11, 0);
      add(new THREE.BoxGeometry(0.064, 0.06, 0.01), 0xe8d6a0, 0, 0.0, 0.028);
      break;
    default: // pie
      add(new THREE.SphereGeometry(0.045, 10, 8).scale(1.3, 0.55, 0.8), 0xd99a4a, 0, 0, 0);
  }
  const m = new THREE.Mesh(mergeGeometries(P, false), new THREE.MeshLambertMaterial({ vertexColors: true }));
  m.name = 'Food_' + kind;
  return m;
}

function signTexture() {
  const c = document.createElement('canvas'); c.width = 512; c.height = 128;
  const g = c.getContext('2d');
  const row = (y, bg, fg, text) => { g.fillStyle = bg; g.fillRect(0, y, 512, 64); g.fillStyle = fg; g.font = 'bold 40px sans-serif'; g.textAlign = 'center'; g.textBaseline = 'middle'; g.fillText(text, 256, y + 33); };
  row(0, '#0d47a1', '#ffeb3b', 'ПРОДУКТЫ · 24 ЧАСА');
  row(64, '#b71c1c', '#ffffff', 'ШАУРМА · КОФЕ');
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace;
  return t;
}

// ------------------------------------------------------------------ магазины
export class Shops {
  constructor(game) {
    this.g = game;
    const city = game.city;
    this.points = city.shopFronts.map((s) => ({ ...s, title: TITLES[s.kind] }));
    this._buildKiosks();
    this.near = null;
    this.ui = this._buildUI();
  }

  /** Ларьки у части остановок: за тротуаром, окном к дороге, в 6 м от павильона. */
  _buildKiosks() {
    const g = this.g, city = g.city;
    const P = [], signs = [];
    const box = (w, h, d, hex, x, y, z, ry) => { const geo = new THREE.BoxGeometry(w, h, d); geo.rotateY(ry); geo.translate(x, y, z); P.push(col(geo, hex)); };
    const off = CITY.HALF + CITY.SIDEWALK + 1.5;
    let n = 0;
    city.busStops.forEach((st, i) => {
      if (i % 2 || n >= 10) return;
      const ux = Math.abs(st.nz), uz = Math.abs(st.nx);              // вдоль дороги
      // точка напротив остановки, но за тротуаром (в глубине квартала)
      const roadX = st.nx ? st.x + st.nx * (CITY.HALF + 2.0) : st.x, roadZ = st.nz ? st.z + st.nz * (CITY.HALF + 2.0) : st.z;
      for (const along of [7, -7]) {
        const x = roadX - st.nx * off + ux * along, z = roadZ - st.nz * off + uz * along;
        let blocked = false;
        city.col.collideCircle(x, z, 2.0, () => { blocked = true; });
        if (blocked) continue;
        const kind = n % 2 ? 'shawarma' : 'kiosk';
        const ry = Math.atan2(st.nx, st.nz);                         // окно смотрит на дорогу
        const y0 = CITY.CURB;
        const body = kind === 'kiosk' ? 0xe9edf2 : 0xf3e3c3, trim = kind === 'kiosk' ? 0x1f4fb0 : 0xb71c1c;
        box(2.8, 2.5, 2.2, body, x, y0 + 1.25, z, ry);
        box(3.1, 0.12, 2.6, trim, x, y0 + 2.56, z, ry);
        const fx = Math.sin(ry), fz = Math.cos(ry);
        box(2.2, 0.9, 0.04, 0x8fb6c8, x + fx * 1.11, y0 + 1.5, z + fz * 1.11, ry);      // окно
        box(2.4, 0.06, 0.32, 0x9a9a9a, x + fx * 1.25, y0 + 1.0, z + fz * 1.25, ry);     // прилавок
        box(2.8, 0.1, 0.05, trim, x + fx * 1.12, y0 + 0.5, z + fz * 1.12, ry);
        signs.push({ x: x + fx * 1.13, y: y0 + 2.2, z: z + fz * 1.13, ry, row: kind === 'kiosk' ? 0 : 1 });
        city.col.addBox(x - (ux * 1.4 + uz * 1.1), z - (uz * 1.4 + ux * 1.1), x + (ux * 1.4 + uz * 1.1), z + (uz * 1.4 + ux * 1.1), 'kiosk');
        this.points.push({ x: x + fx * 1.9, z: z + fz * 1.9, nx: fx, nz: fz, kind, title: TITLES[kind] });
        n++;
        break;
      }
    });
    if (!P.length) return;
    const mesh = new THREE.Mesh(mergeGeometries(P, false), new THREE.MeshLambertMaterial({ vertexColors: true }));
    mesh.name = 'Kiosks';
    mesh.castShadow = g.q.shadows; mesh.receiveShadow = g.q.name === 'high';
    g.scene.add(mesh);
    // вывески одним мешем (две строки текстуры)
    const tex = signTexture(), quads = [];
    for (const s of signs) {
      const q = new THREE.PlaneGeometry(2.6, 0.5);
      const uv = q.attributes.uv;
      for (let k = 0; k < uv.count; k++) uv.setY(k, uv.getY(k) * 0.5 + (s.row ? 0 : 0.5));
      q.rotateY(s.ry); q.translate(s.x, s.y, s.z);
      quads.push(q);
    }
    this.signMat = new THREE.MeshLambertMaterial({ map: tex, emissiveMap: tex, emissive: 0xffffff, emissiveIntensity: 0.15 });
    const sm = new THREE.Mesh(mergeGeometries(quads, false), this.signMat);
    sm.name = 'KioskSigns';
    g.scene.add(sm);
    this.kioskCount = signs.length;
  }

  // ---------------------------------------------------------------- UI
  _buildUI() {
    const el = document.createElement('div');
    el.id = 'shop';
    el.className = 'hidden';
    document.body.appendChild(el);
    el.addEventListener('pointerdown', (e) => e.stopPropagation());
    return el;
  }

  open(pt) {
    const g = this.g, items = MENUS[pt.kind] || MENUS.grocery;
    this.openPt = pt;
    this.ui.innerHTML = `
      <div class="shop-head"><div><div class="shop-t">${pt.title}</div><div class="shop-s">Баланс: ${g.save.money.toLocaleString('ru-RU')} ₽</div></div><div class="shop-x" data-x>✕</div></div>
      <div class="shop-list">${items.map((k) => {
        const it = ITEMS[k], ok = g.save.money >= it.price;
        return `<div class="shop-item ${ok ? '' : 'off'}" data-k="${k}"><span class="si">${it.icon}</span><span class="sn">${it.name}</span><span class="sp">${it.price} ₽</span></div>`;
      }).join('')}</div>`;
    this.ui.classList.remove('hidden');
    this.ui.querySelector('[data-x]').addEventListener('click', () => this.close());
    this.ui.querySelectorAll('[data-k]').forEach((b) => b.addEventListener('click', () => this.buy(b.dataset.k)));
  }

  close() { this.ui.classList.add('hidden'); this.openPt = null; }

  buy(k) {
    const g = this.g, it = ITEMS[k];
    if (!g.save.spend(it.price)) { g.hud.toast('Не хватает денег', 'bad'); return; }
    g.audio.coin();
    this.close();
    g.walker.eat(k);
    g.hud.toast(`${it.icon} ${it.name} · −${it.price} ₽ · «${THANKS[(Math.random() * THANKS.length) | 0]}»`, 'money', 2.5);
  }

  yum() { return YUM[(Math.random() * YUM.length) | 0]; }

  /** Каждый кадр: ближайший магазин для кнопки «МАГАЗИН». */
  update(night) {
    if (this.signMat) this.signMat.emissiveIntensity = 0.15 + night * 0.85;
    const g = this.g;
    let near = null;
    if (g.onFoot && g.walker.state === 'walk') {
      const w = g.walker.pos;
      for (const p of this.points) if (Math.hypot(p.x - w.x, p.z - w.z) < 2.3) { near = p; break; }
    }
    if (near !== this.near) {
      this.near = near;
      const b = document.getElementById('btn-shop');
      b.classList.toggle('hidden', !near);
      if (near) b.textContent = `${near.kind === 'shawarma' ? '🌯' : '🛒'} ${near.title.toUpperCase()}`;
    }
    if (this.openPt && (!near || near !== this.openPt)) this.close();
  }
}
