"use client";

import { useEffect, useRef, useState } from "react";
import { api, API_URL } from "@/lib/api";
import type { AiJob, DesignLists } from "@/lib/types";
import { fmtMoney, fmtRange, label } from "@/lib/units";
import styles from "./Editor.module.css";
import ai from "./AiPanel.module.css";

interface Props {
  designId: string;
  /** Cambia cuando cambia el diseno o la habitacion: se vuelven a pedir las listas. */
  revision: number;
  /** Modelo 3D del visor en GLB. */
  exportGlb: () => Promise<Blob | null>;
  onError: (text: string) => void;
}

const range = (min: number, max: number) => fmtRange({ min, max });

/** Fase 7: lista de compras, lista de materiales (con desperdicio) y descargas. */
export function ExportPanel({ designId, revision, exportGlb, onError }: Props) {
  const [lists, setLists] = useState<DesignLists | null>(null);
  const [blender, setBlender] = useState<AiJob | null>(null);
  const [glbBusy, setGlbBusy] = useState(false);
  // El padre pasa una funcion nueva en cada render: se guarda en una ref para no volver a pedir las listas por eso
  const onErrorRef = useRef(onError);
  useEffect(() => {
    onErrorRef.current = onError;
  }, [onError]);

  useEffect(() => {
    let cancelled = false;
    Promise.all([api.lists(designId), api.aiJobs(designId)])
      .then(([l, jobs]) => {
        if (cancelled) return;
        setLists(l);
        setBlender(jobs.find((j) => j.type === "blender" && j.status === "completado") ?? null);
      })
      .catch(() => !cancelled && onErrorRef.current("No se pudieron cargar las listas."));
    return () => {
      cancelled = true;
    };
  }, [designId, revision]);

  const downloadGlb = async () => {
    setGlbBusy(true);
    try {
      const blob = await exportGlb();
      if (!blob) return onError("No se pudo exportar el modelo 3D.");
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `${designId}.glb`;
      a.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      onError(`No se pudo exportar el modelo 3D: ${String(e)}`);
    } finally {
      setGlbBusy(false);
    }
  };

  const files: [string, string, string][] = [
    ["pdf", "📄 Reporte PDF", "Renders, plano, presupuesto y listas"],
    ["xlsx", "📊 Excel", "Compras, materiales, presupuesto y muebles"],
    ["csv", "🧾 CSV", "Lista de compras y materiales"],
    ["plan.svg", "📐 Plano SVG", "Planta con cotas (editable)"],
    ["plan.pdf", "📐 Plano PDF", "Planta lista para imprimir"],
  ];

  return (
    <>
      <section className={styles.card}>
        <h3 className={styles.subtitle}>Descargas</h3>
        <ul className={styles.objectList}>
          {files.map(([format, title, hint]) => (
            <li key={format}>
              <a className={ai.download} href={api.exportUrl(designId, format)} download>
                <strong>{title}</strong> <span className={styles.muted}>· {hint}</span>
              </a>
            </li>
          ))}
          <li>
            <button type="button" className={ai.download} disabled={glbBusy} onClick={() => void downloadGlb()}>
              <strong>🧊 Modelo 3D (GLB)</strong> <span className={styles.muted}>· {glbBusy ? "exportando…" : "para otros programas 3D"}</span>
            </button>
          </li>
          <li>
            {blender?.result?.blendUrl ? (
              <a className={ai.download} href={`${API_URL}${blender.result.blendUrl}`} download>
                <strong>🏗️ Archivo de Blender (.blend)</strong> <span className={styles.muted}>· del ultimo render</span>
              </a>
            ) : (
              <span className={styles.muted}>🏗️ El archivo .blend aparece cuando haces un render en Blender (pestana IA).</span>
            )}
          </li>
        </ul>
        <p className={styles.hint}>El PDF incluye el ultimo render de Blender y las ultimas imagenes de IA (con su leyenda).</p>
      </section>

      <section className={styles.card}>
        <h3 className={styles.subtitle}>Lista de compras</h3>
        {!lists ? (
          <p className={styles.muted}>Cargando…</p>
        ) : lists.shopping.length === 0 ? (
          <p className={styles.muted}>Todavia no hay muebles.</p>
        ) : (
          <ul className={ai.outcomes}>
            {lists.shopping.map((i) => (
              <li key={i.catalogId}>
                <span>
                  {i.quantity} × <strong>{i.name}</strong> <span className={styles.muted}>({label(i.rubro)}{i.kind === "conceptual" ? ", conceptual" : ""})</span>
                </span>
                <span className={ai.outcomeReason}>
                  {range(i.unitMin, i.unitMax)} c/u · subtotal {range(i.totalMin, i.totalMax)}
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className={styles.card}>
        <h3 className={styles.subtitle}>Lista de materiales</h3>
        {lists && lists.materials.length === 0 && <p className={styles.muted}>Este diseno no tiene materiales aplicados.</p>}
        {lists && lists.materials.length > 0 && (
          <ul className={ai.outcomes}>
            {lists.materials.map((m) => (
              <li key={m.materialId}>
                <span>
                  <strong>{m.name}</strong> <span className={styles.muted}>({label(m.type)})</span>
                </span>
                <span className={ai.outcomeReason}>
                  {m.m2} m² del diseno → comprar {m.m2WithWaste} m² (con {Math.round(lists.wasteFactor * 100)}% de desperdicio) · {fmtMoney(m.priceM2Min)}–
                  {fmtMoney(m.priceM2Max)}/m² · {range(m.totalMin, m.totalMax)}
                </span>
              </li>
            ))}
          </ul>
        )}
        <p className={styles.hint}>Precios [EJEMPLO] estimados en MXN.</p>
      </section>
    </>
  );
}
