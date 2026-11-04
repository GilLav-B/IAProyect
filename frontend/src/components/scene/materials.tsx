"use client";

import { useMemo } from "react";
import { Color, RepeatWrapping, SRGBColorSpace, type Texture } from "three";
import { useTexture } from "@react-three/drei";
import { colorHex } from "@/lib/units";

// Texturas CC0 de Poly Haven (ver public/assets/CREDITOS.md). Se cargan una sola vez y se comparten.
const T = "/assets/textures";

export const TEXTURES = {
  fabric: { normalMap: `${T}/cotton_jersey_nor_gl.jpg`, roughnessMap: `${T}/cotton_jersey_rough.jpg` },
  wood: {
    map: `${T}/oak_veneer_01_diffuse.jpg`,
    normalMap: `${T}/oak_veneer_01_nor_gl.jpg`,
    roughnessMap: `${T}/oak_veneer_01_rough.jpg`,
  },
  floor: {
    map: `${T}/laminate_floor_02_diffuse.jpg`,
    normalMap: `${T}/laminate_floor_02_nor_gl.jpg`,
    roughnessMap: `${T}/laminate_floor_02_rough.jpg`,
  },
  plaster: { normalMap: `${T}/white_plaster_02_nor_gl.jpg`, roughnessMap: `${T}/white_plaster_02_rough.jpg` },
};

export const HDRI = "/assets/hdri/brown_photostudio_02_1k.hdr";

/** Repite la textura (para que la tela o la madera no se vean estiradas). */
export function tile(textures: Record<string, Texture>, repeat: number) {
  for (const [key, t] of Object.entries(textures)) {
    t.wrapS = t.wrapT = RepeatWrapping;
    t.repeat.set(repeat, repeat);
    if (key === "map") t.colorSpace = SRGBColorSpace;
    t.needsUpdate = true;
  }
}

/** Colores del catalogo que son madera: se usa la textura de roble tenida. */
const WOOD_TINT: Record<string, string> = {
  madera_clara: "#f2dcc0",
  madera_oscura: "#7a5a43",
};

export const isWood = (color?: string) => !!color && color in WOOD_TINT;

/** Tela con relieve real; el color lo pone el configurador. */
export function FabricMaterial({ color }: { color?: string }) {
  const tex = useTexture(TEXTURES.fabric, (t) => tile(t as unknown as Record<string, Texture>, 6));
  return <meshStandardMaterial {...tex} color={colorHex(color)} roughness={1} normalScale={[0.6, 0.6]} />;
}

/** Madera de roble; madera_oscura / negro la oscurecen. */
export function WoodMaterial({ color }: { color?: string }) {
  const tex = useTexture(TEXTURES.wood, (t) => tile(t as unknown as Record<string, Texture>, 2));
  const tint = useMemo(() => new Color(color && WOOD_TINT[color] ? WOOD_TINT[color] : color ? colorHex(color) : "#ffffff"), [color]);
  return <meshStandardMaterial {...tex} color={tint} roughness={0.75} />;
}

/** Metal pintado o cepillado. */
export function MetalMaterial({ color = "negro" }: { color?: string }) {
  return <meshStandardMaterial color={colorHex(color)} metalness={0.85} roughness={0.35} />;
}

/** Pintura mate / laca (gabinetes, marcos). */
export function PaintMaterial({ color }: { color?: string }) {
  return <meshStandardMaterial color={colorHex(color)} roughness={0.55} />;
}

/** Usa madera si el color es de madera; si no, pintura. */
export function SurfaceMaterial({ color }: { color?: string }) {
  return isWood(color) ? <WoodMaterial color={color} /> : <PaintMaterial color={color} />;
}
