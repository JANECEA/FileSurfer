using Avalonia.Controls;
using Avalonia.Media;
using FileSurfer.Core.ViewModels;
using FileSurfer.Core.Views;
using FileSurfer.Linux.Models.Shell;
using FileSurfer.Linux.Views;
using ReactiveUI;

namespace FileSurfer.Linux.ViewModels;

/// <summary>
/// The PropertiesWindowViewModel is the ViewModel for the <see cref="PropertiesWindow"/>.
/// </summary>
public sealed class PropertiesWindowViewModel : IDisplayable
{
    private readonly FileSystemEntryViewModel _entry;
    private readonly Window _mainWindow;

    public IImage? Icon => _entry.Icon;
    public string FileName => _entry.Name;
    public string FullPath => _entry.PathToEntry;
    public string Type => _entry.Type;
    public required string Size { get; init; }
    public required string DateCreated { get; init; }
    public required string DateAccessed { get; init; }
    public required string DateModified { get; init; }
    public required string Owner { get; init; }
    public required string Permissions { get; init; }

    public PropertiesWindowViewModel(FileSystemEntryViewModel entry, Window mainWindow)
    {
        _entry = entry;
        _mainWindow = mainWindow;
    }

    public void Show()
    {
        PropertiesWindow window = new() { DataContext = this };
        window.ShowDialog(_mainWindow);
    }
}
