"""
Генератор кузовов автомобилей АвтоВАЗ для Blender 4.2 (запуск в фоне: blender -b --python build_cars.py).

Кузов строится как «каркас из сечений» (как на плазовых чертежах): по длине машины
расставляются станции z, в каждой — поперечное сечение из 10 точек (низ, порог, боковина,
линия плеч, линия окон, кромка крыши/капота, свод крыши). Сетка сечений замыкается торцами,
материалы назначаются по зонам (кузов / стёкла / стойки / днище), характерные линии
получают «crease» (жёсткость ребра для Subdivision Surface), затем:
subdivision → вырез колёсных арок (boolean) → детали по поверхности (raycast) → экспорт GLB.

Система координат игры: +Z вперёд, +X влево, +Y вверх. В Blender: X=x, Y=-z, Z=y
(glTF-экспортёр переводит Z-up в Y-up, и +Z игры совпадает с +Z glTF).
"""
import bpy
import bmesh
import math
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree


def G(x, y, z):
    """Точка игры → Blender."""
    return Vector((x, -z, y))


def lerp(a, b, t):
    return a + (b - a) * t


def smooth01(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def interp(pts, z):
    """Линейная интерполяция по списку (z, y) в любом порядке."""
    p = sorted(pts, key=lambda q: q[0])
    if z <= p[0][0]:
        return p[0][1]
    if z >= p[-1][0]:
        return p[-1][1]
    for i in range(len(p) - 1):
        a, b = p[i], p[i + 1]
        if a[0] <= z <= b[0]:
            k = (z - a[0]) / ((b[0] - a[0]) or 1)
            return a[1] + (b[1] - a[1]) * k
    return p[-1][1]


# ============================================================ материалы
MATS = {
    'paint': dict(color=(0.45, 0.04, 0.06, 1), metallic=0.4, rough=0.28, coat=1.0),
    'glass': dict(color=(0.04, 0.06, 0.07, 1), metallic=0.1, rough=0.03, alpha=0.45),
    'chrome': dict(color=(0.9, 0.9, 0.9, 1), metallic=1.0, rough=0.12),
    'black': dict(color=(0.02, 0.02, 0.02, 1), metallic=0.0, rough=0.6),
    'under': dict(color=(0.01, 0.01, 0.01, 1), metallic=0.0, rough=0.9),
    'rubber': dict(color=(0.015, 0.015, 0.015, 1), metallic=0.0, rough=0.85),
    'headLamp': dict(color=(0.85, 0.85, 0.85, 1), metallic=0.6, rough=0.05),
    'tailLamp': dict(color=(0.35, 0.0, 0.0, 1), metallic=0.2, rough=0.15),
    'reverseLamp': dict(color=(0.8, 0.8, 0.8, 1), metallic=0.2, rough=0.15),
    'indL': dict(color=(0.8, 0.35, 0.0, 1), metallic=0.2, rough=0.15),
    'indR': dict(color=(0.8, 0.35, 0.0, 1), metallic=0.2, rough=0.15),
    'plate': dict(color=(0.9, 0.9, 0.9, 1), metallic=0.0, rough=0.5),
    'interior': dict(color=(0.12, 0.08, 0.06, 1), metallic=0.0, rough=0.8),
    'grille': dict(color=(0.05, 0.05, 0.05, 1), metallic=0.3, rough=0.5),
    'policeBlue': dict(color=(0.05, 0.15, 1.0, 1), metallic=0.1, rough=0.2),
    'policeRed': dict(color=(1.0, 0.05, 0.05, 1), metallic=0.1, rough=0.2),
    'policeStripe': dict(color=(0.05, 0.15, 0.55, 1), metallic=0.2, rough=0.35),
    'taxiSign': dict(color=(1.0, 0.8, 0.0, 1), metallic=0.0, rough=0.4),
}


def get_mat(name):
    m = bpy.data.materials.get(name)
    if m:
        return m
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    p = MATS.get(name, MATS['black'])
    bsdf = m.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = p['color']
    bsdf.inputs['Metallic'].default_value = p['metallic']
    bsdf.inputs['Roughness'].default_value = p['rough']
    if p.get('coat'):
        bsdf.inputs['Coat Weight'].default_value = p['coat']
    if p.get('alpha') is not None:
        bsdf.inputs['Alpha'].default_value = p['alpha']
        m.blend_method = 'BLEND'
    return m


class MatSlots:
    """Список материалов объекта и индексы по имени."""

    def __init__(self):
        self.names = []

    def idx(self, name):
        if name not in self.names:
            self.names.append(name)
        return self.names.index(name)

    def apply(self, obj):
        obj.data.materials.clear()
        for n in self.names:
            obj.data.materials.append(get_mat(n))


# ============================================================ кузов
NPTS = 10  # точек в полусечении


def half_width(S, z):
    W2 = S['W'] / 2
    zF, zR = S['front'], S['rear']
    rF, rR = S['cornerF'], S['cornerR']
    dF, dR = zF - z, z - zR
    hw = W2
    if dF < rF:
        u = 1 - dF / rF
        hw = min(hw, W2 - rF * (1 - math.sqrt(max(0.0, 1 - u * u))))
    if dR < rR:
        u = 1 - dR / rR
        hw = min(hw, W2 - rR * (1 - math.sqrt(max(0.0, 1 - u * u))))
    # «бочка» по длине: чуть уже у торцов
    return hw


def gh_blend(S, z):
    """1 внутри оранжереи (между основанием лобового и заднего стекла), 0 — капот/багажник."""
    zW0, zB0 = S['zW0'], S['zB0']
    tr = 0.14
    if z > zW0:
        return 1 - smooth01((z - zW0) / tr)
    if z < zB0:
        return 1 - smooth01((zB0 - z) / tr)
    return 1.0


def warp_pts(S):
    """Сдвиг верхней части сечений (оконная рамка, крыша) вдоль z относительно нижней:
    задняя кромка бокового стекла идёт наклонно — от side[1] на линии окон до sideTopR у крыши
    (широкая наклонная C/D-стойка седана), аналогично передняя — до sideTopF."""
    zF, zR = S['front'], S['rear']
    pts = []
    for key, zl in (('sideTopR', S['side'][1]), ('sideTopF', S['side'][0])):
        zu = S.get(key)
        if zu is None or abs(zu - zl) < 1e-4:
            continue
        sk = zu - zl
        a = 0.3 + max(0.0, -sk)
        b = 0.3 + max(0.0, sk)
        pts += [(zl - a, zl - a), (zl, zu), (zl + b, zl + b)]
    pts = sorted(pts)
    return [(zR - 1, zR - 1)] + pts + [(zF + 1, zF + 1)]


def upper_z(S, z):
    return interp(S['_warp'], z)


def lower_z(S, zu):
    return interp([(b, a) for a, b in S['_warp']], zu)


def section(S, z):
    """Полусечение: точки 0..5 — на станции z, 6..9 — на сдвинутой станции upper_z(z).
    Возвращает [(x, y, z)]."""
    zu = upper_z(S, z)
    hw = half_width(S, z)
    hwu = half_width(S, zu)
    yt = interp(S['top'], zu)
    t = gh_blend(S, zu)
    zF, zR = S['front'], S['rear']
    endL = S.get('endLen', 0.45)
    endF = smooth01(1 - (zF - z) / endL) if zF - z < endL else 0
    endR = smooth01(1 - (z - zR) / endL) if z - zR < endL else 0
    yfloor = lerp(S['floor'], S['endFloor'], max(endF, endR))
    ysill = max(S['sill'], yfloor + 0.06)
    hwTop = hwu - S['tumble']
    drop = lerp(S['hoodDrop'], S['roofDrop'], t)
    x6 = lerp(hwu * S['hoodEdge'], hwTop, t)
    y6 = yt - drop
    yb = min(interp(S['belt'], z), y6 - 0.012)
    ysh = min(yb - S.get('shoulder', 0.06), yb - 0.02)
    ysh = max(ysh, ysill + 0.05)
    ymid = ysill + 0.45 * (ysh - ysill)
    bulge = S.get('bulge', 0.0)  # выпуклость боковины
    tb = gh_blend(S, z)
    x5 = hw * lerp(0.992, 0.975, tb)
    pts = [
        (0.0, yfloor, z),
        (hw - S.get('floorIn', 0.10), yfloor, z),
        (hw - 0.012, ysill, z),
        (hw + bulge, ymid, z),
        (hw, ysh, z),
        (x5, yb, z),
        (x6, y6, zu),
        (x6 * S.get('pillarK', 0.86), yt - drop * 0.3, zu),
        (x6 * 0.42, yt - drop * 0.05, zu),
        (0.0, yt, zu),
    ]
    return pts


def stations(S):
    zF, zR = S['front'], S['rear']
    S['_warp'] = warp_pts(S)
    up = [S['zW0'], S['zW1'], S['zB1'], S['zB0']] + [z for z, _ in S['top']]
    keys = {zF, zF - 0.025, zF - 0.1, zR, zR + 0.025, zR + 0.1, S['side'][0], S['side'][1]}
    keys.update(round(lower_z(S, z), 5) for z in up)
    for p in S['pillars']:
        pw = S.get('pillarWs', {}).get(p, S['pillarW'])
        keys.add(p + pw / 2)
        keys.add(p - pw / 2)
    for ax in (S['axleF'], S['axleR']):
        keys.update({ax, ax - S['archR'], ax + S['archR']})
    keys = sorted(k for k in keys if zR - 1e-6 <= k <= zF + 1e-6)
    out = []
    for k in keys:
        if out and k - out[-1] < 0.012:
            continue
        out.append(k)
    # добиваем промежутки ≤ 0.3 м
    full = [out[0]]
    for k in out[1:]:
        prev = full[-1]
        gap = k - prev
        n = int(math.ceil(gap / 0.3))
        for j in range(1, n):
            full.append(prev + gap * j / n)
        full.append(k)
    return sorted(set(round(v, 5) for v in full))


def in_ranges(z, ranges):
    return any(a <= z <= b for a, b in ranges)


def build_body_cage(S, slots):
    """Замкнутая сетка кузова с материалами и crease."""
    zs = stations(S)
    bm = bmesh.new()
    crease = bm.edges.layers.float.new('crease_edge')
    rings = []
    for z in zs:
        pts = section(S, z)
        ring = []
        # +x сторона k=0..9, затем −x сторона k=8..1
        for k in range(NPTS):
            x, y, zz = pts[k]
            ring.append(bm.verts.new(G(x, y, zz)))
        for k in range(NPTS - 2, 0, -1):
            x, y, zz = pts[k]
            ring.append(bm.verts.new(G(-x, y, zz)))
        rings.append((z, ring))
    L = len(rings[0][1])

    def strip_of(e):
        # индекс «полосы» по ребру кольца e (e: 0..L-1, ребро e→e+1)
        if e < NPTS - 1:
            return e
        # −x сторона: вершины 9..(L-1) соответствуют k=9,8..1 → ребро (k→k-1) = полоса k-1
        k_from = NPTS - 1 - (e - (NPTS - 1))
        return k_from - 1

    side_lo, side_hi = S['side'][1], S['side'][0]
    pw = lambda p: S.get('pillarWs', {}).get(p, S['pillarW'])
    pillar_ranges = [(p - pw(p) / 2, p + pw(p) / 2) for p in S['pillars']]
    zW0, zW1, zB1, zB0 = S['zW0'], S['zW1'], S['zB1'], S['zB0']
    glass_faces = []
    for i in range(len(rings) - 1):
        z0, r0 = rings[i]
        z1, r1 = rings[i + 1]
        zm = (z0 + z1) / 2
        zmu = (upper_z(S, z0) + upper_z(S, z1)) / 2
        for e in range(L):
            a, b = r0[e], r0[(e + 1) % L]
            c, d = r1[(e + 1) % L], r1[e]
            f = bm.faces.new((a, b, c, d))
            f.smooth = True
            s = strip_of(e)
            mat = 'paint'
            if s == 0:
                mat = 'under'
            elif s == 5 and side_lo <= zm <= side_hi and not in_ranges(zm, pillar_ranges):
                mat = 'glass'
            elif s in (7, 8) and (zW1 <= zmu <= zW0 or zB0 <= zmu <= zB1):
                mat = 'glass'
            elif s == 6 and S.get('pillarless_top') and (zW1 <= zmu <= zW0 or zB0 <= zmu <= zB1):
                mat = 'glass'
            f.material_index = slots.idx(mat)
            if mat == 'glass':
                glass_faces.append(f)
    # торцы
    fr = bm.faces.new(list(reversed(rings[-1][1])))
    fr.smooth = True
    fr.material_index = slots.idx('paint')
    rr = bm.faces.new(rings[0][1])
    rr.smooth = True
    rr.material_index = slots.idx('paint')
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

    # crease: продольные линии и торцы
    cr = S['crease']
    for i in range(len(rings) - 1):
        r0, r1 = rings[i][1], rings[i + 1][1]
        for k, key in ((4, 'shoulder'), (5, 'belt'), (6, 'edge')):
            for vi in (k, L - k):
                e = bm.edges.get((r0[vi], r1[vi]))
                if e:
                    e[crease] = cr[key]
    for _, ring in (rings[0], rings[-1]):
        for j in range(L):
            e = bm.edges.get((ring[j], ring[(j + 1) % L]))
            if e:
                e[crease] = cr['cap']
    # стёкла: рамка (inset), утапливание, жёсткие края
    trim = 'chrome' if S.get('chromeTrim') else 'black'
    if glass_faces:
        res = bmesh.ops.inset_region(bm, faces=glass_faces, thickness=S.get('frame', 0.018), depth=0.0, use_even_offset=True)
        for f in res['faces']:
            f.material_index = slots.idx(trim)
        glass_set = set(glass_faces)
        for f in glass_faces:
            for e in f.edges:
                e[crease] = 1.0
        for f in res['faces']:
            for e in f.edges:
                others = [g for g in e.link_faces if g not in glass_set and g not in res['faces']]
                if others:
                    e[crease] = 1.0
        # острые углы окон: вершины, где жёсткая кромка поворачивает
        vcr = bm.verts.layers.float.new('crease_vert')
        frame_set = set(res['faces'])
        for v in {v for f in list(glass_faces) + list(frame_set) for v in f.verts}:
            hard = [e for e in v.link_edges if e[crease] >= 0.99]
            if len(hard) == 2:
                d0 = (hard[0].other_vert(v).co - v.co).normalized()
                d1 = (hard[1].other_vert(v).co - v.co).normalized()
                if d0.dot(d1) > -0.82:  # угол между кромками < ~145°
                    v[vcr] = S.get('cornerSharp', 0.85)
        # утопить стёкла внутрь на 8 мм
        verts = {v for f in glass_faces for v in f.verts}
        for v in verts:
            v.co -= v.normal * 0.008
    return bm


def mesh_from_bm(bm, name, slots):
    me = bpy.data.meshes.new(name)
    for n in slots.names:  # слоты — до записи сетки, иначе индексы материалов сбросятся
        me.materials.append(get_mat(n))
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def apply_all(ob):
    bpy.context.view_layer.objects.active = ob
    for o in bpy.context.selected_objects:
        o.select_set(False)
    ob.select_set(True)
    for m in list(ob.modifiers):
        bpy.ops.object.modifier_apply(modifier=m.name)


def cut_arches(ob, S, slots):
    """Вырез колёсных арок булевой операцией; стенки ниш — чёрные."""
    for ax in (S['axleF'], S['axleR']):
        bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=S['archR'], depth=S['W'] + 0.6,
                                            location=G(0, S['archY'], ax), rotation=(0, math.pi / 2, 0))
        cut = bpy.context.object
        cut.data.materials.append(get_mat('under'))
        mod = ob.modifiers.new('arch', 'BOOLEAN')
        mod.operation = 'DIFFERENCE'
        mod.solver = 'EXACT'
        mod.object = cut
        mod.material_mode = 'TRANSFER'
        apply_all(ob)
        bpy.data.objects.remove(cut, do_unlink=True)


