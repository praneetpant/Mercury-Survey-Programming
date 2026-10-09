Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Net
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading.Tasks

Public Class GithubRepositoryUploader
    Private Shared ReadOnly Http As New HttpClient()

    Public Shared Async Function DownloadTextFileAsync(repoUrl As String, branch As String, githubToken As String, relativePath As String) As Task(Of String)
        If String.IsNullOrWhiteSpace(branch) Then
            branch = "main"
        End If

        Dim repo = ParseGithubRepository(repoUrl)
        Dim remoteFile = Await GetRemoteFileInfoAsync(repo, branch, relativePath, githubToken)

        If Not remoteFile.Exists OrElse remoteFile.ContentBytes Is Nothing Then
            Return Nothing
        End If

        Return Encoding.UTF8.GetString(remoteFile.ContentBytes)
    End Function

    Public Shared Async Function UploadTextFileAsync(repoUrl As String, branch As String, githubToken As String, relativePath As String, text As String, commitMessage As String) As Task
        If String.IsNullOrWhiteSpace(githubToken) Then
            Throw New ArgumentException("A GitHub token is required.")
        End If

        If String.IsNullOrWhiteSpace(branch) Then
            branch = "main"
        End If

        If String.IsNullOrWhiteSpace(commitMessage) Then
            commitMessage = "Update " & relativePath
        End If

        Dim repo = ParseGithubRepository(repoUrl)
        Dim remoteFile = Await GetRemoteFileInfoAsync(repo, branch, relativePath, githubToken)
        Dim fileBytes = Encoding.UTF8.GetBytes(If(text, String.Empty))

        If remoteFile.Exists AndAlso remoteFile.ContentBytes IsNot Nothing AndAlso ByteArraysEqual(remoteFile.ContentBytes, fileBytes) Then
            Return
        End If

        Await PutFileAsync(repo, branch, relativePath, fileBytes, remoteFile.Sha, commitMessage, githubToken)
    End Function

    Public Shared Async Function UploadDirectoryAsync(repoUrl As String, branch As String, githubToken As String, sourceDirectory As String, commitMessage As String, progress As Action(Of UploadProgress)) As Task(Of UploadSummary)
        If String.IsNullOrWhiteSpace(githubToken) Then
            Throw New ArgumentException("A GitHub token is required for upload.")
        End If

        If String.IsNullOrWhiteSpace(sourceDirectory) OrElse Not Directory.Exists(sourceDirectory) Then
            Throw New DirectoryNotFoundException("Select a local folder to upload.")
        End If

        If String.IsNullOrWhiteSpace(branch) Then
            branch = "main"
        End If

        If String.IsNullOrWhiteSpace(commitMessage) Then
            commitMessage = "Update template files"
        End If

        Dim repo = ParseGithubRepository(repoUrl)
        Dim summary = New UploadSummary()

        progress?.Invoke(New UploadProgress With {.Message = "Checking folder structure against GitHub...", .Current = 0, .Total = 0})
        Dim remoteEntries = Await GetRemoteTreeEntriesAsync(repo, branch, githubToken)
        ValidateSourceDirectoryMatchesRepository(remoteEntries, sourceDirectory)

        Dim files = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories).
            Where(Function(path) Not ShouldSkipPath(sourceDirectory, path)).
            OrderBy(Function(path) path, StringComparer.OrdinalIgnoreCase).
            ToList()

        If files.Count = 0 Then
            Throw New AppUserMessageException("The selected folder does not contain files to upload.")
        End If

        Dim currentIndex As Integer = 0
        Dim localRelativePaths = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        For Each filePath As String In files
            currentIndex += 1
            Dim relativePath = GetRepositoryRelativePath(sourceDirectory, filePath)
            localRelativePaths.Add(relativePath)
            progress?.Invoke(New UploadProgress With {.Message = "Checking " & relativePath & "...", .Current = currentIndex, .Total = files.Count})

            Dim remoteFile = Await GetRemoteFileInfoAsync(repo, branch, relativePath, githubToken)
            Dim fileBytes = File.ReadAllBytes(filePath)

            If remoteFile.Exists AndAlso remoteFile.ContentBytes IsNot Nothing AndAlso ByteArraysEqual(remoteFile.ContentBytes, fileBytes) Then
                summary.Skipped += 1
                AppLogger.Info("Super User upload skipped unchanged file: " & relativePath)
                Continue For
            End If

            progress?.Invoke(New UploadProgress With {.Message = If(remoteFile.Exists, "Updating ", "Creating ") & relativePath & "...", .Current = currentIndex, .Total = files.Count})
            Await PutFileAsync(repo, branch, relativePath, fileBytes, remoteFile.Sha, commitMessage, githubToken)

            If remoteFile.Exists Then
                summary.Updated += 1
            Else
                summary.Created += 1
            End If
        Next

        Dim remoteFilesToDelete = remoteEntries.
            Where(Function(entry) String.Equals(entry.Type, "blob", StringComparison.OrdinalIgnoreCase)).
            Where(Function(entry) Not String.IsNullOrWhiteSpace(entry.Path)).
            Where(Function(entry) Not ShouldSkipRelativePath(entry.Path)).
            Where(Function(entry) Not localRelativePaths.Contains(entry.Path)).
            OrderBy(Function(entry) entry.Path, StringComparer.OrdinalIgnoreCase).
            ToList()

        Dim deleteIndex As Integer = 0
        For Each entry As RemoteTreeEntry In remoteFilesToDelete
            deleteIndex += 1
            progress?.Invoke(New UploadProgress With {.Message = "Deleting " & entry.Path & "...", .Current = deleteIndex, .Total = remoteFilesToDelete.Count})
            Dim sha = entry.Sha
            If String.IsNullOrWhiteSpace(sha) Then
                Dim remoteFile = Await GetRemoteFileInfoAsync(repo, branch, entry.Path, githubToken)
                sha = remoteFile.Sha
            End If

            Await DeleteFileAsync(repo, branch, entry.Path, sha, commitMessage, githubToken)
            summary.Deleted += 1
            AppLogger.Info("Super User upload deleted remote file missing locally: " & entry.Path)
        Next

        progress?.Invoke(New UploadProgress With {.Message = "Upload completed.", .Current = files.Count + remoteFilesToDelete.Count, .Total = files.Count + remoteFilesToDelete.Count})
        Return summary
    End Function

    Private Shared Sub ValidateSourceDirectoryMatchesRepository(remoteEntries As List(Of RemoteTreeEntry), sourceDirectory As String)
        Dim requiredTopLevelEntries = remoteEntries.
            Where(Function(entry) Not String.IsNullOrWhiteSpace(entry.Path)).
            Where(Function(entry) Not ShouldSkipRelativePath(entry.Path)).
            Select(Function(entry) GetTopLevelRepositoryEntry(entry.Path)).
            Where(Function(entry) Not String.IsNullOrWhiteSpace(entry)).
            Distinct(StringComparer.OrdinalIgnoreCase).
            OrderBy(Function(entry) entry, StringComparer.OrdinalIgnoreCase).
            ToList()

        If requiredTopLevelEntries.Count = 0 Then
            Return
        End If

        Dim missingPaths As New List(Of String)()

        For Each entryName As String In requiredTopLevelEntries
            Dim localPath = Path.Combine(sourceDirectory, entryName)

            If Not Directory.Exists(localPath) AndAlso Not File.Exists(localPath) Then
                missingPaths.Add(entryName)
            End If
        Next

        If missingPaths.Count > 0 Then
            Dim shownPaths = String.Join(Environment.NewLine, missingPaths.Take(10))
            If missingPaths.Count > 10 Then
                shownPaths &= Environment.NewLine & "...and " & (missingPaths.Count - 10).ToString() & " more"
            End If

            Throw New AppUserMessageException(
                "The selected folder does not match the selected GitHub template repository." & Environment.NewLine & Environment.NewLine &
                "Missing required top-level folder/file from selected folder:" & Environment.NewLine & shownPaths & Environment.NewLine & Environment.NewLine &
                "Select the template repository root folder before uploading.")
        End If
    End Sub

    Private Shared Function GetTopLevelRepositoryEntry(relativePath As String) As String
        Dim normalizedPath = If(relativePath, String.Empty).Replace("\"c, "/"c).Trim("/"c)
        If String.IsNullOrWhiteSpace(normalizedPath) Then
            Return String.Empty
        End If

        Dim separatorIndex = normalizedPath.IndexOf("/"c)
        If separatorIndex < 0 Then
            Return normalizedPath
        End If

        Return normalizedPath.Substring(0, separatorIndex)
    End Function

    Private Shared Async Function GetRemoteTreeEntriesAsync(repo As GithubRepository, branch As String, githubToken As String) As Task(Of List(Of RemoteTreeEntry))
        Dim url = "https://api.github.com/repos/" & Uri.EscapeDataString(repo.Owner) & "/" & Uri.EscapeDataString(repo.Name) & "/git/trees/" & Uri.EscapeDataString(branch) & "?recursive=1"

        Using request = CreateRequest(HttpMethod.Get, url, githubToken)
            Using response = Await Http.SendAsync(request)
                Dim responseText = Await response.Content.ReadAsStringAsync()
                If Not response.IsSuccessStatusCode Then
                    Throw New AppUserMessageException("GitHub could not read the repository folder structure. HTTP " & CInt(response.StatusCode) & ": " & responseText)
                End If

                Dim entries As New List(Of RemoteTreeEntry)()
                For Each match As Match In Regex.Matches(responseText, "\{(?<object>[^{}]*)\}", RegexOptions.Singleline)
                    Dim objectText = match.Groups("object").Value
                    Dim pathValue = GetJsonString(objectText, "path")
                    Dim typeValue = GetJsonString(objectText, "type")
                    Dim shaValue = GetJsonString(objectText, "sha")

                    If Not String.IsNullOrWhiteSpace(pathValue) AndAlso
                        (String.Equals(typeValue, "blob", StringComparison.OrdinalIgnoreCase) OrElse String.Equals(typeValue, "tree", StringComparison.OrdinalIgnoreCase)) Then
                        entries.Add(New RemoteTreeEntry With {.Path = pathValue, .Type = typeValue, .Sha = shaValue})
                    End If
                Next

                Return entries
            End Using
        End Using
    End Function

    Private Shared Async Function DeleteFileAsync(repo As GithubRepository, branch As String, relativePath As String, sha As String, commitMessage As String, githubToken As String) As Task
        If String.IsNullOrWhiteSpace(sha) Then
            Throw New AppUserMessageException("GitHub could not delete " & relativePath & " because its current SHA was not found.")
        End If

        Dim url = BuildContentsUrl(repo, relativePath)
        Dim body = New StringBuilder()
        body.Append("{")
        body.Append("""message"":""" & EscapeJsonString(commitMessage) & """,")
        body.Append("""branch"":""" & EscapeJsonString(branch) & """,")
        body.Append("""sha"":""" & EscapeJsonString(sha) & """")
        body.Append("}")

        Using request = CreateRequest(HttpMethod.Delete, url, githubToken)
            request.Content = New StringContent(body.ToString(), Encoding.UTF8, "application/json")
            Using response = Await Http.SendAsync(request)
                If response.StatusCode <> HttpStatusCode.OK Then
                    Dim responseText = Await response.Content.ReadAsStringAsync()
                    Throw New AppUserMessageException("GitHub could not delete " & relativePath & ". HTTP " & CInt(response.StatusCode) & ": " & responseText)
                End If
            End Using
        End Using
    End Function

    Private Shared Async Function GetRemoteFileInfoAsync(repo As GithubRepository, branch As String, relativePath As String, githubToken As String) As Task(Of RemoteFileInfo)
        Dim url = BuildContentsUrl(repo, relativePath) & "?ref=" & Uri.EscapeDataString(branch)

        Using request = CreateRequest(HttpMethod.Get, url, githubToken)
            Using response = Await Http.SendAsync(request)
                If response.StatusCode = HttpStatusCode.NotFound Then
                    Return New RemoteFileInfo With {.Exists = False}
                End If

                Dim responseText = Await response.Content.ReadAsStringAsync()
                If Not response.IsSuccessStatusCode Then
                    Throw New AppUserMessageException("GitHub could not read " & relativePath & ". HTTP " & CInt(response.StatusCode) & ": " & responseText)
                End If

                Dim sha = GetJsonString(responseText, "sha")
                Dim encodingName = GetJsonString(responseText, "encoding")
                Dim encodedContent = GetJsonString(responseText, "content")
                Dim contentBytes As Byte() = Nothing

                If String.Equals(encodingName, "base64", StringComparison.OrdinalIgnoreCase) AndAlso Not String.IsNullOrWhiteSpace(encodedContent) Then
                    Try
                        contentBytes = Convert.FromBase64String(Regex.Replace(encodedContent, "\s+", String.Empty))
                    Catch
                    End Try
                End If

                Return New RemoteFileInfo With {
                    .Exists = True,
                    .Sha = sha,
                    .ContentBytes = contentBytes
                }
            End Using
        End Using
    End Function

    Private Shared Async Function PutFileAsync(repo As GithubRepository, branch As String, relativePath As String, fileBytes As Byte(), sha As String, commitMessage As String, githubToken As String) As Task
        Dim url = BuildContentsUrl(repo, relativePath)
        Dim body = New StringBuilder()
        body.Append("{")
        body.Append("""message"":""" & EscapeJsonString(commitMessage) & """,")
        body.Append("""branch"":""" & EscapeJsonString(branch) & """,")
        body.Append("""content"":""" & Convert.ToBase64String(fileBytes) & """")

        If Not String.IsNullOrWhiteSpace(sha) Then
            body.Append(",""sha"":""" & EscapeJsonString(sha) & """")
        End If

        body.Append("}")

        Using request = CreateRequest(HttpMethod.Put, url, githubToken)
            request.Content = New StringContent(body.ToString(), Encoding.UTF8, "application/json")
            Using response = Await Http.SendAsync(request)
                If response.StatusCode <> HttpStatusCode.OK AndAlso response.StatusCode <> HttpStatusCode.Created Then
                    Dim responseText = Await response.Content.ReadAsStringAsync()
                    Throw New AppUserMessageException("GitHub could not upload " & relativePath & ". HTTP " & CInt(response.StatusCode) & ": " & responseText)
                End If
            End Using
        End Using
    End Function

    Private Shared Function CreateRequest(method As HttpMethod, url As String, githubToken As String) As HttpRequestMessage
        Dim request = New HttpRequestMessage(method, url)
        request.Headers.Accept.Add(New MediaTypeWithQualityHeaderValue("application/vnd.github+json"))
        request.Headers.UserAgent.ParseAdd("MercurySurveyProgramming/1.0")
        If Not String.IsNullOrWhiteSpace(githubToken) Then
            request.Headers.Authorization = New AuthenticationHeaderValue("Bearer", githubToken.Trim())
        End If
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28")
        Return request
    End Function

    Private Shared Function BuildContentsUrl(repo As GithubRepository, relativePath As String) As String
        Return "https://api.github.com/repos/" & Uri.EscapeDataString(repo.Owner) & "/" & Uri.EscapeDataString(repo.Name) & "/contents/" & EscapeRepositoryPath(relativePath)
    End Function

    Private Shared Function EscapeRepositoryPath(relativePath As String) As String
        Dim normalizedPath = relativePath.Replace("\"c, "/"c).TrimStart("/"c)
        Return String.Join("/", normalizedPath.Split("/"c).Select(Function(segment) Uri.EscapeDataString(segment)))
    End Function

    Private Shared Function ParseGithubRepository(repoUrl As String) As GithubRepository
        If String.IsNullOrWhiteSpace(repoUrl) Then
            Throw New ArgumentException("GitHub repository URL is required.")
        End If

        Dim patterns = {
            "^https?://github\.com/(?<owner>[^/\s]+)/(?<repo>[^/\s#?]+)",
            "^git@github\.com:(?<owner>[^/\s]+)/(?<repo>[^/\s#?]+)"
        }

        For Each pattern In patterns
            Dim match = Regex.Match(repoUrl, pattern, RegexOptions.IgnoreCase)
            If match.Success Then
                Dim repoName = match.Groups("repo").Value
                If repoName.EndsWith(".git", StringComparison.OrdinalIgnoreCase) Then
                    repoName = repoName.Substring(0, repoName.Length - 4)
                End If

                Return New GithubRepository With {
                    .Owner = match.Groups("owner").Value,
                    .Name = repoName
                }
            End If
        Next

        Throw New ArgumentException("Repository URL must be a GitHub URL, such as https://github.com/owner/repo.")
    End Function

    Private Shared Function ShouldSkipPath(sourceDirectory As String, filePath As String) As Boolean
        Dim relativePath = GetRepositoryRelativePath(sourceDirectory, filePath)
        Return ShouldSkipRelativePath(relativePath)
    End Function

    Private Shared Function ShouldSkipRelativePath(relativePath As String) As Boolean
        Dim parts = relativePath.Split("/"c)

        For Each part As String In parts
            If String.Equals(part, ".git", StringComparison.OrdinalIgnoreCase) OrElse
                String.Equals(part, ".vs", StringComparison.OrdinalIgnoreCase) OrElse
                String.Equals(part, "bin", StringComparison.OrdinalIgnoreCase) OrElse
                String.Equals(part, "obj", StringComparison.OrdinalIgnoreCase) OrElse
                String.Equals(part, "node_modules", StringComparison.OrdinalIgnoreCase) Then
                Return True
            End If
        Next

        Return String.Equals(relativePath, "superusers.json", StringComparison.OrdinalIgnoreCase) OrElse
            String.Equals(relativePath, ".gitattributes", StringComparison.OrdinalIgnoreCase) OrElse
            String.Equals(Path.GetExtension(relativePath), ".bak", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Shared Function GetRepositoryRelativePath(rootPath As String, childPath As String) As String
        Dim normalizedRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) & Path.DirectorySeparatorChar
        Dim normalizedChild = Path.GetFullPath(childPath)

        If normalizedChild.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase) Then
            Return normalizedChild.Substring(normalizedRoot.Length).Replace("\"c, "/"c)
        End If

        Throw New InvalidOperationException("Could not calculate a relative path for uploaded files.")
    End Function

    Private Shared Function GetJsonString(json As String, propertyName As String) As String
        Dim match = Regex.Match(json, """" & Regex.Escape(propertyName) & """\s*:\s*""(?<value>(?:\\.|[^""])*)""", RegexOptions.Singleline)
        If Not match.Success Then
            Return Nothing
        End If

        Return UnescapeJsonString(match.Groups("value").Value)
    End Function

    Private Shared Function EscapeJsonString(value As String) As String
        Return If(value, String.Empty).Replace("\", "\\").Replace("""", "\""").Replace(Microsoft.VisualBasic.ControlChars.Cr, "\r").Replace(Microsoft.VisualBasic.ControlChars.Lf, "\n")
    End Function

    Private Shared Function UnescapeJsonString(value As String) As String
        Return If(value, String.Empty).Replace("\n", Microsoft.VisualBasic.ControlChars.Lf).Replace("\r", Microsoft.VisualBasic.ControlChars.Cr).Replace("\""", """").Replace("\\", "\")
    End Function

    Private Shared Function ByteArraysEqual(firstBytes As Byte(), secondBytes As Byte()) As Boolean
        If firstBytes Is Nothing OrElse secondBytes Is Nothing OrElse firstBytes.Length <> secondBytes.Length Then
            Return False
        End If

        For index As Integer = 0 To firstBytes.Length - 1
            If firstBytes(index) <> secondBytes(index) Then
                Return False
            End If
        Next

        Return True
    End Function

    Public Class UploadSummary
        Public Property Created As Integer
        Public Property Updated As Integer
        Public Property Skipped As Integer
        Public Property Deleted As Integer
    End Class

    Public Class UploadProgress
        Public Property Message As String
        Public Property Current As Integer
        Public Property Total As Integer
    End Class

    Private Class RemoteFileInfo
        Public Property Exists As Boolean
        Public Property Sha As String
        Public Property ContentBytes As Byte()
    End Class

    Private Class RemoteTreeEntry
        Public Property Path As String
        Public Property Type As String
        Public Property Sha As String
    End Class

    Private Class GithubRepository
        Public Property Owner As String
        Public Property Name As String
    End Class
End Class
