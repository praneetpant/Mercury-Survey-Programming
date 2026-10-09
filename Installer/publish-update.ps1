$ErrorActionPreference = 'Stop'

$owner = 'praneetpant'
$repo = 'setupshared'
$branch = 'main'
$tag = 'v1.0.2'
$releaseName = 'Mercury Survey Programming v1.0.2'
$setupAssetName = 'setup-v1.0.2.exe'
$exeAssetName = 'Mercury Survey Programming v1.0.2.exe'
$root = Split-Path -Parent $PSScriptRoot
$setupPath = Join-Path $PSScriptRoot 'Output\Mercury Survey Programming Setup.exe'
$exePath = Join-Path $root 'Mercury Survey Programming\bin\Release\Mercury Survey Programming.exe'
$manifestPath = Join-Path $PSScriptRoot 'Shared\app-update.json'
$tokenPath = Join-Path $PSScriptRoot 'Shared\shared-config-token.txt'

function ConvertFrom-SharedProtectedValue {
    param([string]$Value)

    $prefix = 'shared:v1:'
    if ([string]::IsNullOrWhiteSpace($Value) -or -not $Value.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $Value
    }

    $purpose = 'Mercury Survey Programming shared repository token configuration'
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $key = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($purpose))
    $allBytes = [System.Convert]::FromBase64String($Value.Substring($prefix.Length))
    $iv = New-Object byte[] 16
    [System.Array]::Copy($allBytes, 0, $iv, 0, 16)
    $cipher = New-Object byte[] ($allBytes.Length - 16)
    [System.Array]::Copy($allBytes, 16, $cipher, 0, $cipher.Length)

    $aes = [System.Security.Cryptography.Aes]::Create()
    $aes.Mode = [System.Security.Cryptography.CipherMode]::CBC
    $aes.Padding = [System.Security.Cryptography.PaddingMode]::PKCS7
    $aes.Key = $key
    $aes.IV = $iv

    $decryptor = $aes.CreateDecryptor()
    $plainBytes = $decryptor.TransformFinalBlock($cipher, 0, $cipher.Length)
    return [System.Text.Encoding]::UTF8.GetString($plainBytes)
}

function Get-SharedToken {
    if (-not (Test-Path -LiteralPath $tokenPath)) {
        throw 'Shared GitHub token file was not found.'
    }

    $raw = (Get-Content -Raw -LiteralPath $tokenPath).Trim()
    if ([string]::IsNullOrWhiteSpace($raw)) {
        throw 'Shared GitHub token file is empty.'
    }

    $values = @{}
    foreach ($line in $raw -split "`r?`n") {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith('#')) {
            continue
        }

        $separator = $trimmed.IndexOf('=')
        if ($separator -gt 0) {
            $values[$trimmed.Substring(0, $separator).Trim()] = $trimmed.Substring($separator + 1).Trim()
        }
    }

    foreach ($key in @('githubTokenEncrypted', 'githubToken', 'token')) {
        if ($values.ContainsKey($key)) {
            return (ConvertFrom-SharedProtectedValue $values[$key]).Trim()
        }
    }

    return (ConvertFrom-SharedProtectedValue (($raw -split "`r?`n")[0].Trim())).Trim()
}

function Invoke-GitHubJson {
    param(
        [string]$Method,
        [string]$Uri,
        $Body = $null
    )

    $parameters = @{
        Method = $Method
        Uri = $Uri
        Headers = $script:headers
    }

    if ($null -ne $Body) {
        $parameters.Body = ($Body | ConvertTo-Json -Depth 20)
        $parameters.ContentType = 'application/json'
    }

    return Invoke-RestMethod @parameters
}

function Get-GitHubRelease {
    try {
        return Invoke-GitHubJson -Method Get -Uri "https://api.github.com/repos/$owner/$repo/releases/tags/$tag"
    } catch {
        if ($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -eq 404) {
            return $null
        }

        throw
    }
}

