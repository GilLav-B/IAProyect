# Plataforma Inteligente de Diseno de Interiores Asistida por IA

Proyecto del **Equipo 1** - Instituto Tecnologico de Tijuana (Tijuana, Baja California, Mexico).

Este repositorio contiene la **base de conocimiento en Prolog** (`diseno.pl`) que modela las relaciones, reglas y validaciones del sistema que usara la IA.

## Descripcion

Plataforma web para disenar, planificar, visualizar y configurar espacios interiores usando IA, modelado 3D, catalogos de productos y automatizacion con n8n.

La IA no entrega solo una imagen: el diseno es un **modelo estructurado, editable y persistente** (`DesignState`) conectado con productos, presupuesto y versiones.

Flujo general:

```
Espacio real -> analisis con IA -> requisitos -> propuestas -> modelo 3D
-> productos -> presupuesto -> modificaciones -> versiones -> exportacion
```

## Alcance

**Espacios:** sala, recamara, cocina, oficina, comedor.

**Estilos:** moderno, minimalista, industrial, Japandi, clasico.

**Entradas del usuario:** fotografias, dimensiones, planos, presupuesto, estilo, colores, muebles existentes, restricciones.

**Salidas:** propuesta de diseno, imagenes/render, distribucion, muebles recomendados, materiales, presupuesto, lista de compras, plano 2D, archivo Blender, reporte PDF.

**Tipos de proyecto propuestos:**
- Comercial (oficina)
- Domestica (casera)

## Arquitectura conceptual

| Componente | Funcion |
|---|---|
| Frontend | Editor, UI y visor 3D (React/Next.js, Three.js, WebGL) |
| Backend | API, motor de diseno, validacion y versiones (Node.js o .NET) |
| n8n | Orquestacion de flujos con IA e integraciones |
| IA | Analisis de fotos/planos y propuestas de diseno |
| Catalogos | Productos de proveedores normalizados con adaptadores |
| Blender | Generacion y exportacion 3D (GLB/GLTF, .blend) |
| PostgreSQL + Object Storage | Datos, fotos, planos, modelos y renders |

Principio clave: **IA propone -> el motor valida -> el sistema acepta o rechaza -> el frontend aplica.** La IA no es la fuente de verdad.

## Rol de Prolog en el proyecto

`diseno.pl` representa en Prolog:

- Espacios, estilos, entradas y salidas.
- Elementos estructurales (protegidos) vs modificables.
- Prioridades de requisitos: obligatorio, importante, preferencia, opcional.
- Catalogo de productos, proveedores y materiales.
- Habitaciones con puertas y ventanas, y objetos colocados con posicion y rotacion.
- Disenos como versiones (padre/hijo), bloqueo de elementos y comparacion.
- Presupuesto dinamico por rubro con rangos minimo-maximo.
- **Conflictos** (impiden aceptar un diseno) y **advertencias**.
- Validacion de propuestas de la IA (`veredicto/3`).
- Busqueda de productos similares y reemplazo.
- Lista de compras y de materiales (con factor de desperdicio).
- Explicacion de decisiones e interpretacion de instrucciones en texto.
- Reglas de las IA, principios del proyecto, flujos de n8n y tareas en tiempo real vs asincronas.

## Estructura del repositorio

