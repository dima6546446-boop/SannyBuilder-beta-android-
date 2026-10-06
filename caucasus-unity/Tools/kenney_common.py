"""Общие функции импорта Kenney Car Kit (CC0, kenney.nl): классификация граней по палитре colormap.png."""
import bpy, colorsys
import numpy as np

PAL = None
def load_palette(path):
    """Палитра 512×512: сетка 8×8 ячеек по 64 px, внутри — градиент. Читаем «как есть» (Non-Color)."""
    global PAL
    img = bpy.data.images.load(path)
    img.colorspace_settings.name = 'Non-Color'
    w, h = img.size
    PAL = np.array(img.pixels[:]).reshape(h, w, 4)[:, :, :3]       # строка 0 — нижняя

def cell_of(u, v):
    cx = min(7, max(0, int(u * 8))); cy = min(7, max(0, int(v * 8)))      # v — снизу вверх (как в Blender)
    return cx, cy

def cell_rgb(cell):
    cx, cy = cell
    blk = PAL[cy * 64 + 8: cy * 64 + 56, cx * 64 + 8: cx * 64 + 56]
    return tuple(float(x) for x in blk.reshape(-1, 3).mean(axis=0))

def classify(rgb):
    r, g, b = rgb
    h, s, v = colorsys.rgb_to_hsv(r, g, b)
    if v < 0.03: return 'under'
    if s < 0.2 and v < 0.55: return 'black'
    if s < 0.1 and v > 0.85: return 'white'
    if 0.5 < h < 0.72 and v > 0.8 and s < 0.35: return 'glass'
    if s < 0.35: return 'gray'
    return 'color'
