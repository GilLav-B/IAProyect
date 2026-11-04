"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import Link from "next/link";
import { api, ApiError, API_URL } from "@/lib/api";
import { describe } from "@/lib/reasons";
import type { CatalogMaterial, DesignState, Move, MoveBatchResult, Product, Proposal, Reason, ValidationReport } from "@/lib/types";
import { fmtM, fmtMoney, fmtRange, label } from "@/lib/units";
import { AddFurniture } from "./AddFurniture";
import { ConfigPanel } from "./ConfigPanel";
import { Finishes } from "./Finishes";
import { floorKindOf } from "./scene/materials";
import { RoomScene, type ViewMode } from "./scene/RoomScene";
import styles from "./Editor.module.css";

type Message = { kind: "ok" | "error" | "info"; text: string; reasons?: Reason[] };
/** Movimientos sin guardar, por instanceId. */
type Draft = Record<string, { x: number; y: number; rotation: number }>;

const CHECK_DELAY_MS = 150;

function errorText(e: unknown) {
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
  const checkSeq = useRef(0);

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

  const rotate = useCallback(
    (instanceId: string, delta: 90 | -90) => {
      const obj = shown?.objects.find((o) => o.instanceId === instanceId);
      if (!obj) return;
      if (obj.locked) {
        setMessage({ kind: "info", text: `${instanceId} esta bloqueado. Desbloquealo para moverlo o rotarlo.` });
        return;
      }
      updateDraft(instanceId, { rotation: (obj.rotation + delta + 360) % 360 });
    },
    [shown, updateDraft],
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
    }, part === "walls" ? `Muros: ${label(value.color ?? "")}` : "Piso actualizado");

  if (loadError)
    return (
      <main className={styles.center}>
        <p className={styles.error}>{loadError}</p>
        <Link href="/">← Volver</Link>
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
        <Link href="/" className={styles.back}>
          ← Proyectos
        </Link>
        <div>
          <strong>{state.design.name}</strong>
          <span className={styles.muted}>
            {" "}
            · {label(state.room.spaceType)} {fmtM(state.room.length)} × {fmtM(state.room.width)} × {fmtM(state.room.height)}
          </span>
          <span className={styles.chips}>
            <span className={styles.chip}>{label(state.design.style)}</span>
            <span className={styles.chip}>presupuesto {state.design.budgetMode}</span>
            <span className={styles.chip}>rev. {state.design.revision}</span>
          </span>
        </div>
        <span className={shownReport.valid ? styles.badgeOk : styles.badgeBad}>{shownReport.valid ? "Valido" : "Con conflictos"}</span>
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
          onSelect={setSelectedId}
          onBlocked={(id) => setMessage({ kind: "info", text: `${id} esta bloqueado. Desbloquealo para moverlo.` })}
          onDrag={(id, x, y) => updateDraft(id, { x, y })}
        />
        <div className={styles.viewToggle} role="group" aria-label="Vista">
          {(["3d", "planta"] as const).map((v) => (
            <button key={v} type="button" className={view === v ? styles.viewOn : undefined} onClick={() => setView(v)}>
              {v === "3d" ? "3D" : "Planta"}
            </button>
          ))}
        </div>
        <div className={styles.legend}>
          <span className={styles.swDoor} /> zona de puerta <span className={styles.swWindow} /> acceso a ventana
          <span className={styles.swPending} /> sin guardar <span className={styles.swConflict} /> interfiere
        </div>
      </div>

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

        {selected && selectedProduct ? (
          <ConfigPanel
            obj={selected}
            product={selectedProduct}
            models={products.filter((p) => p.category === selectedProduct.category)}
            problems={problems.get(selected.instanceId) ?? []}
            hasDraft={hasDraft}
            busy={busy}
            onRotate={(delta) => rotate(selected.instanceId, delta)}
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
        )}

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
