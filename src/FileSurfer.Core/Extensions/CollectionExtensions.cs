using System;
using System.Collections.Generic;

namespace FileSurfer.Core.Extensions;

public static class CollectionExtensions
{
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

    public static T[] ConvertToArray<T>(this IReadOnlyCollection<T> collection)
    {
        T[] array = new T[collection.Count];

        int i = 0;
        foreach (T item in collection)
            array[i++] = item;

        return array;
    }

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
