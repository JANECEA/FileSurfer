using System;
using System.Globalization;
using Avalonia.Data.Converters;
using FileSurfer.Core.ViewModels;

namespace FileSurfer.Core.Views.Helpers;

public abstract class OneWayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        ConvertInternal(value, parameter);

    protected abstract object ConvertInternal(object? value, object? parameter);

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    ) => throw new InvalidOperationException();
}

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

public sealed class SameObjectConverter : OneWayConverter
{
    protected override object ConvertInternal(object? value, object? parameter) =>
        ReferenceEquals(value, parameter);
}

public sealed class IsNotEmptyConverter : OneWayConverter
{
    protected override object ConvertInternal(object? value, object? parameter) => value is not 0;
}
