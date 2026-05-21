[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

function Save-PngAsIco {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PngPath,
        [Parameter(Mandatory = $true)]
        [string]$IcoPath
    )

    $sourceBitmap = [System.Drawing.Image]::FromFile($PngPath)
    $resizedBitmap = New-Object System.Drawing.Bitmap 256, 256, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($resizedBitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.DrawImage($sourceBitmap, 0, 0, 256, 256)

    $memory = New-Object System.IO.MemoryStream
    try {
        $resizedBitmap.Save($memory, [System.Drawing.Imaging.ImageFormat]::Png)
        [byte[]]$pngBytes = $memory.ToArray()

        $fs = [System.IO.File]::Create($IcoPath)
        try {
            $writer = New-Object System.IO.BinaryWriter($fs)
            $writer.Write([UInt16]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]1)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]32)
            $writer.Write([UInt32]$pngBytes.Length)
            $writer.Write([UInt32]22)
            $writer.Write($pngBytes)
            $writer.Flush()
        }
        finally {
            $fs.Dispose()
        }
    }
    finally {
        $memory.Dispose()
        $graphics.Dispose()
        $resizedBitmap.Dispose()
        $sourceBitmap.Dispose()
    }
}

$iconDir = $PSScriptRoot
$map = @{
    "sleep.png" = "icon-sleep.ico"
    "do_nothing.png" = "icon-do-nothing.ico"
    "unknown.png" = "icon-unknown.ico"
}

foreach ($entry in $map.GetEnumerator()) {
    $source = Join-Path $iconDir $entry.Key
    $dest = Join-Path $PSScriptRoot $entry.Value
    Save-PngAsIco -PngPath $source -IcoPath $dest
}
