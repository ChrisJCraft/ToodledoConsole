# Toodledo Console

![.NET Version](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)
![Version](https://img.shields.io/badge/version-0.1.0-blue)
![License](https://img.shields.io/badge/license-MIT-blue.svg)
![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey)

A lightweight, modern C# CLI tool for managing your [Toodledo](https://www.toodledo.com/) tasks directly from the terminal. Built with [Spectre.Console](https://spectreconsole.net/) for a beautiful, interactive command-line experience.

## ✨ Features

- **🎨 Beautiful CLI Interface**: Powered by Spectre.Console with rich colors, tables, and formatting
- **⚡ Detailed Task Control**: Set priority, folder, context, due dates, tags, and notes directly from the command line
- **🏷️ Tag Support**: Organize tasks with tags using `#tag` shorthand and a dedicated `tag` command
- **📝 Note Management**: Add detailed notes to tasks with `n:"..."` shorthand and a dedicated `note` command
- **🔍 Powerful Filters & Search**: Apply complex filters (priority, folder, context) or search by keyword
- **🎯 Smart Random Selection**: Intelligent task picking avoids repetition
- **🔐 Secure OAuth2 Authentication**: Industry-standard OAuth2 flow with automatic token refresh
- **💾 Persistent Sessions**: Log in once, stay authenticated for 30 days via refresh tokens
- **🚀 Automatic Browser Launch**: Seamless authentication flow with automatic browser handling
- **📦 Modular Architecture**: Service-based design for maintainability
- **🖥️ Standardized UI**: Consistent 100-column width for all views
- **✨ Modern Code**: 100% clean build with zero warnings (Nullable Reference Types enabled)

## 📋 Commands

| Command | Description |
|---------|-------------|
| `list` | Display all current tasks in a formatted table |
| `stats` | Show productivity dashboard with charts and progress |
| `add [text]` | Create a new task (supports shorthands) |
| `edit [id]` | Edit task using "Shadow Prompt" shorthand mode |
| `view [id]` | View full task details (including notes) |
| `tag [id]... [tags]` | Quickly update tags for one or more tasks |
| `note [id]... [text]` | Quickly update note for one or more tasks |
| `done [id]...` | Mark one or more tasks as completed |
| `delete [id]...` | Permanently remove one or more tasks |
| `star [id]...` | Star one or more tasks |
| `unstar [id]...` | Unstar one or more tasks |
| `find [text]` | Search for tasks by title or keyword |
| `filter [k:v]` | Power-user filters (e.g., `filter p:1 f:Inbox @Work`) |
| `folder [search]` | List all folders (optional search filtering) |
| `folder-add [name]` | Create a new folder |
| `folder-edit [i|n] [new]` | Rename a folder (by ID or name) |
| `folder-delete [i|n]...` | Remove one or more folders (by ID or name) |
| `context [search]` | List all contexts (optional search filtering) |
| `context-add [name]` | Create a new context |
| `context-edit [i|n] [new]` | Rename a context (by ID or name) |
| `context-delete [i|n]...` | Remove one or more contexts (by ID or name) |
| `location [search]` | List all locations (optional search filtering) |
| `location-add [name]` | Create a new location |
| `location-edit [i|n] [new]` | Rename a location (by ID or name) |
| `location-delete [i|n]...` | Remove one or more locations (by ID or name) |
| `setup` | Run the API credential setup wizard |
| `random [k:v]` | Pick a random task (supports selectors like `random p:2`) |
| `help` | Show available commands and usage information |
| `exit` | Close the application |

## 📦 Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- A [Toodledo](https://www.toodledo.com/) account
- Toodledo API credentials (Client ID and Client Secret)

## 🔑 Getting Toodledo API Credentials

Before you can use this application, you need to register it with Toodledo to get API credentials:

1. **Log in to Toodledo**: Go to [https://www.toodledo.com/](https://www.toodledo.com/)
2. **Navigate to API Settings**: Visit [https://api.toodledo.com/3/account/doc_register.php](https://api.toodledo.com/3/account/doc_register.php)
3. **Register Your Application**:
   - **Application Name**: Choose any name (e.g., "My Toodledo Console")
   - **Description**: Brief description of your personal use
   - **Redirect URI**: `http://localhost:5000/callback`
   - **Application Type**: Select "Desktop Application" or "Other"
4. **Save Your Credentials**: After registration, you'll receive:
   - **Client ID**: A unique identifier for your application
   - **Client Secret**: A secret key for authentication
   
   ⚠️ **Keep these credentials secure!** Never share them or commit them to version control.

## 🚀 Installation & Setup

### 1. Clone the Repository

```bash
git clone https://github.com/ChrisJCraft/ToodledoConsole.git
cd ToodledoConsole
```

### 2. Restore Dependencies

```bash
dotnet restore
```

### 3. Run the Application

```bash
dotnet run
```

### 4. Setup

On first run, if `auth.txt` is missing, the **Setup Wizard** will launch automatically:
1. Follow the prompts to enter your **Client ID** and **Client Secret**.
2. Credentials will be saved securely to `auth.txt` (which is ignored by Git).

Alternatively, you can manually create `auth.txt` in the root folder:
```
YOUR_CLIENT_ID
YOUR_CLIENT_SECRET
```

### 5. Authorize the Application

1. A browser window will open automatically.
2. If it doesn't open, manually navigate to `http://localhost:5000`.
3. You'll be redirected to Toodledo to authorize the application.
4. After authorization, you'll be redirected back and can close the browser.
5. The application will save your access token for future use.

> **Note**: The OAuth2 redirect URI is configured for `http://localhost:5000/callback`. Make sure this matches what you registered with Toodledo.

## ⚡ Power-User Shorthands

Use these shorthands with the `add` and `edit` commands for rapid task entry:

| Shorthand | Description | Example |
|-----------|-------------|---------|
| `p:[0-3]` | Priority (0: Low, 1: Medium, 2: High, 3: Top) | `add Milk p:2` |
| `f:[name]` | Folder Name | `add Script f:Work` |
| `@[name]` | Context Name | `add Bread @Store` |
| `!:[shortcut]`| Due Date (today, tomorrow, next week) | `add Taxes !:today` |
| `#[tag]` | Add a tag (can be used multiple times) | `add Release #v0.1.0 #beta` |
| `n:"[text]"` | Add a note (best in quotes) | `add Jira n:"Fix bug"` |
| `*` | Star the task | `add Urgent *` |

> **Pro Tip**: You can combine multiple shorthands in a single command!
> `add Buy milk p:3 @Store f:Personal !:today #groceries n:"Get whole milk"`

## 🔧 How It Works

### OAuth2 Authentication Flow

1. **Initial Request**: The app starts a local web server on port 5000 and opens your browser
2. **Authorization**: You authorize the app on Toodledo's website
3. **Token Exchange**: The app exchanges the authorization code for an access token and refresh token
4. **Token Storage**: Tokens are securely stored in `token.txt` (ignored by Git)
5. **Automatic Refresh**: When the access token expires, the app automatically refreshes it using the refresh token

### Random Task Selection

The `random` command uses intelligent tracking to avoid showing the same tasks repeatedly:
- Tracks previously shown tasks in `random_state.json`
- Resets the pool when all tasks have been shown
- Ensures a fresh experience each time

## 📁 Project Structure

```
ToodledoConsole/
├── Program.cs            # Entry point and command loop
├── AuthService.cs        # OAuth2 authentication & token management
├── TaskService.cs        # Task CRUD and retrieval
├── ContextService.cs     # Context management
├── FolderService.cs      # Folder management
├── LocationService.cs    # Location management
├── FilterService.cs      # Task list filtering logic
├── TaskParserService.cs  # Shorthand parsing & task reconstruction
├── UIService.cs          # Standardized UI rendering (Spectre.Console)
├── InputService.cs       # Console input with history
├── Models.cs             # Shared data models
├── auth.txt              # API credentials (NOT in Git)
├── token.txt             # OAuth tokens (NOT in Git)
├── random_state.json     # Random task selection state (NOT in Git)
├── README.md             # This file
```

## 🐛 Troubleshooting

### Browser Doesn't Open Automatically

If the browser doesn't launch automatically during authentication:
- Manually navigate to `http://localhost:5000` in your browser
- Check if port 5000 is already in use by another application

### "Invalid Client" Error

This usually means:
- Your `auth.txt` file is missing or incorrectly formatted
- Your Client ID or Client Secret is incorrect
- Verify your credentials in the Toodledo API settings
- Run `setup` command to re-enter credentials

### "Redirect URI Mismatch" Error

- Ensure your Toodledo app registration has the redirect URI set to: `http://localhost:5000/callback`
- The URI must match exactly (including the protocol and port)

### Token Expired / Authentication Failed

- Delete `token.txt` and restart the application to re-authenticate
- The app should automatically refresh tokens, but manual re-auth may be needed if the refresh token expires

### Tasks Not Updating After "Done" Command

- Try running the `list` command again to refresh the task list
- Verify the task ID is correct
- Check your internet connection

### Port 5000 Already in Use

If you get an error that port 5000 is already in use:
- Close any other applications using port 5000
- Or modify the port in `AuthService.cs` (remember to update your Toodledo redirect URI accordingly)

## 📚 Additional Resources

- [Toodledo API Documentation](https://api.toodledo.com/3/)
- [Spectre.Console Documentation](https://spectreconsole.net/)
- [.NET 8.0 Documentation](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8)

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🤝 Contributing

Contributions are welcome! Feel free to:
- Report bugs by opening an issue
- Suggest new features
- Submit pull requests
- See [CONTRIBUTING.md](CONTRIBUTING.md) for details.

## 👤 Author

**Christopher Jay Craft**

---

**Enjoy managing your tasks from the terminal!** 🎉