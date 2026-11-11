import type { Reason } from "./types";
import { label } from "./units";

/** Texto legible para conflictos, advertencias y motivos de rechazo del motor. */
export function describe(r: Reason): string {
  const [a, b] = r.args;
  switch (r.code) {
    case "elemento_bloqueado": return `${a} esta bloqueado: desbloquealo para cambiarlo`;
    case "fuera_de_habitacion": return a ? `${a} se sale de la habitacion` : "Se sale de la habitacion";
    case "colision_con": return `Choca con ${a}`;
    case "colision": return `${a} choca con ${b}`;
    case "bloquea_puerta": return b ? `${a} bloquea la puerta ${b}` : `Bloquea la puerta ${a}`;
    case "bloquea_ventana": return b ? `${a} tapa el acceso a la ventana ${b}` : `Tapa el acceso a la ventana ${a}`;
    case "presupuesto_excedido": return "Rebasa el presupuesto maximo";
    case "presupuesto_puede_excederse": return "El presupuesto podria rebasarse (rango maximo arriba del tope)";
    case "rompe_requisito": return `Rompe un requisito obligatorio: ${a}`;
    case "falta_requisito": return `Falta un requisito obligatorio: ${a}`;
    case "requisito_no_cumplido": return `Requisito ${a} no cumplido: ${b}`;
    case "no_cabe": return a ? `${a} no cabe en la habitacion` : "No cabe en la habitacion";
    case "demasiado_alto": return `${a} es mas alto que el techo`;
    case "exceso_muebles": return `Demasiados muebles (${Math.round(Number(a) * 100)}% del piso ocupado)`;
    case "categoria_no_permitida": return a ? `${a} no va en este tipo de espacio` : "Esa categoria no va en este tipo de espacio";
    case "categoria_distinta": return "El producto nuevo es de otra categoria";
    case "color_no_disponible": return `${a}: el color ${label(b ?? "")} no existe para ese producto`;
    case "estilo_incompatible": return `${a} no combina con el estilo del diseno`;
    case "falta_basico": return `Falta un mueble basico: ${label(a ?? "")}`;
    case "elemento_conceptual": return `${a} es conceptual (idea de la IA, no se puede comprar)`;
    case "producto_inexistente": return "El producto no existe en el catalogo";
    case "instancia_repetida": return "Ya existe un objeto con ese id";
    case "estructural_protegido": return `${a} es estructural: necesita confirmacion explicita`;
    case "accion_invalida": return "Accion invalida";
    case "accion_desconocida": return "La IA propuso una accion que no existe";
    case "objeto_inexistente": return "El mueble no existe en este diseno";
    case "propuesta_incompleta": return "A la propuesta le faltan datos";
    case "sin_cambio": return "No cambia nada (ya estaba asi)";
    default: return r.term;
  }
}
