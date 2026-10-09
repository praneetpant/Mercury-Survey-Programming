Imports System
Imports System.IO

Public NotInheritable Class AppLogger
    Private Shared ReadOnly LogDirectory As String = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Mercury Survey Programming")

    Private Shared ReadOnly LogFilePath As String = Path.Combine(LogDirectory, "app.log")
    Private Shared ReadOnly LogLock As New Object()

    Private Sub New()
    End Sub

    Public Shared Function GetLogPath() As String
        Return LogFilePath
    End Function

    Public Shared Sub Clear()
        Try
            SyncLock LogLock
                Directory.CreateDirectory(LogDirectory)
                File.WriteAllText(LogFilePath, String.Empty)
            End SyncLock
        Catch
        End Try
    End Sub

    Public Shared Sub Info(message As String)
        Write("INFO", message, Nothing)
    End Sub

    Public Shared Sub [Error](message As String, ex As Exception)
        Write("ERROR", message, ex)
    End Sub

    Private Shared Sub Write(level As String, message As String, ex As Exception)
        Try
            SyncLock LogLock
                Directory.CreateDirectory(LogDirectory)

                Using writer = New StreamWriter(LogFilePath, append:=True)
                    writer.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") & " [" & level & "] " & If(message, String.Empty))

                    If ex IsNot Nothing Then
                        writer.WriteLine(ex.ToString())
                    End If
                End Using
            End SyncLock
        Catch
        End Try
    End Sub
End Class
