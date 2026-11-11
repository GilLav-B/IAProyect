"""Fase 7: exportaciones (reporte PDF, Excel, CSV y plano 2D en SVG/PDF).

Los datos (listas, presupuesto, validacion) los calcula la API con el motor; aqui solo se dibujan.
Unidades del plano: cm con origen (0,0) en la esquina suroeste, x = este, y = norte.
"""

import csv
import io
import math
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path
from xml.sax.saxutils import escape

from openpyxl import Workbook
from openpyxl.styles import Alignment, Font, PatternFill
from openpyxl.utils import get_column_letter
from reportlab.graphics.shapes import Drawing, Line, Polygon, PolyLine, Rect, String
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4, landscape
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import cm
from reportlab.platypus import Image, KeepTogether, PageBreak, Paragraph, SimpleDocTemplate, Spacer, Table, TableStyle

AI_CAPTION = "Imagen de referencia generada por IA. No representa medidas ni productos exactos."
WALL_CM = 12  # grosor del muro en el plano

CATEGORY_FILL = {
    "sofa": "#d9c6a5", "sillon": "#d9c6a5", "cama": "#cfd8e3", "mesa_centro": "#b08a63", "mesa_comedor": "#b08a63",
    "escritorio": "#b08a63", "mueble_tv": "#8c6a4f", "televisor": "#2f2f33", "alfombra": "#ece3d3", "lampara": "#f2d48a",
    "planta": "#9cc59a", "cuadro": "#c9a27b", "librero": "#a07a58",
}


def money(v: float | int | None) -> str:
    return "-" if v is None else f"${v:,.0f}"


def money_range(lo, hi) -> str:
    return money(lo) if lo == hi else f"{money(lo)} - {money(hi)}"


def human(term: str) -> str:
    return term.replace("_", " ")


# ----------------------------------------------------------------------------- plano: geometria comun


@dataclass
class PlanShapes:
    """Primitivas del plano en cm (y hacia el norte). Se dibujan igual en SVG y en PDF."""
    width: float
    height: float
    polygons: list[tuple[list[tuple[float, float]], str, str, float]] = field(default_factory=list)  # puntos, relleno, linea, grosor
    lines: list[tuple[tuple[float, float], tuple[float, float], str, float]] = field(default_factory=list)
    polylines: list[tuple[list[tuple[float, float]], str, float]] = field(default_factory=list)
    texts: list[tuple[float, float, str, float, str]] = field(default_factory=list)  # x, y, texto, tamano(cm), ancla


def footprint(obj: dict) -> list[tuple[float, float]]:
    """Rectangulo girado del mueble (antihorario; rotacion 0 = el frente mira al sur)."""
    w, d, r = obj["widthCm"], obj["depthCm"], math.radians(obj["rotation"] % 360)
    a = abs(w * math.cos(r)) + abs(d * math.sin(r))
    b = abs(w * math.sin(r)) + abs(d * math.cos(r))
    cx, cy = obj["x"] + a / 2, obj["y"] + b / 2
    pts = [(-w / 2, -d / 2), (w / 2, -d / 2), (w / 2, d / 2), (-w / 2, d / 2)]
    return [(cx + x * math.cos(r) - y * math.sin(r), cy + x * math.sin(r) + y * math.cos(r)) for x, y in pts]


