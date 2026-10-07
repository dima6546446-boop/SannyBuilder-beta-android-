// Процедурный звук на Web Audio: двигатель по оборотам и нагрузке, визг шин, ветер, удары, интерфейс, музыка.
// Никаких внешних файлов — все звуки синтезируются.
export class AudioSystem {
  constructor() {
    this.ctx = null; this.ready = false;
    this.vol = { master: 0.8, engine: 0.7, sfx: 0.8, music: 0.4 };
    this.prevThrottle = 0; this.prevRpm = 0;
  }

  /** Создание контекста — только после жеста пользователя. */
  unlock() {
    if (this.ctx) { if (this.ctx.state === 'suspended') this.ctx.resume(); return; }
    const AC = window.AudioContext || window.webkitAudioContext;
    if (!AC) return;
    const ctx = this.ctx = new AC();
    this.master = ctx.createGain(); this.master.connect(ctx.destination);
    const comp = ctx.createDynamicsCompressor(); comp.threshold.value = -14; comp.ratio.value = 6; comp.connect(this.master); this.bus = comp;
    this.engineBus = ctx.createGain(); this.engineBus.connect(this.bus);
    this.sfxBus = ctx.createGain(); this.sfxBus.connect(this.bus);
    this.musicBus = ctx.createGain(); this.musicBus.connect(this.bus);
    this.noiseBuf = this.makeNoise();
    this.buildEngine(); this.buildTire(); this.buildWind();
    this.applyVolumes();
    this.ready = true;
  }

  makeNoise() {
    const ctx = this.ctx, b = ctx.createBuffer(1, ctx.sampleRate * 2, ctx.sampleRate), d = b.getChannelData(0);
    let last = 0;
    for (let i = 0; i < d.length; i++) { const w = Math.random() * 2 - 1; last = (last + 0.02 * w) / 1.02; d[i] = w * 0.6 + last * 3; }
    return b;
  }
  noiseSrc(loop = true) { const s = this.ctx.createBufferSource(); s.buffer = this.noiseBuf; s.loop = loop; return s; }

  setVolumes(v) { Object.assign(this.vol, v); this.applyVolumes(); }
  applyVolumes() {
    if (!this.ready) return;
    const t = this.ctx.currentTime;
    this.master.gain.setTargetAtTime(this.vol.master, t, 0.05);
    this.engineBus.gain.setTargetAtTime(this.vol.engine * 0.55, t, 0.05);
    this.sfxBus.gain.setTargetAtTime(this.vol.sfx, t, 0.05);
    this.musicBus.gain.setTargetAtTime(this.vol.music * 0.5, t, 0.2);
  }

  buildEngine() {
    const ctx = this.ctx;
    this.eng = { };
    const o1 = ctx.createOscillator(); o1.type = 'sawtooth';
    const o2 = ctx.createOscillator(); o2.type = 'square';
    const o3 = ctx.createOscillator(); o3.type = 'triangle';
    const shaper = ctx.createWaveShaper();
    const curve = new Float32Array(256); for (let i = 0; i < 256; i++) { const x = i / 128 - 1; curve[i] = Math.tanh(x * 2.4); } shaper.curve = curve;
    const lp = ctx.createBiquadFilter(); lp.type = 'lowpass'; lp.Q.value = 2.5;
    const gain = ctx.createGain(); gain.gain.value = 0;
    const g1 = ctx.createGain(), g2 = ctx.createGain(), g3 = ctx.createGain();
    g1.gain.value = 0.5; g2.gain.value = 0.22; g3.gain.value = 0.35;
    o1.connect(g1); o2.connect(g2); o3.connect(g3); g1.connect(shaper); g2.connect(shaper); g3.connect(shaper);
    shaper.connect(lp); lp.connect(gain); gain.connect(this.engineBus);
    // пульсации (такты цилиндров)
    const lfo = ctx.createOscillator(); lfo.frequency.value = 30; const lfoG = ctx.createGain(); lfoG.gain.value = 0.18; lfo.connect(lfoG); lfoG.connect(gain.gain);
    // шум впуска
    const n = this.noiseSrc(); const nlp = ctx.createBiquadFilter(); nlp.type = 'bandpass'; nlp.Q.value = 0.8; nlp.frequency.value = 900; const ng = ctx.createGain(); ng.gain.value = 0; n.connect(nlp); nlp.connect(ng); ng.connect(this.engineBus);
    // турбина
    const tu = ctx.createOscillator(); tu.type = 'sine'; const tg = ctx.createGain(); tg.gain.value = 0; tu.connect(tg); tg.connect(this.engineBus);
    [o1, o2, o3, lfo, n, tu].forEach((x) => x.start());
    this.eng = { o1, o2, o3, lp, gain, lfo, lfoG, nlp, ng, tu, tg };
  }
  buildTire() {
    const ctx = this.ctx;
    const mk = (f, q) => { const n = this.noiseSrc(); const bp = ctx.createBiquadFilter(); bp.type = 'bandpass'; bp.frequency.value = f; bp.Q.value = q; const g = ctx.createGain(); g.gain.value = 0; n.connect(bp); bp.connect(g); g.connect(this.sfxBus); n.start(); return { bp, g }; };
    this.tire = { hi: mk(1700, 7), lo: mk(620, 3), gravel: mk(3200, 0.7) };
  }
  buildWind() {
    const ctx = this.ctx; const n = this.noiseSrc(); const lp = ctx.createBiquadFilter(); lp.type = 'lowpass'; lp.frequency.value = 700; const g = ctx.createGain(); g.gain.value = 0;
    n.connect(lp); lp.connect(g); g.connect(this.sfxBus); n.start(); this.wind = { lp, g };
  }

