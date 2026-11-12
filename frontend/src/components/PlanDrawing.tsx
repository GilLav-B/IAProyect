import type { DesignState, PlacedObject, Product } from "@/lib/types";
import { rotatedDims } from "@/lib/units";
import styles from "./PlanDrawing.module.css";

interface Props {
  state: DesignState;
  products: Map<string, Product>;
  /** thumb = miniatura de las tarjetas; hero = plano grande con cotas y trazo animado. */
  variant?: "thumb" | "hero";
  className?: string;
}

const WALL = 12; // grosor del muro en el dibujo (cm)
/** Categorias que van colgadas o encima de otro mueble: se dibujan como linea, no como huella. */
const THIN = new Set(["televisor", "cuadro"]);

/** Rectangulo girado del mueble en cm (antihorario; rotacion 0 = el frente mira al sur). Igual que el motor. */
function footprint(o: PlacedObject, p: Product): [number, number][] {
  const w = p.dimensions.width;
  const d = p.dimensions.depth;
  const r = (o.rotation * Math.PI) / 180;
  const [a, b] = rotatedDims(o.rotation, w, d);
  const cx = o.position.x + a / 2;
  const cy = o.position.y + b / 2;
  const local: [number, number][] = [[-w / 2, -d / 2], [w / 2, -d / 2], [w / 2, d / 2], [-w / 2, d / 2]];
  return local.map(([x, y]) => [cx + x * Math.cos(r) - y * Math.sin(r), cy + x * Math.sin(r) + y * Math.cos(r)]);
}

/**
 * Plano de planta del diseno, dibujado con sus datos reales (no es una imagen): muros, puertas con su giro,
 * ventanas y la huella de cada mueble con una marca en el frente.
 */
export function PlanDrawing({ state, products, variant = "thumb", className }: Props) {
  const { room } = state;
  const L = room.length;
  const W = room.width;
  const hero = variant === "hero";
  const pad = hero ? 70 : 20;
  const Y = (y: number) => W - y; // el plano crece hacia el norte; el SVG hacia abajo
  const pts = (list: [number, number][]) => list.map(([x, y]) => `${x.toFixed(1)},${Y(y).toFixed(1)}`).join(" ");

  const along = (wall: string, pos: number): [number, number] =>
    wall === "sur" ? [pos, 0] : wall === "norte" ? [pos, W] : wall === "oeste" ? [0, pos] : [L, pos];
  const inward = (wall: string): [number, number] =>
    wall === "sur" ? [0, 1] : wall === "norte" ? [0, -1] : wall === "oeste" ? [1, 0] : [-1, 0];

  const objects = [...state.objects].sort((a, b) => {
    const rank = (o: PlacedObject) => (products.get(o.catalogId)?.category === "alfombra" ? 0 : THIN.has(products.get(o.catalogId)?.category ?? "") ? 2 : 1);
    return rank(a) - rank(b);
  });

  return (
    <svg
      className={`${styles.plan} ${hero ? styles.hero : styles.thumb} ${className ?? ""}`}
      viewBox={`${-pad} ${-pad} ${L + pad * 2} ${W + pad * 2}`}
      role="img"
      aria-label={`Plano de ${room.name}: ${(L / 100).toFixed(2)} por ${(W / 100).toFixed(2)} metros, ${state.objects.length} muebles`}
    >
      {/* Piso y muros */}
      <rect x={0} y={0} width={L} height={W} className={styles.floor} />
      <rect x={-WALL / 2} y={-WALL / 2} width={L + WALL} height={W + WALL} className={styles.walls} strokeWidth={WALL} pathLength={1} />

      {/* Puertas: hueco en el muro, hoja abierta y arco de giro */}
      {room.doors.map((d) => {
        const [hx, hy] = along(d.wall, d.position);
        const [ex, ey] = along(d.wall, d.position + d.width);
        const [nx, ny] = inward(d.wall);
        const r = d.width;
        const ux = (ex - hx) / r;
        const uy = (ey - hy) / r;
        const arc = Array.from({ length: 17 }, (_, i) => {
          const k = (i * Math.PI) / 2 / 16;
          return [hx + r * (ux * Math.cos(k) + nx * Math.sin(k)), hy + r * (uy * Math.cos(k) + ny * Math.sin(k))] as [number, number];
        });
        return (
          <g key={d.id}>
            <line x1={hx} y1={Y(hy)} x2={ex} y2={Y(ey)} className={styles.gap} strokeWidth={WALL + 2} />
            <line x1={hx} y1={Y(hy)} x2={hx + nx * r} y2={Y(hy + ny * r)} className={styles.door} />
            <polyline points={pts(arc)} className={styles.swing} />
          </g>
        );
      })}

      {/* Ventanas: doble linea dentro del muro */}
      {room.windows.map((w) => {
        const [x1, y1] = along(w.wall, w.position);
        const [x2, y2] = along(w.wall, w.position + w.width);
        return (
          <g key={w.id}>
            <line x1={x1} y1={Y(y1)} x2={x2} y2={Y(y2)} className={styles.gap} strokeWidth={WALL + 2} />
            <line x1={x1} y1={Y(y1)} x2={x2} y2={Y(y2)} className={styles.window} strokeWidth={WALL} />
          </g>
        );
      })}

      {/* Muebles: huella real y marca del frente */}
      {objects.map((o, i) => {
        const p = products.get(o.catalogId);
        if (!p) return null;
        const f = footprint(o, p);
        const cat = p.category;
        const kind = cat === "alfombra" ? styles.rug : THIN.has(cat) ? styles.thin : cat === "planta" ? styles.plant : styles.piece;
        return (
          <g key={o.instanceId} className={styles.object} style={hero ? { animationDelay: `${0.5 + i * 0.08}s` } : undefined}>
            <polygon points={pts(f)} className={kind} pathLength={1} />
            {!THIN.has(cat) && cat !== "alfombra" && <line x1={f[0][0]} y1={Y(f[0][1])} x2={f[1][0]} y2={Y(f[1][1])} className={styles.front} />}
          </g>
        );
      })}

      {/* Cotas generales (solo en el plano grande) */}
      {hero && (
        <g className={styles.dims}>
          <line x1={0} y1={W + 40} x2={L} y2={W + 40} />
          <line x1={0} y1={W + 30} x2={0} y2={W + 50} />
          <line x1={L} y1={W + 30} x2={L} y2={W + 50} />
          <text x={L / 2} y={W + 62} textAnchor="middle">
            {(L / 100).toFixed(2)} m
          </text>
          <line x1={-40} y1={0} x2={-40} y2={W} />
          <line x1={-50} y1={0} x2={-30} y2={0} />
          <line x1={-50} y1={W} x2={-30} y2={W} />
          <text x={-52} y={W / 2} textAnchor="middle" transform={`rotate(-90 -52 ${W / 2})`}>
            {(W / 100).toFixed(2)} m
          </text>
        </g>
      )}
    </svg>
  );
}
