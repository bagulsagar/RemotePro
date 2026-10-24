$ErrorActionPreference = 'Stop'

$deviceId = 'db8a8cad-011d-43a1-bfcf-5312c0936e8c'
$tempSql = Join-Path $env:TEMP 'remotepro-provision.sql'
$credentialId = [guid]::NewGuid().ToString()
$inserted = $false
$token = $null

try {
    # Generate a cryptographically secure token
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $bytes = New-Object byte[] 32
    $rng.GetBytes($bytes)
    $rng.Dispose()

    $token = [Convert]::ToBase64String($bytes).
        TrimEnd('=').
        Replace('+', '-').
        Replace('/', '_')

    # Calculate SHA-256 hash
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $hashBytes = $sha.ComputeHash(
        [System.Text.Encoding]::UTF8.GetBytes($token)
    )
    $sha.Dispose()

    $tokenHash = [BitConverter]::ToString($hashBytes).Replace('-', '')

    # Check database state using SQL file, not -c with quoted identifiers
    $checkSql = @'
SELECT COUNT(*) FROM devices
WHERE "Id" = 'db8a8cad-011d-43a1-bfcf-5312c0936e8c';
SELECT COUNT(*) FROM device_credentials;
'@

    [System.IO.File]::WriteAllText(
        $tempSql,
        $checkSql,
        (New-Object System.Text.UTF8Encoding($false))
    )

    $check = @(docker exec remotepro-postgres psql `
        -U remotepro_app -d remotepro -t -A -f /tmp/remotepro-check.sql)

    # Copy check file and run it
    docker cp $tempSql 'remotepro-postgres:/tmp/remotepro-check.sql'
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not copy database check file.'
    }

    $check = @(docker exec remotepro-postgres psql `
        -U remotepro_app -d remotepro -t -A -f /tmp/remotepro-check.sql)

    if ($LASTEXITCODE -ne 0) {
        throw 'Database check failed.'
    }

    if ($check.Count -lt 2) {
        throw 'Unexpected database check result.'
    }

    if ([int]$check[0].Trim() -ne 1) {
        throw 'Expected device was not found.'
    }

    if ([int]$check[1].Trim() -ne 0) {
        throw 'Credentials already exist; stopping safely.'
    }

    # Insert credential hash
    $insertSql = @"
INSERT INTO device_credentials
    ("Id", "DeviceId", "TokenHash", "CreatedAtUtc", "RevokedAtUtc")
VALUES
    ('$credentialId', '$deviceId', '$tokenHash', CURRENT_TIMESTAMP, NULL);
"@

    [System.IO.File]::WriteAllText(
        $tempSql,
        $insertSql,
        (New-Object System.Text.UTF8Encoding($false))
    )

    docker cp $tempSql 'remotepro-postgres:/tmp/remotepro-provision.sql'
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not copy credential SQL file.'
    }

    docker exec remotepro-postgres psql `
        -v ON_ERROR_STOP=1 `
        -U remotepro_app -d remotepro `
        -f /tmp/remotepro-provision.sql

    if ($LASTEXITCODE -ne 0) {
        throw 'Credential insertion failed.'
    }

    $inserted = $true

    # Verify heartbeat
    $body = @{ token = $token } | ConvertTo-Json -Compress

    $response = Invoke-RestMethod `
        -Uri 'http://localhost:5150/api/heartbeat' `
        -Method Post `
        -ContentType 'application/json' `
        -Body $body `
        -TimeoutSec 10

    if ($response.status -ne 'Online') {
        throw 'Heartbeat did not return Online.'
    }

    Write-Host 'Heartbeat authentication successful.' -ForegroundColor Green
    Write-Host "Device status: $($response.status)"
    Write-Host "Device ID: $($response.deviceId)"
    Write-Host "Last seen: $($response.lastSeenAtUtc)"
    Write-Host ''
    Write-Host 'SAVE THIS TOKEN SECURELY:' -ForegroundColor Yellow
    Write-Host $token -ForegroundColor Yellow
}
catch {
    Write-Host "Provisioning failed: $($_.Exception.Message)" -ForegroundColor Red

    if ($inserted) {
        $revokeSql = @"
UPDATE device_credentials
SET "RevokedAtUtc" = CURRENT_TIMESTAMP
WHERE "Id" = '$credentialId';
"@

        [System.IO.File]::WriteAllText(
            $tempSql,
            $revokeSql,
            (New-Object System.Text.UTF8Encoding($false))
        )

        docker cp $tempSql 'remotepro-postgres:/tmp/remotepro-provision.sql' | Out-Null
        docker exec remotepro-postgres psql `
            -v ON_ERROR_STOP=1 `
            -U remotepro_app -d remotepro `
            -f /tmp/remotepro-provision.sql

        Write-Host 'Credential revocation attempted.' -ForegroundColor Yellow
    }

    Write-Host 'Do not share any token or password.' -ForegroundColor Yellow
}
finally {
    Remove-Item $tempSql -Force -ErrorAction SilentlyContinue
    docker exec remotepro-postgres rm -f /tmp/remotepro-provision.sql /tmp/remotepro-check.sql 2>$null | Out-Null

    $token = $null
    $bytes = $null
    $hashBytes = $null
    $body = $null
}