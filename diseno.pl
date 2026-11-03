% =====================================================
% PLATAFORMA DE DISENO DE INTERIORES ASISTIDA POR IA
% Base de conocimiento en Prolog  -  archivo: diseno.pl
%
% Unidades: medidas en cm, precios en pesos MXN.
% Sin acentos a proposito (evita problemas de codificacion
% al guardar desde el Bloc de notas).
%
% [DOC]     = viene del documento del proyecto
% [EJEMPLO] = dato inventado para poder probar el sistema,
%             se reemplaza despues por datos reales
% [SUPUESTO]= decision mia, confirmar con el equipo
% =====================================================

:- discontiguous producto/8, validar_propuesta/3.
:- dynamic bloqueado/2, confirmacion_usuario/2, favorito/1.

% =====================================================
% 1. PROYECTOS Y ESPACIOS
% =====================================================
tipo_proyecto(comercial).
tipo_proyecto(domestico).

% [SUPUESTO] oficina = comercial, el resto = domestico
espacio_de(comercial, oficina).
espacio_de(domestico, sala).
espacio_de(domestico, recamara).
espacio_de(domestico, cocina).
espacio_de(domestico, comedor).

espacio(E) :- espacio_de(_, E).

% Geometria y caracteristicas del inmueble
% [SUPUESTO] la parte comercial quedo vacia en las notas; esto es una propuesta
caracteristica_inmueble(domestico,
    [largo, ancho, altura, muros, piso, techo, puertas, ventanas]).
caracteristica_inmueble(comercial,
    [largo, ancho, altura, muros, piso, techo, puertas, ventanas,
     columnas, numero_personas, areas_de_trabajo]).

% =====================================================
% 2. ESTILOS
% =====================================================
estilo(moderno).
estilo(minimalista).
estilo(industrial).
estilo(japandi).
estilo(clasico).

paleta(moderno,     [gris, blanco, negro]).
paleta(minimalista, [blanco, beige, gris]).
paleta(industrial,  [negro, gris, cafe]).
paleta(japandi,     [beige, blanco, madera_clara, gris]).
paleta(clasico,     [cafe, beige, dorado]).

material_estilo(moderno,     [vidrio, metal, madera]).
material_estilo(minimalista, [madera, concreto, tela]).
material_estilo(industrial,  [metal, concreto, madera]).
material_estilo(japandi,     [madera, tela, ceramica]).
material_estilo(clasico,     [madera, marmol, tela]).

% estilos que combinan entre si
estilo_afin(japandi, minimalista).
estilo_afin(moderno, minimalista).
estilo_afin(moderno, industrial).

estilos_compatibles(A, A).
estilos_compatibles(A, B) :- estilo_afin(A, B).
estilos_compatibles(A, B) :- estilo_afin(B, A).

% =====================================================
% 3. ENTRADAS Y SALIDAS  [DOC]
% =====================================================
entrada(fotografias).
entrada(dimensiones).
entrada(planos).
entrada(presupuesto).
entrada(estilo).
entrada(colores).
entrada(muebles_existentes).
entrada(restricciones).
entrada(comentarios_texto).

salida(propuesta_diseno).
salida(render).
salida(distribucion).
salida(muebles_recomendados).
salida(materiales).
salida(presupuesto).
salida(lista_compras).
salida(lista_materiales).
salida(plano_2d).
salida(modelo_3d).
salida(archivo_blender).
salida(reporte_pdf).

formato_plano(jpg, fase1).
formato_plano(png, fase1).
formato_plano(pdf, fase1).
formato_plano(dwg, fase2).
formato_plano(dxf, fase2).

% Las fotos solo dan medidas aproximadas, las del usuario son la referencia
precision_fuente(fotografias, aproximada).
precision_fuente(medidas_usuario, referencia).
precision_fuente(planos, referencia).

% =====================================================
% 4. ELEMENTOS ESTRUCTURALES VS MODIFICABLES  [DOC]
% =====================================================
estructural(muro).
estructural(columna).
estructural(puerta).
estructural(ventana).

modificable(mueble).
modificable(decoracion).
modificable(iluminacion).
modificable(material).

tipo_elemento(E, estructural) :- estructural(E).
tipo_elemento(E, modificable) :- modificable(E).

% =====================================================
% 5. PRIORIDADES DE REQUISITOS  [DOC]
% =====================================================
prioridad(obligatorio, 1).
prioridad(importante,  2).
prioridad(preferencia, 3).
prioridad(opcional,    4).

sacrificable(Nivel) :- prioridad(Nivel, N), N >= 3.

% =====================================================
% 6. PARAMETROS AJUSTABLES
% =====================================================
parametro(acceso_ventana, 40).        % [SUPUESTO] cm libres frente a ventana
parametro(ocupacion_maxima, 0.6).     % [SUPUESTO] max. fraccion del piso con muebles
parametro(desperdicio_material, 0.10). % [SUPUESTO] 10% de desperdicio
parametro(margen_flexible, 0.10).     % [SUPUESTO] 10% extra en modo flexible
parametro(circulacion_minima, 90).    % [DOC] ~90 cm de circulacion (informativo)

% =====================================================
% 7. CATEGORIAS, RUBROS Y ESPACIOS PERMITIDOS
% =====================================================
rubro(muebles).
rubro(electronica).
rubro(iluminacion).
rubro(decoracion).
rubro(materiales).
rubro(instalacion).

% categoria(Categoria, RubroDePresupuesto)
categoria(sofa,          muebles).
categoria(sillon,        muebles).
categoria(mesa_centro,   muebles).
categoria(mueble_tv,     muebles).
categoria(cama,          muebles).
categoria(escritorio,    muebles).
categoria(silla_oficina, muebles).
categoria(librero,       muebles).
categoria(mesa_comedor,  muebles).
categoria(silla_comedor, muebles).
categoria(isla,          muebles).
categoria(televisor,     electronica).
categoria(lampara,       iluminacion).
categoria(alfombra,      decoracion).
categoria(cuadro,        decoracion).
categoria(planta,        decoracion).

% objetos que no ocupan piso por si solos (van encima, abajo o en pared)
sin_area_piso(alfombra).
sin_area_piso(televisor).
sin_area_piso(cuadro).

