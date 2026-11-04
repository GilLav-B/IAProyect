"use client";

import { useState } from "react";
import { SPACE_CATEGORIES, STYLES } from "@/lib/rules";
import type { Product, Requirement } from "@/lib/types";
import { fmtMoney, label } from "@/lib/units";
import styles from "./Editor.module.css";

export const PRIORITIES: Requirement["priority"][] = ["obligatorio", "importante", "preferencia", "opcional"];

/** Texto del requisito como lo escribe el motor (ej. tiene(televisor,pulgadas,75)). */
export function requirementTerm(r: Requirement): string {
  switch (r.type) {
    case "presupuesto_max":
      return `presupuesto_max(${r.amount})`;
    case "tiene":
      return `tiene(${r.category},${r.attribute},${r.value})`;
    case "conservar":
      return `conservar(${r.element})`;
    case "estilo":
      return `estilo(${r.style})`;
    case "color":
      return `color(${r.category},${r.color})`;
    case "categoria":
      return `categoria(${r.category})`;
  }
}

function describeRequirement(r: Requirement): string {
  switch (r.type) {
    case "presupuesto_max":
      return `Presupuesto maximo ${fmtMoney(r.amount ?? 0)}`;
    case "tiene":
      return `${label(r.category ?? "")} con ${label(r.attribute ?? "")} = ${r.value}`;
    case "conservar":
      return `Conservar ${label(r.element ?? "")}`;
    case "estilo":
      return `Estilo ${r.style}`;
    case "color":
      return `${label(r.category ?? "")} color ${label(r.color ?? "")}`;
    case "categoria":
      return `Incluir ${label(r.category ?? "")}`;
  }
}

interface Props {
  spaceType: string;
  requirements: Requirement[];
  products: Product[];
  busy: boolean;
  onSave: (requirements: Requirement[]) => void;
}

