# Toodledo Console
A lightweight C# CLI tool for managing your Toodledo tasks directly from the terminal.

## Features
- **Persistent Sessions**: Log in once, stay authenticated for 30 days via Refresh Tokens.
- **Secure Secrets**: Client IDs are stored locally in `auth.txt` and never pushed to Git.
- **Commands**:
  - `list`: Show all current tasks.
  - `random`: Pick a random task when you're feeling indecisive.
  - `done [id]`: Mark a task as completed.

## Installation
1. Clone the repo.
2. Run `dotnet run` to generate the `auth.txt` template.
3. Open `auth.txt` and paste your Toodledo Client ID and Secret.
4. Run `dotnet run` again and follow the login instructions.