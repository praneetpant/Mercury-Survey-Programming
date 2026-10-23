$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$projectOutput = Join-Path $root 'Mercury Survey Programming\bin\Release'
$sourceSetup = Join-Path $PSScriptRoot 'Package'
$outputDir = Join-Path $PSScriptRoot 'Output'
$setupExe = Join-Path $outputDir 'Mercury Survey Programming Setup.exe'
$redist = Join-Path $PSScriptRoot 'Redist\NDP48-x86-x64-AllOS-ENU.exe'
$sharedRepositoriesJson = Join-Path $PSScriptRoot 'Shared\repositories.json'
$sharedConfigurationTokenFile = Join-Path $PSScriptRoot 'Shared\shared-config-token.txt'
$githubAppConfigFile = Join-Path $PSScriptRoot 'Shared\github-app.json'
$includeSharedConfigurationToken = $env:MERCURY_INCLUDE_SHARED_TOKEN -eq '1'

function Get-SignToolPath {
    if (-not [string]::IsNullOrWhiteSpace($env:MERCURY_SIGNTOOL_PATH) -and (Test-Path -LiteralPath $env:MERCURY_SIGNTOOL_PATH)) {
        return $env:MERCURY_SIGNTOOL_PATH
    }

    $command = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $kitRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    if (Test-Path -LiteralPath $kitRoot) {
        $candidate = Get-ChildItem -LiteralPath $kitRoot -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
            Sort-Object FullName -Descending |
            Select-Object -First 1

        if ($null -ne $candidate) {
            return $candidate.FullName
        }
    }

    return $null
}

function Invoke-CodeSign {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Description
    )

    $certPath = $env:MERCURY_SIGN_CERT_PATH
    $certPassword = $env:MERCURY_SIGN_CERT_PASSWORD
    $certThumbprint = $env:MERCURY_SIGN_CERT_SHA1
    $timestampUrl = $env:MERCURY_SIGN_TIMESTAMP_URL

    if ([string]::IsNullOrWhiteSpace($timestampUrl)) {
        $timestampUrl = 'http://timestamp.digicert.com'
    }

    if ([string]::IsNullOrWhiteSpace($certPath) -and [string]::IsNullOrWhiteSpace($certThumbprint)) {
        Write-Warning "Code signing skipped for $Description. Set MERCURY_SIGN_CERT_PATH or MERCURY_SIGN_CERT_SHA1 to sign release files."
        return
    }

    $signTool = Get-SignToolPath
    if ([string]::IsNullOrWhiteSpace($signTool)) {
        throw 'signtool.exe was not found. Install the Windows SDK or set MERCURY_SIGNTOOL_PATH.'
    }

    $arguments = @('sign', '/fd', 'SHA256', '/tr', $timestampUrl, '/td', 'SHA256')

    if (-not [string]::IsNullOrWhiteSpace($certPath)) {
        if (-not (Test-Path -LiteralPath $certPath)) {
            throw "Signing certificate was not found: $certPath"
        }

        $arguments += @('/f', $certPath)
        if (-not [string]::IsNullOrWhiteSpace($certPassword)) {
            $arguments += @('/p', $certPassword)
        }
    } else {
        $arguments += @('/sha1', $certThumbprint)
        if ($env:MERCURY_SIGN_CERT_STORE -eq 'machine') {
            $arguments += '/sm'
        }
    }

    $arguments += $Path
    & $signTool @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Code signing failed for $Description."
    }
}

if (-not (Test-Path -LiteralPath (Join-Path $projectOutput 'Mercury Survey Programming.exe'))) {
    throw 'Release build output was not found. Build the solution in Release mode first.'
}

Invoke-CodeSign -Path (Join-Path $projectOutput 'Mercury Survey Programming.exe') -Description 'application EXE'

if (Test-Path -LiteralPath $sourceSetup) {
    Remove-Item -LiteralPath $sourceSetup -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $sourceSetup | Out-Null
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'install.cmd') -Destination (Join-Path $sourceSetup 'install.cmd') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'install-dotnet48.ps1') -Destination (Join-Path $sourceSetup 'install-dotnet48.ps1') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'uninstall.cmd') -Destination (Join-Path $sourceSetup 'uninstall.cmd') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.txt') -Destination (Join-Path $sourceSetup 'README.txt') -Force
Copy-Item -LiteralPath (Join-Path $projectOutput 'Mercury Survey Programming.exe') -Destination (Join-Path $sourceSetup 'Mercury Survey Programming.exe') -Force

