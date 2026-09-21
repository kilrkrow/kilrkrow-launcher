# Requires Windows + .NET 10 SDK. Produces a self-contained win-x64 folder.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root "artifacts\win-x64"
dotnet publish (Join-Path $root "src\KilrkrowLauncher\KilrkrowLauncher.csproj") `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -o $out
Write-Host "Published $out\KilrkrowLauncher.exe"
