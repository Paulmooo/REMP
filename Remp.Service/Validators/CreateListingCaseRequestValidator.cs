using System;
using FluentValidation;
using Remp.Service.DTOs;
using Remp.Service.DTOs.ListingCase;

namespace Remp.Service.Validators;

public class CreateListingCaseRequestValidator : AbstractValidator<CreateListingCaseRequestDto>
{
    public CreateListingCaseRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .Must(title => !string.IsNullOrWhiteSpace(title)).WithMessage("Title is required.")
            .MaximumLength(255).WithMessage("Title cannot exceed 255 characters.");

        RuleFor(x => x.Street)
            .NotEmpty().WithMessage("Street is required.");

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("City is required.");

        RuleFor(x => x.State)
            .NotEmpty().WithMessage("State is required.");

        RuleFor(x => x.PostCode)
            .GreaterThan(0).WithMessage("PostCode must be a positive integer.");
        
        RuleFor(x => x.Bedrooms)
            .GreaterThanOrEqualTo(0).WithMessage("Bedrooms cannot be negative.");
        
        RuleFor(x => x.Bathrooms)
            .GreaterThanOrEqualTo(0).WithMessage("Bathrooms cannot be negative.");

        RuleFor(x => x.Garages)
            .GreaterThanOrEqualTo(0).WithMessage("Garages cannot be negative.");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Price must be a positive number.");
        
        RuleFor(x => x.FloorArea)
            .GreaterThanOrEqualTo(0).WithMessage("FloorArea must be a positive number.");

        RuleFor(x => x.PropertyType)
            .IsInEnum().WithMessage("Invalid PropertyType value.");

        RuleFor(x => x.SaleCategory)
            .IsInEnum().WithMessage("Invalid SaleCategory value.");

    }
}
