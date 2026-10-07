using System;
using System.Collections.Generic;

namespace Zanos.Core
{
    public struct ImpactEvent { public double Speed, X, Z, Nx, Nz; public string Kind; public Prop Prop; }

    /// <summary>Динамический объект (конус, бочка).</summary>
    public sealed class Prop
    {
        public int Id; public string Type; public double X, Z, X0, Z0, Vx, Vz, R, M, Rot, Spin; public bool Hit; public int Color;
    }

    /// <summary>
    /// Мир: статические препятствия, динамические объекты, покрытия и удары (импульсы с трением и упругостью).
    /// Порт rcd/src/physics/world.js.
    /// </summary>
    public sealed class World
    {
        const double Cell = 16;
        public readonly MapData Map;
        readonly List<double[]> segs = new List<double[]>();
        readonly Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();
        readonly List<CircleData> circles = new List<CircleData>();
        public readonly List<Prop> Props = new List<Prop>();
        readonly List<SurfaceData> regions = new List<SurfaceData>();   // в обратном порядке: последние перекрывают первые
        readonly Surface ground;
        readonly Random rnd = new Random(7);

        public World(MapData map)
        {
            Map = map;
            foreach (var s in map.segments) AddSegment(new[] { s.a, s.b, s.c, s.d });
            foreach (var b in map.boxes) foreach (var s in BoxSegments(b.x, b.z, b.w, b.d, b.rot)) AddSegment(s);
            foreach (var c in map.circles) circles.Add(c);
            int id = 0;
            foreach (var p in map.props)
                Props.Add(new Prop { Id = id++, Type = p.type, X = p.x, Z = p.z, X0 = p.x, Z0 = p.z, R = p.r > 0 ? p.r : 0.22, M = p.m > 0 ? p.m : 4 });
            for (int i = map.surfaces.Length - 1; i >= 0; i--) regions.Add(map.surfaces[i]);
            ground = Surface.ByName(map.ground);
        }

        static long Key(int cx, int cz) { return (long)cx * 73856093L ^ (long)cz * 19349663L; }

        public static List<double[]> BoxSegments(double x, double z, double w, double d, double rot)
        {
            double c = Math.Cos(rot), s = Math.Sin(rot);
            double[][] loc = { new[] { -w / 2, -d / 2 }, new[] { w / 2, -d / 2 }, new[] { w / 2, d / 2 }, new[] { -w / 2, d / 2 } };
            var pts = new double[4][];
            for (int i = 0; i < 4; i++) pts[i] = new[] { x + loc[i][0] * c + loc[i][1] * s, z - loc[i][0] * s + loc[i][1] * c };
            var res = new List<double[]>();
            for (int i = 0; i < 4; i++) { var a = pts[i]; var b = pts[(i + 1) % 4]; res.Add(new[] { a[0], a[1], b[0], b[1] }); }
            return res;
        }

        void AddSegment(double[] s)
        {
            int idx = segs.Count; segs.Add(s);
            int minx = (int)Math.Floor(Math.Min(s[0], s[2]) / Cell), maxx = (int)Math.Floor(Math.Max(s[0], s[2]) / Cell);
            int minz = (int)Math.Floor(Math.Min(s[1], s[3]) / Cell), maxz = (int)Math.Floor(Math.Max(s[1], s[3]) / Cell);
            for (int cx = minx; cx <= maxx; cx++)
                for (int cz = minz; cz <= maxz; cz++)
                {
                    List<int> a; long k = Key(cx, cz);
                    if (!grid.TryGetValue(k, out a)) { a = new List<int>(); grid[k] = a; }
                    a.Add(idx);
                }
        }

        public static double ClosestOnSegment(double px, double pz, double ax, double az, double bx, double bz, out double ox, out double oz)
        {
            double dx = bx - ax, dz = bz - az, l2 = dx * dx + dz * dz;
            double t = l2 > 1e-9 ? ((px - ax) * dx + (pz - az) * dz) / l2 : 0;
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            ox = ax + dx * t; oz = az + dz * t;
            return Math.Sqrt((px - ox) * (px - ox) + (pz - oz) * (pz - oz));
        }

