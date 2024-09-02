# FileSurfer - Project Overview


## Project Structure
FileSurfer aims to follow the MVVM design pattern familiar to Avalonia UI.
### Files and Directories
- **Interfaces/**
  - **IFileIOHandler.cs**
    - Defines methods for handling file and directory operations.
  - **IUndoableFileOperation.cs**
    - Represents an undoable file operation within the FileSurfer application.
  - **IVersionControl.cs**
    - Defines methods for interacting with a version control system.
- **Models/**
  - **ArchiveManager.cs**
    - Manages interactions with archive files using SharpCompress.
  - **ClipboardManager.cs**
    - Handles interactions with the Windows clipboard for file operations.
  - **FileNameGenerator.cs**
    - Provides functionality for file and directory name validation and generation.
  - **GitVersionControlHandler.cs**
    - Integrates Git version control features within FileSurfer.
  - **UndoableFileOperations.cs**
    - Contains classes implementing *IUndoableFileOperation*.
  - **UndoRedoHandler.cs**
    - Manages undo and redo operations, tracking history of file operations.
  - **WindowsFileIOHandler.cs**
    - Handles file I/O operations specific to the Windows environment.
  - **WindowsFileRestorer.cs**
    - Interacts with the Windows Shell to restore deleted files and directories.
  - **WindowsShellHandler.cs**
    - Provides methods to interact with Windows Shell features and APIs.
- **ViewModels/**
  - **MainWindowViewModel.cs**
    - The ViewModel for the main window of the FileSurfer application. Manages the data-binding logic between the UI and the application's core logic.
- **Views/**
  - **ErrorWindow.axaml.cs**
    - Represents an error dialog in the FileSurfer application.
  - **MainWindow.axaml.cs**
    - Defines the main window of the FileSurfer application, serving as the primary user interface.
- **App.axaml.cs**
  - Entry point for the FileSurfer application, responsible for initializing the application-wide settings and resources.
- **FileSurferSettings.cs**
  - Manages application settings, including user preferences and configuration.
- **FileSystemEntry.cs**
  - Represents individual file system entries (files, directories, or drives) in FileSurfer. Encapsulates properties such as name, size, and type.
- **Program.cs**
  - The main entry point for the FileSurfer application.
- **ViewLocator.cs**
  - Helps in locating and instantiating view models corresponding to views, following a naming convention.



## Classes and interfaces
**FileSurfer**
- **Models**
  - UndoableFileOperations
    - IUndoableFileOperation
    - CopyFilesTo
    - DuplicateFiles
    - MoveFilesTo
    - MoveFilesToTrash
    - NewDirAs
    - NewFileAs
    - RenameMultiple
    - RenameOne
  - IFileIOHandler
  - IVersionControl
  - ArchiveManager
  - ClipboardManager
  - FileNameGenerator
  - GitVersionControlHandler
  - UndoRedoHandler
    - UndoRedoNode
  - WindowsFileIOHandler
  - WindowsFileProperties
  - WindowsFileRestorer
  - WindowsShellHandler

- **ViewModels**
  - MainWindowViewModel

- **Views**
  - ErrorWindow
  - MainWindow
  
- App
- FileSurferSettings
- FileSystemEntry
- Program
- ViewLocator



## Namespaces

**FileSurfer**: Encapsulates the core functionality of the application. Contains sub-namespaces: models, view models, and views.

- **FileSurfer.Models**: Contains all model classes, which represent the apps business logic, such as file operations, clipboard management, and version control integration.
  - **FileSurfer.Models.UndoableFileOperations**: A specialized sub-namespace containing classes which implement *IUndoableFileOperation*.

- **FileSurfer.ViewModels**: Contains ViewModel classes, which represent a layer between the views (UI) and the models, implementing the logic that binds the data to the user interface.

- **FileSurfer.Views**: Includes all the view classes, which define the user interface elements of the application, such as windows and dialog boxes.



## Future goals and improvements
- Add support for file dragging.
- Add support for rectangular selection of files.
- Add preview icons for image files.
- View native Windows file context menus.
- Introduce more view modes.
- Add interactive settings window.
- Add file content searching.