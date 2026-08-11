param(
    [Parameter(Mandatory)]
    [string] $SourcePath,

    [Parameter(Mandatory)]
    [string] $IconOutputPath,

    [string] $PngOutputPath,

    [switch] $TrimWhite
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function Get-ContentBounds {
    param([Drawing.Bitmap] $Bitmap)

    if (-not $TrimWhite) {
        return [Drawing.Rectangle]::new(0, 0, $Bitmap.Width, $Bitmap.Height)
    }

    $left = $Bitmap.Width
    $top = $Bitmap.Height
    $right = -1
    $bottom = -1
    for ($y = 0; $y -lt $Bitmap.Height; $y += 2) {
        for ($x = 0; $x -lt $Bitmap.Width; $x += 2) {
            $pixel = $Bitmap.GetPixel($x, $y)
            if ($pixel.A -gt 8 -and ($pixel.R -lt 246 -or $pixel.G -lt 246 -or $pixel.B -lt 246)) {
                $left = [Math]::Min($left, $x)
                $top = [Math]::Min($top, $y)
                $right = [Math]::Max($right, $x)
                $bottom = [Math]::Max($bottom, $y)
            }
        }
    }

    if ($right -lt $left -or $bottom -lt $top) {
        return [Drawing.Rectangle]::new(0, 0, $Bitmap.Width, $Bitmap.Height)
    }

    $width = $right - $left + 1
    $height = $bottom - $top + 1
    $padding = [int][Math]::Ceiling([Math]::Max($width, $height) * 0.035)
    $left = [Math]::Max(0, $left - $padding)
    $top = [Math]::Max(0, $top - $padding)
    $right = [Math]::Min($Bitmap.Width - 1, $right + $padding)
    $bottom = [Math]::Min($Bitmap.Height - 1, $bottom + $padding)
    return [Drawing.Rectangle]::FromLTRB($left, $top, $right + 1, $bottom + 1)
}

function New-ResizedPngBytes {
    param(
        [Drawing.Bitmap] $Source,
        [Drawing.Rectangle] $SourceBounds,
        [int] $Size
    )

    $bitmap = [Drawing.Bitmap]::new($Size, $Size, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([Drawing.Color]::Transparent)
        $graphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.CompositingQuality = [Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::HighQuality

        $scale = [Math]::Min($Size / $SourceBounds.Width, $Size / $SourceBounds.Height)
        $width = [int][Math]::Round($SourceBounds.Width * $scale)
        $height = [int][Math]::Round($SourceBounds.Height * $scale)
        $destination = [Drawing.Rectangle]::new(
            [int](($Size - $width) / 2),
            [int](($Size - $height) / 2),
            $width,
            $height)
        $graphics.DrawImage($Source, $destination, $SourceBounds, [Drawing.GraphicsUnit]::Pixel)

        $stream = [IO.MemoryStream]::new()
        $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
        return ,$stream.ToArray()
    } finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$resolvedSource = [IO.Path]::GetFullPath($SourcePath)
$resolvedIcon = [IO.Path]::GetFullPath($IconOutputPath)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($resolvedIcon)) | Out-Null

$sourceImage = [Drawing.Image]::FromFile($resolvedSource)
$source = [Drawing.Bitmap]::new($sourceImage)
$sourceImage.Dispose()
try {
    $bounds = Get-ContentBounds -Bitmap $source
    $sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
    $images = [Collections.Generic.List[byte[]]]::new()
    foreach ($size in $sizes) {
        $images.Add((New-ResizedPngBytes -Source $source -SourceBounds $bounds -Size $size))
    }

    $stream = [IO.File]::Create($resolvedIcon)
    $writer = [IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$sizes.Count)
        $offset = 6 + (16 * $sizes.Count)
        for ($index = 0; $index -lt $sizes.Count; $index++) {
            $size = $sizes[$index]
            $dimension = if ($size -eq 256) { 0 } else { $size }
            $writer.Write([byte]$dimension)
            $writer.Write([byte]$dimension)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$images[$index].Length)
            $writer.Write([uint32]$offset)
            $offset += $images[$index].Length
        }
        foreach ($image in $images) {
            $writer.Write($image)
        }
    } finally {
        $writer.Dispose()
        $stream.Dispose()
    }

    $iconBytes = [IO.File]::ReadAllBytes($resolvedIcon)
    $imageCount = [BitConverter]::ToUInt16($iconBytes, 4)
    if ($imageCount -ne $sizes.Count) {
        throw "生成的 ICO 图层数量错误：$resolvedIcon"
    }
    for ($index = 0; $index -lt $imageCount; $index++) {
        $entryOffset = 6 + (16 * $index)
        $imageLength = [BitConverter]::ToUInt32($iconBytes, $entryOffset + 8)
        $imageOffset = [BitConverter]::ToUInt32($iconBytes, $entryOffset + 12)
        if ($imageLength -lt 8 -or $imageOffset + $imageLength -gt $iconBytes.Length) {
            throw "生成的 ICO 图层范围无效：$resolvedIcon"
        }
        $pngSignature = [byte[]](137, 80, 78, 71, 13, 10, 26, 10)
        for ($signatureIndex = 0; $signatureIndex -lt $pngSignature.Length; $signatureIndex++) {
            if ($iconBytes[$imageOffset + $signatureIndex] -ne $pngSignature[$signatureIndex]) {
                throw "生成的 ICO 图层不是有效 PNG：$resolvedIcon"
            }
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($PngOutputPath)) {
        $resolvedPng = [IO.Path]::GetFullPath($PngOutputPath)
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($resolvedPng)) | Out-Null
        [IO.File]::WriteAllBytes($resolvedPng, (New-ResizedPngBytes -Source $source -SourceBounds $bounds -Size 256))
    }
} finally {
    $source.Dispose()
}
