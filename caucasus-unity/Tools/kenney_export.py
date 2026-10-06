"""
Kenney Car Kit (CC0, kenney.nl) → модели игры CAUCASUS DRIVE.
    blender -b --python kenney_export.py -- <каталог Kenney «Models/GLB format»> <Resources/Cars> <каталог превью> [id,id…]

Что делает с каждой машиной:
  • берёт кузов (колёса отбрасывает — в игре свои крутящиеся шины и диски), растягивает под реальные габариты,
  • красит грани по палитре colormap.png: основной цвет → paint (перекрашивается в игре), стёкла → glass, тёмное → black/under,
    фары/фонари → headLamp/tailLamp, белое → chrome; добавляет номерные плашки,
  • пишет .bytes (формат CDM1, как у остальных машин) и печатает строки для CarDefs.cs и якоря салона.
Координаты Blender-игры: x влево, −y вперёд, z вверх (у Kenney уже так).
"""
import bpy, bmesh, sys, os, math, json, colorsys
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'caucasus-drive', 'tools', 'blender'))
import kenney_common as K
import export_unity as EU

# цель: длина, ширина, высота (м), имя, ник, цена, привод, масса, мощность, момент, колёса, трафик
MODELS = {
    'k_sedan':   dict(src='sedan',            L=4.45, W=1.78, H=1.46, name='Седан «Сити»',        nick='городской седан',      price=420000,  drive='FWD', mass=1280, hp=120, tq=160, style='steel', traffic=1.5, wheelW=0.22),
    'k_sports':  dict(src='sedan-sports',     L=4.60, W=1.86, H=1.38, name='Спорт-седан «Вираж»', nick='заряженный седан',     price=780000,  drive='RWD', mass=1330, hp=230, tq=300, style='sport', traffic=0.5, wheelW=0.25),
    'k_hatch':   dict(src='hatchback-sports', L=4.15, W=1.78, H=1.42, name='Хэтчбек «Дрифтер»',   nick='спорт-хэтчбек',        price=560000,  drive='FWD', mass=1180, hp=165, tq=210, style='star',  traffic=0.8, wheelW=0.23),
    'k_suv':     dict(src='suv',              L=4.60, W=1.90, H=1.85, name='Внедорожник «Горец»', nick='полноприводный',       price=950000,  drive='AWD', mass=1750, hp=150, tq=250, style='steel', traffic=0.8, wheelW=0.26),
    'k_suvlux':  dict(src='suv-luxury',       L=4.90, W=1.96, H=1.80, name='Внедорожник «Князь»', nick='люкс',                 price=1600000, drive='AWD', mass=2050, hp=250, tq=380, style='sport', traffic=0.4, wheelW=0.27),
    'k_van':     dict(src='van',              L=4.80, W=1.90, H=2.00, name='Фургон «Грузовичок»', nick='развозной',            price=520000,  drive='RWD', mass=1700, hp=110, tq=190, style='steel', traffic=0.8, wheelW=0.24),
    'k_pickup':  dict(src='truck',            L=5.10, W=1.90, H=1.80, name='Пикап «Фермер»',      nick='с кузовом',            price=690000,  drive='AWD', mass=1900, hp=140, tq=260, style='steel', traffic=0.6, wheelW=0.26),
}

PAINT_FALLBACK = [0xd9601c, 0xb0161b, 0x1d3f7a, 0x2e7ab0, 0xf2f2f2, 0x1c1c1c, 0x5a6b3a, 0xc4a468]


def hsv(rgb): return colorsys.rgb_to_hsv(*rgb)


def ensure_mat(name):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    return m


def load_body(src):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(SRC, src + '.glb'))
    body = bpy.data.objects['body']
    wheels = [o for o in bpy.data.objects if o.name.startswith('wheel-')]
    for o in bpy.data.objects:
        o.select_set(False)
    return body, wheels


def bbox(o):
    pts = [o.matrix_world @ Vector(c) for c in o.bound_box]
    return [min(p[i] for p in pts) for i in range(3)], [max(p[i] for p in pts) for i in range(3)]


