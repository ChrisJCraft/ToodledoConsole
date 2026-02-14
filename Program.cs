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
        private static FilterService _filterService;
        private static TaskParserService _taskParserService;
        private static List<string> _commandHistory = new List<string>();
        private static int _historyIndex = -1;
        private static string _currentInput = "";
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
                _taskParserService = new TaskParserService(_taskService);
                _filterService = new FilterService(_taskService, _taskParserService);
                
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
            DisplayHelp();
            
            while (true)
            {
                var input = ReadLineWithHistory("[cyan]Toodledo> [/]");
                
                if (string.IsNullOrWhiteSpace(input)) continue;

                string cleanInput = input.Trim();
                string lowerInput = cleanInput.ToLower();

                if (lowerInput == "exit") 
                {
                    AnsiConsole.MarkupLine("[yellow]Goodbye![/]");
                    break;
                }
                
                if (lowerInput == "help") DisplayHelp();
                else if (lowerInput == "list") await ListTasks();
                else if (lowerInput == "random") await ShowRandom();
                else if (lowerInput.StartsWith("filter ")) await FilterTasks(cleanInput.Substring(7).Trim());
                else if (lowerInput.StartsWith("find ")) await SearchTasks(cleanInput.Substring(5).Trim());
                else if (lowerInput.StartsWith("done ")) await CompleteTask(cleanInput.Substring(5).Trim());
                else if (lowerInput.StartsWith("add ")) await AddTask(cleanInput.Substring(4).Trim());
                else if (lowerInput.StartsWith("edit ")) await EditTask(cleanInput.Substring(5).Trim());
                else if (lowerInput.StartsWith("view ")) await ViewTask(cleanInput.Substring(5).Trim());
                else if (lowerInput.StartsWith("tag ")) await TagTask(cleanInput.Substring(4).Trim());
                else if (lowerInput.StartsWith("note ")) await NoteTask(cleanInput.Substring(5).Trim());
                else AnsiConsole.MarkupLine("[red]Unknown command. Type 'help' for available commands.[/]");
            }
        }

        private static string ReadLineWithHistory(string prompt)
        {
            AnsiConsole.Markup(prompt);
            var input = new StringBuilder();
            _historyIndex = _commandHistory.Count;
            _currentInput = "";

            while (true)
            {
                var key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    var result = input.ToString();
                    if (!string.IsNullOrWhiteSpace(result))
                    {
                        _commandHistory.Add(result);
                    }
                    return result;
                }
                else if (key.Key == ConsoleKey.UpArrow)
                {
                    if (_commandHistory.Count > 0 && _historyIndex > 0)
                    {
                        if (_historyIndex == _commandHistory.Count)
                        {
                            _currentInput = input.ToString();
                        }

                        _historyIndex--;
                        ClearCurrentLine(prompt, input.Length);
                        input.Clear();
                        input.Append(_commandHistory[_historyIndex]);
                        AnsiConsole.Markup(input.ToString().EscapeMarkup());
                    }
                }
                else if (key.Key == ConsoleKey.DownArrow)
                {
                    if (_historyIndex < _commandHistory.Count - 1)
                    {
                        _historyIndex++;
                        ClearCurrentLine(prompt, input.Length);
                        input.Clear();
                        input.Append(_commandHistory[_historyIndex]);
                        AnsiConsole.Markup(input.ToString().EscapeMarkup());
                    }
                    else if (_historyIndex == _commandHistory.Count - 1)
                    {
                        _historyIndex++;
                        ClearCurrentLine(prompt, input.Length);
                        input.Clear();
                        input.Append(_currentInput);
                        AnsiConsole.Markup(input.ToString().EscapeMarkup());
                    }
                }
                else if (key.Key == ConsoleKey.Backspace)
                {
                    if (input.Length > 0)
                    {
                        input.Remove(input.Length - 1, 1);
                        Console.Write("\b \b");
                    }
                }
                else if (key.KeyChar != '\u0000' && !char.IsControl(key.KeyChar))
                {
                    input.Append(key.KeyChar);
                    Console.Write(key.KeyChar);
                }
            }
        }

        private static void ClearCurrentLine(string prompt, int inputLength)
        {
            // Go back to the start of the prompt
            // Spectres Console doesn't have a simple way to clear the current line without knowing prompt length
            // But we are in a simple console here.
            // Actually simpler approach: backspace the input and clear it
            for (int i = 0; i < inputLength; i++)
            {
                Console.Write("\b \b");
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
                if (criteria.Priority.HasValue) attributes.Add($"[yellow]Priority:[/] {GetPriorityName(criteria.Priority.Value)}");
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
                
                DisplayTasks(_cachedTasks);
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

                DisplayFilteredTasks(filteredResults, criteria, totalTasksCount);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ Filter error:[/] {ex.Message.EscapeMarkup()}");
            }
        }

        private static void DisplayFilteredTasks(List<ToodledoTask> tasks, FilterCriteria criteria, int totalCount)
        {
            // 1. The Header: Active Filters Panel
            var filterInfo = new List<string>();
            if (criteria.Priority.HasValue) filterInfo.Add($"[yellow]Priority:[/] {GetPriorityName(criteria.Priority.Value)}");
            if (!string.IsNullOrEmpty(criteria.FolderName)) filterInfo.Add($"[green]Folder:[/] {criteria.FolderName}");
            if (!string.IsNullOrEmpty(criteria.ContextName)) filterInfo.Add($"[blue]Context:[/] @{criteria.ContextName}");
            if (criteria.Starred.HasValue) filterInfo.Add($"[gold1]Starred:[/] {(criteria.Starred == 1 ? "Yes" : "No")}");
            if (!string.IsNullOrEmpty(criteria.DueDateShortcut)) filterInfo.Add($"[purple]Due:[/] {criteria.DueDateShortcut}");
            if (!string.IsNullOrEmpty(criteria.SearchTerm)) filterInfo.Add($"[silver]Search:[/] \"{criteria.SearchTerm}\"");

            var filterPanel = new Panel(string.Join(" [dim]|[/] ", filterInfo))
            {
                Header = new PanelHeader("[yellow]Active Filters[/]"),
                Border = BoxBorder.Rounded,
                BorderStyle = Style.Parse("yellow"),
                Padding = new Padding(1, 0)
            };

            AnsiConsole.WriteLine();
            AnsiConsole.Write(filterPanel);

            if (tasks.Count == 0)
            {
                // 3. The "No Match" Blessing
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[orange1]No tasks match those filters. Try being less picky![/]");
                AnsiConsole.WriteLine();
                return;
            }

            // 2. The Table
            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.BorderStyle(Style.Parse("cyan"));
            
            table.AddColumn(new TableColumn("[cyan]ID[/]").Centered());
            table.AddColumn(new TableColumn("[cyan]P[/]").Centered());
            table.AddColumn(new TableColumn("[cyan]Task[/]").LeftAligned());
            table.AddColumn(new TableColumn("[cyan]Tags[/]").LeftAligned());

            foreach (var task in tasks)
            {
                table.AddRow(
                    $"[dim]{task.id}[/]",
                    GetPriorityMarkup(task.priority),
                    $"[white]{task.title}[/]",
                    $"[silver dim]{task.tag}[/]"
                );
            }

            AnsiConsole.Write(table);

            // 2. The Live Counter
            AnsiConsole.MarkupLine($"[dim]Showing {tasks.Count} of {totalCount} tasks[/]");
            AnsiConsole.WriteLine();
        }

        private static string GetPriorityName(int priority)
        {
            return priority switch
            {
                3 => "Top",
                2 => "High",
                1 => "Medium",
                0 => "Low",
                -1 => "Negative",
                _ => "Unknown"
            };
        }

        private static string GetPriorityMarkup(int priority)
        {
            return priority switch
            {
                3 => "[red]![/]",
                2 => "[orange1]![/]",
                1 => "[yellow]![/]",
                0 => "[dim]-[/]",
                -1 => "[dim]?[/]",
                _ => "[dim] [/]"
            };
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
                DisplayTasks(results);
            }
        }

        private static void DisplayTasks(List<ToodledoTask> tasks)
        {
            if (tasks.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No tasks found.[/]");
                return;
            }

            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.BorderStyle(Style.Parse("cyan"));
            
            table.AddColumn(new TableColumn("[cyan]ID[/]").Centered());
            table.AddColumn(new TableColumn("[cyan]Task[/]").LeftAligned());
            table.AddColumn(new TableColumn("[cyan]Tags[/]").LeftAligned());

            foreach (var task in tasks)
            {
                table.AddRow(
                    $"[dim]{task.id}[/]",
                    $"[white]{task.title}[/]",
                    $"[silver dim]{task.tag}[/]"
                );
            }

            AnsiConsole.WriteLine();
            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]Total Tasks: {tasks.Count}[/]");
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

            var folders = await _taskService.GetFoldersAsync();
            var contexts = await _taskService.GetContextsAsync();
            
            DisplayTaskDetail(task, folders, contexts, "Editing Task", "yellow");

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

            var folders = await _taskService.GetFoldersAsync();
            var contexts = await _taskService.GetContextsAsync();

            DisplayTaskDetail(task, folders, contexts, "Task Details", "cyan");
        }

        private static void DisplayTaskDetail(ToodledoTask task, List<ToodledoFolder> folders, List<ToodledoContext> contexts, string title, string color)
        {
            var table = new Table().NoBorder().HideHeaders();
            table.AddColumn("K"); table.AddColumn("V");
            table.AddRow("[dim]ID:[/]", $"[dim]{task.id}[/]");
            table.AddRow("[dim]Title:[/]", $"[white]{task.title.EscapeMarkup()}[/]");
            table.AddRow("[dim]Priority:[/]", GetPriorityMarkup(task.priority));
            
            if (task.folder != 0) 
                table.AddRow("[dim]Folder:[/]", $"[green]{folders.FirstOrDefault(f => f.id == task.folder)?.name ?? "Unknown"}[/]");
            if (task.context != 0) 
                table.AddRow("[dim]Context:[/]", $"[blue]@{contexts.FirstOrDefault(c => c.id == task.context)?.name ?? "Unknown"}[/]");
            if (!string.IsNullOrEmpty(task.tag))
                table.AddRow("[dim]Tags:[/]", $"[silver dim]{task.tag}[/]");
            if (!string.IsNullOrEmpty(task.note))
                table.AddRow("[dim]Note:[/]", $"[silver]{task.note.EscapeMarkup()}[/]");

            var panel = new Panel(table)
            {
                Header = new PanelHeader($"[{color}]{title}[/]"),
                Border = BoxBorder.Rounded,
                BorderStyle = Style.Parse(color),
                Padding = new Padding(1, 0)
            };
            AnsiConsole.WriteLine();
            AnsiConsole.Write(panel);
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
        
        private static void DisplayHelp()
        {
            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.BorderStyle(Style.Parse("cyan"));
            table.HideHeaders();
            
            table.AddColumn(new TableColumn("").Width(20));
            table.AddColumn(new TableColumn(""));

            table.AddRow("[cyan]list[/]", "[dim]Display all active tasks[/]");
    table.AddRow("[cyan]add[/] [white]<text>[/]", "[dim]Create task (ex: add Buy milk p:3 @Store !:today)[/]");
    table.AddRow("[cyan]edit[/] [white]<id>[/]", "[dim]Edit task using shadow prompt shorthand[/]");
    table.AddRow("[cyan]view[/] [white]<id>[/]", "[dim]View full task details (including notes)[/]");
    table.AddRow("[cyan]tag[/] [white]<id> <tags>[/]", "[dim]Quickly update tags for a task[/]");
    table.AddRow("[cyan]note[/] [white]<id> <text>[/]", "[dim]Quickly update note for a task[/]");
    table.AddRow("[cyan]done[/] [white]<id>[/]", "[dim]Mark a task as completed[/]");
    table.AddRow("[cyan]find[/] [white]<text>[/]", "[dim]Search tasks by keyword[/]");
    table.AddRow("[cyan]filter[/] [white][[k:v]][/]", "[dim]Power-user filters (p:2, f:Inbox, @Work...)[/]");
    table.AddRow("[cyan]random[/]", "[dim]Show a random task[/]");
    table.AddRow("[cyan]help[/]", "[dim]Show this help message[/]");
    table.AddRow("[cyan]exit[/]", "[dim]Exit the application[/]");

            AnsiConsole.WriteLine();
            var panel = new Panel(table)
            {
                Header = new PanelHeader("[yellow]Available Commands[/]", Justify.Left),
                Border = BoxBorder.Rounded,
                BorderStyle = Style.Parse("yellow")
            };
            
            AnsiConsole.Write(panel);
            AnsiConsole.WriteLine();
        }
    }
}