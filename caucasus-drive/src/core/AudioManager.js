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
    this.hornOsc = [405, 510].map((f) => {
      const o = ctx.createOscillator(); o.type = 'square'; o.frequency.value = f;
      o.connect(hf); o.start();
      return o;
    });

    // --- дождь (по крыше/асфальту): шум через полосовой фильтр
    const rn = this._noiseLoop();
    this.rainFilter = ctx.createBiquadFilter(); this.rainFilter.type = 'bandpass'; this.rainFilter.frequency.value = 2600; this.rainFilter.Q.value = 0.4;
    this.rainGain = ctx.createGain(); this.rainGain.gain.value = 0;
    rn.connect(this.rainFilter).connect(this.rainGain).connect(this.master);
    this._applyHornType();

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

  /**
   * @param mix — громкость мотора 0..1 (игрок вышел из машины и отошёл — мотор тише)
   */
  update(rpm, load, skid, speed, mix = 1) {
    if (!this.enabled) return;
    const t = this.ctx.currentTime;
    const f = (rpm / 60) * ((this.cyl ?? 4) / 2); // частота вспышек 4-тактного мотора
    this.eOsc1.frequency.setTargetAtTime(f, t, 0.03);
    this.eOsc2.frequency.setTargetAtTime(f * 0.5, t, 0.03);
    this.eOsc3.frequency.setTargetAtTime(f * 1.5, t, 0.03);
    this.eFilter.frequency.setTargetAtTime(220 + rpm * 0.22 + load * 900, t, 0.05);
    this.eGain.gain.setTargetAtTime((0.1 + load * 0.16 + (rpm / 6500) * 0.08) * (this.rasp ?? 1) * mix, t, 0.06);
    this.rumbleGain.gain.setTargetAtTime((0.15 + load * 0.25) * mix, t, 0.1);
    this.skidGain.gain.setTargetAtTime(Math.min(skid, 1) * 0.28, t, 0.05);
    this.windGain.gain.setTargetAtTime(Math.min(speed / 45, 1) * 0.12, t, 0.2);
  }

  // ---------------------------------------------------------------- сигналы (тюнинг)
  _applyHornType() {
    if (!this.hornOsc) return;
    const t = this.hornType || 0;
    const [a, b] = t === 3 ? [233, 294] : [405, 510];      // «Газель-дудка» — низкий двухтон
    this.hornOsc[0].frequency.value = a; this.hornOsc[1].frequency.value = b;
    this.hornOsc.forEach((o) => { o.type = t === 3 ? 'sawtooth' : 'square'; });
  }

  /** Мелодии музыкальных сигналов (собственные мотивы в духе жанра). */
  _hornMelody(type) {
    const A4 = 440, B4 = 494, C5 = 523, D5 = 587, E5 = 659, F5 = 698, Gs5 = 831, A5 = 880, G4 = 392, G5 = 784;
    if (type === 1) return [[E5, 0.1], [E5, 0.1], [F5, 0.1], [E5, 0.1], [D5, 0.1], [C5, 0.1], [B4, 0.1], [C5, 0.2],
      [E5, 0.1], [Gs5, 0.1], [A5, 0.1], [Gs5, 0.1], [F5, 0.1], [E5, 0.1], [D5, 0.1], [E5, 0.1], [A4, 0.3]];
    return [[G4, 0.12], [C5, 0.12], [E5, 0.12], [G5, 0.24], [E5, 0.12], [G5, 0.4]];
  }

  hornSample(type) {
    if (!this.enabled) return;
    this._applyHornType();
    if (type === 1 || type === 2) { this._playHornTune(type); return; }
    this.hornGain.gain.setTargetAtTime(0.22, this.ctx.currentTime, 0.01);
    this.hornGain.gain.setTargetAtTime(0, this.ctx.currentTime + 0.45, 0.02);
  }

  _playHornTune(type) {
    const ctx = this.ctx;
    let t = ctx.currentTime + 0.01;
    const out = ctx.createGain(); out.gain.value = 0.17;
    const lp = ctx.createBiquadFilter(); lp.type = 'lowpass'; lp.frequency.value = 2600;
    out.connect(lp).connect(this.master);
    for (const [f, d] of this._hornMelody(type)) {
      for (const k of [1, 1.26]) {
        const o = ctx.createOscillator(); o.type = 'square'; o.frequency.value = f * (type === 2 ? k : 1);
        const g = ctx.createGain();
        g.gain.setValueAtTime(0, t); g.gain.linearRampToValueAtTime(0.5, t + 0.012);
        g.gain.setValueAtTime(0.5, t + d * 0.85); g.gain.linearRampToValueAtTime(0, t + d * 0.98);
        o.connect(g).connect(out); o.start(t); o.stop(t + d);
        if (type !== 2) break;
      }
      t += d;
    }
    this._tuneEnd = t;
  }

  setHorn(on) {
    if (!this.enabled) return;
    const type = this.hornType || 0;
    if (type === 1 || type === 2) {
      // мелодия играет целиком по нажатию; удержание — повтор после окончания
      if (on && (!this._tuneEnd || this.ctx.currentTime > this._tuneEnd)) this._playHornTune(type);
      this.hornGain.gain.setTargetAtTime(0, this.ctx.currentTime, 0.01);
      return;
    }
    if (this._lastHornType !== type) { this._lastHornType = type; this._applyHornType(); }
    this.hornGain.gain.setTargetAtTime(on ? 0.22 : 0, this.ctx.currentTime, 0.01);
  }

  /** Выстрел прямотока: хлопок + низкий «бах». */
  pop(vol = 1) {
    if (!this.enabled) return;
    this._burst(900, 0.7, 0.55 * vol, 0.09, 'lowpass');
    this._burst(2400, 1.5, 0.18 * vol, 0.05, 'bandpass');
    const ctx = this.ctx, t = ctx.currentTime;
    const o = ctx.createOscillator(); o.type = 'sine';
    o.frequency.setValueAtTime(95, t); o.frequency.exponentialRampToValueAtTime(40, t + 0.08);
    const g = ctx.createGain(); g.gain.setValueAtTime(0.5 * vol, t); g.gain.exponentialRampToValueAtTime(0.001, t + 0.1);
    o.connect(g).connect(this.master); o.start(t); o.stop(t + 0.12);
  }

  pops(n) { for (let i = 0; i < n; i++) setTimeout(() => this.pop(0.8), i * 120 + Math.random() * 50); }

  /** Дождь: 0..1; внутри машины глуше (стук по крыше). */
  setRain(level, inside) {
    if (!this.enabled) return;
    const t = this.ctx.currentTime;
    this.rainGain.gain.setTargetAtTime(level * (inside ? 0.16 : 0.12), t, 0.5);
    this.rainFilter.frequency.setTargetAtTime(inside ? 900 : 2600, t, 0.3);
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

  // ---------------------------------------------------------------- пешеход
  /** Короткий шумовой всплеск через полосовой фильтр. */
  _burst(freq, q, vol, dur, type = 'bandpass', at = 0) {
    if (!this.enabled) return;
    const ctx = this.ctx, t = ctx.currentTime + at;
    const src = ctx.createBufferSource(); src.buffer = this.noise;
    const f = ctx.createBiquadFilter(); f.type = type; f.frequency.value = freq; f.Q.value = q;
    const g = ctx.createGain();
    g.gain.setValueAtTime(0, t);
    g.gain.linearRampToValueAtTime(vol, t + Math.min(0.01, dur * 0.2));
    g.gain.exponentialRampToValueAtTime(0.001, t + dur);
    src.connect(f).connect(g).connect(this.master);
    src.start(t, Math.random() * 0.5, dur + 0.02);
  }

  /** Шаг по асфальту; strength 0.5 — шаг, 1+ — бег/приземление. */
  footstep(strength = 0.6) {
    this._burst(500 + Math.random() * 300, 1.2, 0.09 * strength, 0.07);
    this._burst(2600, 2, 0.025 * strength, 0.04, 'bandpass', 0.008);
  }

  /** Хлопок двери «Жигулей»: глухой удар + дребезг. */
  door() {
    if (!this.enabled) return;
    this._burst(180, 0.8, 0.5, 0.18, 'lowpass');
    this._burst(1400, 4, 0.08, 0.12, 'bandpass', 0.02);
    const ctx = this.ctx, t = ctx.currentTime;
    const o = ctx.createOscillator(); o.type = 'sine';
    o.frequency.setValueAtTime(110, t); o.frequency.exponentialRampToValueAtTime(50, t + 0.15);
    const g = ctx.createGain(); g.gain.setValueAtTime(0.35, t); g.gain.exponentialRampToValueAtTime(0.001, t + 0.18);
    o.connect(g).connect(this.master); o.start(t); o.stop(t + 0.2);
  }

  /** Зажигалка: чирк колёсика + шипение пламени. */
  lighter() {
    this._burst(4200, 3, 0.12, 0.05);
    this._burst(3600, 2, 0.08, 0.04, 'bandpass', 0.06);
    this._burst(1800, 0.5, 0.03, 0.6, 'highpass', 0.1);
  }

  /** Затяжка: тихое потрескивание табака. */
  inhale() {
    for (let i = 0; i < 6; i++) this._burst(5000 + Math.random() * 2000, 6, 0.02, 0.02, 'bandpass', 0.05 + i * 0.12 + Math.random() * 0.05);
    this._burst(900, 0.6, 0.03, 0.7, 'bandpass', 0.05);
  }

  /** Укус: глухой хруст (чипсы/семечки — звонче). */
  bite(crunchy = false) {
    for (let i = 0; i < (crunchy ? 4 : 2); i++) this._burst(crunchy ? 3200 : 1200, crunchy ? 3 : 1.2, crunchy ? 0.07 : 0.09, 0.05, 'bandpass', i * 0.07 + Math.random() * 0.03);
  }

  /** Глоток: низкий «бульк». */
  gulp() {
    if (!this.enabled) return;
    const ctx = this.ctx;
    for (let i = 0; i < 2; i++) {
      const t = ctx.currentTime + i * 0.28;
      const o = ctx.createOscillator(); o.type = 'sine';
      o.frequency.setValueAtTime(220, t); o.frequency.exponentialRampToValueAtTime(90, t + 0.12);
      const g = ctx.createGain(); g.gain.setValueAtTime(0.18, t); g.gain.exponentialRampToValueAtTime(0.001, t + 0.14);
      o.connect(g).connect(this.master); o.start(t); o.stop(t + 0.16);
    }
  }

  exhale() { this._burst(700, 0.5, 0.05, 0.6, 'bandpass'); }

  /** Свист «в два пальца»: резкий подъём, пауза, второй подъём с завитком. */
  whistle() {
    if (!this.enabled) return;
    const ctx = this.ctx, t0 = ctx.currentTime + 0.02;
    const seg = (t, f0, f1, f2, dur, vol) => {
      const o = ctx.createOscillator(); o.type = 'sine';
      o.frequency.setValueAtTime(f0, t);
      o.frequency.exponentialRampToValueAtTime(f1, t + dur * 0.55);
      o.frequency.exponentialRampToValueAtTime(f2, t + dur);
      const vib = ctx.createOscillator(); vib.frequency.value = 7;
      const vg = ctx.createGain(); vg.gain.value = 18;
      vib.connect(vg).connect(o.frequency);
      const g = ctx.createGain();
      g.gain.setValueAtTime(0, t);
      g.gain.linearRampToValueAtTime(vol, t + 0.04);
      g.gain.setValueAtTime(vol, t + dur - 0.06);
      g.gain.linearRampToValueAtTime(0, t + dur);
      // воздух вокруг тона
      const n = ctx.createBufferSource(); n.buffer = this.noise;
      const bp = ctx.createBiquadFilter(); bp.type = 'bandpass'; bp.Q.value = 8;
      bp.frequency.setValueAtTime(f0, t); bp.frequency.exponentialRampToValueAtTime(f1, t + dur * 0.55); bp.frequency.exponentialRampToValueAtTime(f2, t + dur);
      const ng = ctx.createGain(); ng.gain.value = 0.35;
      n.connect(bp).connect(ng).connect(g);
      o.connect(g).connect(this.master);
      o.start(t); o.stop(t + dur + 0.02); vib.start(t); vib.stop(t + dur + 0.02); n.start(t, Math.random() * 0.4, dur + 0.05);
    };
    seg(t0, 1300, 2700, 2500, 0.32, 0.16);
    seg(t0 + 0.42, 1500, 2900, 1700, 0.55, 0.16);
  }

  suspend() { this.ctx?.suspend(); }
  resume() { this.ctx?.resume(); }
}
