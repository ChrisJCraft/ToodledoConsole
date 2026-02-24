using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Spectre.Console;

namespace ToodledoConsole
{


    class Program
    {
        private static AuthService _authService = null!;
        private static TaskService _taskService = null!;
        private static ContextService _contextService = null!;
        private static FolderService _folderService = null!;
        private static LocationService _locationService = null!;
        private static FilterService _filterService = null!;
        private static TaskParserService _taskParserService = null!;
        private static InputService _inputService = null!;

        private static readonly HttpClient _httpClient = new HttpClient();
        private static List<ToodledoTask> _cachedTasks = new List<ToodledoTask>();
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private static readonly string RandomStateFile = "random_state.json";
        private static HashSet<string> _seenTaskIds = new HashSet<string>();
        private static DateTime _lastCommandTime = DateTime.Now;

        static async Task Main(string[] args)
        {
            AnsiConsole.Clear();

            // Ensure app content fits within 100 columns as designed
            if (AnsiConsole.Profile.Width > 100)
            {
                AnsiConsole.Profile.Width = 100;
            }

            // Display styled banner
            var rule = new Rule("[cyan]TOODLEDO CONSOLE[/]");
            rule.Style = Style.Parse("cyan");
            AnsiConsole.Write(rule);

            var versionText = new Markup("[dim]v0.1.0[/]");
            AnsiConsole.Write(versionText);
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine();

            try
            {
                _authService = new AuthService(_httpClient, _jsonOptions);
                _taskService = new TaskService(_httpClient, _authService, _jsonOptions);
                _contextService = new ContextService(_httpClient, _authService, _jsonOptions);
                _folderService = new FolderService(_httpClient, _authService, _jsonOptions);
                _locationService = new LocationService(_httpClient, _authService, _jsonOptions);
                _taskParserService = new TaskParserService(_taskService, _folderService, _contextService);
                _filterService = new FilterService(_taskService, _taskParserService);
                _inputService = new InputService();

                if (!_authService.LoadSecrets())
                {
                    UIService.DisplaySetupWizard();
                    var clientId = AnsiConsole.Ask<string>("Enter your [cyan]Client ID[/]:");
                    var clientSecret = AnsiConsole.Ask<string>("Enter your [cyan]Client Secret[/]:");

                    if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                    {
                        AnsiConsole.MarkupLine("[red]Error:[/] Client ID and Secret are required.");
                        return;
                    }

                    _authService.SetSecrets(clientId, clientSecret);
                    _authService.SaveSecrets();
                    AnsiConsole.MarkupLine("[green]✓ Credentials saved to auth.txt[/]");
                }

                bool authenticated = await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("cyan"))
                    .StartAsync("[cyan]Initializing authentication...[/]", async ctx =>
                    {
                        return await _authService.InitializeAsync();
                    });

                if (!authenticated)
                {
                    AnsiConsole.MarkupLine("[yellow]1. A browser window should open automatically.[/]");
                    AnsiConsole.MarkupLine("[yellow]2. If not, visit http://localhost:5000/ to authorize.[/]");

                    await AnsiConsole.Status()
                        .Spinner(Spinner.Known.Dots)
                        .SpinnerStyle(Style.Parse("yellow"))
                        .StartAsync("[yellow]Waiting for authorization...[/]", async ctx =>
                        {
                            await _authService.AuthorizeAsync();
                        });
                }

                AnsiConsole.MarkupLine("[green]✓ Success! Connection Verified.[/]");
                AnsiConsole.WriteLine();

                await RunCommandLoop();
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ FATAL ERROR:[/] {ex.Message.EscapeMarkup()}");
            }
        }

