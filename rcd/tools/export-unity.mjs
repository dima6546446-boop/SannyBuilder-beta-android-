// Экспорт данных игры для Unity-версии (ZanosUnity): каталог машин -> C#, карты -> JSON.
// Запуск: node tools/export-unity.mjs   (данные в браузерной и Unity-версии всегда идентичны)
import fs from 'node:fs';
import path from 'node:path';
import { CARS, TUNING_PARTS, WHEELS, BODYKITS, SPOILERS, NEONS, PAINTS } from '../src/game/cars.js';
import { MAP_LIST, getMap } from '../src/game/maps.js';

const out = path.resolve('../ZanosUnity/Assets');
const q = (s) => JSON.stringify(String(s));
const d = (n) => (Number.isInteger(n) ? n + '.0' : String(n));
let cs = `// ГЕНЕРИРУЕТСЯ скриптом rcd/tools/export-unity.mjs — не редактировать вручную.
using System.Collections.Generic;

namespace Zanos.Core
{
    public static class CarCatalog
    {
        public static readonly CarSpec[] All = new CarSpec[]
        {
`;
for (const c of CARS) {
  cs += `            new CarSpec { Id = ${q(c.id)}, Brand = ${q(c.brand)}, Name = ${q(c.name)}, Body = ${q(c.body)}, Price = ${c.price}, Level = ${c.level}, Desc = ${q(c.desc)},
                Mass = ${d(c.mass)}, Wheelbase = ${d(c.wheelbase)}, WeightFront = ${d(c.weightFront)}, TrackFront = ${d(c.trackFront)}, TrackRear = ${d(c.trackRear)}, CgHeight = ${d(c.cgHeight)},
                Length = ${d(c.length)}, Width = ${d(c.width)}, Height = ${d(c.height)}, WheelRadius = ${d(c.wheelRadius)},
                PeakTorque = ${d(c.peakTorque)}, PeakRpm = ${d(c.peakRpm)}, Redline = ${d(c.redline)}, IdleRpm = ${d(c.idleRpm)}, Turbo = ${d(c.turbo)},
                Gears = new double[] { ${c.gears.map(d).join(', ')} }, ReverseRatio = ${d(c.reverseRatio)}, FinalDrive = ${d(c.finalDrive)},
                MaxSteerDeg = ${d(c.maxSteerDeg)}, BrakeTorque = ${d(c.brakeTorque)}, TireGrip = ${d(c.tireGrip)}, RearGripBias = ${d(c.rearGripBias)}, RollFront = ${d(c.rollFront)}, DiffLock = ${d(c.diffLock)}, CdA = ${d(c.cdA)},
                YawQuick = ${d(c.yawQuick ?? 1)}, TailHold = ${d(c.tailHold ?? 1)}, Color = ${q(c.color)} },
`;
}
cs += `        };

        public static CarSpec ById(string id) { foreach (var c in All) if (c.Id == id) return c; return All[0]; }

        public sealed class Part { public string Id, Name, Desc; public int Max, Price; public int Rgb; }
        public static readonly Part[] Tuning = new Part[]
        {
${TUNING_PARTS.map((p) => `            new Part { Id = ${q(p.id)}, Name = ${q(p.name)}, Desc = ${q(p.desc)}, Max = ${p.max}, Price = ${p.basePrice} },`).join('\n')}
        };
        public static readonly Part[] Wheels = new Part[]
        {
${WHEELS.map((p) => `            new Part { Id = ${q(p.id)}, Name = ${q(p.name)}, Price = ${p.price}, Max = ${p.spokes} },`).join('\n')}
        };
        public static readonly Part[] BodyKits = new Part[]
        {
${BODYKITS.map((p) => `            new Part { Id = ${q(p.id)}, Name = ${q(p.name)}, Price = ${p.price} },`).join('\n')}
        };
        public static readonly Part[] Spoilers = new Part[]
        {
${SPOILERS.map((p) => `            new Part { Id = ${q(p.id)}, Name = ${q(p.name)}, Price = ${p.price} },`).join('\n')}
        };
        public static readonly Part[] Neons = new Part[]
        {
${NEONS.map((p) => `            new Part { Id = ${q(p.id)}, Name = ${q(p.name)}, Price = ${p.price}, Rgb = ${p.color} },`).join('\n')}
        };
        public static readonly string[] Paints = new string[] { ${PAINTS.map(q).join(', ')} };
    }
}
`;
fs.mkdirSync(`${out}/Scripts/Core`, { recursive: true });
fs.writeFileSync(`${out}/Scripts/Core/CarCatalog.cs`, cs);

