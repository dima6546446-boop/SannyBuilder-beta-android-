"""
Импорт готовой модели машины (GLB/glTF, например с Sketchfab под CC-BY) в игру:
    blender -b --python import_asset.py -- <cid> <model.glb> <web_out_dir> <unity_out_dir> [--flip] [--report r.json]

Что делает:
 1. Склеивает все меши, применяет трансформации, убирает мусор (пол, тени, плоскости).
 2. Разворачивает: длинная ось — вдоль Y, перёд — в −Y (как у наших моделей; «перёд» = сторона,
    противоположная середине крыши: кабина у седанов и хэтчбеков смещена назад). --flip — развернуть на 180°.
 3. Масштабирует по длине из cars_dims.json, ставит на землю и центрирует по габаритам игры.
 4. Удаляет колёса (игра рисует свои вращающиеся): острова геометрии в зоне каждого колеса.
 5. Раскладывает материалы по именам игры: paint, glass, chrome, black, rubber, plate, interior,
    headLamp, tailLamp, reverseLamp, indL, indR (по имени, цвету, металличности, прозрачности и месту).
 6. Добавляет номерные плашки, если их нет (лучом на бампер спереди и сзади).
 7. Делает LOD: LOD_ultra ≤ 60k, LOD_hi ≤ 30k (машина игрока), LOD0 ≤ 4.5k, LOD1 ≤ 1.2k треугольников.
 8. Пишет public/models/<cid>.glb (Draco, LOD_hi/LOD0/LOD1) и Unity Resources/Cars/<cid>.bytes.
"""
import bpy, bmesh, sys, os, json, math, colorsys
from mathutils import Vector, Matrix
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_cars as B
import export_unity as EU

argv = sys.argv[sys.argv.index('--') + 1:]
cid, src, web_out, unity_out = argv[:4]
FLIP = '--flip' in argv
REPORT = argv[argv.index('--report') + 1] if '--report' in argv else None
D = B.DIMS[cid]['dims']
rep = {'cid': cid, 'src': os.path.basename(src)}

BUDGET = {'LOD_ultra': 60000, 'LOD_hi': 30000, 'LOD0': 4500, 'LOD1': 1200}
GAME_MATS = ['paint', 'glass', 'chrome', 'black', 'rubber', 'under', 'plate', 'interior', 'headLamp', 'tailLamp', 'reverseLamp', 'indL', 'indR', 'grille']


def tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def select_only(obs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs: o.select_set(True)
    bpy.context.view_layer.objects.active = obs[0]


# ---------------------------------------------------------------- 1. импорт и склейка
bpy.ops.wm.read_factory_settings(use_empty=True)
if src.lower().endswith(('.glb', '.gltf')):
    bpy.ops.import_scene.gltf(filepath=src)
elif src.lower().endswith('.fbx'):
    bpy.ops.import_scene.fbx(filepath=src)
else:
    bpy.ops.wm.obj_import(filepath=src)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
for o in bpy.context.scene.objects:
    o.hide_set(False); o.hide_viewport = False
select_only(meshes)
bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
for o in meshes:
    if o.data.users > 1: o.data = o.data.copy()  # инстансы → отдельные данные, иначе transform_apply падает
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for o in list(bpy.context.scene.objects):
    if o.type != 'MESH': bpy.data.objects.remove(o)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
for o in meshes:  # модификаторы (зеркало, сабдив) — применить
    bpy.context.view_layer.objects.active = o
    for m in list(o.modifiers):
        try: bpy.ops.object.modifier_apply(modifier=m.name)
        except Exception: o.modifiers.remove(m)
select_only(meshes)
bpy.ops.object.join()
car = bpy.context.view_layer.objects.active
car.name = 'Asset'
rep['tris_src'] = tris(car)
# GLB дублирует вершины на швах UV и острых рёбрах — склеиваем совпадающие (иначе шина распадается на полоски)
_vs = [v.co for v in car.data.vertices]
_diag = (Vector((max(c.x for c in _vs), max(c.y for c in _vs), max(c.z for c in _vs))) - Vector((min(c.x for c in _vs), min(c.y for c in _vs), min(c.z for c in _vs)))).length
_bm = bmesh.new(); _bm.from_mesh(car.data)
bmesh.ops.remove_doubles(_bm, verts=_bm.verts, dist=_diag * 2e-5)
_bm.to_mesh(car.data); _bm.free(); car.data.update()


def islands(ob):
    """Острова геометрии: списки индексов граней (связность по вершинам)."""
    bm = bmesh.new(); bm.from_mesh(ob.data); bm.faces.ensure_lookup_table(); bm.verts.ensure_lookup_table()
    seen = bytearray(len(bm.faces)); out = []
    for f in bm.faces:
        if seen[f.index]: continue
        stack = [f]; seen[f.index] = 1; comp = []
        while stack:
            g = stack.pop(); comp.append(g.index)
            for v in g.verts:
                for h in v.link_faces:
                    if not seen[h.index]: seen[h.index] = 1; stack.append(h)
        out.append(comp)
    return bm, out


def comp_bbox(bm, comp):
    lo = Vector((1e9, 1e9, 1e9)); hi = Vector((-1e9, -1e9, -1e9))
    for fi in comp:
        for v in bm.faces[fi].verts:
            c = v.co
            lo.x = min(lo.x, c.x); lo.y = min(lo.y, c.y); lo.z = min(lo.z, c.z)
            hi.x = max(hi.x, c.x); hi.y = max(hi.y, c.y); hi.z = max(hi.z, c.z)
    return lo, hi


def delete_faces(ob, faces):
    if not faces: return
    bm = bmesh.new(); bm.from_mesh(ob.data); bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.faces[i] for i in faces], context='FACES')
    bm.to_mesh(ob.data); bm.free(); ob.data.update()


