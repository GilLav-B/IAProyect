"use client";

import { useCallback, useEffect, useState } from "react";
import { api, ApiError, API_URL } from "@/lib/api";
import type { AiJob } from "@/lib/types";
import { CORNER_VIEWS, type Shot } from "./scene/RoomScene";
import styles from "./RealisticViews.module.css";

interface Props {
  designId: string;
  objectCount: number;
  busy: boolean;
  /** Captura las 4 vistas de esquina del visor. */
  capture: () => Promise<Shot[]>;
  onError: (text: string) => void;
}

const CAPTION = "Imagen de referencia generada por IA. No representa medidas ni productos exactos.";
const POLL_MS = 2000;

const isDone = (j: AiJob) => j.status === "completado" || j.status === "fallido";
const imageOf = (j: AiJob | undefined) => (j?.status === "completado" && j.result?.imageUrl ? `${API_URL}${j.result.imageUrl}` : null);

/** El lote de vistas mas reciente de esta version (las 4 burbujas), ordenado por vista. */
function latestBatch(jobs: AiJob[]): AiJob[] {
  const newest = jobs.find((j) => j.mode === "render" && j.batch);
  if (!newest) return [];
  return jobs.filter((j) => j.batch === newest.batch).sort((a, b) => (a.view ?? 0) - (b.view ?? 0));
}

/**
 * 4 burbujas arriba a la derecha del visor: fotos hiperrealistas (IA) del diseno desde las 4 esquinas.
 * Se generan solo con el boton (cada lote gasta 4 imagenes de OpenAI). Son ayuda visual: el modelo 3D es la fuente de verdad.
 */
export function RealisticViews({ designId, objectCount, busy, capture, onError }: Props) {
  const [jobs, setJobs] = useState<AiJob[]>([]);
  const [starting, setStarting] = useState(false);
  const [open, setOpen] = useState<number | null>(null);
  const running = jobs.some((j) => !isDone(j));

  // Ultimo lote de esta version
  useEffect(() => {
    let cancelled = false;
    api
      .aiJobs(designId)
      .then((all) => !cancelled && setJobs(latestBatch(all)))
      .catch(() => {});
    return () => {
      cancelled = true;
    };
  }, [designId]);

  // Avance mientras haya vistas sin terminar
  useEffect(() => {
    if (!running) return;
    const timer = setInterval(() => {
      api
        .aiJobs(designId)
        .then((all) => {
          const batch = latestBatch(all);
          if (batch.length > 0) setJobs(batch);
        })
        .catch(() => {});
    }, POLL_MS);
    return () => clearInterval(timer);
  }, [running, designId]);

  const generate = async () => {
    setStarting(true);
    try {
      const shots = await capture();
      if (shots.length === 0 || shots.some((s) => !s.image)) {
        onError("No se pudo capturar el visor 3D.");
        return;
      }
      const batch = crypto.randomUUID().replaceAll("-", "").slice(0, 16);
      const started = await Promise.all(shots.map((shot, view) => api.aiRender(designId, shot.image!, "", shot.visible, { id: batch, view })));
      setJobs(started);
    } catch (e) {
      onError(e instanceof ApiError ? (typeof e.body === "string" ? e.body : `La API respondio ${e.status}`) : String(e));
    } finally {
      setStarting(false);
    }
  };

  const shown = jobs.map(imageOf);
  const ready = shown.map((s, i) => (s ? i : -1)).filter((i) => i >= 0);

  const step = useCallback(
    (dir: 1 | -1) => {
      if (open === null || ready.length === 0) return;
      const pos = ready.indexOf(open);
      setOpen(ready[(pos + dir + ready.length) % ready.length]);
    },
    [open, ready],
  );

  useEffect(() => {
    if (open === null) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") setOpen(null);
      if (e.key === "ArrowRight") step(1);
      if (e.key === "ArrowLeft") step(-1);
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [open, step]);

  const disabled = objectCount === 0 || busy || running || starting;

  return (
    <>
      <div className={styles.box}>
        <button
          type="button"
          className={styles.generate}
          disabled={disabled}
          onClick={() => void generate()}
          title={objectCount === 0 ? "Agrega muebles primero" : "Genera 4 fotos realistas con IA desde las esquinas del cuarto (gasta 4 imagenes de OpenAI)"}
        >
          {starting || running ? "✨ Generando vistas…" : jobs.length > 0 ? "✨ Generar de nuevo" : "✨ Vistas realistas"}
        </button>
        {jobs.length > 0 && (
          <div className={styles.bubbles}>
            {jobs.map((j, i) => {
              const src = shown[i];
              const label = CORNER_VIEWS[j.view ?? i] ?? `Vista ${i + 1}`;
              return (
                <button
                  key={j.id}
                  type="button"
                  className={styles.bubble}
                  disabled={!src}
                  title={j.status === "fallido" ? `${label}: ${j.error ?? "fallo"}` : label}
                  aria-label={`Ver ${label}`}
                  onClick={() => src && setOpen(i)}
                >
                  {/* eslint-disable-next-line @next/next/no-img-element -- imagen servida por la API */}
                  {src ? <img src={src} alt={label} /> : null}
                  {!isDone(j) && (
                    <>
                      <span className={styles.spinner} />
                      <span className={styles.pending}>{j.progress}%</span>
                    </>
                  )}
                  {j.status === "fallido" && <span className={styles.failed}>⛔</span>}
                </button>
              );
            })}
          </div>
        )}
      </div>

      {open !== null && shown[open] && (
        <div className={styles.overlay} role="dialog" aria-modal="true" onClick={() => setOpen(null)}>
          <div className={styles.viewer} onClick={(e) => e.stopPropagation()}>
            {/* eslint-disable-next-line @next/next/no-img-element -- imagen servida por la API */}
            <img src={shown[open]!} alt={CORNER_VIEWS[jobs[open].view ?? open]} />
            <div className={styles.viewerBar}>
              <div>
                <strong>{CORNER_VIEWS[jobs[open].view ?? open]}</strong>
                <p className={styles.caption}>{jobs[open].result?.caption ?? CAPTION}</p>
              </div>
              <div className={styles.nav}>
                <button type="button" onClick={() => step(-1)} disabled={ready.length < 2}>
                  ←
                </button>
                <button type="button" onClick={() => step(1)} disabled={ready.length < 2}>
                  →
                </button>
                <button type="button" onClick={() => setOpen(null)}>
                  Cerrar
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
