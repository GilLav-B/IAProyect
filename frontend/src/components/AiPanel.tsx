"use client";

import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { api, ApiError, API_URL } from "@/lib/api";
import { describe } from "@/lib/reasons";
import type { AiJob, AiOutcome } from "@/lib/types";
import type { Shot, ViewMode } from "./scene/RoomScene";
import { label } from "@/lib/units";
import editor from "./Editor.module.css";
import styles from "./AiPanel.module.css";

interface Props {
  designId: string;
  objectCount: number;
  /** Estilo actual del diseno (valor inicial de "Disenar con IA"). */
  style: string;
  busy: boolean;
  /** Vista actual del visor y como cambiarla (el render usa lo que se ve). */
  view: ViewMode;
  onView: (view: ViewMode) => void;
  capture: () => Promise<Shot | null>;
  /** El chat aplico cambios en este diseno: recargar. */
  onApplied: (applied: number) => void;
  onError: (text: string) => void;
}

const IMAGE_TYPES = ["image/jpeg", "image/png", "image/webp"];
const MAX_IMAGE_MB = 8;
const CAPTION = "Imagen de referencia generada por IA. No representa medidas ni productos exactos.";
const STYLES = ["moderno", "minimalista", "industrial", "japandi", "clasico"];

const isDone = (j: AiJob | null) => j?.status === "completado" || j?.status === "fallido";

function errorText(e: unknown) {
  if (e instanceof ApiError) return typeof e.body === "string" ? e.body : `La API respondio ${e.status}`;
  if (e instanceof TypeError) return `No se pudo conectar con la API en ${API_URL}.`;
  return String(e);
}

/** Sigue un trabajo de IA en vivo (SSE) mientras no termine. Si el SSE se cae, consulta cada 2 s. */
function useJob() {
  const [job, setJob] = useState<AiJob | null>(null);
  const runningId = job && !isDone(job) ? job.id : null;

  useEffect(() => {
    if (!runningId) return;
    let timer: ReturnType<typeof setInterval> | undefined;
    const source = new EventSource(api.aiEventsUrl(runningId));
    source.onmessage = (ev) => setJob(JSON.parse(ev.data) as AiJob);
    source.onerror = () => {
      source.close();
      timer ??= setInterval(() => {
        api.aiJob(runningId).then(setJob).catch(() => {});
      }, 2000);
    };
    return () => {
      source.close();
      if (timer) clearInterval(timer);
    };
  }, [runningId]);

  return [job, setJob] as const;
}

function JobStatus({ job }: { job: AiJob | null }) {
  if (!job) return null;
  if (job.status === "fallido") return <p className={styles.error}>⛔ {job.error ?? "La IA fallo"}</p>;
  if (job.status === "completado") return null;
  return (
    <div>
      <div className={styles.progress}>
        <div className={styles.progressFill} style={{ width: `${Math.max(5, job.progress)}%` }} />
      </div>
      <span className={styles.status}>{job.message ?? "En cola"}…</span>
    </div>
  );
}

const ACTIONS: Record<string, string> = { agregar: "Agregar", mover: "Mover", reemplazar: "Cambiar", quitar: "Quitar" };

function Outcomes({ outcomes }: { outcomes: AiOutcome[] }) {
  if (outcomes.length === 0) return <p className={styles.status}>La IA no propuso cambios.</p>;
  return (
    <ul className={styles.outcomes}>
      {outcomes.map((o, i) => (
        <li key={i}>
          <span>
            {o.applied ? "✅" : "⛔"} {ACTIONS[o.action] ?? o.action}{" "}
            <strong>{o.productName ?? o.instanceId ?? o.catalogId}</strong>
            {o.action !== "agregar" && o.productName && o.instanceId ? <span className={editor.mono}> ({o.instanceId})</span> : null}
          </span>
          {o.reason && <span className={styles.outcomeReason}>{o.reason}</span>}
          {!o.applied && (
            <span className={styles.rejected}>
              El motor lo rechazo: {o.reasons.length > 0 ? o.reasons.map(describe).join("; ") : o.verdict}
            </span>
          )}
        </li>
      ))}
    </ul>
  );
}

