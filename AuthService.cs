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

        private string _clientId;
        private string _clientSecret;
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
            return true;
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
            _tokens.AccessToken = data.access_token; _tokens.RefreshToken = data.refresh_token;
            SaveTokens(); return true;
        }

        public async Task AuthorizeAsync()
        {
            using var listener = new HttpListener(); listener.Prefixes.Add(RedirectUri); listener.Start();
            Console.WriteLine("Authorize in browser at localhost:5000...");
            string code = null;
            while (string.IsNullOrEmpty(code)) {
                var context = await listener.GetContextAsync();
                code = context.Request.QueryString["code"];
                byte[] buffer = Encoding.UTF8.GetBytes("<html><body><h2>Authorized!</h2></body></html>");
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                context.Response.OutputStream.Close();
            }
            listener.Stop();
            var values = new Dictionary<string, string> { { "grant_type", "authorization_code" }, { "code", code }, { "redirect_uri", RedirectUri }, { "client_id", _clientId }, { "client_secret", _clientSecret } };
            var response = await _httpClient.PostAsync("https://api.toodledo.com/3/account/token.php", new FormUrlEncodedContent(values));
            var data = JsonSerializer.Deserialize<TokenResponse>(await response.Content.ReadAsStringAsync(), _jsonOptions);
            _tokens.AccessToken = data.access_token; _tokens.RefreshToken = data.refresh_token;
            SaveTokens();
        }

        private void SaveTokens() => File.WriteAllText(TokenFile, JsonSerializer.Serialize(_tokens, _jsonOptions));
    }
}
