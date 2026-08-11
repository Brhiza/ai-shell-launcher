param(
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
    [string] $Version = '0.1.0.0',

    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [switch] $SkipTests
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactsRoot = Join-Path $projectRoot 'artifacts'
$publishRoot = Join-Path $artifactsRoot 'publish'
$runtimeBundleRoot = Join-Path $artifactsRoot 'runtime-bundle'
$outputPath = Join-Path $artifactsRoot 'AiShellLauncher.exe'
$dotnetCommand = Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue |
    Select-Object -First 1
if ($null -eq $dotnetCommand) {
    throw '缺少 .NET SDK 8：找不到 dotnet 命令。'
}
$dotnet = $dotnetCommand.Source

$vsWhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vsWhere -PathType Leaf)) {
    throw "缺少 Visual Studio Installer：找不到 $vsWhere"
}
$visualStudioRoot = (& $vsWhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath |
    Select-Object -First 1)
if ([string]::IsNullOrWhiteSpace($visualStudioRoot)) {
    throw '缺少 MSVC C++ x64 构建工具。'
}
$vcVars = Join-Path $visualStudioRoot 'VC\Auxiliary\Build\vcvars64.bat'
if (-not (Test-Path -LiteralPath $vcVars -PathType Leaf)) {
    throw "缺少 Visual C++ 编译环境：找不到 $vcVars"
}

function Reset-ProjectDirectory {
    param([Parameter(Mandatory)][string] $Path)

    $fullPath = [IO.Path]::GetFullPath($Path)
    $allowedRoot = [IO.Path]::GetFullPath($artifactsRoot).TrimEnd('\') + '\'
    if (-not $fullPath.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "拒绝清理项目 artifacts 之外的目录：$fullPath"
    }
    if ([IO.Directory]::Exists($fullPath)) {
        [IO.Directory]::Delete($fullPath, $true)
    }
    [IO.Directory]::CreateDirectory($fullPath) | Out-Null
}

[IO.Directory]::CreateDirectory($artifactsRoot) | Out-Null
Reset-ProjectDirectory -Path $publishRoot
Reset-ProjectDirectory -Path $runtimeBundleRoot

& (Join-Path $projectRoot 'scripts\New-LauncherAssets.ps1') `
    -OutputDirectory (Join-Path $projectRoot 'src\AiShellLauncher.App\Assets')

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
& $dotnet build (Join-Path $projectRoot 'AiShellLauncher.sln') -c $Configuration
if ($LASTEXITCODE -ne 0) { throw '托管项目构建失败。' }

if (-not $SkipTests) {
    & $dotnet run --project (Join-Path $projectRoot 'tests\AiShellLauncher.Core.Tests\AiShellLauncher.Core.Tests.csproj') -c $Configuration --no-build
    if ($LASTEXITCODE -ne 0) { throw '核心测试失败。' }
}

$runnerExecutable = Join-Path $projectRoot "src\AiShellLauncher.Runner\bin\$Configuration\net48\AiShellLauncher.Runner.exe"
if (-not (Test-Path -LiteralPath $runnerExecutable -PathType Leaf)) {
    throw 'Runner 构建产物不存在。'
}
Copy-Item -LiteralPath $runnerExecutable -Destination $runtimeBundleRoot

$environmentLines = & $env:ComSpec /d /s /c "`"$vcVars`" >nul && set"
if ($LASTEXITCODE -ne 0) { throw '加载 Visual C++ 编译环境失败。' }
foreach ($line in $environmentLines) {
    $separator = $line.IndexOf('=')
    if ($separator -gt 0) {
        [Environment]::SetEnvironmentVariable($line.Substring(0, $separator), $line.Substring($separator + 1), 'Process')
    }
}

$extensionPath = Join-Path $runtimeBundleRoot 'AiShellLauncher.ShellExtension.dll'
$nativeObjectPath = Join-Path $runtimeBundleRoot 'AiShellLauncherShell.obj'
$nativeImportLibraryPath = Join-Path $runtimeBundleRoot 'AiShellLauncherShell.lib'
$compileArguments = @(
    '/nologo',
    '/std:c++17',
    '/EHsc',
    '/O2',
    '/utf-8',
    '/DUNICODE',
    '/D_UNICODE',
    '/LD',
    '/MT',
    "/Fo$nativeObjectPath",
    (Join-Path $projectRoot 'native\AiShellLauncher.ShellExtension\AiShellLauncherShell.cpp'),
    '/link',
    "/OUT:$extensionPath",
    "/IMPLIB:$nativeImportLibraryPath",
    'ole32.lib',
    'shell32.lib',
    'shlwapi.lib'
)
& cl.exe @compileArguments
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $extensionPath -PathType Leaf)) {
    throw '资源管理器扩展编译失败。'
}

& (Join-Path $projectRoot 'scripts\New-LauncherAssets.ps1') -OutputDirectory (Join-Path $runtimeBundleRoot 'Assets')
& (Join-Path $projectRoot 'scripts\New-BuiltinIconAssets.ps1') -OutputDirectory (Join-Path $runtimeBundleRoot 'Icons')
$manifestContent = (Get-Content -Raw -Encoding utf8 (Join-Path $projectRoot 'package\AppxManifest.xml')).Replace('__VERSION__', $Version)
[IO.File]::WriteAllText(
    (Join-Path $runtimeBundleRoot 'MenuPackageManifest.xml'),
    $manifestContent,
    [Text.UTF8Encoding]::new($false))

$appPublish = Join-Path $publishRoot 'app'
$appProject = Join-Path $projectRoot 'src\AiShellLauncher.App\AiShellLauncher.App.csproj'
& $dotnet build $appProject `
    -c $Configuration `
    "-p:RuntimeBundleRoot=$runtimeBundleRoot" `
    "-p:AssemblyVersion=$Version" `
    "-p:FileVersion=$Version" `
    '-p:DebugType=None' `
    '-p:DebugSymbols=false' `
    -o $appPublish
if ($LASTEXITCODE -ne 0) { throw '设置程序构建失败。' }

if (Test-Path -LiteralPath $outputPath -PathType Leaf) {
    [IO.File]::Delete($outputPath)
}
Copy-Item -LiteralPath (Join-Path $appPublish 'AiShellLauncher.exe') -Destination $outputPath

$selfTest = Start-Process -FilePath $outputPath -ArgumentList '--self-test' -WindowStyle Hidden -Wait -PassThru
if ($selfTest.ExitCode -ne 0) {
    throw "单文件程序自检失败，退出码：$($selfTest.ExitCode)"
}

$buildInfo = [ordered]@{
    version = $Version
    executable = $outputPath
}
$buildInfo | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $artifactsRoot 'build-info.json') -Encoding utf8NoBOM

Write-Host "构建完成：$outputPath" -ForegroundColor Green