        /// <summary>Покрытие в точке (с учётом погоды — через Session).</summary>
        public Surface SurfaceAt(double x, double z)
        {
            foreach (var r in regions)
            {
                if (r.kind == "rect") { if (x >= r.x0 && x <= r.x1 && z >= r.z0 && z <= r.z1) return Surface.ByName(r.type); }
                else if (r.kind == "circle") { if (Math.Sqrt((x - r.x) * (x - r.x) + (z - r.z) * (z - r.z)) <= r.r) return Surface.ByName(r.type); }
                else if (r.kind == "ribbon")
                {
                    if (x < r.minx || x > r.maxx || z < r.minz || z > r.maxz) continue;
                    int n = r.pts.Length / 2, cnt = r.closed ? n : n - 1;
                    for (int i = 0; i < cnt; i++)
                    {
                        int j = (i + 1) % n; double ox, oz;
                        if (ClosestOnSegment(x, z, r.pts[i * 2], r.pts[i * 2 + 1], r.pts[j * 2], r.pts[j * 2 + 1], out ox, out oz) <= r.hw) return Surface.ByName(r.type);
                    }
                }
            }
            return ground;
        }

        void NearSegments(double x, double z, double r, List<int> outList)
        {
            outList.Clear();
            int cx0 = (int)Math.Floor((x - r) / Cell), cx1 = (int)Math.Floor((x + r) / Cell);
            int cz0 = (int)Math.Floor((z - r) / Cell), cz1 = (int)Math.Floor((z + r) / Cell);
            for (int cx = cx0; cx <= cx1; cx++)
                for (int cz = cz0; cz <= cz1; cz++)
                {
                    List<int> a; if (!grid.TryGetValue(Key(cx, cz), out a)) continue;
                    foreach (int i in a) if (!outList.Contains(i)) outList.Add(i);
                }
        }

        struct Circ { public double X, Z, R; }
        readonly List<Circ> cc = new List<Circ>();
        readonly List<int> near = new List<int>();

        void CarCircles(Car car, List<Circ> o)
        {
            var s = car.Spec; double r = s.Width * 0.5 * 0.92;
            double half = Math.Max(0, s.Length * 0.5 - r);
            double fx = Math.Sin(car.H), fz = Math.Cos(car.H);
            o.Clear();
            for (int i = 0; i < 3; i++) { double t = (i / 2.0 * 2 - 1) * half; o.Add(new Circ { X = car.X + fx * t, Z = car.Z + fz * t, R = r }); }
        }

        /// <summary>Столкновения машины с миром; возвращает наибольшую скорость удара за шаг.</summary>
        public double Collide(Car car, double dt, Action<ImpactEvent> onImpact)
        {
            CarCircles(car, cc);
            double maxImpact = 0, m = car.Spec.Mass, Iz = car.Iz;
            for (int iter = 0; iter < 2; iter++)
            {
                for (int ci = 0; ci < cc.Count; ci++)
                {
                    Circ c = cc[ci];                                      // копия: как в JS, пересчёт кругов её не меняет
                    NearSegments(c.X, c.Z, c.R + 0.5, near);
                    var nearCopy = new List<int>(near);
                    foreach (int idx in nearCopy)
                    {
                        var s = segs[idx]; double cpx, cpz;
                        double d = ClosestOnSegment(c.X, c.Z, s[0], s[1], s[2], s[3], out cpx, out cpz);
                        if (d < c.R)
                        {
                            double nx, nz;
                            if (d > 1e-6) { nx = (c.X - cpx) / d; nz = (c.Z - cpz) / d; }
                            else { double lx = s[2] - s[0], lz = s[3] - s[1], ll = Math.Sqrt(lx * lx + lz * lz); if (ll == 0) ll = 1; nx = -lz / ll; nz = lx / ll; }
                            maxImpact = Math.Max(maxImpact, Resolve(car, cpx, cpz, nx, nz, c.R - d, double.PositiveInfinity, m, Iz, onImpact, "wall", null));
                            CarCircles(car, cc);
                        }
                    }
                    foreach (var o in circles)
                    {
                        double d = Math.Sqrt((c.X - o.x) * (c.X - o.x) + (c.Z - o.z) * (c.Z - o.z));
                        if (d < c.R + o.r)
                        {
                            double nx = d > 1e-6 ? (c.X - o.x) / d : 1, nz = d > 1e-6 ? (c.Z - o.z) / d : 0;
                            maxImpact = Math.Max(maxImpact, Resolve(car, o.x + nx * o.r, o.z + nz * o.r, nx, nz, c.R + o.r - d, double.PositiveInfinity, m, Iz, onImpact, "pole", null));
                            CarCircles(car, cc);
                        }
                    }
                    foreach (var p in Props)
                    {
                        double dx = c.X - p.X, dz = c.Z - p.Z;
                        if (dx > 6 || dx < -6 || dz > 6 || dz < -6) continue;
                        double d = Math.Sqrt(dx * dx + dz * dz);
                        if (d < c.R + p.R)
                        {
                            double nx = d > 1e-6 ? dx / d : 1, nz = d > 1e-6 ? dz / d : 0;
                            Resolve(car, p.X + nx * p.R, p.Z + nz * p.R, nx, nz, 0, p.M, m, Iz, onImpact, "prop", p);
                            p.Hit = true;
                            CarCircles(car, cc);
                        }
                    }
                }
            }
            return maxImpact;
        }

