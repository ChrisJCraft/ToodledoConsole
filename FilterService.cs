using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ToodledoConsole
{
    public class FilterService
    {
        private readonly TaskService _taskService;
        private List<ToodledoFolder> _folders;
        private List<ToodledoContext> _contexts;

        public FilterService(TaskService taskService)
        {
            _taskService = taskService;
        }

        public async Task<FilterCriteria> ParseFilterExpression(string input)
        {
            var criteria = new FilterCriteria();
            if (string.IsNullOrWhiteSpace(input)) return criteria;

            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var searchTerms = new List<string>();

            foreach (var part in parts)
            {
                if (part.StartsWith("p:"))
                {
                    if (int.TryParse(part.Substring(2), out int p)) criteria.Priority = p;
                }
                else if (part.StartsWith("f:"))
                {
                    criteria.FolderName = part.Substring(2);
                    criteria.FolderId = await GetFolderIdByName(criteria.FolderName);
                }
                else if (part.StartsWith("@"))
                {
                    criteria.ContextName = part.Substring(1);
                    criteria.ContextId = await GetContextIdByName(criteria.ContextName);
                }
                else if (part.StartsWith("*"))
                {
                    if (int.TryParse(part.Substring(1).Trim(':'), out int s)) criteria.Starred = s;
                }
                else if (part.StartsWith("!:"))
                {
                    criteria.DueDateShortcut = part.Substring(2).ToLower();
                }
                else if (part.StartsWith("s:"))
                {
                    if (int.TryParse(part.Substring(2), out int status)) criteria.Status = status;
                }
                else
                {
                    searchTerms.Add(part);
                }
            }

            if (searchTerms.Any())
            {
                criteria.SearchTerm = string.Join(" ", searchTerms);
            }

            return criteria;
        }

        private async Task<long?> GetFolderIdByName(string name)
        {
            if (_folders == null)
            {
                _folders = await _taskService.GetFoldersAsync();
            }
            return _folders.FirstOrDefault(f => f.name.Equals(name, StringComparison.OrdinalIgnoreCase))?.id;
        }

        private async Task<long?> GetContextIdByName(string name)
        {
            if (_contexts == null)
            {
                _contexts = await _taskService.GetContextsAsync();
            }
            return _contexts.FirstOrDefault(c => c.name.Equals(name, StringComparison.OrdinalIgnoreCase))?.id;
        }

        public List<ToodledoTask> ApplyClientSideFilters(List<ToodledoTask> tasks, FilterCriteria criteria)
        {
            IEnumerable<ToodledoTask> filtered = tasks;

            // Search term (if not already handled by server, though Toodledo API doesn't have a search param in get.php beyond some fields)
            if (!string.IsNullOrEmpty(criteria.SearchTerm))
            {
                filtered = filtered.Where(t => t.title.Contains(criteria.SearchTerm, StringComparison.OrdinalIgnoreCase));
            }

            // Due Date Shortcuts
            if (!string.IsNullOrEmpty(criteria.DueDateShortcut))
            {
                var now = DateTime.UtcNow.Date;
                long todayUnix = new DateTimeOffset(now).ToUnixTimeSeconds();
                
                switch (criteria.DueDateShortcut)
                {
                    case "today":
                        filtered = filtered.Where(t => t.duedate > 0 && IsSameDay(t.duedate, now));
                        break;
                    case "overdue":
                        filtered = filtered.Where(t => t.duedate > 0 && t.duedate < todayUnix && !IsSameDay(t.duedate, now));
                        break;
                    case "tomorrow":
                        var tomorrow = now.AddDays(1);
                        filtered = filtered.Where(t => t.duedate > 0 && IsSameDay(t.duedate, tomorrow));
                        break;
                    case "week":
                        var endOfWeek = now.AddDays(7);
                        long endOfWeekUnix = new DateTimeOffset(endOfWeek).ToUnixTimeSeconds();
                        filtered = filtered.Where(t => t.duedate > 0 && t.duedate >= todayUnix && t.duedate <= endOfWeekUnix);
                        break;
                }
            }

            return filtered.ToList();
        }

        private bool IsSameDay(long unixTime, DateTime date)
        {
            var taskDate = DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime.Date;
            return taskDate == date.Date;
        }

        public string BuildApiQueryString(FilterCriteria criteria)
        {
            var queryParts = new List<string>();

            if (criteria.Priority.HasValue) queryParts.Add($"priority={criteria.Priority.Value}");
            if (criteria.FolderId.HasValue) queryParts.Add($"folder={criteria.FolderId.Value}");
            if (criteria.ContextId.HasValue) queryParts.Add($"context={criteria.ContextId.Value}");
            if (criteria.Starred.HasValue) queryParts.Add($"star={criteria.Starred.Value}");
            // Status is tricky in API if not provided as a filter, but we can try to pass it if we know the field
            // Toodledo 'get' doesn't have a 'status' filter directly usually, but let's check
            
            return queryParts.Any() ? "&" + string.Join("&", queryParts) : "";
        }
    }
}
