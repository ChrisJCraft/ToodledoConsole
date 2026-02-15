using System;
using System.Collections.Generic;
using System.Text;
using Spectre.Console;

namespace ToodledoConsole
{
    public class InputService
    {
        private List<string> _commandHistory = new List<string>();
        private int _historyIndex = -1;
        private string _currentInput = "";

        public string ReadLineWithHistory(string prompt)
        {
            AnsiConsole.Markup(prompt);

            if (Console.IsInputRedirected)
            {
                var redirectedInput = Console.ReadLine();
                if (redirectedInput != null && !string.IsNullOrWhiteSpace(redirectedInput))
                {
                    _commandHistory.Add(redirectedInput);
                }
                return redirectedInput ?? "";
            }

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

        private void ClearCurrentLine(string prompt, int inputLength)
        {
            for (int i = 0; i < inputLength; i++)
            {
                Console.Write("\b \b");
            }
        }
    }
}
