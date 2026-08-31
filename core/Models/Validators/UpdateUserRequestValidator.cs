using FluentValidation;
using OpenClient.Models.DTO.Users;

namespace OpenClient.Models.Validators;

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(50).WithMessage("Nombre: máximo 50 caracteres.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("El apellido es obligatorio.")
            .MaximumLength(50).WithMessage("Apellido: máximo 50 caracteres.");

        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("El nombre de usuario es obligatorio.")
            .MaximumLength(50).WithMessage("Nombre de usuario: máximo 50 caracteres.")
            .Matches("^[A-Za-z0-9._-]+$")
                .WithMessage("El nombre de usuario solo admite letras, números y . _ -");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El email es obligatorio.")
            .EmailAddress().WithMessage("El email no tiene un formato válido.")
            .MaximumLength(255).WithMessage("Email: máximo 255 caracteres.");

        RuleFor(x => x.Role)
            .Must(UserRoles.IsKnown).WithMessage("El rol indicado no está permitido.");
    }
}