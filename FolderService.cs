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
            var folderObject = new[] { new { name = name } };
            var folderData = JsonSerializer.Serialize(folderObject);
            var content = new FormUrlEncodedContent(new[] {
                new KeyValuePair<string, string>("access_token", _authService.AccessToken),
                new KeyValuePair<string, string>("folders", folderData)
            });
            var response = await _httpClient.PostAsync("https://api.toodledo.com/3/folders/add.php", content);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> EditFolderAsync(long id, string name)
        {
            var folderObject = new[] { new { id = id, name = name } };
            var folderData = JsonSerializer.Serialize(folderObject);
            var content = new FormUrlEncodedContent(new[] {
                new KeyValuePair<string, string>("access_token", _authService.AccessToken),
                new KeyValuePair<string, string>("folders", folderData)
            });
            var response = await _httpClient.PostAsync("https://api.toodledo.com/3/folders/edit.php", content);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteFolderAsync(long id)
        {
            var folderData = "[" + id + "]";
            var content = new FormUrlEncodedContent(new[] {
                new KeyValuePair<string, string>("access_token", _authService.AccessToken),
                new KeyValuePair<string, string>("folders", folderData)
            });
            var response = await _httpClient.PostAsync("https://api.toodledo.com/3/folders/delete.php", content);
            return response.IsSuccessStatusCode;
        }
    }
}
