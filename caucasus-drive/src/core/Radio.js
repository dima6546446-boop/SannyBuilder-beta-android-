import { mulberry32 } from '../utils/math.js';

/**
 * Магнитола: три процедурные станции, музыка генерируется на WebAudio (0 КБ в APK).
 *  - «Кавказ FM»: быстрый размер 6/8 в духе лезгинки — доли (низкий «дум», звонкий «тек»),
 *    «гармошка» по гармоническому минору, бас по долям;
 *  - «Ретро ВАЗ»: синти-поп 80-х — бочка/малый, бас восьмыми, арпеджио;
 *  - «Дорожное»: спокойные аккорды электропиано и мягкий ритм.
 * Мелодии сочиняются на лету из гаммы (фраза повторяется AABB), так что эфир не надоедает.
 * Звук идёт через «автомобильный динамик» (срез верхов); пешком — глуше и тише с расстоянием.
 */
export const STATIONS = [
  { id: 'off', name: 'Радио выкл' },
  { id: 'caucasus', name: 'Кавказ FM 101.7', bpm: 138, steps: 6, sub: 2 },
  { id: 'retro', name: 'Ретро ВАЗ 88.3', bpm: 118, steps: 16, sub: 4 },
  { id: 'road', name: 'Дорожное 104.5', bpm: 84, steps: 8, sub: 2 },
];

const hz = (m) => 440 * Math.pow(2, (m - 69) / 12);   // MIDI → Гц

export class Radio {
  constructor(audio) {
    this.audio = audio;
    this.index = 0;
    this.rnd = mulberry32(2024);
    this.ready = false;
  }

  _init() {
    if (this.ready || !this.audio.ctx) return;
    const ctx = this.audio.ctx;
    this.ctx = ctx;
    this.out = ctx.createGain(); this.out.gain.value = 0;
    this.lp = ctx.createBiquadFilter(); this.lp.type = 'lowpass'; this.lp.frequency.value = 6000;
    this.out.connect(this.lp).connect(this.audio.master);
    this.ready = true;
  }

  get station() { return STATIONS[this.index]; }

  set(i) {
    this._init();
    this.index = ((i % STATIONS.length) + STATIONS.length) % STATIONS.length;
    this.step = 0;
    this.bar = 0;
    this.phrase = null;
    if (this.ctx) this.next = this.ctx.currentTime + 0.1;
    return this.station;
  }

  cycle() { return this.set(this.index + 1); }

  /** Каждый кадр: планируем ноты на 0,25 с вперёд. mix — громкость (0 — выкл/далеко). */
  update(mix = 1, muffled = false) {
    if (!this.ready) { this._init(); if (!this.ready) return; this.next = this.ctx.currentTime + 0.1; this.step = 0; this.bar = 0; }
    if (!this.audio.enabled) return;
    const ctx = this.ctx, t = ctx.currentTime;
    const on = this.index > 0;
    this.out.gain.setTargetAtTime(on ? 0.3 * mix * (this.audio.volume ?? 1) : 0, t, 0.15);
    this.lp.frequency.setTargetAtTime(muffled ? 700 : 6000, t, 0.2);
    if (!on || mix <= 0.01) { this.next = t + 0.05; return; }
    const st = this.station;
    const stepDur = 60 / st.bpm / st.sub;
    if (this.next < t) this.next = t + 0.02;
    while (this.next < t + 0.25) {
      this[`_${st.id}`](this.step, this.next, stepDur);
      this.next += stepDur;
      this.step = (this.step + 1) % st.steps;
      if (this.step === 0) this.bar++;
    }
  }

  // ---------------------------------------------------------------- инструменты
  _env(node, t, a, d, peak) {
    const g = this.ctx.createGain();
    g.gain.setValueAtTime(0, t);
    g.gain.linearRampToValueAtTime(peak, t + a);
    g.gain.exponentialRampToValueAtTime(0.0008, t + a + d);
    node.connect(g).connect(this.out);
    return g;
  }

  _tone(t, f, dur, type = 'sawtooth', vol = 0.2, cutoff = 2000, vib = 0) {
    const ctx = this.ctx;
    const o = ctx.createOscillator(); o.type = type; o.frequency.value = f;
    if (vib) {
      const l = ctx.createOscillator(); l.frequency.value = 6;
      const lg = ctx.createGain(); lg.gain.value = f * vib;
      l.connect(lg).connect(o.frequency); l.start(t); l.stop(t + dur + 0.05);
    }
    const flt = ctx.createBiquadFilter(); flt.type = 'lowpass'; flt.frequency.value = cutoff;
    o.connect(flt);
    this._env(flt, t, 0.01, dur, vol);
    o.start(t); o.stop(t + dur + 0.05);
  }

  _drum(t, f0, f1, dur, vol) {
    const o = this.ctx.createOscillator(); o.type = 'sine';
    o.frequency.setValueAtTime(f0, t); o.frequency.exponentialRampToValueAtTime(f1, t + dur);
    this._env(o, t, 0.003, dur, vol);
    o.start(t); o.stop(t + dur + 0.02);
  }