  /** Обновление звука машины каждый кадр. */
  update(car, throttle, surface = 'asphalt', paused = false) {
    if (!this.ready) return;
    const t = this.ctx.currentTime, E = this.eng;
    if (paused) { E.gain.gain.setTargetAtTime(0, t, 0.05); this.tire.hi.g.gain.setTargetAtTime(0, t, 0.05); this.tire.lo.g.gain.setTargetAtTime(0, t, 0.05); this.tire.gravel.g.gain.setTargetAtTime(0, t, 0.05); this.wind.g.gain.setTargetAtTime(0, t, 0.05); return; }
    const rpm = car.rpm, s = car.spec;
    const rn = Math.min(1, rpm / s.redline);
    const f = rpm / 60 * 2;                                  // частота зажигания 4-цилиндрового
    const thr = car.throttleApplied ?? throttle;
    const cut = car.limiter > 0 ? 0.4 : 1;
    E.o1.frequency.setTargetAtTime(f, t, 0.02);
    E.o2.frequency.setTargetAtTime(f * 0.5, t, 0.02);
    E.o3.frequency.setTargetAtTime(f * 2.01, t, 0.02);
    E.lfo.frequency.setTargetAtTime(Math.max(8, f * 0.5), t, 0.05);
    E.lp.frequency.setTargetAtTime(380 + rn * 1500 + thr * 1900, t, 0.04);
    E.gain.gain.setTargetAtTime((0.1 + rn * 0.22 + thr * 0.3) * cut, t, 0.03);
    E.lfoG.gain.setTargetAtTime(0.1 + 0.12 * thr, t, 0.05);
    E.nlp.frequency.setTargetAtTime(500 + rn * 2200, t, 0.05);
    E.ng.gain.setTargetAtTime(thr * rn * 0.3, t, 0.05);
    E.tu.frequency.setTargetAtTime(1800 + car.boost * 3200, t, 0.05);
    E.tg.gain.setTargetAtTime(car.boost * 0.035 * (s.turbo > 0 ? 1 : 0), t, 0.08);
    // выхлопные хлопки при сбросе газа на высоких оборотах
    if (this.prevThrottle > 0.6 && thr < 0.1 && rn > 0.6 && s.turbo > 0) this.pop(0.35);
    if (this.prevThrottle > 0.6 && thr < 0.1 && rn > 0.55 && s.turbo === 0 && Math.random() < 0.3) this.pop(0.18);
    this.prevThrottle = thr;
    // шины
    let sl = 0, front = 0;
    for (let i = 0; i < 4; i++) { sl = Math.max(sl, car.slip[i]); if (i < 2) front = Math.max(front, car.slip[i]); }
    const gravel = surface === 'gravel' || surface === 'dirt' || surface === 'grass';
    const kmh = car.speed * 3.6;
    const sp = Math.min(1, car.speed / 10);
    const fq = 1100 + Math.min(1, kmh / 160) * 900;
    const sq = sl * sp * (gravel ? 0 : 1);
    this.tire.hi.g.gain.setTargetAtTime(Math.min(0.6, sq * 0.55), t, 0.04);
    this.tire.hi.bp.frequency.setTargetAtTime(fq + front * 400, t, 0.05);
    this.tire.lo.g.gain.setTargetAtTime(Math.min(0.5, sq * 0.38), t, 0.04);
    this.tire.lo.bp.frequency.setTargetAtTime(500 + kmh * 3, t, 0.05);
    this.tire.gravel.g.gain.setTargetAtTime(gravel ? Math.min(0.5, (sl * 0.5 + Math.min(1, car.speed / 30) * 0.25) * sp) : 0, t, 0.05);
    // ветер
    const w = Math.min(1, kmh / 180);
    this.wind.g.gain.setTargetAtTime(w * w * 0.35, t, 0.1); this.wind.lp.frequency.setTargetAtTime(300 + w * 1600, t, 0.1);
  }