def plan_shapes(room: dict, objects: list[dict]) -> PlanShapes:
    L, W = room["length"], room["width"]
    s = PlanShapes(L, W)
    # Piso y muros
    s.polygons.append(([(0, 0), (L, 0), (L, W), (0, W)], "#faf8f4", "#faf8f4", 0))
    t = WALL_CM
    s.polygons += [
        ([(-t, -t), (L + t, -t), (L + t, 0), (-t, 0)], "#3d3a35", "#3d3a35", 0),
        ([(-t, W), (L + t, W), (L + t, W + t), (-t, W + t)], "#3d3a35", "#3d3a35", 0),
        ([(-t, 0), (0, 0), (0, W), (-t, W)], "#3d3a35", "#3d3a35", 0),
        ([(L, 0), (L + t, 0), (L + t, W), (L, W)], "#3d3a35", "#3d3a35", 0),
    ]

    def along(wall: str, pos: float) -> tuple[float, float]:
        return {"sur": (pos, 0), "norte": (pos, W), "oeste": (0, pos), "este": (L, pos)}[wall]

    def inward(wall: str) -> tuple[float, float]:
        return {"sur": (0, 1), "norte": (0, -1), "oeste": (1, 0), "este": (-1, 0)}[wall]

    def wall_gap(wall: str, a: float, b: float) -> list[tuple[float, float]]:
        (x1, y1), (x2, y2) = along(wall, a), along(wall, b)
        nx, ny = inward(wall)
        o = lambda x, y, k: (x - nx * k, y - ny * k)  # noqa: E731  hacia afuera k cm
        return [o(x1, y1, -1), o(x2, y2, -1), o(x2, y2, t + 1), o(x1, y1, t + 1)]

    for dr in room.get("doors", []):
        a, b = dr["position"], dr["position"] + dr["width"]
        s.polygons.append((wall_gap(dr["wall"], a, b), "#faf8f4", "#faf8f4", 0))
        # Hoja abierta 90 grados hacia dentro y su arco de giro
        hx, hy = along(dr["wall"], a)
        nx, ny = inward(dr["wall"])
        r = dr["width"]
        s.lines.append(((hx, hy), (hx + nx * r, hy + ny * r), "#3d3a35", 3))
        (ex, ey) = along(dr["wall"], b)
        ux, uy = (ex - hx) / r, (ey - hy) / r
        arc = [(hx + r * (ux * math.cos(k) + nx * math.sin(k)), hy + r * (uy * math.cos(k) + ny * math.sin(k)))
               for k in [i * (math.pi / 2) / 16 for i in range(17)]]
        s.polylines.append((arc, "#8a857c", 1.5))
    for wn in room.get("windows", []):
        a, b = wn["position"], wn["position"] + wn["width"]
        s.polygons.append((wall_gap(wn["wall"], a, b), "#dfeefa", "#5b7fa3", 1.5))
        (x1, y1), (x2, y2) = along(wn["wall"], a), along(wn["wall"], b)
        nx, ny = inward(wn["wall"])
        for k in (t * 0.35, t * 0.65):
            s.lines.append(((x1 - nx * k, y1 - ny * k), (x2 - nx * k, y2 - ny * k), "#5b7fa3", 1.5))
    for col in room.get("columns", []):
        x, y, cw, cd = col["x"], col["y"], col["width"], col["depth"]
        s.polygons.append(([(x, y), (x + cw, y), (x + cw, y + cd), (x, y + cd)], "#3d3a35", "#3d3a35", 0))

    # Muebles: alfombras primero (quedan abajo)
    for obj in sorted(objects, key=lambda o: 0 if o["category"] == "alfombra" else 1):
        pts = footprint(obj)
        fill = CATEGORY_FILL.get(obj["category"], "#d6d2ca")
        s.polygons.append((pts, fill, "#5a554d", 1.5))
        # Linea del frente (lado entre las dos primeras esquinas)
        s.lines.append((pts[0], pts[1], "#c0392b", 2.5))
        # La TV y el cuadro van sobre otro mueble o en el muro (sin area de piso): sin etiqueta para no encimarse
        if obj["category"] in ("televisor", "cuadro"):
            continue
        cx = sum(p[0] for p in pts) / 4
        cy = sum(p[1] for p in pts) / 4
        size = max(7.0, min(13.0, min(obj["widthCm"], obj["depthCm"]) * 0.22))
        if obj["category"] == "alfombra":
            # La alfombra suele quedar bajo la mesa: su nombre va arriba, dentro de la alfombra
            cy = max(p[1] for p in pts) - size * 1.4
            size = min(size, 9.0)
        s.texts.append((cx, cy, obj["name"], size, "middle"))

    # Cotas generales
    s.texts.append((L / 2, -t - 22, f"{L / 100:.2f} m", 14, "middle"))
    s.texts.append((-t - 22, W / 2, f"{W / 100:.2f} m", 14, "middle"))
    s.texts.append((L / 2, W + t + 12, "NORTE", 11, "middle"))
    return s


