Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Security.Cryptography
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading.Tasks

Public NotInheritable Class GithubAppAuthenticator
    Private Const ConfigFileName As String = "github-app.json"
    Private Shared ReadOnly Http As New HttpClient()
    Private Shared _cachedToken As String
    Private Shared _cachedTokenExpiresUtc As DateTime

    Private Sub New()
    End Sub

    Public Shared Async Function GetInstallationTokenAsync() As Task(Of String)
        If Not String.IsNullOrWhiteSpace(_cachedToken) AndAlso DateTime.UtcNow.AddMinutes(5) < _cachedTokenExpiresUtc Then
            Return _cachedToken
        End If

        Dim config = LoadConfig()
        If config Is Nothing Then
            Return String.Empty
        End If

        Dim jwt = CreateJwt(config.AppId, config.PrivateKeyPath)
        Using request = New HttpRequestMessage(HttpMethod.Post, "https://api.github.com/app/installations/" & Uri.EscapeDataString(config.InstallationId) & "/access_tokens")
            request.Headers.Authorization = New AuthenticationHeaderValue("Bearer", jwt)
            request.Headers.Accept.Add(New MediaTypeWithQualityHeaderValue("application/vnd.github+json"))
            request.Headers.UserAgent.ParseAdd("MercurySurveyProgrammingGitHubApp")
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28")

            Using response = Await Http.SendAsync(request).ConfigureAwait(False)
                Dim responseText = Await response.Content.ReadAsStringAsync().ConfigureAwait(False)
                If Not response.IsSuccessStatusCode Then
                    AppLogger.Info("GitHub App token request failed. HTTP " & CInt(response.StatusCode).ToString() & ": " & responseText)
                    Return String.Empty
                End If

                _cachedToken = GetJsonString(responseText, "token")
                Dim expiresText = GetJsonString(responseText, "expires_at")
                Dim parsedExpires As DateTime
                If DateTime.TryParse(expiresText, parsedExpires) Then
                    _cachedTokenExpiresUtc = parsedExpires.ToUniversalTime()
                Else
                    _cachedTokenExpiresUtc = DateTime.UtcNow.AddMinutes(55)
                End If

                Return If(_cachedToken, String.Empty)
            End Using
        End Using
    End Function

    Public Shared Function GetInstallationToken() As String
        Return GetInstallationTokenAsync().GetAwaiter().GetResult()
    End Function

    Public Shared Function HasConfig() As Boolean
        Return LoadConfig() IsNot Nothing
    End Function

    Private Shared Function LoadConfig() As GithubAppConfig
        For Each path As String In GetConfigPaths()
            If Not File.Exists(path) Then
                Continue For
            End If

            Try
                Dim json = File.ReadAllText(path)
                Dim config = New GithubAppConfig With {
                    .AppId = GetJsonString(json, "appId"),
                    .InstallationId = GetJsonString(json, "installationId"),
                    .PrivateKeyPath = Environment.ExpandEnvironmentVariables(GetJsonString(json, "privateKeyPath"))
                }

                If String.IsNullOrWhiteSpace(config.AppId) OrElse
                    String.IsNullOrWhiteSpace(config.InstallationId) OrElse
                    String.IsNullOrWhiteSpace(config.PrivateKeyPath) OrElse
                    Not File.Exists(config.PrivateKeyPath) Then
                    Continue For
                End If

                Return config
            Catch ex As Exception
                AppLogger.Error("Could not load GitHub App configuration.", ex)
            End Try
        Next

        Return Nothing
    End Function

    Private Shared Function GetConfigPaths() As IEnumerable(Of String)
        Dim localAppData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Mercury Survey Programming",
            ConfigFileName)

        Return New String() {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName),
            localAppData
        }
    End Function

    Private Shared Function CreateJwt(appId As String, privateKeyPath As String) As String
        Dim now = DateTimeOffset.UtcNow
        Dim header = "{""alg"":""RS256"",""typ"":""JWT""}"
        Dim payload = "{""iat"":" & now.AddSeconds(-60).ToUnixTimeSeconds().ToString() &
            ",""exp"":" & now.AddMinutes(9).ToUnixTimeSeconds().ToString() &
            ",""iss"":""" & EscapeJsonString(appId) & """}"

        Dim unsignedToken = Base64UrlEncode(Encoding.UTF8.GetBytes(header)) & "." & Base64UrlEncode(Encoding.UTF8.GetBytes(payload))
        Using rsa = ImportRsaPrivateKey(privateKeyPath)
            Dim signature = rsa.SignData(Encoding.ASCII.GetBytes(unsignedToken), CryptoConfig.MapNameToOID("SHA256"))
            Return unsignedToken & "." & Base64UrlEncode(signature)
        End Using
    End Function

    Private Shared Function ImportRsaPrivateKey(path As String) As RSACryptoServiceProvider
        Dim pem = File.ReadAllText(path)
        Dim isPkcs8 = pem.Contains("-----BEGIN PRIVATE KEY-----")
        Dim isPkcs1 = pem.Contains("-----BEGIN RSA PRIVATE KEY-----")
        If Not isPkcs8 AndAlso Not isPkcs1 Then
            Throw New InvalidOperationException("Unsupported GitHub App private key format.")
        End If

        Dim base64 = Regex.Replace(pem, "-----BEGIN [A-Z ]+-----|-----END [A-Z ]+-----|\s", String.Empty)
        Dim der = Convert.FromBase64String(base64)

        If isPkcs8 Then
            Dim outerOffset = 0
            Dim outer = ReadAsnValue(der, outerOffset, &H30)
            Dim offset = 0
            ReadAsnInteger(outer, offset)
            ReadAsnValue(outer, offset, &H30)
            der = ReadAsnValue(outer, offset, &H04)
        End If

        Dim sequenceOffset = 0
        Dim sequence = ReadAsnValue(der, sequenceOffset, &H30)
        Dim seqOffset = 0
        ReadAsnInteger(sequence, seqOffset)

        Dim parameters = New RSAParameters With {
            .Modulus = ReadAsnInteger(sequence, seqOffset),
            .Exponent = ReadAsnInteger(sequence, seqOffset),
            .D = ReadAsnInteger(sequence, seqOffset),
            .P = ReadAsnInteger(sequence, seqOffset),
            .Q = ReadAsnInteger(sequence, seqOffset),
            .DP = ReadAsnInteger(sequence, seqOffset),
            .DQ = ReadAsnInteger(sequence, seqOffset),
            .InverseQ = ReadAsnInteger(sequence, seqOffset)
        }

        Dim rsa = New RSACryptoServiceProvider()
        rsa.PersistKeyInCsp = False
        rsa.ImportParameters(parameters)
        Return rsa
    End Function

    Private Shared Function ReadAsnValue(data As Byte(), ByRef offset As Integer, expectedTag As Integer) As Byte()
        Dim tag = data(offset)
        offset += 1
        If tag <> expectedTag Then
            Throw New InvalidOperationException("Unexpected ASN.1 tag.")
        End If

        Dim length = ReadAsnLength(data, offset)
        Dim value(length - 1) As Byte
        Buffer.BlockCopy(data, offset, value, 0, length)
        offset += length
        Return value
    End Function

    Private Shared Function ReadAsnLength(data As Byte(), ByRef offset As Integer) As Integer
        Dim length = CInt(data(offset))
        offset += 1
        If (length And &H80) = 0 Then
            Return length
        End If

        Dim byteCount = length And &H7F
        length = 0
        For i = 0 To byteCount - 1
            length = (length << 8) Or CInt(data(offset))
            offset += 1
        Next

        Return length
    End Function

    Private Shared Function ReadAsnInteger(data As Byte(), ByRef offset As Integer) As Byte()
        Dim value = ReadAsnValue(data, offset, &H02)
        While value.Length > 1 AndAlso value(0) = 0
            Dim trimmed(value.Length - 2) As Byte
            Buffer.BlockCopy(value, 1, trimmed, 0, trimmed.Length)
            value = trimmed
        End While

        Return value
    End Function

    Private Shared Function Base64UrlEncode(bytes As Byte()) As String
        Return Convert.ToBase64String(bytes).TrimEnd("="c).Replace("+"c, "-"c).Replace("/"c, "_"c)
    End Function

    Private Shared Function GetJsonString(json As String, propertyName As String) As String
        Dim pattern = """" & Regex.Escape(propertyName) & """\s*:\s*""(?<value>(?:\\""|[^""])*)"""
        Dim match = Regex.Match(If(json, String.Empty), pattern)
        If Not match.Success Then
            Return String.Empty
        End If

        Return Regex.Unescape(match.Groups("value").Value)
    End Function

    Private Shared Function EscapeJsonString(value As String) As String
        Return If(value, String.Empty).Replace("\", "\\").Replace(Microsoft.VisualBasic.ControlChars.Quote, "\" & Microsoft.VisualBasic.ControlChars.Quote)
    End Function

    Private Class GithubAppConfig
        Public Property AppId As String
        Public Property InstallationId As String
        Public Property PrivateKeyPath As String
    End Class
End Class
