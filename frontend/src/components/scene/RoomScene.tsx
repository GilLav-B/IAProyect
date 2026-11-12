"use client";

import { Suspense, useEffect, useMemo, useRef, useState, type RefObject } from "react";
import { Canvas, useThree } from "@react-three/fiber";
import { Object3D, Vector3, type PerspectiveCamera } from "three";
import { CameraControls, Environment } from "@react-three/drei";
import { Bloom, EffectComposer, SSAO, ToneMapping, Vignette } from "@react-three/postprocessing";
import { ToneMappingMode, type EffectComposer as EffectComposerImpl } from "postprocessing";
import type { DesignState, PlacedObject, Product, Room } from "@/lib/types";
import { cmToM, rotatedDims } from "@/lib/units";
import { Furniture } from "./Furniture";
import { HDRI, type FloorKind } from "./materials";
import { RoomShell } from "./RoomShell";

export type ViewMode = "3d" | "planta" | "foto";

/** Mueble que aparece en una captura y en que parte del cuadro (para que la IA no invente ni acomode distinto). */
export interface VisibleItem {
  instanceId: string;
  where: string;
}

/** Una captura JPEG del visor y los muebles que se ven en ella. */
export interface Shot {
  image: Blob | null;
  visible: VisibleItem[];
}

/** Capturas del visor para el render con IA. */
export interface SceneCapture {
  /** Lo que se ve ahora. */
  current: () => Promise<Shot>;
  /** 4 vistas a la altura de los ojos desde las esquinas (suroeste, sureste, noreste, noroeste). */
  corners: () => Promise<Shot[]>;
  /** Modelo 3D de la escena en GLB (cuarto y muebles; cada mueble con su instanceId). */
  exportGlb: () => Promise<Blob>;
}

/** Caja de cada mueble en metros (coordenadas de la escena), para saber si sale en una captura. */
interface ItemBox {
  instanceId: string;
  min: [number, number, number];
  max: [number, number, number];
}

function itemBoxes(objects: PlacedObject[], products: Map<string, Product>, lift: Map<string, number>): ItemBox[] {
  return objects.flatMap((o) => {
    const p = products.get(o.catalogId);
    if (!p) return [];
    const [a, b] = rotatedDims(o.rotation, p.dimensions.width, p.dimensions.depth);
    const z0 = cmToM(o.position.z + (lift.get(o.instanceId) ?? 0));
    return [{
      instanceId: o.instanceId,
      min: [cmToM(o.position.x), z0, -cmToM(o.position.y + b)] as [number, number, number],
      max: [cmToM(o.position.x + a), z0 + cmToM(p.dimensions.height), -cmToM(o.position.y)] as [number, number, number],
    }];
  });
}

/**
 * Que muebles salen en lo que ve la camara y donde: izquierda/centro/derecha, cerca/fondo, completo o cortado.
 * Se revisan las 8 esquinas y el centro de la caja de cada mueble (no detecta si otro mueble lo tapa).
 */
