"""Armado de prompts para la IA (antes vivia en C#: AiContext.cs).

La IA solo recibe datos del diseno y del catalogo; nunca claves ni datos personales.
Todo lo que proponga regresa a la API, donde el motor de validacion decide que se aplica.
"""

import json
import math
from pathlib import Path

STYLES = ["moderno", "minimalista", "industrial", "japandi", "clasico"]

# Reglas de /ai-rules que se agregan al prompt de sistema segun el tipo de trabajo
RULES_BY_KIND = {
    "referencia": ["spatial-analysis", "interior-designer", "furniture-selector", "budget-manager"],
    "chat": ["interior-designer", "furniture-selector", "budget-manager"],
    "diseno": ["spatial-analysis", "interior-designer", "furniture-selector", "budget-manager"],
}


def rule(rules_dir: Path, name: str) -> str:
    return (rules_dir / f"{name}.md").read_text(encoding="utf-8")


def system_prompt(rules_dir: Path, kind: str) -> str:
    names = ["global", "safety-rules", *RULES_BY_KIND[kind]]
    return "\n\n".join(rule(rules_dir, n) for n in names) + "\n"


def proposal_schema() -> dict:
    """JSON Schema estricto de la respuesta (Structured Outputs de OpenAI). Igual al formato de interior-designer.md."""
    nullable = lambda t: {"type": [t, "null"]}  # noqa: E731
    return {
        "type": "object",
        "additionalProperties": False,
        "required": ["summary", "style", "proposals"],
        "properties": {
            "summary": {"type": "string"},
            "style": {"type": ["string", "null"], "enum": [*STYLES, None]},
            "proposals": {
                "type": "array",
                "items": {
                    "type": "object",
                    "additionalProperties": False,
                    "required": ["action", "instanceId", "catalogId", "x", "y", "rotation", "reason"],
                    "properties": {
                        "action": {"type": "string", "enum": ["agregar", "mover", "reemplazar", "quitar"]},
                        "instanceId": nullable("string"),
                        "catalogId": nullable("string"),
                        "x": nullable("integer"),
                        "y": nullable("integer"),
                        "rotation": nullable("integer"),
                        "reason": {"type": "string"},
                    },
                },
            },
        },
    }


def _context_json(context: dict) -> str:
    return json.dumps(context, ensure_ascii=False)


def user_text(kind: str, context: dict, message: str | None = None, note: str | None = None, style: str | None = None) -> str:
    note_line = f"Nota del usuario: {note}" if note and note.strip() else ""
    if kind == "referencia":
        return (
            "Copia lo mejor posible el diseno de la imagen de referencia en el cuarto del usuario, respetando sus medidas reales,\n"
            "sus requisitos y su presupuesto. Puedes quitar o reemplazar los muebles actuales que no esten bloqueados.\n"
            f"{note_line}\n\nDatos del cuarto y catalogo (JSON):\n{_context_json(context)}"
        )
    if kind == "chat":
        return (
            f"Instruccion del usuario: {message}\n\n"
            f"Propone solo los cambios necesarios para cumplirla. Datos del cuarto y catalogo (JSON):\n{_context_json(context)}"
        )
    if kind == "diseno":
        target = style if style in STYLES else (context.get("design") or {}).get("style")
        return (
            f"Disena DESDE CERO este cuarto con estilo {target}. Ya se quitaron los muebles que no estaban bloqueados ni son\n"
            "obligatorios; los que quedan (en 'objects') se respetan y no se pueden mover.\n"
            "Propone SOLO acciones 'agregar' con productos del catalogo (solo de allowedCategories), cada una con x, y (cm, esquina\n"
            "suroeste de la caja envolvente) y rotation (0 = el frente mira al sur, 90 = al este). Arma un espacio completo y\n"
            "habitable: los muebles basicos del tipo de espacio, mas la decoracion e iluminacion que haya en el catalogo (alfombra,\n"
            "plantas, cuadros, lamparas) para que se vea terminado. Asientos mirando a la TV o al centro, circulacion libre de al\n"
            "menos 70 cm, nada frente a puertas ni ventanas, y el total dentro del presupuesto (budget.cap) si existe.\n"
            f"{note_line}\n\nDatos del cuarto y catalogo (JSON):\n{_context_json(context)}"
        )
    raise ValueError(f"Tipo de trabajo desconocido: {kind}")


# ----------------------------------------------------------------------------- imagenes


def _rotated(rotation: int, w: float, d: float) -> tuple[float, float]:
    r = rotation % 360
    if r % 90 == 0:
        return (d, w) if r in (90, 270) else (w, d)
    t = math.radians(r)
    c, s = abs(math.cos(t)), abs(math.sin(t))
    return w * c + d * s, w * s + d * c


def describe(obj: dict, product: dict) -> str:
    cfg = obj.get("config") or {}
    dims = product["dimensions"]
    return (
        f"{product['name']} ({product['category'].replace('_', ' ')}), color {cfg.get('color') or product['colors'][0]}, "
        f"material {cfg.get('material') or product['materials'][0]}, {dims['width']}x{dims['depth']}x{dims['height']} cm"
    )


def _room_line(state: dict) -> str:
    room = state["room"]
    return (f"Espacio: {room['spaceType']} de {room['length'] / 100:.2f} x {room['width'] / 100:.2f} m, "
            f"techo de {room['height'] / 100:.2f} m. Estilo: {state['design']['style']}.")


