using System;
using System.Collections.Generic;
using System.Linq;
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

            var taskData = JsonSerializer.Serialize(new[] { taskObject }, _jsonOptions);
            var content = new FormUrlEncodedContent(new[] {
                new KeyValuePair<string, string>("access_token", _authService.AccessToken),
                new KeyValuePair<string, string>("tasks", taskData)
            });
            var response = await _httpClient.PostAsync("https://api.toodledo.com/3/tasks/add.php", content);
            var responseJson = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("errorCode", out var errCode))
            {
                throw new ToodledoApiException(errCode.GetInt32(), doc.RootElement.GetProperty("errorDesc").GetString() ?? "Unknown API Error");
            }

            return response.IsSuccessStatusCode;
        }


        public async Task<List<ToodledoTask>> GetTasksAsync(string queryParams = "")
        {
            var fields = "folder,context,star,priority,duedate,status,tag,note,added,location";
            var url = $"https://api.toodledo.com/3/tasks/get.php?access_token={_authService.AccessToken}&comp=0&fields={fields}{queryParams}";
            var json = await _httpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("errorCode", out var errCode))
            {
                throw new ToodledoApiException(errCode.GetInt32(), doc.RootElement.GetProperty("errorDesc").GetString() ?? "Unknown API Error");
            }

            if (doc.RootElement.ValueKind != JsonValueKind.Array) return new List<ToodledoTask>();

            var tasks = new List<ToodledoTask>();
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                // Skip metadata object which doesn't have an 'id' property
                if (!element.TryGetProperty("id", out var idValue)) continue;

                var task = new ToodledoTask
                {
                    id = (idValue.ValueKind == JsonValueKind.Number ? idValue.GetInt64().ToString() : idValue.GetString()) ?? string.Empty,
                    title = (element.TryGetProperty("title", out var tProp) ? tProp.GetString() : "No Title") ?? "No Title"
                };

                if (element.TryGetProperty("priority", out var pProp)) task.priority = pProp.GetInt32();
                if (element.TryGetProperty("folder", out var fProp)) task.folder = fProp.GetInt64();
                if (element.TryGetProperty("context", out var cProp)) task.context = cProp.GetInt64();
                if (element.TryGetProperty("star", out var sProp)) task.star = sProp.GetInt32();
                if (element.TryGetProperty("duedate", out var dProp)) task.duedate = dProp.GetInt64();
                if (element.TryGetProperty("status", out var stProp)) task.status = stProp.GetInt32();
                if (element.TryGetProperty("tag", out var tagProp)) task.tag = tagProp.GetString() ?? string.Empty;
                if (element.TryGetProperty("note", out var noteProp)) task.note = noteProp.GetString() ?? string.Empty;
                if (element.TryGetProperty("added", out var addedProp)) task.added = addedProp.GetInt64();
                if (element.TryGetProperty("location", out var locProp)) task.location = locProp.GetInt64();

                tasks.Add(task);
            }
            return tasks;
        }

        public async Task<ToodledoTask?> GetTaskAsync(string id)
        {
            var fields = "folder,context,star,priority,duedate,status,tag,note,added,location";
            var url = $"https://api.toodledo.com/3/tasks/get.php?access_token={_authService.AccessToken}&id={id}&fields={fields}";
            var json = await _httpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("errorCode", out var errCode))
            {
                throw new ToodledoApiException(errCode.GetInt32(), doc.RootElement.GetProperty("errorDesc").GetString() ?? "Unknown API Error");
            }

            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;

            // Toodledo API returns an array where the first element is metadata
            // and subsequent elements are the actual tasks. We need to find the one with an ID.
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.TryGetProperty("id", out var idValue))
                {
                    return new ToodledoTask
                    {
                        id = (idValue.ValueKind == JsonValueKind.Number ? idValue.GetInt64().ToString() : idValue.GetString()) ?? string.Empty,
                        title = (element.TryGetProperty("title", out var tProp) ? tProp.GetString() : "No Title") ?? "No Title",
                        priority = element.TryGetProperty("priority", out var pProp) ? pProp.GetInt32() : 0,
                        folder = element.TryGetProperty("folder", out var fProp) ? fProp.GetInt64() : 0,
                        context = element.TryGetProperty("context", out var cProp) ? cProp.GetInt64() : 0,
                        star = element.TryGetProperty("star", out var sProp) ? sProp.GetInt32() : 0,
                        duedate = element.TryGetProperty("duedate", out var dProp) ? dProp.GetInt64() : 0,
                        status = element.TryGetProperty("status", out var stProp) ? stProp.GetInt32() : 0,
                        tag = (element.TryGetProperty("tag", out var tagProp) ? tagProp.GetString() : "") ?? "",
                        note = (element.TryGetProperty("note", out var noteProp) ? noteProp.GetString() : "") ?? "",
                        added = element.TryGetProperty("added", out var addedProp) ? addedProp.GetInt64() : 0,
                        location = element.TryGetProperty("location", out var locProp) ? locProp.GetInt64() : 0
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

            var taskData = JsonSerializer.Serialize(new[] { taskObject }, _jsonOptions);
            var content = new FormUrlEncodedContent(new[] {
                new KeyValuePair<string, string>("access_token", _authService.AccessToken),
                new KeyValuePair<string, string>("tasks", taskData)
            });
            var response = await _httpClient.PostAsync("https://api.toodledo.com/3/tasks/edit.php", content);
            var responseJson = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("errorCode", out var errCode))
            {
                throw new ToodledoApiException(errCode.GetInt32(), doc.RootElement.GetProperty("errorDesc").GetString() ?? "Unknown API Error");
            }

            return response.IsSuccessStatusCode;
        }

        public async Task<bool> CompleteTasksAsync(IEnumerable<string> ids)
        {
            var taskList = new List<Dictionary<string, object>>();
            foreach (var id in ids)
            {
                taskList.Add(new Dictionary<string, object>
                {
                    { "id", id },
                    { "completed", 1 }
                });
            }

            var taskData = JsonSerializer.Serialize(taskList, _jsonOptions);
            var content = new FormUrlEncodedContent(new[] {
                new KeyValuePair<string, string>("access_token", _authService.AccessToken),
                new KeyValuePair<string, string>("tasks", taskData)
            });
            var response = await _httpClient.PostAsync("https://api.toodledo.com/3/tasks/edit.php", content);
            var responseJson = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("errorCode", out var errCode))
            {
                throw new ToodledoApiException(errCode.GetInt32(), doc.RootElement.GetProperty("errorDesc").GetString() ?? "Unknown API Error");
            }

            return response.IsSuccessStatusCode;
        }

        public async Task<bool> CompleteTaskAsync(string id)
        {
            return await CompleteTasksAsync(new[] { id });
        }

        public async Task<bool> DeleteTasksAsync(IEnumerable<string> ids)
        {
            var taskData = JsonSerializer.Serialize(ids.ToArray(), _jsonOptions);
            var content = new FormUrlEncodedContent(new[] {
                new KeyValuePair<string, string>("access_token", _authService.AccessToken),
                new KeyValuePair<string, string>("tasks", taskData)
            });
            var response = await _httpClient.PostAsync("https://api.toodledo.com/3/tasks/delete.php", content);
            var responseJson = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("errorCode", out var errCode))
            {
                throw new ToodledoApiException(errCode.GetInt32(), doc.RootElement.GetProperty("errorDesc").GetString() ?? "Unknown API Error");
            }

            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteTaskAsync(string id)
        {
            return await DeleteTasksAsync(new[] { id });
        }

        public async Task<int> GetCompletedCountAsync()
        {
            var url = $"https://api.toodledo.com/3/tasks/get.php?access_token={_authService.AccessToken}&comp=1&fields=id";
            var json = await _httpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("errorCode", out var errCode))
            {
                throw new ToodledoApiException(errCode.GetInt32(), doc.RootElement.GetProperty("errorDesc").GetString() ?? "Unknown API Error");
            }

            if (doc.RootElement.ValueKind != JsonValueKind.Array) return 0;

            // Toodledo API returns an array where the first element is metadata
            // e.g. {"num":"42","total":"42"}
            var metadata = doc.RootElement[0];
            if (metadata.TryGetProperty("total", out var totalProp))
            {
                if (totalProp.ValueKind == JsonValueKind.Number) return totalProp.GetInt32();
                if (totalProp.ValueKind == JsonValueKind.String && int.TryParse(totalProp.GetString(), out var count)) return count;
            }

            return 0;
        }
    }
}
