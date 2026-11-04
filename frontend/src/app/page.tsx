"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { api, API_URL } from "@/lib/api";
import type { ProjectDetail } from "@/lib/types";
import { fmtRange, label } from "@/lib/units";
import styles from "./page.module.css";

export default function Home() {
  const [projects, setProjects] = useState<ProjectDetail[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    api
      .projects()
      .then((list) => Promise.all(list.map((p) => api.project(p.id))))
      .then((details) => !cancelled && setProjects(details))
      .catch(() => !cancelled && setError(`No se pudo conectar con la API en ${API_URL}. ¿Esta corriendo el backend?`));
    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <main className={styles.main}>
      <h1>Diseno de interiores</h1>
      <p className={styles.muted}>Elige una version de diseno para abrirla en el editor 3D.</p>

      {error && <p className={styles.error}>{error}</p>}
      {!projects && !error && <p>Cargando proyectos…</p>}
      {projects?.length === 0 && <p>No hay proyectos todavia.</p>}

      {projects?.map((p) => (
        <section key={p.id} className={styles.project}>
          <h2>
            {p.name} <span className={styles.muted}>· {label(p.type)}</span>
          </h2>
          <ul className={styles.designs}>
            {p.designs.map((d) => (
              <li key={d.id}>
                <Link href={`/designs/${encodeURIComponent(d.id)}`} className={styles.designCard}>
                  <div className={styles.designHead}>
                    <strong>{d.name}</strong>
                    <span className={d.valid ? styles.ok : styles.bad}>{d.valid ? "Valido" : "Con conflictos"}</span>
                  </div>
                  <div className={styles.muted}>
                    {d.id} · {d.style} · presupuesto {d.budgetMode}
                    {d.parentId && <> · viene de {d.parentId}</>}
                  </div>
                  <div className={styles.total}>{fmtRange(d.total)}</div>
                </Link>
              </li>
            ))}
          </ul>
        </section>
      ))}
    </main>
  );
}
