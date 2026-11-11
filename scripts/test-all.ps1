# Corre todas las pruebas del proyecto y dice cuales pasaron:
#   1. Backend .NET (motor + API; con PostgreSQL usa una BD de prueba "iaproyect_test"; con SWI-Prolog compara contra diseno.pl)
#   2. Servicio de IA en Python (pytest; OpenAI y Blender simulados, no gasta credito)
#   3. Frontend: tipos (tsc), lint (eslint) y compilacion de produccion (next build)
#
# Uso (desde la carpeta del proyecto, con la API DETENIDA para que no bloquee los .dll):
#   powershell -ExecutionPolicy Bypass -File scripts\test-all.ps1

$root = Split-Path -Parent $PSScriptRoot
$env:Path = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User')
$results = [ordered]@{}

# BD de prueba a partir del .env (sin mostrar la contrasena)
$e = @{}
Get-Content (Join-Path $root ".env") -ErrorAction SilentlyContinue | ForEach-Object {
    if ($_ -match '^\s*([A-Z_]+)\s*=\s*(.*)$') { $e[$Matches[1]] = $Matches[2].Trim('"') }
}
if ($e.POSTGRES_PASSWORD) {
    $env:IAPROYECT_TEST_DB = "Host=$($e.POSTGRES_HOST);Port=$($e.POSTGRES_PORT);Database=iaproyect_test;Username=$($e.POSTGRES_USER);Password=$($e.POSTGRES_PASSWORD)"
}

Write-Host "`n== 1/3 Backend (.NET) ==" -ForegroundColor Cyan
Push-Location (Join-Path $root "backend")
dotnet test
$results["Backend .NET"] = $LASTEXITCODE -eq 0
Pop-Location

Write-Host "`n== 2/3 Servicio de IA (Python) ==" -ForegroundColor Cyan
Push-Location (Join-Path $root "ai-service")
& .\.venv\Scripts\python.exe -m pytest -q
$results["Servicio de IA (Python)"] = $LASTEXITCODE -eq 0
Pop-Location

Write-Host "`n== 3/3 Frontend ==" -ForegroundColor Cyan
Push-Location (Join-Path $root "frontend")
npx tsc --noEmit -p .
$results["Frontend tipos (tsc)"] = $LASTEXITCODE -eq 0
npx eslint src
$results["Frontend lint (eslint)"] = $LASTEXITCODE -eq 0
npx next build
$results["Frontend build"] = $LASTEXITCODE -eq 0
Pop-Location

Write-Host "`n== Resumen ==" -ForegroundColor Cyan
foreach ($k in $results.Keys) {
    if ($results[$k]) { Write-Host "  OK     $k" -ForegroundColor Green } else { Write-Host "  FALLO  $k" -ForegroundColor Red }
}
if ($results.Values -contains $false) { exit 1 }