  _noise(t, dur, freq, q, vol, type = 'bandpass') {
    const s = this.ctx.createBufferSource(); s.buffer = this.audio.noise;
    const f = this.ctx.createBiquadFilter(); f.type = type; f.frequency.value = freq; f.Q.value = q;
    s.connect(f);
    this._env(f, t, 0.002, dur, vol);
    s.start(t, Math.random() * 0.5, dur + 0.05);
  }

  /** Фраза из гаммы: случайное блуждание с притяжением к тонике. */
  _makePhrase(scale, len, density) {
    const r = this.rnd, notes = [];
    let i = 7;
    for (let k = 0; k < len; k++) {
      if (r() > density) { notes.push(null); continue; }
      i += Math.round((r() - 0.5) * 4);
      if (k % 6 === 0 && r() < 0.4) i = 7 + (r() < 0.5 ? 0 : 4);
      i = Math.max(0, Math.min(scale.length - 1, i));
      notes.push(scale[i]);
    }
    return notes;
  }

  // ---------------------------------------------------------------- станции
  /** «Кавказ FM»: 6/8, ~138 уд/мин, ля гармонический минор. */
  _caucasus(step, t, sd) {
    // доли: дум . тек дум тек тек
    if (step === 0 || step === 3) this._drum(t, 120, 55, 0.18, 0.55);
    if (step === 2 || step === 4 || step === 5) this._noise(t, 0.05, 3200, 1.2, 0.18);
    if (step === 1 && this.bar % 2) this._noise(t, 0.04, 5000, 2, 0.08);
    // бас по долям: Am Am Dm E
    const roots = [45, 45, 50, 52];
    if (step === 0 || step === 3) this._tone(t, hz(roots[(this.bar >> 1) % 4] - 12 + (step === 3 ? 7 : 0)), sd * 2.6, 'triangle', 0.32, 600);
    // «гармошка»: фраза из 4 тактов, форма AABB
    const scale = [57, 59, 60, 62, 64, 65, 68, 69, 71, 72, 74, 76, 77, 80, 81]; // A гарм. минор
    const blk = Math.floor(this.bar / 8);
    if (!this.phrase || this._blk !== blk) { this._blk = blk; this.phrase = this._makePhrase(scale, 24, 0.85); }
    const n = this.phrase[(this.bar % 4) * 6 + step];
    if (n) { this._tone(t, hz(n), sd * 0.95, 'sawtooth', 0.11, 2600, 0.006); this._tone(t, hz(n + 12), sd * 0.9, 'square', 0.035, 3000); }
  }

  /** «Ретро ВАЗ»: 16-е, 118 уд/мин, Am F C G. */
  _retro(step, t, sd) {
    if (step === 0 || step === 8 || (step === 10 && this.bar % 2)) this._drum(t, 150, 45, 0.22, 0.6);
    if (step === 4 || step === 12) this._noise(t, 0.14, 1800, 0.8, 0.22);
    if (step % 2 === 0) this._noise(t, 0.03, 8000, 1, 0.06, 'highpass');
    const chords = [[57, 60, 64], [53, 57, 60], [48, 52, 55], [55, 59, 62]];
    const ch = chords[this.bar % 4];
    if (step % 2 === 0) this._tone(t, hz(ch[0] - 24 + (step % 4 === 2 ? 12 : 0)), sd * 1.8, 'sawtooth', 0.16, 700);
    if (step === 0) for (const m of ch) this._tone(t, hz(m), sd * 15, 'triangle', 0.035, 1500);
    const arp = [0, 1, 2, 1];
    if (this.bar % 8 >= 4) this._tone(t, hz(ch[arp[step % 4]] + 12), sd * 0.8, 'square', 0.05, 2400);
    else if (step % 4 === 0) {
      if (!this.phrase || this._blk !== this.bar >> 3) { this._blk = this.bar >> 3; this.phrase = this._makePhrase([57, 60, 62, 64, 67, 69, 72, 74, 76, 79, 81], 16, 0.7); }
      const n = this.phrase[(this.bar % 4) * 4 + step / 4];
      if (n) this._tone(t, hz(n + 12), sd * 3.5, 'square', 0.06, 2800, 0.004);
    }
  }

  /** «Дорожное»: 84 уд/мин, Dm7 G7 Cmaj7 Am7, мягко. */
  _road(step, t, sd) {
    if (step === 0 || step === 5) this._drum(t, 110, 50, 0.25, 0.4);
    if (step === 4) this._noise(t, 0.12, 2200, 0.7, 0.12);
    if (step % 2 === 1) this._noise(t, 0.025, 7000, 1, 0.035, 'highpass');
    const chords = [[50, 53, 57, 60], [55, 59, 62, 65], [48, 52, 55, 59], [45, 48, 52, 55]];
    const ch = chords[this.bar % 4];
    if (step === 0) for (const m of ch) this._tone(t, hz(m + 12), sd * 7, 'sine', 0.07, 1800);
    if (step === 0 || step === 6) this._tone(t, hz(ch[0] - 12), sd * 3, 'triangle', 0.25, 500);
    if (step === 3 && this.bar % 2) this._tone(t, hz(ch[2 + ((this.bar >> 1) % 2)] + 24), sd * 2.5, 'sine', 0.06, 3000);
  }
}
