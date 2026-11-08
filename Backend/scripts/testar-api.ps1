param([string]$BaseUrl = 'http://localhost:5088', [string]$Email)
$ErrorActionPreference = 'Stop'
$BaseUrl = $BaseUrl.TrimEnd('/')
Write-Host '1) Verificando API...'
$health = Invoke-RestMethod "$BaseUrl/api/health"
Write-Host "   API: $($health.status)"
if (-not $Email) { Write-Host 'Para testar o banco e dashboard, informe -Email (administrador aprovado).'; exit 0 }
$secure = Read-Host 'Senha do administrador' -AsSecureString
$ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
try { $password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
try {
    $body = @{ email = $Email; password = $password } | ConvertTo-Json
    $login = Invoke-RestMethod -Uri "$BaseUrl/api/auth/login" -Method Post -ContentType 'application/json' -Body $body
} finally { Remove-Variable password -ErrorAction SilentlyContinue }
$headers = @{ Authorization = "Bearer $($login.accessToken)" }
Write-Host '2) Testando SQL Server...'
$database = Invoke-RestMethod "$BaseUrl/api/health/database" -Headers $headers
Write-Host "   Banco: $($database.database)"
Write-Host '3) Consultando projetos...'
$projects = Invoke-RestMethod "$BaseUrl/api/projects" -Headers $headers
Write-Host "   Projetos acessíveis: $(@($projects).Count)"
Write-Host '4) Consultando dashboard...'
$dashboard = Invoke-RestMethod "$BaseUrl/api/dashboard" -Headers $headers
Write-Host "   Projetos: $($dashboard.totalProjects) | Tarefas abertas: $($dashboard.tasksOpen)"
Write-Host 'Teste básico concluído.' -ForegroundColor Green
