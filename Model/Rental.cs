namespace ServerDistorApi.Model;

public class Rental
{
    public int Id { get; set; }
    public int ServerId { get; set; }
    public string ClientId { get; set; }
    public RentalStatus Status { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public DateTime ReadyAfterUtc { get; set; }
    public DateTime? RentedAtUtc { get; set; }
    public DateTime? AutoReleaseAtUtc { get; set; }
    public DateTime? ReleasedAtUtc { get; set; }

    public Rental()
    {
        ClientId = string.Empty;
    }
}

