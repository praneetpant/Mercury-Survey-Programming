Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Threading.Tasks
Imports System.Windows.Forms

Public Class MainForm
    Inherits Form

    Private Const NewProjectTextToReplace As String = ""

    Private ReadOnly _settings As AppSettings
    Private _authorizedUsername As String
    Private _authorizedDimensionsUrl As String

    Private ReadOnly _menuStrip As New MenuStrip()
    Private ReadOnly _helpMenuItem As New ToolStripMenuItem("Help")
    Private ReadOnly _helpOptionsMenuItem As New ToolStripMenuItem("Options...")
    Private ReadOnly _helpCheckUpdatesMenuItem As New ToolStripMenuItem("Check for Updates...")
    Private ReadOnly _helpVersionMenuItem As New ToolStripMenuItem("Version")
    Private ReadOnly _tabControl As New TabControl()
    Private ReadOnly _projectTabPage As New TabPage("Project")
    Private ReadOnly _superUserTabPage As New TabPage("Super User")
    Private ReadOnly _newProjectRadioButton As New RadioButton()
    Private ReadOnly _copyProjectRadioButton As New RadioButton()
    Private ReadOnly _templateGroupBox As New GroupBox()
    Private ReadOnly _templateRadioButtons As New List(Of RadioButton)()
    Private ReadOnly _existingProjectLabel As New Label()
    Private ReadOnly _existingProjectTextBox As New TextBox()
    Private ReadOnly _browseExistingProjectButton As New Button()
    Private ReadOnly _repoUrlLabel As New Label()
    Private ReadOnly _repoUrlTextBox As New TextBox()
    Private ReadOnly _branchLabel As New Label()
    Private ReadOnly _projectNameTextBox As New TextBox()
    Private ReadOnly _branchTextBox As New TextBox()
    Private ReadOnly _saveLocationLabel As New Label()
    Private ReadOnly _saveLocationTextBox As New TextBox()
    Private ReadOnly _overwriteCheckBox As New CheckBox()
    Private ReadOnly _runButton As New Button()
    Private ReadOnly _exitButton As New Button()
    Private ReadOnly _currentOperationCaptionLabel As New Label()
    Private ReadOnly _statusLabel As New Label()
    Private ReadOnly _progressBar As New ProgressBar()
    Private ReadOnly _manageSuperUsersLinkLabel As New LinkLabel()
    Private ReadOnly _githubTokensButton As New Button()
    Private ReadOnly _superTemplateComboBox As New ComboBox()
    Private ReadOnly _superFolderTextBox As New TextBox()
    Private ReadOnly _superBrowseButton As New Button()
    Private ReadOnly _superCommentTextBox As New TextBox()
    Private ReadOnly _superUploadButton As New Button()
    Private ReadOnly _superCurrentOperationCaptionLabel As New Label()
    Private ReadOnly _superProgressBar As New ProgressBar()
    Private ReadOnly _superStatusLabel As New Label()
    Private ReadOnly _busyTimer As New Timer()
    Private _isWorking As Boolean
    Private _busyBaseStatus As String = String.Empty
    Private _busyStep As Integer

    Public Sub New(settings As AppSettings, authorizedUsername As String)
        _settings = settings
        _authorizedUsername = If(authorizedUsername, String.Empty).Trim()
        _authorizedDimensionsUrl = settings.DimensionsUrl
        BuildLayout()
        RefreshSettingsDisplay()
        ApplySuperUserAccess()
        UpdateModeUi()
    End Sub

    Private Sub BuildLayout()
        Text = "Mercury Survey Programming"
        AppBrand.ApplyTo(Me)
        StartPosition = FormStartPosition.CenterScreen
        MinimumSize = New Size(680, 500)
        ClientSize = New Size(680, 500)

        _menuStrip.Dock = DockStyle.Top
        _helpMenuItem.DropDownItems.AddRange({
            _helpOptionsMenuItem,
            _helpCheckUpdatesMenuItem,
            New ToolStripSeparator(),
            _helpVersionMenuItem
        })
        _menuStrip.Items.Add(_helpMenuItem)
        MainMenuStrip = _menuStrip
        AddHandler _helpOptionsMenuItem.Click, AddressOf OptionsButton_Click
        AddHandler _helpCheckUpdatesMenuItem.Click, AddressOf CheckUpdatesButton_Click
        AddHandler _helpVersionMenuItem.Click, AddressOf VersionButton_Click

        _tabControl.Dock = DockStyle.Fill
        _tabControl.TabPages.Add(_projectTabPage)
        _tabControl.TabPages.Add(_superUserTabPage)

        Dim titleLabel = New Label() With {
            .Text = "Mercury Survey Programming",
            .Font = New Font(Font.FontFamily, 14.0F, FontStyle.Bold),
            .AutoSize = True,
            .Location = New Point(24, 20)
        }

        Dim actionGroupBox = New GroupBox() With {
            .Text = "Project Type",
            .Location = New Point(24, 58),
            .Size = New Size(610, 64)
        }

        _newProjectRadioButton.Text = "New Project"
        _newProjectRadioButton.Location = New Point(18, 27)
        _newProjectRadioButton.Size = New Size(120, 24)
        _newProjectRadioButton.Checked = True
        AddHandler _newProjectRadioButton.CheckedChanged, AddressOf ProjectTypeChanged

        _copyProjectRadioButton.Text = "Copy Project"
        _copyProjectRadioButton.Location = New Point(156, 27)
        _copyProjectRadioButton.Size = New Size(120, 24)
        AddHandler _copyProjectRadioButton.CheckedChanged, AddressOf ProjectTypeChanged

        actionGroupBox.Controls.AddRange({_newProjectRadioButton, _copyProjectRadioButton})

        AddLabel("Project Name", 24, 148)
        _projectNameTextBox.Location = New Point(158, 144)
        _projectNameTextBox.Size = New Size(300, 24)
        AddHandler _projectNameTextBox.KeyDown, AddressOf ProjectNameTextBox_KeyDown

        _templateGroupBox.Text = "Template"
        _templateGroupBox.Location = New Point(24, 180)
        _templateGroupBox.Size = New Size(610, 64)

        _existingProjectLabel.Text = "Existing Project"
        _existingProjectLabel.Location = New Point(24, 188)
        _existingProjectLabel.Size = New Size(124, 24)
        _existingProjectTextBox.Location = New Point(158, 184)
        _existingProjectTextBox.Size = New Size(376, 24)
        _browseExistingProjectButton.Text = "Browse..."
        _browseExistingProjectButton.Location = New Point(544, 182)
        _browseExistingProjectButton.Size = New Size(90, 30)
        AddHandler _browseExistingProjectButton.Click, AddressOf BrowseExistingProjectButton_Click

        _repoUrlLabel.Text = "Repository URL"
        _repoUrlLabel.Location = New Point(24, 228)
        _repoUrlLabel.Size = New Size(124, 24)
        _repoUrlTextBox.Location = New Point(158, 224)
        _repoUrlTextBox.Size = New Size(476, 24)

        _branchLabel.Text = "Branch"
        _branchLabel.Location = New Point(24, 268)
        _branchLabel.Size = New Size(124, 24)
        _branchTextBox.Location = New Point(158, 264)
        _branchTextBox.Size = New Size(300, 24)

        _saveLocationLabel.Text = "Save Location"
        _saveLocationLabel.Location = New Point(24, 308)
        _saveLocationLabel.Size = New Size(124, 24)
        _saveLocationTextBox.Location = New Point(158, 304)
        _saveLocationTextBox.Size = New Size(376, 24)
        _saveLocationTextBox.ReadOnly = True

        _exitButton.Text = "Exit"
        _exitButton.Location = New Point(422, 230)
        _exitButton.Size = New Size(86, 34)
        AddHandler _exitButton.Click, AddressOf ExitButton_Click

        _overwriteCheckBox.Text = "Overwrite existing project folder"
        _overwriteCheckBox.Location = New Point(158, 346)
        _overwriteCheckBox.Size = New Size(230, 24)

        _runButton.Text = "Create"
        _runButton.Location = New Point(516, 230)
        _runButton.Size = New Size(118, 34)
        AddHandler _runButton.Click, AddressOf RunButton_Click

        _currentOperationCaptionLabel.Text = "Current operation"
        _currentOperationCaptionLabel.Location = New Point(24, 300)
        _currentOperationCaptionLabel.Size = New Size(610, 18)
        _currentOperationCaptionLabel.ForeColor = Color.DimGray

        _progressBar.Location = New Point(24, 322)
        _progressBar.Size = New Size(610, 18)
        _progressBar.Style = ProgressBarStyle.Blocks

        _statusLabel.Location = New Point(24, 348)
        _statusLabel.Size = New Size(610, 32)
        _statusLabel.ForeColor = Color.DimGray

        _busyTimer.Interval = 350
        AddHandler _busyTimer.Tick, AddressOf BusyTimer_Tick

        _projectTabPage.Controls.AddRange({
            titleLabel,
            actionGroupBox,
            _projectNameTextBox,
            _templateGroupBox,
            _existingProjectLabel,
            _existingProjectTextBox,
            _browseExistingProjectButton,
            _exitButton,
            _runButton,
            _currentOperationCaptionLabel,
            _progressBar,
            _statusLabel
        })

        BuildSuperUserLayout()
        Controls.Add(_tabControl)
        Controls.Add(_menuStrip)
    End Sub

    Private Sub AddLabel(text As String, x As Integer, y As Integer)
        _projectTabPage.Controls.Add(New Label() With {.Text = text, .Location = New Point(x, y), .Size = New Size(124, 24)})
    End Sub

    Private Sub AddSuperLabel(text As String, x As Integer, y As Integer)
        _superUserTabPage.Controls.Add(New Label() With {.Text = text, .Location = New Point(x, y), .Size = New Size(126, 24)})
    End Sub

    Private Sub BuildSuperUserLayout()
        Dim titleLabel = New Label() With {
            .Text = "Super User",
            .Font = New Font(Font.FontFamily, 14.0F, FontStyle.Bold),
            .AutoSize = True,
            .Location = New Point(24, 20)
        }

        _manageSuperUsersLinkLabel.Text = "Manage Super Users"
        _manageSuperUsersLinkLabel.Location = New Point(24, 62)
        _manageSuperUsersLinkLabel.Size = New Size(160, 24)
        AddHandler _manageSuperUsersLinkLabel.LinkClicked, AddressOf ManageSuperUsersLinkLabel_LinkClicked

        _githubTokensButton.Text = "GitHub Tokens..."
        _githubTokensButton.Location = New Point(202, 58)
        _githubTokensButton.Size = New Size(130, 30)
        AddHandler _githubTokensButton.Click, AddressOf GithubTokensButton_Click

        AddSuperLabel("Template", 24, 106)
        _superTemplateComboBox.Location = New Point(158, 102)
        _superTemplateComboBox.Size = New Size(250, 24)
        _superTemplateComboBox.DropDownStyle = ComboBoxStyle.DropDownList

        AddSuperLabel("Local Folder", 24, 146)
        _superFolderTextBox.Location = New Point(158, 142)
        _superFolderTextBox.Size = New Size(376, 24)
        _superBrowseButton.Text = "Browse..."
        _superBrowseButton.Location = New Point(544, 140)
        _superBrowseButton.Size = New Size(90, 30)
        AddHandler _superBrowseButton.Click, AddressOf SuperBrowseButton_Click

        AddSuperLabel("Comments", 24, 186)
        _superCommentTextBox.Location = New Point(158, 182)
        _superCommentTextBox.Size = New Size(376, 50)
        _superCommentTextBox.Multiline = True
        _superCommentTextBox.ScrollBars = ScrollBars.Vertical

        _superUploadButton.Text = "Upload"
        _superUploadButton.Location = New Point(516, 244)
        _superUploadButton.Size = New Size(118, 34)
        AddHandler _superUploadButton.Click, AddressOf SuperUploadButton_Click

        _superCurrentOperationCaptionLabel.Text = "Current operation"
        _superCurrentOperationCaptionLabel.Location = New Point(24, 292)
        _superCurrentOperationCaptionLabel.Size = New Size(610, 18)
        _superCurrentOperationCaptionLabel.ForeColor = Color.DimGray

        _superProgressBar.Location = New Point(24, 314)
        _superProgressBar.Size = New Size(610, 18)
        _superProgressBar.Style = ProgressBarStyle.Blocks

        _superStatusLabel.Location = New Point(24, 340)
        _superStatusLabel.Size = New Size(610, 54)
        _superStatusLabel.ForeColor = Color.DimGray

        _superUserTabPage.Controls.AddRange({
            titleLabel,
            _manageSuperUsersLinkLabel,
            _githubTokensButton,
            _superTemplateComboBox,
            _superFolderTextBox,
            _superBrowseButton,
            _superCommentTextBox,
            _superUploadButton,
            _superCurrentOperationCaptionLabel,
            _superProgressBar,
            _superStatusLabel
        })
    End Sub

    Private Sub GithubTokensButton_Click(sender As Object, e As EventArgs)
        Using tokenForm = New GithubTokenForm(_settings)
            tokenForm.ShowDialog(Me)
        End Using

        RefreshSettingsDisplay()
    End Sub

    Private Sub RefreshSettingsDisplay()
        _saveLocationTextBox.Text = _settings.SaveLocation
        RefreshTemplateOptions()
        RefreshSuperTemplateOptions()
    End Sub

    Private Sub ProjectTypeChanged(sender As Object, e As EventArgs)
        UpdateModeUi()
    End Sub

    Private Sub UpdateModeUi()
        Dim isCopyProject = _copyProjectRadioButton.Checked
        _existingProjectLabel.Visible = isCopyProject
        _existingProjectTextBox.Visible = isCopyProject
        _browseExistingProjectButton.Visible = isCopyProject
        _existingProjectTextBox.Enabled = isCopyProject
        _browseExistingProjectButton.Enabled = isCopyProject
        _templateGroupBox.Visible = Not isCopyProject
        _templateGroupBox.Enabled = Not isCopyProject

        _repoUrlLabel.Visible = False
        _repoUrlTextBox.Visible = False
        _repoUrlTextBox.Enabled = False
        _branchLabel.Visible = False
        _branchTextBox.Visible = False
        _branchTextBox.Enabled = False
        _saveLocationLabel.Visible = False
        _saveLocationTextBox.Visible = False
        _overwriteCheckBox.Visible = False
        _overwriteCheckBox.Checked = False

        _runButton.Text = If(isCopyProject, "Copy", "Create")

        If Not isCopyProject Then
            _repoUrlTextBox.Text = _settings.GetRepositoryUrlForDimensionsUrl(_settings.DimensionsUrl, GetSelectedTemplateKey())
        End If

        LayoutActionArea()
    End Sub

    Private Async Sub RunButton_Click(sender As Object, e As EventArgs)
        Await RunProjectActionAsync()
    End Sub

    Private Async Sub ProjectNameTextBox_KeyDown(sender As Object, e As KeyEventArgs)
        If e.KeyCode <> Keys.Enter Then
            Return
        End If

        e.SuppressKeyPress = True
        If _runButton.Enabled Then
            Await RunProjectActionAsync()
        End If
    End Sub

    Private Async Function RunProjectActionAsync() As Task
        If _settings.IsOptionsIncomplete() Then
            Using setupForm = New SetupForm(_settings, requireValues:=True)
                If setupForm.ShowDialog(Me) <> DialogResult.OK Then
                    Return
                End If
            End Using

            RefreshSettingsDisplay()
        End If

        Dim projectName = NormalizeProjectName(_projectNameTextBox.Text)
        If String.IsNullOrWhiteSpace(projectName) Then
            MessageBox.Show(Me, "Enter a project name.", "Project Name Required", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            _projectNameTextBox.Focus()
            Return
        End If

        _projectNameTextBox.Text = projectName

        Dim selectedTemplateKey = GetSelectedTemplateKey()
        Dim selectedTemplateName = GetTemplateDisplayName(selectedTemplateKey)
        Dim repositoryUrl = If(_newProjectRadioButton.Checked, _settings.GetRepositoryUrlForDimensionsUrl(_settings.DimensionsUrl, selectedTemplateKey), Nothing)
        Dim oldMddName = If(_newProjectRadioButton.Checked, _settings.GetOldMddNameForDimensionsUrl(_settings.DimensionsUrl, selectedTemplateKey), Nothing)
        Dim repositoryBranch = If(_newProjectRadioButton.Checked, _settings.GetBranchForDimensionsUrl(_settings.DimensionsUrl, selectedTemplateKey), String.Empty)
        Dim githubToken = If(_newProjectRadioButton.Checked, _settings.GetGithubTokenForDimensionsUrl(_settings.DimensionsUrl, selectedTemplateKey), String.Empty)
        Dim projectSaveLocation As String

        Try
            projectSaveLocation = _settings.GetProjectSaveLocation()
        Catch ex As Exception
            MessageBox.Show(Me, ex.Message, "Save Location Required", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            AppLogger.Error("Project operation blocked because save location could not be resolved.", ex)
            Return
        End Try

        If _copyProjectRadioButton.Checked AndAlso String.IsNullOrWhiteSpace(_existingProjectTextBox.Text) Then
            MessageBox.Show(Me, "Select the existing project folder to copy.", "Existing Project Required", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            _existingProjectTextBox.Focus()
            Return
        End If

        If _newProjectRadioButton.Checked AndAlso String.IsNullOrWhiteSpace(repositoryUrl) Then
            Dim message = "No GitHub repository is configured for this Dimensions URL and template: " & _settings.DimensionsUrl & " / " & selectedTemplateName & Environment.NewLine & Environment.NewLine &
                "Add a matching entry to:" & Environment.NewLine & AppSettings.GetRepositoryMappingsPath()
            MessageBox.Show(Me, message, "Repository Required", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            AppLogger.Info("New Project blocked because no repository mapping exists for Dimensions URL/template: " & _settings.DimensionsUrl & " / " & selectedTemplateName)
            Return
        End If

        ToggleInputs(False)
        AppLogger.Info("Project operation started. Mode: " & If(_copyProjectRadioButton.Checked, "Copy Project", "New Project") & "; Template: " & selectedTemplateName & "; Project name: " & projectName & "; Save location: " & projectSaveLocation)
        StartWorkingAnimation("Starting")

        Try
            Dim targetPath As String

            If _copyProjectRadioButton.Checked Then
                targetPath = Await GithubRepositoryDownloader.CopyExistingProjectAsync(
                    _existingProjectTextBox.Text.Trim(),
                    projectSaveLocation,
                    projectName,
                    overwrite:=False,
                    progress:=AddressOf SetStatus)
            Else
                targetPath = Await GithubRepositoryDownloader.DownloadAsync(
                    repositoryUrl,
                    projectSaveLocation,
                    projectName,
                    branch:=repositoryBranch,
                    githubToken:=githubToken,
                    overwrite:=False,
                    shouldRenameTextInstances:=True,
                    textToReplace:=If(String.IsNullOrWhiteSpace(oldMddName), NewProjectTextToReplace, oldMddName),
                    progress:=AddressOf SetStatus)
            End If

            StopWorkingAnimation()
            _statusLabel.ForeColor = Color.ForestGreen
            _statusLabel.Text = "Completed: " & targetPath
            AppLogger.Info("Project operation completed. Target path: " & targetPath)
            MessageBox.Show(Me, "Project saved to:" & Environment.NewLine & targetPath, "Project Ready", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            StopWorkingAnimation()
            _statusLabel.ForeColor = Color.Firebrick
            _statusLabel.Text = ex.Message
            AppLogger.Error("Project operation failed.", ex)
            MessageBox.Show(Me, ex.Message, "Project Action Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        Finally
            ToggleInputs(True)
            UpdateModeUi()
        End Try
    End Function

    Private Sub OptionsButton_Click(sender As Object, e As EventArgs)
        Dim oldDimensionsUrl = _settings.DimensionsUrl
        AppLogger.Info("Opening setup/options from main form.")

        Using optionsForm = New OptionsForm(_settings, requireValues:=True, allowRemoveUrls:=_settings.IsSuperUser(_authorizedUsername))
            If optionsForm.ShowDialog(Me) <> DialogResult.OK Then
                Return
            End If
        End Using

        RefreshSettingsDisplay()

        If Not String.Equals(oldDimensionsUrl, _settings.DimensionsUrl, StringComparison.OrdinalIgnoreCase) OrElse
            Not String.Equals(_authorizedDimensionsUrl, _settings.DimensionsUrl, StringComparison.OrdinalIgnoreCase) Then
            Using loginForm = New LoginForm(_settings)
                If loginForm.ShowDialog(Me) = DialogResult.OK Then
                    _authorizedDimensionsUrl = _settings.DimensionsUrl
                    _authorizedUsername = loginForm.AuthorizedUsername
                    Try
                        _settings.SyncSuperUsersFromGithubAsync().GetAwaiter().GetResult()
                    Catch ex As Exception
                        AppLogger.Error("Could not sync superuser list from GitHub after re-login.", ex)
                    End Try
                    ApplySuperUserAccess()
                Else
                    Close()
                End If
            End Using
        End If
    End Sub

    Private Async Sub CheckUpdatesButton_Click(sender As Object, e As EventArgs)
        Dim updateStarted = False

        ToggleInputs(False)
        _currentOperationCaptionLabel.Text = "Current operation: Checking for updates"
        _statusLabel.ForeColor = Color.DimGray
        _statusLabel.Text = "Checking for updates..."
        _progressBar.Style = ProgressBarStyle.Marquee
        _progressBar.MarqueeAnimationSpeed = 35

        Try
            updateStarted = Await AppUpdater.CheckAndRunUpdateIfAvailableAsync(Me, notifyWhenCurrent:=True)
            If updateStarted Then
                AppLogger.Info("Application closing so the updater can install the new version.")
                Application.Exit()
                Return
            End If
        Finally
            If Not updateStarted AndAlso Not IsDisposed Then
                _progressBar.MarqueeAnimationSpeed = 0
                _progressBar.Style = ProgressBarStyle.Blocks
                _progressBar.Value = 0
                _statusLabel.ForeColor = Color.DimGray
                _statusLabel.Text = "Update check completed."
                ToggleInputs(True)
                UpdateModeUi()
            End If
        End Try
    End Sub

    Private Sub VersionButton_Click(sender As Object, e As EventArgs)
        Dim appAssembly = GetType(MainForm).Assembly
        Dim assemblyVersion = appAssembly.GetName().Version
        Dim exePath = appAssembly.Location
        Dim fileVersion = FileVersionInfo.GetVersionInfo(exePath).FileVersion
        Dim message = "Mercury Survey Programming" & Environment.NewLine & Environment.NewLine &
            "Current version: " & If(fileVersion, assemblyVersion.ToString()) & Environment.NewLine &
            "Assembly version: " & assemblyVersion.ToString() & Environment.NewLine & Environment.NewLine &
            "Installed path:" & Environment.NewLine & exePath

        MessageBox.Show(Me, message, "Current Version", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    Private Sub BrowseExistingProjectButton_Click(sender As Object, e As EventArgs)
        Using dialog = New FolderBrowserDialog()
            dialog.Description = "Select the existing project folder to copy"

            If Directory.Exists(_existingProjectTextBox.Text) Then
                dialog.SelectedPath = _existingProjectTextBox.Text
            ElseIf Directory.Exists(_settings.SaveLocation) Then
                dialog.SelectedPath = _settings.SaveLocation
            End If

            If dialog.ShowDialog(Me) = DialogResult.OK Then
                _existingProjectTextBox.Text = dialog.SelectedPath
            End If
        End Using
    End Sub

    Private Sub SuperBrowseButton_Click(sender As Object, e As EventArgs)
        Using dialog = New FolderBrowserDialog()
            dialog.Description = "Select the local folder to upload"

            If Directory.Exists(_superFolderTextBox.Text) Then
                dialog.SelectedPath = _superFolderTextBox.Text
            ElseIf Directory.Exists(_settings.SaveLocation) Then
                dialog.SelectedPath = _settings.SaveLocation
            End If

            If dialog.ShowDialog(Me) = DialogResult.OK Then
                _superFolderTextBox.Text = dialog.SelectedPath
            End If
        End Using
    End Sub

    Private Async Sub SuperUploadButton_Click(sender As Object, e As EventArgs)
        Await RunSuperUploadAsync()
    End Sub

    Private Sub ManageSuperUsersLinkLabel_LinkClicked(sender As Object, e As LinkLabelLinkClickedEventArgs)
        Dim selectedTemplate = TryCast(_superTemplateComboBox.SelectedItem, TemplateListItem)
        Dim selectedTemplateKey = If(selectedTemplate Is Nothing, String.Empty, selectedTemplate.TemplateKey)

        Using manageForm = New ManageSuperUsersForm(_settings, selectedTemplateKey)
            manageForm.ShowDialog(Me)
        End Using

        RefreshSettingsDisplay()
        ApplySuperUserAccess()
    End Sub

    Private Async Function RunSuperUploadAsync() As Task
        Dim selectedTemplate = TryCast(_superTemplateComboBox.SelectedItem, TemplateListItem)
        If selectedTemplate Is Nothing Then
            MessageBox.Show(Me, "Select a template.", "Template Required", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        If String.IsNullOrWhiteSpace(_superFolderTextBox.Text) OrElse Not Directory.Exists(_superFolderTextBox.Text) Then
            MessageBox.Show(Me, "Select a valid local folder to upload.", "Folder Required", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            _superFolderTextBox.Focus()
            Return
        End If

        Dim repositoryUrl = _settings.GetRepositoryUrlForDimensionsUrl(_settings.DimensionsUrl, selectedTemplate.TemplateKey)
        Dim repositoryBranch = _settings.GetBranchForDimensionsUrl(_settings.DimensionsUrl, selectedTemplate.TemplateKey)
        Dim githubToken = _settings.GetGithubTokenForDimensionsUrl(_settings.DimensionsUrl, selectedTemplate.TemplateKey)

        If String.IsNullOrWhiteSpace(repositoryUrl) Then
            MessageBox.Show(Me, "No GitHub repository is configured for this template.", "Repository Required", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        If String.IsNullOrWhiteSpace(githubToken) Then
            MessageBox.Show(Me, "Save a GitHub token for this template first.", "Token Required", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim uploadTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        Dim uploadUser = If(String.IsNullOrWhiteSpace(_authorizedUsername), Environment.UserName, _authorizedUsername)
        Dim userComment = NormalizeCommitComment(_superCommentTextBox.Text)
        Dim commitMessage = "Upload by " & uploadUser & " on " & uploadTimestamp
        If Not String.IsNullOrWhiteSpace(userComment) Then
            commitMessage &= ": " & userComment
        End If

        Dim confirmMessage = "Upload files from:" & Environment.NewLine & _superFolderTextBox.Text.Trim() & Environment.NewLine & Environment.NewLine &
            "To template: " & selectedTemplate.ToString() & Environment.NewLine &
            "Branch: " & If(String.IsNullOrWhiteSpace(repositoryBranch), "main", repositoryBranch) & Environment.NewLine & Environment.NewLine &
            "Commit message: " & commitMessage & Environment.NewLine & Environment.NewLine &
            "This mirrors the selected folder to GitHub. Files missing from the selected folder will be deleted from GitHub, except protected/skipped files."

        If MessageBox.Show(Me, confirmMessage, "Confirm Upload", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) <> DialogResult.OK Then
            Return
        End If

        ToggleInputs(False)
        ToggleSuperInputs(False)
        AppLogger.Clear()
        _superProgressBar.Style = ProgressBarStyle.Marquee
        _superProgressBar.MarqueeAnimationSpeed = 35
        _superProgressBar.Value = 0
        _superStatusLabel.ForeColor = Color.DimGray
        _superStatusLabel.Text = "Starting upload..."
        _superCurrentOperationCaptionLabel.Text = "Current operation: Starting upload..."
        AppLogger.Info("Super User upload started. Template: " & selectedTemplate.TemplateKey & "; Folder: " & _superFolderTextBox.Text.Trim())

        Try
            Dim summary = Await GithubRepositoryUploader.UploadDirectoryAsync(
                repositoryUrl,
                repositoryBranch,
                githubToken,
                _superFolderTextBox.Text.Trim(),
                commitMessage,
                AddressOf SetSuperUploadProgress)

            _superProgressBar.Style = ProgressBarStyle.Blocks
            _superProgressBar.MarqueeAnimationSpeed = 0
            _superProgressBar.Value = _superProgressBar.Maximum
            _superCurrentOperationCaptionLabel.Text = "Current operation: Upload completed"
            _superStatusLabel.ForeColor = Color.ForestGreen
            _superStatusLabel.Text = "Upload completed. Created: " & summary.Created & "; Updated: " & summary.Updated & "; Deleted: " & summary.Deleted & "; Skipped unchanged: " & summary.Skipped
            AppLogger.Info("Super User upload completed. Created: " & summary.Created & "; Updated: " & summary.Updated & "; Deleted: " & summary.Deleted & "; Skipped unchanged: " & summary.Skipped)
            If summary.Skipped > 0 Then
                Dim logMessage = _superStatusLabel.Text & Environment.NewLine & Environment.NewLine &
                    "Skipped unchanged files were written to the app log." & Environment.NewLine &
                    "Open the log file now?"

                If MessageBox.Show(Me, logMessage, "Upload Complete", MessageBoxButtons.YesNo, MessageBoxIcon.Information) = DialogResult.Yes Then
                    OpenLogFile()
                End If
            Else
                MessageBox.Show(Me, _superStatusLabel.Text, "Upload Complete", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End If
        Catch ex As Exception
            _superProgressBar.Style = ProgressBarStyle.Blocks
            _superProgressBar.MarqueeAnimationSpeed = 0
            _superProgressBar.Value = 0
            _superCurrentOperationCaptionLabel.Text = "Current operation: Upload failed"
            _superStatusLabel.ForeColor = Color.Firebrick
            _superStatusLabel.Text = ex.Message
            AppLogger.Error("Super User upload failed.", ex)
            MessageBox.Show(Me, ex.Message, "Upload Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        Finally
            ToggleInputs(True)
            ToggleSuperInputs(True)
            UpdateModeUi()
        End Try
    End Function

    Private Sub OpenLogFile()
        Try
            Dim logPath = AppLogger.GetLogPath()
            If Not File.Exists(logPath) Then
                MessageBox.Show(Me, "Log file was not found:" & Environment.NewLine & logPath, "Log Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Process.Start(New ProcessStartInfo(logPath) With {.UseShellExecute = True})
        Catch ex As Exception
            AppLogger.Error("Could not open log file.", ex)
            MessageBox.Show(Me, ex.Message, "Open Log Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Sub SetSuperStatus(message As String)
        If InvokeRequired Then
            BeginInvoke(New Action(Of String)(AddressOf SetSuperStatus), message)
            Return
        End If

        _superStatusLabel.Text = message
        _superCurrentOperationCaptionLabel.Text = "Current operation: " & message
        AppLogger.Info("Super User upload status: " & message)
    End Sub

    Private Sub SetSuperUploadProgress(progress As GithubRepositoryUploader.UploadProgress)
        If InvokeRequired Then
            BeginInvoke(New Action(Of GithubRepositoryUploader.UploadProgress)(AddressOf SetSuperUploadProgress), progress)
            Return
        End If

        If progress Is Nothing Then
            Return
        End If

        Dim message = If(progress.Message, String.Empty)
        _superStatusLabel.Text = message
        _superCurrentOperationCaptionLabel.Text = "Current operation: " & message

        If progress.Total > 0 Then
            _superProgressBar.Style = ProgressBarStyle.Blocks
            _superProgressBar.MarqueeAnimationSpeed = 0
            _superProgressBar.Minimum = 0
            _superProgressBar.Maximum = progress.Total
            _superProgressBar.Value = Math.Max(_superProgressBar.Minimum, Math.Min(progress.Current, _superProgressBar.Maximum))
        Else
            _superProgressBar.Style = ProgressBarStyle.Marquee
            _superProgressBar.MarqueeAnimationSpeed = 35
        End If

        AppLogger.Info("Super User upload status: " & message)
    End Sub

    Private Sub ExitButton_Click(sender As Object, e As EventArgs)
        AppExit.Quit(Me)
    End Sub

    Private Sub SetStatus(message As String)
        If InvokeRequired Then
            BeginInvoke(New Action(Of String)(AddressOf SetStatus), message)
            Return
        End If

        _busyBaseStatus = message
        AppLogger.Info("Operation status: " & message)
        _currentOperationCaptionLabel.Text = "Current operation: " & message

        If _isWorking Then
            UpdateWorkingStatus()
        Else
            _statusLabel.Text = message
        End If
    End Sub

    Private Sub StartWorkingAnimation(message As String)
        _isWorking = True
        _busyBaseStatus = message
        _busyStep = 0
        _statusLabel.ForeColor = Color.DimGray
        _currentOperationCaptionLabel.Text = "Current operation: " & message
        _progressBar.Style = ProgressBarStyle.Marquee
        _progressBar.MarqueeAnimationSpeed = 35
        _progressBar.Value = 0
        UpdateWorkingStatus()
        _busyTimer.Start()
    End Sub

    Private Sub StopWorkingAnimation()
        _busyTimer.Stop()
        _isWorking = False
        _progressBar.MarqueeAnimationSpeed = 0
        _progressBar.Style = ProgressBarStyle.Blocks
        _progressBar.Value = _progressBar.Maximum
    End Sub

    Private Sub BusyTimer_Tick(sender As Object, e As EventArgs)
        _busyStep = (_busyStep + 1) Mod 4
        UpdateWorkingStatus()
    End Sub

    Private Sub RefreshTemplateOptions()
        Dim selectedTemplateKey = GetSelectedTemplateKey()
        _templateGroupBox.Controls.Clear()
        _templateRadioButtons.Clear()

        Dim templateKeys = _settings.GetTemplateKeysForDimensionsUrl(_settings.DimensionsUrl)
        For index As Integer = 0 To templateKeys.Count - 1
            Dim templateKey = templateKeys(index)
            Dim radioButton = New RadioButton() With {
                .Text = GetTemplateDisplayName(templateKey),
                .Tag = templateKey,
                .Location = New Point(18 + ((index Mod 3) * 190), 27 + ((index \ 3) * 26)),
                .Size = New Size(180, 24),
                .Checked = index = 0
            }
            AddHandler radioButton.CheckedChanged, AddressOf TemplateChanged
            _templateRadioButtons.Add(radioButton)
            _templateGroupBox.Controls.Add(radioButton)
        Next

        If _templateRadioButtons.Count > 0 Then
            Dim existingSelection = _templateRadioButtons.FirstOrDefault(Function(button) String.Equals(CStr(button.Tag), selectedTemplateKey, StringComparison.OrdinalIgnoreCase))
            If existingSelection IsNot Nothing Then
                existingSelection.Checked = True
            End If
        End If

        Dim rowCount = Math.Max(1, CInt(Math.Ceiling(Math.Max(1, _templateRadioButtons.Count) / 3.0R)))
        _templateGroupBox.Height = Math.Max(64, 40 + (rowCount * 26))
    End Sub

    Private Sub RefreshSuperTemplateOptions()
        Dim selectedTemplateKey As String = Nothing
        Dim currentSelection = TryCast(_superTemplateComboBox.SelectedItem, TemplateListItem)
        If currentSelection IsNot Nothing Then
            selectedTemplateKey = currentSelection.TemplateKey
        End If

        _superTemplateComboBox.Items.Clear()
        For Each templateKey As String In _settings.GetTemplateKeysForDimensionsUrl(_settings.DimensionsUrl)
            _superTemplateComboBox.Items.Add(New TemplateListItem(templateKey))
        Next

        If _superTemplateComboBox.Items.Count > 0 Then
            For index As Integer = 0 To _superTemplateComboBox.Items.Count - 1
                Dim item = TryCast(_superTemplateComboBox.Items(index), TemplateListItem)
                If item IsNot Nothing AndAlso String.Equals(item.TemplateKey, selectedTemplateKey, StringComparison.OrdinalIgnoreCase) Then
                    _superTemplateComboBox.SelectedIndex = index
                    Return
                End If
            Next

            _superTemplateComboBox.SelectedIndex = 0
        End If
    End Sub

    Private Sub ApplySuperUserAccess()
        Dim shouldShowSuperUserTab = _settings.IsSuperUser(_authorizedUsername)
        If shouldShowSuperUserTab Then
            If Not _tabControl.TabPages.Contains(_superUserTabPage) Then
                _tabControl.TabPages.Add(_superUserTabPage)
            End If
        ElseIf _tabControl.TabPages.Contains(_superUserTabPage) Then
            _tabControl.TabPages.Remove(_superUserTabPage)
        End If

    End Sub

    Private Sub TemplateChanged(sender As Object, e As EventArgs)
        UpdateModeUi()
    End Sub

    Private Sub LayoutActionArea()
        Dim actionTop = If(_copyProjectRadioButton.Checked, 230, _templateGroupBox.Bottom + 16)
        _exitButton.Location = New Point(422, actionTop)
        _runButton.Location = New Point(516, actionTop)
        _currentOperationCaptionLabel.Location = New Point(24, actionTop + 70)
        _progressBar.Location = New Point(24, actionTop + 92)
        _statusLabel.Location = New Point(24, actionTop + 118)

        ClientSize = New Size(680, Math.Max(420, actionTop + 170))
        MinimumSize = New Size(680, ClientSize.Height)
    End Sub

    Private Sub UpdateWorkingStatus()
        Dim message = If(String.IsNullOrWhiteSpace(_busyBaseStatus), "Working", _busyBaseStatus)
        _statusLabel.Text = message.TrimEnd("."c) & New String("."c, _busyStep)
    End Sub

    Private Shared Function NormalizeProjectName(projectName As String) As String
        Dim cleanedName = If(projectName, String.Empty).Trim()
        If String.IsNullOrWhiteSpace(cleanedName) OrElse cleanedName.StartsWith("MA", StringComparison.OrdinalIgnoreCase) Then
            Return cleanedName
        End If

        Return "MA" & cleanedName
    End Function

    Private Function GetSelectedTemplateKey() As String
        For Each radioButton In _templateRadioButtons
            If radioButton.Checked AndAlso radioButton.Tag IsNot Nothing Then
                Return CStr(radioButton.Tag)
            End If
        Next

        If _templateRadioButtons.Count > 0 AndAlso _templateRadioButtons(0).Tag IsNot Nothing Then
            Return CStr(_templateRadioButtons(0).Tag)
        End If

        Return "classic"
    End Function

    Private Shared Function GetTemplateDisplayName(templateKey As String) As String
        If String.Equals(templateKey, "classic", StringComparison.OrdinalIgnoreCase) Then
            Return "Old Classic Template"
        End If

        If String.Equals(templateKey, "modern", StringComparison.OrdinalIgnoreCase) Then
            Return "New Modern Template"
        End If

        Dim cleanedKey = If(templateKey, String.Empty).Trim()
        If String.IsNullOrWhiteSpace(cleanedKey) Then
            Return "Template"
        End If

        Return Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cleanedKey.Replace("_", " ").Replace("-", " ")) & " Template"
    End Function

    Private Sub ToggleInputs(enabled As Boolean)
        _newProjectRadioButton.Enabled = enabled
        _copyProjectRadioButton.Enabled = enabled
        _templateGroupBox.Enabled = enabled AndAlso _newProjectRadioButton.Checked
        _existingProjectTextBox.Enabled = enabled
        _browseExistingProjectButton.Enabled = enabled
        _repoUrlTextBox.Enabled = enabled
        _projectNameTextBox.Enabled = enabled
        _branchTextBox.Enabled = enabled
        _exitButton.Enabled = True
        _runButton.Enabled = enabled
        _helpOptionsMenuItem.Enabled = enabled
        _helpCheckUpdatesMenuItem.Enabled = enabled
        _helpVersionMenuItem.Enabled = enabled
    End Sub

    Private Sub ToggleSuperInputs(enabled As Boolean)
        _manageSuperUsersLinkLabel.Enabled = enabled
        _superTemplateComboBox.Enabled = enabled
        _superFolderTextBox.Enabled = enabled
        _superBrowseButton.Enabled = enabled
        _superCommentTextBox.Enabled = enabled
        _superUploadButton.Enabled = enabled
    End Sub

    Private Shared Function NormalizeCommitComment(value As String) As String
        Dim cleaned = If(value, String.Empty).Trim()
        cleaned = String.Join(" ", cleaned.Split(New Char() {Convert.ToChar(13), Convert.ToChar(10), Convert.ToChar(9)}, StringSplitOptions.RemoveEmptyEntries))
        Return cleaned
    End Function

    Private Class TemplateListItem
        Public ReadOnly Property TemplateKey As String

        Public Sub New(templateKey As String)
            Me.TemplateKey = templateKey
        End Sub

        Public Overrides Function ToString() As String
            Return GetTemplateDisplayName(TemplateKey)
        End Function
    End Class
End Class
