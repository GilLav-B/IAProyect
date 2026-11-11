"""
Fase 6: DesignState (JSON) -> escena de Blender -> .blend y render (Cycles).

La IA NO construye el .blend: este script arma la escena solo con el DesignState validado y el catalogo.
Cada mueble queda como un objeto (Empty) con el MISMO instanceId que en el DesignState y en Three.js.

Uso (lo llama blender/render_design.ps1):
  blender -b --factory-startup -P blender/build_scene.py -- \
      --design design.json --catalog catalog.json --materials materials.json \
      --assets frontend/public/assets --colors frontend/src/lib/units.ts --out storage/blender/d1 \
      [--views se|so|ne|no|all] [--samples 96] [--width 1600] [--height 1000] [--no-render]

Unidades: el DesignState esta en cm con origen (0,0) en la esquina suroeste; aqui 1 unidad = 1 m,
X = este, Y = norte, Z = arriba. Rotacion en grados antihoraria; 0 = el frente mira al sur, 90 = al este.
"""

import argparse
import json
import math
import os
import re
import sys
import time

import bpy
from mathutils import Vector

# ----------------------------------------------------------------------------- argumentos

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser(description="DesignState -> Blender")
ap.add_argument("--design", required=True)
ap.add_argument("--catalog", required=True)
ap.add_argument("--materials", default=None)
ap.add_argument("--assets", required=True, help="carpeta frontend/public/assets")
ap.add_argument("--colors", required=True, help="frontend/src/lib/units.ts (tabla COLORS)")
ap.add_argument("--out", required=True)
ap.add_argument("--views", default="se", help="se, so, ne, no o all (esquinas a la altura de los ojos)")
ap.add_argument("--samples", type=int, default=96)
ap.add_argument("--width", type=int, default=1600)
ap.add_argument("--height", type=int, default=1000)
ap.add_argument("--no-render", action="store_true")
args = ap.parse_args(argv)

with open(args.design, encoding="utf-8") as f:
    state = json.load(f)
with open(args.catalog, encoding="utf-8") as f:
    products = {p["id"]: p for p in json.load(f)}
catalog_materials = {}
if args.materials and os.path.exists(args.materials):
    with open(args.materials, encoding="utf-8") as f:
        catalog_materials = {m["id"]: m for m in json.load(f)}

ASSETS = os.path.abspath(args.assets)
TEX = os.path.join(ASSETS, "textures")
OUT = os.path.abspath(args.out)
os.makedirs(OUT, exist_ok=True)

# Colores del catalogo: se leen de la misma tabla que usa el visor (una sola fuente)
with open(args.colors, encoding="utf-8") as f:
    units_ts = f.read()
COLORS = dict(re.findall(r'^\s*(\w+):\s*"(#[0-9a-fA-F]{6})"', units_ts.split("const COLORS")[1].split("};")[0], re.M))
DEFAULT_COLOR = "#9aa3ad"

cm = lambda v: v / 100.0  # noqa: E731

room = state["room"]
L, W, H = cm(room["length"]), cm(room["width"]), cm(room["height"])
WALL_T = 0.12          # grosor del muro hacia afuera (m)
DOOR_H_CM = 210        # [SUPUESTO] igual que el visor
WINDOW_H_CM = 120      # [SUPUESTO] alto de ventana si no viene
EYE_M = 1.55           # altura de la camara (ojos)
PAINTING_CENTER_CM = 150  # [SUPUESTO] igual que el visor


def hex_rgba(h):
    h = h.lstrip("#")
    srgb = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    lin = [c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in srgb]
    return (*lin, 1.0)


def color_of(name):
    return hex_rgba(COLORS.get(name or "", DEFAULT_COLOR))


# ----------------------------------------------------------------------------- escena vacia

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = "METRIC"


def link(obj, parent=None):
    scene.collection.objects.link(obj)
    if parent is not None:
        obj.parent = parent
    return obj


# ----------------------------------------------------------------------------- materiales

_mats = {}
_imgs = {}


def image(name):
    path = os.path.join(TEX, name)
    if not os.path.exists(path):
        return None
    if path not in _imgs:
        _imgs[path] = bpy.data.images.load(path)
    return _imgs[path]


