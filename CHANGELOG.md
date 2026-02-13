# Changelog

All notable changes to Toodledo Console will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.5.1] - 2026-02-13

### Added
- **Spectre.Console Integration**: Complete UI overhaul with rich colors, tables, and beautiful formatting
- **Smart Random Task Selection**: Non-repeating random task picker that tracks previously shown tasks
- **Task Counters**: Added "Tasks Remaining" counter to done command and total tasks counter to list command
- **Enhanced Documentation**: Comprehensive README with setup instructions, troubleshooting, and API credential guide
- **Project Metadata**: Added version, author, and repository information to .csproj
- **MIT License**: Added open source license

### Fixed
- **Done Command**: Fixed issue where completed tasks were still appearing in the list
- **OAuth2 Scopes**: Resolved 'invalid permissions' error by updating OAuth2 scope configuration

### Changed
- Updated `.gitignore` to include `random_state.json` for random task tracking

### Security
- Verified sensitive files (`auth.txt`, `token.txt`, `random_state.json`) are not tracked in git history
- Enhanced security documentation in README

## [1.0.0] - 2026-02-11

### Added
- **Core Commands**: list, add, find, done, help, exit
- **OAuth2 Authentication**: Secure authentication flow with automatic token refresh
- **Persistent Sessions**: 30-day authentication via refresh tokens
- **Automatic Browser Launch**: Seamless OAuth flow with automatic browser handling
- **Help Command**: Added command menu with usage information

### Changed
- **Code Refactoring**: Separated code into modular architecture
  - `Models.cs`: Data structures (TokenStorage, TokenResponse, ToodledoTask)
  - `AuthService.cs`: OAuth2 flow and token management
  - `TaskService.cs`: API interactions for task operations
  - `Program.cs`: Main entry point and command handling
- Updated README to reflect new modular architecture

## [0.1.0] - 2026-02-10

### Added
- **Initial Release**: Basic Toodledo task management functionality
- **Find Command**: Search tasks by title
- **Add Task Feature**: Create new tasks from the command line
- **Display Logic**: Refactored task display formatting

---

## Version History Summary

- **v1.5.1** (Current) - Spectre.Console UI, smart random selection, enhanced documentation
- **v1.0.0** - Modular architecture, OAuth2 authentication, core commands
- **v0.1.0** - Initial release with basic functionality
