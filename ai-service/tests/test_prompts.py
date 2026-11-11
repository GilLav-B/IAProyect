from app import prompts
from app.config import REPO_ROOT

RULES = REPO_ROOT / "ai-rules"


def test_prompt_de_sistema_lleva_reglas_globales_y_de_seguridad():
    text = prompts.system_prompt(RULES, "chat")
    assert (RULES / "global.md").read_text(encoding="utf-8").strip() in text
    assert (RULES / "safety-rules.md").read_text(encoding="utf-8").strip() in text
    assert (RULES / "interior-designer.md").read_text(encoding="utf-8").strip() in text


def test_esquema_estricto_no_permite_modificar_estructura():
    schema = prompts.proposal_schema()
    actions = schema["properties"]["proposals"]["items"]["properties"]["action"]["enum"]
    assert "modificar_estructura" not in actions
    assert set(actions) == {"agregar", "mover", "reemplazar", "quitar"}
    assert schema["additionalProperties"] is False


def test_disenar_desde_cero_pide_solo_agregar_con_el_estilo():
    text = prompts.user_text("diseno", {"design": {"style": "moderno"}}, style="japandi")
    assert "estilo japandi" in text
    assert "SOLO acciones 'agregar'" in text


def test_render_solo_lleva_lo_visible(d1, products):
    visible = [{"instanceId": "sofa_d1", "where": "al centro, a media distancia"}]
    text = prompts.render_prompt(RULES, d1, products, "de noche", visible)
    assert "EXACTAMENTE 1 muebles" in text
    assert "al centro, a media distancia" in text
    assert products["tv_75"]["name"] not in text
    assert "no agrega ni cambia objetos" in text


def test_render_sin_muebles_visibles_pide_cuarto_vacio(d1, products):
    assert "NO se ve ningun mueble" in prompts.render_prompt(RULES, d1, products, None, [])


def test_imagen_libre_describe_todos_los_muebles(d1, products):
    text = prompts.free_image_prompt(RULES, d1, products, None)
    for o in d1["objects"]:
        assert products[o["catalogId"]]["name"] in text


def test_el_env_del_proyecto_gana_a_las_variables_de_windows(monkeypatch, tmp_path):
    from app import config

    (tmp_path / ".env").write_text("OPENAI_MODEL=del-env\nOPENAI_IMAGE_MODEL=\n", encoding="utf-8")
    monkeypatch.setattr(config, "REPO_ROOT", tmp_path)
    monkeypatch.setenv("OPENAI_MODEL", "de-windows")
    monkeypatch.setenv("OPENAI_IMAGE_MODEL", "imagen-windows")
    s = config.load_settings()
    assert s.openai_model == "del-env"
    assert s.openai_image_model == "imagen-windows"  # vacio en el .env: se usa la de Windows


def test_401_no_reenvia_la_key():
    import httpx

    from app.openai_client import _error_text

    res = httpx.Response(401, json={"error": {"message": "Incorrect API key provided: sk-proj-****abcd"}})
    assert "sk-" not in _error_text(res)
