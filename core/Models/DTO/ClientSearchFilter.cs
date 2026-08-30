namespace OpenClient.Models.DTO;

public sealed class ClientSearchFilter
{
    private int _page = 1;
    private int _pageSize = 10;
    public string? Search { get; set; }
    public string? Industry { get; set; }
    public string SortBy { get; set; } = "recent";
    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value is 10 or 25 or 50 or 100 ? value : 10;
    }
}