function Remove-ExistingAsset {
    param($Release, [string]$Name)

    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        $assets = Invoke-GitHubJson -Method Get -Uri "https://api.github.com/repos/$owner/$repo/releases/$($Release.id)/assets?per_page=100"
        $matches = @($assets | Where-Object { $_.name -eq $Name })

        if ($matches.Count -eq 0) {
            return
        }

        foreach ($asset in $matches) {
            Invoke-GitHubJson -Method Delete -Uri "https://api.github.com/repos/$owner/$repo/releases/assets/$($asset.id)" | Out-Null
        }

        Start-Sleep -Seconds 2
    }

    throw "GitHub did not remove release asset '$Name' in time."
}

function Add-ReleaseAsset {
    param($Release, [string]$Path, [string]$Name)

    $escapedName = [System.Uri]::EscapeDataString($Name)
    $uploadUrl = "https://uploads.github.com/repos/$owner/$repo/releases/$($Release.id)/assets?name=$escapedName"
    Invoke-RestMethod -Method Post -Uri $uploadUrl -Headers $script:headers -ContentType 'application/octet-stream' -InFile $Path | Out-Null
}

function Update-RepositoryFile {
    param([string]$RepositoryPath, [string]$LocalPath, [string]$Message)

    $sha = $null
    try {
        $existing = Invoke-GitHubJson -Method Get -Uri "https://api.github.com/repos/$owner/$repo/contents/$RepositoryPath`?ref=$branch"
        $sha = $existing.sha
    } catch {
        if (-not ($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -eq 404)) {
            throw
        }
    }

    $content = [System.Convert]::ToBase64String([System.IO.File]::ReadAllBytes($LocalPath))
    $body = @{
        message = $Message
        content = $content
        branch = $branch
    }

    if ($sha) {
        $body.sha = $sha
    }

    Invoke-GitHubJson -Method Put -Uri "https://api.github.com/repos/$owner/$repo/contents/$RepositoryPath" -Body $body | Out-Null
}

if (-not (Test-Path -LiteralPath $setupPath)) {
    throw 'Setup EXE was not found. Build the installer first.'
}

if (-not (Test-Path -LiteralPath $exePath)) {
    throw 'Release app EXE was not found. Build the app first.'
}

if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw 'Update manifest was not found.'
}

$token = Get-SharedToken
if ([string]::IsNullOrWhiteSpace($token)) {
    throw 'Shared GitHub token could not be read.'
}

$script:headers = @{
    Authorization = "Bearer $token"
    Accept = 'application/vnd.github+json'
    'X-GitHub-Api-Version' = '2022-11-28'
    'User-Agent' = 'MercurySurveyProgrammingReleasePublisher'
}

$release = Get-GitHubRelease
if ($null -eq $release) {
    $release = Invoke-GitHubJson -Method Post -Uri "https://api.github.com/repos/$owner/$repo/releases" -Body @{
        tag_name = $tag
        target_commitish = $branch
        name = $releaseName
        body = 'Mercury Survey Programming update.'
        draft = $false
        prerelease = $false
    }
} else {
    $release = Invoke-GitHubJson -Method Patch -Uri "https://api.github.com/repos/$owner/$repo/releases/$($release.id)" -Body @{
        name = $releaseName
        body = 'Mercury Survey Programming update.'
        draft = $false
        prerelease = $false
    }
}

Remove-ExistingAsset -Release $release -Name $setupAssetName
Remove-ExistingAsset -Release $release -Name $exeAssetName
Add-ReleaseAsset -Release $release -Path $setupPath -Name $setupAssetName
Add-ReleaseAsset -Release $release -Path $exePath -Name $exeAssetName
Update-RepositoryFile -RepositoryPath 'app-update.json' -LocalPath $manifestPath -Message 'Update app auto-update manifest to v1.0.2'

[pscustomobject]@{
    Release = $release.html_url
    SetupAsset = $setupAssetName
    ExeAsset = $exeAssetName
    Manifest = 'app-update.json'
}
