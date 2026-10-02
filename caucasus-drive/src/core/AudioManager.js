/**
 * Процедурный звук на WebAudio: без аудиофайлов (0 КБ в APK), минимальная задержка.
 * Двигатель — осцилляторы на частоте вспышек 4-цилиндрового мотора (об/мин / 60 * 2),
 * визг шин и удары — отфильтрованный шум, гудок — два квадратных тона.
 */
export class AudioManager {
  constructor() {
    this.ctx = null;
    this.enabled = false;
  }

  /** Вызывать только из обработчика жеста пользователя (политика autoplay). */
  init() {
    if (this.ctx) return;
    const AC = window.AudioContext || window.webkitAudioContext;
    if (!AC) return;
    const ctx = new AC({ latencyHint: 'interactive' });
    this.ctx = ctx;

    const comp = ctx.createDynamicsCompressor();
    comp.connect(ctx.destination);
    this.master = ctx.createGain();
    this.master.gain.value = 0.8 * (this.volume ?? 1);
    this.master.connect(comp);

    // 1 секунда белого шума — переиспользуется всеми шумовыми источниками
    const len = ctx.sampleRate;
    this.noise = ctx.createBuffer(1, len, ctx.sampleRate);
    const d = this.noise.getChannelData(0);
    for (let i = 0; i < len; i++) d[i] = Math.random() * 2 - 1;

    // --- двигатель ---
    this.eFilter = ctx.createBiquadFilter();
    this.eFilter.type = 'lowpass';
    this.eFilter.Q.value = 3;
    this.eGain = ctx.createGain();
    this.eGain.gain.value = 0;
    this.eFilter.connect(this.eGain).connect(this.master);

    this.eOsc1 = ctx.createOscillator(); this.eOsc1.type = 'sawtooth';
    this.eOsc2 = ctx.createOscillator(); this.eOsc2.type = 'square';
    this.eOsc3 = ctx.createOscillator(); this.eOsc3.type = 'triangle';
    const g1 = ctx.createGain(); g1.gain.value = 0.45;
    const g2 = ctx.createGain(); g2.gain.value = 0.3;
    const g3 = ctx.createGain(); g3.gain.value = 0.35;
    this.eOsc1.connect(g1).connect(this.eFilter);
    this.eOsc2.connect(g2).connect(this.eFilter);
    this.eOsc3.connect(g3).connect(this.eFilter);

    const rumble = this._noiseLoop();
    const rf = ctx.createBiquadFilter(); rf.type = 'bandpass'; rf.frequency.value = 140; rf.Q.value = 0.8;
    this.rumbleGain = ctx.createGain(); this.rumbleGain.gain.value = 0.2;
    rumble.connect(rf).connect(this.rumbleGain).connect(this.eFilter);

    // --- визг шин ---
    const sq = this._noiseLoop();
    const sf = ctx.createBiquadFilter(); sf.type = 'bandpass'; sf.frequency.value = 1900; sf.Q.value = 7;
    this.skidGain = ctx.createGain(); this.skidGain.gain.value = 0;
    sq.connect(sf).connect(this.skidGain).connect(this.master);

    // --- шум ветра/дороги ---
    const wn = this._noiseLoop();
    const wf = ctx.createBiquadFilter(); wf.type = 'lowpass'; wf.frequency.value = 500;
    this.windGain = ctx.createGain(); this.windGain.gain.value = 0;
    wn.connect(wf).connect(this.windGain).connect(this.master);

    // --- гудок «Жигулей» (два тона) ---
    const hf = ctx.createBiquadFilter(); hf.type = 'lowpass'; hf.frequency.value = 2200;
    this.hornGain = ctx.createGain(); this.hornGain.gain.value = 0;
    hf.connect(this.hornGain).connect(this.master);
    for (const f of [405, 510]) {
      const o = ctx.createOscillator(); o.type = 'square'; o.frequency.value = f;
      o.connect(hf); o.start();
    }

    this.eOsc1.start(); this.eOsc2.start(); this.eOsc3.start();
    this.enabled = true;
  }

  _noiseLoop() {
    const s = this.ctx.createBufferSource();
    s.buffer = this.noise;
    s.loop = true;
    s.loopStart = Math.random() * 0.5;
    s.start(0, Math.random());
    return s;
  }

