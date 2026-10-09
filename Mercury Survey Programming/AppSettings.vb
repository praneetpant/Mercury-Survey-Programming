Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Security.Cryptography
Imports System.Text.RegularExpressions
Imports System.Text
Imports System.Threading.Tasks

Public Class AppSettings
    Private Const DefaultDimensionsRepositoryHost As String = "dimensions5.mercuryanalytics.com"
    Private Const DefaultClassicRepositoryUrl As String = "https://github.com/praneetpant/SHELL5.git"
    Private Const DefaultModernRepositoryUrl As String = "https://github.com/praneetpant/Templates2026.git"
    Private Const DefaultClassicOldMddName As String = "SHELL5"
    Private Const DefaultModernOldMddName As String = "SHELL2026"
    Private Const DefaultRepositoryBranch As String = "main"
    Private Const DefaultSuperUser As String = "praneetp"
    Private Const SharedConfigurationRepositoryUrl As String = "https://github.com/praneetpant/setupshared.git"
    Private Const SharedConfigurationBranch As String = "main"
    Private Const SuperUsersRepositoryPath As String = "superusers.json"
    Private Const RepositoryMappingsRepositoryPath As String = "repositories.json"
    Private Const SharedConfigurationTokenFileName As String = "shared-config-token.txt"
    Private Const RequiredSaveRootFolderName As String = "Mercury Survey Programming"
    Private Const SharedProtectedPrefix As String = "shared:v1:"
    Private Const SharedProtectionPurpose As String = "Mercury Survey Programming shared repository token configuration"

    Public Property DimensionsUrl As String
    Public Property DimensionsUrls As New List(Of String)()
    Public Property RepositoryMappings As New Dictionary(Of String, RepositoryTemplateMapping)(StringComparer.OrdinalIgnoreCase)
    Public Property SaveLocation As String
    Public Property AuthMode As String = "form"
    Public Property UserField As String = "username"
    Public Property PasswordField As String = "password"
    Public Property SuccessContains As String
    Public Property FailureContains As String
    Public Property SaveLoginCredentials As Boolean
    Public Property SavedUsername As String
    Public Property SavedPasswordEncrypted As String
    Public Property SharedConfigurationGithubTokenEncrypted As String
    Public Property SuperUsers As New List(Of String)()

    Private Shared ReadOnly SettingsDirectory As String = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Mercury Survey Programming")

    Private Shared ReadOnly SettingsPath As String = Path.Combine(SettingsDirectory, "settings.json")
    Private Shared ReadOnly RepositoryMappingsPath As String = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "repositories.json")
    Private Shared ReadOnly SharedConfigurationTokenPath As String = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SharedConfigurationTokenFileName)
    Private Shared ReadOnly LegacyRepositoryMappingsPath As String = Path.Combine(SettingsDirectory, "repositories.json")
    Private Shared ReadOnly LegacySettingsPath As String = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DownloadRepoShell",
        "settings.json")

    Public Shared Function GetSettingsPath() As String
        Return SettingsPath
    End Function

    Public Shared Function GetRepositoryMappingsPath() As String
        Return RepositoryMappingsPath
    End Function

    Public Shared Function GetBootstrapTokenPath() As String
        Return SharedConfigurationTokenPath
    End Function

    Public Shared Function Load() As AppSettings
        Dim pathToLoad = SettingsPath
        If Not File.Exists(pathToLoad) AndAlso File.Exists(LegacySettingsPath) Then
            pathToLoad = LegacySettingsPath
        End If

        If Not File.Exists(pathToLoad) Then
            Dim empty = New AppSettings()
            empty.RepositoryMappings = LoadRepositoryMappings()
            empty.ApplyDefaults()
            Return empty
        End If

        Try
            Dim values = ReadSettingsFile(pathToLoad)
            Dim loaded = New AppSettings With {
                .DimensionsUrl = GetValue(values, NameOf(DimensionsUrl)),
                .DimensionsUrls = ParseList(GetValue(values, NameOf(DimensionsUrls))),
                .SaveLocation = GetValue(values, NameOf(SaveLocation)),
                .AuthMode = GetValue(values, NameOf(AuthMode)),
                .UserField = GetValue(values, NameOf(UserField)),
                .PasswordField = GetValue(values, NameOf(PasswordField)),
                .SuccessContains = GetValue(values, NameOf(SuccessContains)),
                .FailureContains = GetValue(values, NameOf(FailureContains)),
                .SaveLoginCredentials = GetBooleanValue(values, NameOf(SaveLoginCredentials)),
                .SavedUsername = GetValue(values, NameOf(SavedUsername)),
                .SavedPasswordEncrypted = GetValue(values, NameOf(SavedPasswordEncrypted)),
                .SharedConfigurationGithubTokenEncrypted = GetValue(values, NameOf(SharedConfigurationGithubTokenEncrypted)),
                .SuperUsers = ParseList(GetValue(values, NameOf(SuperUsers)))
            }
            loaded.RepositoryMappings = LoadRepositoryMappings()
            loaded.ApplyDefaults()
            Return loaded
        Catch
            Dim empty = New AppSettings()
            empty.RepositoryMappings = LoadRepositoryMappings()
            empty.ApplyDefaults()
            Return empty
        End Try
    End Function

    Public Sub Save()
        Directory.CreateDirectory(SettingsDirectory)
        Dim lines = New List(Of String) From {
            CreateLine(NameOf(DimensionsUrl), DimensionsUrl),
            CreateLine(NameOf(DimensionsUrls), String.Join(Environment.NewLine, NormalizeUrls(DimensionsUrls).ToArray())),
            CreateLine(NameOf(SaveLocation), SaveLocation),
            CreateLine(NameOf(AuthMode), AuthMode),
            CreateLine(NameOf(UserField), UserField),
            CreateLine(NameOf(PasswordField), PasswordField),
            CreateLine(NameOf(SuccessContains), SuccessContains),
            CreateLine(NameOf(FailureContains), FailureContains),
            CreateLine(NameOf(SaveLoginCredentials), SaveLoginCredentials.ToString()),
            CreateLine(NameOf(SavedUsername), SavedUsername),
            CreateLine(NameOf(SavedPasswordEncrypted), SavedPasswordEncrypted),
            CreateLine(NameOf(SharedConfigurationGithubTokenEncrypted), SharedConfigurationGithubTokenEncrypted),
            CreateLine(NameOf(SuperUsers), String.Join(Environment.NewLine, NormalizeSuperUsers(SuperUsers).ToArray()))
        }

        File.WriteAllLines(SettingsPath, lines.ToArray())
        SaveRepositoryMappings()
    End Sub

    Public Function IsOptionsIncomplete() As Boolean
        Return String.IsNullOrWhiteSpace(DimensionsUrl) OrElse NormalizeUrls(DimensionsUrls).Count = 0 OrElse Not IsSaveLocationRootValid(SaveLocation)
    End Function

    Public Shared Function GetRequiredSaveRootName() As String
        Return RequiredSaveRootFolderName
    End Function

    Public Shared Function IsSaveLocationRootValid(saveLocation As String) As Boolean
        If String.IsNullOrWhiteSpace(saveLocation) Then
            Return False
        End If

        Dim cleanedPath = Path.GetFullPath(saveLocation.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        Return String.Equals(Path.GetFileName(cleanedPath), RequiredSaveRootFolderName, StringComparison.OrdinalIgnoreCase)
    End Function

    Public Function GetProjectSaveLocation() As String
        If Not IsSaveLocationRootValid(SaveLocation) Then
            Throw New AppUserMessageException("Select the output folder named " & RequiredSaveRootFolderName & ".")
        End If

        Dim dimensionsFolder = GetDimensionsFolderName(DimensionsUrl)
        Return Path.Combine(Path.GetFullPath(SaveLocation.Trim()), dimensionsFolder, DateTime.Now.Year.ToString())
    End Function

    Public Shared Function GetDimensionsFolderName(dimensionsUrl As String) As String
        If String.IsNullOrWhiteSpace(dimensionsUrl) Then
            Throw New AppUserMessageException("Dimensions URL is required before the project save folder can be selected.")
        End If

        Dim host = dimensionsUrl.Trim()
        Dim uri As Uri = Nothing
        If Uri.TryCreate(host, UriKind.Absolute, uri) Then
            host = uri.Host
        End If

        Dim match = Regex.Match(host, "dimensions\d+", RegexOptions.IgnoreCase)
        If Not match.Success Then
            Throw New AppUserMessageException("Could not determine the Dimensions folder from URL: " & dimensionsUrl)
        End If

        Return match.Value.ToLowerInvariant()
    End Function

    Public Sub ApplyDefaults()
        If String.IsNullOrWhiteSpace(AuthMode) Then
            AuthMode = "form"
        End If

        If String.IsNullOrWhiteSpace(UserField) Then
            UserField = "username"
        End If

        If String.IsNullOrWhiteSpace(PasswordField) Then
            PasswordField = "password"
        End If

        DimensionsUrl = NormalizeDimensionsLoginUrl(DimensionsUrl)
        DimensionsUrls = NormalizeUrls(DimensionsUrls)
        SuperUsers = NormalizeSuperUsers(SuperUsers)

        If Not SuperUsers.Contains(DefaultSuperUser) Then
            SuperUsers.Insert(0, DefaultSuperUser)
        End If

        If Not String.IsNullOrWhiteSpace(DimensionsUrl) AndAlso Not DimensionsUrls.Contains(DimensionsUrl) Then
            DimensionsUrls.Insert(0, DimensionsUrl)
        End If

        If RepositoryMappings IsNot Nothing Then
            For Each mappingKey As String In RepositoryMappings.Keys.OrderBy(Function(key) key, StringComparer.OrdinalIgnoreCase).ToList()
                Dim dimensionsUrl = CreateDimensionsUrlFromMappingKey(mappingKey)
                If Not String.IsNullOrWhiteSpace(dimensionsUrl) AndAlso Not DimensionsUrls.Contains(dimensionsUrl) Then
                    DimensionsUrls.Add(dimensionsUrl)
                End If
            Next
        End If

        If String.IsNullOrWhiteSpace(DimensionsUrl) AndAlso DimensionsUrls.Count > 0 Then
            DimensionsUrl = DimensionsUrls(0)
        End If

        If RepositoryMappings Is Nothing Then
            RepositoryMappings = New Dictionary(Of String, RepositoryTemplateMapping)(StringComparer.OrdinalIgnoreCase)
        End If

        If Not RepositoryMappings.ContainsKey(DefaultDimensionsRepositoryHost) Then
            Dim defaultMapping = New RepositoryTemplateMapping()
            defaultMapping.SetTemplateInfo("classic", DefaultClassicRepositoryUrl, DefaultClassicOldMddName, DefaultRepositoryBranch)
            defaultMapping.SetTemplateInfo("modern", DefaultModernRepositoryUrl, DefaultModernOldMddName, DefaultRepositoryBranch)
            RepositoryMappings(DefaultDimensionsRepositoryHost) = defaultMapping
        Else
            If String.IsNullOrWhiteSpace(RepositoryMappings(DefaultDimensionsRepositoryHost).ClassicRepositoryUrl) Then
                RepositoryMappings(DefaultDimensionsRepositoryHost).ClassicRepositoryUrl = DefaultClassicRepositoryUrl
            End If

            If String.IsNullOrWhiteSpace(RepositoryMappings(DefaultDimensionsRepositoryHost).ModernRepositoryUrl) Then
                RepositoryMappings(DefaultDimensionsRepositoryHost).ModernRepositoryUrl = DefaultModernRepositoryUrl
            End If

            If String.IsNullOrWhiteSpace(RepositoryMappings(DefaultDimensionsRepositoryHost).GetOldMddName("classic")) Then
                RepositoryMappings(DefaultDimensionsRepositoryHost).SetTemplateInfo("classic", RepositoryMappings(DefaultDimensionsRepositoryHost).ClassicRepositoryUrl, DefaultClassicOldMddName, RepositoryMappings(DefaultDimensionsRepositoryHost).GetBranch("classic"))
            End If

            If String.IsNullOrWhiteSpace(RepositoryMappings(DefaultDimensionsRepositoryHost).GetOldMddName("modern")) Then
                RepositoryMappings(DefaultDimensionsRepositoryHost).SetTemplateInfo("modern", RepositoryMappings(DefaultDimensionsRepositoryHost).ModernRepositoryUrl, DefaultModernOldMddName, RepositoryMappings(DefaultDimensionsRepositoryHost).GetBranch("modern"))
            End If
        End If
    End Sub

    Public Function GetRepositoryUrlForDimensionsUrl(dimensionsUrl As String, templateKey As String) As String
        Dim mapping = GetRepositoryMappingForDimensionsUrl(dimensionsUrl)
        If mapping Is Nothing Then
            Return Nothing
        End If

        Return mapping.GetRepositoryUrl(templateKey)
    End Function

    Public Function GetOldMddNameForDimensionsUrl(dimensionsUrl As String, templateKey As String) As String
        Dim mapping = GetRepositoryMappingForDimensionsUrl(dimensionsUrl)
        If mapping Is Nothing Then
            Return Nothing
        End If

        Return mapping.GetOldMddName(templateKey)
    End Function

    Public Function GetBranchForDimensionsUrl(dimensionsUrl As String, templateKey As String) As String
        Dim mapping = GetRepositoryMappingForDimensionsUrl(dimensionsUrl)
        If mapping Is Nothing Then
            Return Nothing
        End If

        Return mapping.GetBranch(templateKey)
    End Function

    Public Function GetGithubTokenForDimensionsUrl(dimensionsUrl As String, templateKey As String) As String
        Dim mapping = GetRepositoryMappingForDimensionsUrl(dimensionsUrl)
        If mapping Is Nothing Then
            Return Nothing
        End If

        Return mapping.GetGithubToken(templateKey)
    End Function

    Public Sub SetGithubTokenForDimensionsUrl(dimensionsUrl As String, templateKey As String, githubToken As String)
        Dim mapping = GetRepositoryMappingForDimensionsUrl(dimensionsUrl)
        If mapping Is Nothing Then
            Throw New AppUserMessageException("No repository mapping exists for this Dimensions URL.")
        End If

        Dim templateInfo = mapping.GetTemplateInfo(templateKey)
        If templateInfo Is Nothing Then
            Throw New AppUserMessageException("No template mapping exists for this selection.")
        End If

        mapping.SetTemplateInfo(templateKey, templateInfo.RepositoryUrl, templateInfo.OldMddName, templateInfo.Branch, githubToken)
    End Sub

    Public Sub SetSharedConfigurationGithubToken(githubToken As String)
        SharedConfigurationGithubTokenEncrypted = ProtectValue(If(githubToken, String.Empty).Trim())
    End Sub

    Public Function GetSharedConfigurationGithubToken() As String
        Return UnprotectValue(SharedConfigurationGithubTokenEncrypted)
    End Function

    Public Function IsSuperUser(username As String) As Boolean
        Dim cleanedUsername = NormalizeSuperUserName(username)
        If String.IsNullOrWhiteSpace(cleanedUsername) Then
            Return False
        End If

        ApplyDefaults()
        Return SuperUsers.Contains(cleanedUsername)
    End Function

    Public Sub AddSuperUser(username As String)
        Dim cleanedUsername = NormalizeSuperUserName(username)
        If String.IsNullOrWhiteSpace(cleanedUsername) Then
            Throw New ArgumentException("Enter a superuser login name.")
        End If

        ApplyDefaults()
        If Not SuperUsers.Contains(cleanedUsername) Then
            SuperUsers.Add(cleanedUsername)
        End If
    End Sub

    Public Sub RemoveSuperUser(username As String)
        Dim cleanedUsername = NormalizeSuperUserName(username)
        If String.Equals(cleanedUsername, DefaultSuperUser, StringComparison.OrdinalIgnoreCase) Then
            Throw New AppUserMessageException(DefaultSuperUser & " is the default superuser and cannot be removed.")
        End If

        ApplyDefaults()
        SuperUsers.Remove(cleanedUsername)
    End Sub

    Public Async Function SyncSuperUsersFromGithubAsync() As Task(Of Boolean)
        Dim syncToken = GetSharedConfigurationToken()

        Dim json = Await GithubRepositoryUploader.DownloadTextFileAsync(SharedConfigurationRepositoryUrl, SharedConfigurationBranch, syncToken, SuperUsersRepositoryPath)
        If String.IsNullOrWhiteSpace(json) Then
            Return False
        End If

        Dim downloadedUsers = ParseJsonStringArray(json)
        If downloadedUsers.Count = 0 Then
            Return False
        End If

        SuperUsers = NormalizeSuperUsers(downloadedUsers)
        ApplyDefaults()
        Save()
        Return True
    End Function

    Public Async Function SyncRepositoryMappingsFromGithubAsync() As Task(Of Boolean)
        Dim syncToken = GetSharedConfigurationToken()

        Dim json = Await GithubRepositoryUploader.DownloadTextFileAsync(SharedConfigurationRepositoryUrl, SharedConfigurationBranch, syncToken, RepositoryMappingsRepositoryPath)
        If String.IsNullOrWhiteSpace(json) Then
            Return False
        End If

        Dim downloadedMappings = ParseRepositoryMappingsJson(json)
        If downloadedMappings.Count = 0 Then
            Return False
        End If

        RepositoryMappings = downloadedMappings
        ApplyDefaults()
        SaveRepositoryMappings()

        Return True
    End Function

    Public Async Function UploadRepositoryMappingsToGithubAsync(templateKey As String) As Task
        Dim githubToken = GetSharedConfigurationWriteToken(templateKey)
        If String.IsNullOrWhiteSpace(githubToken) Then
            Throw New AppUserMessageException("Save a GitHub token with write access to the shared configuration repository first.")
        End If

        ApplyDefaults()
        SaveRepositoryMappings()

        Await GithubRepositoryUploader.UploadTextFileAsync(
            SharedConfigurationRepositoryUrl,
            SharedConfigurationBranch,
            githubToken,
            RepositoryMappingsRepositoryPath,
            SerializeRepositoryMappingsJson(),
            "Update shared repository configuration")
    End Function

    Public Async Function UploadSuperUsersToGithubAsync(templateKey As String) As Task
        Dim githubToken = GetSharedConfigurationWriteToken(templateKey)
        If String.IsNullOrWhiteSpace(githubToken) Then
            Throw New AppUserMessageException("Save a GitHub token with write access to the shared configuration repository first.")
        End If

        ApplyDefaults()
        Await GithubRepositoryUploader.UploadTextFileAsync(
            SharedConfigurationRepositoryUrl,
            SharedConfigurationBranch,
            githubToken,
            SuperUsersRepositoryPath,
            SerializeJsonStringArray(SuperUsers),
            "Update superuser list")
    End Function

    Private Function GetSharedConfigurationWriteToken(templateKey As String) As String
        Return GetSharedConfigurationToken()
    End Function

    Private Function GetFirstRepositoryAccessForDimensionsUrl(dimensionsUrl As String) As RepositoryAccessInfo
        Dim mapping = GetRepositoryMappingForDimensionsUrl(dimensionsUrl)
        If mapping Is Nothing Then
            Return Nothing
        End If

        For Each templateKey As String In mapping.GetTemplateKeys()
            Dim accessInfo = GetRepositoryAccessForDimensionsUrl(dimensionsUrl, templateKey)
            If accessInfo IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(accessInfo.GithubToken) Then
                Return accessInfo
            End If
        Next

        Return Nothing
    End Function

    Private Function GetFirstRepositoryAccess() As RepositoryAccessInfo
        ApplyDefaults()

        For Each dimensionsUrl As String In DimensionsUrls
            Dim accessInfo = GetFirstRepositoryAccessForDimensionsUrl(dimensionsUrl)
            If accessInfo IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(accessInfo.GithubToken) Then
                Return accessInfo
            End If
        Next

        Return Nothing
    End Function

    Private Function GetSharedConfigurationToken() As String
        Dim bundledSharedToken = LoadSharedConfigurationGithubToken()
        If Not String.IsNullOrWhiteSpace(bundledSharedToken) Then
            Return bundledSharedToken
        End If

        Dim savedSharedToken = GetSharedConfigurationGithubToken()
        If Not String.IsNullOrWhiteSpace(savedSharedToken) Then
            Return savedSharedToken
        End If

        Return String.Empty
    End Function

    Private Function GetFirstRepositoryAccessCandidate() As RepositoryAccessInfo
        ApplyDefaults()

        For Each dimensionsUrl As String In DimensionsUrls
            Dim mapping = GetRepositoryMappingForDimensionsUrl(dimensionsUrl)
            If mapping Is Nothing Then
                Continue For
            End If

            For Each templateKey As String In mapping.GetTemplateKeys()
                Dim accessInfo = GetRepositoryAccessForDimensionsUrl(dimensionsUrl, templateKey)
                If accessInfo IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(accessInfo.RepositoryUrl) Then
                    Return accessInfo
                End If
            Next
        Next

        Return Nothing
    End Function

    Private Function GetRepositoryAccessForDimensionsUrl(dimensionsUrl As String, templateKey As String) As RepositoryAccessInfo
        Dim mapping = GetRepositoryMappingForDimensionsUrl(dimensionsUrl)
        If mapping Is Nothing Then
            Return Nothing
        End If

        Dim templateInfo = mapping.GetTemplateInfo(templateKey)
        If templateInfo Is Nothing OrElse String.IsNullOrWhiteSpace(templateInfo.RepositoryUrl) Then
            Return Nothing
        End If

        Return New RepositoryAccessInfo With {
            .RepositoryUrl = templateInfo.RepositoryUrl,
            .Branch = If(String.IsNullOrWhiteSpace(templateInfo.Branch), DefaultRepositoryBranch, templateInfo.Branch),
            .GithubToken = templateInfo.GithubToken
        }
    End Function

    Public Function GetTemplateKeysForDimensionsUrl(dimensionsUrl As String) As List(Of String)
        Dim mapping = GetRepositoryMappingForDimensionsUrl(dimensionsUrl)
        If mapping Is Nothing Then
            Return New List(Of String)()
        End If

        Return mapping.GetTemplateKeys()
    End Function

    Private Function GetRepositoryMappingForDimensionsUrl(dimensionsUrl As String) As RepositoryTemplateMapping
        If RepositoryMappings Is Nothing OrElse String.IsNullOrWhiteSpace(dimensionsUrl) Then
            Return Nothing
        End If

        Dim normalizedUrl = NormalizeRepositoryMappingKey(dimensionsUrl)
        For Each mapping In RepositoryMappings
            Dim normalizedKey = NormalizeRepositoryMappingKey(mapping.Key)
            If String.IsNullOrWhiteSpace(normalizedKey) Then
                Continue For
            End If

            If normalizedUrl.Equals(normalizedKey, StringComparison.OrdinalIgnoreCase) OrElse
                normalizedUrl.StartsWith(normalizedKey & "/", StringComparison.OrdinalIgnoreCase) OrElse
                normalizedUrl.StartsWith(normalizedKey & ":", StringComparison.OrdinalIgnoreCase) Then
                Return mapping.Value
            End If
        Next

        Return Nothing
    End Function

    Public Function GetSavedPassword() As String
        If String.IsNullOrWhiteSpace(SavedPasswordEncrypted) Then
            Return String.Empty
        End If

        Try
            Dim encryptedBytes = Convert.FromBase64String(SavedPasswordEncrypted)
            Dim plainBytes = ProtectedData.Unprotect(encryptedBytes, Nothing, DataProtectionScope.CurrentUser)
            Return Encoding.UTF8.GetString(plainBytes)
        Catch
            Return String.Empty
        End Try
    End Function

    Public Sub SetSavedCredentials(username As String, password As String)
        SaveLoginCredentials = True
        SavedUsername = If(username, String.Empty)

        Dim plainBytes = Encoding.UTF8.GetBytes(If(password, String.Empty))
        Dim encryptedBytes = ProtectedData.Protect(plainBytes, Nothing, DataProtectionScope.CurrentUser)
        SavedPasswordEncrypted = Convert.ToBase64String(encryptedBytes)
    End Sub

    Public Sub ClearSavedCredentials()
        SaveLoginCredentials = False
        SavedUsername = String.Empty
        SavedPasswordEncrypted = String.Empty
    End Sub

    Public Sub SelectDimensionsUrl(url As String)
        DimensionsUrl = NormalizeDimensionsLoginUrl(url)
        ApplyDefaults()
    End Sub

    Public Sub AddDimensionsUrl(url As String)
        Dim normalizedUrl = NormalizeDimensionsLoginUrl(url)
        If String.IsNullOrWhiteSpace(normalizedUrl) Then
            Return
        End If

        DimensionsUrls = NormalizeUrls(DimensionsUrls)
        If Not DimensionsUrls.Contains(normalizedUrl) Then
            DimensionsUrls.Add(normalizedUrl)
        End If

        DimensionsUrl = normalizedUrl
    End Sub

    Public Sub RemoveDimensionsUrl(url As String)
        Dim normalizedUrl = NormalizeDimensionsLoginUrl(url)
        If String.IsNullOrWhiteSpace(normalizedUrl) Then
            Return
        End If

        Dim normalizedKey = NormalizeRepositoryMappingKey(normalizedUrl)
        DimensionsUrls = NormalizeUrls(DimensionsUrls).
            Where(Function(existingUrl) Not String.Equals(NormalizeRepositoryMappingKey(existingUrl), normalizedKey, StringComparison.OrdinalIgnoreCase)).
            ToList()

        If RepositoryMappings IsNot Nothing Then
            For Each mappingKey As String In RepositoryMappings.Keys.ToList()
                If String.Equals(NormalizeRepositoryMappingKey(mappingKey), normalizedKey, StringComparison.OrdinalIgnoreCase) Then
                    RepositoryMappings.Remove(mappingKey)
                End If
            Next
        End If

        If String.Equals(NormalizeRepositoryMappingKey(DimensionsUrl), normalizedKey, StringComparison.OrdinalIgnoreCase) Then
            DimensionsUrl = If(DimensionsUrls.Count > 0, DimensionsUrls(0), String.Empty)
        End If
    End Sub

    Private Shared Function ReadSettingsFile(path As String) As Dictionary(Of String, String)
        Dim values = New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

        For Each line In File.ReadAllLines(path)
            Dim separatorIndex = line.IndexOf("="c)
            If separatorIndex <= 0 Then
                Continue For
            End If

            Dim key = line.Substring(0, separatorIndex)
            Dim encodedValue = line.Substring(separatorIndex + 1)
            values(key) = Encoding.UTF8.GetString(Convert.FromBase64String(encodedValue))
        Next

        Return values
    End Function

    Private Shared Function GetValue(values As Dictionary(Of String, String), key As String) As String
        If values.ContainsKey(key) Then
            Return values(key)
        End If

        Return Nothing
    End Function

    Private Shared Function GetBooleanValue(values As Dictionary(Of String, String), key As String) As Boolean
        Dim rawValue = GetValue(values, key)
        Dim parsedValue As Boolean
        If Boolean.TryParse(rawValue, parsedValue) Then
            Return parsedValue
        End If

        Return False
    End Function

    Private Shared Function CreateLine(key As String, value As String) As String
        Dim encodedValue = Convert.ToBase64String(Encoding.UTF8.GetBytes(If(value, String.Empty)))
        Return key & "=" & encodedValue
    End Function

    Private Shared Function LoadRepositoryMappings() As Dictionary(Of String, RepositoryTemplateMapping)
        Dim mappings = New Dictionary(Of String, RepositoryTemplateMapping)(StringComparer.OrdinalIgnoreCase)

        If Not File.Exists(RepositoryMappingsPath) AndAlso File.Exists(LegacyRepositoryMappingsPath) Then
            Try
                Directory.CreateDirectory(Path.GetDirectoryName(RepositoryMappingsPath))
                File.Copy(LegacyRepositoryMappingsPath, RepositoryMappingsPath, overwrite:=False)
            Catch
            End Try
        End If

        Try
            Dim json = LoadRepositoryMappingsJson()
            mappings = ParseRepositoryMappingsJson(json)
        Catch
        End Try

        If mappings.Count = 0 Then
            Dim defaultMapping = New RepositoryTemplateMapping()
            defaultMapping.SetTemplateInfo("classic", DefaultClassicRepositoryUrl, DefaultClassicOldMddName, DefaultRepositoryBranch)
            defaultMapping.SetTemplateInfo("modern", DefaultModernRepositoryUrl, DefaultModernOldMddName, DefaultRepositoryBranch)
            mappings(DefaultDimensionsRepositoryHost) = defaultMapping
        End If

        Return mappings
    End Function

    Private Shared Function ParseRepositoryMappingsJson(json As String) As Dictionary(Of String, RepositoryTemplateMapping)
        Dim mappings = New Dictionary(Of String, RepositoryTemplateMapping)(StringComparer.OrdinalIgnoreCase)
        Dim rootProperties = ParseJsonObjectProperties(json)

        For Each rootProperty In rootProperties
            Dim key = rootProperty.Key.Trim()
            Dim value = rootProperty.Value.Trim()

            If String.IsNullOrWhiteSpace(key) OrElse String.IsNullOrWhiteSpace(value) Then
                Continue For
            End If

            If value.StartsWith("""", StringComparison.Ordinal) Then
                mappings(key) = New RepositoryTemplateMapping With {
                    .ClassicRepositoryUrl = UnquoteJsonString(value),
                    .ModernRepositoryUrl = String.Empty
                }
                Continue For
            End If

            If Not value.StartsWith("{", StringComparison.Ordinal) Then
                Continue For
            End If

            Dim repositoryMapping = New RepositoryTemplateMapping()
            Dim templateProperties = ParseJsonObjectProperties(value)

            For Each templateProperty In templateProperties
                Dim templateKey = templateProperty.Key.Trim()
                Dim templateValue = templateProperty.Value.Trim()

                If templateValue.StartsWith("""", StringComparison.Ordinal) Then
                    repositoryMapping.SetRepositoryUrl(templateKey, UnquoteJsonString(templateValue))
                ElseIf templateValue.StartsWith("{", StringComparison.Ordinal) Then
                    Dim templateDetails = ParseJsonObjectProperties(templateValue)
                    Dim repositoryUrl = GetJsonPropertyString(templateDetails, "repositoryUrl")
                    If String.IsNullOrWhiteSpace(repositoryUrl) Then
                        repositoryUrl = GetJsonPropertyString(templateDetails, "repoUrl")
                    End If
                    If String.IsNullOrWhiteSpace(repositoryUrl) Then
                        repositoryUrl = GetJsonPropertyString(templateDetails, "url")
                    End If

                    Dim oldMddName = GetJsonPropertyString(templateDetails, "oldMddName")
                    If String.IsNullOrWhiteSpace(oldMddName) Then
                        oldMddName = GetJsonPropertyString(templateDetails, "oldProjectName")
                    End If

                    Dim branch = GetJsonPropertyString(templateDetails, "branch")
                    If String.IsNullOrWhiteSpace(branch) Then
                        branch = GetJsonPropertyString(templateDetails, "defaultBranch")
                    End If

                    Dim githubToken = GetJsonPropertyString(templateDetails, "githubToken")
                    If String.IsNullOrWhiteSpace(githubToken) Then
                        githubToken = GetJsonPropertyString(templateDetails, "token")
                    End If
                    If String.IsNullOrWhiteSpace(githubToken) Then
                        githubToken = GetJsonPropertyString(templateDetails, "accessToken")
                    End If
                    If String.IsNullOrWhiteSpace(githubToken) Then
                        githubToken = UnprotectValue(GetJsonPropertyString(templateDetails, "githubTokenEncrypted"))
                    End If

                    repositoryMapping.SetTemplateInfo(templateKey, repositoryUrl, oldMddName, branch, githubToken)
                End If
            Next

            If repositoryMapping.GetTemplateKeys().Count > 0 Then
                mappings(key) = repositoryMapping
            End If
        Next

        Return mappings
    End Function

    Private Shared Function LoadRepositoryMappingsJson() As String
        If File.Exists(RepositoryMappingsPath) Then
            Return File.ReadAllText(RepositoryMappingsPath)
        End If

        Using stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MercurySurveyProgramming.repositories.json")
            If stream IsNot Nothing Then
                Using reader = New StreamReader(stream, Encoding.UTF8)
                    Return reader.ReadToEnd()
                End Using
            End If
        End Using

        Return "{" &
               """" & DefaultDimensionsRepositoryHost & """: {" &
               """classic"": {""repositoryUrl"": """ & DefaultClassicRepositoryUrl & """, ""branch"": """ & DefaultRepositoryBranch & """, ""oldMddName"": """ & DefaultClassicOldMddName & """}," &
               """modern"": {""repositoryUrl"": """ & DefaultModernRepositoryUrl & """, ""branch"": """ & DefaultRepositoryBranch & """, ""oldMddName"": """ & DefaultModernOldMddName & """}" &
               "}}"
    End Function

    Private Sub SaveRepositoryMappings()
        Try
            Directory.CreateDirectory(Path.GetDirectoryName(RepositoryMappingsPath))
            File.WriteAllText(RepositoryMappingsPath, SerializeRepositoryMappingsJson())
        Catch ex As Exception
            AppLogger.Error("Could not save repository mappings.", ex)
        End Try
    End Sub

    Private Function SerializeRepositoryMappingsJson() As String
        Dim lines = New List(Of String) From {"{"}
        Dim orderedMappings = RepositoryMappings.Keys.OrderBy(Function(key) key, StringComparer.OrdinalIgnoreCase).ToList()

        For index As Integer = 0 To orderedMappings.Count - 1
            Dim key = orderedMappings(index)
            Dim suffix = If(index = orderedMappings.Count - 1, String.Empty, ",")
            Dim mapping = RepositoryMappings(key)
            lines.Add("  """ & EscapeJsonString(key) & """: {")
            Dim templateKeys = mapping.GetTemplateKeys()

            For templateIndex As Integer = 0 To templateKeys.Count - 1
                Dim templateKey = templateKeys(templateIndex)
                Dim templateSuffix = If(templateIndex = templateKeys.Count - 1, String.Empty, ",")
                Dim templateInfo = mapping.GetTemplateInfo(templateKey)
                lines.Add("    """ & EscapeJsonString(templateKey) & """: {")
                lines.Add("      ""repositoryUrl"": """ & EscapeJsonString(templateInfo.RepositoryUrl) & """,")
                lines.Add("      ""branch"": """ & EscapeJsonString(templateInfo.Branch) & """,")
                lines.Add("      ""githubToken"": """",")
                lines.Add("      ""githubTokenEncrypted"": """ & EscapeJsonString(ProtectSharedValue(templateInfo.GithubToken)) & """,")
                lines.Add("      ""oldMddName"": """ & EscapeJsonString(templateInfo.OldMddName) & """")
                lines.Add("    }" & templateSuffix)
            Next

            lines.Add("  }" & suffix)
        Next

        lines.Add("}")
        Return String.Join(Environment.NewLine, lines.ToArray())
    End Function

    Private Shared Function NormalizeRepositoryMappingKey(value As String) As String
        Dim normalized = If(value, String.Empty).Trim().ToLowerInvariant()
        normalized = Regex.Replace(normalized, "^https?://", String.Empty)
        normalized = normalized.Trim("/"c, " "c)
        Return normalized
    End Function

    Private Shared Function CreateDimensionsUrlFromMappingKey(value As String) As String
        Dim cleaned = If(value, String.Empty).Trim()
        If String.IsNullOrWhiteSpace(cleaned) Then
            Return String.Empty
        End If

        Return NormalizeDimensionsLoginUrl(cleaned)
    End Function

    Private Shared Function NormalizeDimensionsLoginUrl(value As String) As String
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

    Private Shared Function EscapeJsonString(value As String) As String
        Return If(value, String.Empty).Replace("\", "\\").Replace("""", "\""")
    End Function

    Private Shared Function UnescapeJsonString(value As String) As String
        Return If(value, String.Empty).Replace("\""", """").Replace("\\", "\")
    End Function

    Private Shared Function UnquoteJsonString(value As String) As String
        Dim trimmedValue = If(value, String.Empty).Trim()
        If trimmedValue.Length >= 2 AndAlso trimmedValue.StartsWith("""", StringComparison.Ordinal) AndAlso trimmedValue.EndsWith("""", StringComparison.Ordinal) Then
            Return UnescapeJsonString(trimmedValue.Substring(1, trimmedValue.Length - 2)).Trim()
        End If

        Return UnescapeJsonString(trimmedValue).Trim()
    End Function

    Private Shared Function GetJsonPropertyString(properties As Dictionary(Of String, String), propertyName As String) As String
        If properties.ContainsKey(propertyName) Then
            Return UnquoteJsonString(properties(propertyName))
        End If

        Return Nothing
    End Function

    Private Shared Function ProtectValue(value As String) As String
        If String.IsNullOrWhiteSpace(value) Then
            Return String.Empty
        End If

        Try
            Dim plainBytes = Encoding.UTF8.GetBytes(value)
            Dim encryptedBytes = ProtectedData.Protect(plainBytes, Nothing, DataProtectionScope.CurrentUser)
            Return Convert.ToBase64String(encryptedBytes)
        Catch
            Return String.Empty
        End Try
    End Function

    Private Shared Function UnprotectValue(value As String) As String
        If String.IsNullOrWhiteSpace(value) Then
            Return String.Empty
        End If

        If value.StartsWith(SharedProtectedPrefix, StringComparison.OrdinalIgnoreCase) Then
            Return UnprotectSharedValue(value)
        End If

        Try
            Dim encryptedBytes = Convert.FromBase64String(value)
            Dim plainBytes = ProtectedData.Unprotect(encryptedBytes, Nothing, DataProtectionScope.CurrentUser)
            Return Encoding.UTF8.GetString(plainBytes)
        Catch
            Return String.Empty
        End Try
    End Function

    Private Shared Function LoadSharedConfigurationGithubToken() As String
        If Not File.Exists(SharedConfigurationTokenPath) Then
            Return String.Empty
        End If

        Try
            Dim rawText = File.ReadAllText(SharedConfigurationTokenPath).Trim()
            If String.IsNullOrWhiteSpace(rawText) Then
                Return String.Empty
            End If

            Dim properties = ParseSimpleTokenFile(rawText)
            If properties.ContainsKey("githubTokenEncrypted") Then
                Dim decrypted = UnprotectValue(properties("githubTokenEncrypted"))
                If Not String.IsNullOrWhiteSpace(decrypted) Then
                    Return decrypted
                End If
            End If

            If properties.ContainsKey("githubToken") Then
                Return properties("githubToken").Trim()
            End If

            If properties.ContainsKey("token") Then
                Return properties("token").Trim()
            End If

            Return rawText.Split({Convert.ToChar(13), Convert.ToChar(10)}, StringSplitOptions.RemoveEmptyEntries)(0).Trim()
        Catch ex As Exception
            AppLogger.Error("Could not read shared configuration GitHub token.", ex)
            Return String.Empty
        End Try
    End Function

    Private Shared Function ParseSimpleTokenFile(rawText As String) As Dictionary(Of String, String)
        Dim values = New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

        For Each line As String In rawText.Split({Convert.ToChar(13), Convert.ToChar(10)}, StringSplitOptions.RemoveEmptyEntries)
            Dim cleanedLine = line.Trim()
            If String.IsNullOrWhiteSpace(cleanedLine) OrElse cleanedLine.StartsWith("#", StringComparison.Ordinal) Then
                Continue For
            End If

            Dim separatorIndex = cleanedLine.IndexOf("="c)
            If separatorIndex <= 0 Then
                Continue For
            End If

            values(cleanedLine.Substring(0, separatorIndex).Trim()) = cleanedLine.Substring(separatorIndex + 1).Trim()
        Next

        Return values
    End Function

    Private Shared Function ContainsAnyGithubToken(mappings As Dictionary(Of String, RepositoryTemplateMapping)) As Boolean
        If mappings Is Nothing Then
            Return False
        End If

        For Each mapping In mappings.Values
            If mapping Is Nothing Then
                Continue For
            End If

            For Each templateKey As String In mapping.GetTemplateKeys()
                If Not String.IsNullOrWhiteSpace(mapping.GetGithubToken(templateKey)) Then
                    Return True
                End If
            Next
        Next

        Return False
    End Function

    Private Shared Function ProtectSharedValue(value As String) As String
        If String.IsNullOrWhiteSpace(value) Then
            Return String.Empty
        End If

        Try
            Using aes = New AesManaged()
                aes.KeySize = 256
                aes.BlockSize = 128
                aes.Mode = CipherMode.CBC
                aes.Padding = PaddingMode.PKCS7
                aes.Key = GetSharedProtectionKey()
                aes.GenerateIV()

                Dim plainBytes = Encoding.UTF8.GetBytes(value)
                Using output = New MemoryStream()
                    output.Write(aes.IV, 0, aes.IV.Length)
                    Using crypto = New CryptoStream(output, aes.CreateEncryptor(), CryptoStreamMode.Write)
                        crypto.Write(plainBytes, 0, plainBytes.Length)
                        crypto.FlushFinalBlock()
                    End Using

                    Return SharedProtectedPrefix & Convert.ToBase64String(output.ToArray())
                End Using
            End Using
        Catch
            Return String.Empty
        End Try
    End Function

    Private Shared Function UnprotectSharedValue(value As String) As String
        Try
            Dim payload = value.Substring(SharedProtectedPrefix.Length)
            Dim allBytes = Convert.FromBase64String(payload)
            If allBytes.Length <= 16 Then
                Return String.Empty
            End If

            Dim iv(15) As Byte
            Dim cipherBytes(allBytes.Length - 17) As Byte
            Buffer.BlockCopy(allBytes, 0, iv, 0, iv.Length)
            Buffer.BlockCopy(allBytes, iv.Length, cipherBytes, 0, cipherBytes.Length)

            Using aes = New AesManaged()
                aes.KeySize = 256
                aes.BlockSize = 128
                aes.Mode = CipherMode.CBC
                aes.Padding = PaddingMode.PKCS7
                aes.Key = GetSharedProtectionKey()
                aes.IV = iv

                Using input = New MemoryStream(cipherBytes)
                    Using crypto = New CryptoStream(input, aes.CreateDecryptor(), CryptoStreamMode.Read)
                        Using reader = New StreamReader(crypto, Encoding.UTF8)
                            Return reader.ReadToEnd()
                        End Using
                    End Using
                End Using
            End Using
        Catch
            Return String.Empty
        End Try
    End Function

    Private Shared Function GetSharedProtectionKey() As Byte()
        Using sha = SHA256.Create()
            Return sha.ComputeHash(Encoding.UTF8.GetBytes(SharedProtectionPurpose))
        End Using
    End Function

    Private Shared Function ParseJsonObjectProperties(json As String) As Dictionary(Of String, String)
        Dim properties = New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        Dim text = If(json, String.Empty)
        Dim index = 0

        SkipWhiteSpace(text, index)
        If index < text.Length AndAlso text(index) = "{"c Then
            index += 1
        End If

        While index < text.Length
            SkipWhiteSpaceAndCommas(text, index)
            If index >= text.Length OrElse text(index) = "}"c Then
                Exit While
            End If

            If text(index) <> """"c Then
                index += 1
                Continue While
            End If

            Dim key = ReadJsonString(text, index)
            SkipWhiteSpace(text, index)
            If index >= text.Length OrElse text(index) <> ":"c Then
                Continue While
            End If

            index += 1
            SkipWhiteSpace(text, index)
            Dim valueStart = index
            Dim valueEnd = FindJsonValueEnd(text, index)

            If valueEnd > valueStart Then
                properties(key) = text.Substring(valueStart, valueEnd - valueStart).Trim()
            End If

            index = valueEnd
        End While

        Return properties
    End Function

    Private Shared Sub SkipWhiteSpace(text As String, ByRef index As Integer)
        While index < text.Length AndAlso Char.IsWhiteSpace(text(index))
            index += 1
        End While
    End Sub

    Private Shared Sub SkipWhiteSpaceAndCommas(text As String, ByRef index As Integer)
        While index < text.Length AndAlso (Char.IsWhiteSpace(text(index)) OrElse text(index) = ","c)
            index += 1
        End While
    End Sub

    Private Shared Function ReadJsonString(text As String, ByRef index As Integer) As String
        Dim builder = New StringBuilder()
        If index >= text.Length OrElse text(index) <> """"c Then
            Return String.Empty
        End If

        index += 1
        While index < text.Length
            Dim currentChar = text(index)
            If currentChar = "\"c AndAlso index + 1 < text.Length Then
                builder.Append(currentChar)
                index += 1
                builder.Append(text(index))
            ElseIf currentChar = """"c Then
                index += 1
                Return UnescapeJsonString(builder.ToString())
            Else
                builder.Append(currentChar)
            End If

            index += 1
        End While

        Return UnescapeJsonString(builder.ToString())
    End Function

    Private Shared Function FindJsonValueEnd(text As String, startIndex As Integer) As Integer
        If startIndex >= text.Length Then
            Return startIndex
        End If

        If text(startIndex) = """"c Then
            Dim tempIndex = startIndex
            ReadJsonString(text, tempIndex)
            Return tempIndex
        End If

        If text(startIndex) = "{"c Then
            Dim depth = 0
            Dim index = startIndex

            While index < text.Length
                If text(index) = """"c Then
                    ReadJsonString(text, index)
                    Continue While
                End If

                If text(index) = "{"c Then
                    depth += 1
                ElseIf text(index) = "}"c Then
                    depth -= 1
                    If depth = 0 Then
                        Return index + 1
                    End If
                End If

                index += 1
            End While

            Return text.Length
        End If

        Dim simpleIndex = startIndex
        While simpleIndex < text.Length AndAlso text(simpleIndex) <> ","c AndAlso text(simpleIndex) <> "}"c
            simpleIndex += 1
        End While

        Return simpleIndex
    End Function

    Private Shared Function ParseList(value As String) As List(Of String)
        Dim items = New List(Of String)()
        If String.IsNullOrWhiteSpace(value) Then
            Return items
        End If

        For Each item As String In value.Split(New String() {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries)
            Dim cleaned = item.Trim()
            If Not String.IsNullOrWhiteSpace(cleaned) AndAlso Not items.Contains(cleaned) Then
                items.Add(cleaned)
            End If
        Next

        Return items
    End Function

    Private Shared Function NormalizeUrls(urls As List(Of String)) As List(Of String)
        Dim normalized = New List(Of String)()
        If urls Is Nothing Then
            Return normalized
        End If

        For Each url As String In urls
            Dim cleaned = NormalizeDimensionsLoginUrl(url)
            If Not String.IsNullOrWhiteSpace(cleaned) AndAlso Not normalized.Contains(cleaned) Then
                normalized.Add(cleaned)
            End If
        Next

        Return normalized
    End Function

    Private Shared Function NormalizeSuperUsers(users As List(Of String)) As List(Of String)
        Dim normalized = New List(Of String)()
        If users Is Nothing Then
            Return normalized
        End If

        For Each username As String In users
            Dim cleaned = NormalizeSuperUserName(username)
            If Not String.IsNullOrWhiteSpace(cleaned) AndAlso Not normalized.Contains(cleaned) Then
                normalized.Add(cleaned)
            End If
        Next

        Return normalized
    End Function

    Private Shared Function NormalizeSuperUserName(username As String) As String
        Return If(username, String.Empty).Trim().ToLowerInvariant()
    End Function

    Private Shared Function ParseJsonStringArray(json As String) As List(Of String)
        Dim users = New List(Of String)()
        For Each match As Match In Regex.Matches(If(json, String.Empty), """(?<value>(?:\\.|[^""])*)""", RegexOptions.Singleline)
            Dim username = UnescapeJsonString(match.Groups("value").Value).Trim()
            If Not String.IsNullOrWhiteSpace(username) Then
                users.Add(username)
            End If
        Next

        Return users
    End Function

    Private Shared Function SerializeJsonStringArray(values As List(Of String)) As String
        Dim normalizedValues = NormalizeSuperUsers(values)
        Dim lines = New List(Of String) From {"["}

        For index As Integer = 0 To normalizedValues.Count - 1
            Dim suffix = If(index = normalizedValues.Count - 1, String.Empty, ",")
            lines.Add("  """ & EscapeJsonString(normalizedValues(index)) & """" & suffix)
        Next

        lines.Add("]")
        Return String.Join(Environment.NewLine, lines.ToArray())
    End Function
End Class

Public Class RepositoryTemplateMapping
    Public Property Templates As New Dictionary(Of String, RepositoryTemplateInfo)(StringComparer.OrdinalIgnoreCase)

    Public Property ClassicRepositoryUrl As String
        Get
            Return GetRepositoryUrl("classic")
        End Get
        Set(value As String)
            SetRepositoryUrl("classic", value)
        End Set
    End Property

    Public Property ModernRepositoryUrl As String
        Get
            Return GetRepositoryUrl("modern")
        End Get
        Set(value As String)
            SetRepositoryUrl("modern", value)
        End Set
    End Property

    Public Sub SetRepositoryUrl(templateKey As String, repositoryUrl As String)
        SetTemplateInfo(templateKey, repositoryUrl, GetOldMddName(templateKey), GetBranch(templateKey), GetGithubToken(templateKey))
    End Sub

    Public Sub SetTemplateInfo(templateKey As String, repositoryUrl As String, oldMddName As String)
        SetTemplateInfo(templateKey, repositoryUrl, oldMddName, GetBranch(templateKey), GetGithubToken(templateKey))
    End Sub

    Public Sub SetTemplateInfo(templateKey As String, repositoryUrl As String, oldMddName As String, branch As String)
        SetTemplateInfo(templateKey, repositoryUrl, oldMddName, branch, GetGithubToken(templateKey))
    End Sub

    Public Sub SetTemplateInfo(templateKey As String, repositoryUrl As String, oldMddName As String, branch As String, githubToken As String)
        Dim cleanedKey = If(templateKey, String.Empty).Trim().ToLowerInvariant()
        Dim cleanedUrl = If(repositoryUrl, String.Empty).Trim()
        Dim cleanedOldMddName = If(oldMddName, String.Empty).Trim()
        Dim cleanedBranch = If(branch, String.Empty).Trim()
        Dim cleanedGithubToken = If(githubToken, String.Empty).Trim()

        If String.IsNullOrWhiteSpace(cleanedKey) OrElse String.IsNullOrWhiteSpace(cleanedUrl) Then
            Return
        End If

        Templates(cleanedKey) = New RepositoryTemplateInfo With {
            .RepositoryUrl = cleanedUrl,
            .OldMddName = cleanedOldMddName,
            .Branch = cleanedBranch,
            .GithubToken = cleanedGithubToken
        }
    End Sub

    Public Function GetRepositoryUrl(templateKey As String) As String
        Dim templateInfo = GetTemplateInfo(templateKey)
        If templateInfo IsNot Nothing Then
            Return templateInfo.RepositoryUrl
        End If

        Return Nothing
    End Function

    Public Function GetOldMddName(templateKey As String) As String
        Dim templateInfo = GetTemplateInfo(templateKey)
        If templateInfo IsNot Nothing Then
            Return templateInfo.OldMddName
        End If

        Return Nothing
    End Function

    Public Function GetBranch(templateKey As String) As String
        Dim templateInfo = GetTemplateInfo(templateKey)
        If templateInfo IsNot Nothing Then
            Return templateInfo.Branch
        End If

        Return Nothing
    End Function

    Public Function GetGithubToken(templateKey As String) As String
        Dim templateInfo = GetTemplateInfo(templateKey)
        If templateInfo IsNot Nothing Then
            Return templateInfo.GithubToken
        End If

        Return Nothing
    End Function

    Public Function GetTemplateInfo(templateKey As String) As RepositoryTemplateInfo
        Dim cleanedKey = If(templateKey, String.Empty).Trim().ToLowerInvariant()
        If String.IsNullOrWhiteSpace(cleanedKey) Then
            cleanedKey = "classic"
        End If

        If Templates IsNot Nothing AndAlso Templates.ContainsKey(cleanedKey) Then
            Return Templates(cleanedKey)
        End If

        Return Nothing
    End Function

    Public Function GetTemplateKeys() As List(Of String)
        If Templates Is Nothing Then
            Return New List(Of String)()
        End If

        Return Templates.Keys.OrderBy(Function(key) GetTemplateSortValue(key)).ThenBy(Function(key) key).ToList()
    End Function

    Private Shared Function GetTemplateSortValue(templateKey As String) As Integer
        If String.Equals(templateKey, "classic", StringComparison.OrdinalIgnoreCase) Then
            Return 0
        End If

        If String.Equals(templateKey, "modern", StringComparison.OrdinalIgnoreCase) Then
            Return 1
        End If

        Return 10
    End Function
End Class

Public Class RepositoryTemplateInfo
    Public Property RepositoryUrl As String
    Public Property OldMddName As String
    Public Property Branch As String
    Public Property GithubToken As String
End Class

Public Class RepositoryAccessInfo
    Public Property RepositoryUrl As String
    Public Property Branch As String
    Public Property GithubToken As String
End Class
