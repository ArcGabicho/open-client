namespace OpenClient.Models.Api;

public static class ApiPaging
{
    public const int DefaultPage = 1;

    public const int DefaultPageSize = 25;

    public const int MaxPageSize = 100;

    public static bool TryValidate(
        int page,
        int pageSize,
        out ApiErrorResponse? error)
    {
        if (page < 1)
        {
            error = ApiErrorResponse.Create(
                "invalid_pagination",
                "The 'page' parameter must be greater than or equal to 1.");
            return false;
        }

        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            error = ApiErrorResponse.Create(
                "invalid_pagination",
                $"The 'pageSize' parameter must be between 1 and {MaxPageSize}.");
            return false;
        }

        error = null;
        return true;
    }
}