  /** Профиль мотора: число цилиндров (Ока — 2) и «хрипотца» выхлопа. */
  setEngineProfile({ cylinders = 4, rasp = 1 } = {}) {
    this.cyl = cylinders;
    this.rasp = rasp;
  }

  setVolume(v) {
    this.volume = v;
    if (this.master) this.master.gain.setTargetAtTime(0.8 * v, this.ctx.currentTime, 0.05);
  }

  update(rpm, load, skid, speed) {
    if (!this.enabled) return;
    const t = this.ctx.currentTime;
    const f = (rpm / 60) * ((this.cyl ?? 4) / 2); // частота вспышек 4-тактного мотора
    this.eOsc1.frequency.setTargetAtTime(f, t, 0.03);
    this.eOsc2.frequency.setTargetAtTime(f * 0.5, t, 0.03);
    this.eOsc3.frequency.setTargetAtTime(f * 1.5, t, 0.03);
    this.eFilter.frequency.setTargetAtTime(220 + rpm * 0.22 + load * 900, t, 0.05);
    this.eGain.gain.setTargetAtTime((0.1 + load * 0.16 + (rpm / 6500) * 0.08) * (this.rasp ?? 1), t, 0.06);
    this.rumbleGain.gain.setTargetAtTime(0.15 + load * 0.25, t, 0.1);
    this.skidGain.gain.setTargetAtTime(Math.min(skid, 1) * 0.28, t, 0.05);
    this.windGain.gain.setTargetAtTime(Math.min(speed / 45, 1) * 0.12, t, 0.2);
  }

  setHorn(on) {
    if (!this.enabled) return;
    this.hornGain.gain.setTargetAtTime(on ? 0.22 : 0, this.ctx.currentTime, 0.01);
  }

  /** Удар: шумовой всплеск + низкочастотный «бум». strength ∈ [0..1]. */
  crash(strength) {
    if (!this.enabled) return;
    const ctx = this.ctx, t = ctx.currentTime;
    const src = ctx.createBufferSource(); src.buffer = this.noise;
    const lp = ctx.createBiquadFilter(); lp.type = 'lowpass'; lp.frequency.value = 700 + strength * 1500;
    const g = ctx.createGain();
    g.gain.setValueAtTime(0.9 * strength, t);
    g.gain.exponentialRampToValueAtTime(0.001, t + 0.45);
    src.connect(lp).connect(g).connect(this.master);
    src.start(t, Math.random() * 0.5, 0.5);

    const o = ctx.createOscillator(); o.type = 'sine';
    o.frequency.setValueAtTime(90, t);
    o.frequency.exponentialRampToValueAtTime(35, t + 0.25);
    const og = ctx.createGain();
    og.gain.setValueAtTime(0.8 * strength, t);
    og.gain.exponentialRampToValueAtTime(0.001, t + 0.3);
    o.connect(og).connect(this.master);
    o.start(t); o.stop(t + 0.32);
  }

  /** Короткий гудок машины трафика с панорамой по положению. */
  aiHorn(volume, pan) {
    if (!this.enabled || volume < 0.02) return;
    const ctx = this.ctx, t = ctx.currentTime;
    const p = ctx.createStereoPanner ? ctx.createStereoPanner() : null;
    const g = ctx.createGain();
    g.gain.setValueAtTime(0, t);
    g.gain.linearRampToValueAtTime(0.18 * volume, t + 0.02);
    g.gain.setValueAtTime(0.18 * volume, t + 0.3);
    g.gain.linearRampToValueAtTime(0, t + 0.36);
    const out = p ? (p.pan.value = Math.max(-1, Math.min(1, pan)), g.connect(p), p) : g;
    out.connect(this.master);
    for (const f of [330, 392]) {
      const o = ctx.createOscillator(); o.type = 'square'; o.frequency.value = f;
      o.connect(g); o.start(t); o.stop(t + 0.4);
    }
  }

