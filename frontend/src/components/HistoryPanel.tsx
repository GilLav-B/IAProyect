"use client";

import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import type { HistoryEntry } from "@/lib/types";
import styles from "./Editor.module.css";

interface Props {
  designId: string;
  revision: number;
  busy: boolean;
  onRevert: (entry: HistoryEntry) => void;
}

const fmtDate = (iso: string) =>
  new Date(iso).toLocaleString("es-MX", { day: "2-digit", month: "short", hour: "2-digit", minute: "2-digit" });

/** Historial de la version: cada cambio guardado con su revision. Revertir no borra nada: queda como un cambio nuevo. */
export function HistoryPanel({ designId, revision, busy, onRevert }: Props) {
  const [entries, setEntries] = useState<HistoryEntry[]>([]);

  useEffect(() => {
    let cancelled = false;
    api
      .history(designId)
      .then((h) => !cancelled && setEntries([...h].reverse()))
      .catch(() => {});
    return () => {
      cancelled = true;
    };
  }, [designId, revision]);

  const previous = entries[1];

  return (
    <section className={styles.card}>
      <div className={styles.cardHeader}>
        <h3 className={styles.subtitle}>Historial</h3>
        <button type="button" className={styles.smallButton} disabled={busy || !previous} onClick={() => previous && onRevert(previous)}>
          ↶ Deshacer ultimo cambio
        </button>
      </div>
      <ol className={styles.history}>
        {entries.map((h, i) => (
          <li key={h.id} className={i === 0 ? styles.historyCurrent : undefined}>
            <div>
              <strong>rev. {h.revision ?? "—"}</strong> <span className={styles.muted}>{fmtDate(h.date)}</span>
              <div className={styles.historyText}>{h.description}</div>
            </div>
            {i === 0 ? (
              <span className={styles.muted}>actual</span>
            ) : (
              <button type="button" className={styles.smallButton} disabled={busy} onClick={() => onRevert(h)}>
                Volver aqui
              </button>
            )}
          </li>
        ))}
      </ol>
    </section>
  );
}
