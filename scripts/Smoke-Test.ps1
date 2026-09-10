param(
    [string]$ApiUrl = 'http://localhost:8080',
    [string]$ManagementUrl = 'http://localhost:15672',
    [string]$ApiKey = $env:PARTNER_API_KEY,
    [string]$BrokerUser = 'partner',
    [string]$BrokerPassword = 'local-demo-password'
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ApiKey)) { throw 'Set PARTNER_API_KEY or pass -ApiKey.' }

$ready = $false
for ($attempt = 0; $attempt -lt 30; $attempt++) {
    try {
        $health = Invoke-RestMethod "$ApiUrl/health/live" -TimeoutSec 5
        if ($health.status -eq 'alive') { $ready = $true; break }
    } catch { Start-Sleep -Seconds 2 }
}
if (!$ready) { throw 'API did not become healthy.' }

$reference = 'SMOKE-' + [Guid]::NewGuid().ToString('N')
$payload = @{
    partnerId = 'P-1001'; transactionReference = $reference
    amount = 250; currency = 'USD'; timestamp = '2024-05-10T14:30:00Z'
} | ConvertTo-Json
$response = $null
for ($attempt = 0; $attempt -lt 8; $attempt++) {
    try {
        $response = Invoke-WebRequest "$ApiUrl/api/v1/partner/transactions" -UseBasicParsing -Method Post `
            -Headers @{ 'X-Api-Key' = $ApiKey } -ContentType 'application/json' -Body $payload -TimeoutSec 20
        break
    } catch {
        if ($null -eq $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 503) { throw }
        Start-Sleep -Seconds 5
    }
}
if ($null -eq $response -or $response.StatusCode -ne 202) { throw 'Transaction was not accepted.' }
$receipt = $response.Content | ConvertFrom-Json

# Read with requeue to preserve messages. Use a fresh CI broker for an isolated smoke run.
$credentials = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("${BrokerUser}:${BrokerPassword}"))
$messages = Invoke-RestMethod "$ManagementUrl/api/queues/%2F/partner.transactions/get" -Method Post `
    -Headers @{ Authorization = "Basic $credentials" } -ContentType 'application/json' `
    -Body '{"count":100,"ackmode":"ack_requeue_true","encoding":"auto"}' -TimeoutSec 10
$found = $messages | ForEach-Object { $_.payload | ConvertFrom-Json } | Where-Object {
    $_.messageId -eq $receipt.messageId -and $_.transactionReference -eq $reference
}
if (!$found -or $found.partnerName -ne 'Demo partner P-1001' -or $found.amount -ne 250) {
    throw 'Expected enriched message was not found in RabbitMQ.'
}
Write-Output "Smoke test passed: HTTP 202 and enriched RabbitMQ message for $reference."
