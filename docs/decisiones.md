# Decisiones del proyecto

Registro de decisiones aprobadas por el equipo. Si algo cambia, se actualiza aqui con la fecha.

## Imagenes, renders y realismo (2026-11-04)

1. **La fuente de verdad es el DesignState** (medidas, posiciones, productos, precios). Ninguna imagen lo reemplaza.
2. **Tres niveles de visualizacion:**

   | Nivel | Para que | Fidelidad |
   |---|---|---|
   | Visor 3D web (Three.js) | Editar en tiempo real | Exacta (medidas y posiciones reales), estilo semi-realista |
   | Render con Blender | Ver el resultado final de forma fotorrealista | Exacta: se genera del mismo DesignState (JSON -> Python -> Blender) |
   | Imagen generada por IA (API de OpenAI) | **Ayuda visual / referencia** para la persona | Aproximada: puede no respetar medidas, posiciones ni productos |

3. **Las imagenes generadas por IA son solo una ayuda visual.** No se usan para medir, presupuestar, comprar ni validar.
   - En la interfaz se muestran siempre con la leyenda: *"Imagen de referencia generada por IA. No representa medidas ni productos exactos."*
   - Se guardan como representacion del diseno (con su version), nunca como dato del DesignState.
4. **Camino preferido para una imagen realista y fiel:** render de Blender primero y, de forma opcional, la IA mejora la ambientacion usando ese render como base, sin cambiar la distribucion.
5. La generacion de imagenes y renders es asincrona (n8n) con estados: pendiente, en_proceso, completado, fallido.
6. Las claves de API (OpenAI u otras) van solo en `.env`, nunca en el codigo ni en el chat.

Esto se aplica en la Fase 5 (`ai-rules/image-generator.md` y `safety-rules.md`) y en la Fase 6 (Blender).

## Stack y datos (Fases 0 y 1)

- Backend .NET 10 (ASP.NET Core, REST + SSE); frontend Next.js + React Three Fiber; PostgreSQL; n8n solo como orquestador.
- Reglas: motor en C# (fuente de la validacion) y `diseno.pl` (SWI-Prolog) como verificador en pruebas y en ejecucion.
- Medidas en cm enteros (se muestran en m); precios en MXN como rangos min-max.
- Claves del JSON en ingles, valores en espanol sin acentos; 4 niveles de prioridad (obligatorio, importante, preferencia, opcional).
- Rotacion de objetos en multiplos de 90 grados (igual que `diseno.pl`).

## Editor 3D (Fase 3)

- Se puede mover y rotar libremente aunque un mueble atraviese otro; lo que interfiere se marca en rojo.
- Solo se puede **guardar** si el motor no encuentra interferencias. Los objetos bloqueados no se mueven.
- Los muebles se dibujan con medidas exactas del catalogo (la huella que valida el motor no cambia).

## Estilo visual del visor 3D (2026-11-04)

- Assets gratuitos **CC0 de Poly Haven** (`frontend/public/assets`, ver `CREDITOS.md`; se bajan con `npm run assets`):
  modelos GLB (mesa de centro, mueble TV, librero, escritorio, planta), texturas PBR (piso laminado, yeso, tela, roble) y HDRI de interior.
- Cada producto puede traer `asset3D` (ruta del GLB). Sin modelo, el mueble se arma en codigo (sofa, cama, TV, lamparas, alfombra, isla,
  cuadro, comedor, silla de oficina) con la textura del material y el **color elegido en el configurador**.
- Convencion de frente: rotacion 0 = mira al sur; 90 = este; 180 = norte; 270 = oeste.
- La TV y el cuadro "no ocupan piso" (como en `diseno.pl`): la TV se dibuja sobre el mueble que tiene debajo y el cuadro colgado
  a 1.50 m. Es solo visual; el motor no cambia.
- Vista tipo "casa de munecas": los muros cercanos a la camara se ocultan solos. Vistas 3D y Planta.

