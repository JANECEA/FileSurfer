using System;
using System.Globalization;
using Avalonia.Data.Converters;
using FileSurfer.Core.ViewModels;

namespace FileSurfer.Core.Views.Helpers;

/// <summary>
/// Base value converter for one-way bindings that disallow reverse conversion.
/// </summary>
public abstract class OneWayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        ConvertInternal(value, parameter);

    /// <summary>
    /// Converts a source value using an optional converter parameter.
    /// </summary>
    protected abstract object ConvertInternal(object? value, object? parameter);

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    ) => throw new InvalidOperationException();
}

/// <summary>
/// Converts active sort state to an up/down glyph for the matching sort column.
/// </summary>
public sealed class SortArrowConverter : OneWayConverter
{
    private const string UpArrow = "\U000F04BC";
    private const string DownArrow = "\U000F04BD";

    protected override object ConvertInternal(object? value, object? parameter)
    {
        if (
            value is not SortInfo sortInfo
            || parameter is not SortBy sortBy
            || sortInfo.SortBy != sortBy
        )
            return string.Empty;

        return sortInfo.SortReversed ? DownArrow : UpArrow;
    }
}

/// <summary>
/// Converts two inputs to a boolean indicating whether they reference the same object.
/// </summary>
public sealed class SameObjectConverter : OneWayConverter
{
    protected override object ConvertInternal(object? value, object? parameter) =>
        ReferenceEquals(value, parameter);
}

/// <summary>
/// Converts a numeric value to a boolean indicating whether it is non-zero.
/// </summary>
public sealed class IsNotEmptyConverter : OneWayConverter
{
    protected override object ConvertInternal(object? value, object? parameter) => value is not 0;
}
