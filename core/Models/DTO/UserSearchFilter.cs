namespace OpenClient.Models.DTO.Users;

public enum UserStatusFilter
{
    All,
    Active,
    Inactive
}

// Búsqueda, filtros, orden y paginación del listado. Todo se traduce a SQL.
public sealed class UserSearchFilter
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;

    private int _page = 1;
    private int _pageSize = DefaultPageSize;

    // Coincidencia parcial sobre nombre, apellido, username y email.
    public string? Search { get; set; }

    public UserStatusFilter Status { get; set; } = UserStatusFilter.All;

    // Rol exacto; nulo/vacío = cualquiera.
    public string? Role { get; set; }

    // name | username | email | created | status
    public string SortBy { get; set; } = "created";

    // asc | desc
    public string SortDir { get; set; } = "desc";

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value is < 1 or > MaxPageSize ? DefaultPageSize : value;
    }
}