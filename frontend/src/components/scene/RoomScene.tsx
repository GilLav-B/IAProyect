"use client";

import { useState } from "react";
import { Canvas } from "@react-three/fiber";
import { OrbitControls } from "@react-three/drei";
import type { DesignState, Product } from "@/lib/types";
import { cmToM } from "@/lib/units";
import { FurnitureBox } from "./FurnitureBox";
import { RoomShell } from "./RoomShell";

interface Props {
  state: DesignState;
  products: Map<string, Product>;
  selectedId: string | null;
  conflictIds: Set<string>;
  preview: { instanceId: string; x: number; y: number } | null;
  onSelect: (instanceId: string | null) => void;
  onDrag: (instanceId: string, x: number, y: number) => void;
  onDrop: (instanceId: string, x: number, y: number) => void;
}

export function RoomScene({ state, products, selectedId, conflictIds, preview, onSelect, onDrag, onDrop }: Props) {
  const L = cmToM(state.room.length);
  const W = cmToM(state.room.width);
  const H = cmToM(state.room.height);
  const center: [number, number, number] = [L / 2, 0, -W / 2];
  const span = Math.max(L, W);
  // Mientras se arrastra un mueble la camara no se mueve
  const [dragging, setDragging] = useState(false);

  return (
    <Canvas
      shadows="percentage"
      dpr={[1, 2]}
      camera={{ position: [L / 2 + span * 0.55, span * 1.05, span * 0.85], fov: 45 }}
      onPointerMissed={() => onSelect(null)}
    >
      <color attach="background" args={["#eceae4"]} />
      <hemisphereLight args={["#ffffff", "#b9b1a3", 1.2]} />
      <directionalLight
        position={[L / 2 + 2, H + 3, 2]}
        intensity={1.6}
        castShadow
        shadow-mapSize={[2048, 2048]}
        shadow-camera-left={-span}
        shadow-camera-right={span}
        shadow-camera-top={span}
        shadow-camera-bottom={-span}
      />

      <RoomShell room={state.room} onFloorClick={() => onSelect(null)} />

      {state.objects.map((o) => {
        const product = products.get(o.catalogId);
        if (!product) return null;
        return (
          <FurnitureBox
            key={o.instanceId}
            obj={o}
            product={product}
            preview={preview?.instanceId === o.instanceId ? preview : undefined}
            selected={selectedId === o.instanceId}
            inConflict={conflictIds.has(o.instanceId)}
            onSelect={() => onSelect(o.instanceId)}
            onDragging={setDragging}
            onDrag={(x, y) => onDrag(o.instanceId, x, y)}
            onDrop={(x, y) => onDrop(o.instanceId, x, y)}
          />
        );
      })}

      <OrbitControls makeDefault enabled={!dragging} target={center} maxPolarAngle={Math.PI / 2 - 0.05} minDistance={1.5} maxDistance={span * 4} />
    </Canvas>
  );
}
