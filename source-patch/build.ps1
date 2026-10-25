# 从 PCL 2.99 源码编译“完整恢复版”（含整合包安装）
# 需要：Visual Studio 2022（.NET 桌面开发 + .NET Framework 4.8 目标包）+ Git
param(
    [Parameter(Mandatory = $true)][string]$Source,
    [string]$OutDir = (Join-Path $PSScriptRoot 'out')
)

$ErrorActionPreference = 'Stop'
$Source = (Resolve-Path $Source).Path
$vbproj = Join-Path $Source 'Plain Craft Launcher 2\Plain Craft Launcher 2.vbproj'
$meloong = Join-Path $Source 'MeloongCore\Shared\MeloongCore.csproj'
if (-not (Test-Path $vbproj)) { throw "找不到项目文件：$vbproj" }

# 1. 修改两个常量（按字节改，保持文件 GBK 编码）
$secret = Join-Path $Source 'Plain Craft Launcher 2\Modules\ModSecret.vb'
$enc = [System.Text.Encoding]::GetEncoding(28591)
$text = $enc.GetString([System.IO.File]::ReadAllBytes($secret))
$text = $text.Replace('Public Const RestrictMcDownload As Boolean = True',
                      'Public Const RestrictMcDownload As Boolean = False')
$text = $text.Replace('Environment.GetEnvironmentVariable("PCL_MS_CLIENT_ID"), "")',
                      'Environment.GetEnvironmentVariable("PCL_MS_CLIENT_ID"), "c14b0370-8d75-42f8-b329-5b60d39e319f")')
[System.IO.File]::WriteAllBytes($secret, $enc.GetBytes($text))
Write-Host '已应用 RestrictMcDownload = False'

# 2. MeloongCore 子模块
if (-not (Test-Path $meloong)) {
    Write-Host '克隆 MeloongCore...'
    & git clone --depth 1 https://github.com/Meloong-Git/MeloongCore.git (Join-Path $Source 'MeloongCore')
    if ($LASTEXITCODE -ne 0) { throw '克隆 MeloongCore 失败。' }
}

# 3. 检查 4.8 目标包并找 MSBuild
$refAsm = 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8'
if (-not (Test-Path $refAsm)) { throw '缺少 .NET Framework 4.8 目标包，请安装 VS2022 的“.NET 桌面开发”工作负载。' }
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) { throw '没有找到 vswhere.exe，请安装 Visual Studio 2022。' }
$msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw '没有找到 MSBuild.exe。' }

# 4. 编译
& $msbuild $vbproj /restore /p:Configuration=Release /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw "编译失败：$LASTEXITCODE" }

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$exe = Get-ChildItem (Split-Path $vbproj) -Recurse -Filter 'Plain Craft Launcher 2.exe' |
    Sort-Object Length -Descending | Select-Object -First 1
Copy-Item $exe.FullName (Join-Path $OutDir 'Plain Craft Launcher 2.exe') -Force
$hash = (Get-FileHash (Join-Path $OutDir 'Plain Craft Launcher 2.exe') -Algorithm SHA256).Hash
Write-Host "输出：$(Join-Path $OutDir 'Plain Craft Launcher 2.exe')"
Write-Host "SHA-256：$hash"