/** Copiar un diseno de internet en este cuarto: la IA propone, el motor valida y se crea una version nueva. */
function ReferenceSection({ designId, busy, onError }: Pick<Props, "designId" | "busy" | "onError">) {
  const router = useRouter();
  const input = useRef<HTMLInputElement>(null);
  const [picked, setPicked] = useState<{ file: File; url: string } | null>(null);
  const file = picked?.file ?? null;
  const [note, setNote] = useState("");
  const [over, setOver] = useState(false);
  const [job, setStarted] = useJob();
  const running = !!job && !isDone(job);

  // Libera la vista previa anterior
  useEffect(() => () => {
    if (picked) URL.revokeObjectURL(picked.url);
  }, [picked]);

  const pick = (f: File | undefined) => {
    if (!f) return;
    if (!IMAGE_TYPES.includes(f.type)) return onError("La imagen debe ser JPG, PNG o WEBP.");
    if (f.size > MAX_IMAGE_MB * 1024 * 1024) return onError(`La imagen no debe pasar de ${MAX_IMAGE_MB} MB.`);
    setPicked({ file: f, url: URL.createObjectURL(f) });
    setStarted(null);
  };

  const start = async () => {
    if (!file) return;
    try {
      setStarted(await api.aiReference(designId, file, note.trim()));
    } catch (e) {
      onError(errorText(e));
    }
  };

  const result = job?.status === "completado" ? job.result : null;

  return (
    <div className={styles.section}>
      <h4>Copiar un diseno de internet</h4>
      <span className={styles.status}>
        Sube la foto de un cuarto que te guste. La IA lo copia lo mejor posible en tu cuarto (con tus medidas, requisitos y presupuesto) en una
        version nueva; esta no se toca.
      </span>
      <div
        className={`${styles.drop} ${over ? styles.dropOn : ""}`}
        role="button"
        tabIndex={0}
        onClick={() => input.current?.click()}
        onKeyDown={(e) => (e.key === "Enter" || e.key === " ") && input.current?.click()}
        onDragOver={(e) => {
          e.preventDefault();
          setOver(true);
        }}
        onDragLeave={() => setOver(false)}
        onDrop={(e) => {
          e.preventDefault();
          setOver(false);
          pick(e.dataTransfer.files[0]);
        }}
      >
        {/* eslint-disable-next-line @next/next/no-img-element -- vista previa local (blob:) */}
        {picked ? <img src={picked.url} alt="Imagen de referencia" className={styles.thumb} /> : null}
        <span>{file ? `${file.name} · clic para cambiarla` : "Arrastra la imagen aqui o haz clic (JPG, PNG o WEBP, max. 8 MB)"}</span>
        <input ref={input} type="file" accept={IMAGE_TYPES.join(",")} hidden onChange={(e) => pick(e.target.files?.[0])} />
      </div>
      <textarea
        className={styles.textarea}
        placeholder="Nota opcional: ej. 'me gusta la alfombra y las plantas, no quiero gastar mas de 20 mil'"
        value={note}
        maxLength={500}
        onChange={(e) => setNote(e.target.value)}
      />
      <div className={styles.row}>
        <button type="button" className={styles.primary} disabled={!file || busy || running} onClick={() => void start()}>
          {running ? "Copiando…" : "Copiar en mi cuarto"}
        </button>
      </div>
      <JobStatus job={job} />
      {result && (
        <>
          {result.summary && (
            <p className={styles.summary}>
              <strong>La IA propone:</strong> {result.summary}
            </p>
          )}
          {result.style && <span className={styles.status}>Estilo: {label(result.style)}</span>}
          <Outcomes outcomes={result.outcomes ?? []} />
          {result.newDesignId && (
            <div className={styles.row}>
              <button type="button" className={styles.primary} onClick={() => router.push(`/designs/${result.newDesignId}`)}>
                Ver la version nueva
              </button>
              <span className={styles.status}>{result.applied ?? 0} cambios aplicados</span>
            </div>
          )}
        </>
      )}
    </div>
  );
}