categorias_espacio(sala,
    [sofa, sillon, mesa_centro, mueble_tv, televisor, alfombra,
     lampara, cuadro, planta, librero]).
categorias_espacio(recamara,
    [cama, mueble_tv, televisor, alfombra, lampara, cuadro, planta]).
categorias_espacio(cocina,
    [isla, lampara, cuadro, planta]).
categorias_espacio(oficina,
    [escritorio, silla_oficina, librero, lampara, planta, cuadro, alfombra]).
categorias_espacio(comedor,
    [mesa_comedor, silla_comedor, lampara, cuadro, planta, alfombra]).

categoria_en(Espacio, Cat) :-
    categorias_espacio(Espacio, L),
    member(Cat, L).

% muebles basicos que deberia tener cada espacio
basico(sala,     [sofa, mesa_centro, mueble_tv, televisor]).
basico(recamara, [cama]).
basico(cocina,   [isla]).
basico(oficina,  [escritorio, silla_oficina]).
basico(comedor,  [mesa_comedor, silla_comedor]).

% =====================================================
% 8. CATALOGO DE PRODUCTOS
% producto(Id, Nombre, Categoria, dim(Ancho,Fondo,Alto),
%          Materiales, Colores, Estilos, precio(Min,Max))
% Solo sofa_001 viene del documento; el resto es [EJEMPLO]
% =====================================================
producto(sofa_001, 'Sofa Oslo', sofa, dim(280,95,85),
    [tela], [beige,gris,negro], [moderno,minimalista],
    precio(12999,15499)).                                   % [DOC]
producto(sofa_002, 'Sofa Bergen', sofa, dim(220,90,80),
    [tela], [gris,negro], [moderno,industrial],
    precio(8999,10999)).
producto(sofa_003, 'Sofa Kyoto', sofa, dim(160,85,80),
    [tela], [beige,blanco], [minimalista,japandi],
    precio(6500,7999)).
producto(mesa_centro_001, 'Mesa de centro Nord', mesa_centro, dim(110,60,40),
    [madera], [madera_clara,negro], [minimalista,japandi,moderno],
    precio(2200,2800)).
producto(mueble_tv_001, 'Mueble TV Lund', mueble_tv, dim(180,40,50),
    [madera], [madera_clara,madera_oscura], [moderno,minimalista,japandi],
    precio(3500,4200)).
producto(tv_65, 'TV 65 pulgadas', televisor, dim(145,8,84),
    [vidrio,metal], [negro], [moderno,minimalista,industrial,japandi,clasico],
    precio(8999,11999)).
producto(tv_75, 'TV 75 pulgadas', televisor, dim(168,8,97),
    [vidrio,metal], [negro], [moderno,minimalista,industrial,japandi,clasico],
    precio(11999,15999)).
producto(cama_001, 'Cama Queen Oslo', cama, dim(160,210,100),
    [madera], [beige,gris,madera_clara], [moderno,minimalista,japandi],
    precio(7500,9500)).
producto(alfombra_001, 'Alfombra Lana', alfombra, dim(200,300,1),
    [tela], [beige,gris], [moderno,minimalista,japandi,clasico],
    precio(2500,3500)).
producto(lampara_001, 'Lampara de piso Arco', lampara, dim(35,35,160),
    [metal], [negro,blanco], [moderno,industrial,minimalista],
    precio(1200,1800)).
producto(lampara_002, 'Lampara Washi', lampara, dim(40,40,150),
    [madera,tela], [beige], [japandi,minimalista],
    precio(1500,2200)).
producto(escritorio_001, 'Escritorio Forja', escritorio, dim(140,70,75),
    [madera,metal], [madera_oscura,negro], [moderno,industrial],
    precio(3000,4500)).
producto(silla_oficina_001, 'Silla ergonomica', silla_oficina, dim(60,60,110),
    [tela,metal], [negro,gris], [moderno,industrial,minimalista],
    precio(2500,3500)).
producto(librero_001, 'Librero Altura', librero, dim(80,30,180),
    [madera,metal], [negro,madera_oscura], [moderno,industrial,minimalista],
    precio(1800,2600)).
producto(mesa_comedor_001, 'Mesa comedor 6 personas', mesa_comedor, dim(160,90,75),
    [madera], [madera_clara,madera_oscura], [moderno,minimalista,japandi,clasico],
    precio(4500,6500)).
producto(silla_comedor_001, 'Silla comedor Neo', silla_comedor, dim(45,50,90),
    [madera,tela], [beige,gris], [moderno,minimalista,japandi,clasico],
    precio(800,1200)).
producto(isla_001, 'Isla de cocina', isla, dim(120,60,90),
    [madera], [blanco,madera_clara], [moderno,minimalista,japandi],
    precio(5000,7500)).
producto(cuadro_001, 'Cuadro abstracto', cuadro, dim(60,3,80),
    [madera], [beige,gris], [moderno,minimalista,japandi],
    precio(500,900)).
producto(planta_001, 'Planta interior', planta, dim(30,30,90),
    [ceramica], [verde], [moderno,minimalista,japandi,clasico],
    precio(300,600)).
% elemento conceptual: lo propuso la IA, no existe en catalogo
producto(concepto_001, 'Sillon conceptual (IA)', sillon, dim(90,90,80),
    [tela], [beige], [japandi],
    precio(0,0)).


es_conceptual(concepto_001).
es_real(Id) :- producto(Id, _, _, _, _, _, _, _), \+ es_conceptual(Id).

% atributos extra para filtrar por requisitos
atributo(tv_65, pulgadas, 65).
atributo(tv_75, pulgadas, 75).
atributo(cama_001, tamano, queen).
atributo(sofa_001, plazas, 3).
atributo(sofa_002, plazas, 3).
atributo(sofa_003, plazas, 2).

% ---------- proveedores y adaptadores  [DOC] ----------
% [EJEMPLO] nombres genericos
proveedor(prov_muebles,    'Tienda de muebles',     muebles).
proveedor(prov_electronica,'Tienda de electronica', electronica).
proveedor(prov_iluminacion,'Tienda de iluminacion', iluminacion).
proveedor(prov_decoracion, 'Tienda de decoracion',  decoracion).

adaptador(prov_muebles, json).
adaptador(prov_electronica, json).
adaptador(prov_iluminacion, csv).
adaptador(prov_decoracion, csv).