def _surfaces_line(state: dict) -> str | None:
    surfaces = state["room"].get("surfaces")
    return f"Acabados: {json.dumps(surfaces, ensure_ascii=False)}." if surfaces else None


RENDER_RULES = """
MODO RENDER: la imagen adjunta es una captura de un modelo 3D SIMPLIFICADO del cuarto. Genera una fotografia
profesional de interiores, hiperrealista, como de revista de arquitectura.

QUE SE RESPETA AL 100% (el acomodo):
- Mismo punto de vista y perspectiva, misma forma del cuarto, puertas y ventanas en el mismo lugar.
- Cada mueble en la MISMA posicion, con el mismo tamano, la misma orientacion y el mismo color principal.
- La misma cantidad de muebles: PROHIBIDO agregar cosas que no esten (plantas, cuadros, cojines, mantas, cortinas,
  jarrones, libros, lamparas o muebles extra) y prohibido quitar, mover o girar alguno. Lo vacio queda vacio.

QUE NO SE COPIA DE LA CAPTURA (el aspecto del 3D):
- En la captura algunos muebles pueden verse como cajas o bloques lisos. NO los dibujes como cajas: dibuja cada uno como el
  producto real que se describe abajo (por ejemplo un sofa Chesterfield con capitonado y brazos enrollados, un sillon con
  patas y cojines, una mesa con patas o base y cubierta, un librero con repisas), ocupando el mismo lugar y tamano.
- Ignora el aspecto plastico del 3D: sombras negras duras, techo oscuro, colores planos, bordes perfectos.
- Materiales reales con detalle: tela con tejido y arrugas suaves, piel con pliegues, madera con veta, metal con
  reflejos, vidrio. Techo blanco liso. Muros y piso con su textura real.
- Luz natural suave que entra por las ventanas, rebote de luz en muros y techo, sombras suaves de contacto bajo los
  muebles, exposicion balanceada. Lente gran angular de camara profesional, nitido, colores naturales.
- Sin personas, sin texto, sin marcas de agua.
"""


def render_prompt(rules_dir: Path, state: dict, products: dict[str, dict], note: str | None, visible: list[dict] | None) -> str:
    """Convertir una captura del visor 3D en foto realista sin mover nada.
    visible = muebles que el visor calculo que salen en cuadro y donde; si es None se manda la lista completa."""
    lines = [rule(rules_dir, "image-generator"), RENDER_RULES, _room_line(state)]
    if (s := _surfaces_line(state)) is not None:
        lines.append(s)
    by_id = {o["instanceId"]: o for o in state["objects"]}
    if visible is not None:
        shown = [(by_id[v["instanceId"]], products.get(by_id[v["instanceId"]]["catalogId"]), v["where"])
                 for v in visible if v.get("instanceId") in by_id]
        shown = [x for x in shown if x[1] is not None]
        if not shown:
            lines.append("En la captura NO se ve ningun mueble: dibuja solo el cuarto vacio (muros, piso, techo, puertas y ventanas).")
        else:
            lines.append(f"En la captura se ven EXACTAMENTE {len(shown)} muebles y ninguno mas. Que es cada uno y donde esta en el cuadro:")
            lines += [f"- {describe(o, p)}: {where}." for o, p, where in shown]
    else:
        lines.append("Muebles del diseno (sirven para saber que es cada cosa; algunos pueden quedar FUERA del encuadre: "
                     "dibuja SOLO los que se ven en la captura, ni uno mas):")
        lines += [f"- {describe(o, products[o['catalogId']])}." for o in state["objects"] if o["catalogId"] in products]
    if note and note.strip():
        lines.append(f"Nota del usuario (solo ambiente: luz y hora del dia; no agrega ni cambia objetos): {note}")
    return "\n".join(lines) + "\n"


def free_image_prompt(rules_dir: Path, state: dict, products: dict[str, dict], note: str | None) -> str:
    """Imagen libre de "como quedaria", solo con la descripcion del diseno (sin captura)."""
    room = state["room"]

    def near_wall(o: dict, p: dict) -> str:
        a, b = _rotated(o["rotation"], p["dimensions"]["width"], p["dimensions"]["depth"])
        cx, cy = o["position"]["x"] + a / 2, o["position"]["y"] + b / 2
        options = [("pared sur", cy), ("pared norte", room["width"] - cy), ("pared oeste", cx), ("pared este", room["length"] - cx)]
        name, dist = min(options, key=lambda x: x[1])
        return f"junto a la {name}" if dist < min(room["length"], room["width"]) * 0.25 else "hacia el centro"

    lines = [rule(rules_dir, "image-generator"), _room_line(state)]
    if (s := _surfaces_line(state)) is not None:
        lines.append(s)
    lines += [f"Ventana de {w['width'] / 100:.2f} m en la pared {w['wall']}." for w in room.get("windows", [])]
    lines += [f"Puerta en la pared {d['wall']}." for d in room.get("doors", [])]
    lines.append("Muebles (todos, ninguno mas):")
    for o in state["objects"]:
        p = products.get(o["catalogId"])
        if p:
            lines.append(f"- {describe(o, p)}, {near_wall(o, p)}.")
    if note and note.strip():
        lines.append(f"Nota del usuario: {note}")
    return "\n".join(lines) + "\n"
