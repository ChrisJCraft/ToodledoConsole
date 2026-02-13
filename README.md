# Toodledo Console

![.NET Version](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)
![License](https://img.shields.io/badge/license-MIT-blue.svg)
![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey)

A lightweight, modern C# CLI tool for managing your [Toodledo](https://www.toodledo.com/) tasks directly from the terminal. Built with [Spectre.Console](https://spectreconsole.net/) for a beautiful, interactive command-line experience.

## ✨ Features

- **🎨 Beautiful CLI Interface**: Powered by Spectre.Console with rich colors, tables, and formatting
- **🔐 Secure OAuth2 Authentication**: Industry-standard OAuth2 flow with automatic token refresh
- **💾 Persistent Sessions**: Log in once, stay authenticated for 30 days via refresh tokens
- **🚀 Automatic Browser Launch**: Seamless authentication flow with automatic browser handling
- **🎲 Smart Random Task Selection**: Get a random task with intelligent tracking to avoid repetition
- **🔍 Powerful Search**: Find tasks quickly by title
- **📦 Modular Architecture**: Clean separation of concerns with dedicated services for auth and tasks
- **🔒 Secure by Default**: API credentials stored locally and never committed to version control

## 📋 Commands

| Command | Description |
|---------|-------------|
| `list` | Display all current tasks in a formatted table |
| `add [text]` | Create a new task with the specified title |
| `find [text]` | Search for tasks by title (case-insensitive) |
| `done [id]` | Mark a task as completed and remove it from your list |
| `random` | Pick a random task (intelligently avoids recently shown tasks) |
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

### 2. Create the `auth.txt` File

Create a file named `auth.txt` in the project root directory with your Toodledo API credentials:

```
YOUR_CLIENT_ID
YOUR_CLIENT_SECRET
```

- **Line 1**: Your Toodledo Client ID
- **Line 2**: Your Toodledo Client Secret

> **Note**: This file is automatically ignored by Git (listed in `.gitignore`) to keep your credentials safe.

### 3. Restore Dependencies

```bash
dotnet restore
```

### 4. Run the Application

```bash
dotnet run
```

### 5. Authorize the Application

On first run:
1. A browser window will open automatically
2. If it doesn't open, manually navigate to `http://localhost:5000`
3. You'll be redirected to Toodledo to authorize the application
4. After authorization, you'll be redirected back and can close the browser
5. The application will save your access token for future use

> **Note**: The OAuth2 redirect URI is configured for `http://localhost:5000/callback`. Make sure this matches what you registered with Toodledo.

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
├── Program.cs          # Main entry point and command handling loop
├── AuthService.cs      # OAuth2 flow, token management, and refresh logic
├── TaskService.cs      # API interactions for task operations (list, add, find, done)
├── Models.cs           # Data structures (TokenStorage, TokenResponse, ToodledoTask)
├── auth.txt            # Your API credentials (NOT tracked in Git)
├── token.txt           # OAuth tokens (NOT tracked in Git)
├── random_state.json   # Random task selection state (NOT tracked in Git)
└── README.md           # This file
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

## 👤 Author

**Chris Craft**

---

**Enjoy managing your tasks from the terminal!** 🎉