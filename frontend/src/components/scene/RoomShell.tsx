"use client";

import { Grid } from "@react-three/drei";
import type { Room, Wall } from "@/lib/types";
import { cmToM } from "@/lib/units";
import { doorZone, planCenter, windowZone, type PlanRect } from "./geometry";

const WALL_T = 0.1; // grosor visual del muro (m), hacia afuera del cuarto
const DOOR_HEIGHT_CM = 210; // [SUPUESTO] alto de puerta para dibujarla
const WINDOW_HEIGHT_CM = 120; // [SUPUESTO] alto de ventana si no viene en el DesignState

/** Caja pegada a una pared: va de "position" a "position + width" a lo largo del muro. */
function onWall(room: Room, wall: Wall, position: number, width: number, bottom: number, height: number) {
  const L = cmToM(room.length);
  const W = cmToM(room.width);
  const along = cmToM(position + width / 2);
  const y = cmToM(bottom + height / 2);
  const size = { w: cmToM(width), h: cmToM(height), t: WALL_T + 0.02 };
  switch (wall) {
    case "sur":
      return { position: [along, y, WALL_T / 2] as const, args: [size.w, size.h, size.t] as const };
    case "norte":
      return { position: [along, y, -W - WALL_T / 2] as const, args: [size.w, size.h, size.t] as const };
    case "oeste":
      return { position: [-WALL_T / 2, y, -along] as const, args: [size.t, size.h, size.w] as const };
    case "este":
      return { position: [L + WALL_T / 2, y, -along] as const, args: [size.t, size.h, size.w] as const };
  }
}

function Zone({ rect, color }: { rect: PlanRect; color: string }) {
  const [x, z] = planCenter(rect);
  return (
    <mesh position={[x, 0.003, z]} rotation-x={-Math.PI / 2}>
      <planeGeometry args={[cmToM(rect.x2 - rect.x1), cmToM(rect.y2 - rect.y1)]} />
      <meshBasicMaterial color={color} transparent opacity={0.28} depthWrite={false} />
    </mesh>
  );
}

/** Piso, muros, puertas y ventanas. Son estructurales: no se seleccionan ni se mueven. */
export function RoomShell({ room }: { room: Room }) {
  const L = cmToM(room.length);
  const W = cmToM(room.width);
  const H = cmToM(room.height);

  const walls: { key: string; position: [number, number, number]; args: [number, number, number] }[] = [
    { key: "sur", position: [L / 2, H / 2, WALL_T / 2], args: [L + 2 * WALL_T, H, WALL_T] },
    { key: "norte", position: [L / 2, H / 2, -W - WALL_T / 2], args: [L + 2 * WALL_T, H, WALL_T] },
    { key: "oeste", position: [-WALL_T / 2, H / 2, -W / 2], args: [WALL_T, H, W] },
    { key: "este", position: [L + WALL_T / 2, H / 2, -W / 2], args: [WALL_T, H, W] },
  ];

  return (
    <group>
      {/* Piso */}
      <mesh
        position={[L / 2, 0, -W / 2]}
        rotation-x={-Math.PI / 2}
        receiveShadow
      >
        <planeGeometry args={[L, W]} />
        <meshStandardMaterial color="#d8d2c6" />
      </mesh>
      {/* Cuadricula: celdas de 50 cm, secciones de 1 m, solo dentro del cuarto */}
      <Grid
        args={[L, W]}
        position={[L / 2, 0.002, -W / 2]}
        cellSize={0.5}
        cellThickness={0.6}
        cellColor="#bdb4a5"
        sectionSize={1}
        sectionThickness={1}
        sectionColor="#a39886"
        fadeDistance={50}
      />

      {/* Muros semitransparentes para ver el interior */}
      {walls.map((w) => (
        <mesh key={w.key} position={w.position}>
          <boxGeometry args={w.args} />
          <meshStandardMaterial color="#f3efe8" transparent opacity={0.35} depthWrite={false} />
        </mesh>
      ))}

      {/* Puertas y su zona de abatimiento */}
      {room.doors.map((d) => {
        const b = onWall(room, d.wall, d.position, d.width, 0, Math.min(DOOR_HEIGHT_CM, room.height));
        return (
          <group key={d.id}>
            <mesh position={[...b.position]}>
              <boxGeometry args={[...b.args]} />
              <meshStandardMaterial color="#8a5a35" />
            </mesh>
            <Zone rect={doorZone(room, d)} color="#e0803a" />
          </group>
        );
      })}

      {/* Ventanas (desde el antepecho) y su zona de acceso */}
      {room.windows.map((w) => {
        const height = Math.min(w.height ?? WINDOW_HEIGHT_CM, room.height - w.sillHeight);
        const b = onWall(room, w.wall, w.position, w.width, w.sillHeight, height);
        return (
          <group key={w.id}>
            <mesh position={[...b.position]}>
              <boxGeometry args={[...b.args]} />
              <meshStandardMaterial color="#7fb6e0" transparent opacity={0.6} />
            </mesh>
            <Zone rect={windowZone(room, w)} color="#4c9be0" />
          </group>
        );
      })}

      {/* Columnas */}
      {room.columns.map((c) => (
        <mesh key={c.id} position={[cmToM(c.x + c.width / 2), H / 2, -cmToM(c.y + c.depth / 2)]}>
          <boxGeometry args={[cmToM(c.width), H, cmToM(c.depth)]} />
          <meshStandardMaterial color="#cfc8bb" />
        </mesh>
      ))}
    </group>
  );
}