## Crear desde cero y acabados (2026-11-04)

- Pagina `/nuevo`: proyecto -> habitacion (medidas, puertas, ventanas) -> acabados (color de muros, tipo de piso) -> requisitos
  (presupuesto maximo obligatorio, estilo como preferencia, modo de presupuesto) -> primera version vacia.
- [SUPUESTO] Rangos de medidas: largo y ancho de 1.50 a 20 m; alto de 2 a 6 m. Puertas y ventanas deben quedar dentro de su
  pared y no encimarse (`RoomRules` en el motor; la interfaz avisa antes con las mismas reglas).
- Espacios por tipo de proyecto como en `diseno.pl`: comercial = oficina; domestico = sala, recamara, cocina, comedor.
- Los acabados (`surfaces`) son el estado ACTUAL del cuarto y son solo visuales: no suman al presupuesto. Los materiales de
  remodelacion con costo por m2 siguen en `materials` del DesignState (rubro materiales).
- Agregar un mueble: el motor busca el primer lugar libre valido (rejilla de 10 cm, 4 rotaciones). [SUPUESTO] Orden de
  preferencia: sofa, cama, mueble TV, librero, escritorio y cuadro contra la pared y de espaldas a ella; plantas y lamparas
  en esquinas; la TV sobre un mueble de TV si existe; lo demas al centro. Si no se puede por presupuesto, categoria o falta
  de espacio, responde con los motivos.

## Versiones, presupuesto, historial y conflictos (Fase 4, 2026-11-04)

- Pestanas tipo Excel debajo del visor: cada version del mismo cuarto. Duplicar crea una copia independiente (la original
  no cambia); doble clic o "Renombrar" cambia el nombre; "Comparar con..." muestra diferencias por categoria y presupuestos;
  "Borrar" no permite quitar la unica version.
- Presupuesto por rubro con rango min-max, linea del tope y estado (dentro / podria rebasarse / excedido).
- Modos: minimizar (resalta los ahorros sugeridos), ajustar (aprovechar sin pasarse), flexible (+10% sobre el tope, como en
  `diseno.pl`). Los ahorros son solo reemplazos que el motor valida (`sustitucion_ahorro/4`).
- Historial: "Deshacer ultimo cambio" y "Volver aqui" en cualquier revision. Revertir no borra: queda como un cambio nuevo.
- Requisitos editables (prioridad, monto del presupuesto, agregar/quitar). Aplican a todas las versiones de la habitacion.
- Cuando hay `presupuesto_excedido` o `falta_requisito`, el usuario decide que gana: subir el tope, usar modo flexible, ver
  ahorros, agregar el producto que falta, cambiar el estilo o bajar el requisito a preferencia. Nada se aplica solo.

## Rotacion libre (2026-11-04)

- Los muebles giran a cualquier angulo entero de 0 a 359 (antihorario visto desde arriba). Antes solo 0/90/180/270.
- En el editor: clic derecho sobre el mueble y mover en circulo (de 5 en 5 grados; Shift = 1 grado). R / Shift+R gira 90.
  En el panel se puede escribir el angulo exacto. El mueble gira sobre su centro.
- La posicion (x,y) sigue siendo la esquina suroeste de la caja envolvente de la huella (con 0/90/180/270 es exactamente
  la de `diseno.pl`).
- Motor: con multiplos de 90 la huella es el mismo rectangulo de `diseno.pl` (equivalencia con Prolog intacta). Con otros
  angulos es un rectangulo girado; los choques se calculan con el teorema del eje separador y "dentro del cuarto" revisa
  las 4 esquinas.
- `diseno.pl` solo modela 90 grados: el verificador Prolog en ejecucion responde "no aplica" cuando hay angulos libres; ahi la
  validacion la hace solo el motor en C# (que siempre es el que decide).
- BD: migracion `FreeRotation` (CHECK rotation entre 0 y 359). Esquema: `rotation` entero 0-359.

