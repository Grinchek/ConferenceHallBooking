using ConferenceHallBooking.Application.DTOs.Bookings;
using ConferenceHallBooking.Application.Interfaces;
using ConferenceHallBooking.Application.Mapping;
using ConferenceHallBooking.Domain.Entities;
using ConferenceHallBooking.Domain.Exceptions;
using ConferenceHallBooking.Domain.Services;
using FluentValidation;

namespace ConferenceHallBooking.Application.Services;

public sealed class BookingService : IBookingService
{
    private readonly IHallRepository _hallRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IPricingCalculator _pricingCalculator;
    private readonly IValidator<CreateBookingRequest> _validator;

    public BookingService(
        IHallRepository hallRepository,
        IBookingRepository bookingRepository,
        IPricingCalculator pricingCalculator,
        IValidator<CreateBookingRequest> validator)
    {
        _hallRepository = hallRepository;
        _bookingRepository = bookingRepository;
        _pricingCalculator = pricingCalculator;
        _validator = validator;
    }

    public async Task<BookingResponse> CreateAsync(CreateBookingRequest request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var startUtc = request.Start.UtcDateTime;
        var endUtc = request.End.UtcDateTime;

        var hall = await _hallRepository.GetByIdWithDetailsAsync(request.HallId, cancellationToken)
            ?? throw new NotFoundException($"Зал з ID '{request.HallId}' не знайдено.");

        var selectedNames = (request.SelectedServices ?? [])
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var serviceItems = new List<BookingServiceItem>();
        foreach (var name in selectedNames)
        {
            var hallService = hall.Services
                .FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                ?? throw new BusinessRuleException(
                    $"Послуга '{name}' недоступна для залу '{hall.Name}'.");

            serviceItems.Add(new BookingServiceItem(hallService.Name, hallService.Price));
        }

        var pricing = _pricingCalculator.CalculateHallRental(hall.BaseHourlyRate, startUtc, endUtc);
        var durationHours = Math.Round((decimal)(endUtc - startUtc).TotalHours, 2, MidpointRounding.AwayFromZero);

        if (await _bookingRepository.HasOverlapAsync(request.HallId, startUtc, endUtc, cancellationToken))
            throw new ConflictException(
                $"Зал '{hall.Name}' уже заброньовано на період {startUtc:g} – {endUtc:g}.");

        var created = new Booking(
            hall.Id,
            hall.Name,
            startUtc,
            endUtc,
            durationHours,
            pricing.TotalHallCost,
            serviceItems,
            request.CustomerName);

        await _bookingRepository.AddAsync(created, cancellationToken);

        return DtoMapper.ToResponse(created, pricing);
    }

    public async Task<BookingResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Бронювання з ID '{id}' не знайдено.");

        return DtoMapper.ToResponse(booking);
    }

    public async Task CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Бронювання з ID '{id}' не знайдено.");

        if (booking.IsCancelled)
            throw new BusinessRuleException("Бронювання вже скасовано.");

        booking.Cancel();
        await _bookingRepository.UpdateAsync(booking, cancellationToken);
    }
}
