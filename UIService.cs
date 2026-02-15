using System;
using System.Collections.Generic;
using System.Linq;
using Spectre.Console;

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
            table.AddColumn(new TableColumn("[cyan]Task[/]").LeftAligned());
            table.AddColumn(new TableColumn("[cyan]Tags[/]").LeftAligned());

            foreach (var task in tasks)
            {
                table.AddRow(
                    $"[dim]{task.id}[/]",
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
            table.AddColumn(new TableColumn("[cyan]Task[/]").LeftAligned());
            table.AddColumn(new TableColumn("[cyan]Tags[/]").LeftAligned());

            foreach (var task in tasks)
            {
                table.AddRow(
                    $"[dim]{task.id}[/]",
                    GetPriorityMarkup(task.priority),
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
            
            table.AddColumn(new TableColumn("").Width(20));
            table.AddColumn(new TableColumn(""));

            table.AddRow("[cyan]list[/]", "[dim]Display all active tasks[/]");
            table.AddRow("[cyan]contexts[/]", "[dim]Display all contexts[/]");
            table.AddRow("[cyan]add[/] [white]<text>[/]", "[dim]Create task (ex: add Buy milk p:3 @Store !:today)[/]");
            table.AddRow("[cyan]edit[/] [white]<id>[/]", "[dim]Edit task using shadow prompt shorthand[/]");
            table.AddRow("[cyan]view[/] [white]<id>[/]", "[dim]View full task details (including notes)[/]");
            table.AddRow("[cyan]tag[/] [white]<id> <tags>[/]", "[dim]Quickly update tags for a task[/]");
            table.AddRow("[cyan]note[/] [white]<id> <text>[/]", "[dim]Quickly update note for a task[/]");
            table.AddRow("[cyan]done[/] [white]<id>[/]", "[dim]Mark a task as completed[/]");
            table.AddRow("[cyan]delete[/] [white]<id>[/]", "[dim]Permanently remove a task[/]");
            table.AddRow("[cyan]add-context[/] [white]<name>[/]", "[dim]Create a new context[/]");
            table.AddRow("[cyan]edit-context[/] [white]<id|name> <new>[/]", "[dim]Rename a context[/]");
            table.AddRow("[cyan]delete-context[/] [white]<id|name>[/]", "[dim]Remove a context[/]");
            table.AddRow("[cyan]folders[/]", "[dim]Display all folders[/]");
            table.AddRow("[cyan]add-folder[/] [white]<name>[/]", "[dim]Create a new folder[/]");
            table.AddRow("[cyan]edit-folder[/] [white]<id|name> <new>[/]", "[dim]Rename a folder[/]");
            table.AddRow("[cyan]delete-folder[/] [white]<id|name>[/]", "[dim]Remove a folder[/]");
            table.AddRow("[cyan]locations[/]", "[dim]Display all locations[/]");
            table.AddRow("[cyan]add-location[/] [white]<name>[/]", "[dim]Create a new location[/]");
            table.AddRow("[cyan]edit-location[/] [white]<id|name> <new>[/]", "[dim]Rename a location[/]");
            table.AddRow("[cyan]delete-location[/] [white]<id|name>[/]", "[dim]Remove a location[/]");
            table.AddRow("[cyan]find[/] [white]<text>[/]", "[dim]Search tasks by keyword[/]");
            table.AddRow("[cyan]filter[/] [white][[k:v]][/]", "[dim]Power-user filters (p:2, f:Inbox, @Work...)[/]");
            table.AddRow("[cyan]random[/] [white][[k:v]][/]", "[dim]Show random task (supports selectors like p:2, @Work)[/]");
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
    }
}