def plan_svg(room: dict, objects: list[dict], title: str = "") -> str:
    s = plan_shapes(room, objects)
    pad = 70
    vw, vh = s.width + 2 * pad, s.height + 2 * pad + (30 if title else 0)
    X = lambda x: x + pad  # noqa: E731
    Y = lambda y: s.height + pad - y + (30 if title else 0)  # noqa: E731  (SVG crece hacia abajo)
    out = [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {vw:.0f} {vh:.0f}" width="{vw * 1.2:.0f}" '
           f'height="{vh * 1.2:.0f}" font-family="Helvetica, Arial, sans-serif">',
           f'<rect x="0" y="0" width="{vw:.0f}" height="{vh:.0f}" fill="#ffffff"/>']
    if title:
        out.append(f'<text x="{pad}" y="28" font-size="18" fill="#222">{escape(title)}</text>')
    for pts, fill, stroke, sw in s.polygons:
        p = " ".join(f"{X(x):.1f},{Y(y):.1f}" for x, y in pts)
        out.append(f'<polygon points="{p}" fill="{fill}" stroke="{stroke}" stroke-width="{sw}"/>')
    for (x1, y1), (x2, y2), color, sw in s.lines:
        out.append(f'<line x1="{X(x1):.1f}" y1="{Y(y1):.1f}" x2="{X(x2):.1f}" y2="{Y(y2):.1f}" stroke="{color}" stroke-width="{sw}"/>')
    for pts, color, sw in s.polylines:
        p = " ".join(f"{X(x):.1f},{Y(y):.1f}" for x, y in pts)
        out.append(f'<polyline points="{p}" fill="none" stroke="{color}" stroke-width="{sw}" stroke-dasharray="4 3"/>')
    for x, y, text, size, anchor in s.texts:
        out.append(f'<text x="{X(x):.1f}" y="{Y(y) + size / 3:.1f}" font-size="{size:.1f}" text-anchor="{anchor}" fill="#222">{escape(text)}</text>')
    out.append("</svg>")
    return "\n".join(out)


def plan_drawing(room: dict, objects: list[dict], max_w: float, max_h: float) -> Drawing:
    """El mismo plano como dibujo vectorial de ReportLab (para el PDF)."""
    s = plan_shapes(room, objects)
    pad = 70
    scale = min(max_w / (s.width + 2 * pad), max_h / (s.height + 2 * pad))
    d = Drawing((s.width + 2 * pad) * scale, (s.height + 2 * pad) * scale)
    P = lambda x, y: ((x + pad) * scale, (y + pad) * scale)  # noqa: E731
    flat = lambda pts: [c for p in pts for c in P(*p)]  # noqa: E731
    for pts, fill, stroke, sw in s.polygons:
        d.add(Polygon(flat(pts), fillColor=colors.HexColor(fill), strokeColor=colors.HexColor(stroke), strokeWidth=sw * scale))
    for a, b, color, sw in s.lines:
        d.add(Line(*P(*a), *P(*b), strokeColor=colors.HexColor(color), strokeWidth=max(0.4, sw * scale)))
    for pts, color, sw in s.polylines:
        d.add(PolyLine(flat(pts), strokeColor=colors.HexColor(color), strokeWidth=max(0.3, sw * scale), strokeDashArray=[2, 2]))
    for x, y, text, size, _ in s.texts:
        px, py = P(x, y)
        d.add(String(px, py - size * scale / 3, text, fontSize=max(4, size * scale), textAnchor="middle", fillColor=colors.HexColor("#222222")))
    return d


def plan_pdf(room: dict, objects: list[dict], title: str) -> bytes:
    buf = io.BytesIO()
    page = landscape(A4)
    doc = SimpleDocTemplate(buf, pagesize=page, leftMargin=1.5 * cm, rightMargin=1.5 * cm, topMargin=1.2 * cm, bottomMargin=1.2 * cm,
                            title=title)
    styles = getSampleStyleSheet()
    # Alto disponible = hoja - margenes - titulo y nota (si no cabe, ReportLab lo manda a otra hoja)
    drawing = plan_drawing(room, objects, page[0] - 3 * cm, page[1] - 6.5 * cm)
    doc.build([Paragraph(escape(title), styles["Title"]),
               Paragraph("Plano de planta. Cotas en metros; la linea roja marca el frente de cada mueble.", styles["Normal"]),
               Spacer(1, 0.3 * cm), drawing])
    return buf.getvalue()


# ----------------------------------------------------------------------------- reporte PDF


def _table(rows: list[list], widths: list[float], header_bg: str = "#e9e4da") -> Table:
    tbl = Table(rows, colWidths=widths, repeatRows=1)
    tbl.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor(header_bg)),
        ("FONTNAME", (0, 0), (-1, 0), "Helvetica-Bold"),
        ("FONTSIZE", (0, 0), (-1, -1), 8.5),
        ("GRID", (0, 0), (-1, -1), 0.3, colors.HexColor("#cfc8bb")),
        ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
        ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.white, colors.HexColor("#faf8f4")]),
    ]))
    return tbl


