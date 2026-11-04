"use client";

import { Suspense } from "react";
import { FrontSide, type Texture } from "three";
import { useTexture } from "@react-three/drei";
import type { Room, Wall } from "@/lib/types";
import { cmToM, colorHex } from "@/lib/units";
import { doorZone, planCenter, windowZone, type PlanRect } from "./geometry";
import { FLOORS, TEXTURES, tile, type FloorKind } from "./materials";

const DOOR_HEIGHT_CM = 210; // [SUPUESTO] alto de puerta para dibujarla
const WINDOW_HEIGHT_CM = 120; // [SUPUESTO] alto de ventana si no viene en el DesignState
const WALL_COLOR = "#f1ede6";
const CAP = 0.1; // grosor visual del muro visto desde arriba (m)

/**
 * Transformacion de cada pared: posicion del centro y giro para que su cara mire HACIA DENTRO.
 * Con FrontSide, las paredes cercanas a la camara no se dibujan y se ve el interior (vista de "casa de munecas").
 * "along" mide la distancia a lo largo de la pared, igual que la posicion de puertas y ventanas.
 */
function wallFrame(room: Room, wall: Wall) {
  const L = cmToM(room.length);
  const W = cmToM(room.width);
  switch (wall) {
    case "sur":
      return { length: L, at: (along: number) => [along, 0, 0] as const, rotY: Math.PI };
    case "norte":
      return { length: L, at: (along: number) => [along, 0, -W] as const, rotY: 0 };
    case "oeste":
      return { length: W, at: (along: number) => [0, 0, -along] as const, rotY: Math.PI / 2 };
    case "este":
      return { length: W, at: (along: number) => [L, 0, -along] as const, rotY: -Math.PI / 2 };
  }
}

const WALLS: Wall[] = ["sur", "norte", "oeste", "este"];

function Floor({ room, kind }: { room: Room; kind: FloorKind }) {
  const L = cmToM(room.length);
  const W = cmToM(room.width);
  const floor = FLOORS[kind];
  const tex = useTexture(floor.textures, (t) => tile(t as unknown as Record<string, Texture>, 1));
  // La textura se repite segun el tamano real del cuarto
  const repeat: [number, number] = [L / floor.tileM, W / floor.tileM];
  // Si se eligio un color de piso, tine la textura
  const tint = room.surfaces?.floor?.color ? colorHex(room.surfaces.floor.color) : "#ffffff";
  return (
    <mesh position={[L / 2, 0, -W / 2]} rotation-x={-Math.PI / 2} receiveShadow>
      <planeGeometry args={[L, W]} />
      <meshStandardMaterial
        {...tex}
        color={tint}
        roughness={floor.roughness}
        map-repeat={repeat}
        normalMap-repeat={repeat}
        roughnessMap-repeat={repeat}
      />
    </mesh>
  );
}

function Walls({ room }: { room: Room }) {
  const wallColor = room.surfaces?.walls?.color ? colorHex(room.surfaces.walls.color) : WALL_COLOR;
  const H = cmToM(room.height);
  const tex = useTexture(TEXTURES.plaster, (t) => tile(t as unknown as Record<string, Texture>, 2));
  return (
    <>
      {WALLS.map((wall) => {
        const f = wallFrame(room, wall);
        const [x, , z] = f.at(f.length / 2);
        return (
          <group key={wall} position={[x, 0, z]} rotation-y={f.rotY}>
            {/* Cara interior del muro */}
            <mesh position={[0, H / 2, 0]} receiveShadow>
              <planeGeometry args={[f.length, H]} />
              <meshStandardMaterial {...tex} color={wallColor} roughness={0.95} side={FrontSide} />
            </mesh>
            {/* Zoclo */}
            <mesh position={[0, 0.04, 0.006]}>
              <boxGeometry args={[f.length, 0.08, 0.012]} />
              <meshStandardMaterial color="#fbfaf7" roughness={0.6} />
            </mesh>
            {/* Canto superior del muro: marca la planta vista desde arriba */}
            <mesh position={[0, H, -CAP / 2]}>
              <boxGeometry args={[f.length + CAP * 2, 0.02, CAP]} />
              <meshStandardMaterial color="#5f5a52" roughness={0.9} />
            </mesh>
          </group>
        );
      })}
    </>
  );
}

