$ErrorActionPreference = 'Stop'

$requiredRelease = 528040
$redistName = 'NDP48-x86-x64-AllOS-ENU.exe'
$redistPath = Join-Path $PSScriptRoot $redistName

function Get-Net48Release {
    $key = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -ErrorAction SilentlyContinue
    if ($null -eq $key -or $null -eq $key.Release) {
        return 0
    }

    return [int]$key.Release
}

function Show-InstallerMessage($title, $message, $icon) {
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.MessageBox]::Show($message, $title, 0, $icon) | Out-Null
}

if ((Get-Net48Release) -ge $requiredRelease) {
    exit 0
}

if (-not (Test-Path -LiteralPath $redistPath)) {
    Show-InstallerMessage 'Missing .NET Framework' '.NET Framework 4.8 is required, but the bundled Microsoft installer was not found.' 16
    exit 1
}

Show-InstallerMessage 'Installing .NET Framework 4.8' 'This PC needs .NET Framework 4.8. The Microsoft installer will run now. Please approve the Windows prompt if it appears.' 64

$process = Start-Process -FilePath $redistPath -ArgumentList '/q /norestart' -Verb RunAs -Wait -PassThru
$exitCode = $process.ExitCode

if ($exitCode -ne 0 -and $exitCode -ne 3010 -and $exitCode -ne 1641) {
    Show-InstallerMessage 'Install Failed' ('.NET Framework 4.8 installer failed with exit code ' + $exitCode + '.') 16
    exit $exitCode
}

if ((Get-Net48Release) -lt $requiredRelease) {
    Show-InstallerMessage 'Restart Required' '.NET Framework 4.8 was installed or updated, but Windows needs a restart before Mercury Survey Programming can run. Please restart this PC and run setup again.' 48
    exit 3010
}

exit 0
