"use client";

import dynamic from "next/dynamic";

// La vista previa 3D (three.js) genera ids aleatorios al crearse: no se puede prerenderizar en el servidor.
const NewProject = dynamic(() => import("@/components/NewProject").then((m) => m.NewProject), {
  ssr: false,
  loading: () => <main style={{ padding: 24 }}>Cargando…</main>,
});

export function NewProjectPage() {
  return <NewProject />;
}
