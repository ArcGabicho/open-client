using System.Linq.Expressions;
using FluentValidation;

namespace OpenClient.Models.Validators;

// Política de contraseñas del módulo de Usuarios. El proyecto no traía una
// política configurada (solo BCrypt como hasher), así que esta es la línea base:
// mínimo 8 caracteres, con al menos una letra y un dígito, y confirmación exacta.
public static class PasswordRules
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    public static void AddTo<T>(
        AbstractValidator<T> validator,
        Expression<Func<T, string>> password,
        Expression<Func<T, string>> confirmation)
    {
        validator.RuleFor(password)
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MinimumLength(MinLength)
                .WithMessage($"La contraseña debe tener al menos {MinLength} caracteres.")
            .MaximumLength(MaxLength)
                .WithMessage($"La contraseña no puede superar {MaxLength} caracteres.")
            .Matches("[A-Za-z]").WithMessage("La contraseña debe incluir al menos una letra.")
            .Matches("[0-9]").WithMessage("La contraseña debe incluir al menos un dígito.");

        validator.RuleFor(confirmation)
            .Equal(password).WithMessage("La confirmación no coincide con la contraseña.");
    }
}