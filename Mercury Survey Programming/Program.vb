Imports System
Imports System.Windows.Forms

Module Program
    <STAThread>
    Sub Main()
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)

        AppLogger.Info("Application started. Log file: " & AppLogger.GetLogPath())

        If AppUpdater.CheckAndRunUpdateIfAvailable(Nothing) Then
            AppLogger.Info("Application closed to install update.")
            Application.Exit()
            Return
        End If

        Dim settings = AppSettings.Load()

        Try
            If settings.SyncRepositoryMappingsFromGithubAsync().GetAwaiter().GetResult() Then
                AppLogger.Info("Repository configuration synced from GitHub.")
            End If
        Catch ex As Exception
            AppLogger.Error("Could not sync repository configuration from GitHub.", ex)
        End Try

        If settings.IsOptionsIncomplete() Then
            Using setupForm = New SetupForm(settings, requireValues:=True)
                If setupForm.ShowDialog() <> DialogResult.OK Then
                    AppLogger.Info("Application closed before setup was completed.")
                    Return
                End If
            End Using
        End If

        Dim authorizedUsername As String
        Using loginForm = New LoginForm(settings)
            If loginForm.ShowDialog() <> DialogResult.OK Then
                AppLogger.Info("Application closed before login was authorized.")
                Return
            End If

            authorizedUsername = loginForm.AuthorizedUsername
        End Using

        Try
            If settings.SyncSuperUsersFromGithubAsync().GetAwaiter().GetResult() Then
                AppLogger.Info("Superuser list synced from GitHub.")
            End If
        Catch ex As Exception
            AppLogger.Error("Could not sync superuser list from GitHub.", ex)
        End Try

        AppLogger.Info("Login authorized. Opening main form.")
        Application.Run(New MainForm(settings, authorizedUsername))
        AppLogger.Info("Application closed.")
    End Sub
End Module
