param([string]$Image = 'financial-portfolio-api:local')
$ErrorActionPreference = 'Stop'
# This check creates and removes only its own randomly named container and volume.
$testName = 'portfolio-smoke-' + [Guid]::NewGuid().ToString('N')
$volumeName = $testName + '-data'
$previousKey = $env:ApiKey
$bytes = New-Object byte[] 32
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
$env:ApiKey = ([BitConverter]::ToString($bytes)).Replace('-', '')
function Wait-Healthy([string]$baseUrl) {
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        try {
            $health = Invoke-RestMethod "$baseUrl/health" -TimeoutSec 2
            if ($health.status -eq 'healthy') { return }
        } catch { }
        Start-Sleep -Milliseconds 500
    }
    throw 'Container did not become healthy.'
}
try {
    docker volume create $volumeName | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Cannot create test volume.' }
    docker run -d --name $testName -p '127.0.0.1::8080' -e ApiKey --mount "type=volume,source=$volumeName,target=/data" $Image | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Cannot start test container.' }
    $binding = (docker port $testName 8080/tcp).Trim()
    $baseUrl = 'http://' + $binding
    Wait-Healthy $baseUrl
    $headers = @{ 'X-Api-Key' = $env:ApiKey }
    $body = @{ name = 'Container restart test'; clientId = 'DOCKER-TEST'; clientName = 'Test'; riskProfile = 'Moderate' } | ConvertTo-Json
    $portfolio = Invoke-RestMethod "$baseUrl/api/v1/portfolio" -Method Post -Headers $headers -ContentType 'application/json' -Body $body
    foreach ($path in @('/', '/api-docs', '/openapi.json', '/postman.json', '/docs/guide')) {
        $response = Invoke-WebRequest ($baseUrl + $path) -UseBasicParsing
        if ($response.StatusCode -ne 200) { throw "Unavailable container route: $path" }
    }
    docker restart $testName | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Cannot restart test container.' }
    # Docker may assign a different ephemeral host port after restarting.
    $baseUrl = 'http://' + (docker port $testName 8080/tcp).Trim()
    Wait-Healthy $baseUrl
    $restored = Invoke-RestMethod "$baseUrl/api/v1/portfolio/$($portfolio.id)" -Headers $headers
    if ($restored.name -ne $portfolio.name) { throw 'Portfolio did not persist across restart.' }
    Write-Host 'PASS: container startup, migrations, documentation, authenticated creation and persistence across restart.'
} finally {
    docker rm -f $testName 2>$null | Out-Null
    docker volume rm $volumeName 2>$null | Out-Null
    $env:ApiKey = $previousKey
}
