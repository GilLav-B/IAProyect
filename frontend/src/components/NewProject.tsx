"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { api, ApiError, API_URL } from "@/lib/api";
import { BUDGET_MODES, RANGES, roomErrors, SPACES_BY_PROJECT, STYLES, WALLS } from "@/lib/rules";
import type { CatalogMaterial, DesignState, Door, Requirement, Room, Wall, Window } from "@/lib/types";
import { colorHex, fmtMoney, label, WALL_COLORS } from "@/lib/units";
import { floorKindOf, FLOORS } from "./scene/materials";
import { RoomScene } from "./scene/RoomScene";
import styles from "./NewProject.module.css";

const shortId = (prefix: string) => `${prefix}_${crypto.randomUUID().replaceAll("-", "").slice(0, 8)}`;
const toM = (cm: number) => (cm / 100).toFixed(2);
const toCm = (m: string) => Math.round(Number(m) * 100);

/** Campo numerico en metros que guarda cm enteros. */
function Meters({ value, onChange, min, max, step = 0.01 }: { value: number; onChange: (cm: number) => void; min?: number; max?: number; step?: number }) {
  return (
    <span className={styles.meters}>
      <input type="number" step={step} min={min} max={max} value={toM(value)} onChange={(e) => onChange(toCm(e.target.value))} />
      <span>m</span>
    </span>
  );
}

function errorText(e: unknown) {
  if (e instanceof ApiError) {
    const body = e.body as { errors?: Record<string, string[]>; title?: string } | string | null;
    if (body && typeof body === "object" && body.errors) return Object.values(body.errors).flat().join(" · ");
    return typeof body === "string" ? body : `La API respondio ${e.status}`;
  }
  if (e instanceof TypeError) return `No se pudo conectar con la API en ${API_URL}. ¿Esta corriendo el backend?`;
  return String(e);
}