/** Instruccion en texto: la IA propone, el motor revisa, el usuario decide si aplica. */
function ChatSection({ designId, busy, onApplied, onError }: Pick<Props, "designId" | "busy" | "onApplied" | "onError">) {
  const [text, setText] = useState("");
  const [applying, setApplying] = useState(false);
  const [appliedJob, setAppliedJob] = useState<string | null>(null);
  const [job, setStarted] = useJob();
  const running = !!job && !isDone(job);

  const send = async () => {
    if (!text.trim()) return;
    try {
      setAppliedJob(null);
      setStarted(await api.aiChat(designId, text.trim()));
    } catch (e) {
      onError(errorText(e));
    }
  };

  const apply = async () => {
    if (!job) return;
    setApplying(true);
    try {
      const r = await api.aiApply(job.id);
      setAppliedJob(job.id);
      onApplied(r.applied);
    } catch (e) {
      onError(errorText(e));
    } finally {
      setApplying(false);
    }
  };

  const result = job?.status === "completado" ? job.result : null;
  const applicable = result?.applicable ?? 0;

  return (
    <div className={styles.section}>
      <h4>Pedirle un cambio a la IA</h4>
      <textarea
        className={styles.textarea}
        placeholder="Ej. 'hazlo mas minimalista', 'cambia el sofa por uno mas chico', 'agrega una planta junto a la ventana'"
        value={text}
        maxLength={500}
        onChange={(e) => setText(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === "Enter" && (e.ctrlKey || e.metaKey)) void send();
        }}
      />
      <div className={styles.row}>
        <button type="button" className={styles.primary} disabled={!text.trim() || busy || running} onClick={() => void send()}>
          {running ? "Pensando…" : "Enviar"}
        </button>
        <span className={styles.status}>Ctrl+Enter para enviar</span>
      </div>
      <JobStatus job={job} />
      {result && (
        <>
          {result.summary && (
            <p className={styles.summary}>
              <strong>La IA propone:</strong> {result.summary}
            </p>
          )}
          <Outcomes outcomes={result.outcomes ?? []} />
          {appliedJob === job?.id ? (
            <span className={styles.status}>✅ Aplicado. Puedes deshacerlo en Historial.</span>
          ) : (
            <div className={styles.row}>
              <button type="button" className={styles.primary} disabled={applicable === 0 || busy || applying} onClick={() => void apply()}>
                {applying ? "Aplicando…" : `Aplicar lo valido (${applicable})`}
              </button>
              <span className={styles.status}>Nada cambia hasta que lo apliques.</span>
            </div>
          )}
        </>
      )}
    </div>
  );
}

/**
 * Imagenes con IA. Principal: render realista de lo que se ve en el visor 3D (respeta el acomodo).
 * Secundaria: imagen libre a partir de la descripcion. Ambas son solo ayuda visual: el modelo 3D es la fuente de verdad.
 */
