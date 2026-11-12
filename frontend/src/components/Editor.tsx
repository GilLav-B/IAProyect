"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import Link from "next/link";
import { useProgress } from "@react-three/drei";
import { api, ApiError, API_URL } from "@/lib/api";
import { describe } from "@/lib/reasons";
import type { BudgetInfo, CatalogMaterial, DesignState, HistoryEntry, Move, MoveBatchResult, Product, Proposal, Reason, Requirement, Saving, ValidationReport } from "@/lib/types";
import { fmtM, fmtMoney, fmtRange, label, rotateKeepingCenter } from "@/lib/units";
import { AddFurniture } from "./AddFurniture";
import { AiPanel } from "./AiPanel";
import { ExportPanel } from "./ExportPanel";
import { BudgetPanel } from "./BudgetPanel";
import { DecisionBox, type Decision } from "./DecisionBox";
import { HistoryPanel } from "./HistoryPanel";
import { RequirementsPanel } from "./RequirementsPanel";
import { VersionTabs } from "./VersionTabs";
import { ConfigPanel } from "./ConfigPanel";
import { Finishes } from "./Finishes";
import { floorKindOf } from "./scene/materials";
import { RoomScene, type SceneCapture, type Shot, type ViewMode } from "./scene/RoomScene";
import { RealisticViews } from "./RealisticViews";
import styles from "./Editor.module.css";

type Message = { kind: "ok" | "error" | "info"; text: string; reasons?: Reason[] };
/** Movimientos sin guardar, por instanceId. */
type Draft = Record<string, { x: number; y: number; rotation: number }>;

const CHECK_DELAY_MS = 150;

/** Iconos de linea de las pestanas (24x24, trazo 1.6). */
const TAB_ICONS: Record<string, string> = {
  diseno: "M4 20h16M6 20V9l6-5 6 5v11M10 20v-6h4v6",
  ia: "M12 3l1.8 4.7L18.5 9.5l-4.7 1.8L12 16l-1.8-4.7L5.5 9.5l4.7-1.8zM18 15l.9 2.1L21 18l-2.1.9L18 21l-.9-2.1L15 18l2.1-.9z",
  presupuesto: "M4 7h16v10H4zM4 11h16M8 15h3",
  requisitos: "M9 6h11M9 12h11M9 18h11M4 6l1 1 2-2M4 12l1 1 2-2M4 18l1 1 2-2",
  historial: "M4 12a8 8 0 1 0 2.3-5.6M4 4v4h4M12 8v4l3 2",
  exportar: "M12 4v11M7 10l5 5 5-5M5 20h14",
};

function TabIcon({ name }: { name: string }) {
  return (
    <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d={TAB_ICONS[name]} />
    </svg>
  );
}

function errorText(e: unknown) {
  if (e instanceof ApiError && e.status === 404) return "Este diseno no existe. Revisa el enlace o abre uno desde tus proyectos.";
  if (e instanceof ApiError) return `La API respondio ${e.status}: ${JSON.stringify(e.body)}`;
  if (e instanceof TypeError) return `No se pudo conectar con la API en ${API_URL}. ¿Esta corriendo el backend?`;
  return String(e);
}

