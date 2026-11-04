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
- Por ahora los muebles son cajas con medidas reales; despues se usaran modelos 3D gratuitos (CC0) escalados a las medidas del catalogo.
