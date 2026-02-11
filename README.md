# Toodledo Console
A lightweight, modular C# CLI tool for managing your Toodledo tasks directly from the terminal.

## Features
- **Project Organization**: Code split into `Models.cs`, `AuthService.cs`, `TaskService.cs`, and `Program.cs` for clean architecture.
- **Persistent Sessions**: Log in once, stay authenticated for 30 days via Refresh Tokens.
- **Secure Secrets**: Client IDs are stored locally in `auth.txt` and never pushed to Git (added to `.gitignore`).

## Commands
- `list`: Show all current tasks.
- `add [text]`: Create a new task.
- `find [text]`: Search for tasks by title.
- `done [id]`: Mark a task as completed.
- `help`: Show available commands.
- `random`: Pick a random task when you're feeling indecisive.
- `exit`: Close the application.

## Installation & Setup
1. **Clone the repo.**
2. **Create `auth.txt`**: Create a file named `auth.txt` in the project root directory.
   - Line 1: Your Toodledo Client ID
   - Line 2: Your Toodledo Client Secret
   *(Note: This file is ignored by git to keep your secrets safe.)*
3. **Run the application**:
   ```bash
   dotnet run
   ```
4. **Authorize**: Follow the on-screen instructions to authorize the app in your browser locally at `localhost:5000`.

## Project Structure
- **Program.cs**: Main entry point and command handling loop.
- **AuthService.cs**: Handles OAuth2 flow, token management, and refreshing.
- **TaskService.cs**: Manages API interactions for task operations.
- **Models.cs**: Defines data structures (`TokenStorage`, `TokenResponse`, `ToodledoTask`).