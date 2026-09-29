import * as THREE from 'three';

const ZONE_VERT = /* glsl */`
varying vec2 vUv;
void main() { vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }`;

const ZONE_FRAG = /* glsl */`
uniform vec3 color;
uniform float time;
uniform float fill;
uniform vec2 size;
uniform float arrow;
varying vec2 vUv;
void main() {
  vec2 p = vUv * size;                       // метры внутри прямоугольника
  float edge = min(min(p.x, size.x - p.x), min(p.y, size.y - p.y));
  float border = 1.0 - smoothstep(0.08, 0.16, edge);
  // бегущие штрихи по периметру
  float dash = step(0.5, fract((p.x + p.y) * 0.8 - time * 1.5));
  float a = border * (0.65 + 0.35 * dash);
  // стрелка направления (вперёд = +v)
  vec2 c = vUv - vec2(0.5, 0.55);
  float head = step(abs(c.x) * 2.6, c.y * 1.0 + 0.12) * step(c.y, 0.12) * step(-0.02, c.y);
  float shaft = step(abs(c.x), 0.06) * step(-0.22, c.y) * step(c.y, 0.0);
  a = max(a, (head + shaft) * 0.55 * arrow);
  a = max(a, 0.18 + 0.1 * sin(time * 4.0));
  a = max(a, fill * 0.55);
  gl_FragColor = vec4(color, a);
}`;

/** Подсвеченное парковочное место со стрелкой направления. */
export class TargetZone {
  constructor(scene) {
    this.uniforms = {
      color: { value: new THREE.Color(0xffcc33) }, time: { value: 0 }, fill: { value: 0 },
      size: { value: new THREE.Vector2(2.6, 5.4) }, arrow: { value: 1 },
    };
    this.mesh = new THREE.Mesh(
      new THREE.PlaneGeometry(1, 1).rotateX(-Math.PI / 2),
      new THREE.ShaderMaterial({
        uniforms: this.uniforms, vertexShader: ZONE_VERT, fragmentShader: ZONE_FRAG,
        transparent: true, depthWrite: false, toneMapped: false,
        polygonOffset: true, polygonOffsetFactor: -6, polygonOffsetUnits: -6,
      }),
    );
    this.mesh.renderOrder = 3;
    this.mesh.visible = false;
    scene.add(this.mesh);
  }

  /** PlaneGeometry после rotateX: локальная +V (вперёд по стрелке) смотрит в −Z, поэтому доворачиваем на π. */
  show(x, z, heading, w, l, y = 0.03) {
    this.mesh.position.set(x, y, z);
    this.mesh.rotation.set(0, heading + Math.PI, 0);
    this.mesh.scale.set(w, 1, l);
    this.uniforms.size.value.set(w, l);
    this.mesh.visible = true;
  }

  hide() { this.mesh.visible = false; }

  update(dt, state, progress = 0) {
    this.uniforms.time.value += dt;
    this.uniforms.color.value.setHex(state === 'inside' ? 0x35e07a : state === 'wrong' ? 0xff5040 : 0xffcc33);
    this.uniforms.fill.value = progress;
  }
}

const BEAM_FRAG = /* glsl */`
uniform vec3 color;
uniform float time;
varying vec2 vUv;
void main() {
  float a = (1.0 - vUv.y) * (0.55 + 0.15 * sin(time * 3.0 + vUv.y * 8.0));
  gl_FragColor = vec4(color * 1.6, a);
}`;

/** Световой столб-метка (посадка пассажира, точка назначения, чекпоинт экзамена). */
export class Beacon {
  constructor(scene, color = 0xffcc33) {
    this.uniforms = { color: { value: new THREE.Color(color) }, time: { value: 0 } };
    const mat = new THREE.ShaderMaterial({
      uniforms: this.uniforms, vertexShader: ZONE_VERT, fragmentShader: BEAM_FRAG,
      transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, side: THREE.DoubleSide, toneMapped: false,
    });
    this.group = new THREE.Group();
    const beam = new THREE.Mesh(new THREE.CylinderGeometry(2.2, 2.2, 40, 20, 1, true).translate(0, 20, 0), mat);
    const ring = new THREE.Mesh(new THREE.RingGeometry(2.6, 3.2, 32).rotateX(-Math.PI / 2).translate(0, 0.05, 0),
      new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.9, toneMapped: false, depthWrite: false }));
    this.ring = ring;
    this.group.add(beam, ring);
    this.group.visible = false;
    beam.frustumCulled = false;
    scene.add(this.group);
  }

  show(x, z, color, y = 0) {
    if (color !== undefined) { this.uniforms.color.value.setHex(color); this.ring.material.color.setHex(color); }
    this.group.position.set(x, y, z);
    this.group.visible = true;
  }

  hide() { this.group.visible = false; }

  update(dt) {
    this.uniforms.time.value += dt;
    const s = 1 + Math.sin(this.uniforms.time.value * 3) * 0.08;
    this.ring.scale.set(s, 1, s);
  }
}

