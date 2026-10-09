Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.IO.Compression
Imports System.Linq
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Threading.Tasks

Public Class GithubRepositoryDownloader
    Private Shared ReadOnly Http As New HttpClient()
    Private Const FileRetryAttempts As Integer = 60
    Private Const FileRetryDelayMilliseconds As Integer = 1000

    Public Shared Async Function CopyExistingProjectAsync(existingProjectPath As String, destinationParent As String, projectName As String, overwrite As Boolean, progress As Action(Of String)) As Task(Of String)
        Return Await Task.Run(Function()
                                  Return CopyExistingProject(existingProjectPath, destinationParent, projectName, overwrite, progress)
                              End Function)
    End Function

    Private Shared Function CopyExistingProject(existingProjectPath As String, destinationParent As String, projectName As String, overwrite As Boolean, progress As Action(Of String)) As String
        ValidateProjectName(projectName)

        If String.IsNullOrWhiteSpace(existingProjectPath) Then
            Throw New ArgumentException("Existing project is required.")
        End If

        If String.IsNullOrWhiteSpace(destinationParent) Then
            Throw New ArgumentException("Save location is required.")
        End If

        Dim fullDestinationParent = Path.GetFullPath(destinationParent)
        Dim sourcePath = ResolveExistingProjectPath(existingProjectPath, fullDestinationParent)
        If Not Directory.Exists(sourcePath) Then
            Throw New DirectoryNotFoundException("Existing project folder was not found: " & sourcePath)
        End If

        Dim folderPlan = CreateProjectFolderPlan(fullDestinationParent, projectName)
        Dim targetPath = folderPlan.TargetPath
        EnsureTargetIsInsideDestination(fullDestinationParent, targetPath)

        If String.Equals(Path.GetFullPath(sourcePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                         Path.GetFullPath(targetPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                         StringComparison.OrdinalIgnoreCase) Then
            Throw New AppUserMessageException("Existing project and new project target cannot be the same folder.")
        End If

        MoveExistingTargetToOldFolder(targetPath, progress)

        Dim tempRoot = Path.Combine(Path.GetTempPath(), "mercury-survey-programming-" & Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(tempRoot)

        Try
            Dim preparedPath = Path.Combine(tempRoot, "prepared")

            progress?.Invoke("Preparing project outside Dropbox...")
            CopyDirectory(sourcePath, preparedPath, progress)

            progress?.Invoke("Renaming text inside editable files...")
            RenameCopiedProjectReferences(preparedPath, Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), GetAlphanumericProjectName(projectName))
            RenameProjectFilesFolder(preparedPath, GetAlphanumericProjectName(projectName), progress)

            If folderPlan.ShouldArchiveExistingBaseFolder Then
                progress?.Invoke("Copying existing project folder contents to " & folderPlan.ArchivePath & "...")
                ArchiveExistingBaseFolder(folderPlan.BasePath, folderPlan.ArchivePath, folderPlan.TargetPath)
            End If

            progress?.Invoke("Placing finished project in " & targetPath & "...")
            CopyDirectory(preparedPath, targetPath, progress)

            progress?.Invoke("Completed: " & targetPath)
            Return targetPath
        Finally
            If Directory.Exists(tempRoot) Then
                Directory.Delete(tempRoot, recursive:=True)
            End If
        End Try
    End Function

    Public Shared Async Function DownloadAsync(repoUrl As String, destinationParent As String, projectName As String, branch As String, githubToken As String, overwrite As Boolean, shouldRenameTextInstances As Boolean, textToReplace As String, progress As Action(Of String)) As Task(Of String)
        ValidateProjectName(projectName)

        If String.IsNullOrWhiteSpace(destinationParent) Then
            Throw New ArgumentException("Save location is required.")
        End If

        Dim fullDestinationParent = Path.GetFullPath(destinationParent)
        Dim folderPlan = CreateProjectFolderPlan(fullDestinationParent, projectName)
        Dim targetPath = folderPlan.TargetPath
        EnsureTargetIsInsideDestination(fullDestinationParent, targetPath)

        Dim repo = ParseGithubRepository(repoUrl)
        If String.IsNullOrWhiteSpace(branch) Then
            branch = "main"
        End If

        MoveExistingTargetToOldFolder(targetPath, progress)

        RunWithRetry("Creating save location", Sub() Directory.CreateDirectory(fullDestinationParent), progress)

        Dim tempRoot = Path.Combine(Path.GetTempPath(), "mercury-survey-programming-" & Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(tempRoot)

        Try
            Dim zipPath = Path.Combine(tempRoot, repo.Name & ".zip")
            Dim extractPath = Path.Combine(tempRoot, "extract")
            Dim preparedPath = Path.Combine(tempRoot, "prepared")
            Directory.CreateDirectory(extractPath)

            Try
                progress?.Invoke("Downloading " & repo.Owner & "/" & repo.Name & " (" & branch & ")...")
                Await DownloadFileAsync(BuildArchiveUrl(repo.Owner, repo.Name, branch), zipPath, githubToken)

                progress?.Invoke("Extracting archive...")
                ZipFile.ExtractToDirectory(zipPath, extractPath)

                progress?.Invoke("Preparing repository outside Dropbox...")
                CopyDirectory(GetSingleExtractedRoot(extractPath), preparedPath, progress)
            Catch archiveException As Exception
                Dim details = archiveException.Message
                If String.IsNullOrWhiteSpace(details) Then
                    details = archiveException.GetType().Name
                End If

                Throw New AppUserMessageException(
                    "Could not download the GitHub repository using the GitHub API." & Environment.NewLine & Environment.NewLine &
                    "Make sure the GitHub token is saved for the selected template and has access to this private repository." & Environment.NewLine & Environment.NewLine &
                    "Details: " & details)
            End Try

            If shouldRenameTextInstances Then
                progress?.Invoke("Renaming project references...")
                RenameProjectReferences(preparedPath, If(String.IsNullOrWhiteSpace(textToReplace), repo.Name, textToReplace), GetAlphanumericProjectName(projectName))
            End If

            RenameProjectFilesFolder(preparedPath, GetAlphanumericProjectName(projectName), progress)

            progress?.Invoke("Removing repository-only files...")
            RemoveNewProjectExcludedFiles(preparedPath, progress)

            If folderPlan.ShouldArchiveExistingBaseFolder Then
                progress?.Invoke("Copying existing project folder contents to " & folderPlan.ArchivePath & "...")
                ArchiveExistingBaseFolder(folderPlan.BasePath, folderPlan.ArchivePath, folderPlan.TargetPath)
            End If

            progress?.Invoke("Placing finished project in " & targetPath & "...")
            CopyDirectory(preparedPath, targetPath, progress)

            progress?.Invoke("Completed: " & targetPath)
            Return targetPath
        Finally
            If Directory.Exists(tempRoot) Then
                Directory.Delete(tempRoot, recursive:=True)
            End If
        End Try
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

    Private Shared Async Function GetDefaultBranchAsync(owner As String, repoName As String) As Task(Of String)
        Dim apiUrl = "https://api.github.com/repos/" & Uri.EscapeDataString(owner) & "/" & Uri.EscapeDataString(repoName)
        Using request = New HttpRequestMessage(HttpMethod.Get, apiUrl)
            request.Headers.UserAgent.ParseAdd("MercurySurveyProgramming/1.0")

            Using response = Await Http.SendAsync(request)
                If Not response.IsSuccessStatusCode Then
                    Throw New AppUserMessageException("Could not read the default branch from GitHub. Enter a branch manually.")
                End If

                Dim responseText = Await response.Content.ReadAsStringAsync()
                Dim match = Regex.Match(responseText, """default_branch""\s*:\s*""(?<branch>[^""]+)""")
                If match.Success Then
                    Return match.Groups("branch").Value
                End If
            End Using
        End Using

        Throw New AppUserMessageException("GitHub response did not include a default branch. Enter a branch manually.")
    End Function

    Private Shared Function BuildArchiveUrl(owner As String, repoName As String, branch As String) As String
        Dim encodedBranch = String.Join("/", branch.Split("/"c).Select(Function(segment) Uri.EscapeDataString(segment)))
        Return "https://api.github.com/repos/" & Uri.EscapeDataString(owner) & "/" & Uri.EscapeDataString(repoName) & "/zipball/" & encodedBranch
    End Function

    Private Shared Async Function DownloadFileAsync(url As String, outputPath As String, githubToken As String) As Task
        Using request = New HttpRequestMessage(HttpMethod.Get, url)
            request.Headers.UserAgent.ParseAdd("MercurySurveyProgramming/1.0")

            If Not String.IsNullOrWhiteSpace(githubToken) Then
                request.Headers.Authorization = New AuthenticationHeaderValue("Bearer", githubToken.Trim())
            End If

            Using response = Await Http.SendAsync(request)
            If Not response.IsSuccessStatusCode Then
                Throw New AppUserMessageException("GitHub archive download failed with HTTP " & CInt(response.StatusCode) & ".")
            End If

            Using input = Await response.Content.ReadAsStreamAsync()
                Using output = File.Create(outputPath)
                    Await input.CopyToAsync(output)
                End Using
            End Using
            End Using
        End Using
    End Function

    Private Shared Sub RemoveNewProjectExcludedFiles(repositoryPath As String, progress As Action(Of String))
        Dim excludedNames = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
            ".gitattributes",
            "superusers.json"
        }

        For Each filePath As String In Directory.GetFiles(repositoryPath, "*", SearchOption.AllDirectories)
            If excludedNames.Contains(Path.GetFileName(filePath)) Then
                RunWithRetry("Removing " & Path.GetFileName(filePath), Sub() File.Delete(filePath), progress)
            End If
        Next
    End Sub

    Private Shared Function GetSingleExtractedRoot(extractPath As String) As String
        Dim entries = Directory.GetDirectories(extractPath)
        If entries.Length <> 1 Then
            Throw New AppUserMessageException("Expected the GitHub archive to contain one root folder.")
        End If

        Return entries(0)
    End Function

    Private Shared Function ResolveExistingProjectPath(existingProjectPath As String, destinationParent As String) As String
        Dim cleanedPath = existingProjectPath.Trim()
        If Path.IsPathRooted(cleanedPath) Then
            Return Path.GetFullPath(cleanedPath)
        End If

        Return Path.GetFullPath(Path.Combine(destinationParent, cleanedPath))
    End Function

    Private Shared Function CreateProjectFolderPlan(destinationParent As String, projectName As String) As ProjectFolderPlan
        Dim match = Regex.Match(projectName, "(?<number>\d{4,5})\s*(?<letter>[A-Za-z]?)")
        If Not match.Success Then
            Throw New ArgumentException("Project name must contain a 4 or 5 digit project number.")
        End If

        Dim projectNumber = match.Groups("number").Value
        Dim nextAlphabet = match.Groups("letter").Value
        Dim basePath = Path.GetFullPath(Path.Combine(destinationParent, projectNumber))

        If Not Directory.Exists(basePath) Then
            Return New ProjectFolderPlan With {
                .BasePath = basePath,
                .TargetPath = basePath,
                .ArchivePath = Nothing,
                .ShouldArchiveExistingBaseFolder = False
            }
        End If

        If String.IsNullOrWhiteSpace(nextAlphabet) Then
            Throw New ArgumentException("Project number folder already exists. Include the next alphabet after the number, for example " & projectNumber & "A.")
        End If

        Dim requestedProjectFolder = projectNumber & nextAlphabet.ToUpperInvariant()
        Dim archivePath = Path.GetFullPath(Path.Combine(basePath, projectNumber))
        Dim targetPath = Path.GetFullPath(Path.Combine(basePath, requestedProjectFolder))

        Return New ProjectFolderPlan With {
            .BasePath = basePath,
            .ArchivePath = archivePath,
            .TargetPath = targetPath,
            .ShouldArchiveExistingBaseFolder = True
        }
    End Function

    Private Shared Function GetNextAvailableAlphabetFolder(basePath As String, projectNumber As String, requestedAlphabet As String) As String
        Dim startCode = Convert.ToInt32(Char.ToUpperInvariant(requestedAlphabet(0))) + 1
        For letterCode As Integer = startCode To 90
            Dim candidateName = projectNumber & Convert.ToChar(letterCode)
            Dim candidatePath = Path.Combine(basePath, candidateName)
            If Not Directory.Exists(candidatePath) AndAlso Not File.Exists(candidatePath) Then
                Return candidateName
            End If
        Next

        Return Nothing
    End Function

    Private Shared Sub MoveExistingTargetToOldFolder(targetPath As String, progress As Action(Of String))
        If Not Directory.Exists(targetPath) AndAlso Not File.Exists(targetPath) Then
            Return
        End If

        Dim oldPath = GetAvailableOldPath(targetPath)
        progress?.Invoke("Existing project found. Moving it to " & oldPath & "...")

        If Directory.Exists(targetPath) Then
            RunWithRetry("Moving existing project folder to " & oldPath, Sub() Directory.Move(targetPath, oldPath), progress)
        Else
            RunWithRetry("Moving existing project file to " & oldPath, Sub() File.Move(targetPath, oldPath), progress)
        End If
    End Sub

    Private Shared Function GetAvailableOldPath(targetPath As String) As String
        For index As Integer = 0 To 999
            Dim candidate = targetPath & ".old" & DateTime.Now.ToString("ddMMyyHHmmss")
            If index > 0 Then
                candidate &= "." & index.ToString()
            End If

            If Not Directory.Exists(candidate) AndAlso Not File.Exists(candidate) Then
                Return candidate
            End If

            Thread.Sleep(1000)
        Next

        Return targetPath & ".old" & DateTime.Now.ToString("ddMMyyHHmmss") & "." & Guid.NewGuid().ToString("N")
    End Function

    Private Shared Sub ArchiveExistingBaseFolder(basePath As String, archivePath As String, newTargetPath As String)
        If Directory.Exists(archivePath) Then
            Return
        End If

        Directory.CreateDirectory(archivePath)
        CopyDirectoryContents(basePath, archivePath, New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
            Path.GetFullPath(archivePath),
            Path.GetFullPath(newTargetPath)
        })
    End Sub

    Private Shared Sub CopyDirectory(sourceDirectory As String, targetDirectory As String, Optional progress As Action(Of String) = Nothing)
        RunWithRetry("Creating folder " & targetDirectory, Sub() Directory.CreateDirectory(targetDirectory), progress)

        For Each directoryPath In Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories)
            Dim relativePath = GetRelativePath(sourceDirectory, directoryPath)
            Dim destinationDirectory = Path.Combine(targetDirectory, relativePath)
            RunWithRetry("Creating folder " & destinationDirectory, Sub() Directory.CreateDirectory(destinationDirectory), progress)
        Next

        For Each filePath In Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories)
            Dim relativePath = GetRelativePath(sourceDirectory, filePath)
            Dim targetFile = Path.Combine(targetDirectory, relativePath)
            RunWithRetry("Copying " & Path.GetFileName(filePath), Sub() File.Copy(filePath, targetFile, overwrite:=False), progress)
        Next
    End Sub

    Private Shared Sub CopyDirectoryContents(sourceDirectory As String, targetDirectory As String, excludedFullPaths As HashSet(Of String))
        RunWithRetry("Creating folder " & targetDirectory, Sub() Directory.CreateDirectory(targetDirectory), Nothing)

        For Each directoryPath As String In Directory.GetDirectories(sourceDirectory)
            Dim fullDirectoryPath = Path.GetFullPath(directoryPath)
            If excludedFullPaths.Contains(fullDirectoryPath) Then
                Continue For
            End If

            CopyDirectory(directoryPath, Path.Combine(targetDirectory, Path.GetFileName(directoryPath)))
        Next

        For Each filePath As String In Directory.GetFiles(sourceDirectory)
            Dim targetFile = Path.Combine(targetDirectory, Path.GetFileName(filePath))
            RunWithRetry("Copying " & Path.GetFileName(filePath), Sub() File.Copy(filePath, targetFile, overwrite:=False), Nothing)
        Next
    End Sub

    Private Shared Function GetRelativePath(rootPath As String, childPath As String) As String
        Dim normalizedRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) & Path.DirectorySeparatorChar
        Dim normalizedChild = Path.GetFullPath(childPath)

        If normalizedChild.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase) Then
            Return normalizedChild.Substring(normalizedRoot.Length)
        End If

        Throw New InvalidOperationException("Could not calculate a relative path for copied files.")
    End Function

    Private Shared Function IsSamePath(firstPath As String, secondPath As String) As Boolean
        Dim normalizedFirst = Path.GetFullPath(firstPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        Dim normalizedSecond = Path.GetFullPath(secondPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        Return String.Equals(normalizedFirst, normalizedSecond, StringComparison.OrdinalIgnoreCase)
    End Function

    Private Shared Function IsPathInside(parentPath As String, childPath As String) As Boolean
        Dim normalizedParent = Path.GetFullPath(parentPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) & Path.DirectorySeparatorChar
        Dim normalizedChild = Path.GetFullPath(childPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) & Path.DirectorySeparatorChar
        Return normalizedChild.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase)
    End Function

    Private Shared Sub RenameTextInstances(targetDirectory As String, sourceText As String, replacementText As String)
        If String.IsNullOrWhiteSpace(sourceText) OrElse String.IsNullOrWhiteSpace(replacementText) OrElse
            String.Equals(sourceText, replacementText, StringComparison.Ordinal) Then
            Return
        End If

        For Each filePath As String In Directory.GetFiles(targetDirectory, "*", SearchOption.AllDirectories)
            If Not IsEditableFile(filePath) OrElse IsInGeneratedOrGitFolder(targetDirectory, filePath) Then
                Continue For
            End If

            Dim fileInfo = New FileInfo(filePath)
            If fileInfo.Length > 5 * 1024 * 1024 Then
                Continue For
            End If

            Dim originalText = RunWithRetry("Reading " & Path.GetFileName(filePath), Function() File.ReadAllText(filePath), Nothing)
            Dim updatedText = Regex.Replace(originalText, Regex.Escape(sourceText), replacementText, RegexOptions.IgnoreCase)

            If Not String.Equals(originalText, updatedText, StringComparison.Ordinal) Then
                RunWithRetry("Updating text in " & Path.GetFileName(filePath), Sub() File.WriteAllText(filePath, updatedText), Nothing)
            End If
        Next
    End Sub

    Private Shared Sub RenameProjectReferences(targetDirectory As String, sourceText As String, replacementText As String)
        RenameTextInstances(targetDirectory, sourceText, replacementText)
        RenameFileSystemEntries(targetDirectory, sourceText, replacementText)
    End Sub

    Private Shared Sub RenameCopiedProjectReferences(targetDirectory As String, oldProjectName As String, newProjectName As String)
        Dim oldCleanName = GetAlphanumericProjectName(oldProjectName)
        Dim newCleanName = GetAlphanumericProjectName(newProjectName)
        Dim oldProjectNumber = ExtractProjectNumber(oldCleanName)
        Dim newProjectNumber = ExtractProjectNumber(newCleanName)
        Dim oldNameVariants = GetProjectNameVariants(oldCleanName)

        For Each oldNameVariant In oldNameVariants
            RenameTextInstances(targetDirectory, oldNameVariant, newCleanName)
        Next

        If Not String.IsNullOrWhiteSpace(oldProjectNumber) AndAlso Not String.IsNullOrWhiteSpace(newProjectNumber) AndAlso
            Not String.Equals(oldProjectNumber, newProjectNumber, StringComparison.OrdinalIgnoreCase) Then
            RenameTextInstances(targetDirectory, oldProjectNumber, newProjectNumber)
        End If

        RenameCopiedProjectFileSystemEntries(targetDirectory, oldNameVariants, newCleanName, oldProjectNumber, newProjectNumber)
    End Sub

    Private Shared Function GetProjectNameVariants(projectName As String) As List(Of String)
        Dim variants = New List(Of String)()
        Dim cleanedName = GetAlphanumericProjectName(projectName)
        Dim maName = If(cleanedName.StartsWith("MA", StringComparison.OrdinalIgnoreCase), cleanedName, "MA" & cleanedName)

        variants.Add(maName)
        If Not String.Equals(cleanedName, maName, StringComparison.OrdinalIgnoreCase) Then
            variants.Add(cleanedName)
        End If

        Return variants.
            Where(Function(value) Not String.IsNullOrWhiteSpace(value)).
            Distinct(StringComparer.OrdinalIgnoreCase).
            OrderByDescending(Function(value) value.Length).
            ToList()
    End Function

    Private Shared Function ExtractProjectNumber(projectName As String) As String
        Dim match = Regex.Match(If(projectName, String.Empty), "\d{4,5}")
        If match.Success Then
            Return match.Value
        End If

        Return String.Empty
    End Function

    Private Shared Sub RenameCopiedProjectFileSystemEntries(targetDirectory As String, oldNameVariants As List(Of String), newProjectName As String, oldProjectNumber As String, newProjectNumber As String)
        Dim entries = Directory.GetFileSystemEntries(targetDirectory, "*", SearchOption.AllDirectories).
            OrderByDescending(Function(path) path.Length).
            ToList()

        For Each currentPath As String In entries
            If IsInGeneratedOrGitFolder(targetDirectory, currentPath) Then
                Continue For
            End If

            Dim currentName = Path.GetFileName(currentPath)
            If String.IsNullOrWhiteSpace(currentName) Then
                Continue For
            End If

            Dim isDirectory = Directory.Exists(currentPath)
            Dim newName = GetCopiedProjectEntryName(currentName, isDirectory, oldNameVariants, newProjectName, oldProjectNumber, newProjectNumber)
            If String.Equals(currentName, newName, StringComparison.Ordinal) Then
                Continue For
            End If

            Dim parentPath = Path.GetDirectoryName(currentPath)
            Dim newPath = Path.Combine(parentPath, newName)

            If File.Exists(newPath) OrElse Directory.Exists(newPath) Then
                Throw New IOException("Cannot rename " & currentPath & " because " & newPath & " already exists.")
            End If

            If isDirectory Then
                RunWithRetry("Renaming folder " & currentName & " to " & newName, Sub() Directory.Move(currentPath, newPath), Nothing)
            ElseIf File.Exists(currentPath) Then
                RunWithRetry("Renaming file " & currentName & " to " & newName, Sub() File.Move(currentPath, newPath), Nothing)
            End If
        Next
    End Sub

    Private Shared Function GetCopiedProjectEntryName(currentName As String, isDirectory As Boolean, oldNameVariants As List(Of String), newProjectName As String, oldProjectNumber As String, newProjectNumber As String) As String
        For Each oldNameVariant In oldNameVariants
            If String.Equals(currentName, oldNameVariant, StringComparison.OrdinalIgnoreCase) Then
                Return newProjectName
            End If
        Next

        If Not isDirectory Then
            Dim extension = Path.GetExtension(currentName)
            Dim stem = Path.GetFileNameWithoutExtension(currentName)

            For Each oldNameVariant In oldNameVariants
                If String.Equals(stem, oldNameVariant, StringComparison.OrdinalIgnoreCase) Then
                    Return newProjectName & extension
                End If
            Next
        End If

        Dim newName = currentName
        For Each oldNameVariant In oldNameVariants
            newName = Regex.Replace(newName, Regex.Escape(oldNameVariant), newProjectName, RegexOptions.IgnoreCase)
        Next

        If String.Equals(newName, currentName, StringComparison.Ordinal) AndAlso
            Not String.IsNullOrWhiteSpace(oldProjectNumber) AndAlso Not String.IsNullOrWhiteSpace(newProjectNumber) AndAlso
            Not String.Equals(oldProjectNumber, newProjectNumber, StringComparison.OrdinalIgnoreCase) Then
            newName = Regex.Replace(newName, Regex.Escape(oldProjectNumber), newProjectNumber, RegexOptions.IgnoreCase)
        End If

        Return newName
    End Function

    Private Shared Sub RenameFileSystemEntries(targetDirectory As String, sourceText As String, replacementText As String)
        If String.IsNullOrWhiteSpace(sourceText) OrElse String.IsNullOrWhiteSpace(replacementText) OrElse
            String.Equals(sourceText, replacementText, StringComparison.OrdinalIgnoreCase) Then
            Return
        End If

        Dim escapedSourceText = Regex.Escape(sourceText)
        Dim entries = Directory.GetFileSystemEntries(targetDirectory, "*", SearchOption.AllDirectories).
            OrderByDescending(Function(path) path.Length).
            ToList()

        For Each currentPath As String In entries
            If IsInGeneratedOrGitFolder(targetDirectory, currentPath) Then
                Continue For
            End If

            Dim currentName = Path.GetFileName(currentPath)
            If String.IsNullOrWhiteSpace(currentName) OrElse Not Regex.IsMatch(currentName, escapedSourceText, RegexOptions.IgnoreCase) Then
                Continue For
            End If

            Dim newName = Regex.Replace(currentName, escapedSourceText, replacementText, RegexOptions.IgnoreCase)
            Dim parentPath = Path.GetDirectoryName(currentPath)
            Dim newPath = Path.Combine(parentPath, newName)

            If File.Exists(newPath) OrElse Directory.Exists(newPath) Then
                Throw New IOException("Cannot rename " & currentPath & " because " & newPath & " already exists.")
            End If

            If Directory.Exists(currentPath) Then
                RunWithRetry("Renaming folder " & currentName & " to " & newName, Sub() Directory.Move(currentPath, newPath), Nothing)
            ElseIf File.Exists(currentPath) Then
                RunWithRetry("Renaming file " & currentName & " to " & newName, Sub() File.Move(currentPath, newPath), Nothing)
            End If
        Next
    End Sub

    Private Shared Sub RenameProjectFilesFolder(targetDirectory As String, projectName As String, progress As Action(Of String))
        If String.IsNullOrWhiteSpace(projectName) Then
            Return
        End If

        Dim projectFilesFolders = Directory.GetDirectories(targetDirectory, "*", SearchOption.AllDirectories).
            Where(Function(directoryPath) Not IsInGeneratedOrGitFolder(targetDirectory, directoryPath)).
            Where(Function(directoryPath) String.Equals(Path.GetFileName(directoryPath), "PROJECTFILES", StringComparison.OrdinalIgnoreCase)).
            OrderByDescending(Function(directoryPath) directoryPath.Length).
            ToList()

        For Each currentPath As String In projectFilesFolders
            Dim parentPath = Path.GetDirectoryName(currentPath)
            Dim newPath = Path.Combine(parentPath, projectName)

            If IsSamePath(currentPath, newPath) Then
                Continue For
            End If

            If File.Exists(newPath) OrElse Directory.Exists(newPath) Then
                Throw New IOException("Cannot rename PROJECTFILES because " & newPath & " already exists.")
            End If

            RunWithRetry("Renaming PROJECTFILES to " & projectName, Sub() Directory.Move(currentPath, newPath), progress)
        Next
    End Sub

    Private Shared Sub RunWithRetry(description As String, action As Action, progress As Action(Of String))
        For attempt As Integer = 1 To FileRetryAttempts
            Try
                action()
                Return
            Catch ex As Exception When IsTemporaryFileAccessException(ex)
                If attempt >= FileRetryAttempts Then
                    Throw New IOException(description & " failed because the file is still locked by Dropbox or another process: " & ex.Message, ex)
                End If

                If attempt = 1 OrElse attempt Mod 5 = 0 Then
                    progress?.Invoke(description & " is waiting for Dropbox or another file lock to release...")
                End If

                Thread.Sleep(FileRetryDelayMilliseconds)
            End Try
        Next
    End Sub

    Private Shared Function RunWithRetry(Of T)(description As String, action As Func(Of T), progress As Action(Of String)) As T
        For attempt As Integer = 1 To FileRetryAttempts
            Try
                Return action()
            Catch ex As Exception When IsTemporaryFileAccessException(ex)
                If attempt >= FileRetryAttempts Then
                    Throw New IOException(description & " failed because the file is still locked by Dropbox or another process: " & ex.Message, ex)
                End If

                If attempt = 1 OrElse attempt Mod 5 = 0 Then
                    progress?.Invoke(description & " is waiting for Dropbox or another file lock to release...")
                End If

                Thread.Sleep(FileRetryDelayMilliseconds)
            End Try
        Next

        Throw New IOException(description & " failed.")
    End Function

    Private Shared Function IsTemporaryFileAccessException(ex As Exception) As Boolean
        Return TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
    End Function

    Private Shared Function IsEditableFile(filePath As String) As Boolean
        Dim editableExtensions = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
            ".asp",
            ".aspx",
            ".bat",
            ".cmd",
            ".config",
            ".css",
            ".csv",
            ".dms",
            ".htm",
            ".html",
            ".ini",
            ".js",
            ".json",
            ".md",
            ".mdd",
            ".mqd",
            ".mrs",
            ".props",
            ".ps1",
            ".sln",
            ".sql",
            ".targets",
            ".ts",
            ".txt",
            ".vb",
            ".vbproj",
            ".xml",
            ".yaml",
            ".yml"
        }

        Return editableExtensions.Contains(Path.GetExtension(filePath))
    End Function

    Private Shared Function IsInGeneratedOrGitFolder(targetDirectory As String, filePath As String) As Boolean
        Dim relativePath = GetRelativePath(targetDirectory, filePath)
        Dim parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)

        For Each part As String In parts
            If String.Equals(part, ".git", StringComparison.OrdinalIgnoreCase) OrElse
                String.Equals(part, "bin", StringComparison.OrdinalIgnoreCase) OrElse
                String.Equals(part, "obj", StringComparison.OrdinalIgnoreCase) Then
                Return True
            End If
        Next

        Return False
    End Function

    Private Shared Sub ValidateFolderName(folderName As String)
        If String.IsNullOrWhiteSpace(folderName) Then
            Throw New ArgumentException("New folder name is required.")
        End If

        If folderName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 OrElse
            folderName.Contains(Path.DirectorySeparatorChar) OrElse
            folderName.Contains(Path.AltDirectorySeparatorChar) Then
            Throw New ArgumentException("New folder name cannot contain invalid path characters or directory separators.")
        End If
    End Sub

    Private Shared Sub ValidateProjectName(projectName As String)
        If String.IsNullOrWhiteSpace(projectName) Then
            Throw New ArgumentException("Project name is required.")
        End If

        If projectName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 OrElse
            projectName.Contains(Path.DirectorySeparatorChar) OrElse
            projectName.Contains(Path.AltDirectorySeparatorChar) Then
            Throw New ArgumentException("Project name cannot contain invalid path characters or directory separators.")
        End If
    End Sub

    Private Shared Function GetAlphanumericProjectName(projectName As String) As String
        Dim cleanedName = Regex.Replace(If(projectName, String.Empty), "[^A-Za-z0-9]", String.Empty).ToUpperInvariant()
        If String.IsNullOrWhiteSpace(cleanedName) Then
            Throw New ArgumentException("Project name must contain at least one letter or number.")
        End If

        Return cleanedName
    End Function

    Private Shared Sub EnsureTargetIsInsideDestination(destinationParent As String, targetPath As String)
        Dim parentWithSeparator = destinationParent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) & Path.DirectorySeparatorChar
        If Not targetPath.StartsWith(parentWithSeparator, StringComparison.OrdinalIgnoreCase) Then
            Throw New InvalidOperationException("Target path must stay inside the save location.")
        End If
    End Sub

    Private Class GithubRepository
        Public Property Owner As String
        Public Property Name As String
    End Class

    Private Class ProjectFolderPlan
        Public Property BasePath As String
        Public Property ArchivePath As String
        Public Property TargetPath As String
        Public Property ShouldArchiveExistingBaseFolder As Boolean
    End Class

End Class
