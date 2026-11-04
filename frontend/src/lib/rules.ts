// Reglas de diseno.pl que la interfaz necesita para guiar al usuario.
// La validacion definitiva SIEMPRE la hace el backend (motor en C#); esto solo evita errores obvios.
import type { Room, Wall } from "./types";

export const STYLES = ["moderno", "minimalista", "industrial", "japandi", "clasico"] as const;
export const BUDGET_MODES = ["minimizar", "ajustar", "flexible"] as const;
export const WALLS: Wall[] = ["norte", "sur", "este", "oeste"];

/** espacio_de/2: oficina = comercial; el resto = domestico. */
export const SPACES_BY_PROJECT: Record<string, string[]> = {
  domestico: ["sala", "recamara", "cocina", "comedor"],
  comercial: ["oficina"],
};

/** categorias_espacio/2 */
export const SPACE_CATEGORIES: Record<string, string[]> = {
  sala: ["sofa", "sillon", "mesa_centro", "mueble_tv", "televisor", "alfombra", "lampara", "cuadro", "planta", "librero"],
  recamara: ["cama", "mueble_tv", "televisor", "alfombra", "lampara", "cuadro", "planta"],
  cocina: ["isla", "lampara", "cuadro", "planta"],
  oficina: ["escritorio", "silla_oficina", "librero", "lampara", "planta", "cuadro", "alfombra"],
  comedor: ["mesa_comedor", "silla_comedor", "lampara", "cuadro", "planta", "alfombra"],
};

/** [SUPUESTO] rangos de medidas (cm), iguales a RoomRules del backend. */
export const RANGES = { side: { min: 150, max: 2000 }, height: { min: 200, max: 600 } };

const wallLength = (r: Room, w: Wall) => (w === "norte" || w === "sur" ? r.length : r.width);

/** Mismas reglas que RoomRules.Validate del backend, para avisar antes de enviar. */
export function roomErrors(r: Room): string[] {
  const errors: string[] = [];
  const m = (cm: number) => `${(cm / 100).toFixed(2)} m`;
  if (r.length < RANGES.side.min || r.length > RANGES.side.max) errors.push(`El largo debe estar entre ${m(RANGES.side.min)} y ${m(RANGES.side.max)}.`);
  if (r.width < RANGES.side.min || r.width > RANGES.side.max) errors.push(`El ancho debe estar entre ${m(RANGES.side.min)} y ${m(RANGES.side.max)}.`);
  if (r.height < RANGES.height.min || r.height > RANGES.height.max) errors.push(`El alto debe estar entre ${m(RANGES.height.min)} y ${m(RANGES.height.max)}.`);

  const openings = [
    ...r.doors.map((d) => ({ ...d, kind: "puerta" })),
    ...r.windows.map((w) => ({ ...w, kind: "ventana" })),
  ];
  for (const o of openings) {
    if (o.width <= 0) errors.push(`La ${o.kind} ${o.id} necesita un ancho mayor a 0.`);
    if (o.position < 0 || o.position + o.width > wallLength(r, o.wall))
      errors.push(`La ${o.kind} ${o.id} se sale de la pared ${o.wall} (mide ${m(wallLength(r, o.wall))}).`);
  }
  for (const w of r.windows) if (w.sillHeight + (w.height ?? 0) > r.height) errors.push(`La ventana ${w.id} es mas alta que el techo.`);
  for (const wall of WALLS) {
    const list = openings.filter((o) => o.wall === wall).sort((a, b) => a.position - b.position);
    for (let i = 1; i < list.length; i++)
      if (list[i].position < list[i - 1].position + list[i - 1].width)
        errors.push(`La ${list[i].kind} ${list[i].id} se encima con la ${list[i - 1].kind} ${list[i - 1].id} en la pared ${wall}.`);
  }
  return errors;
}
