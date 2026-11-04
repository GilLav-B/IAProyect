// Descarga los assets 3D gratuitos (CC0, Poly Haven) y los optimiza para la web.
// Uso:  npm run assets        (solo baja lo que falta)
//       npm run assets -- --force
//
// Modelos -> public/assets/models/<id>.glb   (texturas WebP de 1024 px, sin simplificar la malla)
// Texturas -> public/assets/textures/<id>_<mapa>.jpg (1k)
// HDRI     -> public/assets/hdri/<id>_1k.hdr
// Licencia de todo: CC0 (https://polyhaven.com/license). Ver public/assets/CREDITOS.md

import { execFileSync } from "node:child_process";
import { existsSync, mkdirSync, rmSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const out = join(root, "public", "assets");
const cache = join(root, ".assets-cache");
const force = process.argv.includes("--force");
const UA = { "User-Agent": "IAProyect/1.0 (proyecto escolar)" };

/** Modelos que encajan con el catalogo (los demas muebles se arman en codigo). */
/** simplify: fraccion de triangulos a conservar (solo para mallas muy densas). */
const MODELS = [
  { id: "modern_coffee_table_01" }, // mesa de centro Nord
  { id: "modern_wooden_cabinet" }, // mueble TV Lund
  { id: "wooden_display_shelves_01" }, // librero
  { id: "metal_office_desk" }, // escritorio Forja
  { id: "potted_plant_01", simplify: 0.25 }, // planta (hojas muy densas)
  // Catalogo ampliado [EJEMPLO] (schemas/examples/catalog-extra.json)
  { id: "sofa_02" }, // sofa Chesterfield
  { id: "Sofa_01" }, // sofa provenzal
  { id: "mid_century_lounge_chair" }, // sillon lounge
  { id: "modern_arm_chair_01" }, // sillon nordico
  { id: "ArmChair_01" }, // sillon bergere
  { id: "industrial_coffee_table" }, // mesa de centro forja
  { id: "coffee_table_round_01" }, // mesa de centro redonda
  { id: "modern_coffee_table_02" }, // mesa de centro calada
  { id: "ClassicConsole_01" }, // consola clasica
  { id: "steel_frame_shelves_03" }, // librero industrial
  { id: "painted_wooden_chair_01" }, // silla de comedor
  { id: "potted_plant_02", simplify: 0.3 }, // planta singonio
  { id: "potted_plant_04", simplify: 0.3 }, // suculenta
];

/** Texturas: [id, mapas]. Diffuse = color, nor_gl = relieve, Rough = rugosidad. */
const TEXTURES = [
  ["laminate_floor_02", ["Diffuse", "nor_gl", "Rough"]], // piso de madera
  ["floor_tiles_06", ["Diffuse", "nor_gl", "Rough"]], // piso ceramico
  ["marble_01", ["Diffuse", "nor_gl", "Rough"]], // piso de marmol
  ["brushed_concrete_2", ["Diffuse", "nor_gl", "Rough"]], // concreto pulido
  ["white_plaster_02", ["nor_gl", "Rough"]], // muros (el color lo pone la escena)
  ["cotton_jersey", ["nor_gl", "Rough"]], // telas (el color lo pone el configurador)
  ["oak_veneer_01", ["Diffuse", "nor_gl", "Rough"]], // madera de muebles
];

const HDRIS = ["brown_photostudio_02"];

async function json(url) {
  const res = await fetch(url, { headers: UA });
  if (!res.ok) throw new Error(`${res.status} ${url}`);
  return res.json();
}

async function download(url, file, tries = 6) {
  mkdirSync(dirname(file), { recursive: true });
  for (let i = 1; i <= tries; i++) {
    try {
      const res = await fetch(url, { headers: UA });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      writeFileSync(file, Buffer.from(await res.arrayBuffer()));
      return;
    } catch (e) {
      if (i === tries) throw new Error(`No se pudo bajar ${url}: ${e.message}`);
      await new Promise((r) => setTimeout(r, 1000 * i));
    }
  }
}

function gltfTransform(args) {
  const bin = join(root, "node_modules", ".bin", process.platform === "win32" ? "gltf-transform.cmd" : "gltf-transform");
  execFileSync(bin, args, { stdio: "inherit", shell: process.platform === "win32" });
}

async function model({ id, simplify }) {
  const target = join(out, "models", `${id}.glb`);
  if (existsSync(target) && !force) return console.log(`= ${id}.glb ya existe`);
  const files = await json(`https://api.polyhaven.com/files/${id}`);
  const gltf = files.gltf["1k"].gltf;
  const dir = join(cache, id);
  rmSync(dir, { recursive: true, force: true });
  await download(gltf.url, join(dir, `${id}.gltf`));
  for (const [path, file] of Object.entries(gltf.include ?? {})) await download(file.url, join(dir, path));
  mkdirSync(dirname(target), { recursive: true });
  gltfTransform([
    "optimize", join(dir, `${id}.gltf`), target,
    "--compress", "false",
    ...(simplify ? ["--simplify", "true", "--simplify-ratio", String(simplify), "--simplify-error", "0.002"] : ["--simplify", "false"]),
    "--texture-compress", "webp", "--texture-size", "1024",
  ]);
  console.log(`+ ${id}.glb`);
}

async function texture(id, maps) {
  const files = await json(`https://api.polyhaven.com/files/${id}`);
  for (const map of maps) {
    const target = join(out, "textures", `${id}_${map.toLowerCase()}.jpg`);
    if (existsSync(target) && !force) continue;
    await download(files[map]["1k"].jpg.url, target);
    console.log(`+ ${id}_${map.toLowerCase()}.jpg`);
  }
}

async function hdri(id) {
  const target = join(out, "hdri", `${id}_1k.hdr`);
  if (existsSync(target) && !force) return console.log(`= ${id}_1k.hdr ya existe`);
  const files = await json(`https://api.polyhaven.com/files/${id}`);
  await download(files.hdri["1k"].hdr.url, target);
  console.log(`+ ${id}_1k.hdr`);
}

for (const m of MODELS) await model(m);
for (const [id, maps] of TEXTURES) await texture(id, maps);
for (const id of HDRIS) await hdri(id);
rmSync(cache, { recursive: true, force: true });
console.log("Listo.");
