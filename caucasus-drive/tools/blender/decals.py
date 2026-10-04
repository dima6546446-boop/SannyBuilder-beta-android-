"""
Детали, «облегающие» кузов: оптика, решётки, воздухозаборники, молдинги, расширители арок.
Сетка точек в плоскости проекции (вид спереди / сзади / сбоку) проецируется лучом на кузов,
из неё выдавливается тонкая оболочка по нормали поверхности — деталь повторяет изгиб крыльев,
капота и бампера, не висит в воздухе и не тонет в кузове.
Координаты игровые: x влево, y вверх, z вперёд.
"""
import bmesh
import math
from mathutils import Matrix, Vector
from carbuilder import G, interp


def _lerp(a, b, t):
    return a + (b - a) * t


class Decals:
    def __init__(self, P, surf, S, lod):
        self.P, self.surf, self.S = P, surf, S
        self.lod = lod
        self.q = {'hi': 1.0, 'lod0': 0.5, 'lod1': 0.25}[lod]

    def n(self, k, lo=1):
        return max(lo, int(round(k * self.q)))

    # ------------------------------------------------------------ проекция
    def project(self, view, a, b):
        D = self.S['dims']['dims']
        if view in ('front', 'rear'):
            # a — развёртка по периметру в плане: до начала скругления угла это x,
            # дальше — длина дуги вокруг угла и затем расстояние вдоль борта
            fr = view == 'front'
            zE = D['front'] if fr else D['rear']
            sz = 1 if fr else -1
            r = max(0.05, self.S['cornerF'] if fr else self.S['cornerR'])
            hw = D['W'] / 2
            xc = hw - r
            sg = 1 if a >= 0 else -1
            ax = abs(a)
            if ax <= xc:
                o, d, fb = (a, b, zE + sz * 2), (0, 0, -sz), (a, b, zE)
            elif ax - xc <= r * math.pi / 2:
                ph = (ax - xc) / r
                nx, nz = math.sin(ph), math.cos(ph)
                px, pz = xc + r * nx, zE - sz * r + sz * r * nz
                o = (sg * (px + nx), b, pz + sz * nz)
                d = (-sg * nx, 0, -sz * nz)
                fb = (sg * px, b, pz)
            else:
                rest = ax - xc - r * math.pi / 2
                pz = zE - sz * (r + rest)
                o, d, fb = (sg * (hw + 1), b, pz), (-sg, 0, 0), (sg * hw, b, pz)
        elif view == 'top':
            o, d, fb = (a, 4, b), (0, -1, 0), (a, 1.0, b)
        else:  # сбоку: view = +1 (левый борт, +x) / -1
            s = view
            o, d, fb = (s * 3, b, a), (-s, 0, 0), (s * D['W'] / 2, b, a)
        p, n = self.surf.hit(o, d)
        if p is None:
            return Vector(fb), Vector((-d[0], -d[1], -d[2]))
        return Vector(p), Vector(n)

    def shell(self, view, fn, nu, nv, mat, thick=0.012, lift=0.0, closed=True):
        """fn(u, v) → (a, b) в плоскости проекции; u, v ∈ [0, 1]."""
        b = bmesh.new()
        top, bot = {}, {}
        for i in range(nu + 1):
            for j in range(nv + 1):
                pa, pb = fn(i / nu, j / nv)
                p, n = self.project(view, pa, pb)
                top[i, j] = b.verts.new(G(*(p + n * (lift + thick))))
                bot[i, j] = b.verts.new(G(*(p + n * (lift - 0.004))))

        def quad(vs):
            try:
                f = b.faces.new(vs)
                f.smooth = True
            except ValueError:
                pass
        for i in range(nu):
            for j in range(nv):
                quad((top[i, j], top[i + 1, j], top[i + 1, j + 1], top[i, j + 1]))
                if closed:
                    quad((bot[i, j + 1], bot[i + 1, j + 1], bot[i + 1, j], bot[i, j]))
        if closed:
            ring = [(i, 0) for i in range(nu)] + [(nu, j) for j in range(nv)] + \
                   [(i, nv) for i in range(nu, 0, -1)] + [(0, j) for j in range(nv, 0, -1)]
            for k in range(len(ring)):
                c0, c1 = ring[k], ring[(k + 1) % len(ring)]
                quad((bot[c0], bot[c1], top[c1], top[c0]))
        self.P._add(b, Matrix.Identity(4), mat)

    # ------------------------------------------------------------ формы
    def region(self, view, x0, x1, ybot, ytop, mat, nu=6, nv=2, thick=0.012, lift=0.0, mirror=1):
        """Область между кривыми ybot(x) и ytop(x) (списки (x, y)) на x ∈ [x0, x1]."""
        def fn(u, v):
            x = _lerp(x0, x1, u)
            y0, y1 = interp(ybot, x), interp(ytop, x)
            return mirror * x, _lerp(y0, y1, v)
        self.shell(view, fn, self.n(nu), self.n(nv), mat, thick, lift)

    def poly_strip(self, view, pts, width, mat, nu=None, thick=0.012, lift=0.0, mirror=1):
        """Лента шириной width вдоль ломаной pts [(a, b)]."""
        segs = [math.dist(pts[k], pts[k + 1]) for k in range(len(pts) - 1)]
        Lt = sum(segs)
        nu = nu or max(2, len(pts) * 2)

        def at(u):
            s = u * Lt
            for k, L in enumerate(segs):
                if s <= L + 1e-9 or k == len(segs) - 1:
                    t = min(1.0, s / (L or 1))
                    (a0, b0), (a1, b1) = pts[k], pts[k + 1]
                    da, db = (a1 - a0) / (L or 1), (b1 - b0) / (L or 1)
                    return a0 + (a1 - a0) * t, b0 + (b1 - b0) * t, -db, da
                s -= L

        def fn(u, v):
            a, b_, na, nb = at(u)
            w = (v - 0.5) * width
            return mirror * (a + na * w), b_ + nb * w
        self.shell(view, fn, self.n(nu, 2), 1, mat, thick, lift)

    def disc(self, view, cx, cy, r, mat, seg=12, thick=0.012, lift=0.0, mirror=1, ry=None):
        """Круглая/эллиптическая деталь (полярная сетка)."""
        ry = ry or r

        def fn(u, v):
            a = 2 * math.pi * u
            rr = max(v, 0.02)
            return mirror * (cx + r * rr * math.cos(a)), cy + ry * rr * math.sin(a)
        self.shell(view, fn, self.n(seg, 6), 1, mat, thick, lift)

    def ring(self, view, cx, cy, r, w, mat, seg=16, thick=0.014, lift=0.0, mirror=1):
        def fn(u, v):
            a = 2 * math.pi * u
            rr = r + (v - 0.5) * w
            return mirror * (cx + rr * math.cos(a)), cy + rr * math.sin(a)
        self.shell(view, fn, self.n(seg, 6), 1, mat, thick, lift)

    def arch_lip(self, side, ax, R, yc, w, mat, thick, a0=-8, a1=188, seg=18):
        """Отбортовка / расширитель колёсной арки (вид сбоку)."""
        def fn(u, v):
            a = math.radians(_lerp(a0, a1, u))
            rr = R + 0.004 + v * w
            return ax + rr * math.cos(a), yc + rr * math.sin(a)
        self.shell(side, fn, self.n(seg, 6), 1, mat, thick)