suministra(Prov, Id) :-
    producto(Id, _, Cat, _, _, _, _, _),
    categoria(Cat, Rubro),
    proveedor(Prov, _, Rubro).

% ---------- materiales ----------
tipo_material(madera).
tipo_material(marmol).
tipo_material(concreto).
tipo_material(ceramica).
tipo_material(pintura).
tipo_material(tela).
tipo_material(metal).
tipo_material(vidrio).

% material(Id, Tipo, Nombre, precio_m2(Min,Max))   [EJEMPLO]
material(pintura_001, pintura, 'Pintura vinilica blanca', precio_m2(60,100)).
material(piso_madera_001, madera, 'Piso laminado madera', precio_m2(450,750)).
material(piso_ceramica_001, ceramica, 'Piso ceramico', precio_m2(300,500)).
material(marmol_001, marmol, 'Marmol', precio_m2(1200,2200)).
material(concreto_001, concreto, 'Concreto pulido', precio_m2(600,900)).

% =====================================================
% 9. HABITACIONES Y ESTRUCTURA
% habitacion(Id, Espacio, Largo, Ancho, Alto)
% Sistema de coordenadas: esquina (0,0) = suroeste
%   x va a lo largo, y va a lo ancho.
% Paredes: sur (y=0), norte (y=Ancho), oeste (x=0), este (x=Largo)
% =====================================================
habitacion(h1, sala,     520, 380, 270).    % [DOC] 5.20 x 3.80 x 2.70
habitacion(h2, recamara, 400, 350, 270).    % [EJEMPLO]

% puerta(Hab, Id, Pared, Posicion, Ancho)
puerta(h1, p1, sur, 50, 90).
puerta(h2, p2, sur, 20, 80).

% ventana(Hab, Id, Pared, Posicion, Ancho, AlturaAntepecho)
ventana(h1, v1, norte, 100, 150, 90).
ventana(h2, v2, este,  100, 120, 90).

area_piso(H, M2) :-
    habitacion(H, _, L, A, _),
    M2 is L * A / 10000.

% requisitos del usuario por habitacion
% requisito(Hab, Nivel, Requisito)
requisito(h1, obligatorio, presupuesto_max(30000)).       % [DOC] ejemplo
requisito(h1, obligatorio, tiene(televisor, pulgadas, 75)).
requisito(h1, obligatorio, conservar(piso)).
requisito(h1, preferencia, estilo(moderno)).
requisito(h1, preferencia, color(sofa, beige)).
requisito(h1, opcional,    categoria(planta)).

requisito(h2, obligatorio, presupuesto_max(20000)).
requisito(h2, obligatorio, tiene(cama, tamano, queen)).
requisito(h2, preferencia, estilo(moderno)).

% =====================================================
% 10. DISENOS (VERSIONES) Y OBJETOS
% =====================================================
% diseno(Id, Habitacion, Estilo)
diseno(d1, h1, moderno).
diseno(d2, h1, moderno).
diseno(d3, h1, industrial).
diseno(d4, h2, moderno).

version_padre(d2, d1).
version_padre(d3, d1).

ancestro(A, D) :- version_padre(D, A).
ancestro(A, D) :- version_padre(D, P), ancestro(A, P).

modo_presupuesto(d1, ajustar).      % minimizar | ajustar | flexible
modo_presupuesto(d2, flexible).

% objeto(Diseno, InstanceId, CatalogId)
objeto(d1, sofa_d1,   sofa_003).
objeto(d1, mesa_d1,   mesa_centro_001).
objeto(d1, stand_d1,  mueble_tv_001).
objeto(d1, tv_d1,     tv_75).
objeto(d1, lampara_d1,lampara_001).

objeto(d2, sofa_d2,   sofa_001).
objeto(d2, mesa_d2,   mesa_centro_001).
objeto(d2, stand_d2,  mueble_tv_001).
objeto(d2, tv_d2,     tv_75).
objeto(d2, lampara_d2,lampara_001).

objeto(d3, sofa_d3,   sofa_002).
objeto(d3, mesa_d3,   mesa_centro_001).
objeto(d3, stand_d3,  mueble_tv_001).
objeto(d3, tv_d3,     tv_75).
objeto(d3, lampara_d3,lampara_001).

objeto(d4, cama_d4,   cama_001).
objeto(d4, lampara_d4,lampara_001).

% coloca(Diseno, Instancia, X, Y, Rotacion)  (X,Y = esquina suroeste del objeto)
% Rotacion: 0, 90, 180 o 270
coloca(d1, sofa_d1,    180, 110, 90).
coloca(d1, mesa_d1,    330, 135, 90).
coloca(d1, stand_d1,   480, 100, 90).
coloca(d1, tv_d1,      490, 106, 90).
coloca(d1, lampara_d1,  60, 300, 0).

coloca(d2, sofa_d2,    180,  50, 90).
coloca(d2, mesa_d2,    330, 135, 90).
coloca(d2, stand_d2,   480, 100, 90).
coloca(d2, tv_d2,      490, 106, 90).
coloca(d2, lampara_d2,  60, 300, 0).

coloca(d3, sofa_d3,     40,  10, 0).     % choca con la puerta a proposito
coloca(d3, mesa_d3,    330, 135, 90).
coloca(d3, stand_d3,   480, 100, 90).
coloca(d3, tv_d3,      490, 106, 90).
coloca(d3, lampara_d3,  60, 300, 0).

coloca(d4, cama_d4,    120,  60, 0).
coloca(d4, lampara_d4, 300, 280, 0).

% config(Diseno, Instancia, Propiedad, Valor)  - configurador de objetos
config(d1, sofa_d1, color, beige).
config(d2, sofa_d2, color, beige).
config(d3, sofa_d3, color, gris).
config(d4, cama_d4, color, beige).

% materiales aplicados: aplica_material(Diseno, Material, m2)
aplica_material(d1, pintura_001, 40).

% instalacion: instalacion(Diseno, precio(Min,Max))  [EJEMPLO]
instalacion(d1, precio(1500, 2500)).
instalacion(d2, precio(1500, 2500)).
instalacion(d3, precio(1500, 2500)).

% bloqueo de elementos por el usuario  [DOC]
bloqueado(d1, tv_d1).

