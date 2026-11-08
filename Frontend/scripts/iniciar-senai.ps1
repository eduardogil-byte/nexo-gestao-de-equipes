$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
if (-not (Get-Command node -ErrorAction SilentlyContinue)) { throw 'Node.js não encontrado. Instale Node.js 20 LTS.' }
if (-not (Get-Command npm -ErrorAction SilentlyContinue)) { throw 'npm não encontrado. Instale Node.js 20 LTS.' }
Write-Host 'Node: ' (node --version)
Write-Host 'npm: ' (npm --version)
if (-not (Test-Path '.env')) { Copy-Item '.env.example' '.env'; Write-Host 'Criado .env a partir de .env.example' }
if (-not (Test-Path 'node_modules')) { Write-Host 'Instalando dependências...'; npm install; if ($LASTEXITCODE -ne 0) { throw 'npm install falhou. Verifique a internet ou o proxy institucional.' } }
Write-Host 'Frontend: http://localhost:5173'
Write-Host 'Backend esperado: http://localhost:5088'
npm run dev