def report_pdf(data: dict, storage_dir: Path) -> bytes:
    buf = io.BytesIO()
    design, project, room = data["design"], data["project"], data["room"]
    title = f"{project['name']} - {design['name']}"
    doc = SimpleDocTemplate(buf, pagesize=A4, leftMargin=1.6 * cm, rightMargin=1.6 * cm, topMargin=1.4 * cm, bottomMargin=1.4 * cm,
                            title=title, author="IAProyect")
    styles = getSampleStyleSheet()
    small = ParagraphStyle("small", parent=styles["Normal"], fontSize=8, textColor=colors.HexColor("#666666"))
    width = A4[0] - 3.2 * cm
    story: list = [
        Paragraph(escape(title), styles["Title"]),
        Paragraph(escape(f"{room['name']} ({room['spaceType']}) - {room['length'] / 100:.2f} x {room['width'] / 100:.2f} m, "
                         f"techo {room['height'] / 100:.2f} m - estilo {design['style']} - revision {design.get('revision', '-')}"),
                  styles["Normal"]),
        Paragraph(f"Generado: {escape(data.get('generatedAt') or datetime.now().isoformat(timespec='minutes'))}", small),
        Spacer(1, 0.4 * cm),
    ]

    # Estado de validacion del motor
    report = data.get("report") or {}
    status = "Valido (sin conflictos)" if report.get("valid") else "Con conflictos"
    story.append(Paragraph(f"<b>Validacion del motor:</b> {status}", styles["Normal"]))
    for c in report.get("conflicts", []):
        story.append(Paragraph(f"Conflicto: {escape(human(c))}", styles["Normal"]))
    for w in report.get("warnings", []):
        story.append(Paragraph(f"Advertencia: {escape(human(w))}", small))
    story.append(Spacer(1, 0.4 * cm))

    # Imagenes (render exacto de Blender o imagenes de IA con su leyenda)
    for img in data.get("images", []):
        path = (storage_dir / img["path"]).resolve()
        if storage_dir not in path.parents or not path.exists():
            continue
        pic = Image(str(path))
        ratio = pic.imageHeight / pic.imageWidth
        pic.drawWidth, pic.drawHeight = width, width * ratio
        story.append(KeepTogether([pic, Paragraph(escape(f"{img['title']}. {img.get('caption') or ''}"), small), Spacer(1, 0.3 * cm)]))

    # Plano
    story += [PageBreak(), Paragraph("Plano 2D", styles["Heading2"]),
              plan_drawing(room, data["objects"], width, A4[1] * 0.55), Spacer(1, 0.4 * cm)]

    # Presupuesto por rubro
    budget = data.get("budget") or {}
    rows = [["Rubro", "Minimo", "Maximo"]]
    rows += [[human(r["rubro"]), money(r["min"]), money(r["max"])] for r in budget.get("byCategory", [])]
    rows.append(["TOTAL", money(budget.get("total", {}).get("min")), money(budget.get("total", {}).get("max"))])
    story += [Paragraph("Presupuesto", styles["Heading2"]), _table(rows, [width * 0.5, width * 0.25, width * 0.25])]
    if budget.get("cap") is not None:
        story.append(Paragraph(f"Tope de presupuesto: {money(budget['cap'])}", small))
    story.append(Spacer(1, 0.4 * cm))

    # Lista de compras
    rows = [["Rubro", "Producto", "Tipo", "Cant.", "Precio unitario", "Subtotal"]]
    rows += [[human(i["rubro"]), i["name"], i["kind"], str(i["quantity"]), money_range(i["unitMin"], i["unitMax"]),
              money_range(i["totalMin"], i["totalMax"])] for i in data.get("shopping", [])]
    story += [Paragraph("Lista de compras", styles["Heading2"]),
              _table(rows, [width * 0.15, width * 0.29, width * 0.1, width * 0.07, width * 0.19, width * 0.2])]
    story.append(Paragraph("Precios [EJEMPLO] estimados en MXN; los elementos conceptuales no se pueden comprar.", small))
    story.append(Spacer(1, 0.4 * cm))

    # Lista de materiales
    waste = data.get("wasteFactor", 0.10)
    rows = [["Material", "Tipo", "m2 diseno", f"m2 a comprar (+{waste:.0%})", "Precio m2", "Subtotal"]]
    rows += [[m["name"], m["type"], f"{m['m2']:g}", str(m["m2WithWaste"]), money_range(m["priceM2Min"], m["priceM2Max"]),
              money_range(m["totalMin"], m["totalMax"])] for m in data.get("materials", [])]
    story += [Paragraph("Lista de materiales", styles["Heading2"]),
              _table(rows, [width * 0.24, width * 0.12, width * 0.14, width * 0.18, width * 0.14, width * 0.18])
              if len(rows) > 1 else Paragraph("Este diseno no tiene materiales aplicados.", styles["Normal"])]
    doc.build(story)
    return buf.getvalue()


