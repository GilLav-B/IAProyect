# Arranca n8n en http://localhost:5678 para el proyecto.
# Uso (desde la carpeta del proyecto):  powershell -ExecutionPolicy Bypass -File n8n\start-n8n.ps1
#
# n8n solo ORQUESTA: recibe los trabajos de la API, llama al servicio de IA en Python (ai-service) y regresa el
# resultado. La key de OpenAI la usa ai-service; n8n no la necesita.
# - Lee N8N_CALLBACK_SECRET y AI_SERVICE_URL del .env de la raiz y los pasa SOLO como variables de entorno del proceso.
#   Los flujos los usan con {{ $env.N8N_CALLBACK_SECRET }} etc.; nunca se escriben en los JSON del repo.
# - Los datos de n8n (cuenta, flujos importados, ejecuciones) quedan en %USERPROFILE%\.n8n, fuera del repo.

$root = Split-Path -Parent $PSScriptRoot
$envFile = Join-Path $root ".env"
if (-not (Test-Path $envFile)) { throw "No existe $envFile. Copia .env.example como .env." }

foreach ($line in Get-Content $envFile) {
    if ($line -match '^\s*(N8N_CALLBACK_SECRET|AI_SERVICE_URL)\s*=\s*(.+?)\s*$') {
        Set-Item -Path "env:$($Matches[1])" -Value $Matches[2].Trim('"')
    }
}
if (-not $env:N8N_CALLBACK_SECRET) { throw "Falta N8N_CALLBACK_SECRET en el .env." }
if (-not $env:AI_SERVICE_URL) { $env:AI_SERVICE_URL = "http://127.0.0.1:8100" }

$env:N8N_PORT = "5678"
$env:N8N_HOST = "localhost"
$env:N8N_PROTOCOL = "http"
$env:WEBHOOK_URL = "http://localhost:5678/"
$env:N8N_SECURE_COOKIE = "false"            # solo en local (http)
$env:N8N_DIAGNOSTICS_ENABLED = "false"
$env:N8N_PERSONALIZATION_ENABLED = "false"
$env:N8N_BLOCK_ENV_ACCESS_IN_NODE = "false" # permite {{ $env.N8N_CALLBACK_SECRET }} en los nodos
$env:GENERIC_TIMEZONE = "America/Tijuana"

$env:Path = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User')
n8n start
