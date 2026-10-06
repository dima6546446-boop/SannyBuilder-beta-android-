"""Проверка .bytes (CDM1) без Unity: читает файл, сверяет обход треугольников с нормалями, рендерит LOD_hi по материалам.
    blender -b --python cdm_check.py -- <файл.bytes> <картинка.png>
"""
import bpy, sys, struct, gzip, math
from mathutils import Vector
path, out = sys.argv[sys.argv.index('--') + 1:][:2]
d = gzip.open(path).read(); p = 0
def i():
    global p; v = struct.unpack_from('<i', d, p)[0]; p += 4; return v
def f(n):
    global p; v = struct.unpack_from('<%df' % n, d, p); p += 4 * n; return v
def s():
    global p; n = i(); v = d[p:p + n].decode(); p += n; return v
assert d[:4] == b'CDM1' or True
d = d if d[:4] == b'CDM1' else d
p = 4; lods = {}
for _ in range(i()):
    name = s(); vc = i()
    pos = [f(3) for _ in range(vc)]; nrm = [f(3) for _ in range(vc)]; uv = [f(2) for _ in range(vc)]
    subs = []
    for _ in range(i()):
        m = s(); n = i(); idx = struct.unpack_from('<%di' % n, d, p); p += 4 * n; subs.append((m, idx))
    lods[name] = (pos, nrm, subs)
pos, nrm, subs = lods['LOD_hi']
ok = bad = 0
for m, idx in subs:
    for t in range(0, len(idx), 3):
        a, b, c = [Vector(pos[k]) for k in idx[t:t + 3]]
        n = (b - a).cross(c - a)          # Unity: по часовой при взгляде спереди ⇔ эта нормаль наружу
        if n.length < 1e-9: continue
        nn = Vector(nrm[idx[t]])
        if n.dot(nn) > 0: ok += 1
        else: bad += 1
print('WINDING ok', ok, 'bad', bad, 'LODs', {k: (len(v[0]), sum(len(x[1]) for x in v[2]) // 3) for k, v in lods.items()}, 'materials', sorted(set(m for m, _ in subs)))
# сцена: Unity (x,y,z) → Blender (x,z,y) отражение, обход сохраняется как «против часовой»
bpy.ops.wm.read_factory_settings(use_empty=True)
me = bpy.data.meshes.new('car'); verts = [(v[0], v[2], v[1]) for v in pos]; faces = []; mi = []
mats = [m for m, _ in subs]
for k, (m, idx) in enumerate(subs):
    for t in range(0, len(idx), 3): faces.append(tuple(idx[t:t + 3])); mi.append(k)
me.from_pydata(verts, [], faces)
ob = bpy.data.objects.new('car', me); bpy.context.scene.collection.objects.link(ob)
COL = {'paint': (0.85, 0.3, 0.1), 'glass': (0.04, 0.06, 0.08), 'chrome': (0.8, 0.8, 0.82), 'black': (0.06, 0.06, 0.07), 'rubber': (0.03, 0.03, 0.03), 'under': (0.01, 0.01, 0.01), 'plate': (0.95, 0.95, 0.92), 'headLamp': (1, 0.9, 0.6), 'tailLamp': (0.6, 0.05, 0.05)}
for m in mats:
    mt = bpy.data.materials.new(m); mt.use_nodes = True; c = COL.get(m, (0.5, 0.5, 0.5)); mt.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (*c, 1); me.materials.append(mt)
for pl, k in zip(me.polygons, mi): pl.material_index = k
sc = bpy.context.scene; sc.render.engine = 'CYCLES'; sc.cycles.samples = 24; sc.cycles.device = 'CPU'; sc.render.resolution_x = 900; sc.render.resolution_y = 520
w = bpy.data.worlds.new('w'); w.use_nodes = True; w.node_tree.nodes['Background'].inputs[0].default_value = (0.75, 0.8, 0.88, 1); sc.world = w
bpy.ops.mesh.primitive_plane_add(size=30); bpy.context.object.data.materials.append(bpy.data.materials.new('g'))
sun = bpy.data.lights.new('s', 'SUN'); sun.energy = 3.5; so = bpy.data.objects.new('s', sun); so.rotation_euler = (0.9, 0.2, 0.6); sc.collection.objects.link(so)
cam = bpy.data.cameras.new('c'); cam.lens = 42; co = bpy.data.objects.new('c', cam); sc.collection.objects.link(co); sc.camera = co
# Unity +z вперёд → Blender +y; смотрим спереди-слева (в Unity +x вправо)
co.location = (-5.0, 5.5, 2.3); co.rotation_euler = (Vector((0, 0, 0.75)) - co.location).to_track_quat('-Z', 'Y').to_euler()
sc.render.filepath = out; bpy.ops.render.render(write_still=True)