# ----------------------------------------------------------------------------- Excel y CSV


def _sheet(ws, header: list[str], rows: list[list], money_cols: tuple[int, ...] = ()) -> None:
    ws.append(header)
    for c in ws[1]:
        c.font = Font(bold=True)
        c.fill = PatternFill("solid", fgColor="E9E4DA")
        c.alignment = Alignment(horizontal="center")
    for r in rows:
        ws.append(r)
    for col in money_cols:
        for row in ws.iter_rows(min_row=2, min_col=col, max_col=col):
            for cell in row:
                cell.number_format = '"$"#,##0'
    for i, _ in enumerate(header, start=1):
        width = max(len(str(c.value or "")) for c in ws[get_column_letter(i)]) + 2
        ws.column_dimensions[get_column_letter(i)].width = min(45, max(10, width))
    ws.freeze_panes = "A2"


def report_xlsx(data: dict) -> bytes:
    wb = Workbook()
    ws = wb.active
    ws.title = "Compras"
    _sheet(ws, ["Rubro", "Producto", "Id catalogo", "Tipo", "Cantidad", "Unitario min", "Unitario max", "Subtotal min", "Subtotal max"],
           [[i["rubro"], i["name"], i["catalogId"], i["kind"], i["quantity"], i["unitMin"], i["unitMax"], i["totalMin"], i["totalMax"]]
            for i in data.get("shopping", [])], money_cols=(6, 7, 8, 9))
    waste = data.get("wasteFactor", 0.10)
    _sheet(wb.create_sheet("Materiales"),
           ["Material", "Id", "Tipo", "m2 del diseno", f"m2 con {waste:.0%} desperdicio", "Precio m2 min", "Precio m2 max", "Subtotal min", "Subtotal max"],
           [[m["name"], m["materialId"], m["type"], m["m2"], m["m2WithWaste"], m["priceM2Min"], m["priceM2Max"], m["totalMin"], m["totalMax"]]
            for m in data.get("materials", [])], money_cols=(6, 7, 8, 9))
    budget = data.get("budget") or {}
    _sheet(wb.create_sheet("Presupuesto"), ["Rubro", "Minimo", "Maximo"],
           [[r["rubro"], r["min"], r["max"]] for r in budget.get("byCategory", [])]
           + [["TOTAL", budget.get("total", {}).get("min"), budget.get("total", {}).get("max")]]
           + ([["Tope", budget["cap"], budget["cap"]]] if budget.get("cap") is not None else []), money_cols=(2, 3))
    _sheet(wb.create_sheet("Muebles"), ["instanceId", "Producto", "Categoria", "x (cm)", "y (cm)", "Rotacion", "Ancho", "Fondo", "Alto", "Color", "Material", "Bloqueado"],
           [[o["instanceId"], o["name"], o["category"], o["x"], o["y"], o["rotation"], o["widthCm"], o["depthCm"], o["heightCm"],
             o.get("color"), o.get("material"), "si" if o.get("locked") else "no"] for o in data["objects"]])
    buf = io.BytesIO()
    wb.save(buf)
    return buf.getvalue()


def report_csv(data: dict) -> bytes:
    """Lista de compras y de materiales en un solo CSV (UTF-8 con BOM para que Excel respete los acentos)."""
    buf = io.StringIO()
    w = csv.writer(buf)
    w.writerow(["tipo", "rubro", "nombre", "id", "cantidad", "unidad", "subtotal_min", "subtotal_max"])
    for i in data.get("shopping", []):
        w.writerow(["mueble", i["rubro"], i["name"], i["catalogId"], i["quantity"], "pieza", i["totalMin"], i["totalMax"]])
    for m in data.get("materials", []):
        w.writerow(["material", "materiales", m["name"], m["materialId"], m["m2WithWaste"], "m2", m["totalMin"], m["totalMax"]])
    return ("﻿" + buf.getvalue()).encode("utf-8")
