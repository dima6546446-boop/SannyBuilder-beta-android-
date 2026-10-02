"""
Детали кузова: оптика, решётки, бамперы, фонари, зеркала, ручки, швы, дворники, номера, салон.
Каждая деталь ставится на реальную поверхность кузова (raycast) и ориентируется по нормали.
Координаты — игровые (z вперёд, y вверх, x влево).
"""
import bpy
import bmesh
import math
from mathutils import Vector, Matrix
from carbuilder import G, get_mat, interp


class Parts:
    """Собирает детали в один bmesh с материалами (быстро, без bpy.ops)."""

    def __init__(self, seg=2):
        self.bm = bmesh.new()
        self.mats = []
        self.seg = seg
        self.uv = self.bm.loops.layers.uv.new('UVMap')

    def mi(self, name):
        if name not in self.mats:
            self.mats.append(name)
        return self.mats.index(name)

    def _add(self, src, matrix, mat):
        """Скопировать временный bmesh src в общий с матрицей и материалом."""
        m = self.mi(mat)
        vmap = {}
        for v in src.verts:
            vmap[v] = self.bm.verts.new(matrix @ v.co)
        uvs = src.loops.layers.uv.active
        for f in src.faces:
            try:
                nf = self.bm.faces.new([vmap[v] for v in f.verts])
            except ValueError:
                continue
            nf.material_index = m
            nf.smooth = f.smooth
            if uvs:
                for la, lb in zip(f.loops, nf.loops):
                    lb[self.uv].uv = la[uvs].uv
        src.free()

    # ---------------- примитивы в локальной системе (x вправо, y вверх, z наружу)
    def box(self, w, h, d, mat, M, bevel=0.0, smooth=False):
        b = bmesh.new()
        bmesh.ops.create_cube(b, size=1.0, calc_uvs=True)
        bmesh.ops.scale(b, vec=(w, h, d), verts=b.verts)
        if bevel > 0 and self.seg > 0:
            bmesh.ops.bevel(b, geom=list(b.edges), offset=min(bevel, w / 2.2, h / 2.2, d / 2.2), segments=self.seg,
                            affect='EDGES', profile=0.5)
        for f in b.faces:
            f.smooth = smooth or bevel > 0
        self._add(b, M, mat)

    def cyl(self, r, d, mat, M, seg=20, r2=None, smooth=True):
        b = bmesh.new()
        bmesh.ops.create_cone(b, cap_ends=True, cap_tris=False, segments=seg, radius1=r, radius2=r2 if r2 is not None else r, depth=d, calc_uvs=True)
        for f in b.faces:
            f.smooth = smooth and len(f.verts) == 4
        self._add(b, M, mat)

    def sphere(self, r, mat, M, sx=1, sy=1, sz=1, seg=16):
        b = bmesh.new()
        bmesh.ops.create_uvsphere(b, u_segments=seg, v_segments=max(6, seg // 2), radius=r, calc_uvs=True)
        bmesh.ops.scale(b, vec=(sx, sy, sz), verts=b.verts)
        for f in b.faces:
            f.smooth = True
        self._add(b, M, mat)

    def torus(self, R, r, mat, M, seg=24, ring=8):
        b = bmesh.new()
        verts = []
        for i in range(seg):
            a = 2 * math.pi * i / seg
            row = []
            for j in range(ring):
                c = 2 * math.pi * j / ring
                row.append(b.verts.new(((R + r * math.cos(c)) * math.cos(a), (R + r * math.cos(c)) * math.sin(a), r * math.sin(c))))
            verts.append(row)
        for i in range(seg):
            for j in range(ring):
                f = b.faces.new((verts[i][j], verts[(i + 1) % seg][j], verts[(i + 1) % seg][(j + 1) % ring], verts[i][(j + 1) % ring]))
                f.smooth = True
        self._add(b, M, mat)

    def to_object(self, name):
        me = bpy.data.meshes.new(name)
        for n in self.mats:
            me.materials.append(get_mat(n))
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        self.bm.to_mesh(me)
        self.bm.free()
        ob = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(ob)
        return ob


def frame(pos, normal, up=(0, 1, 0), roll=0.0, offset=0.0):
    """Матрица Blender: локальная z → нормаль (игровые координаты), y → «вверх»."""
    n = (G(*normal) - G(0, 0, 0)).normalized()
    u = (G(*up) - G(0, 0, 0)).normalized()
    if abs(n.dot(u)) > 0.95:
        u = (G(0, 0, 1) - G(0, 0, 0)).normalized()
    x = u.cross(n).normalized()
    y = n.cross(x).normalized()
    if roll:
        R = Matrix.Rotation(roll, 3, n)
        x, y = R @ x, R @ y
    M = Matrix((x, y, n)).transposed().to_4x4()
    M.translation = G(*pos) + n * offset
    return M


def lframe(pos, rot=(0, 0, 0)):
    """Матрица по позиции и эйлеровым углам в игровых осях (для салона и мелочей)."""
    M = Matrix.Translation(G(*pos))
    # переводим поворот: вокруг игровых x, y(вверх), z(вперёд)
    rx = Matrix.Rotation(rot[0], 4, 'X')
    ry = Matrix.Rotation(rot[1], 4, 'Z')
    rz = Matrix.Rotation(rot[2], 4, 'Y')
    base = Matrix(((1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))  # локальная z → игровая z (вперёд)
    return M @ ry @ rx @ rz @ base


# ============================================================ сборка деталей
def build_details(S, surf, lod):
    d = S['dims']
    D = d['dims']
    hi = lod == 'hi'
    seg = 3 if hi else (1 if lod == 'lod0' else 0)
    P = Parts(seg)
    F, R, W = D['front'], D['rear'], D['W']
    hw = W / 2
    head, tail, gr, bp = d['head'], d['tail'], d['grille'], d['bumper']
    classic = bp['style'] == 'chrome'

    def on_front(x, y, fallback=None):
        p, n = surf.front(x, y)
        if p is None:
            return (x, y, fallback if fallback is not None else F), (0, 0, 1)
        return p, n

    def on_rear(x, y):
        p, n = surf.rear(x, y)
        if p is None:
            return (x, y, R), (0, 0, -1)
        return p, n

    def on_side(z, y, s):
        p, n = surf.side(z, y, s)
        if p is None:
            return (s * hw, y, z), (s, 0, 0)
        return p, n

    # ---------------- передняя оптика
    for s in (1, -1):
        if head['style'] in ('round4', 'round2'):
            for x in head['xs']:
                p, n = on_front(s * x, head['y'])
                r = head['r']
                P.cyl(r + 0.02, 0.05, 'chrome', frame(p, n, offset=0.0))
                P.cyl(r, 0.03, 'headLamp', frame(p, n, offset=0.03), seg=24)
                if hi:
                    P.sphere(r * 0.98, 'headLamp', frame(p, n, offset=0.035), sz=0.25, seg=20)
                    P.cyl(r * 0.25, 0.01, 'chrome', frame(p, n, offset=0.055), seg=12)
        else:
            p, n = on_front(s * head['x'], head['y'])
            w, h = head['w'], head['h']
            if head['style'] == 'rect':
                P.box(w + 0.035, h + 0.035, 0.05, 'chrome', frame(p, n, offset=0.0), bevel=0.008)
                P.box(w, h, 0.03, 'headLamp', frame(p, n, offset=0.02), bevel=0.005)
                if hi:
                    for k in range(5):
                        P.box(0.004, h * 0.9, 0.004, 'chrome', frame(p, n, offset=0.037) @ Matrix.Translation(((k - 2) * w / 5, 0, 0)))
                    P.box(0.012, h, 0.012, 'chrome', frame(p, n, offset=0.036) @ Matrix.Translation((-s * w * 0.12, 0, 0)))
            elif head['style'] == 'wedge':
                P.box(w + 0.02, h + 0.02, 0.04, 'black', frame(p, n, offset=0.0), bevel=0.006)
                P.box(w, h, 0.03, 'headLamp', frame(p, n, offset=0.015), bevel=0.006)
            else:  # modern
                P.box(w + 0.02, h + 0.02, 0.04, 'black', frame(p, n, offset=-0.005), bevel=0.02)
                P.box(w, h, 0.035, 'headLamp', frame(p, n, offset=0.012), bevel=0.02)
                if hi:
                    for k in (-1, 1):
                        P.cyl(h * 0.28, 0.02, 'chrome', frame(p, n, offset=0.02) @ Matrix.Translation((k * w * 0.22, 0.01, 0)), seg=16)
                P.box(w * 0.75, 0.018, 0.02, 'reverseLamp', frame(p, n, offset=0.02) @ Matrix.Translation((s * 0.02, -h * 0.38, 0)))
                # фара «заходит» на крыло
                zs = p[2] - 0.12
                pc, nc = on_side(zs, head['y'] + 0.01, s)
                if pc is not None and abs(pc[2] - p[2]) < 0.4:
                    P.box(0.2, h * 0.85, 0.03, 'headLamp', frame(pc, nc, offset=0.004), bevel=0.02)
        # поворотник
        ind = 'indL' if s > 0 else 'indR'
        if head['style'] in ('round4', 'round2'):
            p, n = on_front(s * (hw - 0.2), bp['y'] + bp['h'] / 2 + 0.06)
            P.box(0.13, 0.05, 0.03, ind, frame(p, n, offset=0.01), bevel=0.006)
        else:
            x = s * (head['x'] + head['w'] / 2 + 0.05)
            p, n = on_front(x, head['y'])
            P.box(0.07, head['h'] * 0.85, 0.03, ind, frame(p, n, offset=0.008), bevel=0.006)

    # ---------------- решётка
    gy = gr['y']
    p, n = on_front(0.0, gy)
    st = gr['style']
    Mg = frame(p, n)
    if st == 'chrome-bars':  # 2107
        P.box(gr['w'] + 0.03, gr['h'] + 0.03, 0.05, 'chrome', Mg, bevel=0.01)
        P.box(gr['w'], gr['h'], 0.02, 'grille', frame(p, n, offset=0.02))
        if lod != 'lod1':
            nb = 18 if hi else 8
            for k in range(nb):
                P.box(0.008, gr['h'] - 0.02, 0.015, 'chrome', frame(p, n, offset=0.03) @ Matrix.Translation(((k - (nb - 1) / 2) * gr['w'] / nb, 0, 0)))
            P.box(0.1, 0.07, 0.02, 'tailLamp', frame(p, n, offset=0.045))
    elif st == 'chrome-mesh':  # 2101
        P.box(gr['w'], gr['h'], 0.04, 'chrome', Mg, bevel=0.01)
        P.box(gr['w'] - 0.04, gr['h'] - 0.04, 0.02, 'grille', frame(p, n, offset=0.012))
        if lod != 'lod1':
            for k in range(-2, 3):
                P.box(gr['w'] - 0.06, 0.008, 0.012, 'chrome', frame(p, n, offset=0.025) @ Matrix.Translation((0, k * gr['h'] / 6, 0)))
    elif st == 'black-chrome':  # 2106
        P.box(gr['w'], gr['h'], 0.035, 'grille', Mg, bevel=0.006)
        for k in (-1, 1):
            P.box(gr['w'] + 0.01, 0.016, 0.04, 'chrome', frame(p, n, offset=0.004) @ Matrix.Translation((0, k * gr['h'] / 2, 0)), bevel=0.004)
        if lod != 'lod1':
            for k in range(-3, 4):
                P.box(0.012, gr['h'] - 0.03, 0.012, 'chrome', frame(p, n, offset=0.02) @ Matrix.Translation((k * 0.075, 0, 0)))
    elif st == 'black-bars':  # Нива
        P.box(gr['w'], gr['h'], 0.035, 'grille', Mg, bevel=0.006)
        if lod != 'lod1':
            for k in range(-2, 3):
                P.box(gr['w'] - 0.04, 0.012, 0.02, 'black', frame(p, n, offset=0.02) @ Matrix.Translation((0, k * 0.045, 0)))
    elif st == 'slot':
        P.box(gr['w'], gr['h'], 0.02, 'grille', Mg, bevel=0.004)
    elif st in ('modern-chrome', 'granta'):
        P.box(gr['w'], gr['h'], 0.02, 'grille', Mg, bevel=0.01)
        P.box(gr['w'] + 0.05, 0.026, 0.03, 'chrome', frame(p, n, offset=0.012) @ Matrix.Translation((0, gr['h'] * 0.22, 0)), bevel=0.008)
        P.box(0.12, 0.075, 0.02, 'chrome', frame(p, n, offset=0.02), bevel=0.01)
    elif st == 'xface':
        P.box(0.36, 0.09, 0.02, 'grille', frame(p, n, offset=0.0) @ Matrix.Translation((0, gr['h'] * 0.42, 0)), bevel=0.01)
        P.box(0.12, 0.07, 0.02, 'chrome', frame(p, n, offset=0.014) @ Matrix.Translation((0, gr['h'] * 0.42, 0)), bevel=0.01)
        for s in (1, -1):
            for (x0, y0, x1, y1) in ((0.56, 0.12, 0.26, -0.02), (0.26, -0.02, 0.56, -0.17)):
                pa, na = on_front(s * (x0 + x1) / 2, gy + (y0 + y1) / 2)
                ang = math.atan2(y1 - y0, s * (x1 - x0))
                L = math.hypot(x1 - x0, y1 - y0)
                P.box(L + 0.02, 0.05, 0.02, 'chrome', frame(pa, na, offset=0.004, roll=ang), bevel=0.012)
            pf, nf = on_front(s * 0.62, gy - 0.19)
            P.cyl(0.04, 0.02, 'headLamp', frame(pf, nf, offset=0.005), seg=16)
        pl, nl = on_front(0, gy - 0.05)
        P.box(0.46, 0.13, 0.02, 'grille', frame(pl, nl, offset=0.0), bevel=0.01)

    # ---------------- бамперы
    def bumper(front):
        sgn = 1 if front else -1
        zface = F if front else R
        y = bp['y']
        on = on_front if front else on_rear
        p, n = on(0.0, y)
        base = p[2]
        if bp['style'] == 'chrome':
            depth = 0.13
            M = frame((0, y, base + sgn * 0.035), (0, 0, sgn))
            P.box(W + 0.03, bp['h'], depth, 'chrome', M, bevel=0.025)
            for s in (1, -1):  # загибы на крылья
                Me = frame((s * (hw - 0.02), y, base - sgn * 0.08), (s, 0, 0))
                P.box(0.22, bp['h'] * 0.95, 0.09, 'chrome', Me, bevel=0.02)
            if bp.get('strip'):
                P.box(W + 0.04, 0.035, depth + 0.012, 'rubber', M, bevel=0.01)
            if bp.get('fangs') and front:
                for s in (1, -1):
                    P.box(0.05, 0.18, 0.07, 'chrome', frame((s * 0.32, y + 0.02, base + sgn * 0.1), (0, 0, sgn)), bevel=0.015)
            return base + sgn * (0.035 + depth / 2)
        if bp['style'] == 'black':
            M = frame((0, y, base + sgn * 0.03), (0, 0, sgn))
            P.box(W + 0.02, bp['h'], 0.12, 'black', M, bevel=0.03)
            for s in (1, -1):
                P.box(0.28, bp['h'] * 0.95, 0.1, 'black', frame((s * (hw - 0.04), y, base - sgn * 0.08), (s, 0, 0)), bevel=0.025)
            return base + sgn * 0.09
        # в цвет кузова: сам бампер — часть обвеса, добавляем нижнюю решётку и молдинг
        M = frame((0, y - bp['h'] * 0.3, base + sgn * 0.005), (0, 0, sgn))
        P.box(W * 0.55, 0.07, 0.03, 'grille', M, bevel=0.015)
        return base + sgn * 0.01

    fFace = bumper(True)
    rFace = bumper(False)

    # ---------------- номера
    py = bp['y'] if bp['style'] != 'body' else bp['y'] + 0.02
    if gr['style'] == 'xface':
        py = bp['y'] - 0.08
    P.box(0.52, 0.115, 0.012, 'plate', frame((0, py, fFace + 0.008), (0, 0, 1)))
    if classic:
        p, n = on_rear(0.0, bp['y'] + bp['h'] / 2 + 0.11)
        P.box(0.52, 0.115, 0.012, 'plate', frame(p, n, offset=0.006))
    else:
        P.box(0.52, 0.115, 0.012, 'plate', frame((0, bp['y'] + 0.02, rFace - 0.008), (0, 0, -1)))

    # ---------------- задние фонари
    for s in (1, -1):
        ind = 'indL' if s > 0 else 'indR'
        st = tail['style']
        tw, th = tail['w'], tail['h']
        p, n = on_rear(s * tail['x'], tail['y'])
        Mt = frame(p, n, offset=0.0)
        if st in ('wide', 'hatch'):
            P.box(tw + 0.02, th + 0.02, 0.03, 'chrome' if classic else 'black', Mt, bevel=0.006)
            segs = [(0.55, 'tailLamp'), (0.22, ind), (0.23, 'reverseLamp')] if st == 'wide' else [(0.62, 'tailLamp'), (0.2, ind), (0.18, 'reverseLamp')]
            x0 = tw / 2
            for frac, mat in segs:
                sw = tw * frac
                cx = x0 - sw / 2
                P.box(sw - 0.006, th, 0.03, mat, frame(p, n, offset=0.012) @ Matrix.Translation((-s * cx, 0, 0)), bevel=0.004)
                x0 -= sw
            if hi and st == 'wide':
                for k in range(4):
                    P.box(tw, 0.004, 0.006, 'chrome', frame(p, n, offset=0.03) @ Matrix.Translation((0, (k - 1.5) * th / 4.5, 0)))
        elif st == 'vertical':
            P.box(tw, th * 0.6, 0.035, 'tailLamp', frame(p, n, offset=0.01) @ Matrix.Translation((0, th * 0.2, 0)), bevel=0.008)
            P.box(tw, th * 0.38, 0.035, ind, frame(p, n, offset=0.01) @ Matrix.Translation((0, -th * 0.3, 0)), bevel=0.008)
        else:  # modern
            P.box(tw, th, 0.04, 'tailLamp', frame(p, n, offset=0.008), bevel=0.02)
            P.box(tw * 0.25, th * 0.4, 0.02, 'reverseLamp', frame(p, n, offset=0.03) @ Matrix.Translation((s * tw * 0.3, 0, 0)), bevel=0.006)
            P.box(tw * 0.3, th * 0.3, 0.02, ind, frame(p, n, offset=0.03) @ Matrix.Translation((-s * tw * 0.2, -th * 0.12, 0)), bevel=0.006)

    if lod == 'lod1':
        return P

    # ---------------- боковины
    beltAt = lambda z: interp(S['belt'], z) - 0.02
    belt = beltAt(0.0)
    for s in (1, -1):
        # зеркала
        zm = S['zW0'] - 0.1
        p, n = on_side(zm, beltAt(zm) + 0.06, s)
        mat = 'paint' if bp['style'] == 'body' else 'black'
        P.box(0.05, 0.05, 0.06, 'black', frame(p, (s, 0, 0), offset=0.03))
        P.box(0.16, 0.1, 0.07, mat, frame((p[0] + s * 0.1, p[1] + 0.03, p[2] - 0.02), (0, 0, 1)), bevel=0.025)
        if hi:
            P.box(0.14, 0.085, 0.01, 'chrome', frame((p[0] + s * 0.1, p[1] + 0.03, p[2] - 0.06), (0, 0, -1)))
        # ручки дверей
        for z in S['handles']:
            ph, nh = on_side(z, beltAt(z) - 0.1, s)
            P.box(0.13 if classic else 0.16, 0.028, 0.025, 'chrome' if classic else mat, frame(ph, nh, offset=0.006), bevel=0.008)
        if hi:
            # швы дверей
            for z in S['seams']:
                for k in range(6):
                    yy = S['sill'] + 0.08 + (belt - 0.05 - S['sill'] - 0.08) * (k + 0.5) / 6
                    ps, ns = on_side(z, yy, s)
                    P.box(0.006, (belt - S['sill'] - 0.13) / 6 + 0.01, 0.004, 'under', frame(ps, ns, offset=0.0005))
            # молдинг
            if d.get('trim'):
                zz = [F - 0.35 - k * (F - R - 0.7) / 10 for k in range(11)]
                for z in zz:
                    pm, nm = on_side(z, d['trim'], s)
                    P.box((F - R - 0.7) / 10 + 0.01, 0.028, 0.012, 'chrome', frame(pm, nm, offset=0.004), bevel=0.005)
            # брызговики у классики
            if classic:
                P.box(0.2, 0.2, 0.012, 'rubber', frame((s * D['track'] / 2, S['sill'] + 0.02, D['axleR'] - D['archR'] - 0.03), (0, 0, -1)))
    # дворники
    for s in (1, -1):
        zc = S['zW0'] - 0.05
        p, n = surf.top(s * 0.22, zc)
        if p:
            P.box(0.5, 0.014, 0.02, 'black', frame(p, n, offset=0.01, roll=s * 0.08))
    # антенна
    if S.get('antenna'):
        p, n = surf.top(-hw + 0.14, 1.0)
        if p:
            P.cyl(0.004, 0.9, 'black', frame((p[0], p[1] + 0.45, p[2] - 0.05), (0, 1, 0), up=(0, 0, 1)), seg=6)
    # глушитель
    P.cyl(0.028, 0.14, 'black', frame((-0.45, 0.26, R + 0.1), (0, 0, -1)), seg=10)
    # рейлинги
    top_y = max(y for _, y in S['top'])
    if d.get('rails'):
        for s in (1, -1):
            P.box(0.04, S['zW1'] - S['zB1'] + 0.2, 0.05, 'black', frame((s * (W / 2 - S['tumble'] - 0.08), top_y + 0.02, (S['zW1'] + S['zB1']) / 2), (0, 1, 0), up=(0, 0, 1)), bevel=0.012)
            for zz in (S['zW1'] - 0.1, S['zB1'] + 0.1):
                P.box(0.05, 0.06, 0.06, 'black', frame((s * (W / 2 - S['tumble'] - 0.08), top_y - 0.01, zz), (0, 1, 0), up=(0, 0, 1)), bevel=0.01)
    if d.get('police'):
        zc = (S['zW1'] + S['zB1']) / 2
        P.box(1.1, 0.03, 0.26, 'black', frame((0, top_y + 0.015, zc), (0, 0, 1)))
        P.box(0.52, 0.11, 0.24, 'policeBlue', frame((0.28, top_y + 0.08, zc), (0, 0, 1)), bevel=0.03)
        P.box(0.52, 0.11, 0.24, 'policeRed', frame((-0.28, top_y + 0.08, zc), (0, 0, 1)), bevel=0.03)
        for s in (1, -1):
            for k in range(12):
                z = F - 0.4 - k * (F - R - 0.8) / 11
                pp, nn = on_side(z, 0.62, s)
                P.box((F - R - 0.8) / 11 + 0.02, 0.11, 0.006, 'policeStripe', frame(pp, nn, offset=0.002))
    if d.get('taxi'):
        zc = (S['zW1'] + S['zB1']) / 2
        P.box(0.52, 0.16, 0.2, 'taxiSign', frame((0, top_y + 0.08, zc), (0, 0, 1)), bevel=0.02)
        for s in (1, -1):
            for k in range(4):
                P.box(0.006, 0.06, 0.1, 'black', frame((s * 0.262, top_y + 0.05 + (k % 2) * 0.06, zc - 0.15 + k * 0.1), (0, 0, 1)))
            for k in range(10):
                z = 1.0 - k * 0.26
                pp, nn = on_side(z, 0.64 + (k % 2) * 0.05, s)
                P.box(0.12, 0.05, 0.005, 'black', frame(pp, nn, offset=0.002))

    if not hi:
        return P
    # ---------------- салон (привязан к оранжерее: передние кресла за лобовым, диван перед задним стеклом)
    c0z = S['zW0']
    dashZ = c0z - 0.28
    cw = W - 2 * S['tumble'] - 0.16
    by = interp(S['belt'], dashZ)
    roof = max(y for _, y in S['top'])
    P.box(cw, 0.22, 0.38, 'interior', frame((0, by - 0.1, dashZ), (0, 0, 1)), bevel=0.04)
    P.box(cw * 0.95, 0.03, 0.2, 'black', frame((0, by + 0.02, dashZ - 0.05), (0, 0, 1)), bevel=0.01)
    P.torus(0.19, 0.022, 'black', frame((0.36, by + 0.01, dashZ - 0.32), (0, 0.35, 1)))
    P.cyl(0.03, 0.3, 'black', frame((0.36, by - 0.05, dashZ - 0.17), (0, 0.35, 1)), seg=8)
    seatZ = dashZ - 0.9
    backTop = min(roof - 0.2, by + 0.3)
    for s in (1, -1):
        P.box(0.5, 0.14, 0.5, 'interior', frame((s * 0.36, by - 0.4, seatZ), (0, 0, 1)), bevel=0.04)
        P.box(0.5, backTop - (by - 0.36), 0.13, 'interior', frame((s * 0.36, (backTop + by - 0.36) / 2, seatZ - 0.27), (0, 0.12, 1)), bevel=0.05)
        P.box(0.27, 0.15, 0.1, 'interior', frame((s * 0.36, backTop + 0.06, seatZ - 0.3), (0, 0, 1)), bevel=0.04)
    rz = max(S['zB0'] + 0.55, seatZ - 0.95)
    if rz < seatZ - 0.55:
        P.box(cw * 0.92, 0.14, 0.48, 'interior', frame((0, by - 0.4, rz + 0.1), (0, 0, 1)), bevel=0.04)
        P.box(cw * 0.92, backTop - 0.05 - (by - 0.36), 0.13, 'interior', frame((0, (backTop - 0.05 + by - 0.36) / 2, rz - 0.18), (0, 0.12, 1)), bevel=0.05)
    P.cyl(0.012, 0.3, 'chrome', frame((0, by - 0.27, dashZ - 0.55), (0, 1, 0.3), up=(0, 0, 1)), seg=6)
    return P
