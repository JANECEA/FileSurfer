using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace FileSurfer.Core.Views.Helpers;

/// <summary>
/// Provides utility operations for reordering and removing selected list box items.
/// </summary>
public static class ListBoxHelper
{
    /// <summary>
    /// Moves the currently selected item one position up in the target collection.
    /// </summary>
    public static void MoveUp<T>(ListBox listBox, ObservableCollection<T> collection)
    {
        int i = listBox.SelectedIndex;
        if (0 < i && i < collection.Count)
            (collection[i - 1], collection[i]) = (collection[i], collection[i - 1]);

        listBox.SelectedIndex = i - 1;
    }

    /// <summary>
    /// Moves the currently selected item one position down in the target collection.
    /// </summary>
    public static void MoveDown<T>(ListBox listBox, ObservableCollection<T> collection)
    {
        int i = listBox.SelectedIndex;
        if (0 <= i && i < collection.Count - 1)
            (collection[i], collection[i + 1]) = (collection[i + 1], collection[i]);

        listBox.SelectedIndex = i + 1;
    }

    /// <summary>
    /// Removes the currently selected item from the target collection.
    /// </summary>
    public static void Remove<T>(ListBox listBox, ObservableCollection<T> collection)
    {
        int i = listBox.SelectedIndex;
        if (i < 0 || i >= collection.Count)
            return;

        collection.RemoveAt(i);
    }
}
