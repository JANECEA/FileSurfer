<p align="center">
  <img src=".github/images/FileSurfer-logo.png" alt="FileSurfer Logo" height="70">
</p>
<h1 align="center">FileSurfer</h1>
<h3 align="center">A Modern, Open-Source File Explorer for Linux and Windows</h3>

---

FileSurfer is an open-source file explorer for Linux and Windows built with Avalonia UI.
FileSurfer is designed for convenience and efficiency, targeting more technical users while still providing a familiar and modern interface.
I've also included features that I personally find useful, even if they're not commonly found in other file explorers.
Your feedback is very welcome: if you encounter any bugs or have suggestions, please open an issue here on GitHub!

---

## ✨ **Features**  
All what you'd expect from a file manager plus some more:  

- **Image Pasting from Clipboard**: Paste images directly from your clipboard into folders.  
- **Batch Renaming**: Rename multiple files or directories at once.  
- **Undo & Redo**: Reverse most file operations with a simple undo/redo feature.  
- **Saving Last Opened Directory**: Pick up where you left off.
- **Git Integration**: Speed up basic interactions with Git repositories directly from the app.  

---

## 🖼️ **Screenshots**  
![Screenshot 1](.github/images/darkUI.png)  
*Dark interface using the list view in a Git repository.*

![Screenshot 1](.github/images/lightUI.png)  
*Light interface using the icon view.*

---

## 🚀 **Getting Started**  

### **On Linux**
Make sure to have the following command line tools installed!:
- [**trash-cli**](https://github.com/andreafrancia/trash-cli): for interacting with the system trash.
- [**resvg**](https://github.com/linebender/resvg): for handling svg icons.
- [**xdg-mime**](https://linux.die.net/man/1/xdg-mime): for resolving file mimetypes (most likely already installed).

### **Download and Install**  
1. Download the latest release from the [Releases Page](https://github.com/JANECEA/FileSurfer/releases/latest).  
2. Just extract the `.zip`, open the executable file, and you're all set!

### **Build from Source**  
If you want to customize FileSurfer, you can build it from source. Check out the 
<br>
[Building from Source](#️-building-from-source) section for detailed instructions.  

---

## 🛠️ **Building from Source**  

### **On Windows**
To build FileSurfer from source on Windows, you'll need the following:  

#### **Prerequisites**  
- [**.NET 8.0 SDK**](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).
- [**Git for Windows**](https://git-scm.com/install/windows): (optional, for Git integration)  

#### **Steps**  
1. Install the [.NET Build Tools](https://visualstudio.microsoft.com/cs/visual-cpp-build-tools/).  
2. Open the *"Developer Command Prompt for VS 2022"* app on your computer.  
3. Run the following commands:  
   ```pwsh  
   dotnet restore <path to FileSurfer.Windows.csproj>  
   msbuild <path to FileSurfer.sln> /t:publish /p:Configuration=Release /p:DeployOnBuild=true  
   ```  
2. Locate the compiled executable at: ` "...\src\FileSurfer.Windows\bin\Release\net8.0-windows\FileSurfer.exe" `

*(You can also use Visual Studio 2022 to build the project.)*  

---

### **On Linux**
To build FileSurfer from source on Windows, you'll need the following:  

#### **Prerequisites**  
- [**.NET 8.0 SDK**](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).
- [**Git for Linux**](https://git-scm.com/install/linux): (optional, for Git integration)  

#### **To Run**:
- [**trash-cli**](https://github.com/andreafrancia/trash-cli): for interacting with the system trash.
- [**resvg**](https://github.com/linebender/resvg): for handling svg icons.
- [**xdg-mime**](https://linux.die.net/man/1/xdg-mime): for resolving file mimetypes (most likely already installed).

#### **Steps**  
1. Run the following commands:  
   ```bash  
   dotnet build  <path to FileSurfer.Linux.csproj> -c Release
   ```  
2. Locate the compiled executable at: ` ".../src/FileSurfer.Linux/bin/Release/net8.0/FileSurfer" `

---

## 📚 **Documentation**  

- **[User Guide](docs/UserGuide/UserGuide.md)**: Learn how to use FileSurfer's features.  
- **[Specification](docs/Specification/)**: Check out the [technical](docs/Specification/TechnicalSpecification.md) and [functional](docs/Specification/FunctionalSpecification.md) specification of the project.  
- **[Technical Documentation](docs/Documentation/Overview.md)**: Take a look at the technical details of the project.
- **[Doxygen](https://janecea.github.io/FileSurfer/)**: Look around the generated doxygen documentation.

---

## 📦 **Dependencies**

FileSurfer relies on the following dependencies:  

### **Common**

#### .NET and Frameworks
- .NET 8.0  
- Avalonia UI  

#### NuGet Packages
- Avalonia (v11.3.9)  
- Avalonia.Desktop (v11.3.9)  
- Avalonia.Themes.Fluent (v11.3.9)  
- Avalonia.Fonts.Inter (v11.3.9)  
- ReactiveUI.Avalonia (v11.3.8)  
- LibGit2Sharp (v0.31.0)  
- SharpCompress (v0.41.0)  

### **Windows**

#### .NET and Frameworks
- Windows Forms

#### COM References
- Shell32 (GUID: `50a7e9b0-70ef-11d1-b75a-00a0c90564fe`)  
- IWshRuntimeLibrary (GUID: `f935dc20-1cf0-11d0-adb9-00c04fd58a0b`)  

### **Linux**

#### NuGet Packages
- Mime-Detective (v25.8.1)  
- Mime-Detective.Definitions.Exhaustive (v25.8.1)  
- Mono.Posix.NETStandard (v1.0.0)  