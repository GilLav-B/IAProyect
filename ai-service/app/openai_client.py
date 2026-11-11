"""Llamadas a OpenAI. La key sale de la configuracion (.env) y nunca se regresa en mensajes de error."""

import json

import httpx

from .config import Settings

API = "https://api.openai.com/v1"


class AiError(Exception):
    """Error que se le puede mostrar al usuario (sin datos sensibles)."""


def _headers(settings: Settings) -> dict[str, str]:
    if not settings.openai_api_key:
        raise AiError("Falta OPENAI_API_KEY en el .env del proyecto.")
    return {"Authorization": f"Bearer {settings.openai_api_key}"}


def _error_text(res: httpx.Response) -> str:
    # En un 401 OpenAI repite parte de la key: no se reenvia su mensaje
    if res.status_code == 401:
        return "OpenAI rechazo la API key (401). Revisa OPENAI_API_KEY en el .env del proyecto."
    try:
        detail = res.json().get("error", {}).get("message") or res.text
    except ValueError:
        detail = res.text
    return f"OpenAI respondio {res.status_code}: {str(detail)[:300]}"


def chat_json(settings: Settings, system: str, user: str, schema: dict, image_data_url: str | None = None, timeout: float = 180) -> dict:
    """Chat Completions con Structured Outputs: la respuesta es JSON valido conforme al esquema."""
    content = [{"type": "text", "text": user},
               {"type": "image_url", "image_url": {"url": image_data_url, "detail": "high"}}] if image_data_url else user
    body = {
        "model": settings.openai_model,
        "messages": [{"role": "system", "content": system}, {"role": "user", "content": content}],
        "response_format": {"type": "json_schema", "json_schema": {"name": "propuestas", "strict": True, "schema": schema}},
    }
    with httpx.Client(timeout=timeout) as client:
        res = client.post(f"{API}/chat/completions", headers=_headers(settings), json=body)
    if res.status_code >= 400:
        raise AiError(_error_text(res))
    message = (res.json().get("choices") or [{}])[0].get("message") or {}
    if message.get("refusal"):
        raise AiError(f"La IA se nego: {message['refusal']}")
    try:
        return json.loads(message.get("content") or "")
    except json.JSONDecodeError as exc:
        raise AiError("La IA no regreso JSON valido.") from exc


def image(settings: Settings, prompt: str, image_data_url: str | None = None, timeout: float = 300) -> str:
    """Genera (o edita, si hay captura) una imagen y regresa el PNG en base64."""
    body = {"model": settings.openai_image_model, "prompt": prompt, "size": "1536x1024", "n": 1,
            "quality": settings.openai_image_quality}
    url = f"{API}/images/generations"
    if image_data_url:
        body["images"] = [{"image_url": image_data_url}]
        url = f"{API}/images/edits"
    with httpx.Client(timeout=timeout) as client:
        res = client.post(url, headers=_headers(settings), json=body)
    if res.status_code >= 400:
        raise AiError(_error_text(res))
    b64 = ((res.json().get("data") or [{}])[0]).get("b64_json")
    if not b64:
        raise AiError("OpenAI no regreso imagen.")
    return b64
