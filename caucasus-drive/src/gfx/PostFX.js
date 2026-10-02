import * as THREE from 'three';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';
import { OutputPass } from 'three/addons/postprocessing/OutputPass.js';

/**
 * Постобработка (только пресет «Высокое» и только ночью): bloom в половинном разрешении +
 * OutputPass (тонмаппинг ACES и sRGB). Буферы композитора с MSAA ×4 — иначе картинка
 * теряла сглаживание и становилась «лесенкой/мылом». Порог и радиус bloom подобраны так,
 * чтобы светились только лампы и фары, а не небо и белые стены.
 * Днём и на слабых GPU выключено — свечение даёт GlowPoints.
 */
export class PostFX {
  constructor(renderer, scene, camera) {
    this.renderer = renderer;
    const size = renderer.getDrawingBufferSize(new THREE.Vector2());
    const rt = new THREE.WebGLRenderTarget(size.x, size.y, { type: THREE.HalfFloatType, samples: 4 });
    this.composer = new EffectComposer(renderer, rt);
    this.renderPass = new RenderPass(scene, camera);
    this.bloom = new UnrealBloomPass(new THREE.Vector2(size.x / 2, size.y / 2), 0.5, 0.25, 0.85);
    this.composer.addPass(this.renderPass);
    this.composer.addPass(this.bloom);
    this.composer.addPass(new OutputPass());
    this.enabled = true;
  }

  setScene(scene, camera) {
    this.renderPass.scene = scene;
    this.renderPass.camera = camera;
  }

  setSize(w, h) {
    this.composer.setPixelRatio(this.renderer.getPixelRatio());
    this.composer.setSize(w, h);
  }

  setNight(night) {
    this.bloom.strength = 0.2 + night * 0.45;
    this.bloom.threshold = 0.95 - night * 0.2;
  }

  render(dt) { this.composer.render(dt); }
}
