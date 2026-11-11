"""Configuracion: lee el .env de la raiz del repo. El .env tiene PRIORIDAD sobre las variables de Windows: puede haber una
OPENAI_API_KEY vieja en las variables del usuario y la del proyecto es la del .env (paso el 2026-11-11: OpenAI respondio 401).

Las claves (OPENAI_API_KEY, N8N_CALLBACK_SECRET) solo viven en el .env; nunca se escriben en codigo ni en respuestas.
"""

import os
import re
from dataclasses import dataclass
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]


def _read_env_file(path: Path) -> dict[str, str]:
    values: dict[str, str] = {}
    if not path.exists():
        return values
    for line in path.read_text(encoding="utf-8").splitlines():
        m = re.match(r"^\s*([A-Z0-9_]+)\s*=\s*(.*?)\s*$", line)
        if m and not line.lstrip().startswith("#"):
            values[m.group(1)] = m.group(2).strip('"')
    return values


@dataclass(frozen=True)
class Settings:
    openai_api_key: str | None
    openai_model: str
    openai_image_model: str
    openai_image_quality: str
    callback_secret: str | None
    storage_dir: Path
    rules_dir: Path
    assets_dir: Path
    colors_file: Path
    blender_path: str | None
    blender_script: Path


def load_settings() -> Settings:
    env = {**os.environ, **{k: v for k, v in _read_env_file(REPO_ROOT / ".env").items() if v}}
    storage = env.get("STORAGE_PATH") or "./storage"
    storage_dir = Path(storage) if Path(storage).is_absolute() else (REPO_ROOT / storage)
    return Settings(
        openai_api_key=env.get("OPENAI_API_KEY") or None,
        openai_model=env.get("OPENAI_MODEL") or "gpt-5.4-mini",
        openai_image_model=env.get("OPENAI_IMAGE_MODEL") or "gpt-image-2",
        openai_image_quality=env.get("OPENAI_IMAGE_QUALITY") or "high",
        callback_secret=env.get("N8N_CALLBACK_SECRET") or None,
        storage_dir=storage_dir.resolve(),
        rules_dir=REPO_ROOT / "ai-rules",
        assets_dir=REPO_ROOT / "frontend" / "public" / "assets",
        colors_file=REPO_ROOT / "frontend" / "src" / "lib" / "units.ts",
        blender_path=env.get("BLENDER_PATH") or find_blender(),
        blender_script=REPO_ROOT / "blender" / "build_scene.py",
    )


def find_blender() -> str | None:
    """Instalacion estandar de Windows (la version mas nueva)."""
    base = Path(r"C:\Program Files\Blender Foundation")
    if not base.exists():
        return None
    found = sorted(base.glob("*/blender.exe"), reverse=True)
    return str(found[0]) if found else None