/** Pieza pegada a la pared (puerta o ventana). */
function OnWall({ room, wall, position, width, children }: { room: Room; wall: Wall; position: number; width: number; children: React.ReactNode }) {
  const f = wallFrame(room, wall);
  const [x, , z] = f.at(cmToM(position + width / 2));
  return (
    <group position={[x, 0, z]} rotation-y={f.rotY}>
      {children}
    </group>
  );
}

function Zone({ rect, color }: { rect: PlanRect; color: string }) {
  const [x, z] = planCenter(rect);
  return (
    <mesh position={[x, 0.004, z]} rotation-x={-Math.PI / 2} raycast={() => null}>
      <planeGeometry args={[cmToM(rect.x2 - rect.x1), cmToM(rect.y2 - rect.y1)]} />
      <meshBasicMaterial color={color} transparent opacity={0.16} depthWrite={false} />
    </mesh>
  );
}

/** Piso, muros, puertas y ventanas. Son estructurales: no se seleccionan ni se mueven. */
export function RoomShell({ room, showZones, floorKind = "madera" }: { room: Room; showZones: boolean; floorKind?: FloorKind }) {
  return (
    <group>
      <Suspense
        fallback={
          <mesh position={[cmToM(room.length) / 2, 0, -cmToM(room.width) / 2]} rotation-x={-Math.PI / 2} receiveShadow>
            <planeGeometry args={[cmToM(room.length), cmToM(room.width)]} />
            <meshStandardMaterial color="#cdb79a" />
          </mesh>
        }
      >
        <Floor room={room} kind={floorKind} />
        <Walls room={room} />
      </Suspense>

      {room.doors.map((d) => {
        const w = cmToM(d.width);
        const h = cmToM(Math.min(DOOR_HEIGHT_CM, room.height));
        return (
          <group key={d.id}>
            <OnWall room={room} wall={d.wall} position={d.position} width={d.width}>
              {/* Planos que miran hacia dentro: si el muro se oculta, la puerta tambien */}
              <mesh position={[0, h / 2 + 0.025, 0.004]}>
                <planeGeometry args={[w + 0.1, h + 0.05]} />
                <meshStandardMaterial color="#efece6" roughness={0.5} side={FrontSide} />
              </mesh>
              <mesh position={[0, h / 2, 0.008]}>
                <planeGeometry args={[w - 0.03, h - 0.03]} />
                <meshStandardMaterial color="#9a6b45" roughness={0.6} side={FrontSide} />
              </mesh>
              <mesh position={[w / 2 - 0.12, h * 0.48, 0.012]}>
                <circleGeometry args={[0.03, 20]} />
                <meshStandardMaterial color="#c9ccd1" metalness={0.9} roughness={0.25} side={FrontSide} />
              </mesh>
            </OnWall>
            {showZones && <Zone rect={doorZone(room, d)} color="#e8590c" />}
          </group>
        );
      })}

      {room.windows.map((win) => {
        const w = cmToM(win.width);
        const sill = cmToM(win.sillHeight);
        const h = cmToM(Math.min(win.height ?? WINDOW_HEIGHT_CM, room.height - win.sillHeight - 10));
        return (
          <group key={win.id}>
            <OnWall room={room} wall={win.wall} position={win.position} width={win.width}>
              <mesh position={[0, sill + h / 2, 0.004]}>
                <planeGeometry args={[w + 0.08, h + 0.08]} />
                <meshStandardMaterial color="#f7f6f2" roughness={0.4} side={FrontSide} />
              </mesh>
              <mesh position={[0, sill + h / 2, 0.008]}>
                <planeGeometry args={[w - 0.04, h - 0.04]} />
                <meshStandardMaterial color="#d6ebfa" emissive="#c4e3ff" emissiveIntensity={0.9} roughness={0.05} side={FrontSide} />
              </mesh>
              <mesh position={[0, sill + h / 2, 0.012]}>
                <planeGeometry args={[0.03, h - 0.04]} />
                <meshStandardMaterial color="#f7f6f2" side={FrontSide} />
              </mesh>
            </OnWall>
            {showZones && <Zone rect={windowZone(room, win)} color="#1c7ed6" />}
          </group>
        );
      })}

      {room.columns.map((c) => (
        <mesh key={c.id} position={[cmToM(c.x + c.width / 2), cmToM(room.height) / 2, -cmToM(c.y + c.depth / 2)]} castShadow receiveShadow>
          <boxGeometry args={[cmToM(c.width), cmToM(room.height), cmToM(c.depth)]} />
          <meshStandardMaterial color={WALL_COLOR} roughness={0.9} />
        </mesh>
      ))}
    </group>
  );
}