## Requisito para la Fase 5: acomodar segun un diseno de referencia (2026-11-04)

Pedido por el usuario: subir una imagen de un diseno de internet (foto de una sala, recamara, etc.) y que la IA acomode el
espacio del usuario siguiendo ese diseno. Propuesta de flujo (se confirma al llegar a la Fase 5):

1. El usuario sube la imagen de referencia (y opcionalmente un texto: "quiero algo asi").
2. n8n -> IA de vision (API de OpenAI del usuario): describe estilo, paleta de colores, materiales, tipo y cantidad de
   muebles y su distribucion relativa (ej. "sofa contra la pared frente a la TV, alfombra al centro").
3. La IA propone un DesignState en JSON usando SOLO productos del catalogo (o conceptuales marcados como tales),
   respetando las medidas reales del cuarto del usuario.
4. El motor valida cada propuesta (agregar/mover/reemplazar). Lo rechazado se corrige o se le explica al usuario.
5. Se crea una version nueva ("Basado en referencia") para no tocar las existentes; el usuario decide si la conserva.
6. Se muestra **como podria quedar la habitacion final** con imagenes de ejemplo: render de Blender (exacto, del DesignState)
   y, opcionalmente, imagenes generadas por IA a partir de ese render (ayuda visual, con su leyenda).
7. La imagen de referencia y cualquier imagen generada son solo ayuda visual (ver la seccion de imagenes).

Objetivo acordado: copiar el diseno de referencia **lo mejor posible**, siempre dentro de las medidas reales, el presupuesto
y los requisitos del usuario.

## Mas modelos 3D al final (2026-11-05)

Cuando la funcionalidad y el diseno de la plataforma esten terminados, se agrega una ronda de **mas modelos 3D** (muebles,
decoracion, iluminacion, variantes por estilo) para que el visor se vea mejor. Se siguen bajando con `npm run assets`
(Poly Haven CC0) o, si hay proveedores reales, con el modelo 3D de cada producto (`asset3D`).

## Catalogo ampliado (2026-11-04)

- `schemas/examples/catalog.json` = catalogo de `diseno.pl` (no se toca: las pruebas contra Prolog dependen de el).
- `schemas/examples/catalog-extra.json` = 30 productos **[EJEMPLO]** aprobados por el usuario, con **precios estimados** en MXN
  (no son de un proveedor real) y `availability: desconocida`. Se usan dentro de las 16 categorias de `diseno.pl`.
- Al arrancar (Development) la API agrega a la BD los productos de ejemplo que falten y llena `asset3D`; no borra ni cambia
  precios ni productos editados a mano.
- Cuando haya proveedores reales, se reemplazan con un adaptador (json/csv) por proveedor.

## IA y n8n (Fase 5, 2026-11-05)

Flujo: **API crea un trabajo -> n8n llama a OpenAI -> n8n regresa el resultado a la API -> el motor valida -> el frontend
ve el avance en vivo (SSE)**. n8n no toca nada en tiempo real (mover, rotar, colores siguen directo en el frontend).

- Trabajos (`ai_jobs`): `referencia` (copiar un diseno de internet), `chat` (instruccion en texto) e `imagen` (como quedaria).
  Estados `pendiente -> en_proceso -> completado | fallido`, con progreso 0-100.
- Flujos de n8n en `n8n/workflows/` (webhooks `iaproyect-propuestas` e `iaproyect-imagen`). Se importan con
  `n8n import:workflow --separate --input=n8n\workflows` y se publican con `n8n publish:workflow --id=...`.
- n8n regresa el resultado a `/api/ai/jobs/{id}/progress|result` con el header `X-Callback-Secret` (`N8N_CALLBACK_SECRET`
  del `.env`). Sin ese secreto la API responde 401.
- La API key de OpenAI **solo** esta en el `.env`; `n8n/start-n8n.ps1` la pasa a n8n como variable de entorno y los flujos la
  leen con `{{ $env.OPENAI_API_KEY }}`. No hay claves en los JSON del repo.
