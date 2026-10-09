Imports System
Imports System.Drawing
Imports System.IO
Imports System.Reflection
Imports System.Windows.Forms

Public NotInheritable Class AppBrand
    Private Shared _appIcon As Icon

    Private Sub New()
    End Sub

    Public Shared Sub ApplyTo(form As Form)
        If form Is Nothing Then
            Return
        End If

        Try
            If _appIcon Is Nothing Then
                Dim iconPath = Path.Combine(Application.StartupPath, "Assets", "MercuryLogo.ico")

                If File.Exists(iconPath) Then
                    _appIcon = New Icon(iconPath)
                Else
                    Using iconStream = OpenEmbeddedIconStream()
                        If iconStream IsNot Nothing Then
                            _appIcon = New Icon(iconStream)
                        End If
                    End Using
                End If

                If _appIcon Is Nothing AndAlso File.Exists(Application.ExecutablePath) Then
                    _appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath)
                End If
            End If

            If _appIcon IsNot Nothing Then
                form.Icon = CType(_appIcon.Clone(), Icon)
            End If
        Catch
        End Try
    End Sub

    Private Shared Function OpenEmbeddedIconStream() As Stream
        Dim executingAssembly = Assembly.GetExecutingAssembly()
        Dim iconStream = executingAssembly.GetManifestResourceStream("MercurySurveyProgramming.Assets.MercuryLogo.ico")

        If iconStream Is Nothing Then
            iconStream = executingAssembly.GetManifestResourceStream("MercurySurveyProgramming.MercuryLogo.ico")
        End If

        Return iconStream
    End Function
End Class
