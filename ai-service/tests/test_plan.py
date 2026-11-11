import math

from app.exports import footprint


def obj(x, y, rot, w=200, d=100):
    return {"x": x, "y": y, "rotation": rot, "widthCm": w, "depthCm": d}


def test_huella_sin_giro_es_la_caja_desde_la_esquina_suroeste():
    pts = footprint(obj(10, 20, 0))
    xs, ys = [p[0] for p in pts], [p[1] for p in pts]
    assert (min(xs), max(xs), min(ys), max(ys)) == (10, 210, 20, 120)


def test_frente_mira_al_sur_con_0_y_al_este_con_90():
    # El frente es el lado entre las dos primeras esquinas
    front = footprint(obj(0, 0, 0))[:2]
    assert all(abs(p[1] - 0) < 1e-9 for p in front)  # lado sur
    front = footprint(obj(0, 0, 90))[:2]
    assert all(abs(p[0] - 100) < 1e-9 for p in front)  # lado este (la caja girada mide 100 x 200)


def test_angulo_libre_cabe_en_su_caja_envolvente():
    pts = footprint(obj(0, 0, 30))
    a = 200 * math.cos(math.radians(30)) + 100 * math.sin(math.radians(30))
    xs = [p[0] for p in pts]
    assert abs(min(xs)) < 1e-9 and abs(max(xs) - a) < 1e-9