function ImageSection({ designId, objectCount, busy, view, onView, capture, onError }: Pick<Props, "designId" | "objectCount" | "busy" | "view" | "onView" | "capture" | "onError">) {
  const [note, setNote] = useState("");
  const [last, setLast] = useState<AiJob | null>(null);
  const [job, setStarted] = useJob();
  const running = !!job && !isDone(job);

  // Ultima imagen generada para esta version
  useEffect(() => {
    let cancelled = false;
    api
      .aiJobs(designId)
      .then((jobs) => !cancelled && setLast(jobs.find((j) => j.type === "imagen" && j.status === "completado") ?? null))
      .catch(() => {});
    return () => {
      cancelled = true;
    };
  }, [designId]);

  const render = async () => {
    try {
      const shot = await capture();
      if (!shot?.image) return onError("No se pudo capturar la vista 3D.");
      setStarted(await api.aiRender(designId, shot.image, note.trim(), shot.visible));
    } catch (e) {
      onError(errorText(e));
    }
  };

  const free = async () => {
    try {
      setStarted(await api.aiImage(designId, note.trim() || undefined));
    } catch (e) {
      onError(errorText(e));
    }
  };

  const shown = job?.status === "completado" ? job : last;
  const imageUrl = shown?.result?.imageUrl;
  const disabled = objectCount === 0 || busy || running;

  return (
    <div className={styles.section}>
      <h4>Render realista</h4>
      <span className={styles.status}>
        La IA convierte lo que ves en el visor en una foto realista, con los muebles en el mismo lugar. Acomoda la camara como quieras;
        la vista <strong>Foto</strong> (a la altura de los ojos) es la que mas se parece a una foto real.
      </span>
      {view !== "foto" && (
        <div className={styles.row}>
          <button type="button" className={editor.smallButton} onClick={() => onView("foto")}>
            Cambiar a vista Foto
          </button>
        </div>
      )}
      <textarea
        className={styles.textarea}
        placeholder="Ambiente opcional: ej. 'de noche con luz calida', 'dia soleado', 'atardecer'"
        value={note}
        maxLength={300}
        onChange={(e) => setNote(e.target.value)}
      />
      <div className={styles.row}>
        <button type="button" className={styles.primary} disabled={disabled} onClick={() => void render()}>
          {running ? "Generando…" : "Render de esta vista"}
        </button>
        <button type="button" className={editor.smallButton} disabled={disabled} onClick={() => void free()} title="Imagen a partir de la descripcion del diseno, sin usar la vista 3D">
          Imagen libre
        </button>
        {objectCount === 0 && <span className={styles.status}>Agrega muebles primero.</span>}
      </div>
      <JobStatus job={job} />
      {imageUrl && (
        <figure style={{ margin: 0, display: "grid", gap: 6 }}>
          <a href={`${API_URL}${imageUrl}`} target="_blank" rel="noreferrer">
            {/* eslint-disable-next-line @next/next/no-img-element -- imagen servida por la API */}
            <img src={`${API_URL}${imageUrl}`} alt="Imagen de referencia de como quedaria la habitacion" className={styles.render} />
          </a>
          <figcaption className={styles.caption}>{shown?.result?.caption ?? CAPTION}</figcaption>
        </figure>
      )}
    </div>
  );
}

/** "Disenar con IA": la IA arma un diseno propio para el cuarto (version nueva; el motor valida cada mueble). */
function DesignSection({ designId, style, busy, onError }: Pick<Props, "designId" | "busy" | "onError"> & { style: string }) {
  const router = useRouter();
  const [target, setTarget] = useState(style);
  const [note, setNote] = useState("");
  const [job, setStarted] = useJob();
  const running = !!job && !isDone(job);

  const start = async () => {
    try {
      setStarted(await api.aiDesign(designId, target, note.trim() || undefined));
    } catch (e) {
      onError(errorText(e));
    }
  };

  const result = job?.status === "completado" ? job.result : null;

  return (
    <div className={styles.section}>
      <h4>Disenar con IA</h4>
      <span className={styles.status}>
        La IA arma un diseno completo para este cuarto (muebles, decoracion e iluminacion del catalogo) con tus medidas, requisitos
        y presupuesto. Se crea una version nueva; lo bloqueado y lo obligatorio se respetan.
      </span>
      <div className={styles.row}>
        <select className={styles.select} value={target} onChange={(e) => setTarget(e.target.value)} aria-label="Estilo">
          {STYLES.map((s) => (
            <option key={s} value={s}>
              {label(s)}
            </option>
          ))}
        </select>
      </div>
      <textarea
        className={styles.textarea}
        placeholder="Nota opcional: ej. 'que se sienta calido', 'espacio para trabajar', 'muchas plantas'"
        value={note}
        maxLength={500}
        onChange={(e) => setNote(e.target.value)}
      />
      <div className={styles.row}>
        <button type="button" className={styles.primary} disabled={busy || running} onClick={() => void start()}>
          {running ? "Disenando…" : "Disenar con IA"}
        </button>
      </div>
      <JobStatus job={job} />
      {result && (
        <>
          {result.summary && (
            <p className={styles.summary}>
              <strong>La IA propone:</strong> {result.summary}
            </p>
          )}
          <Outcomes outcomes={result.outcomes ?? []} />
          {result.newDesignId && (
            <div className={styles.row}>
              <button type="button" className={styles.primary} onClick={() => router.push(`/designs/${result.newDesignId}`)}>
                Ver el diseno de la IA
              </button>
              <span className={styles.status}>{result.applied ?? 0} muebles colocados</span>
            </div>
          )}
        </>
      )}
    </div>
  );
}

const VIEW_NAMES: Record<string, string> = { so: "suroeste", se: "sureste", ne: "noreste", no: "noroeste" };

