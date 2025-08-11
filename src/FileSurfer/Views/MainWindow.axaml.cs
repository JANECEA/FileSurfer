using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.VisualTree;
using FileSurfer.ViewModels;

namespace FileSurfer.Views;

/// <summary>
/// Represents the main <see cref="FileSurfer"/> window.
/// <para>
/// Handles various tasks such as resolving key presses and element focus withing the <see cref="FileSurfer"/> app.
/// </para>
/// </summary>
public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;
    private WrapPanel? _filePanel;
    private readonly DataTemplate _iconViewTemplate;
    private readonly DataTemplate _listViewTemplate;
    private readonly KeyBinding _selectAllKB;
    private readonly KeyBinding _invertSelection;
    private readonly KeyGesture _deleteGesture = KeyGesture.Parse("Delete");
    private readonly KeyGesture _cutGesture = KeyGesture.Parse("Ctrl+X");
    private readonly KeyGesture _copyGesture = KeyGesture.Parse("Ctrl+C");
    private readonly KeyGesture _pasteGesture = KeyGesture.Parse("Ctrl+V");

    /// <summary>
    /// Initializes a new <see cref="MainWindow"/>.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        if (
            Resources["ListViewTemplate"] is not DataTemplate listViewTemplate
            || Resources["IconViewTemplate"] is not DataTemplate iconViewTemplate
        )
            throw new ArgumentNullException();

        _listViewTemplate = listViewTemplate;
        _iconViewTemplate = iconViewTemplate;
        _selectAllKB = KeyBindings.First(keyBinding =>
            keyBinding.Gesture is { KeyModifiers: KeyModifiers.Control, Key: Key.A }
        );
        _invertSelection = KeyBindings.First(keyBinding => keyBinding.Gesture.Key == Key.Multiply);
    }

    /// <summary>
    /// Sets <see cref="_viewModel"/> after <see cref="MainWindowViewModel"/> has been loaded
    /// as <see cref="MainWindow"/>'s <see cref="StyledElement.DataContext"/>.
    /// </summary>
    private void ViewModelLoaded(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && _viewModel is null)
        {
            _viewModel = viewModel;
            SpecialFoldersLoaded(viewModel);
        }
    }

    /// <summary>
    /// Determines if <see cref="SpecialsListBox"/> should be visible after it has been loaded.
    /// </summary>
    private void SpecialFoldersLoaded(MainWindowViewModel viewModel)
    {
        if (viewModel.SpecialFolders.Length > 0)
        {
            SecondSeparator.IsVisible = true;
            SpecialsListBox.IsVisible = true;
        }
    }

    /// <summary>
    /// Determines the current view mode based on <see cref="FileSurferSettings.DisplayMode"/>
    /// after the <see cref="WrapPanel"/> holding current directory contents has been loaded.
    /// </summary>
    private void WrapPanelLoaded(object sender, RoutedEventArgs e)
    {
        if (FileSurferSettings.DisplayMode is DisplayMode.IconView)
            IconView();
    }

    /// <summary>
    /// Recursively finds <see cref="WrapPanel"/> within the visual elements.
    /// <para>
    /// This function is necessary because <see cref="_filePanel"/> can't be accessed via <c>x:Name</c>.
    /// </para>
    /// </summary>
    private WrapPanel? FindWrapPanel(Control parent)
    {
        foreach (Visual child in parent.GetVisualChildren())
        {
            if (child is WrapPanel panel)
            {
                _filePanel = panel;
                return panel;
            }

            if (child is Control control)
            {
                if (FindWrapPanel(control) is WrapPanel result)
                    return result;
            }
        }
        return null;
    }

    /// <summary>
    /// Determines the visibility of <see cref="QuickAccessListBox"/> based on its number of items.
    /// </summary>
    private void OnQuickAccessChanged(object sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_viewModel?.QuickAccess.Count > 0)
        {
            FirstSeparator.IsVisible = true;
            QuickAccessListBox.IsVisible = true;
        }
    }

    /// <summary>
    /// Opens the item that was double tapped or goes to the parent directory of the current directory.
    /// </summary>
    private void FilesDoubleTapped(object sender, TappedEventArgs e)
    {
        if (sender is ListBox listBox)
        {
            Visual? hitElement = (Visual?)listBox.InputHitTest(e.GetPosition(listBox));
            while (hitElement is not null)
            {
                if (hitElement is CheckBox)
                    return;

                hitElement = hitElement.GetVisualParent();
            }

            if (listBox.SelectedItem is FileSystemEntry entry)
                _viewModel?.OpenEntry(entry);
            else
                _viewModel?.GoUp();
        }
    }

    private void OpenClicked(object sender, RoutedEventArgs e) => _viewModel?.OpenEntries();

    private void OpenInNotepad(object sender, RoutedEventArgs e) => _viewModel?.OpenInNotepad();

    private void PinToQuickAccess(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: FileSystemEntry entry })
            _viewModel?.AddToQuickAccess(entry);
    }

    private void MoveUp(object sender, RoutedEventArgs e) =>
        _viewModel?.MoveUp(QuickAccessListBox.SelectedIndex);

    private void MoveDown(object sender, RoutedEventArgs e) =>
        _viewModel?.MoveDown(QuickAccessListBox.SelectedIndex);

    private void RemoveFromQuickAccess(object sender, RoutedEventArgs e) =>
        _viewModel?.RemoveFromQuickAccess(QuickAccessListBox.SelectedIndex);

    private void AddToArchive(object sender, RoutedEventArgs e) => _viewModel?.AddToArchive();

    private void ExtractArchive(object sender, RoutedEventArgs e) => _viewModel?.ExtractArchive();

    private void CopyPath(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: FileSystemEntry entry })
            _viewModel?.CopyPath(entry);
    }

    private void Cut(object sender, RoutedEventArgs e) => _viewModel?.Cut();

    private void Copy(object sender, RoutedEventArgs e) => _viewModel?.Copy();

    private void CreateShortcut(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: FileSystemEntry entry })
            _viewModel?.CreateShortcut(entry);
    }

    private void FlattenFolder(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: FileSystemEntry entry })
            _viewModel?.FlattenFolder(entry);
    }


    private void Delete(object sender, RoutedEventArgs e) => _viewModel?.MoveToTrash();

    private void DeletePermanently(object sender, RoutedEventArgs e) => _viewModel?.Delete();

    private void ShowProperties(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: FileSystemEntry entry })
            _viewModel?.ShowProperties(entry);
    }

    private void OpenAs(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: FileSystemEntry entry })
            _viewModel?.OpenAs(entry);
    }

    /// <summary>
    /// Clears the selection if the user clicks on empty space.
    /// </summary>
    private void FilesTapped(object sender, TappedEventArgs e)
    {
        if (sender is not ListBox listBox)
            return;

        Visual? hitElement = (Visual?)listBox.InputHitTest(e.GetPosition(listBox));
        while (hitElement is not null)
        {
            if (hitElement is ListBoxItem)
                return;

            hitElement = hitElement.GetVisualParent();
        }
        _viewModel?.SelectedFiles.Clear();
        NewNameBar.IsVisible = false;
        CommitMessageBar.IsVisible = false;
    }

    /// <summary>
    /// Clears the selection if any SideBar item has been clicked.
    /// </summary>
    private void SideBarEntryClicked(object sender, TappedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: FileSystemEntry entry })
        {
            _viewModel?.OpenEntry(entry);
            SpecialsListBox.SelectedItems?.Clear();
            QuickAccessListBox.SelectedItems?.Clear();
            DrivesListBox.SelectedItems?.Clear();
        }
    }

    /// <summary>
    /// Handles Middle and Side button interactions with <see cref="FileSurfer"/>.
    /// </summary>
    private void MouseButtonPressed(object sender, PointerPressedEventArgs e)
    {
        PointerPointProperties properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsXButton1Pressed)
            _viewModel?.GoBack();

        if (properties.IsXButton2Pressed)
            _viewModel?.GoForward();

        if (_viewModel is not null && _viewModel.Searching && properties.IsMiddleButtonPressed)
        {
            Visual? hitElement = (Visual?)FileDisplay.InputHitTest(e.GetPosition(FileDisplay));
            while (hitElement is not null)
            {
                if (hitElement is ListBoxItem { DataContext: FileSystemEntry entry })
                {
                    _viewModel?.OpenEntryLocation(entry);
                    return;
                }
                hitElement = hitElement.GetVisualParent();
            }
        }
    }

    /// <summary>
    /// Unbinds interfering keybindings when the user starts typing.
    /// </summary>
    private void TextBoxGotFocus(object? sender = null, GotFocusEventArgs? e = null)
    {
        DeleteButton.HotKey = null;
        CutButton.HotKey = null;
        CopyButton.HotKey = null;
        PasteButton.HotKey = null;
        KeyBindings.Remove(_selectAllKB);
        KeyBindings.Remove(_invertSelection);
    }

    /// <summary>
    /// Rebinds interfering keybindings when the user stops typing.
    /// </summary>
    private void TextBoxLostFocus(object? sender = null, RoutedEventArgs? e = null)
    {
        DeleteButton.HotKey = _deleteGesture;
        CutButton.HotKey = _cutGesture;
        CopyButton.HotKey = _copyGesture;
        PasteButton.HotKey = _pasteGesture;
        KeyBindings.Add(_selectAllKB);
        KeyBindings.Add(_invertSelection);
    }

    /// <summary>
    /// Clears <see cref="SearchBox"/> when it looses focus.
    /// </summary>
    private void SearchBoxLostFocus(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        TextBoxLostFocus();
    }

    /// <summary>
    /// Shows <see cref="NewNameBar"/> and sets <see cref="NameInputBox"/> properties.
    /// </summary>
    private void OnRenameClicked(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || _viewModel.SelectedFiles.Count == 0)
            return;

        NewNameBar.IsVisible = true;
        NameInputBox.Focus();
        NameInputBox.Text = _viewModel.SelectedFiles[^1].Name;
        NameInputBox.SelectionStart = 0;
        NameInputBox.SelectionEnd = _viewModel.GetNameEndIndex(_viewModel.SelectedFiles[^1]);
    }

    /// <summary>
    /// Shows <see cref="CommitMessageBar"/> and focuses <see cref="CommitInputBox"/>.
    /// </summary>
    private void OnCommitClicked(object sender, RoutedEventArgs e)
    {
        CommitMessageBar.IsVisible = true;
        CommitInputBox.Focus();
    }

    /// <summary>
    /// Hides <see cref="NewNameBar"/> and <see cref="CommitMessageBar"/> when either loose focus.
    /// <para>
    /// Invokes <see cref="TextBoxLostFocus(object?, RoutedEventArgs?)"/>.
    /// </para>
    /// </summary>
    private void InputBoxLostFocus(object sender, RoutedEventArgs e)
    {
        NewNameBar.IsVisible = false;
        CommitMessageBar.IsVisible = false;
        TextBoxLostFocus();
    }

    /// <summary>
    /// Relays the new name to <see cref="_viewModel"/> and hides <see cref="NewNameBar"/>.
    /// </summary>
    private void NameEntered()
    {
        if (NameInputBox.Text is string newName)
        {
            _viewModel?.Rename(newName);
            NewNameBar.IsVisible = false;
        }
    }

    /// <summary>
    /// Relays the commit message to <see cref="_viewModel"/>,
    /// hides <see cref="CommitMessageBar"/>,
    /// and clears <see cref="CommitInputBox"/> text.
    /// </summary>
    private void CommitMessageEntered()
    {
        if (CommitInputBox.Text is string commitMessage)
        {
            _viewModel?.Commit(commitMessage);
            CommitMessageBar.IsVisible = false;
            CommitInputBox.Text = string.Empty;
        }
    }

    /// <summary>
    /// Invoked after the checkbox is toggled. Relays the action to <see cref="_viewModel"/>.
    /// </summary>
    private void StagedToggle(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { DataContext: FileSystemEntry entry } checkBox)
        {
            if (checkBox.IsChecked is true)
                _viewModel?.StageFile(entry);
            else
                _viewModel?.UnstageFile(entry);
        }
    }

    /// <summary>
    /// Switches the display mode to <see cref="DisplayMode.ListView"/>.
    /// </summary>
    private void ListView(object? sender = null, RoutedEventArgs? e = null)
    {
        WrapPanel? wrapPanel = _filePanel ?? FindWrapPanel(FileDisplay);
        if (wrapPanel is null)
            return;

        wrapPanel.Orientation = Avalonia.Layout.Orientation.Vertical;
        LabelsPanel.IsVisible = true;
        FileDisplay.ItemTemplate = _listViewTemplate;
        FileSurferSettings.DisplayMode = DisplayMode.ListView;
    }

    /// <summary>
    /// Switches the display mode to <see cref="DisplayMode.IconView"/>.
    /// </summary>
    private void IconView(object? sender = null, RoutedEventArgs? e = null)
    {
        WrapPanel? wrapPanel = _filePanel ?? FindWrapPanel(FileDisplay);
        if (wrapPanel is null)
            return;

        wrapPanel.Orientation = Avalonia.Layout.Orientation.Horizontal;
        LabelsPanel.IsVisible = false;
        FileDisplay.ItemTemplate = _iconViewTemplate;
        FileSurferSettings.DisplayMode = DisplayMode.IconView;
    }

    /// <summary>
    /// Opens the <see cref="SettingsWindow"/> in a dialog mode with <see cref="MainWindow"/> as the owner.
    /// </summary>
    private void OpenSettings(object sender, RoutedEventArgs e)
    {
        SettingsWindow settingsWindow = new();
        settingsWindow.ShowDialog(this);  
    }

    /// <summary>
    /// Handles key presses without keybindings.
    /// </summary>
    private void KeyPressed(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            OnEnterPressed(e);

        if (e.Key == Key.Escape)
            OnEscapePressed(e);

        if (e is { Key: Key.F, KeyModifiers: KeyModifiers.Control })
            OnCtrlFPressed(e);
    }

    /// <summary>
    /// Toggles focus on <see cref="SearchBox"/>.
    /// </summary>
    private void OnCtrlFPressed(KeyEventArgs e)
    {
        e.Handled = true;
        if (SearchBox.IsFocused)
            FocusManager?.ClearFocus();
        else
            SearchBox.Focus();
    }

    /// <summary>
    /// Processes currently focused elements and invokes their respective actions.
    /// </summary>
    private void OnEnterPressed(KeyEventArgs e)
    {
        e.Handled = true;
        if (NewNameBar.IsVisible || CommitMessageBar.IsVisible)
        {
            if (NewNameBar.IsVisible)
                NameEntered();
            if (CommitMessageBar.IsVisible)
                CommitMessageEntered();

            FocusManager?.ClearFocus();
            return;
        }

        if (PathBox.IsFocused)
        {
            FocusManager?.ClearFocus();
            return;
        }

        if (_viewModel is not null)
        {
            if (SearchBox.IsFocused && !string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                _viewModel?.SearchAsync(SearchBox.Text);
                return;
            }
            if (_viewModel.SelectedFiles.Count == 1)
                _viewModel?.OpenEntry(_viewModel.SelectedFiles[0]);
        }
    }

    /// <summary>
    /// Empties selection, cancels searching and clears focus.
    /// </summary>
    private void OnEscapePressed(KeyEventArgs e)
    {
        e.Handled = true;
        _viewModel?.SelectedFiles.Clear();
        if (SearchBox.IsFocused)
            _viewModel?.CancelSearch();

        FocusManager?.ClearFocus();
    }

    /// <summary>
    /// Invokes <see cref="FileSurferSettings.UpdateQuickAccess(System.Collections.Generic.IEnumerable{FileSystemEntry})"/>
    /// and <see cref="FileSurferSettings.SaveSettings"/>,
    /// <para>
    /// and also disposes of <see cref="_viewModel"/>'s resources after the app closes.
    /// </para>
    /// </summary>
    private void OnClosing(object sender, WindowClosingEventArgs e)
    {
        if (_viewModel is not null)
        {
            FileSurferSettings.UpdateQuickAccess(_viewModel.QuickAccess);
            _viewModel.DisposeResources();
        }
        FileSurferSettings.SaveSettings();
    }
}
