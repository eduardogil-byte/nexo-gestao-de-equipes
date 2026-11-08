param(
    [string]$SqlInstance = '.\SQLEXPRESS',
    [string]$Database = 'NexoDb'
)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\src\Nexo.Api\Nexo.Api.csproj'
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'SDK .NET 8 não encontrado. Instale o SDK 8 (não apenas o runtime) e reinicie o terminal.'
}
$versionOutput = & dotnet --list-sdks
if (-not ($versionOutput -match '(^|\s)8\.')) {
    Write-Warning 'SDK 8 não listado. SDK 9/10 pode compilar net8.0, mas precisa dos reference packs 8.'
}
$connection = "Server=$SqlInstance;Database=$Database;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
& dotnet user-secrets set 'ConnectionStrings:DefaultConnection' $connection --project $project
if ($LASTEXITCODE -ne 0) { throw 'Falha ao gravar a conexão no user-secrets.' }
$existing = & dotnet user-secrets list --project $project
if ($LASTEXITCODE -ne 0) { throw 'Falha ao listar user-secrets.' }
if (-not ($existing -match 'Jwt:Key')) {
    $bytes = New-Object byte[] 48
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    $generated = [Convert]::ToBase64String($bytes)
    & dotnet user-secrets set 'Jwt:Key' $generated --project $project
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao gravar chave JWT.' }
    Write-Host 'Chave JWT local criada em user-secrets.' -ForegroundColor Green
} else {
    Write-Host 'Chave JWT local existente preservada.' -ForegroundColor Green
}
Write-Host "Configuração local pronta: $SqlInstance -> $Database" -ForegroundColor Green
Write-Host 'Próximo comando:'
Write-Host "dotnet run --project `"$project`""
