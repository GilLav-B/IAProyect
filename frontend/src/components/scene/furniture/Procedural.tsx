"use client";

import { useMemo } from "react";
import { CanvasTexture, CatmullRomCurve3, SRGBColorSpace, Vector3 } from "three";
import { RoundedBox } from "@react-three/drei";
import { colorHex } from "@/lib/units";
import { FabricMaterial, MetalMaterial, SurfaceMaterial, WoodMaterial } from "../materials";

/**
 * Muebles armados en codigo con las medidas EXACTAS del catalogo.
 * Coordenadas locales en metros: ancho en X, fondo en Z, alto en Y.
 * El frente mira a +Z; el origen es el centro de la huella, a nivel de piso.
 */
export interface Size {
  w: number;
  d: number;
  h: number;
}

interface Props {
  size: Size;
  /** Color elegido en el configurador (valor del catalogo, ej. beige). */
  color?: string;
  /** Material elegido (madera, tela, metal...). */
  material?: string;
  /** Plazas del sofa (atributo del catalogo). */
  seats?: number;
}

function Leg({ x, z, h, r = 0.022 }: { x: number; z: number; h: number; r?: number }) {
  return (
    <mesh position={[x, h / 2, z]} castShadow>
      <cylinderGeometry args={[r, r * 0.8, h, 12]} />
      <WoodMaterial color="madera_oscura" />
    </mesh>
  );
}

/** Sofa (y sillon con seats=1): base, brazos, respaldo y cojines redondeados. */
export function Sofa({ size, color, seats }: Props) {
  const { w, d, h } = size;
  const legH = 0.08;
  const arm = Math.min(0.16, w * 0.12);
  const baseH = Math.max(0.18, h * 0.28);
  const seatH = Math.max(0.14, h * 0.17);
  const backT = Math.min(0.2, d * 0.24);
  const n = Math.max(1, seats ?? Math.round((w - 2 * arm) / 0.65));
  const inner = w - 2 * arm;
  const cushionW = inner / n;
  const seatD = d - backT - 0.02;
  return (
    <group>
      {[[-1, -1], [1, -1], [-1, 1], [1, 1]].map(([sx, sz]) => (
        <Leg key={`${sx}${sz}`} x={sx * (w / 2 - 0.07)} z={sz * (d / 2 - 0.07)} h={legH} />
      ))}
      <RoundedBox args={[w, baseH, d]} radius={0.03} position={[0, legH + baseH / 2, 0]} castShadow receiveShadow>
        <FabricMaterial color={color} />
      </RoundedBox>
      {[-1, 1].map((s) => (
        <RoundedBox key={s} args={[arm, h * 0.62 - legH, d]} radius={0.05} position={[s * (w / 2 - arm / 2), legH + (h * 0.62 - legH) / 2, 0]} castShadow>
          <FabricMaterial color={color} />
        </RoundedBox>
      ))}
      <RoundedBox args={[w, h - legH, backT]} radius={0.05} position={[0, legH + (h - legH) / 2, -d / 2 + backT / 2]} castShadow>
        <FabricMaterial color={color} />
      </RoundedBox>
      {Array.from({ length: n }, (_, i) => {
        const x = -inner / 2 + cushionW * (i + 0.5);
        return (
          <group key={i}>
            <RoundedBox args={[cushionW - 0.015, seatH, seatD]} radius={0.05} smoothness={4} position={[x, legH + baseH + seatH / 2, -d / 2 + backT + seatD / 2]} castShadow>
              <FabricMaterial color={color} />
            </RoundedBox>
            <RoundedBox
              args={[cushionW - 0.03, (h - legH - baseH - seatH) * 0.9, 0.14]}
              radius={0.06}
              smoothness={4}
              position={[x, legH + baseH + seatH + (h - legH - baseH - seatH) * 0.45, -d / 2 + backT + 0.06]}
              rotation-x={-0.12}
              castShadow
            >
              <FabricMaterial color={color} />
            </RoundedBox>
          </group>
        );
      })}
    </group>
  );
}

