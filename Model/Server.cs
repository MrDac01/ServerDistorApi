namespace ServerDistorApi.Model;

public class Server
{
    public int Id { get; set; }
    public string OS { get; set; }
    public int HardwareGB { get; set; }
    public int CoreCPU { get; set; }
    public ServerStatus Status { get; set; }
    public DateTime? RentStartTime { get; set; }
    public DateTime? RentEndTime { get; set; }
    public DateTime? StartRequestTime { get; set; }
    public int RentByUserId { get; set; }

    public Server()
    {
        OS = string.Empty;
        Status = ServerStatus.Free;
        RentStartTime = DateTime.MinValue;
        RentEndTime = DateTime.MinValue;
        StartRequestTime = DateTime.MinValue;
    }

    public Server(string os, int hardwareGb, int coreCpu, int rentByUserId)
    {
        OS = os;
        HardwareGB = hardwareGb;
        CoreCPU = coreCpu;
        Status = ServerStatus.Free;
        RentStartTime = DateTime.MinValue;
        RentEndTime = DateTime.MinValue;
        StartRequestTime = DateTime.MinValue;
        RentByUserId = rentByUserId;
    }
}