param(
    [Parameter(Mandatory)]
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source = Join-Path $projectRoot 'logo.png'
if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
    throw "缺少软件图标源文件：$source"
}

$resolvedOutput = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($resolvedOutput) | Out-Null
& (Join-Path $PSScriptRoot 'Convert-PngToIco.ps1') `
    -SourcePath $source `
    -IconOutputPath (Join-Path $resolvedOutput 'Logo.ico') `
    -PngOutputPath (Join-Path $resolvedOutput 'Logo.png') `
    -TrimWhite