/** Cama: base de madera, colchon, cabecera, almohadas y edredon del color elegido. */
export function Bed({ size, color }: Props) {
  const { w, d, h } = size;
  const frameH = 0.3;
  const mattressH = 0.22;
  const head = 0.08;
  return (
    <group>
      <RoundedBox args={[w, frameH, d - head]} radius={0.02} position={[0, frameH / 2, head / 2]} castShadow receiveShadow>
        <WoodMaterial color="madera_clara" />
      </RoundedBox>
      <RoundedBox args={[w - 0.04, mattressH, d - head - 0.04]} radius={0.05} position={[0, frameH + mattressH / 2, head / 2]} castShadow>
        <meshStandardMaterial color="#f4f1ea" roughness={0.95} />
      </RoundedBox>
      <RoundedBox args={[w, h, head]} radius={0.03} position={[0, h / 2, -d / 2 + head / 2]} castShadow>
        <FabricMaterial color={color} />
      </RoundedBox>
      {[-1, 1].map((s) => (
        <RoundedBox key={s} args={[w / 2 - 0.12, 0.13, 0.42]} radius={0.06} smoothness={4} position={[s * (w / 4), frameH + mattressH + 0.06, -d / 2 + head + 0.3]} rotation-x={-0.25} castShadow>
          <meshStandardMaterial color="#fbfaf6" roughness={1} />
        </RoundedBox>
      ))}
      <RoundedBox args={[w + 0.02, 0.06, (d - head) * 0.66]} radius={0.03} position={[0, frameH + mattressH + 0.02, d / 2 - (d - head) * 0.33]} castShadow>
        <FabricMaterial color={color} />
      </RoundedBox>
    </group>
  );
}

/** TV de pantalla plana (la pantalla mira a +Z). */
export function Television({ size }: Props) {
  const { w, d, h } = size;
  return (
    <group>
      <RoundedBox args={[w, h, Math.max(0.03, d)]} radius={0.008} position={[0, h / 2, 0]} castShadow>
        <meshStandardMaterial color="#141416" metalness={0.4} roughness={0.35} />
      </RoundedBox>
      <mesh position={[0, h / 2 + 0.005, Math.max(0.03, d) / 2 + 0.001]}>
        <planeGeometry args={[w - 0.025, h - 0.03]} />
        <meshStandardMaterial color="#05070b" roughness={0.08} metalness={0.6} envMapIntensity={1.4} />
      </mesh>
    </group>
  );
}

/** Lampara de piso tipo arco: base de marmol, brazo curvo y pantalla. */
export function ArcLamp({ size, color }: Props) {
  const { w, h } = size;
  const curve = useMemo(
    () =>
      new CatmullRomCurve3([
        new Vector3(0, 0.05, 0),
        new Vector3(0, h * 0.75, 0),
        new Vector3(w * 0.25, h * 0.98, 0),
        new Vector3(w * 0.45, h * 0.88, 0),
      ]),
    [w, h],
  );
  return (
    <group position={[-w * 0.2, 0, 0]}>
      <mesh position={[0, 0.025, 0]} castShadow receiveShadow>
        <cylinderGeometry args={[Math.min(w, size.d) * 0.4, Math.min(w, size.d) * 0.42, 0.05, 32]} />
        <meshStandardMaterial color="#e9e6df" roughness={0.3} />
      </mesh>
      <mesh castShadow>
        <tubeGeometry args={[curve, 48, 0.012, 8, false]} />
        <MetalMaterial color={color} />
      </mesh>
      <mesh position={[w * 0.45, h * 0.84, 0]} castShadow>
        <sphereGeometry args={[0.12, 32, 16, 0, Math.PI * 2, 0, Math.PI / 2]} />
        <meshStandardMaterial color={colorHex(color)} metalness={0.6} roughness={0.35} side={2} />
      </mesh>
      <mesh position={[w * 0.45, h * 0.82, 0]}>
        <sphereGeometry args={[0.03, 16, 8]} />
        <meshStandardMaterial color="#fff3d6" emissive="#ffd9a0" emissiveIntensity={2} />
      </mesh>
      <pointLight position={[w * 0.45, h * 0.78, 0]} intensity={0.6} distance={3.5} color="#ffd9a0" />
    </group>
  );
}

