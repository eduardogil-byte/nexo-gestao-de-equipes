$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\src\Nexo.Api\Nexo.Api.csproj'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
& dotnet run --project $project --no-launch-profile --urls 'http://localhost:5088'
if ($LASTEXITCODE -ne 0) { throw "Falha ao executar (código $LASTEXITCODE)." }
