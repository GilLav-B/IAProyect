import type {
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

  config: (id: string, instanceId: string, config: { color?: string; material?: string }) =>
    request<{ object: PlacedObject; report: ValidationReport }>(`/api/designs/${id}/objects/${instanceId}/config`, {
      method: "PATCH",
      body: JSON.stringify(config),
    }),
};
