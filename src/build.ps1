<#
  一键编译 AOE4炼狱天赋点挂机.exe

  依赖：只需要 .NET Framework 4.x 自带的 csc.exe（Win10 / Win11 默认就有）
        不需要装 Visual Studio，也不需要 .NET SDK

  用法：在 PowerShell 里执行
        .\build.ps1
        .\build.ps1 -OutputDir "D:\out"
#>
[CmdletBinding()]
param(
    [string]$OutputDir = ""
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

# ---------- 找 csc.exe ----------
$candidates = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)
$csc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) {
    Write-Host '[X] 找不到 .NET Framework 4.x 的 csc.exe' -ForegroundColor Red
    Write-Host '    Win10/11 默认自带；若确实缺失，请安装 .NET Framework 4.8。' -ForegroundColor Gray
    exit 1
}

# ---------- 输出目录 ----------
if ([string]::IsNullOrWhiteSpace($OutputDir)) { $OutputDir = Join-Path $here 'dist' }
if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null }
$exe = Join-Path $OutputDir 'AOE4炼狱天赋点挂机.exe'

# ---------- 源文件 ----------
$sources = @(
    (Join-Path $here 'AoE4Auto.cs'),
    (Join-Path $here 'Templates.cs')
)
foreach ($s in $sources) {
    if (-not (Test-Path $s)) { Write-Host "[X] 缺少源文件：$s" -ForegroundColor Red; exit 1 }
}
$manifest = Join-Path $here 'app.manifest'

Write-Host ''
Write-Host '==> 编译' -ForegroundColor Cyan
Write-Host "    csc      : $csc" -ForegroundColor Gray
Write-Host "    输出     : $exe" -ForegroundColor Gray
Write-Host ''

$cscArgs = @(
    '/nologo'
    '/target:exe'                      # 控制台子系统（进度都打在控制台里）
    '/platform:anycpu32bitpreferred'   # 32 位优先，避免 64 位下跨进程输入被拦
    '/optimize+'
    '/codepage:65001'                  # 源码是 UTF-8
    "/win32manifest:$manifest"         # DPI 感知 + asInvoker
    "/out:$exe"
    '/r:System.dll'
    '/r:System.Drawing.dll'
    '/r:System.Windows.Forms.dll'
) + $sources

& $csc $cscArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host ''
    Write-Host "[X] 编译失败（退出码 $LASTEXITCODE）" -ForegroundColor Red
    exit $LASTEXITCODE
}

$f = Get-Item $exe
Write-Host ''
Write-Host ("[OK] 编译成功  {0}  ({1:N0} 字节)" -f $f.Name, $f.Length) -ForegroundColor Green
Write-Host ("     路径：{0}" -f $f.FullName) -ForegroundColor Gray
Write-Host ''