% historial de versiones
historial(d2, 'duplicado de d1, sofa cambiado por Oslo').
historial(d3, 'duplicado de d1, estilo industrial, sofa Bergen').

% =====================================================
% 11. GEOMETRIA (rectangulos en planta)
% =====================================================
dims_rot(0,   W, P, W, P).
dims_rot(180, W, P, W, P).
dims_rot(90,  W, P, P, W).
dims_rot(270, W, P, P, W).

rect_producto(Id, X, Y, Rot, rect(X, Y, X2, Y2)) :-
    producto(Id, _, _, dim(W, P, _), _, _, _, _),
    dims_rot(Rot, W, P, A, B),
    X2 is X + A,
    Y2 is Y + B.

huella(D, Inst, R) :-
    objeto(D, Inst, Id),
    coloca(D, Inst, X, Y, Rot),
    rect_producto(Id, X, Y, Rot, R).

rect_dentro(H, rect(X1, Y1, X2, Y2)) :-
    habitacion(H, _, L, A, _),
    X1 >= 0, Y1 >= 0, X2 =< L, Y2 =< A.

rect_solapa(rect(AX1, AY1, AX2, AY2), rect(BX1, BY1, BX2, BY2)) :-
    AX1 < BX2, AX2 > BX1,
    AY1 < BY2, AY2 > BY1.

% zona libre pegada a una pared (para abatir puerta o acceder a ventana)
zona_pared(_, sur, Pos, Ancho, Prof, rect(Pos, 0, X2, Prof)) :-
    X2 is Pos + Ancho.
zona_pared(H, norte, Pos, Ancho, Prof, rect(Pos, Y1, X2, A)) :-
    habitacion(H, _, _, A, _),
    Y1 is A - Prof,
    X2 is Pos + Ancho.
zona_pared(_, oeste, Pos, Ancho, Prof, rect(0, Pos, Prof, Y2)) :-
    Y2 is Pos + Ancho.
zona_pared(H, este, Pos, Ancho, Prof, rect(X1, Pos, L, Y2)) :-
    habitacion(H, _, L, _, _),
    X1 is L - Prof,
    Y2 is Pos + Ancho.

zona_puerta(H, P, Z) :-
    puerta(H, P, Pared, Pos, Ancho),
    zona_pared(H, Pared, Pos, Ancho, Ancho, Z).

zona_ventana(H, V, Z) :-
    ventana(H, V, Pared, Pos, Ancho, _),
    parametro(acceso_ventana, Prof),
    zona_pared(H, Pared, Pos, Ancho, Prof, Z).

ocupa_piso(Id) :-
    producto(Id, _, Cat, _, _, _, _, _),
    \+ sin_area_piso(Cat).

alto_producto(Id, Alto) :-
    producto(Id, _, _, dim(_, _, Alto), _, _, _, _).

% =====================================================
% 12. PRESUPUESTO DINAMICO
% =====================================================
suma_pares(L, Min, Max) :-
    findall(A, member(A-_, L), As),
    findall(B, member(_-B, L), Bs),
    sum_list(As, Min),
    sum_list(Bs, Max).

presupuesto_rubro(D, materiales, Min, Max) :-
    diseno(D, _, _),
    findall(A-B,
        ( aplica_material(D, M, M2),
          material(M, _, _, precio_m2(PA, PB)),
          A is PA * M2,
          B is PB * M2 ),
        L),
    suma_pares(L, Min, Max).
presupuesto_rubro(D, instalacion, Min, Max) :-
    diseno(D, _, _),
    (   instalacion(D, precio(Min, Max))
    ->  true
    ;   Min = 0, Max = 0
    ).
presupuesto_rubro(D, Rubro, Min, Max) :-
    diseno(D, _, _),
    rubro(Rubro),
    Rubro \== materiales,
    Rubro \== instalacion,
    findall(A-B,
        ( objeto(D, _, Id),
          producto(Id, _, Cat, _, _, _, _, precio(A, B)),
          categoria(Cat, Rubro) ),
        L),
    suma_pares(L, Min, Max).

presupuesto_total(D, Min, Max) :-
    diseno(D, _, _),
    findall(A-B, ( rubro(R), presupuesto_rubro(D, R, A, B) ), L),
    suma_pares(L, Min, Max).

presupuesto_max(D, N) :-
    diseno(D, H, _),
    requisito(H, _, presupuesto_max(N)).

tope_efectivo(D, Tope) :-
    presupuesto_max(D, N),
    (   modo_presupuesto(D, flexible)
    ->  parametro(margen_flexible, M),
        Tope is N * (1 + M)
    ;   Tope = N
    ).

% ---------- optimizacion: sustituciones que ahorran ----------
sustitucion_ahorro(D, Inst, Nuevo, Ahorro) :-
    objeto(D, Inst, Actual),
    \+ bloqueado(D, Inst),
    producto(Actual, _, Cat, _, _, _, _, precio(MinA, _)),
    similar(Actual, Nuevo),
    producto(Nuevo, _, Cat, _, _, _, _, precio(MinN, _)),
    MinN < MinA,
    es_real(Nuevo),
    validar_propuesta(D, reemplazar(Inst, Nuevo), []),
    Ahorro is MinA - MinN.

% =====================================================
% 13. REQUISITOS
% =====================================================
% solo se evaluan estos tipos aqui (presupuesto y conservar van aparte)
evaluable(tiene(_, _, _)).
evaluable(estilo(_)).
evaluable(color(_, _)).
evaluable(categoria(_)).

cumple_requisito(D, tiene(Cat, K, V)) :-
    objeto(D, _, Id),
    producto(Id, _, Cat, _, _, _, _, _),
    atributo(Id, K, V), !.
cumple_requisito(D, estilo(S)) :-
    diseno(D, _, S), !.
cumple_requisito(D, color(Cat, Col)) :-
    objeto(D, Inst, Id),
    producto(Id, _, Cat, _, _, _, _, _),
    config(D, Inst, color, Col), !.
cumple_requisito(D, categoria(Cat)) :-
    objeto(D, _, Id),
    producto(Id, _, Cat, _, _, _, _, _), !.

