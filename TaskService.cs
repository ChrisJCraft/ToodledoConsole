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

        public async Task<bool> AddTaskAsync(FilterCriteria criteria)
        {
            var taskObject = new Dictionary<string, object>
            {
                { "title", criteria.SearchTerm ?? "New Task" }
            };

            if (criteria.Priority.HasValue) taskObject["priority"] = criteria.Priority.Value;
            if (criteria.FolderId.HasValue) taskObject["folder"] = criteria.FolderId.Value;
            if (criteria.ContextId.HasValue) taskObject["context"] = criteria.ContextId.Value;
            if (criteria.Starred.HasValue) taskObject["star"] = criteria.Starred.Value;
            if (criteria.Status.HasValue) taskObject["status"] = criteria.Status.Value;
            if (!string.IsNullOrEmpty(criteria.Tag)) taskObject["tag"] = criteria.Tag;
            if (!string.IsNullOrEmpty(criteria.Note)) taskObject["note"] = criteria.Note;

            var taskData = JsonSerializer.Serialize(new[] { taskObject });
            var content = new FormUrlEncodedContent(new[] {
                new KeyValuePair<string, string>("access_token", _authService.AccessToken),
                new KeyValuePair<string, string>("tasks", taskData)
            });
            var response = await _httpClient.PostAsync("https://api.toodledo.com/3/tasks/add.php", content);
            return response.IsSuccessStatusCode;
        }


        public async Task<List<ToodledoTask>> GetTasksAsync(string queryParams = "")
        {
            var fields = "folder,context,star,priority,duedate,status,tag,note";
            var url = $"https://api.toodledo.com/3/tasks/get.php?access_token={_authService.AccessToken}&comp=0&fields={fields}{queryParams}";
            var json = await _httpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return new List<ToodledoTask>();
            
            var tasks = new List<ToodledoTask>();
            foreach (var element in doc.RootElement.EnumerateArray()) {
                // Skip metadata object which doesn't have an 'id' property
                if (!element.TryGetProperty("id", out var idValue)) continue;

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
                    if (element.TryGetProperty("tag", out var tagProp)) task.tag = tagProp.GetString();
                    if (element.TryGetProperty("note", out var noteProp)) task.note = noteProp.GetString();

                    tasks.Add(task);
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

        public async Task<ToodledoTask> GetTaskAsync(string id)
        {
            var fields = "folder,context,star,priority,duedate,status,tag,note";
            var url = $"https://api.toodledo.com/3/tasks/get.php?access_token={_authService.AccessToken}&id={id}&fields={fields}";
            var json = await _httpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            
            // Toodledo API returns an array where the first element is metadata
            // and subsequent elements are the actual tasks. We need to find the one with an ID.
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.TryGetProperty("id", out var idValue))
                {
                    return new ToodledoTask { 
                        id = idValue.ValueKind == JsonValueKind.Number ? idValue.GetInt64().ToString() : idValue.GetString(),
                        title = element.TryGetProperty("title", out var tProp) ? tProp.GetString() : "No Title",
                        priority = element.TryGetProperty("priority", out var pProp) ? pProp.GetInt32() : 0,
                        folder = element.TryGetProperty("folder", out var fProp) ? fProp.GetInt64() : 0,
                        context = element.TryGetProperty("context", out var cProp) ? cProp.GetInt64() : 0,
                        star = element.TryGetProperty("star", out var sProp) ? sProp.GetInt32() : 0,
                        duedate = element.TryGetProperty("duedate", out var dProp) ? dProp.GetInt64() : 0,
                        status = element.TryGetProperty("status", out var stProp) ? stProp.GetInt32() : 0,
                        tag = element.TryGetProperty("tag", out var tagProp) ? tagProp.GetString() : "",
                        note = element.TryGetProperty("note", out var noteProp) ? noteProp.GetString() : ""
                    };
                }
            }
            
            return null;
        }

        public async Task<bool> UpdateTaskAsync(string id, FilterCriteria criteria)
        {
            var taskObject = new Dictionary<string, object>
            {
                { "id", id }
            };

            if (!string.IsNullOrEmpty(criteria.SearchTerm)) taskObject["title"] = criteria.SearchTerm;
            if (criteria.Priority.HasValue) taskObject["priority"] = criteria.Priority.Value;
            if (criteria.FolderId.HasValue) taskObject["folder"] = criteria.FolderId.Value;
            if (criteria.ContextId.HasValue) taskObject["context"] = criteria.ContextId.Value;
            if (criteria.Starred.HasValue) taskObject["star"] = criteria.Starred.Value;
            if (criteria.Status.HasValue) taskObject["status"] = criteria.Status.Value;
            if (!string.IsNullOrEmpty(criteria.Tag)) taskObject["tag"] = criteria.Tag;
            if (!string.IsNullOrEmpty(criteria.Note)) taskObject["note"] = criteria.Note;

            var taskData = JsonSerializer.Serialize(new[] { taskObject });
            var content = new FormUrlEncodedContent(new[] {
                new KeyValuePair<string, string>("access_token", _authService.AccessToken),
                new KeyValuePair<string, string>("tasks", taskData)
            });
            var response = await _httpClient.PostAsync("https://api.toodledo.com/3/tasks/edit.php", content);
            return response.IsSuccessStatusCode;
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

        public async Task<bool> DeleteTaskAsync(string id)
        {
            var taskData = "[\"" + id + "\"]";
            var content = new FormUrlEncodedContent(new[] {
                new KeyValuePair<string, string>("access_token", _authService.AccessToken),
                new KeyValuePair<string, string>("tasks", taskData)
            });
            var response = await _httpClient.PostAsync("https://api.toodledo.com/3/tasks/delete.php", content);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> AddContextAsync(string name)
        {
            var contextObject = new[] { new { name = name } };
            var contextData = JsonSerializer.Serialize(contextObject);
            var content = new FormUrlEncodedContent(new[] {
                new KeyValuePair<string, string>("access_token", _authService.AccessToken),
                new KeyValuePair<string, string>("contexts", contextData)
            });
            var response = await _httpClient.PostAsync("https://api.toodledo.com/3/contexts/add.php", content);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> EditContextAsync(long id, string name)
        {
            var contextObject = new[] { new { id = id, name = name } };
            var contextData = JsonSerializer.Serialize(contextObject);
            var content = new FormUrlEncodedContent(new[] {
                new KeyValuePair<string, string>("access_token", _authService.AccessToken),
                new KeyValuePair<string, string>("contexts", contextData)
            });
            var response = await _httpClient.PostAsync("https://api.toodledo.com/3/contexts/edit.php", content);
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
            return response.IsSuccessStatusCode;
        }
    }
}
