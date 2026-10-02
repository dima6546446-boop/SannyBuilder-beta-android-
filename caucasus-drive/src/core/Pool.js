/**
 * Object pool: все объекты создаются один раз при загрузке.
 * В игровом цикле нет new → нет пауз сборщика мусора (критично на Android).
 */
export class Pool {
  constructor(factory, size) {
    this.all = [];
    this.free = [];
    this.active = [];
    for (let i = 0; i < size; i++) {
      const o = factory(i);
      this.all.push(o);
      this.free.push(o);
    }
  }

  acquire() {
    const o = this.free.pop();
    if (!o) return null;
    this.active.push(o);
    return o;
  }

  release(o) {
    const i = this.active.indexOf(o);
    if (i < 0) return;
    const last = this.active.pop();
    if (i < this.active.length) this.active[i] = last; // swap-remove, O(1)
    this.free.push(o);
  }

  get activeCount() { return this.active.length; }
}