/** Crear desde cero: proyecto -> habitacion (medidas, puertas, ventanas, acabados) -> requisitos -> primera version. */
export function NewProject() {
  const router = useRouter();
  const [name, setName] = useState("Mi proyecto");
  const [projectType, setProjectType] = useState<"domestico" | "comercial">("domestico");
  const [room, setRoom] = useState<Room>({
    id: "nueva",
    name: "Sala",
    spaceType: "sala",
    length: 450,
    width: 350,
    height: 260,
    doors: [{ id: "p1", wall: "sur", position: 40, width: 90 }],
    windows: [{ id: "v1", wall: "norte", position: 150, width: 150, sillHeight: 90, height: 120 }],
    columns: [],
    surfaces: { floor: { materialId: "piso_madera_001" }, walls: { color: "blanco_hueso" } },
  });
  const [style, setStyle] = useState<string>("moderno");
  const [budgetMode, setBudgetMode] = useState<string>("ajustar");
  const [budget, setBudget] = useState(30000);
  const [materials, setMaterials] = useState<CatalogMaterial[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api.materials().then(setMaterials).catch((e) => setError(errorText(e)));
  }, []);

  const floorMaterials = materials.filter((m) => m.type in FLOORS);
  const floorKind = floorKindOf(materials.find((m) => m.id === room.surfaces?.floor?.materialId)?.type);
  const errors = useMemo(() => roomErrors(room), [room]);

  const preview: DesignState = useMemo(
    () => ({
      project: { id: "preview", name, type: projectType },
      design: { id: "preview", name: "Vista previa", style, budgetMode, revision: 0 },
      room,
      objects: [],
    }),
    [name, projectType, room, style, budgetMode],
  );

  const update = (patch: Partial<Room>) => setRoom((r) => ({ ...r, ...patch }));
  const setDoor = (i: number, patch: Partial<Door>) =>
    update({ doors: room.doors.map((d, j) => (j === i ? { ...d, ...patch } : d)) });
  const setWindow = (i: number, patch: Partial<Window>) =>
    update({ windows: room.windows.map((w, j) => (j === i ? { ...w, ...patch } : w)) });
  const nextId = (prefix: string, ids: string[]) => {
    for (let n = 1; ; n++) if (!ids.includes(`${prefix}${n}`)) return `${prefix}${n}`;
  };

  const setSurface = (part: "floor" | "walls", value: { materialId?: string; color?: string }) =>
    update({ surfaces: { ...room.surfaces, [part]: value } });

  const create = async () => {
    setBusy(true);
    setError(null);
    try {
      const projectId = shortId("proy");
      const roomId = shortId("h");
      const designId = shortId("d");
      const requirements: Requirement[] = [
        ...(budget > 0 ? [{ id: "r1", priority: "obligatorio" as const, type: "presupuesto_max" as const, amount: budget }] : []),
        { id: "r2", priority: "preferencia", type: "estilo", style },
      ];
      const finalRoom: Room = { ...room, id: roomId };
      await api.createProject({ id: projectId, name, type: projectType });
      await api.createRoom(projectId, { room: finalRoom, requirements });
      await api.createDesign({
        project: { id: projectId, name, type: projectType },
        design: { id: designId, name: "Diseno 1", style, budgetMode, revision: 0 },
        room: finalRoom,
        requirements,
        objects: [],
      });
      router.push(`/designs/${designId}`);
    } catch (e) {
      setError(errorText(e));
      setBusy(false);
    }
  };

  return (
    <div className={styles.layout}>
      <div className={styles.form}>
        <Link href="/" className={styles.back}>
          Proyectos
        </Link>
        <h1>Nuevo proyecto</h1>
        <p className={styles.muted}>Define el espacio real. Despues agregas muebles del catalogo en el editor 3D.</p>

        <section className={styles.card}>
          <h2>1. Proyecto</h2>
          <label className={styles.field}>
            Nombre
            <input value={name} onChange={(e) => setName(e.target.value)} />
          </label>
          <div className={styles.field}>
            Tipo
            <div className={styles.segmented}>
              {(["domestico", "comercial"] as const).map((t) => (
                <button
                  key={t}
                  type="button"
                  className={projectType === t ? styles.on : undefined}
                  onClick={() => {
                    setProjectType(t);
                    const space = SPACES_BY_PROJECT[t][0];
                    update({ spaceType: space, name: label(space).replace(/^./, (c) => c.toUpperCase()) });
                  }}
                >
                  {label(t)}
                </button>
              ))}
            </div>
          </div>
        </section>

        <section className={styles.card}>
          <h2>2. Habitacion</h2>
          <div className={styles.field}>
            Espacio
            <div className={styles.segmented}>
              {SPACES_BY_PROJECT[projectType].map((s) => (
                <button
                  key={s}
                  type="button"
                  className={room.spaceType === s ? styles.on : undefined}
                  onClick={() => update({ spaceType: s, name: label(s).replace(/^./, (c) => c.toUpperCase()) })}
                >
                  {label(s)}
                </button>
              ))}
            </div>
          </div>
          <div className={styles.row3}>
            <label className={styles.field}>
              Largo
              <Meters value={room.length} min={RANGES.side.min / 100} max={RANGES.side.max / 100} onChange={(length) => update({ length })} />
            </label>
            <label className={styles.field}>
              Ancho
              <Meters value={room.width} min={RANGES.side.min / 100} max={RANGES.side.max / 100} onChange={(width) => update({ width })} />
            </label>
            <label className={styles.field}>
              Alto
              <Meters value={room.height} min={RANGES.height.min / 100} max={RANGES.height.max / 100} onChange={(height) => update({ height })} />
            </label>
          </div>
          <p className={styles.hint}>
            Largo y ancho entre 1.50 y 20 m; alto entre 2 y 6 m. Area: {((room.length * room.width) / 10000).toFixed(2)} m².
          </p>

          <h3>Puertas</h3>
          {room.doors.map((d, i) => (
            <div key={d.id} className={styles.opening}>
              <code>{d.id}</code>
              <select value={d.wall} onChange={(e) => setDoor(i, { wall: e.target.value as Wall })}>
                {WALLS.map((w) => (
                  <option key={w}>{w}</option>
                ))}
              </select>
              <label>
                desde <Meters value={d.position} onChange={(position) => setDoor(i, { position })} />
              </label>
              <label>
                ancho <Meters value={d.width} onChange={(width) => setDoor(i, { width })} />
              </label>
              <button type="button" className={styles.remove} onClick={() => update({ doors: room.doors.filter((_, j) => j !== i) })}>
                Quitar
              </button>
            </div>
          ))}
          <button
            type="button"
            className={styles.add}
            onClick={() => update({ doors: [...room.doors, { id: nextId("p", room.doors.map((d) => d.id)), wall: "sur", position: 0, width: 90 }] })}
          >
            + Puerta
          </button>

          <h3>Ventanas</h3>
          {room.windows.map((w, i) => (
            <div key={w.id} className={styles.opening}>
              <code>{w.id}</code>
              <select value={w.wall} onChange={(e) => setWindow(i, { wall: e.target.value as Wall })}>
                {WALLS.map((wall) => (
                  <option key={wall}>{wall}</option>
                ))}
              </select>
              <label>
                desde <Meters value={w.position} onChange={(position) => setWindow(i, { position })} />
              </label>
              <label>
                ancho <Meters value={w.width} onChange={(width) => setWindow(i, { width })} />
              </label>
              <label>
                antepecho <Meters value={w.sillHeight} onChange={(sillHeight) => setWindow(i, { sillHeight })} />
              </label>
              <button type="button" className={styles.remove} onClick={() => update({ windows: room.windows.filter((_, j) => j !== i) })}>
                Quitar
              </button>
            </div>
          ))}
          <button
            type="button"
            className={styles.add}
            onClick={() =>
              update({
                windows: [...room.windows, { id: nextId("v", room.windows.map((w) => w.id)), wall: "este", position: 0, width: 120, sillHeight: 90, height: 120 }],
              })
            }
          >
            + Ventana
          </button>
          <p className={styles.hint}>&quot;desde&quot; = distancia desde la esquina oeste (paredes norte/sur) o sur (paredes este/oeste).</p>
        </section>

        <section className={styles.card}>
          <h2>3. Acabados actuales</h2>
          <div className={styles.field}>
            Color de muros
            <div className={styles.swatches}>
              {WALL_COLORS.map((c) => (
                <button
                  key={c}
                  type="button"
                  title={label(c)}
                  aria-pressed={room.surfaces?.walls?.color === c}
                  className={room.surfaces?.walls?.color === c ? styles.swatchOn : styles.swatch}
                  style={{ background: colorHex(c) }}
                  onClick={() => setSurface("walls", { color: c })}
                />
              ))}
              <span className={styles.muted}>{label(room.surfaces?.walls?.color ?? "")}</span>
            </div>
          </div>
          <div className={styles.field}>
            Piso
            <div className={styles.segmented}>
              {floorMaterials.map((m) => (
                <button
                  key={m.id}
                  type="button"
                  className={room.surfaces?.floor?.materialId === m.id ? styles.on : undefined}
                  onClick={() => setSurface("floor", { materialId: m.id })}
                >
                  {m.name}
                </button>
              ))}
            </div>
          </div>
        </section>

        <section className={styles.card}>
          <h2>4. Requisitos</h2>
          <div className={styles.row3}>
            <label className={styles.field}>
              Presupuesto maximo
              <span className={styles.meters}>
                <input type="number" min={0} step={500} value={budget} onChange={(e) => setBudget(Number(e.target.value))} />
                <span>MXN</span>
              </span>
            </label>
            <label className={styles.field}>
              Modo
              <select value={budgetMode} onChange={(e) => setBudgetMode(e.target.value)}>
                {BUDGET_MODES.map((m) => (
                  <option key={m}>{m}</option>
                ))}
              </select>
            </label>
            <label className={styles.field}>
              Estilo
              <select value={style} onChange={(e) => setStyle(e.target.value)}>
                {STYLES.map((s) => (
                  <option key={s}>{s}</option>
                ))}
              </select>
            </label>
          </div>
          <p className={styles.hint}>
            Presupuesto {budget > 0 ? fmtMoney(budget) : "sin tope"} (obligatorio) · estilo {style} (preferencia).
          </p>
        </section>

        {errors.length > 0 && (
          <div className={styles.errors}>
            {errors.map((e) => (
              <p key={e}>⛔ {e}</p>
            ))}
          </div>
        )}
        {error && <div className={styles.errors}>⛔ {error}</div>}

        <button type="button" className={styles.create} disabled={busy || errors.length > 0 || !name.trim()} onClick={() => void create()}>
          {busy ? "Creando…" : "Crear y abrir en el editor 3D"}
        </button>
      </div>

      <div className={styles.preview}>
        <RoomScene
          view="3d"
          state={preview}
          floorKind={floorKind}
          products={new Map()}
          selectedId={null}
          conflictIds={new Set()}
          pendingIds={new Set()}
          onSelect={() => {}}
          onBlocked={() => {}}
          onDrag={() => {}}
          onRotate={() => {}}
        />
        <div className={styles.previewLabel}>
          Vista previa · {(room.length / 100).toFixed(2)} × {(room.width / 100).toFixed(2)} × {(room.height / 100).toFixed(2)} m
        </div>
      </div>
    </div>
  );
}
