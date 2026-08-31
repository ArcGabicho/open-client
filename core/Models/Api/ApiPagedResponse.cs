namespace OpenClient.Models.Api;

public sealed class ApiPagedResponse<T>
{
    public IReadOnlyList<T> Data { get; init; } = [];

    public ApiPaginationInfo Pagination { get; init; } = new();

    public static ApiPagedResponse<T> From(
        IReadOnlyList<T> data,
        int page,
        int pageSize,
        int totalItems) => new()
    {
        Data = data,
        Pagination = new ApiPaginationInfo
        {
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = pageSize <= 0
                ? 0
                : (int)Math.Ceiling(totalItems / (double)pageSize)
        }
    };
}

public sealed class ApiPaginationInfo
{
    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalItems { get; init; }

    public int TotalPages { get; init; }
}