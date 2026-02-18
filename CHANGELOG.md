# Changelog

All notable changes to Toodledo Console will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - Initial Release

### Features
- **Core Task Management**: `add`, `list`, `edit`, `view`, `delete`, `done`
- **Organization**: Support for Folders, Contexts, and Locations (CRUD)
- **Productivity Tools**: `stats` dashboard, `star`/`unstar` tasks, `tag` and `note` management
- **Power Features**:
  - `filter` command with advanced query syntax (e.g. `p:1 @Work`)
  - `find` command for keyword search
  - `random` command for intelligent task selection
  - Shorthand syntax for rapid entry (e.g. `add Milk @Store !:today p:2`)
- **Authentication**: Secure OAuth2 flow with automatic token refresh and setup wizard
- **UI**: Rich, interactive terminal interface powered by Spectre.Console with 100-column standardization

## Version History Summary

- **v0.1.0** (Current) - Initial public release
