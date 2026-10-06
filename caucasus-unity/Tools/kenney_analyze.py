import bpy, sys, os, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kenney_common as K
SRC = '/tmp/dl/ck/Models/GLB format/'
K.load_palette(SRC + 'Textures/colormap.png')
for name in sys.argv[sys.argv.index('--') + 1:]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=SRC + name + '.glb')
    body = bpy.data.objects['body']; me = body.data; uv = me.uv_layers.active.data
    area = collections.Counter(); cen = collections.defaultdict(lambda: [0, 0, 0, 0])
    for p in me.polygons:
        u = sum(uv[l].uv[0] for l in p.loop_indices) / len(p.loop_indices); v = sum(uv[l].uv[1] for l in p.loop_indices) / len(p.loop_indices)
        c = K.cell_of(u, v); area[c] += p.area
        c3 = body.matrix_world @ p.center
        a = cen[c]; a[0] += c3.x * p.area; a[1] += c3.y * p.area; a[2] += c3.z * p.area; a[3] += p.area
    print('MODEL', name)
    for c, a in area.most_common():
        rgb = K.cell_rgb(c); w = cen[c]
        print('  cell', c, 'class', K.classify(rgb), 'rgb', tuple(round(x * 255) for x in rgb), 'area', round(a, 2), 'centroid', tuple(round(w[i] / w[3], 2) for i in range(3)))