/** Lampara de papel (washi) con base de madera. */
export function PaperLamp({ size }: Props) {
  const { w, d, h } = size;
  const r = Math.min(w, d) / 2;
  return (
    <group>
      {[0, 1, 2, 3].map((i) => {
        const a = (i * Math.PI) / 2 + Math.PI / 4;
        return (
          <mesh key={i} position={[Math.cos(a) * r * 0.7, h * 0.3, Math.sin(a) * r * 0.7]} castShadow>
            <boxGeometry args={[0.025, h * 0.6, 0.025]} />
            <WoodMaterial color="madera_clara" />
          </mesh>
        );
      })}
      <mesh position={[0, h * 0.72, 0]} castShadow>
        <cylinderGeometry args={[r * 0.95, r, h * 0.55, 32]} />
        <meshStandardMaterial color="#f6efe1" emissive="#ffd9a0" emissiveIntensity={0.55} roughness={1} transparent opacity={0.95} />
      </mesh>
      <pointLight position={[0, h * 0.72, 0]} intensity={0.5} distance={3} color="#ffd9a0" />
    </group>
  );
}

/** Alfombra con textura de tela y orilla. */
export function Rug({ size, color }: Props) {
  const { w, d } = size;
  return (
    <group>
      <mesh position={[0, 0.006, 0]} receiveShadow>
        <boxGeometry args={[w, 0.012, d]} />
        <FabricMaterial color={color} />
      </mesh>
      <mesh position={[0, 0.0125, 0]} receiveShadow>
        <boxGeometry args={[w - 0.12, 0.002, d - 0.12]} />
        <meshStandardMaterial color={colorHex(color)} roughness={1} transparent opacity={0.55} />
      </mesh>
    </group>
  );
}

/** Isla de cocina: gabinete y cubierta de piedra con vuelo. */
export function KitchenIsland({ size, color }: Props) {
  const { w, d, h } = size;
  const top = 0.04;
  return (
    <group>
      <RoundedBox args={[w - 0.06, h - top, d - 0.06]} radius={0.01} position={[0, (h - top) / 2, 0]} castShadow receiveShadow>
        <SurfaceMaterial color={color} />
      </RoundedBox>
      <mesh position={[0, h - top / 2, 0]} castShadow receiveShadow>
        <boxGeometry args={[w, top, d]} />
        <meshStandardMaterial color="#eeeae4" roughness={0.25} />
      </mesh>
    </group>
  );
}

function abstractArt(color?: string) {
  const c = document.createElement("canvas");
  c.width = 256;
  c.height = 340;
  const g = c.getContext("2d")!;
  g.fillStyle = "#f3efe7";
  g.fillRect(0, 0, c.width, c.height);
  g.fillStyle = colorHex(color);
  g.beginPath();
  g.arc(90, 130, 70, 0, Math.PI * 2);
  g.fill();
  g.fillStyle = "#c9a27b";
  g.fillRect(120, 170, 110, 120);
  g.strokeStyle = "#2e2e30";
  g.lineWidth = 6;
  g.beginPath();
  g.moveTo(30, 300);
  g.bezierCurveTo(100, 220, 160, 320, 230, 60);
  g.stroke();
  const t = new CanvasTexture(c);
  t.colorSpace = SRGBColorSpace;
  return t;
}

/** Cuadro: marco y lienzo abstracto. El lienzo mira a +Z. */
export function Painting({ size, color }: Props) {
  const { w, d, h } = size;
  const art = useMemo(() => abstractArt(color), [color]);
  return (
    <group>
      <mesh position={[0, h / 2, 0]} castShadow>
        <boxGeometry args={[w, h, d]} />
        <WoodMaterial color="madera_oscura" />
      </mesh>
      <mesh position={[0, h / 2, d / 2 + 0.001]}>
        <planeGeometry args={[w - 0.06, h - 0.06]} />
        <meshStandardMaterial map={art} roughness={0.9} />
      </mesh>
    </group>
  );
}