```
.
|-- README.md
`-- diseno.pl     # base de conocimiento completa
```

## Requisitos

- [SWI-Prolog](https://www.swi-prolog.org/) (probado en 9.0.4).

## Como ejecutar

```prolog
?- consult('diseno.pl').
?- pruebas.
```

Despues de editar el archivo, recarga con:

```prolog
?- make.
```

## Ejemplos de consultas

```prolog
?- diseno_valido(d1).
?- conflictos(d2, L).
?- advertencias(d1, L).
?- presupuesto_total(d1, Min, Max).
?- veredicto(d1, reemplazar(sofa_d1, sofa_001), V).
?- buscar_similares(sofa_001, L).
?- sustitucion_ahorro(d2, sofa_d2, Nuevo, Ahorro).
?- comparar_versiones(d1, d2, Difs).
?- lista_compras(d1, Items).
?- interpretar([cambia, el, sofa, por, uno, mas, pequeno], Acciones).
```

## Predicados principales

| Predicado | Que hace |
|---|---|
| `producto/8` | Catalogo: id, nombre, categoria, dimensiones, materiales, colores, estilos, precio |
| `habitacion/5`, `puerta/5`, `ventana/6` | Geometria del espacio |
| `diseno/3`, `objeto/3`, `coloca/5`, `config/4` | Version de diseno, objetos y su posicion/configuracion |
| `requisito/3` | Requisitos del usuario por habitacion y prioridad |
| `presupuesto_total/3`, `presupuesto_rubro/4` | Presupuesto dinamico (rangos) |
| `conflicto/2`, `diseno_valido/1` | Conflictos que invalidan un diseno |
| `advertencia/2` | Avisos que no bloquean |
| `validar_propuesta/3`, `veredicto/3` | Validacion de acciones propuestas por la IA |
| `similar/2`, `buscar_similares/2`, `alternativas/3` | Productos similares y reemplazos |
| `comparar_versiones/3` | Diferencias entre disenos |
| `lista_compras/2`, `lista_materiales/2` | Salidas de compra |
| `interpretar/2` | Palabras clave a intenciones |

## Convenciones

- Medidas en **cm**, precios en **pesos MXN**.
- Coordenadas: esquina (0,0) = suroeste; `x` a lo largo, `y` a lo ancho. Paredes: sur, norte, oeste, este.
- Archivo sin acentos para evitar problemas de codificacion.
- Marcadores en comentarios: `[DOC]` viene del documento del proyecto, `[EJEMPLO]` dato inventado para pruebas, `[SUPUESTO]` decision a confirmar.

## Estado actual

- Base de conocimiento cargada y probada con `?- pruebas.` en SWI-Prolog 9.0.4.
- Solo `sofa_001`, la habitacion de 5.20 x 3.80 x 2.70 m y el presupuesto de $30,000 provienen del documento. El resto del catalogo, precios y proveedores son datos de ejemplo.

## Avance del prototipo (capturas)

Asi se ve hoy la plataforma web (backend .NET + frontend Next.js con React Three Fiber). Cada cambio que se hace en el
editor 3D pasa por el motor de validacion, que da los mismos resultados que `diseno.pl`.

**Editor 3D** - sala de `d3`: muebles con medidas reales, sombras y luz natural; a la derecha, la lista de objetos y la
validacion del motor (advertencias de presupuesto y requisitos).

![Editor 3D en perspectiva](docs/img/editor-3d.png)

**Vista de planta** - mismo diseno visto desde arriba, con el sofa seleccionado y su panel de configuracion (modelo, color,
material, precio, medidas, posicion y rotacion). Se ven las zonas de la puerta (naranja) y del acceso a la ventana (azul).

![Editor en vista de planta](docs/img/editor-planta.webp)

**Inicio** - proyectos y sus versiones de diseno, con estilo, modo de presupuesto, rango de precio y si son validas.

![Pagina de inicio con proyectos y versiones](docs/img/inicio-proyectos.png)

> En la captura de planta aparece el aviso "No se pudo conectar con la API": en ese momento el backend se estaba
> reiniciando. Con la API corriendo (`dotnet run --project backend/src/InteriorDesign.Api`) el aviso no aparece.

## Pendientes

- Definir "Geometria y caracteristicas del inmueble" del proyecto comercial (hay una propuesta en `caracteristica_inmueble/2`).
- Reemplazar los datos `[EJEMPLO]` por catalogo y proveedores reales.
- Confirmar con el equipo los parametros `[SUPUESTO]` (desperdicio, ocupacion maxima, acceso a ventanas, margen flexible).

## Principios del proyecto

1. La IA ayuda, pero el usuario decide.
2. El diseno debe ser editable.
3. El modelo estructurado es la fuente de verdad.
4. Las imagenes generadas son representaciones, no la fuente geometrica.
5. Las dimensiones reales deben respetarse.
6. Las restricciones obligatorias deben mantenerse.
7. El presupuesto debe ser visible y dinamico.
8. Cada objeto debe poder identificarse.
9. Los productos reales se diferencian de los elementos conceptuales.
10. Las versiones deben ser independientes.
11. Los cambios deben poder revertirse.
12. La IA debe validar sus propuestas.
13. Los elementos estructurales deben estar protegidos.
14. El sistema debe ser extensible a nuevos proveedores.
15. n8n es orquestador, no sustituto del backend ni del motor 3D.