        double Resolve(Car car, double px, double pz, double nx, double nz, double pen, double otherMass, double m, double Iz, Action<ImpactEvent> onImpact, string kind, Prop prop)
        {
            if (pen > 0) { car.X += nx * pen; car.Z += nz * pen; }
            double rx = px - car.X, rz = pz - car.Z;
            double vpx = car.Vx + car.W * rz, vpz = car.Vz - car.W * rx;
            double ovx = 0, ovz = 0;
            if (prop != null) { ovx = prop.Vx; ovz = prop.Vz; }
            double rvx = vpx - ovx, rvz = vpz - ovz;
            double vn = rvx * nx + rvz * nz;
            if (vn >= 0) return 0;
            double rn = rz * nx - rx * nz;
            double invM = 1 / m + (double.IsInfinity(otherMass) ? 0 : 1 / otherMass);
            double e = prop != null ? 0.5 : Phys.Restitution;
            double denom = invM + (rn * rn) / Iz;
            double j = -(1 + e) * vn / denom;
            car.Vx += nx * j / m; car.Vz += nz * j / m;
            car.W += (rz * nx * j - rx * nz * j) / Iz;
            double tx = -nz, tz = nx;
            double vt = rvx * tx + rvz * tz;
            double jt = Math.Max(-Phys.Friction * j, Math.Min(Phys.Friction * j, -vt / (invM + 1e-6)));
            car.Vx += tx * jt / m; car.Vz += tz * jt / m;
            car.W += (rz * (tx * jt) - rx * (tz * jt)) / Iz;
            if (prop != null)
            {
                prop.Vx -= nx * j / prop.M; prop.Vz -= nz * j / prop.M;
                prop.Vx -= tx * jt / prop.M; prop.Vz -= tz * jt / prop.M;
                prop.Spin += (rnd.NextDouble() - 0.5) * 8;
            }
            double impact = -vn;
            if (onImpact != null && impact > 0.5) onImpact(new ImpactEvent { Speed = impact, X = px, Z = pz, Nx = nx, Nz = nz, Kind = kind, Prop = prop });
            return impact;
        }

        readonly List<int> near2 = new List<int>();
        public void StepProps(double dt)
        {
            foreach (var p in Props)
            {
                if (!p.Hit && p.Vx == 0 && p.Vz == 0) continue;
                p.X += p.Vx * dt; p.Z += p.Vz * dt;
                double sp = Math.Sqrt(p.Vx * p.Vx + p.Vz * p.Vz);
                if (sp > 0.01) { double f = Math.Max(0, 1 - (6.5 / Math.Max(sp, 0.5)) * dt); p.Vx *= f; p.Vz *= f; } else { p.Vx = p.Vz = 0; }
                p.Rot += p.Spin * dt; p.Spin *= Math.Max(0, 1 - 2.5 * dt);
                NearSegments(p.X, p.Z, p.R + 0.3, near2);
                foreach (int idx in near2)
                {
                    var s = segs[idx]; double cpx, cpz;
                    double d = ClosestOnSegment(p.X, p.Z, s[0], s[1], s[2], s[3], out cpx, out cpz);
                    if (d < p.R)
                    {
                        double nx = d > 1e-6 ? (p.X - cpx) / d : 1, nz = d > 1e-6 ? (p.Z - cpz) / d : 0;
                        p.X += nx * (p.R - d); p.Z += nz * (p.R - d);
                        double vn = p.Vx * nx + p.Vz * nz;
                        if (vn < 0) { p.Vx -= 1.5 * vn * nx; p.Vz -= 1.5 * vn * nz; }
                    }
                }
            }
        }

        public void ResetProps() { foreach (var p in Props) { p.X = p.X0; p.Z = p.Z0; p.Vx = p.Vz = 0; p.Rot = 0; p.Spin = 0; p.Hit = false; } }
    }
}
