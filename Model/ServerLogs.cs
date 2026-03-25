namespace ServerDistorApi.Model;

public class ServerLogs
{
    public int Id { get; set; }
    public int ServerId { get; set; }
    public int UserId { get; set; }
    public DateTime RentStart { get; set; }
    public DateTime RentEnd { get; set; }
    public DateTime WasAutoReleased { get; set; }

    public ServerLogs()
    {
    }

    public ServerLogs(int serverId, int userId, DateTime rentStart, DateTime rentEnd, DateTime wasAutoReleased)
    {
        ServerId = serverId;
        UserId = userId;
        RentStart = rentStart;
        RentEnd = rentEnd;
        WasAutoReleased = wasAutoReleased;
    }
}