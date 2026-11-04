"use client";

import { use } from "react";
import { Editor } from "@/components/Editor";

export function DesignEditorPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  return <Editor designId={decodeURIComponent(id)} />;
}
