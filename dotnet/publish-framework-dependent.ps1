# Smaller EXE (~tens of MB): requires .NET 8 *Desktop* Runtime on the machine.
# https://dotnet.microsoft.com/download/dotnet/8.0 — install "Run desktop apps" / Windows x64.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Push-Location $PSScriptRoot
try {
  dotnet publish .\RenPyAutoTranslate.Wpf\RenPyAutoTranslate.Wpf.csproj `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -p:PublishSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false

  Get-ChildItem -LiteralPath $repoRoot -File -ErrorAction Stop |
    Where-Object { $_.Name -like 'RenPyAutoTranslate.*' -and $_.Extension -ne '.exe' } |
    Remove-Item -Force

  $legacy = Join-Path $repoRoot 'publish'
  if (Test-Path -LiteralPath $legacy) {
    Remove-Item -LiteralPath $legacy -Recurse -Force
  }
}
finally {
  Pop-Location
}

Write-Host "Output: $(Join-Path $repoRoot 'RenPyAutoTranslate.exe')"
Write-Host "Users need the .NET 8 Desktop Runtime (x64) installed, or the app will not start."