- Modelos (configurables en `.env`): `OPENAI_MODEL=gpt-5.4-mini` (propuestas, con JSON schema estricto) y
  `OPENAI_IMAGE_MODEL=gpt-image-2` (imagenes 1536x1024).
- La IA responde `{summary, style, proposals[]}` con acciones `agregar | mover | reemplazar | quitar` (nunca
  `modificar_estructura`). `AiPlan` las aplica en orden sobre una copia y **cada una pasa por el motor**; lo rechazado no
  se aplica y se le muestra al usuario con el motivo. Si "agregar" no trae lugar o el lugar choca, el motor busca uno libre.
  Propuestas que no cambian nada (reemplazar por el mismo producto, mover al mismo lugar) se marcan `sin_cambio`.
- `referencia`: crea una **version nueva** "Basado en referencia"; la original no se toca. `chat`: solo simula; el usuario
  aplica con "Aplicar lo valido" (se revalida contra el diseno actual y queda en el historial, se puede deshacer).
- `imagen`: se guarda en `storage/renders/` y se sirve en `/files/renders/...`, siempre con la leyenda "Imagen de referencia
  generada por IA. No representa medidas ni productos exactos."
- Reglas para la IA en `ai-rules/` (global, safety-rules, interior-designer, spatial-analysis, furniture-selector,
  budget-manager, image-generator, blender-generator).

## Render realista con IA desde la vista 3D (2026-11-05)

- El usuario pidio que el render sea con IA y lo mas parecido a una foto real. Se decidio **no** migrar el backend a
  Python: el render con IA lo hace OpenAI (via n8n) y C# solo lo coordina y lo guarda.
- "Render de esta vista": el frontend captura lo que se ve en el visor 3D (JPEG) y la API la manda a n8n, que usa
  `/v1/images/edits` con `gpt-image-2` para convertirla en foto **sin mover nada** (mismo punto de vista, mismos muebles,
  mismo lugar). Solo se dibuja lo que entra en el encuadre. Si no hay captura se usa `/v1/images/generations` ("Imagen libre").
- Vista nueva **Foto** en el visor: camara a 1.55 m (altura de los ojos) dentro del cuarto, lente de 62 grados.
- Sigue siendo ayuda visual con su leyenda; el DesignState es la fuente de verdad.
- Pendiente acordado (en este orden): 2) que la IA llene el espacio por estilo (incluye elementos conceptuales);
  3) que la IA sugiera cambios estructurales que el usuario confirma.

## 4 vistas realistas en burbujas (2026-11-05)

- Pedido del usuario: al terminar el diseno, 4 imagenes pequenas arriba a la derecha del visor para ver como quedaria de
  forma hiperrealista. Se generan **solo con el boton** "Vistas realistas" (cada lote = 4 imagenes de OpenAI).
- El visor captura 4 vistas a la altura de los ojos desde las esquinas (suroeste, sureste, noreste, noroeste) sin que la
  camara del usuario se mueva; cada una va a `/ai/render` con el mismo `batch` y su `view`. Se muestra el ultimo lote de
  la version; clic en una burbuja = vista grande (flechas y Esc), siempre con la leyenda de imagen de referencia.
- Siguiente (acordado): 3D en tiempo real mas realista; despues "Disenar con IA" (la IA arma un diseno propio).

## 3D en tiempo real mas realista (2026-11-05)

- Se instalo `@react-three/postprocessing` 3.2.2 (+ `postprocessing` 6.39) con permiso del usuario.
- Efectos: oclusion ambiental **SSAO** (sombras suaves en esquinas y bajo los muebles), brillo leve (ventana, lamparas),
  tono ACES y vineta suave. N8AO **no** se usa: la version 2.0.1 (la ultima) falla con three 0.187.
