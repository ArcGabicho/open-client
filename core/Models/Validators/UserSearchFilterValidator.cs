using FluentValidation;
using OpenClient.Models.DTO.Users;

namespace OpenClient.Models.Validators;

public sealed class UserSearchFilterValidator : AbstractValidator<UserSearchFilter>
{
    private static readonly string[] AllowedSorts = ["name", "username", "email", "created", "status"];
    private static readonly string[] AllowedDirs = ["asc", "desc"];

    public UserSearchFilterValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("La página debe ser mayor o igual a 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, UserSearchFilter.MaxPageSize)
            .WithMessage($"El tamaño de página debe estar entre 1 y {UserSearchFilter.MaxPageSize}.");

        RuleFor(x => x.SortBy)
            .Must(s => AllowedSorts.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Orden no soportado.");

        RuleFor(x => x.SortDir)
            .Must(s => AllowedDirs.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Dirección de orden no soportada.");

        RuleFor(x => x.Role)
            .Must(UserRoles.IsKnown)
            .When(x => !string.IsNullOrWhiteSpace(x.Role))
            .WithMessage("El rol del filtro no está permitido.");

        RuleFor(x => x.Search)
            .MaximumLength(120).WithMessage("La búsqueda es demasiado larga.");
    }
}