if (Test-Path -LiteralPath $sharedRepositoriesJson) {
    Copy-Item -LiteralPath $sharedRepositoriesJson -Destination (Join-Path $sourceSetup 'repositories.json') -Force
}

if ($includeSharedConfigurationToken -and (Test-Path -LiteralPath $sharedConfigurationTokenFile)) {
    Copy-Item -LiteralPath $sharedConfigurationTokenFile -Destination (Join-Path $sourceSetup 'shared-config-token.txt') -Force
} else {
    Set-Content -LiteralPath (Join-Path $sourceSetup 'shared-config-token.txt') -Value '' -Encoding ASCII
}

if (Test-Path -LiteralPath $githubAppConfigFile) {
    Copy-Item -LiteralPath $githubAppConfigFile -Destination (Join-Path $sourceSetup 'github-app.json') -Force
}

if (Test-Path -LiteralPath $redist) {
    Copy-Item -LiteralPath $redist -Destination (Join-Path $sourceSetup 'NDP48-x86-x64-AllOS-ENU.exe') -Force
}

$tempRoot = Join-Path $env:TEMP 'MercuryInstallerBuild'
$tempPackage = Join-Path $tempRoot 'Package'
$tempOutput = Join-Path $tempRoot 'Output'
$tempSed = Join-Path $tempRoot 'setup.sed'
$tempSetup = Join-Path $tempOutput 'MercurySetup.exe'

if (Test-Path -LiteralPath $tempRoot) {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force
}

Copy-Item -LiteralPath $sourceSetup -Destination $tempPackage -Recurse -Force
New-Item -ItemType Directory -Force -Path $tempOutput | Out-Null

$expectedMinimumSize = [Math]::Min(
    100MB,
    [Math]::Max(1MB, (Get-ChildItem -File -LiteralPath $tempPackage | Measure-Object -Property Length -Sum).Sum * 0.75))

$sedContent = @"
[Version]
Class=IEXPRESS
SEDVersion=3
[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=1
HideExtractAnimation=0
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=%InstallPrompt%
DisplayLicense=%DisplayLicense%
FinishMessage=%FinishMessage%
TargetName=%TargetName%
FriendlyName=%FriendlyName%
AppLaunched=%AppLaunched%
PostInstallCmd=<None>
AdminQuietInstCmd=
UserQuietInstCmd=
SourceFiles=SourceFiles
[Strings]
InstallPrompt=Install Mercury Survey Programming for the current Windows user?
DisplayLicense=
FinishMessage=Mercury Survey Programming installed successfully.
TargetName=$tempSetup
FriendlyName=Mercury Survey Programming Setup
AppLaunched=install.cmd
FILE0="install.cmd"
FILE1="install-dotnet48.ps1"
FILE2="uninstall.cmd"
FILE3="README.txt"
FILE4="Mercury Survey Programming.exe"
FILE5="repositories.json"
FILE6="shared-config-token.txt"
FILE7="NDP48-x86-x64-AllOS-ENU.exe"
FILE8="github-app.json"
[SourceFiles]
SourceFiles0=$tempPackage\
[SourceFiles0]
%FILE0%=
%FILE1%=
%FILE2%=
%FILE3%=
%FILE4%=
%FILE5%=
%FILE6%=
%FILE7%=
%FILE8%=
"@

Set-Content -LiteralPath $tempSed -Value $sedContent -Encoding ASCII
& "$env:WINDIR\System32\iexpress.exe" /N /Q $tempSed

for ($attempt = 0; $attempt -lt 240; $attempt++) {
    if (Test-Path -LiteralPath $tempSetup) {
        $currentSize = (Get-Item -LiteralPath $tempSetup).Length
        if ($currentSize -ge $expectedMinimumSize) {
            break
        }
    }

    Start-Sleep -Milliseconds 500
}

if (-not (Test-Path -LiteralPath $tempSetup) -or (Get-Item -LiteralPath $tempSetup).Length -lt $expectedMinimumSize) {
    throw 'IExpress did not create the setup EXE.'
}

Copy-Item -LiteralPath $tempSetup -Destination $setupExe -Force
Invoke-CodeSign -Path $setupExe -Description 'setup EXE'
Get-Item -LiteralPath $setupExe