        private static async Task RunCommandLoop()
        {
            UIService.DisplayHelp();

            while (true)
            {
                var input = _inputService.ReadLineWithHistory("[cyan]Toodledo> [/]");

                if (string.IsNullOrWhiteSpace(input)) continue;

                if ((DateTime.Now - _lastCommandTime).TotalMinutes > 55)
                {
                    bool refreshed = await AnsiConsole.Status()
                        .Spinner(Spinner.Known.Dots)
                        .SpinnerStyle(Style.Parse("yellow"))
                        .StartAsync("[yellow]Refreshing session due to inactivity...[/]", async ctx =>
                        {
                            return await _authService.RefreshTokenAsync();
                        });
                    
                    if (!refreshed)
                    {
                        AnsiConsole.MarkupLine("[red]✗ Failed to refresh session. You may need to restart the application.[/]");
                    }
                }

                _lastCommandTime = DateTime.Now;

                string cleanInput = input.Trim();
                string lowerInput = cleanInput.ToLower();

                if (lowerInput == "exit")
                {
                    AnsiConsole.MarkupLine("[yellow]Goodbye![/]");
                    break;
                }

                if (lowerInput == "help") UIService.DisplayHelp();
                else if (lowerInput == "list") await ListTasks();
                else if (lowerInput == "stats") await ShowStats();
                else if (lowerInput == "context" || lowerInput.StartsWith("context ")) await ListContexts(cleanInput.Length > 7 ? cleanInput.Substring(8).Trim() : "");
                else if (lowerInput == "random") await ShowRandom();
                else if (lowerInput.StartsWith("random ")) await ShowRandom(cleanInput.Substring(7).Trim());
                else if (lowerInput.StartsWith("filter ")) await FilterTasks(cleanInput.Substring(7).Trim());
                else if (lowerInput.StartsWith("find ")) await SearchTasks(cleanInput.Substring(5).Trim());
                else if (lowerInput.StartsWith("done ")) await CompleteTasks(cleanInput.Substring(5).Trim());
                else if (lowerInput.StartsWith("undone ")) await UncompleteTasks(cleanInput.Substring(7).Trim());
                else if (lowerInput.StartsWith("add ")) await AddTask(cleanInput.Substring(4).Trim());
                else if (lowerInput.StartsWith("edit ")) await EditTask(cleanInput.Substring(5).Trim());
                else if (lowerInput.StartsWith("view ")) await ViewTask(cleanInput.Substring(5).Trim());
                else if (lowerInput.StartsWith("tag ")) await TagTask(cleanInput.Substring(4).Trim());
                else if (lowerInput.StartsWith("note ")) await NoteTask(cleanInput.Substring(5).Trim());
                else if (lowerInput.StartsWith("context-add ")) await AddContext(cleanInput.Substring(12).Trim());
                else if (lowerInput.StartsWith("context-edit ")) await EditContext(cleanInput.Substring(13).Trim());
                else if (lowerInput.StartsWith("context-delete ")) await DeleteContext(cleanInput.Substring(15).Trim());
                else if (lowerInput == "folder" || lowerInput.StartsWith("folder ")) await ListFolders(cleanInput.Length > 6 ? cleanInput.Substring(7).Trim() : "");
                else if (lowerInput.StartsWith("folder-add ")) await AddFolder(cleanInput.Substring(11).Trim());
                else if (lowerInput.StartsWith("folder-edit ")) await EditFolder(cleanInput.Substring(12).Trim());
                else if (lowerInput.StartsWith("folder-delete ")) await DeleteFolder(cleanInput.Substring(14).Trim());
                else if (lowerInput == "location" || lowerInput.StartsWith("location ")) await ListLocations(cleanInput.Length > 8 ? cleanInput.Substring(9).Trim() : "");
                else if (lowerInput.StartsWith("location-add ")) await AddLocation(cleanInput.Substring(13).Trim());
                else if (lowerInput.StartsWith("location-edit ")) await EditLocation(cleanInput.Substring(14).Trim());
                else if (lowerInput.StartsWith("location-delete ")) await DeleteLocation(cleanInput.Substring(16).Trim());
                else if (lowerInput == "setup") await RunSetup();
                else if (lowerInput == "about") UIService.DisplayAbout();
                else if (lowerInput.StartsWith("delete ")) await DeleteTask(cleanInput.Substring(7).Trim());
                else if (lowerInput.StartsWith("star ")) await StarTasks(cleanInput.Substring(5).Trim());
                else if (lowerInput.StartsWith("unstar ")) await UnstarTasks(cleanInput.Substring(7).Trim());
                else AnsiConsole.MarkupLine("[red]Unknown command. Type 'help' for available commands.[/]");
            }
        }

