# Reconstruit l'application puis l'installateur, toujours dans le bon ordre.
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

# 1. On repart d'un dossier publish vide pour ne garder aucun ancien fichier
if (Test-Path .\publish) { Remove-Item .\publish -Recurse -Force }

# 2. Publication autonome (le PC cible n'a pas besoin de .NET)
dotnet publish .\IdeCSharp\IdeCSharp.UI -c Release -r win-x64 --self-contained true -o .\publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish a échoué" }

# 3. Contrôle : l'assistant doit être présent dans la version publiée
$octets = [System.IO.File]::ReadAllBytes("$PSScriptRoot\publish\IdeCSharp.Core.dll")
$core = [System.Text.Encoding]::GetEncoding(28591).GetString($octets)
if ($core -notmatch "GeminiAssistantService") { throw "L'assistant est absent du publish" }

# 4. Compilation de l'installateur
$iscc = @(
  "$env:ProgramFiles\Inno Setup 7\ISCC.exe",
  "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe",
  "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
  "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
  "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup introuvable" }

& $iscc .\installer\Kode.iss
if ($LASTEXITCODE -ne 0) { throw "La compilation Inno Setup a échoué" }
Write-Host "OK : .\Output\Kode-Setup-1.0.0.exe"