  pop(v = 0.3) {
    if (!this.ready) return; const t = this.ctx.currentTime;
    const n = this.noiseSrc(false), g = this.ctx.createGain(), lp = this.ctx.createBiquadFilter(); lp.type = 'lowpass'; lp.frequency.value = 1400;
    g.gain.setValueAtTime(v, t); g.gain.exponentialRampToValueAtTime(0.001, t + 0.12); n.connect(lp); lp.connect(g); g.connect(this.sfxBus); n.start(t, Math.random(), 0.15);
  }
  shift(turbo) {
    if (!this.ready) return; const t = this.ctx.currentTime;
    this.pop(0.12);
    if (turbo) { const n = this.noiseSrc(false), bp = this.ctx.createBiquadFilter(); bp.type = 'highpass'; bp.frequency.value = 2500; const g = this.ctx.createGain(); g.gain.setValueAtTime(0.18, t); g.gain.exponentialRampToValueAtTime(0.001, t + 0.35); n.connect(bp); bp.connect(g); g.connect(this.sfxBus); n.start(t, Math.random(), 0.4); }
  }
  impact(speed) {
    if (!this.ready) return; const t = this.ctx.currentTime, ctx = this.ctx;
    const v = Math.min(1, speed / 14);
    const n = this.noiseSrc(false), lp = ctx.createBiquadFilter(); lp.type = 'lowpass'; lp.frequency.value = 600 + v * 2500; const g = ctx.createGain();
    g.gain.setValueAtTime(0.25 + v * 0.7, t); g.gain.exponentialRampToValueAtTime(0.001, t + 0.25 + v * 0.3); n.connect(lp); lp.connect(g); g.connect(this.sfxBus); n.start(t, Math.random(), 0.7);
    const o = ctx.createOscillator(); o.type = 'sine'; o.frequency.setValueAtTime(130, t); o.frequency.exponentialRampToValueAtTime(45, t + 0.2);
    const og = ctx.createGain(); og.gain.setValueAtTime(0.5 + v * 0.5, t); og.gain.exponentialRampToValueAtTime(0.001, t + 0.3); o.connect(og); og.connect(this.sfxBus); o.start(t); o.stop(t + 0.35);
    // металлический звон
    const m = ctx.createOscillator(); m.type = 'square'; m.frequency.value = 900 + Math.random() * 500; const mg = ctx.createGain(); mg.gain.setValueAtTime(0.05 * v, t); mg.gain.exponentialRampToValueAtTime(0.001, t + 0.2); m.connect(mg); mg.connect(this.sfxBus); m.start(t); m.stop(t + 0.25);
  }
  tick(v = 0.2) { if (!this.ready) return; const t = this.ctx.currentTime, o = this.ctx.createOscillator(), g = this.ctx.createGain(); o.type = 'triangle'; o.frequency.setValueAtTime(700, t); o.frequency.exponentialRampToValueAtTime(300, t + 0.07); g.gain.setValueAtTime(v, t); g.gain.exponentialRampToValueAtTime(0.001, t + 0.1); o.connect(g); g.connect(this.sfxBus); o.start(t); o.stop(t + 0.12); }

  // --- интерфейс ---
  tone(freq, dur = 0.12, type = 'sine', vol = 0.2, delay = 0, slide = 0) {
    if (!this.ready) return; const t = this.ctx.currentTime + delay, o = this.ctx.createOscillator(), g = this.ctx.createGain();
    o.type = type; o.frequency.setValueAtTime(freq, t); if (slide) o.frequency.exponentialRampToValueAtTime(freq * slide, t + dur);
    g.gain.setValueAtTime(0.0001, t); g.gain.exponentialRampToValueAtTime(vol, t + 0.01); g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    o.connect(g); g.connect(this.sfxBus); o.start(t); o.stop(t + dur + 0.02);
  }
  click() { this.tone(520, 0.07, 'triangle', 0.18, 0, 1.4); }
  hover() { this.tone(900, 0.04, 'sine', 0.05); }
  back() { this.tone(420, 0.08, 'triangle', 0.16, 0, 0.7); }
  buy() { this.tone(660, 0.1, 'square', 0.1); this.tone(880, 0.12, 'square', 0.1, 0.09); this.tone(1320, 0.2, 'square', 0.09, 0.18); }
  error() { this.tone(180, 0.18, 'sawtooth', 0.14, 0, 0.7); }
  success() { [523, 659, 784, 1046].forEach((f, i) => this.tone(f, 0.18, 'triangle', 0.18, i * 0.09)); }
  levelUp() { [392, 523, 659, 784, 1046, 1318].forEach((f, i) => this.tone(f, 0.22, 'square', 0.1, i * 0.08)); }
  bank(points) { this.tone(880, 0.08, 'triangle', 0.14); this.tone(1175, 0.14, 'triangle', 0.14, 0.07); if (points > 1500) this.tone(1568, 0.2, 'triangle', 0.12, 0.15); }
  lost() { this.tone(220, 0.3, 'sawtooth', 0.12, 0, 0.5); }
  mult() { this.tone(740, 0.06, 'square', 0.07); }
  gate() { this.tone(988, 0.08, 'triangle', 0.14); this.tone(1318, 0.12, 'triangle', 0.14, 0.07); }
  countdown(go) { this.tone(go ? 880 : 520, go ? 0.4 : 0.12, 'square', 0.14); }

