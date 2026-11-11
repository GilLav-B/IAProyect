"use client";

import { useParams } from "next/navigation";
import { Editor } from "@/components/Editor";

/**
 * El id se lee en el cliente con useParams: el editor es 100% de cliente y asi no depende de que llegue
 * la parte en streaming del servidor (con `use(params)` se podia quedar en "Cargando…" en algunos navegadores).
 */
export function DesignEditorPage() {
  const { id } = useParams<{ id: string }>();
  return <Editor designId={decodeURIComponent(id)} />;
}
