# README - FileSurfer

### This is an open-source file explorer for Windows, built with Avalonia UI (and tears).
Supports all you'd expect from a modern file explorer, plus some extra quirks:
- Image pasting from the system clipboard
- Treating dotfiles as hidden
- Renaming multiple files or directories at once
- Undo & redo for most of the reversible file operations
- Integration with the Git version control system


## Prerequisites
To build and run this project, ensure that the following dependencies are installed:

### .NET and Frameworks
- **[.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-8.0.401-windows-x64-installer)**: The target framework
- **[Visual Studio Build Toos](https://visualstudio.microsoft.com/cs/visual-cpp-build-tools/)**: To build the project
- **Windows Forms**: The project uses Windows Forms to interact with the system clipboard

### NuGet Packages
The following NuGet packages are required:
- **[Avalonia](https://avaloniaui.net/gettingstarted#installation)**(v11.0.10)
- **Avalonia.Desktop** (v11.0.10)
- **Avalonia.Themes.Fluent** (v11.0.10)
- **Avalonia.Fonts.Inter** (v11.0.10)
- **Avalonia.ReactiveUI** (v11.0.10)
- **Avalonia.Diagnostics** (v11.0.10) — *Debug configuration only*
- **LibGit2Sharp** (v0.30.0)
- **SharpCompress** (v0.37.2)

### COM References
The project also relies on the following COM components:
- **Shell32** (GUID: `50a7e9b0-70ef-11d1-b75a-00a0c90564fe`)
- **IWshRuntimeLibrary** (GUID: `f935dc20-1cf0-11d0-adb9-00c04fd58a0b`)

Ensure these COM components are registered on the system where the application is running. If not, you may need to manually register the corresponding DLLs using `regsvr32 <path/to/dll>`.

### Other
- [Git for Windows](https://git-scm.com/download/win): for optional Git integration.


## Building from source
1) Install the .NET build tools using the installer Visual Studio Build Tools Installer.
2) Run the [build.bat](src/build.bat) file in the src directory.

[User Guide](docs/UserGuide.md)
---
[Programming Documentation](docs/ProgrammingDocumentation.md)
---