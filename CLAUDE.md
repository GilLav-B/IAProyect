# CLAUDE.md - Plataforma de Diseno de Interiores Asistida por IA

Este archivo te dice (a Claude Code) como trabajar en este proyecto. Leelo completo antes de hacer cualquier cosa y sigue las fases EN ORDEN.

---

## 1. REGLAS DE TRABAJO (OBLIGATORIAS)

1. **Pregunta antes de suponer.** Si te falta un dato, una decision, una credencial, un archivo o algo es ambiguo, **DETENTE y pregunta al usuario**. Nunca inventes datos, precios, claves, URLs ni resultados.
2. **Una fase a la vez.** No avances a la siguiente fase sin que el usuario confirme que la anterior esta aprobada.
3. **Un pendiente a la vez.** Dentro de una fase, trabaja una tarea, muestrala, y luego sigue.
4. **Di siempre que archivo creas o modificas** y donde va el codigo.
5. **Di claramente cuando algo NO fue probado.** Si no puedes correrlo, escribelo asi: "No probado: <motivo>".
6. **Entrega codigo completo**, no fragmentos con "..." ni "el resto igual".
7. **Prueba lo que escribas.** Corre los tests/comandos tu mismo y muestra el resultado real. Si falla, corrige antes de reportar.
8. **No hagas cambios fuera de la fase actual** ni refactors que no se pidieron.
9. **Nunca subas secretos.** API keys y contrasenas van en `.env` (y `.env` en `.gitignore`). Pide las claves al usuario, no las inventes.
10. **Antes de instalar paquetes o ejecutar comandos destructivos** (borrar, resetear BD, `git push --force`), pide confirmacion.
11. **Comunicacion:** espanol informal mexicano, respuestas cortas, concretas y paso a paso.
12. **Commits pequenos y descriptivos** al terminar cada tarea aprobada. No hagas `push` sin que el usuario lo pida.

### Cuando DEBES parar y preguntar
- Falta una decision de tecnologia o arquitectura.
- Necesitas una API key, cuenta, servicio instalado (n8n, Blender, PostgreSQL) o archivo que no esta en la carpeta.
- Un test falla y no entiendes por que despues de 2 intentos.
- Los datos del documento y los de `diseno.pl` se contradicen.
- La tarea implica modificar elementos fuera de la fase actual.
- Algo requiere probar visualmente en el navegador (pidele al usuario que lo revise y te diga que ve).

---

## 2. CONTEXTO DEL PROYECTO

Plataforma web que permite disenar, visualizar, modificar y presupuestar un espacio interior real con IA y un entorno 3D interactivo. El usuario da fotos, planos, medidas, presupuesto y preferencias; la IA propone disenos; el usuario edita cada objeto en 3D, compara versiones y exporta.

**La IA NO es la fuente de verdad.** Flujo obligatorio:

```
IA propone -> Design Engine valida -> sistema acepta/rechaza -> frontend aplica
```

### Archivos de referencia (leelos primero)
- `README.md` - resumen del proyecto.
- `diseno.pl` - base de conocimiento en Prolog ya probada. **Es la especificacion de las reglas de negocio.**
- PDF del proyecto "Plataforma Inteligente de Diseno de Interiores Asistida por IA" - si no esta en la carpeta, **pideselo al usuario**.

### Alcance
- Espacios: sala, recamara, cocina, oficina, comedor.
- Estilos: moderno, minimalista, industrial, japandi, clasico.
- Entradas: fotografias, dimensiones, planos, presupuesto, estilo, colores, muebles existentes, restricciones.
- Salidas: propuesta de diseno, render, distribucion, muebles recomendados, materiales, presupuesto, lista de compras, plano 2D, archivo Blender, reporte PDF.
- Proyectos: comercial (oficina) y domestico.

### Tecnologias (segun el documento)
- Frontend: React / Next.js, Three.js, WebGL.
- Backend: Node.js o .NET, REST API, WebSocket o SSE.
- Base de datos: PostgreSQL.
- Automatizacion: n8n (orquestador, NO el backend).
- 3D: Three.js, Blender + Python, GLB/GLTF.
- Almacenamiento: object storage.

> Los datos de `diseno.pl` marcados `[EJEMPLO]` son inventados solo para pruebas. Los `[SUPUESTO]` son decisiones por confirmar. No los trates como datos reales.

---

## 3. PRINCIPIOS DE DOMINIO (NO SE ROMPEN)