# мусор: плоские огромные острова (пол/тень) — площадь проекции больше 2× площади машины
bm, comps = islands(car)
lo_all = Vector((min(v.co.x for v in car.data.vertices), min(v.co.y for v in car.data.vertices), min(v.co.z for v in car.data.vertices)))
hi_all = Vector((max(v.co.x for v in car.data.vertices), max(v.co.y for v in car.data.vertices), max(v.co.z for v in car.data.vertices)))
junk = []
sizes = []
for comp in comps:
    lo, hi = comp_bbox(bm, comp)
    sizes.append((lo, hi, comp))
zmin = min((l.z for l, h, c in sizes), default=0)
real = [max(h - l) for l, h, c in sizes if len(c) > 8 or min(h - l) > 0.02 * max(h - l)]
body = max(real, default=1)  # размер машины без плоских «подложек»
for lo, hi, comp in sizes:
    d = hi - lo; mx = max(d.x, d.y, d.z)
    flat = min(d.x, d.y, d.z) < 0.01 * mx
    # пол/фон: плоский и больше машины; тень: плоская подложка у самого низа, сравнимая с машиной
    floor = flat and len(comp) <= 8 and mx > 1.15 * body
    shadow = flat and len(comp) <= 4 and lo.z - zmin < 0.02 * body and mx > 0.5 * body
    if floor or shadow: junk += comp
bm.free()
delete_faces(car, junk)
rep['junk_faces'] = len(junk)

# ---------------------------------------------------------------- 2–3. ориентация и масштаб
def bbox(ob):
    xs = [v.co.x for v in ob.data.vertices]; ys = [v.co.y for v in ob.data.vertices]; zs = [v.co.z for v in ob.data.vertices]
    return Vector((min(xs), min(ys), min(zs))), Vector((max(xs), max(ys), max(zs)))


lo, hi = bbox(car)
if hi.x - lo.x > hi.y - lo.y:  # длина по X → повернуть к Y
    car.data.transform(Matrix.Rotation(math.pi / 2, 4, 'Z'))
    lo, hi = bbox(car)
# крыша: вершины в верхних 12 % высоты; кабина смещена к заду машины
# крыша — горизонтальные грани (нормаль вверх) в верхних 20 % высоты, с весом по площади:
# антенны, рейлинги и мигалки тонкие и почти не весят
_zs = sorted(v.co.z for v in car.data.vertices)
z_roof = _zs[int(len(_zs) * 0.985)]  # «высота крыши» без антенны и мигалок
top = z_roof - (z_roof - lo.z) * 0.2
wy = wsum = 0.0
for p in car.data.polygons:
    if p.center.z >= top and p.normal.z > 0.7:
        wy += p.center.y * p.area; wsum += p.area
