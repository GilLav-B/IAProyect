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
