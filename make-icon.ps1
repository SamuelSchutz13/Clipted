# Gera clipted.ico (várias resoluções, 32 bits com transparência).
# Rode: powershell -ExecutionPolicy Bypass -File make-icon.ps1
Add-Type -AssemblyName System.Drawing
$sizes = 16, 20, 24, 32, 40, 48, 64, 256

function RoundRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = [Drawing.Drawing2D.GraphicsPath]::new()
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    $p
}

function Brush($a, $r, $g, $b) { [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb($a, $r, $g, $b)) }

function Line($g, $w, $a, $r, $gr, $b, $x1, $x2, $y) {
    $pen = [Drawing.Pen]::new([Drawing.Color]::FromArgb($a, $r, $gr, $b), $w)
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
    $g.DrawLine($pen, $x1, $y, $x2, $y)
}

# desenhado num quadro de 256 e escalado; até 24 px usa uma versão simplificada que continua legível
function Draw([int]$s) {
    $small = $s -le 24
    $bmp = [Drawing.Bitmap]::new($s, $s, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.ScaleTransform($s / 256, $s / 256)

    $tile = RoundRect 8 8 240 240 60
    $bg = [Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.RectangleF]::new(8, 8, 240, 240),
        [Drawing.Color]::FromArgb(132, 108, 255), [Drawing.Color]::FromArgb(46, 88, 238), 65)
    $g.FillPath($bg, $tile)

    if (-not $small) {
        $g.SetClip($tile) # brilho suave no topo
        $gloss = [Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.RectangleF]::new(0, 6, 256, 124),
            [Drawing.Color]::FromArgb(70, 255, 255, 255), [Drawing.Color]::FromArgb(0, 255, 255, 255), 90)
        $g.FillRectangle($gloss, 0, 8, 256, 120)
        $g.ResetClip()

        $st = $g.Save() # folha de trás inclinada: o "histórico"
        $g.TranslateTransform(156, 124); $g.RotateTransform(12)
        $g.FillPath((Brush 105 255 255 255), (RoundRect -52 -72 104 144 16))
        $g.Restore($st)

        for ($i = 1; $i -le 6; $i++) { $g.FillPath((Brush 12 20 16 90), (RoundRect (62 - $i) (76 + $i) (118 + 2 * $i) (152 + $i) (20 + $i))) } # sombra
        $g.FillPath([Drawing.Brushes]::White, (RoundRect 62 70 118 152 20))
        $g.FillPath((Brush 255 40 42 140), (RoundRect 92 52 58 36 14)) # prendedor
        $g.FillEllipse((Brush 255 132 108 255), 114, 60, 14, 14)
        Line $g 15 255 84 96 255 88 154 124
        Line $g 15 255 205 210 238 88 154 154
        Line $g 15 255 205 210 238 88 130 184
    } else {
        $g.FillPath([Drawing.Brushes]::White, (RoundRect 50 60 144 176 24))
        $g.FillPath((Brush 255 40 42 140), (RoundRect 88 34 68 44 16))
        Line $g 26 255 84 96 255 82 162 126
        Line $g 26 255 190 196 236 82 136 180
    }
    $g.Dispose()
    $bmp
}

# entrada BMP 32 bits (BGRA de baixo pra cima + máscara AND vazia)
function Dib($bmp) {
    $s = $bmp.Width
    $data = $bmp.LockBits([Drawing.Rectangle]::new(0, 0, $s, $s), 'ReadOnly', 'Format32bppArgb')
    $px = New-Object byte[] ($s * $s * 4)
    [Runtime.InteropServices.Marshal]::Copy($data.Scan0, $px, 0, $px.Length)
    $bmp.UnlockBits($data)
    $maskRow = [int][Math]::Ceiling($s / 32) * 4
    $ms = [IO.MemoryStream]::new(); $w = [IO.BinaryWriter]::new($ms)
    $w.Write([uint32]40); $w.Write([int32]$s); $w.Write([int32]($s * 2)); $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]0); $w.Write([uint32]($px.Length + $maskRow * $s)); $w.Write([int32]0); $w.Write([int32]0); $w.Write([uint32]0); $w.Write([uint32]0)
    for ($y = $s - 1; $y -ge 0; $y--) { $w.Write($px, $y * $s * 4, $s * 4) }
    $w.Write((New-Object byte[] ($maskRow * $s)))
    $w.Flush()
    , $ms.ToArray()
}

$images = foreach ($s in $sizes) { , (Dib (Draw $s)) }
$ms = [IO.MemoryStream]::new(); $w = [IO.BinaryWriter]::new($ms)
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $b = [byte]($sizes[$i] % 256) # 256 é gravado como 0
    $w.Write($b); $w.Write($b); $w.Write([byte]0); $w.Write([byte]0); $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$images[$i].Length); $w.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write($img) }
$w.Flush()
[IO.File]::WriteAllBytes((Join-Path $PSScriptRoot 'clipted.ico'), $ms.ToArray())
# prévia para conferir o desenho: 256 e, ampliados, 48 / 32 / 16
$prev = [Drawing.Bitmap]::new(256 * 4 + 40, 256)
$pg = [Drawing.Graphics]::FromImage($prev)
$pg.Clear([Drawing.Color]::FromArgb(230, 230, 235))
$pg.InterpolationMode = 'NearestNeighbor'; $pg.PixelOffsetMode = 'Half'
$pg.DrawImage((Draw 256), 0, 0)
$x = 266
foreach ($s in 48, 32, 16) { $pg.DrawImage((Draw $s), $x, 0, 256, 256); $x += 266 }
$prev.Save((Join-Path $env:TEMP 'clipted-preview.png'))
