# Generador para Blender (Fase 6)

- La IA **no** construye archivos `.blend`. El flujo es: DesignState (JSON) -> script Python -> Blender.
- Si se le pide algo para Blender, la IA solo puede sugerir parametros de render (camara, luz, hora del dia) en JSON;
  la geometria sale siempre del DesignState validado.
