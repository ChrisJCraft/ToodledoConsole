using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ToodledoConsole
{


    class Program
    {
        private static AuthService _authService;
        private static TaskService _taskService;
        private static readonly HttpClient _httpClient = new HttpClient();
        private static List<ToodledoTask> _cachedTasks = new List<ToodledoTask>();
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        static async Task Main(string[] args)
        {
            Console.Clear();
            Console.WriteLine("========================================");
            Console.WriteLine("   TOODLEDO CONSOLE v1.5.1");
            Console.WriteLine("========================================");

            try
            {
                _authService = new AuthService(_httpClient, _jsonOptions);
                _taskService = new TaskService(_httpClient, _authService, _jsonOptions);
                if (!_authService.LoadSecrets())
                {
                    Console.WriteLine($"Error: {AuthService.AuthFile} not found or invalid.");
                    Console.WriteLine("Please create 'auth.txt' with Client ID on line 1 and Client Secret on line 2.");
                    return;
                }

                bool authenticated = await _authService.InitializeAsync();

                if (!authenticated)
                {
                    Console.WriteLine("1. A browser window should open automatically.");
                    Console.WriteLine("2. If not, visit http://localhost:5000/ to authorize.");
                    await _authService.AuthorizeAsync();
                }

                Console.WriteLine("Success! Connection Verified.");
                await RunCommandLoop();
            }
            catch (Exception ex) { Console.WriteLine("\n[FATAL ERROR]: " + ex.Message); }
        }

        private static async Task RunCommandLoop()
        {
            DisplayHelp();
            while (true)
            {
                Console.Write("\nToodledo> ");
                string input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input)) continue;

                string cleanInput = input.Trim();
                string lowerInput = cleanInput.ToLower();

                if (lowerInput == "exit") break;
                if (lowerInput == "help") DisplayHelp();
                else if (lowerInput == "list") await ListTasks();
                else if (lowerInput == "random") await ShowRandom();
                else if (lowerInput.StartsWith("find ")) await SearchTasks(cleanInput.Substring(5).Trim());
                else if (lowerInput.StartsWith("done ")) await CompleteTask(cleanInput.Substring(5).Trim());
                else if (lowerInput.StartsWith("add ")) await AddTask(cleanInput.Substring(4).Trim());
                else Console.WriteLine("Unknown command.");
            }
        }

        private static async Task AddTask(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return;
            if (await _taskService.AddTaskAsync(title)) {
                Console.WriteLine($"[ADDED]: {title}");
                await ListTasks();
            } else Console.WriteLine("Error adding task.");
        }

        private static async Task ListTasks()
        {
            try {
                _cachedTasks = await _taskService.GetTasksAsync();
                DisplayTasks(_cachedTasks);
            } catch (Exception ex) { Console.WriteLine("Error: " + ex.Message); }
        }

        private static async Task SearchTasks(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword)) return;
            if (_cachedTasks.Count == 0) await ListTasks();
            var results = _cachedTasks.FindAll(t => t.title.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            if (results.Count == 0) Console.WriteLine($"No matches for: '{keyword}'");
            else DisplayTasks(results);
        }

        private static void DisplayTasks(List<ToodledoTask> tasks)
        {
            Console.WriteLine($"\n{"ID",-12} | " + "Task");
            Console.WriteLine(new string('-', 45));
            foreach (var t in tasks) Console.WriteLine($"{t.id,-12} | {t.title}");
        }

        private static async Task ShowRandom()
        {
            if (_cachedTasks.Count == 0) await ListTasks();
            if (_cachedTasks.Count > 0) {
                var t = _cachedTasks[new Random().Next(_cachedTasks.Count)];
                Console.WriteLine($"\n[PICK]: {t.title} (ID: {t.id})");
            }
        }

        private static async Task CompleteTask(string id)
        {
            if (await _taskService.CompleteTaskAsync(id)) Console.WriteLine("Task Completed!");
            else Console.WriteLine("Error completing task.");
        }
        private static void DisplayHelp()
        {
            Console.WriteLine("\nCommands: 'list' | 'find [text]' | 'add [text]' | 'random' | 'done [id]' | 'help' | 'exit'");
        }
    }
}