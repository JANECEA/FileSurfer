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

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;
    private WrapPanel? _filePanel;
    private readonly DataTemplate _iconViewTemplate;
    private readonly DataTemplate _listViewTemplate;
    private readonly KeyBinding _selectAllKB;
    private readonly KeyGesture _deleteGesture = KeyGesture.Parse("Delete");
    private readonly KeyGesture _cutGesture = KeyGesture.Parse("Ctrl+X");
    private readonly KeyGesture _copyGesture = KeyGesture.Parse("Ctrl+C");
    private readonly KeyGesture _pasteGesture = KeyGesture.Parse("Ctrl+V");

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
            keyBinding is not null
            && keyBinding.Gesture.KeyModifiers == KeyModifiers.Control
            && keyBinding.Gesture.Key == Key.A
        );
    }

    private void ViewModelLoaded(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && _viewModel is null)
        {
            _viewModel = viewModel;
            SpecialFoldersLoaded(viewModel);
        }
    }

    private void SpecialFoldersLoaded(MainWindowViewModel viewModel)
    {
        if (viewModel.SpecialFolders.Length > 0)
        {
            SecondSeparator.IsVisible = true;
            SpecialsListBox.IsVisible = true;
        }
    }

    private void WrapPanelLoaded(object sender, RoutedEventArgs e)
    {
        if (FileSurferSettings.DisplayMode is DisplayModeEnum.IconView)
            IconView();
    }

    private WrapPanel? FindWrapPanel(Control parent)
    {
        if (parent == null)
            return null;

        foreach (var child in parent.GetVisualChildren())
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

    private void OnQuickAccessChanged(object sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_viewModel?.QuickAccess.Count > 0)
        {
            FirstSeparator.IsVisible = true;
            QuickAccessListBox.IsVisible = true;
        }
    }

    private void FilesDoubleTapped(object sender, TappedEventArgs e)
    {
        if (sender is ListBox listBox)
        {
            Visual? hitElement = (Visual?)listBox.InputHitTest(e.GetPosition(listBox));
            while (hitElement is not null)
            {
                if (hitElement is CheckBox)
                    return;

                hitElement = Avalonia.VisualTree.VisualExtensions.GetVisualParent(hitElement);
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
        if (sender is MenuItem item && item.DataContext is FileSystemEntry entry)
            _viewModel?.AddToQuickAccess(entry);
    }

    private void RemoveFromQuickAccess(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.DataContext is FileSystemEntry entry)
            _viewModel?.RemoveFromQuickAccess(entry);
    }

    private void AddToArchive(object sender, RoutedEventArgs e) => _viewModel?.AddToArchive();

    private void ExtractArchive(object sender, RoutedEventArgs e) => _viewModel?.ExtractArchive();

    private void CopyPath(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.DataContext is FileSystemEntry entry)
            _viewModel?.CopyPath(entry);
    }

    private void Cut(object sender, RoutedEventArgs e) => _viewModel?.Cut();

    private void Copy(object sender, RoutedEventArgs e) => _viewModel?.Copy();

    private void CreateShortcut(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.DataContext is FileSystemEntry entry)
            _viewModel?.CreateShortcut(entry);
    }

    private void Delete(object sender, RoutedEventArgs e) => _viewModel?.MoveToTrash();

    private void DeletePermanently(object sender, RoutedEventArgs e) => _viewModel?.Delete();

    private void ShowProperties(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.DataContext is FileSystemEntry entry)
            _viewModel?.ShowProperties(entry);
    }

    private void FilesTapped(object sender, TappedEventArgs e)
    {
        if (sender is not ListBox listBox)
            return;

        Visual? hitElement = (Visual?)listBox.InputHitTest(e.GetPosition(listBox));
        while (hitElement is not null)
        {
            if (hitElement is ListBoxItem)
                return;

            hitElement = Avalonia.VisualTree.VisualExtensions.GetVisualParent(hitElement);
        }
        _viewModel?.SelectedFiles.Clear();
        NewNameBar.IsVisible = false;
        CommitMessageBar.IsVisible = false;
    }

    private void SideBarEntryClicked(object sender, TappedEventArgs e)
    {
        if (sender is ListBox listBox && listBox.SelectedItem is FileSystemEntry entry)
        {
            _viewModel?.OpenEntry(entry);
            SpecialsListBox.SelectedItems?.Clear();
            QuickAccessListBox.SelectedItems?.Clear();
            DrivesListBox.SelectedItems?.Clear();
        }
    }

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
                if (hitElement is ListBoxItem item && item.DataContext is FileSystemEntry entry)
                {
                    this._viewModel?.OpenEntryLocation(entry);
                    return;
                }
                hitElement = Avalonia.VisualTree.VisualExtensions.GetVisualParent(hitElement);
            }
        }
    }

    private void TextBoxGotFocus(object? sender = null, GotFocusEventArgs? e = null)
    {
        DeleteButton.HotKey = null;
        CutButton.HotKey = null;
        CopyButton.HotKey = null;
        PasteButton.HotKey = null;
        KeyBindings.Remove(_selectAllKB);
    }

    private void TextBoxLostFocus(object? sender = null, RoutedEventArgs? e = null)
    {
        DeleteButton.HotKey = _deleteGesture;
        CutButton.HotKey = _cutGesture;
        CopyButton.HotKey = _copyGesture;
        PasteButton.HotKey = _pasteGesture;
        KeyBindings.Add(_selectAllKB);
    }

    private void SearchBoxLostFocus(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        TextBoxLostFocus();
    }

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

    private void OnCommitClicked(object sender, RoutedEventArgs e)
    {
        CommitMessageBar.IsVisible = true;
        CommitInputBox.Focus();
    }

    private void InputBoxLostFocus(object sender, RoutedEventArgs e)
    {
        NewNameBar.IsVisible = false;
        CommitMessageBar.IsVisible = false;
        TextBoxLostFocus();
    }

    private void NameEntered()
    {
        if (NameInputBox.Text is string newName)
        {
            _viewModel?.Rename(newName);
            NewNameBar.IsVisible = false;
        }
    }

    private void CommitMessageEntered()
    {
        if (CommitInputBox.Text is string commitMessage)
        {
            _viewModel?.Commit(commitMessage);
            CommitMessageBar.IsVisible = false;
            CommitInputBox.Text = string.Empty;
        }
    }

    private void StagedToggle(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.DataContext is FileSystemEntry entry)
        {
            if (checkBox.IsChecked is true)
                _viewModel?.StageFile(entry);
            else
                _viewModel?.UnstageFile(entry);
        }
    }

    private void ListView(object? sender = null, RoutedEventArgs? e = null)
    {
        WrapPanel? wrapPanel = _filePanel ?? FindWrapPanel(FileDisplay);
        if (wrapPanel is null)
            return;

        wrapPanel.Orientation = Avalonia.Layout.Orientation.Vertical;
        LabelsPanel.IsVisible = true;
        FileDisplay.ItemTemplate = _listViewTemplate;
        FileSurferSettings.DisplayMode = DisplayModeEnum.ListView;
    }

    private void IconView(object? sender = null, RoutedEventArgs? e = null)
    {
        WrapPanel? wrapPanel = _filePanel ?? FindWrapPanel(FileDisplay);
        if (wrapPanel is null)
            return;

        wrapPanel.Orientation = Avalonia.Layout.Orientation.Horizontal;
        LabelsPanel.IsVisible = false;
        FileDisplay.ItemTemplate = _iconViewTemplate;
        FileSurferSettings.DisplayMode = DisplayModeEnum.IconView;
    }

    private void KeyPressed(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            OnEnterPressed(e);

        if (e.Key == Key.Escape)
            OnEscapePressed(e);

        if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.Control)
            OnCtrlFPressed(e);
    }

    private void OnCtrlFPressed(KeyEventArgs e)
    {
        e.Handled = true;
        if (SearchBox.IsFocused)
            FocusManager?.ClearFocus();
        else
            SearchBox.Focus();
    }

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
                _viewModel?.SearchRelay(SearchBox.Text);
                return;
            }
            if (_viewModel.SelectedFiles.Count == 1)
                _viewModel?.OpenEntry(_viewModel.SelectedFiles[0]);
        }
    }

    private void OnEscapePressed(KeyEventArgs e)
    {
        e.Handled = true;
        _viewModel?.SelectedFiles.Clear();
        if (SearchBox.IsFocused)
            _viewModel?.CancelSearch();

        FocusManager?.ClearFocus();
    }

    private void OnClosing(object sender, WindowClosingEventArgs e)
    {
        if (_viewModel is not null)
            FileSurferSettings.UpdateQuickAccess(_viewModel.QuickAccess);
        FileSurferSettings.SaveSettings();
    }
}
