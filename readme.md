# TaskbarIconOverlay

A Windows utility that applies custom overlay icons to taskbar buttons based on window titles or process names.  Optimized for VS Code/VSCodium.

## Motivation

Windows applications often look identical in the taskbar, making it hard to distinguish between multiple instances or different projects. While Windows doesn't allow you to easily change the icon of a program for different instances, it does support overlay icons (small badges on taskbar buttons) - and this is what this tool takes full advantage of. By adding custom overlay icons to taskbar buttons, you can easily identify specific windows at a glance.

While this tool was primarily built to distinguish between multiple VS Code/VSCodium workspace windows, it's kept generic enough to work with any application. Perfect for developers working with multiple Visual Studio Code workspaces, browser windows for different projects, or any scenario where you want visual distinction between similar applications. Especially useful when you've disabled taskbar item grouping, as each window gets its own taskbar button.

## Requirements

- **Platform**: Windows x86-64 (Intel/AMD 64-bit processors)
- **Tested on**: Windows 11 25H2
- Other Windows versions may work but have not been tested

## How to Use

### Quick Start

1. Download `TaskbarIconOverlay.exe` from the [dist](dist) folder
2. Add your icons to an `icons` folder in the same directory
3. Run the executable

**Optional:** Create a `config.json` file if you want to customize the icons folder location (see Configuration section below)

### Configuration (config.json)

**Optional:** By default, the tool loads icons from the `icons` folder in the same directory as the executable.

If you want to use a different location, create a `config.json` file in the same directory as the executable:

```json
{
  "IconsPath": "C:\\Path\\To\\Your\\Icons"
}
```

- `iconsPath`: Directory containing your `.ico` files (relative to the executable, defaults to `icons`)

### Icon Naming

Icons should be named to match:
- **Process name**: `chrome.ico`, `firefox.ico`, `code.ico`
- **VS Code workspaces**: The tool automatically extracts workspace names from VS Code window titles

For example, if you have a VS Code workspace named "TaskbarIconOverlay", create `TaskbarIconOverlay.ico` in your icons folder.

### Command-Line Flags

- **Default** (no flags): Apply overlays once to all matching windows
  ```
  TaskbarIconOverlay.exe
  ```

- **Watch mode** (`--watch` or `-w`): Continuously monitor and apply overlays
  ```
  TaskbarIconOverlay.exe --watch
  TaskbarIconOverlay.exe --watch 5
  ```
  Optional: Specify interval in seconds (default: 3)

- **List windows** (`--list-all` or `-l`): Show all visible windows with their process names
  ```
  TaskbarIconOverlay.exe --list-all
  ```

## Where to Get Icons

- [icon-icons.com](https://icon-icons.com/) - Large collection of free icons
  - Download the `.ico` file format (not PNG or other formats)
  - 512px resolution works well
- Convert PNG images to ICO format using [CloudConvert](https://cloudconvert.com/png-to-ico)

## Special Logic for VS Code

The tool includes special handling for Visual Studio Code and VSCodium:

- Automatically extracts workspace/folder names from VS Code window titles
- Looks for matching `.ico` files based on the workspace name
- Falls back to the process name (`code.ico`) if no workspace-specific icon exists

**Example:** If you have a workspace file named `my-cool-project.code-workspace`, the tool will look for an icon file named `my-cool-project.ico` in your icons folder.

This allows different VS Code windows to have different overlay icons based on which project you're working on.

## Development & Testing

To test the program during development without building an executable:

**Prerequisites:**
- .NET SDK (tested with version 10.0.1, released December 9, 2025)
- Download from: [https://dotnet.microsoft.com/en-us/download/dotnet?cid=getdotnetcorecli](https://dotnet.microsoft.com/en-us/download/dotnet?cid=getdotnetcorecli)

**Run the program:**
```powershell
dotnet run
```

**With command-line arguments:**
```powershell
dotnet run -- --watch
dotnet run -- --list-all
dotnet run -- --watch 5
```

Note: The `--` separator is required to pass arguments to the program (not to the `dotnet run` command itself).

## How to Compile

To build the single-file executable for distribution:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\dist
```

This creates a standalone executable at `dist\TaskbarIconOverlay.exe` that includes the .NET runtime and requires no additional dependencies.

## TODO

- [ ] Support for Cursor editor
- [ ] Support for Zed editor
