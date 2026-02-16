using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace ToodledoConsole
{
    public class FolderService
    {
        private readonly HttpClient _httpClient;
        private readonly AuthService _authService;
        private readonly JsonSerializerOptions _jsonOptions;

        public FolderService(HttpClient httpClient, AuthService authService, JsonSerializerOptions jsonOptions)
        {
            _httpClient = httpClient;
            _authService = authService;
            _jsonOptions = jsonOptions;
        }

        public async Task<List<ToodledoFolder>> GetFoldersAsync()
        {
            var url = $"https://api.toodledo.com/3/folders/get.php?access_token={_authService.AccessToken}";
            var json = await _httpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("errorCode", out var errCode))
            {
                throw new ToodledoApiException(errCode.GetInt32(), doc.RootElement.GetProperty("errorDesc").GetString() ?? "Unknown API Error");
            }

            if (doc.RootElement.ValueKind != JsonValueKind.Array) return new List<ToodledoFolder>();

            var folders = new List<ToodledoFolder>();
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.TryGetProperty("id", out var idProp))
                {
                    folders.Add(new ToodledoFolder
                    {
                        id = idProp.GetInt64(),
                        name = element.GetProperty("name").GetString() ?? "Unknown"
                    });
                }
            }
            return folders;
        }

        public async Task<bool> AddFolderAsync(string name)
        {
            var content = new FormUrlEncodedContent(new[] {
                new KeyValuePair<string, string>("access_token", _authService.AccessToken),
                new KeyValuePair<string, string>("name", name)
            });
            var response = await _httpClient.PostAsync("https://api.toodledo.com/3/folders/add.php", content);
            var responseJson = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("errorCode", out var errCode))
            {
                throw new ToodledoApiException(errCode.GetInt32(), doc.RootElement.GetProperty("errorDesc").GetString() ?? "Unknown API Error");
            }

            return response.IsSuccessStatusCode;
        }

        public async Task<bool> EditFolderAsync(long id, string name)
        {
            var content = new FormUrlEncodedContent(new[] {
                new KeyValuePair<string, string>("access_token", _authService.AccessToken),
                new KeyValuePair<string, string>("id", id.ToString()),
                new KeyValuePair<string, string>("name", name)
            });
            var response = await _httpClient.PostAsync("https://api.toodledo.com/3/folders/edit.php", content);
            var responseJson = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("errorCode", out var errCode))
            {
                throw new ToodledoApiException(errCode.GetInt32(), doc.RootElement.GetProperty("errorDesc").GetString() ?? "Unknown API Error");
            }

            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteFolderAsync(long id)
        {
            var content = new FormUrlEncodedContent(new[] {
                new KeyValuePair<string, string>("access_token", _authService.AccessToken),
                new KeyValuePair<string, string>("id", id.ToString())
            });
            var response = await _httpClient.PostAsync("https://api.toodledo.com/3/folders/delete.php", content);
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
