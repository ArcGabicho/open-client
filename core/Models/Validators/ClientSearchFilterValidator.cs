using FluentValidation;
using OpenClient.Models.DTO;

namespace OpenClient.Models.Validators;

/// <summary>
/// Comprobación defensiva de los parámetros de consulta que llegan por query
/// string al controlador REST. Los setters de <see cref="ClientSearchFilter"/>
/// ya autocorrigen valores fuera de rango; este validador los rechaza de forma
/// explícita antes de llegar al servicio.
/// </summary>
public sealed class ClientSearchFilterValidator : AbstractValidator<ClientSearchFilter>
{
    private static readonly int[] AllowedPageSizes = { 10, 25, 50, 100 };
    private static readonly string[] AllowedSorts = { "recent", "name", "oldest" };

    public ClientSearchFilterValidator()
    {
        RuleFor(filter => filter.Page)
            .GreaterThanOrEqualTo(1).WithMessage("La página debe ser mayor o igual a 1.");

        RuleFor(filter => filter.PageSize)
            .Must(size => AllowedPageSizes.Contains(size))
            .WithMessage("El tamaño de página debe ser 10, 25, 50 o 100.");

        RuleFor(filter => filter.SortBy)
            .Must(sort => AllowedSorts.Contains(sort))
            .WithMessage("El orden debe ser 'recent', 'name' u 'oldest'.");
    }
}
