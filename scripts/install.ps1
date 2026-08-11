param(
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
    [string] $Version = '0.1.0.0',

    [switch] $SkipBuild,

    [switch] $SkipExplorerRestart
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$executable = Join-Path $projectRoot 'artifacts\AiShellLauncher.exe'

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot 'build.ps1') -Version $Version
    if ($LASTEXITCODE -ne 0) { throw '构建失败，安装已停止。' }
}
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "找不到安装程序：$executable"
}

$arguments = @('--install')
if (-not $SkipExplorerRestart) {
    $arguments += '--restart-explorer'
}
& $executable @arguments
if ($LASTEXITCODE -ne 0) { throw '右键菜单安装失败。' }

Write-Host 'AI Shell Launcher 已安装。' -ForegroundColor Green
