Imports System
Imports System.Drawing
Imports System.Windows.Forms

Public Class GithubTokenForm
    Inherits Form

    Private ReadOnly _settings As AppSettings
    Private ReadOnly _dimensionsUrlComboBox As New ComboBox()
    Private ReadOnly _templateComboBox As New ComboBox()
    Private ReadOnly _tokenTextBox As New TextBox()

    Public Sub New(settings As AppSettings)
        _settings = settings
        BuildLayout()
        LoadSettingsIntoControls()
    End Sub

    Private Sub BuildLayout()
        Text = "GitHub Token"
        AppBrand.ApplyTo(Me)
        StartPosition = FormStartPosition.CenterParent
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        ClientSize = New Size(560, 210)

        Dim titleLabel = New Label() With {
            .Text = "GitHub Token",
            .Font = New Font(Font.FontFamily, 13.0F, FontStyle.Bold),
            .AutoSize = True,
            .Location = New Point(22, 18)
        }

        AddLabel("Dimensions URL", 24, 66)
        _dimensionsUrlComboBox.Location = New Point(162, 62)
        _dimensionsUrlComboBox.Size = New Size(360, 24)
        _dimensionsUrlComboBox.DropDownStyle = ComboBoxStyle.DropDownList
        AddHandler _dimensionsUrlComboBox.SelectedIndexChanged, AddressOf DimensionsUrlChanged

        AddLabel("Template", 24, 104)
        _templateComboBox.Location = New Point(162, 100)
        _templateComboBox.Size = New Size(200, 24)
        _templateComboBox.DropDownStyle = ComboBoxStyle.DropDownList

        AddLabel("Token", 24, 142)
        _tokenTextBox.Location = New Point(162, 138)
        _tokenTextBox.Size = New Size(360, 24)
        _tokenTextBox.UseSystemPasswordChar = True

        Dim saveButton = New Button() With {.Text = "Save", .Location = New Point(350, 168), .Size = New Size(82, 30)}
        Dim cancelButton = New Button() With {.Text = "Cancel", .Location = New Point(440, 168), .Size = New Size(82, 30)}
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
            _templateComboBox,
            _tokenTextBox,
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

        If _dimensionsUrlComboBox.Items.Count = 0 AndAlso Not String.IsNullOrWhiteSpace(_settings.DimensionsUrl) Then
            _dimensionsUrlComboBox.Items.Add(_settings.DimensionsUrl)
        End If

        _dimensionsUrlComboBox.Text = _settings.DimensionsUrl
        If _dimensionsUrlComboBox.SelectedIndex < 0 AndAlso _dimensionsUrlComboBox.Items.Count > 0 Then
            _dimensionsUrlComboBox.SelectedIndex = 0
        End If

        RefreshTemplateList()
    End Sub

    Private Sub DimensionsUrlChanged(sender As Object, e As EventArgs)
        RefreshTemplateList()
    End Sub

    Private Sub RefreshTemplateList()
        _templateComboBox.Items.Clear()

        For Each templateKey As String In _settings.GetTemplateKeysForDimensionsUrl(_dimensionsUrlComboBox.Text)
            _templateComboBox.Items.Add(New TemplateListItem(templateKey))
        Next

        If _templateComboBox.Items.Count > 0 Then
            _templateComboBox.SelectedIndex = 0
        End If
    End Sub

    Private Async Sub SaveButton_Click(sender As Object, e As EventArgs)
        Dim selectedUrl = _dimensionsUrlComboBox.Text.Trim()
        Dim token = _tokenTextBox.Text.Trim()

        If String.IsNullOrWhiteSpace(selectedUrl) Then
            MessageBox.Show(Me, "Select a Dimensions URL.", "Token Required", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        If String.IsNullOrWhiteSpace(token) Then
            MessageBox.Show(Me, "Paste the GitHub token.", "Token Required", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            _tokenTextBox.Focus()
            Return
        End If

        Try
            Dim selectedTemplate = TryCast(_templateComboBox.SelectedItem, TemplateListItem)
            If selectedTemplate Is Nothing Then
                MessageBox.Show(Me, "Select a template.", "Template Required", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            _settings.SetGithubTokenForDimensionsUrl(selectedUrl, selectedTemplate.TemplateKey, token)
            _settings.Save()
            Try
                Await _settings.UploadRepositoryMappingsToGithubAsync(selectedTemplate.TemplateKey)
            Catch syncEx As Exception
                AppLogger.Error("GitHub token saved locally, but shared repository configuration sync failed.", syncEx)
                Dim syncMessage = "Token saved on this PC, but it could not sync to the shared configuration repository." & Environment.NewLine & Environment.NewLine &
                    "The GitHub token must have write access to praneetpant/setupshared and Contents read/write permission." & Environment.NewLine & Environment.NewLine &
                    "GitHub said: " & SimplifyGithubError(syncEx.Message)
                MessageBox.Show(Me, syncMessage, "Shared Sync Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End Try

            _tokenTextBox.Clear()
            DialogResult = DialogResult.OK
            Close()
        Catch ex As Exception
            MessageBox.Show(Me, ex.Message, "Token Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Shared Function SimplifyGithubError(message As String) As String
        If String.IsNullOrWhiteSpace(message) Then
            Return "GitHub rejected the request."
        End If

        If message.IndexOf("HTTP 403", StringComparison.OrdinalIgnoreCase) >= 0 Then
            Return "HTTP 403 - token does not have access to update setupshared."
        End If

        Return message
    End Function

    Private Class TemplateListItem
        Public ReadOnly Property TemplateKey As String

        Public Sub New(templateKey As String)
            Me.TemplateKey = templateKey
        End Sub

        Public Overrides Function ToString() As String
            If String.Equals(TemplateKey, "classic", StringComparison.OrdinalIgnoreCase) Then
                Return "Old Classic Template"
            End If

            If String.Equals(TemplateKey, "modern", StringComparison.OrdinalIgnoreCase) Then
                Return "New Modern Template"
            End If

            Return Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(TemplateKey.Replace("_", " ").Replace("-", " ")) & " Template"
        End Function
    End Class
End Class
