# 编译字节补丁工具（只需要 Windows 自带的 .NET Framework 编译器）
param(
    [string]$OutDir = (Join-Path $PSScriptRoot 'bin')
)

$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'src\PCL299Patch.cs'
if (-not (Test-Path $source)) { throw "找不到源码：$source" }

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) { throw '没有找到 csc.exe，请确认系统是 Windows 且装有 .NET Framework 4.x。' }

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$out = Join-Path $OutDir 'PCL299-Download-Restore-Patch.exe'

& $csc /nologo /target:winexe /optimize+ "/out:$out" `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll $source
if ($LASTEXITCODE -ne 0) { throw "编译失败，退出码 $LASTEXITCODE" }

$hash = (Get-FileHash $out -Algorithm SHA256).Hash
Write-Host "输出：$out"
Write-Host "大小：$((Get-Item $out).Length) 字节"
Write-Host "SHA-256：$hash"
