import type {
  ApplyResult,
  DesignState,
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
