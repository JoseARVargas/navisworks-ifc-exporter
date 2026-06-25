#Requires -Version 5.1
<#
.SYNOPSIS
    Compila o NavisworksIfcExporter e empacota o instalador .exe via Inno Setup.
.PARAMETER Version
    Versao do installer. Ex: "1.2.0" (sobrescreve o #define AppVersion no .iss)
.EXAMPLE
    .\build_installer.ps1
    .\build_installer.ps1 -Version "1.1.0"
#>
param([string]$Version = "")

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir  = $PSScriptRoot
$projectDir = Split-Path $scriptDir -Parent
$issFile    = Join-Path $scriptDir "PHD_NavisPlugin.iss"
$assetsDir  = Join-Path $scriptDir "assets"
$outputDir  = Join-Path $scriptDir "output"

Write-Host ""
Write-Host "===  PHD Eng. Digital - Build Installer  ===" -ForegroundColor Cyan
Write-Host ""

# 1. dotnet build
Write-Host "-> Building NavisworksIfcExporter (Release)..." -ForegroundColor Yellow
Push-Location $projectDir
try {
    dotnet build NavisworksIfcExporter.csproj -c Release -v minimal
    if ($LASTEXITCODE -ne 0) { throw "dotnet build falhou (exit $LASTEXITCODE)" }
} finally {
    Pop-Location
}
Write-Host "  Build concluido." -ForegroundColor Green

# 2. Assets do wizard
New-Item -ItemType Directory -Force -Path $assetsDir | Out-Null
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$bannerPath = Join-Path $assetsDir "wizard_banner.bmp"
$smallPath  = Join-Path $assetsDir "wizard_small.bmp"
$iconPath   = Join-Path $assetsDir "phd_icon.ico"

if ((-not (Test-Path $bannerPath)) -or (-not (Test-Path $smallPath))) {
    Write-Host "-> Gerando assets visuais do wizard..." -ForegroundColor Yellow
    & "$scriptDir\generate_assets.ps1" -AssetsDir $assetsDir
}

if (-not (Test-Path $iconPath)) {
    Write-Host "  (!) phd_icon.ico nao encontrado - usando icone padrao do Inno Setup." -ForegroundColor DarkYellow
}

# 3. Localizar ISCC.exe
Write-Host "-> Procurando Inno Setup Compiler (ISCC.exe)..." -ForegroundColor Yellow

$isccFromPath = $null
$isccCmd = Get-Command iscc -ErrorAction SilentlyContinue
if ($isccCmd) {
    $isccFromPath = $isccCmd.Source
}

$isccCandidates = @(
    $isccFromPath,
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 7\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 7\ISCC.exe"
)

$iscc = $null
foreach ($candidate in $isccCandidates) {
    if ($candidate -and (Test-Path $candidate)) {
        $iscc = $candidate
        break
    }
}

if (-not $iscc) {
    Write-Host ""
    Write-Host "ERRO: Inno Setup nao encontrado." -ForegroundColor Red
    Write-Host "Baixe gratuitamente em: https://jrsoftware.org/isdl.php" -ForegroundColor Red
    exit 1
}
Write-Host "  Encontrado: $iscc" -ForegroundColor Green

# 4. Compilar installer
Write-Host "-> Compilando installer com Inno Setup..." -ForegroundColor Yellow

$isccArgs = @($issFile)
if ($Version -ne "") {
    $isccArgs += "/DAppVersion=$Version"
    Write-Host "  Versao override: $Version"
}

& $iscc @isccArgs
if ($LASTEXITCODE -ne 0) { throw "ISCC.exe falhou (exit $LASTEXITCODE)" }

# 5. Resultado
$exeFile = Get-ChildItem $outputDir -Filter "*.exe" |
           Sort-Object LastWriteTime -Descending |
           Select-Object -First 1

if ($exeFile) {
    $sizeMb = [math]::Round($exeFile.Length / 1MB, 1)
    $tag = if ($Version -ne "") { $Version } else { "1.0.0" }
    Write-Host ""
    Write-Host "===  INSTALADOR GERADO  ===" -ForegroundColor Green
    Write-Host "  Arquivo : $($exeFile.FullName)" -ForegroundColor White
    Write-Host "  Tamanho : $sizeMb MB" -ForegroundColor White
    Write-Host ""
    Write-Host "Proximos passos para publicar no GitHub:" -ForegroundColor Cyan
    Write-Host "  1. git tag v$tag"
    Write-Host "  2. git push origin --tags"
    Write-Host "  3. gh release create v$tag '$($exeFile.FullName)' --title 'v$tag' --notes-file RELEASE_NOTES.md"
} else {
    Write-Host "AVISO: nenhum .exe encontrado em $outputDir" -ForegroundColor DarkYellow
}
