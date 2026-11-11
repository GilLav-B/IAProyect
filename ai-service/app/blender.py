"""Render exacto con Blender (Fase 6): DesignState -> blender/build_scene.py -> .blend + PNG.

La IA no construye el .blend: la geometria sale del DesignState validado. Aqui solo se prepara la entrada,
se corre Blender en segundo plano y se reporta el avance a la API (una vista a la vez).
"""

import json
import re
import subprocess
import time
from pathlib import Path

import httpx

from .config import Settings

VIEW_ORDER = ["so", "se", "ne", "no"]


class BlenderError(Exception):
    pass


def views_of(views: str) -> list[str]:
    names = VIEW_ORDER if views == "all" else [v.strip() for v in views.split(",") if v.strip()]
    bad = [v for v in names if v not in VIEW_ORDER]
    if bad or not names:
        raise BlenderError(f"Vista invalida: {', '.join(bad) or '(vacia)'}. Usa so, se, ne, no o all.")
    return names


def command(settings: Settings, entrada: Path, out: Path, views: str, samples: int, width: int, height: int) -> list[str]:
    if not settings.blender_path or not Path(settings.blender_path).exists():
        raise BlenderError("No encontre Blender. Pon BLENDER_PATH en el .env (ruta a blender.exe).")
    return [
        settings.blender_path, "-b", "--factory-startup", "-P", str(settings.blender_script), "--",
        "--design", str(entrada / "design.json"), "--catalog", str(entrada / "catalog.json"),
        "--materials", str(entrada / "materials.json"),
        "--assets", str(settings.assets_dir), "--colors", str(settings.colors_file),
        "--out", str(out), "--views", views, "--samples", str(samples), "--width", str(width), "--height", str(height),
    ]


def _progress(settings: Settings, callback_url: str | None, progress: int, message: str) -> None:
    if not callback_url or not settings.callback_secret:
        return
    try:
        httpx.post(f"{callback_url}/progress", json={"progress": progress, "message": message},
                   headers={"X-Callback-Secret": settings.callback_secret}, timeout=10)
    except httpx.HTTPError:
        pass  # el avance es informativo: si falla, el render sigue


def render(settings: Settings, job_id: str, state: dict, products: list[dict], materials: list[dict],
           views: str = "se", samples: int = 64, width: int = 1280, height: int = 800,
           callback_url: str | None = None, timeout_s: int = 3600) -> dict:
    if not re.fullmatch(r"[A-Za-z0-9_\-]{1,40}", job_id):
        raise BlenderError("jobId invalido.")
    names = views_of(views)
    out = settings.storage_dir / "blender" / job_id
    entrada = out / "entrada"
    entrada.mkdir(parents=True, exist_ok=True)
    (entrada / "design.json").write_text(json.dumps(state, ensure_ascii=False), encoding="utf-8")
    (entrada / "catalog.json").write_text(json.dumps(products, ensure_ascii=False), encoding="utf-8")
    (entrada / "materials.json").write_text(json.dumps(materials, ensure_ascii=False), encoding="utf-8")

    cmd = command(settings, entrada, out, ",".join(names), samples, width, height)
    _progress(settings, callback_url, 20, "Armando la escena en Blender")
    started = time.time()
    done = 0
    log: list[str] = []
    with subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding="utf-8", errors="replace") as proc:
        assert proc.stdout is not None
        for line in proc.stdout:
            log.append(line.rstrip())
            if line.startswith("[ok] .blend guardado"):
                _progress(settings, callback_url, 30, f"Escena lista; renderizando {len(names)} vista(s) con Cycles")
            elif line.startswith("[ok] render"):
                done += 1
                _progress(settings, callback_url, 30 + int(65 * done / len(names)), f"Vista {done} de {len(names)} lista")
            if time.time() - started > timeout_s:
                proc.kill()
                raise BlenderError("Blender tardo demasiado y se detuvo.")
        code = proc.wait()
    if code != 0:
        tail = "\n".join(line for line in log[-15:] if line)
        raise BlenderError(f"Blender termino con error ({code}). Ultimas lineas:\n{tail}")

    result = json.loads((out / "resultado.json").read_text(encoding="utf-8"))
    rel = lambda p: Path(p).resolve().relative_to(settings.storage_dir).as_posix()  # noqa: E731
    return {
        "blend": rel(result["blend"]),
        "images": [{"view": r["view"], "path": rel(r["file"]), "seconds": r["seconds"]} for r in result["renders"]],
        "seconds": round(time.time() - started, 1),
    }
