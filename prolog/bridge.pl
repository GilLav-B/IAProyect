% =====================================================
% Puente .NET -> diseno.pl
% Uso (lo arma InteriorDesign.PrologBridge):
%   swipl -q -g "consult('prolog/bridge.pl')" -g "bridge_main('hechos.pl')" -t halt diseno.pl
%
% 1. Borra los hechos de ejemplo de diseno.pl (habitaciones, catalogo, disenos...).
% 2. Carga los hechos generados desde el DesignState (archivo hechos.pl).
% 3. El archivo de hechos trae al final terminos consulta(Clave, Var, Meta):
%    por cada uno imprime "Clave<TAB>Resultado" con writeq, o "Clave<TAB>false".
% No modifica diseno.pl: solo cambia los hechos en memoria.
% =====================================================

hecho_reemplazable(habitacion/5).
hecho_reemplazable(puerta/5).
hecho_reemplazable(ventana/6).
hecho_reemplazable(requisito/3).
hecho_reemplazable(diseno/3).
hecho_reemplazable(version_padre/2).
hecho_reemplazable(modo_presupuesto/2).
hecho_reemplazable(objeto/3).
hecho_reemplazable(coloca/5).
hecho_reemplazable(config/4).
hecho_reemplazable(aplica_material/3).
hecho_reemplazable(instalacion/2).
hecho_reemplazable(bloqueado/2).
hecho_reemplazable(confirmacion_usuario/2).
hecho_reemplazable(historial/2).
hecho_reemplazable(producto/8).
hecho_reemplazable(es_conceptual/1).
hecho_reemplazable(atributo/3).
hecho_reemplazable(material/4).

limpiar_hechos :-
    forall(hecho_reemplazable(N/A),
           ( abolish(N/A), dynamic(N/A) )).

cargar_hechos(Archivo) :-
    setup_call_cleanup(
        open(Archivo, read, S, [encoding(utf8)]),
        leer_terminos(S),
        close(S)).

leer_terminos(S) :-
    read_term(S, T, []),
    (   T == end_of_file
    ->  true
    ;   guardar(T),
        leer_terminos(S)
    ).

guardar(consulta(Clave, Var, Meta)) :- !,
    assertz(pendiente(Clave, Var, Meta)).
guardar(T) :-
    assertz(T).

:- dynamic pendiente/3.

responder :-
    forall(pendiente(Clave, Var, Meta),
           (   catch(once(Meta), E, (print_message(error, E), fail))
           ->  format("~w\t~q~n", [Clave, Var])
           ;   format("~w\tfalse~n", [Clave])
           )).

bridge_main(Archivo) :-
    limpiar_hechos,
    cargar_hechos(Archivo),
    responder.
