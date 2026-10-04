"""
Лица моделей: фары, решётки, воздухозаборники, фонари, молдинги — по прототипам.
Всё строится облегающими деталями (decals.py) по реальной поверхности кузова.
x — от оси к левому борту (для правого зеркалится), y — высота, z — вперёд.
"""


def _shift(pts, dy):
    return [(x, y + dy) for x, y in pts]


def lamp(Dc, view, s, x0, x1, bot, top, body='headLamp', frame='black', m=0.012, nu=8, lift=0.0, thick=0.014):
    """Фара/фонарь: корпус (рамка) + стекло с отступом m."""
    Dc.region(view, x0, x1, _shift(bot, -m * 0.6), _shift(top, m * 0.6), frame, nu=nu, nv=2, thick=thick * 0.7, lift=lift, mirror=s)
    Dc.region(view, x0 + m, x1 - m, bot, top, body, nu=nu, nv=2, thick=thick, lift=lift, mirror=s)


# ======================================================================== перед
def front_priora(Dc, P, d):
    for s in (1, -1):
        bot, top = [(0.30, 0.655), (0.52, 0.64), (0.80, 0.71)], [(0.30, 0.76), (0.80, 0.79)]
        lamp(Dc, 'front', s, 0.29, 0.81, bot, top)
        for x in (0.42, 0.58):
            Dc.ring('front', x, 0.705, 0.042, 0.012, 'chrome', thick=0.022, mirror=s)
            Dc.disc('front', x, 0.705, 0.034, 'headLamp', thick=0.02, mirror=s)
        Dc.region('front', 0.70, 0.79, [(0.70, 0.705), (0.79, 0.725)], [(0.70, 0.765), (0.79, 0.78)],
                  'indL' if s > 0 else 'indR', nu=2, nv=1, thick=0.02, mirror=s)
        # противотуманки в нижнем заборнике
        Dc.region('front', 0.48, 0.70, [(0.48, 0.37), (0.70, 0.40)], [(0.48, 0.48), (0.70, 0.49)], 'black', nu=4, nv=1, thick=0.01, mirror=s)
        Dc.disc('front', 0.60, 0.435, 0.04, 'headLamp', thick=0.02, mirror=s)
    # решётка: хромовая трапеция с перемычкой и значком
    Dc.region('front', -0.27, 0.27, [(-0.27, 0.645), (-0.2, 0.635), (0.2, 0.635), (0.27, 0.645)], [(-0.27, 0.75), (0.27, 0.75)], 'chrome', nu=6, nv=2, thick=0.012)
    Dc.region('front', -0.245, 0.245, [(-0.245, 0.655), (-0.2, 0.648), (0.2, 0.648), (0.245, 0.655)], [(-0.245, 0.738), (0.245, 0.738)], 'grille', nu=6, nv=2, thick=0.016)
    Dc.region('front', -0.25, 0.25, [(-0.3, 0.69)], [(-0.3, 0.705)], 'chrome', nu=6, nv=1, thick=0.026)
    Dc.disc('front', 0, 0.697, 0.05, 'chrome', thick=0.032, ry=0.034)
    # нижний воздухозаборник
    Dc.region('front', -0.40, 0.40, [(-0.4, 0.37), (0.4, 0.37)], [(-0.4, 0.47), (-0.3, 0.48), (0.3, 0.48), (0.4, 0.47)], 'grille', nu=6, nv=1, thick=0.01)


