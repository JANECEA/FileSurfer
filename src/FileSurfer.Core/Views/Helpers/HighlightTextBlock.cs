using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace FileSurfer.Core.Views.Helpers;

public class HighlightTextBlock : TextBlock
{
    public static readonly StyledProperty<string?> QueryProperty = AvaloniaProperty.Register<
        HighlightTextBlock,
        string?
    >(nameof(Query));

    public static readonly StyledProperty<IBrush?> HighlightBrushProperty =
        AvaloniaProperty.Register<HighlightTextBlock, IBrush?>(
            nameof(HighlightBrush),
            defaultValue: Brushes.Goldenrod
        );

    public string? Query
    {
        get => GetValue(QueryProperty);
        set => SetValue(QueryProperty, value);
    }

    public IBrush? HighlightBrush
    {
        get => GetValue(HighlightBrushProperty);
        set => SetValue(HighlightBrushProperty, value);
    }

    static HighlightTextBlock()
    {
        QueryProperty.Changed.AddClassHandler<HighlightTextBlock>((x, _) => x.UpdateInlines());
        TextProperty.Changed.AddClassHandler<HighlightTextBlock>((x, _) => x.UpdateInlines());
        HighlightBrushProperty.Changed.AddClassHandler<HighlightTextBlock>(
            (x, _) => x.UpdateHighlightBrush()
        );
    }

    private void UpdateHighlightBrush()
    {
        if (Inlines is null)
            return;

        foreach (Inline? inline in Inlines)
            if (inline is Run { Background: not null } run)
                run.Background = HighlightBrush;
    }

    private void UpdateInlines()
    {
        Inlines?.Clear();

        string fullText = Text ?? string.Empty;
        string query = Query ?? string.Empty;

        if (string.IsNullOrEmpty(query))
        {
            Inlines?.Add(new Run(fullText));
            return;
        }

        int index = 0;
        while (index < fullText.Length)
        {
            int matchIndex = fullText.IndexOf(query, index, StringComparison.OrdinalIgnoreCase);

            if (matchIndex < 0)
            {
                Inlines?.Add(new Run(fullText[index..]));
                break;
            }

            if (matchIndex > index)
                Inlines?.Add(new Run(fullText.Substring(index, matchIndex - index)));

            Inlines?.Add(
                new Run(fullText.Substring(matchIndex, query.Length))
                {
                    Background = HighlightBrush,
                }
            );

            index = matchIndex + query.Length;
        }
    }
}
