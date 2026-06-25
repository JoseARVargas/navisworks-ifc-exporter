#Requires -Version 5.1
<#
.SYNOPSIS
    Gera os assets visuais do wizard do Inno Setup usando System.Drawing.
    Produz wizard_banner.bmp (164x314) e wizard_small.bmp (55x55).
.PARAMETER AssetsDir
    Pasta onde os arquivos serão salvos.
#>
param([string]$AssetsDir = "$PSScriptRoot\assets")

Add-Type -AssemblyName System.Drawing

New-Item -ItemType Directory -Force -Path $AssetsDir | Out-Null

function Save-Bmp {
    param($bitmap, $path)
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Bmp)
    $bitmap.Dispose()
    Write-Host "  Gerado: $path"
}

# ── wizard_banner.bmp (164 × 314, painel esquerdo do wizard) ─────────────────
$bmp = New-Object System.Drawing.Bitmap(164, 314)
$g   = [System.Drawing.Graphics]::FromImage($bmp)

# Gradiente manual (System.Drawing.Drawing2D disponível no .NET 4.8)
$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    [System.Drawing.Point]::new(0, 0),
    [System.Drawing.Point]::new(0, 314),
    [System.Drawing.Color]::FromArgb(30, 60, 120),   # azul escuro PHD
    [System.Drawing.Color]::FromArgb(10, 30, 70)
)
$g.FillRectangle($brush, 0, 0, 164, 314)
$brush.Dispose()

# Faixa laranja no topo
$orangeBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(220, 100, 20))
$g.FillRectangle($orangeBrush, 0, 0, 164, 6)
$orangeBrush.Dispose()

# Texto "PHD" centralizado
$font  = New-Object System.Drawing.Font("Segoe UI", 28, [System.Drawing.FontStyle]::Bold)
$brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$sf    = New-Object System.Drawing.StringFormat
$sf.Alignment = [System.Drawing.StringAlignment]::Center
$g.DrawString("PHD", $font, $brush, [System.Drawing.RectangleF]::new(0, 80, 164, 60), $sf)
$font.Dispose()

# Texto "Eng. Digital" menor
$font2 = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Regular)
$g.DrawString("Eng. Digital", $font2, $brush, [System.Drawing.RectangleF]::new(0, 138, 164, 30), $sf)
$font2.Dispose()

# Linha de versão no rodapé
$fontSmall = New-Object System.Drawing.Font("Segoe UI", 7)
$grayBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(160, 180, 210))
$g.DrawString("Navisworks Plugin", $fontSmall, $grayBrush, [System.Drawing.RectangleF]::new(0, 290, 164, 20), $sf)
$fontSmall.Dispose(); $grayBrush.Dispose(); $brush.Dispose()

$g.Dispose()
Save-Bmp $bmp (Join-Path $AssetsDir "wizard_banner.bmp")

# ── wizard_small.bmp (55 × 55, canto superior direito) ───────────────────────
$bmp2 = New-Object System.Drawing.Bitmap(55, 55)
$g2   = [System.Drawing.Graphics]::FromImage($bmp2)
$bg   = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(30, 60, 120))
$g2.FillRectangle($bg, 0, 0, 55, 55)
$bg.Dispose()
$font3  = New-Object System.Drawing.Font("Segoe UI", 14, [System.Drawing.FontStyle]::Bold)
$white  = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$sf2    = New-Object System.Drawing.StringFormat
$sf2.Alignment = [System.Drawing.StringAlignment]::Center
$sf2.LineAlignment = [System.Drawing.StringAlignment]::Center
$g2.DrawString("PHD", $font3, $white, [System.Drawing.RectangleF]::new(0, 0, 55, 55), $sf2)
$font3.Dispose(); $white.Dispose()
$g2.Dispose()
Save-Bmp $bmp2 (Join-Path $AssetsDir "wizard_small.bmp")

Write-Host "Assets gerados em: $AssetsDir"
