using System.Text;
using Microsoft.AspNetCore.Identity;

namespace CityQuest.DataAccess.Identity;

public sealed class LetterPasswordValidator : IPasswordValidator<ApplicationUser>
{
    public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
    {
        var hasLetter = password is not null && password.EnumerateRunes().Any(Rune.IsLetter);
        return Task.FromResult(hasLetter ? IdentityResult.Success : IdentityResult.Failed(
            new IdentityError { Code = "PasswordRequiresLetter", Description = "Додай до пароля хоча б одну літеру." }));
    }
}