// карты -> JSON (JsonUtility-совместимые плоские структуры)
fs.mkdirSync(`${out}/Resources/Zanos/Maps`, { recursive: true });
const r3 = (v) => Math.round(v * 1000) / 1000;
for (const e of MAP_LIST) {
  const m = getMap(e.id);
  const flat = (a) => a.map(r3);
  const j = {
    id: m.id, name: m.name, desc: m.desc, level: m.level, ground: m.ground, theme: m.theme, time: m.time,
    spawn: m.spawn, bounds: m.bounds || { x0: -150, z0: -100, x1: 150, z1: 100 },
    segments: m.segments.map((s) => ({ a: r3(s[0]), b: r3(s[1]), c: r3(s[2]), d: r3(s[3]) })),
    boxes: m.boxes.map((b) => ({ x: r3(b.x), z: r3(b.z), w: r3(b.w), d: r3(b.d), rot: r3(b.rot || 0) })),
    circles: m.circles.map((c) => ({ x: r3(c.x), z: r3(c.z), r: r3(c.r) })),
    props: m.props.map((p) => ({ type: p.type, x: r3(p.x), z: r3(p.z), r: p.r, m: p.m, color: p.color || 0 })),
    surfaces: m.surfaces.map((s) => ({ kind: s.kind, type: s.type, x0: s.x0 ?? 0, z0: s.z0 ?? 0, x1: s.x1 ?? 0, z1: s.z1 ?? 0, x: s.x ?? 0, z: s.z ?? 0, r: s.r ?? 0, hw: s.hw ?? 0, closed: !!s.closed,
      minx: s.minx ?? 0, maxx: s.maxx ?? 0, minz: s.minz ?? 0, maxz: s.maxz ?? 0, pts: s.pts ? s.pts.flatMap((p) => [r3(p[0]), r3(p[1])]) : [] })),
    buildings: [...m.buildings, ...m.decoBuildings].map((b, i) => ({ x: r3(b.x), z: r3(b.z), w: r3(b.w), d: r3(b.d), h: r3(b.h), rot: r3(b.rot || 0), style: b.style, color: b.color, solid: i < m.buildings.length })),
    containers: m.containers.map((c) => ({ x: r3(c.x), z: r3(c.z), w: c.w, d: c.d, y: c.y, rot: r3(c.rot), color: c.color })),
    parked: m.parked.map((p) => ({ x: r3(p.x), z: r3(p.z), rot: r3(p.rot), color: p.color })),
    trees: m.trees.map((t) => ({ x: r3(t.x), z: r3(t.z), s: r3(t.s) })),
    lamps: m.lamps.map((l) => ({ x: r3(l.x), z: r3(l.z), h: l.h })),
    barriers: m.barriers.map((b) => ({ style: b.style, closed: !!b.closed, pts: b.pts.flatMap((p) => [r3(p[0]), r3(p[1])]) })),
    markings: m.markings.map((k) => ({ color: k.color, w: k.w, closed: !!k.closed, dashOn: k.dash ? k.dash[0] : 0, dashOff: k.dash ? k.dash[1] : 0, pts: k.pts.flatMap((p) => [r3(p[0]), r3(p[1])]) })),
    gates: m.gates.map((g) => ({ x1: r3(g.x1), z1: r3(g.z1), x2: r3(g.x2), z2: r3(g.z2), x: r3(g.x), z: r3(g.z) })),
    pads: m.pads.map((p) => ({ kind: p.kind, x: p.x, z: p.z, r: p.r ?? 0 })),
    challenges: m.challenges.map((c) => ({ id: c.id, type: c.type, name: c.name, desc: c.desc, target: c.target ?? 0, hold: c.hold ?? 0, time: c.time, needDrift: !!c.needDrift, money: c.reward.money, xp: c.reward.xp })),
    timedTime: m.timed.time, timedGoal: m.timed.goal,
  };
  fs.writeFileSync(`${out}/Resources/Zanos/Maps/${m.id}.json`, JSON.stringify(j));
}
console.log('Экспортировано: CarCatalog.cs и', MAP_LIST.length, 'карты');
