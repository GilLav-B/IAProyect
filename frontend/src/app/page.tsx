"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { api, API_URL } from "@/lib/api";
import type { DesignState, DesignSummary, Product, ProjectDetail } from "@/lib/types";
import { fmtRange, label } from "@/lib/units";
import { PlanDrawing } from "@/components/PlanDrawing";
import styles from "./page.module.css";

/** Hoja de una version: su plano dibujado con los datos reales, nombre, estilo, estado y costo. */
function DesignSheet({ design, index, products }: { design: DesignSummary; index: number; products: Map<string, Product> }) {
  const [state, setState] = useState<DesignState | null>(null);

  useEffect(() => {
    let cancelled = false;
    api
      .design(design.id)
      .then((s) => !cancelled && setState(s))
      .catch(() => {});
    return () => {
      cancelled = true;
    };
  }, [design.id]);

  return (
    <Link href={`/designs/${encodeURIComponent(design.id)}`} className={styles.sheet}>
      <div className={styles.sheetPlan}>
        {state && products.size > 0 ? <PlanDrawing state={state} products={products} /> : <div className={styles.planLoading} />}
        <span className={styles.sheetNumber} aria-label={`Version ${index + 1}`}>
          {index + 1}
        </span>
      </div>
      <div className={styles.sheetBody}>
        <div className={styles.sheetHead}>
          <strong className={styles.sheetName}>{design.name}</strong>
          <span className={design.valid ? styles.ok : styles.bad}>{design.valid ? "Valido" : "Con conflictos"}</span>
        </div>
        <div className={styles.sheetMeta}>
          <span>{label(design.style)}</span>
          <span>Presupuesto {design.budgetMode}</span>
        </div>
        <div className={styles.sheetTotal}>{fmtRange(design.total)}</div>
      </div>
    </Link>
  );
}

export default function Home() {
  const [projects, setProjects] = useState<ProjectDetail[] | null>(null);
  const [products, setProducts] = useState<Product[]>([]);
  const [hero, setHero] = useState<DesignState | null>(null);
  const [error, setError] = useState<string | null>(null);
  const productMap = useMemo(() => new Map(products.map((p) => [p.id, p])), [products]);

  useEffect(() => {
    let cancelled = false;
    Promise.all([api.projects().then((list) => Promise.all(list.map((p) => api.project(p.id)))), api.products()])
      .then(([details, catalog]) => {
        if (cancelled) return;
        setProjects(details);
        setProducts(catalog);
        // El plano grande es el de la sala de ejemplo (o el primer diseno que haya)
        const first = details.find((p) => p.id === "proyecto_ejemplo")?.designs[0] ?? details.flatMap((p) => p.designs)[0];
        if (first) api.design(first.id).then((s) => !cancelled && setHero(s)).catch(() => {});
      })
      .catch(() => !cancelled && setError(`No se pudo conectar con la API en ${API_URL}. Arranca el backend y recarga la pagina.`));
    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <div className={styles.page}>
      <header className={styles.bar}>
        <Link href="/" className={styles.wordmark}>
          IAProyect
        </Link>
        <nav className={styles.nav} aria-label="Principal">
          <a href="#proyectos">Proyectos</a>
          <Link href="/nuevo" className={styles.navButton}>
            Nuevo proyecto
          </Link>
        </nav>
      </header>

      <section className={styles.hero}>
        <div className={styles.heroText}>
          <h1>Disena tu espacio sobre medidas reales</h1>
          <p>
            Acomoda muebles en 3D, pide propuestas a la IA y revisa cuanto cuesta antes de comprar. Cada cambio se revisa contra
            puertas, ventanas y tu presupuesto.
          </p>
          <div className={styles.heroActions}>
            <Link href="/nuevo" className={styles.primary}>
              Crear proyecto
            </Link>
            {hero && (
              <Link href={`/designs/${encodeURIComponent(hero.design.id)}`} className={styles.secondary}>
                Abrir el ejemplo
              </Link>
            )}
          </div>
        </div>
        <figure className={styles.heroPlan}>
          {hero && products.length > 0 ? (
            <PlanDrawing state={hero} products={productMap} variant="hero" />
          ) : (
            <div className={styles.heroPlaceholder} />
          )}
          {hero && (
            <figcaption>
              {hero.room.name} de {(hero.room.length / 100).toFixed(2)} × {(hero.room.width / 100).toFixed(2)} m con {hero.objects.length} muebles,
              dibujada con los datos del diseno.
            </figcaption>
          )}
        </figure>
      </section>

      <section id="proyectos" className={styles.projects}>
        <h2>Tus proyectos</h2>
        {error && <p className={styles.error}>{error}</p>}
        {!projects && !error && <p className={styles.muted}>Cargando proyectos…</p>}
        {projects?.length === 0 && (
          <p className={styles.empty}>
            Todavia no hay proyectos. <Link href="/nuevo">Crea el primero</Link> con las medidas de tu cuarto.
          </p>
        )}
        {projects?.map((p) => (
          <article key={p.id} className={styles.project}>
            <header className={styles.projectHead}>
              <h3>{p.name}</h3>
              <span className={styles.projectMeta}>
                {label(p.type)}, {p.designs.length} {p.designs.length === 1 ? "version" : "versiones"}
              </span>
            </header>
            <ul className={styles.sheets}>
              {p.designs.map((d, i) => (
                <li key={d.id}>
                  <DesignSheet design={d} index={i} products={productMap} />
                </li>
              ))}
            </ul>
          </article>
        ))}
      </section>
    </div>
  );
}
