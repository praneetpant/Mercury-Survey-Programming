Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Text.RegularExpressions
Imports System.Windows.Forms

Public Class OptionsForm
    Inherits Form

    Private ReadOnly _settings As AppSettings
    Private ReadOnly _requireValues As Boolean
    Private ReadOnly _allowRemoveUrls As Boolean
    Private ReadOnly _dimensionsUrlListBox As New ListBox()
    Private ReadOnly _dimensionsUrlTextBox As New TextBox()
    Private ReadOnly _usernameTextBox As New TextBox()
    Private ReadOnly _passwordTextBox As New TextBox()
    Private ReadOnly _saveLocationTextBox As New TextBox()
    Private _currentCredentialUrl As String
    Private _loadingSelection As Boolean

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
        ClientSize = New Size(620, 430)

        Dim titleLabel = New Label() With {
            .Text = "Options",
            .Font = New Font(Font.FontFamily, 13.0F, FontStyle.Bold),
            .AutoSize = True,
            .Location = New Point(22, 18)
        }

        AddLabel("Dimensions URLs", 24, 62)
        _dimensionsUrlListBox.Location = New Point(162, 58)
        _dimensionsUrlListBox.Size = New Size(420, 112)
        AddHandler _dimensionsUrlListBox.SelectedIndexChanged, AddressOf DimensionsUrlSelectionChanged

        AddLabel("URL", 24, 190)
        _dimensionsUrlTextBox.Location = New Point(162, 186)
        _dimensionsUrlTextBox.Size = New Size(330, 24)

        Dim addUrlButton = New Button() With {.Text = "Add", .Location = New Point(500, 184), .Size = New Size(82, 28)}
        AddHandler addUrlButton.Click, AddressOf AddUrlButton_Click

        Dim removeUrlButton = New Button() With {.Text = "Remove", .Location = New Point(500, 216), .Size = New Size(82, 28), .Enabled = _allowRemoveUrls, .Visible = _allowRemoveUrls}
        AddHandler removeUrlButton.Click, AddressOf RemoveUrlButton_Click

        AddLabel("Username", 24, 232)
        _usernameTextBox.Location = New Point(162, 228)
        _usernameTextBox.Size = New Size(220, 24)

        AddLabel("Password", 24, 270)
        _passwordTextBox.Location = New Point(162, 266)
        _passwordTextBox.Size = New Size(220, 24)
        _passwordTextBox.UseSystemPasswordChar = True

        Dim credentialHintLabel = New Label() With {
            .Text = "Credentials are saved separately for the selected URL.",
            .Location = New Point(162, 296),
            .Size = New Size(420, 24),
            .ForeColor = Color.DimGray
        }

        AddLabel("Output Root", 24, 334)
        _saveLocationTextBox.Location = New Point(162, 330)
        _saveLocationTextBox.Size = New Size(330, 24)

        Dim browseButton = New Button() With {.Text = "Browse...", .Location = New Point(500, 328), .Size = New Size(82, 28)}
        AddHandler browseButton.Click, AddressOf BrowseButton_Click

        Dim saveButton = New Button() With {.Text = "Save", .Location = New Point(410, 382), .Size = New Size(82, 30)}
        Dim cancelButton = New Button() With {.Text = "Cancel", .Location = New Point(500, 382), .Size = New Size(82, 30)}
        AddHandler saveButton.Click, AddressOf SaveButton_Click
        AddHandler cancelButton.Click, Sub()
                                           DialogResult = DialogResult.Cancel
                                           Close()
                                       End Sub

        AcceptButton = saveButton
        CancelButton = cancelButton

        Controls.AddRange({
            titleLabel,
            _dimensionsUrlListBox,
            _dimensionsUrlTextBox,
            addUrlButton,
            removeUrlButton,
            _usernameTextBox,
            _passwordTextBox,
            credentialHintLabel,
            _saveLocationTextBox,
            browseButton,
            saveButton,
            cancelButton
        })
    End Sub

    Private Sub AddLabel(text As String, x As Integer, y As Integer)
        Controls.Add(New Label() With {.Text = text, .Location = New Point(x, y), .Size = New Size(126, 24)})
    End Sub

    Private Sub LoadSettingsIntoControls()
        _settings.ApplyDefaults()
        _dimensionsUrlListBox.Items.Clear()

        For Each url As String In _settings.DimensionsUrls
            _dimensionsUrlListBox.Items.Add(url)
        Next

        _saveLocationTextBox.Text = _settings.SaveLocation
        SelectUrl(_settings.DimensionsUrl)

        If _dimensionsUrlListBox.SelectedIndex < 0 AndAlso _dimensionsUrlListBox.Items.Count > 0 Then
            _dimensionsUrlListBox.SelectedIndex = 0
        End If
    End Sub

    Private Sub DimensionsUrlSelectionChanged(sender As Object, e As EventArgs)
        If _loadingSelection Then
            Return
        End If

        SaveCurrentCredentialFields()
        Dim selectedUrl = TryCast(_dimensionsUrlListBox.SelectedItem, String)
        LoadCredentialFields(selectedUrl)
    End Sub

    Private Sub AddUrlButton_Click(sender As Object, e As EventArgs)
        Dim url = NormalizeOptionUrl(_dimensionsUrlTextBox.Text)
        If String.IsNullOrWhiteSpace(url) Then
            MessageBox.Show(Me, "Enter a Dimensions URL to add.", "Options Required", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            _dimensionsUrlTextBox.Focus()
            Return
        End If

        If Not IsValidHttpUrl(url) Then
            MessageBox.Show(Me, "Enter a valid Dimensions HTTP or HTTPS URL.", "Options Required", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            _dimensionsUrlTextBox.Focus()
            Return
        End If

        SaveCurrentCredentialFields()
        If Not ContainsUrl(url) Then
            _dimensionsUrlListBox.Items.Add(url)
        End If

        SelectUrl(url)
    End Sub

    Private Sub RemoveUrlButton_Click(sender As Object, e As EventArgs)
        If Not _allowRemoveUrls Then
            Return
        End If

        Dim url = TryCast(_dimensionsUrlListBox.SelectedItem, String)
        If String.IsNullOrWhiteSpace(url) Then
            MessageBox.Show(Me, "Select a Dimensions URL to remove.", "Remove URL", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        If _dimensionsUrlListBox.Items.Count <= 1 Then
            MessageBox.Show(Me, "At least one Dimensions URL must remain.", "Remove URL", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        If MessageBox.Show(Me, "Remove this Dimensions URL?" & Environment.NewLine & Environment.NewLine & url, "Remove URL", MessageBoxButtons.YesNo, MessageBoxIcon.Question) <> DialogResult.Yes Then
            Return
        End If

        _settings.ClearSavedCredentialsForUrl(url)
        _dimensionsUrlListBox.Items.Remove(url)
        _currentCredentialUrl = Nothing
        _dimensionsUrlTextBox.Clear()
        _usernameTextBox.Clear()
        _passwordTextBox.Clear()

        If _dimensionsUrlListBox.Items.Count > 0 Then
            _dimensionsUrlListBox.SelectedIndex = 0
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
        SaveCurrentCredentialFields()

        If Not ValidateOptions() Then
            Return
        End If

        Dim remainingUrls = GetUrlList()
        Dim selectedUrl = TryCast(_dimensionsUrlListBox.SelectedItem, String)
        If String.IsNullOrWhiteSpace(selectedUrl) AndAlso remainingUrls.Count > 0 Then
            selectedUrl = remainingUrls(0)
        End If

        If _allowRemoveUrls Then
            For Each existingUrl As String In _settings.DimensionsUrls.ToList()
                If Not remainingUrls.Any(Function(url) String.Equals(url, existingUrl, StringComparison.OrdinalIgnoreCase)) Then
                    _settings.RemoveDimensionsUrl(existingUrl)
                End If
            Next
        End If

        _settings.DimensionsUrls.Clear()
        For Each url As String In remainingUrls
            If Not _settings.DimensionsUrls.Contains(url) Then
                _settings.DimensionsUrls.Add(url)
            End If
        Next

        If Not String.IsNullOrWhiteSpace(selectedUrl) Then
            _settings.SelectDimensionsUrl(selectedUrl)
        End If

        _settings.SaveLocation = _saveLocationTextBox.Text.Trim()
        _settings.AuthMode = "form"
        _settings.UserField = "username"
        _settings.PasswordField = "password"
        _settings.SuccessContains = String.Empty
        _settings.FailureContains = String.Empty
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
        Dim urls = GetUrlList()

        If _requireValues AndAlso urls.Count = 0 Then
            MessageBox.Show(Me, "Add at least one Dimensions URL.", "Options Required", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            _dimensionsUrlTextBox.Focus()
            Return False
        End If

        For Each url As String In urls
            If Not IsValidHttpUrl(url) Then
                MessageBox.Show(Me, "Enter a valid Dimensions HTTP or HTTPS URL.", "Options Required", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return False
            End If
        Next

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

        Return True
    End Function

    Private Sub SelectUrl(url As String)
        _loadingSelection = True
        Try
            For index As Integer = 0 To _dimensionsUrlListBox.Items.Count - 1
                If String.Equals(CStr(_dimensionsUrlListBox.Items(index)), url, StringComparison.OrdinalIgnoreCase) Then
                    _dimensionsUrlListBox.SelectedIndex = index
                    LoadCredentialFields(url)
                    Return
                End If
            Next
        Finally
            _loadingSelection = False
        End Try
    End Sub

    Private Sub LoadCredentialFields(url As String)
        _currentCredentialUrl = url
        _dimensionsUrlTextBox.Text = If(url, String.Empty)
        _usernameTextBox.Text = If(String.IsNullOrWhiteSpace(url), String.Empty, _settings.GetSavedUsernameForUrl(url))
        _passwordTextBox.Text = If(String.IsNullOrWhiteSpace(url), String.Empty, _settings.GetSavedPasswordForUrl(url))
    End Sub

    Private Sub SaveCurrentCredentialFields()
        If String.IsNullOrWhiteSpace(_currentCredentialUrl) Then
            Return
        End If

        If String.IsNullOrWhiteSpace(_usernameTextBox.Text) AndAlso String.IsNullOrWhiteSpace(_passwordTextBox.Text) Then
            _settings.ClearSavedCredentialsForUrl(_currentCredentialUrl)
            Return
        End If

        _settings.SetSavedCredentialsForUrl(_currentCredentialUrl, _usernameTextBox.Text.Trim(), _passwordTextBox.Text)
    End Sub

    Private Function GetUrlList() As List(Of String)
        Dim urls = New List(Of String)()
        For Each item As Object In _dimensionsUrlListBox.Items
            Dim url = NormalizeOptionUrl(CStr(item))
            If Not String.IsNullOrWhiteSpace(url) AndAlso Not urls.Contains(url) Then
                urls.Add(url)
            End If
        Next

        Return urls
    End Function

    Private Function ContainsUrl(url As String) As Boolean
        Return GetUrlList().Any(Function(existingUrl) String.Equals(existingUrl, url, StringComparison.OrdinalIgnoreCase))
    End Function

    Private Shared Function NormalizeOptionUrl(value As String) As String
        Dim cleaned = If(value, String.Empty).Trim()
        If String.IsNullOrWhiteSpace(cleaned) Then
            Return String.Empty
        End If

        If Not cleaned.StartsWith("http://", StringComparison.OrdinalIgnoreCase) AndAlso
            Not cleaned.StartsWith("https://", StringComparison.OrdinalIgnoreCase) Then
            cleaned = "http://" & cleaned.Trim("/"c, " "c)
        End If

        Dim uri As Uri = Nothing
        If Uri.TryCreate(cleaned, UriKind.Absolute, uri) Then
            If Regex.IsMatch(uri.Host, "^dimensions\d+\.mercuryanalytics\.com$", RegexOptions.IgnoreCase) AndAlso
                (String.IsNullOrWhiteSpace(uri.AbsolutePath) OrElse String.Equals(uri.AbsolutePath, "/", StringComparison.Ordinal)) Then
                Return "http://" & uri.Host.ToLowerInvariant() & "/SPSSMR/DimensionNet/"
            End If
        End If

        Return cleaned
    End Function

    Private Shared Function IsValidHttpUrl(value As String) As Boolean
        Dim uri As Uri = Nothing
        Return Uri.TryCreate(value, UriKind.Absolute, uri) AndAlso
            (uri.Scheme = Uri.UriSchemeHttp OrElse uri.Scheme = Uri.UriSchemeHttps)
    End Function
End Class
