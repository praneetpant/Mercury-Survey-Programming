Imports System
Imports System.Collections.Generic
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Text
Imports System.Threading.Tasks

Public Class DimensionsAuthenticator
    Private Shared ReadOnly Http As New HttpClient()

    Public Shared Async Function ValidateAsync(settings As AppSettings, username As String, password As String) As Task
        If settings Is Nothing Then
            Throw New ArgumentNullException(NameOf(settings))
        End If

        settings.ApplyDefaults()

        If String.IsNullOrWhiteSpace(settings.DimensionsUrl) Then
            Throw New ArgumentException("Dimensions URL is required.")
        End If

        If String.IsNullOrWhiteSpace(username) Then
            Throw New ArgumentException("Username is required.")
        End If

        If String.IsNullOrWhiteSpace(password) Then
            Throw New ArgumentException("Password is required.")
        End If

        Dim loginUri As Uri = Nothing
        If Not Uri.TryCreate(settings.DimensionsUrl, UriKind.Absolute, loginUri) OrElse
            (loginUri.Scheme <> Uri.UriSchemeHttp AndAlso loginUri.Scheme <> Uri.UriSchemeHttps) Then
            Throw New ArgumentException("Dimensions URL must be an absolute HTTP or HTTPS URL.")
        End If

        Dim responseText As String
        Dim authMode = settings.AuthMode.Trim().ToLowerInvariant()

        If authMode = "basic" Then
            responseText = Await ValidateBasicLoginAsync(loginUri, username, password)
        ElseIf authMode = "form" Then
            responseText = Await ValidateFormLoginAsync(loginUri, settings, username, password)
        Else
            Throw New ArgumentException("Authentication mode must be either form or basic.")
        End If

        If Not String.IsNullOrEmpty(settings.FailureContains) AndAlso
            responseText.IndexOf(settings.FailureContains, StringComparison.OrdinalIgnoreCase) >= 0 Then
            Throw New UnauthorizedAccessException("Wrong username or password.")
        End If

        If Not String.IsNullOrEmpty(settings.SuccessContains) AndAlso
            responseText.IndexOf(settings.SuccessContains, StringComparison.OrdinalIgnoreCase) < 0 Then
            Throw New UnauthorizedAccessException("Wrong username or password.")
        End If
    End Function

    Private Shared Async Function ValidateFormLoginAsync(loginUri As Uri, settings As AppSettings, username As String, password As String) As Task(Of String)
        Dim formFields = New Dictionary(Of String, String) From {
            {settings.UserField, username},
            {settings.PasswordField, password}
        }

        Using content = New FormUrlEncodedContent(formFields)
            Using response = Await Http.PostAsync(loginUri, content)
                Dim responseText = Await response.Content.ReadAsStringAsync()
                If Not response.IsSuccessStatusCode Then
                    If CInt(response.StatusCode) = 401 OrElse CInt(response.StatusCode) = 403 Then
                        Throw New UnauthorizedAccessException("Wrong username or password.")
                    End If

                    If CInt(response.StatusCode) = 405 Then
                        AppLogger.Info("Dimensions form login returned HTTP 405. Retrying with basic authentication.")
                        Return Await ValidateBasicLoginAsync(loginUri, username, password)
                    End If

                    Throw New AppUserMessageException("Dimensions server returned HTTP " & CInt(response.StatusCode) & " while checking login.")
                End If

                Return responseText
            End Using
        End Using
    End Function

    Private Shared Async Function ValidateBasicLoginAsync(loginUri As Uri, username As String, password As String) As Task(Of String)
        Using request = New HttpRequestMessage(HttpMethod.Get, loginUri)
            Dim token = Convert.ToBase64String(Encoding.UTF8.GetBytes(username & ":" & password))
            request.Headers.Authorization = New AuthenticationHeaderValue("Basic", token)

            Using response = Await Http.SendAsync(request)
                Dim responseText = Await response.Content.ReadAsStringAsync()
                If Not response.IsSuccessStatusCode Then
                    If CInt(response.StatusCode) = 401 OrElse CInt(response.StatusCode) = 403 Then
                        Throw New UnauthorizedAccessException("Wrong username or password.")
                    End If

                    Throw New AppUserMessageException("Dimensions server returned HTTP " & CInt(response.StatusCode) & " while checking login.")
                End If

                Return responseText
            End Using
        End Using
    End Function
End Class