/** Requisitos de la habitacion con su prioridad. Obligatorio bloquea; importante y preferencia solo avisan; opcional no avisa. */
export function RequirementsPanel({ spaceType, requirements, products, busy, onSave }: Props) {
  const [draft, setDraft] = useState<Requirement[] | null>(null);
  const list = draft ?? requirements;
  const dirty = draft !== null;
  const categories = SPACE_CATEGORIES[spaceType] ?? [];

  const [newType, setNewType] = useState<"categoria" | "tiene" | "color" | "estilo">("categoria");
  const [newCategory, setNewCategory] = useState(categories[0] ?? "sofa");
  const [newAttr, setNewAttr] = useState("");
  const [newValue, setNewValue] = useState("");
  const [newColor, setNewColor] = useState("");
  const [newStyle, setNewStyle] = useState<string>(STYLES[0]);
  const [newPriority, setNewPriority] = useState<Requirement["priority"]>("preferencia");

  const inCategory = products.filter((p) => p.category === newCategory);
  const attrs = [...new Set(inCategory.flatMap((p) => Object.keys(p.attributes ?? {})))];
  const values = [...new Set(inCategory.map((p) => p.attributes?.[newAttr]).filter((v) => v !== undefined).map(String))];
  const colors = [...new Set(inCategory.flatMap((p) => p.colors))];

  const edit = (i: number, patch: Partial<Requirement>) => setDraft(list.map((r, j) => (j === i ? { ...r, ...patch } : r)));

  const add = () => {
    let id = 1;
    while (list.some((r) => r.id === `r${id}`)) id++;
    const base = { id: `r${id}`, priority: newPriority };
    let req: Requirement;
    if (newType === "categoria") req = { ...base, type: "categoria", category: newCategory };
    else if (newType === "estilo") req = { ...base, type: "estilo", style: newStyle };
    else if (newType === "color") req = { ...base, type: "color", category: newCategory, color: newColor || colors[0] };
    else {
      const raw = newValue || values[0];
      const value = raw !== undefined && raw !== "" && !Number.isNaN(Number(raw)) ? Number(raw) : raw;
      req = { ...base, type: "tiene", category: newCategory, attribute: newAttr || attrs[0], value };
    }
    setDraft([...list, req]);
  };

  const canAdd =
    newType === "categoria" || newType === "estilo" || (newType === "color" && colors.length > 0) || (newType === "tiene" && attrs.length > 0);

  return (
    <section className={styles.card}>
      <h3 className={styles.subtitle}>Requisitos de la habitacion</h3>
      <ul className={styles.reqList}>
        {list.map((r, i) => (
          <li key={r.id}>
            <div className={styles.reqText}>
              {describeRequirement(r)}
              {r.type === "presupuesto_max" && (
                <span className={styles.reqAmount}>
                  $
                  <input type="number" min={0} step={500} value={r.amount ?? 0} disabled={busy} onChange={(e) => edit(i, { amount: Number(e.target.value) })} />
                </span>
              )}
            </div>
            <select value={r.priority} disabled={busy} onChange={(e) => edit(i, { priority: e.target.value as Requirement["priority"] })}>
              {PRIORITIES.map((p) => (
                <option key={p}>{p}</option>
              ))}
            </select>
            <button type="button" className={styles.smallButton} disabled={busy} onClick={() => setDraft(list.filter((_, j) => j !== i))} aria-label="Quitar requisito">
              ✕
            </button>
          </li>
        ))}
      </ul>

      <details className={styles.addReq}>
        <summary>+ Agregar requisito</summary>
        <div className={styles.addReqGrid}>
          <select value={newType} onChange={(e) => setNewType(e.target.value as typeof newType)}>
            <option value="categoria">Incluir una categoria</option>
            <option value="tiene">Producto con caracteristica</option>
            <option value="color">Color de una categoria</option>
            <option value="estilo">Estilo</option>
          </select>
          {newType !== "estilo" && (
            <select value={newCategory} onChange={(e) => setNewCategory(e.target.value)}>
              {categories.map((c) => (
                <option key={c} value={c}>
                  {label(c)}
                </option>
              ))}
            </select>
          )}
          {newType === "tiene" && (
            <>
              <select value={newAttr || attrs[0] || ""} onChange={(e) => setNewAttr(e.target.value)}>
                {attrs.length === 0 && <option value="">(sin caracteristicas)</option>}
                {attrs.map((a) => (
                  <option key={a}>{a}</option>
                ))}
              </select>
              <select value={newValue || values[0] || ""} onChange={(e) => setNewValue(e.target.value)}>
                {values.map((v) => (
                  <option key={v}>{v}</option>
                ))}
              </select>
            </>
          )}
          {newType === "color" && (
            <select value={newColor || colors[0] || ""} onChange={(e) => setNewColor(e.target.value)}>
              {colors.map((c) => (
                <option key={c} value={c}>
                  {label(c)}
                </option>
              ))}
            </select>
          )}
          {newType === "estilo" && (
            <select value={newStyle} onChange={(e) => setNewStyle(e.target.value)}>
              {STYLES.map((s) => (
                <option key={s}>{s}</option>
              ))}
            </select>
          )}
          <select value={newPriority} onChange={(e) => setNewPriority(e.target.value as Requirement["priority"])}>
            {PRIORITIES.map((p) => (
              <option key={p}>{p}</option>
            ))}
          </select>
          <button type="button" className={styles.smallButton} disabled={!canAdd || busy} onClick={add}>
            Agregar
          </button>
        </div>
      </details>

      {dirty && (
        <div className={styles.draftActions}>
          <button type="button" className={styles.primary} disabled={busy} onClick={() => {
            onSave(list);
            setDraft(null);
          }}>
            Guardar requisitos
          </button>
          <button type="button" disabled={busy} onClick={() => setDraft(null)}>
            Descartar
          </button>
        </div>
      )}
      <p className={styles.hint}>
        Obligatorio bloquea el diseno; importante y preferencia solo avisan; opcional no avisa. Aplica a todas las versiones de esta
        habitacion.
      </p>
    </section>
  );
}
