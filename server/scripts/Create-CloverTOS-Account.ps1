param(
    [string]$Username = "clover",
    [string]$Password = "clover123",
    [string]$HostName = "127.0.0.1",
    [int]$WebPort = 8080
)

$ErrorActionPreference = "Stop"

function Write-Step($Message) {
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Write-Ok($Message) {
    Write-Host "OK  $Message" -ForegroundColor Green
}

if ($Username.Length -lt 4) {
    throw "O usuario precisa ter pelo menos 4 caracteres."
}

if ($Password.Length -lt 6) {
    throw "A senha precisa ter pelo menos 6 caracteres."
}

$uri = "http://${HostName}:${WebPort}/api/account/create"
$body = @{
    username = $Username
    password1 = $Password
    password2 = $Password
} | ConvertTo-Json

Write-Step "Criando/verificando conta '$Username'"

try {
    $response = Invoke-RestMethod -Method Post -Uri $uri -Body $body -ContentType "application/json"
    if ($response.result -eq 0) {
        Write-Ok "Conta criada: $Username"
    }
    else {
        throw "Resposta inesperada do servidor: $($response | ConvertTo-Json -Compress)"
    }
}
catch {
    $message = $_.Exception.Message
    if ($message -like "*already exists*") {
        Write-Ok "Conta ja existe: $Username"
        return
    }

    if ($_.ErrorDetails -and $_.ErrorDetails.Message -like "*already exists*") {
        Write-Ok "Conta ja existe: $Username"
        return
    }

    throw
}
