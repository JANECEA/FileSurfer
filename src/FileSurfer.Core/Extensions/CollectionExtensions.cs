using System;
using System.Collections.Generic;

namespace FileSurfer.Core.Extensions;

/// <summary>
/// Provides helper extension methods for working with collections.
/// </summary>
public static class CollectionExtensions
{
    /// <summary>
    /// Converts a read-only collection into an array by projecting each item.
    /// </summary>
    /// <param name="collection">The source collection.</param>
    /// <param name="action">The projection function applied to each source item.</param>
    /// <returns>An array containing projected items in source order.</returns>
    public static TOut[] ConvertToArray<TIn, TOut>(
        this IReadOnlyCollection<TIn> collection,
        Func<TIn, TOut> action
    )
    {
        TOut[] array = new TOut[collection.Count];

        int i = 0;
        foreach (TIn item in collection)
            array[i++] = action(item);

        return array;
    }

    /// <summary>
    /// Copies a read-only collection into a new array.
    /// </summary>
    /// <param name="collection">The source collection.</param>
    /// <returns>An array containing source items in source order.</returns>
    public static T[] ConvertToArray<T>(this IReadOnlyCollection<T> collection)
    {
        T[] array = new T[collection.Count];

        int i = 0;
        foreach (T item in collection)
            array[i++] = item;

        return array;
    }

    /// <summary>
    /// Enumerates chunk sizes while filling a reusable buffer from a source sequence.
    /// </summary>
    /// <param name="source">The source sequence to chunk.</param>
    /// <param name="buffer">The reusable destination buffer.</param>
    /// <returns>A sequence of chunk lengths written into <paramref name="buffer"/>.</returns>
    public static IEnumerable<int> EfficientChunk<T>(this IEnumerable<T> source, T[] buffer)
    {
        int index = 0;
        foreach (T item in source)
        {
            buffer[index++] = item;

            if (index == buffer.Length)
            {
                yield return index;
                index = 0;
            }
        }
        if (index != 0)
            yield return index;
    }

    /// <summary>
    /// Determines whether two collections contain the same items with the same multiplicities, regardless of order.
    /// </summary>
    /// <param name="collectionA">The first collection.</param>
    /// <param name="collectionB">The second collection.</param>
    /// <returns><see langword="true"/> when both collections are equivalent as multisets; otherwise, <see langword="false"/>.</returns>
    public static bool EqualsUnordered<T>(
        this IReadOnlyCollection<T> collectionA,
        IReadOnlyCollection<T> collectionB
    )
        where T : IComparable<T>
    {
        if (collectionA.Count != collectionB.Count)
            return false;

        if (collectionA.Count == 0)
            return true;

        Dictionary<T, int> counts = new();
        foreach (T item in collectionA)
            if (counts.TryGetValue(item, out int count))
                counts[item] = count + 1;
            else
                counts[item] = 1;

        foreach (T item in collectionB)
        {
            if (!counts.TryGetValue(item, out int count))
                return false;

            if (count == 1)
                counts.Remove(item);
            else
                counts[item] = count - 1;
        }
        return counts.Count == 0;
    }
}