1. La IA ayuda, el usuario decide.
2. El diseno debe ser editable.
3. El modelo estructurado (`DesignState`) es la fuente de verdad; las imagenes son solo representacion.
4. Las dimensiones reales se respetan.
5. Las restricciones obligatorias se mantienen.
6. El presupuesto es visible y dinamico (rangos min-max).
7. Cada objeto tiene un `instanceId` unico y el mismo id se usa en catalogo, DesignState, Three.js y Blender.
8. Productos reales != elementos conceptuales (la IA puede crear conceptuales).
9. Las versiones son independientes (duplicar no afecta la original).
10. Los cambios deben poder revertirse.
11. Elementos estructurales (muros, columnas, puertas, ventanas) protegidos: solo se modifican con accion explicita del usuario.
12. Extensible a nuevos proveedores mediante adaptadores.
13. **n8n no controla interacciones en tiempo real** (mover, rotar, cambiar color/material van directo en frontend/3D). n8n es solo para IA, integraciones y procesos pesados/asincronos.
14. La IA solo genera JSON valido conforme al esquema; el codigo hace la validacion definitiva.

### Reglas de validacion que el motor debe implementar (vienen de `diseno.pl`)
- Conflictos (bloquean): no cabe, fuera de habitacion, demasiado alto, colision, bloquea puerta, bloquea ventana, exceso de muebles, categoria no permitida, color no disponible, presupuesto excedido, falta requisito obligatorio.
- Advertencias (no bloquean): presupuesto puede excederse, estilo incompatible, requisito importante/preferencia no cumplido, falta mueble basico, elemento conceptual.
- Propuestas: `agregar`, `mover`, `reemplazar`, `quitar`, `modificar_estructura`.
- Prioridades: obligatorio, importante, preferencia, opcional.
- Unidades: cm y pesos MXN. Origen (0,0) = esquina suroeste.

---

## 4. FASES

Al terminar cada fase entrega el **Reporte de fase** (seccion 5) y espera aprobacion.

### FASE 0 - Preparacion y decisiones
**Objetivo:** entender el proyecto y fijar el stack.
**Tareas:**
1. Lee `README.md`, `diseno.pl` y el PDF. Resume en 10 lineas lo que entendiste.
2. Revisa el entorno: sistema operativo, versiones de Node, .NET, Python, Git, PostgreSQL, Blender, n8n, SWI-Prolog.
3. **PREGUNTA al usuario (una pregunta a la vez):**
   - Backend: Node.js o .NET.
   - Frontend: React/Next.js (segun documento) u otra opcion.
   - Como quiere manejar las reglas: portarlas a codigo del backend, o llamar a `diseno.pl` desde el backend.
   - Si ya tiene PostgreSQL, n8n y Blender instalados.
   - Nombre del repositorio y si ya existe en git.
4. Con las respuestas, propon la estructura de carpetas y esperala aprobacion antes de crearla.
**Terminada cuando:** el usuario aprobo el stack y la estructura. Se crea la estructura vacia, `.gitignore`, `.env.example`.

### FASE 1 - Modelo de datos (DesignState)
**Objetivo:** definir el esquema central.
**Tareas:**
1. Esquema JSON de `DesignState`: proyecto, espacio, geometria, requisitos con prioridad, objetos (`instanceId`, `catalogId`, tipo, posicion, rotacion, escala), configuracion, materiales, iluminacion, presupuesto, version, historial.
2. Esquema del catalogo de productos (ver `producto/8` en `diseno.pl`) incluyendo `tipo: real | conceptual`.
3. Esquema de habitacion (largo, ancho, alto, puertas, ventanas).
4. Esquema PostgreSQL con migraciones.
**Pregunta al usuario si:** falta algun campo que el equipo necesite (por ejemplo, geometria del inmueble comercial, que quedo pendiente).
**Terminada cuando:** los esquemas validan los datos de ejemplo `d1`-`d4` de `diseno.pl` convertidos a JSON.

### FASE 2 - Backend y motor de validacion
**Objetivo:** API + Design Engine que reproduce las reglas de `diseno.pl`.
**Tareas:**
1. CRUD de proyectos, habitaciones, disenos, objetos y catalogo.
2. Motor de validacion: colisiones, puertas, ventanas, limites, presupuesto, requisitos, bloqueos.
3. Endpoint de validacion de propuestas (equivalente a `veredicto/3`).
4. Versiones: duplicar, ramificar, comparar, historial, revertir.
5. Presupuesto por rubro con rangos.
6. Busqueda de similares y reemplazo.
**Tests obligatorios** (deben dar el mismo resultado que Prolog):
- `d1` valido, con advertencia de presupuesto.
- `d2` con `presupuesto_excedido`.
- `d3` con `bloquea_puerta`.
- `d4` valido.
- Mover un objeto bloqueado -> rechazado.
- Reemplazar TV 75" por 65" con requisito obligatorio -> rechazado.
- `modificar_estructura` sin confirmacion -> rechazado.
**Pregunta al usuario si:** un resultado difiere de Prolog y no sabes cual es el correcto.
**Terminada cuando:** todos los tests pasan y se mostro la salida real.