cy = (lo.y + hi.y) / 2
roof_c = wy / wsum if wsum > 0 else cy
rep['roof_offset'] = round((roof_c - cy) / (hi.y - lo.y), 3)
# голосование «где зад» (+1 — зад в +Y): красные фонари (мелкие красные материалы), имена материалов
# фар/фонарей, смещение крыши. Красный кузов не мешает: материалы больше 6 % площади не считаются.
def _base_rgb(m):
    if not m or not m.use_nodes: return (0.5, 0.5, 0.5)
    b = next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    return tuple(b.inputs['Base Color'].default_value[:3]) if b else (0.5, 0.5, 0.5)
_mats = car.data.materials
_area = [0.0] * max(1, len(_mats)); _tot = 0.0
for p in car.data.polygons: _area[p.material_index] += p.area; _tot += p.area
votes = []
red_y = red_w = 0.0; head_y = head_w = 0.0; tail_y = tail_w = 0.0
for p in car.data.polygons:
    mi = p.material_index
    m = _mats[mi] if mi < len(_mats) else None
    nm = (m.name.lower() if m else '')
    r, g, b = _base_rgb(m)
    small = _area[mi] < 0.06 * _tot
    if small and r > 0.45 and g < 0.22 and b < 0.22: red_y += p.center.y * p.area; red_w += p.area
    if any(w in nm for w in ('head', 'fara', 'фар', 'front', 'перед')): head_y += p.center.y * p.area; head_w += p.area
    if any(w in nm for w in ('tail', 'stop', 'rear', 'back', 'зад', 'стоп', 'brake')): tail_y += p.center.y * p.area; tail_w += p.area
if red_w > 0: votes.append(('red', 2.0 * (1 if red_y / red_w > cy else -1)))
if head_w > 0: votes.append(('head', 1.5 * (1 if head_y / head_w < cy else -1)))
if tail_w > 0: votes.append(('tail', 1.5 * (1 if tail_y / tail_w > cy else -1)))
votes.append(('roof', (1 if roof_c > cy else -1) * min(1.0, abs(roof_c - cy) / (hi.y - lo.y) * 20)))
rep['orientation_votes'] = {k: round(v, 2) for k, v in votes}
front_is_minus_y = sum(v for k, v in votes) >= 0  # зад в +Y → перёд в −Y
if not front_is_minus_y: car.data.transform(Matrix.Rotation(math.pi, 4, 'Z'))
if FLIP: car.data.transform(Matrix.Rotation(math.pi, 4, 'Z'))
lo, hi = bbox(car)
L = D['front'] - D['rear']
s = L / (hi.y - lo.y)
car.data.transform(Matrix.Scale(s, 4))
lo, hi = bbox(car)
# перёд (min y) → −front; земля → z = 0; центр по X
car.data.transform(Matrix.Translation(Vector((-(lo.x + hi.x) / 2, -D['front'] - lo.y, -lo.z))))
lo, hi = bbox(car)
rep['scale'] = round(s, 4)
rep['size'] = [round(hi.x - lo.x, 3), round(hi.y - lo.y, 3), round(hi.z - lo.z, 3)]
rep['width_game'] = D['W']

# ---------------------------------------------------------------- 4. колёса
# шина — «круглый» остров у земли размером ~2R; всё, что целиком внутри её цилиндра (диск, болты,
# тормоз) — тоже колесо. Подкрылки и арки больше шины и выше неё — остаются.
R, TR = D['wheelR'], D['track']
bm, comps = islands(car)
boxes = [(comp_bbox(bm, c), c) for c in comps]
if os.environ.get('DEBUG_ISLANDS'):
    for (l, h), c in [b for b in boxes if b[0][0].z < 0.15][:20]:
        print('ISLAND', len(c), [round(v, 3) for v in l], [round(v, 3) for v in h])
tires = []
for (lo_c, hi_c), comp in boxes:
    d = hi_c - lo_c
    dia = max(d.y, d.z)
    if not (1.3 * R < dia < 3.0 * R): continue
    if abs(d.y - d.z) > 0.18 * dia or d.x > 0.5 or d.x < 0.04: continue
    if lo_c.z > 0.08: continue
    c = (lo_c + hi_c) / 2
    if abs(c.x) < 0.25: continue  # запаска/что-то по центру
    tires.append((c, dia / 2, d.x))