def pbr_material(key, base=(0.8, 0.8, 0.8, 1), roughness=0.6, metallic=0.0, maps=None, scale=1.0, tint=None,
                 emission=None, emission_strength=0.0, transmission=0.0):
    """Principled BSDF. maps = prefijo de textura (diffuse/rough) con proyeccion de caja en coordenadas de objeto
    (no hace falta UV y la textura no se estira). scale = repeticiones por metro."""
    if key in _mats:
        return _mats[key]
    m = bpy.data.materials.new(key)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = base
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    if transmission:
        bsdf.inputs["Transmission Weight"].default_value = transmission
        bsdf.inputs["IOR"].default_value = 1.45
    if emission is not None:
        bsdf.inputs["Emission Color"].default_value = emission
        bsdf.inputs["Emission Strength"].default_value = emission_strength
    if maps:
        coord = nt.nodes.new("ShaderNodeTexCoord")
        mapping = nt.nodes.new("ShaderNodeMapping")
        mapping.inputs["Scale"].default_value = (scale, scale, scale)
        nt.links.new(coord.outputs["Object"], mapping.inputs["Vector"])

        def tex(suffix, non_color):
            img = image(f"{maps}_{suffix}.jpg")
            if img is None:
                return None
            n = nt.nodes.new("ShaderNodeTexImage")
            n.image = img
            n.projection = "BOX"
            n.projection_blend = 0.25
            if non_color:
                n.image.colorspace_settings.name = "Non-Color"
            nt.links.new(mapping.outputs["Vector"], n.inputs["Vector"])
            return n

        diffuse = tex("diffuse", False)
        if diffuse is not None:
            if tint is not None:
                mix = nt.nodes.new("ShaderNodeMix")
                mix.data_type = "RGBA"
                mix.blend_type = "MULTIPLY"
                mix.inputs["Factor"].default_value = 1.0
                nt.links.new(diffuse.outputs["Color"], mix.inputs[6])
                mix.inputs[7].default_value = tint
                nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
            else:
                nt.links.new(diffuse.outputs["Color"], bsdf.inputs["Base Color"])
        rough = tex("rough", True)
        if rough is not None:
            nt.links.new(rough.outputs["Color"], bsdf.inputs["Roughness"])
        # Relieve con Bump desde la textura de rugosidad. No se usa el normal map: con proyeccion de caja no hay
        # tangentes de UV y deja manchas negras en las caras.
        if rough is not None:
            bump = nt.nodes.new("ShaderNodeBump")
            bump.inputs["Strength"].default_value = 0.25
            bump.inputs["Distance"].default_value = 0.002
            nt.links.new(rough.outputs["Color"], bump.inputs["Height"])
            nt.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    _mats[key] = m
    return m


WOOD_TINT = {"madera_clara": "#f2dcc0", "madera_oscura": "#7a5a43"}


def fabric(color):
    return pbr_material(f"tela_{color}", base=color_of(color), roughness=1.0, maps="cotton_jersey", scale=5.0)


def wood(color=None):
    tint = hex_rgba(WOOD_TINT[color]) if color in WOOD_TINT else (color_of(color) if color else (1, 1, 1, 1))
    return pbr_material(f"madera_{color}", roughness=0.7, maps="oak_veneer_01", scale=2.0, tint=tint)


def paint(color):
    return pbr_material(f"pintura_{color}", base=color_of(color), roughness=0.5)


def metal(color="negro"):
    return pbr_material(f"metal_{color}", base=color_of(color), roughness=0.35, metallic=0.85)


def surface(color):
    return wood(color) if color in WOOD_TINT else paint(color)


def plain(key, rgba, roughness=0.5, metallic=0.0, **kw):
    return pbr_material(key, base=rgba, roughness=roughness, metallic=metallic, **kw)


# ----------------------------------------------------------------------------- primitivas
# Coordenadas locales de cada mueble (m): X = ancho, Y = fondo (el FRENTE mira a -Y = sur con rotacion 0), Z = alto.
# Los tamanos y posiciones replican frontend/src/components/scene/furniture/Procedural.tsx
# (alla el frente es +Z de three.js, que equivale a -Y aqui).


def box(parent, size, center, mat, bevel=0.0, rot_x=0.0, name="pieza"):
    bpy.ops.mesh.primitive_cube_add(size=1)
    o = bpy.context.active_object
    o.name = name
    o.scale = size
    o.location = center
    o.rotation_euler = (rot_x, 0, 0)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel > 0:
        b = o.modifiers.new("Redondeo", "BEVEL")
        b.width = min(bevel, min(size) / 2.01)
        b.segments = 4
        b.limit_method = "NONE"
        b.harden_normals = True  # sin esto el suavizado deja manchas negras en las caras planas
        bpy.ops.object.shade_smooth()
    o.data.materials.append(mat)
    o.parent = parent
    return o


def cylinder(parent, r, h, center, mat, r_top=None, verts=24, name="pieza"):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r, radius2=r if r_top is None else r_top, depth=h, location=center)
    o = bpy.context.active_object
    o.name = name
    bpy.ops.object.shade_smooth()
    o.data.materials.append(mat)
    o.parent = parent
    return o


def leg(parent, x, y, h, r=0.022):
    cylinder(parent, r, h, (x, y, h / 2), wood("madera_oscura"), r_top=r * 0.8, verts=12, name="pata")


def build_sofa(p, w, d, h, color, seats=None):
    leg_h = 0.08
    arm = min(0.16, w * 0.12)
    base_h = max(0.18, h * 0.28)
    seat_h = max(0.14, h * 0.17)
    back_t = min(0.2, d * 0.24)
    n = max(1, seats or round((w - 2 * arm) / 0.65))
    inner = w - 2 * arm
    cw = inner / n
    seat_d = d - back_t - 0.02
    mat = fabric(color)
    for sx in (-1, 1):
        for sy in (-1, 1):
            leg(p, sx * (w / 2 - 0.07), sy * (d / 2 - 0.07), leg_h)
    # Base y respaldo un poco mas chicos que los brazos: caras en el mismo plano dan manchas negras en Cycles
    box(p, (w - 0.012, d - 0.012, base_h), (0, 0, leg_h + base_h / 2), mat, 0.03, name="base")
    arm_h = h * 0.62 - leg_h
    for s in (-1, 1):
        box(p, (arm, d, arm_h), (s * (w / 2 - arm / 2), 0, leg_h + arm_h / 2), mat, 0.05, name="brazo")
    box(p, (w - 0.006, back_t - 0.006, h - leg_h - 0.004), (0, d / 2 - back_t / 2, leg_h + (h - leg_h) / 2), mat, 0.05, name="respaldo")
    back_cushion_h = (h - leg_h - base_h - seat_h) * 0.9
    for i in range(n):
        x = -inner / 2 + cw * (i + 0.5)
        box(p, (cw - 0.015, seat_d, seat_h), (x, d / 2 - back_t - seat_d / 2, leg_h + base_h + seat_h / 2), mat, 0.05, name="cojin")
        box(p, (cw - 0.03, 0.14, back_cushion_h), (x, d / 2 - back_t - 0.06, leg_h + base_h + seat_h + (h - leg_h - base_h - seat_h) * 0.45),
            mat, 0.06, rot_x=-0.12, name="cojin_respaldo")


