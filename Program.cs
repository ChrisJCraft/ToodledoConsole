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

        static async Task Main(string[] args)
        {
            AnsiConsole.Clear();
            
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
                    AnsiConsole.MarkupLine("[yellow]Setup: Toodledo API credentials not found.[/]");
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
                else if (lowerInput == "contexts" || lowerInput.StartsWith("contexts ")) await ListContexts(cleanInput.Length > 8 ? cleanInput.Substring(9).Trim() : "");
                else if (lowerInput == "random") await ShowRandom();
                else if (lowerInput.StartsWith("random ")) await ShowRandom(cleanInput.Substring(7).Trim());
                else if (lowerInput.StartsWith("filter ")) await FilterTasks(cleanInput.Substring(7).Trim());
                else if (lowerInput.StartsWith("find ")) await SearchTasks(cleanInput.Substring(5).Trim());
                else if (lowerInput.StartsWith("done ")) await CompleteTask(cleanInput.Substring(5).Trim());
                else if (lowerInput.StartsWith("add ")) await AddTask(cleanInput.Substring(4).Trim());
                else if (lowerInput.StartsWith("edit ")) await EditTask(cleanInput.Substring(5).Trim());
                else if (lowerInput.StartsWith("view ")) await ViewTask(cleanInput.Substring(5).Trim());
                else if (lowerInput.StartsWith("tag ")) await TagTask(cleanInput.Substring(4).Trim());
                else if (lowerInput.StartsWith("note ")) await NoteTask(cleanInput.Substring(5).Trim());
                else if (lowerInput.StartsWith("add-context ")) await AddContext(cleanInput.Substring(12).Trim());
                else if (lowerInput.StartsWith("edit-context ")) await EditContext(cleanInput.Substring(13).Trim());
                else if (lowerInput.StartsWith("delete-context ")) await DeleteContext(cleanInput.Substring(15).Trim());
                else if (lowerInput == "folders" || lowerInput.StartsWith("folders ")) await ListFolders(cleanInput.Length > 7 ? cleanInput.Substring(8).Trim() : "");
                else if (lowerInput.StartsWith("add-folder ")) await AddFolder(cleanInput.Substring(11).Trim());
                else if (lowerInput.StartsWith("edit-folder ")) await EditFolder(cleanInput.Substring(12).Trim());
                else if (lowerInput.StartsWith("delete-folder ")) await DeleteFolder(cleanInput.Substring(14).Trim());
                else if (lowerInput == "locations" || lowerInput.StartsWith("locations ")) await ListLocations(cleanInput.Length > 9 ? cleanInput.Substring(10).Trim() : "");
                else if (lowerInput.StartsWith("add-location ")) await AddLocation(cleanInput.Substring(13).Trim());
                else if (lowerInput.StartsWith("edit-location ")) await EditLocation(cleanInput.Substring(14).Trim());
                else if (lowerInput.StartsWith("delete-location ")) await DeleteLocation(cleanInput.Substring(16).Trim());
                else if (lowerInput.StartsWith("delete ")) await DeleteTask(cleanInput.Substring(7).Trim());
                else if (lowerInput == "dev-import") await ImportReleaseTasks();
                else if (lowerInput == "dev-cleanup") await CleanupReleaseTasks();
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

        private static async Task CompleteTask(string id)
        {
            bool success = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("green"))
                .StartAsync($"[green]Completing task...[/]", async ctx =>
                {
                    return await _taskService.CompleteTaskAsync(id);
                });
            
            if (success) 
            {
                AnsiConsole.MarkupLine("[green]✓ Task Completed![/]");
                _cachedTasks.RemoveAll(t => t.id == id);
                _seenTaskIds.Remove(id);
                SaveRandomState();
                AnsiConsole.MarkupLine($"[cyan]{_cachedTasks.Count} Tasks Remaining.[/]");
            }
            else 
            {
                AnsiConsole.MarkupLine("[red]✗ Error completing task.[/]");
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

            var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                AnsiConsole.MarkupLine("[red]Usage: tag <id> <tags>[/]");
                return;
            }

            string id = parts[0];
            string tags = parts[1];

            var criteria = new FilterCriteria { Tag = tags };

            bool success = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("cyan"))
                .StartAsync("[cyan]Updating tags...[/]", async ctx =>
                {
                    return await _taskService.UpdateTaskAsync(id, criteria);
                });

            if (success)
            {
                AnsiConsole.MarkupLine("[green]✓ Tags Updated![/]");
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

            var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                AnsiConsole.MarkupLine("[red]Usage: note <id> <text>[/]");
                return;
            }

            string id = parts[0];
            string note = parts[1];

            var criteria = new FilterCriteria { Note = note };

            bool success = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("cyan"))
                .StartAsync("[cyan]Updating note...[/]", async ctx =>
                {
                    return await _taskService.UpdateTaskAsync(id, criteria);
                });

            if (success)
            {
                AnsiConsole.MarkupLine("[green]✓ Note Updated![/]");
                await ListTasks();
            }
            else
            {
                AnsiConsole.MarkupLine("[red]✗ Error updating note.[/]");
            }
        }

        private static async Task DeleteTask(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            var task = await _taskService.GetTaskAsync(id);
            if (task == null)
            {
                AnsiConsole.MarkupLine("[red]✗ Task not found.[/]");
                return;
            }

            AnsiConsole.MarkupLine($"[yellow]Are you sure you want to delete:[/] [white]{task.title}[/]? [dim](y/n)[/]");
            var key = Console.ReadKey(true);
            if (key.Key != ConsoleKey.Y)
            {
                AnsiConsole.MarkupLine("[yellow]Delete cancelled.[/]");
                return;
            }

            bool success = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("red"))
                .StartAsync("[red]Deleting task...[/]", async ctx =>
                {
                    return await _taskService.DeleteTaskAsync(id);
                });

            if (success)
            {
                AnsiConsole.MarkupLine("[green]✓ Task Deleted![/]");
                await ListTasks();
            }
            else
            {
                AnsiConsole.MarkupLine("[red]✗ Error deleting task.[/]");
            }
        }

        private static async Task AddContext(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                AnsiConsole.MarkupLine("[red]Usage: add-context <name>[/]");
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
                AnsiConsole.MarkupLine("[red]Usage: edit-context <id_or_name> <new_name>[/]");
                return;
            }

            var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                AnsiConsole.MarkupLine("[red]Usage: edit-context <id_or_name> <new_name>[/]");
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

        private static async Task DeleteContext(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                AnsiConsole.MarkupLine("[red]Usage: delete-context <id_or_name>[/]");
                return;
            }

            try
            {
                var contexts = await _contextService.GetContextsAsync();
                var context = contexts.FirstOrDefault(c => c.id.ToString() == identifier || c.name.Equals(identifier, StringComparison.OrdinalIgnoreCase));

                if (context == null)
                {
                    AnsiConsole.MarkupLine($"[red]✗ Context not found: {identifier}[/]");
                    return;
                }

                AnsiConsole.MarkupLine($"[yellow]Are you sure you want to delete context:[/] [white]{context.name}[/]? [dim](y/n)[/]");
                var key = Console.ReadKey(true);
                if (key.Key != ConsoleKey.Y)
                {
                    AnsiConsole.MarkupLine("[yellow]Delete cancelled.[/]");
                    return;
                }

                bool success = await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("red"))
                    .StartAsync("[red]Deleting context...[/]", async ctx =>
                    {
                        return await _contextService.DeleteContextAsync(context.id);
                    });

                if (success)
                {
                    _taskParserService.ClearCache();
                    AnsiConsole.MarkupLine("[green]✓ Context Deleted![/]");
                }
                else
                    AnsiConsole.MarkupLine("[red]✗ Error deleting context.[/]");
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
                AnsiConsole.MarkupLine("[red]Usage: add-folder <name>[/]");
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
                AnsiConsole.MarkupLine("[red]Usage: edit-folder <id_or_name> <new_name>[/]");
                return;
            }

            var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                AnsiConsole.MarkupLine("[red]Usage: edit-folder <id_or_name> <new_name>[/]");
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

        private static async Task DeleteFolder(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                AnsiConsole.MarkupLine("[red]Usage: delete-folder <id_or_name>[/]");
                return;
            }

            try
            {
                var folders = await _folderService.GetFoldersAsync();
                var folder = folders.FirstOrDefault(f => f.id.ToString() == identifier || f.name.Equals(identifier, StringComparison.OrdinalIgnoreCase));

                if (folder == null)
                {
                    AnsiConsole.MarkupLine($"[red]✗ Folder not found: {identifier}[/]");
                    return;
                }

                AnsiConsole.MarkupLine($"[yellow]Are you sure you want to delete folder:[/] [white]{folder.name}[/]? [dim](y/n)[/]");
                var key = Console.ReadKey(true);
                if (key.Key != ConsoleKey.Y)
                {
                    AnsiConsole.MarkupLine("[yellow]Delete cancelled.[/]");
                    return;
                }

                bool success = await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("red"))
                    .StartAsync("[red]Deleting folder...[/]", async ctx =>
                    {
                        return await _folderService.DeleteFolderAsync(folder.id);
                    });

                if (success)
                {
                    _taskParserService.ClearCache();
                    AnsiConsole.MarkupLine("[green]✓ Folder Deleted![/]");
                }
                else
                    AnsiConsole.MarkupLine("[red]✗ Error deleting folder.[/]");
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
                AnsiConsole.MarkupLine("[red]Usage: add-location <name>[/]");
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
                AnsiConsole.MarkupLine("[red]Usage: edit-location <id_or_name> <new_name>[/]");
                return;
            }

            var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                AnsiConsole.MarkupLine("[red]Usage: edit-location <id_or_name> <new_name>[/]");
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

        private static async Task DeleteLocation(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                AnsiConsole.MarkupLine("[red]Usage: delete-location <id_or_name>[/]");
                return;
            }

            try
            {
                var locations = await _locationService.GetLocationsAsync();
                var loc = locations.FirstOrDefault(l => l.id.ToString() == identifier || l.name.Equals(identifier, StringComparison.OrdinalIgnoreCase));

                if (loc == null)
                {
                    AnsiConsole.MarkupLine($"[red]✗ Location not found: {identifier}[/]");
                    return;
                }

                AnsiConsole.MarkupLine($"[yellow]Are you sure you want to delete location:[/] [white]{loc.name}[/]? [dim](y/n)[/]");
                var key = Console.ReadKey(true);
                if (key.Key != ConsoleKey.Y)
                {
                    AnsiConsole.MarkupLine("[yellow]Delete cancelled.[/]");
                    return;
                }

                bool success = await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("red"))
                    .StartAsync("[red]Deleting location...[/]", async ctx =>
                    {
                        return await _locationService.DeleteLocationAsync(loc.id);
                    });

                if (success)
                    AnsiConsole.MarkupLine("[green]✓ Location Deleted![/]");
                else
                    AnsiConsole.MarkupLine("[red]✗ Error deleting location.[/]");
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
        private static async Task ImportReleaseTasks()
        {
            try
            {
                AnsiConsole.MarkupLine("[cyan]🔍 Step 1: Checking contexts...[/]");
                var contexts = await _contextService.GetContextsAsync();
                var releaseContext = contexts.FirstOrDefault(c => c.name.Equals("Release", StringComparison.OrdinalIgnoreCase));

                if (releaseContext == null)
                {
                    AnsiConsole.MarkupLine("[yellow]⚠️ 'Release' context not found. Creating it...[/]");
                    bool created = await _contextService.AddContextAsync("Release");
                    if (!created)
                    {
                        AnsiConsole.MarkupLine("[red]✗ Failed to create 'Release' context via API.[/]");
                        return;
                    }
                    
                    AnsiConsole.MarkupLine("[green]✓ Context created. Refreshing...[/]");
                    _taskParserService.ClearCache();
                    contexts = await _contextService.GetContextsAsync();
                    releaseContext = contexts.FirstOrDefault(c => c.name.Equals("Release", StringComparison.OrdinalIgnoreCase));
                }

                if (releaseContext == null)
                {
                    AnsiConsole.MarkupLine("[red]✗ Critical Error: 'Release' context still missing after creation.[/]");
                    return;
                }

                AnsiConsole.MarkupLine($"[green]✓ Context Verified: [white]{releaseContext.name}[/] (ID: {releaseContext.id})[/]");
                
                AnsiConsole.MarkupLine("[cyan]🔍 Step 2: Checking existing tasks in @Release...[/]");
                var existingTasks = await _taskService.GetTasksAsync($"&context={releaseContext.id}");
                
                var tasksToAdd = new[]
                {
                    "[CLI] Version Bump: Update Program.cs and .csproj to 2.0.0",
                    "[UX] Multi-ID Support: Allow done id1 id2 id3 to clear multiple tasks",
                    "[Feature] Star Toggle: Add dedicated star <id> and unstar <id> commands",
                    "[Build] Zero Warning Check: Confirm dotnet build is 100% clean",
                    "[Dashboard] Empty State Audit: Ensure stats handles empty accounts",
                    "[Dashboard] Top Facts Logic: Verify Oldest Task handles null/edge cases",
                    "[Docs] Command Table Sync: Audit README.md against actual help output",
                    "[Security] Auth Protection: Verify .gitignore and security in README",
                    "[CI] GitHub Action Run: Confirm build.yml passes on MacOS",
                    "[Repo] Metadata Sweep: Set GitHub topics and description",
                    "[Dogfood] Final Verification: Mark all tasks complete using the tool"
                };

                int imported = 0;
                int skipped = 0;

                foreach (var taskTitle in tasksToAdd)
                {
                    if (existingTasks.Any(et => et.title.Equals(taskTitle, StringComparison.OrdinalIgnoreCase)))
                    {
                        skipped++;
                        continue;
                    }

                    var criteria = new FilterCriteria 
                    { 
                        SearchTerm = taskTitle,
                        ContextId = releaseContext.id,
                        Priority = 2 
                    };

                    await AnsiConsole.Status()
                        .Spinner(Spinner.Known.Dots)
                        .SpinnerStyle(Style.Parse("cyan"))
                        .StartAsync($"[cyan]Importing:[/] {taskTitle.EscapeMarkup()}...", async ctx =>
                        {
                            await _taskService.AddTaskAsync(criteria);
                        });
                    imported++;
                }

                AnsiConsole.MarkupLine($"[green]✓ Done! {imported} tasks imported, {skipped} skipped (already exists).[/]");
                AnsiConsole.MarkupLine("[yellow]Tip: Run 'filter @Release' to see your backlog.[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ Import failed:[/] {ex.Message.EscapeMarkup()}");
            }
        }

        private static async Task CleanupReleaseTasks()
        {
            try
            {
                AnsiConsole.MarkupLine("[yellow]🔍 Searching for 'Release' context...[/]");
                var contexts = await _contextService.GetContextsAsync();
                var rc = contexts.FirstOrDefault(c => c.name.Equals("Release", StringComparison.OrdinalIgnoreCase));
                
                if (rc == null)
                {
                    AnsiConsole.MarkupLine("[red]✗ Context 'Release' not found. Nothing to clean.[/]");
                    return;
                }

                AnsiConsole.MarkupLine("[cyan]🔍 Fetching all tasks in @Release context...[/]");
                var tasks = await _taskService.GetTasksAsync($"&context={rc.id}");
                
                if (tasks.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]No tasks found in @Release context.[/]");
                    return;
                }

                AnsiConsole.MarkupLine($"[yellow]⚠️ WARNING: This will delete ALL {tasks.Count} tasks in the '@Release' context.[/]");
                AnsiConsole.MarkupLine("[white]Are you sure? (y/n)[/]");
                var key = Console.ReadKey(true);
                if (key.Key != ConsoleKey.Y)
                {
                    AnsiConsole.MarkupLine("[yellow]Cleanup cancelled.[/]");
                    return;
                }

                foreach (var task in tasks)
                {
                    await AnsiConsole.Status()
                        .Spinner(Spinner.Known.Dots)
                        .SpinnerStyle(Style.Parse("red"))
                        .StartAsync($"[red]Deleting:[/] {task.title.EscapeMarkup()}...", async ctx =>
                        {
                            await _taskService.DeleteTaskAsync(task.id);
                        });
                }

                AnsiConsole.MarkupLine("[green]✓ Cleanup complete. Context @Release is now empty.[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ Cleanup failed:[/] {ex.Message.EscapeMarkup()}");
            }
        }
    }
}