# по одной (самой крупной) шине на угол
corners = {}
for c, r, w in tires:
    key = (c.x > 0, c.y > 0)
    if key not in corners or r > corners[key][1]: corners[key] = (c, r, w)
remove = set()
for c, r, w in corners.values():
    for (lo_c, hi_c), comp in boxes:
        if (lo_c.y >= c.y - r * 1.06 and hi_c.y <= c.y + r * 1.06 and lo_c.z >= c.z - r * 1.06 and hi_c.z <= c.z + r * 1.06
                and min(abs(lo_c.x), abs(hi_c.x)) > abs(c.x) - w * 1.6 and (lo_c.x + hi_c.x > 0) == (c.x > 0)):
            remove.update(comp)
bm.free()
delete_faces(car, sorted(remove))
rep['wheels_found'] = len(corners)
rep['wheel_faces_removed'] = len(remove)
if len(corners) == 4:
    ys = sorted({round(c.y, 2) for c, r, w in corners.values()})
    fy = [c.y for c, r, w in corners.values() if c.y < 0]; ry = [c.y for c, r, w in corners.values() if c.y > 0]
    rep['model_wheels'] = {
        'axleF': round(-sum(fy) / len(fy), 3), 'axleR': round(-sum(ry) / len(ry), 3),
        'track': round(sum(abs(c.x) for c, r, w in corners.values()) / 2, 3),
        'wheelR': round(sum(r for c, r, w in corners.values()) / 4, 3), 'wheelW': round(sum(w for c, r, w in corners.values()) / 4, 3),
    }
rep['game_wheels'] = {k: D[k] for k in ('axleF', 'axleR', 'track', 'wheelR', 'wheelW')}
# точная подгонка: колёсная база модели = базе игры, оси — на местах осей игры (колёса игры встают в арки)
if len(corners) == 4:
    mw = rep['model_wheels']
    k = (D['axleF'] - D['axleR']) / (mw['axleF'] - mw['axleR'])
    car.data.transform(Matrix.Scale(k, 4))
    dy = -D['axleF'] - k * (-mw['axleF'])
    car.data.transform(Matrix.Translation(Vector((0, dy, 0))))
    lo, hi = bbox(car)  # земля остаётся на z = 0 (низ шин до удаления колёс), масштаб — от неё
    rep['wheelbase_fit'] = round(k, 4)
    rep['fitted_wheels'] = {'track': round(mw['track'] * k, 3), 'wheelR': round(mw['wheelR'] * k, 3), 'wheelW': round(mw['wheelW'] * k, 3)}
    rep['size'] = [round(hi.x - lo.x, 3), round(hi.y - lo.y, 3), round(hi.z - lo.z, 3)]
    rep['front_rear'] = [round(-lo.y, 3), round(-hi.y, 3)]

# ---------------------------------------------------------------- 5. материалы
def mat_info(m):
    info = {'name': (m.name if m else '').lower(), 'color': (0.5, 0.5, 0.5), 'metal': 0.0, 'alpha': 1.0, 'trans': 0.0, 'emit': 0.0, 'tex': False}
    if not m or not m.use_nodes: return info
    bsdf = next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if not bsdf: return info
    col = bsdf.inputs['Base Color']
    info['color'] = tuple(col.default_value[:3])
    if col.is_linked:
        info['tex'] = True
        node = col.links[0].from_node
        img = getattr(node, 'image', None)
        if img and img.size[0] > 0:
            px = img.pixels[:]; n = len(px) // 4; step = max(1, n // 4000)
            r = g = b = 0.0; k = 0
            for i in range(0, n, step): r += px[i * 4]; g += px[i * 4 + 1]; b += px[i * 4 + 2]; k += 1
            info['color'] = (r / k, g / k, b / k)
    info['metal'] = bsdf.inputs['Metallic'].default_value
    info['alpha'] = bsdf.inputs['Alpha'].default_value
    tw = bsdf.inputs.get('Transmission Weight') or bsdf.inputs.get('Transmission')
    info['trans'] = tw.default_value if tw else 0.0
    es = bsdf.inputs.get('Emission Strength')
    ec = bsdf.inputs.get('Emission Color') or bsdf.inputs.get('Emission')
    if es and ec: info['emit'] = es.default_value * max(ec.default_value[:3])
    if getattr(m, 'blend_method', 'OPAQUE') in ('BLEND', 'HASHED') and info['alpha'] < 0.99: info['trans'] = max(info['trans'], 0.5)
    return info


