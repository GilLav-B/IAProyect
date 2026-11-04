"use client";

import { Suspense, useEffect, useMemo, useRef, useState } from "react";
import { Canvas } from "@react-three/fiber";
import { CameraControls, Environment } from "@react-three/drei";
import type { DesignState, PlacedObject, Product } from "@/lib/types";
import { cmToM, rotatedDims } from "@/lib/units";
import { Furniture } from "./Furniture";
import { HDRI, type FloorKind } from "./materials";
import { RoomShell } from "./RoomShell";

export type ViewMode = "3d" | "planta";

interface Props {
  /** Estado a dibujar (lo guardado + el borrador). */
  state: DesignState;
  /** 3d = perspectiva desde la esquina; planta = desde arriba. */
  view: ViewMode;
  /** Tipo de piso (segun el material del catalogo elegido). */
  floorKind?: FloorKind;
  products: Map<string, Product>;
  selectedId: string | null;
  conflictIds: Set<string>;
  pendingIds: Set<string>;
  onSelect: (instanceId: string | null) => void;
  onBlocked: (instanceId: string) => void;
  onDrag: (instanceId: string, x: number, y: number) => void;
  /** Giro libre (clic derecho + mover en circulo). */
  onRotate: (instanceId: string, rotation: number) => void;
}

/** [SUPUESTO] un cuadro sin mueble debajo se cuelga con su centro a 1.50 m. */
const PAINTING_CENTER_CM = 150;

/**
 * Altura visual extra (cm). En diseno.pl la TV y el cuadro "no ocupan piso" (sin_area_piso):
 * la TV se pone sobre el mueble que tiene debajo y el cuadro se cuelga. Solo cambia el dibujo, no la validacion.
 */
function elevations(objects: PlacedObject[], products: Map<string, Product>) {
  const rect = (o: PlacedObject, p: Product) => {
    const [a, b] = rotatedDims(o.rotation, p.dimensions.width, p.dimensions.depth);
    return { x1: o.position.x, y1: o.position.y, x2: o.position.x + a, y2: o.position.y + b };
  };
  const result = new Map<string, number>();
  for (const o of objects) {
    const p = products.get(o.catalogId);
    if (!p || o.position.z > 0) continue;
    if (p.category !== "televisor" && p.category !== "cuadro") continue;
    const r = rect(o, p);
    let top = 0;
    for (const other of objects) {
      const q = products.get(other.catalogId);
      if (!q || other === o || ["televisor", "cuadro", "alfombra"].includes(q.category)) continue;
      const s = rect(other, q);
      if (r.x1 < s.x2 && r.x2 > s.x1 && r.y1 < s.y2 && r.y2 > s.y1) top = Math.max(top, other.position.z + q.dimensions.height);
    }
    // El cuadro se cuelga a su altura; si hay un mueble debajo, queda al menos 10 cm por encima de el
    if (p.category === "cuadro") top = Math.max(top + 10, PAINTING_CENTER_CM - p.dimensions.height / 2);
    result.set(o.instanceId, top);
  }
  return result;
}

/**
 * Camara. Vive DENTRO del Canvas: los hijos del Canvas se montan despues que el componente padre,
 * asi que el efecto que mueve la camara tiene que estar aqui para encontrar los controles.
 */
function CameraRig({ view, L, W, enabled }: { view: ViewMode; L: number; W: number; enabled: boolean }) {
  const controls = useRef<CameraControls>(null);
  const first = useRef(true);
  const span = Math.max(L, W);

  useEffect(() => {
    const c = controls.current;
    if (!c) return;
    const smooth = !first.current;
    first.current = false;
    if (view === "planta") void c.setLookAt(L / 2, span * 1.75, -W / 2 + 0.01, L / 2, 0, -W / 2, smooth);
    else void c.setLookAt(L + span * 0.35, span * 1.0, span * 0.75, L / 2, 0.6, -W / 2, smooth);
  }, [view, L, W, span]);

  return (
    <CameraControls
      ref={controls}
      makeDefault
      enabled={enabled}
      maxPolarAngle={Math.PI / 2 - 0.08}
      minDistance={1.5}
      maxDistance={span * 3}
    />
  );
}

export function RoomScene({ state, view, floorKind, products, selectedId, conflictIds, pendingIds, onSelect, onBlocked, onDrag, onRotate }: Props) {
  const L = cmToM(state.room.length);
  const W = cmToM(state.room.width);
  const H = cmToM(state.room.height);
  const span = Math.max(L, W);
  // Mientras se arrastra un mueble la camara no se mueve
  const [dragging, setDragging] = useState(false);
  // Al soltar un arrastre el navegador manda un "click" que no cae en el mueble: no debe quitar la seleccion
  const dragEndedAt = useRef(0);
  const handleDragging = (value: boolean) => {
    if (!value) dragEndedAt.current = performance.now();
    setDragging(value);
  };
  const lift = useMemo(() => elevations(state.objects, products), [state.objects, products]);

  return (
    // Sin menu contextual: el clic derecho sobre un mueble sirve para girarlo
    <div style={{ width: "100%", height: "100%" }} onContextMenu={(e) => e.preventDefault()}>
    <Canvas
      shadows="percentage"
      dpr={[1, 2]}
      gl={{ toneMappingExposure: 0.82 }}
      // Fuera de la esquina sureste: se ocultan los muros sur y este y se ve el interior
      camera={{ position: [L + span * 0.35, span * 1.0, span * 0.75], fov: 40 }}
      onPointerMissed={() => {
        if (performance.now() - dragEndedAt.current > 300) onSelect(null);
      }}
    >
      <color attach="background" args={["#e7e3dc"]} />
      <fog attach="fog" args={["#e7e3dc", span * 3, span * 6]} />

      {/* Luz: HDRI de interior para reflejos + "sol" calido que entra y proyecta sombras */}
      <Suspense fallback={null}>
        <Environment files={HDRI} environmentIntensity={0.45} />
      </Suspense>
      <ambientLight intensity={0.15} />
      <directionalLight
        position={[L * 0.2, H + 3.5, -W - 2]}
        intensity={1.7}
        color="#fff1dc"
        castShadow
        shadow-mapSize={[2048, 2048]}
        shadow-bias={-0.0004}
        shadow-normalBias={0.02}
        shadow-camera-left={-span}
        shadow-camera-right={span}
        shadow-camera-top={span}
        shadow-camera-bottom={-span}
        shadow-camera-far={span * 6}
      />

      <RoomShell room={state.room} floorKind={floorKind} showZones={dragging || selectedId !== null} />

      {state.objects.map((o) => {
        const product = products.get(o.catalogId);
        if (!product) return null;
        return (
          <Furniture
            key={o.instanceId}
            obj={o}
            product={product}
            elevationCm={lift.get(o.instanceId) ?? 0}
            selected={selectedId === o.instanceId}
            inConflict={conflictIds.has(o.instanceId)}
            pending={pendingIds.has(o.instanceId)}
            onSelect={() => onSelect(o.instanceId)}
            onBlocked={() => onBlocked(o.instanceId)}
            onDragging={handleDragging}
            onDrag={(x, y) => onDrag(o.instanceId, x, y)}
            onRotate={(rotation) => onRotate(o.instanceId, rotation)}
          />
        );
      })}

      <CameraRig view={view} L={L} W={W} enabled={!dragging} />
    </Canvas>
    </div>
  );
}
