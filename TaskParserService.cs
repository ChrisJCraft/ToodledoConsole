using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ToodledoConsole
{
    public class TaskParserService
    {
        private readonly TaskService _taskService;
        private List<ToodledoFolder> _folders;
        private List<ToodledoContext> _contexts;

        public TaskParserService(TaskService taskService)
        {
            _taskService = taskService;
        }

        public async Task<FilterCriteria> ParseAsync(string input)
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
                else if (part.StartsWith("#"))
                {
                    var newTag = part.Substring(1);
                    if (string.IsNullOrEmpty(criteria.Tag))
                    {
                        criteria.Tag = newTag;
                    }
                    else
                    {
                        criteria.Tag += "," + newTag;
                    }
                }
                else if (part.StartsWith("n:"))
                {
                    var note = part.Substring(2);
                    if (note.StartsWith("\"") && note.EndsWith("\"") && note.Length >= 2)
                    {
                        note = note.Substring(1, note.Length - 2);
                    }
                    criteria.Note = note;
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

        public string ToRawString(ToodledoTask task, List<ToodledoFolder> folders, List<ToodledoContext> contexts)
        {
            var parts = new List<string> { task.title };

            if (task.priority != 0) parts.Add($"p:{task.priority}");
            
            if (task.folder != 0)
            {
                var folder = folders.FirstOrDefault(f => f.id == task.folder);
                if (folder != null) parts.Add($"f:{folder.name}");
            }

            if (task.context != 0)
            {
                var context = contexts.FirstOrDefault(c => c.id == task.context);
                if (context != null) parts.Add($"@{context.name}");
            }

            if (task.star != 0) parts.Add($"*:{task.star}");
            
            if (task.status != 0) parts.Add($"s:{task.status}");

            if (!string.IsNullOrEmpty(task.tag))
            {
                var tags = task.tag.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var tag in tags)
                {
                    parts.Add($"#{tag.Trim()}");
                }
            }

            if (!string.IsNullOrEmpty(task.note))
            {
                parts.Add($"n:\"{task.note}\"");
            }

            // Note: duedate shortcut reconstruction is harder as we only stored the shortcut in criteria,
            // not a specific date mapping back to a shortcut easily without logic.
            // For now, focus on core attributes.

            return string.Join(" ", parts);
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
    }
}
