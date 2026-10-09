Imports System
Imports System.Diagnostics
Imports System.IO
Imports System.Net
Imports System.Net.Http
Imports System.Reflection
Imports System.Security.Cryptography
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading.Tasks
Imports System.Drawing
Imports System.Windows.Forms

Public NotInheritable Class AppUpdater
    Private Const UpdateManifestUrl As String = "https://raw.githubusercontent.com/praneetpant/setupshared/main/app-update.json"
    Private Const UpdateManifestApiUrl As String = "https://api.github.com/repos/praneetpant/setupshared/contents/app-update.json?ref=main"
    Private Shared ReadOnly Http As New HttpClient() With {.Timeout = TimeSpan.FromSeconds(30)}

    Shared Sub New()
        ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol Or SecurityProtocolType.Tls12
    End Sub

    Private Sub New()
    End Sub

    Public Shared Function CheckAndRunUpdateIfAvailable(owner As IWin32Window, Optional notifyWhenCurrent As Boolean = False) As Boolean
        Return CheckAndRunUpdateIfAvailableAsync(owner, notifyWhenCurrent).GetAwaiter().GetResult()
    End Function

    Public Shared Async Function CheckAndRunUpdateIfAvailableAsync(owner As IWin32Window, Optional notifyWhenCurrent As Boolean = False) As Task(Of Boolean)
        Try
            Dim manifest = Await GetUpdateManifestAsync()
            If manifest Is Nothing OrElse Not manifest.IsUsable Then
                AppLogger.Info("Update check completed. No usable update manifest found.")
                If notifyWhenCurrent Then
                    MessageBox.Show(owner, "No update information is currently available.", "Update Check", MessageBoxButtons.OK, MessageBoxIcon.Information)
                End If

                Return False
            End If

            Dim currentVersion = GetCurrentVersion()
            If Not IsNewerVersion(manifest.Version, currentVersion) Then
                AppLogger.Info("Update check completed. Current version: " & currentVersion.ToString() & "; latest version: " & manifest.Version.ToString())
                If notifyWhenCurrent Then
                    MessageBox.Show(owner, "You are already running the latest version." & Environment.NewLine & Environment.NewLine & "Current version: " & currentVersion.ToString(), "Update Check", MessageBoxButtons.OK, MessageBoxIcon.Information)
                End If

                Return False
            End If

            Dim prompt = "A newer version of Mercury Survey Programming is available." & Environment.NewLine & Environment.NewLine &
                "Current version: " & currentVersion.ToString() & Environment.NewLine &
                "New version: " & manifest.Version.ToString() & Environment.NewLine & Environment.NewLine &
                "Install the update now?"

            If Not String.IsNullOrWhiteSpace(manifest.Notes) Then
                prompt &= Environment.NewLine & Environment.NewLine & manifest.Notes
            End If

            If Not manifest.Required AndAlso MessageBox.Show(owner, prompt, "Update Available", MessageBoxButtons.YesNo, MessageBoxIcon.Information) <> DialogResult.Yes Then
                AppLogger.Info("Update available but user chose not to install it.")
                Return False
            End If

            If manifest.Required Then
                MessageBox.Show(owner, prompt, "Required Update", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End If

            Using progressForm = New UpdateProgressForm(manifest)
                progressForm.ShowDialog(owner)

                If progressForm.UpdateStarted Then
                    AppLogger.Info("Update handoff started. Closing application.")
                    Application.Exit()
                    Return True
                End If

                If progressForm.UpdateException IsNot Nothing Then
                    Throw progressForm.UpdateException
                End If

                Return False
            End Using
        Catch ex As Exception
            AppLogger.Error("Update check failed.", ex)
            If notifyWhenCurrent Then
                MessageBox.Show(owner, "Update check failed:" & Environment.NewLine & ex.Message, "Update Check Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End If

            Return False
        End Try
    End Function

    Private Shared Async Function GetUpdateManifestAsync() As Task(Of UpdateManifest)
        Dim rawFailure As String = String.Empty

        Try
            Dim json = Await FetchStringAsync(BuildRawManifestUrl())
            Dim manifest = ParseManifest(json)
            If manifest IsNot Nothing AndAlso manifest.IsUsable Then
                AppLogger.Info("Update manifest loaded from raw GitHub URL. Latest version: " & manifest.Version.ToString())
                Return manifest
            End If

            rawFailure = "Raw GitHub manifest was found but did not contain usable update data."
        Catch ex As Exception
            rawFailure = ex.Message
            AppLogger.Error("Raw GitHub update manifest check failed.", ex)
        End Try

        Try
            Dim apiJson = Await FetchStringAsync(UpdateManifestApiUrl)
            Dim manifestJson = DecodeGitHubContentResponse(apiJson)
            Dim manifest = ParseManifest(manifestJson)
            If manifest IsNot Nothing AndAlso manifest.IsUsable Then
                AppLogger.Info("Update manifest loaded from GitHub API fallback. Latest version: " & manifest.Version.ToString())
                Return manifest
            End If

            Throw New AppUserMessageException("GitHub API manifest did not contain usable update data.")
        Catch ex As Exception
            AppLogger.Error("GitHub API update manifest fallback failed.", ex)
            Throw New AppUserMessageException("Could not read update information from GitHub." & Environment.NewLine & "Raw check: " & rawFailure & Environment.NewLine & "API check: " & ex.Message)
        End Try
    End Function

    Private Shared Function BuildRawManifestUrl() As String
        Return UpdateManifestUrl & "?cacheBust=" & Uri.EscapeDataString(DateTime.UtcNow.Ticks.ToString())
    End Function

    Private Shared Async Function FetchStringAsync(url As String) As Task(Of String)
        Using request = New HttpRequestMessage(HttpMethod.Get, url)
            request.Headers.UserAgent.ParseAdd("MercurySurveyProgramming/" & GetCurrentVersion().ToString())
            request.Headers.CacheControl = New System.Net.Http.Headers.CacheControlHeaderValue() With {.NoCache = True, .NoStore = True}
            request.Headers.Pragma.ParseAdd("no-cache")

            Using response = Await Http.SendAsync(request)
                If response.StatusCode = HttpStatusCode.NotFound Then
                    Throw New AppUserMessageException("Update manifest was not found.")
                End If

                If Not response.IsSuccessStatusCode Then
                    Throw New AppUserMessageException("HTTP " & CInt(response.StatusCode) & " while reading update manifest.")
                End If

                Return Await response.Content.ReadAsStringAsync()
            End Using
        End Using
    End Function

    Private Shared Function DecodeGitHubContentResponse(apiJson As String) As String
        Dim content = GetJsonString(apiJson, "content")
        Dim contentEncoding = GetJsonString(apiJson, "encoding")

        If String.IsNullOrWhiteSpace(content) Then
            Throw New AppUserMessageException("GitHub API response did not include file content.")
        End If

        If Not String.Equals(contentEncoding, "base64", StringComparison.OrdinalIgnoreCase) Then
            Throw New AppUserMessageException("GitHub API response used an unsupported content encoding.")
        End If

        content = content.Replace(Convert.ToChar(13), String.Empty).Replace(Convert.ToChar(10), String.Empty)
        Return Encoding.UTF8.GetString(Convert.FromBase64String(content))
    End Function

    Private Shared Async Function DownloadUpdatePackageAsync(manifest As UpdateManifest, progress As Action(Of String, Integer)) As Task(Of String)
        Dim updateDirectory = Path.Combine(Path.GetTempPath(), "Mercury Survey Programming Update")
        Directory.CreateDirectory(updateDirectory)

        Dim packageName = If(manifest.IsExePatch, "Mercury Survey Programming " & manifest.Version.ToString() & ".exe", "Mercury Survey Programming Setup " & manifest.Version.ToString() & ".exe")
        Dim packagePath = Path.Combine(updateDirectory, packageName)
        If File.Exists(packagePath) Then
            File.Delete(packagePath)
        End If

        progress?.Invoke("Connecting to update server...", -1)

        Using response = Await Http.GetAsync(manifest.InstallerUrl, HttpCompletionOption.ResponseHeadersRead)
            If Not response.IsSuccessStatusCode Then
                Throw New AppUserMessageException("Could not download update installer. HTTP " & CInt(response.StatusCode) & ".")
            End If

            Dim totalBytes = response.Content.Headers.ContentLength.GetValueOrDefault(0)
            Dim copiedBytes As Long = 0
            Dim buffer(81919) As Byte

            Using input = Await response.Content.ReadAsStreamAsync()
                Using output = File.Create(packagePath)
                    While True
                        Dim read = Await input.ReadAsync(buffer, 0, buffer.Length)
                        If read <= 0 Then
                            Exit While
                        End If

                        Await output.WriteAsync(buffer, 0, read)
                        copiedBytes += read

                        If totalBytes > 0 Then
                            Dim percent = CInt(Math.Max(0, Math.Min(100, (copiedBytes * 100L) \ totalBytes)))
                            progress?.Invoke("Downloading update... " & FormatByteCount(copiedBytes) & " of " & FormatByteCount(totalBytes), percent)
                        Else
                            progress?.Invoke("Downloading update... " & FormatByteCount(copiedBytes), -1)
                        End If
                    End While
                End Using
            End Using
        End Using

        If Not String.IsNullOrWhiteSpace(manifest.Sha256) Then
            progress?.Invoke("Verifying downloaded update...", -1)
            Dim actualHash = CalculateSha256(packagePath)
            If Not String.Equals(actualHash, manifest.Sha256, StringComparison.OrdinalIgnoreCase) Then
                Try
                    File.Delete(packagePath)
                Catch
                End Try

                Throw New AppUserMessageException("The downloaded update did not match the expected checksum.")
            End If
        End If

        progress?.Invoke("Download complete.", 100)
        Return packagePath
    End Function

    Private Shared Async Function DownloadAndLaunchUpdateAsync(manifest As UpdateManifest, progress As Action(Of String, Integer)) As Task
        Dim updatePath = Await DownloadUpdatePackageAsync(manifest, progress)

        If manifest.IsExePatch Then
            progress?.Invoke("Preparing app patch...", -1)
            AppLogger.Info("Launching EXE patch updater: " & updatePath)
            LaunchExePatch(updatePath)
        Else
            progress?.Invoke("Starting installer...", -1)
            AppLogger.Info("Launching installer updater: " & updatePath)
            Process.Start(New ProcessStartInfo(updatePath) With {.UseShellExecute = True})
        End If
    End Function

    Private Shared Function ParseManifest(json As String) As UpdateManifest
        Dim manifest = New UpdateManifest()
        manifest.VersionText = GetJsonString(json, "version")
        manifest.InstallerUrl = GetJsonString(json, "installerUrl")
        manifest.Sha256 = GetJsonString(json, "sha256")
        manifest.Notes = GetJsonString(json, "notes")
        manifest.PackageType = GetJsonString(json, "packageType")
        manifest.Required = GetJsonBoolean(json, "required")

        Dim parsedVersion As Version = Nothing
        If Version.TryParse(manifest.VersionText, parsedVersion) Then
            manifest.Version = parsedVersion
        End If

        Return manifest
    End Function

    Private Shared Function GetCurrentVersion() As Version
        Dim version = Assembly.GetExecutingAssembly().GetName().Version
        If version Is Nothing Then
            Return New Version(1, 0, 0, 0)
        End If

        Return version
    End Function

    Private Shared Function IsNewerVersion(candidate As Version, current As Version) As Boolean
        If candidate Is Nothing OrElse current Is Nothing Then
            Return False
        End If

        Return candidate.CompareTo(current) > 0
    End Function

    Private Shared Function GetJsonString(json As String, propertyName As String) As String
        Dim pattern = """" & Regex.Escape(propertyName) & """\s*:\s*""(?<value>(?:\\.|[^""])*)"""
        Dim match = Regex.Match(If(json, String.Empty), pattern, RegexOptions.IgnoreCase Or RegexOptions.Singleline)
        If Not match.Success Then
            Return String.Empty
        End If

        Return Regex.Unescape(match.Groups("value").Value).Trim()
    End Function

    Private Shared Function GetJsonBoolean(json As String, propertyName As String) As Boolean
        Dim pattern = """" & Regex.Escape(propertyName) & """\s*:\s*(?<value>true|false)"
        Dim match = Regex.Match(If(json, String.Empty), pattern, RegexOptions.IgnoreCase Or RegexOptions.Singleline)
        Return match.Success AndAlso String.Equals(match.Groups("value").Value, "true", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Shared Function CalculateSha256(path As String) As String
        Using sha = SHA256.Create()
            Using input = File.OpenRead(path)
                Dim bytes = sha.ComputeHash(input)
                Return BitConverter.ToString(bytes).Replace("-", String.Empty).ToLowerInvariant()
            End Using
        End Using
    End Function

    Private Shared Function FormatByteCount(byteCount As Long) As String
        If byteCount >= 1024L * 1024L * 1024L Then
            Return (byteCount / (1024.0R * 1024.0R * 1024.0R)).ToString("0.0") & " GB"
        End If

        If byteCount >= 1024L * 1024L Then
            Return (byteCount / (1024.0R * 1024.0R)).ToString("0.0") & " MB"
        End If

        If byteCount >= 1024L Then
            Return (byteCount / 1024.0R).ToString("0.0") & " KB"
        End If

        Return byteCount.ToString() & " bytes"
    End Function

    Private Shared Sub LaunchExePatch(downloadedExePath As String)
        Dim currentExePath = Assembly.GetExecutingAssembly().Location
        Dim updateDirectory = Path.Combine(Path.GetTempPath(), "Mercury Survey Programming Update")
        Directory.CreateDirectory(updateDirectory)

        Dim scriptPath = Path.Combine(updateDirectory, "apply-exe-update.ps1")
        Dim backupPath = currentExePath & ".old" & DateTime.Now.ToString("yyyyMMddHHmmss")
        Dim processId = Process.GetCurrentProcess().Id

        Dim script = "$ErrorActionPreference = 'Stop'" & Environment.NewLine &
            "param([string]$Source, [string]$Target, [string]$Backup, [int]$ProcessId)" & Environment.NewLine &
            "try { Wait-Process -Id $ProcessId -Timeout 60 -ErrorAction SilentlyContinue } catch { }" & Environment.NewLine &
            "for ($attempt = 0; $attempt -lt 30; $attempt++) {" & Environment.NewLine &
            "    try {" & Environment.NewLine &
            "        if (Test-Path -LiteralPath $Target) { Move-Item -LiteralPath $Target -Destination $Backup -Force }" & Environment.NewLine &
            "        Copy-Item -LiteralPath $Source -Destination $Target -Force" & Environment.NewLine &
            "        Start-Process -FilePath $Target" & Environment.NewLine &
            "        exit 0" & Environment.NewLine &
            "    } catch {" & Environment.NewLine &
            "        Start-Sleep -Seconds 1" & Environment.NewLine &
            "    }" & Environment.NewLine &
            "}" & Environment.NewLine &
            "exit 1" & Environment.NewLine

        File.WriteAllText(scriptPath, script)

        Dim arguments = "-NoProfile -ExecutionPolicy Bypass -File " & QuoteArgument(scriptPath) &
            " -Source " & QuoteArgument(downloadedExePath) &
            " -Target " & QuoteArgument(currentExePath) &
            " -Backup " & QuoteArgument(backupPath) &
            " -ProcessId " & processId.ToString()

        Process.Start(New ProcessStartInfo("powershell.exe", arguments) With {
            .UseShellExecute = False,
            .CreateNoWindow = True,
            .WindowStyle = ProcessWindowStyle.Hidden
        })
    End Sub

    Private Shared Sub LaunchInstallerAfterExit(installerPath As String)
        Dim updateDirectory = Path.Combine(Path.GetTempPath(), "Mercury Survey Programming Update")
        Directory.CreateDirectory(updateDirectory)

        Dim scriptPath = Path.Combine(updateDirectory, "run-installer-after-exit.ps1")
        Dim processId = Process.GetCurrentProcess().Id

        Dim script = "$ErrorActionPreference = 'Stop'" & Environment.NewLine &
            "param([string]$Installer, [int]$ProcessId)" & Environment.NewLine &
            "try { Wait-Process -Id $ProcessId -Timeout 60 -ErrorAction SilentlyContinue } catch { }" & Environment.NewLine &
            "Start-Process -FilePath $Installer" & Environment.NewLine

        File.WriteAllText(scriptPath, script)

        Dim arguments = "-NoProfile -ExecutionPolicy Bypass -File " & QuoteArgument(scriptPath) &
            " -Installer " & QuoteArgument(installerPath) &
            " -ProcessId " & processId.ToString()

        Process.Start(New ProcessStartInfo("powershell.exe", arguments) With {
            .UseShellExecute = False,
            .CreateNoWindow = True,
            .WindowStyle = ProcessWindowStyle.Hidden
        })
    End Sub

    Private Shared Function QuoteArgument(value As String) As String
        Return """" & If(value, String.Empty).Replace("""", "\""") & """"
    End Function

    Private Class UpdateManifest
        Public Property VersionText As String
        Public Property Version As Version
        Public Property InstallerUrl As String
        Public Property Sha256 As String
        Public Property Notes As String
        Public Property PackageType As String
        Public Property Required As Boolean

        Public ReadOnly Property IsUsable As Boolean
            Get
                Return Version IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(InstallerUrl)
            End Get
        End Property

        Public ReadOnly Property IsExePatch As Boolean
            Get
                Return String.Equals(PackageType, "exe", StringComparison.OrdinalIgnoreCase)
            End Get
        End Property
    End Class

    Private Class UpdateProgressForm
        Inherits Form

        Private ReadOnly _manifest As UpdateManifest
        Private ReadOnly _statusLabel As New Label()
        Private ReadOnly _progressBar As New ProgressBar()
        Private ReadOnly _detailLabel As New Label()

        Public Property UpdateStarted As Boolean
        Public Property UpdateException As Exception

        Public Sub New(manifest As UpdateManifest)
            _manifest = manifest
            BuildLayout()
        End Sub

        Private Sub BuildLayout()
            Text = "Updating Mercury Survey Programming"
            AppBrand.ApplyTo(Me)
            StartPosition = FormStartPosition.CenterParent
            FormBorderStyle = FormBorderStyle.FixedDialog
            MaximizeBox = False
            MinimizeBox = False
            ControlBox = False
            ClientSize = New Size(460, 150)

            Dim titleLabel = New Label() With {
                .Text = "Installing Update",
                .Font = New Font(Font.FontFamily, 13.0F, FontStyle.Bold),
                .Location = New Point(18, 18),
                .Size = New Size(420, 28)
            }

            _statusLabel.Text = "Preparing update..."
            _statusLabel.Location = New Point(20, 58)
            _statusLabel.Size = New Size(420, 22)

            _progressBar.Location = New Point(20, 84)
            _progressBar.Size = New Size(420, 20)
            _progressBar.Style = ProgressBarStyle.Marquee
            _progressBar.MarqueeAnimationSpeed = 35

            _detailLabel.Text = "The app will close after the installer starts."
            _detailLabel.Location = New Point(20, 114)
            _detailLabel.Size = New Size(420, 22)
            _detailLabel.ForeColor = Color.DimGray

            Controls.AddRange({titleLabel, _statusLabel, _progressBar, _detailLabel})
        End Sub

        Protected Overrides Async Sub OnShown(e As EventArgs)
            MyBase.OnShown(e)

            Try
                Await DownloadAndLaunchUpdateAsync(_manifest, AddressOf SetProgress)
                SetProgress("Installer started. Closing app...", 100)
                UpdateStarted = True
                DialogResult = DialogResult.OK
                Close()
            Catch ex As Exception
                UpdateException = ex
                AppLogger.Error("Update failed while progress window was open.", ex)
                DialogResult = DialogResult.Abort
                Close()
            End Try
        End Sub

        Private Sub SetProgress(message As String, percent As Integer)
            If InvokeRequired Then
                BeginInvoke(New Action(Of String, Integer)(AddressOf SetProgress), message, percent)
                Return
            End If

            _statusLabel.Text = If(String.IsNullOrWhiteSpace(message), "Working...", message)

            If percent >= 0 Then
                _progressBar.Style = ProgressBarStyle.Blocks
                _progressBar.MarqueeAnimationSpeed = 0
                _progressBar.Value = Math.Max(_progressBar.Minimum, Math.Min(percent, _progressBar.Maximum))
            Else
                _progressBar.Style = ProgressBarStyle.Marquee
                _progressBar.MarqueeAnimationSpeed = 35
            End If
        End Sub
    End Class
End Class
