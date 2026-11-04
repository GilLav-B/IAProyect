"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { api } from "@/lib/api";
import type { CompareResult, DesignSummary } from "@/lib/types";
import { fmtRange, label } from "@/lib/units";
import styles from "./Editor.module.css";

interface Props {
  projectId: string;
  roomId: string;
  currentId: string;
  /** Cambia con cada guardado para refrescar totales. */
  revision: number;
  busy: boolean;
  onError: (text: string) => void;
  onRenamed: () => void;
  productName: (catalogId: string) => string;
}

/**
 * Pestanas tipo hojas de Excel: cada version del mismo cuarto es una pestana independiente.
 * Duplicar crea una copia nueva (la original no cambia); comparar muestra diferencias y presupuestos.
 */
export function VersionTabs({ projectId, roomId, currentId, revision, busy, onError, onRenamed, productName }: Props) {
  const router = useRouter();
  const [designs, setDesigns] = useState<DesignSummary[]>([]);
  const [renaming, setRenaming] = useState<string | null>(null);
  const [compareWith, setCompareWith] = useState<string>("");
  const [comparison, setComparison] = useState<CompareResult | null>(null);
  const [working, setWorking] = useState(false);

  useEffect(() => {
    let cancelled = false;
    api
      .project(projectId)
      .then((p) => !cancelled && setDesigns(p.designs.filter((d) => d.roomId === roomId)))
      .catch((e) => !cancelled && onError(String(e)));
    return () => {
      cancelled = true;
    };
  }, [projectId, roomId, revision, onError]);

  const current = designs.find((d) => d.id === currentId);

  const duplicate = async () => {
    setWorking(true);
    try {
      const copy = await api.duplicate(currentId, `${current?.name ?? "Diseno"} (copia)`);
      router.push(`/designs/${copy.design.id}`);
    } catch (e) {
      onError(String(e));
      setWorking(false);
    }
  };

  const rename = async (name: string) => {
    setRenaming(null);
    if (!name.trim() || name === current?.name) return;
    try {
      await api.patchDesign(currentId, { name: name.trim() });
      onRenamed();
    } catch (e) {
      onError(String(e));
    }
  };

  const remove = async () => {
    if (designs.length <= 1) return onError("No se puede borrar la unica version de esta habitacion.");
    if (!window.confirm(`¿Borrar "${current?.name}"? Las demas versiones no cambian.`)) return;
    try {
      await api.deleteDesign(currentId);
      router.push(`/designs/${designs.find((d) => d.id !== currentId)!.id}`);
    } catch (e) {
      onError(String(e));
    }
  };

  const compare = async (otherId: string) => {
    setCompareWith(otherId);
    setComparison(otherId ? await api.compare(currentId, otherId).catch(() => null) : null);
  };

  const other = designs.find((d) => d.id === compareWith);

  return (
    <div className={styles.tabsBar}>
      {comparison && other && (
        <div className={styles.comparePanel}>
          <div className={styles.compareHead}>
            <strong>
              {current?.name} vs {other.name}
            </strong>
            <button type="button" onClick={() => void compare("")} aria-label="Cerrar comparacion">
              ✕
            </button>
          </div>
          <table className={styles.compareTable}>
            <thead>
              <tr>
                <th>Categoria</th>
                <th>{current?.name}</th>
                <th>{other.name}</th>
              </tr>
            </thead>
            <tbody>
              {comparison.differences.length === 0 && (
                <tr>
                  <td colSpan={3}>Mismos productos por categoria.</td>
                </tr>
              )}
              {comparison.differences.map((d) => (
                <tr key={d.term}>
                  <td>{label(d.args[0])}</td>
                  <td>{d.args[1] === "ninguno" ? "—" : productName(d.args[1])}</td>
                  <td>{d.args[2] === "ninguno" ? "—" : productName(d.args[2])}</td>
                </tr>
              ))}
              <tr className={styles.compareTotal}>
                <td>Total</td>
                <td>{fmtRange(comparison.totalA)}</td>
                <td>{fmtRange(comparison.totalB)}</td>
              </tr>
            </tbody>
          </table>
          {comparison.differences.length > 0 && <p className={styles.hint}>{comparison.differences.length} diferencias por categoria.</p>}
        </div>
      )}

      <div className={styles.tabs} role="tablist" aria-label="Versiones del diseno">
        {designs.map((d, i) => (
          <div key={d.id} className={d.id === currentId ? styles.tabOn : styles.tab} role="tab" aria-selected={d.id === currentId}>
            {renaming === d.id ? (
              <input
                autoFocus
                defaultValue={d.name}
                className={styles.tabInput}
                onBlur={(e) => void rename(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === "Enter") void rename(e.currentTarget.value);
                  if (e.key === "Escape") setRenaming(null);
                }}
              />
            ) : (
              <button
                type="button"
                className={styles.tabButton}
                disabled={busy}
                onClick={() => d.id !== currentId && router.push(`/designs/${d.id}`)}
                onDoubleClick={() => d.id === currentId && setRenaming(d.id)}
                title={d.id === currentId ? "Doble clic para renombrar" : `Abrir ${d.name}`}
              >
                <span className={styles.tabName}>
                  {i + 1}. {d.name}
                </span>
                <span className={styles.tabMeta}>
                  {label(d.style)} · {fmtRange(d.total)} {d.valid ? "" : "⛔"}
                </span>
              </button>
            )}
          </div>
        ))}
        <button type="button" className={styles.tabAdd} disabled={busy || working} onClick={() => void duplicate()} title="Duplicar esta version">
          + Duplicar
        </button>
      </div>

      <div className={styles.tabTools}>
        <select value={compareWith} onChange={(e) => void compare(e.target.value)} aria-label="Comparar con">
          <option value="">Comparar con…</option>
          {designs
            .filter((d) => d.id !== currentId)
            .map((d) => (
              <option key={d.id} value={d.id}>
                {d.name}
              </option>
            ))}
        </select>
        <button type="button" onClick={() => setRenaming(currentId)} disabled={busy}>
          Renombrar
        </button>
        <button type="button" className={styles.danger} onClick={() => void remove()} disabled={busy || designs.length <= 1}>
          Borrar
        </button>
      </div>
    </div>
  );
}
