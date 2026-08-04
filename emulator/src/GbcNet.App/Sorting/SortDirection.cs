namespace GbcNet.App.Sorting;

internal enum SortDirection
{
    Ascending = 0,
    Descending = 1,
}

internal static class SortDirectionExtensions
{
    internal static bool IsAscending(this SortDirection direction) =>
        direction switch
        {
            SortDirection.Ascending => true,
            SortDirection.Descending => false,
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, message: null),
        };
}
