@echo off
setlocal

set "APP_NAME=Mercury Survey Programming"
set "APP_EXE=Mercury Survey Programming.exe"
set "SOURCE=%~dp0app"
set "TARGET=%LOCALAPPDATA%\Programs\Mercury Survey Programming"
set "START_MENU=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Mercury Survey Programming"
set "DESKTOP=%USERPROFILE%\Desktop"

if not exist "%SOURCE%\%APP_EXE%" set "SOURCE=%~dp0"

if not exist "%SOURCE%\%APP_EXE%" (
    echo Installer files are incomplete. Could not find "%SOURCE%\%APP_EXE%".
    pause
    exit /b 1
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SOURCE%\install-dotnet48.ps1"
if errorlevel 1 exit /b 1

if not exist "%TARGET%" mkdir "%TARGET%"
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$target=Join-Path $env:LOCALAPPDATA 'Programs\Mercury Survey Programming\Mercury Survey Programming.exe'; $deadline=(Get-Date).AddSeconds(60); do { $running=@(Get-Process -Name 'Mercury Survey Programming' -ErrorAction SilentlyContinue | Where-Object { $_.Path -and ([string]::Equals($_.Path, $target, [System.StringComparison]::OrdinalIgnoreCase)) }); if ($running.Count -eq 0) { exit 0 }; Start-Sleep -Seconds 1 } while ((Get-Date) -lt $deadline); exit 0"
copy /Y "%SOURCE%\Mercury Survey Programming.exe" "%TARGET%\" >nul
copy /Y "%SOURCE%\uninstall.cmd" "%TARGET%\" >nul
if exist "%SOURCE%\Mercury Survey Programming.xml" copy /Y "%SOURCE%\Mercury Survey Programming.xml" "%TARGET%\" >nul
if exist "%SOURCE%\repositories.json" copy /Y "%SOURCE%\repositories.json" "%TARGET%\" >nul
if exist "%SOURCE%\shared-config-token.txt" copy /Y "%SOURCE%\shared-config-token.txt" "%TARGET%\" >nul
if exist "%SOURCE%\Assets\MercuryLogo.ico" if not exist "%TARGET%\Assets" mkdir "%TARGET%\Assets"
if exist "%SOURCE%\Assets\MercuryLogo.ico" copy /Y "%SOURCE%\Assets\MercuryLogo.ico" "%TARGET%\Assets\" >nul
if exist "%SOURCE%\Assets\MercuryLogo.png" if not exist "%TARGET%\Assets" mkdir "%TARGET%\Assets"
if exist "%SOURCE%\Assets\MercuryLogo.png" copy /Y "%SOURCE%\Assets\MercuryLogo.png" "%TARGET%\Assets\" >nul
if exist "%SOURCE%\MercuryLogo.ico" if not exist "%TARGET%\Assets" mkdir "%TARGET%\Assets"
if exist "%SOURCE%\MercuryLogo.ico" copy /Y "%SOURCE%\MercuryLogo.ico" "%TARGET%\Assets\" >nul
if exist "%SOURCE%\MercuryLogo.png" if not exist "%TARGET%\Assets" mkdir "%TARGET%\Assets"
if exist "%SOURCE%\MercuryLogo.png" copy /Y "%SOURCE%\MercuryLogo.png" "%TARGET%\Assets\" >nul
if errorlevel 1 (
    echo Installation failed while copying files to "%TARGET%".
    pause
    exit /b 1
)

if not exist "%START_MENU%" mkdir "%START_MENU%"

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$shell=New-Object -ComObject WScript.Shell; $target=Join-Path $env:LOCALAPPDATA 'Programs\Mercury Survey Programming\Mercury Survey Programming.exe'; $icon=$target; $startDir=Split-Path $target; $desktop=Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'Mercury Survey Programming.lnk'; $startMenu=Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Mercury Survey Programming\Mercury Survey Programming.lnk'; foreach ($path in @($desktop,$startMenu)) { $shortcut=$shell.CreateShortcut($path); $shortcut.TargetPath=$target; $shortcut.WorkingDirectory=$startDir; $shortcut.IconLocation=$icon; $shortcut.Save() }"

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$installDir=Join-Path $env:LOCALAPPDATA 'Programs\Mercury Survey Programming'; $exe=Join-Path $installDir 'Mercury Survey Programming.exe'; $version=(Get-Item $exe).VersionInfo.FileVersion; $uninstall=Join-Path $installDir 'uninstall.cmd'; $quotedUninstall=[char]34 + $uninstall + [char]34; $key='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Mercury Survey Programming'; New-Item -Path $key -Force | Out-Null; New-ItemProperty -Path $key -Name DisplayName -Value 'Mercury Survey Programming' -PropertyType String -Force | Out-Null; New-ItemProperty -Path $key -Name DisplayVersion -Value $version -PropertyType String -Force | Out-Null; New-ItemProperty -Path $key -Name Publisher -Value 'Mercury Analytics' -PropertyType String -Force | Out-Null; New-ItemProperty -Path $key -Name InstallLocation -Value $installDir -PropertyType String -Force | Out-Null; New-ItemProperty -Path $key -Name DisplayIcon -Value $exe -PropertyType String -Force | Out-Null; New-ItemProperty -Path $key -Name UninstallString -Value ('cmd.exe /c ' + $quotedUninstall) -PropertyType String -Force | Out-Null; New-ItemProperty -Path $key -Name QuietUninstallString -Value ('cmd.exe /c ' + $quotedUninstall + ' /quiet') -PropertyType String -Force | Out-Null; New-ItemProperty -Path $key -Name NoModify -Value 1 -PropertyType DWord -Force | Out-Null; New-ItemProperty -Path $key -Name NoRepair -Value 1 -PropertyType DWord -Force | Out-Null; New-ItemProperty -Path $key -Name EstimatedSize -Value ([int]([Math]::Ceiling((Get-Item $exe).Length / 1KB))) -PropertyType DWord -Force | Out-Null; New-ItemProperty -Path $key -Name InstallDate -Value (Get-Date -Format 'yyyyMMdd') -PropertyType String -Force | Out-Null"

start "" "%TARGET%\%APP_EXE%"
exit /b 0
