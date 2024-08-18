using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.VisualTree;
using FileSurfer.ViewModels;
using System;

namespace FileSurfer.Views;

public partial class MainWindow : Window
{
    private WrapPanel? _filePanel;
    private readonly DataTemplate _iconViewTemplate;
    private readonly DataTemplate _listViewTemplate;
    private readonly KeyGesture _deleteGesture = KeyGesture.Parse("Delete");

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

    private void FilesDoubleTapped(object sender, TappedEventArgs e)
    {
        if (sender is ListBox listBox && DataContext is MainWindowViewModel viewModel)
        {
            if (listBox.SelectedItem is FileSystemEntry entry)
                viewModel.OpenEntry(entry);
            else
                viewModel.GoUp();
        }
    }

    private void FilesTapped(object sender, TappedEventArgs e)
    {
        if (sender is not ListBox listBox || DataContext is not MainWindowViewModel viewModel)
            return;

        Visual? hitElement = (Visual?)listBox.InputHitTest(e.GetPosition(listBox));
        while (hitElement is not null)
        {
            if (hitElement is ListBoxItem)
                return;

            hitElement = Avalonia.VisualTree.VisualExtensions.GetVisualParent(hitElement);
        }
        viewModel.SelectedFiles.Clear();
        NewNameBar.IsVisible = false;
        CommitMessageBar.IsVisible = false;
    }

    private void MouseButtonPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        PointerPointProperties properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsXButton1Pressed)
            viewModel.GoBack();

        if (properties.IsXButton2Pressed)
            viewModel.GoForward();
    }

    private void SearchBoxLostFocus(object sender,  RoutedEventArgs e) =>
        SearchBox.Text = string.Empty;

    private void OnRenameClicked(object sender, RoutedEventArgs e)
    {
        if (
            DataContext is not MainWindowViewModel viewModel 
            || viewModel.SelectedFiles.Count < 1
        )
            return;

        DeleteButton.HotKey = null;
        NewNameBar.IsVisible = true;
        NameInputBox.Focus();
        NameInputBox.Text = viewModel.SelectedFiles[^1].Name;
        NameInputBox.SelectionStart = 0;
        NameInputBox.SelectionEnd = viewModel.GetNameEndIndex(viewModel.SelectedFiles[^1]);
    }

    private void OnCommitClicked(object sender, RoutedEventArgs e)
    {
        DeleteButton.HotKey = null;
        CommitMessageBar.IsVisible = true;
        CommitInputBox.Focus();
    }

    private void InputBoxLostFocus(object sender, RoutedEventArgs e)
    {
        NewNameBar.IsVisible = false;
        CommitMessageBar.IsVisible = false;
        DeleteButton.HotKey = _deleteGesture;
    }

    private void NameEntered()
    {
        if (
            DataContext is MainWindowViewModel viewModel
            && NameInputBox.Text is string newName
        )
        {
            DeleteButton.HotKey = KeyGesture.Parse("Delete");
            viewModel.Rename(newName);
            NewNameBar.IsVisible = false;
        }
    }

    private void CommitMessageEntered()
    {
        if (
            DataContext is MainWindowViewModel viewModel
            && CommitInputBox.Text is string commitMessage
        )
        {
            viewModel.Commit(commitMessage);
            CommitMessageBar.IsVisible = false;
        }
    }

    private void ListView(object sender, RoutedEventArgs e)
    {
        WrapPanel? wrapPanel = _filePanel ?? FindWrapPanel(FileDisplay);
        if (wrapPanel is null)
            return;

        wrapPanel.Orientation = Avalonia.Layout.Orientation.Vertical;
        LabelsPanel.IsVisible = true;
        FileDisplay.ItemTemplate = _listViewTemplate;
    }

    private void IconView(object sender, RoutedEventArgs e)
    {
        WrapPanel? wrapPanel = _filePanel ?? FindWrapPanel(FileDisplay);
        if (wrapPanel is null)
            return;

        wrapPanel.Orientation = Avalonia.Layout.Orientation.Horizontal;
        LabelsPanel.IsVisible = false;
        FileDisplay.ItemTemplate = _iconViewTemplate;
    }

    private void KeyPressed(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            OnEnterPressed(e);

        if (e.Key == Key.Escape)
            OnEscapePressed(e);
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

            DeleteButton.HotKey = _deleteGesture;
            return;
        }

        if (DataContext is MainWindowViewModel viewModel)
        {
            if (viewModel.SelectedFiles.Count == 1)
                viewModel.OpenEntry(viewModel.SelectedFiles[0]);

            if (SearchBox.IsFocused && !string.IsNullOrWhiteSpace(SearchBox.Text))
                viewModel.SearchRelay(SearchBox.Text);
        }
    }

    private void OnEscapePressed(KeyEventArgs e)
    {
        e.Handled = true;
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SelectedFiles.Clear();

            if (SearchBox.IsFocused)
                viewModel.CancelSearch();
        }
        FocusManager?.ClearFocus();
    }
}
