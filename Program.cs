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
        private static AuthService _authService;
        private static TaskService _taskService;
        private static ContextService _contextService;
        private static FolderService _folderService;
        private static FilterService _filterService;
        private static TaskParserService _taskParserService;
        private static InputService _inputService;
        
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
            
            var versionText = new Markup("[dim]v1.5.1[/]");
            AnsiConsole.Write(versionText);
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine();

            try
            {
                _authService = new AuthService(_httpClient, _jsonOptions);
                _taskService = new TaskService(_httpClient, _authService, _jsonOptions);
                _contextService = new ContextService(_httpClient, _authService, _jsonOptions);
                _folderService = new FolderService(_httpClient, _authService, _jsonOptions);
                _taskParserService = new TaskParserService(_taskService, _folderService, _contextService);
                _filterService = new FilterService(_taskService, _taskParserService);
                _inputService = new InputService();
                
                if (!_authService.LoadSecrets())
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] {AuthService.AuthFile} not found or invalid.");
                    AnsiConsole.MarkupLine("[yellow]Please create 'auth.txt' with Client ID on line 1 and Client Secret on line 2.[/]");
                    return;
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
                else if (lowerInput == "contexts") await ListContexts();
                else if (lowerInput == "random") await ShowRandom();
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
                else if (lowerInput == "folders") await ListFolders();
                else if (lowerInput.StartsWith("add-folder ")) await AddFolder(cleanInput.Substring(11).Trim());
                else if (lowerInput.StartsWith("edit-folder ")) await EditFolder(cleanInput.Substring(12).Trim());
                else if (lowerInput.StartsWith("delete-folder ")) await DeleteFolder(cleanInput.Substring(14).Trim());
                else if (lowerInput.StartsWith("delete ")) await DeleteTask(cleanInput.Substring(7).Trim());
                else AnsiConsole.MarkupLine("[red]Unknown command. Type 'help' for available commands.[/]");
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


        private static async Task ShowRandom()
        {
            if (_cachedTasks.Count == 0) await ListTasks();
            if (_cachedTasks.Count == 0) return;

            LoadRandomState();
            
            // Get candidates (tasks not yet seen)
            var candidates = _cachedTasks.Where(t => !_seenTaskIds.Contains(t.id)).ToList();
            
            // If all tasks have been seen, reset the cycle
            if (candidates.Count == 0)
            {
                _seenTaskIds.Clear();
                candidates = _cachedTasks.ToList();
                AnsiConsole.MarkupLine("[yellow]🔄 CYCLE COMPLETE - Starting fresh random order[/]");
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
                Padding = new Padding(2, 1)
            };
            
            AnsiConsole.WriteLine();
            AnsiConsole.Write(panel);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]Progress: {_seenTaskIds.Count}/{_cachedTasks.Count} tasks shown this cycle[/]");
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

            bool success = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("cyan"))
                .StartAsync("[cyan]Adding context...[/]", async ctx =>
                {
                    return await _contextService.AddContextAsync(name);
                });

            if (success)
                AnsiConsole.MarkupLine($"[green]✓ Context Added:[/] {name.EscapeMarkup()}");
            else
                AnsiConsole.MarkupLine("[red]✗ Error adding context.[/]");
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
                AnsiConsole.MarkupLine($"[green]✓ Context Updated:[/] {newName.EscapeMarkup()}");
            else
                AnsiConsole.MarkupLine("[red]✗ Error updating context.[/]");
        }

        private static async Task DeleteContext(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                AnsiConsole.MarkupLine("[red]Usage: delete-context <id_or_name>[/]");
                return;
            }

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
                AnsiConsole.MarkupLine("[green]✓ Context Deleted![/]");
            else
                AnsiConsole.MarkupLine("[red]✗ Error deleting context.[/]");
        }

        private static async Task ListContexts()
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

                UIService.DisplayContexts(contexts);
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

            bool success = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("cyan"))
                .StartAsync("[cyan]Adding folder...[/]", async ctx =>
                {
                    return await _folderService.AddFolderAsync(name);
                });

            if (success)
                AnsiConsole.MarkupLine($"[green]✓ Folder Added:[/] {name.EscapeMarkup()}");
            else
                AnsiConsole.MarkupLine("[red]✗ Error adding folder.[/]");
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
                AnsiConsole.MarkupLine($"[green]✓ Folder Updated:[/] {newName.EscapeMarkup()}");
            else
                AnsiConsole.MarkupLine("[red]✗ Error updating folder.[/]");
        }

        private static async Task DeleteFolder(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                AnsiConsole.MarkupLine("[red]Usage: delete-folder <id_or_name>[/]");
                return;
            }

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
                AnsiConsole.MarkupLine("[green]✓ Folder Deleted![/]");
            else
                AnsiConsole.MarkupLine("[red]✗ Error deleting folder.[/]");
        }

        private static async Task ListFolders()
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

                UIService.DisplayFolders(folders);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ Error:[/] {ex.Message.EscapeMarkup()}");
            }
        }
    }
}