        private static async Task ShowStats()
        {
            try
            {
                var tasks = await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("cyan"))
                    .StartAsync("[cyan]Loading dashboard data...[/]", async ctx =>
                    {
                        var t = await _taskService.GetTasksAsync();
                        var f = await _folderService.GetFoldersAsync();
                        var c = await _contextService.GetContextsAsync();
                        var l = await _locationService.GetLocationsAsync();
                        var comp = await _taskService.GetCompletedCountAsync();
                        return (t, f, c, l, comp);
                    });

                UIService.DisplayStats(tasks.t, tasks.f, tasks.c, tasks.l, tasks.comp);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ Error loading stats:[/] {ex.Message.EscapeMarkup()}");
            }
        }

        private static async Task AddTask(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return;

            var criteria = await _taskParserService.ParseAsync(input);
            string title = criteria.SearchTerm ?? "New Task";

            bool success = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("cyan"))
                .StartAsync($"[cyan]Adding task...[/]", async ctx =>
                {
                    return await _taskService.AddTaskAsync(criteria);
                });

            if (success)
            {
                AnsiConsole.MarkupLine($"[green]✓ ADDED:[/] {title.EscapeMarkup()}");

                // Show applied attributes
                var attributes = new List<string>();
                if (criteria.Priority.HasValue) attributes.Add($"[yellow]Priority:[/] {UIService.GetPriorityName(criteria.Priority.Value)}");
                if (!string.IsNullOrEmpty(criteria.FolderName)) attributes.Add($"[green]Folder:[/] {criteria.FolderName}");
                if (!string.IsNullOrEmpty(criteria.ContextName)) attributes.Add($"[blue]Context:[/] @{criteria.ContextName}");
                if (criteria.Starred.HasValue) attributes.Add($"[gold1]Starred:[/] {(criteria.Starred == 1 ? "Yes" : "No")}");

                if (attributes.Count > 0)
                {
                    AnsiConsole.MarkupLine($"[dim]Attributes:[/] {string.Join(" [dim]|[/] ", attributes)}");
                }

                await ListTasks();
            }
            else
            {
                AnsiConsole.MarkupLine("[red]✗ Error adding task.[/]");
            }
        }


        private static async Task ListTasks()
        {
            try
            {
                _cachedTasks = await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("cyan"))
                    .StartAsync("[cyan]Loading tasks...[/]", async ctx =>
                    {
                        return await _taskService.GetTasksAsync();
                    });

                UIService.DisplayTasks(_cachedTasks);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ Error:[/] {ex.Message.EscapeMarkup()}");
            }
        }

        private static async Task FilterTasks(string filterExpression)
        {
            if (string.IsNullOrWhiteSpace(filterExpression))
            {
                await ListTasks();
                return;
            }

            try
            {
                var criteria = await _filterService.ParseFilterExpression(filterExpression);
                var queryParams = _filterService.BuildApiQueryString(criteria);

                var tasks = await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("cyan"))
                    .StartAsync("[cyan]Filtering tasks...[/]", async ctx =>
                    {
                        return await _taskService.GetTasksAsync(queryParams);
                    });

                var filteredResults = _filterService.ApplyClientSideFilters(tasks, criteria);

                // Get total count for the footer
                var totalTasksCount = _cachedTasks.Count > 0 ? _cachedTasks.Count : (await _taskService.GetTasksAsync()).Count;
                if (_cachedTasks.Count == 0) _cachedTasks = tasks;

                UIService.DisplayFilteredTasks(filteredResults, criteria, totalTasksCount);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ Filter error:[/] {ex.Message.EscapeMarkup()}");
            }
        }


        private static async Task SearchTasks(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword)) return;
            if (_cachedTasks.Count == 0) await ListTasks();

            var results = _cachedTasks.FindAll(t => t.title.Contains(keyword, StringComparison.OrdinalIgnoreCase));

            if (results.Count == 0)
            {
                AnsiConsole.MarkupLine($"[yellow]No matches for: '{keyword}'[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[cyan]Search results for:[/] [white]{keyword}[/]");
                UIService.DisplayTasks(results);
            }
        }


        private static async Task ShowRandom(string? filterExpression = null)
        {
            List<ToodledoTask> pool;

            if (string.IsNullOrWhiteSpace(filterExpression))
            {
                if (_cachedTasks.Count == 0) await ListTasks();
                pool = _cachedTasks;
            }
            else
            {
                try
                {
                    var criteria = await _filterService.ParseFilterExpression(filterExpression);
                    var queryParams = _filterService.BuildApiQueryString(criteria);
                    var tasks = await AnsiConsole.Status()
                        .Spinner(Spinner.Known.Dots)
                        .SpinnerStyle(Style.Parse("cyan"))
                        .StartAsync($"[cyan]Picking random from {filterExpression}...[/]", async ctx =>
                        {
                            return await _taskService.GetTasksAsync(queryParams);
                        });
                    pool = _filterService.ApplyClientSideFilters(tasks, criteria);
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]✗ Filter error:[/] {ex.Message.EscapeMarkup()}");
                    return;
                }
            }

            if (pool.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No tasks found matching your selection.[/]");
                return;
            }

            LoadRandomState();

            // Get candidates (tasks not yet seen)
            var candidates = pool.Where(t => !_seenTaskIds.Contains(t.id)).ToList();

            // If all matching tasks have been seen, reset only those for a fresh cycle
            if (candidates.Count == 0)
            {
                foreach (var taskItem in pool) _seenTaskIds.Remove(taskItem.id);
                candidates = pool.ToList();
                AnsiConsole.MarkupLine("[yellow]🔄 CYCLE COMPLETE for this selection - Starting fresh random order[/]");
            }

            // Pick random task from candidates
            var t = candidates[new Random().Next(candidates.Count)];
            _seenTaskIds.Add(t.id);
            SaveRandomState();

            // Display in a styled panel
            var panel = new Panel($"[white]{t.title}[/]\n\n[dim]ID: {t.id}[/]")
            {
                Header = new PanelHeader("[green]🎯 RANDOM PICK[/]", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = Style.Parse("green"),
                Padding = new Padding(2, 1),
                Width = 100
            };

            AnsiConsole.WriteLine();
            AnsiConsole.Write(panel);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]Progress for this selection: {pool.Count(t => _seenTaskIds.Contains(t.id))}/{pool.Count} tasks shown this cycle[/]");
        }

        private static void LoadRandomState()
        {
            if (File.Exists(RandomStateFile))
            {
                try
                {
                    var json = File.ReadAllText(RandomStateFile);
                    var ids = JsonSerializer.Deserialize<List<string>>(json);
                    _seenTaskIds = ids != null ? new HashSet<string>(ids) : new HashSet<string>();
                }
                catch { _seenTaskIds = new HashSet<string>(); }
            }
        }

        private static void SaveRandomState()
        {
            try
            {
                var json = JsonSerializer.Serialize(_seenTaskIds.ToList());
                File.WriteAllText(RandomStateFile, json);
            }
            catch { /* Silently fail */ }
        }

        private static async Task CompleteTasks(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return;

            var ids = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (ids.Length == 0) return;

            bool success = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("green"))
                .StartAsync($"[green]Completing {(ids.Length > 1 ? ids.Length.ToString() + " tasks" : "task")}...[/]", async ctx =>
                {
                    return await _taskService.CompleteTasksAsync(ids);
                });

            if (success)
            {
                AnsiConsole.MarkupLine($"[green]✓ {(ids.Length > 1 ? ids.Length.ToString() + " Tasks" : "Task")} Completed![/]");
                var completedTasks = ids
                    .Select(id => _cachedTasks.FirstOrDefault(t => t.id == id))
                    .Where(t => t != null)
                    .ToList();
                foreach (var id in ids)
                {
                    _cachedTasks.RemoveAll(t => t.id == id);
                    _seenTaskIds.Remove(id);
                }
                SaveRandomState();
                foreach (var t in completedTasks)
                {
                    AnsiConsole.MarkupLine($"  [grey]#{Markup.Escape(t!.id)}[/] [white]{Markup.Escape(t.title)}[/]");
                }
                AnsiConsole.MarkupLine($"[cyan]{_cachedTasks.Count} Tasks Remaining.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]✗ Error completing one or more tasks.[/]");
            }
        }

        private static async Task UncompleteTasks(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return;

            var ids = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (ids.Length == 0) return;

            bool success = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("yellow"))
                .StartAsync($"[yellow]Marking {(ids.Length > 1 ? ids.Length.ToString() + " tasks" : "task")} as incomplete...[/]", async ctx =>
                {
                    return await _taskService.UncompleteTasksAsync(ids);
                });

            if (success)
            {
                AnsiConsole.MarkupLine($"[green]✓ {(ids.Length > 1 ? ids.Length.ToString() + " Tasks" : "Task")} marked incomplete![/]");
                AnsiConsole.MarkupLine($"[dim]Note: Task(s) restored to active list.[/]");
                // Refresh cached tasks so the restored task appears in list
                await ListTasks();
            }
            else
            {
                AnsiConsole.MarkupLine("[red]✗ Error marking one or more tasks as incomplete.[/]");
            }
        }

        private static async Task StarTasks(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return;

            var ids = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (ids.Length == 0) return;

            bool success = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("gold1"))
                .StartAsync($"[gold1]Starring {(ids.Length > 1 ? ids.Length.ToString() + " tasks" : "task")}...[/]", async ctx =>
                {
                    return await _taskService.StarTasksAsync(ids);
                });

            if (success)
            {
                AnsiConsole.MarkupLine($"[green]✓ {(ids.Length > 1 ? ids.Length.ToString() + " Tasks" : "Task")} Starred![/]");
                foreach (var id in ids)
                {
                    var task = _cachedTasks.FirstOrDefault(t => t.id == id);
                    if (task != null) task.star = 1;
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[red]✗ Error starring one or more tasks.[/]");
            }
        }

        private static async Task UnstarTasks(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return;

            var ids = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (ids.Length == 0) return;

            bool success = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("dim"))
                .StartAsync($"[dim]Unstarring {(ids.Length > 1 ? ids.Length.ToString() + " tasks" : "task")}...[/]", async ctx =>
                {
                    return await _taskService.UnstarTasksAsync(ids);
                });

            if (success)
            {
                AnsiConsole.MarkupLine($"[green]✓ {(ids.Length > 1 ? ids.Length.ToString() + " Tasks" : "Task")} Unstarred![/]");
                foreach (var id in ids)
                {
                    var task = _cachedTasks.FirstOrDefault(t => t.id == id);
                    if (task != null) task.star = 0;
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[red]✗ Error unstarring one or more tasks.[/]");
            }
        }

        private static async Task EditTask(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            var task = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("cyan"))
                .StartAsync("[cyan]Fetching task details...[/]", async ctx =>
                {
                    return await _taskService.GetTaskAsync(id);
                });

            if (task == null)
            {
                AnsiConsole.MarkupLine($"[red]✗ Task not found: {id}[/]");
                return;
            }

            var folders = await _folderService.GetFoldersAsync();
            var contexts = await _contextService.GetContextsAsync();

            UIService.DisplayTaskDetail(task, folders, contexts, "Editing Task", "yellow");

            // Reconstruct raw string for Shadow Prompt
            string rawString = _taskParserService.ToRawString(task, folders, contexts);

            // Shadow Prompt: Use Ask with DefaultValue
            var editedInput = AnsiConsole.Ask<string>("[cyan]Edit: [/]", rawString);

            if (string.IsNullOrWhiteSpace(editedInput) || editedInput == rawString)
            {
                AnsiConsole.MarkupLine("[yellow]No changes made.[/]");
                return;
            }

            var criteria = await _taskParserService.ParseAsync(editedInput);

            bool success = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("cyan"))
                .StartAsync("[cyan]Updating task...[/]", async ctx =>
                {
                    return await _taskService.UpdateTaskAsync(id, criteria);
                });

            if (success)
            {
                AnsiConsole.MarkupLine("[green]✓ Task Updated![/]");
                await ListTasks();
            }
            else
            {
                AnsiConsole.MarkupLine("[red]✗ Error updating task.[/]");
            }
        }

        private static async Task ViewTask(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            var task = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("cyan"))
                .StartAsync("[cyan]Fetching task details...[/]", async ctx =>
                {
                    return await _taskService.GetTaskAsync(id);
                });

            if (task == null)
            {
                AnsiConsole.MarkupLine("[red]✗ Task not found.[/]");
                return;
            }

            var folders = await _folderService.GetFoldersAsync();
            var contexts = await _contextService.GetContextsAsync();

            UIService.DisplayTaskDetail(task, folders, contexts, "Task Details", "cyan");
        }


        private static async Task TagTask(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return;

            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var ids = new List<string>();
            int i = 0;
            
            // Collect all leading numeric parts as IDs
            while (i < parts.Length)
            {
                string cleanId = parts[i].TrimEnd(',');
                if (long.TryParse(cleanId, out _))
                {
                    ids.Add(cleanId);
                    i++;
                }
                else break;
            }

            if (ids.Count == 0 || i == parts.Length)
            {
                AnsiConsole.MarkupLine("[red]Usage: tag <id1> [id2] ... <tags>[/]");
                return;
            }

            string tags = string.Join(" ", parts.Skip(i));
            var criteria = new FilterCriteria { Tag = tags };

            bool success = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("cyan"))
                .StartAsync($"[cyan]Updating tags for {ids.Count} task(s)...[/]", async ctx =>
                {
                    return await _taskService.UpdateTasksAsync(ids, criteria);
                });

            if (success)
            {
                AnsiConsole.MarkupLine($"[green]✓ Tags Updated for {ids.Count} task(s)![/]");
                await ListTasks();
            }
            else
            {
                AnsiConsole.MarkupLine("[red]✗ Error updating tags.[/]");
            }
        }

        private static async Task NoteTask(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return;

            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var ids = new List<string>();
            int i = 0;

            // Collect all leading numeric parts as IDs
            while (i < parts.Length)
            {
                string cleanId = parts[i].TrimEnd(',');
                if (long.TryParse(cleanId, out _))
                {
                    ids.Add(cleanId);
                    i++;
                }
                else break;
            }

            if (ids.Count == 0 || i == parts.Length)
            {
                AnsiConsole.MarkupLine("[red]Usage: note <id1> [id2] ... <text>[/]");
                return;
            }

            string note = string.Join(" ", parts.Skip(i));
            var criteria = new FilterCriteria { Note = note };

            bool success = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("cyan"))
                .StartAsync($"[cyan]Updating note for {ids.Count} task(s)...[/]", async ctx =>
                {
                    return await _taskService.UpdateTasksAsync(ids, criteria);
                });

            if (success)
            {
                AnsiConsole.MarkupLine($"[green]✓ Note Updated for {ids.Count} task(s)![/]");
                await ListTasks();
            }
            else
            {
                AnsiConsole.MarkupLine("[red]✗ Error updating note.[/]");
            }
        }

        private static async Task DeleteTask(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return;

            var ids = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (ids.Length == 0) return;

            var tasksToDelete = new List<ToodledoTask>();
            foreach (var id in ids)
            {
                var task = await _taskService.GetTaskAsync(id);
                if (task != null) tasksToDelete.Add(task);
                else AnsiConsole.MarkupLine($"[yellow]⚠ Task not found: {id}[/]");
            }

            if (tasksToDelete.Count == 0) return;

            if (!UIService.ConfirmDeletion("Task", tasksToDelete.Select(t => t.title).ToList()))
            {
                AnsiConsole.MarkupLine("[yellow]Delete cancelled.[/]");
                return;
            }

            bool success = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("red"))
                .StartAsync("[red]Deleting tasks...[/]", async ctx =>
                {
                    return await _taskService.DeleteTasksAsync(tasksToDelete.Select(t => t.id));
                });

            if (success)
            {
                AnsiConsole.MarkupLine($"[green]✓ {tasksToDelete.Count} Task(s) Deleted![/]");
                foreach (var t in tasksToDelete)
                {
                    _cachedTasks.RemoveAll(ct => ct.id == t.id);
                    _seenTaskIds.Remove(t.id);
                }
                SaveRandomState();
                await ListTasks();
            }
            else
            {
                AnsiConsole.MarkupLine("[red]✗ Error deleting one or more tasks.[/]");
            }
        }

        private static async Task AddContext(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                AnsiConsole.MarkupLine("[red]Usage: context-add <name>[/]");
                return;
            }

            try
            {
                bool success = await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("cyan"))
                    .StartAsync("[cyan]Adding context...[/]", async ctx =>
                    {
                        return await _contextService.AddContextAsync(name);
                    });

                if (success)
                {
                    _taskParserService.ClearCache();
                    AnsiConsole.MarkupLine($"[green]✓ Context Added:[/] {name.EscapeMarkup()}");
                }
                else
                    AnsiConsole.MarkupLine("[red]✗ Error adding context.[/]");
            }
            catch (ToodledoApiException ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ API Error ({ex.ErrorCode}):[/] {ex.Message.EscapeMarkup()}");
            }
        }

        private static async Task EditContext(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                AnsiConsole.MarkupLine("[red]Usage: context-edit <id_or_name> <new_name>[/]");
                return;
            }

            var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                AnsiConsole.MarkupLine("[red]Usage: context-edit <id_or_name> <new_name>[/]");
                return;
            }

            string identifier = parts[0];
            string newName = parts[1];

            try
            {
                var contexts = await _contextService.GetContextsAsync();
                var context = contexts.FirstOrDefault(c => c.id.ToString() == identifier || c.name.Equals(identifier, StringComparison.OrdinalIgnoreCase));

                if (context == null)
                {
                    AnsiConsole.MarkupLine($"[red]✗ Context not found: {identifier}[/]");
                    return;
                }

                bool success = await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("cyan"))
                    .StartAsync("[cyan]Updating context...[/]", async ctx =>
                    {
                        return await _contextService.EditContextAsync(context.id, newName);
                    });

                if (success)
                {
                    _taskParserService.ClearCache();
                    AnsiConsole.MarkupLine($"[green]✓ Context Updated:[/] {newName.EscapeMarkup()}");
                }
                else
                    AnsiConsole.MarkupLine("[red]✗ Error updating context.[/]");
            }
            catch (ToodledoApiException ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ API Error ({ex.ErrorCode}):[/] {ex.Message.EscapeMarkup()}");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ Error:[/] {ex.Message.EscapeMarkup()}");
            }
        }

        private static async Task DeleteContext(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                AnsiConsole.MarkupLine("[red]Usage: context-delete <id1> <id2>...[/]");
                return;
            }

            var identifiers = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (identifiers.Length == 0) return;

            try
            {
                var allContexts = await _contextService.GetContextsAsync();
                var contextsToDelete = new List<ToodledoContext>();

                foreach (var id in identifiers)
                {
                    var context = allContexts.FirstOrDefault(c => c.id.ToString() == id || c.name.Equals(id, StringComparison.OrdinalIgnoreCase));
                    if (context != null) contextsToDelete.Add(context);
                    else AnsiConsole.MarkupLine($"[yellow]⚠ Context not found: {id}[/]");
                }

                if (contextsToDelete.Count == 0) return;

                if (!UIService.ConfirmDeletion("Context", contextsToDelete.Select(c => c.name).ToList()))
                {
                    AnsiConsole.MarkupLine("[yellow]Delete cancelled.[/]");
                    return;
                }

                bool success = await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("red"))
                    .StartAsync("[red]Deleting contexts...[/]", async ctx =>
                    {
                        return await _contextService.DeleteContextsAsync(contextsToDelete.Select(c => c.id));
                    });

                if (success)
                {
                    _taskParserService.ClearCache();
                    AnsiConsole.MarkupLine($"[green]✓ {contextsToDelete.Count} Context(s) Deleted![/]");
                }
                else
                    AnsiConsole.MarkupLine("[red]✗ Error deleting one or more contexts.[/]");
            }
            catch (ToodledoApiException ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ API Error ({ex.ErrorCode}):[/] {ex.Message.EscapeMarkup()}");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ Error:[/] {ex.Message.EscapeMarkup()}");
            }
        }

        private static async Task ListContexts(string filter = "")
        {
            try
            {
                var contexts = await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("cyan"))
                    .StartAsync("[cyan]Loading contexts...[/]", async ctx =>
                    {
                        return await _contextService.GetContextsAsync();
                    });

                if (!string.IsNullOrWhiteSpace(filter))
                {
                    contexts = contexts.Where(c => c.name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                UIService.DisplayContexts(contexts);
            }
            catch (ToodledoApiException ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ API Error ({ex.ErrorCode}):[/] {ex.Message.EscapeMarkup()}");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ Error:[/] {ex.Message.EscapeMarkup()}");
            }
        }

        private static async Task AddFolder(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                AnsiConsole.MarkupLine("[red]Usage: folder-add <name>[/]");
                return;
            }

            try
            {
                bool success = await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("cyan"))
                    .StartAsync("[cyan]Adding folder...[/]", async ctx =>
                    {
                        return await _folderService.AddFolderAsync(name);
                    });

                if (success)
                {
                    _taskParserService.ClearCache();
                    AnsiConsole.MarkupLine($"[green]✓ Folder Added:[/] {name.EscapeMarkup()}");
                }
                else
                    AnsiConsole.MarkupLine("[red]✗ Error adding folder.[/]");
            }
            catch (ToodledoApiException ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ API Error ({ex.ErrorCode}):[/] {ex.Message.EscapeMarkup()}");
            }
        }

        private static async Task EditFolder(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                AnsiConsole.MarkupLine("[red]Usage: folder-edit <id_or_name> <new_name>[/]");
                return;
            }

            var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                AnsiConsole.MarkupLine("[red]Usage: folder-edit <id_or_name> <new_name>[/]");
                return;
            }

            string identifier = parts[0];
            string newName = parts[1];

            try
            {
                var folders = await _folderService.GetFoldersAsync();
                var folder = folders.FirstOrDefault(f => f.id.ToString() == identifier || f.name.Equals(identifier, StringComparison.OrdinalIgnoreCase));

                if (folder == null)
                {
                    AnsiConsole.MarkupLine($"[red]✗ Folder not found: {identifier}[/]");
                    return;
                }

                bool success = await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("cyan"))
                    .StartAsync("[cyan]Updating folder...[/]", async ctx =>
                    {
                        return await _folderService.EditFolderAsync(folder.id, newName);
                    });

                if (success)
                {
                    _taskParserService.ClearCache();
                    AnsiConsole.MarkupLine($"[green]✓ Folder Updated:[/] {newName.EscapeMarkup()}");
                }
                else
                    AnsiConsole.MarkupLine("[red]✗ Error updating folder.[/]");
            }
            catch (ToodledoApiException ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ API Error ({ex.ErrorCode}):[/] {ex.Message.EscapeMarkup()}");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ Error:[/] {ex.Message.EscapeMarkup()}");
            }
        }

        private static async Task DeleteFolder(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                AnsiConsole.MarkupLine("[red]Usage: folder-delete <id1> <id2>...[/]");
                return;
            }

            var identifiers = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (identifiers.Length == 0) return;

            try
            {
                var allFolders = await _folderService.GetFoldersAsync();
                var foldersToDelete = new List<ToodledoFolder>();

                foreach (var id in identifiers)
                {
                    var folder = allFolders.FirstOrDefault(f => f.id.ToString() == id || f.name.Equals(id, StringComparison.OrdinalIgnoreCase));
                    if (folder != null) foldersToDelete.Add(folder);
                    else AnsiConsole.MarkupLine($"[yellow]⚠ Folder not found: {id}[/]");
                }

                if (foldersToDelete.Count == 0) return;

                if (!UIService.ConfirmDeletion("Folder", foldersToDelete.Select(f => f.name).ToList()))
                {
                    AnsiConsole.MarkupLine("[yellow]Delete cancelled.[/]");
                    return;
                }

                bool success = await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("red"))
                    .StartAsync("[red]Deleting folders...[/]", async ctx =>
                    {
                        return await _folderService.DeleteFoldersAsync(foldersToDelete.Select(f => f.id));
                    });

                if (success)
                {
                    _taskParserService.ClearCache();
                    AnsiConsole.MarkupLine($"[green]✓ {foldersToDelete.Count} Folder(s) Deleted![/]");
                }
                else
                    AnsiConsole.MarkupLine("[red]✗ Error deleting one or more folders.[/]");
            }
            catch (ToodledoApiException ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ API Error ({ex.ErrorCode}):[/] {ex.Message.EscapeMarkup()}");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ Error:[/] {ex.Message.EscapeMarkup()}");
            }
        }

        private static async Task ListFolders(string filter = "")
        {
            try
            {
                var folders = await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("cyan"))
                    .StartAsync("[cyan]Loading folders...[/]", async ctx =>
                    {
                        return await _folderService.GetFoldersAsync();
                    });

                if (!string.IsNullOrWhiteSpace(filter))
                {
                    folders = folders.Where(f => f.name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                UIService.DisplayFolders(folders);
            }
            catch (ToodledoApiException ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ API Error ({ex.ErrorCode}):[/] {ex.Message.EscapeMarkup()}");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ Error:[/] {ex.Message.EscapeMarkup()}");
            }
        }

        private static async Task AddLocation(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                AnsiConsole.MarkupLine("[red]Usage: location-add <name>[/]");
                return;
            }

            try
            {
                bool success = await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("cyan"))
                    .StartAsync("[cyan]Adding location...[/]", async ctx =>
                    {
                        return await _locationService.AddLocationAsync(name);
                    });

                if (success)
                    AnsiConsole.MarkupLine($"[green]✓ Location Added:[/] {name.EscapeMarkup()}");
                else
                    AnsiConsole.MarkupLine("[red]✗ Error adding location.[/]");
            }
            catch (ToodledoApiException ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ API Error ({ex.ErrorCode}):[/] {ex.Message.EscapeMarkup()}");
            }
        }

        private static async Task EditLocation(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                AnsiConsole.MarkupLine("[red]Usage: location-edit <id_or_name> <new_name>[/]");
                return;
            }

            var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                AnsiConsole.MarkupLine("[red]Usage: location-edit <id_or_name> <new_name>[/]");
                return;
            }

            string identifier = parts[0];
            string newName = parts[1];

            try
            {
                var locations = await _locationService.GetLocationsAsync();
                var loc = locations.FirstOrDefault(l => l.id.ToString() == identifier || l.name.Equals(identifier, StringComparison.OrdinalIgnoreCase));

                if (loc == null)
                {
                    AnsiConsole.MarkupLine($"[red]✗ Location not found: {identifier}[/]");
                    return;
                }

                bool success = await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("cyan"))
                    .StartAsync("[cyan]Updating location...[/]", async ctx =>
                    {
                        return await _locationService.EditLocationAsync(loc.id, newName);
                    });

                if (success)
                    AnsiConsole.MarkupLine($"[green]✓ Location Updated:[/] {newName.EscapeMarkup()}");
                else
                    AnsiConsole.MarkupLine("[red]✗ Error updating location.[/]");
            }
            catch (ToodledoApiException ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ API Error ({ex.ErrorCode}):[/] {ex.Message.EscapeMarkup()}");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ Error:[/] {ex.Message.EscapeMarkup()}");
            }
        }

        private static async Task DeleteLocation(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                AnsiConsole.MarkupLine("[red]Usage: location-delete <id1> <id2>...[/]");
                return;
            }

            var identifiers = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (identifiers.Length == 0) return;

            try
            {
                var allLocations = await _locationService.GetLocationsAsync();
                var locationsToDelete = new List<ToodledoLocation>();

                foreach (var id in identifiers)
                {
                    var location = allLocations.FirstOrDefault(l => l.id.ToString() == id || l.name.Equals(id, StringComparison.OrdinalIgnoreCase));
                    if (location != null) locationsToDelete.Add(location);
                    else AnsiConsole.MarkupLine($"[yellow]⚠ Location not found: {id}[/]");
                }

                if (locationsToDelete.Count == 0) return;

                if (!UIService.ConfirmDeletion("Location", locationsToDelete.Select(l => l.name).ToList()))
                {
                    AnsiConsole.MarkupLine("[yellow]Delete cancelled.[/]");
                    return;
                }

                bool success = await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("red"))
                    .StartAsync("[red]Deleting locations...[/]", async ctx =>
                    {
                        return await _locationService.DeleteLocationsAsync(locationsToDelete.Select(l => l.id));
                    });

                if (success)
                {
                    _taskParserService.ClearCache();
                    AnsiConsole.MarkupLine($"[green]✓ {locationsToDelete.Count} Location(s) Deleted![/]");
                }
                else
                    AnsiConsole.MarkupLine("[red]✗ Error deleting one or more locations.[/]");
            }
            catch (ToodledoApiException ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ API Error ({ex.ErrorCode}):[/] {ex.Message.EscapeMarkup()}");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ Error:[/] {ex.Message.EscapeMarkup()}");
            }
        }

        private static async Task RunSetup()
        {
            UIService.DisplaySetupWizard();
            var clientId = AnsiConsole.Ask<string>("Enter your [cyan]Client ID[/]:");
            var clientSecret = AnsiConsole.Ask<string>("Enter your [cyan]Client Secret[/]:");

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                AnsiConsole.MarkupLine("[yellow]Setup cancelled. No changes made.[/]");
                return;
            }

            _authService.SetSecrets(clientId, clientSecret);
            _authService.SaveSecrets();
            AnsiConsole.MarkupLine("[green]✓ Credentials saved to auth.txt[/]");
            AnsiConsole.MarkupLine("[yellow]Note: You may need to restart the app for changes to take full effect if tokens are invalid.[/]");

            // Check if user wants to authorize now
            if (AnsiConsole.Confirm("Would you like to authorize in the browser now?"))
            {
                AnsiConsole.MarkupLine("[yellow]1. A browser window should open automatically.[/]");
                AnsiConsole.MarkupLine("[yellow]2. If not, visit http://localhost:5000/ to authorize.[/]");

                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("yellow"))
                    .StartAsync("[yellow]Waiting for authorization...[/]", async ctx =>
                    {
                        await _authService.AuthorizeAsync();
                    });
                
                AnsiConsole.MarkupLine("[green]✓ Success! Connection Verified.[/]");
            }
        }

        private static async Task ListLocations(string filter = "")
        {
            try
            {
                var locations = await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("cyan"))
                    .StartAsync("[cyan]Loading locations...[/]", async ctx =>
                    {
                        return await _locationService.GetLocationsAsync();
                    });

                if (!string.IsNullOrWhiteSpace(filter))
                {
                    locations = locations.Where(l => l.name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                UIService.DisplayLocations(locations);
            }
            catch (ToodledoApiException ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ API Error ({ex.ErrorCode}):[/] {ex.Message.EscapeMarkup()}");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ Error:[/] {ex.Message.EscapeMarkup()}");
            }
        }
    }
}