def front_granta(Dc, P, d):
    for s in (1, -1):
        bot, top = [(0.31, 0.70), (0.55, 0.665), (0.81, 0.745)], [(0.31, 0.80), (0.81, 0.835)]
        lamp(Dc, 'front', s, 0.30, 0.82, bot, top)
        Dc.ring('front', 0.45, 0.745, 0.05, 0.012, 'chrome', thick=0.022, mirror=s)
        Dc.disc('front', 0.45, 0.745, 0.04, 'headLamp', thick=0.02, mirror=s)
        Dc.region('front', 0.56, 0.78, [(0.56, 0.705), (0.78, 0.755)], [(0.56, 0.715), (0.78, 0.765)], 'headLamp', nu=3, nv=1, thick=0.022, mirror=s)
        Dc.region('front', 0.73, 0.80, [(0.73, 0.77), (0.80, 0.78)], [(0.73, 0.815), (0.80, 0.825)], 'indL' if s > 0 else 'indR', nu=2, nv=1, thick=0.02, mirror=s)
        Dc.region('front', 0.50, 0.72, [(0.50, 0.40), (0.72, 0.43)], [(0.50, 0.50), (0.72, 0.51)], 'black', nu=4, nv=1, thick=0.01, mirror=s)
        Dc.disc('front', 0.61, 0.455, 0.042, 'headLamp', thick=0.02, mirror=s)
    # верхняя щель и хромовая «бровь» со значком
    Dc.region('front', -0.28, 0.28, [(-0.28, 0.745), (0.28, 0.745)], [(-0.28, 0.79), (0.28, 0.79)], 'grille', nu=6, nv=1, thick=0.01)
    Dc.poly_strip('front', [(-0.33, 0.745), (-0.12, 0.718), (0.12, 0.718), (0.33, 0.745)], 0.028, 'chrome', thick=0.022)
    Dc.disc('front', 0, 0.725, 0.055, 'chrome', thick=0.03, ry=0.036)
    # большой трапециевидный заборник
    Dc.region('front', -0.43, 0.43, [(-0.43, 0.61), (-0.30, 0.40), (0.30, 0.40), (0.43, 0.61)], [(-0.43, 0.63), (0.43, 0.63)], 'chrome', nu=8, nv=2, thick=0.01)
    Dc.region('front', -0.40, 0.40, [(-0.40, 0.605), (-0.29, 0.42), (0.29, 0.42), (0.40, 0.605)], [(-0.40, 0.612), (0.40, 0.612)], 'grille', nu=8, nv=2, thick=0.014)


def front_vesta(Dc, P, d):
    for s in (1, -1):
        bot, top = [(0.36, 0.745), (0.52, 0.765), (0.84, 0.80)], [(0.36, 0.795), (0.60, 0.835), (0.84, 0.86)]
        lamp(Dc, 'front', s, 0.35, 0.85, bot, top)
        for x in (0.52, 0.66):
            Dc.ring('front', x, 0.80, 0.03, 0.01, 'chrome', thick=0.022, mirror=s)
        Dc.poly_strip('front', [(0.40, 0.762), (0.60, 0.785), (0.82, 0.81)], 0.012, 'headLamp', thick=0.024, mirror=s)
        Dc.region('front', 0.76, 0.84, [(0.76, 0.82), (0.84, 0.83)], [(0.76, 0.845), (0.84, 0.855)], 'indL' if s > 0 else 'indR', nu=2, nv=1, thick=0.022, mirror=s)
        # X: верхняя и нижняя «ветви» хрома
        Dc.poly_strip('front', [(0.37, 0.75), (0.17, 0.585), (0.30, 0.47), (0.56, 0.37)], 0.05, 'chrome', thick=0.024, mirror=s)
        # противотуманки в нишах снаружи от нижних ветвей
        Dc.region('front', 0.50, 0.78, [(0.50, 0.38), (0.78, 0.42)], [(0.50, 0.44), (0.78, 0.52)], 'black', nu=4, nv=1, thick=0.008, mirror=s)
        Dc.disc('front', 0.66, 0.44, 0.038, 'headLamp', thick=0.018, mirror=s)
    # верхняя решётка между ветвями и значок
    Dc.region('front', -0.34, 0.34, [(-0.34, 0.745), (-0.16, 0.60), (0.16, 0.60), (0.34, 0.745)], [(-0.34, 0.77), (0.34, 0.77)], 'grille', nu=8, nv=2, thick=0.012)
    Dc.disc('front', 0, 0.69, 0.062, 'chrome', thick=0.03, ry=0.042)
    # нижний заборник
    Dc.region('front', -0.48, 0.48, [(-0.48, 0.355), (0.48, 0.355)], [(-0.48, 0.38), (-0.17, 0.55), (0.17, 0.55), (0.48, 0.38)], 'grille', nu=8, nv=2, thick=0.012)


