$ErrorActionPreference = 'Stop'

$repoUrl = 'https://github.com/praneetpant/Mercury-Survey-Programming.git'
$root = Split-Path -Parent $PSScriptRoot
$tokenPath = Join-Path $PSScriptRoot 'Shared\source-repo-token.txt'

function Get-SourceRepositoryToken {
    if (-not (Test-Path -LiteralPath $tokenPath)) {
        throw 'Source repository token file was not found. Save it to Installer\Shared\source-repo-token.txt first.'
    }

    $encrypted = (Get-Content -Raw -LiteralPath $tokenPath).Trim()
    if ([string]::IsNullOrWhiteSpace($encrypted)) {
        throw 'Source repository token file is empty.'
    }

    $secure = ConvertTo-SecureString $encrypted
    $credential = New-Object System.Management.Automation.PSCredential('token', $secure)
    return $credential.GetNetworkCredential().Password
}

Push-Location $root
try {
    $token = Get-SourceRepositoryToken
    $basic = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("x-access-token:$token"))

    $remoteNames = git remote
    if ($remoteNames -contains 'origin') {
        git remote set-url origin $repoUrl
    } else {
        git remote add origin $repoUrl
    }

    git add -A
    $pending = git status --short
    if ($pending) {
        $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
        git commit -m "Update Mercury Survey Programming source $timestamp"
    }

    git -c "http.https://github.com/.extraheader=AUTHORIZATION: basic $basic" push -u origin master
} finally {
    Pop-Location
}
