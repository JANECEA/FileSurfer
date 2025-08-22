# Functional Specification

## Overview  
FileSurfer is a file explorer for Windows with a graphical interface built with **C#** on the **Avalonia UI** platform.
 The user interface is similar to that of the Windows 11 file explorer, while providing a few new features.

## Features  

### 1. Navigation  
- **Displaying directory contents**: Listing files and subdirectories in the main panel  
  - Allows listing files vertically or in a grid  
- **Browsing directories**: Using the path panel, quick-access sidebar, and clicking on folders  
- **Navigation**: Back, forward, and up one level using toolbar buttons and keyboard shortcuts  
- **Searching for files and directories**: Via the search bar in the top toolbar  
- **Status bar**: Displays data such as the number of (selected) files and their total size  

### 2. Quick Access  
- **User-added folders and files**: Allows users to add, remove, and reorder items in quick access  
- **Windows special folders**: Such as Pictures, Downloads, etc.  
- **Listing of computer drives**  

### 3. File Operations  
- **Creation**: New files and directories  
- **Moving and copying**: Support for cut, copy, and paste operations  
- **Renaming**: Files and folders  
- **Deletion**: Moving to the recycle bin or permanent deletion of files and folders  
- **Archive handling**: Archiving and extraction – supports multiple formats like `.zip`, `.rar`, etc.  
- **Sorting**: Four sorting modes with the option to reverse the order  
- **Pasting images from clipboard**  
- **Flatten folder**: Moves all contents of a folder up one level and then deletes the folder  
- **Undo and Redo**: For most file operations  

### 4. Git Integration  
- **Branch selection**: Switching between repository branches  
- **Pulling and pushing changes**: Synchronization with a remote repository  
- **Staging changes**: Via checkboxes next to modified files and folders  
- **Committing changes**: Confirming changes via file commits  

### 5. Context Menu  
- Less frequently used functions are activated via the context menu on files.  

### 6. Keyboard Shortcuts  
| **Action**                               | **Shortcut**                                  |
|------------------------------------------|---------------------------------------------- |
| **Go to previous directory**             | `Alt+LeftArrow`, `Side mouse button back`     |
| **Go to next directory**                 | `Alt+RightArrow`, `Side mouse button forward` |
| **Go up one directory**                  | `Alt+ArrowUp`, `Double tap empty space`       |
| **Reload current directory**             | `F5`                                          |
| **Open current directory in PowerShell** | `F12`                                         |
| **Focus the Search Bar**                 | `Ctrl+F`                                      |
| **New File**                             | `Ctrl+N`                                      |
| **New Directory**                        | `Ctrl+Shift+N`                                |
| **Cut**                                  | `Ctrl+X`                                      |
| **Copy**                                 | `Ctrl+C`                                      |
| **Paste**                                | `Ctrl+V`                                      |
| **Rename**                               | `F2`                                          |
| **Delete**                               | `Delete`                                      |
| **Delete permanently**                   | `Shift+Delete`                                |
| **Undo**                                 | `Ctrl+Z`                                      |
| **Redo**                                 | `Ctrl+Y`                                      |
| **Select all**                           | `Ctrl+A`                                      |
| **Select none**                          | `Ctrl+Shift+L`                                |
| **Invert selection**                     | `*`                                           |
| **Open path**                            | `Middle mouse button`                         |

### 7. Configuration  
FileSurfer provides an interactive window for adjusting settings. Data is saved in the `settings.json` file:  
```json
{
  "useDarkMode": true,                      // Choose Dark mode or Light mode
  "openInLastLocation": true,               // Updates "openIn" dynamically
  "openIn": "C:\\Users\\User\\Downloads",   // The directory, which the app will open in
  "fileSizeDisplayLimit": 4096,             // Numerical limit before FileSurfer uses the next byte unit
  "displayMode": "ListView",                // Specifies how files are displayed. Available options are: ListView, IconView
  "defaultSort": "Name",                    // Specifies the sorting mode. Available options are: Name, Date, Type, Size
  "sortReversed": false,                    // Displays contents in reverse order according to the current sorting mode
  "showSpecialFolders": true,               // Shows special folders (such as Music or Downloads) in the sidebar
  "showProtectedFiles": false,              // Shows files protected by the OS in directory contents
  "showHiddenFiles": true,                  // Shows hidden files in directory contents
  "treatDotFilesAsHidden": true,            // Considers files and directories starting with '.' as hidden by the OS
  "gitIntegration": true,                   // Turns on git integration with automatic detection for git repositories
  "showUndoRedoErrorDialogs": true,         // Shows or hides errors from undo / redo operations
  "automaticRefresh": true,                 // Automatically refreshes directory contents
  "automaticRefreshInterval": 3000,         // Specifies how often an automatic refresh should occur (in milliseconds) 
  "allowImagePastingFromClipboard": true,   // Allows pasting images to directories from the system clipboard
  "newImageName": "New Image",              // Name of the pasted image
  "newFileName": "New File",                // Name of a newly created file
  "newDirectoryName": "New Folder",         // Name of a newly created directory
  "thisPCLabel": "This PC",                 // What "This PC" 'directory' will be called
  "notepadApp": "notepad.exe",              // The application, the 'Open in Notepad' context menu option will open
  "quickAccess": []                         // Paths to your quick access items will be stored here
}
```