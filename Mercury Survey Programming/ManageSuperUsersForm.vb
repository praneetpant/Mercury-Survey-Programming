Imports System
Imports System.Drawing
Imports System.Threading.Tasks
Imports System.Windows.Forms

Public Class ManageSuperUsersForm
    Inherits Form

    Private ReadOnly _settings As AppSettings
    Private ReadOnly _templateKey As String
    Private ReadOnly _superUsersListBox As New ListBox()
    Private ReadOnly _superUserNameTextBox As New TextBox()
    Private ReadOnly _addButton As New Button()
    Private ReadOnly _removeButton As New Button()
    Private ReadOnly _closeButton As New Button()
    Private ReadOnly _statusLabel As New Label()
    Private _hasChanges As Boolean
    Private _allowClose As Boolean
    Private _isClosing As Boolean

    Public Sub New(settings As AppSettings, templateKey As String)
        _settings = settings
        _templateKey = If(templateKey, String.Empty).Trim()
        BuildLayout()
        RefreshSuperUsersList()
        AddHandler FormClosing, AddressOf ManageSuperUsersForm_FormClosing
    End Sub

    Private Sub BuildLayout()
        Text = "Manage Super Users"
        AppBrand.ApplyTo(Me)
        StartPosition = FormStartPosition.CenterParent
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        ClientSize = New Size(560, 244)

        Dim titleLabel = New Label() With {
            .Text = "Manage Super Users",
            .Font = New Font(Font.FontFamily, 13.0F, FontStyle.Bold),
            .AutoSize = True,
            .Location = New Point(22, 18)
        }

        Dim superUsersLabel = New Label() With {
            .Text = "Superusers",
            .Location = New Point(24, 64),
            .Size = New Size(126, 24)
        }

        _superUsersListBox.Location = New Point(162, 60)
        _superUsersListBox.Size = New Size(300, 88)

        _superUserNameTextBox.Location = New Point(162, 160)
        _superUserNameTextBox.Size = New Size(300, 24)

        _addButton.Text = "Add"
        _addButton.Location = New Point(472, 58)
        _addButton.Size = New Size(66, 30)
        AddHandler _addButton.Click, AddressOf AddButton_Click

        _removeButton.Text = "Remove"
        _removeButton.Location = New Point(472, 94)
        _removeButton.Size = New Size(66, 30)
        AddHandler _removeButton.Click, AddressOf RemoveButton_Click

        _closeButton.Text = "Close"
        _closeButton.Location = New Point(464, 202)
        _closeButton.Size = New Size(74, 30)
        AddHandler _closeButton.Click, Sub()
                                           Close()
                                       End Sub

        _statusLabel.Location = New Point(24, 202)
        _statusLabel.Size = New Size(430, 32)
        _statusLabel.ForeColor = Color.DimGray

        AcceptButton = _addButton
        CancelButton = _closeButton

        Controls.AddRange({
            titleLabel,
            superUsersLabel,
            _superUsersListBox,
            _superUserNameTextBox,
            _addButton,
            _removeButton,
            _closeButton,
            _statusLabel
        })
    End Sub

    Private Sub RefreshSuperUsersList()
        _settings.ApplyDefaults()
        _superUsersListBox.Items.Clear()

        For Each username As String In _settings.SuperUsers
            _superUsersListBox.Items.Add(username)
        Next
    End Sub

    Private Sub AddButton_Click(sender As Object, e As EventArgs)
        Try
            _settings.AddSuperUser(_superUserNameTextBox.Text)
            _settings.Save()
            _hasChanges = True
            _superUserNameTextBox.Clear()
            RefreshSuperUsersList()
            SetStatus("Superuser added. Changes will sync on close.", False)
        Catch ex As Exception
            SetStatus(ex.Message, True)
            MessageBox.Show(Me, ex.Message, "Superuser Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Sub RemoveButton_Click(sender As Object, e As EventArgs)
        Dim selectedUser = TryCast(_superUsersListBox.SelectedItem, String)
        If String.IsNullOrWhiteSpace(selectedUser) Then
            MessageBox.Show(Me, "Select a superuser to remove.", "Superuser Required", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Try
            _settings.RemoveSuperUser(selectedUser)
            _settings.Save()
            _hasChanges = True
            RefreshSuperUsersList()
            SetStatus("Superuser removed. Changes will sync on close.", False)
        Catch ex As Exception
            SetStatus(ex.Message, True)
            MessageBox.Show(Me, ex.Message, "Superuser Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Async Sub ManageSuperUsersForm_FormClosing(sender As Object, e As FormClosingEventArgs)
        If _allowClose OrElse Not _hasChanges Then
            Return
        End If

        If _isClosing Then
            e.Cancel = True
            Return
        End If

        e.Cancel = True
        _isClosing = True

        Try
            If Await SyncSuperUsersAsync() Then
                _hasChanges = False
                _allowClose = True
                DialogResult = DialogResult.OK
                Close()
            End If
        Finally
            _isClosing = False
        End Try
    End Sub

    Private Async Function SyncSuperUsersAsync() As Task(Of Boolean)
        If String.IsNullOrWhiteSpace(_templateKey) Then
            MessageBox.Show(Me, "Select a template on the Super User tab before syncing.", "Template Required", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return False
        End If

        ToggleInputs(False)
        SetStatus("Syncing superusers...", False)

        Try
            Await _settings.UploadSuperUsersToGithubAsync(_templateKey)
            SetStatus("Superuser list synced to GitHub.", False)
            AppLogger.Info("Superuser list synced to GitHub using template: " & _templateKey)
            Return True
        Catch ex As Exception
            SetStatus(ex.Message, True)
            AppLogger.Error("Superuser list sync failed.", ex)
            MessageBox.Show(Me, ex.Message, "Superuser Sync Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return False
        Finally
            ToggleInputs(True)
        End Try
    End Function

    Private Sub ToggleInputs(enabled As Boolean)
        _superUsersListBox.Enabled = enabled
        _superUserNameTextBox.Enabled = enabled
        _addButton.Enabled = enabled
        _removeButton.Enabled = enabled
        _closeButton.Enabled = True
    End Sub

    Private Sub SetStatus(message As String, isError As Boolean)
        _statusLabel.ForeColor = If(isError, Color.Firebrick, Color.DimGray)
        _statusLabel.Text = message
    End Sub
End Class
