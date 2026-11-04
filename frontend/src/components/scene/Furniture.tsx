"use client";

import { Suspense, useRef, useState } from "react";
import { Plane, Vector3 } from "three";
import { useThree, type ThreeEvent } from "@react-three/fiber";
import { Edges } from "@react-three/drei";
import type { PlacedObject, Product } from "@/lib/types";
import { cmToM, normalizeRotation, rotatedDims } from "@/lib/units";
import { ModelAsset } from "./furniture/ModelAsset";
import {
  ArcLamp,
  Bed,
  Bookshelf,
  DiningChair,
  GenericBox,
  KitchenIsland,
  OfficeChair,
  Painting,
  PaperLamp,
  Rug,
  Sofa,
  Table,
  Television,
  TvCabinet,
  type Size,
} from "./furniture/Procedural";

const FLOOR = new Plane(new Vector3(0, 1, 0), 0);
// [SUPUESTO] el arrastre se ajusta a 5 cm; con Shift, a 1 cm
const snap = (cm: number, fine: boolean) => (fine ? Math.round(cm) : Math.round(cm / 5) * 5);
// [SUPUESTO] el giro con clic derecho se ajusta a 5 grados; con Shift, a 1 grado
const snapAngle = (deg: number, fine: boolean) => normalizeRotation(fine ? deg : Math.round(deg / 5) * 5);

/** Elige como dibujar el producto: modelo GLB del catalogo (asset3D) o mueble armado en codigo. */
function Model({ product, obj, size }: { product: Product; obj: PlacedObject; size: Size }) {
  const color = obj.config?.color ?? product.colors[0];
  const material = obj.config?.material ?? product.materials[0];
  if (product.asset3D) {
    const tint = obj.config?.color && obj.config.color !== product.colors[0] ? obj.config.color : undefined;
    return <ModelAsset url={product.asset3D} size={size} tint={tint} />;
  }
  const props = { size, color, material };
  switch (product.category) {
    case "sofa":
      return <Sofa {...props} seats={Number(product.attributes?.plazas) || undefined} />;
    case "sillon":
      return <Sofa {...props} seats={1} />;
    case "cama":
      return <Bed {...props} />;
    case "televisor":
      return <Television {...props} />;
    case "lampara":
      return product.materials.includes("tela") ? <PaperLamp {...props} /> : <ArcLamp {...props} />;
    case "alfombra":
      return <Rug {...props} />;
    case "isla":
      return <KitchenIsland {...props} />;
    case "cuadro":
      return <Painting {...props} />;
    case "mesa_comedor":
    case "mesa_centro":
    case "escritorio":
      return <Table {...props} />;
    case "silla_comedor":
      return <DiningChair {...props} />;
    case "silla_oficina":
      return <OfficeChair {...props} />;
    case "mueble_tv":
      return <TvCabinet {...props} />;
    case "librero":
      return <Bookshelf {...props} />;
    default:
      return <GenericBox {...props} />;
  }
}

interface Props {
  obj: PlacedObject;
  product: Product;
  /** Altura extra en cm (la TV va sobre su mueble, el cuadro colgado). Solo visual. */
  elevationCm: number;
  selected: boolean;
  inConflict: boolean;
  pending: boolean;
  onSelect: () => void;
  onBlocked: () => void;
  /** Avisa cuando empieza y termina un arrastre o giro (para pausar la camara). */
  onDragging: (dragging: boolean) => void;
  onDrag: (x: number, y: number) => void;
  /** Giro libre con clic derecho: nuevo angulo (0-359). */
  onRotate: (rotation: number) => void;
}

type Gesture =
  | { kind: "move"; dx: number; dy: number; last: { x: number; y: number } }
  | { kind: "rotate"; cx: number; cy: number; startAngle: number; base: number; last: number };

/**
 * Un mueble en la escena. La huella (lo que valida el motor) es exacta; el modelo se ajusta a ella.
 * Clic izquierdo + arrastrar = mover. Clic derecho + mover en circulo = girar a cualquier angulo.
 * Convencion de frente: rotacion 0 = mira al sur; 90 = este; 180 = norte; 270 = oeste (antihorario visto desde arriba).
 */
