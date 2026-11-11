import { Suspense } from "react";
import { DesignEditorPage } from "./DesignEditorPage";

// Con cacheComponents la ruta dinamica pide un limite de Suspense; el editor es 100% de cliente.
export default function Page() {
  return (
    <Suspense fallback={<main style={{ padding: 24 }}>Cargando…</main>}>
      <DesignEditorPage />
    </Suspense>
  );
}