def has(name, words): return any(w in name for w in words)


GLASS_W = ['glass', 'window', 'стекл', 'windshield', 'steklo', 'okno', 'окно', 'windscreen']
LAMP_W = ['light', 'lamp', 'fara', 'фар', 'headl', 'tail', 'stop', 'фонар', 'povorot', 'поворот', 'turn', 'signal', 'indicator', 'blinker', 'reverse', 'lens', 'optic', 'оптик', 'габарит']
TIRE_W = ['tire', 'tyre', 'rubber', 'резин', 'шин', 'shina', 'wheel_r', 'protector']
CHROME_W = ['chrome', 'хром', 'nickel', 'никел', 'polish', 'mirror_chrome']
PLATE_W = ['plate', 'номер', 'licen', 'nomer', 'number']
INT_W = ['interior', 'salon', 'салон', 'seat', 'сиден', 'dash', 'торпед', 'руль', 'steer', 'carpet', 'cabin', 'inside', 'door_in', 'panel_in']
PAINT_W = ['body', 'paint', 'кузов', 'carpaint', 'car_paint', 'kuzov', 'кр', 'color', 'colour', 'base_col', 'exterior']

me = car.data
infos = [mat_info(m) for m in me.materials]
# площадь поверхности на материал
area = [0.0] * max(1, len(me.materials))
for p in me.polygons: area[p.material_index] += p.area
classes = []
for i, inf in enumerate(infos):
    n, (r, g, b) = inf['name'], inf['color']
    h, l, sat = colorsys.rgb_to_hls(r, g, b)
    lum = 0.2126 * r + 0.7152 * g + 0.0722 * b
    if has(n, PLATE_W): c = 'plate'
    elif has(n, LAMP_W): c = 'lamp'
    elif has(n, GLASS_W) or inf['trans'] > 0.3 or inf['alpha'] < 0.9: c = 'glass'
    elif has(n, TIRE_W): c = 'rubber'
    elif has(n, INT_W): c = 'interior'
    elif has(n, CHROME_W) or (inf['metal'] > 0.6 and lum > 0.35 and sat < 0.25): c = 'chrome'
    elif inf['emit'] > 0.5: c = 'lamp'
    elif has(n, PAINT_W) and lum > 0.03: c = 'paint?'
    elif lum < 0.12: c = 'black'
    else: c = 'paint?'
    classes.append(c)
# кузов — самый большой по площади «цветной» материал; остальные кандидаты — по близости цвета к нему
cands = [i for i, c in enumerate(classes) if c == 'paint?']
paint_i = max(cands, key=lambda i: area[i]) if cands else None
if paint_i is None:  # всё распознано иначе — берём самый большой непрозрачный не-чёрный
    opaque = [i for i, c in enumerate(classes) if c in ('chrome', 'black')]
    paint_i = max(opaque, key=lambda i: area[i]) if opaque else 0
pc = infos[paint_i]['color']
for i, c in enumerate(classes):
    if c == 'paint?':
        dc = sum(abs(a - b) for a, b in zip(infos[i]['color'], pc))
        classes[i] = 'paint' if dc < 0.25 else ('black' if sum(infos[i]['color']) / 3 < 0.25 else 'chrome' if sum(infos[i]['color']) / 3 > 0.55 else 'black')
classes[paint_i] = 'paint'
rep['materials'] = {me.materials[i].name if me.materials[i] else str(i): classes[i] for i in range(len(classes))}

