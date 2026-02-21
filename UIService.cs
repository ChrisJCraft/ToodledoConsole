using System;
using System.Collections.Generic;
using System.Linq;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace ToodledoConsole
{
    public static class UIService
    {
        public static void DisplayTasks(List<ToodledoTask> tasks)
        {
            if (tasks.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No tasks found.[/]");
                return;
            }

            var table = new Table().Width(100);
            table.Border(TableBorder.Rounded);
            table.BorderStyle(Style.Parse("cyan"));

            table.AddColumn(new TableColumn("[cyan]ID[/]").Centered());
            table.AddColumn(new TableColumn("[gold1]★[/]").Centered().Width(3));
            table.AddColumn(new TableColumn("[cyan]Task[/]").LeftAligned());
            table.AddColumn(new TableColumn("[cyan]Tags[/]").LeftAligned());

            foreach (var task in tasks)
            {
                table.AddRow(
                    $"[dim]{task.id}[/]",
                    GetStarMarkup(task.star),
                    $"[white]{task.title.EscapeMarkup()}[/]",
                    $"[silver dim]{task.tag.EscapeMarkup()}[/]"
                );
            }

            AnsiConsole.WriteLine();
            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]Total Tasks: {tasks.Count}[/]");
        }

        public static void DisplayFilteredTasks(List<ToodledoTask> tasks, FilterCriteria criteria, int totalCount)
        {
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
                Padding = new Padding(1, 0),
                Width = 100
            };

            AnsiConsole.WriteLine();
            AnsiConsole.Write(filterPanel);

            if (tasks.Count == 0)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[orange1]No tasks match those filters. Try being less picky![/]");
                AnsiConsole.WriteLine();
                return;
            }

            var table = new Table().Width(100);
            table.Border(TableBorder.Rounded);
            table.BorderStyle(Style.Parse("cyan"));

            table.AddColumn(new TableColumn("[cyan]ID[/]").Centered());
            table.AddColumn(new TableColumn("[cyan]P[/]").Centered());
            table.AddColumn(new TableColumn("[gold1]★[/]").Centered().Width(3));
            table.AddColumn(new TableColumn("[cyan]Task[/]").LeftAligned());
            table.AddColumn(new TableColumn("[cyan]Tags[/]").LeftAligned());

            foreach (var task in tasks)
            {
                table.AddRow(
                    $"[dim]{task.id}[/]",
                    GetPriorityMarkup(task.priority),
                    GetStarMarkup(task.star),
                    $"[white]{task.title.EscapeMarkup()}[/]",
                    $"[silver dim]{task.tag.EscapeMarkup()}[/]"
                );
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine($"[dim]Showing {tasks.Count} of {totalCount} tasks[/]");
            AnsiConsole.WriteLine();
        }

        public static void DisplayTaskDetail(ToodledoTask task, List<ToodledoFolder> folders, List<ToodledoContext> contexts, string title, string color)
        {
            var table = new Table().NoBorder().HideHeaders();
            table.AddColumn("K"); table.AddColumn("V");
            table.AddRow("[dim]ID:[/]", $"[dim]{task.id}[/]");
            table.AddRow("[dim]Title:[/]", $"[white]{task.title.EscapeMarkup()}[/]");
            table.AddRow("[dim]Priority:[/]", GetPriorityMarkup(task.priority));
            if (task.star == 1)
                table.AddRow("[dim]Starred:[/]", "[gold1]★ Yes[/]");

            if (task.folder != 0)
                table.AddRow("[dim]Folder:[/]", $"[green]{folders.FirstOrDefault(f => f.id == task.folder)?.name.EscapeMarkup() ?? "Unknown"}[/]");
            if (task.context != 0)
                table.AddRow("[dim]Context:[/]", $"[blue]@{contexts.FirstOrDefault(c => c.id == task.context)?.name.EscapeMarkup() ?? "Unknown"}[/]");
            if (!string.IsNullOrEmpty(task.tag))
                table.AddRow("[dim]Tags:[/]", $"[silver dim]{task.tag.EscapeMarkup()}[/]");
            if (!string.IsNullOrEmpty(task.note))
                table.AddRow("[dim]Note:[/]", $"[silver]{task.note.EscapeMarkup()}[/]");

            var panel = new Panel(table)
            {
                Header = new PanelHeader($"[{color}]{title}[/]"),
                Border = BoxBorder.Rounded,
                BorderStyle = Style.Parse(color),
                Padding = new Padding(1, 0),
                Width = 100
            };
            AnsiConsole.WriteLine();
            AnsiConsole.Write(panel);
        }

        public static void DisplayStats(List<ToodledoTask> tasks, List<ToodledoFolder> folders, List<ToodledoContext> contexts, List<ToodledoLocation> locations, int completedCount)
        {
            if (tasks.Count == 0 && completedCount == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No tasks to analyze.[/]");
                return;
            }

            // --- Progress Bar ---
            int totalTasks = tasks.Count + completedCount;
            double progressPercent = totalTasks > 0 ? (double)completedCount / totalTasks * 100 : 0;

            // --- Priority Breakdown ---
            var priorityGroups = tasks.GroupBy(t => t.priority)
                .OrderByDescending(g => g.Key);

            var priorityChart = new BreakdownChart().Width(100);
            foreach (var group in priorityGroups)
            {
                var color = group.Key switch
                {
                    3 => Color.Red,
                    2 => Color.Orange1,
                    1 => Color.Yellow,
                    0 => Color.Grey,
                    -1 => Color.Blue,
                    _ => Color.Silver
                };
                priorityChart.AddItem(GetPriorityName(group.Key), group.Count(), color);
            }

            // --- Folder Breakdown ---
            var folderChart = new BarChart()
                .Width(100)
                .Label("[green]Tasks by Folder[/]")
                .CenterLabel();

            var folderGroups = tasks.GroupBy(t => t.folder)
                .OrderByDescending(g => g.Count())
                .Take(5);

            int fIdx = 0;
            var folderColors = new[] { Color.Aquamarine1, Color.Green, Color.SeaGreen1, Color.DarkGreen, Color.Lime };
            foreach (var group in folderGroups)
            {
                var folderName = group.Key == 0 ? "No Folder" : folders.FirstOrDefault(f => f.id == group.Key)?.name ?? "Unknown";
                folderChart.AddItem(folderName, group.Count(), folderColors[fIdx % folderColors.Length]);
                fIdx++;
            }

            // --- Context Breakdown ---
            var contextChart = new BarChart()
                .Width(100)
                .Label("[blue]Tasks by Context[/]")
                .CenterLabel();

            var contextGroups = tasks.GroupBy(t => t.context)
                .OrderByDescending(g => g.Count())
                .Take(5);

            int cIdx = 0;
            var contextColors = new[] { Color.CadetBlue, Color.Blue, Color.SkyBlue1, Color.DeepSkyBlue1, Color.DodgerBlue1 };
            foreach (var group in contextGroups)
            {
                var contextName = group.Key == 0 ? "No Context" : contexts.FirstOrDefault(c => c.id == group.Key)?.name ?? "Unknown";
                contextChart.AddItem($"@{contextName}", group.Count(), contextColors[cIdx % contextColors.Length]);
                cIdx++;
            }

            // --- Due Date Summary ---
            var now = DateTime.UtcNow.Date;
            long todayUnix = new DateTimeOffset(now).ToUnixTimeSeconds();
            var tomorrow = now.AddDays(1);
            var week = now.AddDays(7);

            var overdueCount = tasks.Count(t => t.duedate > 0 && t.duedate < todayUnix && !IsSameDay(t.duedate, now));
            var todayCount = tasks.Count(t => t.duedate > 0 && IsSameDay(t.duedate, now));
            var tomorrowCount = tasks.Count(t => t.duedate > 0 && IsSameDay(t.duedate, tomorrow));
            var weekCount = tasks.Count(t => t.duedate > 0 && t.duedate > todayUnix && t.duedate <= new DateTimeOffset(week).ToUnixTimeSeconds());
            var noDueCount = tasks.Count(t => t.duedate == 0);

            var dueTable = new Table().NoBorder().HideHeaders().Width(100);
            dueTable.AddColumn("Label"); dueTable.AddColumn("Value");
            dueTable.AddRow("[red]Overdue[/]", overdueCount.ToString());
            dueTable.AddRow("[yellow]Today[/]", todayCount.ToString());
            dueTable.AddRow("[green]Tomorrow[/]", tomorrowCount.ToString());
            dueTable.AddRow("[cyan]Next 7 Days[/]", weekCount.ToString());
            dueTable.AddRow("[dim]Someday/No Date[/]", noDueCount.ToString());

            // --- Facts Section ---
            var topContext = tasks.Where(t => t.context != 0)
                .GroupBy(t => t.context)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();
            string topContextName = topContext != null ? contexts.FirstOrDefault(c => c.id == topContext.Key)?.name ?? "Unknown" : "N/A";

            var topFolder = tasks.Where(t => t.folder != 0)
                .GroupBy(t => t.folder)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();
            string topFolderName = topFolder != null ? folders.FirstOrDefault(f => f.id == topFolder.Key)?.name ?? "Unknown" : "N/A";

            var topLocation = tasks.Where(t => t.location != 0)
                .GroupBy(t => t.location)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();
            string topLocationName = topLocation != null ? locations.FirstOrDefault(l => l.id == topLocation.Key)?.name ?? "Unknown" : "N/A";

            var oldestTask = tasks.Where(t => t.added > 0).OrderBy(t => t.added).FirstOrDefault();
            string oldestTaskTitle = oldestTask != null && !string.IsNullOrWhiteSpace(oldestTask.title) ? oldestTask.title.EscapeMarkup() : "[dim](no title)[/]";
            string oldestTaskInfo = oldestTask != null ? $"{oldestTaskTitle} [dim]({DateTimeOffset.FromUnixTimeSeconds(oldestTask.added).LocalDateTime:yyyy-MM-dd})[/]" : "N/A";

            var factTable = new Table().NoBorder().HideHeaders().Width(100);
            factTable.AddColumn("Label"); factTable.AddColumn("Value");
            factTable.AddRow("[cyan]Top Context:[/]", $"[blue]@{topContextName}[/] [dim]({topContext?.Count() ?? 0} tasks)[/]");
            factTable.AddRow("[cyan]Top Folder:[/]", $"[green]{topFolderName}[/] [dim]({topFolder?.Count() ?? 0} tasks)[/]");
            factTable.AddRow("[cyan]Top Location:[/]", $"[yellow]{topLocationName}[/] [dim]({topLocation?.Count() ?? 0} tasks)[/]");
            factTable.AddRow("[cyan]Oldest Task:[/]", oldestTaskInfo);

            // --- Assembly ---
            AnsiConsole.WriteLine();
            var headRule = new Rule("[gold1]📊 TASK DASHBOARD[/]");
            headRule.Style = Style.Parse("gold1");
            AnsiConsole.Write(headRule);
            AnsiConsole.WriteLine();

            // Progress Panel
            var progressChart = new BreakdownChart().Width(96);
            progressChart.AddItem("Completed", completedCount, Color.Green);
            if (tasks.Count > 0)
            {
                progressChart.AddItem("Remaining", tasks.Count, Color.Yellow);
            }

            var progressElements = new List<IRenderable>
            {
                new Markup($"[dim]Completed Tasks:[/] [green]{completedCount}[/] [dim]| Remaining Tasks:[/] [yellow]{tasks.Count}[/] [dim]| Total:[/] [white]{totalTasks}[/] [dim]({progressPercent:F1}%)[/]"),
                new Text("") // Spacer
            };

            if (totalTasks > 0)
            {
                progressElements.Add(progressChart);
            }

            AnsiConsole.Write(new Panel(new Rows(progressElements)) { Header = new PanelHeader("[cyan]Task Progress[/]"), Border = BoxBorder.Rounded, Width = 100 });
            AnsiConsole.WriteLine();

            var columns = new Columns(
                new Panel(factTable) { Header = new PanelHeader("[yellow]Top Facts[/]"), Border = BoxBorder.Rounded, Width = 48 },
                new Panel(dueTable) { Header = new PanelHeader("[purple]Due Date Summary[/]"), Border = BoxBorder.Rounded, Width = 48 }
            );
            AnsiConsole.Write(columns);
            AnsiConsole.WriteLine();

            if (tasks.Count > 0)
            {
                AnsiConsole.Write(new Panel(priorityChart) { Header = new PanelHeader("[yellow]Priority Distribution[/]"), Border = BoxBorder.Rounded, Width = 100 });
                AnsiConsole.WriteLine();

                AnsiConsole.Write(folderChart);
                AnsiConsole.WriteLine();

                AnsiConsole.Write(contextChart);
                AnsiConsole.WriteLine();
            }
            else
            {
                AnsiConsole.MarkupLine("[dim]No active tasks to display charts for.[/]");
                AnsiConsole.WriteLine();
            }
        }

        private static bool IsSameDay(long unixTime, DateTime date)
        {
            var taskDate = DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime.Date;
            return taskDate == date.Date;
        }

        public static void DisplayContexts(List<ToodledoContext> contexts)
        {
            if (contexts.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No contexts found.[/]");
                return;
            }

            var table = new Table().Width(100);
            table.Border(TableBorder.Rounded);
            table.BorderStyle(Style.Parse("cyan"));

            table.AddColumn(new TableColumn("[cyan]ID[/]").Centered());
            table.AddColumn(new TableColumn("[cyan]Context Name[/]").LeftAligned());

            foreach (var context in contexts)
            {
                table.AddRow(
                    $"[dim]{context.id}[/]",
                    $"[white]{context.name.EscapeMarkup()}[/]"
                );
            }

            AnsiConsole.WriteLine();
            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]Total Contexts: {contexts.Count}[/]");
        }

        public static void DisplayFolders(List<ToodledoFolder> folders)
        {
            if (folders.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No folders found.[/]");
                return;
            }

            var table = new Table().Width(100);
            table.Border(TableBorder.Rounded);
            table.BorderStyle(Style.Parse("cyan"));

            table.AddColumn(new TableColumn("[cyan]ID[/]").Centered());
            table.AddColumn(new TableColumn("[cyan]Folder Name[/]").LeftAligned());

            foreach (var folder in folders)
            {
                table.AddRow(
                    $"[dim]{folder.id}[/]",
                    $"[white]{folder.name.EscapeMarkup()}[/]"
                );
            }

            AnsiConsole.WriteLine();
            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]Total Folders: {folders.Count}[/]");
        }

        public static void DisplayLocations(List<ToodledoLocation> locations)
        {
            if (locations.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No locations found.[/]");
                return;
            }

            var table = new Table().Width(100);
            table.Border(TableBorder.Rounded);
            table.BorderStyle(Style.Parse("cyan"));

            table.AddColumn(new TableColumn("[cyan]ID[/]").Centered());
            table.AddColumn(new TableColumn("[cyan]Location Name[/]").LeftAligned());
            table.AddColumn(new TableColumn("[cyan]Description[/]").LeftAligned());

            foreach (var loc in locations)
            {
                table.AddRow(
                    $"[dim]{loc.id}[/]",
                    $"[white]{loc.name.EscapeMarkup()}[/]",
                    $"[dim]{loc.description.EscapeMarkup()}[/]"
                );
            }

            AnsiConsole.WriteLine();
            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]Total Locations: {locations.Count}[/]");
        }

        public static void DisplayHelp()
        {
            var table = new Table().Width(100);
            table.Border(TableBorder.Rounded);
            table.BorderStyle(Style.Parse("cyan"));
            table.HideHeaders();

            table.AddColumn(new TableColumn("").Width(32));
            table.AddColumn(new TableColumn(""));

            // --- TASK ---
            table.AddRow("[yellow]TASK[/]", "");
            table.AddRow("[cyan]list[/]", "[dim]Display all active tasks[/]");
            table.AddRow("[cyan]stats[/]", "[dim]Show productivity dashboard[/]");
            table.AddRow("[cyan]add[/] [white]<text> [[params]][/]", "[dim]Create a task with optional parameters:[/]");
            table.AddRow("", "[dim]  [white]p:[[0-3]][/] Priority      [white]f:name[/]  Folder        [white]@name[/]  Context[/]");
            table.AddRow("", "[dim]  [white]!:date[/]  Due Date      [white]*:1[/]     Star          [white]s:#[/]    Status[/]");
            table.AddRow("", "[dim]  [white]#tag[/]    Tag           [white]n:\"...\"[/] Note[/]");
            table.AddRow("", "[dim]  (e.g., add Buy milk p:3 @Store f:Personal !:today)[/]");
            table.AddRow("[cyan]edit[/] [white]<id>[/]", "[dim]Edit task via shadow prompt:[/]");
            table.AddRow("", "[dim]  (supports same parameters as add)[/]");
            table.AddRow("[cyan]view[/] [white]<id>[/]", "[dim]View full task details (including notes)[/]");
            table.AddRow("[cyan]done[/] [white]<id1> [[id2]]...[/]", "[dim]Mark one or more tasks as completed[/]");
            table.AddRow("[cyan]delete[/] [white]<id1> [[id2]]...[/]", "[dim]Permanently remove one or more tasks[/]");
            table.AddRow("[cyan]star[/] [white]<id1> [[id2]]...[/]", "[dim]Star one or more tasks[/]");
            table.AddRow("[cyan]unstar[/] [white]<id1> [[id2]]...[/]", "[dim]Unstar one or more tasks[/]");
            table.AddRow("[cyan]tag[/] [white]<id1> [[id2]]... <tags>[/]", "[dim]Quickly update tags for one or more tasks[/]");
            table.AddRow("[cyan]note[/] [white]<id1> [[id2]]... <text>[/]", "[dim]Quickly update note for one or more tasks[/]");
            table.AddRow("[cyan]find[/] [white]<text>[/]", "[dim]Search tasks by keyword[/]");
            table.AddRow("[cyan]filter[/] [white][[params]][/]", "[dim]List tasks matching parameters:[/]");
            table.AddRow("", "[dim]  (supports same parameters as add)[/]");
            table.AddRow("[cyan]random[/] [white][[params]][/]", "[dim]Show random task matching parameters:[/]");
            table.AddRow("", "[dim]  (supports same parameters as add)[/]");

            // --- CONTEXT ---
            table.AddEmptyRow();
            table.AddRow("[yellow]CONTEXT[/]", "");
            table.AddRow("[cyan]context[/] [white][[search]][/]", "[dim]Display all contexts (optional search filtering)[/]");
            table.AddRow("[cyan]context-add[/] [white]<name>[/]", "[dim]Create a new context[/]");
            table.AddRow("[cyan]context-edit[/] [white]<id|name> <new>[/]", "[dim]Rename a context[/]");
            table.AddRow("[cyan]context-delete[/] [white]<id1> <id2>...[/]", "[dim]Remove one or more contexts[/]");

            // --- FOLDER ---
            table.AddEmptyRow();
            table.AddRow("[yellow]FOLDER[/]", "");
            table.AddRow("[cyan]folder[/] [white][[search]][/]", "[dim]Display all folders (optional search filtering)[/]");
            table.AddRow("[cyan]folder-add[/] [white]<name>[/]", "[dim]Create a new folder[/]");
            table.AddRow("[cyan]folder-edit[/] [white]<id|name> <new>[/]", "[dim]Rename a folder[/]");
            table.AddRow("[cyan]folder-delete[/] [white]<id1> <id2>...[/]", "[dim]Remove one or more folders[/]");

            // --- LOCATION ---
            table.AddEmptyRow();
            table.AddRow("[yellow]LOCATION[/]", "");
            table.AddRow("[cyan]location[/] [white][[search]][/]", "[dim]Display all locations (optional search filtering)[/]");
            table.AddRow("[cyan]location-add[/] [white]<name>[/]", "[dim]Create a new location[/]");
            table.AddRow("[cyan]location-edit[/] [white]<id|name> <new>[/]", "[dim]Rename a location[/]");
            table.AddRow("[cyan]location-delete[/] [white]<id1> <id2>...[/]", "[dim]Remove one or more locations[/]");

            // --- SYSTEM ---
            table.AddEmptyRow();
            table.AddRow("[yellow]SYSTEM[/]", "");
            table.AddRow("[cyan]setup[/]", "[dim]Run the API credential setup wizard[/]");
            table.AddRow("[cyan]help[/]", "[dim]Show this help message[/]");
            table.AddRow("[cyan]exit[/]", "[dim]Exit the application[/]");

            AnsiConsole.WriteLine();
            var panel = new Panel(table)
            {
                Header = new PanelHeader("[yellow]Available Commands[/]", Justify.Left),
                Border = BoxBorder.Rounded,
                BorderStyle = Style.Parse("yellow"),
                Width = 100
            };

            AnsiConsole.Write(panel);
            AnsiConsole.WriteLine();
        }

        public static void DisplaySetupWizard()
        {
            var grid = new Grid();
            grid.AddColumn();
            grid.AddRow(new Markup("[bold yellow]Step 1:[/] Create a Toodledo account if you don't have one."));
            grid.AddRow(new Markup("[bold yellow]Step 2:[/] Visit the [link=https://www.toodledo.com/api/register.php]Toodledo API Registration[/] page."));
            grid.AddRow(new Markup("[bold yellow]Step 3:[/] Register a new application to get your [cyan]Client ID[/] and [cyan]Client Secret[/]."));
            grid.AddRow(new Markup("[dim]       (Redirect URI can be http://localhost:5000/)[/]"));
            grid.AddRow(new Text(""));
            grid.AddRow(new Markup("[bold green]Enter your credentials below to get started![/]"));

            var panel = new Panel(grid)
            {
                Header = new PanelHeader("[cyan]Toodledo API Setup Wizard[/]"),
                Border = BoxBorder.Rounded,
                BorderStyle = Style.Parse("cyan"),
                Padding = new Padding(2, 1),
                Width = 100
            };

            AnsiConsole.WriteLine();
            AnsiConsole.Write(panel);
            AnsiConsole.WriteLine();
        }

        public static string GetPriorityName(int priority)
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

        public static string GetPriorityMarkup(int priority)
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

        public static string GetStarMarkup(int star)
        {
            return star == 1 ? "[gold1]★[/]" : "[dim]·[/]";
        }
        public static bool ConfirmDeletion(string entityType, List<string> itemNames)
        {
            if (itemNames.Count == 0) return false;

            var table = new Table().NoBorder().HideHeaders().Width(100);
            table.AddColumn("Item");
            foreach (var name in itemNames)
            {
                table.AddRow($"[red]•[/] [white]{name.EscapeMarkup()}[/]");
            }

            var panel = new Panel(table)
            {
                Header = new PanelHeader($"[yellow]Confirm Deletion: {entityType}[/]", Justify.Left),
                Border = BoxBorder.Rounded,
                BorderStyle = Style.Parse("yellow"),
                Width = 100
            };

            AnsiConsole.WriteLine();
            AnsiConsole.Write(panel);

            return AnsiConsole.Confirm($"[red]CRITICAL:[/] Are you sure you want to delete these {itemNames.Count} {entityType}(s)?", false);
        }
    }
}