  // --- музыка: процедурный синтвейв-луп ---
  startMusic() {
    if (!this.ready || this.musicOn) return; this.musicOn = true;
    const ctx = this.ctx, bpm = 112, step = 60 / bpm / 4;
    this.musicStep = 0; this.musicNext = ctx.currentTime + 0.1;
    const bass = [45, 45, 52, 45, 48, 48, 55, 48, 43, 43, 50, 43, 47, 47, 54, 52]; // MIDI
    const arp = [69, 72, 76, 72, 74, 77, 81, 77, 67, 71, 74, 71, 71, 74, 79, 74];
    const mtof = (m) => 440 * Math.pow(2, (m - 69) / 12);
    const sched = () => {
      if (!this.musicOn) return;
      while (this.musicNext < ctx.currentTime + 0.3) {
        const s = this.musicStep % 16, t = this.musicNext;
        // бас
        if (s % 2 === 0) this.musicNote(mtof(bass[s]), t, step * 1.7, 'sawtooth', 0.22, 420);
        // арпеджио
        this.musicNote(mtof(arp[s]), t, step * 0.9, 'square', 0.05, 2400);
        // ударные
        if (s % 4 === 0) this.musicKick(t);
        if (s % 4 === 2) this.musicHat(t, 0.12);
        if (s % 8 === 4) this.musicSnare(t);
        this.musicStep++; this.musicNext += step;
      }
      this.musicTimer = setTimeout(sched, 100);
    };
    sched();
  }
  stopMusic() { this.musicOn = false; clearTimeout(this.musicTimer); }
  musicNote(f, t, dur, type, vol, cutoff) {
    const ctx = this.ctx, o = ctx.createOscillator(), g = ctx.createGain(), lp = ctx.createBiquadFilter(); lp.type = 'lowpass'; lp.frequency.value = cutoff;
    o.type = type; o.frequency.value = f; g.gain.setValueAtTime(0.0001, t); g.gain.exponentialRampToValueAtTime(vol, t + 0.01); g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    o.connect(lp); lp.connect(g); g.connect(this.musicBus); o.start(t); o.stop(t + dur + 0.05);
  }
  musicKick(t) { const ctx = this.ctx, o = ctx.createOscillator(), g = ctx.createGain(); o.frequency.setValueAtTime(140, t); o.frequency.exponentialRampToValueAtTime(40, t + 0.14); g.gain.setValueAtTime(0.7, t); g.gain.exponentialRampToValueAtTime(0.001, t + 0.2); o.connect(g); g.connect(this.musicBus); o.start(t); o.stop(t + 0.22); }
  musicHat(t, v) { const ctx = this.ctx, n = this.noiseSrc(false), hp = ctx.createBiquadFilter(); hp.type = 'highpass'; hp.frequency.value = 7000; const g = ctx.createGain(); g.gain.setValueAtTime(v, t); g.gain.exponentialRampToValueAtTime(0.001, t + 0.05); n.connect(hp); hp.connect(g); g.connect(this.musicBus); n.start(t, Math.random(), 0.08); }
  musicSnare(t) { const ctx = this.ctx, n = this.noiseSrc(false), bp = ctx.createBiquadFilter(); bp.type = 'bandpass'; bp.frequency.value = 1800; const g = ctx.createGain(); g.gain.setValueAtTime(0.35, t); g.gain.exponentialRampToValueAtTime(0.001, t + 0.16); n.connect(bp); bp.connect(g); g.connect(this.musicBus); n.start(t, Math.random(), 0.2); }
}