/** Конусы (InstancedMesh) — сбитые конусы падают. */
export class Cones {
  constructor(scene, max = 240) {
    const g = new THREE.ConeGeometry(0.22, 0.75, 10, 3, true).translate(0, 0.375, 0);
    const col = [], pos = g.attributes.position;
    for (let i = 0; i < pos.count; i++) {
      const y = pos.getY(i);
      const white = y > 0.3 && y < 0.5;
      col.push(...(white ? [0.95, 0.95, 0.95] : [1.0, 0.32, 0.02]));
    }
    g.setAttribute('color', new THREE.Float32BufferAttribute(col, 3));
    const base = new THREE.BoxGeometry(0.46, 0.04, 0.46).translate(0, 0.02, 0);
    const bc = [];
    for (let i = 0; i < base.attributes.position.count; i++) bc.push(0.1, 0.1, 0.1);
    base.setAttribute('color', new THREE.Float32BufferAttribute(bc, 3));
    const merged = mergeTwo(g.toNonIndexed(), base.toNonIndexed());
    this.mesh = new THREE.InstancedMesh(merged, new THREE.MeshLambertMaterial({ vertexColors: true }), max);
    this.mesh.count = 0;
    this.mesh.castShadow = true;
    this.mesh.frustumCulled = false;
    scene.add(this.mesh);
    this.list = [];
    this.max = max;
    this._m = new THREE.Matrix4();
    this._q = new THREE.Quaternion();
    this._e = new THREE.Euler();
    this._p = new THREE.Vector3();
    this._s = new THREE.Vector3(1, 1, 1);
  }

  set(points) {
    this.list = points.slice(0, this.max).map(([x, z]) => ({ x, z, tilt: 0, dir: 0, hit: false }));
    this._write();
  }

  clear() { this.list = []; this.mesh.count = 0; }

  knock(i, nx, nz) {
    const c = this.list[i];
    if (!c || c.hit) return;
    c.hit = true;
    c.dir = Math.atan2(nx, nz);
  }

  update(dt) {
    let dirty = false;
    for (const c of this.list) if (c.hit && c.tilt < 1.45) { c.tilt = Math.min(1.45, c.tilt + dt * 6); dirty = true; }
    if (dirty) this._write();
  }

  _write() {
    this.list.forEach((c, i) => {
      this._e.set(-c.tilt * Math.cos(c.dir), 0, c.tilt * Math.sin(c.dir), 'YXZ');
      this._q.setFromEuler(this._e);
      this._m.compose(this._p.set(c.x, 0, c.z), this._q, this._s);
      this.mesh.setMatrixAt(i, this._m);
    });
    this.mesh.count = this.list.length;
    this.mesh.instanceMatrix.needsUpdate = true;
  }
}

function mergeTwo(a, b) {
  const g = new THREE.BufferGeometry();
  for (const name of ['position', 'normal', 'color']) {
    const A = a.attributes[name].array, B = b.attributes[name].array;
    const arr = new Float32Array(A.length + B.length);
    arr.set(A); arr.set(B, A.length);
    g.setAttribute(name, new THREE.BufferAttribute(arr, 3));
  }
  return g;
}

/** Простой пассажир-пешеход (для такси). */
export class Pedestrian {
  constructor(scene) {
    this.group = new THREE.Group();
    const body = new THREE.Mesh(new THREE.CylinderGeometry(0.22, 0.26, 1.1, 8).translate(0, 0.95, 0), new THREE.MeshLambertMaterial({ color: 0x2d4a7a }));
    const legs = new THREE.Mesh(new THREE.CylinderGeometry(0.2, 0.16, 0.8, 8).translate(0, 0.4, 0), new THREE.MeshLambertMaterial({ color: 0x222222 }));
    const head = new THREE.Mesh(new THREE.SphereGeometry(0.16, 10, 8).translate(0, 1.68, 0), new THREE.MeshLambertMaterial({ color: 0xe0b090 }));
    const hat = new THREE.Mesh(new THREE.CylinderGeometry(0.17, 0.19, 0.12, 10).translate(0, 1.82, 0), new THREE.MeshLambertMaterial({ color: 0x6a4a2a }));
    this.body = body;
    this.group.add(body, legs, head, hat);
    this.group.traverse((o) => { o.castShadow = true; });
    this.group.visible = false;
    scene.add(this.group);
    this.t = 0;
  }

  show(x, y, z, color) {
    this.group.position.set(x, y, z);
    if (color) this.body.material.color.setHex(color);
    this.group.visible = true;
  }

  hide() { this.group.visible = false; }

  update(dt, lookX, lookZ) {
    if (!this.group.visible) return;
    this.t += dt;
    this.group.rotation.y = Math.atan2(lookX - this.group.position.x, lookZ - this.group.position.z);
    this.group.position.y += Math.sin(this.t * 6) * 0.002; // «машет рукой»
  }
}
