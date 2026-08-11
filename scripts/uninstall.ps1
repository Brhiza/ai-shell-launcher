param(
    [switch] $RemoveUserData,
    [switch] $SkipExplorerRestart
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$executable = Join-Path $projectRoot 'artifacts\AiShellLauncher.exe'
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    $executable = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'AiShellLauncher\Runtime\AiShellLauncher.exe'
}
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw '找不到 AI Shell Launcher.exe，无法执行卸载。'
}

$arguments = @('--uninstall')
if (-not $SkipExplorerRestart) {
    $arguments += '--restart-explorer'
}
& $executable @arguments
if ($LASTEXITCODE -ne 0) { throw '右键菜单卸载失败。' }

if ($RemoveUserData) {
    $configRoot = [IO.Path]::GetFullPath((Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'AiShellLauncher'))
    $localAppDataRoot = [IO.Path]::GetFullPath([Environment]::GetFolderPath('LocalApplicationData')).TrimEnd('\') + '\'
    if (-not $configRoot.StartsWith($localAppDataRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "拒绝删除 LocalAppData 之外的目录：$configRoot"
    }
    if ([IO.Directory]::Exists($configRoot)) {
        [IO.Directory]::Delete($configRoot, $true)
    }
}

Write-Host 'AI Shell Launcher 已卸载；默认保留用户配置。' -ForegroundColor Green
