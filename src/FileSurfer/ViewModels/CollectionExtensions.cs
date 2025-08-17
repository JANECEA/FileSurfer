using System;
using System.Collections.Generic;

namespace FileSurfer.ViewModels;

public static class CollectionExtensions
{
    public static TOut[] ConvertToArray<TIn, TOut>(
        this ICollection<TIn> collection,
        Func<TIn, TOut> action
    )
    {
        TOut[] array = new TOut[collection.Count];

        int i = 0;
        foreach (TIn item in collection)
            array[i++] = action(item);

        return array;
    }

    public static T[] ConvertToArray<T>(this ICollection<T> collection)
    {
        T[] array = new T[collection.Count];

        int i = 0;
        foreach (T item in collection)
            array[i++] = item;

        return array;
    }
}
