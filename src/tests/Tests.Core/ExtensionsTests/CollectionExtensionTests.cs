using FileSurfer.Core.Extensions;

namespace Tests.Core.ExtensionsTests;

public class CollectionExtensionTests
{
    public static TheoryData<int[], int[]> ConvertToArrayData =>
        new()
        {
            { [], [] },
            { [1], [1] },
            { [1, 2, 3], [1, 2, 3] },
            { [-5, 0, 5], [-5, 0, 5] },
        };

    [Theory]
    [MemberData(nameof(ConvertToArrayData))]
    public void ConvertToArray_Copies_Source_Items(int[] source, int[] expected)
    {
        int[] result = source.ConvertToArray();
        Assert.Equal(expected, result);
        Assert.NotSame(source, result);
    }

    public static TheoryData<int[], int[]> ConvertToArrayWithProjectionData =>
        new()
        {
            { [], [] },
            { [1], [2] },
            { [1, 2, 3], [2, 4, 6] },
            { [-2, 0, 2], [-4, 0, 4] },
        };

    [Theory]
    [MemberData(nameof(ConvertToArrayWithProjectionData))]
    public void ConvertToArray_WithProjection_Projects_InOrder(int[] source, int[] expected)
    {
        int[] result = source.ConvertToArray(static n => n * 2);
        Assert.Equal(expected, result);
    }

    public static TheoryData<int[], int, int[]> EfficientChunkCountData =>
        new()
        {
            { [], 3, [] },
            { [1], 3, [1] },
            { [1, 2, 3], 3, [3] },
            { [1, 2, 3, 4], 3, [3, 1] },
            { [1, 2, 3, 4, 5, 6], 2, [2, 2, 2] },
            { [1, 2, 3, 4, 5], 10, [5] },
        };

    [Theory]
    [MemberData(nameof(EfficientChunkCountData))]
    public void EfficientChunk_Yields_Correct_Chunk_Sizes(
        int[] source,
        int bufferSize,
        int[] expectedChunkSizes
    )
    {
        int[] buffer = new int[bufferSize];
        int[] chunkSizes = source.EfficientChunk(buffer).ToArray();
        Assert.Equal(expectedChunkSizes, chunkSizes);
    }

    [Fact]
    public void EfficientChunk_Fills_Buffer_With_Chunk_Content()
    {
        int[] source = [1, 2, 3, 4, 5];
        int[] buffer = new int[2];
        List<int[]> observedChunks = [];
        foreach (int chunkSize in source.EfficientChunk(buffer))
            observedChunks.Add(buffer[..chunkSize].ToArray());
        Assert.Equal(3, observedChunks.Count);
        Assert.Equal([1, 2], observedChunks[0]);
        Assert.Equal([3, 4], observedChunks[1]);
        Assert.Equal([5], observedChunks[2]);
    }

    public static TheoryData<int[], int[], bool> EqualsUnorderedData =>
        new()
        {
            { [], [], true },
            { [1], [1], true },
            { [1, 2, 3], [3, 2, 1], true },
            { [1, 1, 2], [2, 1, 1], true },
            { [1, 1, 2], [1, 2, 2], false },
            { [1, 2], [1, 2, 3], false },
            { [1, 2, 3], [1, 2, 4], false },
        };

    [Theory]
    [MemberData(nameof(EqualsUnorderedData))]
    public void EqualsUnordered_Compares_Collections_As_Multisets(
        int[] left,
        int[] right,
        bool expected
    )
    {
        bool result = left.EqualsUnordered(right);
        Assert.Equal(expected, result);
    }
}
