"""Genera los flujos de n8n (n8n/workflows/*.json). n8n solo ORQUESTA: recibe el trabajo de la API,
avisa el avance, llama al servicio de IA en Python (ai-service) y regresa el resultado a la API.
Las llamadas a OpenAI y a Blender las hace ai-service; n8n no necesita la key de OpenAI.

Uso:  python n8n/build-workflows.py   (despues importar y publicar, ver README)
"""

import json
from pathlib import Path

OUT = Path(__file__).parent / "workflows"
SECRET_HEADER = {"parameters": [{"name": "X-Callback-Secret", "value": "={{ $env.N8N_CALLBACK_SECRET }}"}]}
AI = "={{ ($env.AI_SERVICE_URL || 'http://127.0.0.1:8100') }}"


def node(uid: str, name: str, ntype: str, version, pos, params: dict, **extra) -> dict:
    return {"id": uid, "name": name, "type": ntype, "typeVersion": version, "position": pos, "parameters": params, **extra}


def workflow(wid: str, name: str, path: str, webhook_id: str, start_msg: str, service_path: str, fields: list[str],
             timeout_ms: int, notes: str) -> dict:
    u = lambda n: f"{webhook_id[:-2]}{n:02d}"  # noqa: E731
    body = "{ " + ", ".join(f"{f}: b.{f}" for f in fields) + " }"
    nodes = [
        node(u(1), "Recibir trabajo", "n8n-nodes-base.webhook", 2, [0, 0],
             {"httpMethod": "POST", "path": path, "responseMode": "onReceived", "options": {"responseCode": 202}},
             webhookId=webhook_id, notes=notes),
        node(u(2), "Avisar inicio", "n8n-nodes-base.httpRequest", 4.2, [240, 0],
             {"method": "POST", "url": "={{ $json.body.callbackUrl }}/progress", "sendHeaders": True, "headerParameters": SECRET_HEADER,
              "sendBody": True, "specifyBody": "json",
              "jsonBody": "={{ JSON.stringify({ progress: 15, message: " + json.dumps(start_msg) + " }) }}",
              "options": {"timeout": 10000}},
             onError="continueRegularOutput"),
        node(u(3), "Servicio de IA (Python)", "n8n-nodes-base.httpRequest", 4.2, [480, 0],
             {"method": "POST", "url": AI[:-3] + f" + '{service_path}' }}}}",
              "sendBody": True, "specifyBody": "json",
              "jsonBody": "={{ (() => { const b = $('Recibir trabajo').item.json.body; return JSON.stringify(" + body + "); })() }}",
              "options": {"timeout": timeout_ms}},
             onError="continueErrorOutput",
             notes="ai-service hace la IA (prompts con /ai-rules, OpenAI, Blender) y regresa { ok, data, error }."),
        node(u(4), "Servicio no respondio", "n8n-nodes-base.code", 2, [720, 120],
             {"jsCode": "// Why Code: mensaje corto para el usuario cuando ai-service no responde o truena.\n"
                        "const e = $json.error ?? {};\n"
                        "const detail = e.description || e.message || 'sin respuesta';\n"
                        "return [{ json: { ok: false, error: `El servicio de IA (Python) fallo: ${String(detail).slice(0, 300)}` } }];"}),
        node(u(5), "Regresar resultado", "n8n-nodes-base.httpRequest", 4.2, [960, 0],
             {"method": "POST", "url": "={{ $('Recibir trabajo').item.json.body.callbackUrl }}/result", "sendHeaders": True,
              "headerParameters": SECRET_HEADER, "sendBody": True, "specifyBody": "json",
              "jsonBody": "={{ JSON.stringify({ ok: $json.ok, data: $json.data ?? null, error: $json.error ?? null }) }}",
              "options": {"timeout": 60000}},
             retryOnFail=True, maxTries=3, waitBetweenTries=2000,
             notes="La API valida con el motor todo lo que propuso la IA antes de aplicarlo."),
    ]
    connections = {
        "Recibir trabajo": {"main": [[{"node": "Avisar inicio", "type": "main", "index": 0}]]},
        "Avisar inicio": {"main": [[{"node": "Servicio de IA (Python)", "type": "main", "index": 0}]]},
        "Servicio de IA (Python)": {"main": [[{"node": "Regresar resultado", "type": "main", "index": 0}],
                                             [{"node": "Servicio no respondio", "type": "main", "index": 0}]]},
        "Servicio no respondio": {"main": [[{"node": "Regresar resultado", "type": "main", "index": 0}]]},
    }
    return {"id": wid, "name": name, "active": False,
            "settings": {"executionOrder": "v1", "saveDataSuccessExecution": "none", "saveDataErrorExecution": "all"},
            "nodes": nodes, "connections": connections}


FLOWS = {
    "iaproyect-propuestas.json": workflow(
        "IAProyPropuesta1", "IAProyect - Propuestas de diseno (referencia, chat, disenar)", "iaproyect-propuestas",
        "6d0e3a52-7b1c-4f0a-9f3e-1a2b3c4d5e01", "La IA esta pensando la propuesta", "/ai/proposals",
        ["kind", "context", "message", "note", "style", "imageDataUrl"], 240000,
        "Lo llama la API (.NET) con: jobId, callbackUrl, kind (referencia|chat|diseno), context y opcionales message, note, "
        "style, imageDataUrl. Responde 202 de inmediato; el resultado se regresa por callbackUrl."),
    "iaproyect-imagen.json": workflow(
        "IAProyImagen0001", "IAProyect - Imagenes (render realista y como quedaria)", "iaproyect-imagen",
        "9a4b6c1d-2e3f-4a5b-8c7d-1a2b3c4d5e02", "Generando la imagen con IA (alrededor de un minuto)", "/ai/image",
        ["mode", "state", "products", "visible", "note", "imageDataUrl"], 360000,
        "Lo llama la API (.NET) con: jobId, callbackUrl, mode (render|libre), state, products y opcionales visible, note, "
        "imageDataUrl. La imagen es solo ayuda visual, no exacta."),
    "iaproyect-blender.json": workflow(
        "IAProyBlender001", "IAProyect - Render exacto en Blender", "iaproyect-blender",
        "4c2e8a10-5d3b-4e7f-9a1c-1a2b3c4d5e03", "Preparando Blender", "/blender/render",
        ["jobId", "callbackUrl", "state", "products", "materials", "views", "samples", "width", "height"], 3600000,
        "Lo llama la API (.NET). ai-service corre blender/build_scene.py (DesignState -> .blend + PNG) y reporta el avance "
        "por vista directo a la API. En CPU tarda minutos."),
}

if __name__ == "__main__":
    OUT.mkdir(exist_ok=True)
    for file, wf in FLOWS.items():
        (OUT / file).write_text(json.dumps(wf, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print("escrito", file)
