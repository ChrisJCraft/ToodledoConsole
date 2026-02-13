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

        public async Task<List<ToodledoTask>> GetTasksAsync(string queryParams = "")
        {
            var fields = "folder,context,star,priority,duedate,status";
            var url = $"https://api.toodledo.com/3/tasks/get.php?access_token={_authService.AccessToken}&comp=0&fields={fields}{queryParams}";
            var json = await _httpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return new List<ToodledoTask>();
            
            var tasks = new List<ToodledoTask>();
            foreach (var element in doc.RootElement.EnumerateArray()) {
                if (element.TryGetProperty("id", out var idValue)) {
                    var task = new ToodledoTask { 
                        id = idValue.ValueKind == JsonValueKind.Number ? idValue.GetInt64().ToString() : idValue.GetString(),
                        title = element.TryGetProperty("title", out var tProp) ? tProp.GetString() : "No Title"
                    };

                    if (element.TryGetProperty("priority", out var pProp)) task.priority = pProp.GetInt32();
                    if (element.TryGetProperty("folder", out var fProp)) task.folder = fProp.GetInt64();
                    if (element.TryGetProperty("context", out var cProp)) task.context = cProp.GetInt64();
                    if (element.TryGetProperty("star", out var sProp)) task.star = sProp.GetInt32();
                    if (element.TryGetProperty("duedate", out var dProp)) task.duedate = dProp.GetInt64();
                    if (element.TryGetProperty("status", out var stProp)) task.status = stProp.GetInt32();

                    tasks.Add(task);
                }
            }
            return tasks;
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
                        name = element.GetProperty("name").GetString()
                    });
                }
            }
            return folders;
        }

        public async Task<List<ToodledoContext>> GetContextsAsync()
        {
            var url = $"https://api.toodledo.com/3/contexts/get.php?access_token={_authService.AccessToken}";
            var json = await _httpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return new List<ToodledoContext>();

            var contexts = new List<ToodledoContext>();
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.TryGetProperty("id", out var idProp))
                {
                    contexts.Add(new ToodledoContext
                    {
                        id = idProp.GetInt64(),
                        name = element.GetProperty("name").GetString()
                    });
                }
            }
            return contexts;
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
