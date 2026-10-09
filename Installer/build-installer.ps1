$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$projectOutput = Join-Path $root 'Mercury Survey Programming\bin\Release'
$sourceSetup = Join-Path $PSScriptRoot 'Package'
$outputDir = Join-Path $PSScriptRoot 'Output'
$setupExe = Join-Path $outputDir 'Mercury Survey Programming Setup.exe'
$redist = Join-Path $PSScriptRoot 'Redist\NDP48-x86-x64-AllOS-ENU.exe'
$sharedRepositoriesJson = Join-Path $PSScriptRoot 'Shared\repositories.json'
$sharedConfigurationTokenFile = Join-Path $PSScriptRoot 'Shared\shared-config-token.txt'
$includeSharedConfigurationToken = $env:MERCURY_INCLUDE_SHARED_TOKEN -eq '1'

if (-not (Test-Path -LiteralPath (Join-Path $projectOutput 'Mercury Survey Programming.exe'))) {
    throw 'Release build output was not found. Build the solution in Release mode first.'
}

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
Get-Item -LiteralPath $setupExe
