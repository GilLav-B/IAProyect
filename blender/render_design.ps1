# Fase 6: genera el .blend y el render de un diseno con Blender (Cycles).
# Uso (desde la carpeta del proyecto, con la API corriendo):
#   powershell -ExecutionPolicy Bypass -File blender\render_design.ps1 -DesignId d1
#   powershell -ExecutionPolicy Bypass -File blender\render_design.ps1 -DesignId d1 -Views all -Samples 128
#
# - Baja el DesignState validado y el catalogo de la API y se los pasa a blender\build_scene.py.
# - Resultado en storage\blender\<DesignId>\ : <id>.blend, <id>_<vista>.png y resultado.json.
# - Ruta de Blender: variable BLENDER_PATH del .env (si no esta, busca la instalacion estandar).

param(
    [Parameter(Mandatory = $true)][string]$DesignId,
    [string]$Views = "se",          # se, so, ne, no o all
    [int]$Samples = 96,
    [int]$Width = 1600,
    [int]$Height = 1000,
    [switch]$NoRender,
    [string]$ApiUrl = "http://localhost:5000"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

$blender = $null
$envFile = Join-Path $root ".env"
if (Test-Path $envFile) {
    foreach ($line in Get-Content $envFile) {
        if ($line -match '^\s*BLENDER_PATH\s*=\s*(.+?)\s*$') { $blender = $Matches[1].Trim('"') }
    }
}
if (-not $blender) {
    $blender = Get-ChildItem "C:\Program Files\Blender Foundation" -Recurse -Filter blender.exe -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $blender -or -not (Test-Path $blender)) { throw "No encontre Blender. Pon BLENDER_PATH en el .env (ruta a blender.exe)." }

$out = Join-Path $root "storage\blender\$DesignId"
$inputDir = Join-Path $out "entrada"
New-Item -ItemType Directory -Force $inputDir | Out-Null

# Datos validados desde la API (el motor ya reviso el diseno)
$design = Join-Path $inputDir "design.json"
$catalog = Join-Path $inputDir "catalog.json"
$materials = Join-Path $inputDir "materials.json"
Invoke-WebRequest "$ApiUrl/api/designs/$DesignId" -OutFile $design -UseBasicParsing
Invoke-WebRequest "$ApiUrl/api/catalog/products" -OutFile $catalog -UseBasicParsing
Invoke-WebRequest "$ApiUrl/api/catalog/materials" -OutFile $materials -UseBasicParsing

$blenderArgs = @(
    "-b", "--factory-startup", "-P", (Join-Path $PSScriptRoot "build_scene.py"), "--",
    "--design", $design, "--catalog", $catalog, "--materials", $materials,
    "--assets", (Join-Path $root "frontend\public\assets"),
    "--colors", (Join-Path $root "frontend\src\lib\units.ts"),
    "--out", $out, "--views", $Views, "--samples", $Samples, "--width", $Width, "--height", $Height
)
if ($NoRender) { $blenderArgs += "--no-render" }

Write-Host "Blender: $blender"
& $blender @blenderArgs
if ($LASTEXITCODE -ne 0) { throw "Blender termino con error ($LASTEXITCODE)." }
Write-Host "Listo: $out"
