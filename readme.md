# TaskbarIconOverlay

A Windows utility that applies custom overlay icons to taskbar buttons based on window titles or process names.  Optimized for VS Code/VSCodium.

![Screenshot](screenshot.png)

## Motivation

I use Windows 11 combined with [Windhawk's "Disable grouping on the taskbar"](https://windhawk.net/mods/taskbar-grouping) mod which means my taskbar items aren't grouped and it shows the icon only. As a result, when running an application in multiple instances (like VS Code), it's difficult to distinguish which icon corresponds to which project or window.

While Windows doesn't allow you to easily change the icon of a program for different instances, it does support overlay icons (small badges on taskbar buttons) - and this is what this tool takes full advantage of. By adding custom overlay icons to taskbar buttons, you can easily identify specific windows at a glance.

While this tool was primarily built to distinguish between multiple VS Code/VSCodium workspace windows (and perhaps other popular text editors in the future), it's kept generic enough to work with any application. Perfect for developers working with multiple Visual Studio Code workspaces, browser windows for different projects, or any scenario where you want visual distinction between similar applications.

## Requirements

- **Platform**: Windows x86-64 (Intel/AMD 64-bit processors)
- **Tested on**: Windows 11 25H2
- Other Windows versions may work but have not been tested
- .NET 8 Desktop Runtime installed

## How to Use

### Quick Start

1. Download the latest release in the releases section of GitHub
2. Add your icons to an `icons` folder in the same directory
3. Run the executable (make sure .NET 8 Desktop Runtime installed)

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

## Where to Get Icons

- [icon-icons.com](https://icon-icons.com/) - Large collection of free icons
  - Download the `.ico` file format (not PNG or other formats)
  - 512px resolution works well
- Convert PNG images to ICO format using [CloudConvert](https://cloudconvert.com/png-to-ico)

## Special Logic for VS Code

The tool includes special handling for Visual Studio Code and VSCodium:

- Automatically extracts workspace/folder names from VS Code window titles
- Looks for matching `.ico` files in a subfolder named after the process (e.g., `icons/Code/` for VS Code, `icons/VSCodium/` for VSCodium)
- Falls back to the process name (`code.ico` or `vscodium.ico`) in the root icons folder if no workspace-specific icon exists

**Example:** If you have a VS Code workspace named `my-cool-project`, the tool will look for `icons/Code/my-cool-project.ico`. For VSCodium, it would look for `icons/VSCodium/my-cool-project.ico`.

This allows different VS Code windows to have different overlay icons based on which project you're working on.

## How to Develop & Test

To test the program during development without building an executable:

**Prerequisites:**
- .NET SDK (tested with version 10.0.1, released December 9, 2025)
- Download from: [https://dotnet.microsoft.com/en-us/download/dotnet?cid=getdotnetcorecli](https://dotnet.microsoft.com/en-us/download/dotnet?cid=getdotnetcorecli)

**Run the program:**
```powershell
dotnet run
```

### How to Compile

To build the executable for distribution:

```powershell
dotnet publish -c Release -r win-x64 -o .\dist --no-self-contained -p:PublishSingleFile=false
```

This creates a standalone executable at `dist\TaskbarIconOverlay.exe` that includes the .NET runtime and requires no additional dependencies.

## TODO

- [ ] Support for other VSCode derivatives (Cursor, Kiro, etc.)
- [ ] Support for Zed Editor
