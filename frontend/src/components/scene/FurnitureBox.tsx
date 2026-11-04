"use client";

import { useRef, useState } from "react";
import { Plane, Vector3 } from "three";
import type { ThreeEvent } from "@react-three/fiber";
import { Edges } from "@react-three/drei";
import type { PlacedObject, Product } from "@/lib/types";
import { cmToM, colorHex, rotatedDims } from "@/lib/units";

const FLOOR = new Plane(new Vector3(0, 1, 0), 0);
const SNAP_CM = 5; // [SUPUESTO] el arrastre se ajusta a una rejilla de 5 cm

const snap = (cm: number) => Math.round(cm / SNAP_CM) * SNAP_CM;

interface Props {
  obj: PlacedObject;
  product: Product;
  /** Posicion temporal mientras se arrastra (cm). */
  preview?: { x: number; y: number };
  selected: boolean;
  inConflict: boolean;
  onSelect: () => void;
  /** Avisa cuando empieza y termina el arrastre (para pausar la camara). */
  onDragging: (dragging: boolean) => void;
  onDrag: (x: number, y: number) => void;
  onDrop: (x: number, y: number) => void;
}

/**
 * Mueble como caja con las medidas reales del producto (los GLB llegan despues).
 * Se arrastra sobre el piso; al soltar se manda la propuesta "mover" al motor.
 */
export function FurnitureBox({ obj, product, preview, selected, inConflict, onSelect, onDragging, onDrag, onDrop }: Props) {
  const drag = useRef<{ dx: number; dy: number; moved: boolean; last: { x: number; y: number } } | null>(null);
  const [hovered, setHovered] = useState(false);

  const x = preview?.x ?? obj.position.x;
  const y = preview?.y ?? obj.position.y;
  const [a, b] = rotatedDims(obj.rotation, product.dimensions.width, product.dimensions.depth);
  const h = product.dimensions.height;

  // Planta (cm) -> escena (m): X = x, Z = -y. La caja se dibuja con las medidas ya rotadas.
  const position: [number, number, number] = [cmToM(x + a / 2), cmToM(obj.position.z + h / 2), -cmToM(y + b / 2)];

  const floorHit = (e: ThreeEvent<PointerEvent>) => {
    const hit = new Vector3();
    return e.ray.intersectPlane(FLOOR, hit) ? { x: hit.x * 100, y: -hit.z * 100 } : null;
  };

  const handleDown = (e: ThreeEvent<PointerEvent>) => {
    e.stopPropagation();
    onSelect();
    const p = floorHit(e);
    if (!p) return;
    (e.target as Element).setPointerCapture(e.pointerId);
    drag.current = { dx: p.x - obj.position.x, dy: p.y - obj.position.y, moved: false, last: { x: obj.position.x, y: obj.position.y } };
    onDragging(true);
  };

  const handleMove = (e: ThreeEvent<PointerEvent>) => {
    if (!drag.current) return;
    const p = floorHit(e);
    if (!p) return;
    const nx = snap(p.x - drag.current.dx);
    const ny = snap(p.y - drag.current.dy);
    if (nx === drag.current.last.x && ny === drag.current.last.y) return;
    drag.current.moved = true;
    drag.current.last = { x: nx, y: ny };
    onDrag(nx, ny);
  };

  const handleUp = (e: ThreeEvent<PointerEvent>) => {
    const d = drag.current;
    drag.current = null;
    (e.target as Element).releasePointerCapture(e.pointerId);
    onDragging(false);
    if (d?.moved) onDrop(d.last.x, d.last.y);
  };

  const base = colorHex(obj.config?.color ?? product.colors[0]);
  const edge = inConflict ? "#e03131" : selected ? "#1c7ed6" : obj.locked ? "#495057" : hovered ? "#74c0fc" : "#5c5f66";

  return (
    <mesh
      position={position}
      castShadow
      receiveShadow
      onPointerDown={handleDown}
      onPointerMove={handleMove}
      onPointerUp={handleUp}
      onPointerOver={(e) => {
        e.stopPropagation();
        setHovered(true);
      }}
      onPointerOut={() => setHovered(false)}
    >
      <boxGeometry args={[cmToM(a), cmToM(h), cmToM(b)]} />
      <meshStandardMaterial
        color={base}
        emissive={inConflict ? "#c92a2a" : "#000000"}
        emissiveIntensity={inConflict ? 0.35 : 0}
        transparent={product.kind === "conceptual"}
        opacity={product.kind === "conceptual" ? 0.55 : 1}
      />
      <Edges color={edge} lineWidth={selected || inConflict ? 2.5 : 1} />
    </mesh>
  );
}
