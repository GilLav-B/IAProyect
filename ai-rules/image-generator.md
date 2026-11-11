# Generador de imagenes

Genera una imagen **referencial** de como podria verse el cuarto con el diseno actual.

- Describe exactamente lo que hay en el DesignState: tipo de espacio, medidas aproximadas, color de muros, piso, y cada
  mueble con su color, material y ubicacion relativa (contra que pared, que mira hacia que).
- Respeta el estilo y la paleta del diseno. Fotografia de interiores realista, luz natural, angulo amplio a la altura de
  los ojos, sin personas, sin texto, sin marcas de agua.
- No agregues muebles que no esten en el diseno ni quites los que si estan.
- La imagen es solo una **ayuda visual**: en la plataforma se muestra con la leyenda
  "Imagen de referencia generada por IA. No representa medidas ni productos exactos."

## Modo render (captura del visor 3D)

Cuando se adjunta una captura del modelo 3D, la tarea es **convertirla en foto realista**, no inventar un cuarto nuevo:

- Mismo punto de vista, misma forma del cuarto, mismas puertas y ventanas.
- Cada mueble en la misma posicion, con el mismo tamano y orientacion. Si en la captura se ve como una caja simple,
  se dibuja como el mueble real descrito (mismo tamano y lugar).
- Solo cambia el realismo: materiales, texturas, iluminacion, sombras y detalles del estilo.
- Solo aparecen los muebles que se ven en la captura: los que quedan fuera del encuadre no se dibujan. El visor manda la
  lista exacta de lo visible y en que parte del cuadro esta (izquierda/centro/derecha, cerca/fondo).
- Prohibido agregar decoracion que no este en la captura (plantas, cuadros, cojines, mantas, cortinas, jarrones, libros).
  Las zonas vacias se quedan vacias. La nota del usuario solo cambia ambiente (luz, hora), nunca objetos.
- No se agregan ni se quitan muebles. Sin personas, sin texto, sin marcas de agua.
