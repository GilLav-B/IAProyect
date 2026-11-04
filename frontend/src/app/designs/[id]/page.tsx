import { Suspense } from "react";
import { DesignEditorPage } from "./DesignEditorPage";

// Con cacheComponents, leer params pide un limite de Suspense; el editor es 100% de cliente.
export default function Page({ params }: PageProps<"/designs/[id]">) {
  return (
    <Suspense fallback={<main style={{ padding: 24 }}>Cargando…</main>}>
      <DesignEditorPage params={params} />
    </Suspense>
  );
}
