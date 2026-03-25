namespace ServerDistorApi.Services;

public interface ISystemClock
{
    DateTime UtcNow { get; }
}

