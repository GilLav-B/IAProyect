"""Servicio de IA en Python (FastAPI).

Flujo: API (C#) crea el trabajo -> n8n lo orquesta -> este servicio hace la IA (prompts, OpenAI, imagenes, Blender)
-> n8n regresa el resultado a la API -> el motor valida antes de aplicar. Las exportaciones las pide la API directo.

Solo escucha en 127.0.0.1 (ver start-ai-service.ps1). Arranque:
  ai-service\\.venv\\Scripts\\python.exe -m uvicorn app.main:app --app-dir ai-service --host 127.0.0.1 --port 8100
"""

from typing import Literal

from fastapi import FastAPI, HTTPException
from fastapi.responses import Response
from pydantic import BaseModel

from . import blender, exports, prompts
from .config import load_settings
from .openai_client import AiError, chat_json, image

settings = load_settings()
app = FastAPI(title="IAProyect - servicio de IA", version="1.0")


class Result(BaseModel):
    """Formato que n8n le reenvia a la API en /api/ai/jobs/{id}/result."""
    ok: bool
    data: dict | None = None
    error: str | None = None


class ProposalsRequest(BaseModel):
    kind: Literal["referencia", "chat", "diseno"]
    context: dict
    message: str | None = None
    note: str | None = None
    style: str | None = None
    imageDataUrl: str | None = None


class VisibleItem(BaseModel):
    instanceId: str
    where: str


class ImageRequest(BaseModel):
    mode: Literal["render", "libre"]
    state: dict
    products: list[dict]
    visible: list[VisibleItem] | None = None
    note: str | None = None
    imageDataUrl: str | None = None


class BlenderRequest(BaseModel):
    jobId: str
    callbackUrl: str | None = None
    state: dict
    products: list[dict]
    materials: list[dict] = []
    views: str = "se"
    samples: int = 64
    width: int = 1280
    height: int = 800


@app.get("/health")
def health() -> dict:
    return {
        "ok": True,
        "openai": bool(settings.openai_api_key),
        "models": {"text": settings.openai_model, "image": settings.openai_image_model, "imageQuality": settings.openai_image_quality},
        "blender": bool(settings.blender_path),
        "rules": settings.rules_dir.exists(),
    }


@app.post("/ai/proposals", response_model=Result)
def proposals(req: ProposalsRequest) -> Result:
    """Propuestas de la IA (copiar referencia, chat o disenar desde cero). La API las valida con el motor."""
    if req.kind == "referencia" and not req.imageDataUrl:
        return Result(ok=False, error="Falta la imagen de referencia.")
    if req.kind == "chat" and not (req.message or "").strip():
        return Result(ok=False, error="Falta la instruccion del chat.")
    try:
        data = chat_json(
            settings,
            prompts.system_prompt(settings.rules_dir, req.kind),
            prompts.user_text(req.kind, req.context, message=req.message, note=req.note, style=req.style),
            prompts.proposal_schema(),
            image_data_url=req.imageDataUrl if req.kind == "referencia" else None,
        )
        return Result(ok=True, data=data)
    except AiError as exc:
        return Result(ok=False, error=str(exc))


@app.post("/ai/image", response_model=Result)
def ai_image(req: ImageRequest) -> Result:
    """Render realista desde la captura del visor (mode=render) o imagen libre del diseno (mode=libre)."""
    products = {p["id"]: p for p in req.products}
    if req.mode == "render":
        if not req.imageDataUrl:
            return Result(ok=False, error="Falta la captura del visor 3D.")
        visible = [v.model_dump() for v in req.visible] if req.visible is not None else None
        prompt = prompts.render_prompt(settings.rules_dir, req.state, products, req.note, visible)
    else:
        prompt = prompts.free_image_prompt(settings.rules_dir, req.state, products, req.note)
    try:
        return Result(ok=True, data={"b64": image(settings, prompt, req.imageDataUrl if req.mode == "render" else None)})
    except AiError as exc:
        return Result(ok=False, error=str(exc))


@app.post("/blender/render", response_model=Result)
def blender_render(req: BlenderRequest) -> Result:
    """Render exacto en Blender (tarda minutos: CPU). Reporta avance a la API si viene callbackUrl."""
    try:
        data = blender.render(settings, req.jobId, req.state, req.products, req.materials, req.views,
                              max(8, min(req.samples, 512)), max(320, min(req.width, 3840)), max(240, min(req.height, 2160)),
                              callback_url=req.callbackUrl)
        return Result(ok=True, data=data)
    except blender.BlenderError as exc:
        return Result(ok=False, error=str(exc))


EXPORTS = {
    "pdf": "application/pdf",
    "xlsx": "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    "csv": "text/csv; charset=utf-8",
    "plan.svg": "image/svg+xml",
    "plan.pdf": "application/pdf",
}


@app.post("/export/{fmt}")
def export(fmt: str, data: dict) -> Response:
    """Reporte PDF, Excel, CSV y plano 2D. Los datos ya vienen calculados por la API (motor)."""
    if fmt not in EXPORTS:
        raise HTTPException(404, f"Formato no soportado: {fmt}")
    title = f"{data['project']['name']} - {data['design']['name']}"
    if fmt == "pdf":
        body = exports.report_pdf(data, settings.storage_dir)
    elif fmt == "xlsx":
        body = exports.report_xlsx(data)
    elif fmt == "csv":
        body = exports.report_csv(data)
    elif fmt == "plan.svg":
        body = exports.plan_svg(data["room"], data["objects"], title).encode("utf-8")
    else:
        body = exports.plan_pdf(data["room"], data["objects"], title)
    return Response(content=body, media_type=EXPORTS[fmt])
