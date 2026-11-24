#requires -Version 5.1
$ErrorActionPreference = 'Stop'

$ProjectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$OutDir = Join-Path $ProjectDir 'publish'
$OutExe = Join-Path $OutDir 'JiahaoToolbox.exe'

if (-not (Test-Path $OutDir)) {
    New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
}

$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    throw "未找到 .NET Framework 4.x 编译器: $csc"
}

$sources = @(
    (Join-Path $ProjectDir 'AssemblyInfo.cs'),
    (Join-Path $ProjectDir 'Program.cs'),
    (Join-Path $ProjectDir 'Core\Models.cs'),
    (Join-Path $ProjectDir 'Core\ToolboxConfig.cs'),
    (Join-Path $ProjectDir 'Core\AutostartManager.cs'),
    (Join-Path $ProjectDir 'Core\ElevationHelper.cs'),
    (Join-Path $ProjectDir 'Core\Win32Native.cs'),
    (Join-Path $ProjectDir 'Core\EcoEngine.cs'),
    (Join-Path $ProjectDir 'Core\TrustedInstallerHelper.cs'),
    (Join-Path $ProjectDir 'Core\RegistryAclHelper.cs'),
    (Join-Path $ProjectDir 'Core\DefenderManager.cs'),
    (Join-Path $ProjectDir 'Core\WindowsUpdateManager.cs'),
    (Join-Path $ProjectDir 'Core\SmartAppControlManager.cs'),
    (Join-Path $ProjectDir 'UI\Theme.cs'),
    (Join-Path $ProjectDir 'UI\VectorIcons.cs'),
    (Join-Path $ProjectDir 'UI\FluentControls.cs'),
    (Join-Path $ProjectDir 'UI\AppPickerDialog.cs'),
    (Join-Path $ProjectDir 'UI\ProcessDetailDialog.cs'),
    (Join-Path $ProjectDir 'UI\OverviewPage.cs'),
    (Join-Path $ProjectDir 'UI\EcoPage.cs'),
    (Join-Path $ProjectDir 'UI\GuardUpdatePage.cs'),
    (Join-Path $ProjectDir 'UI\SettingsPage.cs'),
    (Join-Path $ProjectDir 'UI\MainForm.cs')
)

$manifest = Join-Path $ProjectDir 'app.manifest'
$icon = Join-Path $ProjectDir 'app.ico'

$argsList = @(
    '/nologo',
    '/target:winexe',
    '/platform:x64',
    '/optimize+',
    '/warn:4',
    '/utf8output',
    "/out:$OutExe",
    "/win32manifest:$manifest",
    "/win32icon:$icon",
    '/reference:System.dll',
    '/reference:System.Core.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll',
    '/reference:System.ServiceProcess.dll',
    '/reference:System.Web.Extensions.dll'
) + $sources

Write-Host "正在编译 JiahaoToolbox.exe (嘉豪工具箱, x64 .NET Framework 4.8 原生单文件)..." -ForegroundColor Cyan
& $csc @argsList
if ($LASTEXITCODE -ne 0) {
    throw "编译失败，退出码: $LASTEXITCODE"
}

$fi = Get-Item $OutExe
$hash = (Get-FileHash $OutExe -Algorithm SHA256).Hash
Write-Host "编译成功!" -ForegroundColor Green
Write-Host "输出路径: $($fi.FullName)"
Write-Host "文件大小: $($fi.Length) 字节 ($([Math]::Round($fi.Length / 1KB, 1)) KB)"
Write-Host "SHA-256 : $hash"
