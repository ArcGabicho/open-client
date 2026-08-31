using FluentValidation;
using OpenClient.Models.DTO;

namespace OpenClient.Models.Validators;

public sealed class ContactMessageValidator : AbstractValidator<ContactMessage>
{
    public ContactMessageValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(120).WithMessage("Nombre: máximo 120 caracteres.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo es obligatorio.")
            .EmailAddress().WithMessage("El correo no tiene un formato válido.")
            .MaximumLength(255).WithMessage("Correo: máximo 255 caracteres.");

        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("El asunto es obligatorio.")
            .MaximumLength(150).WithMessage("Asunto: máximo 150 caracteres.");

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("El mensaje es obligatorio.")
            .MinimumLength(10).WithMessage("El mensaje es demasiado corto.")
            .MaximumLength(4000).WithMessage("El mensaje es demasiado largo.");
    }
}