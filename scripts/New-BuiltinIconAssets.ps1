param(
    [Parameter(Mandatory)]
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sourceRoot = Join-Path $projectRoot 'assets\builtin'
$resolvedOutput = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($resolvedOutput) | Out-Null

foreach ($name in @('codex', 'claude', 'antigravity', 'grok', 'opencode', 'openclaw', 'hermes')) {
    $source = Join-Path $sourceRoot ($name + '.png')
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "缺少内置工具图标：$source"
    }
    & (Join-Path $PSScriptRoot 'Convert-PngToIco.ps1') `
        -SourcePath $source `
        -IconOutputPath (Join-Path $resolvedOutput ($name + '.ico'))
}
