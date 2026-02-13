using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace ToodledoConsole
{
    public class TaskService
    {
        private readonly HttpClient _httpClient;
        private readonly AuthService _authService;
        private readonly JsonSerializerOptions _jsonOptions;

        public TaskService(HttpClient httpClient, AuthService authService, JsonSerializerOptions jsonOptions)
        {
            _httpClient = httpClient;
            _authService = authService;
            _jsonOptions = jsonOptions;
        }

        public async Task<bool> AddTaskAsync(string title)
        {
            var taskData = JsonSerializer.Serialize(new[] { new { title = title } });
            var content = new FormUrlEncodedContent(new[] {
                new KeyValuePair<string, string>("access_token", _authService.AccessToken),
                new KeyValuePair<string, string>("tasks", taskData)
            });
            var response = await _httpClient.PostAsync("https://api.toodledo.com/3/tasks/add.php", content);
            return response.IsSuccessStatusCode;
        }

        public async Task<List<ToodledoTask>> GetTasksAsync()
        {
            var url = "https://api.toodledo.com/3/tasks/get.php?access_token=" + _authService.AccessToken + "&comp=0";
            var json = await _httpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return new List<ToodledoTask>();
            
            var tasks = new List<ToodledoTask>();
            foreach (var element in doc.RootElement.EnumerateArray()) {
                if (element.TryGetProperty("id", out var idValue)) {
                    tasks.Add(new ToodledoTask { 
                        id = idValue.ValueKind == JsonValueKind.Number ? idValue.GetInt64().ToString() : idValue.GetString(),
                        title = element.TryGetProperty("title", out var tProp) ? tProp.GetString() : "No Title"
                    });
                }
            }
            return tasks;
        }

        public async Task<bool> CompleteTaskAsync(string id)
        {
            var taskData = "[{\"id\":\"" + id + "\",\"completed\":1}]";
            var content = new FormUrlEncodedContent(new[] {
                new KeyValuePair<string, string>("access_token", _authService.AccessToken),
                new KeyValuePair<string, string>("tasks", taskData)
            });
            var response = await _httpClient.PostAsync("https://api.toodledo.com/3/tasks/edit.php", content);
            return response.IsSuccessStatusCode;
        }
    }
}