def build(cid, cfg):
    body, wheels = load_body(cfg['src'])
    me = body.data
    uv = me.uv_layers.active.data
    # --- классификация граней по ячейкам палитры
    cells = {}
    for p in me.polygons:
        u = sum(uv[l].uv[0] for l in p.loop_indices) / p.loop_indices.__len__()
        v = sum(uv[l].uv[1] for l in p.loop_indices) / p.loop_indices.__len__()
        c = K.cell_of(u, v)
        cells.setdefault(c, []).append(p.index)
    area = {c: sum(me.polygons[i].area for i in ids) for c, ids in cells.items()}
    # основной цвет кузова — самая большая «цветная» ячейка
    color_cells = {c: a for c, a in area.items() if K.classify(K.cell_rgb(c)) == 'color' and c not in ((1, 0), (1, 1), (2, 0))}
    paint_hue = None
    if color_cells:
        pc = max(color_cells, key=color_cells.get)
        paint_hue = hsv(K.cell_rgb(pc))[0]
    # --- направление: фары амбер (1,0)/(1,1) должны быть у −y
    ys = [me.polygons[i].center.y for c in ((1, 0), (1, 1)) for i in cells.get(c, [])]
    if ys and sum(ys) / len(ys) > 0:
        body.rotation_euler[2] = math.pi
        bpy.context.view_layer.update()
        bpy.context.view_layer.objects.active = body; body.select_set(True)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
        for w in wheels:
            w.rotation_euler[2] = math.pi
        bpy.context.view_layer.update()
    mn, mx = bbox(body)
    wc = []
    for w in wheels:
        a, b = bbox(w)
        wc.append(((a[0] + b[0]) / 2, (a[1] + b[1]) / 2, (a[2] + b[2]) / 2, (b[2] - a[2]) / 2, (b[0] - a[0])))
    wc = [c for c in wc if abs(c[0]) > 0.15]              # запаска на двери (у внедорожника) — не колесо оси
    for w in wheels: bpy.data.objects.remove(w)          # колёса игры — свои
    sx = cfg['W'] / (mx[0] - mn[0]); sy = cfg['L'] / (mx[1] - mn[1]); sz = cfg['H'] / mx[2]
    # --- перенос в «центр базы»: середина между осями
    ycen = sum(w[1] for w in wc) / len(wc)
    body.data.transform(body.matrix_world)
    body.matrix_world.identity()
    for v in me.vertices:
        v.co = Vector(((v.co.x) * sx, (v.co.y - ycen) * sy, v.co.z * sz))
    me.update()
    # glTF приносит свои «split normals» — сбрасываем, чтобы нормали считались по граням (плоское low-poly освещение)
    bpy.context.view_layer.objects.active = body
    try: bpy.ops.mesh.customdata_custom_splitnormals_clear()
    except Exception: pass
    # --- материалы
    names = ['paint', 'glass', 'chrome', 'black', 'rubber', 'under', 'plate', 'headLamp', 'tailLamp', 'indL', 'indR', 'grille', 'reverseLamp']
    me.materials.clear()
    for n in names: me.materials.append(ensure_mat(n))
    idx = {n: i for i, n in enumerate(names)}
    for c, ids in cells.items():
        rgb = K.cell_rgb(c); cl = K.classify(rgb)
        if c in ((0, 0), (0, 1)): m = 'glass'
        elif c in ((1, 0), (1, 1)): m = 'headLamp'
        elif c == (2, 0): m = 'tailLamp'
        elif cl == 'under': m = 'under'
        elif cl == 'black': m = 'black'
        elif cl == 'gray': m = 'black'
        elif cl == 'white': m = 'chrome'
        elif cl == 'glass': m = 'glass'
        else: m = 'paint'
        for i in ids: me.polygons[i].material_index = idx[m]
        for i in ids: me.polygons[i].use_smooth = False
    # --- габариты по преобразованному кузову (игровые: z вперёд = −y)
    bmn = [min(v.co[i] for v in me.vertices) for i in range(3)]
    bmx = [max(v.co[i] for v in me.vertices) for i in range(3)]
    front, rear = -bmn[1], -bmx[1]
    wR = sum(w[3] for w in wc) / len(wc) * sz
    axleF = max(-(w[1] - ycen) * sy for w in wc); axleR = min(-(w[1] - ycen) * sy for w in wc)
    wheelW = cfg['wheelW']
    xo = max(abs(w[0]) + w[4] / 2 for w in wc) * sx          # внешний край шины Kenney
    track = 2 * (xo - wheelW / 2 - 0.02)
    # --- номерные плашки: тонкие коробки, плашка смотрит наружу
    bm = bmesh.new(); bm.from_mesh(me)
    ymid = bmn[2] + (bmx[2] - bmn[2]) * 0.0
    plate_z = max(0.36, bmn[2] + 0.33)
    for sgn, yy in ((-1, bmn[1]), (1, bmx[1])):          # −y — перед
        res = bmesh.ops.create_cube(bm, size=1.0)
        vs = res['verts']
        for v in vs:
            v.co = Vector((v.co.x * 0.52, yy + sgn * (-0.006) + v.co.y * 0.012 + (0.0 if sgn < 0 else 0.0), plate_z + v.co.z * 0.115))
            v.co.y = yy + (-0.004 if sgn < 0 else 0.004) + (v.co.y - yy) * 1.0 if False else v.co.y
        for f in set(f for v in vs for f in v.link_faces): f.material_index = idx['plate']
    bm.to_mesh(me); bm.free()
    # --- салон: якоря по геометрии (z вперёд)
    anchors = anchors_of(me, idx, bmn, bmx, front, rear)
    return body, dict(W=cfg['W'], front=front, rear=rear, axleF=axleF, axleR=axleR, track=track, wheelR=wR, wheelW=wheelW,
                      sill=max(0.28, bmn[2] + 0.12), paint_hue=paint_hue), anchors, wc


