using ConferenceHallBooking.Application.DTOs.Bookings;
using ConferenceHallBooking.Application.DTOs.Halls;
using ConferenceHallBooking.Application.DTOs.Reports;

namespace ConferenceHallBooking.Application.Interfaces;

public interface IHallService
{
    Task<HallResponse> CreateAsync(CreateHallRequest request, CancellationToken cancellationToken = default);
    Task<HallResponse> UpdateAsync(Guid id, UpdateHallRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<HallResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HallResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AvailableHallResponse>> SearchAvailableAsync(SearchAvailableHallsRequest request, CancellationToken cancellationToken = default);
}

public interface IBookingService
{
    Task<BookingResponse> CreateAsync(CreateBookingRequest request, CancellationToken cancellationToken = default);
    Task<BookingResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task CancelAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IReportService
{
    Task<AnalyticsSummaryDto> GetAnalyticsAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RevenueByHallDto>> GetRevenueByHallAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OccupancyReportDto>> GetOccupancyAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PopularServiceDto>> GetPopularServicesAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);
}
