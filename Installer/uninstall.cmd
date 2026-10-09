@echo off
setlocal

set "TARGET=%LOCALAPPDATA%\Programs\Mercury Survey Programming"
set "START_MENU=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Mercury Survey Programming"
set "DESKTOP_LINK=%USERPROFILE%\Desktop\Mercury Survey Programming.lnk"
set "QUIET="

if /I "%~1"=="/quiet" set "QUIET=1"

if exist "%DESKTOP_LINK%" del "%DESKTOP_LINK%"
if exist "%START_MENU%" rmdir /S /Q "%START_MENU%"
if exist "%TARGET%" rmdir /S /Q "%TARGET%"

reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\Mercury Survey Programming" /f >nul 2>nul

if not defined QUIET (
    echo Mercury Survey Programming has been removed from this Windows user profile.
    pause
)