def build_bed(p, w, d, h, color):
    frame_h, mattress_h, head = 0.3, 0.22, 0.08
    box(p, (w, d - head, frame_h), (0, -head / 2, frame_h / 2), wood("madera_clara"), 0.02, name="base")
    box(p, (w - 0.04, d - head - 0.04, mattress_h), (0, -head / 2, frame_h + mattress_h / 2), plain("colchon", hex_rgba("#f4f1ea"), 0.95), 0.05, name="colchon")
    box(p, (w, head, h), (0, d / 2 - head / 2, h / 2), fabric(color), 0.03, name="cabecera")
    for s in (-1, 1):
        box(p, (w / 2 - 0.12, 0.42, 0.13), (s * w / 4, d / 2 - head - 0.3, frame_h + mattress_h + 0.06), plain("almohada", hex_rgba("#fbfaf6"), 1.0), 0.06, rot_x=-0.25, name="almohada")
    box(p, (w + 0.02, (d - head) * 0.66, 0.06), (0, -d / 2 + (d - head) * 0.33, frame_h + mattress_h + 0.02), fabric(color), 0.03, name="edredon")


def build_tv(p, w, d, h, _color):
    t = max(0.03, d)
    box(p, (w, t, h), (0, 0, h / 2), plain("tv_marco", hex_rgba("#141416"), 0.35, 0.4), 0.008, name="tv")
    box(p, (w - 0.025, 0.002, h - 0.03), (0, -t / 2 - 0.001, h / 2 + 0.005), plain("tv_pantalla", hex_rgba("#05070b"), 0.08, 0.6), name="pantalla")


def build_arc_lamp(p, w, d, h, color):
    ox = -w * 0.2
    base_r = min(w, d) * 0.4
    cylinder(p, base_r, 0.05, (ox, 0, 0.025), plain("marmol_lampara", hex_rgba("#e9e6df"), 0.3), r_top=base_r * 0.95, verts=32, name="base")
    curve = bpy.data.curves.new("arco", "CURVE")
    curve.dimensions = "3D"
    curve.bevel_depth = 0.012
    curve.bevel_resolution = 4
    spline = curve.splines.new("NURBS")
    pts = [(0, 0, 0.05), (0, 0, h * 0.75), (w * 0.25, 0, h * 0.98), (w * 0.45, 0, h * 0.88)]
    spline.points.add(len(pts) - 1)
    for i, (x, y, z) in enumerate(pts):
        spline.points[i].co = (x + ox, y, z, 1)
    spline.use_endpoint_u = True
    spline.order_u = 3
    arc = bpy.data.objects.new("brazo", curve)
    arc.data.materials.append(metal(color))
    link(arc, p)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.12, location=(ox + w * 0.45, 0, h * 0.84))
    shade = bpy.context.active_object
    shade.name = "pantalla"
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="DESELECT")
    bpy.ops.object.mode_set(mode="OBJECT")
    for v in shade.data.vertices:
        v.select = v.co.z < -0.001
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.delete(type="VERT")
    bpy.ops.object.mode_set(mode="OBJECT")
    sol = shade.modifiers.new("Grosor", "SOLIDIFY")
    sol.thickness = 0.004
    bpy.ops.object.shade_smooth()
    shade.data.materials.append(pbr_material(f"pantalla_{color}", base=color_of(color), roughness=0.35, metallic=0.6))
    shade.parent = p
    lamp_light(p, (ox + w * 0.45, 0, h * 0.78), 40)


def build_paper_lamp(p, w, d, h, _color):
    r = min(w, d) / 2
    for i in range(4):
        a = i * math.pi / 2 + math.pi / 4
        box(p, (0.025, 0.025, h * 0.6), (math.cos(a) * r * 0.7, math.sin(a) * r * 0.7, h * 0.3), wood("madera_clara"), name="pata")
    cylinder(p, r, h * 0.55, (0, 0, h * 0.72), plain("papel_washi", hex_rgba("#f6efe1"), 1.0, emission=hex_rgba("#ffd9a0"), emission_strength=1.5,
                                                     transmission=0.6), r_top=r * 0.95, verts=32, name="pantalla")
    lamp_light(p, (0, 0, h * 0.72), 35)


