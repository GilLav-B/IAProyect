# Arranca todo el proyecto, cada servicio en su propia ventana de PowerShell:
#   API (.NET) :5000 · Web (Next.js) :3000 · Servicio de IA (Python) :8100 · n8n :5678
#
# Uso (desde la carpeta del proyecto):
#   powershell -ExecutionPolicy Bypass -File scripts\start-all.ps1          # web en modo desarrollo
#   powershell -ExecutionPolicy Bypass -File scripts\start-all.ps1 -Prod    # web compilada (mas rapida)
#
# Para detener: cierra las ventanas (o Ctrl+C en cada una). PostgreSQL debe estar corriendo como servicio de Windows.

param([switch]$Prod)

$root = Split-Path -Parent $PSScriptRoot

function Start-Window($title, $command) {
    Start-Process powershell -ArgumentList "-NoExit", "-ExecutionPolicy", "Bypass", "-Command",
        "`$host.UI.RawUI.WindowTitle = '$title'; Set-Location '$root'; $command"
}

Start-Window "IAProyect - API" "dotnet run --project backend/src/InteriorDesign.Api --launch-profile http"
Start-Window "IAProyect - IA (Python)" "& '$root\ai-service\start-ai-service.ps1'"
Start-Window "IAProyect - n8n" "& '$root\n8n\start-n8n.ps1'"
if ($Prod) {
    Start-Window "IAProyect - Web" "npm --prefix frontend run build; if (`$?) { npm --prefix frontend run start }"
} else {
    Start-Window "IAProyect - Web" "npm --prefix frontend run dev"
}

Write-Host "Arrancando... en ~30 s abre http://localhost:3000"
Write-Host "API (Swagger): http://localhost:5000/swagger · IA: http://127.0.0.1:8100/health · n8n: http://localhost:5678"
