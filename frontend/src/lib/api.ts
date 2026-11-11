import type {
  AiApplyResult,
  DesignLists,
  AiJob,
  ApplyResult,
  BudgetInfo,
  CatalogMaterial,
  CompareResult,
  HistoryEntry,
  Saving,
  DesignState,
  RoomWithRequirements,
  Move,
  MoveBatchResult,
  PlacedObject,
  Product,
  ProjectDetail,
  Proposal,
  ValidationReport,
  Verdict,
} from "./types";

export const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

export class ApiError extends Error {
  constructor(
    public status: number,
    public body: unknown,
  ) {
    super(`La API respondio ${status}`);
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${API_URL}${path}`, {
    ...init,
    headers: { "Content-Type": "application/json", ...init?.headers },
    cache: "no-store",
  });
  const text = await res.text();
  const body = text ? JSON.parse(text) : null;
  if (!res.ok) throw new ApiError(res.status, body);
  return body as T;
}

/** Manda una imagen como multipart (campo "image") y regresa el trabajo de IA creado. */
async function postImage(path: string, image: Blob, fileName: string, note: string, extra?: Record<string, string>): Promise<AiJob> {
  const form = new FormData();
  form.append("image", image, image instanceof File ? image.name : fileName);
  form.append("note", note);
  for (const [k, v] of Object.entries(extra ?? {})) form.append(k, v);
  // Sin Content-Type: el navegador pone el boundary del multipart
  const res = await fetch(`${API_URL}${path}`, { method: "POST", body: form, cache: "no-store" });
  const text = await res.text();
  const body = text ? (() => { try { return JSON.parse(text); } catch { return text; } })() : null;
  if (!res.ok) throw new ApiError(res.status, body);
  return body as AiJob;
}

export const api = {
  projects: () => request<{ id: string; name: string; type: string }[]>("/api/projects"),
  project: (id: string) => request<ProjectDetail>(`/api/projects/${id}`),
  design: (id: string) => request<DesignState>(`/api/designs/${id}`),
  validation: (id: string) => request<ValidationReport>(`/api/designs/${id}/validation`),
  products: () => request<Product[]>("/api/catalog/products"),
  materials: () => request<CatalogMaterial[]>("/api/catalog/materials"),

  createProject: (body: { id?: string; name: string; type: string }) =>
    request<{ id: string }>("/api/projects", { method: "POST", body: JSON.stringify(body) }),
  createRoom: (projectId: string, body: RoomWithRequirements) =>
    request<RoomWithRequirements>(`/api/projects/${projectId}/rooms`, { method: "POST", body: JSON.stringify(body) }),
  room: (roomId: string) => request<RoomWithRequirements>(`/api/rooms/${roomId}`),
  updateRoom: (roomId: string, body: RoomWithRequirements) =>
    request<RoomWithRequirements>(`/api/rooms/${roomId}`, { method: "PUT", body: JSON.stringify(body) }),
  createDesign: (state: DesignState) =>
    request<{ state: DesignState; report: ValidationReport }>("/api/designs", { method: "POST", body: JSON.stringify(state) }),

  /** Agrega un producto: el motor elige el lugar. Si no se puede, 409 con los motivos. */
  async addObject(id: string, catalogId: string): Promise<{ ok: true; result: ApplyResult } | { ok: false; verdict: Verdict }> {
    try {
      const result = await request<ApplyResult>(`/api/designs/${id}/objects`, { method: "POST", body: JSON.stringify({ catalogId }) });
      return { ok: true, result };
    } catch (e) {
      if (e instanceof ApiError && e.status === 409) return { ok: false, verdict: e.body as Verdict };
      throw e;
    }
  },

  /** Valida y aplica. Si el motor la rechaza la API responde 409 con el veredicto. */
  async apply(id: string, proposal: Proposal): Promise<{ ok: true; result: ApplyResult } | { ok: false; verdict: Verdict }> {
    try {
      const result = await request<ApplyResult>(`/api/designs/${id}/proposals/apply`, {
        method: "POST",
        body: JSON.stringify(proposal),
      });
      return { ok: true, result };
    } catch (e) {
      if (e instanceof ApiError && e.status === 409) return { ok: false, verdict: e.body as Verdict };
      throw e;
    }
  },

  /** Revisa el borrador sin guardar (para pintar conflictos en vivo). */
  checkMoves: (id: string, moves: Move[]) =>
    request<MoveBatchResult>(`/api/designs/${id}/proposals/moves?dryRun=true`, {
      method: "POST",
      body: JSON.stringify({ moves }),
    }),

  /** Guarda el borrador solo si todos los movimientos son validos; si no, 409 con los motivos. */
  async saveMoves(id: string, moves: Move[]): Promise<{ ok: boolean; result: MoveBatchResult }> {
    try {
      const result = await request<MoveBatchResult>(`/api/designs/${id}/proposals/moves`, {
        method: "POST",
        body: JSON.stringify({ moves }),
      });
      return { ok: true, result };
    } catch (e) {
      if (e instanceof ApiError && e.status === 409) return { ok: false, result: e.body as MoveBatchResult };
      throw e;
    }
  },

  budget: (id: string) => request<BudgetInfo>(`/api/designs/${id}/budget`),
  savings: (id: string) => request<Saving[]>(`/api/designs/${id}/savings`),
  history: (id: string) => request<HistoryEntry[]>(`/api/designs/${id}/history`),
  revert: (id: string, historyId: number) =>
    request<DesignState>(`/api/designs/${id}/history/${historyId}/revert`, { method: "POST" }),
  duplicate: (id: string, name?: string) =>
    request<DesignState>(`/api/designs/${id}/duplicate`, { method: "POST", body: JSON.stringify({ name }) }),
  compare: (a: string, b: string) => request<CompareResult>(`/api/designs/${a}/compare/${b}`),
  patchDesign: (id: string, patch: { name?: string; style?: string; budgetMode?: string; favorite?: boolean }) =>
    request<ValidationReport>(`/api/designs/${id}`, { method: "PATCH", body: JSON.stringify(patch) }),
  deleteDesign: (id: string) => request<null>(`/api/designs/${id}`, { method: "DELETE" }),

  lock: (id: string, instanceId: string, locked: boolean) =>
    request<PlacedObject>(`/api/designs/${id}/objects/${instanceId}/lock`, {
      method: "PATCH",
      body: JSON.stringify({ locked }),
    }),

  // ---------- IA: los trabajos los procesa n8n; el motor valida todo lo que propone la IA ----------

  /** Copiar un diseno de referencia (imagen) en este cuarto. Al terminar crea una version nueva. */
  aiReference: (id: string, image: File, note: string) => postImage(`/api/designs/${id}/ai/reference`, image, "referencia", note),
  /** Render realista con IA a partir de una captura del visor 3D (respeta el acomodo). */
  aiRender: (id: string, capture: Blob, note: string, visible: { instanceId: string; where: string }[], batch?: { id: string; view: number }) =>
    postImage(`/api/designs/${id}/ai/render`, capture, "vista.jpg", note, {
      visible: JSON.stringify(visible),
      ...(batch && { batch: batch.id, view: String(batch.view) }),
    }),
  aiChat: (id: string, message: string) =>
    request<AiJob>(`/api/designs/${id}/ai/chat`, { method: "POST", body: JSON.stringify({ message }) }),
  aiImage: (id: string, note?: string) =>
    request<AiJob>(`/api/designs/${id}/ai/image`, { method: "POST", body: JSON.stringify({ note }) }),
  aiJobs: (id: string) => request<AiJob[]>(`/api/designs/${id}/ai/jobs`),
  /** "Disenar con IA": la IA arma un diseno propio (version nueva). */
  aiDesign: (id: string, style?: string, note?: string) =>
    request<AiJob>(`/api/designs/${id}/ai/design`, { method: "POST", body: JSON.stringify({ style, note }) }),
  /** Render exacto en Blender (minutos en CPU). */
  blender: (id: string, views: string, quality: "rapida" | "alta") =>
    request<AiJob>(`/api/designs/${id}/blender`, { method: "POST", body: JSON.stringify({ views, quality }) }),

  // ---------- Fase 7: listas y exportacion ----------
  lists: (id: string) => request<DesignLists>(`/api/designs/${id}/lists`),
  /** URL de descarga: pdf, xlsx, csv, plan.svg o plan.pdf (lo genera el servicio de IA en Python). */
  exportUrl: (id: string, format: string) => `${API_URL}/api/designs/${id}/export/${format}`,
  aiJob: (jobId: string) => request<AiJob>(`/api/ai/jobs/${jobId}`),
  aiApply: (jobId: string) => request<AiApplyResult>(`/api/ai/jobs/${jobId}/apply`, { method: "POST" }),
  /** Avance en vivo (SSE). */
  aiEventsUrl: (jobId: string) => `${API_URL}/api/ai/jobs/${jobId}/events`,

  config: (id: string, instanceId: string, config: { color?: string; material?: string }) =>
    request<{ object: PlacedObject; report: ValidationReport }>(`/api/designs/${id}/objects/${instanceId}/config`, {
      method: "PATCH",
      body: JSON.stringify(config),
    }),
};