% =====================================================
% 14. CONFLICTOS (impiden aceptar el diseno)  [DOC]
% =====================================================
colision(D, A, B) :-
    huella(D, A, RA),
    huella(D, B, RB),
    A @< B,
    objeto(D, A, IA), ocupa_piso(IA),
    objeto(D, B, IB), ocupa_piso(IB),
    rect_solapa(RA, RB).

bloquea_puerta(D, Inst, P) :-
    diseno(D, H, _),
    huella(D, Inst, R),
    objeto(D, Inst, Id), ocupa_piso(Id),
    zona_puerta(H, P, Z),
    rect_solapa(R, Z).

bloquea_ventana(D, Inst, V) :-
    diseno(D, H, _),
    huella(D, Inst, R),
    objeto(D, Inst, Id), ocupa_piso(Id),
    alto_producto(Id, Alto),
    ventana(H, V, _, _, _, Antepecho),
    Alto > Antepecho,
    zona_ventana(H, V, Z),
    rect_solapa(R, Z).

area_ocupada(D, Total) :-
    findall(A,
        ( huella(D, Inst, rect(X1, Y1, X2, Y2)),
          objeto(D, Inst, Id), ocupa_piso(Id),
          A is (X2 - X1) * (Y2 - Y1) ),
        L),
    sum_list(L, Total).

ocupacion(D, Prop) :-
    diseno(D, H, _),
    habitacion(H, _, L, A, _),
    area_ocupada(D, T),
    Prop is T / (L * A).

conflicto(D, no_cabe(Inst)) :-
    objeto(D, Inst, Id),
    diseno(D, H, _),
    producto(Id, _, _, dim(W, P, _), _, _, _, _),
    habitacion(H, _, L, A, _),
    \+ ( ( W =< L, P =< A ) ; ( P =< L, W =< A ) ).

conflicto(D, demasiado_alto(Inst)) :-
    objeto(D, Inst, Id),
    diseno(D, H, _),
    alto_producto(Id, Alto),
    habitacion(H, _, _, _, AltoHab),
    Alto > AltoHab.

conflicto(D, fuera_de_habitacion(Inst)) :-
    diseno(D, H, _),
    huella(D, Inst, R),
    \+ rect_dentro(H, R).

conflicto(D, colision(A, B))        :- colision(D, A, B).
conflicto(D, bloquea_puerta(I, P))  :- bloquea_puerta(D, I, P).
conflicto(D, bloquea_ventana(I, V)) :- bloquea_ventana(D, I, V).

conflicto(D, exceso_muebles(Prop)) :-
    ocupacion(D, Prop),
    parametro(ocupacion_maxima, Max),
    Prop > Max.

conflicto(D, categoria_no_permitida(Inst)) :-
    objeto(D, Inst, Id),
    producto(Id, _, Cat, _, _, _, _, _),
    diseno(D, H, _),
    habitacion(H, Esp, _, _, _),
    \+ categoria_en(Esp, Cat).

conflicto(D, color_no_disponible(Inst, Col)) :-
    config(D, Inst, color, Col),
    objeto(D, Inst, Id),
    producto(Id, _, _, _, _, Colores, _, _),
    \+ member(Col, Colores).

conflicto(D, presupuesto_excedido) :-
    tope_efectivo(D, Tope),
    presupuesto_total(D, Min, _),
    Min > Tope.

conflicto(D, falta_requisito(Req)) :-
    diseno(D, H, _),
    requisito(H, obligatorio, Req),
    evaluable(Req),
    \+ cumple_requisito(D, Req).

diseno_valido(D) :-
    diseno(D, _, _),
    \+ conflicto(D, _).

conflictos(D, L) :- findall(C, conflicto(D, C), L).

% =====================================================
% 15. ADVERTENCIAS (no bloquean, pero se informan)
% =====================================================
advertencia(D, presupuesto_puede_excederse) :-
    tope_efectivo(D, Tope),
    presupuesto_total(D, Min, Max),
    Min =< Tope,
    Max > Tope.

advertencia(D, estilo_incompatible(Inst)) :-
    diseno(D, _, S),
    objeto(D, Inst, Id),
    producto(Id, _, _, _, _, _, Estilos, _),
    \+ ( member(E, Estilos), estilos_compatibles(S, E) ).

advertencia(D, requisito_no_cumplido(Nivel, Req)) :-
    diseno(D, H, _),
    requisito(H, Nivel, Req),
    ( Nivel == importante ; Nivel == preferencia ),
    evaluable(Req),
    \+ cumple_requisito(D, Req).

advertencia(D, falta_basico(Cat)) :-
    diseno(D, H, _),
    habitacion(H, Esp, _, _, _),
    basico(Esp, Basicos),
    member(Cat, Basicos),
    \+ ( objeto(D, _, Id), producto(Id, _, Cat, _, _, _, _, _) ).

advertencia(D, elemento_conceptual(Inst)) :-
    objeto(D, Inst, Id),
    es_conceptual(Id).

advertencias(D, L) :- findall(A, advertencia(D, A), L).

% =====================================================
% 16. BLOQUEO Y PROPUESTAS DE LA IA
% IA propone -> motor valida -> acepta/rechaza -> frontend aplica  [DOC]
% =====================================================
modificable_por_ia(D, Inst) :-
    objeto(D, Inst, _),
    \+ bloqueado(D, Inst).

% validar_propuesta(+Diseno, +Accion, -Motivos)   Motivos = [] => se acepta
% Acciones: agregar(Inst,Id,X,Y,Rot) | mover(Inst,X,Y,Rot)
%           reemplazar(Inst,NuevoId) | quitar(Inst) | modificar_estructura(Elem)

colision_rect(D, Excl, R, Otro) :-
    huella(D, Otro, RO),
    Otro \== Excl,
    objeto(D, Otro, IdO), ocupa_piso(IdO),
    rect_solapa(R, RO).

motivo_colocacion(D, _, Id, X, Y, Rot, fuera_de_habitacion) :-
    diseno(D, H, _),
    rect_producto(Id, X, Y, Rot, R),
    \+ rect_dentro(H, R).
motivo_colocacion(D, Excl, Id, X, Y, Rot, colision_con(O)) :-
    ocupa_piso(Id),
    rect_producto(Id, X, Y, Rot, R),
    colision_rect(D, Excl, R, O).
