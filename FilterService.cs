using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ToodledoConsole
{
    public class FilterService
    {
        private readonly TaskService _taskService;
        private readonly TaskParserService _taskParserService;

        public FilterService(TaskService taskService, TaskParserService taskParserService)
        {
            _taskService = taskService;
            _taskParserService = taskParserService;
        }

        public async Task<FilterCriteria> ParseFilterExpression(string input)
        {
            return await _taskParserService.ParseAsync(input);
        }

        public List<ToodledoTask> ApplyClientSideFilters(List<ToodledoTask> tasks, FilterCriteria criteria)
        {
            IEnumerable<ToodledoTask> filtered = tasks;

            // Search term (if not already handled by server, though Toodledo API doesn't have a search param in get.php beyond some fields)
            if (!string.IsNullOrEmpty(criteria.SearchTerm))
            {
                filtered = filtered.Where(t => t.title.Contains(criteria.SearchTerm, StringComparison.OrdinalIgnoreCase));
            }

            // Priority
            if (criteria.Priority.HasValue)
            {
                filtered = filtered.Where(t => t.priority == criteria.Priority.Value);
            }

            // Folder
            if (criteria.FolderId.HasValue)
            {
                filtered = filtered.Where(t => t.folder == criteria.FolderId.Value);
            }

            // Context
            if (criteria.ContextId.HasValue)
            {
                filtered = filtered.Where(t => t.context == criteria.ContextId.Value);
            }

            // Star
            if (criteria.Starred.HasValue)
            {
                filtered = filtered.Where(t => t.star == criteria.Starred.Value);
            }

            // Status
            if (criteria.Status.HasValue)
            {
                filtered = filtered.Where(t => t.status == criteria.Status.Value);
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