export function Editor({ designId }: { designId: string }) {
  const [state, setState] = useState<DesignState | null>(null);
  const [report, setReport] = useState<ValidationReport | null>(null);
  const [products, setProducts] = useState<Product[]>([]);
  const [materials, setMaterials] = useState<CatalogMaterial[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [draft, setDraft] = useState<Draft>({});
  const [check, setCheck] = useState<{ forDraft: Draft; result: MoveBatchResult } | null>(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<Message | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [view, setView] = useState<ViewMode>("3d");
  const [sideTab, setSideTab] = useState<"diseno" | "ia" | "presupuesto" | "requisitos" | "historial" | "exportar">("diseno");
  const [requirements, setRequirements] = useState<Requirement[]>([]);
  const [budget, setBudget] = useState<BudgetInfo | null>(null);
  /** Sube cuando cambia la habitacion (requisitos/acabados), que no cambia la revision del diseno. */
  const [roomVersion, setRoomVersion] = useState(0);
  const checkSeq = useRef(0);
  const captureRef = useRef<SceneCapture | null>(null);
  const onCaptureReady = useCallback((capture: SceneCapture) => {
    captureRef.current = capture;
  }, []);
  /** Quita la seleccion y espera a que se redibuje, para que la captura no lleve marcas ni zonas. */
  /**
   * Quita la seleccion, espera a que terminen de cargar los modelos 3D (mientras cargan se dibuja una caja: la IA la
   * copiaria) y a que se redibuje, para que la captura no lleve marcas, zonas ni cajas provisionales.
   */
  const clearForCapture = useCallback(async () => {
    setSelectedId(null);
    const until = performance.now() + 20000;
    while (useProgress.getState().active && performance.now() < until) await new Promise((r) => setTimeout(r, 200));
    // Dos cuadros para que se redibuje sin seleccion; si la pestana esta en segundo plano no hay cuadros: maximo 150 ms
    await Promise.race([
      new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))),
      new Promise((r) => setTimeout(r, 150)),
    ]);
  }, []);
  const captureView = useCallback(async (): Promise<Shot | null> => {
    await clearForCapture();
    return captureRef.current ? captureRef.current.current() : null;
  }, [clearForCapture]);
  const captureCorners = useCallback(async (): Promise<Shot[]> => {
    await clearForCapture();
    return captureRef.current ? captureRef.current.corners() : [];
  }, [clearForCapture]);
  const exportGlb = useCallback(async (): Promise<Blob | null> => {
    await clearForCapture();
    return captureRef.current ? captureRef.current.exportGlb() : null;
  }, [clearForCapture]);

  const reload = useCallback(async () => {
    const [s, r] = await Promise.all([api.design(designId), api.validation(designId)]);
    setState(s);
    setReport(r);
  }, [designId]);

  useEffect(() => {
    let cancelled = false;
    Promise.all([api.design(designId), api.validation(designId), api.products(), api.materials()])
      .then(([s, r, p, m]) => {
        if (cancelled) return;
        setState(s);
        setReport(r);
        setProducts(p);
        setMaterials(m);
      })
      .catch((e) => !cancelled && setLoadError(errorText(e)));
    return () => {
      cancelled = true;
    };
  }, [designId]);

  // Requisitos de la habitacion y presupuesto: se recargan con cada guardado o cambio de habitacion
  const roomId = state?.room.id;
  const revision = state?.design.revision;
  useEffect(() => {
    if (!roomId) return;
    let cancelled = false;
    Promise.all([api.room(roomId), api.budget(designId)])
      .then(([room, b]) => {
        if (cancelled) return;
        setRequirements(room.requirements);
        setBudget(b);
      })
      .catch(() => {});
    return () => {
      cancelled = true;
    };
  }, [designId, roomId, revision, roomVersion]);

  const moves: Move[] = useMemo(
    () => Object.entries(draft).map(([instanceId, m]) => ({ instanceId, ...m })),
    [draft],
  );
  const hasDraft = moves.length > 0;

  // Revision en vivo del borrador (sin guardar): el motor dice que interfiere mientras acomodas
  useEffect(() => {
    if (moves.length === 0) return;
    const seq = ++checkSeq.current;
    const timer = setTimeout(() => {
      api
        .checkMoves(designId, moves)
        .then((result) => seq === checkSeq.current && setCheck({ forDraft: draft, result }))
        .catch((e) => seq === checkSeq.current && setMessage({ kind: "error", text: errorText(e) }));
    }, CHECK_DELAY_MS);
    return () => clearTimeout(timer);
  }, [designId, moves, draft]);

  // La revision solo cuenta si corresponde al borrador actual
  const currentCheck = hasDraft && check?.forDraft === draft ? check.result : null;
  const checking = hasDraft && !currentCheck;

  /** Lo guardado + el borrador: es lo que se dibuja. */
  const shown = useMemo<DesignState | null>(() => {
    if (!state) return null;
    if (!hasDraft) return state;
    return {
      ...state,
      objects: state.objects.map((o) => {
        const d = draft[o.instanceId];
        return d ? { ...o, position: { ...o.position, x: d.x, y: d.y }, rotation: d.rotation } : o;
      }),
    };
  }, [state, draft, hasDraft]);

  const productMap = useMemo(() => new Map(products.map((p) => [p.id, p])), [products]);
  const objectIds = useMemo(() => new Set(state?.objects.map((o) => o.instanceId)), [state]);
  const shownReport = currentCheck?.report ?? report;
  const problems = useMemo(() => {
    const map = new Map<string, Reason[]>();
    for (const r of currentCheck?.results ?? []) if (!r.verdict.accepted) map.set(r.instanceId, r.verdict.reasons);
    return map;
  }, [currentCheck]);
  const conflictIds = useMemo(
    () =>
      new Set([
        ...(shownReport?.conflicts.flatMap((c) => c.args).filter((a) => objectIds.has(a)) ?? []),
        ...problems.keys(),
      ]),
    [shownReport, objectIds, problems],
  );
  const pendingIds = useMemo(() => new Set(Object.keys(draft)), [draft]);

  const updateDraft = useCallback(
    (instanceId: string, change: Partial<{ x: number; y: number; rotation: number }>) => {
      const saved = state?.objects.find((o) => o.instanceId === instanceId);
      if (!saved) return;
      setDraft((prev) => {
        const base = prev[instanceId] ?? { x: saved.position.x, y: saved.position.y, rotation: saved.rotation };
        const next = { ...base, ...change };
        const copy = { ...prev };
        // Si regresa a donde estaba guardado, deja de ser un cambio pendiente
        if (next.x === saved.position.x && next.y === saved.position.y && next.rotation === saved.rotation) delete copy[instanceId];
        else copy[instanceId] = next;
        return copy;
      });
    },
    [state],
  );

  /** Gira a cualquier angulo (0-359) sin mover el centro del mueble. */
  const rotateTo = useCallback(
    (instanceId: string, rotation: number) => {
      const obj = shown?.objects.find((o) => o.instanceId === instanceId);
      const product = obj ? productMap.get(obj.catalogId) : undefined;
      if (!obj || !product) return;
      if (obj.locked) {
        setMessage({ kind: "info", text: `${instanceId} esta bloqueado. Desbloquealo para moverlo o rotarlo.` });
        return;
      }
      updateDraft(
        instanceId,
        rotateKeepingCenter(obj.position.x, obj.position.y, obj.rotation, rotation, product.dimensions.width, product.dimensions.depth),
      );
    },
    [shown, productMap, updateDraft],
  );

  const rotate = useCallback(
    (instanceId: string, delta: 90 | -90) => {
      const obj = shown?.objects.find((o) => o.instanceId === instanceId);
      if (obj) rotateTo(instanceId, obj.rotation + delta);
    },
    [shown, rotateTo],
  );

  // Atajos: R rota a la derecha, Shift+R a la izquierda, Esc quita la seleccion
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.target instanceof HTMLSelectElement || e.target instanceof HTMLInputElement) return;
      if (e.key === "Escape") setSelectedId(null);
      if ((e.key === "r" || e.key === "R") && selectedId) rotate(selectedId, e.shiftKey ? -90 : 90);
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [selectedId, rotate]);

  const save = async () => {
    setBusy(true);
    try {
      const res = await api.saveMoves(designId, moves);
      if (res.ok && res.result.state) {
        setState(res.result.state);
        setReport(res.result.report);
        setDraft({});
        setMessage({ kind: "ok", text: `Guardado: ${moves.length} ${moves.length === 1 ? "cambio" : "cambios"}` });
      } else {
        setCheck({ forDraft: draft, result: res.result });
        setMessage({ kind: "error", text: "No se guardo: hay muebles que interfieren (en rojo)" });
      }
    } catch (e) {
      setMessage({ kind: "error", text: errorText(e) });
    } finally {
      setBusy(false);
    }
  };

  /** Cambios que no son de posicion: tambien pasan por el motor y se guardan al momento. */
  const propose = async (proposal: Proposal, okText: string) => {
    setBusy(true);
    try {
      const res = await api.apply(designId, proposal);
      if (res.ok) {
        setState(res.result.state);
        setReport(res.result.report);
        setMessage({ kind: "ok", text: okText });
      } else {
        setMessage({ kind: "error", text: "El motor rechazo el cambio", reasons: res.verdict.reasons });
      }
    } catch (e) {
      setMessage({ kind: "error", text: errorText(e) });
    } finally {
      setBusy(false);
    }
  };

  const run = async (action: () => Promise<unknown>, okText: string) => {
    setBusy(true);
    try {
      await action();
      await reload();
      setMessage({ kind: "ok", text: okText });
    } catch (e) {
      setMessage({ kind: "error", text: errorText(e) });
    } finally {
      setBusy(false);
    }
  };

  /** Agrega un producto del catalogo; el motor elige un lugar libre y valido. */
  const addFurniture = async (catalogId: string) => {
    setBusy(true);
    try {
      const res = await api.addObject(designId, catalogId);
      if (res.ok) {
        const before = new Set(state?.objects.map((o) => o.instanceId));
        const added = res.result.state.objects.find((o) => !before.has(o.instanceId));
        setState(res.result.state);
        setReport(res.result.report);
        if (added) setSelectedId(added.instanceId);
        setMessage({ kind: "ok", text: `Agregado: ${productMap.get(catalogId)?.name}. Arrastralo para acomodarlo.` });
      } else {
        setMessage({ kind: "error", text: "No se pudo agregar", reasons: res.verdict.reasons });
      }
    } catch (e) {
      setMessage({ kind: "error", text: errorText(e) });
    } finally {
      setBusy(false);
    }
  };

  /** Cambia un acabado de la habitacion (GET + PUT de la habitacion completa). */
  const changeFinish = (part: "floor" | "walls", value: { materialId?: string; color?: string }) =>
    void run(async () => {
      const current = await api.room(state!.room.id);
      await api.updateRoom(current.room.id, { ...current, room: { ...current.room, surfaces: { ...current.room.surfaces, [part]: value } } });
      setRoomVersion((v) => v + 1);
    }, part === "walls" ? `Muros: ${label(value.color ?? "")}` : "Piso actualizado");

  /** Guarda los requisitos de la habitacion (aplica a todas sus versiones). */
  const saveRequirements = (next: Requirement[], okText = "Requisitos guardados") =>
    run(async () => {
      const current = await api.room(state!.room.id);
      await api.updateRoom(current.room.id, { room: current.room, requirements: next });
      setRoomVersion((v) => v + 1);
    }, okText);

  const changeDesign = (patch: { budgetMode?: string; style?: string }, okText: string) =>
    run(() => api.patchDesign(designId, patch), okText);

  const revertTo = (entry: HistoryEntry) =>
    run(() => api.revert(designId, entry.id), `Regreso a la revision ${entry.revision ?? ""}`);

  const applySaving = (s: Saving) =>
    void propose({ action: "reemplazar", instanceId: s.instanceId, catalogId: s.suggestedCatalogId }, `Ahorro aplicado: ${s.suggestedName}`);

  /** El usuario decide que requisito gana en un conflicto. */
  const decide = (d: Decision, text: string) => {
    switch (d.kind) {
      case "raise_budget":
        void saveRequirements(
          requirements.map((r) => (r.type === "presupuesto_max" ? { ...r, amount: d.amount } : r)),
          text,
        );
        break;
      case "flexible":
        void changeDesign({ budgetMode: "flexible" }, text);
        break;
      case "show_savings":
        setSideTab("presupuesto");
        break;
      case "add_product":
        void addFurniture(d.catalogId);
        break;
      case "priority":
        void saveRequirements(requirements.map((r) => (r.id === d.requirementId ? { ...r, priority: d.priority } : r)), text);
        break;
      case "style":
        void changeDesign({ style: d.style }, text);
        break;
    }
  };

  if (loadError)
    return (
      <main className={styles.center}>
        <p className={styles.error}>{loadError}</p>
        <Link href="/" className={styles.centerLink}>
          Ir a mis proyectos
        </Link>
      </main>
    );
  if (!state || !shown || !report || !shownReport) return <main className={styles.center}>Cargando diseno…</main>;

  const selected = shown.objects.find((o) => o.instanceId === selectedId) ?? null;
  const selectedProduct = selected ? productMap.get(selected.catalogId) : undefined;
  const canSave = hasDraft && !!currentCheck?.accepted && !busy;
  const floorKind = floorKindOf(materials.find((m) => m.id === state.room.surfaces?.floor?.materialId)?.type);

  return (
    <div className={styles.layout}>
      <header className={styles.topbar}>
        <Link href="/" className={styles.brand} aria-label="IAProyect, ir a mis proyectos">
          IAProyect
        </Link>
        <div className={styles.heading}>
          <nav className={styles.crumbs} aria-label="Ruta">
            <Link href="/">Proyectos</Link>
            <span aria-hidden="true">/</span>
            <span>{state.project.name}</span>
          </nav>
          <h1 className={styles.designTitle}>{state.design.name}</h1>
        </div>
        <span className={styles.chips}>
          <span className={styles.chip}>{label(state.design.style)}</span>
          <span className={styles.chip}>Presupuesto {state.design.budgetMode}</span>
        </span>
        <span className={shownReport.valid ? styles.statusOk : styles.statusBad}>{shownReport.valid ? "Valido" : "Con conflictos"}</span>
      </header>

      <div className={styles.viewport}>
        <RoomScene
          view={view}
          state={shown}
          floorKind={floorKind}
          products={productMap}
          selectedId={selectedId}
          conflictIds={conflictIds}
          pendingIds={pendingIds}
          onSelect={(id) => {
            setSelectedId(id);
            if (id) setSideTab("diseno");
          }}
          onBlocked={(id) => setMessage({ kind: "info", text: `${id} esta bloqueado. Desbloquealo para moverlo.` })}
          onDrag={(id, x, y) => updateDraft(id, { x, y })}
          onRotate={rotateTo}
          onCaptureReady={onCaptureReady}
        />
        <div className={styles.viewToggle} role="group" aria-label="Vista">
          {(["3d", "foto", "planta"] as const).map((v) => (
            <button key={v} type="button" className={view === v ? styles.viewOn : undefined} onClick={() => setView(v)}>
              {v === "3d" ? "3D" : v === "foto" ? "Foto" : "Planta"}
            </button>
          ))}
        </div>
        <RealisticViews
          key={designId}
          designId={designId}
          objectCount={state.objects.length}
          busy={busy || hasDraft}
          capture={captureCorners}
          onError={(text) => setMessage({ kind: "error", text })}
        />
        {/* Cuadro de rotulacion, como en un plano: que cuarto es, medidas, vista y que significa cada color */}
        <dl className={styles.titleBlock}>
          <div>
            <dt>Espacio</dt>
            <dd>{state.room.name}</dd>
          </div>
          <div>
            <dt>Medidas</dt>
            <dd>
              {fmtM(state.room.length)} × {fmtM(state.room.width)}, alto {fmtM(state.room.height)}
            </dd>
          </div>
          <div>
            <dt>Vista</dt>
            <dd>{view === "3d" ? "3D" : view === "foto" ? "Foto" : "Planta"}</dd>
          </div>
          <div>
            <dt>Revision</dt>
            <dd>{state.design.revision}</dd>
          </div>
          <div className={styles.tbLegend}>
            <span><i className={styles.swDoor} /> Zona de puerta</span>
            <span><i className={styles.swWindow} /> Acceso a ventana</span>
            <span><i className={styles.swPending} /> Sin guardar</span>
            <span><i className={styles.swConflict} /> Interfiere</span>
          </div>
        </dl>
      </div>

      <VersionTabs
        projectId={state.project.id}
        roomId={state.room.id}
        currentId={designId}
        revision={state.design.revision}
        busy={busy || hasDraft}
        onError={(text) => setMessage({ kind: "error", text })}
        onRenamed={() => void reload()}
        productName={(id) => productMap.get(id)?.name ?? id}
      />

      <aside className={styles.sidebar}>
        {hasDraft && (
          <section className={currentCheck && !currentCheck.accepted ? styles.draftBad : styles.draftBar}>
            <div>
              <strong>
                {moves.length} {moves.length === 1 ? "cambio sin guardar" : "cambios sin guardar"}
              </strong>
              <div className={styles.muted}>
                {checking
                  ? "Revisando con el motor…"
                  : currentCheck?.accepted
                    ? "Todo bien, se puede guardar"
                    : `${problems.size} ${problems.size === 1 ? "mueble interfiere" : "muebles interfieren"}`}
              </div>
            </div>
            <div className={styles.draftActions}>
              <button type="button" className={styles.primary} disabled={!canSave} onClick={() => void save()}>
                Guardar cambios
              </button>
              <button type="button" disabled={busy} onClick={() => setDraft({})}>
                Descartar
              </button>
            </div>
          </section>
        )}

        {message && (
          <div className={message.kind === "ok" ? styles.msgOk : message.kind === "error" ? styles.msgError : styles.msgInfo}>
            <strong>{message.text}</strong>
            {message.reasons && (
              <ul>
                {message.reasons.map((r) => (
                  <li key={r.term}>{describe(r)}</li>
                ))}
              </ul>
            )}
          </div>
        )}

        <DecisionBox
          conflicts={shownReport.conflicts}
          requirements={requirements}
          budget={budget}
          products={products}
          spaceType={state.room.spaceType}
          busy={busy || hasDraft}
          onDecide={decide}
        />

        <nav className={styles.sideTabs} role="tablist">
          {(
            [
              ["diseno", "Diseno"],
              ["ia", "IA"],
              ["presupuesto", "Costo"],
              ["requisitos", "Requisitos"],
              ["historial", "Historial"],
              ["exportar", "Exportar"],
            ] as const
          ).map(([key, text]) => (
            <button key={key} type="button" role="tab" aria-selected={sideTab === key} className={sideTab === key ? styles.sideTabOn : undefined} onClick={() => setSideTab(key)}>
              <TabIcon name={key} />
              <span>{text}</span>
            </button>
          ))}
        </nav>

        {sideTab === "presupuesto" && (
          <BudgetPanel
            designId={designId}
            revision={state.design.revision + roomVersion * 100000}
            busy={busy || hasDraft}
            onMode={(mode) => void changeDesign({ budgetMode: mode }, `Modo de presupuesto: ${mode}`)}
            onApplySaving={applySaving}
            productName={(id) => productMap.get(id)?.name ?? id}
          />
        )}
        {sideTab === "requisitos" && (
          <RequirementsPanel
            key={roomVersion}
            spaceType={state.room.spaceType}
            requirements={requirements}
            products={products}
            busy={busy}
            onSave={(next) => void saveRequirements(next)}
          />
        )}
        {sideTab === "ia" && (
          <AiPanel
            key={designId}
            designId={designId}
            objectCount={state.objects.length}
            style={state.design.style}
            view={view}
            onView={setView}
            capture={captureView}
            busy={busy || hasDraft}
            onApplied={(applied) => {
              void reload();
              setMessage({ kind: applied > 0 ? "ok" : "info", text: applied > 0 ? `IA: ${applied} cambios aplicados` : "El motor no acepto ninguna propuesta" });
            }}
            onError={(text) => setMessage({ kind: "error", text })}
          />
        )}
        {sideTab === "exportar" && (
          <ExportPanel
            key={designId}
            designId={designId}
            revision={state.design.revision + roomVersion * 100000}
            exportGlb={exportGlb}
            onError={(text) => setMessage({ kind: "error", text })}
          />
        )}
        {sideTab === "historial" && <HistoryPanel designId={designId} revision={state.design.revision} busy={busy || hasDraft} onRevert={(e) => void revertTo(e)} />}

        {sideTab === "diseno" && (selected && selectedProduct ? (
          <ConfigPanel
            obj={selected}
            product={selectedProduct}
            models={products.filter((p) => p.category === selectedProduct.category)}
            problems={problems.get(selected.instanceId) ?? []}
            hasDraft={hasDraft}
            busy={busy}
            onRotate={(delta) => rotate(selected.instanceId, delta)}
            onRotateTo={(deg) => rotateTo(selected.instanceId, deg)}
            onReplace={(catalogId) =>
              void propose({ action: "reemplazar", instanceId: selected.instanceId, catalogId }, `Modelo cambiado a ${productMap.get(catalogId)?.name}`)
            }
            onColor={(color) => void run(() => api.config(designId, selected.instanceId, { color }), `Color: ${label(color)}`)}
            onMaterial={(material) => void run(() => api.config(designId, selected.instanceId, { material }), `Material: ${label(material)}`)}
            onToggleLock={() =>
              void run(
                () => api.lock(designId, selected.instanceId, !selected.locked),
                selected.locked ? `${selected.instanceId} desbloqueado` : `${selected.instanceId} bloqueado`,
              )
            }
            onRemove={() => {
              const id = selected.instanceId;
              void propose({ action: "quitar", instanceId: id }, `Quitado: ${selectedProduct.name}`).then(() => setSelectedId(null));
            }}
          />
        ) : (
          <>
            <section className={styles.card}>
              <h3 className={styles.subtitle}>En el diseno ({shown.objects.length})</h3>
              {shown.objects.length === 0 && <p className={styles.muted}>Todavia no hay muebles. Agrega del catalogo.</p>}
              <ul className={styles.objectList}>
                {shown.objects.map((o) => (
                  <li key={o.instanceId}>
                    <button type="button" onClick={() => setSelectedId(o.instanceId)}>
                      {o.locked ? "🔒 " : ""}
                      {productMap.get(o.catalogId)?.name ?? o.catalogId}
                      <span className={styles.mono}> {o.instanceId}</span>
                    </button>
                  </li>
                ))}
              </ul>
            </section>
            <AddFurniture spaceType={state.room.spaceType} products={products} disabled={busy || hasDraft} onAdd={(id) => void addFurniture(id)} />
            <Finishes room={state.room} materials={materials} disabled={busy || hasDraft} onChange={changeFinish} />
          </>
        ))}

        <section className={styles.card}>
          <h3 className={styles.subtitle}>Validacion del motor{hasDraft ? " (como quedaria)" : ""}</h3>
          {shownReport.conflicts.length === 0 && shownReport.warnings.length === 0 && (
            <p className={styles.muted}>Sin conflictos ni advertencias.</p>
          )}
          {shownReport.conflicts.map((c) => (
            <p key={c.term} className={styles.conflict}>
              ⛔ {describe(c)}
            </p>
          ))}
          {shownReport.warnings.map((w) => (
            <p key={w.term} className={styles.warning}>
              ⚠️ {describe(w)}
            </p>
          ))}
          <p className={styles.total}>
            Total estimado: <strong>{fmtRange(shownReport.budget.total)}</strong>
            {shownReport.budget.effectiveCap !== undefined && <> · tope {fmtMoney(shownReport.budget.effectiveCap)}</>}
          </p>
        </section>
      </aside>
    </div>
  );
}
