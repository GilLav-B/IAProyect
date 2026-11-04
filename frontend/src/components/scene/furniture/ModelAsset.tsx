"use client";

import { useMemo } from "react";
import { Box3, Color, Mesh, type Material, MeshStandardMaterial, Vector3 } from "three";
import { useGLTF } from "@react-three/drei";
import { colorHex } from "@/lib/units";
import type { Size } from "./Procedural";

interface Props {
  url: string;
  size: Size;
  /** Color elegido; si es distinto al color original del producto se tine el modelo. */
  tint?: string;
}

/**
 * Modelo GLB (CC0) ajustado EXACTAMENTE a las medidas del producto: se escala en cada eje para
 * ocupar ancho x fondo x alto del catalogo, apoyado en el piso y centrado en su huella.
 * Si el modelo viene "acostado" al reves (su lado largo en el otro eje), se gira 90 grados antes.
 */
export function ModelAsset({ url, size, tint }: Props) {
  const { scene } = useGLTF(url);

  const object = useMemo(() => {
    const copy = scene.clone(true);
    copy.traverse((child) => {
      if (!(child instanceof Mesh)) return;
      child.castShadow = true;
      child.receiveShadow = true;
      if (tint) {
        const tintColor = new Color(colorHex(tint));
        const tintMat = (m: Material) => {
          const c = m.clone();
          if (c instanceof MeshStandardMaterial) c.color.multiply(tintColor);
          return c;
        };
        child.material = Array.isArray(child.material) ? child.material.map(tintMat) : tintMat(child.material);
      }
    });

    const raw = new Box3().setFromObject(copy).getSize(new Vector3());
    // La escala se aplica en ejes locales ANTES de la rotacion: si se gira 90 grados,
    // el eje X local termina ocupando el fondo y el Z local el ancho.
    const swap = size.w >= size.d !== raw.x >= raw.z;
    if (swap) {
      copy.rotation.y = Math.PI / 2;
      copy.scale.set(size.d / raw.x, size.h / raw.y, size.w / raw.z);
    } else {
      copy.scale.set(size.w / raw.x, size.h / raw.y, size.d / raw.z);
    }
    const fitted = new Box3().setFromObject(copy);
    const center = fitted.getCenter(new Vector3());
    copy.position.set(-center.x, -fitted.min.y, -center.z);
    return copy;
  }, [scene, size.w, size.d, size.h, tint]);

  return <primitive object={object} />;
}
