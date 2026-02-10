using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ToodledoConsole
{
    public class TokenStorage
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
    }

    class Program
    {
        private const string AuthFile = "auth.txt";
        private const string TokenFile = "token.txt";
        private const string RedirectUri = "http://localhost:5000/";

        private static string _clientId;
        private static string _clientSecret;
        private static TokenStorage _tokens = new TokenStorage();
        private static readonly HttpClient _httpClient = new HttpClient();
        private static List<ToodledoTask> _cachedTasks = new List<ToodledoTask>();

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        static async Task Main(string[] args)
        {
            Console.Clear();
            Console.WriteLine("========================================");
            Console.WriteLine("   TOODLEDO CONSOLE v1.4.1");
            Console.WriteLine("========================================");

            try
            {
                if (!LoadSecrets()) return;

                bool authenticated = false;
                if (File.Exists(TokenFile))
                {
                    string content = File.ReadAllText(TokenFile);
                    if (!string.IsNullOrWhiteSpace(content) && content.Trim().StartsWith("{"))
                    {
                        _tokens = JsonSerializer.Deserialize<TokenStorage>(content, _jsonOptions) ?? new TokenStorage();
                        Console.Write("Verifying session... ");
                        authenticated = await CheckConnectionAsync();

                        if (!authenticated && !string.IsNullOrEmpty(_tokens.RefreshToken))
                        {
                            Console.WriteLine("\nAccess expired. Attempting refresh...");
                            authenticated = await RefreshTokenAsync();
                        }
                    }
                }

                if (!authenticated)
                {
                    Console.WriteLine("\n[AUTH REQUIRED]");
                    Console.WriteLine("1. Open your 'login.html' file.");
                    Console.WriteLine("2. Click 'Authorize Application'.");
                    await AuthorizeAsync();
                }

                Console.WriteLine("Success! Connection Verified.");
                await RunCommandLoop();
            }
            catch (Exception ex)
            {
                Console.WriteLine("\n[FATAL ERROR]: " + ex.Message);
            }
        }

        private static bool LoadSecrets()
        {
            if (!File.Exists(AuthFile))
            {
                File.WriteAllLines(AuthFile, new[] { "CLIENT_ID_HERE", "CLIENT_SECRET_HERE" });
                Console.WriteLine("Action Required: Update auth.txt with your API keys.");
                return false;
            }
            var lines = File.ReadAllLines(AuthFile);
            if (lines.Length < 2 || lines[0].Contains("HERE")) return false;
            _clientId = lines[0].Trim();
            _clientSecret = lines[1].Trim();
            return true;
        }

        private static async Task<bool> CheckConnectionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("https://api.toodledo.com/3/account/get.php?access_token=" + _tokens.AccessToken);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        private static async Task<bool> RefreshTokenAsync()
        {
            var values = new Dictionary<string, string> {
                { "grant_type", "refresh_token" },
                { "refresh_token", _tokens.RefreshToken },
                { "client_id", _clientId },
                { "client_secret", _clientSecret }
            };
            var response = await _httpClient.PostAsync("https://api.toodledo.com/3/account/token.php", new FormUrlEncodedContent(values));
            if (!response.IsSuccessStatusCode) return false;

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<TokenResponse>(json, _jsonOptions);
            _tokens.AccessToken = data.access_token;
            _tokens.RefreshToken = data.refresh_token;
            SaveTokens();
            return true;
        }

        private static async Task AuthorizeAsync()
        {
            using var listener = new HttpListener();
            listener.Prefixes.Add(RedirectUri);
            listener.Start();
            Console.WriteLine("Waiting for browser authorization on localhost:5000...");

            string code = null;
            while (string.IsNullOrEmpty(code))
            {
                var context = await listener.GetContextAsync();
                code = context.Request.QueryString["code"];

                string responseHtml = string.IsNullOrEmpty(code) 
                    ? "<html><body><h2>Waiting...</h2></body></html>" 
                    : "<html><body><h2>Authorized! Return to Console.</h2></body></html>";

                byte[] buffer = Encoding.UTF8.GetBytes(responseHtml);
                context.Response.ContentType = "text/html";
                context.Response.ContentLength64 = buffer.Length;
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                context.Response.OutputStream.Close();
            }

            listener.Stop();

            var values = new Dictionary<string, string> {
                { "grant_type", "authorization_code" },
                { "code", code },
                { "redirect_uri", RedirectUri },
                { "client_id", _clientId },
                { "client_secret", _clientSecret }
            };
            var response = await _httpClient.PostAsync("https://api.toodledo.com/3/account/token.php", new FormUrlEncodedContent(values));
            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<TokenResponse>(json, _jsonOptions);
            if (data != null) {
                _tokens.AccessToken = data.access_token;
                _tokens.RefreshToken = data.refresh_token;
                SaveTokens();
            }
        }

        private static async Task RunCommandLoop()
        {
            Console.WriteLine("\nCommands: 'list' | 'find [text]' | 'random' | 'done [id]' | 'exit'");
            while (true)
            {
                Console.Write("\nToodledo> ");
                string input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input)) continue;

                string cleanInput = input.Trim();
                string lowerInput = cleanInput.ToLower();

                if (lowerInput == "exit") break;
                
                if (lowerInput == "list") 
                {
                    await ListTasks();
                }
                else if (lowerInput == "random") 
                {
                    await ShowRandom();
                }
                else if (lowerInput.StartsWith("find ")) 
                {
                    string term = cleanInput.Substring(5).Trim();
                    await SearchTasks(term);
                }
                else if (lowerInput.StartsWith("done ")) 
                {
                    string id = cleanInput.Substring(5).Trim();
                    await CompleteTask(id);
                }
                else 
                {
                    Console.WriteLine("Unknown command.");
                }
            }
        }

        private static async Task ListTasks()
        {
            try 
            {
                var url = "https://api.toodledo.com/3/tasks/get.php?access_token=" + _tokens.AccessToken;
                var response = await _httpClient.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();
                
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return;

                _cachedTasks.Clear();
                foreach (var element in doc.RootElement.EnumerateArray()) 
                {
                    if (element.TryGetProperty("id", out var idValue)) 
                    {
                        string idStr = idValue.ValueKind == JsonValueKind.Number 
                            ? idValue.GetInt64().ToString() 
                            : idValue.GetString();

                        string titleStr = element.TryGetProperty("title", out var tProp) 
                            ? tProp.GetString() 
                            : "No Title";

                        _cachedTasks.Add(new ToodledoTask { id = idStr, title = titleStr });
                    }
                }

                DisplayTasks(_cachedTasks);
            }
            catch (Exception ex) { Console.WriteLine("Error: " + ex.Message); }
        }

        private static async Task SearchTasks(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword)) {
                Console.WriteLine("Please provide a search term.");
                return;
            }

            if (_cachedTasks.Count == 0) await ListTasks();
            
            var results = _cachedTasks.FindAll(t => t.title.Contains(keyword, StringComparison.OrdinalIgnoreCase));

            if (results.Count == 0) {
                Console.WriteLine($"No tasks found matching: '{keyword}'");
            } else {
                Console.WriteLine($"\nFound {results.Count} matches:");
                DisplayTasks(results);
            }
        }

        private static void DisplayTasks(List<ToodledoTask> tasks)
        {
            Console.WriteLine(string.Format("\n{0,-12} | {1}", "ID", "Task"));
            Console.WriteLine(new string('-', 45));
            foreach (var t in tasks) 
            {
                Console.WriteLine(string.Format("{0,-12} | {1}", t.id, t.title));
            }
        }

        private static async Task ShowRandom()
        {
            if (_cachedTasks.Count == 0) await ListTasks();
            if (_cachedTasks.Count > 0) {
                var idx = new Random().Next(_cachedTasks.Count);
                var t = _cachedTasks[idx];
                Console.WriteLine("\n[PICK]: " + t.title + " (ID: " + t.id + ")");
            }
        }

        private static async Task CompleteTask(string id)
        {
            var taskData = "[{\"id\":\"" + id + "\",\"completed\":1}]";
            var content = new FormUrlEncodedContent(new[] {
                new KeyValuePair<string, string>("access_token", _tokens.AccessToken),
                new KeyValuePair<string, string>("tasks", taskData)
            });
            var response = await _httpClient.PostAsync("https://api.toodledo.com/3/tasks/edit.php", content);
            Console.WriteLine(response.IsSuccessStatusCode ? "Task Completed!" : "Error completing task.");
        }

        private static void SaveTokens() => File.WriteAllText(TokenFile, JsonSerializer.Serialize(_tokens, _jsonOptions));
    }

    public class TokenResponse { public string access_token { get; set; } public string refresh_token { get; set; } }
    public class ToodledoTask { public string id { get; set; } public string title { get; set; } }
}