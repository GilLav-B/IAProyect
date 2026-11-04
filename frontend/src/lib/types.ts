// Tipos del contrato JSON (schemas/design-state.schema.json y catalog.schema.json).
// Medidas en cm enteros; precios en MXN.

export interface PriceRange {
  min: number;
  max: number;
}

export interface Door {
  id: string;
  wall: Wall;
  position: number;
  width: number;
}

export interface Window {
  id: string;
  wall: Wall;
  position: number;
  width: number;
  sillHeight: number;
  height?: number;
}

export type Wall = "norte" | "sur" | "este" | "oeste";

export interface Room {
  id: string;
  name?: string;
  spaceType: string;
  length: number;
  width: number;
  height: number;
  doors: Door[];
  windows: Window[];
  columns: { id: string; x: number; y: number; width: number; depth: number }[];
  measurementSource?: string;
  /** Acabados actuales: piso (material del catalogo y/o color) y color de muros. */
  surfaces?: {
    floor?: { materialId?: string; color?: string };
    walls?: { materialId?: string; color?: string };
    ceiling?: { materialId?: string; color?: string };
  };
}

/** Material del catalogo (pisos, pintura...). */
export interface CatalogMaterial {
  id: string;
  type: string;
  name: string;
  pricePerM2: PriceRange;
}

export interface Requirement {
  id: string;
  priority: "obligatorio" | "importante" | "preferencia" | "opcional";
  type: "presupuesto_max" | "tiene" | "conservar" | "estilo" | "color" | "categoria";
  amount?: number;
  category?: string;
  attribute?: string;
  value?: string | number;
  element?: string;
  style?: string;
  color?: string;
  description?: string;
}

export interface PlacedObject {
  instanceId: string;
  catalogId: string;
  type: string;
  position: { x: number; y: number; z: number };
  rotation: number;
  config?: { color?: string; material?: string };
  locked: boolean;
}

export interface DesignState {
  project: { id: string; name: string; type: string };
  design: {
    id: string;
    name: string;
    parentId?: string | null;
    style: string;
    budgetMode: string;
    revision: number;
  };
  room: Room;
  requirements?: Requirement[];
  objects: PlacedObject[];
  budget?: { total: PriceRange; effectiveCap?: number };
}

export interface Product {
  id: string;
  name: string;
  category: string;
  kind: "real" | "conceptual";
  dimensions: { width: number; depth: number; height: number };
  materials: string[];
  colors: string[];
  styles: string[];
  price: PriceRange;
  attributes: Record<string, string | number>;
  /** Ruta del modelo 3D (GLB). Sin modelo, el mueble se arma en codigo. */
  asset3D?: string;
}

export interface Reason {
  code: string;
  args: string[];
  term: string;
}

export interface ValidationReport {
  designId: string;
  valid: boolean;
  conflicts: Reason[];
  warnings: Reason[];
  budget: { total: PriceRange; effectiveCap?: number };
}

export interface Verdict {
  accepted: boolean;
  verdict: string;
  reasons: Reason[];
}

export interface ApplyResult {
  verdict: Verdict;
  state: DesignState;
  report: ValidationReport;
}

export interface DesignSummary {
  id: string;
  name: string;
  parentId?: string | null;
  roomId: string;
  style: string;
  budgetMode: string;
  revision: number;
  valid: boolean;
  total: PriceRange;
}

export interface ProjectDetail {
  id: string;
  name: string;
  type: string;
  roomIds: string[];
  designs: DesignSummary[];
}

/** Movimiento del borrador del editor (cm, grados). */
export interface Move {
  instanceId: string;
  x: number;
  y: number;
  rotation: number;
}

export interface MoveBatchResult {
  accepted: boolean;
  results: { instanceId: string; verdict: Verdict }[];
  report: ValidationReport;
  state?: DesignState;
}

export type Proposal =
  | { action: "mover"; instanceId: string; x: number; y: number; rotation: number }
  | { action: "reemplazar"; instanceId: string; catalogId: string }
  | { action: "quitar"; instanceId: string };

/** GET /api/designs/{id}/budget */
export interface BudgetInfo {
  designId: string;
  budgetMode: "minimizar" | "ajustar" | "flexible";
  budgetMax?: number;
  effectiveCap?: number;
  byCategory: Record<string, PriceRange>;
  total: PriceRange;
  status: "sin_tope" | "dentro" | "puede_excederse" | "excedido";
}

/** Reemplazo valido que ahorra dinero (sustitucion_ahorro/4). */
export interface Saving {
  instanceId: string;
  currentCatalogId: string;
  suggestedCatalogId: string;
  suggestedName: string;
  saving: number;
}

export interface HistoryEntry {
  id: number;
  date: string;
  author?: string;
  description: string;
  changedInstanceIds: string[];
  revision?: number;
}

export interface CompareResult {
  a: string;
  b: string;
  differences: Reason[];
  totalA: PriceRange;
  totalB: PriceRange;
}

/** Habitacion + requisitos, como la manda y recibe /api/rooms. */
export interface RoomWithRequirements {
  room: Room;
  requirements: Requirement[];
}