motivo_colocacion(D, _, Id, X, Y, Rot, bloquea_puerta(P)) :-
    ocupa_piso(Id),
    diseno(D, H, _),
    rect_producto(Id, X, Y, Rot, R),
    zona_puerta(H, P, Z),
    rect_solapa(R, Z).
motivo_colocacion(D, _, Id, X, Y, Rot, bloquea_ventana(V)) :-
    ocupa_piso(Id),
    alto_producto(Id, Alto),
    diseno(D, H, _),
    rect_producto(Id, X, Y, Rot, R),
    ventana(H, V, _, _, _, Antepecho),
    Alto > Antepecho,
    zona_ventana(H, V, Z),
    rect_solapa(R, Z).

% --- mover ---
validar_propuesta(D, mover(Inst, X, Y, Rot), Motivos) :-
    objeto(D, Inst, Id),
    findall(M, motivo_colocacion(D, Inst, Id, X, Y, Rot, M), M1),
    (   bloqueado(D, Inst)
    ->  Motivos = [elemento_bloqueado(Inst) | M1]
    ;   Motivos = M1
    ).

% --- agregar ---
validar_propuesta(D, agregar(Inst, Id, X, Y, Rot), Motivos) :-
    diseno(D, _, _),
    findall(M, motivo_agregar(D, Inst, Id, X, Y, Rot, M), Motivos).

motivo_agregar(D, Inst, _, _, _, _, instancia_repetida) :-
    objeto(D, Inst, _).
motivo_agregar(_, _, Id, _, _, _, producto_inexistente) :-
    \+ producto(Id, _, _, _, _, _, _, _).
motivo_agregar(D, _, Id, _, _, _, categoria_no_permitida) :-
    producto(Id, _, Cat, _, _, _, _, _),
    diseno(D, H, _),
    habitacion(H, Esp, _, _, _),
    \+ categoria_en(Esp, Cat).
motivo_agregar(D, _, Id, _, _, _, no_cabe) :-
    producto(Id, _, _, dim(W, P, _), _, _, _, _),
    diseno(D, H, _),
    habitacion(H, _, L, A, _),
    \+ ( ( W =< L, P =< A ) ; ( P =< L, W =< A ) ).
motivo_agregar(D, Inst, Id, X, Y, Rot, M) :-
    motivo_colocacion(D, Inst, Id, X, Y, Rot, M).
motivo_agregar(D, _, Id, _, _, _, presupuesto_excedido) :-
    tope_efectivo(D, Tope),
    presupuesto_total(D, Min, _),
    producto(Id, _, _, _, _, _, _, precio(PM, _)),
    Min + PM > Tope.

% --- reemplazar (conserva posicion y rotacion)  [DOC] ---
validar_propuesta(D, reemplazar(Inst, Nuevo), Motivos) :-
    objeto(D, Inst, Actual),
    coloca(D, Inst, X, Y, Rot),
    findall(M, motivo_reemplazo(D, Inst, Actual, Nuevo, X, Y, Rot, M), Motivos).

motivo_reemplazo(D, Inst, _, _, _, _, _, elemento_bloqueado(Inst)) :-
    bloqueado(D, Inst).
motivo_reemplazo(_, _, _, Nuevo, _, _, _, producto_inexistente) :-
    \+ producto(Nuevo, _, _, _, _, _, _, _).
motivo_reemplazo(_, _, Actual, Nuevo, _, _, _, categoria_distinta) :-
    producto(Actual, _, C1, _, _, _, _, _),
    producto(Nuevo, _, C2, _, _, _, _, _),
    C1 \== C2.
motivo_reemplazo(D, Inst, _, Nuevo, X, Y, Rot, M) :-
    motivo_colocacion(D, Inst, Nuevo, X, Y, Rot, M).
motivo_reemplazo(D, _, Actual, Nuevo, _, _, _, presupuesto_excedido) :-
    tope_efectivo(D, Tope),
    presupuesto_total(D, Min, _),
    producto(Actual, _, _, _, _, _, _, precio(MinA, _)),
    producto(Nuevo,  _, _, _, _, _, _, precio(MinN, _)),
    Min - MinA + MinN > Tope.
motivo_reemplazo(D, _, Actual, Nuevo, _, _, _, rompe_requisito(tiene(Cat, K, V))) :-
    producto(Actual, _, Cat, _, _, _, _, _),
    diseno(D, H, _),
    requisito(H, obligatorio, tiene(Cat, K, V)),
    \+ atributo(Nuevo, K, V).

% --- quitar ---
validar_propuesta(D, quitar(Inst), Motivos) :-
    objeto(D, Inst, Id),
    producto(Id, _, Cat, _, _, _, _, _),
    diseno(D, H, _),
    findall(M,
        (   bloqueado(D, Inst), M = elemento_bloqueado(Inst)
        ;   requisito(H, obligatorio, tiene(Cat, K, V)),
            atributo(Id, K, V),
            \+ ( objeto(D, Otra, Id2), Otra \== Inst, atributo(Id2, K, V) ),
            M = rompe_requisito(tiene(Cat, K, V))
        ),
        Motivos).

% --- estructura: protegida salvo accion explicita del usuario ---
validar_propuesta(D, modificar_estructura(E), Motivos) :-
    diseno(D, _, _),
    (   \+ estructural(E)
    ->  Motivos = [no_es_elemento_estructural(E)]
    ;   confirmacion_usuario(D, E)
    ->  Motivos = []
    ;   Motivos = [estructural_protegido(E)]
    ).

veredicto(D, Accion, V) :-
    (   validar_propuesta(D, Accion, M)
    ->  (   M == [] -> V = aceptada ; V = rechazada(M) )
    ;   V = rechazada([accion_invalida])
    ).

% =====================================================
% 17. BUSQUEDA DE SIMILARES Y REEMPLAZO  [DOC]
% =====================================================
similar(A, B) :-
    producto(A, _, Cat, _, _, _, EA, _),
    producto(B, _, Cat, _, _, _, EB, _),
    A \== B,
    once(( member(E, EA), memberchk(E, EB) )).

buscar_similares(Id, Lista) :-
    findall(Min-Otro,
        ( similar(Id, Otro),
          producto(Otro, _, _, _, _, _, _, precio(Min, _)) ),
        L0),
    keysort(L0, L1),
    pairs_values(L1, Lista).