# слоты игры; фонари — по положению грани (перед/зад) и цвету
slot = {n: i for i, n in enumerate(GAME_MATS)}
new_idx = []
for p in me.polygons:
    c = classes[p.material_index] if p.material_index < len(classes) else 'black'
    if c == 'lamp':
        r, g, b = infos[p.material_index]['color']
        orange = r > 0.5 and 0.2 < g < 0.75 and b < 0.3
        red = r > 0.35 and g < 0.25 and b < 0.25
        y, x = p.center.y, p.center.x
        if orange: c = 'indL' if x > 0 else 'indR'
        elif y < -D['front'] * 0.3: c = 'headLamp'
        elif red or y > 0: c = 'tailLamp'
        else: c = 'reverseLamp'
    new_idx.append(slot[c])
me.materials.clear()
for n in GAME_MATS: me.materials.append(B.cb.get_mat(n))
for p, k in zip(me.polygons, new_idx): p.material_index = k
me.update()

# ---------------------------------------------------------------- 6. номера
if not any(GAME_MATS[k] == 'plate' for k in new_idx):
    deps = bpy.context.evaluated_depsgraph_get()
    plates = 0
    for sgn, z0 in ((-1, 0.48), (1, 0.55)):  # −1 — перёд (−Y)
        origin = Vector((0, sgn * 6.0, z0))
        ok, loc, nrm, *_ = bpy.context.scene.ray_cast(deps, origin, Vector((0, -sgn, 0)))
        if not ok: continue
        loc = loc + Vector((0, sgn * 0.012, 0))
        bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
        pl = bpy.context.object
        pl.scale = (0.52, 0.01, 0.115)
        bpy.ops.object.transform_apply(scale=True)
        pl.data.materials.append(B.cb.get_mat('plate'))
        select_only([car, pl]); bpy.ops.object.join(); car = bpy.context.view_layer.objects.active
        plates += 1
    rep['plates_added'] = plates
me = car.data

# ---------------------------------------------------------------- 7. LOD
bpy.context.view_layer.objects.active = car
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.quads_convert_to_tris(); bpy.ops.mesh.remove_doubles(threshold=0.0004)
bpy.ops.object.mode_set(mode='OBJECT')
if me.has_custom_normals: bpy.ops.mesh.customdata_custom_splitnormals_clear()


def make_lod(name, budget, drop_interior):
    ob = car.copy(); ob.data = car.data.copy(); ob.name = name; ob.data.name = name
    bpy.context.scene.collection.objects.link(ob)
    if drop_interior:
        k = GAME_MATS.index('interior')
        delete_faces(ob, [p.index for p in ob.data.polygons if p.material_index == k])
    t = tris(ob)
    if t > budget:
        m = ob.modifiers.new('dec', 'DECIMATE'); m.ratio = budget / t; m.use_collapse_triangulate = True
        bpy.context.view_layer.objects.active = ob; bpy.ops.object.modifier_apply(modifier='dec')
    select_only([ob]); bpy.ops.object.shade_smooth_by_angle(angle=math.radians(38))
    return ob


lods = [make_lod('LOD_ultra', BUDGET['LOD_ultra'], False), make_lod('LOD_hi', BUDGET['LOD_hi'], False),
        make_lod('LOD0', BUDGET['LOD0'], True), make_lod('LOD1', BUDGET['LOD1'], True)]
bpy.data.objects.remove(car)
rep['tris'] = {o.name: tris(o) for o in lods}

# ---------------------------------------------------------------- 8. экспорт
os.makedirs(web_out, exist_ok=True); os.makedirs(unity_out, exist_ok=True)
select_only(lods[1:])
glb = os.path.join(web_out, f'{cid}.glb')
bpy.ops.export_scene.gltf(
    filepath=glb, use_selection=True, export_apply=True, export_format='GLB', export_texcoords=True, export_normals=True,
    export_materials='EXPORT', export_draco_mesh_compression_enable=True, export_draco_mesh_compression_level=7,
    export_draco_position_quantization=14, export_draco_normal_quantization=10, export_yup=True, export_cameras=False, export_lights=False)
rep['glb_kb'] = os.path.getsize(glb) // 1024
rep['unity'] = EU.write_bytes(os.path.join(unity_out, f'{cid}.bytes'), lods)
rep['unity_kb'] = os.path.getsize(os.path.join(unity_out, f'{cid}.bytes')) // 1024
print('IMPORT_REPORT ' + json.dumps(rep, ensure_ascii=False))
if REPORT: json.dump(rep, open(REPORT, 'w'), ensure_ascii=False, indent=1)
