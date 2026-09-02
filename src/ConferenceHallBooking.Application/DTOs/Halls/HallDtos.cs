namespace ConferenceHallBooking.Application.DTOs.Halls;

public sealed record ServiceDto(string Name, decimal Price);

public sealed record HallResponse(
    Guid Id,
    string Name,
    int Capacity,
    decimal BaseHourlyRate,
    IReadOnlyList<ServiceDto> Services);

public sealed record CreateHallRequest(
    string Name,
    int Capacity,
    decimal BaseHourlyRate,
    IReadOnlyList<ServiceDto>? Services);

public sealed record UpdateHallRequest(
    string Name,
    int Capacity,
    decimal BaseHourlyRate,
    IReadOnlyList<ServiceDto>? Services);

public sealed record SearchAvailableHallsRequest(
    DateTimeOffset Start,
    DateTimeOffset End,
    int RequiredCapacity);

public sealed record AvailableHallResponse(
    Guid Id,
    string Name,
    int Capacity,
    decimal BaseHourlyRate,
    IReadOnlyList<ServiceDto> Services,
    decimal EstimatedHallRentalCost);
