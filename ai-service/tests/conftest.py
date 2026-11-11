import json
import sys
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "ai-service"))

EXAMPLES = ROOT / "schemas" / "examples"


@pytest.fixture(scope="session")
def catalog() -> dict:
    return json.loads((EXAMPLES / "catalog.json").read_text(encoding="utf-8"))


@pytest.fixture(scope="session")
def products(catalog) -> dict[str, dict]:
    return {p["id"]: p for p in catalog["products"]}


@pytest.fixture()
def d1() -> dict:
    return json.loads((EXAMPLES / "d1.json").read_text(encoding="utf-8"))


@pytest.fixture()
def export_data(d1, products) -> dict:
    """Datos de exportacion como los arma la API (aqui con numeros de ejemplo, no calculados)."""
    objects = []
    for o in d1["objects"]:
        p = products[o["catalogId"]]
        objects.append({
            "instanceId": o["instanceId"], "catalogId": o["catalogId"], "name": p["name"], "category": p["category"], "kind": p["kind"],
            "x": o["position"]["x"], "y": o["position"]["y"], "rotation": o["rotation"],
            "widthCm": p["dimensions"]["width"], "depthCm": p["dimensions"]["depth"], "heightCm": p["dimensions"]["height"],
            "color": (o.get("config") or {}).get("color"), "material": None, "locked": o.get("locked", False),
        })
    return {
        "project": d1["project"], "design": d1["design"], "room": d1["room"], "objects": objects,
        "shopping": [{"rubro": "muebles", "catalogId": "sofa_003", "name": "Sofa", "kind": "real", "quantity": 1,
                      "unitMin": 1000, "unitMax": 2000, "totalMin": 1000, "totalMax": 2000}],
        "materials": [{"materialId": "pintura_001", "name": "Pintura vinilica blanca", "type": "pintura", "m2": 40, "m2WithWaste": 44,
                       "priceM2Min": 60, "priceM2Max": 100, "totalMin": 2640, "totalMax": 4400}],
        "wasteFactor": 0.10,
        "budget": {"total": {"min": 3640, "max": 6400}, "cap": 30000, "byCategory": [{"rubro": "muebles", "min": 1000, "max": 2000}]},
        "report": {"valid": True, "conflicts": [], "warnings": ["presupuesto_puede_excederse"]},
        "images": [],
        "generatedAt": "2026-11-11T10:00",
    }
