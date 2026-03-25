using ServerDistorApi.Model;

namespace ServerDistorApi.Contracts;

public sealed record CreateServerRequest(string OS, int HardwareGB, int CoreCPU, bool IsPoweredOn = true);
public sealed record CreateRentalRequest(int ServerId, string ClientId);
public sealed record RentalReadinessResponse(int RentalId, int ServerId, bool IsReady, RentalStatus Status, DateTime ReadyAfterUtc);

public enum RentServerError
{
    None = 0,
    ServerNotFound = 1,
    Busy = 2,
    StateChanged = 3
}

public sealed record RentServerResult(RentServerError Error, Rental? Rental)
{
    public static RentServerResult Success(Rental rental) => new(RentServerError.None, rental);
}

public enum ReleaseRentalError
{
    None = 0,
    RentalNotFound = 1
}

public sealed record ReleaseRentalResult(ReleaseRentalError Error, bool AlreadyReleased);

