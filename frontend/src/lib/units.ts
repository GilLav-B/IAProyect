// Se guarda en cm (enteros) y se muestra en metros.
// En la escena 1 unidad = 1 m. Plano: x = largo, y = ancho. Escena: X = x, Z = -y, Y = altura.

export const cmToM = (cm: number) => cm / 100;

/** "2.80 m" */
export const fmtM = (cm: number) => `${(cm / 100).toFixed(2)} m`;

export const fmtMoney = (n: number) =>
  n.toLocaleString("es-MX", { style: "currency", currency: "MXN", maximumFractionDigits: 0 });

export const fmtRange = (r: { min: number; max: number }) =>
  r.min === r.max ? fmtMoney(r.min) : `${fmtMoney(r.min)} - ${fmtMoney(r.max)}`;

/** Medidas en planta segun la rotacion (igual que dims_rot/5 en diseno.pl). */
export const rotatedDims = (rotation: number, width: number, depth: number): [number, number] =>
  rotation === 90 || rotation === 270 ? [depth, width] : [width, depth];

/** Colores del catalogo (valores en espanol) a hex para la escena. [SUPUESTO] tonos aproximados. */
const COLORS: Record<string, string> = {
  beige: "#d9c7a7",
  gris: "#8b9099",
  negro: "#2e2e30",
  blanco: "#efefea",
  madera_clara: "#c9a27b",
  madera_oscura: "#6e4b30",
  cafe: "#7b5332",
  verde: "#4f8b52",
  dorado: "#c9a64a",
};

export const colorHex = (name?: string) => (name && COLORS[name]) || "#9aa3ad";

export const label = (value: string) => value.replaceAll("_", " ");
