using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.VisualTree;
using FileSurfer.Core.ViewModels;

namespace FileSurfer.Core.Views;

/// <summary>
/// Represents the main <see cref="FileSurfer"/> window.
/// <para>
/// Handles various tasks such as resolving key presses and element focus withing the <see cref="FileSurfer"/> app.
/// </para>
/// </summary>
public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;

    private readonly KeyBinding _selectAllKb;
    private readonly KeyBinding _invertSelection;
    private readonly KeyGesture _deleteGesture = KeyGesture.Parse("Delete");
    private readonly KeyGesture _cutGesture = KeyGesture.Parse("Ctrl+X");
    private readonly KeyGesture _copyGesture = KeyGesture.Parse("Ctrl+C");
    private readonly KeyGesture _pasteGesture = KeyGesture.Parse("Ctrl+V");

    private readonly DataTemplate _iconViewTemplate;
    private readonly DataTemplate _listViewTemplate;
    private readonly DataTemplate _searchViewTemplate;
    private readonly ItemsPanelTemplate _listViewPanel;
    private readonly ItemsPanelTemplate _iconViewPanel;

    private DataTemplate _previousTemplate;
    private ItemsPanelTemplate _previousPanel;

    public MainWindow()
    {
        InitializeComponent();

        if (
            Resources["ListViewTemplate"] is not DataTemplate listViewTemplate
            || Resources["IconViewTemplate"] is not DataTemplate iconViewTemplate
            || Resources["SearchViewTemplate"] is not DataTemplate searchViewTemplate
            || Resources["ListViewPanel"] is not ItemsPanelTemplate listViewPanel
            || Resources["IconViewPanel"] is not ItemsPanelTemplate iconViewPanel
        )
            throw new UnreachableException();

        _listViewTemplate = listViewTemplate;
        _iconViewTemplate = iconViewTemplate;
        _searchViewTemplate = searchViewTemplate;
        _listViewPanel = listViewPanel;
        _iconViewPanel = iconViewPanel;

        _previousPanel = listViewPanel;
        _previousTemplate = listViewTemplate;

        _selectAllKb = KeyBindings.First(keyBinding =>
            keyBinding.Gesture is { KeyModifiers: KeyModifiers.Control, Key: Key.A }
        );
        _invertSelection = KeyBindings.First(keyBinding => keyBinding.Gesture.Key == Key.Multiply);

        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    private void ViewModelLoaded(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && _viewModel is null)
            _viewModel = viewModel;

        ClearFocus();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MainWindowViewModel vm)
            vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowViewModel.Searching) || _viewModel is null)
            return;

        if (_viewModel.Searching)
        {
            FileDisplay.ItemTemplate = _searchViewTemplate;
            FileDisplay.ItemsPanel = _listViewPanel;
        }
        else
        {
            FileDisplay.ItemTemplate = _previousTemplate;
            FileDisplay.ItemsPanel = _previousPanel;
            SearchBox.Text = string.Empty;
        }
    }

    private void OnSpecialFoldersChanged(object sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_viewModel is null)
            return;

        bool show = _viewModel.SpecialFolders.Count > 0;
        SecondSeparator.IsVisible = show;
        SpecialsListBox.IsVisible = show;
        SpecialsLabel.IsVisible = show;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (FileSurferSettings.DisplayMode is DisplayMode.IconView)
            IconView();
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

            if (listBox.SelectedItem is FileSystemEntryViewModel entry)
                _viewModel?.OpenEntry(entry.FileSystemEntry);
            else
                _viewModel?.GoUp();
        }
    }

    private void MoveUpQuickAccess(object sender, RoutedEventArgs e) =>
        ListBoxHelper.MoveUp(QuickAccessListBox, _viewModel!.QuickAccess);

    private void MoveDownQuickAccess(object sender, RoutedEventArgs e) =>
        ListBoxHelper.MoveDown(QuickAccessListBox, _viewModel!.QuickAccess);

    private void RemoveFromQuickAccess(object sender, RoutedEventArgs e) =>
        ListBoxHelper.Remove(QuickAccessListBox, _viewModel!.QuickAccess);

    private void MoveUpSftp(object sender, RoutedEventArgs e) =>
        ListBoxHelper.MoveUp(SftpListBox, _viewModel!.SftpConnectionsVms);

    private void MoveDownSftp(object sender, RoutedEventArgs e) =>
        ListBoxHelper.MoveDown(SftpListBox, _viewModel!.SftpConnectionsVms);

    private void RemoveSftp(object sender, RoutedEventArgs e) =>
        ListBoxHelper.Remove(SftpListBox, _viewModel!.SftpConnectionsVms);

    private void EditSftpConnection(object sender, RoutedEventArgs e)
    {
        if (SftpListBox.SelectedItem is not SftpConnectionViewModel connectionVm)
            return;

        EditSftpWindow window = new() { ViewModel = connectionVm };
        window.ShowDialog(this);
    }

    private void CloseSftp(object sender, RoutedEventArgs e)
    {
        if (SftpListBox.SelectedItem is SftpConnectionViewModel connectionVm)
            _viewModel?.CloseSftpConnection(connectionVm);
    }

    private void OnAddSftpButtonClicked(object sender, RoutedEventArgs e)
    {
        SftpConnectionViewModel viewModel = new(vm => _viewModel?.SftpConnectionsVms.Add(vm));
        EditSftpWindow window = new() { ViewModel = viewModel };
        window.ShowDialog(this);
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

    private void SideBarEntryClicked(object sender, TappedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: SideBarEntryViewModel entry })
            _viewModel?.OpenLocalEntry(entry);

        ClearFocus();
    }

    private void SftpEntryClicked(object sender, TappedEventArgs e)
    {
        if (
            _viewModel is not null
            && sender is ListBox { SelectedItem: SftpConnectionViewModel connectionVm }
        )
            _ = _viewModel.OpenSftpConnection(connectionVm);

        ClearFocus();
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
        KeyBindings.Remove(_selectAllKb);
        KeyBindings.Remove(_invertSelection);
    }

    private void TextBoxLostFocus(object? sender = null, RoutedEventArgs? e = null)
    {
        DeleteButton.HotKey = _deleteGesture;
        CutButton.HotKey = _cutGesture;
        CopyButton.HotKey = _copyGesture;
        PasteButton.HotKey = _pasteGesture;
        KeyBindings.Add(_selectAllKb);
        KeyBindings.Add(_invertSelection);
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
    /// Invokes <see cref="TextBoxLostFocus"/>.
    /// </para>
    /// </summary>
    private void InputBoxLostFocus(object sender, RoutedEventArgs e)
    {
        NewNameBar.IsVisible = false;
        CommitMessageBar.IsVisible = false;
        TextBoxLostFocus();
    }

    /// <summary>
    /// Resets text in <see cref="PathBox"/>.
    /// <para>
    /// Invokes <see cref="TextBoxLostFocus"/>.
    /// </para>
    /// </summary>
    private void PathBoxLostFocus(object sender, RoutedEventArgs e)
    {
        PathBox.Text = _viewModel?.CurrentDir ?? string.Empty;
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
            _viewModel?.GitCommit(commitMessage.Trim());
            CommitMessageBar.IsVisible = false;
            CommitInputBox.Text = string.Empty;
        }
    }

    /// <summary>
    /// Invoked after the checkbox is toggled. Relays the action to <see cref="_viewModel"/>.
    /// </summary>
    private void StagedToggle(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { DataContext: FileSystemEntryViewModel entry } checkBox)
        {
            if (checkBox.IsChecked is true)
                _viewModel?.GitStage(entry);
            else
                _viewModel?.GitUnstage(entry);
        }
    }

    private void ListView(object? sender = null, RoutedEventArgs? e = null)
    {
        FileDisplay.ItemsPanel = _previousPanel = _listViewPanel;
        FileDisplay.ItemTemplate = _previousTemplate = _listViewTemplate;
        FileSurferSettings.DisplayMode = DisplayMode.ListView;
    }

    private void IconView(object? sender = null, RoutedEventArgs? e = null)
    {
        FileDisplay.ItemsPanel = _previousPanel = _iconViewPanel;
        FileDisplay.ItemTemplate = _previousTemplate = _iconViewTemplate;
        FileSurferSettings.DisplayMode = DisplayMode.IconView;
    }

    private void OpenSettings(object sender, RoutedEventArgs e)
    {
        SettingsWindow settingsWindow = new();
        settingsWindow.ShowDialog(this);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OnEnterPressed(e);
            return;
        }
        if (e.Key == Key.Escape)
        {
            OnEscapePressed(e);
            return;
        }
        if (e is { KeyModifiers: KeyModifiers.Control, Key: Key.F })
        {
            OnCtrlFPressed(e);
            return;
        }

        if (
            e.Key is not (Key.Up or Key.Down or Key.Left or Key.Right)
            || FileDisplay.IsKeyboardFocusWithin
            || FocusManager?.GetFocusedElement() is TextBox
        )
            return;

        if (FileDisplay.ItemCount > 0)
        {
            FileDisplay.SelectedIndex = 0;
            FileDisplay.ContainerFromIndex(0)?.Focus();
        }
        e.Handled = true;
    }

    /// <summary>
    /// Toggles focus on <see cref="SearchBox"/>.
    /// </summary>
    private void OnCtrlFPressed(KeyEventArgs e)
    {
        e.Handled = true;
        if (SearchBox.IsFocused)
            ClearFocus();
        else
            SearchBox.Focus();
    }

    private void ClearFocus()
    {
        FocusSink.Focus();
        SpecialsListBox.SelectedItems?.Clear();
        QuickAccessListBox.SelectedItems?.Clear();
        DrivesListBox.SelectedItems?.Clear();
        SftpListBox.SelectedItems?.Clear();
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

            return;
        }

        if (PathBox.IsFocused)
        {
            if (PathBox.Text is string path)
                _viewModel?.SetNewLocation(path);
            ClearFocus();
            return;
        }

        if (_viewModel is not null)
        {
            if (SearchBox.IsFocused && !string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                _viewModel?.SearchAsync(SearchBox.Text);
                ClearFocus();
            }
            else if (_viewModel.SelectedFiles.Count == 1)
                _viewModel?.OpenEntry(_viewModel.SelectedFiles[0].FileSystemEntry);
        }
    }

    private void OnEscapePressed(KeyEventArgs e)
    {
        e.Handled = true;
        _viewModel?.SelectedFiles.Clear();
        ClearFocus();
    }

    private void OnClosing(object? sender, WindowClosingEventArgs? e)
    {
        if (_viewModel is not null)
        {
            _viewModel.CloseApp();
            _viewModel.Dispose();
        }
        FileSurferSettings.SaveSettings();
    }
}
