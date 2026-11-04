"use client";

import { describe } from "@/lib/reasons";
import type { PlacedObject, Product, Reason } from "@/lib/types";
import { colorHex, fmtM, fmtRange, label, rotatedDims } from "@/lib/units";
import styles from "./Editor.module.css";

interface Props {
  /** Objeto como se ve ahora (con el borrador aplicado). */
  obj: PlacedObject;
  product: Product;
  /** Productos de la misma categoria, para cambiar de modelo. */
  models: Product[];
  /** Motivos por los que su movimiento no se puede guardar. */
  problems: Reason[];
  /** Hay movimientos sin guardar: modelo, color, material y bloqueo esperan a que se guarden o descarten. */
  hasDraft: boolean;
  busy: boolean;
  onRotate: (delta: 90 | -90) => void;
  onReplace: (catalogId: string) => void;
  onColor: (color: string) => void;
  onMaterial: (material: string) => void;
  onToggleLock: () => void;
}

/** Panel tipo configurador de vehiculos: modelo, color, material y precio del objeto seleccionado. */
export function ConfigPanel(p: Props) {
  const { obj, product, models, problems, hasDraft, busy } = p;
  const [a, b] = rotatedDims(obj.rotation, product.dimensions.width, product.dimensions.depth);
  const color = obj.config?.color ?? product.colors[0];
  const material = obj.config?.material ?? product.materials[0];
  const configLocked = busy || hasDraft;

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

      {problems.length > 0 && (
        <div className={styles.msgError}>
          <strong>Asi no se puede guardar</strong>
          <ul>
            {problems.map((r) => (
              <li key={r.term}>{describe(r)}</li>
            ))}
          </ul>
        </div>
      )}

      <dl className={styles.specs}>
        <dt>Modelo</dt>
        <dd>
          <select value={product.id} disabled={configLocked || obj.locked} onChange={(e) => p.onReplace(e.target.value)}>
            {models.map((m) => (
              <option key={m.id} value={m.id}>
                {m.name}
              </option>
            ))}
          </select>
        </dd>

        <dt>Color</dt>
        <dd>
          <div className={styles.swatches}>
            {product.colors.map((c) => (
              <button
                key={c}
                type="button"
                title={label(c)}
                aria-label={label(c)}
                aria-pressed={c === color}
                disabled={configLocked}
                className={c === color ? styles.swatchOn : styles.swatch}
                style={{ background: colorHex(c) }}
                onClick={() => c !== color && p.onColor(c)}
              />
            ))}
            <span className={styles.swatchLabel}>{label(color)}</span>
          </div>
        </dd>

        <dt>Material</dt>
        <dd>
          <div className={styles.pills}>
            {product.materials.map((m) => (
              <button
                key={m}
                type="button"
                aria-pressed={m === material}
                disabled={configLocked}
                className={m === material ? styles.pillOn : styles.pill}
                onClick={() => m !== material && p.onMaterial(m)}
              >
                {label(m)}
              </button>
            ))}
          </div>
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
        <button type="button" onClick={() => p.onRotate(-90)} disabled={busy || obj.locked} title="Rotar a la izquierda (Shift+R)">
          ⟲ 90°
        </button>
        <button type="button" onClick={() => p.onRotate(90)} disabled={busy || obj.locked} title="Rotar a la derecha (R)">
          ⟳ 90°
        </button>
        <button type="button" onClick={p.onToggleLock} disabled={configLocked} className={obj.locked ? styles.locked : undefined}>
          {obj.locked ? "🔒 Bloqueado" : "🔓 Libre"}
        </button>
      </div>
      <p className={styles.hint}>
        Arrastra para mover (Shift = ajuste fino de 1 cm). R / Shift+R rota. Puedes atravesar otros muebles mientras acomodas; solo
        no te deja guardar si algo interfiere.
        {hasDraft && " Modelo, color, material y bloqueo se cambian despues de guardar o descartar los movimientos."}
      </p>
    </section>
  );
}
