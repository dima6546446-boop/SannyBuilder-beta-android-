"""
Габариты салона для игры (без Blender): берёт формы кузова из shapes.py и пишет
src/config/interiorAnchors.json — по ним Interior.js строит торпедо, кресла и обшивку
точно внутри кузова каждой модели.
    python3 tools/blender/export_interior.py
"""
import json, os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from shapes import SHAPES

DIMS = json.load(open(os.path.join(HERE, 'cars_dims.json')))
KEYS = ['W', 'tumble', 'zW0', 'zW1', 'zB1', 'zB0', 'belt', 'top', 'floor', 'sill', 'side', 'pillars']
out = {}
for cid, d in DIMS.items():
    S = dict(SHAPES.get(cid) or SHAPES.get(d.get('base', d['id'])) or SHAPES['vaz2107'])
    S['W'] = d['dims']['W']
    out[cid] = {k: S[k] for k in KEYS if k in S}
dst = os.path.join(HERE, '..', '..', 'src', 'config', 'interiorAnchors.json')
json.dump(out, open(dst, 'w'), indent=1)
print('wrote', os.path.normpath(dst), len(out))