def anchors_of(me, idx, bmn, bmx, front, rear):
    """top/belt/окна в игровых осях (z вперёд, y вверх) для салона: профиль крыши, линия окон, лобовое и заднее стекло."""
    zs = lambda v: -v.co.y
    top = {}
    for v in me.vertices:
        k = round(zs(v) / 0.25)
        top[k] = max(top.get(k, -9), v.co.z)
    topl = [(k * 0.25, y) for k, y in sorted(top.items(), reverse=True)]
    gl = [p for p in me.polygons if p.material_index == idx['glass']]
    # лобовое: нормаль смотрит вперёд (−y) и вверх; заднее: назад
    fw = [p for p in gl if p.normal.y < -0.3]; bk = [p for p in gl if p.normal.y > 0.3]
    def zr(ps):
        a = [zs(me.vertices[i]) for p in ps for i in p.vertices]
        return (max(a), min(a)) if a else (None, None)
    zW0, zW1 = zr(fw); zB1, zB0 = zr(bk)
    zB1b, zB0b = (zB0, zB1) if zB1 is not None and zB0 is not None and zB1 < zB0 else (zB1, zB0)
    side = [p for p in gl if abs(p.normal.x) > 0.5]
    belt = {}
    for p in side:
        for i in p.vertices:
            v = me.vertices[i]; k = round(zs(v) / 0.25)
            belt[k] = min(belt.get(k, 9), v.co.z)
    ys = sorted(belt.items(), reverse=True)
    sill = sum(y for _, y in ys) / len(ys) if ys else bmn[2] + 0.8 * (bmx[2] - bmn[2])
    if zW0 is None: zW0, zW1 = front - 1.2, front - 2.0
    if zB1b is None: zB1b, zB0b = rear + 1.0, rear + 0.5
    roofY = bmx[2]
    roofX = max(abs(v.co.x) for v in me.vertices if v.co.z > roofY - 0.16)
    # у Kenney нижняя линия окна почти плоская → belt как «замок» окна, края — по профилю капота/багажника
    def top_at(z):
        best = min(topl, key=lambda t: abs(t[0] - z)); return best[1]
    belt_pts = [(front, top_at(front) * 0.97), (zW0, sill), (zB0b, sill), (rear, top_at(rear) * 0.97)]
    return dict(W=bmx[0] * 2, tumble=max(0.02, bmx[0] - roofX), zW0=zW0, zW1=zW1, zB1=zB1b, zB0=zB0b, floor=max(0.2, bmn[2] + 0.12), belt=belt_pts, top=topl)


def fmt(vals): return 'new[] { ' + ', '.join(('%.3ff' % v).replace('.000f', '.0f') for v in vals) + ' }'


def anchor_cs(cid, a):
    tf = [x for z, y in a['belt'] for x in (z, y)]
    top = [x for z, y in a['top'] for x in (z, y)]
    return ('                case "%s": return new Anchor { W = %.3ff, tumble = %.3ff, zW0 = %.3ff, zW1 = %.3ff, zB1 = %.3ff, zB0 = %.3ff, floor = %.3ff, belt = %s, top = %s };'
            % (cid, a['W'], a['tumble'], a['zW0'], a['zW1'], a['zB1'], a['zB0'], a['floor'], fmt(tf), fmt(top)))