def front_largus(Dc, P, d):
    for s in (1, -1):
        bot, top = [(0.34, 0.735), (0.60, 0.72), (0.82, 0.80)], [(0.34, 0.875), (0.82, 0.905)]
        lamp(Dc, 'front', s, 0.33, 0.83, bot, top)
        Dc.ring('front', 0.47, 0.80, 0.055, 0.012, 'chrome', thick=0.022, mirror=s)
        Dc.disc('front', 0.47, 0.80, 0.045, 'headLamp', thick=0.02, mirror=s)
        Dc.ring('front', 0.64, 0.80, 0.045, 0.012, 'chrome', thick=0.022, mirror=s)
        Dc.region('front', 0.74, 0.81, [(0.74, 0.80), (0.81, 0.82)], [(0.74, 0.86), (0.81, 0.88)], 'indL' if s > 0 else 'indR', nu=2, nv=1, thick=0.02, mirror=s)
        Dc.region('front', 0.52, 0.74, [(0.52, 0.42), (0.74, 0.44)], [(0.52, 0.52), (0.74, 0.53)], 'black', nu=4, nv=1, thick=0.01, mirror=s)
        Dc.disc('front', 0.63, 0.475, 0.042, 'headLamp', thick=0.02, mirror=s)
    # хромированная решётка с двумя планками
    Dc.region('front', -0.31, 0.31, [(-0.31, 0.71), (-0.27, 0.70), (0.27, 0.70), (0.31, 0.71)], [(-0.31, 0.875), (0.31, 0.875)], 'chrome', nu=6, nv=2, thick=0.012)
    Dc.region('front', -0.285, 0.285, [(-0.285, 0.718), (0.285, 0.718)], [(-0.285, 0.858), (0.285, 0.858)], 'grille', nu=6, nv=2, thick=0.016)
    for y in (0.765, 0.815):
        Dc.region('front', -0.28, 0.28, [(-0.3, y)], [(-0.3, y + 0.014)], 'chrome', nu=6, nv=1, thick=0.024)
    Dc.disc('front', 0, 0.79, 0.055, 'chrome', thick=0.032, ry=0.04)
    Dc.region('front', -0.46, 0.46, [(-0.46, 0.40), (0.46, 0.40)], [(-0.46, 0.52), (0.46, 0.52)], 'grille', nu=6, nv=1, thick=0.01)


def front_niva(Dc, P, d):
    # решётка во всю ширину передка: круглые фары по краям, горизонтальные планки между ними
    Dc.region('front', -0.80, 0.80, [(-0.8, 0.735), (0.8, 0.735)], [(-0.8, 0.985), (0.8, 0.985)], 'grille', nu=10, nv=2, thick=0.012)
    for k in range(5):
        y = 0.775 + k * 0.045
        Dc.region('front', -0.44, 0.44, [(-0.5, y)], [(-0.5, y + 0.016)], 'black', nu=6, nv=1, thick=0.026)
    for s in (1, -1):
        Dc.ring('front', 0.615, 0.86, 0.105, 0.03, 'chrome', thick=0.03, mirror=s)
        Dc.disc('front', 0.615, 0.86, 0.092, 'headLamp', seg=16, thick=0.03, mirror=s)
        Dc.disc('front', 0.615, 0.86, 0.02, 'chrome', seg=8, thick=0.04, mirror=s)
        # подфарник + поворотник под фарой
        Dc.region('front', 0.52, 0.72, [(0.52, 0.665), (0.72, 0.665)], [(0.52, 0.715), (0.72, 0.715)], 'indL' if s > 0 else 'indR', nu=3, nv=1, thick=0.02, mirror=s)