def lamp_light(parent, loc, watts):
    data = bpy.data.lights.new("luz_lampara", "POINT")
    data.energy = watts
    data.color = (1.0, 0.85, 0.66)
    data.shadow_soft_size = 0.05
    o = bpy.data.objects.new("luz_lampara", data)
    o.location = loc
    link(o, parent)


def build_rug(p, w, d, _h, color):
    box(p, (w, d, 0.012), (0, 0, 0.006), fabric(color), 0.004, name="alfombra")


def build_island(p, w, d, h, color):
    top = 0.04
    box(p, (w - 0.06, d - 0.06, h - top), (0, 0, (h - top) / 2), surface(color), 0.01, name="gabinete")
    box(p, (w, d, top), (0, 0, h - top / 2), plain("cubierta_piedra", hex_rgba("#eeeae4"), 0.25), name="cubierta")


def build_painting(p, w, d, h, color):
    box(p, (w, d, h), (0, 0, h / 2), wood("madera_oscura"), name="marco")
    m = bpy.data.materials.new(f"lienzo_{color}")
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.9
    # Lienzo abstracto: un circulo del color elegido sobre fondo claro (igual idea que el visor)
    coord = nt.nodes.new("ShaderNodeTexCoord")
    grad = nt.nodes.new("ShaderNodeTexGradient")
    grad.gradient_type = "SPHERICAL"
    mapping = nt.nodes.new("ShaderNodeMapping")
    mapping.inputs["Location"].default_value = (-0.15, 0, -0.1)
    mapping.inputs["Scale"].default_value = (5, 5, 5)
    nt.links.new(coord.outputs["Object"], mapping.inputs["Vector"])
    nt.links.new(mapping.outputs["Vector"], grad.inputs["Vector"])
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = "CONSTANT"
    ramp.color_ramp.elements[0].color = hex_rgba("#f3efe7")
    ramp.color_ramp.elements[1].position = 0.02
    ramp.color_ramp.elements[1].color = color_of(color)
    nt.links.new(grad.outputs["Fac"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], bsdf.inputs["Base Color"])
    box(p, (w - 0.06, 0.002, h - 0.06), (0, -d / 2 - 0.001, h / 2), m, name="lienzo")


def build_table(p, w, d, h, color):
    top = 0.04
    mat = surface(color or "madera_clara")
    box(p, (w, d, top), (0, 0, h - top / 2), mat, 0.01, name="cubierta")
    for sx in (-1, 1):
        for sy in (-1, 1):
            box(p, (0.05, 0.05, h - top), (sx * (w / 2 - 0.08), sy * (d / 2 - 0.08), (h - top) / 2), mat, name="pata")


def build_dining_chair(p, w, d, h, color):
    seat = 0.46
    for sx in (-1, 1):
        for sy in (-1, 1):
            box(p, (0.03, 0.03, seat), (sx * (w / 2 - 0.03), sy * (d / 2 - 0.03), seat / 2), wood("madera_clara"), name="pata")
    box(p, (w, d, 0.06), (0, 0, seat), fabric(color), 0.02, name="asiento")
    box(p, (w, 0.05, h - seat), (0, d / 2 - 0.025, seat + (h - seat) / 2), fabric(color), 0.02, rot_x=-0.08, name="respaldo")


def build_office_chair(p, w, d, h, color):
    seat = 0.48
    r = min(w, d) * 0.42
    for i in range(5):
        a = i * 2 * math.pi / 5
        o = box(p, (r, 0.04, 0.03), (math.cos(a) * r / 2, math.sin(a) * r / 2, 0.06), metal("negro"), name="estrella")
        o.rotation_euler = (0, 0, a)
    cylinder(p, 0.025, seat - 0.06, (0, 0, (seat - 0.06) / 2 + 0.06), metal("gris"), verts=12, name="piston")
    box(p, (w * 0.85, d * 0.8, 0.08), (0, -0.02, seat), fabric(color), 0.03, name="asiento")
    box(p, (w * 0.8, 0.07, h - seat - 0.05), (0, d / 2 - 0.06, seat + (h - seat) / 2), fabric(color), 0.03, rot_x=-0.1, name="respaldo")
    for s in (-1, 1):
        box(p, (0.04, d * 0.5, 0.03), (s * w * 0.43, 0, seat + 0.15), metal("negro"), name="brazo")


def build_tv_cabinet(p, w, d, h, color):
    leg_h = min(0.12, h * 0.2)
    body = h - leg_h
    for sx in (-1, 1):
        for sy in (-1, 1):
            cylinder(p, 0.012, leg_h, (sx * (w / 2 - 0.06), sy * (d / 2 - 0.05), leg_h / 2), metal("negro"), verts=10, name="pata")
    box(p, (w, d, body), (0, 0, leg_h + body / 2), surface(color), 0.01, name="cuerpo")
    doors = max(2, round(w / 0.55))
    dw = (w - 0.04) / doors
    for i in range(doors):  # ranuras entre puertas
        if i > 0:
            box(p, (0.004, 0.004, body - 0.04), (-w / 2 + 0.02 + dw * i, -d / 2 - 0.001, leg_h + body / 2), plain("ranura", hex_rgba("#1a1a1a"), 0.8), name="ranura")


