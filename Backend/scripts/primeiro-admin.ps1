param([Parameter(Mandatory = $true)][string]$Email)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\src\Nexo.Api\Nexo.Api.csproj'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
& dotnet run --project $project -- --bootstrap-admin $Email
if ($LASTEXITCODE -ne 0) { throw 'Falha ao promover o primeiro administrador.' }
