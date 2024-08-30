# README - FileSurfer

## This is an open-source file explorer for Windows, built with Avalonia UI (and tears).
Supports all you'd expect from a modern file explorer, plus some extra quirks:
- Image pasting from the system clipboard
- Treating dotfiles as hidden
- Renaming multiple files or directories at once
- Undo & redo for most of the reversible file operations
- Integration with the Git version control system


## Dependencies
To build and run this project, ensure that the following dependencies are installed:

### .NET and Frameworks
- **.NET 8.0**
- **Avalonia UI**
- **Windows Forms**: The project uses Windows Forms to interact with the system clipboard

### NuGet Packages
- **[Avalonia](https://avaloniaui.net/gettingstarted#installation)** (v11.0.10)
- **Avalonia.Desktop** (v11.0.10)
- **Avalonia.Themes.Fluent** (v11.0.10)
- **Avalonia.Fonts.Inter** (v11.0.10)
- **Avalonia.ReactiveUI** (v11.0.10)
- **Avalonia.Diagnostics** (v11.0.10) — *Debug configuration only*
- **LibGit2Sharp** (v0.30.0)
- **SharpCompress** (v0.37.2)

### COM References
- **Shell32** (GUID: `50a7e9b0-70ef-11d1-b75a-00a0c90564fe`)
- **IWshRuntimeLibrary** (GUID: `f935dc20-1cf0-11d0-adb9-00c04fd58a0b`)

Ensure these COM components are registered on the system where the application is running. If not, you may need to manually register the corresponding DLLs using `regsvr32 "path/to/dll"`.

### Other
- [Git for Windows](https://git-scm.com/download/win): for optional Git integration.


## Building from source
1) Install the .NET Build tool using the [Visual Studio Build Tools](https://visualstudio.microsoft.com/cs/visual-cpp-build-tools/) installer.
2) Open "Developer command prompt for VS 2022"
3) Run `dotnet restore "path\to\csproject-file\FileSurfer.csproj"`
4) Run `msbuild "path\to\project\solution\FileSurfer.sln" /t:publish /p:Configuration=Release /p:DeployOnBuild=true`
5) Find `.\src\FileSurfer\bin\Release\net8.0-windows\FileSurfer.exe` to run the app.

[User Guide](docs/UserGuide.md)
---
[Programming Documentation](docs/ProgrammingDocumentation.md)
---