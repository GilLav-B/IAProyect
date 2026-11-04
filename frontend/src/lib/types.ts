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

export type Proposal =
  | { action: "mover"; instanceId: string; x: number; y: number; rotation: number }
  | { action: "reemplazar"; instanceId: string; catalogId: string };