def build_bookshelf(p, w, d, h, color):
    t = 0.025
    mat = surface(color)
    shelves = max(2, round(h / 0.35))
    for s in (-1, 1):
        box(p, (t, d, h), (s * (w / 2 - t / 2), 0, h / 2), mat, name="costado")
    box(p, (w, 0.01, h), (0, d / 2 - 0.005, h / 2), mat, name="fondo")
    for i in range(shelves + 1):
        box(p, (w - 2 * t, d, t), (0, 0, min(h - t / 2, t / 2 + i * (h - t) / shelves)), mat, name="repisa")


def build_generic(p, w, d, h, color, material):
    mat = fabric(color) if material == "tela" else metal(color) if material == "metal" else surface(color)
    box(p, (w, d, h), (0, 0, h / 2), mat, min(0.03, w / 4, d / 4, h / 4), name="cuerpo")


def build_procedural(p, product, w, d, h, color, material):
    cat = product["category"]
    if cat == "sofa":
        seats = (product.get("attributes") or {}).get("plazas")
        build_sofa(p, w, d, h, color, int(seats) if seats else None)
    elif cat == "sillon":
        build_sofa(p, w, d, h, color, 1)
    elif cat == "cama":
        build_bed(p, w, d, h, color)
    elif cat == "televisor":
        build_tv(p, w, d, h, color)
    elif cat == "lampara":
        (build_paper_lamp if "tela" in product.get("materials", []) else build_arc_lamp)(p, w, d, h, color)
    elif cat == "alfombra":
        build_rug(p, w, d, h, color)
    elif cat == "isla":
        build_island(p, w, d, h, color)
    elif cat == "cuadro":
        build_painting(p, w, d, h, color)
    elif cat in ("mesa_comedor", "mesa_centro", "escritorio"):
        build_table(p, w, d, h, color)
    elif cat == "silla_comedor":
        build_dining_chair(p, w, d, h, color)
    elif cat == "silla_oficina":
        build_office_chair(p, w, d, h, color)
    elif cat == "mueble_tv":
        build_tv_cabinet(p, w, d, h, color)
    elif cat == "librero":
        build_bookshelf(p, w, d, h, color)
    else:
        build_generic(p, w, d, h, color, material)


# ----------------------------------------------------------------------------- modelos GLB


def world_bbox(objs):
    pts = [o.matrix_world @ Vector(c) for o in objs if o.type == "MESH" for c in o.bound_box]
    mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return mn, mx


def tint_materials(objs, rgba):
    for o in objs:
        if o.type != "MESH":
            continue
        for slot in o.material_slots:
            if not slot.material or not slot.material.use_nodes:
                continue
            m = slot.material.copy()
            slot.material = m
            nt = m.node_tree
            bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
            if bsdf is None:
                continue
            base = bsdf.inputs["Base Color"]
            if base.is_linked:
                src = base.links[0].from_socket
                mix = nt.nodes.new("ShaderNodeMix")
                mix.data_type = "RGBA"
                mix.blend_type = "MULTIPLY"
                mix.inputs["Factor"].default_value = 1.0
                nt.links.new(src, mix.inputs[6])
                mix.inputs[7].default_value = rgba
                nt.links.new(mix.outputs[2], base)
            else:
                base.default_value = tuple(a * b for a, b in zip(base.default_value, rgba))


