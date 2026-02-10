using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace FileSurfer.Core.Views;

public sealed record SortInfo(SortBy SortBy, bool SortReversed);

public class SortArrowConverter : IValueConverter
{
    private const string UpArrow = "\U000F04BC";
    private const string DownArrow = "\U000F04BD";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (
            value is not SortInfo sortInfo
            || parameter is not SortBy sortBy
            || sortInfo.SortBy != sortBy
        )
            return string.Empty;

        return sortInfo.SortReversed ? DownArrow : UpArrow;
    }

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    ) => throw new NotImplementedException();
}

public class NotNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null;

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    ) => throw new NotImplementedException();
}