/** Mesa (comedor o generica): cubierta y 4 patas. */
export function Table({ size, color }: Props) {
  const { w, d, h } = size;
  const top = 0.04;
  return (
    <group>
      <RoundedBox args={[w, top, d]} radius={0.01} position={[0, h - top / 2, 0]} castShadow receiveShadow>
        <SurfaceMaterial color={color ?? "madera_clara"} />
      </RoundedBox>
      {[[-1, -1], [1, -1], [-1, 1], [1, 1]].map(([sx, sz]) => (
        <mesh key={`${sx}${sz}`} position={[sx * (w / 2 - 0.08), (h - top) / 2, sz * (d / 2 - 0.08)]} castShadow>
          <boxGeometry args={[0.05, h - top, 0.05]} />
          <SurfaceMaterial color={color ?? "madera_clara"} />
        </mesh>
      ))}
    </group>
  );
}

/** Silla de comedor: patas de madera, asiento y respaldo tapizados. */
export function DiningChair({ size, color }: Props) {
  const { w, d, h } = size;
  const seatY = 0.46;
  return (
    <group>
      {[[-1, -1], [1, -1], [-1, 1], [1, 1]].map(([sx, sz]) => (
        <mesh key={`${sx}${sz}`} position={[sx * (w / 2 - 0.03), seatY / 2, sz * (d / 2 - 0.03)]} castShadow>
          <boxGeometry args={[0.03, seatY, 0.03]} />
          <WoodMaterial color="madera_clara" />
        </mesh>
      ))}
      <RoundedBox args={[w, 0.06, d]} radius={0.02} position={[0, seatY, 0]} castShadow>
        <FabricMaterial color={color} />
      </RoundedBox>
      <RoundedBox args={[w, h - seatY, 0.05]} radius={0.02} position={[0, seatY + (h - seatY) / 2, -d / 2 + 0.025]} rotation-x={-0.08} castShadow>
        <FabricMaterial color={color} />
      </RoundedBox>
    </group>
  );
}

/** Silla de oficina: base de estrella, piston, asiento, respaldo y brazos. */
export function OfficeChair({ size, color }: Props) {
  const { w, d, h } = size;
  const seatY = 0.48;
  return (
    <group>
      {[0, 1, 2, 3, 4].map((i) => {
        const a = (i * 2 * Math.PI) / 5;
        const r = Math.min(w, d) * 0.42;
        return (
          <mesh key={i} position={[(Math.cos(a) * r) / 2, 0.06, (Math.sin(a) * r) / 2]} rotation-y={-a} castShadow>
            <boxGeometry args={[r, 0.03, 0.04]} />
            <MetalMaterial color="negro" />
          </mesh>
        );
      })}
      <mesh position={[0, (seatY - 0.06) / 2 + 0.06, 0]} castShadow>
        <cylinderGeometry args={[0.025, 0.025, seatY - 0.06, 12]} />
        <MetalMaterial color="gris" />
      </mesh>
      <RoundedBox args={[w * 0.85, 0.08, d * 0.8]} radius={0.03} position={[0, seatY, 0.02]} castShadow>
        <FabricMaterial color={color} />
      </RoundedBox>
      <RoundedBox args={[w * 0.8, h - seatY - 0.05, 0.07]} radius={0.03} position={[0, seatY + (h - seatY) / 2, -d / 2 + 0.06]} rotation-x={-0.1} castShadow>
        <FabricMaterial color={color} />
      </RoundedBox>
      {[-1, 1].map((s) => (
        <mesh key={s} position={[s * (w * 0.43), seatY + 0.15, 0]} castShadow>
          <boxGeometry args={[0.04, 0.03, d * 0.5]} />
          <MetalMaterial color="negro" />
        </mesh>
      ))}
    </group>
  );
}

/** Respaldo generico: caja redondeada con el material elegido. */
export function GenericBox({ size, color, material }: Props) {
  const { w, d, h } = size;
  return (
    <RoundedBox args={[w, h, d]} radius={Math.min(0.03, w / 4, d / 4, h / 4)} position={[0, h / 2, 0]} castShadow receiveShadow>
      {material === "tela" ? <FabricMaterial color={color} /> : material === "metal" ? <MetalMaterial color={color} /> : <SurfaceMaterial color={color} />}
    </RoundedBox>
  );
}