def build_body(S, level):
    slots = MatSlots()
    bm = build_body_cage(S, slots)
    ob = mesh_from_bm(bm, 'body', slots)
    if level > 0:
        m = ob.modifiers.new('sub', 'SUBSURF')
        m.levels = level
        m.render_levels = level
        m.use_creases = True
        m.boundary_smooth = 'PRESERVE_CORNERS'
        apply_all(ob)
    cut_arches(ob, S, slots)
    return ob


# ============================================================ поверхность (raycast)
class Surface:
    def __init__(self, ob):
        dg = bpy.context.evaluated_depsgraph_get()
        self.bvh = BVHTree.FromObject(ob, dg)

    def hit(self, origin_g, dir_g):
        o = G(*origin_g)
        d = G(*dir_g) - G(0, 0, 0)
        loc, nor, _, _ = self.bvh.ray_cast(o, d.normalized(), 10)
        if loc is None:
            return None, None
        # обратно в координаты игры
        return (loc.x, loc.z, -loc.y), (nor.x, nor.z, -nor.y)

    def front(self, x, y):
        p, n = self.hit((x, y, 5), (0, 0, -1))
        return p, n

    def rear(self, x, y):
        return self.hit((x, y, -5), (0, 0, 1))

    def side(self, z, y, s=1):
        return self.hit((s * 3, y, z), (-s, 0, 0))

    def top(self, x, z):
        return self.hit((x, 5, z), (0, -1, 0))
