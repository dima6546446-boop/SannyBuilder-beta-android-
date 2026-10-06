"""
Экспорт машин для Unity-версии (caucasus-unity/Assets/CaucasusDrive/Resources/Cars/<id>.bytes):
    blender -b --python export_unity.py -- <out_dir> [id,id,...] [--hi-level 3]

Те же формы, что и для веб-версии (shapes.py → carbuilder.py → details.py), но LOD_hi
по умолчанию с субдивом 3 (гладкий кузов для машины игрока), плюс LOD0 и LOD1 для трафика.

Формат .bytes (gzip, внутри little-endian):
  'CDM1', int32 lodCount
  на каждый LOD: str name, int32 vertexCount, float3 pos[], float3 normal[], float2 uv[],
                 int32 submeshCount, на каждый: str material, int32 indexCount, int32 idx[]
  str = int32 длина + UTF-8.
Координаты уже в системе Unity: +X вправо, +Y вверх, +Z вперёд (левосторонняя),
поэтому X зеркалится, а порядок вершин в треугольнике переставлен (лицевая сторона — по часовой).
"""
import bpy, bmesh, sys, os, struct, math, gzip
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_cars as B


def wstr(f, s):
    b = s.encode('utf-8')
    f.write(struct.pack('<i', len(b)))
    f.write(b)


def mesh_data(ob):
    """Треугольники объекта, разбитые по материалам; вершины уникальны по (позиция, нормаль, uv)."""
    deps = bpy.context.evaluated_depsgraph_get()
    ev = ob.evaluated_get(deps)
    me = ev.to_mesh()
    me.calc_loop_triangles()
    normals = me.corner_normals
    uvl = me.uv_layers.active.data if me.uv_layers.active else None
    mats = [m.name if m else 'paint' for m in me.materials] or ['paint']
    verts, index = [], {}
    subs = {}
    for tri in me.loop_triangles:
        ids = []
        for li in tri.loops:
            vi = me.loops[li].vertex_index
            co = me.vertices[vi].co
            n = normals[li].vector
            uv = uvl[li].uv if uvl else (0.0, 0.0)
            # Blender (x, y, z), где x — влево от машины, -y — вперёд, z — вверх → Unity (-x, z, -y)
            key = (round(-co.x, 5), round(co.z, 5), round(-co.y, 5), round(-n.x, 3), round(n.z, 3), round(-n.y, 3), round(uv[0], 4), round(uv[1], 4))
            k = index.get(key)
            if k is None:
                k = index[key] = len(verts)
                verts.append(key)
            ids.append(k)
        name = mats[tri.material_index] if tri.material_index < len(mats) else mats[0]
        name = name.split('.')[0]
        subs.setdefault(name, []).extend((ids[0], ids[2], ids[1]))  # смена обхода
    ev.to_mesh_clear()
    return verts, subs


def export_car(cid, out_dir, hi_level):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    obs = []
    # LOD_ultra — субдив hi_level (качество «Высокое»), LOD_hi — субдив 2 (как в веб-версии)
    for lvl, name in ((hi_level, 'LOD_ultra'), (1, 'LOD_hi')):
        B.LEVELS['hi'] = lvl
        ob = B.build_car_object(cid, 'hi')
        ob.name = name
        obs.append(ob)
    obs += [B.build_car_object(cid, l) for l in ('lod0', 'lod1')]
    path = os.path.join(out_dir, f'{cid}.bytes')
    stats = write_bytes(path, obs)
    print(f'UNITY {cid}: {stats}  {os.path.getsize(path) // 1024} KB (gzip)')


def write_bytes(path, obs):
    """Записать объекты-LOD (имя объекта = имя LOD) в .bytes (gzip). Возвращает {LOD: (вершины, треугольники)}."""
    stats = {}
    with open(path, 'wb') as f:
        f.write(b'CDM1')
        f.write(struct.pack('<i', len(obs)))
        for ob in obs:
            verts, subs = mesh_data(ob)
            wstr(f, ob.name)
            f.write(struct.pack('<i', len(verts)))
            for v in verts: f.write(struct.pack('<3f', v[0], v[1], v[2]))
            for v in verts: f.write(struct.pack('<3f', v[3], v[4], v[5]))
            for v in verts: f.write(struct.pack('<2f', v[6], v[7]))
            f.write(struct.pack('<i', len(subs)))
            for name, idx in subs.items():
                wstr(f, name)
                f.write(struct.pack('<i', len(idx)))
                f.write(struct.pack(f'<{len(idx)}i', *idx))
            stats[ob.name] = (len(verts), sum(len(i) for i in subs.values()) // 3)
    raw = open(path, 'rb').read()
    open(path, 'wb').write(gzip.compress(raw, 9))  # загрузчик в Unity распознаёт gzip по сигнатуре
    return stats


if __name__ == '__main__':
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    level = 2
    if '--hi-level' in argv:
        i = argv.index('--hi-level'); level = int(argv[i + 1]); del argv[i:i + 2]
    out = argv[0]
    os.makedirs(out, exist_ok=True)
    ids = argv[1].split(',') if len(argv) > 1 else list(B.DIMS.keys())
    for cid in ids:
        export_car(cid, out, level)