function visibleItems(cam: PerspectiveCamera, boxes: ItemBox[]): VisibleItem[] {
  cam.updateMatrixWorld();
  const v = new Vector3();
  const inFrame = (x: number, y: number, z: number) => {
    v.set(x, y, z);
    if (v.clone().applyMatrix4(cam.matrixWorldInverse).z >= -0.05) return false; // detras de la camara
    v.project(cam);
    return Math.abs(v.x) <= 1 && Math.abs(v.y) <= 1;
  };
  return boxes.flatMap((b) => {
    const [x0, y0, z0] = b.min;
    const [x1, y1, z1] = b.max;
    const corners = [x0, x1].flatMap((x) => [y0, y1].flatMap((y) => [z0, z1].map((z) => [x, y, z] as const)));
    const seen = corners.filter(([x, y, z]) => inFrame(x, y, z)).length;
    const center: [number, number, number] = [(x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2];
    const centerIn = inFrame(...center);
    if (seen === 0 && !centerIn) return [];
    v.set(...center).project(cam);
    const side = v.x < -0.33 ? "a la izquierda" : v.x > 0.33 ? "a la derecha" : "al centro";
    const dist = cam.position.distanceTo(new Vector3(...center));
    const depth = dist < 1.8 ? "cerca de la camara" : dist < 3.5 ? "a media distancia" : "al fondo";
    const cut = seen < 8 || !centerIn ? ", cortado por el borde del cuadro" : "";
    return [{ instanceId: b.instanceId, where: `${side}, ${depth}${cut}` }];
  });
}

/** Nombre de cada vista de corners(), en el mismo orden. */
export const CORNER_VIEWS = ["Esquina suroeste", "Esquina sureste", "Esquina noreste", "Esquina noroeste"];

interface Props {
  /** Estado a dibujar (lo guardado + el borrador). */
  state: DesignState;
  /** 3d = perspectiva desde la esquina; planta = desde arriba; foto = a la altura de los ojos, dentro del cuarto. */
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
  /** Recibe las funciones para capturar el visor. */
  onCaptureReady?: (capture: SceneCapture) => void;
}

/** Altura de la camara en la vista "foto" (m): ojos de una persona de pie. */
const EYE_HEIGHT_M = 1.55;

/**
 * De donde entra el sol: desde afuera de la primera ventana, alto, hacia el centro del cuarto.
 * Sin ventanas, desde el noroeste. Devuelve la posicion de la luz (m, coordenadas de la escena).
 */
function sunPosition(room: Room): [number, number, number] {
  const L = cmToM(room.length);
  const W = cmToM(room.width);
  const H = cmToM(room.height);
  const win = room.windows[0];
  if (!win) return [L * 0.2, H + 3.5, -W - 2];
  const along = cmToM(win.position + win.width / 2);
  const away = 4; // que tan lejos de la pared esta el "sol"
  switch (win.wall) {
    case "sur":
      return [along + 1, H + 2.5, away];
    case "norte":
      return [along + 1, H + 2.5, -W - away];
    case "oeste":
      return [-away, H + 2.5, -along - 1];
    case "este":
      return [L + away, H + 2.5, -along - 1];
  }
}

/** Lamparas del diseno: una luz calida en la parte de arriba de cada una (solo se ve, no se valida). */
function lampLights(objects: PlacedObject[], products: Map<string, Product>) {
  return objects.flatMap((o) => {
    const p = products.get(o.catalogId);
    if (!p || p.category !== "lampara") return [];
    const [a, b] = rotatedDims(o.rotation, p.dimensions.width, p.dimensions.depth);
    const top = cmToM(o.position.z + p.dimensions.height) - 0.15;
    return [{ id: o.instanceId, position: [cmToM(o.position.x + a / 2), top, -cmToM(o.position.y + b / 2)] as [number, number, number] }];
  });
}

/** Techo: plano que mira hacia abajo. Desde arriba (vista 3D y planta) no se dibuja; desde dentro (Foto) si. */
function Ceiling({ L, W, H }: { L: number; W: number; H: number }) {
  return (
    <mesh position={[L / 2, H - 0.002, -W / 2]} rotation-x={Math.PI / 2} receiveShadow raycast={() => null}>
      <planeGeometry args={[L, W]} />
      {/* Casi no le pega luz directa: un poco de emision para que no salga oscuro (se veia gris/negro en la vista Foto) */}
      <meshStandardMaterial color="#f7f5f0" emissive="#f7f5f0" emissiveIntensity={0.35} roughness={0.95} />
    </mesh>
  );
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
    // Lente mas abierto dentro del cuarto, como una foto de interiores
    const cam = c.camera as PerspectiveCamera;
    cam.fov = view === "foto" ? 62 : 40;
    cam.updateProjectionMatrix();
    if (view === "planta") void c.setLookAt(L / 2, span * 1.75, -W / 2 + 0.01, L / 2, 0, -W / 2, smooth);
    // Desde la esquina sureste, dentro del cuarto, mirando hacia la esquina noroeste
    else if (view === "foto") void c.setLookAt(L - 0.3, EYE_HEIGHT_M, -0.3, L * 0.35, 0.55, -W * 0.6, smooth);
    else void c.setLookAt(L + span * 0.35, span * 1.0, span * 0.75, L / 2, 0.6, -W / 2, smooth);
  }, [view, L, W, span]);

  return (
    <CameraControls
      ref={controls}
      makeDefault
      enabled={enabled}
      maxPolarAngle={Math.PI / 2 - 0.08}
      minDistance={view === "foto" ? 0.5 : 1.5}
      maxDistance={span * 3}
    />
  );
}

/** Distancia de la camara a los muros en las vistas de esquina (m). */
const CORNER_INSET_M = 0.35;

/**
 * Expone las capturas del visor. Cada cuadro se dibuja y se lee del canvas en el mismo momento (sin preserveDrawingBuffer).
 * Se dibuja con los efectos (oclusion, tono) para que la captura se vea igual que el visor.
 * Las vistas de esquina mueven la camara, capturan y la regresan antes del siguiente cuadro: el usuario no ve el salto.
 */
