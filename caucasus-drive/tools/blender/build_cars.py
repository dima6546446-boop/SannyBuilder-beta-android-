"""
Сборка всех моделей в GLB:
    blender -b --python build_cars.py -- <out_dir> [id,id,...]
В каждом GLB три меша: LOD_hi (машина игрока, ~20–30k треуг.), LOD0 (трафик вблизи), LOD1 (трафик вдали).
"""
import bpy, sys, os, json, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import carbuilder as cb
import details
from shapes import SHAPES

HERE = os.path.dirname(os.path.abspath(__file__))
DIMS = json.load(open(os.path.join(HERE, 'cars_dims.json')))
LEVELS = {'hi': 2, 'lod0': 1, 'lod1': 0}
NAMES = {'hi': 'LOD_hi', 'lod0': 'LOD0', 'lod1': 'LOD1'}


def shape_for(cid):
    d = DIMS[cid]
    base = SHAPES.get(cid) or SHAPES.get(d['id']) or SHAPES['vaz2107']
    dm = d['dims']
    S = dict(base)
    S.update(W=dm['W'], front=dm['front'], rear=dm['rear'], axleF=dm['axleF'], axleR=dm['axleR'],
             archR=dm['archR'], wheelR=dm['wheelR'], track=dm['track'])
    S.setdefault('seams', [])
    S.setdefault('handles', [z + 0.2 for z in S['seams'][1:]])
    S['dims'] = d
    return S


def select_only(obs):
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for o in obs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = obs[0]


def build_car_object(cid, lod='hi'):
    S = shape_for(cid)
    body = cb.build_body(S, LEVELS[lod])
    surf = cb.Surface(body)
    parts = details.build_details(S, surf, lod).to_object('details')
    select_only([body, parts])
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = NAMES[lod]
    ob.data.name = NAMES[lod]
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(38))
    if lod in ('lod0', 'lod1'):
        m = ob.modifiers.new('dec', 'DECIMATE')
        m.ratio = 0.6 if lod == 'lod0' else 0.5
        m.use_collapse_triangulate = True
        cb.apply_all(ob)
    return ob


def build_glb(cid, out_dir):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    obs = [build_car_object(cid, l) for l in ('hi', 'lod0', 'lod1')]
    stats = {o.name: sum(len(p.vertices) - 2 for p in o.data.polygons) for o in obs}
    select_only(obs)
    path = os.path.join(out_dir, f'{cid}.glb')
    bpy.ops.export_scene.gltf(
        filepath=path, use_selection=True, export_apply=True, export_format='GLB',
        export_texcoords=True, export_normals=True, export_materials='EXPORT',
        export_draco_mesh_compression_enable=True, export_draco_mesh_compression_level=7,
        export_draco_position_quantization=14, export_draco_normal_quantization=10,
        export_yup=True, export_cameras=False, export_lights=False,
    )
    print(f'BUILT {cid}: {stats}  {os.path.getsize(path) // 1024} KB')


if __name__ == '__main__':
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    if argv and not argv[0].endswith('.png'):
        out = argv[0]
        os.makedirs(out, exist_ok=True)
        ids = argv[1].split(',') if len(argv) > 1 else list(DIMS.keys())
        for cid in ids:
            build_glb(cid, out)
