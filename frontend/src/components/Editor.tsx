"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { api, ApiError, API_URL } from "@/lib/api";
import { describe } from "@/lib/reasons";
import type { DesignState, Product, Proposal, Reason, ValidationReport } from "@/lib/types";
import { fmtM, fmtMoney, fmtRange, label } from "@/lib/units";
import { ConfigPanel } from "./ConfigPanel";
import { RoomScene } from "./scene/RoomScene";
import styles from "./Editor.module.css";

type Message = { kind: "ok" | "error" | "info"; text: string; reasons?: Reason[] };

function errorText(e: unknown) {
  if (e instanceof ApiError) return `La API respondio ${e.status}: ${JSON.stringify(e.body)}`;
  if (e instanceof TypeError) return `No se pudo conectar con la API en ${API_URL}. ¿Esta corriendo el backend?`;
  return String(e);
}

export function Editor({ designId }: { designId: string }) {
  const [state, setState] = useState<DesignState | null>(null);
  const [report, setReport] = useState<ValidationReport | null>(null);
  const [products, setProducts] = useState<Product[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [preview, setPreview] = useState<{ instanceId: string; x: number; y: number } | null>(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<Message | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);

  const reload = useCallback(async () => {
    const [s, r] = await Promise.all([api.design(designId), api.validation(designId)]);
    setState(s);
    setReport(r);
  }, [designId]);

  useEffect(() => {
    let cancelled = false;
    Promise.all([api.design(designId), api.validation(designId), api.products()])
      .then(([s, r, p]) => {
        if (cancelled) return;
        setState(s);
        setReport(r);
        setProducts(p);
      })
      .catch((e) => !cancelled && setLoadError(errorText(e)));
    return () => {
      cancelled = true;
    };
  }, [designId]);

  const productMap = useMemo(() => new Map(products.map((p) => [p.id, p])), [products]);
  const objectIds = useMemo(() => new Set(state?.objects.map((o) => o.instanceId)), [state]);
  const conflictIds = useMemo(
    () => new Set(report?.conflicts.flatMap((c) => c.args).filter((a) => objectIds.has(a))),
    [report, objectIds],
  );

  /** Toda edicion pasa por el motor: valida y, solo si se acepta, se aplica. */
  const propose = useCallback(
    async (proposal: Proposal, okText: string) => {
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
        setPreview(null);
        setBusy(false);
      }
    },
    [designId],
  );

  const run = useCallback(
    async (action: () => Promise<unknown>, okText: string) => {
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
    },
    [reload],
  );

  if (loadError)
    return (
      <main className={styles.center}>
        <p className={styles.error}>{loadError}</p>
        <Link href="/">← Volver</Link>
      </main>
    );
  if (!state || !report) return <main className={styles.center}>Cargando diseno…</main>;

  const selected = state.objects.find((o) => o.instanceId === selectedId) ?? null;
  const selectedProduct = selected ? productMap.get(selected.catalogId) : undefined;

  const handleDrop = (instanceId: string, x: number, y: number) => {
    const obj = state.objects.find((o) => o.instanceId === instanceId)!;
    if (obj.position.x === x && obj.position.y === y) {
      setPreview(null);
      return;
    }
    void propose(
      { action: "mover", instanceId, x, y, rotation: obj.rotation },
      `${instanceId} movido a x ${fmtM(x)}, y ${fmtM(y)}`,
    );
  };

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
            · {label(state.room.spaceType)} {fmtM(state.room.length)} × {fmtM(state.room.width)} × {fmtM(state.room.height)} ·
            estilo {state.design.style} · revision {state.design.revision}
          </span>
        </div>
        <span className={report.valid ? styles.badgeOk : styles.badgeBad}>{report.valid ? "Valido" : "Con conflictos"}</span>
      </header>

      <div className={styles.viewport}>
        <RoomScene
          state={state}
          products={productMap}
          selectedId={selectedId}
          conflictIds={conflictIds}
          preview={preview}
          onSelect={setSelectedId}
          onDrag={(instanceId, x, y) => setPreview({ instanceId, x, y })}
          onDrop={handleDrop}
        />
        {preview && (
          <div className={styles.dragInfo}>
            {preview.instanceId}: x {fmtM(preview.x)}, y {fmtM(preview.y)}
          </div>
        )}
        <div className={styles.legend}>
          <span className={styles.swDoor} /> zona de puerta <span className={styles.swWindow} /> acceso a ventana
          <span className={styles.swConflict} /> en conflicto
        </div>
      </div>

      <aside className={styles.sidebar}>
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
            busy={busy}
            onRotate={() =>
              void propose(
                { action: "mover", instanceId: selected.instanceId, x: selected.position.x, y: selected.position.y, rotation: (selected.rotation + 90) % 360 },
                `${selected.instanceId} rotado a ${(selected.rotation + 90) % 360}°`,
              )
            }
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
          />
        ) : (
          <section className={styles.card}>
            <p className={styles.muted}>Haz clic en un mueble para seleccionarlo.</p>
            <ul className={styles.objectList}>
              {state.objects.map((o) => (
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
        )}

        <section className={styles.card}>
          <h3 className={styles.subtitle}>Validacion del motor</h3>
          {report.conflicts.length === 0 && report.warnings.length === 0 && <p className={styles.muted}>Sin conflictos ni advertencias.</p>}
          {report.conflicts.map((c) => (
            <p key={c.term} className={styles.conflict}>
              ⛔ {describe(c)}
            </p>
          ))}
          {report.warnings.map((w) => (
            <p key={w.term} className={styles.warning}>
              ⚠️ {describe(w)}
            </p>
          ))}
          <p className={styles.total}>
            Total estimado: <strong>{fmtRange(report.budget.total)}</strong>
            {report.budget.effectiveCap !== undefined && <> · tope {fmtMoney(report.budget.effectiveCap)}</>}
          </p>
        </section>
      </aside>
    </div>
  );
}
