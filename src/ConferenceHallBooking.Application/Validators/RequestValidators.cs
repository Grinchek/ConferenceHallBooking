using ConferenceHallBooking.Application.DTOs.Bookings;
using ConferenceHallBooking.Application.DTOs.Halls;
using FluentValidation;

namespace ConferenceHallBooking.Application.Validators;

public sealed class CreateHallRequestValidator : AbstractValidator<CreateHallRequest>
{
    public CreateHallRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Назва залу є обов'язковою.")
            .MaximumLength(100);

        RuleFor(x => x.Capacity)
            .GreaterThan(0).WithMessage("Місткість має бути більшою за 0.");

        RuleFor(x => x.BaseHourlyRate)
            .GreaterThanOrEqualTo(0).WithMessage("Базова вартість не може бути від'ємною.");

        RuleForEach(x => x.Services).ChildRules(service =>
        {
            service.RuleFor(s => s.Name).NotEmpty().MaximumLength(100);
            service.RuleFor(s => s.Price).GreaterThanOrEqualTo(0);
        }).When(x => x.Services is not null);
    }
}

public sealed class UpdateHallRequestValidator : AbstractValidator<UpdateHallRequest>
{
    public UpdateHallRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Назва залу є обов'язковою.")
            .MaximumLength(100);

        RuleFor(x => x.Capacity)
            .GreaterThan(0);

        RuleFor(x => x.BaseHourlyRate)
            .GreaterThanOrEqualTo(0);

        RuleForEach(x => x.Services).ChildRules(service =>
        {
            service.RuleFor(s => s.Name).NotEmpty().MaximumLength(100);
            service.RuleFor(s => s.Price).GreaterThanOrEqualTo(0);
        }).When(x => x.Services is not null);
    }
}

public sealed class SearchAvailableHallsRequestValidator : AbstractValidator<SearchAvailableHallsRequest>
{
    public SearchAvailableHallsRequestValidator()
    {
        RuleFor(x => x.RequiredCapacity)
            .GreaterThan(0).WithMessage("Потрібна місткість має бути більшою за 0.");

        RuleFor(x => x.End)
            .GreaterThan(x => x.Start).WithMessage("Час завершення має бути пізніше за час початку.");

        RuleFor(x => x)
            .Must(x => (x.End - x.Start).TotalHours <= 24)
            .WithMessage("Інтервал пошуку не може перевищувати 24 години.");
    }
}

public sealed class CreateBookingRequestValidator : AbstractValidator<CreateBookingRequest>
{
    public CreateBookingRequestValidator()
    {
        RuleFor(x => x.HallId)
            .NotEmpty();

        RuleFor(x => x.Start)
            .GreaterThanOrEqualTo(_ => DateTimeOffset.UtcNow)
            .WithMessage("Не можна бронювати на минулий час.");

        RuleFor(x => x.End)
            .GreaterThan(x => x.Start).WithMessage("Час завершення має бути пізніше за час початку.");

        RuleFor(x => x)
            .Must(x => (x.End - x.Start).TotalMinutes >= 30)
            .WithMessage("Мінімальна тривалість бронювання — 30 хвилин.")
            .Must(x => (x.End - x.Start).TotalHours <= 12)
            .WithMessage("Максимальна тривалість бронювання — 12 годин.");

        RuleFor(x => x.CustomerName)
            .MaximumLength(200)
            .When(x => x.CustomerName is not null);

        RuleForEach(x => x.SelectedServices)
            .NotEmpty()
            .When(x => x.SelectedServices is not null);
    }
}