% alternativas que SI se pueden poner en el lugar de un objeto
alternativas(D, Inst, Lista) :-
    objeto(D, Inst, Actual),
    findall(Nuevo,
        ( similar(Actual, Nuevo),
          validar_propuesta(D, reemplazar(Inst, Nuevo), []) ),
        Lista).

producto_de_estilo(Id, Estilo) :-
    producto(Id, _, _, _, _, _, Estilos, _),
    member(Estilo, Estilos).

% =====================================================
% 18. VERSIONES: COMPARACION  [DOC]
% =====================================================
producto_por_categoria(D, Cat, Id) :-
    objeto(D, _, Id),
    producto(Id, _, Cat, _, _, _, _, _).

diferencia(A, B, Cat, IdA, IdB) :-
    producto_por_categoria(A, Cat, IdA),
    (   producto_por_categoria(B, Cat, IdB) -> true ; IdB = ninguno ),
    IdA \== IdB.
diferencia(A, B, Cat, ninguno, IdB) :-
    producto_por_categoria(B, Cat, IdB),
    \+ producto_por_categoria(A, Cat, _).

comparar_versiones(A, B, Difs) :-
    findall(dif(Cat, IdA, IdB), diferencia(A, B, Cat, IdA, IdB), L),
    sort(L, Difs).

% =====================================================
% 19. LISTA DE COMPRAS Y MATERIALES  [DOC]
% =====================================================
lista_compras(D, Items) :-
    diseno(D, _, _),
    findall(Id, objeto(D, _, Id), Ids0),
    sort(Ids0, Ids),
    findall(item(Rubro, Cant, Nombre),
        ( member(Id, Ids),
          aggregate_all(count, objeto(D, _, Id), Cant),
          producto(Id, Nombre, Cat, _, _, _, _, _),
          categoria(Cat, Rubro) ),
        Items).

lista_materiales(D, Items) :-
    diseno(D, _, _),
    parametro(desperdicio_material, F),
    findall(item(Nombre, Cant, m2),
        ( aplica_material(D, M, M2),
          material(M, _, Nombre, _),
          Cant is ceiling(M2 * (1 + F)) ),
        Items).

% =====================================================
% 20. EXPLICACION DE DECISIONES  [DOC]
% =====================================================
razon(D, Inst, cabe_en_la_habitacion) :-
    objeto(D, Inst, _),
    \+ conflicto(D, no_cabe(Inst)),
    \+ conflicto(D, fuera_de_habitacion(Inst)).
razon(D, Inst, estilo_compatible(S)) :-
    objeto(D, Inst, _),
    diseno(D, _, S),
    \+ advertencia(D, estilo_incompatible(Inst)).
razon(D, Inst, sin_colisiones) :-
    objeto(D, Inst, _),
    \+ colision(D, Inst, _),
    \+ colision(D, _, Inst).
razon(D, Inst, no_bloquea_puertas_ni_ventanas) :-
    objeto(D, Inst, _),
    \+ bloquea_puerta(D, Inst, _),
    \+ bloquea_ventana(D, Inst, _).
razon(D, Inst, producto_real) :-
    objeto(D, Inst, Id),
    es_real(Id).

explicacion(D, Inst, Razones) :-
    objeto(D, Inst, _),
    findall(R, razon(D, Inst, R), Razones).

% =====================================================
% 21. ASISTENTE CONVERSACIONAL (palabras clave -> intencion)  [DOC]
% =====================================================
intencion(calida,        ajustar_iluminacion(calida)).
intencion(calido,        ajustar_iluminacion(calida)).
intencion(iluminacion,   objetivo(iluminacion)).
intencion(moderno,       cambiar_estilo(moderno)).
intencion(minimalista,   cambiar_estilo(minimalista)).
intencion(industrial,    cambiar_estilo(industrial)).
intencion(japandi,       cambiar_estilo(japandi)).
intencion(clasico,       cambiar_estilo(clasico)).
intencion(pequeno,       cambiar_tamano(menor)).
intencion(chico,         cambiar_tamano(menor)).
intencion(grande,        cambiar_tamano(mayor)).
intencion(barato,        modo_presupuesto(minimizar)).
intencion(economico,     modo_presupuesto(minimizar)).
intencion(presupuesto,   objetivo(presupuesto)).
intencion(cambia,        accion(reemplazar)).
intencion(cambiar,       accion(reemplazar)).
intencion(conserva,      accion(bloquear)).
intencion(manten,        accion(bloquear)).
intencion(quita,         accion(quitar)).
intencion(agrega,        accion(agregar)).
intencion(sofa,          objetivo(sofa)).
intencion(tele,          objetivo(televisor)).
intencion(televisor,     objetivo(televisor)).
intencion(television,    objetivo(televisor)).
intencion(cama,          objetivo(cama)).
intencion(mesa,          objetivo(mesa_centro)).
intencion(alfombra,      objetivo(alfombra)).
intencion(lampara,       objetivo(lampara)).

% interpretar([cambia,el,sofa,por,uno,mas,pequeno], Acciones)
interpretar(Palabras, Acciones) :-
    findall(A, ( member(P, Palabras), intencion(P, A) ), L),
    sort(L, Acciones).

% =====================================================
% 22. ILUMINACION  [DOC]
% =====================================================
modo_luz(dia).
modo_luz(noche).
tipo_luz(natural).
tipo_luz(artificial).
temperatura_luz(calida, 2700).
temperatura_luz(neutra, 4000).
temperatura_luz(fria,   6000).
propiedad_luz(intensidad).
propiedad_luz(temperatura).
propiedad_luz(tipo).
propiedad_luz(posicion).

% =====================================================
% 23. REGLAS PARA LAS IA (/ai-rules)  [DOC]
% =====================================================
agente(global).
agente(interior_designer).
agente(spatial_analysis).
agente(furniture_selector).
agente(budget_manager).
agente(image_generator).
agente(blender_generator).
agente(safety_rules).

regla_ia(global, 'Nunca modificar las dimensiones del espacio').
regla_ia(global, 'Nunca mover paredes estructurales').
regla_ia(global, 'Nunca eliminar objetos obligatorios').
regla_ia(global, 'Nunca bloquear puertas ni ventanas').
regla_ia(global, 'Respetar las dimensiones reales de los productos').
regla_ia(global, 'No superar el presupuesto sin autorizacion').
regla_ia(global, 'Mantener la circulacion').
regla_ia(global, 'Generar unicamente JSON valido conforme al DesignSchema').
regla_ia(safety_rules, 'El codigo hace la validacion definitiva, los .md solo guian a la IA').

