import * as THREE from 'three';

/**
 * Окружение для PBR-отражений (краска, хром, стёкла, асфальт на «Высоком»):
 * PMREM из того же шейдерного неба, что и в сцене — отражения меняются вместе
 * со временем суток. Перегенерация редкая (при смене времени на ~1,5 игровых часа).
 */
export class EnvManager {
  constructor(renderer, skyMaterial, groundColor) {
    this.renderer = renderer;
    this.pmrem = new THREE.PMREMGenerator(renderer);
    this.scene = new THREE.Scene();
    this.sky = new THREE.Mesh(new THREE.SphereGeometry(100, 32, 16), skyMaterial);
    this.scene.add(this.sky);
    this.groundMat = new THREE.MeshBasicMaterial({ color: groundColor });
    const ground = new THREE.Mesh(new THREE.CircleGeometry(90, 24).rotateX(-Math.PI / 2).translate(0, -1.5, 0), this.groundMat);
    this.scene.add(ground);
    // «здания» на горизонте — тёмные блоки, чтобы в хроме было что отражать
    const blocks = new THREE.MeshBasicMaterial({ color: 0x3a3e44 });
    this.blockMat = blocks;
    for (let i = 0; i < 18; i++) {
      const a = (i / 18) * Math.PI * 2, h = 8 + Math.random() * 20;
      const b = new THREE.Mesh(new THREE.BoxGeometry(14, h, 6), blocks);
      b.position.set(Math.cos(a) * 60, h / 2 - 1.5, Math.sin(a) * 60);
      b.lookAt(0, h / 2, 0);
      this.scene.add(b);
    }
    this.texture = null;
    this.lastKey = -999;
  }

  update(time, groundColor, night, force = false) {
    const key = Math.round(time / 1.5); // шаг 1,5 игровых часа (~1 мин): PMREM дорогой, на телефоне — рывок
    if (!force && key === this.lastKey) return this.texture;
    this.lastKey = key;
    if (groundColor) this.groundMat.color.copy(groundColor).multiplyScalar(0.5);
    this.blockMat.color.setScalar(0.25 * (1 - night * 0.8));
    const rt = this.pmrem.fromScene(this.scene, 0.02, 0.1, 400);
    const old = this.rt;
    this.rt = rt;
    this.texture = rt.texture;
    if (old) old.dispose();
    return this.texture;
  }
}
