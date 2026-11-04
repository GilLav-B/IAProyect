// Se guarda en cm (enteros) y se muestra en metros.
// En la escena 1 unidad = 1 m. Plano: x = largo, y = ancho. Escena: X = x, Z = -y, Y = altura.

export const cmToM = (cm: number) => cm / 100;

/** "2.80 m" */
export const fmtM = (cm: number) => `${(cm / 100).toFixed(2)} m`;

export const fmtMoney = (n: number) =>
  n.toLocaleString("es-MX", { style: "currency", currency: "MXN", maximumFractionDigits: 0 });

export const fmtRange = (r: { min: number; max: number }) =>
  r.min === r.max ? fmtMoney(r.min) : `${fmtMoney(r.min)} - ${fmtMoney(r.max)}`;

/** Rotacion en 0..359. */
export const normalizeRotation = (deg: number) => ((Math.round(deg) % 360) + 360) % 360;

/**
 * Caja envolvente en planta segun la rotacion. Con multiplos de 90 es dims_rot/5 de diseno.pl;
 * con cualquier otro angulo es la caja del rectangulo girado (igual que el motor).
 */
export function rotatedDims(rotation: number, width: number, depth: number): [number, number] {
  const r = normalizeRotation(rotation);
  if (r % 90 === 0) return r === 90 || r === 270 ? [depth, width] : [width, depth];
  const t = (r * Math.PI) / 180;
  const c = Math.abs(Math.cos(t));
  const s = Math.abs(Math.sin(t));
  return [width * c + depth * s, width * s + depth * c];
}

/**
 * Nueva posicion al girar sin mover el centro del mueble. La posicion (x,y) es la esquina suroeste de la
 * caja envolvente, que cambia de tamano con el angulo.
 */
export function rotateKeepingCenter(x: number, y: number, rotation: number, newRotation: number, width: number, depth: number) {
  const [a, b] = rotatedDims(rotation, width, depth);
  const [a2, b2] = rotatedDims(newRotation, width, depth);
  return {
    x: Math.round(x + a / 2 - a2 / 2),
    y: Math.round(y + b / 2 - b2 / 2),
    rotation: normalizeRotation(newRotation),
  };
}

/** Colores del catalogo (valores en espanol) a hex para la escena. [SUPUESTO] tonos aproximados. */
const COLORS: Record<string, string> = {
  beige: "#c4ab85",
  gris: "#7d828a",
  negro: "#262628",
  blanco: "#e6e4de",
  madera_clara: "#c9a27b",
  madera_oscura: "#6e4b30",
  cafe: "#7b5332",
  verde: "#4f8b52",
  dorado: "#c9a64a",
  // Colores de muro
  blanco_hueso: "#efe9dd",
  arena: "#d9c9ad",
  gris_claro: "#cfcfca",
  verde_salvia: "#aebba4",
  azul_claro: "#bccbd6",
  terracota: "#c98d6e",
};

/** [SUPUESTO] paleta de pintura para muros. */
export const WALL_COLORS = ["blanco_hueso", "blanco", "arena", "gris_claro", "verde_salvia", "azul_claro", "terracota"];

export const colorHex = (name?: string) => (name && COLORS[name]) || "#9aa3ad";

export const label = (value: string) => value.replaceAll("_", " ");