  /** Короткий тональный сигнал (UI, парктроник). */
  beep(freq = 1400, dur = 0.06, vol = 0.12, type = 'square') {
    if (!this.enabled) return;
    const ctx = this.ctx, t = ctx.currentTime;
    const o = ctx.createOscillator(); o.type = type; o.frequency.value = freq;
    const g = ctx.createGain();
    g.gain.setValueAtTime(0, t);
    g.gain.linearRampToValueAtTime(vol, t + 0.005);
    g.gain.setValueAtTime(vol, t + dur - 0.01);
    g.gain.linearRampToValueAtTime(0, t + dur);
    o.connect(g).connect(this.master);
    o.start(t); o.stop(t + dur + 0.02);
  }

  /** Щелчок реле поворотника. */
  tick(on) {
    if (!this.enabled) return;
    const ctx = this.ctx, t = ctx.currentTime;
    const src = ctx.createBufferSource(); src.buffer = this.noise;
    const bp = ctx.createBiquadFilter(); bp.type = 'bandpass'; bp.frequency.value = on ? 2600 : 1800; bp.Q.value = 3;
    const g = ctx.createGain();
    g.gain.setValueAtTime(0.35, t);
    g.gain.exponentialRampToValueAtTime(0.001, t + 0.03);
    src.connect(bp).connect(g).connect(this.master);
    src.start(t, Math.random() * 0.5, 0.04);
  }

  /** Последовательность нот: [freq, длительность] */
  melody(notes, type = 'triangle', vol = 0.16) {
    if (!this.enabled) return;
    let t = this.ctx.currentTime + 0.02;
    for (const [f, d] of notes) {
      if (f > 0) {
        const o = this.ctx.createOscillator(); o.type = type; o.frequency.value = f;
        const g = this.ctx.createGain();
        g.gain.setValueAtTime(0, t);
        g.gain.linearRampToValueAtTime(vol, t + 0.015);
        g.gain.exponentialRampToValueAtTime(0.001, t + d * 0.95);
        o.connect(g).connect(this.master);
        o.start(t); o.stop(t + d);
      }
      t += d;
    }
  }

  coin() { this.melody([[988, 0.08], [1319, 0.22]], 'square', 0.08); }
  success() { this.melody([[523, 0.12], [659, 0.12], [784, 0.12], [1047, 0.35]], 'triangle', 0.18); }
  fail() { this.melody([[392, 0.18], [330, 0.18], [262, 0.4]], 'sawtooth', 0.09); }
  click() { this.beep(900, 0.03, 0.06, 'triangle'); }

  /** «Крякалка» ДПС. */
  siren() {
    if (!this.enabled) return;
    const ctx = this.ctx, t = ctx.currentTime;
    const o = ctx.createOscillator(); o.type = 'sawtooth';
    o.frequency.setValueAtTime(600, t);
    o.frequency.exponentialRampToValueAtTime(1500, t + 0.25);
    o.frequency.exponentialRampToValueAtTime(700, t + 0.5);
    const lp = ctx.createBiquadFilter(); lp.type = 'lowpass'; lp.frequency.value = 2500;
    const g = ctx.createGain();
    g.gain.setValueAtTime(0.0, t);
    g.gain.linearRampToValueAtTime(0.14, t + 0.03);
    g.gain.setValueAtTime(0.14, t + 0.45);
    g.gain.linearRampToValueAtTime(0, t + 0.55);
    o.connect(lp).connect(g).connect(this.master);
    o.start(t); o.stop(t + 0.6);
  }

  /** Затвор камеры фиксации нарушений. */
  shutter() {
    if (!this.enabled) return;
    const ctx = this.ctx, t = ctx.currentTime;
    for (const dt of [0, 0.07]) {
      const src = ctx.createBufferSource(); src.buffer = this.noise;
      const hp = ctx.createBiquadFilter(); hp.type = 'highpass'; hp.frequency.value = 3000;
      const g = ctx.createGain();
      g.gain.setValueAtTime(0.3, t + dt);
      g.gain.exponentialRampToValueAtTime(0.001, t + dt + 0.05);
      src.connect(hp).connect(g).connect(this.master);
      src.start(t + dt, Math.random() * 0.5, 0.06);
    }
  }

  suspend() { this.ctx?.suspend(); }
  resume() { this.ctx?.resume(); }
}