/** Render exacto en Blender: sale directo del DesignState (sin IA), con medidas y posiciones reales. */
function BlenderSection({ designId, objectCount, busy, onError }: Pick<Props, "designId" | "objectCount" | "busy" | "onError">) {
  const [views, setViews] = useState("se");
  const [quality, setQuality] = useState<"rapida" | "alta">("rapida");
  const [last, setLast] = useState<AiJob | null>(null);
  const [job, setStarted] = useJob();
  const running = !!job && !isDone(job);

  useEffect(() => {
    let cancelled = false;
    api
      .aiJobs(designId)
      .then((jobs) => !cancelled && setLast(jobs.find((j) => j.type === "blender" && j.status === "completado") ?? null))
      .catch(() => {});
    return () => {
      cancelled = true;
    };
  }, [designId]);

  const start = async () => {
    try {
      setStarted(await api.blender(designId, views, quality));
    } catch (e) {
      onError(errorText(e));
    }
  };

  const shown = job?.status === "completado" ? job : last;
  const estimate = (quality === "alta" ? 3.5 : 0.7) * (views === "all" ? 4 : 1);

  return (
    <div className={styles.section}>
      <h4>Render exacto en Blender</h4>
      <span className={styles.status}>
        Render fotografico hecho en Blender directo del modelo: cada mueble con su medida y posicion real (sin IA). Tambien genera el
        archivo .blend para abrirlo en Blender.
      </span>
      <div className={styles.row}>
        <select className={styles.select} value={views} onChange={(e) => setViews(e.target.value)} aria-label="Vistas">
          <option value="se">1 vista (esquina sureste)</option>
          <option value="all">4 vistas (las 4 esquinas)</option>
        </select>
        <select className={styles.select} value={quality} onChange={(e) => setQuality(e.target.value as "rapida" | "alta")} aria-label="Calidad">
          <option value="rapida">Rapida (800x500)</option>
          <option value="alta">Alta (1280x800)</option>
        </select>
      </div>
      <div className={styles.row}>
        <button type="button" className={styles.primary} disabled={objectCount === 0 || busy || running} onClick={() => void start()}>
          {running ? "Renderizando…" : "Renderizar en Blender"}
        </button>
        <span className={styles.status}>~{estimate < 1 ? "1" : Math.round(estimate)} min en tu computadora</span>
      </div>
      <JobStatus job={job} />
      {shown?.result?.images && (
        <>
          <div className={styles.renders}>
            {shown.result.images.map((img) => (
              <a key={img.url} href={`${API_URL}${img.url}`} target="_blank" rel="noreferrer" title={`Vista ${VIEW_NAMES[img.view] ?? img.view}`}>
                {/* eslint-disable-next-line @next/next/no-img-element -- imagen servida por la API */}
                <img src={`${API_URL}${img.url}`} alt={`Render de Blender, vista ${VIEW_NAMES[img.view] ?? img.view}`} className={styles.render} />
              </a>
            ))}
          </div>
          <p className={styles.caption}>{shown.result.caption}</p>
          {shown.result.blendUrl && (
            <a className={editor.smallButton} href={`${API_URL}${shown.result.blendUrl}`} download>
              Descargar .blend
            </a>
          )}
        </>
      )}
    </div>
  );
}

/** Pestana IA del editor. Todo lo que propone la IA pasa por el motor de validacion antes de aplicarse. */
export function AiPanel(props: Props) {
  return (
    <section className={editor.card}>
      <DesignSection designId={props.designId} style={props.style} busy={props.busy} onError={props.onError} />
      <ImageSection
        designId={props.designId}
        objectCount={props.objectCount}
        busy={props.busy}
        view={props.view}
        onView={props.onView}
        capture={props.capture}
        onError={props.onError}
      />
      <BlenderSection designId={props.designId} objectCount={props.objectCount} busy={props.busy} onError={props.onError} />
      <ReferenceSection designId={props.designId} busy={props.busy} onError={props.onError} />
      <ChatSection designId={props.designId} busy={props.busy} onApplied={props.onApplied} onError={props.onError} />
      <p className={editor.hint}>La IA solo propone: el motor valida cada cambio y tu decides.</p>
    </section>
  );
}