# ======================================================================== зад
def _tail(Dc, s, x0, x1, bot, top, split, nu=8):
    """Фонарь из секций: split — [(x_до, материал)] от внутреннего края наружу."""
    Dc.region('rear', x0 - 0.01, x1 + 0.01, _shift(bot, -0.01), _shift(top, 0.01), 'black', nu=nu, nv=2, thick=0.01, mirror=s)
    xa = x0
    for xb, mat in split:
        m = mat if mat != 'ind' else ('indL' if s > 0 else 'indR')
        Dc.region('rear', xa + 0.004, xb - 0.004, bot, top, m, nu=max(2, nu // len(split)), nv=2, thick=0.016, mirror=s)
        xa = xb


def rear_priora(Dc, P, d):
    for s in (1, -1):
        _tail(Dc, s, 0.33, 0.77, [(0.33, 0.83), (0.77, 0.80)], [(0.33, 0.92), (0.77, 0.905)],
              [(0.46, 'reverseLamp'), (0.57, 'ind'), (0.77, 'tailLamp')])
    Dc.region('rear', -0.30, 0.30, [(-0.3, 0.885)], [(-0.3, 0.9)], 'chrome', nu=6, nv=1, thick=0.012)
    Dc.region('rear', -0.55, 0.55, [(-0.55, 0.32)], [(-0.55, 0.37)], 'black', nu=6, nv=1, thick=0.01)


def rear_granta(Dc, P, d):
    for s in (1, -1):
        _tail(Dc, s, 0.42, 0.79, [(0.42, 0.87), (0.79, 0.82)], [(0.42, 0.985), (0.79, 0.97)],
              [(0.52, 'reverseLamp'), (0.62, 'ind'), (0.79, 'tailLamp')])
    Dc.region('rear', -0.42, 0.42, [(-0.42, 0.33)], [(-0.42, 0.38)], 'black', nu=6, nv=1, thick=0.01)


def rear_vesta(Dc, P, d):
    for s in (1, -1):
        _tail(Dc, s, 0.40, 0.82, [(0.40, 0.905), (0.82, 0.87)], [(0.40, 0.975), (0.82, 0.965)],
              [(0.54, 'reverseLamp'), (0.64, 'ind'), (0.82, 'tailLamp')])
        Dc.poly_strip('rear', [(0.42, 0.95), (0.77, 0.945), (0.80, 0.90)], 0.012, 'tailLamp', thick=0.022, mirror=s)
        Dc.region('rear', 0.50, 0.78, [(0.50, 0.33), (0.78, 0.35)], [(0.50, 0.36), (0.78, 0.38)], 'tailLamp', nu=2, nv=1, thick=0.012, mirror=s)
    Dc.region('rear', -0.36, 0.36, [(-0.36, 0.935)], [(-0.36, 0.95)], 'chrome', nu=6, nv=1, thick=0.012)
    Dc.region('rear', -0.60, 0.60, [(-0.6, 0.30)], [(-0.6, 0.33)], 'black', nu=6, nv=1, thick=0.01)


def rear_largus(Dc, P, d):
    for s in (1, -1):
        Dc.region('rear', 0.67, 0.835, [(0.67, 0.72)], [(0.67, 1.15)], 'black', nu=2, nv=4, thick=0.01, mirror=s)
        Dc.region('rear', 0.68, 0.825, [(0.695, 0.95)], [(0.695, 1.13)], 'tailLamp', nu=2, nv=3, thick=0.018, mirror=s)
        Dc.region('rear', 0.68, 0.825, [(0.695, 0.86)], [(0.695, 0.945)], 'indL' if s > 0 else 'indR', nu=2, nv=1, thick=0.018, mirror=s)
        Dc.region('rear', 0.68, 0.825, [(0.695, 0.74)], [(0.695, 0.855)], 'reverseLamp', nu=2, nv=1, thick=0.018, mirror=s)
    # распашные двери: шов посередине и ручка
    Dc.region('rear', -0.004, 0.004, [(0, 0.55)], [(0, 1.15)], 'under', nu=1, nv=4, thick=0.004)
    Dc.region('rear', -0.60, 0.60, [(-0.6, 0.33)], [(-0.6, 0.39)], 'black', nu=6, nv=1, thick=0.01)


def rear_vaz2109(Dc, P, d):
    # широкие фонари, между ними — чёрная панель с номером
    for s in (1, -1):
        _tail(Dc, s, 0.40, 0.80, [(0.40, 0.72), (0.80, 0.72)], [(0.40, 0.855), (0.80, 0.855)],
              [(0.50, 'reverseLamp'), (0.60, 'ind'), (0.80, 'tailLamp')])
        for k in range(3):
            y = 0.75 + k * 0.04
            Dc.region('rear', 0.62, 0.78, [(0.6, y)], [(0.6, y + 0.006)], 'black', nu=2, nv=1, thick=0.022, mirror=s)
    Dc.region('rear', -0.40, 0.40, [(-0.4, 0.72)], [(-0.4, 0.855)], 'black', nu=6, nv=1, thick=0.008)


def rear_niva(Dc, P, d):
    for s in (1, -1):
        Dc.region('rear', 0.66, 0.80, [(0.66, 0.78)], [(0.66, 1.06)], 'black', nu=2, nv=3, thick=0.01, mirror=s)
        Dc.region('rear', 0.675, 0.785, [(0.675, 0.94)], [(0.675, 1.05)], 'tailLamp', nu=2, nv=1, thick=0.018, mirror=s)
        Dc.region('rear', 0.675, 0.785, [(0.675, 0.865)], [(0.675, 0.935)], 'indL' if s > 0 else 'indR', nu=2, nv=1, thick=0.018, mirror=s)
        Dc.region('rear', 0.675, 0.785, [(0.675, 0.79)], [(0.675, 0.86)], 'reverseLamp' if s < 0 else 'tailLamp', nu=2, nv=1, thick=0.018, mirror=s)


FRONT = {'priora': front_priora, 'granta': front_granta, 'vesta': front_vesta, 'largus': front_largus, 'niva': front_niva}
REAR = {'priora': rear_priora, 'granta': rear_granta, 'vesta': rear_vesta, 'largus': rear_largus,
        'vaz2109': rear_vaz2109, 'niva': rear_niva}


# ======================================================================== бока
SIDE = {
    # (y низ, y верх, материал, z от, z до) — молдинги
    'vaz2109': dict(mold=(0.50, 0.57, 'black'), lip=(0.025, 0.008, 'paint')),
    'oka': dict(mold=(0.46, 0.52, 'black'), lip=(0.02, 0.008, 'paint')),
    'niva': dict(lip=(0.065, 0.022, 'paint'), lipAng=(-2, 182)),
    'largus': dict(mold=(0.54, 0.60, 'black'), lip=(0.035, 0.012, 'black')),
    'priora': dict(mold=(0.52, 0.565, 'paint'), lip=(0.02, 0.008, 'paint')),
    'granta': dict(lip=(0.022, 0.008, 'paint')),
    'vesta': dict(lip=(0.025, 0.01, 'paint')),
    'vaz2101': dict(lip=(0.018, 0.008, 'paint')),
    'vaz2106': dict(lip=(0.018, 0.008, 'paint'), vent=True),
    'vaz2107': dict(lip=(0.018, 0.008, 'paint')),
}


def sides(Dc, P, S, kind, lod):
    cfg = SIDE.get(kind, {})
    D = S['dims']['dims']
    for s in (1, -1):
        if cfg.get('lip') and lod != 'lod1':
            w, t, mat = cfg['lip']
            a0, a1 = cfg.get('lipAng', (-6, 186))
            for ax in (D['axleF'], D['axleR']):
                Dc.arch_lip(s, ax, D['archR'], S['archY'], w, mat, t, a0, a1)
        if cfg.get('mold') and lod == 'hi':
            y0, y1, mat = cfg['mold']
            ra, rb = D['axleF'] - D['archR'] - 0.03, D['axleR'] + D['archR'] + 0.03
            Dc.region(s, rb, ra, [(rb, y0), (ra, y0)], [(rb, y1), (ra, y1)], mat, nu=10, nv=1, thick=0.01)
        if cfg.get('vent') and lod != 'lod1':
            # решётки вентиляции на C-стойке (ВАЗ-2106)
            z0, z1 = S['side'][1] - 0.035, S['side'][1] - 0.165
            yb = 0.0
            from carbuilder import interp
            yb = interp(S['belt'], (z0 + z1) / 2) + 0.03
            Dc.region(s, z1, z0, [(z1, yb), (z0, yb)], [(z1, yb + 0.085), (z0, yb + 0.11)], 'black', nu=2, nv=1, thick=0.008)
            if lod == 'hi':
                for k in range(5):
                    yy = yb + 0.012 + k * 0.017
                    Dc.region(s, z1 + 0.01, z0 - 0.01, [(z1, yy), (z0, yy + 0.004)], [(z1, yy + 0.006), (z0, yy + 0.01)], 'chrome', nu=1, nv=1, thick=0.014)
