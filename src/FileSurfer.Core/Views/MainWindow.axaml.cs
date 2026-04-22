using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FileSurfer.Core.ViewModels;
using FileSurfer.Core.Views.Helpers;

namespace FileSurfer.Core.Views;

/// <summary>
/// Represents the main <see cref="FileSurfer"/> window.
/// <para/>
/// Handles various tasks such as resolving key presses and element focus withing the <see cref="FileSurfer"/> app.
/// </summary>
public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;

    private readonly Dictionary<Button, KeyGesture> _buttonHotKeys = new();
    private readonly List<KeyBinding> _keyBindings = new();
    private readonly KeyBinding? _backupDeleteKeyBinding;

    private readonly DataTemplate _iconViewTemplate;
    private readonly DataTemplate _listViewTemplate;
    private readonly DataTemplate _searchViewTemplate;
    private readonly ItemsPanelTemplate _listViewPanel;
    private readonly ItemsPanelTemplate _iconViewPanel;

    private DataTemplate _previousTemplate;
    private ItemsPanelTemplate _previousPanel;

    /// <summary>
    /// Initializes the main window, resolves required templates, and wires global key handling.
    /// </summary>
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

        _backupDeleteKeyBinding = KeyBindings.FirstOrDefault(kb => kb.Gesture.Key is Key.None);

        AddHandler(KeyDownEvent, TunnelKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// Caches the typed view model after the window data context is assigned.
    /// </summary>
    private void ViewModelLoaded(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && _viewModel is null)
            _viewModel = viewModel;

        ClearFocus();
    }

    /// <summary>
    /// Subscribes to property-change notifications when a new data context is assigned.
    /// </summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MainWindowViewModel vm)
            vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel is null)
            return;

        switch (e.PropertyName)
        {
            case nameof(MainWindowViewModel.Searching) when _viewModel.Searching:
                FileDisplay.ItemTemplate = _searchViewTemplate;
                FileDisplay.ItemsPanel = _listViewPanel;
                break;

            case nameof(MainWindowViewModel.Searching):
                FileDisplay.ItemTemplate = _previousTemplate;
                FileDisplay.ItemsPanel = _previousPanel;
                SearchBox.Text = string.Empty;
                break;

            case nameof(MainWindowViewModel.IsLocal) when _backupDeleteKeyBinding is not null:
                _backupDeleteKeyBinding.Gesture = _viewModel.IsLocal
                    ? new KeyGesture(Key.None)
                    : new KeyGesture(Key.Delete);
                break;
        }
    }

    /// <summary>
    /// Performs initial UI setup after the window is loaded.
    /// </summary>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        foreach (Button button in this.GetVisualDescendants().OfType<Button>())
        {
            ConstructToolTip(button);
            if (button.HotKey is not null)
                _buttonHotKeys[button] = button.HotKey;
        }

        _keyBindings.AddRange(KeyBindings);

        if (FileSurferSettings.DisplayMode is DisplayMode.IconView)
            IconView();
    }

    private static void ConstructToolTip(Button button)
    {
        KeyGesture? gesture = button.HotKey;
        if (gesture is null)
            return;

        object? tooltip = ToolTip.GetTip(button);
        object? content = button.Content;

        switch (tooltip)
        {
            case not null:
                ToolTip.SetTip(button, $"{tooltip} ({gesture})");
                break;
            case null when content is not null:
                ToolTip.SetTip(button, $"{content} ({gesture})");
                break;
            case null:
                ToolTip.SetTip(button, gesture.ToString());
                break;
        }
    }

    /// <summary>
    /// Navigates to the selected previous location from the back history flyout.
    /// </summary>
    private void GoBackTapped(object? sender, SelectionChangedEventArgs e)
    {
        GoBackButton.ContextFlyout?.Hide();
        GoForwardButton.ContextFlyout?.Hide();
        if (e.AddedItems is [LocationDisplay location])
            _viewModel?.GoBackCommand.Execute(location).Subscribe();
    }

    /// <summary>
    /// Navigates to the selected location from the forward history flyout.
    /// </summary>
    private void GoForwardTapped(object? sender, SelectionChangedEventArgs e)
    {
        GoBackButton.ContextFlyout?.Hide();
        GoForwardButton.ContextFlyout?.Hide();
        if (e.AddedItems is [LocationDisplay location])
            _viewModel?.GoForwardCommand.Execute(location).Subscribe();
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
                _viewModel?.OpenEntryCommand.Execute(entry).Subscribe();
            else
                _viewModel?.GoUpCommand.Execute().Subscribe();
        }
    }

    /// <summary>
    /// Moves the selected quick-access entry one position up.
    /// </summary>
    private void MoveUpQuickAccess(object sender, RoutedEventArgs e) =>
        ListBoxHelper.MoveUp(QuickAccessListBox, _viewModel!.QuickAccess);

    /// <summary>
    /// Moves the selected quick-access entry one position down.
    /// </summary>
    private void MoveDownQuickAccess(object sender, RoutedEventArgs e) =>
        ListBoxHelper.MoveDown(QuickAccessListBox, _viewModel!.QuickAccess);

    /// <summary>
    /// Removes the selected item from quick access.
    /// </summary>
    private void RemoveFromQuickAccess(object sender, RoutedEventArgs e) =>
        ListBoxHelper.Remove(QuickAccessListBox, _viewModel!.QuickAccess);

    /// <summary>
    /// Moves the selected SFTP connection one position up.
    /// </summary>
    private void MoveUpSftp(object sender, RoutedEventArgs e) =>
        ListBoxHelper.MoveUp(SftpListBox, _viewModel!.SftpConnectionsVms);

    /// <summary>
    /// Moves the selected SFTP connection one position down.
    /// </summary>
    private void MoveDownSftp(object sender, RoutedEventArgs e) =>
        ListBoxHelper.MoveDown(SftpListBox, _viewModel!.SftpConnectionsVms);

    /// <summary>
    /// Removes the selected SFTP connection from the saved list.
    /// </summary>
    private void RemoveSftp(object sender, RoutedEventArgs e) =>
        ListBoxHelper.Remove(SftpListBox, _viewModel!.SftpConnectionsVms);

    /// <summary>
    /// Opens the edit dialog for the currently selected SFTP connection.
    /// </summary>
    private void EditSftpConnection(object sender, RoutedEventArgs e)
    {
        if (SftpListBox.SelectedItem is not SftpConnectionViewModel connectionVm)
            return;

        EditSftpWindow window = new() { ViewModel = connectionVm };
        window.ShowDialog(this);
    }

    /// <summary>
    /// Closes the selected active SFTP connection.
    /// </summary>
    private void CloseSftp(object sender, RoutedEventArgs e)
    {
        if (SftpListBox.SelectedItem is SftpConnectionViewModel connectionVm)
            _viewModel?.CloseSftpCommand.Execute(connectionVm).Subscribe();
    }

    /// <summary>
    /// Opens the dialog used to create and add a new SFTP connection.
    /// </summary>
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

    /// <summary>
    /// Opens the selected sidebar location and clears transient selection focus.
    /// </summary>
    private void SideBarEntryClicked(object sender, TappedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: SideBarEntryViewModel entry })
            _viewModel?.OpenSideBarEntryCommand.Execute(entry).Subscribe();

        ClearFocus();
    }

    /// <summary>
    /// Opens the selected SFTP connection and clears transient selection focus.
    /// </summary>
    private void SftpEntryClicked(object sender, TappedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: SftpConnectionViewModel connectionVm })
            _viewModel?.OpenSftpCommand.Execute(connectionVm).Subscribe();

        ClearFocus();
    }

    /// <summary>
    /// Handles Middle and Side button interactions with <see cref="FileSurfer"/>.
    /// </summary>
    private void MouseButtonPressed(object sender, PointerPressedEventArgs e)
    {
        PointerPointProperties properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsXButton1Pressed)
            _viewModel?.GoBackCommand.Execute(null).Subscribe();

        if (properties.IsXButton2Pressed)
            _viewModel?.GoForwardCommand.Execute(null).Subscribe();
    }

    /// <summary>
    /// Shows <see cref="NewNameBar"/> and sets <see cref="NameInputBox"/> properties.
    /// </summary>
    private void OnRenameClicked(object sender, RoutedEventArgs e)
    {
        if (
            _viewModel is null
            || _viewModel.SelectedFiles.Count == 0
            || FileDisplay.SelectedItems is not IList list
            || list.Count == 0
            || list[^1] is not FileSystemEntryViewModel entry
        )
            return;

        NewNameBar.IsVisible = true;
        NameInputBox.Focus();
        NameInputBox.Text = entry.Name;

        NameInputBox.SelectionStart = 0;
        NameInputBox.SelectionEnd = entry.FileSystemEntry.NameWoExtension.Length;
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
    /// </summary>
    private void InputBarLostFocus(object sender, RoutedEventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            for (
                Visual? current = FocusManager?.GetFocusedElement() as Visual;
                current is not null;
                current = current.GetVisualParent()
            )
                if (ReferenceEquals(current, sender))
                    return;

            NewNameBar.IsVisible = false;
            CommitMessageBar.IsVisible = false;
        });

    /// <summary>
    /// Resets text in <see cref="PathBox"/>.
    /// </summary>
    private void PathBoxLostFocus(object sender, RoutedEventArgs e) =>
        PathBox.Text = _viewModel?.PathBoxText ?? string.Empty;

    /// <summary>
    /// Executes <see cref="MainWindowViewModel.RenameCommand"/> and hides the new name bar.
    /// </summary>
    private void NameEntered(object? sender = null, RoutedEventArgs? e = null)
    {
        if (NameInputBox.Text is string newName)
        {
            _viewModel?.RenameCommand.Execute(newName).Subscribe();
            NewNameBar.IsVisible = false;
            NameInputBox.Text = string.Empty;
        }
    }

    /// <summary>
    /// Executes <see cref="MainWindowViewModel.GitCommitCommand"/> and hides the commit message bar.
    /// </summary>
    private void CommitMessageEntered(object? sender = null, RoutedEventArgs? e = null)
    {
        if (CommitInputBox.Text is string commitMessage)
        {
            _viewModel?.GitCommitCommand.Execute(commitMessage).Subscribe();
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
                _viewModel?.GitStageCommand.Execute(entry).Subscribe();
            else
                _viewModel?.GitUnstageCommand.Execute(entry).Subscribe();
        }
    }

    /// <summary>
    /// Switches file display to list mode and persists the selected mode.
    /// </summary>
    private void ListView(object? sender = null, RoutedEventArgs? e = null)
    {
        FileDisplay.ItemsPanel = _previousPanel = _listViewPanel;
        FileDisplay.ItemTemplate = _previousTemplate = _listViewTemplate;
        FileSurferSettings.DisplayMode = DisplayMode.ListView;
    }

    /// <summary>
    /// Switches file display to icon mode and persists the selected mode.
    /// </summary>
    private void IconView(object? sender = null, RoutedEventArgs? e = null)
    {
        FileDisplay.ItemsPanel = _previousPanel = _iconViewPanel;
        FileDisplay.ItemTemplate = _previousTemplate = _iconViewTemplate;
        FileSurferSettings.DisplayMode = DisplayMode.IconView;
    }

    /// <summary>
    /// Opens the application settings window.
    /// </summary>
    private void OpenSettings(object sender, RoutedEventArgs e)
    {
        SettingsWindow settingsWindow = new();
        settingsWindow.ShowDialog(this);
    }

    private void SuppressHotKeys()
    {
        KeyBindings.Clear();
        foreach (Button button in _buttonHotKeys.Keys)
            button.HotKey = null;
    }

    /// <summary>
    /// Temporarily suppresses hotkeys when focus enters a text input.
    /// </summary>
    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
        if (e.Source is TextBox)
            SuppressHotKeys();
    }

    private void RestoreHotKeys()
    {
        KeyBindings.AddRange(_keyBindings);
        foreach ((Button button, KeyGesture gesture) in _buttonHotKeys)
            button.HotKey = gesture;
    }

    /// <summary>
    /// Restores hotkeys when focus leaves a text input.
    /// </summary>
    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        if (e.Source is TextBox)
            RestoreHotKeys();
    }

    private void TunnelKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e)
        {
            case { Key: Key.Enter }:
                OnEnterPressed(e);
                return;
            case { Key: Key.Escape }:
                OnEscapePressed(e);
                return;
            case { KeyModifiers: KeyModifiers.Control, Key: Key.F }:
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
                _viewModel?.SetNewLocationCommand.Execute(path).Subscribe();
            ClearFocus();
            return;
        }

        if (_viewModel is not null)
        {
            if (SearchBox.IsFocused && !string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                _viewModel?.SearchCommand.Execute(SearchBox.Text).Subscribe();
                ClearFocus();
            }
            else if (_viewModel.SelectedFiles.Count == 1)
                _viewModel.OpenEntryCommand.Execute(_viewModel.SelectedFiles[0]).Subscribe();
            else if (_viewModel.SelectedFiles.Count > 1)
                _viewModel.OpenEntriesCommand.Execute().Subscribe();
        }
    }

    private void OnEscapePressed(KeyEventArgs e)
    {
        e.Handled = true;
        _viewModel?.SelectedFiles.Clear();
        ClearFocus();
    }

    /// <summary>
    /// Disposes the view model and saves settings when the window is closing.
    /// </summary>
    private void OnClosing(object? sender, WindowClosingEventArgs? e)
    {
        _viewModel?.Dispose();
        FileSurferSettings.SaveSettings();
    }
}
