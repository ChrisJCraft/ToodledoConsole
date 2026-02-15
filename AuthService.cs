using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ToodledoConsole
{
    public class AuthService
    {
        public const string AuthFile = "auth.txt";
        private const string TokenFile = "token.txt";
        private const string RedirectUri = "http://localhost:5000/";

        private string _clientId = string.Empty;
        private string _clientSecret = string.Empty;
        private TokenStorage _tokens = new TokenStorage();
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public AuthService(HttpClient httpClient, JsonSerializerOptions jsonOptions)
        {
            _httpClient = httpClient;
            _jsonOptions = jsonOptions;
        }

        public string AccessToken => _tokens.AccessToken;

        public bool LoadSecrets()
        {
            if (!File.Exists(AuthFile)) return false;
            var lines = File.ReadAllLines(AuthFile);
            if (lines.Length < 2) return false;
            _clientId = lines[0].Trim();
            _clientSecret = lines[1].Trim();
            return !string.IsNullOrEmpty(_clientId) && !string.IsNullOrEmpty(_clientSecret);
        }

        public void SetSecrets(string clientId, string clientSecret)
        {
            _clientId = clientId;
            _clientSecret = clientSecret;
        }

        public void SaveSecrets()
        {
            File.WriteAllLines(AuthFile, new[] { _clientId, _clientSecret });
        }

        public async Task<bool> InitializeAsync()
        {
             if (File.Exists(TokenFile))
             {
                 string content = File.ReadAllText(TokenFile);
                 if (!string.IsNullOrWhiteSpace(content) && content.Trim().StartsWith("{"))
                 {
                     _tokens = JsonSerializer.Deserialize<TokenStorage>(content, _jsonOptions) ?? new TokenStorage();
                     Console.Write("Verifying session... ");
                     bool authenticated = await CheckConnectionAsync();

                     if (!authenticated && !string.IsNullOrEmpty(_tokens.RefreshToken))
                     {
                         Console.WriteLine("\nAccess expired. Attempting refresh...");
                         authenticated = await RefreshTokenAsync();
                     }
                     return authenticated;
                 }
             }
             return false;
        }

        public async Task<bool> CheckConnectionAsync()
        {
            try { return (await _httpClient.GetAsync("https://api.toodledo.com/3/account/get.php?access_token=" + _tokens.AccessToken)).IsSuccessStatusCode; }
            catch { return false; }
        }

        public async Task<bool> RefreshTokenAsync()
        {
            var values = new Dictionary<string, string> { { "grant_type", "refresh_token" }, { "refresh_token", _tokens.RefreshToken }, { "client_id", _clientId }, { "client_secret", _clientSecret } };
            var response = await _httpClient.PostAsync("https://api.toodledo.com/3/account/token.php", new FormUrlEncodedContent(values));
            if (!response.IsSuccessStatusCode) return false;
            var data = JsonSerializer.Deserialize<TokenResponse>(await response.Content.ReadAsStringAsync(), _jsonOptions);
            if (data == null) return false;
            _tokens.AccessToken = data.access_token; _tokens.RefreshToken = data.refresh_token;
            SaveTokens(); return true;
        }

        public async Task AuthorizeAsync()
        {
            using var listener = new HttpListener();
            listener.Prefixes.Add(RedirectUri);
            listener.Start();
            
            Console.WriteLine("Authorize in browser at localhost:5000...");
            
            // Open the browser automatically
            try { 
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = RedirectUri, UseShellExecute = true }); 
            } catch { }

            string? code = null;
            while (string.IsNullOrEmpty(code))
            {
                var context = await listener.GetContextAsync();
                var request = context.Request;
                var response = context.Response;

                if (request.Url?.AbsolutePath == "/")
                {
                    code = request.QueryString["code"];
                    if (!string.IsNullOrEmpty(code))
                    {
                        // User has returned with code
                        string successHtml = GetSuccessHtml();
                        byte[] buffer = Encoding.UTF8.GetBytes(successHtml);
                        response.ContentType = "text/html; charset=utf-8";
                        response.ContentEncoding = Encoding.UTF8;
                        response.ContentLength64 = buffer.Length;
                        response.OutputStream.Write(buffer, 0, buffer.Length);
                        response.OutputStream.Close();
                    }
                    else
                    {
                        // Redirect to Toodledo Auth
                        string scope = "basic%20tasks%20notes%20lists%20write";
                        string state = Guid.NewGuid().ToString("N"); // Simple state
                        string authUrl = $"https://api.toodledo.com/3/account/authorize.php?response_type=code&client_id={_clientId}&state={state}&scope={scope}";
                        
                        response.Redirect(authUrl);
                        response.OutputStream.Close();
                    }
                }
                else
                {
                   // Ignore other requests (favicon, etc)
                   response.StatusCode = 404;
                   response.Close();
                }
            }
            listener.Stop();

            var values = new Dictionary<string, string> { 
                { "grant_type", "authorization_code" }, 
                { "code", code }, 
                { "redirect_uri", RedirectUri }, 
                { "client_id", _clientId }, 
                { "client_secret", _clientSecret } 
            };
            
            var apiResponse = await _httpClient.PostAsync("https://api.toodledo.com/3/account/token.php", new FormUrlEncodedContent(values));
            if (apiResponse.IsSuccessStatusCode)
            {
                 var json = await apiResponse.Content.ReadAsStringAsync();
                 var data = JsonSerializer.Deserialize<TokenResponse>(json, _jsonOptions);
                 if (data != null)
                 {
                     _tokens.AccessToken = data.access_token; 
                     _tokens.RefreshToken = data.refresh_token; 
                     SaveTokens();
                 }
            }
            else
            {
                 Console.WriteLine("Error exchanging code for token.");
            }
        }

        private string GetSuccessHtml()
        {
            return @"
                <html>
                <head>
                    <style>
                        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; text-align: center; padding: 50px; background-color: #f0f2f5; color: #333; }
                        .container { background: white; padding: 40px; border-radius: 8px; box-shadow: 0 4px 6px rgba(0,0,0,0.1); display: inline-block; max-width: 400px; }
                        h1 { color: #28a745; margin-bottom: 20px; }
                        p { font-size: 18px; margin-bottom: 30px; color: #666; }
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <h1>Authorization Successful!</h1>
                        <p>You have successfully logged in to Toodledo Console.</p>
                        <p>You can close this window now and return to the application.</p>
                    </div>
                </body>
                </html>";
        }

        private void SaveTokens() => File.WriteAllText(TokenFile, JsonSerializer.Serialize(_tokens, _jsonOptions));
    }
}
