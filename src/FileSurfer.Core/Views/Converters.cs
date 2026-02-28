using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace FileSurfer.Core.Views;

public sealed record SortInfo(SortBy SortBy, bool SortReversed);

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
    ) => throw new NotImplementedException();
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
