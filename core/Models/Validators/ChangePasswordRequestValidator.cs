using FluentValidation;
using OpenClient.Models.DTO.Users;

namespace OpenClient.Models.Validators;

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        PasswordRules.AddTo(this, x => x.NewPassword, x => x.ConfirmPassword);
    }
}