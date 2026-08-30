using FluentValidation;
using OpenClient.Models.DTO;

namespace OpenClient.Models.Validators;

public sealed class ClientEditModelValidator : AbstractValidator<ClientEditModel>
{
    public ClientEditModelValidator()
    {
        RuleFor(model => model.CompanyName)
            .NotEmpty().WithMessage("La razón comercial es obligatoria.")
            .MaximumLength(100).WithMessage("Razón comercial: máximo 100 caracteres.");

        RuleFor(model => model.LegalName)
            .MaximumLength(100).WithMessage("Razón social: máximo 100 caracteres.")
            .When(model => !string.IsNullOrWhiteSpace(model.LegalName));

        RuleFor(model => model.Industry)
            .MaximumLength(200).WithMessage("Industria: máximo 200 caracteres.")
            .When(model => !string.IsNullOrWhiteSpace(model.Industry));

        RuleFor(model => model.TaxId)
            .Matches(@"^\d{11}$").WithMessage("El RUC debe tener 11 dígitos.")
            .When(model => !string.IsNullOrWhiteSpace(model.TaxId));

        RuleFor(model => model.Email)
            .EmailAddress().WithMessage("El correo no tiene un formato válido.")
            .MaximumLength(400).WithMessage("Correo: máximo 400 caracteres.")
            .When(model => !string.IsNullOrWhiteSpace(model.Email));

        RuleFor(model => model.PhoneNumber)
            .MaximumLength(20).WithMessage("Teléfono: máximo 20 caracteres.")
            .When(model => !string.IsNullOrWhiteSpace(model.PhoneNumber));

        RuleFor(model => model.Website)
            .Must(BeAValidUrl).WithMessage("El sitio web debe ser una URL absoluta (http/https).")
            .MaximumLength(500).WithMessage("Sitio web: máximo 500 caracteres.")
            .When(model => !string.IsNullOrWhiteSpace(model.Website));

        RuleFor(model => model.FirstName)
            .MaximumLength(50).When(model => !string.IsNullOrWhiteSpace(model.FirstName));

        RuleFor(model => model.LastName)
            .MaximumLength(50).When(model => !string.IsNullOrWhiteSpace(model.LastName));

        RuleFor(model => model.JobTitle)
            .MaximumLength(50).When(model => !string.IsNullOrWhiteSpace(model.JobTitle));

        RuleFor(model => model.Address)
            .MaximumLength(500).When(model => !string.IsNullOrWhiteSpace(model.Address));

        RuleFor(model => model.District)
            .MaximumLength(100).When(model => !string.IsNullOrWhiteSpace(model.District));

        RuleFor(model => model.Province)
            .MaximumLength(100).When(model => !string.IsNullOrWhiteSpace(model.Province));
    }

    private static bool BeAValidUrl(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            || (Uri.TryCreate(value, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));
    }
}