def run(ids, out_dir, prev_dir):
    os.makedirs(out_dir, exist_ok=True); os.makedirs(prev_dir, exist_ok=True)
    cs_defs, cs_anch, report = [], [], {}
    for cid in ids:
        cfg = MODELS[cid]
        body, d, anchors, wc = build(cid, cfg)
        objs = []
        for name in ('LOD_ultra', 'LOD_hi', 'LOD0', 'LOD1'):
            o = body.copy(); o.data = body.data.copy(); o.name = name
            bpy.context.scene.collection.objects.link(o); objs.append(o)
        stats = EU.write_bytes(os.path.join(out_dir, cid + '.bytes'), objs)
        print('KENNEY', cid, stats, {k: (round(v, 3) if isinstance(v, float) else v) for k, v in d.items()})
        report[cid] = dict(dims=d, anchors={k: v for k, v in anchors.items()})
        cs_anch.append(anchor_cs(cid, anchors))
        sp = dict(drive=cfg['drive'])
        cs_defs.append(cid)
        # превью: рендер в Cycles с цветами материалов
        render_preview(body, os.path.join(prev_dir, cid + '.png'))
    json.dump(report, open(os.path.join(prev_dir, 'report.json'), 'w'), indent=1, default=float)
    open(os.path.join(prev_dir, 'anchors.txt'), 'w').write('\n'.join(cs_anch))


COL = {'paint': (0.85, 0.3, 0.1), 'glass': (0.04, 0.06, 0.08), 'chrome': (0.8, 0.8, 0.82), 'black': (0.06, 0.06, 0.07), 'rubber': (0.03, 0.03, 0.03),
       'under': (0.01, 0.01, 0.01), 'plate': (0.95, 0.95, 0.92), 'headLamp': (1.0, 0.9, 0.6), 'tailLamp': (0.6, 0.05, 0.05)}


def render_preview(body, path):
    for m in body.data.materials:
        m.use_nodes = True
        bs = m.node_tree.nodes.get('Principled BSDF')
        c = COL.get(m.name.split('.')[0], (0.5, 0.5, 0.5))
        bs.inputs['Base Color'].default_value = (c[0], c[1], c[2], 1)
        bs.inputs['Roughness'].default_value = 0.35
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'; sc.cycles.samples = 24; sc.cycles.device = 'CPU'
    sc.render.resolution_x = 900; sc.render.resolution_y = 520
    sc.render.filepath = path
    w = bpy.data.worlds.new('w'); w.use_nodes = True; w.node_tree.nodes['Background'].inputs[0].default_value = (0.75, 0.8, 0.88, 1); w.node_tree.nodes['Background'].inputs[1].default_value = 1.2
    sc.world = w
    bpy.ops.mesh.primitive_plane_add(size=30, location=(0, 0, 0))
    gr = bpy.context.object; gm = bpy.data.materials.new('g'); gm.use_nodes = True; gm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.5, 0.5, 0.5, 1); gr.data.materials.append(gm)
    sun = bpy.data.lights.new('s', 'SUN'); sun.energy = 3.5; so = bpy.data.objects.new('s', sun); so.rotation_euler = (0.9, 0.2, 0.6); sc.collection.objects.link(so)
    cam = bpy.data.cameras.new('c'); cam.lens = 42; co = bpy.data.objects.new('c', cam); sc.collection.objects.link(co); sc.camera = co
    L = 6.5
    co.location = (-L * 0.75, -L * 0.85, 2.3)     # перед машины — к −y, смотрим спереди-слева
    d = Vector((0, -0.4, 0.75)) - co.location
    co.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    for o in list(bpy.data.objects):
        if o.name in ('LOD_ultra', 'LOD_hi', 'LOD0', 'LOD1'): o.hide_render = (o.name != 'LOD_hi')
    bpy.ops.render.render(write_still=True)


if __name__ == '__main__':
    a = sys.argv[sys.argv.index('--') + 1:]
    SRC = a[0]
    K.load_palette(os.path.join(SRC, 'Textures', 'colormap.png'))
    ids = a[3].split(',') if len(a) > 3 else list(MODELS)
    run(ids, a[1], a[2])
