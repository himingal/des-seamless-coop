# Generates src/DesCoop.App/app.ico (a blue soul-sign flame on a dark gold ring). Run: powershell -File tools/make-icon.ps1
Add-Type -AssemblyName System.Drawing
$out = Join-Path $PSScriptRoot '..\src\DesCoop.App\app.ico'
$sizes = 256, 64, 48, 32, 16
$pngs = @()
foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $pad = [Math]::Max(1, $s * 0.04)
    $rect = New-Object System.Drawing.RectangleF $pad, $pad, ($s - 2 * $pad), ($s - 2 * $pad)
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(255, 34, 28, 18)), ([System.Drawing.Color]::FromArgb(255, 10, 9, 7)), 90
    $g.FillEllipse($bg, $rect)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 201, 164, 92)), ([Math]::Max(1, $s * 0.06))
    $g.DrawEllipse($pen, $rect)
    # flame: two bezier lobes
    $cx = $s / 2; $top = $s * 0.18; $bot = $s * 0.80; $w = $s * 0.22
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddBezier($cx, $top, ($cx + $w * 1.6), ($s * 0.45), ($cx + $w * 1.3), $bot, $cx, $bot)
    $path.AddBezier($cx, $bot, ($cx - $w * 1.3), $bot, ($cx - $w * 1.6), ($s * 0.45), $cx, $top)
    $flameRect = $path.GetBounds()
    $fb = New-Object System.Drawing.Drawing2D.LinearGradientBrush $flameRect, ([System.Drawing.Color]::FromArgb(255, 190, 230, 255)), ([System.Drawing.Color]::FromArgb(255, 40, 110, 210)), 90
    $g.FillPath($fb, $path)
    $inner = New-Object System.Drawing.Drawing2D.GraphicsPath
    $iw = $w * 0.5; $itop = $s * 0.42
    $inner.AddBezier($cx, $itop, ($cx + $iw * 1.6), ($s * 0.58), ($cx + $iw * 1.2), ($bot - $s * 0.04), $cx, ($bot - $s * 0.04))
    $inner.AddBezier($cx, ($bot - $s * 0.04), ($cx - $iw * 1.2), ($bot - $s * 0.04), ($cx - $iw * 1.6), ($s * 0.58), $cx, $itop)
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(230, 245, 252, 255))), $inner)
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += , $ms.ToArray()
    $bmp.Dispose()
}
$fs = [System.IO.File]::Create($out)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $d = $pngs[$i]
    $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s }))); $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $bw.Write([byte]0); $bw.Write([byte]0); $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$d.Length); $bw.Write([UInt32]$offset); $offset += $d.Length
}
foreach ($d in $pngs) { $bw.Write($d) }
$bw.Close()
Write-Output "wrote $out"