- Luz: el "sol" entra desde afuera de la primera ventana hacia el centro del cuarto; cada lampara del diseno tiene una
  luz calida. Techo que solo se ve desde dentro (vista Foto); en 3D y Planta no estorba.
- Las capturas para las burbujas y el render pasan por los mismos efectos, para que se vean igual que el visor.

## Render fiel al modelo 3D (2026-11-05)

- Problema reportado: las imagenes de IA no seguian el diseno 3D (inventaban decoracion, p. ej. una planta).
- Causa: al prompt se le mandaban TODOS los muebles del diseno, aunque no salieran en la captura.
- Arreglo: el visor calcula por captura que muebles salen en cuadro (caja de cada mueble proyectada a la camara) y en que
  parte (izquierda/centro/derecha, cerca/media/fondo, cortado por el borde). La API manda a la IA SOLO esa lista, con
  "se ven EXACTAMENTE N muebles", y prohibe agregar decoracion o mover/cambiar muebles. La nota del usuario solo cambia
  ambiente (luz/hora). `gpt-image-2` no acepta `input_fidelity` (ya trabaja con fidelidad alta).
- Limite conocido: no detecta si un mueble queda tapado por otro (se cuenta como visible).

## Calidad de imagen y aspecto realista (2026-11-05)

- Problema reportado: las imagenes se veian "plasticas". Causas: (1) OpenAI regresaba calidad baja por defecto
  (~158 tokens de imagen) y (2) el prompt pedia copiar la captura tal cual, asi que copiaba las cajas de los muebles sin
  modelo 3D, el techo oscuro y las sombras negras.
- Arreglo: `quality` configurable en `.env` (`OPENAI_IMAGE_QUALITY`, por defecto `high`: ~5,500 tokens de imagen,
  ~1 minuto por imagen, mas caro). El prompt separa **acomodo** (estricto: posicion, tamano, orientacion, color, cantidad)
  de **aspecto** (cada caja se dibuja como el producto real descrito; techo blanco; luz natural; materiales con detalle).
- Techo del visor con un poco de emision para que la captura no salga oscura.

## Fase 6: Blender (2026-11-05)

- Alcance acordado: **solo interiores** (fachadas quedan fuera).
- `blender/build_scene.py` (corre dentro de Blender 4.3): DesignState validado + catalogo -> cuarto con huecos reales
  de puertas y ventanas, piso/muros/techo con las texturas PBR del visor, muebles, luces, camara -> `.blend` + render
  Cycles. Cada mueble es un Empty con el **mismo instanceId** del DesignState (y sus propiedades `instanceId`, `catalogId`).
- Muebles: con `asset3D` se importa el mismo GLB del visor y se ajusta EXACTO a ancho x fondo x alto (igual que
  ModelAsset.tsx); sin GLB se arman pieza por pieza igual que `Procedural.tsx`. TV sobre su mueble y cuadro colgado igual
  que en el visor (solo visual). Colores: se leen de la tabla COLORS de `frontend/src/lib/units.ts` (una sola fuente).
- Luz: cielo Nishita + sol que entra por la primera ventana, portales de luz en las ventanas, relleno suave del techo y
  luz calida en cada lampara. Vidrio transparente para las sombras (deja pasar el sol). Tono AgX.
- Materiales con proyeccion de caja en coordenadas de objeto (no necesitan UV). Relieve con Bump (no normal map:
  sin tangentes de UV deja manchas negras).
- `blender/render_design.ps1 -DesignId d1 [-Views all] [-Samples 64]` baja el diseno y el catalogo de la API y deja todo
  en `storage/blender/<id>/` (fuera del repo).
- Equipo del usuario: Intel Iris Xe (sin NVIDIA) -> Cycles en CPU. Referencia medida: 800x500 a 32 muestras ~ 40 s.

## IA en Python, Fase 7 y cierre (2026-11-11)

