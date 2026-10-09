Imports System
Imports System.Windows.Forms

Public NotInheritable Class AppExit
    Private Sub New()
    End Sub

    Public Shared Sub Quit(owner As Form)
        AppLogger.Info("Exit requested from " & If(owner?.Text, "form") & ".")

        If owner IsNot Nothing Then
            owner.DialogResult = DialogResult.Cancel
            owner.Close()
        End If

        Application.Exit()
    End Sub
End Class
