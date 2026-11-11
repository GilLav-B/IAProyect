# Arranca el servicio de IA en Python en http://127.0.0.1:8100 (solo local).
# Uso (desde la carpeta del proyecto):  powershell -ExecutionPolicy Bypass -File ai-service\start-ai-service.ps1
#
# - Lee OPENAI_API_KEY, OPENAI_MODEL, OPENAI_IMAGE_MODEL, OPENAI_IMAGE_QUALITY, N8N_CALLBACK_SECRET y BLENDER_PATH del .env.
# - Si no existe el entorno virtual, lo crea e instala ai-service\requirements.txt.

$ErrorActionPreference = "Stop"
$service = $PSScriptRoot
$venvPython = Join-Path $service ".venv\Scripts\python.exe"

if (-not (Test-Path $venvPython)) {
    $python = (Get-Command python -ErrorAction SilentlyContinue).Source
    if (-not $python -or $python -like "*WindowsApps*" -or $python -like "*system32*") {
        $python = Get-ChildItem "$env:LOCALAPPDATA\Python" -Recurse -Filter python.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -notlike "*\.venv\*" } | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
    }
    if (-not $python) { throw "No encontre Python 3. Instalalo desde python.org." }
    & $python -m venv (Join-Path $service ".venv")
    & $venvPython -m pip install -r (Join-Path $service "requirements.txt")
}

& $venvPython -m uvicorn app.main:app --app-dir $service --host 127.0.0.1 --port 8100
