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

## Catalogo ampliado (2026-11-04)

- `schemas/examples/catalog.json` = catalogo de `diseno.pl` (no se toca: las pruebas contra Prolog dependen de el).
- `schemas/examples/catalog-extra.json` = 30 productos **[EJEMPLO]** aprobados por el usuario, con **precios estimados** en MXN
  (no son de un proveedor real) y `availability: desconocida`. Se usan dentro de las 16 categorias de `diseno.pl`.
- Al arrancar (Development) la API agrega a la BD los productos de ejemplo que falten y llena `asset3D`; no borra ni cambia
  precios ni productos editados a mano.
- Cuando haya proveedores reales, se reemplazan con un adaptador (json/csv) por proveedor.
