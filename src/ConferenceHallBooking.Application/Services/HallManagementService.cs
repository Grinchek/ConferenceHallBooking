using ConferenceHallBooking.Application.DTOs.Halls;
using ConferenceHallBooking.Application.Interfaces;
using ConferenceHallBooking.Application.Mapping;
using ConferenceHallBooking.Domain.Entities;
using ConferenceHallBooking.Domain.Exceptions;
using ConferenceHallBooking.Domain.Services;
using FluentValidation;

namespace ConferenceHallBooking.Application.Services;

public sealed class HallManagementService : IHallService
{
    private readonly IHallRepository _hallRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPricingCalculator _pricingCalculator;
    private readonly IValidator<CreateHallRequest> _createValidator;
    private readonly IValidator<UpdateHallRequest> _updateValidator;
    private readonly IValidator<SearchAvailableHallsRequest> _searchValidator;

    public HallManagementService(
        IHallRepository hallRepository,
        IUnitOfWork unitOfWork,
        IPricingCalculator pricingCalculator,
        IValidator<CreateHallRequest> createValidator,
        IValidator<UpdateHallRequest> updateValidator,
        IValidator<SearchAvailableHallsRequest> searchValidator)
    {
        _hallRepository = hallRepository;
        _unitOfWork = unitOfWork;
        _pricingCalculator = pricingCalculator;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _searchValidator = searchValidator;
    }

    public async Task<HallResponse> CreateAsync(CreateHallRequest request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        if (await _hallRepository.ExistsByNameAsync(request.Name, cancellationToken: cancellationToken))
            throw new ConflictException($"Зал із назвою '{request.Name}' уже існує.");

        var services = (request.Services ?? [])
            .Select(s => new HallServiceItem(s.Name, s.Price))
            .ToList();

        var hall = new Hall(
            request.Name,
            request.Capacity,
            request.BaseHourlyRate,
            services.Select(s => new HallService(s.Name, s.Price)));

        await _hallRepository.AddAsync(hall, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return DtoMapper.ToResponse(hall);
    }

    public async Task<HallResponse> UpdateAsync(Guid id, UpdateHallRequest request, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var hall = await _hallRepository.GetByIdWithDetailsAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Зал з ID '{id}' не знайдено.");

        if (await _hallRepository.ExistsByNameAsync(request.Name, id, cancellationToken))
            throw new ConflictException($"Зал із назвою '{request.Name}' уже існує.");

        hall.UpdateDetails(request.Name, request.Capacity, request.BaseHourlyRate);

        if (request.Services is not null)
        {
            hall.ReplaceServices(request.Services.Select(s => new HallService(s.Name, s.Price)));
        }

        await _hallRepository.UpdateAsync(hall, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return DtoMapper.ToResponse(hall);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var hall = await _hallRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Зал з ID '{id}' не знайдено.");

        hall.SoftDelete();
        await _hallRepository.UpdateAsync(hall, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<HallResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var hall = await _hallRepository.GetByIdWithDetailsAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Зал з ID '{id}' не знайдено.");

        return DtoMapper.ToResponse(hall);
    }

    public async Task<IReadOnlyList<HallResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var halls = await _hallRepository.GetAllAsync(cancellationToken);
        return halls.Select(DtoMapper.ToResponse).ToList();
    }

    public async Task<IReadOnlyList<AvailableHallResponse>> SearchAvailableAsync(
        SearchAvailableHallsRequest request,
        CancellationToken cancellationToken = default)
    {
        await _searchValidator.ValidateAndThrowAsync(request, cancellationToken);

        var halls = await _hallRepository.SearchAvailableAsync(
            request.Start,
            request.End,
            request.RequiredCapacity,
            cancellationToken);

        return halls
            .Select(hall =>
            {
                var pricing = _pricingCalculator.CalculateHallRental(hall.BaseHourlyRate, request.Start, request.End);
                return DtoMapper.ToAvailableResponse(hall, pricing.TotalHallCost);
            })
            .OrderBy(h => h.EstimatedHallRentalCost)
            .ToList();
    }

    private sealed record HallServiceItem(string Name, decimal Price);
}
