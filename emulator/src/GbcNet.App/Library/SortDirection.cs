namespace GbcNet.App.Library;

internal enum SortDirection
{
    Ascending = 0,
    Descending = 1,
}

internal static class SortDirectionExtensions
{
    extension(SortDirection direction)
    {
        internal bool IsAscending() =>
            direction switch
            {
                SortDirection.Ascending => true,
                SortDirection.Descending => false,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(direction),
                    direction,
                    message: null
                ),
            };
    }
}
