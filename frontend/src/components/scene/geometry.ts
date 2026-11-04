import type { Door, Room, Wall, Window } from "@/lib/types";

/** Rectangulo en planta en cm; (x1,y1) = esquina suroeste. */
export interface PlanRect {
  x1: number;
  y1: number;
  x2: number;
  y2: number;
}

/** Zona libre pegada a una pared (zona_pared/6 en diseno.pl). */
export function wallZone(room: Room, wall: Wall, position: number, width: number, depth: number): PlanRect {
  switch (wall) {
    case "sur":
      return { x1: position, y1: 0, x2: position + width, y2: depth };
    case "norte":
      return { x1: position, y1: room.width - depth, x2: position + width, y2: room.width };
    case "oeste":
      return { x1: 0, y1: position, x2: depth, y2: position + width };
    case "este":
      return { x1: room.length - depth, y1: position, x2: room.length, y2: position + width };
  }
}

/** La puerta necesita tanto espacio libre como su ancho (zona_puerta/3). */
export const doorZone = (room: Room, d: Door) => wallZone(room, d.wall, d.position, d.width, d.width);

/** [SUPUESTO] 40 cm libres frente a la ventana, igual que parametro(acceso_ventana, 40). */
export const WINDOW_ACCESS_CM = 40;
export const windowZone = (room: Room, w: Window) => wallZone(room, w.wall, w.position, w.width, WINDOW_ACCESS_CM);

/** Centro de un rectangulo de planta en coordenadas de escena (m): X = x, Z = -y. */
export const planCenter = (r: PlanRect): [number, number] => [(r.x1 + r.x2) / 200, -(r.y1 + r.y2) / 200];
