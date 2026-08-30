namespace OpenClient.Models.DTO;

public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public int TotalPages =>
        PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public int FirstItemIndex =>
        TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;

    public int LastItemIndex =>
        Math.Min(Page * PageSize, TotalCount);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;
}