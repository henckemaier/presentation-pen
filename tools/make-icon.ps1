# Gera assets\PresentationPen.ico (16-256 px) com o mesmo desenho do ícone da bandeja (IconFactory.cs).
# Uso: powershell -ExecutionPolicy Bypass -File tools\make-icon.ps1
Add-Type -AssemblyName System.Drawing

$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'assets\PresentationPen.ico'
New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null

function New-IconPng([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    $k = $size / 32.0   # o desenho original é em 32x32
    $bg = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 229, 57, 53))
    $g.FillEllipse($bg, 1 * $k, 1 * $k, 30 * $k, 30 * $k)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), ([Math]::Max(1.5, 3.5 * $k))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawBezier($pen, 7 * $k, 21 * $k, 11 * $k, 6 * $k, 19 * $k, 27 * $k, 25 * $k, 11 * $k)
    $g.Dispose(); $bg.Dispose(); $pen.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return , $ms.ToArray()
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$images = foreach ($s in $sizes) { , (New-IconPng $s) }

# formato ICO: cabeçalho (6 bytes) + diretório (16 bytes por imagem) + imagens PNG
$fs = [System.IO.File]::Create($out)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $data = $images[$i]
    $dim = if ($s -ge 256) { 0 } else { $s }   # 0 significa 256
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$data.Length); $w.Write([UInt32]$offset)
    $offset += $data.Length
}
foreach ($data in $images) { $w.Write($data) }
$w.Close()
"ícone gerado: $out"
