using ServerDistorApi.Contracts;
using ServerDistorApi.Model;

namespace ServerDistorApi.Services;

public interface IServerServices
{
    Task<IReadOnlyList<Server>> GetServersAsync(CancellationToken ct);
    Task<Server> AddServerAsync(CreateServerRequest request, CancellationToken ct);
    Task<Server?> GetServerByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<Server>> SearchFreeServersAsync(string? os, int? minHardwareGb, int? minCoreCpu, CancellationToken ct);
    Task<RentServerResult> RentServerAsync(CreateRentalRequest request, CancellationToken ct);
    Task<RentalReadinessResponse?> GetReadinessAsync(int rentalId, CancellationToken ct);
    Task<ReleaseRentalResult> ReleaseRentalAsync(int rentalId, CancellationToken ct);
}

