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
      <header className={styles.hero}>
        <div className={styles.brand}>IAProyect</div>
        <h1>Diseno de interiores asistido por IA</h1>
        <p className={styles.muted}>
          Abre una version para editarla en 3D. Cada cambio pasa por el motor de reglas: medidas reales, puertas, ventanas y
          presupuesto.
        </p>
        <Link href="/nuevo" className={styles.newButton}>
          + Nuevo proyecto desde cero
        </Link>
      </header>

      {error && <p className={styles.error}>{error}</p>}
      {!projects && !error && <p className={styles.muted}>Cargando proyectos…</p>}
      {projects?.length === 0 && <p>No hay proyectos todavia.</p>}

      {projects?.map((p) => (
        <section key={p.id} className={styles.project}>
          <h2>
            {p.name} <span className={styles.tag}>{label(p.type)}</span>
          </h2>
          <ul className={styles.designs}>
            {p.designs.map((d, i) => (
              <li key={d.id}>
                <Link href={`/designs/${encodeURIComponent(d.id)}`} className={styles.designCard}>
                  <div className={styles.cardTop} data-style={d.style}>
                    <span className={styles.number}>{i + 1}</span>
                    <span className={styles.styleName}>{label(d.style)}</span>
                  </div>
                  <div className={styles.cardBody}>
                    <div className={styles.designHead}>
                      <strong>{d.name}</strong>
                      <span className={d.valid ? styles.ok : styles.bad}>{d.valid ? "Valido" : "Con conflictos"}</span>
                    </div>
                    <div className={styles.muted}>
                      presupuesto {d.budgetMode}
                      {d.parentId && <> · viene de {d.parentId}</>}
                    </div>
                    <div className={styles.total}>{fmtRange(d.total)}</div>
                  </div>
                </Link>
              </li>
            ))}
          </ul>
        </section>
      ))}
    </main>
  );
}