function CaptureBridge({ L, W, boxes, composer, onReady }: { L: number; W: number; boxes: ItemBox[]; composer: RefObject<EffectComposerImpl | null>; onReady: (capture: SceneCapture) => void }) {
  const gl = useThree((s) => s.gl);
  const scene = useThree((s) => s.scene);
  const camera = useThree((s) => s.camera);
  useEffect(() => {
    const toBlob = async (dataUrl: string) => (dataUrl.startsWith("data:image") ? (await fetch(dataUrl)).blob() : null);
    const shot = () => {
      if (composer.current) composer.current.render();
      else gl.render(scene, camera);
      return { dataUrl: gl.domElement.toDataURL("image/jpeg", 0.92), visible: visibleItems(camera as PerspectiveCamera, boxes) };
    };
    const toShot = async (s: { dataUrl: string; visible: VisibleItem[] }): Promise<Shot> => ({ image: await toBlob(s.dataUrl), visible: s.visible });
    onReady({
      exportGlb: async () => {
        const { GLTFExporter } = await import("three/examples/jsm/exporters/GLTFExporter.js");
        const result = await new GLTFExporter().parseAsync(scene, { binary: true, onlyVisible: true });
        return new Blob([result as ArrayBuffer], { type: "model/gltf-binary" });
      },
      current: () => toShot(shot()),
      corners: () => {
        const cam = camera as PerspectiveCamera;
        const saved = { position: cam.position.clone(), quaternion: cam.quaternion.clone(), fov: cam.fov };
        const i = CORNER_INSET_M;
        // Mismo orden que CORNER_VIEWS. Eje z de la escena = -y del plano (norte = -W).
        const points: [number, number][] = [[i, -i], [L - i, -i], [L - i, -W + i], [i, -W + i]];
        const shots = points.map(([x, z]) => {
          cam.fov = 62;
          cam.updateProjectionMatrix();
          cam.position.set(x, EYE_HEIGHT_M, z);
          // Hacia el centro del cuarto, un poco mas alla, mirando ligeramente hacia abajo
          cam.lookAt(L / 2 + (L / 2 - x) * 0.25, 0.7, -W / 2 + (-W / 2 - z) * 0.25);
          return shot();
        });
        cam.position.copy(saved.position);
        cam.quaternion.copy(saved.quaternion);
        cam.fov = saved.fov;
        cam.updateProjectionMatrix();
        shot();
        return Promise.all(shots.map(toShot));
      },
    });
  }, [gl, scene, camera, L, W, boxes, composer, onReady]);
  return null;
}

export function RoomScene({ state, view, floorKind, products, selectedId, conflictIds, pendingIds, onSelect, onBlocked, onDrag, onRotate, onCaptureReady }: Props) {
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
  const lamps = useMemo(() => lampLights(state.objects, products), [state.objects, products]);
  const boxes = useMemo(() => itemBoxes(state.objects, products, lift), [state.objects, products, lift]);
  const sun = useMemo(() => sunPosition(state.room), [state.room]);
  // El sol apunta al centro del cuarto (el target tiene que estar en la escena para que cuente)
  const sunTarget = useMemo(() => new Object3D(), []);
  const composer = useRef<EffectComposerImpl | null>(null);

  return (
    // Sin menu contextual: el clic derecho sobre un mueble sirve para girarlo
    <div style={{ width: "100%", height: "100%" }} onContextMenu={(e) => e.preventDefault()}>
    <Canvas
      shadows="percentage"
      dpr={[1, 2]}
      // El tono (ACES, tipo camara) lo pone el postprocesado; aqui solo la exposicion
      gl={{ toneMappingExposure: 0.95 }}
      // Fuera de la esquina sureste: se ocultan los muros sur y este y se ve el interior
      camera={{ position: [L + span * 0.35, span * 1.0, span * 0.75], fov: 40 }}
      onPointerMissed={() => {
        if (performance.now() - dragEndedAt.current > 300) onSelect(null);
      }}
    >
      <color attach="background" args={["#e3e6e0"]} />
      <fog attach="fog" args={["#e3e6e0", span * 3, span * 6]} />

      {/* Luz: HDRI de interior para reflejos + "sol" calido que entra por la ventana y proyecta sombras */}
      <Suspense fallback={null}>
        <Environment files={HDRI} environmentIntensity={0.5} />
      </Suspense>
      <ambientLight intensity={0.12} />
      <primitive object={sunTarget} position={[L / 2, 0, -W / 2]} />
      <directionalLight
        position={sun}
        target={sunTarget}
        intensity={2.8}
        color="#fff0d9"
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
      <Ceiling L={L} W={W} H={H} />
      {lamps.map((l) => (
        <pointLight key={l.id} position={l.position} intensity={2.5} distance={5} decay={2} color="#ffd8a8" />
      ))}

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
      {onCaptureReady && <CaptureBridge L={L} W={W} boxes={boxes} composer={composer} onReady={onCaptureReady} />}

      {/* Postprocesado: sombras suaves en esquinas y bajo los muebles (SSAO), brillo de ventana/lamparas y tono de camara.
          N8AO no se usa: su version empaquetada no es compatible con three 0.187. */}
      <EffectComposer ref={composer} multisampling={4} enableNormalPass>
        <SSAO samples={16} rings={4} radius={0.08} intensity={1.6} luminanceInfluence={0.6} bias={0.035} worldDistanceThreshold={25} worldDistanceFalloff={5} worldProximityThreshold={0.6} worldProximityFalloff={0.4} />
        <Bloom luminanceThreshold={0.92} luminanceSmoothing={0.2} intensity={0.35} mipmapBlur />
        <ToneMapping mode={ToneMappingMode.ACES_FILMIC} />
        <Vignette offset={0.3} darkness={0.35} />
      </EffectComposer>
    </Canvas>
    </div>
  );
}
