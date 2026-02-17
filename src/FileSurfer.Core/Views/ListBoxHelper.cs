using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace FileSurfer.Core.ViewModels;

public static class ListBoxHelper
{
    public static void MoveUp<T>(ListBox listBox, ObservableCollection<T> collection)
    {
        int i = listBox.SelectedIndex;
        if (0 < i && i < collection.Count)
            (collection[i - 1], collection[i]) = (collection[i], collection[i - 1]);

        listBox.SelectedIndex = i - 1;
    }

    public static void MoveDown<T>(ListBox listBox, ObservableCollection<T> collection)
    {
        int i = listBox.SelectedIndex;
        if (0 <= i && i < collection.Count - 1)
            (collection[i], collection[i + 1]) = (collection[i + 1], collection[i]);

        listBox.SelectedIndex = i + 1;
    }

    public static void Remove<T>(ListBox listBox, ObservableCollection<T> collection)
    {
        int i = listBox.SelectedIndex;
        if (i < 0 || i >= collection.Count)
            return;

        collection.RemoveAt(i);
    }
}
