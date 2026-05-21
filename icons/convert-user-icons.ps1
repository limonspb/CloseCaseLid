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

    [byte[]]$pngBytes = [System.IO.File]::ReadAllBytes($PngPath)

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