def build_glb(p, path, w, d, h, tint):
    """Igual que ModelAsset.tsx: escala por eje para ocupar EXACTO ancho x fondo x alto, apoyado en el piso y centrado.
    Si el lado largo del modelo esta en el otro eje, se gira 90 grados antes de escalar."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    fit = bpy.data.objects.new("ajuste", None)
    link(fit, p)
    for o in new:
        if o.parent is None:
            o.parent = fit
    bpy.context.view_layer.update()
    mn, mx = world_bbox(new)
    raw = mx - mn
    swap = (w >= d) != (raw.x >= raw.y)
    if swap:
        fit.rotation_euler = (0, 0, math.pi / 2)
        fit.scale = (d / raw.x, w / raw.y, h / raw.z)
    else:
        fit.scale = (w / raw.x, d / raw.y, h / raw.z)
    bpy.context.view_layer.update()
    mn, mx = world_bbox(new)
    center = (mn + mx) / 2
    pw = p.matrix_world.translation
    fit.location = (pw.x - center.x, pw.y - center.y, pw.z - mn.z)
    if tint is not None:
        tint_materials(new, tint)


# ----------------------------------------------------------------------------- cuarto


def wall_frame(wall):
    """Origen, direccion a lo largo de la pared y normal hacia AFUERA."""
    return {
        "sur": (Vector((0, 0, 0)), Vector((1, 0, 0)), Vector((0, -1, 0)), L),
        "norte": (Vector((0, W, 0)), Vector((1, 0, 0)), Vector((0, 1, 0)), L),
        "oeste": (Vector((0, 0, 0)), Vector((0, 1, 0)), Vector((-1, 0, 0)), W),
        "este": (Vector((L, 0, 0)), Vector((0, 1, 0)), Vector((1, 0, 0)), W),
    }[wall]


def piece(parent, wall, a, b, z0, z1, mat, thick=WALL_T, inset=0.0, name="muro"):
    """Caja de la pared entre a..b (a lo largo) y z0..z1 (alto); inset > 0 la mete hacia el cuarto."""
    o, along, out, _ = wall_frame(wall)
    mid = o + along * ((a + b) / 2) + out * (thick / 2 - inset)
    sx = (b - a) if along.x else thick
    sy = thick if along.x else (b - a)
    return box(parent, (sx, sy, z1 - z0), (mid.x, mid.y, (z0 + z1) / 2), mat, name=name)


surfaces = room.get("surfaces") or {}
wall_color = (surfaces.get("walls") or {}).get("color")
wall_mat = pbr_material(f"muro_{wall_color}", base=hex_rgba(COLORS.get(wall_color, "#f1ede6")), roughness=0.9, maps="white_plaster_02", scale=0.6)
floor_info = surfaces.get("floor") or {}
floor_type = (catalog_materials.get(floor_info.get("materialId") or "") or {}).get("type")
FLOORS = {"madera": ("laminate_floor_02", 1.6, 0.8), "ceramica": ("floor_tiles_06", 1.2, 0.45),
          "marmol": ("marble_01", 2.4, 0.25), "concreto": ("brushed_concrete_2", 2.4, 0.7)}
floor_tex, floor_tile, floor_rough = FLOORS.get(floor_type, FLOORS["madera"])
floor_tint = hex_rgba(COLORS[floor_info["color"]]) if floor_info.get("color") in COLORS else None
floor_mat = pbr_material(f"piso_{floor_tex}_{floor_info.get('color')}", roughness=floor_rough, maps=floor_tex, scale=1 / floor_tile, tint=floor_tint)

shell = bpy.data.objects.new("habitacion", None)
link(shell)

# Piso y techo (un poco mas grandes para que queden bajo los muros)
box(shell, (L + 2 * WALL_T, W + 2 * WALL_T, 0.02), (L / 2, W / 2, -0.01), floor_mat, name="piso")
ceiling_mat = plain("techo", hex_rgba("#f7f5f0"), 0.95)
box(shell, (L + 2 * WALL_T, W + 2 * WALL_T, 0.05), (L / 2, W / 2, H + 0.025), ceiling_mat, name="techo")

# Huecos por pared: (desde, hasta, abajo, arriba) en m
openings = {w_: [] for w_ in ("sur", "norte", "oeste", "este")}
for dr in room.get("doors", []):
    openings[dr["wall"]].append((cm(dr["position"]), cm(dr["position"] + dr["width"]), 0.0, cm(min(DOOR_H_CM, room["height"]))))
for wn in room.get("windows", []):
    sill = cm(wn["sillHeight"])
    wh = cm(min(wn.get("height") or WINDOW_H_CM, room["height"] - wn["sillHeight"] - 10))
    openings[wn["wall"]].append((cm(wn["position"]), cm(wn["position"] + wn["width"]), sill, sill + wh))

baseboard_mat = plain("zoclo", hex_rgba("#fbfaf7"), 0.6)
for wall, holes in openings.items():
    _, _, _, length = wall_frame(wall)
    cuts = sorted({0.0, length, *[c for h_ in holes for c in (h_[0], h_[1])]})
    for a, b in zip(cuts, cuts[1:]):
        if b - a < 1e-4:
            continue
        hole = next((h_ for h_ in holes if h_[0] <= a + 1e-6 and h_[1] >= b - 1e-6), None)
        if hole is None:
            piece(shell, wall, a, b, 0, H, wall_mat)
            piece(shell, wall, a, b, 0, 0.08, baseboard_mat, thick=0.012, inset=0.012, name="zoclo")
        else:
            if hole[2] > 0:
                piece(shell, wall, a, b, 0, hole[2], wall_mat)
                piece(shell, wall, a, b, 0, 0.08, baseboard_mat, thick=0.012, inset=0.012, name="zoclo")
            if hole[3] < H:
                piece(shell, wall, a, b, hole[3], H, wall_mat)

# Puertas: marco y hoja de madera cerrada
door_wood = wood("madera_clara")
trim = plain("marco_blanco", hex_rgba("#efece6"), 0.5)
for dr in room.get("doors", []):
    wall = dr["wall"]
    a, b = cm(dr["position"]), cm(dr["position"] + dr["width"])
    dh = cm(min(DOOR_H_CM, room["height"]))
    piece(shell, wall, a + 0.01, b - 0.01, 0, dh - 0.01, door_wood, thick=0.04, inset=-0.04, name="puerta")
    piece(shell, wall, a - 0.05, a, 0, dh + 0.05, trim, thick=0.02, inset=0.02, name="marco")
    piece(shell, wall, b, b + 0.05, 0, dh + 0.05, trim, thick=0.02, inset=0.02, name="marco")
    piece(shell, wall, a - 0.049, b + 0.049, dh, dh + 0.05, trim, thick=0.021, inset=0.021, name="marco")
    o, along, out, _ = wall_frame(wall)
    knob = o + along * (b - 0.1) - out * 0.03
    cylinder(shell, 0.025, 0.02, (knob.x, knob.y, dh * 0.48), metal("gris"), verts=16, name="manija")

# Ventanas: marco, parteluz y vidrio (sin refraccion para que el sol entre)
glass = bpy.data.materials.new("vidrio")
glass.use_nodes = True
gnt = glass.node_tree
gnt.nodes.remove(gnt.nodes["Principled BSDF"])
lp = gnt.nodes.new("ShaderNodeLightPath")
transparent = gnt.nodes.new("ShaderNodeBsdfTransparent")
gl = gnt.nodes.new("ShaderNodeBsdfGlass")
gl.inputs["Roughness"].default_value = 0.0
gl.inputs["IOR"].default_value = 1.05
mix = gnt.nodes.new("ShaderNodeMixShader")
gnt.links.new(lp.outputs["Is Shadow Ray"], mix.inputs["Fac"])
gnt.links.new(gl.outputs["BSDF"], mix.inputs[1])
gnt.links.new(transparent.outputs["BSDF"], mix.inputs[2])
gnt.links.new(mix.outputs["Shader"], gnt.nodes["Material Output"].inputs["Surface"])
frame_mat = plain("marco_ventana", hex_rgba("#2b2b2d"), 0.4, 0.3)
for wn in room.get("windows", []):
    wall = wn["wall"]
    a, b = cm(wn["position"]), cm(wn["position"] + wn["width"])
    z0 = cm(wn["sillHeight"])
    z1 = z0 + cm(min(wn.get("height") or WINDOW_H_CM, room["height"] - wn["sillHeight"] - 10))
    f = 0.04
    for (pa, pb, pz0, pz1) in [(a, b, z0, z0 + f), (a, b, z1 - f, z1), (a, a + f, z0, z1), (b - f, b, z0, z1), ((a + b) / 2 - f / 2, (a + b) / 2 + f / 2, z0, z1)]:
        piece(shell, wall, pa, pb, pz0, pz1, frame_mat, thick=0.06, inset=-0.03, name="marco_ventana")
    piece(shell, wall, a + f, b - f, z0 + f, z1 - f, glass, thick=0.006, inset=-0.06, name="vidrio")
    piece(shell, wall, a - 0.02, b + 0.02, z0 - 0.03, z0, plain("repisa_ventana", hex_rgba("#f2efe9"), 0.4), thick=0.18, inset=0.06, name="repisa")
    # Portal de luz: ayuda a Cycles a muestrear el cielo que entra por la ventana (menos ruido)
    o, along, out, _ = wall_frame(wall)
    c = o + along * ((a + b) / 2) + out * 0.005
    ld = bpy.data.lights.new("portal_ventana", "AREA")
    ld.shape = "RECTANGLE"
    ld.size, ld.size_y = b - a, z1 - z0
    ld.cycles.is_portal = True
    lo = bpy.data.objects.new("portal_ventana", ld)
    lo.location = (c.x, c.y, (z0 + z1) / 2)
    # El area apunta a -Z local: se gira para que mire hacia dentro del cuarto
    lo.rotation_euler = out.to_track_quat("Z", "Y").to_euler()
    link(lo, shell)

for col in room.get("columns", []):
    box(shell, (cm(col["width"]), cm(col["depth"]), H), (cm(col["x"] + col["width"] / 2), cm(col["y"] + col["depth"] / 2), H / 2), wall_mat, name=f"columna_{col['id']}")

# ----------------------------------------------------------------------------- muebles


def rotated_dims(rot, w, d):
    r = rot % 360
    if r % 90 == 0:
        return (d, w) if r in (90, 270) else (w, d)
    t = math.radians(r)
    c, s = abs(math.cos(t)), abs(math.sin(t))
    return w * c + d * s, w * s + d * c


def elevations(objects):
    """Igual que el visor: la TV va encima de su mueble y el cuadro colgado. Solo visual, no se valida."""
    def rect(o, p):
        a, b = rotated_dims(o["rotation"], p["dimensions"]["width"], p["dimensions"]["depth"])
        return o["position"]["x"], o["position"]["y"], o["position"]["x"] + a, o["position"]["y"] + b

    result = {}
    for o in objects:
        p = products.get(o["catalogId"])
        if not p or o["position"].get("z", 0) > 0 or p["category"] not in ("televisor", "cuadro"):
            continue
        x1, y1, x2, y2 = rect(o, p)
        top = 0
        for other in objects:
            q = products.get(other["catalogId"])
            if not q or other is o or q["category"] in ("televisor", "cuadro", "alfombra"):
                continue
            a1, b1, a2, b2 = rect(other, q)
            if x1 < a2 and x2 > a1 and y1 < b2 and y2 > b1:
                top = max(top, other["position"].get("z", 0) + q["dimensions"]["height"])
        if p["category"] == "cuadro":
            top = max(top + 10, PAINTING_CENTER_CM - p["dimensions"]["height"] / 2)
        result[o["instanceId"]] = top
    return result


furniture = bpy.data.objects.new("muebles", None)
link(furniture)
lift = elevations(state["objects"])
for o in state["objects"]:
    p = products.get(o["catalogId"])
    if p is None:
        print(f"[aviso] {o['instanceId']}: producto {o['catalogId']} no esta en el catalogo, se omite")
        continue
    dims = p["dimensions"]
    w, d, h = cm(dims["width"]), cm(dims["depth"]), cm(dims["height"])
    a, b = rotated_dims(o["rotation"], dims["width"], dims["depth"])
    cx, cy = cm(o["position"]["x"] + a / 2), cm(o["position"]["y"] + b / 2)
    z = cm(o["position"].get("z", 0) + lift.get(o["instanceId"], 0))
    # Empty con el MISMO instanceId del DesignState (principio 7)
    root = bpy.data.objects.new(o["instanceId"], None)
    root["catalogId"] = o["catalogId"]
    root["instanceId"] = o["instanceId"]
    link(root, furniture)
    root.location = (cx, cy, z)
    bpy.context.view_layer.update()
    config = o.get("config") or {}
    color = config.get("color") or p["colors"][0]
    material = config.get("material") or p["materials"][0]
    asset = p.get("asset3D")
    glb = os.path.join(ASSETS, asset.split("/assets/", 1)[1].replace("/", os.sep)) if asset and "/assets/" in asset else None
    if glb and os.path.exists(glb):
        tint = color_of(config["color"]) if config.get("color") and config["color"] != p["colors"][0] else None
        build_glb(root, glb, w, d, h, tint)
    else:
        build_procedural(root, p, w, d, h, color, material)
    root.rotation_euler = (0, 0, math.radians(o["rotation"]))

# ----------------------------------------------------------------------------- luz

world = bpy.data.worlds.new("cielo")
scene.world = world
world.use_nodes = True
sky = world.node_tree.nodes.new("ShaderNodeTexSky")
sky.sky_type = "NISHITA"
sky.sun_elevation = math.radians(35)
sky.sun_intensity = 0.35
win = room["windows"][0] if room.get("windows") else None
sun_dir_out = wall_frame(win["wall"])[2] if win else Vector((0, 1, 0))
# El sol viene desde afuera de la primera ventana (igual que el visor)
sky.sun_rotation = math.atan2(sun_dir_out.x, sun_dir_out.y)
bg = world.node_tree.nodes["Background"]
bg.inputs["Strength"].default_value = 0.6
world.node_tree.links.new(sky.outputs["Color"], bg.inputs["Color"])

sun_data = bpy.data.lights.new("sol", "SUN")
sun_data.energy = 3.5
sun_data.angle = math.radians(2.5)
sun_data.color = (1.0, 0.95, 0.88)
sun = bpy.data.objects.new("sol", sun_data)
link(sun)
# Direccion: desde afuera de la ventana, bajando 35 grados hacia el cuarto
direction = (-sun_dir_out * math.cos(math.radians(35)) + Vector((0, 0, -math.sin(math.radians(35))))).normalized()
sun.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()

# Relleno suave del techo (como la luz rebotada de una casa real)
fill = bpy.data.lights.new("relleno_techo", "AREA")
fill.shape = "RECTANGLE"
fill.size, fill.size_y = L * 0.7, W * 0.7
fill.energy = 10 * L * W  # W de luz suave; con mas se quema la imagen
fill.color = (1.0, 0.96, 0.9)
fill_o = bpy.data.objects.new("relleno_techo", fill)
fill_o.location = (L / 2, W / 2, H - 0.03)
link(fill_o)

# ----------------------------------------------------------------------------- camara y render

VIEWS = {"so": (0.35, 0.35), "se": (L - 0.35, 0.35), "ne": (L - 0.35, W - 0.35), "no": (0.35, W - 0.35)}
names = list(VIEWS) if args.views == "all" else [v.strip() for v in args.views.split(",")]

cam_data = bpy.data.cameras.new("camara")
cam_data.lens_unit = "FOV"
cam_data.angle = math.radians(75)  # gran angular de interiores (horizontal)
cam_data.clip_start = 0.05
cam = bpy.data.objects.new("camara", cam_data)
link(cam)
scene.camera = cam

scene.render.engine = "CYCLES"
scene.cycles.device = "CPU"
scene.cycles.samples = args.samples
scene.cycles.use_denoising = True
scene.cycles.denoiser = "OPENIMAGEDENOISE"
scene.cycles.max_bounces = 8
scene.cycles.caustics_reflective = False
scene.cycles.caustics_refractive = False
scene.render.resolution_x = args.width
scene.render.resolution_y = args.height
scene.render.image_settings.file_format = "PNG"
scene.view_settings.view_transform = "AgX"
scene.view_settings.look = "AgX - Medium High Contrast"
scene.view_settings.exposure = 0.0

design_id = state["design"]["id"]


def aim(view):
    x, y = VIEWS[view]
    cam.location = (x, y, EYE_M)
    target = Vector((L / 2 + (L / 2 - x) * 0.25, W / 2 + (W / 2 - y) * 0.25, 0.75))
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()


aim(names[0])
blend_path = os.path.join(OUT, f"{design_id}.blend")
bpy.ops.wm.save_as_mainfile(filepath=blend_path, compress=True)
print(f"[ok] .blend guardado: {blend_path}")

renders = []
if not args.no_render:
    for v in names:
        aim(v)
        scene.render.filepath = os.path.join(OUT, f"{design_id}_{v}.png")
        t0 = time.time()
        bpy.ops.render.render(write_still=True)
        renders.append({"view": v, "file": scene.render.filepath, "seconds": round(time.time() - t0, 1)})
        print(f"[ok] render {v}: {scene.render.filepath} ({renders[-1]['seconds']} s)")

with open(os.path.join(OUT, "resultado.json"), "w", encoding="utf-8") as f:
    json.dump({"designId": design_id, "blend": blend_path, "renders": renders,
               "objects": [o["instanceId"] for o in state["objects"]]}, f, ensure_ascii=False, indent=2)
