# Importa y publica los flujos de n8n del proyecto (n8n\workflows\*.json).
# Uso (con n8n DETENIDO; publicar con n8n corriendo no siempre se aplica):
#   powershell -ExecutionPolicy Bypass -File n8n\publish-workflows.ps1
#
# Se publica uno por uno y con limite de tiempo: a veces "n8n publish:workflow" se queda colgado.

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$env:Path = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User')

if (Get-NetTCPConnection -LocalPort 5678 -State Listen -ErrorAction SilentlyContinue) {
    throw "n8n esta corriendo en el puerto 5678. Detenlo primero (Ctrl+C en su ventana) y vuelve a correr este script."
}

Write-Host "Importando flujos..."
n8n import:workflow --separate --input="$root\n8n\workflows"

$ids = "IAProyPropuesta1", "IAProyImagen0001", "IAProyBlender001"
foreach ($id in $ids) {
    Write-Host "Publicando $id..."
    $log = Join-Path $env:TEMP "n8n_publish_$id.log"
    $p = Start-Process -FilePath "cmd.exe" -ArgumentList "/c n8n publish:workflow --id=$id > `"$log`" 2>&1" -PassThru -WindowStyle Hidden
    if (-not $p.WaitForExit(150000)) {
        $p.Kill()
        Write-Warning "$id tardo demasiado; vuelve a correr el script."
    }
}
Write-Host "Listo. Arranca n8n con n8n\start-n8n.ps1 (los webhooks tardan unos segundos en registrarse)."
