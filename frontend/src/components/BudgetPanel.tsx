"use client";

import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import { BUDGET_MODES } from "@/lib/rules";
import type { BudgetInfo, Saving } from "@/lib/types";
import { fmtMoney, fmtRange, label } from "@/lib/units";
import styles from "./Editor.module.css";

const MODE_HELP: Record<string, string> = {
  minimizar: "Buscar alternativas mas baratas manteniendo los requisitos.",
  ajustar: "Aprovechar el presupuesto sin pasarse del tope.",
  flexible: "Permitir hasta 10% por encima del tope.",
};

const STATUS: Record<BudgetInfo["status"], { text: string; className: string }> = {
  sin_tope: { text: "Sin tope definido", className: "badgeOk" },
  dentro: { text: "Dentro del presupuesto", className: "badgeOk" },
  puede_excederse: { text: "Podria rebasarse", className: "badgeWarn" },
  excedido: { text: "Excedido", className: "badgeBad" },
};

interface Props {
  designId: string;
  /** Cambia con cada guardado: vuelve a pedir el presupuesto. */
  revision: number;
  busy: boolean;
  onMode: (mode: string) => void;
  onApplySaving: (s: Saving) => void;
  productName: (catalogId: string) => string;
}

/** Presupuesto dinamico por rubro (rangos min-max), modo de presupuesto y ahorros sugeridos por el motor. */
export function BudgetPanel({ designId, revision, busy, onMode, onApplySaving, productName }: Props) {
  const [budget, setBudget] = useState<BudgetInfo | null>(null);
  const [savings, setSavings] = useState<Saving[]>([]);

  useEffect(() => {
    let cancelled = false;
    Promise.all([api.budget(designId), api.savings(designId)])
      .then(([b, s]) => {
        if (cancelled) return;
        setBudget(b);
        setSavings(s);
      })
      .catch(() => {});
    return () => {
      cancelled = true;
    };
  }, [designId, revision]);

  if (!budget) return <section className={styles.card}>Cargando presupuesto…</section>;

  const rows = Object.entries(budget.byCategory).filter(([, r]) => r.max > 0);
  const scale = Math.max(budget.total.max, budget.effectiveCap ?? 0, 1);
  const status = STATUS[budget.status];
  // Un ahorro por objeto: el mayor
  const best = Object.values(
    savings.reduce<Record<string, Saving>>((acc, s) => {
      if (!acc[s.instanceId] || acc[s.instanceId].saving < s.saving) acc[s.instanceId] = s;
      return acc;
    }, {}),
  ).sort((a, b) => b.saving - a.saving);

  return (
    <section className={styles.card}>
      <div className={styles.cardHeader}>
        <h3 className={styles.subtitle}>Presupuesto</h3>
        <span className={styles[status.className]}>{status.text}</span>
      </div>

      <div className={styles.budgetTotal}>
        <strong>{fmtRange(budget.total)}</strong>
        {budget.effectiveCap !== undefined && (
          <span className={styles.muted}>
            tope {fmtMoney(budget.effectiveCap)}
            {budget.budgetMode === "flexible" && budget.budgetMax !== undefined && ` (${fmtMoney(budget.budgetMax)} + 10%)`}
          </span>
        )}
      </div>

      {/* Barra total: rango min-max y linea del tope */}
      <div className={styles.bar} aria-hidden>
        <div className={styles.barRange} style={{ left: `${(budget.total.min / scale) * 100}%`, width: `${((budget.total.max - budget.total.min) / scale) * 100}%` }} />
        <div className={styles.barFill} style={{ width: `${(budget.total.min / scale) * 100}%` }} />
        {budget.effectiveCap !== undefined && <div className={styles.barCap} style={{ left: `${(budget.effectiveCap / scale) * 100}%` }} />}
      </div>

      <ul className={styles.rubros}>
        {rows.length === 0 && <li className={styles.muted}>Todavia no hay costos.</li>}
        {rows.map(([rubro, r]) => (
          <li key={rubro}>
            <span className={styles.rubroName}>{label(rubro)}</span>
            <span className={styles.rubroBar}>
              <span style={{ width: `${(r.min / scale) * 100}%` }} />
              <span className={styles.rubroRange} style={{ width: `${((r.max - r.min) / scale) * 100}%` }} />
            </span>
            <span className={styles.rubroValue}>{fmtRange(r)}</span>
          </li>
        ))}
      </ul>

      <div className={styles.field}>
        <span className={styles.muted}>Modo</span>
        <div className={styles.pills}>
          {BUDGET_MODES.map((m) => (
            <button
              key={m}
              type="button"
              disabled={busy}
              className={budget.budgetMode === m ? styles.pillOn : styles.pill}
              onClick={() => m !== budget.budgetMode && onMode(m)}
            >
              {m}
            </button>
          ))}
        </div>
        <span className={styles.hint}>{MODE_HELP[budget.budgetMode]}</span>
      </div>

      {best.length > 0 && (
        <div className={budget.budgetMode === "minimizar" || budget.status === "excedido" ? styles.savingsStrong : styles.savings}>
          <strong>Ahorros posibles</strong>
          <ul>
            {best.map((s) => (
              <li key={s.instanceId}>
                <span>
                  {productName(s.currentCatalogId)} → <b>{s.suggestedName}</b>
                  <span className={styles.muted}> ahorra {fmtMoney(s.saving)}</span>
                </span>
                <button type="button" disabled={busy} onClick={() => onApplySaving(s)}>
                  Aplicar
                </button>
              </li>
            ))}
          </ul>
          <span className={styles.hint}>Solo reemplazos que el motor valida: mismo tipo, estilo compatible, caben y no rompen requisitos.</span>
        </div>
      )}
    </section>
  );
}
