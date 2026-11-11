"""Endpoints del servicio. OpenAI y Blender se simulan: estas pruebas no gastan credito ni tardan minutos."""

import io

import pytest
from fastapi.testclient import TestClient
from openpyxl import load_workbook

from app import main
from app.openai_client import AiError

client = TestClient(main.app)


def test_health():
    r = client.get("/health")
    assert r.status_code == 200
    assert r.json()["rules"] is True


def test_propuestas_regresa_lo_que_dice_la_ia(monkeypatch):
    seen = {}

    def fake_chat(settings, system, user, schema, image_data_url=None):
        seen.update(system=system, user=user, image=image_data_url)
        return {"summary": "ok", "style": "moderno", "proposals": []}

    monkeypatch.setattr(main, "chat_json", fake_chat)
    r = client.post("/ai/proposals", json={"kind": "chat", "context": {"objects": []}, "message": "agrega una planta"})
    assert r.json() == {"ok": True, "data": {"summary": "ok", "style": "moderno", "proposals": []}, "error": None}
    assert "agrega una planta" in seen["user"]
    assert seen["image"] is None


def test_referencia_sin_imagen_falla():
    r = client.post("/ai/proposals", json={"kind": "referencia", "context": {}})
    assert r.json()["ok"] is False


def test_error_de_openai_se_regresa_como_fallo(monkeypatch):
    def boom(*a, **k):
        raise AiError("OpenAI respondio 429: limite")

    monkeypatch.setattr(main, "chat_json", boom)
    r = client.post("/ai/proposals", json={"kind": "chat", "context": {}, "message": "x"})
    assert r.json() == {"ok": False, "data": None, "error": "OpenAI respondio 429: limite"}


def test_render_usa_la_captura_y_solo_lo_visible(monkeypatch, d1, products):
    seen = {}

    def fake_image(settings, prompt, image_data_url=None):
        seen.update(prompt=prompt, image=image_data_url)
        return "QUJD"

    monkeypatch.setattr(main, "image", fake_image)
    r = client.post("/ai/image", json={
        "mode": "render", "state": d1, "products": list(products.values()), "imageDataUrl": "data:image/jpeg;base64,AAA",
        "visible": [{"instanceId": "sofa_d1", "where": "a la izquierda, cerca de la camara"}],
    })
    assert r.json()["data"] == {"b64": "QUJD"}
    assert seen["image"] == "data:image/jpeg;base64,AAA"
    assert "EXACTAMENTE 1 muebles" in seen["prompt"]


def test_imagen_libre_no_manda_captura(monkeypatch, d1, products):
    seen = {}
    monkeypatch.setattr(main, "image", lambda s, prompt, image_data_url=None: seen.update(image=image_data_url) or "QUJD")
    r = client.post("/ai/image", json={"mode": "libre", "state": d1, "products": list(products.values())})
    assert r.json()["ok"] is True
    assert seen["image"] is None


def test_blender_vista_invalida(d1, products):
    r = client.post("/blender/render", json={"jobId": "job_x", "state": d1, "products": list(products.values()), "views": "arriba"})
    assert r.json()["ok"] is False
    assert "Vista invalida" in r.json()["error"]


@pytest.mark.parametrize("fmt,start", [("pdf", b"%PDF"), ("plan.pdf", b"%PDF"), ("plan.svg", b"<svg"), ("xlsx", b"PK")])
def test_exportaciones(export_data, fmt, start):
    r = client.post(f"/export/{fmt}", json=export_data)
    assert r.status_code == 200
    assert r.content.startswith(start)


def test_excel_tiene_las_hojas_y_el_desperdicio(export_data):
    wb = load_workbook(io.BytesIO(client.post("/export/xlsx", json=export_data).content))
    assert wb.sheetnames == ["Compras", "Materiales", "Presupuesto", "Muebles"]
    assert wb["Materiales"]["E1"].value == "m2 con 10% desperdicio"
    assert wb["Materiales"]["E2"].value == 44


def test_csv_con_bom_y_filas(export_data):
    text = client.post("/export/csv", json=export_data).content.decode("utf-8")
    assert text.startswith("﻿tipo,rubro")
    assert "material,materiales,Pintura vinilica blanca,pintura_001,44,m2" in text


def test_plano_svg_dibuja_cada_mueble(export_data):
    svg = client.post("/export/plan.svg", json=export_data).text
    for o in export_data["objects"]:
        if o["category"] not in ("televisor", "cuadro"):  # van encima de otro mueble o en el muro: sin etiqueta
            assert o["name"] in svg
    assert svg.count("<polygon") >= len(export_data["objects"]) + 5  # piso + 4 muros + cada mueble


def test_formato_desconocido(export_data):
    assert client.post("/export/docx", json=export_data).status_code == 404
