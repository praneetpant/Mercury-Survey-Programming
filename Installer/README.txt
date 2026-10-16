Mercury Survey Programming Installer

Run "Mercury Survey Programming Setup.exe" to install the app for the current Windows user.

The installer copies the app to:
%LOCALAPPDATA%\Programs\Mercury Survey Programming

It creates shortcuts on the Desktop and Start Menu.
It also registers Mercury Survey Programming in Windows Add/Remove Programs
for the current user.

The app requires .NET Framework 4.8 or newer. The setup EXE bundles the
official Microsoft .NET Framework 4.8 offline runtime installer and runs it
automatically if the target PC needs it.

The installed application is a single EXE. Default repository/template
configuration and the logo are embedded inside the EXE. To override template
configuration later, place a repositories.json file beside the installed EXE.

Git for Windows is not required. Template downloads and uploads use the GitHub
API with the saved GitHub token.

If Installer\Shared\repositories.json exists when the installer is built, setup
bundles and installs it beside the app EXE as the initial shared repository
configuration. Superusers can update GitHub tokens from the Super User tab; the
app saves them in shared encrypted form and uploads repositories.json to GitHub.

If Installer\Shared\bootstrap-token.txt exists when the installer is built,
setup bundles and installs it beside the app EXE. Startup uses that token only
when repositories.json does not already contain a usable token, downloads the
latest shared repositories.json from GitHub, and then removes the bootstrap
token file after a successful token-bearing sync.

This installer does not use ClickOnce signing, so it avoids ClickOnce digital
signature/manifest validation errors. Windows may still show "Unknown publisher"
or SmartScreen warnings until the installer is signed with a trusted
code-signing certificate.

Antivirus / Norton CyberCapture
-------------------------------
Unsigned installer and updater EXEs can be flagged by reputation-based antivirus
systems because each release is a new executable downloaded from the internet.
The durable fix is to sign every release with a trusted Authenticode code-signing
certificate.

build-installer.ps1 supports automatic signing when these environment variables
are set before building:

MERCURY_SIGN_CERT_PATH       Path to a .pfx code-signing certificate.
MERCURY_SIGN_CERT_PASSWORD   Password for the .pfx certificate.
MERCURY_SIGN_CERT_SHA1       Optional certificate thumbprint from the Windows cert store.
MERCURY_SIGN_CERT_STORE      Set to machine when using a LocalMachine cert store thumbprint.
MERCURY_SIGN_TIMESTAMP_URL   Optional timestamp server. Defaults to DigiCert.
MERCURY_SIGNTOOL_PATH        Optional explicit path to signtool.exe.

When signing is configured, the build signs both Mercury Survey Programming.exe
and Mercury Survey Programming Setup.exe. Without a trusted certificate, the
build still works but antivirus reputation warnings may continue until Norton
has seen enough clean installs or the file is submitted to Norton for review.
