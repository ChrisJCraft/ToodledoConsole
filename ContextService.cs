using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace ToodledoConsole
{
    public class ToodledoApiException : Exception
    {
        public int ErrorCode { get; }
        public ToodledoApiException(int errorCode, string message) : base(message)
        {
            ErrorCode = errorCode;
        }
    }
    public class ContextService
    {
        private readonly HttpClient _httpClient;
        private readonly AuthService _authService;
        private readonly JsonSerializerOptions _jsonOptions;

        public ContextService(HttpClient httpClient, AuthService authService, JsonSerializerOptions jsonOptions)
        {
            _httpClient = httpClient;
            _authService = authService;
            _jsonOptions = jsonOptions;
        }

        public async Task<List<ToodledoContext>> GetContextsAsync()
        {
            var url = $"https://api.toodledo.com/3/contexts/get.php?access_token={_authService.AccessToken}";
            var json = await _httpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("errorCode", out var errCode))
            {
                throw new ToodledoApiException(errCode.GetInt32(), doc.RootElement.GetProperty("errorDesc").GetString() ?? "Unknown API Error");
            }

            if (doc.RootElement.ValueKind != JsonValueKind.Array) return new List<ToodledoContext>();

            var contexts = new List<ToodledoContext>();
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.TryGetProperty("id", out var idProp))
                {
                    contexts.Add(new ToodledoContext
                    {
                        id = idProp.GetInt64(),
                        name = element.GetProperty("name").GetString() ?? "Unknown"
                    });
                }
            }
            return contexts;
        }

        public async Task<bool> AddContextAsync(string name)
        {
            var contextObject = new Dictionary<string, object> { { "name", name } };
            var contextData = JsonSerializer.Serialize(new[] { contextObject }, _jsonOptions);
            var content = new FormUrlEncodedContent(new[] {
                new KeyValuePair<string, string>("access_token", _authService.AccessToken),
                new KeyValuePair<string, string>("contexts", contextData)
            });
            var response = await _httpClient.PostAsync("https://api.toodledo.com/3/contexts/add.php", content);
            var responseJson = await response.Content.ReadAsStringAsync();
            
            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("errorCode", out var errCode))
            {
                throw new ToodledoApiException(errCode.GetInt32(), doc.RootElement.GetProperty("errorDesc").GetString() ?? "Unknown API Error");
            }

            return response.IsSuccessStatusCode;
        }

        public async Task<bool> EditContextAsync(long id, string name)
        {
            var contextObject = new Dictionary<string, object> { { "id", id }, { "name", name } };
            var contextData = JsonSerializer.Serialize(new[] { contextObject }, _jsonOptions);
            var content = new FormUrlEncodedContent(new[] {
                new KeyValuePair<string, string>("access_token", _authService.AccessToken),
                new KeyValuePair<string, string>("contexts", contextData)
            });
            var response = await _httpClient.PostAsync("https://api.toodledo.com/3/contexts/edit.php", content);
            var responseJson = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("errorCode", out var errCode))
            {
                throw new ToodledoApiException(errCode.GetInt32(), doc.RootElement.GetProperty("errorDesc").GetString() ?? "Unknown API Error");
            }

            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteContextAsync(long id)
        {
            var contextData = "[" + id + "]";
            var content = new FormUrlEncodedContent(new[] {
                new KeyValuePair<string, string>("access_token", _authService.AccessToken),
                new KeyValuePair<string, string>("contexts", contextData)
            });
            var response = await _httpClient.PostAsync("https://api.toodledo.com/3/contexts/delete.php", content);
            var responseJson = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("errorCode", out var errCode))
            {
                throw new ToodledoApiException(errCode.GetInt32(), doc.RootElement.GetProperty("errorDesc").GetString() ?? "Unknown API Error");
            }

            return response.IsSuccessStatusCode;
        }
    }
}