- **Decision del usuario:** todo el procesamiento de IA va en Python. Se eligio "servicio Python + n8n": `ai-service/`
  (FastAPI, 127.0.0.1:8100) arma los prompts con `/ai-rules`, llama a OpenAI (propuestas e imagenes), corre Blender y genera
  las exportaciones. n8n solo orquesta (ya no necesita la key de OpenAI). C# conserva datos, motor, versiones y listas, y ya
  no arma prompts (`AiContext` solo junta los datos que se mandan).
- El `.env` del proyecto tiene prioridad sobre las variables de Windows (habia una `OPENAI_API_KEY` vieja en el usuario y
  OpenAI respondia 401). En un 401 no se reenvia el mensaje de OpenAI porque repite parte de la key.
- **Disenar con IA** (`POST /api/designs/{id}/ai/design`): el motor quita todo lo no bloqueado (lo obligatorio, como la TV
  de 75", se queda), la IA propone solo "agregar" del estilo elegido y cada mueble pasa por el motor; se crea una version
  nueva "Diseno IA (estilo)". El estilo final es el que pidio el usuario, no el que diga la IA.
- **Blender desde la pagina** (`POST /api/designs/{id}/blender`): trabajo asincrono tipo `blender`; calidad rapida
  (800x500, 32 muestras, ~1 min por vista) o alta (1280x800, 64 muestras, ~3-4 min). Avance por vista. Las rutas que
  regresa el servicio se validan (sin `..`) y se sirven en `/files/...` (incluye `.blend`).
- **Fase 7:** desperdicio 10% confirmado por el usuario (`Rules.MaterialWaste`). `lista_compras` y `lista_materiales`
  estan en el motor (`Lists.cs`) y se comparan contra Prolog. El subtotal de materiales usa los m2 a comprar (con
  desperdicio); el presupuesto sigue usando los m2 del diseno, igual que `presupuesto_rubro/4`. Formatos elegidos: PDF del
  reporte, Excel (y CSV), plano 2D en SVG y PDF, modelo 3D en GLB (desde el visor) y `.blend` (del ultimo render).
- **Arreglos encontrados al cerrar:**
  - `next build` fallaba en `/nuevo` (three.js genera ids aleatorios al prerenderizar): la pagina se carga solo en cliente.
  - El editor se quedaba en "Cargando…" en algunos navegadores esperando `use(params)`; ahora lee el id con `useParams()`
    (abre en ~0.6 s).
  - Publicar flujos con n8n corriendo no siempre se aplica: `n8n/publish-workflows.ps1` los publica uno por uno con n8n
    detenido.
- Scripts: `scripts/start-all.ps1` (todo en ventanas separadas) y `scripts/test-all.ps1` (todas las pruebas).

## Rediseno de la interfaz (2026-11-11)

- Concepto **"plano y material"**: el lenguaje de un plano arquitectonico (plantas, cotas, cuadro de rotulacion) con
  materiales mexicanos. Colores en `frontend/src/app/globals.css`: cal `#EEF0EC` (fondo), grafito `#1E2328` (texto),
  azul talavera `#1F4FA3` (acciones y seleccion), jade / ambar / grana para valido / advertencia / conflicto.
- Tipografia: una sola familia, **Archivo**; los titulos usan su ancho expandido (como la rotulacion de un plano).
- Elemento distintivo: cada diseno se dibuja como su **plano real** (`PlanDrawing.tsx`, con los datos del DesignState):
  en el inicio cada version es una hoja con su plano y el hero muestra la sala de ejemplo con cotas, trazada una sola vez
  al abrir (unico movimiento automatico; se respeta "reducir movimiento"). En el editor, el visor tiene un **cuadro de
  rotulacion** (espacio, medidas, vista, revision y significado de colores).
- Panel lateral con pestanas de icono y nombre fijas arriba; hojas de versiones que se desplazan en vez de aplastarse;
  mensajes con borde de color segun su tipo; sin emojis en titulos. Error 404 de un diseno con mensaje claro y boton a
  proyectos.