principio(1,  'La IA ayuda, pero el usuario decide').
principio(2,  'El diseno debe ser editable').
principio(3,  'El modelo estructurado es la fuente de verdad').
principio(4,  'Las imagenes generadas son representaciones, no la fuente geometrica').
principio(5,  'Las dimensiones reales deben respetarse').
principio(6,  'Las restricciones obligatorias deben mantenerse').
principio(7,  'El presupuesto debe ser visible y dinamico').
principio(8,  'Cada objeto debe poder identificarse').
principio(9,  'Los productos reales deben diferenciarse de elementos conceptuales').
principio(10, 'Las versiones deben ser independientes').
principio(11, 'Los cambios deben poder revertirse').
principio(12, 'La IA debe validar sus propuestas').
principio(13, 'Los elementos estructurales deben estar protegidos').
principio(14, 'El sistema debe ser extensible a nuevos proveedores').
principio(15, 'n8n es orquestador, no sustituto del backend ni del motor 3D').

% =====================================================
% 24. ARQUITECTURA, FLUJO Y n8n  [DOC]
% =====================================================
componente(frontend).
componente(backend).
componente(n8n).
componente(ia).
componente(catalogos).
componente(blender).
componente(base_datos).
componente(almacenamiento).

flujo([usuario, fotos_planos_medidas, analisis_ia, espacio_estructurado,
       requisitos, ia_diseno, propuestas, catalogo, validacion_espacial,
       presupuesto, modelo_3d, versiones, modificacion, exportacion]).

siguiente_paso(A, B) :- flujo(F), nextto(A, B, F).

flujo_n8n(analisis_fotos,
    [upload, vision_ai, space_json, validation, database]).
flujo_n8n(generacion_diseno,
    [requisitos, interior_designer_ai, product_matcher,
     budget_calculator, validation, design_state]).
flujo_n8n(generacion_imagenes,
    [design_state, prompt_builder, image_ai, render]).
flujo_n8n(blender,
    [design_state, blender_python, blend_o_render]).

% tiempo real: frontend/3D, SIN n8n
tarea_tiempo_real(mover_objeto).
tarea_tiempo_real(rotar_objeto).
tarea_tiempo_real(cambiar_color).
tarea_tiempo_real(cambiar_material).
tarea_tiempo_real(mover_lampara).

% pesadas o con IA: pasan por n8n como trabajos asincronos
tarea_n8n(analisis_fotos).
tarea_n8n(analisis_planos).
tarea_n8n(generacion_diseno).
tarea_n8n(generacion_imagenes).
tarea_n8n(generacion_blender).
tarea_n8n(renderizado).
tarea_n8n(busqueda_productos).

requiere_n8n(T) :- tarea_n8n(T).
asincrona(T)    :- tarea_n8n(T).

estado_trabajo(pendiente).
estado_trabajo(en_proceso).
estado_trabajo(completado).
estado_trabajo(fallido).

tecnologia(frontend, 'React / Next.js').
tecnologia(frontend, 'Three.js').
tecnologia(frontend, 'WebGL').
tecnologia(backend, 'Node.js o .NET').
tecnologia(backend, 'REST API').
tecnologia(backend, 'WebSocket o SSE').
tecnologia(base_datos, 'PostgreSQL').
tecnologia(automatizacion, 'n8n').
tecnologia(modelo_3d, 'GLB / GLTF').
tecnologia(modelo_3d, 'Blender + Python').
tecnologia(almacenamiento, 'Object Storage').

formato_exportacion(render_hd).
formato_exportacion(plano_2d).
formato_exportacion(modelo_3d).
formato_exportacion(archivo_blender).
formato_exportacion(lista_productos).
formato_exportacion(lista_materiales).
formato_exportacion(presupuesto).
formato_exportacion(reporte_pdf).

funcion_futura(medicion_en_3d).
funcion_futura(modo_dia_noche).
funcion_futura(recorrido_virtual).
funcion_futura(vr).
funcion_futura(ar).
funcion_futura(comparacion_antes_despues).
funcion_futura(busqueda_visual_productos).
funcion_futura(favoritos).
funcion_futura(historial_proyectos).

% =====================================================
% 25. PRUEBAS RAPIDAS:   ?- pruebas.
% =====================================================
q(Meta) :-
    format("~n?- ~q~n", [Meta]),
    (   call(Meta) -> format("   -> ~q~n", [Meta]) ; format("   -> false~n") ).

pruebas :-
    q(diseno_valido(d1)),
    q(conflictos(d1, _)),
    q(advertencias(d1, _)),
    q(presupuesto_total(d1, _, _)),
    q(conflictos(d2, _)),
    q(conflictos(d3, _)),
    q(diseno_valido(d4)),
    q(sustitucion_ahorro(d2, sofa_d2, _, _)),
    q(advertencias(d3, _)),
    q(buscar_similares(sofa_001, _)),
    q(veredicto(d1, mover(sofa_d1, 200, 110, 90), _)),
    q(veredicto(d1, mover(tv_d1, 300, 100, 0), _)),
    q(veredicto(d1, reemplazar(tv_d1, tv_65), _)),
    q(veredicto(d1, reemplazar(sofa_d1, sofa_001), _)),
    q(veredicto(d1, agregar(alfombra_d1, alfombra_001, 150, 90, 90), _)),
    q(veredicto(d1, mover(lampara_d1, 120, 345, 0), _)),
    q(veredicto(d1, agregar(escr_d1, escritorio_001, 100, 100, 0), _)),
    q(veredicto(d1, modificar_estructura(muro), _)),
    q(comparar_versiones(d1, d2, _)),
    q(lista_compras(d1, _)),
    q(lista_materiales(d1, _)),
    q(explicacion(d1, sofa_d1, _)),
    q(interpretar([cambia, el, sofa, por, uno, mas, pequeno], _)),
    q(findall(X, ancestro(X, d3), _)),
    q(siguiente_paso(analisis_ia, _)).
