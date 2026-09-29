import * as THREE from 'three';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';
import { OutputPass } from 'three/addons/postprocessing/OutputPass.js';

/**
 * Постобработка (только пресет «Высокое»): bloom в половинном разрешении + OutputPass
 * (тонмаппинг ACES и sRGB). На слабых GPU выключено — там свечение даёт GlowPoints.
 */
export class PostFX {
  constructor(renderer, scene, camera) {
    this.renderer = renderer;
    const size = renderer.getDrawingBufferSize(new THREE.Vector2());
    this.composer = new EffectComposer(renderer);
    this.renderPass = new RenderPass(scene, camera);
    this.bloom = new UnrealBloomPass(new THREE.Vector2(size.x / 2, size.y / 2), 0.55, 0.45, 0.82);
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
    this.bloom.strength = 0.35 + night * 0.55;
    this.bloom.threshold = 0.9 - night * 0.25;
  }

  render(dt) { this.composer.render(dt); }
}
