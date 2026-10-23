$ErrorActionPreference = 'Stop'

function ConvertTo-Base64Url {
    param([byte[]]$Bytes)

    return [System.Convert]::ToBase64String($Bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function Read-AsnLength {
    param([byte[]]$Data, [ref]$Offset)

    $length = [int]$Data[$Offset.Value]
    $Offset.Value += 1

    if (($length -band 0x80) -eq 0) {
        return $length
    }

    $byteCount = $length -band 0x7F
    $length = 0
    for ($i = 0; $i -lt $byteCount; $i++) {
        $length = ($length -shl 8) -bor [int]$Data[$Offset.Value]
        $Offset.Value += 1
    }

    return $length
}

function Read-AsnValue {
    param([byte[]]$Data, [ref]$Offset, [byte]$ExpectedTag)

    $tag = $Data[$Offset.Value]
    $Offset.Value += 1
    if ($tag -ne $ExpectedTag) {
        throw ("Unexpected ASN.1 tag. Expected 0x{0:X2}, got 0x{1:X2}." -f $ExpectedTag, $tag)
    }

    $length = Read-AsnLength -Data $Data -Offset $Offset
    $value = New-Object byte[] $length
    [System.Array]::Copy($Data, $Offset.Value, $value, 0, $length)
    $Offset.Value += $length
    return $value
}

function Read-AsnInteger {
    param([byte[]]$Data, [ref]$Offset)

    $value = Read-AsnValue -Data $Data -Offset $Offset -ExpectedTag 0x02
    while ($value.Length -gt 1 -and $value[0] -eq 0) {
        $trimmed = New-Object byte[] ($value.Length - 1)
        [System.Array]::Copy($value, 1, $trimmed, 0, $trimmed.Length)
        $value = $trimmed
    }

    return $value
}

function Import-RsaPrivateKeyFromPem {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "GitHub App private key file was not found: $Path"
    }

    $pem = Get-Content -Raw -LiteralPath $Path
    $isPkcs8 = $pem -match '-----BEGIN PRIVATE KEY-----'
    $isPkcs1 = $pem -match '-----BEGIN RSA PRIVATE KEY-----'

    if (-not $isPkcs8 -and -not $isPkcs1) {
        throw 'Unsupported private key format. Expected BEGIN PRIVATE KEY or BEGIN RSA PRIVATE KEY.'
    }

    $base64 = ($pem -replace '-----BEGIN [A-Z ]+-----', '' -replace '-----END [A-Z ]+-----', '' -replace '\s', '')
    $der = [System.Convert]::FromBase64String($base64)

    if ($isPkcs8) {
        $outerOffset = 0
        $outer = Read-AsnValue -Data $der -Offset ([ref]$outerOffset) -ExpectedTag 0x30
        $offset = 0
        [void](Read-AsnInteger -Data $outer -Offset ([ref]$offset))
        [void](Read-AsnValue -Data $outer -Offset ([ref]$offset) -ExpectedTag 0x30)
        $der = Read-AsnValue -Data $outer -Offset ([ref]$offset) -ExpectedTag 0x04
    }

    $sequenceOffset = 0
    $sequence = Read-AsnValue -Data $der -Offset ([ref]$sequenceOffset) -ExpectedTag 0x30
    $offset = 0

    [void](Read-AsnInteger -Data $sequence -Offset ([ref]$offset))
    $parameters = New-Object System.Security.Cryptography.RSAParameters
    $parameters.Modulus = Read-AsnInteger -Data $sequence -Offset ([ref]$offset)
    $parameters.Exponent = Read-AsnInteger -Data $sequence -Offset ([ref]$offset)
    $parameters.D = Read-AsnInteger -Data $sequence -Offset ([ref]$offset)
    $parameters.P = Read-AsnInteger -Data $sequence -Offset ([ref]$offset)
    $parameters.Q = Read-AsnInteger -Data $sequence -Offset ([ref]$offset)
    $parameters.DP = Read-AsnInteger -Data $sequence -Offset ([ref]$offset)
    $parameters.DQ = Read-AsnInteger -Data $sequence -Offset ([ref]$offset)
    $parameters.InverseQ = Read-AsnInteger -Data $sequence -Offset ([ref]$offset)

    $rsa = New-Object System.Security.Cryptography.RSACryptoServiceProvider
    $rsa.PersistKeyInCsp = $false
    $rsa.ImportParameters($parameters)
    return $rsa
}

function New-GitHubAppJwt {
    param(
        [Parameter(Mandatory = $true)][string]$AppId,
        [Parameter(Mandatory = $true)][string]$PrivateKeyPath
    )

    $now = [DateTimeOffset]::UtcNow
    $header = '{"alg":"RS256","typ":"JWT"}'
    $payload = @{
        iat = [int]$now.AddSeconds(-60).ToUnixTimeSeconds()
        exp = [int]$now.AddMinutes(9).ToUnixTimeSeconds()
        iss = $AppId
    } | ConvertTo-Json -Compress

    $headerPart = ConvertTo-Base64Url -Bytes ([System.Text.Encoding]::UTF8.GetBytes($header))
    $payloadPart = ConvertTo-Base64Url -Bytes ([System.Text.Encoding]::UTF8.GetBytes($payload))
    $unsignedToken = "$headerPart.$payloadPart"

    $rsa = Import-RsaPrivateKeyFromPem -Path $PrivateKeyPath
    try {
        $signature = $rsa.SignData(
            [System.Text.Encoding]::ASCII.GetBytes($unsignedToken),
            [System.Security.Cryptography.CryptoConfig]::MapNameToOID('SHA256'))
    } finally {
        $rsa.Dispose()
    }

    return "$unsignedToken.$(ConvertTo-Base64Url -Bytes $signature)"
}

function Get-GitHubAppConfig {
    param([string]$ConfigPath)

    if (-not (Test-Path -LiteralPath $ConfigPath)) {
        return $null
    }

    return Get-Content -Raw -LiteralPath $ConfigPath | ConvertFrom-Json
}

function Get-GitHubAppInstallationToken {
    param([string]$ConfigPath)

    $config = Get-GitHubAppConfig -ConfigPath $ConfigPath
    if ($null -eq $config) {
        return $null
    }

    if ([string]::IsNullOrWhiteSpace([string]$config.appId) -or
        [string]::IsNullOrWhiteSpace([string]$config.installationId) -or
        [string]::IsNullOrWhiteSpace([string]$config.privateKeyPath)) {
        throw "GitHub App config is incomplete: $ConfigPath"
    }

    $jwt = New-GitHubAppJwt -AppId ([string]$config.appId) -PrivateKeyPath ([string]$config.privateKeyPath)
    $headers = @{
        Authorization = "Bearer $jwt"
        Accept = 'application/vnd.github+json'
        'X-GitHub-Api-Version' = '2022-11-28'
        'User-Agent' = 'MercurySurveyProgrammingGitHubApp'
    }

    $tokenResponse = Invoke-RestMethod `
        -Method Post `
        -Uri "https://api.github.com/app/installations/$($config.installationId)/access_tokens" `
        -Headers $headers

    return $tokenResponse.token
}
