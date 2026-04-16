# Requires .NET 8 SDK. Builds one self-contained win-x64 EXE at the repository root.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Push-Location $PSScriptRoot
try {
  dotnet publish .\RenPyAutoTranslate.Wpf\RenPyAutoTranslate.Wpf.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false

  # Keep only RenPyAutoTranslate.exe in the repo root (drop pdb, loose dlls, deps, runtimeconfig).
  Get-ChildItem -LiteralPath $repoRoot -File -ErrorAction Stop |
    Where-Object { $_.Name -like 'RenPyAutoTranslate.*' -and $_.Extension -ne '.exe' } |
    Remove-Item -Force

  # Remove legacy publish\ output from older layouts.
  $legacy = Join-Path $repoRoot 'publish'
  if (Test-Path -LiteralPath $legacy) {
    Remove-Item -LiteralPath $legacy -Recurse -Force
  }
}
finally {
  Pop-Location
}

Write-Host "Output: $(Join-Path $repoRoot 'RenPyAutoTranslate.exe')"
