# Gera o instalador: publica o app e compila installer\PresentationPen.iss com o Inno Setup 6.
# Uso: powershell -ExecutionPolicy Bypass -File build-installer.ps1
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$project = Join-Path $root 'src\PresentationPen\PresentationPen.csproj'

[xml]$csproj = Get-Content $project
$version = @($csproj.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ })[0]
if (-not $version) { $version = '1.0.0' }
Write-Host "PresentationPen $version"

Write-Host '1/2 publicando o app...'
dotnet publish $project -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false -o (Join-Path $root 'publish')
if ($LASTEXITCODE -ne 0) { throw 'falha no dotnet publish (o app está aberto? feche-o pela bandeja)' }

$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 não encontrado. Instale com: winget install JRSoftware.InnoSetup' }

Write-Host '2/2 compilando o instalador...'
& $iscc /Q "/DAppVersion=$version" (Join-Path $root 'installer\PresentationPen.iss')
if ($LASTEXITCODE -ne 0) { throw 'falha ao compilar o instalador' }

$setup = Join-Path $root "installer\Output\PresentationPen-Setup-$version.exe"
Write-Host "Instalador gerado: $setup"
