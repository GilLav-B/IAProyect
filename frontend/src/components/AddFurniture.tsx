"use client";

import { useState } from "react";
import { SPACE_CATEGORIES } from "@/lib/rules";
import type { Product } from "@/lib/types";
import { colorHex, fmtM, fmtRange, label } from "@/lib/units";
import styles from "./Editor.module.css";

interface Props {
  spaceType: string;
  products: Product[];
  disabled: boolean;
  onAdd: (catalogId: string) => void;
}

/** Catalogo para agregar muebles: solo las categorias permitidas en este espacio (categorias_espacio/2). */
export function AddFurniture({ spaceType, products, disabled, onAdd }: Props) {
  const allowed = SPACE_CATEGORIES[spaceType] ?? [];
  const [category, setCategory] = useState<string>("todas");
  const list = products.filter((p) => allowed.includes(p.category) && (category === "todas" || p.category === category));
  const categories = allowed.filter((c) => products.some((p) => p.category === c));

  return (
    <section className={styles.card}>
      <h3 className={styles.subtitle}>Agregar del catalogo</h3>
      <div className={styles.pills}>
        {["todas", ...categories].map((c) => (
          <button key={c} type="button" className={category === c ? styles.pillOn : styles.pill} onClick={() => setCategory(c)}>
            {label(c)}
          </button>
        ))}
      </div>
      <ul className={styles.catalog}>
        {list.map((p) => (
          <li key={p.id}>
            <span className={styles.catalogDot} style={{ background: colorHex(p.colors[0]) }} />
            <div className={styles.catalogInfo}>
              <strong>{p.name}</strong>
              <span className={styles.muted}>
                {fmtRange(p.price)} · {fmtM(p.dimensions.width)} × {fmtM(p.dimensions.depth)}
                {p.kind === "conceptual" && " · conceptual"}
              </span>
            </div>
            <button type="button" disabled={disabled} onClick={() => onAdd(p.id)}>
              Agregar
            </button>
          </li>
        ))}
        {list.length === 0 && <li className={styles.muted}>No hay productos de esta categoria en el catalogo.</li>}
      </ul>
      <p className={styles.hint}>El motor busca un lugar libre que no choque ni tape puertas o ventanas; luego lo acomodas.</p>
    </section>
  );
}
