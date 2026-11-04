"use client";

import type { PlacedObject, Product } from "@/lib/types";
import { fmtM, fmtRange, label, rotatedDims } from "@/lib/units";
import styles from "./Editor.module.css";

interface Props {
  obj: PlacedObject;
  product: Product;
  /** Productos de la misma categoria, para cambiar de modelo. */
  models: Product[];
  busy: boolean;
  onRotate: () => void;
  onReplace: (catalogId: string) => void;
  onColor: (color: string) => void;
  onMaterial: (material: string) => void;
  onToggleLock: () => void;
}

/** Panel tipo configurador de vehiculos: modelo, color, material y precio del objeto seleccionado. */
export function ConfigPanel({ obj, product, models, busy, onRotate, onReplace, onColor, onMaterial, onToggleLock }: Props) {
  const [a, b] = rotatedDims(obj.rotation, product.dimensions.width, product.dimensions.depth);
  const color = obj.config?.color ?? product.colors[0];
  const material = obj.config?.material ?? product.materials[0];

  return (
    <section className={styles.card}>
      <header className={styles.cardHeader}>
        <div>
          <div className={styles.eyebrow}>{label(product.category)}</div>
          <h2 className={styles.title}>{product.name}</h2>
          <code className={styles.mono}>{obj.instanceId}</code>
        </div>
        <span className={product.kind === "real" ? styles.badgeReal : styles.badgeConcept}>
          {product.kind === "real" ? "Producto real" : "Conceptual"}
        </span>
      </header>

      <dl className={styles.specs}>
        <dt>Modelo</dt>
        <dd>
          <select value={product.id} disabled={busy || obj.locked} onChange={(e) => onReplace(e.target.value)}>
            {models.map((m) => (
              <option key={m.id} value={m.id}>
                {m.name}
              </option>
            ))}
          </select>
        </dd>

        <dt>Color</dt>
        <dd>
          <select value={color} disabled={busy} onChange={(e) => onColor(e.target.value)}>
            {product.colors.map((c) => (
              <option key={c} value={c}>
                {label(c)}
              </option>
            ))}
          </select>
        </dd>

        <dt>Material</dt>
        <dd>
          <select value={material} disabled={busy} onChange={(e) => onMaterial(e.target.value)}>
            {product.materials.map((m) => (
              <option key={m} value={m}>
                {label(m)}
              </option>
            ))}
          </select>
        </dd>

        <dt>Precio</dt>
        <dd className={styles.price}>{fmtRange(product.price)}</dd>

        <dt>Medidas</dt>
        <dd>
          {fmtM(product.dimensions.width)} × {fmtM(product.dimensions.depth)} × {fmtM(product.dimensions.height)} alto
        </dd>

        <dt>Posicion</dt>
        <dd>
          x {fmtM(obj.position.x)}, y {fmtM(obj.position.y)} · ocupa {fmtM(a)} × {fmtM(b)}
        </dd>

        <dt>Rotacion</dt>
        <dd>{obj.rotation}°</dd>
      </dl>

      <div className={styles.actions}>
        <button type="button" onClick={onRotate} disabled={busy}>
          Rotar 90°
        </button>
        <button type="button" onClick={onToggleLock} disabled={busy} className={obj.locked ? styles.locked : undefined}>
          {obj.locked ? "🔒 Bloqueado" : "🔓 Desbloqueado"}
        </button>
      </div>
      <p className={styles.hint}>
        Arrastra el objeto en la escena; al soltarlo el motor valida el movimiento. Bloqueado = la IA no lo puede cambiar.
      </p>
    </section>
  );
}
