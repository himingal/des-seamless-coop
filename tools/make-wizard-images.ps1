# Generates the installer side/top images (Demon's Souls remake mood: black stone, tarnished gold, blue soul flame).
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$outDir = Join-Path $root 'installer'

$fonts = New-Object System.Drawing.Text.PrivateFontCollection
try { $fonts.AddFontFile((Join-Path $root 'src\DesCoop.App\Fonts\Cinzel.ttf')) } catch { }
function SerifFont([float]$size) {
    if ($fonts.Families.Count -gt 0) { return New-Object System.Drawing.Font($fonts.Families[0], $size, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel) }
    return New-Object System.Drawing.Font('Georgia', $size, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
}

function Flame($g, [float]$cx, [float]$top, [float]$bot, [float]$w) {
    $mid = $top + ($bot - $top) * 0.55
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddBezier($cx, $top, ($cx + $w * 1.6), $mid, ($cx + $w * 1.3), $bot, $cx, $bot)
    $p.AddBezier($cx, $bot, ($cx - $w * 1.3), $bot, ($cx - $w * 1.6), $mid, $cx, $top)
    # soft radial glow behind the flame
    $gw = $w * 5.5; $gh = ($bot - $top) * 1.9
    $glowPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $glowPath.AddEllipse([float]($cx - $gw / 2), [float]($mid - $gh / 2), [float]$gw, [float]$gh)
    $glow = New-Object System.Drawing.Drawing2D.PathGradientBrush $glowPath
    $glow.CenterColor = [System.Drawing.Color]::FromArgb(110, 110, 170, 255)
    $glow.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 110, 170, 255))
    $g.FillPath($glow, $glowPath)
    $b = New-Object System.Drawing.Drawing2D.LinearGradientBrush ($p.GetBounds()), ([System.Drawing.Color]::FromArgb(255, 215, 238, 255)), ([System.Drawing.Color]::FromArgb(255, 45, 105, 200)), 90
    $g.FillPath($b, $p)
}

function Backdrop($g, [int]$w, [int]$h) {
    $g.Clear([System.Drawing.Color]::FromArgb(255, 7, 7, 9))
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddEllipse([float](-$w * 0.6), [float](-$h * 0.35), [float]($w * 2.2), [float]($h * 1.1))
    $pg = New-Object System.Drawing.Drawing2D.PathGradientBrush $path
    $pg.CenterColor = [System.Drawing.Color]::FromArgb(255, 44, 37, 24)
    $pg.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 7, 7, 9))
    $g.FillRectangle($pg, 0, 0, $w, $h)
}

function Save($bmp, $name) {
    $bmp.Save((Join-Path $outDir $name), [System.Drawing.Imaging.ImageFormat]::Bmp)
    $bmp.Dispose()
}

foreach ($scale in 1, 2) {
    # Side image (welcome / finish pages): 164x314 @100%
    $w = 164 * $scale; $h = 314 * $scale
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'
    Backdrop $g $w $h
    Flame $g ($w / 2) (70 * $scale) (150 * $scale) (18 * $scale)
    $gold = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 196, 164, 104))
    $bone = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 230, 220, 198))
    $fmt = New-Object System.Drawing.StringFormat; $fmt.Alignment = 'Center'
    $g.DrawString("DEMON'S", (SerifFont (22 * $scale)), $bone, [System.Drawing.RectangleF]::new(0, 186 * $scale, $w, 40 * $scale), $fmt)
    $g.DrawString("SOULS", (SerifFont (22 * $scale)), $bone, [System.Drawing.RectangleF]::new(0, 212 * $scale, $w, 40 * $scale), $fmt)
    $rule = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.RectangleF]::new(20 * $scale, 0, $w - 40 * $scale, 1)), ([System.Drawing.Color]::FromArgb(0, 196, 164, 104)), ([System.Drawing.Color]::FromArgb(0, 196, 164, 104)), 0
    $blend = New-Object System.Drawing.Drawing2D.ColorBlend
    $blend.Colors = @([System.Drawing.Color]::FromArgb(0, 196, 164, 104), [System.Drawing.Color]::FromArgb(200, 196, 164, 104), [System.Drawing.Color]::FromArgb(0, 196, 164, 104))
    $blend.Positions = @(0.0, 0.5, 1.0)
    $rule.InterpolationColors = $blend
    $g.FillRectangle($rule, 20 * $scale, 246 * $scale, $w - 40 * $scale, [Math]::Max(1, $scale))
    $g.DrawString("SEAMLESS CO-OP", (SerifFont (10 * $scale)), $gold, [System.Drawing.RectangleF]::new(0, 254 * $scale, $w, 20 * $scale), $fmt)
    $g.Dispose()
    Save $bmp $(if ($scale -eq 1) { 'wizard.bmp' } else { 'wizard-200.bmp' })

    # Small image (top-right of inner pages): 55x55 @100%
    $s = 55 * $scale
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    Backdrop $g $s $s
    Flame $g ($s / 2) (9 * $scale) (47 * $scale) (8.5 * $scale)
    $g.Dispose()
    Save $bmp $(if ($scale -eq 1) { 'wizard-small.bmp' } else { 'wizard-small-200.bmp' })
}
Write-Output "wizard images written to $outDir"
