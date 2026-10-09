Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Windows.Forms

Public Class OptionsForm
    Inherits Form

    Private ReadOnly _settings As AppSettings
    Private ReadOnly _requireValues As Boolean
    Private ReadOnly _allowRemoveUrls As Boolean
    Private ReadOnly _dimensionsUrlComboBox As New ComboBox()
    Private ReadOnly _saveLocationTextBox As New TextBox()
    Private ReadOnly _authModeComboBox As New ComboBox()
    Private ReadOnly _userFieldTextBox As New TextBox()
    Private ReadOnly _passwordFieldTextBox As New TextBox()
    Private ReadOnly _successContainsTextBox As New TextBox()
    Private ReadOnly _failureContainsTextBox As New TextBox()

    Public Sub New(settings As AppSettings, requireValues As Boolean, Optional allowRemoveUrls As Boolean = False)
        _settings = settings
        _requireValues = requireValues
        _allowRemoveUrls = allowRemoveUrls
        BuildLayout()
        LoadSettingsIntoControls()
    End Sub

    Private Sub BuildLayout()
        Text = "Options"
        AppBrand.ApplyTo(Me)
        StartPosition = FormStartPosition.CenterParent
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        ClientSize = New Size(560, 392)

        Dim titleLabel = New Label() With {
            .Text = "Options",
            .Font = New Font(Font.FontFamily, 13.0F, FontStyle.Bold),
            .AutoSize = True,
            .Location = New Point(22, 18)
        }

        AddLabel("Dimensions URL", 24, 62)
        _dimensionsUrlComboBox.Location = New Point(162, 58)
        _dimensionsUrlComboBox.Size = New Size(190, 24)
        _dimensionsUrlComboBox.DropDownStyle = ComboBoxStyle.DropDown

        Dim addUrlButton = New Button() With {.Text = "Add", .Location = New Point(358, 57), .Size = New Size(80, 28)}
        AddHandler addUrlButton.Click, AddressOf AddUrlButton_Click
        Dim removeUrlButton = New Button() With {.Text = "Remove", .Location = New Point(442, 57), .Size = New Size(80, 28), .Enabled = _allowRemoveUrls, .Visible = _allowRemoveUrls}
        AddHandler removeUrlButton.Click, AddressOf RemoveUrlButton_Click

        AddLabel("Output Root", 24, 98)
        _saveLocationTextBox.Location = New Point(162, 94)
        _saveLocationTextBox.Size = New Size(272, 24)

        Dim browseButton = New Button() With {.Text = "Browse...", .Location = New Point(442, 93), .Size = New Size(80, 28)}
        AddHandler browseButton.Click, AddressOf BrowseButton_Click

        AddLabel("Auth Mode", 24, 134)
        _authModeComboBox.Location = New Point(162, 130)
        _authModeComboBox.Size = New Size(160, 24)
        _authModeComboBox.DropDownStyle = ComboBoxStyle.DropDownList
        _authModeComboBox.Items.AddRange({"form", "basic"})

        AddLabel("User Field", 24, 170)
        _userFieldTextBox.Location = New Point(162, 166)
        _userFieldTextBox.Size = New Size(160, 24)

        AddLabel("Password Field", 24, 206)
        _passwordFieldTextBox.Location = New Point(162, 202)
        _passwordFieldTextBox.Size = New Size(160, 24)

        AddLabel("Success Contains", 24, 242)
        _successContainsTextBox.Location = New Point(162, 238)
        _successContainsTextBox.Size = New Size(360, 24)

        AddLabel("Failure Contains", 24, 278)
        _failureContainsTextBox.Location = New Point(162, 274)
        _failureContainsTextBox.Size = New Size(360, 24)

        Dim saveButton = New Button() With {.Text = "Save", .Location = New Point(350, 350), .Size = New Size(82, 30)}
        Dim cancelButton = New Button() With {.Text = "Cancel", .Location = New Point(440, 350), .Size = New Size(82, 30)}
        AddHandler saveButton.Click, AddressOf SaveButton_Click
        AddHandler cancelButton.Click, Sub()
                                           DialogResult = DialogResult.Cancel
                                           Close()
                                       End Sub

        AcceptButton = saveButton
        CancelButton = cancelButton

        Controls.AddRange({
            titleLabel,
            _dimensionsUrlComboBox,
            addUrlButton,
            removeUrlButton,
            _saveLocationTextBox,
            browseButton,
            _authModeComboBox,
            _userFieldTextBox,
            _passwordFieldTextBox,
            _successContainsTextBox,
            _failureContainsTextBox,
            saveButton,
            cancelButton
        })
    End Sub

    Private Sub AddLabel(text As String, x As Integer, y As Integer)
        Controls.Add(New Label() With {.Text = text, .Location = New Point(x, y), .Size = New Size(126, 24)})
    End Sub

    Private Sub LoadSettingsIntoControls()
        _settings.ApplyDefaults()
        _dimensionsUrlComboBox.Items.Clear()
        For Each url As String In _settings.DimensionsUrls
            _dimensionsUrlComboBox.Items.Add(url)
        Next
        _dimensionsUrlComboBox.Text = _settings.DimensionsUrl
        _saveLocationTextBox.Text = _settings.SaveLocation
        _authModeComboBox.SelectedItem = _settings.AuthMode
        If _authModeComboBox.SelectedIndex < 0 Then
            _authModeComboBox.SelectedItem = "form"
        End If

        _userFieldTextBox.Text = _settings.UserField
        _passwordFieldTextBox.Text = _settings.PasswordField
        _successContainsTextBox.Text = _settings.SuccessContains
        _failureContainsTextBox.Text = _settings.FailureContains
    End Sub

    Private Sub AddUrlButton_Click(sender As Object, e As EventArgs)
        Dim url = _dimensionsUrlComboBox.Text.Trim()
        If String.IsNullOrWhiteSpace(url) Then
            MessageBox.Show(Me, "Enter a Dimensions URL to add.", "Options Required", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            _dimensionsUrlComboBox.Focus()
            Return
        End If

        If Not IsValidHttpUrl(url) Then
            MessageBox.Show(Me, "Enter a valid Dimensions HTTP or HTTPS URL.", "Options Required", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            _dimensionsUrlComboBox.Focus()
            Return
        End If

        If Not _dimensionsUrlComboBox.Items.Contains(url) Then
            _dimensionsUrlComboBox.Items.Add(url)
        End If

        _dimensionsUrlComboBox.SelectedItem = url
    End Sub

    Private Sub RemoveUrlButton_Click(sender As Object, e As EventArgs)
        If Not _allowRemoveUrls Then
            Return
        End If

        Dim url = _dimensionsUrlComboBox.Text.Trim()
        If String.IsNullOrWhiteSpace(url) Then
            MessageBox.Show(Me, "Select a Dimensions URL to remove.", "Remove URL", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            _dimensionsUrlComboBox.Focus()
            Return
        End If

        If _dimensionsUrlComboBox.Items.Count <= 1 Then
            MessageBox.Show(Me, "At least one Dimensions URL must remain.", "Remove URL", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        If MessageBox.Show(Me, "Remove this Dimensions URL?" & Environment.NewLine & Environment.NewLine & url, "Remove URL", MessageBoxButtons.YesNo, MessageBoxIcon.Question) <> DialogResult.Yes Then
            Return
        End If

        For index As Integer = _dimensionsUrlComboBox.Items.Count - 1 To 0 Step -1
            If String.Equals(CStr(_dimensionsUrlComboBox.Items(index)).Trim(), url, StringComparison.OrdinalIgnoreCase) Then
                _dimensionsUrlComboBox.Items.RemoveAt(index)
            End If
        Next

        If _dimensionsUrlComboBox.Items.Count > 0 Then
            _dimensionsUrlComboBox.SelectedIndex = 0
        End If
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
        If Not ValidateOptions() Then
            Return
        End If

        Dim selectedUrl = _dimensionsUrlComboBox.Text.Trim()
        Dim remainingUrls = New List(Of String)()
        For Each item As Object In _dimensionsUrlComboBox.Items
            Dim url = CStr(item).Trim()
            If Not String.IsNullOrWhiteSpace(url) AndAlso Not remainingUrls.Contains(url) Then
                remainingUrls.Add(url)
            End If
        Next

        If _allowRemoveUrls Then
            For Each existingUrl As String In _settings.DimensionsUrls.ToList()
                If Not remainingUrls.Any(Function(url) String.Equals(url, existingUrl, StringComparison.OrdinalIgnoreCase)) Then
                    _settings.RemoveDimensionsUrl(existingUrl)
                End If
            Next
        End If

        _settings.DimensionsUrls.Clear()
        For Each url As String In remainingUrls
            If Not String.IsNullOrWhiteSpace(url) AndAlso Not _settings.DimensionsUrls.Contains(url) Then
                _settings.DimensionsUrls.Add(url)
            End If
        Next
        _settings.AddDimensionsUrl(selectedUrl)
        _settings.SaveLocation = _saveLocationTextBox.Text.Trim()
        _settings.AuthMode = CStr(_authModeComboBox.SelectedItem)
        _settings.UserField = _userFieldTextBox.Text.Trim()
        _settings.PasswordField = _passwordFieldTextBox.Text.Trim()
        _settings.SuccessContains = _successContainsTextBox.Text
        _settings.FailureContains = _failureContainsTextBox.Text
        _settings.ApplyDefaults()

        Try
            Directory.CreateDirectory(_settings.SaveLocation)
            Directory.CreateDirectory(_settings.GetProjectSaveLocation())
            _settings.Save()
            If _allowRemoveUrls Then
                Try
                    _settings.UploadRepositoryMappingsToGithubAsync("classic").GetAwaiter().GetResult()
                Catch syncEx As Exception
                    MessageBox.Show(Me, "Options were saved on this PC, but could not sync to shared configuration:" & Environment.NewLine & syncEx.Message, "Shared Sync Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                End Try
            End If
            DialogResult = DialogResult.OK
            Close()
        Catch ex As Exception
            MessageBox.Show(Me, ex.Message, "Options Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Function ValidateOptions() As Boolean
        If _requireValues OrElse Not String.IsNullOrWhiteSpace(_dimensionsUrlComboBox.Text) Then
            If Not IsValidHttpUrl(_dimensionsUrlComboBox.Text.Trim()) Then
                MessageBox.Show(Me, "Enter a valid Dimensions HTTP or HTTPS URL.", "Options Required", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                _dimensionsUrlComboBox.Focus()
                Return False
            End If
        End If

        If _requireValues OrElse Not String.IsNullOrWhiteSpace(_saveLocationTextBox.Text) Then
            If String.IsNullOrWhiteSpace(_saveLocationTextBox.Text) Then
                MessageBox.Show(Me, "Select the output folder named " & AppSettings.GetRequiredSaveRootName() & ".", "Options Required", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                _saveLocationTextBox.Focus()
                Return False
            End If

            If Not AppSettings.IsSaveLocationRootValid(_saveLocationTextBox.Text) Then
                MessageBox.Show(Me, "Output root must be the folder named " & AppSettings.GetRequiredSaveRootName() & ".", "Options Required", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                _saveLocationTextBox.Focus()
                Return False
            End If
        End If

        If CStr(_authModeComboBox.SelectedItem) = "form" Then
            If String.IsNullOrWhiteSpace(_userFieldTextBox.Text) OrElse String.IsNullOrWhiteSpace(_passwordFieldTextBox.Text) Then
                MessageBox.Show(Me, "Enter form field names for username and password.", "Options Required", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return False
            End If
        End If

        Return True
    End Function

    Private Function IsValidHttpUrl(value As String) As Boolean
        Dim uri As Uri = Nothing
        Return Uri.TryCreate(value, UriKind.Absolute, uri) AndAlso
            (uri.Scheme = Uri.UriSchemeHttp OrElse uri.Scheme = Uri.UriSchemeHttps)
    End Function
End Class
