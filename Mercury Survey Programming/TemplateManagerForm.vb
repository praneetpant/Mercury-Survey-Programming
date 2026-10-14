Imports System
Imports System.Drawing
Imports System.Threading.Tasks
Imports System.Windows.Forms

Public Class TemplateManagerForm
    Inherits Form

    Private ReadOnly _settings As AppSettings
    Private ReadOnly _dimensionsUrlComboBox As New ComboBox()
    Private ReadOnly _templatesListBox As New ListBox()
    Private ReadOnly _templateKeyTextBox As New TextBox()
    Private ReadOnly _repositoryUrlTextBox As New TextBox()
    Private ReadOnly _branchTextBox As New TextBox()
    Private ReadOnly _oldMddNameTextBox As New TextBox()
    Private ReadOnly _tokenTextBox As New TextBox()
    Private ReadOnly _newButton As New Button()
    Private ReadOnly _saveButton As New Button()
    Private ReadOnly _deleteButton As New Button()
    Private ReadOnly _closeButton As New Button()
    Private ReadOnly _statusLabel As New Label()
    Private _hasChanges As Boolean
    Private _allowClose As Boolean
    Private _isClosing As Boolean

    Public Sub New(settings As AppSettings)
        _settings = settings
        BuildLayout()
        LoadSettingsIntoControls()
        AddHandler FormClosing, AddressOf TemplateManagerForm_FormClosing
    End Sub

    Private Sub BuildLayout()
        Text = "Manage Templates"
        AppBrand.ApplyTo(Me)
        StartPosition = FormStartPosition.CenterParent
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        ClientSize = New Size(720, 420)

        Dim titleLabel = New Label() With {
            .Text = "Manage Templates",
            .Font = New Font(Font.FontFamily, 13.0F, FontStyle.Bold),
            .AutoSize = True,
            .Location = New Point(22, 18)
        }

        AddLabel("Dimensions URL", 24, 62)
        _dimensionsUrlComboBox.Location = New Point(150, 58)
        _dimensionsUrlComboBox.Size = New Size(532, 24)
        _dimensionsUrlComboBox.DropDownStyle = ComboBoxStyle.DropDownList
        AddHandler _dimensionsUrlComboBox.SelectedIndexChanged, AddressOf DimensionsUrlChanged

        AddLabel("Templates", 24, 102)
        _templatesListBox.Location = New Point(150, 100)
        _templatesListBox.Size = New Size(206, 188)
        AddHandler _templatesListBox.SelectedIndexChanged, AddressOf TemplateSelectionChanged

        AddLabel("Template Name", 376, 102)
        _templateKeyTextBox.Location = New Point(494, 98)
        _templateKeyTextBox.Size = New Size(188, 24)

        AddLabel("Repository URL", 376, 140)
        _repositoryUrlTextBox.Location = New Point(494, 136)
        _repositoryUrlTextBox.Size = New Size(188, 24)

        AddLabel("Branch", 376, 178)
        _branchTextBox.Location = New Point(494, 174)
        _branchTextBox.Size = New Size(188, 24)

        AddLabel("Old MDD Name", 376, 216)
        _oldMddNameTextBox.Location = New Point(494, 212)
        _oldMddNameTextBox.Size = New Size(188, 24)

        AddLabel("Access Token", 376, 254)
        _tokenTextBox.Location = New Point(494, 250)
        _tokenTextBox.Size = New Size(188, 24)
        _tokenTextBox.UseSystemPasswordChar = True

        Dim tokenHintLabel = New Label() With {
            .Text = "Blank on edit keeps existing token.",
            .Location = New Point(494, 278),
            .Size = New Size(188, 34),
            .ForeColor = Color.DimGray
        }

        _newButton.Text = "New"
        _newButton.Location = New Point(150, 302)
        _newButton.Size = New Size(66, 30)
        AddHandler _newButton.Click, AddressOf NewButton_Click

        _saveButton.Text = "Save"
        _saveButton.Location = New Point(224, 302)
        _saveButton.Size = New Size(66, 30)
        AddHandler _saveButton.Click, AddressOf SaveButton_Click

        _deleteButton.Text = "Delete"
        _deleteButton.Location = New Point(298, 302)
        _deleteButton.Size = New Size(66, 30)
        AddHandler _deleteButton.Click, AddressOf DeleteButton_Click

        _closeButton.Text = "Close"
        _closeButton.Location = New Point(608, 374)
        _closeButton.Size = New Size(74, 30)
        AddHandler _closeButton.Click, Sub()
                                           Close()
                                       End Sub

        _statusLabel.Location = New Point(24, 350)
        _statusLabel.Size = New Size(560, 54)
        _statusLabel.ForeColor = Color.DimGray

        AcceptButton = _saveButton
        CancelButton = _closeButton

        Controls.AddRange({
            titleLabel,
            _dimensionsUrlComboBox,
            _templatesListBox,
            _templateKeyTextBox,
            _repositoryUrlTextBox,
            _branchTextBox,
            _oldMddNameTextBox,
            _tokenTextBox,
            tokenHintLabel,
            _newButton,
            _saveButton,
            _deleteButton,
            _closeButton,
            _statusLabel
        })
    End Sub

    Private Sub AddLabel(text As String, x As Integer, y As Integer)
        Controls.Add(New Label() With {.Text = text, .Location = New Point(x, y), .Size = New Size(118, 24)})
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

    Private Sub RefreshTemplateList(Optional selectedTemplateKey As String = Nothing)
        _templatesListBox.Items.Clear()

        For Each templateKey As String In _settings.GetTemplateKeysForDimensionsUrl(_dimensionsUrlComboBox.Text)
            _templatesListBox.Items.Add(New TemplateListItem(templateKey))
        Next

        If _templatesListBox.Items.Count > 0 Then
            For index As Integer = 0 To _templatesListBox.Items.Count - 1
                Dim item = TryCast(_templatesListBox.Items(index), TemplateListItem)
                If item IsNot Nothing AndAlso String.Equals(item.TemplateKey, selectedTemplateKey, StringComparison.OrdinalIgnoreCase) Then
                    _templatesListBox.SelectedIndex = index
                    Return
                End If
            Next

            _templatesListBox.SelectedIndex = 0
        Else
            ClearTemplateFields()
        End If
    End Sub

    Private Sub TemplateSelectionChanged(sender As Object, e As EventArgs)
        Dim item = TryCast(_templatesListBox.SelectedItem, TemplateListItem)
        If item Is Nothing Then
            Return
        End If

        Dim templateInfo = _settings.GetTemplateInfoForDimensionsUrl(_dimensionsUrlComboBox.Text, item.TemplateKey)
        _templateKeyTextBox.Text = item.TemplateKey
        _templateKeyTextBox.ReadOnly = True
        _repositoryUrlTextBox.Text = If(templateInfo Is Nothing, String.Empty, templateInfo.RepositoryUrl)
        _branchTextBox.Text = If(templateInfo Is Nothing, String.Empty, templateInfo.Branch)
        _oldMddNameTextBox.Text = If(templateInfo Is Nothing, String.Empty, templateInfo.OldMddName)
        _tokenTextBox.Clear()
        SetStatus("Edit details, or paste a new token to replace the existing token.", False)
    End Sub

    Private Sub NewButton_Click(sender As Object, e As EventArgs)
        _templatesListBox.ClearSelected()
        ClearTemplateFields()
        _templateKeyTextBox.ReadOnly = False
        _branchTextBox.Text = "main"
        _templateKeyTextBox.Focus()
        SetStatus("Enter a template name, repository URL, old MDD name, and access token.", False)
    End Sub

    Private Sub SaveButton_Click(sender As Object, e As EventArgs)
        Dim selectedItem = TryCast(_templatesListBox.SelectedItem, TemplateListItem)
        Dim isNewTemplate = selectedItem Is Nothing
        Dim templateKey = _templateKeyTextBox.Text.Trim()
        Dim token = _tokenTextBox.Text.Trim()

        If String.IsNullOrWhiteSpace(templateKey) Then
            MessageBox.Show(Me, "Enter a template name.", "Template Required", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            _templateKeyTextBox.Focus()
            Return
        End If

        If String.IsNullOrWhiteSpace(_repositoryUrlTextBox.Text) Then
            MessageBox.Show(Me, "Enter the GitHub repository URL.", "Repository Required", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            _repositoryUrlTextBox.Focus()
            Return
        End If

        If isNewTemplate AndAlso String.IsNullOrWhiteSpace(token) Then
            MessageBox.Show(Me, "Paste the GitHub access token for the new template.", "Token Required", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            _tokenTextBox.Focus()
            Return
        End If

        Try
            _settings.SetTemplateForDimensionsUrl(
                _dimensionsUrlComboBox.Text,
                templateKey,
                _repositoryUrlTextBox.Text,
                _oldMddNameTextBox.Text,
                _branchTextBox.Text,
                token,
                keepExistingTokenWhenBlank:=Not isNewTemplate)
            _settings.Save()
            _hasChanges = True
            _tokenTextBox.Clear()
            RefreshTemplateList(templateKey)
            SetStatus("Template saved. Changes will sync to GitHub on close.", False)
        Catch ex As Exception
            SetStatus(ex.Message, True)
            MessageBox.Show(Me, ex.Message, "Template Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Sub DeleteButton_Click(sender As Object, e As EventArgs)
        Dim selectedItem = TryCast(_templatesListBox.SelectedItem, TemplateListItem)
        If selectedItem Is Nothing Then
            MessageBox.Show(Me, "Select a template to delete.", "Template Required", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        If MessageBox.Show(Me, "Delete this template?" & Environment.NewLine & Environment.NewLine & selectedItem.ToString(), "Delete Template", MessageBoxButtons.YesNo, MessageBoxIcon.Question) <> DialogResult.Yes Then
            Return
        End If

        Try
            _settings.RemoveTemplateForDimensionsUrl(_dimensionsUrlComboBox.Text, selectedItem.TemplateKey)
            _settings.Save()
            _hasChanges = True
            RefreshTemplateList()
            SetStatus("Template deleted. Changes will sync to GitHub on close.", False)
        Catch ex As Exception
            SetStatus(ex.Message, True)
            MessageBox.Show(Me, ex.Message, "Template Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Async Sub TemplateManagerForm_FormClosing(sender As Object, e As FormClosingEventArgs)
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
            If Await SyncTemplatesAsync() Then
                _hasChanges = False
                _allowClose = True
                DialogResult = DialogResult.OK
                Close()
            End If
        Finally
            _isClosing = False
        End Try
    End Sub

    Private Async Function SyncTemplatesAsync() As Task(Of Boolean)
        ToggleInputs(False)
        SetStatus("Syncing templates to GitHub...", False)

        Try
            Await _settings.UploadRepositoryMappingsToGithubAsync("classic")
            SetStatus("Template list synced to GitHub.", False)
            AppLogger.Info("Template list synced to GitHub.")
            Return True
        Catch ex As Exception
            SetStatus(ex.Message, True)
            AppLogger.Error("Template list sync failed.", ex)
            MessageBox.Show(Me, ex.Message, "Template Sync Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return False
        Finally
            ToggleInputs(True)
        End Try
    End Function

    Private Sub ClearTemplateFields()
        _templateKeyTextBox.Text = String.Empty
        _templateKeyTextBox.ReadOnly = False
        _repositoryUrlTextBox.Text = String.Empty
        _branchTextBox.Text = "main"
        _oldMddNameTextBox.Text = String.Empty
        _tokenTextBox.Clear()
    End Sub

    Private Sub ToggleInputs(enabled As Boolean)
        _dimensionsUrlComboBox.Enabled = enabled
        _templatesListBox.Enabled = enabled
        _templateKeyTextBox.Enabled = enabled
        _repositoryUrlTextBox.Enabled = enabled
        _branchTextBox.Enabled = enabled
        _oldMddNameTextBox.Enabled = enabled
        _tokenTextBox.Enabled = enabled
        _newButton.Enabled = enabled
        _saveButton.Enabled = enabled
        _deleteButton.Enabled = enabled
        _closeButton.Enabled = True
    End Sub

    Private Sub SetStatus(message As String, isError As Boolean)
        _statusLabel.ForeColor = If(isError, Color.Firebrick, Color.DimGray)
        _statusLabel.Text = message
    End Sub

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
