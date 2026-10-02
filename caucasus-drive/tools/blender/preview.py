"""Рендер превью моделей (Cycles, CPU): blender -b --python preview.py -- out.png id1,id2 [angle]"""
import bpy, sys, math, os
sys.path.insert(0, os.path.dirname(__file__))
from mathutils import Vector

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
out = argv[0] if argv else '/tmp/preview.png'
ids = argv[1].split(',') if len(argv) > 1 else ['vaz2107']
view = argv[2] if len(argv) > 2 else 'front34'
import math
glb_dir = argv[3] if len(argv) > 3 else None

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene

if glb_dir:
    objs = []
    for i, cid in enumerate(ids):
        bpy.ops.import_scene.gltf(filepath=os.path.join(glb_dir, f'{cid}.glb'))
        for o in bpy.context.selected_objects:
            if o.type == 'MESH' and not o.name.startswith('LOD_hi'):
                o.hide_render = True
            o.location.x += (i - (len(ids) - 1) / 2) * 2.6
else:
    import carbuilder, build_cars
    for i, cid in enumerate(ids):
        ob = build_cars.build_car_object(cid, os.environ.get('LOD', 'hi'))
        ob.location.x += (i - (len(ids) - 1) / 2) * 2.6
        D = build_cars.DIMS[cid]['dims']
        tm = bpy.data.materials.new('tire'); tm.use_nodes = True
        tm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.02, 0.02, 0.02, 1)
        hm = bpy.data.materials.new('hub'); hm.use_nodes = True
        hb = hm.node_tree.nodes['Principled BSDF']; hb.inputs['Base Color'].default_value = (0.8, 0.8, 0.8, 1); hb.inputs['Metallic'].default_value = 1; hb.inputs['Roughness'].default_value = 0.2
        for z in (D['axleF'], D['axleR']):
            for sgn in (1, -1):
                x = sgn * D['track'] / 2 + ob.location.x
                bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=D['wheelR'], depth=D['wheelW'], location=(x, -z, D['wheelR']), rotation=(0, math.pi / 2, 0))
                bpy.context.object.data.materials.append(tm); bpy.ops.object.shade_smooth()
                bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=D['wheelR'] * 0.6, depth=0.02, location=(x + sgn * D['wheelW'] / 2, -z, D['wheelR']), rotation=(0, math.pi / 2, 0))
                bpy.context.object.data.materials.append(hm)

# пол, свет, камера
bpy.ops.mesh.primitive_plane_add(size=60)
fl = bpy.context.object
m = bpy.data.materials.new('floor'); m.use_nodes = True
b = m.node_tree.nodes['Principled BSDF']; b.inputs['Base Color'].default_value = (0.3, 0.3, 0.3, 1); b.inputs['Roughness'].default_value = 0.5
fl.data.materials.append(m)
world = bpy.data.worlds.new('w'); sc.world = world; world.use_nodes = True
bg = world.node_tree.nodes['Background']; bg.inputs['Color'].default_value = (0.55, 0.65, 0.8, 1); bg.inputs['Strength'].default_value = 0.8
sun = bpy.data.objects.new('sun', bpy.data.lights.new('sun', 'SUN')); sc.collection.objects.link(sun)
sun.data.energy = 3.5; sun.rotation_euler = (math.radians(40), 0, math.radians(30))
n = len(ids)
dist = 6.5 + n * 1.6
views = {
    'front34': (dist * 0.75, -dist * 0.75, 2.4),
    'rear34': (dist * 0.75, dist * 0.75, 2.4),
    'side': (dist, 0, 1.2),
    'front': (0.01, -dist, 1.2),
    'rear': (0.01, dist, 1.2),
    'top': (0.01, -0.01, dist * 1.3),
}
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam)
cam.location = Vector(views[view])
cam.data.lens = 50
d = Vector((0, 0, 0.7)) - cam.location
cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
sc.camera = cam
sc.render.engine = 'CYCLES'; sc.cycles.samples = int(os.environ.get('SAMPLES', 24)); sc.cycles.device = 'CPU'
sc.cycles.use_denoising = True
sc.render.resolution_x = int(os.environ.get('W', 900)); sc.render.resolution_y = int(os.environ.get('H', 500))
sc.render.filepath = out
sc.view_settings.view_transform = 'AgX'
bpy.ops.render.render(write_still=True)
print('PREVIEW_OK', out)
