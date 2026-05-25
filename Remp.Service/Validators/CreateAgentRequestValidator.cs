using FluentValidation;
using Remp.Service.DTOs.User;

namespace Remp.Service.Validators;

public class CreateAgentRequestValidator : AbstractValidator<CreateAgentRequestDto>
{
    public CreateAgentRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email format is invalid.")
            .MaximumLength(256).WithMessage("Email cannot exceed 256 characters.");

        RuleFor(x => x.AgentFirstName)
            .NotEmpty().WithMessage("Agent first name is required.")
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("Agent first name is required.")
            .MaximumLength(100).WithMessage("Agent first name cannot exceed 100 characters.");

        RuleFor(x => x.AgentLastName)
            .NotEmpty().WithMessage("Agent last name is required.")
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("Agent last name is required.")
            .MaximumLength(100).WithMessage("Agent last name cannot exceed 100 characters.");

        RuleFor(x => x.AvatarUrl)
            .MaximumLength(1000).WithMessage("AvatarUrl cannot exceed 1000 characters.")
            .Must(url => string.IsNullOrWhiteSpace(url) || Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("AvatarUrl must be a valid absolute URL.");
    }
}
