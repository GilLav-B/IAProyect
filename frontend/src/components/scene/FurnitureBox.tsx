"use client";

import { useRef, useState } from "react";
import { Plane, Vector3 } from "three";
import type { ThreeEvent } from "@react-three/fiber";
import { Edges } from "@react-three/drei";
import type { PlacedObject, Product } from "@/lib/types";
import { cmToM, colorHex, rotatedDims } from "@/lib/units";

const FLOOR = new Plane(new Vector3(0, 1, 0), 0);
// [SUPUESTO] el arrastre se ajusta a 5 cm; con Shift, a 1 cm
const snap = (cm: number, fine: boolean) => (fine ? Math.round(cm) : Math.round(cm / 5) * 5);

interface Props {
  obj: PlacedObject;
  product: Product;
  selected: boolean;
  /** Choca, tapa una puerta, etc. Se pinta de rojo pero se puede seguir moviendo. */
  inConflict: boolean;
  /** Tiene cambios sin guardar. */
  pending: boolean;
  onSelect: () => void;
  /** Se intento arrastrar un objeto bloqueado. */
  onBlocked: () => void;
  /** Avisa cuando empieza y termina el arrastre (para pausar la camara). */
  onDragging: (dragging: boolean) => void;
  onDrag: (x: number, y: number) => void;
}

/**
 * Mueble como caja con las medidas reales del producto (los modelos 3D llegan despues).
 * Se arrastra libremente sobre el piso; el motor revisa en vivo y solo se guarda si todo es valido.
 */
export function FurnitureBox({ obj, product, selected, inConflict, pending, onSelect, onBlocked, onDragging, onDrag }: Props) {
  const drag = useRef<{ dx: number; dy: number; last: { x: number; y: number } } | null>(null);
  const [hovered, setHovered] = useState(false);

  const [a, b] = rotatedDims(obj.rotation, product.dimensions.width, product.dimensions.depth);
  const h = product.dimensions.height;

  // Planta (cm) -> escena (m): X = x, Z = -y. La caja se dibuja con las medidas ya rotadas.
  const position: [number, number, number] = [
    cmToM(obj.position.x + a / 2),
    cmToM(obj.position.z + h / 2),
    -cmToM(obj.position.y + b / 2),
  ];

  const floorHit = (e: ThreeEvent<PointerEvent>) => {
    const hit = new Vector3();
    return e.ray.intersectPlane(FLOOR, hit) ? { x: hit.x * 100, y: -hit.z * 100 } : null;
  };

  const handleDown = (e: ThreeEvent<PointerEvent>) => {
    e.stopPropagation();
    onSelect();
    if (obj.locked) {
      onBlocked();
      return;
    }
    const p = floorHit(e);
    if (!p) return;
    (e.target as Element).setPointerCapture(e.pointerId);
    drag.current = { dx: p.x - obj.position.x, dy: p.y - obj.position.y, last: { x: obj.position.x, y: obj.position.y } };
    onDragging(true);
  };

  const handleMove = (e: ThreeEvent<PointerEvent>) => {
    if (!drag.current) return;
    const p = floorHit(e);
    if (!p) return;
    const fine = e.nativeEvent.shiftKey;
    const nx = snap(p.x - drag.current.dx, fine);
    const ny = snap(p.y - drag.current.dy, fine);
    if (nx === drag.current.last.x && ny === drag.current.last.y) return;
    drag.current.last = { x: nx, y: ny };
    onDrag(nx, ny);
  };

  const handleUp = (e: ThreeEvent<PointerEvent>) => {
    if (!drag.current) return;
    drag.current = null;
    (e.target as Element).releasePointerCapture(e.pointerId);
    onDragging(false);
  };

  const base = colorHex(obj.config?.color ?? product.colors[0]);
  const edge = inConflict ? "#e03131" : selected ? "#1c7ed6" : pending ? "#f08c00" : obj.locked ? "#495057" : hovered ? "#74c0fc" : "#5c5f66";

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
        document.body.style.cursor = obj.locked ? "not-allowed" : "grab";
      }}
      onPointerOut={() => {
        setHovered(false);
        document.body.style.cursor = "";
      }}
    >
      <boxGeometry args={[cmToM(a), cmToM(h), cmToM(b)]} />
      <meshStandardMaterial
        color={base}
        emissive={inConflict ? "#c92a2a" : "#000000"}
        emissiveIntensity={inConflict ? 0.45 : 0}
        transparent={product.kind === "conceptual" || inConflict}
        opacity={product.kind === "conceptual" ? 0.55 : inConflict ? 0.8 : 1}
      />
      <Edges color={edge} lineWidth={selected || inConflict || pending ? 2.5 : 1} />
    </mesh>
  );
}
