"use client";

import type { BudgetInfo, Product, Reason, Requirement } from "@/lib/types";
import { fmtMoney, label } from "@/lib/units";
import { requirementTerm } from "./RequirementsPanel";
import styles from "./Editor.module.css";

export type Decision =
  | { kind: "raise_budget"; amount: number }
  | { kind: "flexible" }
  | { kind: "show_savings" }
  | { kind: "add_product"; catalogId: string }
  | { kind: "priority"; requirementId: string; priority: Requirement["priority"] }
  | { kind: "style"; style: string };

interface Props {
  conflicts: Reason[];
  requirements: Requirement[];
  budget: BudgetInfo | null;
  products: Product[];
  spaceType: string;
  busy: boolean;
  onDecide: (d: Decision, text: string) => void;
}

/** Separa "tiene(televisor,pulgadas,75)" en ["tiene", "televisor", "pulgadas", "75"]. */
function parseTerm(term: string): string[] {
  const m = term.match(/^(\w+)\((.*)\)$/);
  return m ? [m[1], ...m[2].split(",").map((s) => s.replace(/^'|'$/g, ""))] : [term];
}

/**
 * Cuando el motor encuentra un conflicto de presupuesto o de requisitos, el usuario decide que gana.
 * Cada opcion es una accion concreta; nada se aplica sin que el usuario la elija.
 */
export function DecisionBox({ conflicts, requirements, budget, products, spaceType, busy, onDecide }: Props) {
  const relevant = conflicts.filter((c) => c.code === "presupuesto_excedido" || c.code === "falta_requisito");
  if (relevant.length === 0) return null;

  return (
    <section className={styles.decision}>
      <strong>Hay que decidir</strong>
      {relevant.map((c) => {
        const options: { label: string; decision: Decision; text: string }[] = [];
        let title = "";

        if (c.code === "presupuesto_excedido" && budget) {
          title = `El minimo estimado (${fmtMoney(budget.total.min)}) rebasa el tope (${fmtMoney(budget.effectiveCap ?? 0)}).`;
          const amount = Math.ceil(budget.total.min / 1000) * 1000;
          options.push({ label: `Subir el tope a ${fmtMoney(amount)}`, decision: { kind: "raise_budget", amount }, text: `Tope subido a ${fmtMoney(amount)}` });
          if (budget.budgetMode !== "flexible" && budget.budgetMax !== undefined && budget.total.min <= budget.budgetMax * 1.1)
            options.push({ label: "Usar modo flexible (+10%)", decision: { kind: "flexible" }, text: "Modo flexible" });
          options.push({ label: "Ver ahorros posibles", decision: { kind: "show_savings" }, text: "" });
        }

        if (c.code === "falta_requisito") {
          const [type, ...args] = parseTerm(c.args[0] ?? "");
          const req = requirements.find((r) => r.priority === "obligatorio" && requirementTerm(r) === c.args[0]);
          title = `Falta un requisito obligatorio: ${c.args[0]}`;
          if (type === "tiene") {
            const [category, attribute, value] = args;
            products
              .filter((p) => p.category === category && String(p.attributes?.[attribute]) === value)
              .slice(0, 2)
              .forEach((p) =>
                options.push({ label: `Agregar ${p.name}`, decision: { kind: "add_product", catalogId: p.id }, text: `Agregado: ${p.name}` }),
              );
          }
          if (type === "categoria") {
            const [category] = args;
            const cheapest = products.filter((p) => p.category === category && p.kind === "real").sort((a, b) => a.price.min - b.price.min)[0];
            if (cheapest)
              options.push({ label: `Agregar ${cheapest.name}`, decision: { kind: "add_product", catalogId: cheapest.id }, text: `Agregado: ${cheapest.name}` });
          }
          if (type === "estilo")
            options.push({ label: `Cambiar el estilo a ${args[0]}`, decision: { kind: "style", style: args[0] }, text: `Estilo ${args[0]}` });
          if (req)
            options.push({
              label: "Que sea solo preferencia",
              decision: { kind: "priority", requirementId: req.id, priority: "preferencia" },
              text: "Requisito cambiado a preferencia",
            });
        }

        return (
          <div key={c.term} className={styles.decisionItem}>
            <p>{title}</p>
            <div className={styles.decisionOptions}>
              {options.map((o) => (
                <button key={o.label} type="button" disabled={busy} onClick={() => onDecide(o.decision, o.text)}>
                  {o.label}
                </button>
              ))}
            </div>
          </div>
        );
      })}
      <span className={styles.hint}>
        Tu decides que requisito gana. Espacio: {label(spaceType)}.
      </span>
    </section>
  );
}
