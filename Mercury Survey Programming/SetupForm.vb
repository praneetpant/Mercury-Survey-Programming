Imports System
Imports System.Drawing
Imports System.IO
Imports System.Windows.Forms

Public Class SetupForm
    Inherits Form

    Private ReadOnly _settings As AppSettings
    Private ReadOnly _requireValues As Boolean
    Private ReadOnly _saveLocationTextBox As New TextBox()

    Public Sub New(settings As AppSettings, requireValues As Boolean)
        _settings = settings
        _requireValues = requireValues
        BuildLayout()
        LoadSettingsIntoControls()
    End Sub

    Private Sub BuildLayout()
        Text = "Setup"
        AppBrand.ApplyTo(Me)
        StartPosition = FormStartPosition.CenterParent
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        ClientSize = New Size(560, 176)

        Dim titleLabel = New Label() With {
            .Text = "Setup",
            .Font = New Font(Font.FontFamily, 13.0F, FontStyle.Bold),
            .AutoSize = True,
            .Location = New Point(22, 18)
        }

        Dim saveLocationLabel = New Label() With {.Text = "Output Root", .Location = New Point(24, 66), .Size = New Size(126, 24)}
        _saveLocationTextBox.Location = New Point(162, 62)
        _saveLocationTextBox.Size = New Size(272, 24)

        Dim browseButton = New Button() With {.Text = "Browse...", .Location = New Point(442, 61), .Size = New Size(80, 28)}
        AddHandler browseButton.Click, AddressOf BrowseButton_Click

        Dim saveButton = New Button() With {.Text = "Save", .Location = New Point(350, 126), .Size = New Size(82, 30)}
        Dim cancelButton = New Button() With {.Text = "Cancel", .Location = New Point(440, 126), .Size = New Size(82, 30)}
        AddHandler saveButton.Click, AddressOf SaveButton_Click
        AddHandler cancelButton.Click, Sub()
                                           DialogResult = DialogResult.Cancel
                                           Close()
                                       End Sub

        AcceptButton = saveButton
        CancelButton = cancelButton

        Controls.AddRange({
            titleLabel,
            saveLocationLabel,
            _saveLocationTextBox,
            browseButton,
            saveButton,
            cancelButton
        })
    End Sub

    Private Sub LoadSettingsIntoControls()
        _settings.ApplyDefaults()
        _saveLocationTextBox.Text = _settings.SaveLocation
    End Sub

    Private Sub BrowseButton_Click(sender As Object, e As EventArgs)
        Using dialog = New FolderBrowserDialog()
            dialog.Description = "Select the Mercury Survey Programming output root folder"
            dialog.SelectedPath = If(String.IsNullOrWhiteSpace(_saveLocationTextBox.Text), Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), _saveLocationTextBox.Text)
            If dialog.ShowDialog(Me) = DialogResult.OK Then
                _saveLocationTextBox.Text = dialog.SelectedPath
            End If
        End Using
    End Sub

    Private Sub SaveButton_Click(sender As Object, e As EventArgs)
        If String.IsNullOrWhiteSpace(_saveLocationTextBox.Text) Then
            MessageBox.Show(Me, "Select the output folder named " & AppSettings.GetRequiredSaveRootName() & ".", "Setup Required", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            _saveLocationTextBox.Focus()
            Return
        End If

        If Not AppSettings.IsSaveLocationRootValid(_saveLocationTextBox.Text) Then
            MessageBox.Show(Me, "Output root must be the folder named " & AppSettings.GetRequiredSaveRootName() & ".", "Setup Required", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            _saveLocationTextBox.Focus()
            Return
        End If

        _settings.SaveLocation = _saveLocationTextBox.Text.Trim()
        _settings.ApplyDefaults()

        Try
            Directory.CreateDirectory(_settings.SaveLocation)
            Directory.CreateDirectory(_settings.GetProjectSaveLocation())
            _settings.Save()
            DialogResult = DialogResult.OK
            Close()
        Catch ex As Exception
            MessageBox.Show(Me, ex.Message, "Setup Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub
End Class
