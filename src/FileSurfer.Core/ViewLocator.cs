using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using FileSurfer.Core.ViewModels;

namespace FileSurfer.Core;

/// <summary>
/// The ViewLocator class in Avalonia resolves and instantiates views for given view models by following a naming convention.
/// </summary>
public class ViewLocator : IDataTemplate
{
    /// <summary>
    /// Resolves and creates the appropriate view for a given view model based on naming conventions.
    /// <para>
    /// If the corresponding view is found, it sets its <see cref="Avalonia.StyledElement.DataContext"/> to the provided view model,
    /// otherwise it returns a fallback view with a <c>"Not Found"</c> message.
    /// </para>
    /// </summary>
    /// <param name="param">The view model instance for which to locate and build a corresponding view.</param>
    /// <returns>The constructed view with the view model set as its <see cref="Avalonia.StyledElement.DataContext"/>,
    /// or a <see cref="TextBlock"/> indicating the view was not found.</returns>
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        string name = param
            .GetType()
            .FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        Type? type = Type.GetType(name);

        if (type is not null)
        {
            Control control = (Control)Activator.CreateInstance(type)!;
            control.DataContext = param;
            return control;
        }
        return new TextBlock { Text = "Not Found: " + name };
    }

    /// <summary>
    /// Determines if the provided data object is of type MainWindowViewModel.
    /// </summary>
    /// <param name="data">The object to check.</param>
    /// <returns><see langword="true"/> if the object is a MainWindowViewModel, otherwise returns <see langword="false"/>.</returns>
    public bool Match(object? data)
    {
        return data is MainWindowViewModel;
    }
}
