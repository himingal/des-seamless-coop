# Tests, publishes the single-file exe and builds the installer into dist/.
param([string]$Version = "1.0.0")
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

dotnet test tests/DesCoop.Tests -c Release -nologo
if ($LASTEXITCODE -ne 0) { throw "tests failed" }

Remove-Item -Recurse -Force publish -ErrorAction SilentlyContinue
dotnet publish src/DesCoop.App -c Release -o publish -p:Version=$Version -nologo
if ($LASTEXITCODE -ne 0) { throw "publish failed" }

$iscc = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe", "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe") |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 not found (winget install JRSoftware.InnoSetup)" }
& $iscc "/DAppVersion=$Version" installer/DesCoop.iss
if ($LASTEXITCODE -ne 0) { throw "installer build failed" }
Get-ChildItem dist
