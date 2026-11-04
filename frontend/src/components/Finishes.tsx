"use client";

import type { CatalogMaterial, Room } from "@/lib/types";
import { colorHex, label, WALL_COLORS } from "@/lib/units";
import { FLOORS } from "./scene/materials";
import styles from "./Editor.module.css";

interface Props {
  room: Room;
  materials: CatalogMaterial[];
  disabled: boolean;
  onChange: (part: "floor" | "walls", value: { materialId?: string; color?: string }) => void;
}

/** Acabados actuales del cuarto: color de muros y tipo de piso. Se guardan en la habitacion. */
export function Finishes({ room, materials, disabled, onChange }: Props) {
  const wall = room.surfaces?.walls?.color;
  const floor = room.surfaces?.floor?.materialId;
  return (
    <section className={styles.card}>
      <h3 className={styles.subtitle}>Acabados de la habitacion</h3>
      <dl className={styles.specs}>
        <dt>Muros</dt>
        <dd>
          <div className={styles.swatches}>
            {WALL_COLORS.map((c) => (
              <button
                key={c}
                type="button"
                title={label(c)}
                aria-pressed={wall === c}
                disabled={disabled}
                className={wall === c ? styles.swatchOn : styles.swatch}
                style={{ background: colorHex(c) }}
                onClick={() => onChange("walls", { ...room.surfaces?.walls, color: c })}
              />
            ))}
          </div>
        </dd>
        <dt>Piso</dt>
        <dd>
          <div className={styles.pills}>
            {materials
              .filter((m) => m.type in FLOORS)
              .map((m) => (
                <button
                  key={m.id}
                  type="button"
                  disabled={disabled}
                  className={floor === m.id ? styles.pillOn : styles.pill}
                  onClick={() => onChange("floor", { materialId: m.id })}
                >
                  {m.name}
                </button>
              ))}
          </div>
        </dd>
      </dl>
      <p className={styles.hint}>Cambia la habitacion: aplica a todas sus versiones de diseno.</p>
    </section>
  );
}