### FASE 3 - Frontend base y visor 3D
**Objetivo:** ver y editar un diseno en 3D.
**Tareas:**
1. Escena Three.js con la habitacion (piso, muros, puertas, ventanas).
2. Objetos como cajas con las dimensiones del producto (los modelos GLB reales vienen despues).
3. Seleccionar un objeto, moverlo y rotarlo; la validacion se llama al soltar.
4. Panel de configuracion (modelo, color, material, precio) tipo configurador de vehiculos.
5. Bloquear/desbloquear elementos.
**Pide al usuario:** que abra el navegador y confirme que se ve y se mueve bien. No digas "funciona" sin esa confirmacion.
**Terminada cuando:** el usuario aprobo visualmente.

### FASE 4 - Presupuesto y versiones en la interfaz
**Tareas:**
1. Presupuesto dinamico visible, por rubro, que se actualiza al cambiar objetos.
2. Pestanas de versiones tipo hojas de Excel (Diseno 1, 2, 3...), duplicar y comparar.
3. Historial y revertir cambios.
4. Modos de presupuesto: minimizar, ajustar, flexible.
5. Aviso de conflictos con la opcion de que el usuario decida que requisito gana.
**Terminada cuando:** el usuario aprobo el flujo completo en navegador.

### FASE 5 - Integracion con IA y n8n
**Objetivo:** analisis de fotos/planos y generacion de disenos.
**Tareas:**
1. Workflows de n8n exportados como JSON en el repo: analisis de fotos, generacion de diseno, generacion de imagenes, Blender.
2. Trabajos asincronos con estados (pendiente, en_proceso, completado, fallido) y progreso hacia el frontend.
3. La IA devuelve JSON que **siempre** pasa por el motor de validacion antes de aplicarse.
4. Archivos de reglas para las IA en `/ai-rules` (global, interior-designer, spatial-analysis, furniture-selector, budget-manager, image-generator, blender-generator, safety-rules).
5. Chat del editor que traduce instrucciones a cambios sobre el DesignState.
**DETENTE y pide al usuario:** API keys (IA de vision, generacion de imagenes), URL y credenciales de n8n. Nunca inventes claves ni uses claves de ejemplo como reales.
**Terminada cuando:** una foto de prueba produce un espacio estructurado y una propuesta validada, mostrado con resultados reales.

### FASE 6 - Blender
**Tareas:**
1. Script Python: `DesignState (JSON) -> geometria -> modelos -> materiales -> iluminacion -> camara -> .blend / render`.
2. La IA **no** construye el `.blend`: solo JSON -> Python -> Blender.
**Pide al usuario:** confirmar que Blender esta instalado y la ruta del ejecutable.
**Terminada cuando:** se genera un `.blend` y un render de `d1` y el usuario lo revisa.

### FASE 7 - Exportacion y listas
**Tareas:** lista de compras, lista de materiales (con factor de desperdicio), presupuesto, plano 2D, reporte PDF, exportar modelo 3D.
**Pregunta al usuario:** el factor de desperdicio y los formatos finales (en `diseno.pl` el 10% es solo `[SUPUESTO]`).

### FASE 8 - Pruebas finales y documentacion
**Tareas:** pruebas de punta a punta, README actualizado, instrucciones de instalacion, lista de pendientes conocidos.

---

## 5. FORMATO DEL REPORTE DE FASE

Al terminar cada fase responde exactamente con:

```
FASE X COMPLETADA
Archivos creados/modificados: <lista con rutas>
Como probarlo: <comandos exactos>
Resultado real de las pruebas: <salida pegada>
NO probado: <lo que no pudiste verificar y por que>
Supuestos que tome: <lista, o "ninguno">
Necesito de ti: <preguntas o confirmaciones>
```

Despues **espera** la aprobacion del usuario. No empieces la siguiente fase por tu cuenta.

---

## 6. PROHIBIDO

- Inventar productos, precios, claves, resultados de pruebas o datos del equipo.
- Decir "listo" o "funciona" sin haberlo corrido.
- Saltarte fases o mezclar fases.
- Dejar la validacion solo a la IA o solo a los archivos `.md`: el codigo valida siempre.
- Usar n8n para acciones en tiempo real.
- Modificar elementos estructurales sin accion explicita del usuario.
- Subir `.env`, claves o datos personales al repositorio.
- Hacer `git push` o borrar datos sin que el usuario lo confirme.
