Imports System
Imports System.Drawing
Imports System.Threading.Tasks
Imports System.Windows.Forms

Public Class LoginForm
    Inherits Form

    Private ReadOnly _settings As AppSettings
    Private ReadOnly _serverComboBox As New ComboBox()
    Private ReadOnly _usernameTextBox As New TextBox()
    Private ReadOnly _passwordTextBox As New TextBox()
    Private ReadOnly _saveCredentialsCheckBox As New CheckBox()
    Private ReadOnly _statusLabel As New Label()
    Private ReadOnly _loginButton As New Button()
    Private ReadOnly _optionsButton As New Button()
    Private ReadOnly _exitButton As New Button()
    Public ReadOnly Property AuthorizedUsername As String

    Public Sub New(settings As AppSettings)
        _settings = settings
        BuildLayout()
    End Sub

    Private Sub BuildLayout()
        Text = "Dimensions Login"
        AppBrand.ApplyTo(Me)
        StartPosition = FormStartPosition.CenterScreen
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        ClientSize = New Size(460, 292)

        Dim titleLabel = New Label() With {
            .Text = "Dimensions Login",
            .Font = New Font(Font.FontFamily, 13.0F, FontStyle.Bold),
            .AutoSize = True,
            .Location = New Point(22, 18)
        }

        Dim serverLabel = New Label() With {.Text = "Server", .Location = New Point(24, 58), .AutoSize = True}
        _serverComboBox.Location = New Point(124, 54)
        _serverComboBox.Size = New Size(310, 24)
        _serverComboBox.DropDownStyle = ComboBoxStyle.DropDownList
        RefreshServerList()

        Dim usernameLabel = New Label() With {.Text = "Username", .Location = New Point(24, 96), .AutoSize = True}
        _usernameTextBox.Location = New Point(124, 92)
        _usernameTextBox.Size = New Size(310, 24)

        Dim passwordLabel = New Label() With {.Text = "Password", .Location = New Point(24, 134), .AutoSize = True}
        _passwordTextBox.Location = New Point(124, 130)
        _passwordTextBox.Size = New Size(310, 24)
        _passwordTextBox.UseSystemPasswordChar = True

        _saveCredentialsCheckBox.Text = "Save username and password"
        _saveCredentialsCheckBox.Location = New Point(124, 164)
        _saveCredentialsCheckBox.Size = New Size(230, 24)
        _saveCredentialsCheckBox.Checked = _settings.SaveLoginCredentials

        If _settings.SaveLoginCredentials Then
            _usernameTextBox.Text = _settings.SavedUsername
            _passwordTextBox.Text = _settings.GetSavedPassword()
        End If

        _statusLabel.Location = New Point(24, 198)
        _statusLabel.Size = New Size(410, 28)
        _statusLabel.ForeColor = Color.Firebrick

        _exitButton.Text = "Exit"
        _exitButton.Location = New Point(160, 242)
        _exitButton.Size = New Size(86, 30)
        AddHandler _exitButton.Click, AddressOf ExitButton_Click

        _loginButton.Text = "Login"
        _loginButton.Location = New Point(254, 242)
        _loginButton.Size = New Size(86, 30)
        AddHandler _loginButton.Click, AddressOf LoginButton_Click

        _optionsButton.Text = "Options..."
        _optionsButton.Location = New Point(348, 242)
        _optionsButton.Size = New Size(86, 30)
        AddHandler _optionsButton.Click, AddressOf OptionsButton_Click

        AcceptButton = _loginButton
        CancelButton = Nothing

        Controls.AddRange({
            titleLabel,
            serverLabel,
            _serverComboBox,
            usernameLabel,
            _usernameTextBox,
            passwordLabel,
            _passwordTextBox,
            _saveCredentialsCheckBox,
            _statusLabel,
            _exitButton,
            _loginButton,
            _optionsButton
        })
    End Sub

    Private Sub RefreshServerList()
        _settings.ApplyDefaults()
        _serverComboBox.Items.Clear()

        For Each url As String In _settings.DimensionsUrls
            _serverComboBox.Items.Add(url)
        Next

        If Not String.IsNullOrWhiteSpace(_settings.DimensionsUrl) AndAlso Not _serverComboBox.Items.Contains(_settings.DimensionsUrl) Then
            _serverComboBox.Items.Insert(0, _settings.DimensionsUrl)
        End If

        If Not String.IsNullOrWhiteSpace(_settings.DimensionsUrl) Then
            _serverComboBox.SelectedItem = _settings.DimensionsUrl
        End If

        If _serverComboBox.SelectedIndex < 0 AndAlso _serverComboBox.Items.Count > 0 Then
            _serverComboBox.SelectedIndex = 0
        End If
    End Sub

    Private Async Sub LoginButton_Click(sender As Object, e As EventArgs)
        Await TryLoginAsync()
    End Sub

    Private Async Function TryLoginAsync() As Task
        ToggleInputs(False)
        _statusLabel.ForeColor = Color.DimGray
        _statusLabel.Text = "Authorizing..."
        AppLogger.Info("Login authorization started.")

        Try
            If _serverComboBox.SelectedItem Is Nothing Then
                Throw New AppUserMessageException("Select a Dimensions server.")
            End If

            _settings.SelectDimensionsUrl(CStr(_serverComboBox.SelectedItem))
            _settings.Save()
            Await DimensionsAuthenticator.ValidateAsync(_settings, _usernameTextBox.Text.Trim(), _passwordTextBox.Text)

            If _saveCredentialsCheckBox.Checked Then
                _settings.SetSavedCredentials(_usernameTextBox.Text.Trim(), _passwordTextBox.Text)
                AppLogger.Info("Login credentials saved using Windows user encryption.")
            Else
                _settings.ClearSavedCredentials()
                AppLogger.Info("Saved login credentials cleared.")
            End If

            _settings.Save()
            AppLogger.Info("Login authorized for server: " & _settings.DimensionsUrl)
            _AuthorizedUsername = _usernameTextBox.Text.Trim()
            DialogResult = DialogResult.OK
            Close()
        Catch ex As UnauthorizedAccessException
            AppLogger.Error("Login authorization failed.", ex)
            ShowLoginFailure("Wrong username or password.")
        Catch ex As Exception
            AppLogger.Error("Login failed.", ex)
            ShowLoginFailure(ex.Message)
        End Try
    End Function

    Private Sub ShowLoginFailure(message As String)
        _statusLabel.ForeColor = Color.Firebrick
        _statusLabel.Text = message
        MessageBox.Show(Me, message, "Login Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        ToggleInputs(True)
        _passwordTextBox.SelectAll()
        _passwordTextBox.Focus()
    End Sub

    Private Sub OptionsButton_Click(sender As Object, e As EventArgs)
        AppLogger.Info("Opening setup/options from login form.")
        Using optionsForm = New OptionsForm(_settings, requireValues:=True, allowRemoveUrls:=False)
            optionsForm.ShowDialog(Me)
        End Using
        RefreshServerList()
    End Sub

    Private Sub ExitButton_Click(sender As Object, e As EventArgs)
        AppExit.Quit(Me)
    End Sub

    Private Sub ToggleInputs(enabled As Boolean)
        _serverComboBox.Enabled = enabled
        _usernameTextBox.Enabled = enabled
        _passwordTextBox.Enabled = enabled
        _saveCredentialsCheckBox.Enabled = enabled
        _exitButton.Enabled = True
        _loginButton.Enabled = enabled
        _optionsButton.Enabled = enabled
    End Sub
End Class