export function Furniture({ obj, product, elevationCm, selected, inConflict, pending, onSelect, onBlocked, onDragging, onDrag, onRotate }: Props) {
  const gesture = useRef<Gesture | null>(null);
  const [hovered, setHovered] = useState(false);
  const get = useThree((s) => s.get);

  const { width, depth, height } = product.dimensions;
  const [a, b] = rotatedDims(obj.rotation, width, depth);
  const size: Size = { w: cmToM(width), d: cmToM(depth), h: cmToM(height) };
  const base = cmToM(obj.position.z + elevationCm);
  // Centro de la caja envolvente: el mueble gira alrededor de el
  const cxCm = obj.position.x + a / 2;
  const cyCm = obj.position.y + b / 2;
  const center: [number, number, number] = [cmToM(cxCm), base, -cmToM(cyCm)];

  const floorHit = (e: ThreeEvent<PointerEvent>) => {
    const hit = new Vector3();
    return e.ray.intersectPlane(FLOOR, hit) ? { x: hit.x * 100, y: -hit.z * 100 } : null;
  };

  /** La camara se pausa al instante (antes del siguiente render) para que no se mueva junto con el mueble. */
  const pauseCamera = (paused: boolean) => {
    const controls = get().controls as { enabled: boolean } | null;
    if (controls) controls.enabled = !paused;
    onDragging(paused);
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
    if (e.nativeEvent.button === 2) {
      gesture.current = {
        kind: "rotate",
        cx: cxCm,
        cy: cyCm,
        startAngle: (Math.atan2(p.y - cyCm, p.x - cxCm) * 180) / Math.PI,
        base: obj.rotation,
        last: obj.rotation,
      };
    } else {
      gesture.current = { kind: "move", dx: p.x - obj.position.x, dy: p.y - obj.position.y, last: { x: obj.position.x, y: obj.position.y } };
    }
    pauseCamera(true);
  };

  const handleMove = (e: ThreeEvent<PointerEvent>) => {
    const g = gesture.current;
    if (!g) return;
    const p = floorHit(e);
    if (!p) return;
    const fine = e.nativeEvent.shiftKey;
    if (g.kind === "move") {
      const nx = snap(p.x - g.dx, fine);
      const ny = snap(p.y - g.dy, fine);
      if (nx === g.last.x && ny === g.last.y) return;
      g.last = { x: nx, y: ny };
      onDrag(nx, ny);
    } else {
      // Angulo del cursor alrededor del centro (plano: x este, y norte; positivo = antihorario)
      const angle = (Math.atan2(p.y - g.cy, p.x - g.cx) * 180) / Math.PI;
      const next = snapAngle(g.base + angle - g.startAngle, fine);
      if (next === g.last) return;
      g.last = next;
      onRotate(next);
    }
  };

  const handleUp = (e: ThreeEvent<PointerEvent>) => {
    if (!gesture.current) return;
    gesture.current = null;
    (e.target as Element).releasePointerCapture(e.pointerId);
    pauseCamera(false);
  };

  const outline = inConflict ? "#e03131" : selected ? "#1c7ed6" : pending ? "#f08c00" : hovered ? "#74c0fc" : null;
  const conceptual = product.kind === "conceptual";

  return (
    <group
      position={center}
      onPointerDown={handleDown}
      onPointerMove={handleMove}
      onPointerUp={handleUp}
      onContextMenu={(e) => e.nativeEvent.preventDefault()}
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
      {/* El modelo y su contorno giran juntos; positivo = antihorario visto desde arriba */}
      <group rotation-y={(obj.rotation * Math.PI) / 180}>
        <Suspense fallback={<GenericBox size={size} color={product.colors[0]} />}>
          <Model product={product} obj={obj} size={size} />
        </Suspense>

        {/* Huella real (lo que valida el motor): contorno de seleccion, cambios o conflicto */}
        {(outline || conceptual) && (
          <mesh position={[0, size.h / 2, 0]} raycast={() => null}>
            <boxGeometry args={[size.w + 0.01, size.h + 0.01, size.d + 0.01]} />
            <meshBasicMaterial
              color={inConflict ? "#e03131" : conceptual ? "#fab005" : outline ?? "#000"}
              transparent
              opacity={inConflict ? 0.22 : conceptual ? 0.12 : 0}
              depthWrite={false}
            />
            {outline && <Edges color={outline} lineWidth={selected || inConflict ? 2.5 : 1.5} />}
          </mesh>
        )}
      </group>
    </group>
  );
}
