using FluentValidation;
using Remp.Service.DTOs.ListingCase;

namespace Remp.Service.Validators;

public class CreateCaseContactRequestValidator : AbstractValidator<CreateCaseContactRequestDto>
{
    public CreateCaseContactRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CompanyName).MaximumLength(200);
        RuleFor(x => x.Email).MaximumLength(256).EmailAddress()
            .When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.ProfileUrl).MaximumLength(1000).Must(url => string.IsNullOrEmpty(url) ||
            (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
             (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) &&
             !string.IsNullOrEmpty(uri.Host) && string.IsNullOrEmpty(uri.UserInfo)))
            .WithMessage("ProfileUrl must be an absolute HTTP(S) URL without credentials.");
        RuleFor(x => x.PhoneNumber).MaximumLength(32).Must(phone => string.IsNullOrEmpty(phone) ||
            (phone.Any(char.IsAsciiDigit) && phone.All(c => char.IsAsciiDigit(c) || "+-(). ".Contains(c))))
            .WithMessage("PhoneNumber must contain digits and only + - ( ) . or spaces.");
    }
}
